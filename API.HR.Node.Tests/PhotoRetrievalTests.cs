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

public sealed class PhotoRetrievalTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IFileStorageService> _storageMock;
    private readonly EmployeeProfilePhotoService _service;
    private readonly ClaimsPrincipal _testUser;

    public PhotoRetrievalTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(dbOptions);

        _storageMock = new Mock<IFileStorageService>();
        var imageProcessorMock = new Mock<IImageProcessor>();
        var malwareScannerMock = new Mock<IFileMalwareScanner>();
        var auditLoggerMock = new Mock<IAuditLogger>();
        var loggerMock = new Mock<ILogger<EmployeeProfilePhotoService>>();

        _service = new EmployeeProfilePhotoService(
            _dbContext,
            _storageMock.Object,
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
    public async Task GetPhoto_WhenNoPhotoExists_ShouldReturnNotFound()
    {
        var result = await _service.GetPhotoAsync("EMP100", thumbnail: false, ifNoneMatchHeader: null, _testUser, "127.0.0.1", "TestAgent");

        result.Should().BeOfType<PhotoServiceResult.NotFound>();
    }

    [Fact]
    public async Task GetPhoto_WhenActivePhotoExists_ShouldReturnStreamData()
    {
        // Seed database with an active photo
        var photo = new EmployeeProfilePhoto
        {
            EmployeeID = "EMP100",
            StorageKey = "employees/EMP100/profile/1/original",
            ThumbnailStorageKey = "employees/EMP100/profile/1/thumbnail",
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

        var sampleStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        _storageMock
            .Setup(s => s.OpenReadAsync("employees/EMP100/profile/1/original", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleStream);

        var result = await _service.GetPhotoAsync("EMP100", thumbnail: false, ifNoneMatchHeader: null, _testUser, "127.0.0.1", "TestAgent");

        result.Should().BeOfType<PhotoServiceResult.PhotoStreamData>();
        var streamData = (PhotoServiceResult.PhotoStreamData)result;
        streamData.ContentType.Should().Be("image/jpeg");
        streamData.ETag.Should().Be("\"abc123hash-v1\"");
    }

    [Fact]
    public async Task GetPhoto_WhenETagMatchesIfNoneMatch_ShouldReturnNotModified()
    {
        var photo = new EmployeeProfilePhoto
        {
            EmployeeID = "EMP100",
            StorageKey = "employees/EMP100/profile/1/original",
            ThumbnailStorageKey = "employees/EMP100/profile/1/thumbnail",
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

        var etag = "\"abc123hash-v1\"";

        var result = await _service.GetPhotoAsync("EMP100", thumbnail: false, ifNoneMatchHeader: etag, _testUser, "127.0.0.1", "TestAgent");

        result.Should().BeOfType<PhotoServiceResult.NotModified>();
    }
}
