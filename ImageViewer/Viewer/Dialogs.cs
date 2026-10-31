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

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80 };
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
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
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
    public SettingsDialog(ISettingsStorage settingsStorage, Settings settings, IDefaultViewer defaultViewer)
    {
        App.EnsureDialogTheme();

        Title = "Preferences";
        Width = 420;
        SizeToContent = SizeToContent.Height;
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

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80 };
        ok.Click += async (_, _) =>
        {
            settings.StartInFullScreen = fullScreen.IsChecked == true;
            settings.DefaultZoom = zoom.SelectedIndex;
            await settingsStorage.SaveSettings(settings);
            Close(true);
        };

        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Thickness(16),
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
                makeDefault,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { ok, cancel }
                }
            }
        };
    }
}
