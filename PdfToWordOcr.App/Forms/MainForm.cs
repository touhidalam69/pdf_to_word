using PdfToWordOcr.App.Config;

namespace PdfToWordOcr.App.Forms;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();

        var defaults = AppSettings.LoadDefaults();
        cmbModel.Text = defaults.Model;
        numDpi.Value = defaults.Dpi;
        cmbLanguage.Text = defaults.Language;
        cmbFont.Text = defaults.Font;
    }
}
