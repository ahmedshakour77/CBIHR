using System.IO;
using System.Text;
using API.HR.Node.Options;
using API.HR.Node.Services.Images;
using API.HR.Node.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class UploadValidationTests
{
    private readonly ImageSharpProcessor _processor;
    private readonly ProfilePhotoOptions _options;

    public UploadValidationTests()
    {
        _options = new ProfilePhotoOptions
        {
            MaxFileSizeBytes = 5 * 1024 * 1024,
            MinDimension = 50,
            MaxDimension = 4096,
            MaxPixelCount = 25_000_000,
            AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"],
            AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"]
        };

        var optionsMock = new Mock<IOptions<ProfilePhotoOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);

        var loggerMock = new Mock<ILogger<ImageSharpProcessor>>();
        _processor = new ImageSharpProcessor(optionsMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task ValidateImage_ValidJpeg_ShouldSucceed()
    {
        using var stream = CreateSampleImageStream("jpeg", 200, 200);

        var result = await _processor.ValidateImageAsync(stream, "photo.jpg", "image/jpeg", stream.Length);

        result.IsValid.Should().BeTrue();
        result.DetectedContentType.Should().Be("image/jpeg");
        result.Width.Should().Be(200);
        result.Height.Should().Be(200);
    }

    [Fact]
    public async Task ValidateImage_ValidPng_ShouldSucceed()
    {
        using var stream = CreateSampleImageStream("png", 150, 150);

        var result = await _processor.ValidateImageAsync(stream, "photo.png", "image/png", stream.Length);

        result.IsValid.Should().BeTrue();
        result.DetectedContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task ValidateImage_ValidWebp_ShouldSucceed()
    {
        using var stream = CreateSampleImageStream("webp", 180, 180);

        var result = await _processor.ValidateImageAsync(stream, "photo.webp", "image/webp", stream.Length);

        result.IsValid.Should().BeTrue();
        result.DetectedContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task ValidateImage_OversizedFile_ShouldFail()
    {
        using var stream = new MemoryStream(new byte[100]);

        var result = await _processor.ValidateImageAsync(stream, "big.jpg", "image/jpeg", _options.MaxFileSizeBytes + 1);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum allowed limit");
    }

    [Fact]
    public async Task ValidateImage_InvalidExtension_ShouldFail()
    {
        using var stream = new MemoryStream(new byte[100]);

        var result = await _processor.ValidateImageAsync(stream, "malicious.exe", "image/jpeg", 100);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File extension '.exe' is not supported");
    }

    [Fact]
    public async Task ValidateImage_FakeExtensionWithExeContent_ShouldFailMagicBytesCheck()
    {
        // Renamed exe to photo.jpg
        var fakeExeBytes = Encoding.UTF8.GetBytes("MZ fake executable header content");
        using var stream = new MemoryStream(fakeExeBytes);

        var result = await _processor.ValidateImageAsync(stream, "photo.jpg", "image/jpeg", stream.Length);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not match a valid image format");
    }

    [Fact]
    public async Task ValidateImage_CorruptedFile_ShouldFailDecoding()
    {
        // Valid JPEG header but broken body
        var corruptBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(corruptBytes);

        var result = await _processor.ValidateImageAsync(stream, "corrupt.jpg", "image/jpeg", stream.Length);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("corrupted");
    }

    [Fact]
    public async Task ValidateImage_DimensionsTooSmall_ShouldFail()
    {
        using var stream = CreateSampleImageStream("jpeg", 20, 20); // Less than 50x50

        var result = await _processor.ValidateImageAsync(stream, "tiny.jpg", "image/jpeg", stream.Length);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("smaller than minimum allowed threshold");
    }

    [Fact]
    public async Task MalwareScanner_NullScanner_ShouldReturnCleanInDevAndFailInProduction()
    {
        var loggerMock = new Mock<ILogger<NullFileMalwareScanner>>();
        var devEnvMock = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        devEnvMock.Setup(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Development);

        var devScanner = new NullFileMalwareScanner(devEnvMock.Object, loggerMock.Object);

        using var stream = new MemoryStream(new byte[50]);
        var devResult = await devScanner.ScanAsync(stream, "test.jpg");

        devResult.Status.Should().Be(MalwareScanStatus.Clean);
        devResult.EngineName.Should().Be("NullScannerPlaceholderDevMode");

        var prodEnvMock = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        prodEnvMock.Setup(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Production);

        var prodScanner = new NullFileMalwareScanner(prodEnvMock.Object, loggerMock.Object);
        var prodResult = await prodScanner.ScanAsync(stream, "test.jpg");

        prodResult.Status.Should().Be(MalwareScanStatus.Failed);
        prodResult.IsRejected().Should().BeTrue();
    }

    private static MemoryStream CreateSampleImageStream(string format, int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var ms = new MemoryStream();

        switch (format.ToLowerInvariant())
        {
            case "png":
                image.SaveAsPng(ms);
                break;
            case "webp":
                image.SaveAsWebp(ms);
                break;
            default:
                image.SaveAsJpeg(ms);
                break;
        }

        ms.Position = 0;
        return ms;
    }
}
