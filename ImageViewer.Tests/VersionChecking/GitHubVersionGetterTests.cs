using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ImageViewer.Tests;

public class GitHubVersionGetterTests
{
    [Test]
    public void GetReleaseUrl_ReturnsTheCorrectUrl()
    {
        var getter = new GitHubVersionGetter();
        var url = getter.GetReleaseUrl(new Version(1, 0, 0));
        Assert.That(url, Is.EqualTo("https://github.com/LBognanni/ImageViewer/releases/tag/1.0.0"));
    }

    [Ignore("development only")]
    [Test]
    public async Task GetLatestVersion_Works()
    {
        var version = await (new GitHubVersionGetter()).GetLatestVersion();
        Assert.IsNotNull(version);
        Assert.AreEqual(new Version(1,10,0), version);
    }
}