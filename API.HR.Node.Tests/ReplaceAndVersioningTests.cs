using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Images;
using API.HR.Node.Services.ProfilePhotos;
using API.HR.Node.Services.Security;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace API.HR.Node.Tests;

public sealed class ReplaceAndVersioningTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmployeeProfilePhotoService _service;
    private readonly ClaimsPrincipal _testUser;

    public ReplaceAndVersioningTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new ApplicationDbContext(dbOptions);

        var storageMock = new Mock<IFileStorageService>();
        storageMock.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream s, string key, CancellationToken ct) => key);

        var imageProcessorMock = new Mock<IImageProcessor>();
        imageProcessorMock.Setup(p => p.ValidateImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageValidationResult(true, null, 200, 200, "image/jpeg"));

        imageProcessorMock.Setup(p => p.ProcessAndGenerateThumbnailAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedImageResult([1, 2], [3, 4], 200, 200, "image/jpeg", "sha256hash"));

        var malwareScannerMock = new Mock<IFileMalwareScanner>();
        malwareScannerMock.Setup(m => m.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MalwareScanResult(MalwareScanStatus.Clean, "TestScanner"));

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
            new Claim(ClaimTypes.Name, "AdminUser"),
            new Claim(ClaimTypes.Role, "Admin")
        ], "TestAuth"));
    }

    [Fact]
    public async Task Upload_FirstAndSecondUpload_ShouldIncrementVersionAndKeepSingleActivePhoto()
    {
        // Seed Employee
        _dbContext.Employees.Add(new Employee { EmployeeID = "EMP200", Name = "Jane Doe" });
        await _dbContext.SaveChangesAsync();

        var formFile1 = CreateDummyFormFile("photo1.jpg");
        var formFile2 = CreateDummyFormFile("photo2.jpg");

        // 1st Upload (Version 1)
        var result1 = await _service.UploadOrReplacePhotoAsync("EMP200", formFile1, _testUser, "127.0.0.1", "TestAgent");
        result1.Should().BeOfType<PhotoServiceResult.Success<Contracts.ProfilePhotoResponse>>();
        var res1Data = ((PhotoServiceResult.Success<Contracts.ProfilePhotoResponse>)result1).Data;
        res1Data.Version.Should().Be(1);

        // 2nd Upload (Version 2)
        var result2 = await _service.UploadOrReplacePhotoAsync("EMP200", formFile2, _testUser, "127.0.0.1", "TestAgent");
        result2.Should().BeOfType<PhotoServiceResult.Success<Contracts.ProfilePhotoResponse>>();
        var res2Data = ((PhotoServiceResult.Success<Contracts.ProfilePhotoResponse>)result2).Data;
        res2Data.Version.Should().Be(2);

        // Verify Database state
        var activePhotos = await _dbContext.EmployeeProfilePhotos
            .Where(p => p.EmployeeID == "EMP200" && p.IsActive)
            .ToListAsync();

        activePhotos.Count.Should().Be(1);
        activePhotos.First().Version.Should().Be(2);

        var allPhotos = await _dbContext.EmployeeProfilePhotos
            .Where(p => p.EmployeeID == "EMP200")
            .ToListAsync();

        allPhotos.Count.Should().Be(2);
        allPhotos.Single(p => p.Version == 1).IsActive.Should().BeFalse();
    }

    private static IFormFile CreateDummyFormFile(string fileName)
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var ms = new MemoryStream(bytes);
        return new FormFile(ms, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }
}
