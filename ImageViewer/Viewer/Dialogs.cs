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
    public SettingsDialog(ISettingsStorage settingsStorage, Settings settings)
    {
        App.EnsureDialogTheme();

        Title = "Preferences";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var fullScreen = new CheckBox { Content = "Start in full screen", IsChecked = settings.StartInFullScreen };
        var transparent = new CheckBox
        {
            Content = "Make window transparent when out of focus",
            IsChecked = settings.TransparentWhenOutOfFocus
        };
        var zoom = new ComboBox
        {
            ItemsSource = new[] { "Fit to screen", "Actual size" },
            SelectedIndex = (int)settings.DefaultZoom,
            MinWidth = 160
        };

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80 };
        ok.Click += async (_, _) =>
        {
            settings.StartInFullScreen = fullScreen.IsChecked == true;
            settings.DefaultZoom = zoom.SelectedIndex;
            settings.TransparentWhenOutOfFocus = transparent.IsChecked == true;
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
                transparent,
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
