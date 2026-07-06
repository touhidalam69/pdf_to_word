# Task: Build "PdfToWordBangla" — WinForms app for scanned Bangla PDF → Word conversion

## What this app does
User selects a scanned PDF (image-only, no text layer, Bangla/Bengali content),
clicks Convert, and gets a .docx with the OCR'd Unicode Bangla text. OCR is done
via the Anthropic Messages API (vision) — NOT Tesseract.

## Solution structure — create exactly this

PdfToWordBangla.sln
├── PdfToWordBangla.Core/          # class library, net10.0, ZERO WinForms refs
│   ├── Models/
│   │   ├── ConversionOptions.cs   # record: InputPath, OutputPath, Model, Dpi, Font, ContinueOnPageFailure
│   │   ├── PageProgress.cs        # record: Completed, Total, Message
│   │   └── ConversionResult.cs    # record: OutputPath, Pages, FailedPages (int[]), Elapsed
│   ├── PdfRasterizer.cs
│   ├── OcrClient.cs
│   ├── DocxWriter.cs
│   └── ConversionPipeline.cs
└── PdfToWordBangla.App/           # WinForms exe, net10.0-windows
    ├── Forms/
    │   ├── MainForm.cs
    │   └── SettingsForm.cs
    └── Config/AppSettings.cs

## NuGet packages
Core: PDFtoImage (latest), DocumentFormat.OpenXml (latest)
App:  references Core only. Use System.Text.Json (built-in) — no Newtonsoft.

## Core implementation requirements

### PdfRasterizer.cs
- Input: PDF byte[] + DPI (default 150 — deliberate, do not raise: Claude
  downscales to ~1568px long edge anyway, and >150 risks the 5MB API limit).
- Use PDFtoImage.Conversion.GetPageCount / Conversion.ToImage(pdf, page: i, options: new(Dpi: dpi)).
- Encode each page: PNG first; if the PNG exceeds 5MB, re-encode as JPEG quality 88.
- Return per-page: byte[] data + media type ("image/png" or "image/jpeg").
- Expose as IEnumerable<PageImage> or async enumerable — do NOT load all pages
  into memory at once for large PDFs; yield one at a time and dispose SKImage/SKBitmap.

### OcrClient.cs
- Single long-lived HttpClient (injected or static) — never new HttpClient per call.
- POST https://api.anthropic.com/v1/messages
  Headers: x-api-key, anthropic-version: 2023-06-01
- Body:
  - model: from options (default "claude-sonnet-5")
  - max_tokens: 8000
  - system prompt (use verbatim):
    "You are an OCR engine for scanned Bangla (Bengali) documents. Transcribe
    every character exactly as printed, in Unicode Bangla. Preserve paragraph
    breaks (one blank line between paragraphs) and reading order. Do NOT
    translate, correct, normalize, summarize, or add commentary. Output ONLY
    the transcription. If the page is blank, output nothing."
  - messages: one user message containing an image block (base64, correct
    media_type) + text block "Transcribe this page."
- Parse response: concatenate all content blocks where type == "text".
- Retry policy: on 429, 529, 500, or HttpRequestException/timeout → retry up to
  3 times, delay 2^attempt seconds + random jitter (0–500ms). On final failure
  throw OcrPageException carrying the page number.
- Honor CancellationToken on every await.

### DocxWriter.cs
- Input: List<string> pageTexts (one entry per PDF page), font name, output path.
- CRITICAL — Bangla is a complex script. Every Run must set:
  - RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font }
  - FontSize { Val = "24" } AND FontSizeComplexScript { Val = "24" }  // 12pt
  - Languages { Val = "en-US", Bidi = "bn-BD" }
  Omitting ComplexScript/FontSizeComplexScript breaks Bangla rendering in Word —
  this is the #1 failure mode, do not skip it.
- Split each page's text on "\n\n" → separate Paragraphs. Single "\n" inside a
  paragraph → soft line break (Break element inside the Run).
- Text elements: Space = SpaceProcessingModeValues.Preserve.
- Insert a page break (Break { Type = BreakValues.Page }) between source pages,
  not after the last one.
- Pages that failed OCR (null/placeholder): write a visible marker paragraph
  "[[OCR FAILED — page N]]" so the user can fix manually.

### ConversionPipeline.cs
- Signature:
  Task<ConversionResult> RunAsync(ConversionOptions opt,
      IProgress<PageProgress> progress, CancellationToken ct)
- Flow: rasterize page → OCR → report progress → next page. Sequential is fine
  (do NOT parallelize in v1 — rate limits + ordering complexity not worth it).
- ct.ThrowIfCancellationRequested() between pages.
- If ContinueOnPageFailure: catch OcrPageException, record page number in
  FailedPages, insert placeholder text, continue. Else rethrow.
- After last page: DocxWriter.Write, return result with elapsed time.

## App (WinForms) implementation requirements

### MainForm
Controls:
- btnSelectPdf + read-only TextBox showing path + Label with page count
  (call GetPageCount immediately on selection to validate the file)
- txtOutputPath + browse button (SaveFileDialog, default = input name with .docx)
- cmbModel: "claude-sonnet-5", "claude-haiku-4-5" (editable dropdown)
- numDpi: 72–300, default 150
- cmbFont: "Nirmala UI", "SolaimanLipi", "Kalpurush" (editable)
- chkContinueOnFailure: checked by default
- btnConvert, btnCancel, btnSettings
- ProgressBar + status Label ("Page 4/12")
- RichTextBox log (append-only, auto-scroll)
- btnOpenOutput, btnOpenFolder (enabled only after success;
  Process.Start with UseShellExecute = true)

State machine — enforce strictly:
  Idle → Ready (valid PDF selected) → Running → Completed | Cancelled | Failed → Ready
- btnConvert enabled only in Ready. btnCancel enabled only in Running.
- All inputs disabled during Running.
- Centralize in a SetState(AppState) method — no scattered .Enabled flips.

Threading — non-negotiable:
- async void only on event handlers; everything else async Task.
- Create Progress<PageProgress> ON the UI thread (constructor captures
  SynchronizationContext) and pass to pipeline. Never touch controls from Core.
- CancellationTokenSource: field, created per run, disposed in finally,
  btnCancel calls .Cancel().
- Wrap RunAsync in try/catch: OperationCanceledException → Cancelled state;
  Exception → Failed state + message in log. finally → re-enable UI.
- Guard against re-entrancy: if a run is active, btnConvert does nothing.

### SettingsForm + AppSettings
- API key resolution order:
  1. ANTHROPIC_API_KEY environment variable
  2. DPAPI-encrypted file %APPDATA%\PdfToWordBangla\key.dat
     (ProtectedData.Protect/Unprotect, DataProtectionScope.CurrentUser)
  3. If neither → open SettingsForm on startup and require entry
- SettingsForm: masked TextBox for key, Save encrypts to key.dat.
  NEVER write the key to appsettings.json, logs, or plaintext anywhere.
- appsettings.json (optional): default model/dpi/font only.

### Project/csproj details
- App: <TargetFramework>net10.0-windows</TargetFramework>, <UseWindowsForms>true</UseWindowsForms>
- App manifest: PerMonitorV2 DPI awareness (blurry on 4K otherwise).
- ApplicationConfiguration.Initialize() in Program.cs (default template).

## Acceptance checklist — verify each before finishing
1. Solution builds with zero warnings on net10.0.
2. UI stays responsive during conversion (no Invoke deadlocks, no frozen window).
3. Cancel mid-run → Cancelled state within one page, UI re-enabled, no orphan tasks.
4. Output .docx opens in Word: Bangla text renders in the chosen font at 12pt,
   conjuncts (যুক্তবর্ণ) intact, page breaks between source pages.
5. Kill network mid-run with ContinueOnFailure on → placeholders written,
   FailedPages populated, app doesn't crash.
6. API key survives app restart via DPAPI file; key never appears in any file in
   the repo or output.
7. Selecting a non-PDF or corrupt file → friendly error, state returns to Idle.

## Explicitly out of scope (do not build)
- Tesseract or any local OCR fallback
- Table/column/image layout reconstruction (plain paragraphs only)
- Batch/multi-file queue
- Parallel page OCR
- Installer/MSIX (plain build output is fine)

Work through Core first (rasterizer → OCR client → docx writer → pipeline),
then the WinForms shell. Ask nothing — all decisions are specified above.