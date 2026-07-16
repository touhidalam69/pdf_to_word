# PdfToWordOcr

[![Build](https://github.com/touhidalam69/pdf_to_word/actions/workflows/build.yml/badge.svg)](https://github.com/touhidalam69/pdf_to_word/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A Windows desktop app that converts scanned PDFs — image-only, no text layer —
into editable `.docx` or `.md` files, in the language you choose. OCR is
performed using the [Claude API](https://www.anthropic.com/api) (vision), not
a local OCR engine like Tesseract.

## Status

Core (rasterizer, OCR clients, docx/Markdown writers, pipelines) and the
WinForms app are complete and build with zero warnings. Very large PDFs
(1000+ pages) are supported through a resumable batch mode.

## How it works

1. Select a scanned PDF and choose its language from a dropdown.
2. The app rasterizes each page to an image.
3. Each page image is sent to the Claude API for OCR, with a prompt built
   from your selected language (no language is hardcoded — the app works the
   same way regardless of which one you pick).
4. Transcribed text is assembled into a `.docx` (with proper complex-script
   font settings, paragraph breaks, and page breaks) or a `.md` (with
   headings, lists, and tables preserved).

Small PDFs are converted synchronously, page by page. Above 50 pages (or on
request), the app switches to **batch mode**.

## Large PDFs — batch mode, resume, and pilot

Batch mode submits pages to the
[Message Batches API](https://platform.claude.com/docs/en/build-with-claude/batch-processing)
in chunks of 200 — half the token price of synchronous calls, and built for
exactly this workload.

- **Checkpointing:** all progress lives in a `{input}.pdf.work\` directory
  next to the input (rasterized pages, per-page OCR text, a job manifest, a
  log). Killing the app at any point loses nothing: submitted batches keep
  processing on Anthropic's servers and are collected the next time you
  press Start. The Start button reads **Resume** when saved progress exists,
  and **Reset job** discards it.
- **Pilot mode:** convert only the first 10 pages (`*.pilot.md` /
  `*.pilot.docx`) to check OCR quality and prompt fit cheaply before
  committing to the whole book. Pilot results are reused by the full run.
- **Retry:** pages that fail (truncation, refusals, API errors) are retried
  for up to three cycles. Pages that still fail are reported by number, and
  the output is not written until they succeed or you reset the job.
- **Cost estimate:** a rough per-run estimate is shown after selecting a
  PDF, based on current batch pricing for the selected model.

## Solution layout

```
PdfToWordOcr.slnx
├── PdfToWordOcr.Core/    # class library — rasterizer, OCR clients, writers, pipelines
├── PdfToWordOcr.App/     # WinForms UI
└── PdfToWordOcr.Tests/   # xunit tests (batch parsing, stitching, resume)
```

## Requirements

- .NET 10.0 SDK
- Windows (WinForms UI)
- An Anthropic API key with access to the Claude API

## Configuration

The app resolves your Anthropic API key in this order:

1. `PdfToWordOcr.App/appsettings.local.json` — copy
   `appsettings.local.json.example` to `appsettings.local.json` and fill in
   `apiKey`. This file is gitignored and never committed.
2. `ANTHROPIC_API_KEY` environment variable
3. A DPAPI-encrypted key file at `%APPDATA%\PdfToWordOcr\key.dat`
   (created via the in-app Settings dialog — encrypted, unlike option 1)
4. If none of the above are present, the Settings dialog opens on startup
   and prompts for a key

The API key is never written to logs or to the tracked `appsettings.json`,
and never appears anywhere in this repository.

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
- Layout reconstruction beyond Markdown structure — no column/image layout
- Multi-file queue (batch mode covers one large PDF at a time)
- Parallel synchronous OCR (batch mode already parallelizes server-side)
- Installer/MSIX packaging
- Automatic language detection — you always pick the language explicitly

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for how
to get set up and what to check before opening a PR. Please also read the
[Code of Conduct](CODE_OF_CONDUCT.md). Security issues should go through
[SECURITY.md](SECURITY.md) instead of a public issue.

## License

[MIT](LICENSE)
