# PdfToWordOcr

A Windows desktop app that converts scanned PDFs — image-only, no text layer —
into editable `.docx` files, in the language you choose. OCR is performed
using the [Claude API](https://www.anthropic.com/api) (vision), not a local
OCR engine like Tesseract.

## Status

Implemented per [CLAUDE.md](CLAUDE.md): Core (rasterizer, OCR client, docx
writer, pipeline) and the WinForms app are both complete and build with zero
warnings.

## How it works

1. Select a scanned PDF and choose its language from a dropdown.
2. The app rasterizes each page to an image.
3. Each page image is sent to the Claude API for OCR, with a system prompt
   built from your selected language (no language is hardcoded — the app
   works the same way regardless of which one you pick).
4. Transcribed text is assembled into a `.docx` with proper complex-script
   font settings for the selected language, paragraph breaks, and page breaks
   matching the source PDF.

## Solution layout

```
PdfToWordOcr.slnx
├── PdfToWordOcr.Core/   # class library — rasterizer, OCR client, docx writer, pipeline
└── PdfToWordOcr.App/    # WinForms UI
```

## Requirements

- .NET 10.0 SDK
- Windows (WinForms UI)
- An Anthropic API key with access to the Claude API

## Configuration

The app resolves your Anthropic API key in this order:

1. `ANTHROPIC_API_KEY` environment variable
2. A DPAPI-encrypted key file at `%APPDATA%\PdfToWordOcr\key.dat`
   (created via the in-app Settings dialog)
3. If neither is present, the Settings dialog opens on startup and prompts for
   a key

The API key is never written to logs, `appsettings.json`, or any file in this
repository.

## Building

```
dotnet build PdfToWordOcr.slnx
```

## Running

```
dotnet run --project PdfToWordOcr.App
```

## Out of scope

- Local OCR fallback (Tesseract, etc.)
- Table/column/image layout reconstruction — plain paragraphs only
- Batch/multi-file queue
- Parallel page OCR
- Installer/MSIX packaging
- Automatic language detection — you always pick the language explicitly

## License

[MIT](LICENSE)
