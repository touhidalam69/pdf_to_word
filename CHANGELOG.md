# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Initial implementation: `PdfToWordOcr.Core` (PDF rasterization, Claude
  vision OCR client, complex-script-safe `.docx` writer, conversion
  pipeline) and `PdfToWordOcr.App` (WinForms UI with language/model/DPI/font
  selection, cancellable conversion with progress reporting, and
  DPAPI-encrypted API key storage).
- Open-source project scaffolding: CI build workflow, issue/PR templates,
  contributing guide, code of conduct, and security policy.
