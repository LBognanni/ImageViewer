using System;
using System.Windows.Forms;

namespace ImageViewer;

public partial class frmSettings : Form
{
    private readonly ISettingsStorage _settingsStorage;
    private readonly Settings _settings;

    public frmSettings(ISettingsStorage settingsStorage, Settings settings)
    {
        _settingsStorage = settingsStorage;
        _settings = settings;
        InitializeComponent();

        cbFullScreen.Checked = settings.StartInFullScreen;
        cbTransparent.Checked = settings.TransparentWhenOutOfFocus;
        ddZoom.SelectedIndex = (int)settings.DefaultZoom;
    }

    private async void cmdOk_Click(object sender, EventArgs e)
    {
        _settings.StartInFullScreen = cbFullScreen.Checked;
        _settings.DefaultZoom = ddZoom.SelectedIndex;
        _settings.TransparentWhenOutOfFocus = cbTransparent.Checked;
        await _settingsStorage.SaveSettings(_settings).ConfigureAwait(false);
        this.Close();
    }
}