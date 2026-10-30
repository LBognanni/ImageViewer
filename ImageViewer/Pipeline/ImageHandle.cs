using System.Threading;
using SkiaSharp;

namespace ImageViewer;

public enum ImageTier
{
    Instant,
    Screen,
    Full
}

/// <summary>
/// A ref-counted decoded image. The render thread can still be drawing an image after the
/// store evicts it, so pixels are only freed when the last holder releases its reference.
/// </summary>
public sealed class ImageHandle
{
    private int _references = 1;

    public ImageHandle(string path, ImageTier tier, SKImage image, ImageInfo source, SKColor background)
    {
        Path = path;
        Tier = tier;
        Image = image;
        Source = source;
        Background = background;
    }

    public string Path { get; }
    public ImageTier Tier { get; }
    public SKImage Image { get; }
    public ImageInfo Source { get; }
    public SKColor Background { get; }
    public long Bytes => (long)Image.Width * Image.Height * 4;

    /// <summary>
    /// Take a reference. Fails if the pixels are already gone.
    /// </summary>
    public bool TryAcquire()
    {
        while (true)
        {
            var current = Volatile.Read(ref _references);
            if (current == 0)
                return false;
            if (Interlocked.CompareExchange(ref _references, current + 1, current) == current)
                return true;
        }
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _references) == 0)
        {
            Image.Dispose();
        }
    }
}
