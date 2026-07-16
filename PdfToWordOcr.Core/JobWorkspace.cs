using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Core;

/// <summary>
/// The on-disk work directory for one conversion job ("{input}.pdf.work\").
/// A page's OCR output file existing is the single source of truth for
/// "this page is done" — an empty file means a blank page, a missing file
/// means the page still needs OCR. failures.txt is informational only.
/// </summary>
public sealed class JobWorkspace
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JobWorkspace(string inputPdfPath)
    {
        Root = inputPdfPath + ".work";
        PagesDirectory = Path.Combine(Root, "pages");
        OcrDirectory = Path.Combine(Root, "ocr");
        ManifestPath = Path.Combine(Root, "job.json");
        FailuresPath = Path.Combine(Root, "failures.txt");
        LogPath = Path.Combine(Root, "log.txt");
    }

    public string Root { get; }
    public string PagesDirectory { get; }
    public string OcrDirectory { get; }
    public string ManifestPath { get; }
    public string FailuresPath { get; }
    public string LogPath { get; }

    /// <summary>1-based, 4-digit zero-padded stem — also the batch custom_id and stitch key.</summary>
    public static string PageStem(int pageNumber) => $"page_{pageNumber:D4}";

    public static int? TryParsePageNumber(string customId) =>
        customId.StartsWith("page_", StringComparison.Ordinal)
            && int.TryParse(customId.AsSpan(5), out var page)
            && page > 0
            ? page
            : null;

    public string PageImagePath(int pageNumber) => Path.Combine(PagesDirectory, PageStem(pageNumber) + ".jpg");

    public string OcrTextPath(int pageNumber) => Path.Combine(OcrDirectory, PageStem(pageNumber) + ".txt");

    public bool HasPageImage(int pageNumber) => File.Exists(PageImagePath(pageNumber));

    public bool HasOcrText(int pageNumber) => File.Exists(OcrTextPath(pageNumber));

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(PagesDirectory);
        Directory.CreateDirectory(OcrDirectory);
    }

    /// <summary>Pages in 1..totalPages that still have no OCR output file.</summary>
    public IReadOnlyList<int> GetPendingPages(int totalPages) =>
        Enumerable.Range(1, totalPages).Where(page => !HasOcrText(page)).ToArray();

    public string ReadOcrText(int pageNumber) => File.ReadAllText(OcrTextPath(pageNumber), Utf8NoBom);

    public void WriteOcrText(int pageNumber, string text) =>
        File.WriteAllText(OcrTextPath(pageNumber), text, Utf8NoBom);

    public JobManifest? LoadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JobManifest>(File.ReadAllText(ManifestPath, Utf8NoBom), ManifestJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes job.json and flushes it through OS buffers to disk.</summary>
    public void SaveManifest(JobManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
        using var stream = new FileStream(ManifestPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        stream.Write(Utf8NoBom.GetBytes(json));
        stream.Flush(flushToDisk: true);
    }

    /// <summary>Append-only, informational: "custom_id&lt;TAB&gt;reason".</summary>
    public void AppendFailure(string customId, string reason) =>
        File.AppendAllText(FailuresPath, $"{customId}\t{reason}{Environment.NewLine}", Utf8NoBom);

    public void Log(string message) =>
        File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}", Utf8NoBom);

    /// <summary>True when a previous run left anything worth resuming.</summary>
    public bool HasAnyProgress() =>
        File.Exists(ManifestPath)
        || (Directory.Exists(OcrDirectory) && Directory.EnumerateFiles(OcrDirectory, "page_*.txt").Any())
        || (Directory.Exists(PagesDirectory) && Directory.EnumerateFiles(PagesDirectory, "page_*.jpg").Any());

    /// <summary>Deletes the entire work directory ("Reset job").</summary>
    public void Delete()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
