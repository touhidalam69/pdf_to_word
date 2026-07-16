# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Batch mode for very large scanned PDFs (1000+ pages) using the Anthropic
  Message Batches API at 50% of synchronous token pricing. Auto mode picks
  batch above 50 pages; a manual mode override is available.
- Filesystem checkpointing and resume: every stage writes to a
  `{input}.pdf.work\` directory beside the input, so a killed or cancelled
  run continues where it left off — including collecting batches that were
  submitted but never downloaded. A "Reset job" button discards saved
  progress.
- Markdown (`.md`) output as a second format alongside Word, with
  structure-preserving OCR prompts (headings, lists, GitHub tables) and
  `<!-- page N -->` markers.
- Pilot mode: run the full pipeline on the first 10 pages only
  (`*.pilot.md` / `*.pilot.docx`); pilot results are reused by the full run.
- Failure tracking and retry: failed pages are retried for up to three
  cycles (synchronously below 25 pages, otherwise in a new batch); pages
  that still fail are reported by number and block stitching until resolved.
- Cost estimate label, output-format and processing-mode selectors, and a
  warning when the selected PDF looks like a digital (non-scanned) document.
- Editable OCR prompt templates in Settings (per output format, with a
  `{LANGUAGE}` placeholder), stored in `%APPDATA%\PdfToWordOcr\settings.json`.
- Unit test project (`PdfToWordOcr.Tests`) covering batch result parsing,
  Markdown stitching, chunk boundaries, and kill/resume behavior; CI now
  runs `dotnet test`.
- Initial implementation: `PdfToWordOcr.Core` (PDF rasterization, Claude
  vision OCR client, complex-script-safe `.docx` writer, conversion
  pipeline) and `PdfToWordOcr.App` (WinForms UI with language/model/DPI/font
  selection, cancellable conversion with progress reporting, and
  DPAPI-encrypted API key storage).
- Open-source project scaffolding: CI build workflow, issue/PR templates,
  contributing guide, code of conduct, and security policy.
