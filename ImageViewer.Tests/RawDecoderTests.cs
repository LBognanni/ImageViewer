using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class RawDecoderTests
{
    private readonly RawImageDecoder _sut = new();

    [Test]
    public void FindsTheLargestJpeg()
    {
        var small = File.ReadAllBytes(TestImages.Jpeg(64, 32, SKColors.Red));
        var large = File.ReadAllBytes(TestImages.Jpeg(640, 320, SKColors.Blue));
        var file = new byte[100].Concat(small).Concat(new byte[50]).Concat(large).Concat(new byte[10]).ToArray();

        Assert.That(JpegPreviewFinder.FindLargest(file), Is.EqualTo((100 + small.Length + 50, 640, 320)));
    }

    [Test]
    public void SkipsLosslessJpeg()
    {
        // How DNG and CR2 store the sensor. The header is the only part that's looked at.
        byte[] lossless = { 0xFF, 0xD8, 0xFF, 0xC3, 0, 11, 8, 0x10, 0, 0x20, 0, 1, 1, 0x11, 0 };
        Assert.That(JpegPreviewFinder.FindLargest(lossless), Is.Null);
    }

    [Test]
    public void SurvivesFilesWithNoJpeg()
    {
        byte[] noise = { 0xFF, 0xD8, 0xFF, 0xE0, 0xFF, 0xFF, 0xD8, 0xFF };
        Assert.That(JpegPreviewFinder.FindLargest(noise), Is.Null);
        Assert.That(JpegPreviewFinder.FindLargest(Array.Empty<byte>()), Is.Null);
    }

    [Test]
    public void ShowsThePreviewWhenTheFileIsNotARawThatCanBeDeveloped()
    {
        var path = RawWithPreview(1600, 800);

        var info = _sut.Probe(path)!;
        Assert.That(info.Width, Is.EqualTo(1600));
        Assert.That(info.HasCheapScaling, Is.True);

        var decoded = _sut.Decode(path, new SKSizeI(400, 400))!;
        Assert.That(decoded.Image.Width, Is.EqualTo(400));
        Assert.That(decoded.Source.Width, Is.EqualTo(1600));
    }

    [Test]
    public void PreviewCanBeTheFullImage()
    {
        var path = RawWithPreview(320, 160);
        Assert.That(_sut.Decode(path, null)!.Image.Width, Is.EqualTo(320));
    }

    [Test]
    public void NoticesWhenAFileChanges()
    {
        var path = RawWithPreview(320, 160);
        Assert.That(_sut.Probe(path)!.Width, Is.EqualTo(320));

        File.WriteAllBytes(path, File.ReadAllBytes(RawWithPreview(640, 160)));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.That(_sut.Probe(path)!.Width, Is.EqualTo(640));
    }

    [Test]
    public void GarbageGivesNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"imageviewer-test-{Guid.NewGuid():N}.arw");
        File.WriteAllText(path, "not an image");
        Assert.That(_sut.Probe(path), Is.Null);
        Assert.That(_sut.Decode(path, null), Is.Null);
    }

    [Test]
    public void LeavesOtherExtensionsAlone()
    {
        Assert.That(_sut.Probe(TestImages.Jpeg(32, 16, SKColors.Blue)), Is.Null);
    }

    /// <summary>
    /// A JPEG in junk, which is all a raw file amounts to for the search. ImageMagick can't develop this.
    /// </summary>
    private static string RawWithPreview(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"imageviewer-test-{Guid.NewGuid():N}.arw");
        var jpeg = File.ReadAllBytes(TestImages.Jpeg(width, height, SKColors.Blue));
        File.WriteAllBytes(path, new byte[4096].Concat(jpeg).Concat(new byte[4096]).ToArray());
        return path;
    }
}
