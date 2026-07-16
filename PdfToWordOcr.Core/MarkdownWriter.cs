using System.Text;

namespace PdfToWordOcr.Core;

/// <summary>
/// Stitches per-page OCR text files into one Markdown document, streaming a
/// page at a time — the book is never assembled in memory.
/// </summary>
public static class MarkdownWriter
{
    public static void Write(JobWorkspace workspace, int totalPages, string outputPath)
    {
        var missing = workspace.GetPendingPages(totalPages);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot write output: page(s) {string.Join(", ", missing)} have no OCR text yet. Run Retry first.");
        }

        using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(false));

        for (var pageNumber = 1; pageNumber <= totalPages; pageNumber++)
        {
            var text = workspace.ReadOcrText(pageNumber);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            writer.Write($"\n<!-- page {pageNumber} -->\n\n");
            writer.Write(text);
            if (!text.EndsWith('\n'))
            {
                writer.Write('\n');
            }
        }
    }
}
