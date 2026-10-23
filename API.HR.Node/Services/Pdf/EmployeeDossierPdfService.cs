using System.Globalization;
using Microsoft.EntityFrameworkCore;
using API.HR.Node.Data;
using API.HR.Node.Services.Storage;
using QRCoder;
using QuestPDF.Fluent;

namespace API.HR.Node.Services.Pdf;

public sealed class EmployeeDossierPdfService : IEmployeeDossierPdfService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IWebHostEnvironment _env;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<EmployeeDossierPdfService> _logger;

    public EmployeeDossierPdfService(
        ApplicationDbContext db,
        IFileStorageService storage,
        IWebHostEnvironment env,
        ILogger<EmployeeDossierPdfService> logger,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
    {
        _db = db;
        _storage = storage;
        _env = env;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<byte[]> GenerateDossierPdfAsync(int employeeId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var emp = await _db.Employees.AsNoTracking()
            .Include(e => e.Formation)
            .Include(e => e.AdministrativeUnit)
                .ThenInclude(u => u!.UnitType)
            .Include(e => e.AdministrativeUnit)
                .ThenInclude(u => u!.ParentUnit)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.Grade)
            .Include(e => e.Stage)
            .Include(e => e.EducationLevel)
            .Include(e => e.Specialization)
            .Include(e => e.Gender)
            .Include(e => e.Religion)
            .Include(e => e.Nationality)
            .Include(e => e.MaritalStatus)
            .Include(e => e.EmployeeStatus)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (emp is null)
            throw new KeyNotFoundException($"Employee with ID {employeeId} not found.");

        return await GenerateDossierInternalAsync(emp, baseUrl, cancellationToken);
    }

    public async Task<byte[]> GenerateDossierPdfByEmployeeIdNumberAsync(string employeeIdNumber, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var emp = await _db.Employees.AsNoTracking()
            .Include(e => e.Formation)
            .Include(e => e.AdministrativeUnit)
                .ThenInclude(u => u!.UnitType)
            .Include(e => e.AdministrativeUnit)
                .ThenInclude(u => u!.ParentUnit)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.Grade)
            .Include(e => e.Stage)
            .Include(e => e.EducationLevel)
            .Include(e => e.Specialization)
            .Include(e => e.Gender)
            .Include(e => e.Religion)
            .Include(e => e.Nationality)
            .Include(e => e.MaritalStatus)
            .Include(e => e.EmployeeStatus)
            .FirstOrDefaultAsync(e => e.EmployeeID == employeeIdNumber, cancellationToken);

        if (emp is null)
            throw new KeyNotFoundException($"Employee with EmployeeID {employeeIdNumber} not found.");

        return await GenerateDossierInternalAsync(emp, baseUrl, cancellationToken);
    }

    private async Task<byte[]> GenerateDossierInternalAsync(Employee emp, string? baseUrl, CancellationToken ct)
    {
        // 1. Managed units
        var managedUnits = await _db.AdministrativeUnits.AsNoTracking()
            .Include(u => u.UnitType)
            .Include(u => u.ParentUnit)
            .Where(u => u.ManagerEmployeeId == emp.EmployeeID)
            .ToListAsync(ct);

        var effectiveUnit = emp.AdministrativeUnit ?? (managedUnits.Count > 0 ? managedUnits[0] : null);

        var isUnitManager = managedUnits.Any(u => u.Id == effectiveUnit?.Id)
            || (!string.IsNullOrWhiteSpace(effectiveUnit?.ManagerEmployeeId) && effectiveUnit.ManagerEmployeeId == emp.EmployeeID);

        string? unitManagerTitle = null;
        string? parentUnitName = effectiveUnit?.ParentUnit?.Name;

        if (isUnitManager)
        {
            unitManagerTitle = string.IsNullOrWhiteSpace(effectiveUnit?.ManagerTitle) ? "مسؤول التشكيل" : effectiveUnit.ManagerTitle;
        }
        else if (effectiveUnit != null)
        {
            unitManagerTitle = string.IsNullOrWhiteSpace(effectiveUnit.ManagerTitle) ? "مسؤول التشكيل" : effectiveUnit.ManagerTitle;
        }

        // Build Administrative Lineage (from immediate unit upwards according to hierarchy)
        var lineageNames = new List<string>();
        if (effectiveUnit != null)
        {
            var closures = await _db.AdministrativeUnitClosures.AsNoTracking()
                .Where(c => c.DescendantId == effectiveUnit.Id)
                .Include(c => c.Ancestor)
                    .ThenInclude(a => a!.UnitType)
                .OrderBy(c => c.Depth) // Depth 0 = immediate unit, 1 = parent, 2 = grandparent...
                .ToListAsync(ct);

            if (closures.Count > 0)
            {
                foreach (var cl in closures)
                {
                    var ancestorName = cl.Ancestor?.Name?.Trim();
                    var typeCode = cl.Ancestor?.UnitType?.Code;
                    var typeName = cl.Ancestor?.UnitType?.Name?.Trim();

                    if (ancestorName == "المحافظ" || typeCode == "GOV" || typeName == "المحافظ")
                        continue;

                    if (!string.IsNullOrWhiteSpace(ancestorName) && !lineageNames.Contains(ancestorName))
                    {
                        lineageNames.Add(ancestorName);
                    }
                }
            }
            else
            {
                var curr = effectiveUnit;
                int safety = 0;
                while (curr != null && safety++ < 10)
                {
                    var uName = curr.Name?.Trim();
                    var typeCode = curr.UnitType?.Code;
                    var typeName = curr.UnitType?.Name?.Trim();

                    if (uName != "المحافظ" && typeCode != "GOV" && typeName != "المحافظ")
                    {
                        if (!string.IsNullOrWhiteSpace(uName) && !lineageNames.Contains(uName))
                        {
                            lineageNames.Add(uName);
                        }
                    }

                    if (curr.ParentUnit != null)
                    {
                        curr = curr.ParentUnit;
                    }
                    else if (curr.ParentUnitId.HasValue)
                    {
                        curr = await _db.AdministrativeUnits.AsNoTracking()
                            .Include(u => u.ParentUnit)
                            .Include(u => u.UnitType)
                            .FirstOrDefaultAsync(u => u.Id == curr.ParentUnitId.Value, ct);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        else if (emp.Formation != null)
        {
            lineageNames.Add(emp.Formation.Name.Trim());
        }

        // 2. Phones & Emails
        var phones = await _db.EmployeePhones.AsNoTracking()
            .Where(p => p.EmployeeID == emp.EmployeeID)
            .OrderByDescending(p => p.IsPrimary)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var emails = await _db.EmployeeEmails.AsNoTracking()
            .Where(e => e.EmployeeID == emp.EmployeeID)
            .OrderByDescending(e => e.IsPrimary)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        // 3. Appreciation Letters
        var letters = await _db.AppreciationLetters.AsNoTracking()
            .Include(l => l.Type)
            .Where(l => l.EmployeeId == emp.Id)
            .OrderByDescending(l => l.LetterDate)
            .ToListAsync(ct);

        // 3b. Leaves
        var leaves = await _db.EmployeeLeaves.AsNoTracking()
            .Include(l => l.LeaveType)
            .Where(l => l.EmployeeId == emp.Id)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(ct);

        // 3c. Career Movements
        var careerMovements = await _db.EmployeeCareerMovements.AsNoTracking()
            .Where(m => m.EmployeeId == emp.Id)
            .OrderBy(m => m.MovementDate)
            .ToListAsync(ct);

        // 3d. Training Courses
        var courses = await _db.EmployeeCourses.AsNoTracking()
            .Where(c => c.EmployeeId == emp.Id)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

        // 3e. Active Allowance Period & Items
        var activeAllowancePeriod = await _db.EmployeeAllowancePeriods.AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.AllowanceType)
            .Where(p => p.EmployeeId == emp.Id && p.IsCurrent)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        // 3f. Disciplinary Penalties
        var penalties = await _db.EmployeePenalties.AsNoTracking()
            .Include(p => p.PenaltyType)
            .Where(p => p.EmployeeId == emp.Id)
            .OrderByDescending(p => p.PenaltyDate)
            .ToListAsync(ct);

        // 3g. Martyrs (ذوي الشهداء)
        var martyrs = await _db.EmployeeMartyrs.AsNoTracking()
            .Include(m => m.MartyrdomType)
            .Include(m => m.Kinship)
            .Where(m => m.EmployeeId == emp.Id)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        // 3h. Residences (معلومات السكن)
        var residences = await _db.EmployeeResidences.AsNoTracking()
            .Include(r => r.Governorate)
            .Where(r => r.EmployeeId == emp.Id)
            .OrderByDescending(r => r.ToDate == null ? 1 : 0)
            .ThenByDescending(r => r.FromDate)
            .ToListAsync(ct);

        // 4. Photo bytes
        byte[]? photoBytes = null;
        var activePhoto = await _db.EmployeeProfilePhotos.AsNoTracking()
            .Where(p => p.EmployeeID == emp.EmployeeID && p.IsActive)
            .FirstOrDefaultAsync(ct);

        if (activePhoto != null)
        {
            try
            {
                using var photoStream = await _storage.OpenReadAsync(activePhoto.StorageKey, ct);
                if (photoStream != null)
                {
                    using var ms = new MemoryStream();
                    await photoStream.CopyToAsync(ms, ct);
                    photoBytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load profile photo for employee {EmployeeID}", emp.EmployeeID);
            }
        }

        // 5. Logo bytes
        byte[]? logoBytes = null;
        try
        {
            var logoPath = Path.Combine(_env.WebRootPath, "img", "logo.png");
            if (File.Exists(logoPath))
            {
                logoBytes = await File.ReadAllBytesAsync(logoPath, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read logo image from webroot.");
        }

        // 6. Security Verification Token, Snapshot Data Digest & Export Audit Logging
        var verificationToken = DossierVerificationHelper.GenerateOpaqueToken();
        var snapshotDigest = DossierVerificationHelper.ComputeEmployeeDataDigest(emp);

        var http = _httpContextAccessor?.HttpContext;
        var exportedByUserId = http?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var exportedByUserName = http?.User?.Identity?.Name ?? "النظام";
        var clientIp = http?.Connection?.RemoteIpAddress?.ToString();

        var auditRecord = new DossierExportAudit
        {
            VerificationToken = verificationToken,
            EmployeeId = emp.Id,
            EmployeeIDNumber = emp.EmployeeID,
            EmployeeName = emp.Name,
            FormationName = emp.Formation?.Name,
            JobTitleName = emp.JobTitle?.Name,
            SnapshotDataDigest = snapshotDigest,
            ExportedByUserId = exportedByUserId,
            ExportedByUserName = exportedByUserName,
            ExportedFromIp = clientIp,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            _db.DossierExportAudits.Add(auditRecord);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist DossierExportAudit record for employee {EmpId}.", emp.EmployeeID);
        }

        // Generate completely opaque, clean URL: /v/{token}
        var verificationUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? $"/v/{verificationToken}"
            : $"{baseUrl.TrimEnd('/')}/v/{verificationToken}";

        byte[]? qrBytes = null;
        try
        {
            using var qrGen = new QRCodeGenerator();
            using var qrData = qrGen.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            qrBytes = qrCode.GetGraphic(12);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not generate QR code for employee dossier.");
        }

        // 7. Calculate service lengths & age
        var (sYears, sMonths, sDays) = CalculateService(emp.HireDate);
        var (aYears, aMonths) = CalculateAge(emp.BirthDate);

        var model = new EmployeeDossierPdfModel
        {
            Employee = emp,
            EffectiveUnit = effectiveUnit,
            ManagedUnits = managedUnits,
            Phones = phones,
            Emails = emails,
            AppreciationLetters = letters,
            Leaves = leaves,
            Penalties = penalties,
            Martyrs = martyrs,
            Residences = residences,
            CareerMovements = careerMovements,
            Courses = courses,
            ServiceYears = sYears,
            ServiceMonths = sMonths,
            ServiceDays = sDays,
            AgeYears = aYears,
            AgeMonths = aMonths,
            ProfilePhotoBytes = photoBytes,
            QrCodeBytes = qrBytes,
            PortalVerificationUrl = verificationUrl,
            VerificationToken = verificationToken,
            SnapshotDataDigest = snapshotDigest,
            LogoBytes = logoBytes,
            IsUnitManager = isUnitManager,
            UnitManagerTitle = unitManagerTitle,
            AdministrativeParentUnitName = parentUnitName,
            AdministrativeLineage = lineageNames,
            ActiveAllowancePeriod = activeAllowancePeriod
        };

        var doc = new EmployeeDossierPdfDocument(model);
        return doc.GeneratePdf();
    }

    private static (int Years, int Months, int Days) CalculateService(DateOnly? hireDate)
    {
        if (hireDate is null) return (0, 0, 0);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (hireDate > today) return (0, 0, 0);

        var y = today.Year - hireDate.Value.Year;
        var m = today.Month - hireDate.Value.Month;
        var d = today.Day - hireDate.Value.Day;

        if (d < 0)
        {
            m--;
            var prev = today.AddMonths(-1);
            d += DateTime.DaysInMonth(prev.Year, prev.Month);
        }
        if (m < 0)
        {
            y--;
            m += 12;
        }

        return (Math.Max(0, y), Math.Max(0, m), Math.Max(0, d));
    }

    private static (int Years, int Months) CalculateAge(DateOnly? birthDate)
    {
        if (birthDate is null) return (0, 0);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (birthDate > today) return (0, 0);

        var y = today.Year - birthDate.Value.Year;
        var m = today.Month - birthDate.Value.Month;

        if (m < 0 || (m == 0 && today.Day < birthDate.Value.Day))
        {
            y--;
            m = (m + 12) % 12;
        }

        return (Math.Max(0, y), Math.Max(0, m));
    }
}
