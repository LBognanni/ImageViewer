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
        ".bmp", ".gif", ".ico", ".jpeg", ".jfif", ".jif", ".jpg", ".png", ".wbmp", ".webp"
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

        var source = GetInfo(codec);
        var size = target == null ? codec.Info.Size : GetDecodeSize(codec, source, target.Value);
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            return null;
        }

        // A scaled decode is already close enough. Downsampling on the CPU costs more than the decode did.
        if (target != null && !source.HasCheapScaling)
        {
            bitmap = DownsampleToFit(bitmap, target.Value);
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

    private static SKSizeI GetDecodeSize(SKCodec codec, ImageInfo source, SKSizeI target)
    {
        var needed = FitScale(source.Width, source.Height, target);
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

    private static SKBitmap DownsampleToFit(SKBitmap bitmap, SKSizeI target)
    {
        var scale = FitScale(bitmap.Width, bitmap.Height, target);
        if (scale >= 1)
            return bitmap;

        var size = new SKImageInfo(
            Math.Max(1, (int)Math.Ceiling(bitmap.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(bitmap.Height * scale)),
            bitmap.ColorType, bitmap.AlphaType);
        var resized = new SKBitmap(size);
        if (!bitmap.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)))
        {
            resized.Dispose();
            return bitmap;
        }
        bitmap.Dispose();
        return resized;
    }

    /// <summary>
    /// The scale at which an image exactly fits the target. Never above 1.
    /// </summary>
    private static float FitScale(int width, int height, SKSizeI target) =>
        Math.Min(1f, Math.Min((float)target.Width / width, (float)target.Height / height));
}
