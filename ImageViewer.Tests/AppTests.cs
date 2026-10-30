using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ImageViewer.Tests;

[TestFixture]
public class AppTests
{
    [Test]
    public void TheAppHasItsOwnName()
    {
        ClassicAssert.AreEqual("Image Viewer", new App(null!).Name);
    }
}
