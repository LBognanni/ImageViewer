namespace ImageViewer
{
    public interface IImageCache
    {
        ImageMeta GetOrLoadImage(string fileName);
        void ReplaceImage(string fileName, ImageMeta newMeta);
    }
}