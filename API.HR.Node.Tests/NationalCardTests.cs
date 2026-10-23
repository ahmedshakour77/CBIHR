using System.Text;
using API.HR.Node.Data;
using API.HR.Node.Services.IdentityDocuments;
using API.HR.Node.Services.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class NationalCardTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class InMemoryFileStorage : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _storage = new();

        public Task<string> SaveAsync(Stream stream, string storageKey, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _storage[storageKey] = ms.ToArray();
            return Task.FromResult(storageKey);
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (_storage.TryGetValue(storageKey, out var data))
            {
                return Task.FromResult<Stream?>(new MemoryStream(data));
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_storage.ContainsKey(storageKey));
        }

        public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var removed = _storage.Remove(storageKey);
            return Task.FromResult(removed);
        }
    }

    [Fact]
    public void NationalCardService_ParseMrz_ExtractsAllFieldsCorrectlyForTD1()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new InMemoryFileStorage();
        var logger = NullLogger<NationalCardService>.Instance;
        var service = new NationalCardService(dbContext, storage, logger);

        // 3 lines of 30 characters for Iraqi National Card TD1
        // Line 1: "I<IRQ1234567897<<<<<<<<<<<<<<<"
        // Line 2: "9005152M3005158IRQ199012345678"
        // Line 3: "AL<<ALI<<<<<<<<<<<<<<<<<<<<<<<<"
        var mrzText = "I<IRQ1234567897<<<<<<<<<<<<<<<\n9005152M3005158IRQ199012345678\nAL<<ALI<<<<<<<<<<<<<<<<<<<<<<<<";

        var result = service.ParseMrz(mrzText);

        result.IsValid.Should().BeTrue();
        result.DocumentType.Should().Be("I");
        result.IssuingCountry.Should().Be("IRQ");
        result.DocumentNumber.Should().Be("123456789");
        result.Nationality.Should().Be("IRQ");
        result.Gender.Should().Be("ذكر");
        result.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
        result.ExpiryDate.Should().Be(new DateOnly(2030, 5, 15));
        result.Surname.Should().Be("AL");
        result.GivenNames.Should().Be("ALI");
        result.FullNameEn.Should().Be("ALI AL");
        result.NationalIdNumber.Should().Be("199012345678");
    }

    [Fact]
    public async Task NationalCardService_RegistersCard_AndSyncsEmployeeData()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new InMemoryFileStorage();
        var logger = NullLogger<NationalCardService>.Instance;
        var service = new NationalCardService(dbContext, storage, logger);

        var employee = new Employee
        {
            Id = 1,
            EmployeeID = "EMP-001",
            Name = "علي حسن كاظم",
            FirstName = "علي",
            SecondName = "حسن",
            ThirdName = "كاظم"
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var dummyFaceBytes = Encoding.UTF8.GetBytes("fake_face_dg2_image_bytes");
        var base64Face = Convert.ToBase64String(dummyFaceBytes);

        var request = new NationalCardRegisterRequest
        {
            DocumentNumber = "001234567",
            NationalIdNumber = "199012345678",
            FullNameAr = "علي حسن كاظم",
            FirstNameEn = "ALI",
            LastNameEn = "HASSAN",
            DateOfBirth = new DateOnly(1990, 5, 15),
            Gender = "ذكر",
            Nationality = "IRQ",
            IssuingState = "IRQ",
            IssueDate = new DateOnly(2020, 1, 1),
            ExpiryDate = new DateOnly(2030, 1, 1),
            IsPassiveAuthenticationValid = true,
            SodHash = "SHA256:4b227777d4dd1fc61c6f884f48641d02b4d121d3fd328cb08b5531fcacdabf8a",
            CertificateIssuer = "Country Signing CA - Republic of Iraq",
            BiometricFaceBase64 = base64Face
        };

        var response = await service.RegisterOrUpdateAsync(1, request, "test-user");

        response.Should().NotBeNull();
        response.DocumentNumber.Should().Be("001234567");
        response.NationalIdNumber.Should().Be("199012345678");
        response.IsPassiveAuthenticationValid.Should().BeTrue();
        response.HasBiometricPhoto.Should().BeTrue();
        response.BiometricPhotoUrl.Should().Be($"/api/national-cards/{response.Id}/biometric-photo");

        // Verify Employee table was updated with NationalId and BirthDate
        var updatedEmployee = await dbContext.Employees.FindAsync(1);
        updatedEmployee.Should().NotBeNull();
        updatedEmployee!.NationalId.Should().Be("199012345678");
        updatedEmployee.BirthDate.Should().Be(new DateOnly(1990, 5, 15));

        // Search by Document Number
        var searchByDoc = await service.GetByDocumentOrNationalIdAsync("001234567");
        searchByDoc.Should().NotBeNull();
        searchByDoc!.EmployeeId.Should().Be(1);

        // Search by National ID Number
        var searchByNid = await service.GetByDocumentOrNationalIdAsync("199012345678");
        searchByNid.Should().NotBeNull();
        searchByNid!.EmployeeId.Should().Be(1);
    }

    [Fact]
    public async Task NationalCardService_RegistersCard_FromFlutterMobilePayload()
    {
        using var dbContext = CreateInMemoryDbContext();
        var storage = new InMemoryFileStorage();
        var logger = NullLogger<NationalCardService>.Instance;
        var service = new NationalCardService(dbContext, storage, logger);

        var employee = new Employee
        {
            Id = 2,
            EmployeeID = "EMP-002",
            Name = "احمد عبد الشكور محمود الجبوري"
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        // Exact JSON structure sent by the Flutter mobile client
        var flutterRequest = new NationalCardRegisterRequest
        {
            DocumentNumber = "A60275642",
            DocumentType = "ID",
            NationalNumber = "199285057919",
            PersonalNumber = "199285057919",
            FullNameArabic = "احمد عبد الشكور محمود الجبوري",
            FullNameEnglish = "AXHMD ALJBWRY",
            Surname = "ALJBWRY",
            GivenNames = "AXHMD",
            BirthDate = "1992-07-07",
            ExpiryDate = new DateOnly(2032, 9, 6),
            IssueDate = new DateOnly(2022, 9, 7),
            RawMrz = "IDIRQA602756424199285057919<<<9207075M3209066IRQ<<<<<<<<<<<5ALJBWRY<<AXHMD<<<<<<<<<<<<<<<<",
            IsBacAuthenticated = true,
            IsSodHashVerified = true,
            FacePhotoBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake_face_image_bytes"))
        };

        var response = await service.RegisterOrUpdateAsync(2, flutterRequest, "FlutterApp");

        response.Should().NotBeNull();
        response.DocumentNumber.Should().Be("A60275642");
        response.NationalIdNumber.Should().Be("199285057919");
        response.FullNameAr.Should().Be("احمد عبد الشكور محمود الجبوري");
        response.FullNameEn.Should().Be("AXHMD ALJBWRY");
        response.DateOfBirth.Should().Be(new DateOnly(1992, 7, 7));
        response.IsPassiveAuthenticationValid.Should().BeTrue();
        response.HasBiometricPhoto.Should().BeTrue();
    }
}
