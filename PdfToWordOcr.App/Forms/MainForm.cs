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

        btnSelectPdf.Click += BtnSelectPdf_Click;
        btnConvert.Click += BtnConvert_Click;
        btnCancel.Click += BtnCancel_Click;
        btnBrowseOutput.Click += BtnBrowseOutput_Click;
        btnOpenOutput.Click += BtnOpenOutput_Click;
        btnOpenFolder.Click += BtnOpenFolder_Click;
        btnSettings.Click += BtnSettings_Click;

        SetState(AppState.Idle);
    }

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
        chkContinueOnFailure.Enabled = inputsEnabled;
        btnSettings.Enabled = inputsEnabled;

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
            txtPdfPath.Text = dialog.FileName;
            lblPageCount.Text = $"{pageCount} page(s)";
            txtOutputPath.Text = Path.ChangeExtension(dialog.FileName, ".docx");

            AppendLog($"Loaded {Path.GetFileName(dialog.FileName)} ({pageCount} page(s)).");
            SetState(AppState.Ready);
        }
        catch (Exception ex)
        {
            _pdfPath = null;
            txtPdfPath.Text = string.Empty;
            lblPageCount.Text = string.Empty;
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
            ContinueOnPageFailure: chkContinueOnFailure.Checked);

        var progress = new Progress<PageProgress>(OnProgress);
        var pipeline = new ConversionPipeline(new OcrClient(SharedHttpClient, apiKey));

        try
        {
            var result = await pipeline.RunAsync(options, progress, _cts.Token);
            var failedSummary = result.FailedPages.Length == 0
                ? "none"
                : string.Join(", ", result.FailedPages);
            AppendLog($"Completed: {result.Pages} page(s) in {result.Elapsed:mm\\:ss}. Failed pages: {failedSummary}.");
            SetState(AppState.Completed);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelled.");
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
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
    }

    private void OnProgress(PageProgress progress)
    {
        progressBar.Maximum = Math.Max(progress.Total, 1);
        progressBar.Value = Math.Min(progress.Completed, progressBar.Maximum);
        lblStatus.Text = progress.Message;
        AppendLog(progress.Message);
    }

    private void BtnBrowseOutput_Click(object? sender, EventArgs e)
    {
        var defaultName = string.IsNullOrEmpty(txtOutputPath.Text) && _pdfPath is not null
            ? Path.ChangeExtension(_pdfPath, ".docx")
            : txtOutputPath.Text;

        using var dialog = new SaveFileDialog
        {
            Filter = "Word document (*.docx)|*.docx",
            FileName = defaultName,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtOutputPath.Text = dialog.FileName;
        }
    }

    private void BtnOpenOutput_Click(object? sender, EventArgs e)
    {
        if (File.Exists(txtOutputPath.Text))
        {
            Process.Start(new ProcessStartInfo(txtOutputPath.Text) { UseShellExecute = true });
        }
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        var folder = Path.GetDirectoryName(txtOutputPath.Text);
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
