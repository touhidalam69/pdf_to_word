namespace PdfToWordOcr.App.Forms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblInstructions;
    private Label lblApiKey;
    private TextBox txtApiKey;
    private Label lblPromptHeader;
    private Label lblWordTemplate;
    private TextBox txtWordTemplate;
    private Label lblMarkdownTemplate;
    private TextBox txtMarkdownTemplate;
    private Button btnResetPrompts;
    private Button btnSave;
    private Button btnCancel;

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
        lblInstructions = new Label();
        lblApiKey = new Label();
        txtApiKey = new TextBox();
        lblPromptHeader = new Label();
        lblWordTemplate = new Label();
        txtWordTemplate = new TextBox();
        lblMarkdownTemplate = new Label();
        txtMarkdownTemplate = new TextBox();
        btnResetPrompts = new Button();
        btnSave = new Button();
        btnCancel = new Button();
        SuspendLayout();

        lblInstructions.AutoSize = true;
        lblInstructions.Location = new Point(12, 12);
        lblInstructions.MaximumSize = new Size(560, 0);
        lblInstructions.Text = "Enter your Anthropic API key. It is encrypted and stored locally, and is " +
            "never written to logs or configuration files. Leave blank to keep the current key.";

        lblApiKey.AutoSize = true;
        lblApiKey.Location = new Point(12, 64);
        lblApiKey.Text = "API key:";

        txtApiKey.Location = new Point(12, 84);
        txtApiKey.Size = new Size(560, 23);
        txtApiKey.UseSystemPasswordChar = true;

        lblPromptHeader.AutoSize = true;
        lblPromptHeader.Location = new Point(12, 124);
        lblPromptHeader.MaximumSize = new Size(560, 0);
        lblPromptHeader.Text = "OCR prompt templates — {LANGUAGE} is replaced with the selected language. " +
            "The [BLANK] sentinel marks pages with no legible text.";

        lblWordTemplate.AutoSize = true;
        lblWordTemplate.Location = new Point(12, 160);
        lblWordTemplate.Text = "Word (plain paragraphs) prompt:";

        txtWordTemplate.Location = new Point(12, 180);
        txtWordTemplate.Size = new Size(560, 110);
        txtWordTemplate.Multiline = true;
        txtWordTemplate.ScrollBars = ScrollBars.Vertical;
        txtWordTemplate.AcceptsReturn = true;

        lblMarkdownTemplate.AutoSize = true;
        lblMarkdownTemplate.Location = new Point(12, 302);
        lblMarkdownTemplate.Text = "Markdown prompt:";

        txtMarkdownTemplate.Location = new Point(12, 322);
        txtMarkdownTemplate.Size = new Size(560, 110);
        txtMarkdownTemplate.Multiline = true;
        txtMarkdownTemplate.ScrollBars = ScrollBars.Vertical;
        txtMarkdownTemplate.AcceptsReturn = true;

        btnResetPrompts.Text = "Reset to defaults";
        btnResetPrompts.Location = new Point(12, 446);
        btnResetPrompts.Size = new Size(130, 27);
        btnResetPrompts.Click += BtnResetPrompts_Click;

        btnSave.Text = "Save";
        btnSave.Location = new Point(416, 446);
        btnSave.Size = new Size(75, 27);
        btnSave.Click += BtnSave_Click;

        btnCancel.Text = "Cancel";
        btnCancel.Location = new Point(497, 446);
        btnCancel.Size = new Size(75, 27);
        btnCancel.DialogResult = DialogResult.Cancel;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(584, 487);
        Controls.Add(lblInstructions);
        Controls.Add(lblApiKey);
        Controls.Add(txtApiKey);
        Controls.Add(lblPromptHeader);
        Controls.Add(lblWordTemplate);
        Controls.Add(txtWordTemplate);
        Controls.Add(lblMarkdownTemplate);
        Controls.Add(txtMarkdownTemplate);
        Controls.Add(btnResetPrompts);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Settings";
        CancelButton = btnCancel;
        AcceptButton = btnSave;
        ResumeLayout(false);
        PerformLayout();
    }
}
