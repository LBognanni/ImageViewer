using NUnit.Framework;
using System.Drawing;

namespace ImageViewer.Tests
{
    [TestFixture]
    public class ImageLoaderTests : LocalPathTests
    {
        [Test]
        public void ImageLoader_CanLoadAnImage()
        {
            var sut = new ImageLoader();
            var img = sut.LoadImage(LocalPath("testimages\\red.png"));
            Assert.IsNotNull(img.Image);
            Assert.AreEqual(Color.Red.ToArgb(), img.AverageColor.ToArgb());
            Assert.AreEqual(10, img.ActualWidth);
            Assert.AreEqual(10, img.ActualHeight);
            Assert.AreEqual(true, img.IsFullResImage);
        }

        [Test]
        public void QuickImageLoader_CanLoadAnImage()
        {
            var sut = new QuickImageLoader();
            var img = sut.LoadImage(LocalPath("testimages\\red.png"));
            Assert.IsNotNull(img.Image);
            //Assert.AreEqual(Color.Red, img.AverageColor);
            Assert.AreEqual(10, img.ActualWidth);
            Assert.AreEqual(10, img.ActualHeight);
            Assert.AreEqual(false, img.IsFullResImage);
        }
    }
}
