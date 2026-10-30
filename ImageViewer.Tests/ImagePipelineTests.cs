using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SkiaSharp;

namespace ImageViewer.Tests;

[TestFixture]
public class ImagePipelineTests
{
    private static readonly SKSizeI Screen = new(1920, 1080);

    [Test]
    public void DecodesTheCurrentImageFirst_AtInstantThenScreen()
    {
        var decoder = new FakeDecoder();
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        sut.SetWindow(new[] { "a", "b" }, false);
        WaitUntil(() => decoder.Log.Count >= 3);

        ClassicAssert.AreEqual(new[] { "a:640", "a:1920", "b:1920" }, decoder.Log.ToArray());
    }

    [Test]
    public void RapidNavigation_GetsInstantImagesForTheWindowBeforeAnythingSharp()
    {
        var decoder = new FakeDecoder();
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        sut.SetWindow(new[] { "a", "b", "c" }, true);
        WaitUntil(() => decoder.Log.Count >= 6);

        ClassicAssert.AreEqual(new[] { "a:640", "b:640", "c:640", "a:1920", "b:1920", "c:1920" }, decoder.Log.ToArray());
    }

    [Test]
    public void MovingOn_DropsQueuedWork()
    {
        var decoder = new FakeDecoder { Gate = new ManualResetEventSlim(false) };
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        // The worker gets stuck on the first job, with the others queued behind it
        sut.SetWindow(new[] { "old1", "old2", "old3" }, false);
        WaitUntil(() => decoder.Started >= 1);

        sut.SetWindow(new[] { "new" }, false);
        decoder.Gate.Set();
        WaitUntil(() => decoder.Log.Contains("new:1920"));

        ClassicAssert.IsFalse(decoder.Log.Any(l => l.StartsWith("old2") || l.StartsWith("old3")));
    }

    [Test]
    public void FormatsWithoutCheapScaling_SkipTheInstantTier()
    {
        var decoder = new FakeDecoder { CheapScaling = false };
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        sut.SetWindow(new[] { "a" }, false);
        WaitUntil(() => HasTier(sut, "a", ImageTier.Screen));
        Thread.Sleep(50);

        ClassicAssert.AreEqual(new[] { "a:1920" }, decoder.Log.ToArray());
    }

    [Test]
    public void RequestFull_DecodesAtFullResolution()
    {
        var decoder = new FakeDecoder();
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        sut.SetWindow(new[] { "a" }, false);
        WaitUntil(() => HasTier(sut, "a", ImageTier.Screen));
        sut.RequestFull("a");
        WaitUntil(() => HasTier(sut, "a", ImageTier.Full));

        ClassicAssert.Contains("a:full", decoder.Log.ToArray());
    }

    [Test]
    public void PrefetchFull_SkipsTheScreenTier()
    {
        var decoder = new FakeDecoder();
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1) { PrefetchFull = true };

        sut.SetWindow(new[] { "a" }, false);
        WaitUntil(() => HasTier(sut, "a", ImageTier.Full));

        ClassicAssert.IsFalse(decoder.Log.Contains("a:1920"));
    }

    [Test]
    public void AFileThatCannotBeDecoded_ShowsTheBrokenImage()
    {
        var decoder = new FakeDecoder { Fail = true };
        using var sut = new ImagePipeline(decoder, 1 << 30, Screen, 1);

        sut.SetWindow(new[] { "a" }, false);
        WaitUntil(() => HasTier(sut, "a", ImageTier.Full));

        var handle = sut.AcquireBest("a")!;
        ClassicAssert.Greater(handle.Image.Width, 0);
        handle.Release();
    }

    [Test]
    public void BackgroundIsSampledFromTheImage()
    {
        var path = TestImages.Jpeg(64, 64, SKColors.Red);
        using var sut = new ImagePipeline(new SkiaImageDecoder(), 1 << 30, Screen, 1);

        sut.SetWindow(new[] { path }, false);
        WaitUntil(() => HasTier(sut, path, ImageTier.Screen));

        var handle = sut.AcquireBest(path)!;
        ClassicAssert.Greater(handle.Background.Red, 240);
        ClassicAssert.Less(handle.Background.Green, 15);
        handle.Release();
    }

    private static bool HasTier(ImagePipeline pipeline, string path, ImageTier tier)
    {
        var handle = pipeline.AcquireBest(path);
        if (handle == null)
            return false;
        var result = handle.Tier >= tier;
        handle.Release();
        return result;
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.ElapsedMilliseconds > 3000)
                Assert.Fail("Timed out");
            Thread.Sleep(1);
        }
    }

    private sealed class FakeDecoder : IImageDecoder
    {
        private const int FullSize = 4000;
        private readonly object _lock = new();
        private readonly List<string> _log = new();
        private int _started;

        public bool CheapScaling { get; init; } = true;
        public bool Fail { get; init; }
        public ManualResetEventSlim? Gate { get; init; }
        public int Started => Volatile.Read(ref _started);

        public List<string> Log
        {
            get
            {
                lock (_lock)
                {
                    return _log.ToList();
                }
            }
        }

        public IReadOnlySet<string> Extensions { get; } = new HashSet<string>();

        public ImageInfo? Probe(string path) =>
            Fail ? null : new ImageInfo(FullSize, FullSize, SKEncodedOrigin.TopLeft, CheapScaling);

        public DecodedImage? Decode(string path, SKSizeI? target)
        {
            Interlocked.Increment(ref _started);
            Gate?.Wait();
            lock (_lock)
            {
                _log.Add($"{path}:{(target == null ? "full" : target.Value.Width.ToString())}");
            }

            // Sized as if decoded, without the memory of a real full size image
            using var bitmap = new SKBitmap(2, 2);
            var size = target?.Width ?? FullSize;
            var source = new ImageInfo(target == null ? 2 : size * 2, target == null ? 2 : size * 2, SKEncodedOrigin.TopLeft, CheapScaling);
            return new DecodedImage(SKImage.FromBitmap(bitmap), source);
        }
    }
}
