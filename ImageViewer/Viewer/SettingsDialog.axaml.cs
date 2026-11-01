using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace ImageViewer;

public sealed partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        App.EnsureDialogTheme();
        InitializeComponent();
    }

    /// <param name="settingsStorage">Where to save the settings</param>
    /// <param name="settings">The settings to edit</param>
    /// <param name="defaultViewer">Makes this the system's image viewer</param>
    /// <param name="openUri">Opens links, or null to use the system browser</param>
    public SettingsDialog(ISettingsStorage settingsStorage, Settings settings, IDefaultViewer defaultViewer,
        Func<Uri, Task<bool>>? openUri = null) : this()
    {
        About.OpenUri = openUri;
        FullScreen.IsChecked = settings.StartInFullScreen;
        Zoom.SelectedIndex = (int)settings.DefaultZoom;

        MakeDefault.Click += async (_, _) =>
        {
            try
            {
                await MessageDialog.Show(this, await defaultViewer.MakeDefault());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception)
            {
                await MessageDialog.Show(this, e.Message);
            }
        };

        Ok.Click += async (_, _) =>
        {
            settings.StartInFullScreen = FullScreen.IsChecked == true;
            settings.DefaultZoom = Zoom.SelectedIndex;
            await settingsStorage.SaveSettings(settings);
            Close(true);
        };

        Cancel.Click += (_, _) => Close(false);
    }
}
