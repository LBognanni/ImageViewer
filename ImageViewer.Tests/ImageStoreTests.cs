using NUnit.Framework.Legacy;
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
        ClassicAssert.AreEqual(OneImage, sut.Bytes);
        ClassicAssert.IsTrue(sut.Has("a", ImageTier.Instant));
    }

    [Test]
    public void HasIsSatisfiedByHigherTiersOnly()
    {
        var sut = new ImageStore(OneImage * 10);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        ClassicAssert.IsTrue(sut.Has("a", ImageTier.Screen));
        ClassicAssert.IsFalse(sut.Has("a", ImageTier.Full));
    }

    [Test]
    public void AcquireBest_PrefersTheHighestTier()
    {
        var sut = new ImageStore(OneImage * 10);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        sut.Add(TestImages.Handle("a", ImageTier.Full));
        var best = sut.AcquireBest("a");
        ClassicAssert.AreEqual(ImageTier.Full, best!.Tier);
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

        ClassicAssert.IsTrue(sut.Has("next", ImageTier.Screen));
        ClassicAssert.IsTrue(sut.Has("now", ImageTier.Screen));
        ClassicAssert.IsFalse(sut.Has("far", ImageTier.Screen));
    }

    [Test]
    public void FilesOutsideTheWindowGoFirst()
    {
        var sut = new ImageStore(OneImage);
        sut.Add(TestImages.Handle("old", ImageTier.Screen));
        sut.SetRanks(new Dictionary<string, int> { ["now"] = 0 });
        sut.Add(TestImages.Handle("now", ImageTier.Screen));
        ClassicAssert.IsFalse(sut.Has("old", ImageTier.Screen));
    }

    [Test]
    public void NeverEvictsTheCurrentImage_EvenOverBudget()
    {
        var sut = new ImageStore(1);
        sut.SetRanks(new Dictionary<string, int> { ["now"] = 0 });
        sut.Add(TestImages.Handle("now", ImageTier.Full));
        ClassicAssert.IsTrue(sut.Has("now", ImageTier.Full));
    }

    [Test]
    public void AHeldImageSurvivesEviction()
    {
        var sut = new ImageStore(OneImage);
        sut.Add(TestImages.Handle("a", ImageTier.Screen));
        var held = sut.AcquireBest("a")!;

        sut.Remove("a");

        ClassicAssert.AreEqual(0, sut.Bytes);
        ClassicAssert.Greater(held.Image.Width, 0);
        ClassicAssert.IsTrue(held.TryAcquire(), "the pixels should still be alive");
        held.Release();
        held.Release();
        ClassicAssert.IsFalse(held.TryAcquire(), "the last release should free the pixels");
    }
}
