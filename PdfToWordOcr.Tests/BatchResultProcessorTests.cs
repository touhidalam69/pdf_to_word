using System.Text.Json;
using PdfToWordOcr.Core;

namespace PdfToWordOcr.Tests;

public class BatchResultProcessorTests
{
    private const string BanglaSample = "এই পাতায় লেখা আছে";

    private static string Line(string customId, object result) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["custom_id"] = customId, ["result"] = result });

    private static object Succeeded(string text, string stopReason = "end_turn") => new
    {
        type = "succeeded",
        message = new { content = new[] { new { type = "text", text } }, stop_reason = stopReason },
    };

    [Fact]
    public void SucceededTextIsReturned()
    {
        var result = BatchResultProcessor.ProcessLine(Line("page_0007", Succeeded(BanglaSample)), "Bangla");

        Assert.Equal(BatchResultKind.Succeeded, result.Kind);
        Assert.Equal("page_0007", result.CustomId);
        Assert.Equal(BanglaSample, result.Text);
    }

    [Fact]
    public void MaxTokensStopReasonIsTruncated()
    {
        var result = BatchResultProcessor.ProcessLine(Line("page_0001", Succeeded("partial...", "max_tokens")), "English");

        Assert.Equal(BatchResultKind.Truncated, result.Kind);
        Assert.Equal("truncated", result.FailureReason);
    }

    [Fact]
    public void BlankSentinelBecomesEmpty()
    {
        var result = BatchResultProcessor.ProcessLine(Line("page_0002", Succeeded("[BLANK]")), "English");

        Assert.Equal(BatchResultKind.Blank, result.Kind);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public void WhitespaceOnlyOutputIsBlank()
    {
        var result = BatchResultProcessor.ProcessLine(Line("page_0002", Succeeded("  \n ")), "English");

        Assert.Equal(BatchResultKind.Blank, result.Kind);
    }

    [Fact]
    public void FencePairIsStripped()
    {
        var fenced = "```markdown\n# Title\n\nBody text.\n```";
        var result = BatchResultProcessor.ProcessLine(Line("page_0003", Succeeded(fenced)), "English");

        Assert.Equal(BatchResultKind.Succeeded, result.Kind);
        Assert.Equal("# Title\n\nBody text.", result.Text);
    }

    [Fact]
    public void UnclosedFenceIsKept()
    {
        var text = "```markdown\n# Title without closing fence";
        var result = BatchResultProcessor.ProcessLine(Line("page_0003", Succeeded(text)), "English");

        Assert.Equal(BatchResultKind.Succeeded, result.Kind);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void ErroredCarriesReason()
    {
        var errored = new { type = "errored", error = new { type = "api_error", message = "overloaded" } };
        var result = BatchResultProcessor.ProcessLine(Line("page_0004", errored), "English");

        Assert.Equal(BatchResultKind.Errored, result.Kind);
        Assert.Contains("api_error", result.FailureReason);
        Assert.Contains("overloaded", result.FailureReason);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("canceled")]
    public void ExpiredAndCanceledAreErrored(string type)
    {
        var result = BatchResultProcessor.ProcessLine(Line("page_0005", new { type }), "English");

        Assert.Equal(BatchResultKind.Errored, result.Kind);
        Assert.Equal(type, result.FailureReason);
    }

    [Fact]
    public void ShortRefusalWithoutScriptIsRefusal()
    {
        var result = BatchResultProcessor.ProcessLine(
            Line("page_0006", Succeeded("I can't transcribe this document for you.")), "Bangla");

        Assert.Equal(BatchResultKind.Refusal, result.Kind);
    }

    [Fact]
    public void RefusalOpeningWithScriptCharactersIsAccepted()
    {
        // Contains Bangla characters, so the "no script chars" leg fails → real content.
        var text = $"I cannot — {BanglaSample}";
        var result = BatchResultProcessor.ProcessLine(Line("page_0006", Succeeded(text)), "Bangla");

        Assert.Equal(BatchResultKind.Succeeded, result.Kind);
    }

    [Fact]
    public void LongTextStartingLikeRefusalIsAccepted()
    {
        var text = "I cannot overstate the significance. " + new string('x', 200);
        var result = BatchResultProcessor.ProcessLine(Line("page_0006", Succeeded(text)), "Bangla");

        Assert.Equal(BatchResultKind.Succeeded, result.Kind);
    }

    [Fact]
    public void ClassifyTextMatchesBatchClassification()
    {
        var result = BatchResultProcessor.ClassifyText("page_0009", "```\n[BLANK]\n```", "English");

        Assert.Equal(BatchResultKind.Blank, result.Kind);
    }
}
