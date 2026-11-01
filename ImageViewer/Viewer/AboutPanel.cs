using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImageViewer;

/// <summary>
/// What the app is and where to find out more. Nothing here goes online unless a link is clicked.
/// </summary>
public sealed class AboutPanel : UserControl
{
    private const int LogoSize = 96;
    private const double SecondaryText = 0.7;

    private static readonly Color Donate = Color.Parse("#FFDD65");
    private static readonly Color DonateHover = Color.Parse("#FFE786");
    private static readonly Color DonatePressed = Color.Parse("#F1CB43");
    private static readonly Color DonateText = Color.Parse("#223B36");

    private readonly Func<Uri, Task<bool>> _openUri;
    private readonly SelectableTextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Orange, IsVisible = false };

    /// <param name="openUri">Opens a link, returning false if that wasn't possible</param>
    /// <param name="version">The running version, or null if it isn't known</param>
    public AboutPanel(Func<Uri, Task<bool>> openUri, Version? version)
    {
        _openUri = openUri;

        var text = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Image Viewer", FontSize = 24, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Fast and private image viewer", Opacity = SecondaryText },
                new TextBlock { Text = version == null ? "Development version" : $"Version {version.ToString(3)}", Opacity = SecondaryText }
            }
        };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { Logo(), text } };

        Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                header,
                new TextBlock
                {
                    Text = "© Loris Bognanni. Image Viewer is free software under the GPLv3. If you find it useful, please consider donating.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = SecondaryText
                },
                DonateButton(),
                new WrapPanel
                {
                    Children =
                    {
                        Link("GitHub", "https://github.com/LBognanni/ImageViewer"),
                        Link("Report an issue", "https://github.com/LBognanni/ImageViewer/issues"),
                        Link("Releases", "https://github.com/LBognanni/ImageViewer/releases"),
                        Link("codemade.net","https://codemade.net")
                    }
                },
                _error
            }
        };
    }

    public static Version? RunningVersion => Assembly.GetExecutingAssembly().GetName().Version;

    private static Image Logo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("imageviewer.png")!;
        return new Image { Source = new Bitmap(stream), Width = LogoSize, Height = LogoSize };
    }

    private Button DonateButton()
    {
        var button = new Button { Content = "☕ Buy me a coffee", Foreground = new SolidColorBrush(DonateText), Background = new SolidColorBrush(Donate) };

        // The theme swaps these in for the hover and pressed states, whatever the button's own colors
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(DonateHover);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(DonatePressed);
        button.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(DonateText);
        button.Resources["ButtonForegroundPressed"] = new SolidColorBrush(DonateText);
        Open(button, new Uri("https://buymeacoffee.com/codemade"));
        return button;
    }

    private HyperlinkButton Link(string text, string url)
    {
        var link = new HyperlinkButton { Content = text };
        Open(link, new Uri(url));
        return link;
    }

    private void Open(Button button, Uri uri)
    {
        // Tooltips wrap at 320 pixels by default, which splits the longer links
        ToolTip.SetTip(button, new ToolTip
        {
            MaxWidth = double.PositiveInfinity,
            Content = new TextBlock { Text = uri.AbsoluteUri, TextWrapping = TextWrapping.NoWrap }
        });
        button.Click += async (_, _) =>
        {
            _error.IsVisible = false;
            if (!await TryOpen(uri))
            {
                _error.Text = $"Couldn't open your browser. The link is {uri.AbsoluteUri}";
                _error.IsVisible = true;
            }
        };
    }

    private async Task<bool> TryOpen(Uri uri)
    {
        try
        {
            return await _openUri(uri);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            return false;
        }
    }
}
