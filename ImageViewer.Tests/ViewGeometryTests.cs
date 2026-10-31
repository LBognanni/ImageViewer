using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class ViewGeometryTests
{
    [Test]
    public void BestFit_LeavesSmallImagesAtActualSize()
    {
        Assert.That(ViewGeometry.EffectiveZoom(TestImages.Info(100, 100), 0, 0, 800, 600), Is.EqualTo(1f));
    }

    [Test]
    public void BestFit_ScalesLargeImagesDown()
    {
        Assert.That(ViewGeometry.EffectiveZoom(TestImages.Info(1600, 800), 0, 0, 800, 600), Is.EqualTo(0.5f));
    }

    [Test]
    public void BestFit_AccountsForRotation()
    {
        Assert.That(ViewGeometry.EffectiveZoom(TestImages.Info(800, 1600), 90, 0, 800, 800), Is.EqualTo(0.5f));
    }

    [Test]
    public void BestFit_AccountsForExifOrientation()
    {
        var info = TestImages.Info(1600, 800, SKEncodedOrigin.RightTop);
        Assert.That(ViewGeometry.DisplaySize(info, 0), Is.EqualTo((800, 1600)));
        Assert.That(ViewGeometry.DisplaySize(info, 90), Is.EqualTo((1600, 800)));
    }

    [Test]
    public void ActualSize_IsOneToOneInPhysicalPixels()
    {
        // At 200% scaling the view is 1600x1200 physical pixels, so a 100px image covers 100 of them
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 100), 100, 100, 0, 1, default, 1600, 1200);
        var corner = matrix.MapPoint(0, 0);
        var opposite = matrix.MapPoint(100, 100);
        Assert.That(opposite.X - corner.X, Is.EqualTo(100));
        Assert.That(opposite.Y - corner.Y, Is.EqualTo(100));
    }

    [Test]
    public void SmallerDecodes_CoverTheSameAreaAsTheFullImage()
    {
        var info = TestImages.Info(4000, 2000);
        var full = ViewGeometry.Transform(info, 4000, 2000, 0, 0, default, 800, 600).MapPoint(4000, 2000);
        var quarter = ViewGeometry.Transform(info, 1000, 500, 0, 0, default, 800, 600).MapPoint(1000, 500);
        Assert.That(quarter.X, Is.EqualTo(full.X).Within(1));
        Assert.That(quarter.Y, Is.EqualTo(full.Y).Within(1));
    }

    [Test]
    public void ImageIsCentered()
    {
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 0, 1, default, 800, 600);
        var center = matrix.MapPoint(50, 25);
        Assert.That(center.X, Is.EqualTo(400).Within(1));
        Assert.That(center.Y, Is.EqualTo(300).Within(1));
    }

    [Test]
    public void PanMovesTheImage()
    {
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 0, 1, new SKPoint(10, -20), 800, 600);
        var center = matrix.MapPoint(50, 25);
        Assert.That(center.X, Is.EqualTo(410).Within(1));
        Assert.That(center.Y, Is.EqualTo(280).Within(1));
    }

    [Test]
    public void Rotation90_IsClockwise()
    {
        // The top left corner of the image ends up at the top right
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 90, 1, default, 800, 600);
        var point = matrix.MapPoint(0, 0);
        Assert.That(point.X, Is.GreaterThan(400));
        Assert.That(point.Y, Is.LessThan(300));
    }

    [TestCase(SKEncodedOrigin.TopLeft, false, false)]
    [TestCase(SKEncodedOrigin.TopRight, true, false)]
    [TestCase(SKEncodedOrigin.BottomRight, true, true)]
    [TestCase(SKEncodedOrigin.BottomLeft, false, true)]
    [TestCase(SKEncodedOrigin.LeftTop, false, false)]
    [TestCase(SKEncodedOrigin.RightTop, true, false)]
    [TestCase(SKEncodedOrigin.RightBottom, true, true)]
    [TestCase(SKEncodedOrigin.LeftBottom, false, true)]
    public void ExifOrientation_PutsTheFirstPixelInTheRightCorner(SKEncodedOrigin origin, bool right, bool bottom)
    {
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50, origin), 100, 50, 0, 1, default, 800, 600);
        var point = matrix.MapPoint(0, 0);
        Assert.That(point.X > 400, Is.EqualTo(right), "horizontal");
        Assert.That(point.Y > 300, Is.EqualTo(bottom), "vertical");
    }
}
