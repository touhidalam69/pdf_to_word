using System.Diagnostics;
using System.Net;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Core;

/// <summary>
/// Resumable batch conversion: rasterize → submit batches → poll → collect →
/// retry → stitch, checkpointing every stage in the work directory. Killing
/// the app at any point and pressing Start again continues without redoing
/// completed pages or resubmitting collected batches.
/// </summary>
public sealed class BatchConversionPipeline
{
    public const int PilotPageCount = 10;

    private const long RequiredFreeDiskMargin = 500L * 1024 * 1024;

    private readonly OcrClient _syncClient;
    private readonly BatchOcrClient _batchClient;

    public BatchConversionPipeline(OcrClient syncClient, BatchOcrClient batchClient)
    {
        _syncClient = syncClient;
        _batchClient = batchClient;
    }

    public async Task<ConversionResult> RunAsync(
        ConversionOptions opt,
        IProgress<PageProgress> progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var pdfBytes = await File.ReadAllBytesAsync(opt.InputPath, ct).ConfigureAwait(false);
        var totalPages = PdfRasterizer.GetPageCount(pdfBytes);
        var jobPages = opt.Pilot ? Math.Min(PilotPageCount, totalPages) : totalPages;

        var workspace = new JobWorkspace(opt.InputPath);
        workspace.EnsureDirectories();
        CheckDiskSpace(workspace, pdfBytes.LongLength);

        var prompt = OcrPrompts.Build(opt.Language, opt.Format, opt.PromptTemplate);
        var outputPath = opt.Pilot ? MakePilotPath(opt.OutputPath) : opt.OutputPath;

        var manifest = workspace.LoadManifest() ?? new JobManifest();
        manifest.PageCount = totalPages;
        manifest.Mode = ProcessingMode.Batch;
        manifest.Model = opt.Model;
        manifest.Format = opt.Format;
        workspace.SaveManifest(manifest);

        workspace.Log($"Run started: {jobPages}/{totalPages} page(s), model {opt.Model}, format {opt.Format}"
            + (opt.Pilot ? ", pilot mode" : "") + ".");

        await CollectOutstandingBatchesAsync(workspace, manifest, opt.Language, jobPages, progress, ct)
            .ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
        await RasterizePendingAsync(pdfBytes, workspace, opt.Dpi, jobPages, progress, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
        var pending = workspace.GetPendingPages(jobPages).Where(workspace.HasPageImage).ToArray();
        if (pending.Length > 0)
        {
            await RunBatchCycleAsync(workspace, manifest, pending, opt.Model, prompt, opt.Language, jobPages, progress, ct)
                .ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        var permanentlyFailed = await FailureRetrier.RetryMissingAsync(
                workspace,
                jobPages,
                _syncClient,
                opt.Model,
                opt.Language,
                prompt,
                (pages, token) => RunBatchCycleAsync(
                    workspace, manifest, pages.Where(workspace.HasPageImage).ToArray(),
                    opt.Model, prompt, opt.Language, jobPages, progress, token),
                message => progress.Report(new PageProgress(CountDone(workspace, jobPages), jobPages, message)),
                ct)
            .ConfigureAwait(false);

        if (permanentlyFailed.Count > 0)
        {
            var pageList = string.Join(", ", permanentlyFailed);
            workspace.Log($"Permanently failed page(s) after {FailureRetrier.MaxCycles} retry cycles: {pageList}.");
            progress.Report(new PageProgress(
                jobPages - permanentlyFailed.Count,
                jobPages,
                $"{permanentlyFailed.Count} page(s) permanently failed: {pageList}. Output not written — press Start to retry."));

            stopwatch.Stop();
            return new ConversionResult(outputPath, jobPages, permanentlyFailed.ToArray(), stopwatch.Elapsed);
        }

        progress.Report(new PageProgress(jobPages, jobPages, $"Writing {opt.Format} output..."));
        if (opt.Format == OutputFormat.Markdown)
        {
            MarkdownWriter.Write(workspace, jobPages, outputPath);
        }
        else
        {
            DocxWriter.WriteFromWorkDir(workspace, jobPages, opt.Font, opt.Language, outputPath);
        }

        workspace.Log($"Output written: {outputPath}.");
        stopwatch.Stop();
        return new ConversionResult(outputPath, jobPages, [], stopwatch.Elapsed);
    }

    /// <summary>"history.docx" → "history.pilot.docx" (same for .md).</summary>
    public static string MakePilotPath(string outputPath)
    {
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(outputPath) + ".pilot" + extension);
    }

    private static void CheckDiskSpace(JobWorkspace workspace, long inputBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(workspace.Root));
        if (root is null)
        {
            return;
        }

        var required = inputBytes * 3 + RequiredFreeDiskMargin;
        var free = new DriveInfo(root).AvailableFreeSpace;
        if (free < required)
        {
            throw new InvalidOperationException(
                $"Not enough disk space: {free / (1024 * 1024)} MB free, "
                + $"about {required / (1024 * 1024)} MB needed for the work directory.");
        }
    }

    /// <summary>Startup recovery: batches submitted by a previous run but never collected.</summary>
    private async Task CollectOutstandingBatchesAsync(
        JobWorkspace workspace,
        JobManifest manifest,
        string language,
        int jobPages,
        IProgress<PageProgress> progress,
        CancellationToken ct)
    {
        foreach (var submission in manifest.Batches.Where(batch => !batch.Collected))
        {
            ct.ThrowIfCancellationRequested();
            progress.Report(new PageProgress(
                CountDone(workspace, jobPages), jobPages,
                $"Collecting previously submitted batch {submission.BatchId}..."));

            try
            {
                var status = await _batchClient
                    .PollUntilEndedAsync(
                        submission.BatchId,
                        counts => ReportBatchCounts(workspace, jobPages, 1, 1, counts, progress),
                        ct)
                    .ConfigureAwait(false);
                await _batchClient
                    .CollectResultsAsync(workspace, submission.BatchId, status.ResultsUrl, language, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // Batch or its results expired uncollected: the affected pages are
                // simply still missing and the normal resubmit path picks them up.
                workspace.Log(
                    $"Batch {submission.BatchId} is no longer available ({(int?)ex.StatusCode}); "
                    + "its pages will be resubmitted.");
            }

            submission.Collected = true;
            workspace.SaveManifest(manifest);
        }
    }

    private static async Task RasterizePendingAsync(
        byte[] pdfBytes,
        JobWorkspace workspace,
        int dpi,
        int jobPages,
        IProgress<PageProgress> progress,
        CancellationToken ct)
    {
        var needingImages = workspace.GetPendingPages(jobPages)
            .Where(page => !workspace.HasPageImage(page))
            .ToList();
        if (needingImages.Count == 0)
        {
            return;
        }

        var total = needingImages.Count;
        var done = 0;

        // A single corrupt page must not sink the run: record it, drop it, and
        // continue rasterizing the rest.
        while (needingImages.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var completed = new HashSet<int>();
            try
            {
                await foreach (var page in PdfRasterizer
                    .RasterizeToDirectoryAsync(pdfBytes, workspace, dpi, needingImages, workspace.Log, ct)
                    .WithCancellation(ct))
                {
                    completed.Add(page);
                    done++;
                    progress.Report(new PageProgress(done, total, $"Rasterizing {done}/{total}"));
                }

                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failedPage = needingImages.FirstOrDefault(page => !completed.Contains(page));
                if (failedPage == 0)
                {
                    throw;
                }

                workspace.AppendFailure(JobWorkspace.PageStem(failedPage), $"rasterize failed: {ex.Message}");
                workspace.Log($"Page {failedPage} failed to rasterize: {ex.Message}");
                done++;
                needingImages = needingImages.Where(page => !completed.Contains(page) && page != failedPage).ToList();
            }
        }
    }

    /// <summary>One submit→poll→collect pass over the given pages, chunked per batch.</summary>
    private async Task RunBatchCycleAsync(
        JobWorkspace workspace,
        JobManifest manifest,
        IReadOnlyList<int> pages,
        string model,
        string prompt,
        string language,
        int jobPages,
        IProgress<PageProgress> progress,
        CancellationToken ct)
    {
        if (pages.Count == 0)
        {
            return;
        }

        var chunks = BatchOcrClient.ChunkPages(pages);
        var submissions = new List<BatchSubmission>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report(new PageProgress(
                CountDone(workspace, jobPages), jobPages,
                $"Submitting batch {i + 1}/{chunks.Count} ({chunks[i].Length} page(s))..."));

            var batchId = await _batchClient
                .SubmitBatchAsync(workspace, chunks[i], model, prompt, ct)
                .ConfigureAwait(false);

            var submission = new BatchSubmission { BatchId = batchId, PageNumbers = chunks[i] };
            submissions.Add(submission);
            manifest.Batches.Add(submission);
            workspace.SaveManifest(manifest);
            workspace.Log($"Submitted batch {batchId} covering {chunks[i].Length} page(s).");
        }

        for (var i = 0; i < submissions.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var submission = submissions[i];
            var batchNumber = i + 1;

            var status = await _batchClient
                .PollUntilEndedAsync(
                    submission.BatchId,
                    counts => ReportBatchCounts(workspace, jobPages, batchNumber, submissions.Count, counts, progress),
                    ct)
                .ConfigureAwait(false);

            await _batchClient
                .CollectResultsAsync(workspace, submission.BatchId, status.ResultsUrl, language, ct)
                .ConfigureAwait(false);

            submission.Collected = true;
            workspace.SaveManifest(manifest);
            workspace.Log($"Collected batch {submission.BatchId}.");
        }
    }

    private static void ReportBatchCounts(
        JobWorkspace workspace,
        int jobPages,
        int batchNumber,
        int batchCount,
        BatchCounts counts,
        IProgress<PageProgress> progress)
    {
        var failed = counts.Errored + counts.Canceled + counts.Expired;
        progress.Report(new PageProgress(
            CountDone(workspace, jobPages),
            jobPages,
            $"Batch {batchNumber}/{batchCount} — {counts.Succeeded} done, {failed} failed, {counts.Processing} processing"));
    }

    private static int CountDone(JobWorkspace workspace, int jobPages) =>
        jobPages - workspace.GetPendingPages(jobPages).Count;
}
