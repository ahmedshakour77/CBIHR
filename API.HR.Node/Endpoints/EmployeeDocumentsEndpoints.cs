using System.Security.Claims;
using System.Text.RegularExpressions;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.IdentityDocuments;
using API.HR.Node.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

#region DTOs

public sealed record RegisterPassportRequest(
    string? EmployeeID,
    string PassportNumber,
    bool IsElectronic,
    string? PassportType,
    string? CountryCode,
    string? IssuingCountry,
    string? IssuingAuthority,
    string? NationalIdNumber,
    string? FullNameAr,
    string? FullNameEn,
    string? MotherName,
    string? PlaceOfBirth,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string? RawMrz,
    string? PassportImageBase64,
    string? BiometricPhotoBase64,
    string? Notes);

public sealed record RegisterResidenceRequest(
    string? EmployeeID,
    string? FormNumber,
    DateOnly? FormDate,
    string? InformationOffice,
    string? FamilyHeadName,
    int? GovernorateId,
    string? GovernorateName,
    string? District,
    string? Mahalla,
    string? Zuqaq,
    string? Dar,
    string? HousingType,
    string? ComplexName,
    string? BuildingNumber,
    string? FloorNumber,
    string? ApartmentNumber,
    double? Latitude,
    double? Longitude,
    string? LocationUrl,
    string? CardFrontBase64,
    string? CardBackBase64);

public sealed record RegisterNationalCardRequest(
    string? EmployeeID,
    string DocumentNumber,
    string? NationalIdNumber,
    string? FullNameAr,
    string? FirstNameEn,
    string? LastNameEn,
    string? FullNameEn,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? IssuingState,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string? RawMrz,
    string? BiometricFaceBase64,
    string? FrontImageBase64,
    string? BackImageBase64,
    bool IsPassiveAuthenticationValid);

public sealed record DocumentSummaryDto(
    string EmployeeID,
    string EmployeeName,
    bool HasNationalCard,
    bool HasResidence,
    bool HasPassport,
    bool AllDocumentsComplete,
    object? NationalCard,
    object? Residence,
    object? Passport);

#endregion

public static class EmployeeDocumentsEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/documents")
            .WithTags("Employee Official Documents")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // 1. Get all documents for current authenticated employee
        group.MapGet("/me", GetMyDocuments)
            .WithName("GetMyDocuments")
            .WithSummary("استرجاع كافة المستمسكات الرسمية الثلاثة للموظف الحالي (البطاقة الموحدة، بطاقة السكن، جواز السفر).");

        // 2. Get all documents for a specific employee (Admin / Staff)
        group.MapGet("/{employeeID}", GetEmployeeDocuments)
            .WithName("GetEmployeeDocuments")
            .WithSummary("استرجاع كافة المستمسكات الرسمية لموظف محدد (خاص بالمسؤولين والمدراء).")
            .RequireAuthorization(AuthorizationPolicies.Staff);

        // 3. Register / Update Passport (الجواز: إلكتروني أو عادي)
        group.MapPost("/passport", SavePassport)
            .WithName("SaveEmployeePassport")
            .WithSummary("تسجيل أو تحديث جواز السفر للموظف (مع تحديد نوع الجواز: إلكتروني بشريحة أو عادي).");

        // 4. Register / Update Residence Card (بطاقة السكن: دائماً عادي ورقي)
        group.MapPost("/residence", SaveResidence)
            .WithName("SaveEmployeeResidence")
            .WithSummary("تسجيل أو تحديث بطاقة السكن للموظف (دائماً عادية ورقية).");

        // 5. Register / Update Unified National Card (البطاقة الوطنية: دائماً إلكترونية)
        group.MapPost("/national-card", SaveNationalCard)
            .WithName("SaveEmployeeNationalCard")
            .WithSummary("تسجيل أو تحديث البطاقة الوطنية الموحدة (دائماً إلكترونية ذات شريحة).");

        return endpoints;
    }

    private static async Task<IResult> GetMyDocuments(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var empId = await GetCurrentEmployeeIdAsync(principal, userManager);
        if (string.IsNullOrWhiteSpace(empId))
        {
            return Results.Json(new { message = "هذا الحساب غير مرتبط بسجل موظف." }, statusCode: StatusCodes.Status403Forbidden);
        }

        return await BuildDocumentsResponseAsync(empId, db, ct);
    }

    private static async Task<IResult> GetEmployeeDocuments(
        string employeeID,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        return await BuildDocumentsResponseAsync(employeeID, db, ct);
    }

    private static async Task<IResult> BuildDocumentsResponseAsync(
        string employeeID,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var emp = await db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeID, ct);

        if (emp is null)
        {
            return Results.NotFound(new { message = $"لم يتم العثور على سجل للموظف بالرقم: {employeeID}" });
        }

        // 1. Unified National Card (Always Electronic)
        var nationalCardEntity = await db.EmployeeNationalCards
            .AsNoTracking()
            .Where(c => c.EmployeeId == emp.Id && c.IsActive)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        object? nationalCardDto = null;
        if (nationalCardEntity is not null)
        {
            nationalCardDto = new
            {
                id = nationalCardEntity.Id,
                documentType = "البطاقة الوطنية الموحدة",
                isElectronic = true,
                documentFormat = "إلكتروني (شريحة ذكية)",
                documentNumber = nationalCardEntity.DocumentNumber,
                nationalIdNumber = nationalCardEntity.NationalIdNumber,
                fullNameAr = nationalCardEntity.FullNameAr,
                firstNameEn = nationalCardEntity.FirstNameEn,
                lastNameEn = nationalCardEntity.LastNameEn,
                fullNameEn = nationalCardEntity.FullNameEn,
                dateOfBirth = nationalCardEntity.DateOfBirth,
                gender = nationalCardEntity.Gender,
                nationality = nationalCardEntity.Nationality,
                issuingState = nationalCardEntity.IssuingState,
                issueDate = nationalCardEntity.IssueDate,
                expiryDate = nationalCardEntity.ExpiryDate,
                rawMrz = nationalCardEntity.RawMrz,
                hasFrontImage = !string.IsNullOrWhiteSpace(nationalCardEntity.FrontImagePath),
                frontImageUrl = !string.IsNullOrWhiteSpace(nationalCardEntity.FrontImagePath) ? $"/api/national-cards/{nationalCardEntity.Id}/front-image" : null,
                hasBackImage = !string.IsNullOrWhiteSpace(nationalCardEntity.BackImagePath),
                backImageUrl = !string.IsNullOrWhiteSpace(nationalCardEntity.BackImagePath) ? $"/api/national-cards/{nationalCardEntity.Id}/back-image" : null,
                hasBiometricPhoto = !string.IsNullOrWhiteSpace(nationalCardEntity.BiometricPhotoPath),
                biometricPhotoUrl = !string.IsNullOrWhiteSpace(nationalCardEntity.BiometricPhotoPath) ? $"/api/national-cards/{nationalCardEntity.Id}/biometric-photo" : null,
                isPassiveAuthenticationValid = nationalCardEntity.IsPassiveAuthenticationValid,
                verificationTimestamp = nationalCardEntity.VerificationTimestamp
            };
        }

        // 2. Residence Card (Always Standard / Paper)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var residenceEntity = await db.EmployeeResidences
            .AsNoTracking()
            .Include(r => r.Governorate)
            .Where(r => r.EmployeeId == emp.Id && (r.ToDate == null || r.ToDate >= today))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct)
            ?? await db.EmployeeResidences
                .AsNoTracking()
                .Include(r => r.Governorate)
                .Where(r => r.EmployeeId == emp.Id)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(ct);

        object? residenceDto = null;
        if (residenceEntity is not null)
        {
            residenceDto = new
            {
                id = residenceEntity.Id,
                documentType = "بطاقة السكن",
                isElectronic = false,
                documentFormat = "عادي (ورقي)",
                formNumber = residenceEntity.FormNumber,
                formDate = residenceEntity.FormDate,
                informationOffice = residenceEntity.InformationOffice,
                familyHeadName = residenceEntity.FamilyHeadName,
                governorateId = residenceEntity.GovernorateId,
                governorate = residenceEntity.Governorate?.Name,
                district = residenceEntity.District,
                mahalla = residenceEntity.Mahalla,
                zuqaq = residenceEntity.Zuqaq,
                dar = residenceEntity.Dar,
                housingType = residenceEntity.HousingType,
                complexName = residenceEntity.ComplexName,
                buildingNumber = residenceEntity.BuildingNumber,
                floorNumber = residenceEntity.FloorNumber,
                apartmentNumber = residenceEntity.ApartmentNumber,
                latitude = residenceEntity.Latitude,
                longitude = residenceEntity.Longitude,
                locationUrl = residenceEntity.LocationUrl,
                hasCardFront = !string.IsNullOrWhiteSpace(residenceEntity.CardFrontKey),
                cardFrontUrl = !string.IsNullOrWhiteSpace(residenceEntity.CardFrontKey) ? $"/api/residence/{residenceEntity.Id}/card/front" : null,
                hasCardBack = !string.IsNullOrWhiteSpace(residenceEntity.CardBackKey),
                cardBackUrl = !string.IsNullOrWhiteSpace(residenceEntity.CardBackKey) ? $"/api/residence/{residenceEntity.Id}/card/back" : null
            };
        }

        // 3. Passport (Electronic or Standard)
        var passportEntity = await db.EmployeePassports
            .AsNoTracking()
            .Where(p => p.EmployeeId == emp.Id && p.IsActive)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        object? passportDto = null;
        if (passportEntity is not null)
        {
            passportDto = new
            {
                id = passportEntity.Id,
                documentType = "جواز السفر",
                isElectronic = passportEntity.IsElectronic,
                passportCategory = passportEntity.PassportCategory, // "إلكتروني" أو "عادي"
                passportType = passportEntity.PassportType,       // عادي (P)، دبلوماسي (D)، خدمة (S)، خاص (Sp)
                passportNumber = passportEntity.PassportNumber,
                countryCode = passportEntity.CountryCode,
                issuingCountry = passportEntity.IssuingCountry,
                issuingAuthority = passportEntity.IssuingAuthority,
                nationalIdNumber = passportEntity.NationalIdNumber,
                fullNameAr = passportEntity.FullNameAr,
                fullNameEn = passportEntity.FullNameEn,
                motherName = passportEntity.MotherName,
                placeOfBirth = passportEntity.PlaceOfBirth,
                dateOfBirth = passportEntity.DateOfBirth,
                gender = passportEntity.Gender,
                issueDate = passportEntity.IssueDate,
                expiryDate = passportEntity.ExpiryDate,
                rawMrz = passportEntity.RawMrz,
                hasImage = !string.IsNullOrWhiteSpace(passportEntity.PassportImageKey),
                imageUrl = !string.IsNullOrWhiteSpace(passportEntity.PassportImageKey) ? $"/api/passport/{passportEntity.Id}/image" : null,
                hasBioPhoto = !string.IsNullOrWhiteSpace(passportEntity.BiometricPhotoKey),
                biometricPhotoUrl = !string.IsNullOrWhiteSpace(passportEntity.BiometricPhotoKey) ? $"/api/passport/{passportEntity.Id}/photo" : null,
                notes = passportEntity.Notes
            };
        }

        var hasNat = nationalCardDto is not null;
        var hasRes = residenceDto is not null;
        var hasPass = passportDto is not null;

        var result = new DocumentSummaryDto(
            emp.EmployeeID,
            emp.Name,
            hasNat,
            hasRes,
            hasPass,
            hasNat && hasRes && hasPass,
            nationalCardDto,
            residenceDto,
            passportDto);

        return Results.Ok(result);
    }

    private static async Task<IResult> SavePassport(
        [FromBody] RegisterPassportRequest req,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IFileStorageService storage,
        CancellationToken ct)
    {
        var targetEmpId = await ResolveTargetEmployeeIdAsync(req.EmployeeID, principal, userManager);
        if (string.IsNullOrWhiteSpace(targetEmpId))
        {
            return Results.BadRequest(new { message = "الرقم الوظيفي للموظف مطلوب أو غير صالح." });
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == targetEmpId, ct);
        if (employee is null)
        {
            return Results.NotFound(new { message = $"سجل الموظف غير موجود بالنظام ({targetEmpId})." });
        }

        if (string.IsNullOrWhiteSpace(req.PassportNumber))
        {
            return Results.BadRequest(new { message = "رقم جواز السفر حقل إلزامي." });
        }

        // Look for existing active passport for this employee
        var passport = await db.EmployeePassports
            .FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.IsActive, ct);

        if (passport is null)
        {
            passport = new EmployeePassport
            {
                EmployeeId = employee.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.EmployeePassports.Add(passport);
        }

        passport.PassportNumber = req.PassportNumber.Trim().ToUpperInvariant();
        passport.IsElectronic = req.IsElectronic;
        passport.PassportType = string.IsNullOrWhiteSpace(req.PassportType) ? "عادي (P)" : req.PassportType.Trim();
        passport.CountryCode = string.IsNullOrWhiteSpace(req.CountryCode) ? "IRQ" : req.CountryCode.Trim().ToUpperInvariant();
        passport.IssuingCountry = string.IsNullOrWhiteSpace(req.IssuingCountry) ? "العراق" : req.IssuingCountry.Trim();
        passport.IssuingAuthority = req.IssuingAuthority?.Trim();
        passport.NationalIdNumber = req.NationalIdNumber?.Trim();
        passport.FullNameAr = req.FullNameAr?.Trim() ?? employee.Name;
        passport.FullNameEn = req.FullNameEn?.Trim();
        passport.MotherName = req.MotherName?.Trim() ?? employee.MotherName;
        passport.PlaceOfBirth = req.PlaceOfBirth?.Trim();
        passport.DateOfBirth = req.DateOfBirth ?? employee.BirthDate;
        passport.Gender = req.Gender?.Trim() ?? (employee.GenderId.HasValue ? "ذكر" : null);
        passport.IssueDate = req.IssueDate;
        passport.ExpiryDate = req.ExpiryDate;
        passport.RawMrz = req.RawMrz?.Trim();
        passport.Notes = req.Notes?.Trim();

        // Save Bio Page Image if provided
        if (!string.IsNullOrWhiteSpace(req.PassportImageBase64))
        {
            var key = $"passports/{employee.EmployeeID}/passport_{passport.PassportNumber}_{DateTime.UtcNow.Ticks}.jpg";
            passport.PassportImageKey = await SaveBase64ImageAsync(req.PassportImageBase64, key, storage, ct);
            passport.PassportImageFileName = $"passport_{passport.PassportNumber}.jpg";
            passport.PassportImageContentType = "image/jpeg";
        }

        // Save Biometric Chip Photo if provided
        if (!string.IsNullOrWhiteSpace(req.BiometricPhotoBase64))
        {
            var key = $"passports/{employee.EmployeeID}/bio_{passport.PassportNumber}_{DateTime.UtcNow.Ticks}.jpg";
            passport.BiometricPhotoKey = await SaveBase64ImageAsync(req.BiometricPhotoBase64, key, storage, ct);
            passport.BiometricPhotoFileName = $"passport_bio_{passport.PassportNumber}.jpg";
            passport.BiometricPhotoContentType = "image/jpeg";
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            message = "تم حفظ وتحديث بيانات جواز السفر بنجاح.",
            passportId = passport.Id,
            passportNumber = passport.PassportNumber,
            isElectronic = passport.IsElectronic,
            passportCategory = passport.PassportCategory,
            passportType = passport.PassportType
        });
    }

    private static async Task<IResult> SaveResidence(
        [FromBody] RegisterResidenceRequest req,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IFileStorageService storage,
        CancellationToken ct)
    {
        var targetEmpId = await ResolveTargetEmployeeIdAsync(req.EmployeeID, principal, userManager);
        if (string.IsNullOrWhiteSpace(targetEmpId))
        {
            return Results.BadRequest(new { message = "الرقم الوظيفي للموظف مطلوب أو غير صالح." });
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == targetEmpId, ct);
        if (employee is null)
        {
            return Results.NotFound(new { message = $"سجل الموظف غير موجود بالنظام ({targetEmpId})." });
        }

        // Find or create residence
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var residence = await db.EmployeeResidences
            .FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && (r.ToDate == null || r.ToDate >= today), ct);

        if (residence is null)
        {
            residence = new EmployeeResidence
            {
                EmployeeId = employee.Id,
                EmployeeIdNumber = employee.EmployeeID,
                FromDate = today
            };
            db.EmployeeResidences.Add(residence);
        }

        residence.FormNumber = req.FormNumber?.Trim();
        residence.FormDate = req.FormDate;
        residence.InformationOffice = req.InformationOffice?.Trim();
        residence.FamilyHeadName = req.FamilyHeadName?.Trim() ?? employee.Name;
        residence.GovernorateId = req.GovernorateId;
        residence.District = req.District?.Trim();
        residence.Mahalla = req.Mahalla?.Trim();
        residence.Zuqaq = req.Zuqaq?.Trim();
        residence.Dar = req.Dar?.Trim();
        residence.HousingType = string.IsNullOrWhiteSpace(req.HousingType) ? "دار" : req.HousingType.Trim();
        residence.ComplexName = req.ComplexName?.Trim();
        residence.BuildingNumber = req.BuildingNumber?.Trim();
        residence.FloorNumber = req.FloorNumber?.Trim();
        residence.ApartmentNumber = req.ApartmentNumber?.Trim();
        residence.Latitude = req.Latitude;
        residence.Longitude = req.Longitude;
        residence.LocationUrl = req.LocationUrl?.Trim() ??
            (req.Latitude.HasValue && req.Longitude.HasValue
                ? $"https://www.google.com/maps?q={req.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{req.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : null);

        // Save card front image if provided
        if (!string.IsNullOrWhiteSpace(req.CardFrontBase64))
        {
            var key = $"residence/{employee.EmployeeID}/front_{DateTime.UtcNow.Ticks}.jpg";
            residence.CardFrontKey = await SaveBase64ImageAsync(req.CardFrontBase64, key, storage, ct);
            residence.CardFrontFileName = $"residence_front_{employee.EmployeeID}.jpg";
            residence.CardFrontContentType = "image/jpeg";
        }

        // Save card back image if provided
        if (!string.IsNullOrWhiteSpace(req.CardBackBase64))
        {
            var key = $"residence/{employee.EmployeeID}/back_{DateTime.UtcNow.Ticks}.jpg";
            residence.CardBackKey = await SaveBase64ImageAsync(req.CardBackBase64, key, storage, ct);
            residence.CardBackFileName = $"residence_back_{employee.EmployeeID}.jpg";
            residence.CardBackContentType = "image/jpeg";
        }

        residence.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            message = "تم حفظ وتحديث بيانات بطاقة السكن بنجاح.",
            residenceId = residence.Id,
            isElectronic = false,
            documentFormat = "عادي (ورقي)",
            formNumber = residence.FormNumber,
            informationOffice = residence.InformationOffice
        });
    }

    private static async Task<IResult> SaveNationalCard(
        [FromBody] RegisterNationalCardRequest req,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        INationalCardService cardService,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var targetEmpId = await ResolveTargetEmployeeIdAsync(req.EmployeeID, principal, userManager);
        if (string.IsNullOrWhiteSpace(targetEmpId))
        {
            return Results.BadRequest(new { message = "الرقم الوظيفي للموظف مطلوب أو غير صالح." });
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == targetEmpId, ct);
        if (employee is null)
        {
            return Results.NotFound(new { message = $"سجل الموظف غير موجود بالنظام ({targetEmpId})." });
        }

        var serviceRequest = new NationalCardRegisterRequest
        {
            DocumentNumber = req.DocumentNumber,
            NationalIdNumber = req.NationalIdNumber,
            FullNameAr = req.FullNameAr,
            FirstNameEn = req.FirstNameEn,
            LastNameEn = req.LastNameEn,
            FullNameEn = req.FullNameEn,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            Nationality = req.Nationality,
            IssuingState = string.IsNullOrWhiteSpace(req.IssuingState) ? "IRQ" : req.IssuingState,
            IssueDate = req.IssueDate,
            ExpiryDate = req.ExpiryDate,
            RawMrz = req.RawMrz,
            BiometricFaceBase64 = req.BiometricFaceBase64,
            FrontImageBase64 = req.FrontImageBase64,
            BackImageBase64 = req.BackImageBase64,
            IsPassiveAuthenticationValid = req.IsPassiveAuthenticationValid
        };

        var username = principal.Identity?.Name ?? targetEmpId;
        var result = await cardService.RegisterOrUpdateAsync(employee.Id, serviceRequest, username, ct);

        return Results.Ok(new
        {
            success = true,
            message = "تم حفظ وتحديث بيانات البطاقة الوطنية الموحدة بنجاح.",
            isElectronic = true,
            documentFormat = "إلكتروني (شريحة ذكية)",
            card = result
        });
    }

    #region Helper Methods

    private static async Task<string?> ResolveTargetEmployeeIdAsync(
        string? requestedEmployeeId,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var currentEmpId = await GetCurrentEmployeeIdAsync(principal, userManager);
        var isStaff = StaffRoles.IsStaff(principal);

        // If not staff, force self-service (can only update their own documents)
        if (!isStaff)
        {
            return currentEmpId;
        }

        // If staff and an explicit employeeID was sent, use it; otherwise fallback to self
        return !string.IsNullOrWhiteSpace(requestedEmployeeId) ? requestedEmployeeId.Trim() : currentEmpId;
    }

    private static async Task<string?> GetCurrentEmployeeIdAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var claimEmpId = principal.FindFirst("employee_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimEmpId)) return claimEmpId;

        var user = await userManager.GetUserAsync(principal);
        return user?.EmployeeID;
    }

    private static async Task<string> SaveBase64ImageAsync(
        string base64Data,
        string storageKey,
        IFileStorageService storage,
        CancellationToken ct)
    {
        var cleanBase64 = base64Data.Trim();
        var match = Regex.Match(cleanBase64, @"data:image\/[a-zA-Z]+;base64,(?<data>.+)");
        if (match.Success)
        {
            cleanBase64 = match.Groups["data"].Value;
        }

        var bytes = Convert.FromBase64String(cleanBase64);
        using var memoryStream = new MemoryStream(bytes);
        return await storage.SaveAsync(memoryStream, storageKey, ct);
    }

    #endregion
}
