using System;
using System.Collections.Generic;
using SkiaSharp;

namespace ImageViewer;

public sealed class SkiaImageDecoder : IImageDecoder
{
    // libjpeg scales by eighths. The GPU takes care of the rest, so anything that fits will do.
    private static readonly float[] Scales = { 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f };

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".ico", ".jpe", ".jpeg", ".jfif", ".jif", ".jpg", ".jps", ".mpo", ".pjpeg", ".png", ".wbmp", ".webp"
    };

    public ImageInfo? Probe(string path)
    {
        using var data = SKData.Create(path);
        if (data == null)
            return null;
        using var codec = SKCodec.Create(data);
        return codec == null ? null : GetInfo(codec);
    }

    public DecodedImage? Decode(string path, SKSizeI? target)
    {
        // SKData memory-maps the file, so the decoder pulls pages in as it needs them
        using var data = SKData.Create(path);
        if (data == null)
            return null;
        using var codec = SKCodec.Create(data);
        if (codec == null)
            return null;

        return Decode(codec, GetInfo(codec), target);
    }

    /// <summary>
    /// Decode from a codec that's already open, reporting <paramref name="source"/> as the image it came from
    /// </summary>
    public static DecodedImage? Decode(SKCodec codec, ImageInfo source, SKSizeI? target)
    {
        var size = target == null ? codec.Info.Size : GetDecodeSize(codec, target.Value);
        var alpha = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, alpha);

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            return null;
        }

        bitmap.SetImmutable();
        // The image takes over the bitmap's pixels, which stay valid after this bitmap is disposed
        using (bitmap)
        {
            return new DecodedImage(SKImage.FromBitmap(bitmap), source);
        }
    }

    private static ImageInfo GetInfo(SKCodec codec)
    {
        var full = codec.Info.Size;
        return new ImageInfo(full.Width, full.Height, codec.EncodedOrigin, codec.GetScaledDimensions(0.5f) != full);
    }

    private static SKSizeI GetDecodeSize(SKCodec codec, SKSizeI target)
    {
        var needed = FitScale(codec.Info.Width, codec.Info.Height, target);
        foreach (var scale in Scales)
        {
            if (scale >= needed)
            {
                var size = codec.GetScaledDimensions(scale);
                if (size.Width > 0 && size.Height > 0)
                    return size;
            }
        }
        return codec.Info.Size;
    }

    /// <summary>
    /// The scale at which an image exactly fits the target. Never above 1.
    /// </summary>
    private static float FitScale(int width, int height, SKSizeI target) =>
        Math.Min(1f, Math.Min((float)target.Width / width, (float)target.Height / height));
}
