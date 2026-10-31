using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SkiaSharp;

namespace ImageViewer;

public sealed class ViewerWindow : Window
{
    private readonly Navigator _navigator;
    private readonly ImagePipeline _pipeline;
    private readonly ISettingsStorage _settingsStorage;
    private readonly Settings _settings;
    private readonly ITempData _tempData;
    private readonly ITrash _trash;
    private readonly IClickThrough _clickThrough;
    private readonly IDefaultViewer _defaultViewer;
    private readonly IReadOnlySet<string> _extensions;
    private readonly string? _settingsError;
    private readonly Action _onFirstImageShown;

    private readonly ImageSurface _surface;
    private readonly MessageOverlay _overlay = new();
    private readonly Dictionary<string, Action> _defaultActions;
    private readonly (string, string)[] _actionDescriptions;

    private DispatcherTimer? _autoPlayTimer;
    private bool _isClickThrough;
    private bool _isDeleting;
    private string? _updateMessage;
    private string? _updateUrl;
    private int _listingVersion;

    internal ImageSurface Surface => _surface;
    internal MessageOverlay Overlay => _overlay;

    public ViewerWindow(Navigator navigator, ImagePipeline pipeline, ISettingsStorage settingsStorage, Settings settings,
        ITempData tempData, ITrash trash, IClickThrough clickThrough, IDefaultViewer defaultViewer, IReadOnlySet<string> extensions,
        string? settingsError, Action onFirstImageShown)
    {
        _navigator = navigator;
        _pipeline = pipeline;
        _settingsStorage = settingsStorage;
        _settings = settings;
        _tempData = tempData;
        _trash = trash;
        _clickThrough = clickThrough;
        _defaultViewer = defaultViewer;
        _extensions = extensions;
        _settingsError = settingsError;
        _onFirstImageShown = onFirstImageShown;

        _defaultActions = new Dictionary<string, Action>()
        {
            { nameof(AutoPlay), AutoPlay },
            { nameof(AutoPlayFaster), AutoPlayFaster },
            { nameof(ToggleBorderless), ToggleBorderless },
            { nameof(CopyFileName), CopyFileName },
            { nameof(ToggleZoom), ToggleZoom },
            { nameof(Transparency1), Transparency1 },
            { nameof(Transparency2), Transparency2 },
            { nameof(Transparency3), Transparency3 },
            { nameof(Transparency4), Transparency4 },
            { nameof(Transparency5), Transparency5 },
            { nameof(ExitFullScreenOrClose), ExitFullScreenOrClose },
            { nameof(ToggleFullScreen), ToggleFullScreen },
            { nameof(ToggleHelpMessage), ToggleHelpMessage },
            { nameof(PreviousImage), PreviousImage },
            { nameof(ReOpen), ReOpen },
            { nameof(ShowPreferences), ShowPreferences },
            { nameof(Close), Close },
            { nameof(RotateImage), RotateImage },
            { nameof(RotateImageInv), RotateImageInv },
            { nameof(NextImage), NextImage },
            { nameof(Shuffle), Shuffle },
            { nameof(AutoPlaySlower), AutoPlaySlower },
            { nameof(ToggleTopMost), ToggleTopMost },
            { nameof(ToggleClickThrough), ToggleClickThrough },
            { nameof(DeleteCurrentFile), DeleteCurrentFile }
        };
        _actionDescriptions = new[]
        {
            ( nameof(AutoPlay), "Toggle Autoplay" ),
            ( nameof(AutoPlayFaster), "Autoplay Faster" ),
            ( nameof(AutoPlaySlower), "Autoplay Slower" ),
            ( nameof(ToggleZoom), "Toggle Zoom" ),
            ( nameof(CopyFileName), "Copy File Name" ),
            ( nameof(PreviousImage), "Previous Image" ),
            ( nameof(NextImage), "Next Image" ),
            ( nameof(ToggleBorderless), "Toggle Borderless" ),
            ( nameof(ToggleFullScreen), "Toggle FullScreen" ),
            ( nameof(ToggleTopMost), "Toggle TopMost" ),
            ( nameof(ToggleClickThrough), "Toggle ClickThrough" ),
            ( nameof(Transparency1), "Transparency: 1/5" ),
            ( nameof(Transparency2), "Transparency: 2/5" ),
            ( nameof(Transparency3), "Transparency: 3/5" ),
            ( nameof(Transparency4), "Transparency: 4/5" ),
            ( nameof(Transparency5), "Transparency: 5/5" ),
            ( nameof(ReOpen), "Show the Open dialog" ),
            ( nameof(ShowPreferences), "Show Preferences" ),
            ( nameof(RotateImage), "Rotate Image clockwise" ),
            ( nameof(RotateImageInv), "Rotate Image counter-clockwise" ),
            ( nameof(Shuffle), "Shuffle" ),
            ( nameof(DeleteCurrentFile), "Delete Current File"),
            ( nameof(Close), "Quit" ),
            ( nameof(ToggleHelpMessage), "Show this help message" ),
        };

        Title = "Image Viewer";
        Width = 1304;
        Height = 837;
        // ImageSurface paints the whole window, so anything here would show through when it fades
        Background = null;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _surface = new ImageSurface(pipeline) { DefaultZoom = settings.DefaultZoom };
        Content = new Panel { Children = { _surface, _overlay } };

        if (settings.StartInFullScreen)
        {
            WindowState = WindowState.FullScreen;
            Topmost = true;
        }

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _surface.DoubleTapped += (_, _) => ToggleFullScreen();
        _navigator.CurrentChanged += ShowCurrent;
        _pipeline.ImageReady += path => Dispatcher.UIThread.Post(() => _surface.OnImageReady(path));
        Opened += OnOpened;
        Activated += OnActivated;
        Deactivated += OnDeactivated;

        if (_navigator.Current != null)
        {
            ShowCurrent();
            ListFolder();
        }
    }

    /// <summary>
    /// Show a file that came from outside the window, like a macOS "open with"
    /// </summary>
    public void OpenFile(string fileName)
    {
        _navigator.Open(fileName);
        ListFolder();
    }

    public void ShowUpdateMessage(string message, string url)
    {
        _updateMessage = message;
        _updateUrl = url;
        _overlay.ShowMessage(message, 10000);
    }

    private void ShowCurrent()
    {
        var current = _navigator.Current;
        Title = current == null ? "Image Viewer" : $"{Path.GetFileName(current)} - Image Viewer";
        _surface.Show(current, _navigator.Upcoming);
    }

    private async void ListFolder()
    {
        var version = ++_listingVersion;
        var current = _navigator.Current;
        if (current == null)
            return;

        try
        {
            var files = await Navigator.ListFolder(Path.GetDirectoryName(current)!, _extensions);
            if (version == _listingVersion)
            {
                _navigator.ApplyFolder(files);
                _surface.SetUpcoming(_navigator.Upcoming);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Can't list the folder: {e.Message}");
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        var largest = Screens.All.Select(s => s.Bounds.Size).OrderByDescending(s => s.Width * s.Height).FirstOrDefault();
        if (largest.Width > 0)
        {
            _pipeline.SetScreenSize(new SKSizeI(largest.Width, largest.Height));
        }

        LoadIcon();
        _surface.Focus();

        if (_settingsError != null && !await AskToUseDefaultSettings())
        {
            Close();
            return;
        }

        if (_navigator.Current == null)
        {
            // Finder delivers its file just after launch, through an event
            if (OperatingSystem.IsMacOS())
            {
                await Task.Delay(300);
            }
            if (_navigator.Current == null && !await PickFile())
            {
                Close();
                return;
            }
        }

        _overlay.ShowMessage("Press H for help", 1000);
        _onFirstImageShown();
    }

    private void LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
        if (stream != null)
        {
            Icon = new WindowIcon(stream);
        }
    }

    private async Task<bool> AskToUseDefaultSettings()
    {
        var useDefaults = await MessageDialog.Show(this,
            $"Unable to load settings. Load default settings instead?\nThis will overwrite any custom changes to your settings script.\n\nError: {_settingsError}",
            canCancel: true);
        if (useDefaults)
        {
            await _settingsStorage.SaveSettings(_settings);
        }
        return useDefaults;
    }

    private async Task<bool> PickFile()
    {
        App.EnsureDialogTheme();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image file") { Patterns = _extensions.Select(x => "*" + x).ToList() }
            }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path == null)
            return false;

        OpenFile(path);
        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {        if (e.Key == Key.Enter && _updateUrl != null && _overlay.Text == _updateMessage)
        {
            OpenUrl(_updateUrl);
            e.Handled = true;
            return;
        }

        if (!_settings.TryGetKeyBindingFor(new KeyCombination(e.Key, e.KeyModifiers), out var keyBinding))
            return;

        e.Handled = true;
        if (!string.IsNullOrEmpty(keyBinding!.Action) && _defaultActions.TryGetValue(keyBinding.Action!, out var action))
        {
            action();
        }
        else if (!string.IsNullOrEmpty(keyBinding.Command) && _navigator.Current != null)
        {
            RunCommand(keyBinding);
        }
    }

    private async void RunCommand(KeyBinding keyBinding)
    {
        var command = string.Format(keyBinding.Command!, _navigator.Current);
        try
        {
            var (exe, parameters) = FileUtilites.SplitCommand(command);
            Process.Start(new ProcessStartInfo(exe, parameters) { UseShellExecute = true });
            if (keyBinding.IsDeleteAction)
            {
                RemoveCurrentFromList();
            }
        }
        catch (Exception ex)
        {
            await MessageDialog.Show(this, $"Cannot run custom command: {ex.Message}.\nThe command ran was:\n{command}");
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Can't open {url}: {e.Message}");
        }
    }

    private void RemoveCurrentFromList()
    {
        var removed = _navigator.Current;
        _navigator.RemoveCurrent();
        if (removed != null)
        {
            _pipeline.Forget(removed);
        }
    }

    private void AutoPlay()
    {
        if (_autoPlayTimer == null)
        {
            _autoPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _autoPlayTimer.Tick += (_, _) => NextImage();
        }

        if (_autoPlayTimer.IsEnabled)
        {
            _autoPlayTimer.Stop();
            _overlay.ShowMessage("Autoplay OFF", 900);
        }
        else
        {
            _autoPlayTimer.Start();
            _overlay.ShowMessage("Autoplay ON", 900);
        }
    }

    private void Transparency5() => SetTransparency(.75, 5);
    private void Transparency4() => SetTransparency(.5, 4);
    private void Transparency3() => SetTransparency(.3, 3);
    private void Transparency2() => SetTransparency(.2, 2);
    private void Transparency1() => SetTransparency(.1, 1);

    /// <summary>
    /// Fade the window straight away. Pressing the same level again makes it solid.
    /// </summary>
    private void SetTransparency(double opacity, int level)
    {
        if (_surface.ImageOpacity == opacity)
        {
            ApplyOpacity(1);
            _overlay.ShowMessage("Transparency off", 500);
        }
        else
        {
            ApplyOpacity(opacity);
            _overlay.ShowMessage($"Transparency: {level}/5", 500);
        }
    }

    /// <summary>
    /// Window.Opacity fades what Avalonia draws, and the surface fades the image it draws itself
    /// </summary>
    private void ApplyOpacity(double opacity)
    {
        Opacity = opacity;
        _surface.ImageOpacity = opacity;
    }

    private void AutoPlaySlower() => ChangeAutoPlayInterval(1.112);

    private void AutoPlayFaster() => ChangeAutoPlayInterval(0.9);

    private void ChangeAutoPlayInterval(double factor)
    {
        if (_autoPlayTimer != null)
        {
            _autoPlayTimer.Interval = _autoPlayTimer.Interval * factor;
            _overlay.ShowMessage($"Autoplay interval: {(int)_autoPlayTimer.Interval.TotalMilliseconds}ms", 300);
        }
    }

    private async void CopyFileName()
    {
        if (_navigator.Current != null && Clipboard != null)
        {
            await Clipboard.SetTextAsync(_navigator.Current);
            _overlay.ShowMessage("File name copied to clipboard", 300);
        }
    }

    private void ToggleZoom()
    {
        if (_surface.Zoom != 1)
        {
            _surface.Zoom = 1;
        }
        else
        {
            _surface.Zoom = 0;
            _surface.ResetTransform();
        }
    }

    private void ToggleTopMost()
    {
        Topmost = !Topmost;
        _overlay.ShowMessage($"Now {(Topmost ? "" : "not ")}Top most", 300);
    }

    private async void ReOpen()
    {
        if (!await PickFile())
        {
            Close();
        }
    }

    private void ToggleHelpMessage()
    {
        // Other messages give way to the help, which is what "Press H for help" promises
        var help = GetHelpText();
        if (_overlay.Text == help)
        {
            _overlay.HideMessage();
        }
        else
        {
            _overlay.ShowMessage(help, 10000);
        }
    }

    private string GetHelpText()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var (command, description) in _actionDescriptions)
        {
            var keyBinding = _settings.KeyBindings.FirstOrDefault(x => x.Value.Action == command);
            // Settings saved by older versions may not have a binding for every action
            if (keyBinding.Key != null)
            {
                sb.AppendFormat("{0}\t{1}\r\n", FormatKey(keyBinding.Key), description);
            }
        }
        return sb.ToString();
    }

    private string FormatKey(string key)
    {
        if (key.Contains('|'))
        {
            return String.Join("+",
                key.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(FormatKey));
        }

        string k = key.ToLower() switch
        {
            "shift" => "⇧",
            "left" => "←",
            "right" => "→",
            "add" => "+",
            "subtract" => "-",
            "d1" => "1",
            "d2" => "2",
            "d3" => "3",
            "d4" => "4",
            "d5" => "5",
            "d6" => "6",
            "d7" => "7",
            "d8" => "8",
            "d9" => "9",
            "control" => "CTRL",
            "controlkey" => "CTRL",
            _ => key.ToUpper()
        };

        return $"[{k}]";
    }

    private void ExitFullScreenOrClose()
    {
        if (Topmost)
        {
            ToggleFullScreen();
        }
        else
        {
            Close();
        }
    }

    private async void ShowPreferences()
    {
        if (Topmost)
        {
            ToggleFullScreen();
        }

        var dialog = new SettingsDialog(_settingsStorage, _settings, _defaultViewer);
        if (await dialog.ShowDialog<bool>(this))
        {
            _surface.DefaultZoom = _settings.DefaultZoom;
            _pipeline.PrefetchFull = _settings.DefaultZoom != 0;
        }
    }

    private void Shuffle() => _navigator.Shuffle();

    private void ToggleClickThrough()
    {
        _isClickThrough = !_isClickThrough;
        if (_isClickThrough)
        {
            _overlay.ShowMessage("Window will be click-thru once out of focus", 1000);
        }
        else
        {
            SetClickThrough(false);
            _overlay.ShowMessage("Click-thru disabled", 500);
        }
    }

    private void SetClickThrough(bool enabled)
    {
        var handle = TryGetPlatformHandle();
        if (handle == null || !_clickThrough.TrySet(handle, enabled))
        {
            if (enabled)
            {
                _overlay.ShowMessage("Click-thru isn't available on this system", 2000);
            }
        }
    }

    private void ToggleBorderless()
    {
        if (Topmost)
        {
            WindowDecorations = WindowDecorations.Full;
            Topmost = false;
        }
        else
        {
            WindowDecorations = WindowDecorations.None;
            Topmost = true;
        }
    }

    private async void DeleteCurrentFile()
    {
        var current = _navigator.Current;
        if (current == null || _isDeleting || _navigator.Count <= 1)
            return;

        _isDeleting = true;
        try
        {
            var yesterday = DateTime.Now.AddDays(-1);
            var lastAlerted = _tempData.ReadTime("LastAlertedOnDelete") ?? yesterday;
            if (lastAlerted <= yesterday)
            {
                if (!await MessageDialog.Show(this, "Are you sure you want to delete the current file?", canCancel: true))
                    return;
                _tempData.WriteTime("LastAlertedOnDelete", DateTime.Now);
            }

            await _trash.MoveToTrash(current);
            RemoveCurrentFromList();
            Debug.WriteLine($"Deleted file: {current}.");
        }
        catch (OperationCanceledException)
        {
            // do nothing
        }
        catch (Exception e)
        {
            await MessageDialog.Show(this, $"Can't delete this file: {e.Message}");
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        _surface.Focus();
        if (_isClickThrough)
        {
            SetClickThrough(false);
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClickThrough)
        {
            SetClickThrough(true);
        }
    }

    private void ToggleFullScreen()
    {
        if (Topmost)
        {
            Topmost = false;
            WindowDecorations = WindowDecorations.Full;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.FullScreen;
            Topmost = true;
        }
    }

    private void RotateImageInv() => _surface.Rotation += 90;

    private void RotateImage() => _surface.Rotation -= 90;

    private void PreviousImage() => _navigator.Previous();

    private void NextImage() => _navigator.Next();
}
