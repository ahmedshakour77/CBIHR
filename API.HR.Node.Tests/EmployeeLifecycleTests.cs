using API.HR.Node.Data;
using API.HR.Node.Services.Employees;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class EmployeeLifecycleTests
{
    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApplicationDbContext(_options));
    }

    [Fact]
    public async Task QueryFilter_AutomaticallyExcludesArchivedEmployeesFromNormalQueriesAndReports()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            db.Employees.AddRange(
                new Employee { EmployeeID = "EMP_ACTIVE", Name = "Active Staff", IsArchived = false },
                new Employee { EmployeeID = "EMP_ARCHIVED", Name = "Archived Staff", IsArchived = true, ArchiveReason = "إجازة بدون راتب" }
            );
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            // Normal query (simulating reports, dashboard counts, directory)
            var activeCount = await db.Employees.CountAsync();
            var activeList = await db.Employees.ToListAsync();

            activeCount.Should().Be(1);
            activeList.Should().ContainSingle(e => e.EmployeeID == "EMP_ACTIVE");
            activeList.Should().NotContain(e => e.EmployeeID == "EMP_ARCHIVED");

            // Query with IgnoreQueryFilters (simulating archive view)
            var totalCount = await db.Employees.IgnoreQueryFilters().CountAsync();
            var archivedList = await db.Employees.IgnoreQueryFilters().Where(e => e.IsArchived).ToListAsync();

            totalCount.Should().Be(2);
            archivedList.Should().ContainSingle(e => e.EmployeeID == "EMP_ARCHIVED");
        }
    }

    [Fact]
    public async Task ArchiveAndRestore_UpdatesLifecycleStateCorrectly()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(dbOptions);
        var mockStorage = new Mock<IFileStorageService>();
        var service = new EmployeeLifecycleService(factory, mockStorage.Object, NullLogger<EmployeeLifecycleService>.Instance);

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            db.Employees.Add(new Employee { EmployeeID = "EMP_10", Name = "Ahmed Ali", IsArchived = false });
            await db.SaveChangesAsync();
        }

        // 1. Archive
        var archiveResult = await service.ArchiveEmployeeAsync("EMP_10", "إعارة خارجية", "AdminUser");
        archiveResult.Succeeded.Should().BeTrue();

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            // Report count is now 0 because employee is archived
            var reportCount = await db.Employees.CountAsync();
            reportCount.Should().Be(0);

            var archived = await db.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EmployeeID == "EMP_10");
            archived.Should().NotBeNull();
            archived!.IsArchived.Should().BeTrue();
            archived.ArchiveReason.Should().Be("إعارة خارجية");
            archived.ArchivedBy.Should().Be("AdminUser");
            archived.ArchivedAt.Should().NotBeNull();
        }

        // Check archived count and list from service
        var archivedCount = await service.GetArchivedCountAsync();
        archivedCount.Should().Be(1);

        var archivedEmployees = await service.GetArchivedEmployeesAsync();
        archivedEmployees.Should().ContainSingle(e => e.EmployeeID == "EMP_10" && e.ArchiveReason == "إعارة خارجية");

        // 2. Restore
        var restoreResult = await service.RestoreEmployeeAsync("EMP_10", "AdminUser");
        restoreResult.Succeeded.Should().BeTrue();

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            // Immediately visible in all reports again
            var reportCount = await db.Employees.CountAsync();
            reportCount.Should().Be(1);

            var restored = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == "EMP_10");
            restored.Should().NotBeNull();
            restored!.IsArchived.Should().BeFalse();
            restored.ArchiveReason.Should().BeNull();
            restored.ArchivedBy.Should().BeNull();
            restored.ArchivedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task DeletePermanently_CompletelyRemovesEmployeeAndAllAssociatedData()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(dbOptions);
        var mockStorage = new Mock<IFileStorageService>();
        mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new EmployeeLifecycleService(factory, mockStorage.Object, NullLogger<EmployeeLifecycleService>.Instance);

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            var emp = new Employee { Id = 1, EmployeeID = "EMP_DEL", Name = "ToDelete Staff" };
            db.Employees.Add(emp);

            db.EmployeeProfilePhotos.Add(new EmployeeProfilePhoto
            {
                EmployeeID = "EMP_DEL",
                StorageKey = "skey1",
                ThumbnailStorageKey = "skey1_thumb",
                OriginalFileName = "photo.jpg",
                ContentType = "image/jpeg",
                FileSize = 100,
                Width = 50,
                Height = 50,
                Sha256 = "hash",
                Version = 1,
                IsActive = true,
                CreatedBy = "Tester"
            });

            db.EmployeePhones.Add(new EmployeePhone { EmployeeID = "EMP_DEL", PhoneNumber = "07700000000" });
            db.EmployeeEmails.Add(new EmployeeEmail { EmployeeID = "EMP_DEL", Email = "del@example.com" });
            db.EmployeeLeaves.Add(new EmployeeLeave { EmployeeId = 1, DaysCount = 5, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 1, 5) });
            db.EmployeePenalties.Add(new EmployeePenalty { EmployeeId = 1, PenaltyDate = new DateOnly(2026, 2, 1) });
            db.EmployeeFinancialAccounts.Add(new Data.Financial.EmployeeFinancialAccount { EmployeeId = 1 });

            await db.SaveChangesAsync();
        }

        // Hard Delete
        var deleteResult = await service.DeletePermanentlyAsync("EMP_DEL", "SuperAdmin");
        deleteResult.Succeeded.Should().BeTrue();

        await using (var db = new ApplicationDbContext(dbOptions))
        {
            var emp = await db.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EmployeeID == "EMP_DEL");
            emp.Should().BeNull();

            var photos = await db.EmployeeProfilePhotos.Where(p => p.EmployeeID == "EMP_DEL").ToListAsync();
            photos.Should().BeEmpty();

            var phones = await db.EmployeePhones.Where(p => p.EmployeeID == "EMP_DEL").ToListAsync();
            phones.Should().BeEmpty();

            var emails = await db.EmployeeEmails.Where(e => e.EmployeeID == "EMP_DEL").ToListAsync();
            emails.Should().BeEmpty();

            var leaves = await db.EmployeeLeaves.Where(l => l.EmployeeId == 1).ToListAsync();
            leaves.Should().BeEmpty();

            var penalties = await db.EmployeePenalties.Where(p => p.EmployeeId == 1).ToListAsync();
            penalties.Should().BeEmpty();

            var finAcc = await db.EmployeeFinancialAccounts.FirstOrDefaultAsync(a => a.EmployeeId == 1);
            finAcc.Should().BeNull();
        }

        mockStorage.Verify(s => s.DeleteAsync("skey1", It.IsAny<CancellationToken>()), Times.Once);
        mockStorage.Verify(s => s.DeleteAsync("skey1_thumb", It.IsAny<CancellationToken>()), Times.Once);
    }
}
