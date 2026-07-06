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

    private Button btnConvert;
    private Button btnCancel;
    private Button btnSettings;
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

        btnConvert = new Button();
        btnCancel = new Button();
        btnSettings = new Button();
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
        cmbModel.Items.AddRange(new object[] { "claude-sonnet-5", "claude-haiku-4-5" });

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

        // Row 4: Continue on failure
        chkContinueOnFailure.Location = new Point(12, 128);
        chkContinueOnFailure.Size = new Size(220, 24);
        chkContinueOnFailure.Text = "Continue on page failure";
        chkContinueOnFailure.Checked = true;

        // Row 5: Action buttons
        btnConvert.Location = new Point(12, 164);
        btnConvert.Size = new Size(100, 30);
        btnConvert.Text = "Convert";

        btnCancel.Location = new Point(120, 164);
        btnCancel.Size = new Size(100, 30);
        btnCancel.Text = "Cancel";
        btnCancel.Enabled = false;

        btnSettings.Location = new Point(228, 164);
        btnSettings.Size = new Size(100, 30);
        btnSettings.Text = "Settings...";

        btnOpenOutput.Location = new Point(558, 164);
        btnOpenOutput.Size = new Size(110, 30);
        btnOpenOutput.Text = "Open File";
        btnOpenOutput.Enabled = false;
        btnOpenOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        btnOpenFolder.Location = new Point(674, 164);
        btnOpenFolder.Size = new Size(96, 30);
        btnOpenFolder.Text = "Open Folder";
        btnOpenFolder.Enabled = false;
        btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 6: Progress
        progressBar.Location = new Point(12, 208);
        progressBar.Size = new Size(660, 23);
        progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lblStatus.Location = new Point(680, 212);
        lblStatus.Size = new Size(180, 15);
        lblStatus.Text = "Idle";
        lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Row 7: Log
        txtLog.Location = new Point(12, 240);
        txtLog.Size = new Size(848, 328);
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.ReadOnly = true;
        txtLog.BackColor = SystemColors.Window;

        // Form
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(872, 580);
        MinimumSize = new Size(700, 400);
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
        Controls.Add(chkContinueOnFailure);
        Controls.Add(btnConvert);
        Controls.Add(btnCancel);
        Controls.Add(btnSettings);
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
