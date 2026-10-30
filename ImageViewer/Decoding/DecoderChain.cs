using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Tries each decoder in order. Decoders that claim the file's extension go first,
/// and the others still get a chance, because extensions can be wrong.
/// </summary>
public sealed class DecoderChain : IImageDecoder
{
    private readonly IReadOnlyList<IImageDecoder> _decoders;

    public DecoderChain(IReadOnlyList<IImageDecoder> decoders)
    {
        _decoders = decoders;
        Extensions = new HashSet<string>(decoders.SelectMany(d => d.Extensions), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> Extensions { get; }

    public ImageInfo? Probe(string path) => FirstResult(path, d => d.Probe(path));

    public DecodedImage? Decode(string path, SKSizeI? target) => FirstResult(path, d => d.Decode(path, target));

    private T? FirstResult<T>(string path, Func<IImageDecoder, T?> attempt) where T : class
    {
        var extension = Path.GetExtension(path);
        foreach (var decoder in _decoders.OrderBy(d => d.Extensions.Contains(extension) ? 0 : 1))
        {
            try
            {
                var result = attempt(decoder);
                if (result != null)
                    return result;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Let the next decoder try
            }
        }
        return null;
    }
}
