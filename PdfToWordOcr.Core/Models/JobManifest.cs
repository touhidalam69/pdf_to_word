namespace PdfToWordOcr.Core.Models;

/// <summary>
/// One submitted batch: the API batch id plus the pages it covers.
/// Collected flips to true once results have been downloaded and written
/// to the work directory.
/// </summary>
public sealed class BatchSubmission
{
    public string BatchId { get; set; } = string.Empty;
    public int[] PageNumbers { get; set; } = [];
    public bool Collected { get; set; }
}

/// <summary>
/// Persisted state of a conversion job (job.json in the work directory).
/// Flushed to disk immediately after every batch submission so a crash
/// never loses track of in-flight batches.
/// </summary>
public sealed class JobManifest
{
    public int PageCount { get; set; }
    public ProcessingMode Mode { get; set; }
    public string Model { get; set; } = string.Empty;
    public OutputFormat Format { get; set; }
    public List<BatchSubmission> Batches { get; set; } = [];
}
