using Diary.Core;
using Diary.Core.Storage;
using Moq;
using Xunit;

namespace Diary.Tests;

public class StorageServiceTests
{
    private const string UserId = "KS-23";
    private const long Mb = 1024L * 1024;

    private readonly Mock<INotifier> _notifier = new();
    private readonly StorageService _service;

    public StorageServiceTests()
    {
        _service = new StorageService(_notifier.Object);
    }

    [Fact]
    public void NotificationIsSentOnceAtWarningLevel()
    {
        _service.CheckUsage(UserId, 80 * Mb, isPremium: false);

        _notifier.Verify(n => n.Send(UserId, StorageService.WarningMessage), Times.Once);
    }

    [Fact]
    public void NotificationHasExceededMessageWhenLimitExceeded()
    {
        _service.CheckUsage(UserId, 101 * Mb, isPremium: false);

        _notifier.Verify(
            n => n.Send(It.Is<string>(u => u == UserId),
                        It.Is<string>(m => m == StorageService.ExceededMessage)),
            Times.Once);
    }

    [Fact]
    public void NotificationIsNotSentWhenUsageIsNormal()
    {
        _service.CheckUsage(UserId, 10 * Mb, isPremium: false);

        _notifier.Verify(
            n => n.Send(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void NotificationIsNotRepeatedWhileStatusUnchanged()
    {
        _service.CheckUsage(UserId, 85 * Mb, isPremium: false);
        _service.CheckUsage(UserId, 90 * Mb, isPremium: false);

        _notifier.Verify(
            n => n.Send(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void NotificationIsSentAgainAfterReturnToNormal()
    {
        _service.CheckUsage(UserId, 85 * Mb, isPremium: false);
        _service.CheckUsage(UserId, 10 * Mb, isPremium: false);
        _service.CheckUsage(UserId, 90 * Mb, isPremium: false);

        _notifier.Verify(
            n => n.Send(UserId, StorageService.WarningMessage), Times.Exactly(2));
    }

    [Fact]
    public void PremiumUserWithSameUsageGetsNoNotification()
    {
        _service.CheckUsage(UserId, 85 * Mb, isPremium: true);

        _notifier.Verify(
            n => n.Send(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StubNotifierDoesNotAffectReturnedStatus()
    {
        var stub = Mock.Of<INotifier>();
        var service = new StorageService(stub);

        var status = service.CheckUsage(UserId, 101 * Mb, isPremium: false);

        Assert.Equal(StorageStatus.Exceeded, status);
    }

    [Fact]
    public void StubNotifierThrows_ExceptionPropagatesFromCheckUsage()
    {
        var stub = new Mock<INotifier>();
        stub.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Поштовий сервіс недоступний"));
        var service = new StorageService(stub.Object);

        Assert.Throws<InvalidOperationException>(
            () => service.CheckUsage(UserId, 85 * Mb, isPremium: false));
    }

    [Fact]
    public void ConstructorRejectsNullNotifier()
    {
        Assert.Throws<ArgumentNullException>(() => new StorageService(null!));
    }
}