using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.Core;

/// <summary>
/// OCR prompt templates shared by the batch client and the synchronous retry
/// path. Templates carry a {LANGUAGE} placeholder substituted with the user's
/// language selection — no language is ever hardcoded.
/// </summary>
public static class OcrPrompts
{
    public const string LanguagePlaceholder = "{LANGUAGE}";

    /// <summary>Model output meaning "this page has no legible text" — stored as an empty ocr file.</summary>
    public const string BlankSentinel = "[BLANK]";

    public const string DefaultMarkdownTemplate =
        "Transcribe this scanned page into clean Markdown.\n" +
        "- Preserve structure: headings (#, ##), bullet/numbered lists, tables (GitHub Markdown table syntax).\n" +
        "- Keep the original {LANGUAGE} text exactly as written; do not translate or romanize.\n" +
        "- OMIT page numbers and running headers/footers that repeat on every page.\n" +
        "- Do not describe figures; transcribe figure captions only, as italic text.\n" +
        "- Output ONLY the Markdown. No preamble, no code fences.\n" +
        "- If the page has no legible text, output exactly: [BLANK]";

    public const string DefaultWordTemplate =
        "You are an OCR engine for scanned {LANGUAGE} documents. Transcribe " +
        "every character exactly as printed, in Unicode {LANGUAGE} script. " +
        "Preserve paragraph breaks (one blank line between paragraphs) and " +
        "reading order. OMIT page numbers and running headers/footers that " +
        "repeat on every page. Do NOT translate, correct, normalize, " +
        "summarize, or add commentary. Output ONLY the transcription. " +
        "If the page has no legible text, output exactly: [BLANK]";

    public static string DefaultTemplate(OutputFormat format) =>
        format == OutputFormat.Markdown ? DefaultMarkdownTemplate : DefaultWordTemplate;

    public static string Build(string language, OutputFormat format, string? templateOverride = null)
    {
        var template = string.IsNullOrWhiteSpace(templateOverride)
            ? DefaultTemplate(format)
            : templateOverride;
        return template.Replace(LanguagePlaceholder, language, StringComparison.Ordinal);
    }
}
