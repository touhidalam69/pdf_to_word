using PdfToWordOcr.App.Config;

namespace PdfToWordOcr.App.Forms;

public partial class SettingsForm : Form
{
    public SettingsForm()
    {
        InitializeComponent();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var apiKey = txtApiKey.Text.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show(this, "Please enter an API key.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppSettings.SaveApiKey(apiKey);
        DialogResult = DialogResult.OK;
        Close();
    }
}
