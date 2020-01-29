namespace ImageViewer
{
    public interface IImageLoader
    {
        ImageMeta LoadImage(string fileName);
    }
}