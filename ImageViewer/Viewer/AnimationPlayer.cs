using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Plays the current file if it's animated. Frames skip the pipeline: only one file plays at a time,
/// and a long animation would otherwise push the images around it out of the store.
/// </summary>
public sealed class AnimationPlayer
{
    private readonly Action<string, SKImage> _showFrame;
    private CancellationTokenSource? _playing;
    private string? _path;

    /// <param name="showFrame">Called on the calling thread, and takes ownership of the image</param>
    public AnimationPlayer(Action<string, SKImage> showFrame)
    {
        _showFrame = showFrame;
    }

    public void Play(string? path)
    {
        if (path == _path)
            return;

        Stop();
        _path = path;
        if (path != null && AnimatedImage.MayBeAnimated(path))
        {
            _playing = new CancellationTokenSource();
            Run(path, _playing.Token);
        }
    }

    public void Stop()
    {
        _path = null;
        _playing?.Cancel();
        _playing?.Dispose();
        _playing = null;
    }

    private async void Run(string path, CancellationToken token)
    {
        AnimationFrame? frame = null;
        try
        {
            using var animation = await Task.Run(() => AnimatedImage.TryOpen(path), token);
            if (animation == null)
                return;

            var delay = animation.FirstDelayMs;
            while (true)
            {
                var shown = Stopwatch.StartNew();
                frame = await Task.Run(animation.Next);
                if (frame == null)
                    return;

                await Task.Delay(Math.Max(0, delay - (int)shown.ElapsedMilliseconds), token);
                _showFrame(path, frame.Image);
                delay = frame.DelayMs;
                frame = null;
            }
        }
        catch (OperationCanceledException)
        {
            frame?.Image.Dispose();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Can't play {path}: {e.Message}");
        }
    }
}
