using System;
using System.IO;
using SkiaSharp;

namespace ImageViewer.Tests;

public static class TestImages
{
    public static ImageInfo Info(int width, int height, SKEncodedOrigin origin = SKEncodedOrigin.TopLeft) =>
        new(width, height, origin, true);

    public static ImageHandle Handle(string path, ImageTier tier, int size = 10)
    {
        using var bitmap = new SKBitmap(size, size);
        return new ImageHandle(path, tier, SKImage.FromBitmap(bitmap), Info(size, size), SKColors.Black);
    }

    public static string Jpeg(int width, int height, SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"imageviewer-test-{Guid.NewGuid():N}.jpg");
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Insert an EXIF block holding only an orientation tag after the JPEG start marker
    /// </summary>
    public static void AddExifOrientation(string jpegPath, ushort orientation)
    {
        var jpeg = File.ReadAllBytes(jpegPath);
        byte[] exif =
        {
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8,
            0, 1,
            0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0,
            0, 0, 0, 0
        };

        using var output = File.Create(jpegPath);
        output.Write(jpeg, 0, 2);
        output.Write(exif);
        output.Write(jpeg, 2, jpeg.Length - 2);
    }
}
