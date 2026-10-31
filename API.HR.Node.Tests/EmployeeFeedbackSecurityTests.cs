using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Data.Feedback;
using API.HR.Node.Security;
using API.HR.Node.Services.Features;
using API.HR.Node.Services.Feedback;
using API.HR.Node.Services.Security;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class EmployeeFeedbackSecurityTests
{
    private (ApplicationDbContext DbContext, EmployeeFeedbackService Service, Mock<IAuthorizationService> AuthMock) CreateTestService()
    {
        var dbName = $"FeedbackSecurityTests_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new ApplicationDbContext(options);

        var fileStorageMock = new Mock<IFileStorageService>();
        var malwareScannerMock = new Mock<IFileMalwareScanner>();
        var featureServiceMock = new Mock<ISystemFeatureService>();
        var authServiceMock = new Mock<IAuthorizationService>();

        var service = new EmployeeFeedbackService(
            db,
            fileStorageMock.Object,
            malwareScannerMock.Object,
            featureServiceMock.Object,
            authServiceMock.Object,
            NullLogger<EmployeeFeedbackService>.Instance);

        return (db, service, authServiceMock);
    }

    [Fact]
    public async Task UpdateFeedbackStatusAsync_CallerWithFeedbackViewOnly_ShouldBeDenied()
    {
        var (db, service, authMock) = CreateTestService();

        var feedback = new EmployeeFeedback
        {
            Id = 10,
            ReferenceNumber = "FB-2026-0001",
            Title = "اقتراح تحسين الخدمة",
            Description = "نص المقترح الوارد من أحد الموظفين للتحسين والتطوير.",
            FeedbackType = "فكرة",
            Status = FeedbackStatus.Pending
        };
        db.EmployeeFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        // Caller has Feedback.View only
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.FeedbackView));
        var callerPrincipal = new ClaimsPrincipal(identity);

        // Mock authorization service to simulate requirement failure for Feedback.Edit
        authMock
            .Setup(a => a.AuthorizeAsync(
                callerPrincipal,
                null,
                It.Is<IAuthorizationRequirement[]>(r => r.OfType<PermissionRequirement>().Any(pr => pr.Permission == Permissions.FeedbackEdit))))
            .ReturnsAsync(AuthorizationResult.Failed());

        var result = await service.UpdateFeedbackStatusAsync(
            feedback.Id,
            FeedbackStatus.Resolved,
            "تمت المراجعة والموافقة",
            callerPrincipal);

        result.Should().BeFalse("Caller with Feedback.View only must be rejected on status mutation");

        var reloaded = await db.EmployeeFeedbacks.FindAsync(feedback.Id);
        reloaded!.Status.Should().Be(FeedbackStatus.Pending, "Database state must remain untouched when authorization fails");
        reloaded.AdminNotes.Should().BeNull();
    }

    [Fact]
    public async Task UpdateFeedbackStatusAsync_CallerWithFeedbackEdit_ShouldBeAllowed()
    {
        var (db, service, authMock) = CreateTestService();

        var feedback = new EmployeeFeedback
        {
            Id = 20,
            ReferenceNumber = "FB-2026-0002",
            Title = "ملاحظة بخصوص نظام البصمة",
            Description = "نص الملاحظة الواردة من الموظف لغرض المعالجة.",
            FeedbackType = "ملاحظة",
            Status = FeedbackStatus.Pending
        };
        db.EmployeeFeedbacks.Add(feedback);
        await db.SaveChangesAsync();

        // Caller has Feedback.Edit
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "admin-reviewer-id"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "HR_Reviewer"));
        identity.AddClaim(new Claim("permission", Permissions.FeedbackEdit));
        var callerPrincipal = new ClaimsPrincipal(identity);

        // Mock authorization service to simulate success for Feedback.Edit
        authMock
            .Setup(a => a.AuthorizeAsync(
                callerPrincipal,
                null,
                It.Is<IAuthorizationRequirement[]>(r => r.OfType<PermissionRequirement>().Any(pr => pr.Permission == Permissions.FeedbackEdit))))
            .ReturnsAsync(AuthorizationResult.Success());

        var result = await service.UpdateFeedbackStatusAsync(
            feedback.Id,
            FeedbackStatus.Resolved,
            "تم التحقق وحل المشكلة المذكورة.",
            callerPrincipal);

        result.Should().BeTrue("Caller with Feedback.Edit must be permitted to mutate feedback status");

        var reloaded = await db.EmployeeFeedbacks.FindAsync(feedback.Id);
        reloaded!.Status.Should().Be(FeedbackStatus.Resolved);
        reloaded.AdminNotes.Should().Be("تم التحقق وحل المشكلة المذكورة.");
        reloaded.RespondedByUserName.Should().Be("HR_Reviewer");
        reloaded.RespondedAt.Should().NotBeNull();
    }
}
