namespace PdfToWordOcr.App.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Button btnSelectPdf;
    private TextBox txtPdfPath;
    private Label lblPageCount;

    private Label lblOutputPath;
    private TextBox txtOutputPath;
    private Button btnBrowseOutput;

    private Label lblModel;
    private ComboBox cmbModel;
    private Label lblDpi;
    private NumericUpDown numDpi;
    private Label lblLanguage;
    private ComboBox cmbLanguage;
    private Label lblFont;
    private ComboBox cmbFont;

    private CheckBox chkContinueOnFailure;
    private Label lblFormat;
    private ComboBox cmbFormat;
    private Label lblMode;
    private ComboBox cmbMode;
    private CheckBox chkPilot;
    private Label lblCostEstimate;

    private Button btnConvert;
    private Button btnCancel;
    private Button btnSettings;
    private Button btnResetJob;
    private Button btnOpenOutput;
    private Button btnOpenFolder;

    private ProgressBar progressBar;
    private Label lblStatus;
    private RichTextBox txtLog;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        btnSelectPdf = new Button();
        txtPdfPath = new TextBox();
        lblPageCount = new Label();

        lblOutputPath = new Label();
        txtOutputPath = new TextBox();
        btnBrowseOutput = new Button();

        lblModel = new Label();
        cmbModel = new ComboBox();
        lblDpi = new Label();
        numDpi = new NumericUpDown();
        lblLanguage = new Label();
        cmbLanguage = new ComboBox();
        lblFont = new Label();
        cmbFont = new ComboBox();

        chkContinueOnFailure = new CheckBox();
        lblFormat = new Label();
        cmbFormat = new ComboBox();
        lblMode = new Label();
        cmbMode = new ComboBox();
        chkPilot = new CheckBox();
        lblCostEstimate = new Label();

        btnConvert = new Button();
        btnCancel = new Button();
        btnSettings = new Button();
        btnResetJob = new Button();
        btnOpenOutput = new Button();
        btnOpenFolder = new Button();

        progressBar = new ProgressBar();
        lblStatus = new Label();
        txtLog = new RichTextBox();

        ((System.ComponentModel.ISupportInitialize)numDpi).BeginInit();
        SuspendLayout();

        // Row 1: PDF selection
        btnSelectPdf.Location = new Point(12, 12);
        btnSelectPdf.Size = new Size(100, 27);
        btnSelectPdf.Text = "Select PDF...";

        txtPdfPath.Location = new Point(120, 14);
        txtPdfPath.Size = new Size(548, 23);
        txtPdfPath.ReadOnly = true;
        txtPdfPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lblPageCount.Location = new Point(680, 18);
        lblPageCount.Size = new Size(180, 15);
        lblPageCount.Text = string.Empty;
        lblPageCount.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 2: Output path
        lblOutputPath.Location = new Point(12, 54);
        lblOutputPath.Size = new Size(60, 15);
        lblOutputPath.Text = "Output:";

        txtOutputPath.Location = new Point(80, 50);
        txtOutputPath.Size = new Size(588, 23);
        txtOutputPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        btnBrowseOutput.Location = new Point(680, 49);
        btnBrowseOutput.Size = new Size(90, 27);
        btnBrowseOutput.Text = "Browse...";
        btnBrowseOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 3: Model / DPI / Language / Font
        lblModel.Location = new Point(12, 94);
        lblModel.Size = new Size(60, 15);
        lblModel.Text = "Model:";

        cmbModel.Location = new Point(80, 90);
        cmbModel.Size = new Size(160, 23);
        cmbModel.DropDownStyle = ComboBoxStyle.DropDown;
        cmbModel.Items.AddRange(new object[] { "claude-sonnet-5", "claude-haiku-4-5", "claude-haiku-4-5-20251001" });

        lblDpi.Location = new Point(250, 94);
        lblDpi.Size = new Size(35, 15);
        lblDpi.Text = "DPI:";

        numDpi.Location = new Point(290, 90);
        numDpi.Size = new Size(70, 23);
        numDpi.Minimum = 72;
        numDpi.Maximum = 300;
        numDpi.Value = 150;

        lblLanguage.Location = new Point(376, 94);
        lblLanguage.Size = new Size(65, 15);
        lblLanguage.Text = "Language:";

        cmbLanguage.Location = new Point(446, 90);
        cmbLanguage.Size = new Size(140, 23);
        cmbLanguage.DropDownStyle = ComboBoxStyle.DropDown;
        cmbLanguage.Items.AddRange(new object[] { "English", "Bangla", "Hindi", "Urdu", "Arabic", "Tamil", "Nepali" });

        lblFont.Location = new Point(596, 94);
        lblFont.Size = new Size(35, 15);
        lblFont.Text = "Font:";

        cmbFont.Location = new Point(636, 90);
        cmbFont.Size = new Size(134, 23);
        cmbFont.DropDownStyle = ComboBoxStyle.DropDown;
        cmbFont.Items.AddRange(new object[] { "Nirmala UI", "Arial", "Times New Roman" });

        // Row 4: Format / Mode / Pilot / Continue on failure
        lblFormat.Location = new Point(12, 132);
        lblFormat.Size = new Size(60, 15);
        lblFormat.Text = "Format:";

        cmbFormat.Location = new Point(80, 128);
        cmbFormat.Size = new Size(160, 23);
        cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFormat.Items.AddRange(new object[] { "Word (.docx)", "Markdown (.md)" });

        lblMode.Location = new Point(250, 132);
        lblMode.Size = new Size(40, 15);
        lblMode.Text = "Mode:";

        cmbMode.Location = new Point(290, 128);
        cmbMode.Size = new Size(130, 23);
        cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMode.Items.AddRange(new object[] { "Auto", "Synchronous", "Batch" });

        chkPilot.Location = new Point(446, 128);
        chkPilot.Size = new Size(170, 24);
        chkPilot.Text = "Pilot (first 10 pages)";

        chkContinueOnFailure.Location = new Point(636, 128);
        chkContinueOnFailure.Size = new Size(220, 24);
        chkContinueOnFailure.Text = "Continue on page failure";
        chkContinueOnFailure.Checked = true;

        // Row 5: Cost estimate
        lblCostEstimate.Location = new Point(12, 162);
        lblCostEstimate.Size = new Size(758, 15);
        lblCostEstimate.Text = string.Empty;
        lblCostEstimate.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Row 6: Action buttons
        btnConvert.Location = new Point(12, 188);
        btnConvert.Size = new Size(100, 30);
        btnConvert.Text = "Start";

        btnCancel.Location = new Point(120, 188);
        btnCancel.Size = new Size(100, 30);
        btnCancel.Text = "Cancel";
        btnCancel.Enabled = false;

        btnSettings.Location = new Point(228, 188);
        btnSettings.Size = new Size(100, 30);
        btnSettings.Text = "Settings...";

        btnResetJob.Location = new Point(336, 188);
        btnResetJob.Size = new Size(100, 30);
        btnResetJob.Text = "Reset job";
        btnResetJob.Enabled = false;

        btnOpenOutput.Location = new Point(558, 188);
        btnOpenOutput.Size = new Size(110, 30);
        btnOpenOutput.Text = "Open File";
        btnOpenOutput.Enabled = false;
        btnOpenOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        btnOpenFolder.Location = new Point(674, 188);
        btnOpenFolder.Size = new Size(96, 30);
        btnOpenFolder.Text = "Open Folder";
        btnOpenFolder.Enabled = false;
        btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 7: Progress
        progressBar.Location = new Point(12, 232);
        progressBar.Size = new Size(660, 23);
        progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lblStatus.Location = new Point(680, 236);
        lblStatus.Size = new Size(180, 15);
        lblStatus.Text = "Idle";
        lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 8: Log
        txtLog.Location = new Point(12, 264);
        txtLog.Size = new Size(848, 328);
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.ReadOnly = true;
        txtLog.BackColor = SystemColors.Window;

        // Form
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(872, 604);
        MinimumSize = new Size(700, 430);
        Controls.Add(btnSelectPdf);
        Controls.Add(txtPdfPath);
        Controls.Add(lblPageCount);
        Controls.Add(lblOutputPath);
        Controls.Add(txtOutputPath);
        Controls.Add(btnBrowseOutput);
        Controls.Add(lblModel);
        Controls.Add(cmbModel);
        Controls.Add(lblDpi);
        Controls.Add(numDpi);
        Controls.Add(lblLanguage);
        Controls.Add(cmbLanguage);
        Controls.Add(lblFont);
        Controls.Add(cmbFont);
        Controls.Add(lblFormat);
        Controls.Add(cmbFormat);
        Controls.Add(lblMode);
        Controls.Add(cmbMode);
        Controls.Add(chkPilot);
        Controls.Add(chkContinueOnFailure);
        Controls.Add(lblCostEstimate);
        Controls.Add(btnConvert);
        Controls.Add(btnCancel);
        Controls.Add(btnSettings);
        Controls.Add(btnResetJob);
        Controls.Add(btnOpenOutput);
        Controls.Add(btnOpenFolder);
        Controls.Add(progressBar);
        Controls.Add(lblStatus);
        Controls.Add(txtLog);
        Text = "PdfToWordOcr";

        ((System.ComponentModel.ISupportInitialize)numDpi).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
