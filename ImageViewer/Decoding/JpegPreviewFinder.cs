using System;

namespace ImageViewer;

/// <summary>
/// Finds the JPEG previews that cameras store inside raw files. Every maker lays out their files
/// differently, but the JPEG itself is always there to be found.
/// </summary>
public static class JpegPreviewFinder
{
    private const byte Marker = 0xFF;
    private const byte StartOfScan = 0xDA;

    private static ReadOnlySpan<byte> StartOfImage => new byte[] { Marker, 0xD8, Marker };

    /// <summary>
    /// The largest JPEG in the file, or null if it has none. Lossless JPEG is left out,
    /// because that's how many raw formats store the sensor data.
    /// </summary>
    /// <returns>Where the JPEG starts, and its width and height</returns>
    public static (int Offset, int Width, int Height)? FindLargest(ReadOnlySpan<byte> file)
    {
        (int Offset, int Width, int Height)? best = null;
        var position = 0;
        while (true)
        {
            var next = file[position..].IndexOf(StartOfImage);
            if (next < 0)
                return best;

            position += next;
            var size = ReadSize(file[position..]);
            if (size != null && (best == null || size.Value.Width * (long)size.Value.Height > best.Value.Width * (long)best.Value.Height))
            {
                best = (position, size.Value.Width, size.Value.Height);
            }
            position += 2;
        }
    }

    /// <summary>
    /// Walk the segments up to the frame header. Random data rarely gets that far.
    /// </summary>
    private static (int Width, int Height)? ReadSize(ReadOnlySpan<byte> jpeg)
    {
        var position = 2;
        while (position + 9 <= jpeg.Length && jpeg[position] == Marker)
        {
            var type = jpeg[position + 1];
            if (type == StartOfScan)
                return null;

            // Baseline, extended and progressive
            if (type is 0xC0 or 0xC1 or 0xC2)
            {
                var height = jpeg[position + 5] << 8 | jpeg[position + 6];
                var width = jpeg[position + 7] << 8 | jpeg[position + 8];
                return width > 0 && height > 0 ? (width, height) : null;
            }

            // The other frame types are lossless or arithmetic-coded, which nothing here can read
            if (type is >= 0xC3 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return null;

            position += 2 + (jpeg[position + 2] << 8 | jpeg[position + 3]);
        }
        return null;
    }
}
