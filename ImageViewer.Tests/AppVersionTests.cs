using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class AppVersionTests
{
    [TestCase("2.1.3", "Version 2.1.3")]
    [TestCase("2.0.0-rc6", "Version 2.0.0-rc6")]
    [TestCase("0.0.0", "Development version")]
    [TestCase(null, "Development version")]
    public void DescribesTheVersion(string? version, string expected) =>
        Assert.That(AppVersion.Describe(version), Is.EqualTo(expected));
}
