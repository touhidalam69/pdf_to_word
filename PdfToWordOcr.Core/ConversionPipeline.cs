using System.Diagnostics;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Core;

public sealed class ConversionPipeline
{
    private readonly OcrClient _ocrClient;

    public ConversionPipeline(OcrClient ocrClient)
    {
        _ocrClient = ocrClient;
    }

    public async Task<ConversionResult> RunAsync(
        ConversionOptions opt,
        IProgress<PageProgress> progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var pdfBytes = await File.ReadAllBytesAsync(opt.InputPath, ct).ConfigureAwait(false);
        var totalPages = PdfRasterizer.GetPageCount(pdfBytes);

        var pageTexts = new List<string?>(totalPages);
        var failedPages = new List<int>();

        var pageNumber = 0;
        await foreach (var pageImage in PdfRasterizer.RasterizeAsync(pdfBytes, opt.Dpi, ct).WithCancellation(ct))
        {
            pageNumber++;

            string? text;
            try
            {
                text = await _ocrClient
                    .TranscribePageAsync(pageImage, opt.Model, opt.Language, pageNumber, ct)
                    .ConfigureAwait(false);
            }
            catch (OcrPageException) when (opt.ContinueOnPageFailure)
            {
                failedPages.Add(pageNumber);
                text = null;
            }

            pageTexts.Add(text);
            progress.Report(new PageProgress(pageNumber, totalPages, $"Page {pageNumber}/{totalPages}"));

            ct.ThrowIfCancellationRequested();
        }

        DocxWriter.Write(pageTexts, opt.Font, opt.Language, opt.OutputPath);

        stopwatch.Stop();
        return new ConversionResult(opt.OutputPath, totalPages, failedPages.ToArray(), stopwatch.Elapsed);
    }
}
