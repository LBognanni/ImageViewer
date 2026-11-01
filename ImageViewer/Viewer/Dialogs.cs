using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ImageViewer;

public sealed class MessageDialog : Window
{
    private MessageDialog(string message, bool canCancel)
    {
        Title = "Image Viewer";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => Close(true);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { ok }
        };
        if (canCancel)
        {
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons }
        };
    }

    /// <summary>
    /// Returns true if the user pressed Ok
    /// </summary>
    public static async Task<bool> Show(Window owner, string message, bool canCancel = false)
    {
        App.EnsureDialogTheme();
        return await new MessageDialog(message, canCancel).ShowDialog<bool>(owner);
    }
}

public sealed class SettingsDialog : Window
{
    /// <param name="settingsStorage">Where to save the settings</param>
    /// <param name="settings">The settings to edit</param>
    /// <param name="defaultViewer">Makes this the system's image viewer</param>
    /// <param name="openUri">Opens links, or null to use the system browser</param>
    public SettingsDialog(ISettingsStorage settingsStorage, Settings settings, IDefaultViewer defaultViewer,
        Func<Uri, Task<bool>>? openUri = null)
    {
        App.EnsureDialogTheme();

        Title = "Preferences";
        Width = 460;
        // A fixed height, so that the window doesn't jump between tabs
        Height = 430;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var fullScreen = new CheckBox { Content = "Start in full screen", IsChecked = settings.StartInFullScreen };
        var zoom = new ComboBox
        {
            ItemsSource = new[] { "Fit to screen", "Actual size" },
            SelectedIndex = (int)settings.DefaultZoom,
            MinWidth = 160
        };

        var makeDefault = new Button { Content = "Set as default image viewer" };
        makeDefault.Click += async (_, _) =>
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

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += async (_, _) =>
        {
            settings.StartInFullScreen = fullScreen.IsChecked == true;
            settings.DefaultZoom = zoom.SelectedIndex;
            await settingsStorage.SaveSettings(settings);
            Close(true);
        };

        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close(false);

        var general = new StackPanel
        {
            Margin = new Thickness(0, 12),
            Spacing = 12,
            Children =
            {
                fullScreen,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Default zoom:", VerticalAlignment = VerticalAlignment.Center },
                        zoom
                    }
                },
                makeDefault
            }
        };

        var about = new AboutPanel(openUri ?? (uri => Launcher.LaunchUriAsync(uri)), AboutPanel.RunningVersion) { Margin = new Thickness(0, 12) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { ok, cancel }
        };
        Grid.SetRow(buttons, 1);

        Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new TabControl
                {
                    Items =
                    {
                        new TabItem { Header = "General", Content = general },
                        new TabItem { Header = "About", Content = about }
                    }
                },
                buttons
            }
        };
    }
}
