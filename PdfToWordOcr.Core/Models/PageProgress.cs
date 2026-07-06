namespace PdfToWordOcr.Core.Models;

public sealed record PageProgress(int Completed, int Total, string Message);
