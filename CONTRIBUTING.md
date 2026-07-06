# Contributing

Thanks for considering a contribution to PdfToWordOcr.

## Getting set up

- .NET 10.0 SDK
- Windows (the app is WinForms, Windows-only)
- An Anthropic API key if you want to exercise actual OCR conversions

```
dotnet build PdfToWordOcr.slnx
dotnet run --project PdfToWordOcr.App
```

Set your key via the `ANTHROPIC_API_KEY` environment variable, or enter it
in the app's Settings dialog on first run (it's encrypted with DPAPI and
stored under `%APPDATA%\PdfToWordOcr\`).

## Project layout

- `PdfToWordOcr.Core` — rasterization, OCR client, docx writer, pipeline.
  No WinForms references; keep it that way.
- `PdfToWordOcr.App` — the WinForms UI. Should only reference Core, plus
  whatever WinForms/BCL APIs it needs directly.

A couple of design choices that look arbitrary but aren't:

- DPI defaults to 150, not higher. Claude downscales images to ~1568px on
  the long edge anyway, and raising DPI mainly risks tripping the 5MB
  per-image API limit for no OCR quality benefit.
- Every OpenXML `Run` sets `RunFonts.ComplexScript`, `FontSizeComplexScript`,
  and a `Languages.Bidi` tag unconditionally, even for Latin-script text.
  Omitting these is the #1 cause of broken complex-script rendering
  (conjuncts, ligatures) in Word, and setting them doesn't harm non-complex
  scripts, so there's no need for per-language branching in `DocxWriter`.

## Before opening a PR

- `dotnet build PdfToWordOcr.slnx` should succeed with **zero warnings** —
  this is a hard requirement, not a suggestion.
- Manually exercise whatever you changed. There's no automated test suite
  yet, so a build passing isn't the same as the feature working.
- Never commit an API key, a `key.dat` file, or a scanned document that
  contains real/sensitive content.
- Keep the scope of a PR focused. If you find something else worth fixing
  along the way, open a separate issue or PR for it.

## What's explicitly out of scope

See the "Out of scope" section in [README.md](README.md#out-of-scope) —
things like a local OCR fallback, layout reconstruction, batch processing,
and an installer are intentionally not planned. If you want to make a case
for one of these, open an issue first to discuss before sending a PR.

## Reporting bugs / requesting features

Use the issue templates — they'll prompt you for the details that are
actually useful for triage (language/DPI/model used, log output, etc.).

## Security

Please see [SECURITY.md](SECURITY.md) for how to report vulnerabilities —
do not open a public issue for security-sensitive reports.
