using NUnit.Framework.Legacy;
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
            _trash, _clickThrough.Object, decoder.Extensions, null, () => { });
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
        ClassicAssert.AreEqual("a.jpg - Image Viewer", _sut.Title);
    }

    [AvaloniaTest]
    public void RightAndLeftMoveThroughTheFolder()
    {
        Open();
        Press(PhysicalKey.ArrowRight);
        ClassicAssert.AreEqual("b.jpg - Image Viewer", _sut.Title);
        Press(PhysicalKey.ArrowLeft);
        Press(PhysicalKey.ArrowLeft);
        ClassicAssert.AreEqual("c.jpg - Image Viewer", _sut.Title);
    }

    [AvaloniaTest]
    public void UnboundKeysDoNothing()
    {
        Open();
        Press(PhysicalKey.J);
        ClassicAssert.AreEqual("a.jpg - Image Viewer", _sut.Title);
    }

    [AvaloniaTest]
    public void RotateKeys()
    {
        Open();
        Press(PhysicalKey.R);
        ClassicAssert.AreEqual(270, _sut.Surface.Rotation);
        Press(PhysicalKey.R, RawInputModifiers.Shift);
        Press(PhysicalKey.R, RawInputModifiers.Shift);
        ClassicAssert.AreEqual(90, _sut.Surface.Rotation);
    }

    [AvaloniaTest]
    public void MovingResetsTheView()
    {
        Open();
        Press(PhysicalKey.R);
        Press(PhysicalKey.Digit1);
        Press(PhysicalKey.ArrowRight);
        ClassicAssert.AreEqual(0, _sut.Surface.Rotation);
        ClassicAssert.AreEqual(0m, _sut.Surface.Zoom);
    }

    [AvaloniaTest]
    public void ToggleZoomSwitchesBetweenFitAndActualSize()
    {
        Open();
        Press(PhysicalKey.Digit1);
        ClassicAssert.AreEqual(1m, _sut.Surface.Zoom);
        Press(PhysicalKey.Digit1);
        ClassicAssert.AreEqual(0m, _sut.Surface.Zoom);
    }

    [AvaloniaTest]
    public void HelpListsTheKeyBindings()
    {
        Open();
        Press(PhysicalKey.H);
        StringAssert.Contains("Rotate Image clockwise", _sut.Overlay.Text);
        StringAssert.Contains("[⇧]+[R]", _sut.Overlay.Text);
    }

    [AvaloniaTest]
    public void HelpSkipsActionsWithNoBinding()
    {
        var settings = new Settings();
        settings.KeyBindings = settings.KeyBindings.Where(k => k.Key != "R").ToDictionary(k => k.Key, k => k.Value);
        Open(settings: settings);
        Press(PhysicalKey.H);
        StringAssert.DoesNotContain("Rotate Image clockwise", _sut.Overlay.Text);
    }

    [AvaloniaTest]
    public void DeleteSendsTheFileToTheTrashAndMovesOn()
    {
        Open();
        var first = _navigator.Current!;
        Press(PhysicalKey.D);
        Pump(() => _navigator.Count == 2);
        ClassicAssert.AreEqual(new[] { first }, _trash.Trashed);
        ClassicAssert.AreEqual("b.jpg - Image Viewer", _sut.Title);
    }

    [AvaloniaTest]
    public void AFailedDeleteKeepsTheFile()
    {
        Open();
        _trash.Fail = new OperationCanceledException();
        Press(PhysicalKey.D);
        Dispatcher.UIThread.RunJobs();
        ClassicAssert.AreEqual(3, _navigator.Count);
    }

    [AvaloniaTest]
    public void FullScreenToggles_AndEscapeLeavesItBeforeClosing()
    {
        Open();
        Press(PhysicalKey.F);
        ClassicAssert.AreEqual(WindowState.FullScreen, _sut.WindowState);
        ClassicAssert.IsTrue(_sut.Topmost);

        Press(PhysicalKey.Escape);
        ClassicAssert.AreEqual(WindowState.Maximized, _sut.WindowState);
        ClassicAssert.IsFalse(_sut.Topmost);
        ClassicAssert.IsTrue(_sut.IsVisible);

        Press(PhysicalKey.Escape);
        ClassicAssert.IsFalse(_sut.IsVisible);
    }

    [AvaloniaTest]
    public void BorderlessToggles()
    {
        Open();
        Press(PhysicalKey.B);
        ClassicAssert.AreEqual(WindowDecorations.None, _sut.WindowDecorations);
        Press(PhysicalKey.B);
        ClassicAssert.AreEqual(WindowDecorations.Full, _sut.WindowDecorations);
    }

    [AvaloniaTest]
    public void TopMostToggles()
    {
        Open();
        Press(PhysicalKey.T);
        ClassicAssert.IsTrue(_sut.Topmost);
        Press(PhysicalKey.T);
        ClassicAssert.IsFalse(_sut.Topmost);
    }

    [AvaloniaTest]
    public void TransparencyKeys_ApplyImmediately_AndStay()
    {
        Open();
        Press(PhysicalKey.Digit2);
        ClassicAssert.AreEqual(.1, _sut.Opacity);
        ClassicAssert.AreEqual(.1, _sut.Surface.ImageOpacity);
        ClassicAssert.AreEqual("Transparency: 1/5", _sut.Overlay.Text);

        Press(PhysicalKey.Digit6);
        ClassicAssert.AreEqual(.75, _sut.Surface.ImageOpacity);
        ClassicAssert.AreEqual("Transparency: 5/5", _sut.Overlay.Text);

        using var wait = new CancellationTokenSource(1200);
        Dispatcher.UIThread.MainLoop(wait.Token);
        ClassicAssert.AreEqual(.75, _sut.Surface.ImageOpacity, "the level should stay");
    }

    [AvaloniaTest]
    public void PressingTheSameTransparencyKeyAgain_MakesTheWindowSolid()
    {
        Open();
        Press(PhysicalKey.Digit3);
        Press(PhysicalKey.Digit3);
        ClassicAssert.AreEqual(1, _sut.Opacity);
        ClassicAssert.AreEqual(1, _sut.Surface.ImageOpacity);
        ClassicAssert.AreEqual("Transparency off", _sut.Overlay.Text);
    }

    [AvaloniaTest]
    public void Transparency_FadesTheImageItself()
    {
        Open();
        Pump(() => HasSharpImage(_navigator.Current!));
        var solid = PixelAt(_sut.Width / 2, _sut.Height / 2);

        _sut.Surface.ImageOpacity = .3;
        Dispatcher.UIThread.RunJobs();

        ClassicAssert.Less(PixelAt(_sut.Width / 2, _sut.Height / 2).Red, solid.Red * .6);
    }

    [AvaloniaTest]
    public void QuitCloses()
    {
        Open();
        Press(PhysicalKey.Q);
        ClassicAssert.IsFalse(_sut.IsVisible);
    }

    [AvaloniaTest]
    public void AutoPlayShowsItsState()
    {
        Open();
        Press(PhysicalKey.A);
        ClassicAssert.AreEqual("Autoplay ON", _sut.Overlay.Text);
        Press(PhysicalKey.A);
        ClassicAssert.AreEqual("Autoplay OFF", _sut.Overlay.Text);
    }

    [AvaloniaTest]
    public void AutoPlaySpeedKeys_WorkOnTheNumpadAndTheMainKeyboard()
    {
        Open();
        Press(PhysicalKey.A);

        Press(PhysicalKey.NumPadAdd);
        ClassicAssert.AreEqual("Autoplay interval: 1800ms", _sut.Overlay.Text);
        Press(PhysicalKey.Equal);
        ClassicAssert.AreEqual("Autoplay interval: 1620ms", _sut.Overlay.Text);
        Press(PhysicalKey.Equal, RawInputModifiers.Shift);
        ClassicAssert.AreEqual("Autoplay interval: 1458ms", _sut.Overlay.Text);

        Press(PhysicalKey.NumPadSubtract);
        ClassicAssert.AreEqual("Autoplay interval: 1621ms", _sut.Overlay.Text);
        Press(PhysicalKey.Minus);
        ClassicAssert.AreEqual("Autoplay interval: 1802ms", _sut.Overlay.Text);
    }

    [AvaloniaTest]
    public void ClickThroughTurnsOff_WhenToggledOff()
    {
        Open();
        Press(PhysicalKey.W);
        StringAssert.Contains("click-thru", _sut.Overlay.Text);
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
        ClassicAssert.AreEqual("a.jpg - Image Viewer", _sut.Title);
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
        ClassicAssert.Greater(center.Red, 45);
        ClassicAssert.Less(center.Red, 75);
        ClassicAssert.Less(center.Blue, 55);
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
        ClassicAssert.Greater(center.Red, 105, "the next image should show on the same frame");
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
        frame.Save(path);
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
