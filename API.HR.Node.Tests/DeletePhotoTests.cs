using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Images;
using API.HR.Node.Services.ProfilePhotos;
using API.HR.Node.Services.Security;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class DeletePhotoTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmployeeProfilePhotoService _service;
    private readonly ClaimsPrincipal _testUser;

    public DeletePhotoTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(dbOptions);

        var storageMock = new Mock<IFileStorageService>();
        var imageProcessorMock = new Mock<IImageProcessor>();
        var malwareScannerMock = new Mock<IFileMalwareScanner>();
        var auditLoggerMock = new Mock<IAuditLogger>();
        var loggerMock = new Mock<ILogger<EmployeeProfilePhotoService>>();

        _service = new EmployeeProfilePhotoService(
            _dbContext,
            storageMock.Object,
            imageProcessorMock.Object,
            malwareScannerMock.Object,
            auditLoggerMock.Object,
            loggerMock.Object);

        _testUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Name, "AdminUser")
        ], "TestAuth"));
    }

    [Fact]
    public async Task DeletePhoto_WhenActivePhotoExists_ShouldSoftDelete()
    {
        var photo = new EmployeeProfilePhoto
        {
            EmployeeID = "EMP300",
            StorageKey = "employees/EMP300/profile/1/original",
            ThumbnailStorageKey = "employees/EMP300/profile/1/thumbnail",
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg",
            FileSize = 1000,
            Width = 200,
            Height = 200,
            Sha256 = "abc123hash",
            Version = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "AdminUser"
        };
        _dbContext.EmployeeProfilePhotos.Add(photo);
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeletePhotoAsync("EMP300", _testUser, "127.0.0.1", "TestAgent");

        result.Should().BeOfType<PhotoServiceResult.Success<bool>>();

        var dbPhoto = await _dbContext.EmployeeProfilePhotos.FindAsync(photo.Id);
        dbPhoto!.IsActive.Should().BeFalse();
        dbPhoto.DeletedAt.Should().NotBeNull();
        dbPhoto.DeletedBy.Should().Be("AdminUser");
    }

    [Fact]
    public async Task DeletePhoto_WhenNoActivePhoto_ShouldReturnNotFound()
    {
        var result = await _service.DeletePhotoAsync("EMP999", _testUser, "127.0.0.1", "TestAgent");

        result.Should().BeOfType<PhotoServiceResult.NotFound>();
    }
}
