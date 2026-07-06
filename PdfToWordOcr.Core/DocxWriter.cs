using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PdfToWordOcr.Core;

public static class DocxWriter
{
    private static readonly Dictionary<string, string> LanguageBidiTags =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["English"] = "en-US",
            ["Bangla"] = "bn-BD",
            ["Hindi"] = "hi-IN",
            ["Urdu"] = "ur-PK",
            ["Arabic"] = "ar-SA",
            ["Tamil"] = "ta-IN",
            ["Nepali"] = "ne-NP",
        };

    public static void Write(IReadOnlyList<string?> pageTexts, string font, string language, string outputPath)
    {
        var bidiTag = LanguageBidiTags.TryGetValue(language, out var tag) ? tag : "en-US";

        using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        for (var i = 0; i < pageTexts.Count; i++)
        {
            var pageContent = pageTexts[i] ?? $"[[OCR FAILED — page {i + 1}]]";
            AppendPage(body, pageContent, font, bidiTag, isLastPage: i == pageTexts.Count - 1);
        }

        mainPart.Document.Save();
    }

    private static void AppendPage(Body body, string pageText, string font, string bidiTag, bool isLastPage)
    {
        Paragraph? lastParagraph = null;

        foreach (var paragraphText in pageText.Split("\n\n"))
        {
            var paragraph = new Paragraph();
            var lines = paragraphText.Split('\n');

            for (var l = 0; l < lines.Length; l++)
            {
                var run = CreateRun(font, bidiTag);
                run.AppendChild(new Text(lines[l]) { Space = SpaceProcessingModeValues.Preserve });
                if (l < lines.Length - 1)
                {
                    run.AppendChild(new Break());
                }

                paragraph.AppendChild(run);
            }

            body.AppendChild(paragraph);
            lastParagraph = paragraph;
        }

        if (!isLastPage && lastParagraph is not null)
        {
            var pageBreakRun = CreateRun(font, bidiTag);
            pageBreakRun.AppendChild(new Break { Type = BreakValues.Page });
            lastParagraph.AppendChild(pageBreakRun);
        }
    }

    private static Run CreateRun(string font, string bidiTag) =>
        new(
            new RunProperties(
                new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font },
                new FontSize { Val = "24" },
                new FontSizeComplexScript { Val = "24" },
                new Languages { Val = "en-US", Bidi = bidiTag }));
}
