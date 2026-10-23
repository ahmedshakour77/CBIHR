using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Options;
using API.HR.Node.Security;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class SecurityHardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hr-storage-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside;

    public SecurityHardeningTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "store"));
        _outside = Path.Combine(_root, "secret.txt");
        File.WriteAllText(_outside, "not yours");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private LocalFileStorageService CreateStorage(string storageRoot)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_root);
        var options = Microsoft.Extensions.Options.Options.Create(new ProfilePhotoOptions { StorageRoot = storageRoot });
        return new LocalFileStorageService(options, env.Object, NullLogger<LocalFileStorageService>.Instance);
    }

    [Fact]
    public async Task Delete_WithTraversalKey_IsRefusedAndLeavesFileAlone()
    {
        var storage = CreateStorage("store");

        var act = () => storage.DeleteAsync("../secret.txt");

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(_outside).Should().BeTrue();
    }

    [Fact]
    public async Task Exists_WithTraversalKey_IsRefused()
    {
        var storage = CreateStorage("store");

        var act = () => storage.ExistsAsync("../secret.txt");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Read_FromSiblingFolderSharingThePrefix_IsRefused()
    {
        // "store2" starts with "store": a prefix check without a trailing separator let this through.
        Directory.CreateDirectory(Path.Combine(_root, "store2"));
        File.WriteAllText(Path.Combine(_root, "store2", "x.txt"), "sibling");
        var storage = CreateStorage("store");

        var act = () => storage.OpenReadAsync("../store2/x.txt");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveThenRead_InsideRoot_RoundTrips()
    {
        var storage = CreateStorage("store");
        await using var input = new MemoryStream("hello"u8.ToArray());

        var key = await storage.SaveAsync(input, "employees/1/a.bin");
        await using var read = await storage.OpenReadAsync(key);

        read.Should().NotBeNull();
        (await storage.ExistsAsync(key)).Should().BeTrue();
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data?maps.app.goo.gl")]
    [InlineData("https://intranet.local/goo.gl/maps/abc")]
    [InlineData("http://maps.app.goo.gl/abc")]
    [InlineData("https://evil.example/?q=maps.app.goo.gl")]
    public async Task ShortLinkResolver_NeverFetchesNonGoogleHosts(string url)
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);

        var result = await EmployeeResidence.ResolveUrlAndCoordinatesAsync(url, client);

        result.Should().BeNull();
        handler.Requests.Should().Be(0);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("HRManager", true)]
    [InlineData("FormationManager", true)]
    [InlineData("Supervisor", true)]
    [InlineData("Employee", false)]
    public void StaffRoles_MatchTheFourStaffRoles(string role, bool expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        StaffRoles.IsStaff(principal).Should().Be(expected);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
