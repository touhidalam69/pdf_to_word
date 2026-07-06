using PdfToWordOcr.App.Config;
using PdfToWordOcr.Core;

namespace PdfToWordOcr.App.Forms;

public partial class MainForm : Form
{
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

    public MainForm()
    {
        InitializeComponent();

        var defaults = AppSettings.LoadDefaults();
        cmbModel.Text = defaults.Model;
        numDpi.Value = defaults.Dpi;
        cmbLanguage.Text = defaults.Language;
        cmbFont.Text = defaults.Font;

        btnSelectPdf.Click += BtnSelectPdf_Click;

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

    private void AppendLog(string message)
    {
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }
}
