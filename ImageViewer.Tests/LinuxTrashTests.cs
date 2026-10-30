using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ImageViewer.Tests;

[TestFixture]
[Platform("Linux")]
public class LinuxTrashTests
{
    private string? _originalDataHome;
    private string _dataHome = null!;

    /// <summary>
    /// Trash tools use $XDG_DATA_HOME, so a test folder keeps these files out of the real Trash
    /// </summary>
    [SetUp]
    public void Setup()
    {
        _originalDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _dataHome = Directory.CreateTempSubdirectory("imageviewer-trash").FullName;
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _dataHome);
    }

    [TearDown]
    public void TearDown() => Environment.SetEnvironmentVariable("XDG_DATA_HOME", _originalDataHome);

    [Test]
    public async Task MovesTheFileToTheTrash_RatherThanDeletingIt()
    {
        var file = Path.Combine(_dataHome, "image.jpg");
        await File.WriteAllTextAsync(file, "pixels");

        try
        {
            await new LinuxTrash().MoveToTrash(file);
        }
        catch (IOException)
        {
            Assert.Inconclusive("No trash tool is installed here");
        }

        ClassicAssert.IsFalse(File.Exists(file));
        ClassicAssert.IsTrue(File.Exists(Path.Combine(_dataHome, "Trash", "files", "image.jpg")));
    }

    [Test]
    public void AFileThatCannotBeTrashed_Throws()
    {
        Assert.ThrowsAsync<IOException>(() => new LinuxTrash().MoveToTrash(Path.Combine(_dataHome, "missing.jpg")));
    }
}
