using System;
using System.IO;
using System.Linq;
using ImageMagick;
using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class MagickDecoderTests
{
    private readonly MagickImageDecoder _sut = new();

    [Test]
    public void EveryExtensionHasAFormatThatCanBeRead()
    {
        var readable = MagickNET.SupportedFormats.Where(f => f.SupportsReading).Select(f => f.Format).ToHashSet();
        var chain = Program.CreateDecoder();
        foreach (var extension in chain.Extensions.Where(_sut.Extensions.Contains))
        {
            var format = Enum.Parse<MagickFormat>(TypeName(extension), true);
            Assert.That(readable, Does.Contain(format), extension);
        }
    }

    [TestCase(MagickFormat.Tiff, ".tif")]
    [TestCase(MagickFormat.Tga, ".tga")]
    [TestCase(MagickFormat.Psd, ".psd")]
    [TestCase(MagickFormat.Pcx, ".pcx")]
    [TestCase(MagickFormat.Ppm, ".ppm")]
    [TestCase(MagickFormat.Qoi, ".qoi")]
    [TestCase(MagickFormat.Jp2, ".jp2")]
    [TestCase(MagickFormat.Jxl, ".jxl")]
    [TestCase(MagickFormat.Avif, ".avif")]
    [TestCase(MagickFormat.Exr, ".exr")]
    public void DecodesAndKeepsColors(MagickFormat format, string extension)
    {
        var path = Write(format, extension, MagickColors.Red);

        var info = _sut.Probe(path);
        var decoded = _sut.Decode(path, null);

        Assert.That(info!.Width, Is.EqualTo(WritesWidth));
        Assert.That(info.HasCheapScaling, Is.False);
        AssertPixel(decoded!, SKColors.Red);
    }

    [TestCase(WritesWidth)]
    [TestCase(WritesWidth + 1)]
    public void RgbConversionReachesTheLastPixel(int width)
    {
        // Odd sizes leave a remainder that the fast path doesn't cover
        using var image = new MagickImage(MagickColors.Blue, (uint)width, 3);
        image.Format = MagickFormat.Tiff;
        var path = TempPath(".tif");
        image.Write(path);

        using var pixels = _sut.Decode(path, null)!.Image.PeekPixels();
        Assert.That(pixels.GetPixelColor(width - 1, 2), Is.EqualTo(SKColors.Blue));
    }

    [Test]
    public void GrayImagesComeOutGray()
    {
        using var image = new MagickImage(new MagickColor("#808080"), WritesWidth, 8);
        image.ColorType = ColorType.Grayscale;
        image.Format = MagickFormat.Tiff;
        var path = TempPath(".tif");
        image.Write(path);

        AssertPixel(_sut.Decode(path, null)!, new SKColor(0x80, 0x80, 0x80));
    }

    [Test]
    public void CmykIsConvertedForTheScreen()
    {
        using var image = new MagickImage(MagickColors.Red, WritesWidth, 8);
        image.ColorSpace = ColorSpace.CMYK;
        image.Format = MagickFormat.Tiff;
        var path = TempPath(".tif");
        image.Write(path);

        AssertPixel(_sut.Decode(path, null)!, SKColors.Red);
    }

    [Test]
    public void TransparencyIsPremultiplied()
    {
        using var image = new MagickImage(new MagickColor("#FF000080"), WritesWidth, 8);
        image.Format = MagickFormat.Tga;
        var path = TempPath(".tga");
        image.Write(path);

        using var pixels = _sut.Decode(path, null)!.Image.PeekPixels();
        var raw = pixels.GetPixelSpan<uint>()[0];
        var (alpha, red) = (raw >> 24, SKImageInfo.PlatformColorType == SKColorType.Bgra8888 ? raw >> 16 & 0xFF : raw & 0xFF);
        Assert.That(alpha, Is.EqualTo(0x80).Within(1));
        Assert.That(red, Is.EqualTo(alpha).Within(1));
    }

    [Test]
    public void ReadsOrientationFromTheFile()
    {
        using var image = new MagickImage(MagickColors.Red, 16, 8);
        image.Orientation = Orientation.RightTop;
        image.Format = MagickFormat.Tiff;
        var path = TempPath(".tif");
        image.Write(path);

        Assert.That(_sut.Probe(path)!.Origin, Is.EqualTo(SKEncodedOrigin.RightTop));
    }

    [Test]
    public void ReadsTheFirstPage()
    {
        using var pages = new MagickImageCollection { new MagickImage(MagickColors.Red, 16, 8), new MagickImage(MagickColors.Lime, 32, 8) };
        var path = TempPath(".tif");
        pages.Write(path, MagickFormat.Tiff);

        var decoded = _sut.Decode(path, null)!;
        Assert.That(decoded.Image.Width, Is.EqualTo(16));
        AssertPixel(decoded, SKColors.Red);
    }

    [Test]
    public void SvgKeepsItsTransparentBackground()
    {
        var path = TempPath(".svg");
        File.WriteAllText(path, """<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"><rect x="10" y="0" width="10" height="20" fill="#f00"/></svg>""");

        using var pixels = _sut.Decode(path, null)!.Image.PeekPixels();
        Assert.That(pixels.GetPixelColor(2, 10).Alpha, Is.EqualTo(0));
        Assert.That(pixels.GetPixelColor(16, 10), Is.EqualTo(SKColors.Red));
    }

    [Test]
    public void PathsAreNotInterpreted()
    {
        var path = Write(MagickFormat.Tiff, "[0].tif", MagickColors.Red);
        Assert.That(_sut.Decode(path, null), Is.Not.Null);
    }

    [Test]
    public void DamagedFilesGiveNull()
    {
        var path = TempPath(".tif");
        File.WriteAllText(path, "not an image");
        Assert.That(_sut.Probe(path), Is.Null);
        Assert.That(_sut.Decode(path, null), Is.Null);
    }

    [Test]
    public void ScriptsAreNeverRun()
    {
        var target = TempPath(".png");
        var path = TempPath(".jpg");
        File.WriteAllText(path, $"push graphic-context\nviewbox 0 0 8 8\nimage over 0,0 0,0 '{target}'\npop graphic-context\n");
        Assert.That(_sut.Decode(path, null), Is.Null);

        var mvg = TempPath(".tga");
        File.Copy(path, mvg);
        Assert.That(_sut.Decode(mvg, null), Is.Null);
    }

    [Test]
    public void AWrongExtensionIsDetected()
    {
        var path = Write(MagickFormat.Tiff, ".xyz", MagickColors.Red);
        AssertPixel(_sut.Decode(path, null)!, SKColors.Red);
    }

    [Test]
    public void Chain_FindsWhatAFileReallyIs()
    {
        var chain = Program.CreateDecoder();

        // A JPEG named as a format ImageMagick owns, and the other way round
        var jpeg = TempPath(".heic");
        File.Move(TestImages.Jpeg(32, 16, SKColors.Blue), jpeg);
        var tiff = Write(MagickFormat.Tiff, ".jpg", MagickColors.Red);

        Assert.That(chain.Probe(jpeg)!.HasCheapScaling, Is.True);
        Assert.That(chain.Decode(jpeg, null)!.Image.Width, Is.EqualTo(32));
        Assert.That(chain.Decode(tiff, null), Is.Not.Null);
    }

    private const int WritesWidth = 8;

    private static string TypeName(string extension) => extension switch
    {
        ".dib" => "Dib",
        ".hif" => "Heif",
        ".jpf" or ".jpx" => "Jp2",
        _ => extension[1..]
    };

    private static void AssertPixel(DecodedImage decoded, SKColor expected)
    {
        using var pixels = decoded.Image.PeekPixels();
        var actual = pixels.GetPixelColor(1, 1);
        Assert.That(actual.Red, Is.EqualTo(expected.Red).Within(2));
        Assert.That(actual.Green, Is.EqualTo(expected.Green).Within(2));
        Assert.That(actual.Blue, Is.EqualTo(expected.Blue).Within(2));
    }

    private static string Write(MagickFormat format, string extension, MagickColor color)
    {
        using var image = new MagickImage(color, WritesWidth, WritesWidth);
        var path = TempPath(extension);
        image.Format = format;
        image.Quality = 100;
        using (var stream = File.Create(path))
        {
            image.Write(stream);
        }
        return path;
    }

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"imageviewer-test-{Guid.NewGuid():N}{extension}");
}
