using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ImageViewer.Tests;
using Moq;
using NUnit.Framework;
using SkiaSharp;

[assembly: AvaloniaTestApplication(typeof(ViewerTestApp))]

namespace ImageViewer.Tests;

public class ViewerTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ViewerTestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Sends key presses through a real window. Headless needs no display, so these run anywhere
/// and never touch the desktop they run on.
/// </summary>
[TestFixture]
public class ViewerWindowTests
{
    private string _folder = null!;
    private ImagePipeline _pipeline = null!;
    private Navigator _navigator = null!;
    private Settings _settings = null!;
    private FakeTrash _trash = null!;
    private Mock<IClickThrough> _clickThrough = null!;
    private ViewerWindow _sut = null!;

    private void Open(string first = "a.jpg", Settings? settings = null)
    {
        _folder = Directory.CreateTempSubdirectory("imageviewer-ui").FullName;
        byte index = 0;
        foreach (var name in new[] { "a.jpg", "b.jpg", "c.jpg" })
        {
            var made = TestImages.Jpeg(1200, 800, new SKColor((byte)(60 * ++index), 40, 40));
            File.Move(made, Path.Combine(_folder, name));
        }

        var decoder = new SkiaImageDecoder();
        _pipeline = new ImagePipeline(decoder, 1 << 30, new SKSizeI(1920, 1080), 2);
        _navigator = new Navigator(_pipeline);
        _settings = settings ?? new Settings();
        _trash = new FakeTrash();
        _clickThrough = new Mock<IClickThrough>();
        _clickThrough.Setup(c => c.TrySet(It.IsAny<IPlatformHandle>(), It.IsAny<bool>())).Returns(true);

        var tempData = new Mock<ITempData>();
        tempData.Setup(t => t.ReadTime(It.IsAny<string>())).Returns(DateTime.Now);

        _navigator.Open(Path.Combine(_folder, first));
        _sut = new ViewerWindow(_navigator, _pipeline, new Mock<ISettingsStorage>().Object, _settings, tempData.Object,
            _trash, _clickThrough.Object, new Mock<IDefaultViewer>().Object, decoder.Extensions, null, () => { });
        _sut.Show();
        Pump(() => _navigator.Count == 3);
    }

    [TearDown]
    public void TearDown()
    {
        _sut?.Close();
        _pipeline?.Dispose();
    }

    /// <summary>
    /// Run the UI thread until something background work is doing has finished
    /// </summary>
    private static void Pump(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.ElapsedMilliseconds > 5000)
                Assert.Fail("Timed out");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        _sut.KeyPressQwerty(key, modifiers);
        // The key may have closed the window
        if (_sut.IsVisible)
        {
            _sut.KeyReleaseQwerty(key, modifiers);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    public void ShowsTheFileInTheTitle()
    {
        Open();
        Assert.That(_sut.Title, Is.EqualTo("a.jpg - Image Viewer"));
    }

    [AvaloniaTest]
    public void RightAndLeftMoveThroughTheFolder()
    {
        Open();
        Press(PhysicalKey.ArrowRight);
        Assert.That(_sut.Title, Is.EqualTo("b.jpg - Image Viewer"));
        Press(PhysicalKey.ArrowLeft);
        Press(PhysicalKey.ArrowLeft);
        Assert.That(_sut.Title, Is.EqualTo("c.jpg - Image Viewer"));
    }

    [AvaloniaTest]
    public void UnboundKeysDoNothing()
    {
        Open();
        Press(PhysicalKey.J);
        Assert.That(_sut.Title, Is.EqualTo("a.jpg - Image Viewer"));
    }

    [AvaloniaTest]
    public void RotateKeys()
    {
        Open();
        Press(PhysicalKey.R);
        Assert.That(_sut.Surface.Rotation, Is.EqualTo(270));
        Press(PhysicalKey.R, RawInputModifiers.Shift);
        Press(PhysicalKey.R, RawInputModifiers.Shift);
        Assert.That(_sut.Surface.Rotation, Is.EqualTo(90));
    }

    [AvaloniaTest]
    public void MovingResetsTheView()
    {
        Open();
        Press(PhysicalKey.R);
        Press(PhysicalKey.Digit1);
        Press(PhysicalKey.ArrowRight);
        Assert.That(_sut.Surface.Rotation, Is.EqualTo(0));
        Assert.That(_sut.Surface.Zoom, Is.EqualTo(0m));
    }

    [AvaloniaTest]
    public void ToggleZoomSwitchesBetweenFitAndActualSize()
    {
        Open();
        Press(PhysicalKey.Digit1);
        Assert.That(_sut.Surface.Zoom, Is.EqualTo(1m));
        Press(PhysicalKey.Digit1);
        Assert.That(_sut.Surface.Zoom, Is.EqualTo(0m));
    }

    [AvaloniaTest]
    public void HelpCoversEveryAction()
    {
        Open();
        // The help key has no row of its own: it's in the hint for closing the help
        foreach (var action in _sut.ActionNames.Where(a => a != "ToggleHelpMessage"))
        {
            var settings = new Settings();
            settings.KeyBindings = new Dictionary<string, KeyBinding> { { "X", new KeyBinding(action) } };
            var sheet = HelpSheet.Build(_sut.HelpSections, settings, _sut.ActionNames, Array.Empty<string>());
            Assert.That(sheet.Sections.Sum(s => s.Rows.Count), Is.EqualTo(1), action);
        }
    }

    [AvaloniaTest]
    public void HelpKeyTogglesTheHelp()
    {
        Open();
        Press(PhysicalKey.H);
        Assert.That(_sut.Help.IsOpen, Is.True);
        Press(PhysicalKey.H);
        Assert.That(_sut.Help.IsOpen, Is.False);
    }

    [AvaloniaTest]
    public void EscapeClosesTheHelpBeforeTheWindow()
    {
        Open();
        Press(PhysicalKey.H);
        Press(PhysicalKey.Escape);
        Assert.That(_sut.Help.IsOpen, Is.False);
        Assert.That(_sut.IsVisible, Is.True);

        Press(PhysicalKey.Escape);
        Assert.That(_sut.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void AnimationsPlayUntilYouMoveOn()
    {
        Open();
        var gif = Path.Combine(_folder, "animated.gif");
        File.Move(TestImages.Gif(4, 4, new GifFrame(0, 0, 4, 4, 1, 20), new GifFrame(0, 0, 4, 4, 2, 20)), gif);
        _sut.OpenFile(gif);
        Pump(() => _sut.Surface.Shown?.Path == gif);

        var first = _sut.Surface.Shown;
        Pump(() => _sut.Surface.Shown != first);

        Press(PhysicalKey.ArrowRight);
        Pump(() => _sut.Surface.Shown?.Path != gif);
        var next = _sut.Surface.Shown;
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
        Assert.That(_sut.Surface.Shown, Is.SameAs(next));
    }

    [AvaloniaTest]
    public void DeleteSendsTheFileToTheTrashAndMovesOn()
    {
        Open();
        var first = _navigator.Current!;
        Press(PhysicalKey.D);
        Pump(() => _navigator.Count == 2);
        Assert.That(_trash.Trashed, Is.EqualTo(new[] { first }));
        Assert.That(_sut.Title, Is.EqualTo("b.jpg - Image Viewer"));
    }

    [AvaloniaTest]
    public void AFailedDeleteKeepsTheFile()
    {
        Open();
        _trash.Fail = new OperationCanceledException();
        Press(PhysicalKey.D);
        Dispatcher.UIThread.RunJobs();
        Assert.That(_navigator.Count, Is.EqualTo(3));
    }

    [AvaloniaTest]
    public void FullScreenToggles_AndEscapeLeavesItBeforeClosing()
    {
        Open();
        Press(PhysicalKey.F);
        Assert.That(_sut.WindowState, Is.EqualTo(WindowState.FullScreen));
        Assert.That(_sut.Topmost, Is.True);

        Press(PhysicalKey.Escape);
        Assert.That(_sut.WindowState, Is.EqualTo(WindowState.Maximized));
        Assert.That(_sut.Topmost, Is.False);
        Assert.That(_sut.IsVisible, Is.True);

        Press(PhysicalKey.Escape);
        Assert.That(_sut.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void BorderlessToggles()
    {
        Open();
        Press(PhysicalKey.B);
        Assert.That(_sut.WindowDecorations, Is.EqualTo(WindowDecorations.None));
        Press(PhysicalKey.B);
        Assert.That(_sut.WindowDecorations, Is.EqualTo(WindowDecorations.Full));
    }

    [AvaloniaTest]
    public void TopMostToggles()
    {
        Open();
        Press(PhysicalKey.T);
        Assert.That(_sut.Topmost, Is.True);
        Press(PhysicalKey.T);
        Assert.That(_sut.Topmost, Is.False);
    }

    [AvaloniaTest]
    public void TransparencyKeys_ApplyImmediately_AndStay()
    {
        Open();
        Press(PhysicalKey.Digit2);
        Assert.That(_sut.Opacity, Is.EqualTo(.1));
        Assert.That(_sut.Surface.ImageOpacity, Is.EqualTo(.1));
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Transparency: 1/5"));

        Press(PhysicalKey.Digit6);
        Assert.That(_sut.Surface.ImageOpacity, Is.EqualTo(.75));
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Transparency: 5/5"));

        using var wait = new CancellationTokenSource(1200);
        Dispatcher.UIThread.MainLoop(wait.Token);
        Assert.That(_sut.Surface.ImageOpacity, Is.EqualTo(.75), "the level should stay");
    }

    [AvaloniaTest]
    public void PressingTheSameTransparencyKeyAgain_MakesTheWindowSolid()
    {
        Open();
        Press(PhysicalKey.Digit3);
        Press(PhysicalKey.Digit3);
        Assert.That(_sut.Opacity, Is.EqualTo(1));
        Assert.That(_sut.Surface.ImageOpacity, Is.EqualTo(1));
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Transparency off"));
    }

    [AvaloniaTest]
    public void Transparency_FadesTheImageItself()
    {
        Open();
        Pump(() => HasSharpImage(_navigator.Current!));
        var solid = PixelAt(_sut.Width / 2, _sut.Height / 2);

        _sut.Surface.ImageOpacity = .3;
        Dispatcher.UIThread.RunJobs();

        Assert.That(PixelAt(_sut.Width / 2, _sut.Height / 2).Red, Is.LessThan(solid.Red * .6));
    }

    [AvaloniaTest]
    public void QuitCloses()
    {
        Open();
        Press(PhysicalKey.Q);
        Assert.That(_sut.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void AutoPlayShowsItsState()
    {
        Open();
        Press(PhysicalKey.A);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay ON"));
        Press(PhysicalKey.A);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay OFF"));
    }

    [AvaloniaTest]
    public void AutoPlaySpeedKeys_WorkOnTheNumpadAndTheMainKeyboard()
    {
        Open();
        Press(PhysicalKey.A);

        Press(PhysicalKey.NumPadAdd);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay interval: 1800ms"));
        Press(PhysicalKey.Equal);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay interval: 1620ms"));
        Press(PhysicalKey.Equal, RawInputModifiers.Shift);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay interval: 1458ms"));

        Press(PhysicalKey.NumPadSubtract);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay interval: 1621ms"));
        Press(PhysicalKey.Minus);
        Assert.That(_sut.Overlay.Text, Is.EqualTo("Autoplay interval: 1802ms"));
    }

    [AvaloniaTest]
    public void ClickThroughTurnsOff_WhenToggledOff()
    {
        Open();
        Press(PhysicalKey.W);
        Assert.That(_sut.Overlay.Text, Does.Contain("click-thru"));
        Press(PhysicalKey.W);
        _clickThrough.Verify(c => c.TrySet(It.IsAny<IPlatformHandle>(), false), Times.AtLeastOnce);
    }

    [AvaloniaTest]
    public void CopyFileNamePutsThePathOnTheClipboard()
    {
        Open();
        Press(PhysicalKey.C);
        Pump(() => _sut.Overlay.Text == "File name copied to clipboard");
    }

    [AvaloniaTest]
    public void ShuffleKeepsTheCurrentFileOnScreen()
    {
        Open();
        Press(PhysicalKey.S);
        Assert.That(_sut.Title, Is.EqualTo("a.jpg - Image Viewer"));
    }

    [AvaloniaTest]
    public void ACustomDeleteCommandRemovesTheFileFromTheList()
    {
        var settings = new Settings();
        settings.KeyBindings = new Dictionary<string, KeyBinding>
        {
            ["X"] = new KeyBinding { Command = "true {0}", IsDeleteAction = true }
        };
        Open(settings: settings);
        Press(PhysicalKey.X);
        Pump(() => _navigator.Count == 2);
    }

    [AvaloniaTest]
    public void DrawsTheImage_AndUsesItsEdgeColorForTheBackground()
    {
        Open();
        Pump(() => HasSharpImage(_navigator.Current!));
        var center = PixelAt(_sut.Width / 2, _sut.Height / 2);
        Assert.That(center.Red, Is.GreaterThan(45));
        Assert.That(center.Red, Is.LessThan(75));
        Assert.That(center.Blue, Is.LessThan(55));
    }

    [AvaloniaTest]
    public void TheNextImageIsPrefetchedBeforeMovingToIt()
    {
        Open();
        var next = _navigator.Upcoming!;
        Pump(() => HasSharpImage(next));

        Press(PhysicalKey.ArrowRight);
        Dispatcher.UIThread.RunJobs();

        var center = PixelAt(_sut.Width / 2, _sut.Height / 2);
        Assert.That(center.Red, Is.GreaterThan(105), "the next image should show on the same frame");
    }

    private bool HasSharpImage(string path)
    {
        var handle = _pipeline.AcquireBest(path);
        if (handle == null)
            return false;
        var sharp = handle.Tier >= ImageTier.Screen;
        handle.Release();
        return sharp;
    }

    private SKColor PixelAt(double x, double y)
    {
        using var frame = _sut.CaptureRenderedFrame()!;
        var path = Path.Combine(_folder, "frame.png");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        using var bitmap = SKBitmap.Decode(path);
        return bitmap.GetPixel((int)x, (int)y);
    }

    private sealed class FakeTrash : ITrash
    {
        public List<string> Trashed { get; } = new();
        public Exception? Fail { get; set; }

        public Task MoveToTrash(string fileName)
        {
            if (Fail != null)
                throw Fail;
            Trashed.Add(fileName);
            return Task.CompletedTask;
        }
    }
}
