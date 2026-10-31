using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class AppTests
{
    [Test]
    public void TheAppHasItsOwnName()
    {
        Assert.That(new App(null!).Name, Is.EqualTo("Image Viewer"));
    }
}
