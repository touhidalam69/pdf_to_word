namespace PdfToWordOcr.Core.Models;

public sealed record ConversionOptions(
    string InputPath,
    string OutputPath,
    string Model,
    int Dpi,
    string Font,
    string Language,
    bool ContinueOnPageFailure);
