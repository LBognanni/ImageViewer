﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public ImageMeta LoadImage(string fileName) => LoadImageAsync(fileName).Result;

        public ImageMeta GetOrLoadImage(string fileName) => _innerCache.GetOrLoadImage(fileName);

        private string _lastImageToLoad = "";

        public async Task<ImageMeta> LoadImageAsync(string fileName)
        {
            _lastImageToLoad = fileName;
            var fastTask = Task.Run(() => _fastLoader.LoadImage(fileName));
            var slowTask = Task.Run(() => _slowLoader.LoadImage(fileName));
            var firstTask = await Task.WhenAny(slowTask, fastTask).ConfigureAwait(false);
            if (firstTask == fastTask)
            {
                Debug.WriteLine("fast was faster");
                ThreadPool.QueueUserWorkItem(state =>
                {
                    var img = slowTask.Result;
                    if (_lastImageToLoad == fileName)
                    {
                        _receiver.ReceiveImage(img);
                    }
                    ReplaceImage(fileName, img);
                });
            }
            else
            {
                Debug.WriteLine("slow was faster");
            }
            var img = await firstTask.ConfigureAwait(false);

            if (_lastImageToLoad == fileName)
            {
                _receiver.ReceiveImage(img);
            }

            return img;
        }

        public void ReplaceImage(string fileName, ImageMeta image)
        {
            if (_lastImageToLoad == fileName)
            {
                _receiver.ReceiveImage(image);
            }
            _innerCache.ReplaceImage(fileName, image);
        }

    }
}
