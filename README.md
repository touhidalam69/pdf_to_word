# PdfToWordBangla

A Windows desktop app that converts scanned Bangla (Bengali) PDFs — image-only,
no text layer — into editable `.docx` files. OCR is performed using the
[Claude API](https://www.anthropic.com/api) (vision), not a local OCR engine
like Tesseract.

## Status

Specification complete ([CLAUDE.md](CLAUDE.md)); implementation in progress.

## How it works

1. Select a scanned Bangla PDF.
2. The app rasterizes each page to an image.
3. Each page image is sent to the Claude API for OCR, with a system prompt
   tuned to transcribe Bangla script exactly (no translation, no summarizing).
4. Transcribed text is assembled into a `.docx` with proper Bangla/complex-script
   font settings, paragraph breaks, and page breaks matching the source PDF.

## Solution layout

```
PdfToWordBangla.sln
├── PdfToWordBangla.Core/   # class library — rasterizer, OCR client, docx writer, pipeline
└── PdfToWordBangla.App/    # WinForms UI
```

## Requirements

- .NET 10.0 SDK
- Windows (WinForms UI)
- An Anthropic API key with access to the Claude API

## Configuration

The app resolves your Anthropic API key in this order:

1. `ANTHROPIC_API_KEY` environment variable
2. A DPAPI-encrypted key file at `%APPDATA%\PdfToWordBangla\key.dat`
   (created via the in-app Settings dialog)
3. If neither is present, the Settings dialog opens on startup and prompts for
   a key

The API key is never written to logs, `appsettings.json`, or any file in this
repository.

## Building

```
dotnet build PdfToWordBangla.sln
```

## Running

```
dotnet run --project PdfToWordBangla.App
```

## Out of scope

- Local OCR fallback (Tesseract, etc.)
- Table/column/image layout reconstruction — plain paragraphs only
- Batch/multi-file queue
- Parallel page OCR
- Installer/MSIX packaging

## License

[MIT](LICENSE)
