using NUnit.Framework.Legacy;
using System;
using System.IO;
using NUnit.Framework;

namespace ImageViewer.Tests;

public class FileBasedTempDataTests
{
    private string _file = null!;

    [SetUp]
    public void Setup()
    {
        _file = Path.Combine(Directory.CreateTempSubdirectory().FullName, "nested", "state.json");
    }

    [Test]
    public void AfterSavingTempData_CanReadItBack()
    {
        var sut = new FileBasedTempData(_file);
        var now = DateTime.Now;
        sut.WriteTime("test", now);
        Assert.That(sut.ReadTime("test"), Is.EqualTo(now));
    }

    [Test]
    public void WhenNoData_NullIsReturned()
    {
        ClassicAssert.IsNull(new FileBasedTempData(_file).ReadTime("aa"));
    }

    [Test]
    public void WhenSavingManyProperties_AllCanBeReadBack()
    {
        var sut = new FileBasedTempData(_file);
        var first = new DateTime(2026, 1, 1);
        var second = new DateTime(2026, 2, 2);
        sut.WriteTime("first", first);
        sut.WriteTime("second", second);
        Assert.That(sut.ReadTime("first"), Is.EqualTo(first));
        Assert.That(sut.ReadTime("second"), Is.EqualTo(second));
    }

    [Test]
    public void ADamagedFile_IsTreatedAsEmpty()
    {
        var sut = new FileBasedTempData(_file);
        sut.WriteTime("test", DateTime.Now);
        File.WriteAllText(_file, "{ not json");
        ClassicAssert.IsNull(sut.ReadTime("test"));
    }
}
