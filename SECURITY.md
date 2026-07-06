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
- The Anthropic API key is resolved from the `ANTHROPIC_API_KEY`
  environment variable or a DPAPI-encrypted file at
  `%APPDATA%\PdfToWordOcr\key.dat` (`CurrentUser` scope). It is never
  written to logs, `appsettings.json`, or any other plaintext file. If you
  find a code path where it is, that's a valid report.
- PDF content you convert is sent to the Anthropic API for OCR and is not
  otherwise transmitted or stored outside your machine by this app.
