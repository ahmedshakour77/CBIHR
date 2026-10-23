using API.HR.Node.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class EmployeeApiRegressionTests
{
    [Fact]
    public async Task EmployeeEntity_CanSaveAndQueryWithProfilePhotoRelations()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new ApplicationDbContext(dbOptions);

        var employee = new Employee
        {
            EmployeeID = "REG100",
            Name = "Regression Employee"
        };
        dbContext.Employees.Add(employee);

        var photo = new EmployeeProfilePhoto
        {
            EmployeeID = "REG100",
            StorageKey = "key1",
            ThumbnailStorageKey = "key1_thumb",
            OriginalFileName = "test.jpg",
            ContentType = "image/jpeg",
            FileSize = 500,
            Width = 100,
            Height = 100,
            Sha256 = "hash100",
            Version = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "Tester"
        };
        dbContext.EmployeeProfilePhotos.Add(photo);

        await dbContext.SaveChangesAsync();

        var queriedEmployee = await dbContext.Employees.SingleOrDefaultAsync(e => e.EmployeeID == "REG100");
        queriedEmployee.Should().NotBeNull();
        queriedEmployee!.Name.Should().Be("Regression Employee");

        var hasPhoto = await dbContext.EmployeeProfilePhotos.AnyAsync(p => p.EmployeeID == "REG100" && p.IsActive);
        hasPhoto.Should().BeTrue();
    }

    [Fact]
    public async Task ContactLookup_ResolvesEmployeeByEmailAndPhone()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new ApplicationDbContext(dbOptions);

        var employee = new Employee
        {
            EmployeeID = "EMP_CONTACT_1",
            Name = "John Doe"
        };
        dbContext.Employees.Add(employee);

        dbContext.EmployeeEmails.Add(new EmployeeEmail
        {
            EmployeeID = "EMP_CONTACT_1",
            Email = "john.doe@company.com",
            IsPrimary = true
        });

        dbContext.EmployeePhones.Add(new EmployeePhone
        {
            EmployeeID = "EMP_CONTACT_1",
            PhoneNumber = "+19995551234",
            IsPrimary = true
        });

        await dbContext.SaveChangesAsync();

        var matchedByEmail = await dbContext.EmployeeEmails
            .Where(e => e.Email.ToLower() == "JOHN.DOE@COMPANY.COM".ToLower())
            .Select(e => e.EmployeeID)
            .FirstOrDefaultAsync();

        var matchedByPhone = await dbContext.EmployeePhones
            .Where(p => p.PhoneNumber == "+19995551234")
            .Select(p => p.EmployeeID)
            .FirstOrDefaultAsync();

        matchedByEmail.Should().Be("EMP_CONTACT_1");
        matchedByPhone.Should().Be("EMP_CONTACT_1");
    }
}
