namespace PdfToWordOcr.Core.Models;

public sealed record ConversionOptions(
    string InputPath,
    string OutputPath,
    string Model,
    int Dpi,
    string Font,
    string Language,
    bool ContinueOnPageFailure,
    OutputFormat Format = OutputFormat.Word,
    ProcessingMode Mode = ProcessingMode.Auto,
    bool Pilot = false);
