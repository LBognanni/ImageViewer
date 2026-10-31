using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
[Platform("Linux")]
public class LinuxDefaultViewerTests
{
    private string? _originalDataHome;
    private string? _originalConfigHome;
    private string? _originalAppImage;
    private string _home = null!;

    /// <summary>
    /// These folders keep the test away from the real default applications
    /// </summary>
    [SetUp]
    public void Setup()
    {
        _originalDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _originalConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        _originalAppImage = Environment.GetEnvironmentVariable("APPIMAGE");
        _home = Directory.CreateTempSubdirectory("imageviewer-default").FullName;
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, "data"));
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_home, "config"));
        Directory.CreateDirectory(Path.Combine(_home, "config"));
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _originalDataHome);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _originalConfigHome);
        Environment.SetEnvironmentVariable("APPIMAGE", _originalAppImage);
    }

    [Test]
    public async Task MakeDefault_InstallsTheAppAndClaimsItsFormats()
    {
        Environment.SetEnvironmentVariable("APPIMAGE", "/opt/ImageViewer.AppImage");

        try
        {
            await new LinuxDefaultViewer().MakeDefault();
        }
        catch (IOException)
        {
            Assert.Inconclusive("xdg-mime is not installed here");
        }

        var entry = await File.ReadAllTextAsync(Path.Combine(_home, "data", "applications", "imageviewer.desktop"));
        Assert.That(entry, Does.Contain("Exec=\"/opt/ImageViewer.AppImage\" %f"));
        Assert.That(File.Exists(Path.Combine(_home, "data", "icons", "hicolor", "512x512", "apps", "imageviewer.png")), Is.True);

        var defaults = await File.ReadAllTextAsync(Path.Combine(_home, "config", "mimeapps.list"));
        Assert.That(defaults, Does.Contain("image/jpeg=imageviewer.desktop"));
    }

    [Test]
    public void ThePathComesFromTheAppImage_NotItsTemporaryMount()
    {
        Environment.SetEnvironmentVariable("APPIMAGE", "/opt/ImageViewer.AppImage");
        Assert.That(LinuxDefaultViewer.GetProgramPath(), Is.EqualTo("/opt/ImageViewer.AppImage"));
    }

    [Test]
    public void WithoutAnAppImage_ThePathIsThisProgram()
    {
        Environment.SetEnvironmentVariable("APPIMAGE", null);
        Assert.That(LinuxDefaultViewer.GetProgramPath(), Is.EqualTo(Environment.ProcessPath));
    }

    [TestCase("/home/me/my apps/viewer", "Exec=\"/home/me/my apps/viewer\" %f")]
    [TestCase("/home/me/100%/viewer", "Exec=\"/home/me/100%%/viewer\" %f")]
    [TestCase("/home/me/$x/viewer", "Exec=\"/home/me/\\\\$x/viewer\" %f")]
    public void ThePathIsQuotedForTheDesktopEntry(string path, string expected)
    {
        Assert.That(LinuxDefaultViewer.GetDesktopEntry(path), Does.Contain(expected));
    }
}
