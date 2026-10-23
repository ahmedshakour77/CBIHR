using System;
using System.Linq;
using System.Threading.Tasks;
using API.HR.Node.Data;
using API.HR.Node.Data.Notifications;
using API.HR.Node.Services.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public class NotificationServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NotificationTestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SendAsync_ShouldCreateUnreadNotification_AndFireEvent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(dbContext, loggerMock.Object);

        var eventFired = false;
        string? notifiedUser = null;
        service.OnNotificationChanged += u =>
        {
            eventFired = true;
            notifiedUser = u;
        };

        // Act
        var result = await service.SendAsync(
            userId: "user_1",
            title: "طلب جديد",
            message: "تم توجيه طلب جديد إليك",
            type: NotificationType.Workflow,
            linkUrl: "/workflows");

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("user_1");
        result.Title.Should().Be("طلب جديد");
        result.IsRead.Should().BeFalse();
        result.Type.Should().Be(NotificationType.Workflow);

        eventFired.Should().BeTrue();
        notifiedUser.Should().Be("user_1");

        var count = await service.GetUnreadCountAsync("user_1");
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(dbContext, loggerMock.Object);

        var notif = await service.SendAsync("user_2", "تنبيه", "رسالة تنبيه");

        // Act
        await service.MarkAsReadAsync(notif.Id, "user_2");

        // Assert
        var unreadCount = await service.GetUnreadCountAsync("user_2");
        unreadCount.Should().Be(0);

        var notifications = await service.GetNotificationsAsync("user_2");
        notifications.Should().HaveCount(1);
        notifications[0].IsRead.Should().BeTrue();
        notifications[0].ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkAllUserNotificationsAsRead()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(dbContext, loggerMock.Object);

        await service.SendAsync("user_3", "تنبيه 1", "رسالة 1");
        await service.SendAsync("user_3", "تنبيه 2", "رسالة 2");
        await service.SendAsync("other_user", "تنبيه 3", "رسالة 3");

        // Act
        await service.MarkAllAsReadAsync("user_3");

        // Assert
        (await service.GetUnreadCountAsync("user_3")).Should().Be(0);
        (await service.GetUnreadCountAsync("other_user")).Should().Be(1);
    }

    [Fact]
    public async Task DeleteNotificationAsync_ShouldRemoveNotification()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(dbContext, loggerMock.Object);

        var notif = await service.SendAsync("user_4", "حذف", "رسالة للحذف");

        // Act
        await service.DeleteNotificationAsync(notif.Id, "user_4");

        // Assert
        var notifications = await service.GetNotificationsAsync("user_4");
        notifications.Should().BeEmpty();
    }

    [Fact]
    public void DependencyInjection_ShouldResolveNotificationServiceWithoutAmbiguity()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => CreateInMemoryDbContext());
        services.AddScoped<INotificationService, NotificationService>();

        // This simulates HostApplicationBuilder.Build() with full validation enabled
        var provider = services.BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<INotificationService>();
        resolved.Should().NotBeNull();
    }
}
