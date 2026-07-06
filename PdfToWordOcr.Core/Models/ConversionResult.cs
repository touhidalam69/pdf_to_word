namespace PdfToWordOcr.Core.Models;

public sealed record ConversionResult(
    string OutputPath,
    int Pages,
    int[] FailedPages,
    TimeSpan Elapsed);
