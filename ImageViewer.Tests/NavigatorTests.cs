using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class NavigatorTests
{
    private FakeWindow _window = null!;
    private DateTime _now;
    private Navigator _sut = null!;

    [SetUp]
    public void Setup()
    {
        _window = new FakeWindow();
        _now = new DateTime(2026, 1, 1);
        _sut = new Navigator(_window, () => _now);
    }

    private void OpenFolder(string current, params string[] files)
    {
        _sut.Open(current);
        _sut.ApplyFolder(files.ToList());
    }

    private void Move(Action action)
    {
        _now = _now.AddSeconds(1);
        action();
    }

    [Test]
    public void Open_ShowsTheFileBeforeTheFolderIsKnown()
    {
        _sut.Open("b");
        Assert.That(_sut.Current, Is.EqualTo("b"));
        Assert.That(_window.Last, Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void ApplyFolder_KeepsTheCurrentFile()
    {
        OpenFolder("c", "a", "b", "c", "d");
        Assert.That(_sut.Current, Is.EqualTo("c"));
        Assert.That(_sut.Count, Is.EqualTo(4));
        Assert.That(_window.Last[1], Is.EqualTo("d"));
    }

    [Test]
    public void ApplyFolder_KeepsAFileTheListingDoesNotRecognize()
    {
        OpenFolder("b.tiff", "a.jpg", "c.jpg");
        Assert.That(_sut.Current, Is.EqualTo("b.tiff"));
        Assert.That(_sut.Count, Is.EqualTo(3));
        Move(_sut.Next);
        Assert.That(_sut.Current, Is.EqualTo("c.jpg"));
    }

    [Test]
    public void Next_WrapsAround()
    {
        OpenFolder("b", "a", "b");
        Move(_sut.Next);
        Assert.That(_sut.Current, Is.EqualTo("a"));
    }

    [Test]
    public void Previous_WrapsAround()
    {
        OpenFolder("a", "a", "b");
        Move(_sut.Previous);
        Assert.That(_sut.Current, Is.EqualTo("b"));
    }

    [Test]
    public void PrefetchFollowsTheDirectionOfTravel()
    {
        OpenFolder("c", "a", "b", "c", "d", "e", "f", "g", "h");

        Move(_sut.Next);
        Assert.That(_window.Last, Is.EqualTo(new[] { "d", "e", "c", "f", "g", "h" }));

        Move(_sut.Previous);
        Assert.That(_window.Last, Is.EqualTo(new[] { "c", "b", "d", "a", "h", "g" }));
    }

    [Test]
    public void PrefetchWindowHasNoDuplicatesInSmallFolders()
    {
        OpenFolder("a", "a", "b");
        Assert.That(_window.Last, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Upcoming_FollowsTheDirectionOfTravel()
    {
        OpenFolder("b", "a", "b", "c");
        Assert.That(_sut.Upcoming, Is.EqualTo("c"));
        Move(_sut.Previous);
        Move(_sut.Previous);
        Assert.That(_sut.Current, Is.EqualTo("c"));
        Assert.That(_sut.Upcoming, Is.EqualTo("b"));
    }

    [Test]
    public void FastMovesAreRapid_SlowOnesAreNot()
    {
        OpenFolder("a", "a", "b", "c", "d");

        Move(_sut.Next);
        Assert.That(_window.LastRapid, Is.False);

        _now = _now.AddMilliseconds(30);
        _sut.Next();
        Assert.That(_window.LastRapid, Is.True);

        Move(_sut.Next);
        Assert.That(_window.LastRapid, Is.False);
    }

    [Test]
    public void RemoveCurrent_MovesToTheFileAfterIt()
    {
        OpenFolder("b", "a", "b", "c");
        _sut.RemoveCurrent();
        Assert.That(_sut.Current, Is.EqualTo("c"));
        Assert.That(_sut.Count, Is.EqualTo(2));
    }

    [Test]
    public void RemoveCurrent_WrapsWhenRemovingTheLastFile()
    {
        OpenFolder("c", "a", "b", "c");
        _sut.RemoveCurrent();
        Assert.That(_sut.Current, Is.EqualTo("a"));
    }

    [Test]
    public void Shuffle_KeepsShowingTheSameFile()
    {
        var files = Enumerable.Range(0, 50).Select(i => i.ToString()).ToArray();
        OpenFolder("7", files);
        _sut.Shuffle();
        Assert.That(_sut.Current, Is.EqualTo("7"));
        Assert.That(_sut.Count, Is.EqualTo(50));
    }

    [Test]
    public void MovingNotifies()
    {
        OpenFolder("a", "a", "b");
        var notified = 0;
        _sut.CurrentChanged += () => notified++;
        Move(_sut.Next);
        Assert.That(notified, Is.EqualTo(1));
    }

    [Test]
    public async Task ListFolder_MatchesExtensionsInAnyCase_AndSkipsOthers()
    {
        var folder = Directory.CreateTempSubdirectory("imageviewer-test").FullName;
        File.WriteAllText(Path.Combine(folder, "B.JPG"), "");
        File.WriteAllText(Path.Combine(folder, "a.jpg"), "");
        File.WriteAllText(Path.Combine(folder, "c.txt"), "");

        var extensions = new SkiaImageDecoder().Extensions;
        var files = await Navigator.ListFolder(folder, extensions);

        Assert.That(files.Select(Path.GetFileName).ToArray(), Is.EqualTo(new[] { "a.jpg", "B.JPG" }));
    }

    private sealed class FakeWindow : IImageWindow
    {
        public IReadOnlyList<string> Last { get; private set; } = Array.Empty<string>();
        public bool LastRapid { get; private set; }

        public void SetWindow(IReadOnlyList<string> filesByPriority, bool rapid)
        {
            Last = filesByPriority;
            LastRapid = rapid;
        }
    }
}
