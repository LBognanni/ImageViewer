using System;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Where an image goes on screen. All sizes are in physical pixels, so that
/// actual size means one image pixel for each screen pixel at any display scaling.
/// </summary>
public static class ViewGeometry
{
    /// <summary>
    /// The size of an image once its EXIF orientation and rotation are applied
    /// </summary>
    public static (int Width, int Height) DisplaySize(ImageInfo source, int rotation)
    {
        var swapped = SwapsAxes(source.Origin) != (rotation % 180 != 0);
        return swapped ? (source.Height, source.Width) : (source.Width, source.Height);
    }

    /// <summary>
    /// The scale from source pixels to view pixels. A zoom of 0 means best fit: images
    /// that fit the view stay at actual size, and larger ones are scaled down.
    /// </summary>
    public static float EffectiveZoom(ImageInfo source, int rotation, decimal zoom, float viewWidth, float viewHeight)
    {
        if (zoom != 0)
            return (float)zoom;

        var (w, h) = DisplaySize(source, rotation);
        if (w < viewWidth && h < viewHeight)
            return 1;

        return Math.Min(viewWidth / w, viewHeight / h);
    }

    /// <summary>
    /// The transform from the pixels of a decoded image, which may be
    /// smaller than the source, to the view.
    /// </summary>
    public static SKMatrix Transform(ImageInfo source, int pixelWidth, int pixelHeight, int rotation, decimal zoom,
        SKPoint pan, float viewWidth, float viewHeight)
    {
        var scale = EffectiveZoom(source, rotation, zoom, viewWidth, viewHeight);

        var matrix = SKMatrix.CreateTranslation(-pixelWidth / 2f, -pixelHeight / 2f);
        matrix = matrix.PostConcat(SKMatrix.CreateScale((float)source.Width / pixelWidth, (float)source.Height / pixelHeight));
        matrix = matrix.PostConcat(OriginMatrix(source.Origin));
        matrix = matrix.PostConcat(RotationMatrix(rotation));
        matrix = matrix.PostConcat(SKMatrix.CreateScale(scale, scale));

        // Whole pixels keep the image sharp at actual size
        matrix.TransX = MathF.Round(matrix.TransX + viewWidth / 2 + pan.X);
        matrix.TransY = MathF.Round(matrix.TransY + viewHeight / 2 + pan.Y);
        return matrix;
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) => origin is SKEncodedOrigin.LeftTop
        or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    private static SKMatrix OriginMatrix(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => Axes(-1, 0, 0, 1),
        SKEncodedOrigin.BottomRight => Axes(-1, 0, 0, -1),
        SKEncodedOrigin.BottomLeft => Axes(1, 0, 0, -1),
        SKEncodedOrigin.LeftTop => Axes(0, 1, 1, 0),
        SKEncodedOrigin.RightTop => Axes(0, -1, 1, 0),
        SKEncodedOrigin.RightBottom => Axes(0, -1, -1, 0),
        SKEncodedOrigin.LeftBottom => Axes(0, 1, -1, 0),
        _ => SKMatrix.Identity
    };

    private static SKMatrix RotationMatrix(int rotation) => rotation switch
    {
        90 => Axes(0, -1, 1, 0),
        180 => Axes(-1, 0, 0, -1),
        270 => Axes(0, 1, -1, 0),
        _ => SKMatrix.Identity
    };

    private static SKMatrix Axes(float scaleX, float skewX, float skewY, float scaleY) => new()
    {
        ScaleX = scaleX,
        SkewX = skewX,
        SkewY = skewY,
        ScaleY = scaleY,
        Persp2 = 1
    };
}
