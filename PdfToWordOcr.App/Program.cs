using PdfToWordOcr.App.Config;
using PdfToWordOcr.App.Forms;

namespace PdfToWordOcr.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        if (AppSettings.TryGetApiKey() is null)
        {
            using var settingsForm = new SettingsForm();
            if (settingsForm.ShowDialog() != DialogResult.OK || AppSettings.TryGetApiKey() is null)
            {
                return;
            }
        }

        Application.Run(new MainForm());
    }
}