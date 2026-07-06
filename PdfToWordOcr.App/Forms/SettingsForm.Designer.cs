namespace PdfToWordOcr.App.Forms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblInstructions;
    private Label lblApiKey;
    private TextBox txtApiKey;
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
        btnSave = new Button();
        btnCancel = new Button();
        SuspendLayout();

        lblInstructions.AutoSize = true;
        lblInstructions.Location = new Point(12, 12);
        lblInstructions.MaximumSize = new Size(360, 0);
        lblInstructions.Text = "Enter your Anthropic API key. It is encrypted and stored locally, and is " +
            "never written to logs or configuration files.";

        lblApiKey.AutoSize = true;
        lblApiKey.Location = new Point(12, 64);
        lblApiKey.Text = "API key:";

        txtApiKey.Location = new Point(12, 84);
        txtApiKey.Size = new Size(360, 23);
        txtApiKey.UseSystemPasswordChar = true;

        btnSave.Text = "Save";
        btnSave.Location = new Point(216, 120);
        btnSave.Size = new Size(75, 27);
        btnSave.Click += BtnSave_Click;

        btnCancel.Text = "Cancel";
        btnCancel.Location = new Point(297, 120);
        btnCancel.Size = new Size(75, 27);
        btnCancel.DialogResult = DialogResult.Cancel;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(384, 159);
        Controls.Add(lblInstructions);
        Controls.Add(lblApiKey);
        Controls.Add(txtApiKey);
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
