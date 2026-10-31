using System;
using System.IO;
using SkiaSharp;

namespace ImageViewer.Tests;

/// <param name="Color">Index into <see cref="TestImages.GifPalette"/></param>
/// <param name="DelayMs">Rounded down to tens of milliseconds, as GIF stores them</param>
/// <param name="Disposal">1 keeps the frame, 3 restores what was there before it</param>
public sealed record GifFrame(int X, int Y, int Width, int Height, byte Color, int DelayMs = 100, byte Disposal = 1);

public static class TestImages
{
    public static readonly SKColor[] GifPalette = { SKColors.Black, SKColors.Red, SKColors.Lime, SKColors.Blue };

    /// <summary>
    /// Write a GIF frame by frame. SkiaSharp can't encode them, and this way tests can say exactly how frames overlap.
    /// </summary>
    public static string Gif(int width, int height, params GifFrame[] frames)
    {
        using var gif = new MemoryStream();
        gif.Write("GIF89a"u8);
        gif.Write(BitConverter.GetBytes((ushort)width));
        gif.Write(BitConverter.GetBytes((ushort)height));
        gif.Write(new byte[] { 0x81, 0, 0 });
        foreach (var color in GifPalette)
        {
            gif.Write(new[] { color.Red, color.Green, color.Blue });
        }

        foreach (var frame in frames)
        {
            var delay = BitConverter.GetBytes((ushort)(frame.DelayMs / 10));
            gif.Write(new byte[] { 0x21, 0xF9, 4, (byte)(frame.Disposal << 2), delay[0], delay[1], 0, 0, 0x2C });
            foreach (var value in new[] { frame.X, frame.Y, frame.Width, frame.Height })
            {
                gif.Write(BitConverter.GetBytes((ushort)value));
            }
            gif.WriteByte(0);
            gif.WriteByte(2);
            WriteGifPixels(gif, frame);
        }
        gif.WriteByte(0x3B);

        var path = Path.Combine(Path.GetTempPath(), $"imageviewer-test-{Guid.NewGuid():N}.gif");
        File.WriteAllBytes(path, gif.ToArray());
        return path;
    }

    /// <summary>
    /// LZW without the compression: a clear code before each pixel keeps every code three bits wide
    /// </summary>
    private static void WriteGifPixels(Stream gif, GifFrame frame)
    {
        const int clear = 4, end = 5;
        var bits = new System.Collections.BitArray(0);
        void Add(int code)
        {
            for (var i = 0; i < 3; i++)
            {
                bits.Length++;
                bits[bits.Length - 1] = (code >> i & 1) == 1;
            }
        }

        for (var i = 0; i < frame.Width * frame.Height; i++)
        {
            Add(clear);
            Add(frame.Color);
        }
        Add(end);

        var bytes = new byte[(bits.Length + 7) / 8];
        bits.CopyTo(bytes, 0);
        for (var i = 0; i < bytes.Length; i += 255)
        {
            var length = Math.Min(255, bytes.Length - i);
            gif.WriteByte((byte)length);
            gif.Write(bytes, i, length);
        }
        gif.WriteByte(0);
    }

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
