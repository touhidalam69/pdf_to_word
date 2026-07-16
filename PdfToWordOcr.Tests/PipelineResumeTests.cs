using PdfToWordOcr.Core;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Tests;

/// <summary>
/// Headless kill/resume coverage: each test simulates "the app died at stage X"
/// by preparing the work directory the way a killed run would have left it,
/// then runs a fresh pipeline over it and asserts no work is redone and no
/// duplicate API spend happens.
/// </summary>
public class PipelineResumeTests
{
    private static ConversionOptions Options(TempDir dir, string inputPdf, OutputFormat format = OutputFormat.Markdown) =>
        new(
            InputPath: inputPdf,
            OutputPath: Path.Combine(dir.Path, format == OutputFormat.Markdown ? "output.md" : "output.docx"),
            Model: "claude-haiku-4-5-20251001",
            Dpi: 72,
            Font: "Nirmala UI",
            Language: "English",
            ContinueOnPageFailure: true,
            Format: format,
            Mode: ProcessingMode.Batch);

    private static string WritePdf(TempDir dir, int pages)
    {
        var path = Path.Combine(dir.Path, "input.pdf");
        File.WriteAllBytes(path, MinimalPdf.Create(pages));
        return path;
    }

    private static BatchConversionPipeline Pipeline(FakeBatchServer server)
    {
        var client = server.CreateClient();
        return new BatchConversionPipeline(new OcrClient(client, "test-key"), new BatchOcrClient(client, "test-key"));
    }

    [Fact]
    public async Task FullRunProducesMarkdownFromSingleBatch()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 3);
        var server = new FakeBatchServer();
        var options = Options(dir, inputPdf);

        var result = await Pipeline(server).RunAsync(options, new NullProgress(), CancellationToken.None);

        Assert.Empty(result.FailedPages);
        Assert.Equal(3, result.Pages);
        Assert.Equal(1, server.PostCount);
        var output = File.ReadAllText(options.OutputPath);
        Assert.Contains("<!-- page 1 -->", output);
        Assert.Contains("Text of page_0003.", output);

        var workspace = new JobWorkspace(inputPdf);
        Assert.Empty(workspace.GetPendingPages(3));
        Assert.True(Assert.Single(workspace.LoadManifest()!.Batches).Collected);
    }

    [Fact]
    public async Task FullRunProducesDocx()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 2);
        var server = new FakeBatchServer();
        var options = Options(dir, inputPdf, OutputFormat.Word);

        var result = await Pipeline(server).RunAsync(options, new NullProgress(), CancellationToken.None);

        Assert.Empty(result.FailedPages);
        Assert.True(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task CompletedWorkDirIsReusedWithoutAnyResubmission()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 3);
        var options = Options(dir, inputPdf);

        await Pipeline(new FakeBatchServer()).RunAsync(options, new NullProgress(), CancellationToken.None);
        File.Delete(options.OutputPath);

        // "Killed after OCR finished" — a fresh session must stitch straight
        // from ocr\*.txt. Any POST against this server would fail the run.
        var strictServer = new FakeBatchServer { RejectSubmissions = true };
        var result = await Pipeline(strictServer).RunAsync(options, new NullProgress(), CancellationToken.None);

        Assert.Empty(result.FailedPages);
        Assert.Equal(0, strictServer.PostCount);
        Assert.True(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task UncollectedBatchIsCollectedWithoutResubmission()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 3);
        var options = Options(dir, inputPdf);

        // "Killed between submit and collect": manifest references a batch the
        // server knows about, but no results were ever written locally.
        var workspace = new JobWorkspace(inputPdf);
        workspace.EnsureDirectories();
        workspace.SaveManifest(new JobManifest
        {
            PageCount = 3,
            Mode = ProcessingMode.Batch,
            Model = options.Model,
            Format = OutputFormat.Markdown,
            Batches = [new BatchSubmission { BatchId = "batch_seeded", PageNumbers = [1, 2, 3] }],
        });

        var server = new FakeBatchServer { RejectSubmissions = true };
        server.SeedBatch("batch_seeded", [1, 2, 3]);

        var result = await Pipeline(server).RunAsync(options, new NullProgress(), CancellationToken.None);

        Assert.Empty(result.FailedPages);
        Assert.Equal(0, server.PostCount);
        Assert.True(workspace.LoadManifest()!.Batches.All(batch => batch.Collected));
        Assert.True(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task ExpiredUncollectedBatchIsResubmitted()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 3);
        var options = Options(dir, inputPdf);

        var workspace = new JobWorkspace(inputPdf);
        workspace.EnsureDirectories();
        workspace.SaveManifest(new JobManifest
        {
            PageCount = 3,
            Mode = ProcessingMode.Batch,
            Model = options.Model,
            Format = OutputFormat.Markdown,
            Batches = [new BatchSubmission { BatchId = "batch_gone", PageNumbers = [1, 2, 3] }],
        });

        var server = new FakeBatchServer();
        server.GoneBatchIds.Add("batch_gone");

        var result = await Pipeline(server).RunAsync(options, new NullProgress(), CancellationToken.None);

        // The expired batch's pages were simply "missing" and went through the
        // normal resubmit path.
        Assert.Empty(result.FailedPages);
        Assert.Equal(1, server.PostCount);
        Assert.True(File.Exists(options.OutputPath));
    }

    [Fact]
    public async Task PilotRunConvertsFirstPagesOnlyAndIsReusedByFullRun()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 12);
        var server = new FakeBatchServer();
        var pilotOptions = Options(dir, inputPdf) with { Pilot = true };

        var pilotResult = await Pipeline(server).RunAsync(pilotOptions, new NullProgress(), CancellationToken.None);

        Assert.Equal(BatchConversionPipeline.PilotPageCount, pilotResult.Pages);
        var pilotPath = BatchConversionPipeline.MakePilotPath(pilotOptions.OutputPath);
        Assert.True(File.Exists(pilotPath));
        Assert.Equal(1, server.PostCount);

        // Full run reuses the pilot's 10 pages: only 2 more pages get submitted.
        var fullResult = await Pipeline(server).RunAsync(
            Options(dir, inputPdf), new NullProgress(), CancellationToken.None);

        Assert.Equal(12, fullResult.Pages);
        Assert.Empty(fullResult.FailedPages);
        Assert.Equal(2, server.PostCount);
        var output = File.ReadAllText(Options(dir, inputPdf).OutputPath);
        Assert.Contains("<!-- page 12 -->", output);
    }

    [Fact]
    public async Task PermanentFailuresBlockOutputAndAreReported()
    {
        using var dir = new TempDir();
        var inputPdf = WritePdf(dir, 3);
        var options = Options(dir, inputPdf);

        // Page 2 errors on every cycle; sync retries also fail (the fake server
        // has no /v1/messages endpoint, so the sync client gives up per-page).
        var server = new FakeBatchServer
        {
            ResultLineFactory = customId => customId == "page_0002"
                ? """{"custom_id":"page_0002","result":{"type":"errored","error":{"type":"api_error","message":"boom"}}}"""
                : FakeBatchServer.SucceededLine(customId, $"Text of {customId}."),
        };

        var result = await Pipeline(server).RunAsync(options, new NullProgress(), CancellationToken.None);

        Assert.Equal(new[] { 2 }, result.FailedPages);
        Assert.False(File.Exists(options.OutputPath));

        var workspace = new JobWorkspace(inputPdf);
        Assert.Contains("page_0002", File.ReadAllText(workspace.FailuresPath));
    }
}
