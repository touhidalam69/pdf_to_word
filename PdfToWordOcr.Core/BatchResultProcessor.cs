using System.Text;
using System.Text.Json;

namespace PdfToWordOcr.Core;

public enum BatchResultKind
{
    Succeeded,
    Blank,
    Truncated,
    Refusal,
    Errored,
}

public sealed record BatchPageResult(string CustomId, BatchResultKind Kind, string Text, string? FailureReason);

/// <summary>
/// Classifies one JSONL line from a Message Batches results stream. Pure and
/// stateless so every branch is unit-testable without HTTP.
/// </summary>
public static class BatchResultProcessor
{
    private static readonly string[] RefusalOpenings = ["I can't", "I'm unable", "I cannot"];

    // Unicode ranges per curated language, used only to recognize refusals:
    // a "successful" transcription that contains none of the document's script
    // is suspect. Latin-script and free-typed languages are absent on purpose —
    // refusal text is Latin too, so the test would always pass.
    private static readonly Dictionary<string, (int Start, int End)[]> ScriptRanges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Bangla"] = [(0x0980, 0x09FF)],
            ["Hindi"] = [(0x0900, 0x097F)],
            ["Nepali"] = [(0x0900, 0x097F)],
            ["Urdu"] = [(0x0600, 0x06FF), (0x0750, 0x077F), (0xFB50, 0xFDFF)],
            ["Arabic"] = [(0x0600, 0x06FF), (0x0750, 0x077F), (0xFB50, 0xFDFF)],
            ["Tamil"] = [(0x0B80, 0x0BFF)],
        };

    public static BatchPageResult ProcessLine(string jsonLine, string language)
    {
        using var document = JsonDocument.Parse(jsonLine);
        var root = document.RootElement;

        var customId = root.GetProperty("custom_id").GetString() ?? string.Empty;
        var result = root.GetProperty("result");
        var resultType = result.GetProperty("type").GetString();

        switch (resultType)
        {
            case "succeeded":
                return ProcessSucceeded(customId, result.GetProperty("message"), language);
            case "errored":
                return new BatchPageResult(customId, BatchResultKind.Errored, string.Empty, DescribeError(result));
            case "expired":
            case "canceled":
                return new BatchPageResult(customId, BatchResultKind.Errored, string.Empty, resultType);
            default:
                return new BatchPageResult(customId, BatchResultKind.Errored, string.Empty, $"unknown result type '{resultType}'");
        }
    }

    private static BatchPageResult ProcessSucceeded(string customId, JsonElement message, string language)
    {
        if (message.TryGetProperty("stop_reason", out var stopReason)
            && stopReason.GetString() == "max_tokens")
        {
            return new BatchPageResult(customId, BatchResultKind.Truncated, string.Empty, "truncated");
        }

        return ClassifyText(customId, ExtractText(message), language);
    }

    /// <summary>
    /// Normalizes and classifies a model transcription (fence stripping, the
    /// [BLANK] sentinel, the refusal heuristic). Shared with the synchronous
    /// retry path so retried pages match batch output exactly.
    /// </summary>
    public static BatchPageResult ClassifyText(string customId, string rawText, string language)
    {
        var text = StripFencePair(rawText).Trim();

        if (text.Length == 0 || string.Equals(text, OcrPrompts.BlankSentinel, StringComparison.Ordinal))
        {
            return new BatchPageResult(customId, BatchResultKind.Blank, string.Empty, null);
        }

        if (LooksLikeRefusal(text, language))
        {
            return new BatchPageResult(customId, BatchResultKind.Refusal, string.Empty, "refusal");
        }

        return new BatchPageResult(customId, BatchResultKind.Succeeded, text, null);
    }

    private static string ExtractText(JsonElement message)
    {
        var sb = new StringBuilder();
        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type)
                    && type.GetString() == "text"
                    && block.TryGetProperty("text", out var text))
                {
                    sb.Append(text.GetString());
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>Removes a single accidental ``` fence pair wrapping the whole output.</summary>
    private static string StripFencePair(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0 || !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var inner = trimmed[(firstLineEnd + 1)..^3];
        return inner.Trim('\r', '\n');
    }

    private static bool LooksLikeRefusal(string text, string language)
    {
        if (text.Length >= 200)
        {
            return false;
        }

        if (!RefusalOpenings.Any(opening => text.StartsWith(opening, StringComparison.Ordinal)))
        {
            return false;
        }

        // Only trust the heuristic when we can also confirm the document's
        // script is absent; for Latin-script languages the opening + length
        // checks are all we have.
        if (ScriptRanges.TryGetValue(language, out var ranges))
        {
            return !text.Any(ch => ranges.Any(range => ch >= range.Start && ch <= range.End));
        }

        return true;
    }

    private static string DescribeError(JsonElement result)
    {
        if (result.TryGetProperty("error", out var error))
        {
            var type = error.TryGetProperty("type", out var t) ? t.GetString() : null;
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            return $"errored: {type ?? "unknown"}{(message is null ? string.Empty : $" — {message}")}";
        }

        return "errored";
    }
}
