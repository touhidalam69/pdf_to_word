using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using PdfToWordOcr.App.Config;
using PdfToWordOcr.Core;
using PdfToWordOcr.Core.Models;

namespace PdfToWordOcr.App.Forms;

public partial class MainForm : Form
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    /// <summary>Auto mode switches to Batch above this page count.</summary>
    private const int AutoBatchThreshold = 50;

    /// <summary>PDFs averaging less than this per page probably already have a text layer.</summary>
    private const int DigitalPdfBytesPerPage = 15 * 1024;

    // Rough per-page token estimate for a 1568px scan sent for OCR.
    private const int EstimatedInputTokensPerPage = 1600;
    private const int EstimatedOutputTokensPerPage = 1000;

    // Batch API prices in USD per million tokens (50% of synchronous prices).
    // UPDATE ME when Anthropic pricing changes: https://claude.com/pricing
    private static readonly (string Match, decimal BatchInput, decimal BatchOutput)[] BatchPrices =
    [
        ("haiku", 0.50m, 2.50m),
        ("sonnet", 1.50m, 7.50m),
    ];

    private enum AppState
    {
        Idle,
        Ready,
        Running,
        Completed,
        Cancelled,
        Failed,
    }

    private string? _pdfPath;
    private int _pageCount;
    private string? _lastOutputPath;
    private bool _activeJobIsBatch;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public MainForm()
    {
        InitializeComponent();

        var defaults = AppSettings.LoadDefaults();
        cmbModel.Text = defaults.Model;
        numDpi.Value = defaults.Dpi;
        cmbLanguage.Text = defaults.Language;
        cmbFont.Text = defaults.Font;
        cmbFormat.SelectedIndex = 0;
        cmbMode.SelectedIndex = 0;

        btnSelectPdf.Click += BtnSelectPdf_Click;
        btnConvert.Click += BtnConvert_Click;
        btnCancel.Click += BtnCancel_Click;
        btnBrowseOutput.Click += BtnBrowseOutput_Click;
        btnOpenOutput.Click += BtnOpenOutput_Click;
        btnOpenFolder.Click += BtnOpenFolder_Click;
        btnSettings.Click += BtnSettings_Click;
        btnResetJob.Click += BtnResetJob_Click;
        cmbFormat.SelectedIndexChanged += (_, _) => OnConversionSetupChanged(updateOutputExtension: true);
        cmbMode.SelectedIndexChanged += (_, _) => OnConversionSetupChanged(updateOutputExtension: false);
        cmbModel.TextChanged += (_, _) => UpdateCostEstimate();
        chkPilot.CheckedChanged += (_, _) => UpdateCostEstimate();
        FormClosing += MainForm_FormClosing;

        SetState(AppState.Idle);
    }

    private OutputFormat SelectedFormat =>
        cmbFormat.SelectedIndex == 1 ? OutputFormat.Markdown : OutputFormat.Word;

    private ProcessingMode SelectedMode => cmbMode.SelectedIndex switch
    {
        1 => ProcessingMode.Synchronous,
        2 => ProcessingMode.Batch,
        _ => ProcessingMode.Auto,
    };

    /// <summary>
    /// Resolves Auto by page count. Markdown always runs through the batch
    /// pipeline — it is built on the work directory.
    /// </summary>
    private bool UseBatchPipeline =>
        SelectedFormat == OutputFormat.Markdown
        || SelectedMode == ProcessingMode.Batch
        || (SelectedMode == ProcessingMode.Auto && _pageCount > AutoBatchThreshold);

    private void SetState(AppState state)
    {
        var inputsEnabled = state != AppState.Running;
        btnSelectPdf.Enabled = inputsEnabled;
        txtOutputPath.Enabled = inputsEnabled;
        btnBrowseOutput.Enabled = inputsEnabled;
        cmbModel.Enabled = inputsEnabled;
        numDpi.Enabled = inputsEnabled;
        cmbLanguage.Enabled = inputsEnabled;
        cmbFont.Enabled = inputsEnabled;
        cmbFormat.Enabled = inputsEnabled;
        cmbMode.Enabled = inputsEnabled;
        chkPilot.Enabled = inputsEnabled;
        chkContinueOnFailure.Enabled = inputsEnabled;
        btnSettings.Enabled = inputsEnabled;
        btnResetJob.Enabled = inputsEnabled && _pdfPath is not null;

        btnConvert.Enabled = state is AppState.Ready or AppState.Completed or AppState.Cancelled or AppState.Failed
            && _pdfPath is not null;
        btnCancel.Enabled = state == AppState.Running;
        btnOpenOutput.Enabled = state == AppState.Completed;
        btnOpenFolder.Enabled = state == AppState.Completed;
    }

    private void BtnSelectPdf_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var pdfBytes = File.ReadAllBytes(dialog.FileName);
            var pageCount = PdfRasterizer.GetPageCount(pdfBytes);

            _pdfPath = dialog.FileName;
            _pageCount = pageCount;
            txtPdfPath.Text = dialog.FileName;
            lblPageCount.Text = $"{pageCount} page(s)";
            txtOutputPath.Text = Path.ChangeExtension(dialog.FileName, OutputExtension());

            AppendLog($"Loaded {Path.GetFileName(dialog.FileName)} ({pageCount} page(s)).");

            if (pageCount > 0 && pdfBytes.LongLength / pageCount < DigitalPdfBytesPerPage)
            {
                AppendLog("Warning: this PDF is very small per page — it may already contain a text layer.");
                MessageBox.Show(
                    this,
                    "This PDF averages less than 15 KB per page, which usually means it is a digital PDF "
                    + "with a selectable text layer rather than a scan. OCR will still run, but a direct "
                    + "text export would be cheaper and more accurate.",
                    "Possibly not a scanned PDF",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            UpdateStartButton();
            UpdateCostEstimate();
            SetState(AppState.Ready);
        }
        catch (Exception ex)
        {
            _pdfPath = null;
            _pageCount = 0;
            txtPdfPath.Text = string.Empty;
            lblPageCount.Text = string.Empty;
            lblCostEstimate.Text = string.Empty;
            MessageBox.Show(
                this,
                $"Could not open this file as a PDF:\n{ex.Message}",
                "Invalid PDF",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SetState(AppState.Idle);
        }
    }

    private async void BtnConvert_Click(object? sender, EventArgs e)
    {
        if (_isRunning || _pdfPath is null)
        {
            return;
        }

        var apiKey = AppSettings.TryGetApiKey();
        if (apiKey is null)
        {
            MessageBox.Show(this, "No API key is configured. Open Settings to add one.", "Missing API Key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        _activeJobIsBatch = UseBatchPipeline;
        _cts = new CancellationTokenSource();
        progressBar.Value = 0;
        lblStatus.Text = "Starting...";
        SetState(AppState.Running);

        var options = new ConversionOptions(
            InputPath: _pdfPath,
            OutputPath: txtOutputPath.Text,
            Model: cmbModel.Text,
            Dpi: (int)numDpi.Value,
            Font: cmbFont.Text,
            Language: cmbLanguage.Text,
            ContinueOnPageFailure: chkContinueOnFailure.Checked,
            Format: SelectedFormat,
            Mode: SelectedMode,
            Pilot: chkPilot.Checked);

        var progress = new Progress<PageProgress>(OnProgress);
        var syncClient = new OcrClient(SharedHttpClient, apiKey);

        try
        {
            ConversionResult result;
            if (_activeJobIsBatch)
            {
                AppendLog(SelectedMode == ProcessingMode.Synchronous && SelectedFormat == OutputFormat.Markdown
                    ? "Markdown output always uses the batch pipeline."
                    : "Using batch mode (Message Batches API, 50% cheaper).");
                var pipeline = new BatchConversionPipeline(syncClient, new BatchOcrClient(SharedHttpClient, apiKey));
                result = await pipeline.RunAsync(options, progress, _cts.Token);
            }
            else
            {
                result = await new ConversionPipeline(syncClient).RunAsync(options, progress, _cts.Token);
            }

            if (_activeJobIsBatch && result.FailedPages.Length > 0)
            {
                AppendLog($"{result.FailedPages.Length} page(s) permanently failed: "
                    + $"{string.Join(", ", result.FailedPages)}. Output was not written — press Start to retry them.");
                SetState(AppState.Failed);
            }
            else
            {
                _lastOutputPath = result.OutputPath;
                var failedSummary = result.FailedPages.Length == 0
                    ? "none"
                    : string.Join(", ", result.FailedPages);
                AppendLog($"Completed: {result.Pages} page(s) in {result.Elapsed:mm\\:ss}. Failed pages: {failedSummary}.");
                SetState(AppState.Completed);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(_activeJobIsBatch
                ? "Cancelled. Progress is saved in the work directory — press Start to resume."
                : "Cancelled.");
            SetState(AppState.Cancelled);
        }
        catch (Exception ex)
        {
            AppendLog($"Failed: {ex.Message}");
            SetState(AppState.Failed);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _isRunning = false;
            UpdateStartButton();
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
    }

    private void BtnResetJob_Click(object? sender, EventArgs e)
    {
        if (_pdfPath is null || _isRunning)
        {
            return;
        }

        var workspace = new JobWorkspace(_pdfPath);
        if (!workspace.HasAnyProgress())
        {
            AppendLog("No saved job progress to reset.");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Delete all saved progress for this PDF?\n\n{workspace.Root}\n\n"
            + "Rasterized pages and collected OCR text will be lost, and the next run starts from scratch.",
            "Reset job",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        workspace.Delete();
        AppendLog("Work directory deleted — the next run starts from scratch.");
        UpdateStartButton();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        var message = _activeJobIsBatch
            ? "A batch job is still running. Batches already submitted keep processing on Anthropic's "
              + "servers and will be collected the next time you press Start.\n\nClose anyway?"
            : "A conversion is still running and will be aborted.\n\nClose anyway?";
        var answer = MessageBox.Show(this, message, "Job in progress",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (answer == DialogResult.Yes)
        {
            _cts?.Cancel();
        }
        else
        {
            e.Cancel = true;
        }
    }

    private void OnProgress(PageProgress progress)
    {
        progressBar.Maximum = Math.Max(progress.Total, 1);
        progressBar.Value = Math.Min(progress.Completed, progressBar.Maximum);
        lblStatus.Text = $"{progress.Completed}/{progress.Total}";
        AppendLog(progress.Message);
    }

    private void OnConversionSetupChanged(bool updateOutputExtension)
    {
        if (updateOutputExtension && !string.IsNullOrEmpty(txtOutputPath.Text))
        {
            txtOutputPath.Text = Path.ChangeExtension(txtOutputPath.Text, OutputExtension());
        }

        UpdateStartButton();
        UpdateCostEstimate();
    }

    private string OutputExtension() => SelectedFormat == OutputFormat.Markdown ? ".md" : ".docx";

    private void UpdateStartButton()
    {
        var canResume = _pdfPath is not null
            && UseBatchPipeline
            && new JobWorkspace(_pdfPath).HasAnyProgress();
        btnConvert.Text = canResume ? "Resume" : "Start";
    }

    private void UpdateCostEstimate()
    {
        if (_pageCount == 0)
        {
            lblCostEstimate.Text = string.Empty;
            return;
        }

        var price = BatchPrices.FirstOrDefault(p =>
            cmbModel.Text.Contains(p.Match, StringComparison.OrdinalIgnoreCase));
        if (price.Match is null)
        {
            lblCostEstimate.Text = "Cost estimate: unknown model pricing.";
            return;
        }

        var pages = chkPilot.Checked ? Math.Min(BatchConversionPipeline.PilotPageCount, _pageCount) : _pageCount;
        var multiplier = UseBatchPipeline ? 1m : 2m; // synchronous API costs 2x batch
        var baseCost = pages
            * (EstimatedInputTokensPerPage * price.BatchInput + EstimatedOutputTokensPerPage * price.BatchOutput)
            * multiplier / 1_000_000m;

        var modeLabel = UseBatchPipeline ? "batch" : "synchronous";
        lblCostEstimate.Text =
            $"Estimated OCR cost ({modeLabel}, {pages} page(s)): ${baseCost * 0.7m:0.00}–${baseCost * 1.5m:0.00} (rough estimate)";
    }

    private void BtnBrowseOutput_Click(object? sender, EventArgs e)
    {
        var defaultName = string.IsNullOrEmpty(txtOutputPath.Text) && _pdfPath is not null
            ? Path.ChangeExtension(_pdfPath, OutputExtension())
            : txtOutputPath.Text;

        var filter = SelectedFormat == OutputFormat.Markdown
            ? "Markdown (*.md)|*.md"
            : "Word document (*.docx)|*.docx";

        using var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultName,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtOutputPath.Text = dialog.FileName;
        }
    }

    private void BtnOpenOutput_Click(object? sender, EventArgs e)
    {
        var path = _lastOutputPath ?? txtOutputPath.Text;
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        var folder = Path.GetDirectoryName(_lastOutputPath ?? txtOutputPath.Text);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm();
        settingsForm.ShowDialog(this);
    }

    private void AppendLog(string message)
    {
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }
}
