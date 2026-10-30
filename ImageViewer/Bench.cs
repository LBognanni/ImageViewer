using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Measures the pipeline without a window: <c>ImageViewer --bench folder</c>.
/// </summary>
static class Bench
{
    private const int TimeoutMs = 10000;

    public static int Run(string folder)
    {
        var decoder = new DecoderChain(new IImageDecoder[] { new SkiaImageDecoder() });

        var sw = Stopwatch.StartNew();
        var files = Navigator.ListFolder(folder, decoder.Extensions).Result;
        Console.WriteLine($"Listed {files.Count} files in {sw.Elapsed.TotalMilliseconds:F1} ms");
        if (files.Count < 8)
        {
            Console.WriteLine("Needs at least 8 images");
            return 1;
        }

        var screen = new SKSizeI(3840, 2160);
        Console.WriteLine($"Screen {screen.Width}x{screen.Height}, {ImagePipeline.DefaultWorkers()} workers");

        Measure("Not prefetched (cold jumps)", decoder, screen, files, (nav, _) => nav.Next(), settleMs: 0, jumpCold: true);
        Measure("Prefetched (browsing, 500 ms per image)", decoder, screen, files, (nav, _) => nav.Next(), settleMs: 500, jumpCold: false);
        MeasureHeldKey(decoder, screen, files);
        return 0;
    }

    private static void Measure(string name, IImageDecoder decoder, SKSizeI screen, List<string> files,
        Action<Navigator, int> move, int settleMs, bool jumpCold)
    {
        var first = new List<double>();
        var sharp = new List<double>();
        var count = Math.Min(files.Count, 12);

        using var shared = NewPipeline(decoder, screen);
        var navigator = new Navigator(shared);
        navigator.Open(files[0]);
        navigator.ApplyFolder(new List<string>(files));
        WaitFor(shared, files[0], ImageTier.Screen);
        Thread.Sleep(settleMs);

        for (var i = 0; i < count - 1; i++)
        {
            ImagePipeline pipeline;
            Navigator nav;
            if (jumpCold)
            {
                pipeline = NewPipeline(decoder, screen);
                nav = new Navigator(pipeline);
            }
            else
            {
                pipeline = shared;
                nav = navigator;
            }

            var sw = Stopwatch.StartNew();
            if (jumpCold)
            {
                nav.Open(files[i]);
            }
            else
            {
                move(nav, i);
            }
            var current = nav.Current!;
            first.Add(WaitFor(pipeline, current, ImageTier.Instant, sw));
            sharp.Add(WaitFor(pipeline, current, ImageTier.Screen, sw));

            if (jumpCold)
            {
                pipeline.Dispose();
            }
            else
            {
                Thread.Sleep(settleMs);
            }
        }

        Console.WriteLine($"{name}:");
        Console.WriteLine($"  first image  median {Median(first),7:F1} ms   worst {first.Max(),7:F1} ms");
        Console.WriteLine($"  sharp image  median {Median(sharp),7:F1} ms   worst {sharp.Max(),7:F1} ms");
    }

    private static void MeasureHeldKey(IImageDecoder decoder, SKSizeI screen, List<string> files)
    {
        using var pipeline = NewPipeline(decoder, screen);
        var navigator = new Navigator(pipeline);
        navigator.Open(files[0]);
        navigator.ApplyFolder(new List<string>(files));

        // Typical key repeat
        const int intervalMs = 33;
        var moves = Math.Min(60, files.Count * 3);
        var shown = 0;
        for (var i = 0; i < moves; i++)
        {
            navigator.Next();
            Thread.Sleep(intervalMs);
            using var _ = new Release(pipeline.AcquireBest(navigator.Current!), () => shown++);
        }

        var settle = Stopwatch.StartNew();
        WaitFor(pipeline, navigator.Current!, ImageTier.Screen);
        Console.WriteLine($"Held key, {1000 / intervalMs}/s:");
        Console.WriteLine($"  {shown} of {moves} moves had an image to show");
        Console.WriteLine($"  sharp {settle.Elapsed.TotalMilliseconds:F1} ms after release");
    }

    private static ImagePipeline NewPipeline(IImageDecoder decoder, SKSizeI screen) =>
        new(decoder, ImagePipeline.DefaultBudget(), screen, ImagePipeline.DefaultWorkers());

    private static double WaitFor(ImagePipeline pipeline, string path, ImageTier tier, Stopwatch? timer = null)
    {
        timer ??= Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < TimeoutMs)
        {
            var handle = pipeline.AcquireBest(path);
            if (handle != null)
            {
                var ready = handle.Tier >= tier;
                handle.Release();
                if (ready)
                    return timer.Elapsed.TotalMilliseconds;
            }
            Thread.Sleep(1);
        }
        throw new TimeoutException($"{path} never reached {tier}");
    }

    private static double Median(List<double> values) => values.OrderBy(v => v).ElementAt(values.Count / 2);

    private sealed class Release : IDisposable
    {
        private readonly ImageHandle? _handle;

        public Release(ImageHandle? handle, Action onImage)
        {
            _handle = handle;
            if (handle != null)
            {
                onImage();
            }
        }

        public void Dispose() => _handle?.Release();
    }
}
