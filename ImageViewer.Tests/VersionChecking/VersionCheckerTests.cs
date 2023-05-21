using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;

namespace ImageViewer.Tests;

public class VersionCheckerTests
{
    private VersionChecker _sut;
    private readonly Version _currentVersion = new(1,2, 3);
    private Mock<IVersionGetter> _versionGetter;
    private Mock<ITempData> _tempData;
    private Mock<IWindowsNotification> _notifier;
    private readonly Expression<Action<IWindowsNotification>> _notifierExpression = x=>x.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>());

    [SetUp]
    public void Setup()
    {
        _versionGetter = new Mock<IVersionGetter>();
        _notifier = new Mock<IWindowsNotification>();
        _tempData = new Mock<ITempData>();
        _sut = new VersionChecker(_versionGetter.Object, _currentVersion, _notifier.Object, _tempData.Object);
        _versionGetter.Setup(x => x.GetReleaseUrl(It.IsAny<Version>())).Returns("http://foo.bar");
        _notifier.Setup(_notifierExpression).Verifiable();
    }
    
    [TestCase(2,0,0)]
    [TestCase(1,3,0)]
    [TestCase(1,2,4)]
    public async Task WhenThereIsANewVersion_AndHasNotNotifiedToday_ItNotifies(int major, int minor, int patch)
    {
        _versionGetter.Setup(x => x.GetLatestVersion()).ReturnsAsync(new Version(major, minor, patch));
        await _sut.NotifyIfNewVersion();
        _notifier.Verify(_notifierExpression, Times.Once);
        _tempData.Verify(x=>x.Read<DateTime>(VersionChecker.LASTNOTIFICATIONDATE), Times.Once);
        _tempData.Verify(x=>x.Write(VersionChecker.LASTNOTIFICATIONDATE, It.IsAny<DateTime>()), Times.Once);
    }
    
    [TestCase(2,0,0)]
    [TestCase(1,3,0)]
    [TestCase(1,2,4)]
    public async Task WhenThereIsANewVersion_AndHasNotifiedToday_ItDoesNotNotify(int major, int minor, int patch)
    {
        _tempData.Setup(x => x.Read<DateTime>(VersionChecker.LASTNOTIFICATIONDATE)).Returns(DateTime.Now.AddHours(-20));
        _versionGetter.Setup(x => x.GetLatestVersion()).ReturnsAsync(new Version(major, minor, patch));
        await _sut.NotifyIfNewVersion();
        _notifier.Verify(_notifierExpression, Times.Never);
    }
    
    [TestCase(1,1,3)]
    [TestCase(1,2,2)]
    [TestCase(0,0,1)]
    public async Task WhenThereIsNotANewVersion_ItDoesNotNotify(int major, int minor, int patch)
    {
        _versionGetter.Setup(x => x.GetLatestVersion()).ReturnsAsync(new Version(major, minor, patch));
        await _sut.NotifyIfNewVersion();
        _notifier.Verify(_notifierExpression, Times.Never);
        _tempData.Verify(x=>x.Read<DateTime>(VersionChecker.LASTNOTIFICATIONDATE), Times.Never);
    }

    [Test]
    public async Task WhenThereWasAnErrorRetrievingVersion_ItDoesNotNotify()
    {
        _versionGetter.Setup(x => x.GetLatestVersion()).ReturnsAsync((Version)null);
        await _sut.NotifyIfNewVersion();
        _notifier.Verify(_notifierExpression, Times.Never);
        _tempData.Verify(x=>x.Read<DateTime>(VersionChecker.LASTNOTIFICATIONDATE), Times.Never);
    }
}