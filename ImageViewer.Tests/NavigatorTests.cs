using NUnit.Framework.Legacy;
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
        ClassicAssert.AreEqual("b", _sut.Current);
        ClassicAssert.AreEqual(new[] { "b" }, _window.Last);
    }

    [Test]
    public void ApplyFolder_KeepsTheCurrentFile()
    {
        OpenFolder("c", "a", "b", "c", "d");
        ClassicAssert.AreEqual("c", _sut.Current);
        ClassicAssert.AreEqual(4, _sut.Count);
        ClassicAssert.AreEqual("d", _window.Last[1]);
    }

    [Test]
    public void ApplyFolder_KeepsAFileTheListingDoesNotRecognize()
    {
        OpenFolder("b.tiff", "a.jpg", "c.jpg");
        ClassicAssert.AreEqual("b.tiff", _sut.Current);
        ClassicAssert.AreEqual(3, _sut.Count);
        Move(_sut.Next);
        ClassicAssert.AreEqual("c.jpg", _sut.Current);
    }

    [Test]
    public void Next_WrapsAround()
    {
        OpenFolder("b", "a", "b");
        Move(_sut.Next);
        ClassicAssert.AreEqual("a", _sut.Current);
    }

    [Test]
    public void Previous_WrapsAround()
    {
        OpenFolder("a", "a", "b");
        Move(_sut.Previous);
        ClassicAssert.AreEqual("b", _sut.Current);
    }

    [Test]
    public void PrefetchFollowsTheDirectionOfTravel()
    {
        OpenFolder("c", "a", "b", "c", "d", "e", "f", "g", "h");

        Move(_sut.Next);
        ClassicAssert.AreEqual(new[] { "d", "e", "c", "f", "g", "h" }, _window.Last);

        Move(_sut.Previous);
        ClassicAssert.AreEqual(new[] { "c", "b", "d", "a", "h", "g" }, _window.Last);
    }

    [Test]
    public void PrefetchWindowHasNoDuplicatesInSmallFolders()
    {
        OpenFolder("a", "a", "b");
        ClassicAssert.AreEqual(new[] { "a", "b" }, _window.Last);
    }

    [Test]
    public void Upcoming_FollowsTheDirectionOfTravel()
    {
        OpenFolder("b", "a", "b", "c");
        ClassicAssert.AreEqual("c", _sut.Upcoming);
        Move(_sut.Previous);
        Move(_sut.Previous);
        ClassicAssert.AreEqual("c", _sut.Current);
        ClassicAssert.AreEqual("b", _sut.Upcoming);
    }

    [Test]
    public void FastMovesAreRapid_SlowOnesAreNot()
    {
        OpenFolder("a", "a", "b", "c", "d");

        Move(_sut.Next);
        ClassicAssert.IsFalse(_window.LastRapid);

        _now = _now.AddMilliseconds(30);
        _sut.Next();
        ClassicAssert.IsTrue(_window.LastRapid);

        Move(_sut.Next);
        ClassicAssert.IsFalse(_window.LastRapid);
    }

    [Test]
    public void RemoveCurrent_MovesToTheFileAfterIt()
    {
        OpenFolder("b", "a", "b", "c");
        _sut.RemoveCurrent();
        ClassicAssert.AreEqual("c", _sut.Current);
        ClassicAssert.AreEqual(2, _sut.Count);
    }

    [Test]
    public void RemoveCurrent_WrapsWhenRemovingTheLastFile()
    {
        OpenFolder("c", "a", "b", "c");
        _sut.RemoveCurrent();
        ClassicAssert.AreEqual("a", _sut.Current);
    }

    [Test]
    public void Shuffle_KeepsShowingTheSameFile()
    {
        var files = Enumerable.Range(0, 50).Select(i => i.ToString()).ToArray();
        OpenFolder("7", files);
        _sut.Shuffle();
        ClassicAssert.AreEqual("7", _sut.Current);
        ClassicAssert.AreEqual(50, _sut.Count);
    }

    [Test]
    public void MovingNotifies()
    {
        OpenFolder("a", "a", "b");
        var notified = 0;
        _sut.CurrentChanged += () => notified++;
        Move(_sut.Next);
        ClassicAssert.AreEqual(1, notified);
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

        ClassicAssert.AreEqual(new[] { "a.jpg", "B.JPG" }, files.Select(Path.GetFileName).ToArray());
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
