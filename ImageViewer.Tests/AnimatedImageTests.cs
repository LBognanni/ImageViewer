using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class AnimatedImageTests
{
    private static AnimatedImage Open(params GifFrame[] frames) =>
        AnimatedImage.TryOpen(TestImages.Gif(4, 4, frames))!;

    private static SKColor Pixel(AnimationFrame frame, int x, int y)
    {
        using var bitmap = SKBitmap.FromImage(frame.Image);
        return bitmap.GetPixel(x, y);
    }

    [Test]
    public void AFileWithOneFrameIsNotAnAnimation()
    {
        var path = TestImages.Gif(4, 4, new GifFrame(0, 0, 4, 4, 1));
        Assert.That(AnimatedImage.TryOpen(path), Is.Null);
    }

    [Test]
    public void FramesBuildOnEachOther()
    {
        using var sut = Open(new GifFrame(0, 0, 4, 4, 1), new GifFrame(0, 0, 2, 2, 3), new GifFrame(2, 2, 2, 2, 2));
        sut.Next();
        var last = sut.Next()!;
        Assert.That(Pixel(last, 0, 0), Is.EqualTo(SKColors.Blue));
        Assert.That(Pixel(last, 3, 0), Is.EqualTo(SKColors.Red));
        Assert.That(Pixel(last, 3, 3), Is.EqualTo(SKColors.Lime));
    }

    [Test]
    public void AFrameCanRestoreWhatWasBeforeIt()
    {
        using var sut = Open(new GifFrame(0, 0, 4, 4, 1), new GifFrame(0, 0, 2, 2, 3, Disposal: 3), new GifFrame(2, 2, 2, 2, 2));
        var second = sut.Next()!;
        var last = sut.Next()!;
        Assert.That(Pixel(second, 0, 0), Is.EqualTo(SKColors.Blue));
        Assert.That(Pixel(last, 0, 0), Is.EqualTo(SKColors.Red));
        Assert.That(Pixel(last, 3, 3), Is.EqualTo(SKColors.Lime));
    }

    [Test]
    public void LoopingStartsOverCleanly()
    {
        using var sut = Open(new GifFrame(0, 0, 4, 4, 1), new GifFrame(0, 0, 2, 2, 3));
        sut.Next();
        Assert.That(Pixel(sut.Next()!, 0, 0), Is.EqualTo(SKColors.Red));
        Assert.That(Pixel(sut.Next()!, 0, 0), Is.EqualTo(SKColors.Blue));
    }

    [Test]
    public void SlowsDownFramesWithNoDelay()
    {
        using var sut = Open(new GifFrame(0, 0, 4, 4, 1, 0), new GifFrame(0, 0, 4, 4, 2, 70));
        Assert.That(sut.FirstDelayMs, Is.EqualTo(100));
        Assert.That(sut.Next()!.DelayMs, Is.EqualTo(70));
    }
}
