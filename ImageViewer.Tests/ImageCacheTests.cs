using NUnit.Framework;
using Moq;
using System;
using System.Threading.Tasks;

namespace ImageViewer.Tests
{
    [TestFixture]
    public class ImageCacheTests
    {
        [Test]
        public void ImageCache_LoadsAnImage()
        {
            var mock = new Mock<IImageLoader>();
            mock
                .Setup(l=>l.LoadImage(It.IsAny<string>()))
                .Returns(new ImageMeta());
            ImageCache cache = new ImageCache(mock.Object, 1);
            cache.GetOrLoadImage("test.png");
            mock.Verify(m => m.LoadImage(It.IsAny<string>()), Times.Exactly(1));            
        }


        [Test]
        public void ImageCache_LoadsTheSameImageOnlyOnce()
        {
            var mock = new Mock<IImageLoader>();
            mock
                .Setup(l => l.LoadImage(It.IsAny<string>()))
                .Returns(new ImageMeta());
            
            ImageCache cache = new ImageCache(mock.Object, 1);
            cache.GetOrLoadImage("test.png");
            cache.GetOrLoadImage("test.png");
            mock.Verify(m => m.LoadImage(It.IsAny<string>()), Times.Exactly(1));
        }

        [Test]
        public void ImageCache_DropsOldImages()
        {
            var mock = new Mock<IImageLoader>();
            mock
                .Setup(l => l.LoadImage(It.IsAny<string>()))
                .Returns(new ImageMeta());

            ImageCache cache = new ImageCache(mock.Object, 1);
            cache.GetOrLoadImage("test1.png");
            cache.GetOrLoadImage("test1.png");
            cache.GetOrLoadImage("test2.png");
            cache.GetOrLoadImage("test1.png");
            mock.Verify(m => m.LoadImage(It.IsAny<string>()), Times.Exactly(3));
        }

        [Test]
        public void ImageCache_KeepsMostUsedImage()
        {
            var mock = new Mock<IImageLoader>();
            mock
                .Setup(l => l.LoadImage(It.IsAny<string>()))
                .Returns(new ImageMeta());

            ImageCache cache = new ImageCache(mock.Object, 2);
            cache.GetOrLoadImage("test1.png");      // Cache = [test1(1)]
            cache.GetOrLoadImage("test1.png");      // Cache = [test1(2)]
            cache.GetOrLoadImage("test2.png");      // Cache = [test1(2), test2(1)]
            cache.GetOrLoadImage("test1.png");      // Cache = [test1(3), test2(1)]
            cache.GetOrLoadImage("test3.png");      // Cache = [test1(3), test3(1)]
            cache.GetOrLoadImage("test1.png");      // Cache = [test1(4), test3(1)]
            cache.GetOrLoadImage("test2.png");      // Cache = [test1(4), test2(1)]
            mock.Verify(m => m.LoadImage(It.IsAny<string>()), Times.Exactly(4));
        }

        [Test]
        public async Task TwoStepImageCache_ReceivesTwiceWithSlowLoader()
        {
            var receiver = new Mock<IReceiveImage>();
            var slowLoader = GetLoader(100);
            var fastLoader = GetLoader(50);
            
            TwoStepImageCache cache = new TwoStepImageCache(slowLoader.Object, fastLoader.Object, receiver.Object, 1);
            cache.LoadImage("test.png");

            await Task.Delay(200);

            slowLoader.Verify(l => l.LoadImage(It.IsAny<string>()), Times.Once());
            fastLoader.Verify(l => l.LoadImage(It.IsAny<string>()), Times.Once());

            receiver.Verify(r => r.ReceiveImage(It.IsAny<ImageMeta>()), Times.Exactly(2));
        }

        [Test]
        public async Task TwoStepImageCache_ReceivesOnceWithFastLoader()
        {
            var receiver = new Mock<IReceiveImage>();
            var slowLoader = GetLoader(50);
            var fastLoader = GetLoader(500);

            TwoStepImageCache cache = new TwoStepImageCache(slowLoader.Object, fastLoader.Object, receiver.Object, 1);
            cache.LoadImage("test.png");

            await Task.Delay(600);

            slowLoader.Verify(l => l.LoadImage(It.IsAny<string>()), Times.Once());
            fastLoader.Verify(l => l.LoadImage(It.IsAny<string>()), Times.Once());

            receiver.Verify(r => r.ReceiveImage(It.IsAny<ImageMeta>()), Times.Once);
        }

        private Mock<IImageLoader> GetLoader(int delay)
        {
            var loader = new Mock<IImageLoader>();
            loader.Setup(l => l.LoadImage(It.IsAny<string>())).Returns(() =>
              {
                  Task.Delay(delay).Wait();
                  return new ImageMeta();
              });
            return loader;
        }
    }
}
