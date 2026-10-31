using System.Collections.Generic;
using System.IO;
using Moq;
using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class DecoderTests : LocalPathTests
{
    private readonly SkiaImageDecoder _sut = new();

    [Test]
    public void CanDecodeAFile()
    {
        var decoded = _sut.Decode(LocalPath(Path.Combine("testimages", "red.png")), null);
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Image.Width, Is.EqualTo(10));
        Assert.That(decoded.Source.Height, Is.EqualTo(10));
    }

    [Test]
    public void ReturnsNullForSomethingThatIsNotAnImage()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "not an image");
        Assert.That(_sut.Decode(path, null), Is.Null);
        Assert.That(_sut.Probe(path), Is.Null);
    }

    [Test]
    public void JpegHasCheapScaling_AndDecodesSmallerThanFull()
    {
        var path = TestImages.Jpeg(1600, 800, SKColors.Blue);
        Assert.That(_sut.Probe(path)!.HasCheapScaling, Is.True);

        var decoded = _sut.Decode(path, new SKSizeI(400, 400))!;
        Assert.That(decoded.Image.Width, Is.EqualTo(400));
        Assert.That(decoded.Source.Width, Is.EqualTo(1600));
    }

    [Test]
    public void ScaledDecodeNeverGoesBelowTheTarget()
    {
        var path = TestImages.Jpeg(1600, 800, SKColors.Blue);
        var decoded = _sut.Decode(path, new SKSizeI(500, 500))!;

        // The closest eighth above 500 is 3/8
        Assert.That(decoded.Image.Width, Is.EqualTo(600));
    }

    [Test]
    public void PngHasNoCheapScaling_SoItStaysAtFullSize()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        using (var bitmap = new SKBitmap(800, 400))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(path, data.ToArray());
        }

        Assert.That(_sut.Probe(path)!.HasCheapScaling, Is.False);
        Assert.That(_sut.Decode(path, new SKSizeI(200, 200))!.Image.Width, Is.EqualTo(800));
    }

    [TestCase((ushort)1, SKEncodedOrigin.TopLeft)]
    [TestCase((ushort)3, SKEncodedOrigin.BottomRight)]
    [TestCase((ushort)6, SKEncodedOrigin.RightTop)]
    [TestCase((ushort)8, SKEncodedOrigin.LeftBottom)]
    public void ReadsExifOrientation(ushort orientation, SKEncodedOrigin expected)
    {
        var path = TestImages.Jpeg(32, 16, SKColors.Blue);
        TestImages.AddExifOrientation(path, orientation);
        Assert.That(_sut.Probe(path)!.Origin, Is.EqualTo(expected));
    }

    [Test]
    public void Chain_TriesTheDecoderForTheExtensionFirst()
    {
        var calls = new List<string>();
        var other = FakeDecoder(".abc", calls, "other", null);
        var owner = FakeDecoder(".xyz", calls, "owner", TestImages.Info(1, 1));

        var info = new DecoderChain(new[] { other.Object, owner.Object }).Probe("file.XYZ");

        Assert.That(info, Is.Not.Null);
        Assert.That(calls, Is.EqualTo(new[] { "owner" }));
    }

    [Test]
    public void Chain_FallsBackToOtherDecoders()
    {
        var calls = new List<string>();
        var owner = FakeDecoder(".xyz", calls, "owner", null);
        var other = FakeDecoder(".abc", calls, "other", TestImages.Info(1, 1));

        var info = new DecoderChain(new[] { other.Object, owner.Object }).Probe("file.xyz");

        Assert.That(info, Is.Not.Null);
        Assert.That(calls, Is.EqualTo(new[] { "owner", "other" }));
    }

    [Test]
    public void Chain_ExtensionsAreTheUnionOfAllDecoders()
    {
        var chain = new DecoderChain(new[]
        {
            FakeDecoder(".abc", new(), "a", null).Object,
            FakeDecoder(".xyz", new(), "b", null).Object
        });
        Assert.That(chain.Extensions.Contains(".ABC"), Is.True);
        Assert.That(chain.Extensions.Contains(".xyz"), Is.True);
    }

    private static Mock<IImageDecoder> FakeDecoder(string extension, List<string> calls, string name, ImageInfo? result)
    {
        var decoder = new Mock<IImageDecoder>();
        decoder.Setup(d => d.Extensions).Returns(new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { extension });
        decoder.Setup(d => d.Probe(It.IsAny<string>())).Callback(() => calls.Add(name)).Returns(result);
        return decoder;
    }
}
