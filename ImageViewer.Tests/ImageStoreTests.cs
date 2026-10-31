using System.Collections.Generic;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class ImageStoreTests
{
    // A 10x10 image takes 400 bytes
    private const long OneImage = 400;

    [Test]
    public void BetterTiersReplaceInstant()
    {
        var sut = new ImageStore(OneImage * 10);
        sut.Add(TestImages.Handle("a", ImageTier.Instant));
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        Assert.That(sut.Bytes, Is.EqualTo(OneImage));
        Assert.That(sut.Has("a", ImageTier.Instant), Is.True);
    }

    [Test]
    public void HasIsSatisfiedByHigherTiersOnly()
    {
        var sut = new ImageStore(OneImage * 10);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        Assert.That(sut.Has("a", ImageTier.Screen), Is.True);
        Assert.That(sut.Has("a", ImageTier.Full), Is.False);
    }

    [Test]
    public void AcquireBest_PrefersTheHighestTier()
    {
        var sut = new ImageStore(OneImage * 10);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        sut.Add(TestImages.Handle("a", ImageTier.Full));
        var best = sut.AcquireBest("a");
        Assert.That(best!.Tier, Is.EqualTo(ImageTier.Full));
        best.Release();
    }

    [Test]
    public void EvictsTheFurthestImage_NotTheOldest()
    {
        var sut = new ImageStore(OneImage * 2);
        sut.SetRanks(new Dictionary<string, int> { ["now"] = 0, ["next"] = 1, ["far"] = 2 });
        sut.Add(TestImages.Handle("next", ImageTier.Screen));
        sut.Add(TestImages.Handle("far", ImageTier.Screen));
        sut.Add(TestImages.Handle("now", ImageTier.Screen));

        Assert.That(sut.Has("next", ImageTier.Screen), Is.True);
        Assert.That(sut.Has("now", ImageTier.Screen), Is.True);
        Assert.That(sut.Has("far", ImageTier.Screen), Is.False);
    }

    [Test]
    public void FilesOutsideTheWindowGoFirst()
    {
        var sut = new ImageStore(OneImage);
        sut.Add(TestImages.Handle("old", ImageTier.Screen));
        sut.SetRanks(new Dictionary<string, int> { ["now"] = 0 });
        sut.Add(TestImages.Handle("now", ImageTier.Screen));
        Assert.That(sut.Has("old", ImageTier.Screen), Is.False);
    }

    [Test]
    public void NeverEvictsTheCurrentImage_EvenOverBudget()
    {
        var sut = new ImageStore(1);
        sut.SetRanks(new Dictionary<string, int> { ["now"] = 0 });
        sut.Add(TestImages.Handle("now", ImageTier.Full));
        Assert.That(sut.Has("now", ImageTier.Full), Is.True);
    }

    [Test]
    public void AHeldImageSurvivesEviction()
    {
        var sut = new ImageStore(OneImage);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        var held = sut.AcquireBest("a")!;

        sut.Remove("a");

        Assert.That(sut.Bytes, Is.EqualTo(0));
        Assert.That(held.Image.Width, Is.GreaterThan(0));
        Assert.That(held.TryAcquire(), Is.True, "the pixels should still be alive");
        held.Release();
        held.Release();
        Assert.That(held.TryAcquire(), Is.False, "the last release should free the pixels");
    }
}
