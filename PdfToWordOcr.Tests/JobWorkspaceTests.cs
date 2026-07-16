using PdfToWordOcr.Core;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Tests;

public class JobWorkspaceTests
{
    [Fact]
    public void PageStemIsFourDigitOneBased()
    {
        Assert.Equal("page_0001", JobWorkspace.PageStem(1));
        Assert.Equal("page_1000", JobWorkspace.PageStem(1000));
    }

    [Theory]
    [InlineData("page_0042", 42)]
    [InlineData("page_1000", 1000)]
    public void TryParsePageNumberRoundTrips(string customId, int expected) =>
        Assert.Equal(expected, JobWorkspace.TryParsePageNumber(customId));

    [Theory]
    [InlineData("page_")]
    [InlineData("page_abc")]
    [InlineData("page_0000")]
    [InlineData("other_0001")]
    public void TryParsePageNumberRejectsInvalidIds(string customId) =>
        Assert.Null(JobWorkspace.TryParsePageNumber(customId));

    [Fact]
    public void PendingPagesAreThoseWithoutOcrFiles()
    {
        using var dir = new TempDir();
        var workspace = new JobWorkspace(Path.Combine(dir.Path, "input.pdf"));
        workspace.EnsureDirectories();
        workspace.WriteOcrText(1, "text");
        workspace.WriteOcrText(3, string.Empty); // blank page still counts as done

        Assert.Equal(new[] { 2, 4 }, workspace.GetPendingPages(4));
    }

    [Fact]
    public void ManifestRoundTrips()
    {
        using var dir = new TempDir();
        var workspace = new JobWorkspace(Path.Combine(dir.Path, "input.pdf"));
        workspace.EnsureDirectories();

        workspace.SaveManifest(new JobManifest
        {
            PageCount = 42,
            Mode = ProcessingMode.Batch,
            Model = "claude-haiku-4-5-20251001",
            Format = OutputFormat.Markdown,
            Batches = [new BatchSubmission { BatchId = "batch_x", PageNumbers = [1, 2], Collected = true }],
        });

        var loaded = workspace.LoadManifest();

        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.PageCount);
        Assert.Equal(ProcessingMode.Batch, loaded.Mode);
        Assert.Equal(OutputFormat.Markdown, loaded.Format);
        var batch = Assert.Single(loaded.Batches);
        Assert.Equal("batch_x", batch.BatchId);
        Assert.Equal(new[] { 1, 2 }, batch.PageNumbers);
        Assert.True(batch.Collected);
    }

    [Fact]
    public void DeleteRemovesEverything()
    {
        using var dir = new TempDir();
        var workspace = new JobWorkspace(Path.Combine(dir.Path, "input.pdf"));
        workspace.EnsureDirectories();
        workspace.WriteOcrText(1, "text");

        Assert.True(workspace.HasAnyProgress());
        workspace.Delete();
        Assert.False(workspace.HasAnyProgress());
        Assert.False(Directory.Exists(workspace.Root));
    }
}
