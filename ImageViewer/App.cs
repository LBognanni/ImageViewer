using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace ImageViewer;

/// <summary>
/// Everything the main window needs, built in <see cref="Program"/> before Avalonia starts
/// so that the first image is already decoding while the UI comes up.
/// </summary>
public sealed record AppServices(
    Navigator Navigator,
    ImagePipeline Pipeline,
    ISettingsStorage SettingsStorage,
    Settings Settings,
    ITempData TempData,
    ITrash Trash,
    IClickThrough ClickThrough,
    IReadOnlySet<string> Extensions,
    string? SettingsError);

public sealed class App : Application, IUpdateNotifier
{
    private static bool _dialogThemeLoaded;
    private readonly AppServices _services;
    private ViewerWindow? _window;

    public App(AppServices services)
    {
        _services = services;
        // Otherwise system dialogs and menus call this "Avalonia Application"
        Name = "Image Viewer";
    }

    /// <summary>
    /// The main window draws everything itself and needs no theme, and loading
    /// one is slow, so only dialogs pay for it and only when they're used.
    /// </summary>
    public static void EnsureDialogTheme()
    {
        if (_dialogThemeLoaded || Current == null)
            return;

        _dialogThemeLoaded = true;
        Current.RequestedThemeVariant = ThemeVariant.Dark;
        Current.Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var s = _services;
            _window = new ViewerWindow(s.Navigator, s.Pipeline, s.SettingsStorage, s.Settings, s.TempData, s.Trash,
                s.ClickThrough, s.Extensions, s.SettingsError, () => CheckVersionLater());
            desktop.MainWindow = _window;
        }

        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Activated += OnActivated;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is FileActivatedEventArgs { Files.Count: > 0 } file && file.Files[0].TryGetLocalPath() is { } path)
        {
            _window?.OpenFile(path);
        }
    }

    /// <summary>
    /// Wait for the startup hint to go, because the overlay shows one message at a time
    /// </summary>
    private void CheckVersionLater()
    {
        DispatcherTimer.RunOnce(() =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return;

            var checker = new VersionChecker(new GitHubVersionGetter(), version, this, _services.TempData);
            Task.Run(async () =>
            {
                try
                {
                    await checker.NotifyIfNewVersion();
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Can't check new version: {e.Message}.");
                }
            });
        }, TimeSpan.FromSeconds(2));
    }

    public void Notify(string message, string url) =>
        Dispatcher.UIThread.Post(() => _window?.ShowUpdateMessage(message, url));
}
