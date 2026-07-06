# Security Policy

## Supported versions

This project doesn't yet have tagged releases — only the `main` branch is
supported. Security fixes will land there.

## Reporting a vulnerability

Please do **not** open a public GitHub issue for security vulnerabilities.

Instead, email **touhidalam69@gmail.com** with a description of the issue
and steps to reproduce. Please allow a reasonable amount of time to
respond and address the report before any public disclosure.

## Scope notes

- The app talks to the Anthropic API (`api.anthropic.com`) over HTTPS to
  perform OCR. No other network calls are made.
- The Anthropic API key is resolved, in order, from: an untracked
  `appsettings.local.json` next to the app (gitignored — see
  `appsettings.local.json.example`), the `ANTHROPIC_API_KEY` environment
  variable, or a DPAPI-encrypted file at `%APPDATA%\PdfToWordOcr\key.dat`
  (`CurrentUser` scope, via the Settings dialog). It is never written to
  logs or to the tracked `appsettings.json`. If you find a code path where
  it is, that's a valid report.
- `appsettings.local.json` is plaintext on disk (unlike the DPAPI-encrypted
  Settings-dialog option) — it's convenient for local development but
  offers no protection if something else with filesystem access to your
  machine can read it. Prefer the Settings dialog if that matters to you.
- PDF content you convert is sent to the Anthropic API for OCR and is not
  otherwise transmitted or stored outside your machine by this app.
