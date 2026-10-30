using System.Collections.Generic;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// What a decoder can tell from the file header alone.
/// </summary>
/// <param name="Width">Width as stored, before <paramref name="Origin"/> is applied</param>
/// <param name="Height">Height as stored, before <paramref name="Origin"/> is applied</param>
/// <param name="Origin">EXIF orientation, applied at draw time</param>
/// <param name="HasCheapScaling">True if a smaller decode costs less than a full one</param>
public sealed record ImageInfo(int Width, int Height, SKEncodedOrigin Origin, bool HasCheapScaling);

public sealed record DecodedImage(SKImage Image, ImageInfo Source);

public interface IImageDecoder
{
    /// <summary>
    /// Lower-case file extensions, including the dot
    /// </summary>
    IReadOnlySet<string> Extensions { get; }

    ImageInfo? Probe(string path);

    /// <summary>
    /// Decode as cheaply as possible while still fitting at least <paramref name="target"/>.
    /// A null target decodes at full resolution.
    /// </summary>
    DecodedImage? Decode(string path, SKSizeI? target);
}
