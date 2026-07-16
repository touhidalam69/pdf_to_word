using System.Text;
using PdfToWordOcr.Core;

namespace PdfToWordOcr.Tests;

public class MarkdownWriterTests
{
    private static (JobWorkspace Workspace, string OutputPath) Setup(TempDir dir)
    {
        var workspace = new JobWorkspace(Path.Combine(dir.Path, "input.pdf"));
        workspace.EnsureDirectories();
        return (workspace, Path.Combine(dir.Path, "output.md"));
    }

    [Fact]
    public void PagesAreStitchedInOrderWithMarkers()
    {
        using var dir = new TempDir();
        var (workspace, outputPath) = Setup(dir);
        workspace.WriteOcrText(1, "# First");
        workspace.WriteOcrText(2, "Second page text.");
        workspace.WriteOcrText(3, "Third.");

        MarkdownWriter.Write(workspace, 3, outputPath);

        var output = File.ReadAllText(outputPath);
        var p1 = output.IndexOf("<!-- page 1 -->", StringComparison.Ordinal);
        var p2 = output.IndexOf("<!-- page 2 -->", StringComparison.Ordinal);
        var p3 = output.IndexOf("<!-- page 3 -->", StringComparison.Ordinal);
        Assert.True(p1 >= 0 && p1 < p2 && p2 < p3);
        Assert.Contains("# First", output);
        Assert.Contains("Third.", output);
    }

    [Fact]
    public void BlankPagesAreSkippedEntirely()
    {
        using var dir = new TempDir();
        var (workspace, outputPath) = Setup(dir);
        workspace.WriteOcrText(1, "One");
        workspace.WriteOcrText(2, string.Empty);
        workspace.WriteOcrText(3, "Three");

        MarkdownWriter.Write(workspace, 3, outputPath);

        var output = File.ReadAllText(outputPath);
        Assert.DoesNotContain("<!-- page 2 -->", output);
        Assert.Contains("<!-- page 3 -->", output);
    }

    [Fact]
    public void MissingPagesRefuseToStitchAndAreListed()
    {
        using var dir = new TempDir();
        var (workspace, outputPath) = Setup(dir);
        workspace.WriteOcrText(1, "One");
        workspace.WriteOcrText(4, "Four");

        var ex = Assert.Throws<InvalidOperationException>(() => MarkdownWriter.Write(workspace, 4, outputPath));

        Assert.Contains("2, 3", ex.Message);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void OutputIsUtf8WithoutBom()
    {
        using var dir = new TempDir();
        var (workspace, outputPath) = Setup(dir);
        workspace.WriteOcrText(1, "এই পাতা");

        MarkdownWriter.Write(workspace, 1, outputPath);

        var bytes = File.ReadAllBytes(outputPath);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "output must not start with a BOM");
        Assert.Contains("এই", Encoding.UTF8.GetString(bytes));
    }
}
