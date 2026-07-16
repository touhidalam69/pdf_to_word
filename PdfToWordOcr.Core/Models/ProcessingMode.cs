namespace PdfToWordOcr.Core.Models;

public enum ProcessingMode
{
    /// <summary>Synchronous for small PDFs, Batch above the page threshold.</summary>
    Auto,
    Synchronous,
    Batch,
}
