using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer
{
    public class TwoStepImageCache : IImageCache, IImageLoader
    {
        private readonly IImageCache _innerCache;
        private readonly IImageLoader _slowLoader;
        private readonly IImageLoader _fastLoader;
        private readonly IReceiveImage _receiver;

        public TwoStepImageCache(IImageLoader slowLoader, IImageLoader fastLoader, IReceiveImage receiver, int maxCacheSize)
        {
            _innerCache = new ImageCache(this, maxCacheSize);
            _slowLoader = slowLoader;
            _fastLoader = fastLoader;
            _receiver = receiver;
        }

        public void ReplaceImage(string fileName, ImageMeta image)
        {
            _receiver.ReceiveImage(image);
            _innerCache.ReplaceImage(fileName, image);
        }

        public ImageMeta LoadImage(string fileName) => LoadImageAsync(fileName).Result;

        public ImageMeta GetOrLoadImage(string fileName) => _innerCache.GetOrLoadImage(fileName);

        public async Task<ImageMeta> LoadImageAsync(string fileName)
        {
            var slowTask = Task.Run(() => _slowLoader.LoadImage(fileName));
            var fastTask = Task.Run(() => _fastLoader.LoadImage(fileName));
            var firstTask = await Task.WhenAny(slowTask, fastTask).ConfigureAwait(false);
            if (firstTask == fastTask)
            {
                Console.WriteLine("fast was faster");
                ThreadPool.QueueUserWorkItem(state => ReplaceImage(fileName, slowTask.Result));
            }
            else
            {
                Console.WriteLine("slow was faster");
            }
            var img = await firstTask.ConfigureAwait(false);

            _receiver.ReceiveImage(img);

            return img;
        }

    }
}
