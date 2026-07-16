using PdfToWordOcr.App.Config;
using PdfToWordOcr.Core;

namespace PdfToWordOcr.App.Forms;

public partial class SettingsForm : Form
{
    public SettingsForm()
    {
        InitializeComponent();

        var settings = AppSettings.LoadUserSettings();
        txtWordTemplate.Text = ToDisplay(settings.WordPromptTemplate ?? OcrPrompts.DefaultWordTemplate);
        txtMarkdownTemplate.Text = ToDisplay(settings.MarkdownPromptTemplate ?? OcrPrompts.DefaultMarkdownTemplate);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var apiKey = txtApiKey.Text.Trim();
        if (apiKey.Length == 0 && AppSettings.TryGetApiKey() is null)
        {
            MessageBox.Show(this, "Please enter an API key.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (apiKey.Length > 0)
        {
            AppSettings.SaveApiKey(apiKey);
        }

        AppSettings.SaveUserSettings(new UserSettings
        {
            WordPromptTemplate = ToOverride(txtWordTemplate.Text, OcrPrompts.DefaultWordTemplate),
            MarkdownPromptTemplate = ToOverride(txtMarkdownTemplate.Text, OcrPrompts.DefaultMarkdownTemplate),
        });

        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnResetPrompts_Click(object? sender, EventArgs e)
    {
        txtWordTemplate.Text = ToDisplay(OcrPrompts.DefaultWordTemplate);
        txtMarkdownTemplate.Text = ToDisplay(OcrPrompts.DefaultMarkdownTemplate);
    }

    /// <summary>WinForms text boxes need CRLF line breaks.</summary>
    private static string ToDisplay(string template) => template.ReplaceLineEndings();

    /// <summary>Null when the text is empty or matches the built-in default (= no override stored).</summary>
    private static string? ToOverride(string text, string defaultTemplate)
    {
        var normalized = text.ReplaceLineEndings("\n").Trim();
        return normalized.Length == 0 || normalized == defaultTemplate ? null : normalized;
    }
}
