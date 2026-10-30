using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using SkiaSharp;

namespace ImageViewer;

public interface IImageWindow
{
    /// <summary>
    /// Tell the pipeline what the user is looking at.
    /// </summary>
    /// <param name="filesByPriority">The current file, followed by the files to prefetch</param>
    /// <param name="rapid">True while the user is moving faster than images can be decoded</param>
    void SetWindow(IReadOnlyList<string> filesByPriority, bool rapid);
}

/// <summary>
/// Decides what gets decoded and when. Each navigation replaces the whole list of pending work,
/// so a held arrow key can't build a backlog: only decodes already running are left to finish.
/// </summary>
public sealed class ImagePipeline : IImageWindow, IDisposable
{
    private static readonly SKSizeI InstantSize = new(640, 640);

    private readonly object _lock = new();
    private readonly IImageDecoder _decoder;
    private readonly ImageStore _store;
    private readonly List<Job> _pending = new();
    private readonly HashSet<Job> _inFlight = new();
    private readonly HashSet<string> _noCheapScaling = new();
    private IReadOnlyList<string> _window = Array.Empty<string>();
    private SKSizeI _screen;
    private string? _fullRequested;
    private bool _rapid;
    private bool _disposed;

    /// <summary>
    /// Raised on a worker thread whenever a decode finishes
    /// </summary>
    public event Action<string>? ImageReady;

    public ImagePipeline(IImageDecoder decoder, long budgetBytes, SKSizeI screen, int workers)
    {
        _decoder = decoder;
        _store = new ImageStore(budgetBytes);
        _screen = screen;
        for (var i = 0; i < workers; i++)
        {
            new Thread(Work) { IsBackground = true, Name = $"Decoder {i}" }.Start();
        }
    }

    /// <summary>
    /// A budget of a quarter of the RAM, up to 2 GB
    /// </summary>
    public static long DefaultBudget() =>
        Math.Min(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 4, 2L * 1024 * 1024 * 1024);

    public static int DefaultWorkers() => Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

    /// <summary>
    /// True to prefetch full resolution, when images open at actual size
    /// </summary>
    public bool PrefetchFull { get; set; }

    public void SetScreenSize(SKSizeI screen)
    {
        lock (_lock)
        {
            _screen = screen;
        }
    }

    public void SetWindow(IReadOnlyList<string> filesByPriority, bool rapid)
    {
        lock (_lock)
        {
            if (_window.Count == 0 || filesByPriority.Count == 0 || _window[0] != filesByPriority[0])
            {
                _fullRequested = null;
            }

            _window = filesByPriority;
            _rapid = rapid;

            var ranks = new Dictionary<string, int>();
            for (var i = 0; i < filesByPriority.Count; i++)
            {
                ranks.TryAdd(filesByPriority[i], i);
            }
            _store.SetRanks(ranks);
            Plan();
        }
    }

    /// <summary>
    /// Ask for the current image at full resolution, because the view has zoomed past what it has
    /// </summary>
    public void RequestFull(string path)
    {
        lock (_lock)
        {
            _fullRequested = path;
            Plan();
        }
    }

    /// <summary>
    /// Get the best image available right now. The caller must release the handle.
    /// </summary>
    public ImageHandle? AcquireBest(string path)
    {
        lock (_lock)
        {
            return _store.AcquireBest(path);
        }
    }

    public void Forget(string path)
    {
        lock (_lock)
        {
            _store.Remove(path);
            _noCheapScaling.Remove(path);
            Plan();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _pending.Clear();
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// Replace the pending list with what the current window needs, most urgent first.
    /// </summary>
    private void Plan()
    {
        _pending.Clear();
        if (_window.Count == 0)
            return;

        var current = _window[0];
        var prefetchTier = PrefetchFull ? ImageTier.Full : ImageTier.Screen;

        Enqueue(current, ImageTier.Instant);
        if (_fullRequested == current)
        {
            Enqueue(current, ImageTier.Full);
        }

        if (_rapid)
        {
            // Show something for each image first, and get sharp once things calm down
            for (var i = 1; i < _window.Count; i++)
            {
                Enqueue(_window[i], ImageTier.Instant);
            }
            Enqueue(current, prefetchTier);
        }
        else
        {
            Enqueue(current, prefetchTier);
            for (var i = 1; i < _window.Count; i++)
            {
                Enqueue(_window[i], prefetchTier);
            }
        }
        Monitor.PulseAll(_lock);
    }

    private void Enqueue(string path, ImageTier tier)
    {
        if (tier == ImageTier.Instant && _noCheapScaling.Contains(path))
        {
            tier = ImageTier.Screen;
        }

        var job = new Job(path, tier);
        if (_store.Has(path, tier) || _inFlight.Contains(job) || _pending.Contains(job))
            return;

        // A running decode of a better tier will satisfy this one soon enough
        for (var t = tier + 1; t <= ImageTier.Full; t++)
        {
            if (_inFlight.Contains(new Job(path, t)))
                return;
        }
        _pending.Add(job);
    }

    private void Work()
    {
        while (true)
        {
            Job job;
            lock (_lock)
            {
                while (_pending.Count == 0 && !_disposed)
                {
                    Monitor.Wait(_lock);
                }
                if (_disposed)
                    return;

                job = _pending[0];
                _pending.RemoveAt(0);
                _inFlight.Add(job);
            }

            var handle = Decode(job, out var hasCheapScaling);

            lock (_lock)
            {
                _inFlight.Remove(job);
                if (_disposed)
                {
                    handle?.Release();
                    return;
                }

                if (!hasCheapScaling)
                {
                    _noCheapScaling.Add(job.Path);
                }
                if (handle != null)
                {
                    _store.Add(handle);
                }

                // Everything visible is on screen, so prefetching can begin again
                if (_rapid && _pending.Count == 0)
                {
                    _rapid = false;
                }
                Plan();
            }

            ImageReady?.Invoke(job.Path);
        }
    }

    private ImageHandle? Decode(Job job, out bool hasCheapScaling)
    {
        hasCheapScaling = true;
        try
        {
            var info = ProbeWithRetry(job.Path);
            if (info == null)
                return LoadBroken(job.Path);

            hasCheapScaling = info.HasCheapScaling;
            SKSizeI screen;
            lock (_lock)
            {
                screen = _screen;
            }

            var tier = job.Tier == ImageTier.Instant && !hasCheapScaling ? ImageTier.Screen : job.Tier;
            SKSizeI? target = tier switch
            {
                ImageTier.Instant => InstantSize,
                ImageTier.Screen => screen,
                _ => null
            };

            var decoded = _decoder.Decode(job.Path, target);
            return decoded == null ? LoadBroken(job.Path) : ToHandle(job.Path, tier, decoded);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Can't decode {job.Path}: {e.Message}");
            return LoadBroken(job.Path);
        }
    }

    /// <summary>
    /// A file that's still being written or scanned can fail once and be fine moments later
    /// </summary>
    private ImageInfo? ProbeWithRetry(string path)
    {
        var info = _decoder.Probe(path);
        if (info != null || !IsLocked(path))
            return info;

        Thread.Sleep(100);
        return _decoder.Probe(path);
    }

    private static bool IsLocked(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return false;
        }
        catch (IOException)
        {
            return File.Exists(path);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ImageHandle ToHandle(string path, ImageTier tier, DecodedImage decoded)
    {
        var image = decoded.Image;
        // An image decoded at its own size already is the best tier
        if (image.Width >= decoded.Source.Width && image.Height >= decoded.Source.Height)
        {
            tier = ImageTier.Full;
        }
        return new ImageHandle(path, tier, image, decoded.Source, GetEdgeColor(image));
    }

    private static ImageHandle? LoadBroken(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("broken.png");
        var image = stream == null ? null : SKImage.FromEncodedData(stream);
        if (image == null)
            return null;
        return new ImageHandle(path, ImageTier.Full, image,
            new ImageInfo(image.Width, image.Height, SKEncodedOrigin.TopLeft, false), SKColors.Black);
    }

    /// <summary>
    /// Sample the edges, so the background blends with the image
    /// </summary>
    private static SKColor GetEdgeColor(SKImage image)
    {
        using var pixels = image.PeekPixels();
        if (pixels == null)
            return SKColors.Black;

        int w = image.Width - 1, h = image.Height - 1;
        var samples = new[]
        {
            pixels.GetPixelColor(0, 0), pixels.GetPixelColor(0, h), pixels.GetPixelColor(w, 0), pixels.GetPixelColor(w, h),
            pixels.GetPixelColor(w / 2, 0), pixels.GetPixelColor(0, h / 2), pixels.GetPixelColor(w / 2, h), pixels.GetPixelColor(w, h / 2)
        };

        int r = 0, g = 0, b = 0;
        foreach (var color in samples)
        {
            r += color.Red;
            g += color.Green;
            b += color.Blue;
        }
        return new SKColor((byte)(r / samples.Length), (byte)(g / samples.Length), (byte)(b / samples.Length));
    }

    private readonly record struct Job(string Path, ImageTier Tier);
}
