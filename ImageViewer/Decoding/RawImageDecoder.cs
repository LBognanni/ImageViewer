using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Camera raw files. Developing one takes seconds, but each also holds a JPEG the camera made,
/// which takes milliseconds. So that JPEG is the image, down to how the file scales and browses,
/// and the sensor data is only developed when the view shows more detail than the JPEG has.
/// </summary>
public sealed class RawImageDecoder : IImageDecoder
{
    /// <summary>
    /// A preview this close to the size of the sensor counts as the full image, so that
    /// a few pixels of difference never start a develop that takes seconds
    /// </summary>
    private const float FullSizePreview = 0.9f;

    private const int MaxRemembered = 64;

    private static readonly Dictionary<string, MagickFormat> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".3fr"] = MagickFormat.ThreeFr, [".arw"] = MagickFormat.Arw, [".cr2"] = MagickFormat.Cr2,
        [".cr3"] = MagickFormat.Cr3, [".crw"] = MagickFormat.Crw, [".dcr"] = MagickFormat.Dcr,
        [".dng"] = MagickFormat.Dng, [".fff"] = MagickFormat.Fff,
        [".iiq"] = MagickFormat.Iiq, [".k25"] = MagickFormat.K25, [".kdc"] = MagickFormat.Kdc,
        [".mdc"] = MagickFormat.Mdc, [".mef"] = MagickFormat.Mef, [".mos"] = MagickFormat.Mos,
        [".mrw"] = MagickFormat.Mrw, [".nef"] = MagickFormat.Nef, [".nrw"] = MagickFormat.Nrw,
        [".orf"] = MagickFormat.Orf, [".pef"] = MagickFormat.Pef, [".raf"] = MagickFormat.Raf,
        [".rw2"] = MagickFormat.Rw2, [".rwl"] = MagickFormat.Rwl, [".sr2"] = MagickFormat.Sr2,
        [".srf"] = MagickFormat.Srf, [".srw"] = MagickFormat.Srw, [".sti"] = MagickFormat.Sti,
        [".x3f"] = MagickFormat.X3f
    };

    private static readonly (string, string)[] DevelopOptions =
    {
        // Otherwise the colors jump when the develop replaces the camera's JPEG
        ("dng:use-camera-wb", "true"),
        // PPG. The default, AHD, takes twice as long.
        ("dng:interpolation-quality", "2")
    };

    // The pipeline probes before every decode, and in a raw file that's the slow part of getting a preview
    private readonly Dictionary<string, Layout> _layouts = new();

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(Formats.Keys, StringComparer.OrdinalIgnoreCase);

    public ImageInfo? Probe(string path) =>
        Formats.TryGetValue(Path.GetExtension(path), out var format) ? GetLayout(path, format)?.Source : null;

    public DecodedImage? Decode(string path, SKSizeI? target)
    {
        if (!Formats.TryGetValue(Path.GetExtension(path), out var format))
            return null;

        var layout = GetLayout(path, format);
        if (layout == null)
            return null;

        if (layout.Preview != null && (target != null || layout.PreviewIsFullSize))
        {
            using var preview = Preview.Open(path, layout.Preview.Value);
            var decoded = preview == null ? null : SkiaImageDecoder.Decode(preview.Codec, layout.Source, target);
            if (decoded != null)
                return decoded;
        }

        var developed = MagickImageDecoder.Read(path, format, DevelopOptions);
        return developed == null ? null : developed with { Source = layout.Source };
    }

    private Layout? GetLayout(string path, MagickFormat format)
    {
        var modified = File.GetLastWriteTimeUtc(path);
        lock (_layouts)
        {
            if (_layouts.TryGetValue(path, out var known) && known.Modified == modified)
                return known;
        }

        var layout = ReadLayout(path, format, modified);
        if (layout != null)
        {
            lock (_layouts)
            {
                if (_layouts.Count >= MaxRemembered)
                {
                    _layouts.Clear();
                }
                _layouts[path] = layout;
            }
        }
        return layout;
    }

    private static Layout? ReadLayout(string path, MagickFormat format, DateTime modified)
    {
        var sensor = MagickImageDecoder.Probe(path, format);
        using var preview = Preview.Find(path);
        if (preview == null)
            return sensor == null ? null : new Layout(modified, sensor, null, false);

        var size = preview.Codec.Info.Size;
        // Raw files often keep their orientation outside the JPEG
        var origin = preview.Codec.EncodedOrigin != SKEncodedOrigin.TopLeft || sensor == null
            ? preview.Codec.EncodedOrigin
            : sensor.Origin;

        var fullSize = sensor == null || Math.Max(size.Width, size.Height) >= Math.Max(sensor.Width, sensor.Height) * FullSizePreview;
        var source = fullSize
            ? new ImageInfo(size.Width, size.Height, origin, true)
            : sensor! with { Origin = origin, HasCheapScaling = true };
        return new Layout(modified, source, preview.Offset, fullSize);
    }

    /// <param name="Modified">When the file last changed, so that an edited file is read again</param>
    /// <param name="Source">The image as the viewer should think of it</param>
    /// <param name="Preview">Where the JPEG starts, or null if there's none</param>
    /// <param name="PreviewIsFullSize">True if it never needs developing</param>
    private sealed record Layout(DateTime Modified, ImageInfo Source, long? Preview, bool PreviewIsFullSize);

    private sealed class Preview : IDisposable
    {
        private readonly SKData _data;

        private Preview(SKData data, SKCodec codec, long offset)
        {
            _data = data;
            Codec = codec;
            Offset = offset;
        }

        public SKCodec Codec { get; }

        /// <summary>
        /// Where the JPEG starts, or 0 if Skia read it out of the raw file
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Skia understands most raw files already, and reads the preview without searching for it.
        /// It doesn't know ARW or CR3, or the rarer formats.
        /// </summary>
        public static unsafe Preview? Find(string path)
        {
            var file = SKData.Create(path);
            if (file == null)
                return null;

            var codec = SKCodec.Create(file);
            if (codec?.EncodedFormat == SKEncodedImageFormat.Jpeg)
                return new Preview(file, codec, 0);
            codec?.Dispose();

            var bytes = new ReadOnlySpan<byte>((void*)file.Data, (int)Math.Min(file.Size, int.MaxValue));
            var found = JpegPreviewFinder.FindLargest(bytes);
            var preview = found == null ? null : At(file, found.Value.Offset);
            file.Dispose();
            return preview;
        }

        public static Preview? Open(string path, long offset)
        {
            var file = SKData.Create(path);
            if (file == null)
                return null;
            if (offset > 0)
            {
                var preview = At(file, offset);
                file.Dispose();
                return preview;
            }

            var codec = SKCodec.Create(file);
            if (codec != null)
                return new Preview(file, codec, 0);

            file.Dispose();
            return null;
        }

        private static Preview? At(SKData file, long offset)
        {
            // Everything after the JPEG goes along, and the decoder stops at the end of the image
            var data = file.Subset((ulong)offset, (ulong)(file.Size - offset));
            var codec = SKCodec.Create(data);
            if (codec != null)
                return new Preview(data, codec, offset);

            data.Dispose();
            return null;
        }

        public void Dispose()
        {
            Codec.Dispose();
            _data.Dispose();
        }
    }
}
