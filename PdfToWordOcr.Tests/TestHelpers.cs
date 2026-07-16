using System.Net;
using System.Text;
using System.Text.Json;

namespace PdfToWordOcr.Tests;

/// <summary>Self-deleting temp directory for filesystem-backed tests.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PdfToWordOcrTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort — leftover temp dirs are harmless.
        }
    }
}

public static class MinimalPdf
{
    /// <summary>
    /// Builds a tiny valid PDF with the given number of blank 100×100pt pages —
    /// enough for PDFium to parse and rasterize without any external fixture.
    /// </summary>
    public static byte[] Create(int pageCount)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{i + 3} 0 R"))}] /Count {pageCount} >>",
        };
        objects.AddRange(Enumerable.Repeat("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>", pageCount));

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefStart = sb.Length;
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}

/// <summary>
/// In-memory stand-in for the Message Batches API: accepts submissions,
/// reports them ended on the first poll, and serves JSONL results. Lets the
/// pipeline run end-to-end with zero network and zero spend.
/// </summary>
public sealed class FakeBatchServer : HttpMessageHandler
{
    private readonly Dictionary<string, List<string>> _batches = [];
    private int _nextBatchId;

    public int PostCount { get; private set; }

    /// <summary>When true, any submission attempt fails the test's assertion path with a 500.</summary>
    public bool RejectSubmissions { get; set; }

    /// <summary>Batch ids that respond 404 (expired/unknown) on status polls.</summary>
    public HashSet<string> GoneBatchIds { get; } = [];

    /// <summary>Result JSONL line per custom_id; defaults to a succeeded transcription.</summary>
    public Func<string, string> ResultLineFactory { get; set; } = customId =>
        SucceededLine(customId, $"Text of {customId}.");

    public HttpClient CreateClient() => new(this) { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Registers a batch as if a previous session had submitted it.</summary>
    public void SeedBatch(string batchId, IEnumerable<int> pageNumbers) =>
        _batches[batchId] = pageNumbers.Select(PdfToWordOcr.Core.JobWorkspace.PageStem).ToList();

    public static string SucceededLine(string customId, string text) =>
        JsonSerializer.Serialize(new
        {
            custom_id = customId,
            result = new
            {
                type = "succeeded",
                message = new
                {
                    content = new[] { new { type = "text", text } },
                    stop_reason = "end_turn",
                },
            },
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Post && path.EndsWith("/v1/messages", StringComparison.Ordinal))
        {
            // Synchronous retry endpoint: answer with a refusal so retried pages
            // fail fast and deterministically in permanent-failure scenarios.
            return Json(new
            {
                content = new[] { new { type = "text", text = "I can't transcribe this." } },
                stop_reason = "end_turn",
            });
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/messages/batches", StringComparison.Ordinal))
        {
            PostCount++;
            if (RejectSubmissions)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var body = await request.Content!.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            var customIds = json.RootElement.GetProperty("requests").EnumerateArray()
                .Select(r => r.GetProperty("custom_id").GetString()!)
                .ToList();

            var batchId = $"batch_{++_nextBatchId}";
            _batches[batchId] = customIds;
            return Json(new { id = batchId, processing_status = "in_progress" });
        }

        if (request.Method == HttpMethod.Get && path.Contains("/messages/batches/", StringComparison.Ordinal))
        {
            var batchId = path[(path.LastIndexOf('/') + 1)..];

            if (path.EndsWith("/results", StringComparison.Ordinal))
            {
                batchId = path.Split('/')[^2];
                return ResultsResponse(batchId);
            }

            if (GoneBatchIds.Contains(batchId) || !_batches.ContainsKey(batchId))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Json(new
            {
                id = batchId,
                processing_status = "ended",
                request_counts = new { processing = 0, succeeded = _batches[batchId].Count, errored = 0, canceled = 0, expired = 0 },
                results_url = $"https://api.anthropic.com/v1/messages/batches/{batchId}/results",
            });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage ResultsResponse(string batchId)
    {
        if (GoneBatchIds.Contains(batchId) || !_batches.TryGetValue(batchId, out var customIds))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var jsonl = string.Join("\n", customIds.Select(id => ResultLineFactory(id)));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonl, Encoding.UTF8, "application/x-jsonl"),
        };
    }

    private static HttpResponseMessage Json(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
}

public sealed class NullProgress : IProgress<PdfToWordOcr.Core.Models.PageProgress>
{
    public void Report(PdfToWordOcr.Core.Models.PageProgress value)
    {
    }
}
