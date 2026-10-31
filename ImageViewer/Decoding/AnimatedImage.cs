using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace ImageViewer;

public sealed record AnimationFrame(SKImage Image, int DelayMs);

/// <summary>
/// Decodes the frames of an animation in order, looping forever. Frames can build on the one
/// before, so they share a buffer. Files are read into memory, because a mapped file can't be deleted
/// on Windows while it's playing. Only use one of these from one thread at a time.
/// </summary>
public sealed class AnimatedImage : IDisposable
{
    private const int ShortestDelayMs = 10;
    private const int DelayForShortFramesMs = 100;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".gif", ".webp" };

    private readonly SKData _data;
    private readonly SKCodec _codec;
    private readonly SKCodecFrameInfo[] _frames;
    private readonly SKImageInfo _info;
    private readonly SKBitmap _canvas;
    private int _index;

    private AnimatedImage(SKData data, SKCodec codec)
    {
        _data = data;
        _codec = codec;
        _frames = codec.FrameInfo;
        _info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        _canvas = new SKBitmap(_info);
    }

    /// <summary>
    /// How long the first frame stays up. That frame comes from the pipeline, like any image.
    /// </summary>
    public int FirstDelayMs => Delay(0);

    public static bool MayBeAnimated(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Open a file, or return null if it isn't an animation
    /// </summary>
    public static AnimatedImage? TryOpen(string path)
    {
        var data = SKData.CreateCopy(File.ReadAllBytes(path));
        var codec = SKCodec.Create(data);
        if (codec == null || codec.FrameCount < 2)
        {
            codec?.Dispose();
            data.Dispose();
            return null;
        }

        var animation = new AnimatedImage(data, codec);
        if (animation.DecodeInto(0, -1))
            return animation;

        animation.Dispose();
        return null;
    }

    /// <summary>
    /// The next frame, or null if the file turns out to be damaged
    /// </summary>
    public AnimationFrame? Next()
    {
        var next = (_index + 1) % _frames.Length;
        if (!DecodeInto(next, PriorFrameFor(next)))
            return null;

        _index = next;
        return new AnimationFrame(SKImage.FromPixelCopy(_info, _canvas.GetPixels(), _canvas.RowBytes), Delay(next));
    }

    /// <summary>
    /// Reuse the buffer when it holds what the frame builds on. Otherwise the codec rebuilds that from earlier frames.
    /// </summary>
    private int PriorFrameFor(int frame)
    {
        var required = _frames[frame].RequiredFrame;
        var restores = _frames[_index].DisposalMethod == SKCodecAnimationDisposalMethod.RestorePrevious;
        return required >= 0 && _index >= required && _index < frame && !restores ? _index : -1;
    }

    private bool DecodeInto(int frame, int priorFrame)
    {
        var result = _codec.GetPixels(_info, _canvas.GetPixels(), new SKCodecOptions(frame, priorFrame));
        return result is SKCodecResult.Success or SKCodecResult.IncompleteInput;
    }

    /// <summary>
    /// Plenty of files ask for no delay at all, and browsers slow those down in the same way
    /// </summary>
    private int Delay(int frame)
    {
        var delay = _frames[frame].Duration;
        return delay <= ShortestDelayMs ? DelayForShortFramesMs : delay;
    }

    public void Dispose()
    {
        _canvas.Dispose();
        _codec.Dispose();
        _data.Dispose();
    }
}
