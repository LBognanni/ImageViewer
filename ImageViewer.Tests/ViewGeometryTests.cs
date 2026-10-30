using NUnit.Framework.Legacy;
using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class ViewGeometryTests
{
    [Test]
    public void BestFit_LeavesSmallImagesAtActualSize()
    {
        ClassicAssert.AreEqual(1f, ViewGeometry.EffectiveZoom(TestImages.Info(100, 100), 0, 0, 800, 600));
    }

    [Test]
    public void BestFit_ScalesLargeImagesDown()
    {
        ClassicAssert.AreEqual(0.5f, ViewGeometry.EffectiveZoom(TestImages.Info(1600, 800), 0, 0, 800, 600));
    }

    [Test]
    public void BestFit_AccountsForRotation()
    {
        ClassicAssert.AreEqual(0.5f, ViewGeometry.EffectiveZoom(TestImages.Info(800, 1600), 90, 0, 800, 800));
    }

    [Test]
    public void BestFit_AccountsForExifOrientation()
    {
        var info = TestImages.Info(1600, 800, SKEncodedOrigin.RightTop);
        ClassicAssert.AreEqual((800, 1600), ViewGeometry.DisplaySize(info, 0));
        ClassicAssert.AreEqual((1600, 800), ViewGeometry.DisplaySize(info, 90));
    }

    [Test]
    public void ActualSize_IsOneToOneInPhysicalPixels()
    {
        // At 200% scaling the view is 1600x1200 physical pixels, so a 100px image covers 100 of them
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 100), 100, 100, 0, 1, default, 1600, 1200);
        var corner = matrix.MapPoint(0, 0);
        var opposite = matrix.MapPoint(100, 100);
        ClassicAssert.AreEqual(100, opposite.X - corner.X);
        ClassicAssert.AreEqual(100, opposite.Y - corner.Y);
    }

    [Test]
    public void SmallerDecodes_CoverTheSameAreaAsTheFullImage()
    {
        var info = TestImages.Info(4000, 2000);
        var full = ViewGeometry.Transform(info, 4000, 2000, 0, 0, default, 800, 600).MapPoint(4000, 2000);
        var quarter = ViewGeometry.Transform(info, 1000, 500, 0, 0, default, 800, 600).MapPoint(1000, 500);
        ClassicAssert.AreEqual(full.X, quarter.X, 1);
        ClassicAssert.AreEqual(full.Y, quarter.Y, 1);
    }

    [Test]
    public void ImageIsCentered()
    {
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 0, 1, default, 800, 600);
        var center = matrix.MapPoint(50, 25);
        ClassicAssert.AreEqual(400, center.X, 1);
        ClassicAssert.AreEqual(300, center.Y, 1);
    }

    [Test]
    public void PanMovesTheImage()
    {
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 0, 1, new SKPoint(10, -20), 800, 600);
        var center = matrix.MapPoint(50, 25);
        ClassicAssert.AreEqual(410, center.X, 1);
        ClassicAssert.AreEqual(280, center.Y, 1);
    }

    [Test]
    public void Rotation90_IsClockwise()
    {
        // The top left corner of the image ends up at the top right
        var matrix = ViewGeometry.Transform(TestImages.Info(100, 50), 100, 50, 90, 1, default, 800, 600);
        var point = matrix.MapPoint(0, 0);
        ClassicAssert.Greater(point.X, 400);
        ClassicAssert.Less(point.Y, 300);
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
        ClassicAssert.AreEqual(right, point.X > 400, "horizontal");
        ClassicAssert.AreEqual(bottom, point.Y > 300, "vertical");
    }
}
