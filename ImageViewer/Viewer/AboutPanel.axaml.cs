using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace ImageViewer;

/// <summary>
/// What the app is and where to find out more. Nothing here goes online unless a link is clicked.
/// </summary>
public sealed partial class AboutPanel : UserControl
{
    public AboutPanel()
    {
        InitializeComponent();

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("imageviewer.png")!;
        Logo.Source = new Bitmap(stream);
        VersionLabel.Text = AppVersion.Describe(AppVersion.Running);
    }

    /// <summary>
    /// Opens a link, returning false if that wasn't possible. Null uses the system browser.
    /// </summary>
    public Func<Uri, Task<bool>>? OpenUri { get; set; }

    private async void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        var uri = new Uri((string)((Control)sender!).Tag!);
        Error.IsVisible = false;
        if (!await TryOpen(uri))
        {
            Error.Text = $"Couldn't open your browser. The link is {uri.AbsoluteUri}";
            Error.IsVisible = true;
        }
    }

    private async Task<bool> TryOpen(Uri uri)
    {
        try
        {
            return await (OpenUri ?? TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync)(uri);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            return false;
        }
    }
}
