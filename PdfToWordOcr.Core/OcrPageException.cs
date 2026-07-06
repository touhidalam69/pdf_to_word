namespace PdfToWordOcr.Core;

public sealed class OcrPageException : Exception
{
    public int PageNumber { get; }

    public OcrPageException(int pageNumber, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        PageNumber = pageNumber;
    }
}
