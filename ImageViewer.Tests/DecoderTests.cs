using NUnit.Framework.Legacy;
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
        ClassicAssert.IsNotNull(decoded);
        ClassicAssert.AreEqual(10, decoded!.Image.Width);
        ClassicAssert.AreEqual(10, decoded.Source.Height);
    }

    [Test]
    public void ReturnsNullForSomethingThatIsNotAnImage()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "not an image");
        ClassicAssert.IsNull(_sut.Decode(path, null));
        ClassicAssert.IsNull(_sut.Probe(path));
    }

    [Test]
    public void JpegHasCheapScaling_AndDecodesSmallerThanFull()
    {
        var path = TestImages.Jpeg(1600, 800, SKColors.Blue);
        ClassicAssert.IsTrue(_sut.Probe(path)!.HasCheapScaling);

        var decoded = _sut.Decode(path, new SKSizeI(400, 400))!;
        ClassicAssert.AreEqual(400, decoded.Image.Width);
        ClassicAssert.AreEqual(1600, decoded.Source.Width);
    }

    [Test]
    public void ScaledDecodeNeverGoesBelowTheTarget()
    {
        var path = TestImages.Jpeg(1600, 800, SKColors.Blue);
        var decoded = _sut.Decode(path, new SKSizeI(500, 500))!;

        // The closest eighth above 500 is 3/8
        ClassicAssert.AreEqual(600, decoded.Image.Width);
    }

    [Test]
    public void PngHasNoCheapScaling_ButIsStillReducedToTheTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        using (var bitmap = new SKBitmap(800, 400))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(path, data.ToArray());
        }

        ClassicAssert.IsFalse(_sut.Probe(path)!.HasCheapScaling);
        ClassicAssert.AreEqual(200, _sut.Decode(path, new SKSizeI(200, 200))!.Image.Width);
    }

    [TestCase((ushort)1, SKEncodedOrigin.TopLeft)]
    [TestCase((ushort)3, SKEncodedOrigin.BottomRight)]
    [TestCase((ushort)6, SKEncodedOrigin.RightTop)]
    [TestCase((ushort)8, SKEncodedOrigin.LeftBottom)]
    public void ReadsExifOrientation(ushort orientation, SKEncodedOrigin expected)
    {
        var path = TestImages.Jpeg(32, 16, SKColors.Blue);
        TestImages.AddExifOrientation(path, orientation);
        ClassicAssert.AreEqual(expected, _sut.Probe(path)!.Origin);
    }

    [Test]
    public void Chain_TriesTheDecoderForTheExtensionFirst()
    {
        var calls = new List<string>();
        var other = FakeDecoder(".abc", calls, "other", null);
        var owner = FakeDecoder(".xyz", calls, "owner", TestImages.Info(1, 1));

        var info = new DecoderChain(new[] { other.Object, owner.Object }).Probe("file.XYZ");

        ClassicAssert.IsNotNull(info);
        ClassicAssert.AreEqual(new[] { "owner" }, calls);
    }

    [Test]
    public void Chain_FallsBackToOtherDecoders()
    {
        var calls = new List<string>();
        var owner = FakeDecoder(".xyz", calls, "owner", null);
        var other = FakeDecoder(".abc", calls, "other", TestImages.Info(1, 1));

        var info = new DecoderChain(new[] { other.Object, owner.Object }).Probe("file.xyz");

        ClassicAssert.IsNotNull(info);
        ClassicAssert.AreEqual(new[] { "owner", "other" }, calls);
    }

    [Test]
    public void Chain_ExtensionsAreTheUnionOfAllDecoders()
    {
        var chain = new DecoderChain(new[]
        {
            FakeDecoder(".abc", new(), "a", null).Object,
            FakeDecoder(".xyz", new(), "b", null).Object
        });
        ClassicAssert.IsTrue(chain.Extensions.Contains(".ABC"));
        ClassicAssert.IsTrue(chain.Extensions.Contains(".xyz"));
    }

    private static Mock<IImageDecoder> FakeDecoder(string extension, List<string> calls, string name, ImageInfo? result)
    {
        var decoder = new Mock<IImageDecoder>();
        decoder.Setup(d => d.Extensions).Returns(new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { extension });
        decoder.Setup(d => d.Probe(It.IsAny<string>())).Callback(() => calls.Add(name)).Returns(result);
        return decoder;
    }
}
