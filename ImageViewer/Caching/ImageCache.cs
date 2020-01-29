using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ImageViewer
{
    public class ImageCache : IImageCache
    {
        protected ConcurrentDictionary<string, ImageMeta> _cache = new ConcurrentDictionary<string, ImageMeta>();
        private readonly IImageLoader _loader;
        private readonly int _maxCacheSize;

        public ImageCache(IImageLoader loader, int maxCacheSize)
        {
            _loader = loader;
            _maxCacheSize = maxCacheSize;
        }

        public ImageMeta GetOrLoadImage(string fileName)
        {
            var image = _cache.GetOrAdd(fileName, PruneCacheAndLoadImage);
            image.LastUsed++;
            foreach (var img in _cache.Values)
            {
                img.LastUsed--;
            }
            return image;
        }

        private ImageMeta PruneCacheAndLoadImage(string fileName)
        {
            if (_cache.Count >= _maxCacheSize)
            {
                RemoveLeastUsedItemFromCache();
            }
            return _loader.LoadImage(fileName);
        }

        private void RemoveLeastUsedItemFromCache()
        {
            var leastUsedItem = _cache.OrderBy(el => el.Value.LastUsed).First();
            if (_cache.TryRemove(leastUsedItem.Key, out var _))
            {
                leastUsedItem.Value.Dispose();
            }
        }

        public void ReplaceImage(string fileName, ImageMeta newMeta)
        {
            if(_cache.TryGetValue(fileName, out var oldImage))
            {
                oldImage.Dispose();
            }
            _cache[fileName] = newMeta;
        }
    }
}
