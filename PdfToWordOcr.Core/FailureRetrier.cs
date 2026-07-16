namespace PdfToWordOcr.Core;

/// <summary>
/// Drives retry cycles for pages that still have no OCR output. Small
/// remainders go through the existing synchronous client with a higher token
/// budget; large remainders go through another batch cycle (supplied by the
/// pipeline so submissions are recorded in the job manifest).
/// </summary>
public static class FailureRetrier
{
    public const int SyncRetryThreshold = 25;
    public const int MaxCycles = 3;
    public const int RetryMaxTokens = 8192;

    /// <summary>
    /// Runs up to <see cref="MaxCycles"/> retry cycles and returns the pages
    /// that are still missing afterwards (empty = everything transcribed).
    /// </summary>
    public static async Task<IReadOnlyList<int>> RetryMissingAsync(
        JobWorkspace workspace,
        int totalPages,
        OcrClient syncClient,
        string model,
        string language,
        string prompt,
        Func<IReadOnlyList<int>, CancellationToken, Task> runBatchCycle,
        Action<string>? report,
        CancellationToken ct)
    {
        for (var cycle = 1; cycle <= MaxCycles; cycle++)
        {
            ct.ThrowIfCancellationRequested();

            var pending = workspace.GetPendingPages(totalPages);
            if (pending.Count == 0)
            {
                return [];
            }

            report?.Invoke($"Retry cycle {cycle}/{MaxCycles} — {pending.Count} page(s) pending.");
            workspace.Log($"Retry cycle {cycle}/{MaxCycles}: pages {string.Join(", ", pending)}.");

            if (pending.Count <= SyncRetryThreshold)
            {
                await RetrySynchronouslyAsync(workspace, pending, syncClient, model, language, prompt, report, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                await runBatchCycle(pending, ct).ConfigureAwait(false);
            }
        }

        return workspace.GetPendingPages(totalPages);
    }

    private static async Task RetrySynchronouslyAsync(
        JobWorkspace workspace,
        IReadOnlyList<int> pages,
        OcrClient syncClient,
        string model,
        string language,
        string prompt,
        Action<string>? report,
        CancellationToken ct)
    {
        var done = 0;
        foreach (var pageNumber in pages)
        {
            ct.ThrowIfCancellationRequested();

            var stem = JobWorkspace.PageStem(pageNumber);
            var image = new PageImage(
                await File.ReadAllBytesAsync(workspace.PageImagePath(pageNumber), ct).ConfigureAwait(false),
                "image/jpeg");

            try
            {
                var rawText = await syncClient
                    .TranscribePageAsync(image, model, language, pageNumber, ct, prompt, RetryMaxTokens)
                    .ConfigureAwait(false);

                var result = BatchResultProcessor.ClassifyText(stem, rawText, language);
                switch (result.Kind)
                {
                    case BatchResultKind.Succeeded:
                        workspace.WriteOcrText(pageNumber, result.Text);
                        break;
                    case BatchResultKind.Blank:
                        workspace.WriteOcrText(pageNumber, string.Empty);
                        break;
                    default:
                        workspace.AppendFailure(stem, result.FailureReason ?? "unknown");
                        workspace.Log($"Sync retry of page {pageNumber} classified as {result.FailureReason}.");
                        break;
                }
            }
            catch (OcrPageException ex)
            {
                workspace.AppendFailure(stem, ex.Message);
                workspace.Log($"Sync retry of page {pageNumber} failed: {ex.Message}");
            }

            done++;
            report?.Invoke($"Retrying failed pages — {done}/{pages.Count}.");
        }
    }
}
