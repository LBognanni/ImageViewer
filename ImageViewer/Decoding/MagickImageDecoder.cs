using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics;
using ImageMagick;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Everything Skia can't open. These formats have no cheap scaling that ImageMagick can reach: its
/// jp2:reduce-factor returns a crop of the image, not all of it.
/// </summary>
public sealed class MagickImageDecoder : IImageDecoder
{
    /// <summary>
    /// Only real image formats. Others need programs that may not be installed (PDF), have no header to
    /// check (raw RGB), or aren't images at all: ImageMagick can run its own scripting formats, so files
    /// are never left to pick their format for themselves.
    /// </summary>
    private static readonly Dictionary<string, MagickFormat> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ase"] = MagickFormat.Ase, [".aseprite"] = MagickFormat.Aseprite, [".avif"] = MagickFormat.Avif,
        [".cin"] = MagickFormat.Cin, [".cur"] = MagickFormat.Cur, [".dcm"] = MagickFormat.Dcm,
        [".dcx"] = MagickFormat.Dcx, [".dds"] = MagickFormat.Dds, [".dib"] = MagickFormat.Dib,
        [".dpx"] = MagickFormat.Dpx, [".exr"] = MagickFormat.Exr, [".farbfeld"] = MagickFormat.Farbfeld,
        [".ff"] = MagickFormat.Ff, [".fits"] = MagickFormat.Fits, [".fts"] = MagickFormat.Fts,
        [".hdr"] = MagickFormat.Hdr, [".heic"] = MagickFormat.Heic, [".heif"] = MagickFormat.Heif,
        [".hif"] = MagickFormat.Heif, [".icb"] = MagickFormat.Icb, [".j2c"] = MagickFormat.J2c,
        [".j2k"] = MagickFormat.J2k, [".jng"] = MagickFormat.Jng, [".jp2"] = MagickFormat.Jp2,
        [".jpc"] = MagickFormat.Jpc, [".jpf"] = MagickFormat.Jp2, [".jpx"] = MagickFormat.Jp2,
        [".jxl"] = MagickFormat.Jxl, [".mng"] = MagickFormat.Mng, [".ora"] = MagickFormat.Ora,
        [".pam"] = MagickFormat.Pam, [".pbm"] = MagickFormat.Pbm, [".pcd"] = MagickFormat.Pcd,
        [".pct"] = MagickFormat.Pct, [".pcx"] = MagickFormat.Pcx, [".pfm"] = MagickFormat.Pfm,
        [".pgm"] = MagickFormat.Pgm, [".phm"] = MagickFormat.Phm, [".pict"] = MagickFormat.Pict,
        [".pnm"] = MagickFormat.Pnm, [".ppm"] = MagickFormat.Ppm, [".psb"] = MagickFormat.Psb,
        [".psd"] = MagickFormat.Psd, [".ptif"] = MagickFormat.Ptif, [".qoi"] = MagickFormat.Qoi,
        [".ras"] = MagickFormat.Ras, [".sgi"] = MagickFormat.Sgi, [".sun"] = MagickFormat.Sun,
        [".svg"] = MagickFormat.Svg, [".svgz"] = MagickFormat.Svgz, [".tga"] = MagickFormat.Tga,
        [".tif"] = MagickFormat.Tif, [".tiff"] = MagickFormat.Tiff, [".vda"] = MagickFormat.Vda,
        [".viff"] = MagickFormat.Viff, [".vips"] = MagickFormat.Vips, [".vst"] = MagickFormat.Vst,
        [".wpg"] = MagickFormat.Wpg, [".xbm"] = MagickFormat.Xbm, [".xcf"] = MagickFormat.Xcf,
        [".xpm"] = MagickFormat.Xpm
    };

    /// <summary>
    /// What a file may turn out to be when its extension is wrong: anything above, and the
    /// formats Skia refused, which is how a damaged JPEG or PNG still gets a second chance
    /// </summary>
    private static readonly HashSet<MagickFormat> Detectable = new(Formats.Values)
    {
        MagickFormat.Bmp, MagickFormat.Gif, MagickFormat.Ico, MagickFormat.Jpeg, MagickFormat.Png, MagickFormat.WebP
    };

    private const int SignatureBytes = 4096;

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(Formats.Keys, StringComparer.OrdinalIgnoreCase);

    public ImageInfo? Probe(string path)
    {
        var format = FormatOf(path);
        return format == null ? null : Probe(path, format.Value);
    }

    public DecodedImage? Decode(string path, SKSizeI? target)
    {
        var format = FormatOf(path);
        return format == null ? null : Read(path, format.Value);
    }

    /// <summary>
    /// Read the header. A stream keeps ImageMagick away from the path, where it treats characters
    /// such as [ and : as instructions, and means a large file isn't loaded just to read its size.
    /// </summary>
    public static ImageInfo? Probe(string path, MagickFormat format)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = new MagickImage();
            image.Ping(stream, Settings(format));
            return ToInfo(image);
        }
        catch (MagickException)
        {
            return null;
        }
    }

    /// <param name="path">The file to read</param>
    /// <param name="format">What to read it as</param>
    /// <param name="defines">Options for the coder, as pairs of name and value</param>
    public static DecodedImage? Read(string path, MagickFormat format, params (string Name, string Value)[] defines)
    {
        try
        {
            var settings = Settings(format);
            foreach (var (name, value) in defines)
            {
                settings.SetDefine(name, value);
            }

            using var stream = File.OpenRead(path);
            using var image = new MagickImage();
            image.Read(stream, settings);

            // Formats like CMYK TIFF and linear HDR hold something other than what the screen shows
            if (image.ColorSpace != ColorSpace.sRGB && image.ColorSpace != ColorSpace.Gray)
            {
                image.ColorSpace = ColorSpace.sRGB;
            }
            return new DecodedImage(ToImage(image), ToInfo(image));
        }
        catch (MagickException)
        {
            return null;
        }
    }

    /// <summary>
    /// The first frame is the whole image: the first page of a TIFF, or the merged layers of a PSD.
    /// SVG would be drawn on white without a background.
    /// </summary>
    private static MagickReadSettings Settings(MagickFormat format) =>
        new() { Format = format, FrameIndex = 0, FrameCount = 1, BackgroundColor = MagickColors.Transparent };

    private static ImageInfo ToInfo(MagickImage image)
    {
        var origin = image.Orientation == Orientation.Undefined ? SKEncodedOrigin.TopLeft : (SKEncodedOrigin)image.Orientation;
        return new ImageInfo((int)image.Width, (int)image.Height, origin, false);
    }

    private static MagickFormat? FormatOf(string path)
    {
        if (Formats.TryGetValue(Path.GetExtension(path), out var format))
            return format;

        try
        {
            using var stream = File.OpenRead(path);
            var signature = new byte[Math.Min(stream.Length, SignatureBytes)];
            stream.ReadExactly(signature);
            var detected = signature.Length == 0 ? null : MagickFormatInfo.Create(signature);
            return detected != null && Detectable.Contains(detected.Format) ? detected.Format : null;
        }
        catch (MagickException)
        {
            return null;
        }
    }

    private static unsafe SKImage ToImage(MagickImage image)
    {
        int width = (int)image.Width, height = (int)image.Height;
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var pixels = image.GetPixelsUnsafe();

        // Reading the cache directly saves the 150 ms that ImageMagick takes to export 24 megapixels
        var channels = image.Channels.ToArray();
        if (channels.SequenceEqual(new[] { PixelChannel.Red, PixelChannel.Green, PixelChannel.Blue }))
        {
            var count = width * height;
            ExpandRgb(new ReadOnlySpan<byte>((void*)pixels.GetAreaPointer(0, 0, image.Width, image.Height), count * 3),
                new Span<byte>((void*)bitmap.GetPixels(), count * 4));
        }
        else
        {
            var source = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            if (channels.SequenceEqual(new[] { PixelChannel.Red, PixelChannel.Green, PixelChannel.Blue, PixelChannel.Alpha }))
            {
                using var pixmap = new SKPixmap(source, pixels.GetAreaPointer(0, 0, image.Width, image.Height));
                pixmap.ReadPixels(info, bitmap.GetPixels(), info.RowBytes);
            }
            else
            {
                // Gray and palette images have a layout of their own
                fixed (byte* exported = pixels.ToByteArray(PixelMapping.RGBA))
                {
                    using var pixmap = new SKPixmap(source, (IntPtr)exported);
                    pixmap.ReadPixels(info, bitmap.GetPixels(), info.RowBytes);
                }
            }
        }

        bitmap.SetImmutable();
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>
    /// Turn RGB into opaque pixels in the platform's byte order
    /// </summary>
    private static void ExpandRgb(ReadOnlySpan<byte> rgb, Span<byte> output)
    {
        var bgra = SKImageInfo.PlatformColorType == SKColorType.Bgra8888;
        int first = bgra ? 2 : 0, last = bgra ? 0 : 2;

        // Each pass reads 16 bytes and uses 12 of them, so the last few pixels are left for the loop below
        var order = Vector128.Create((byte)first, 1, (byte)last, 0xFF, (byte)(first + 3), 4, (byte)(last + 3), 0xFF,
            (byte)(first + 6), 7, (byte)(last + 6), 0xFF, (byte)(first + 9), 10, (byte)(last + 9), 0xFF);
        var opaque = Vector128.Create(0xFF000000).AsByte();

        int from = 0, to = 0;
        while (from + 16 <= rgb.Length)
        {
            var pixels = Vector128.Shuffle(Vector128.Create(rgb.Slice(from, 16)), order) | opaque;
            pixels.CopyTo(output.Slice(to, 16));
            from += 12;
            to += 16;
        }

        for (; from < rgb.Length; from += 3, to += 4)
        {
            output[to] = rgb[from + first];
            output[to + 1] = rgb[from + 1];
            output[to + 2] = rgb[from + last];
            output[to + 3] = 0xFF;
        }
    }
}
