using API.HR.Node.Data;
using API.HR.Node.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Employees;

public sealed class EmployeeLifecycleService : IEmployeeLifecycleService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<EmployeeLifecycleService> _logger;

    public EmployeeLifecycleService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IFileStorageService fileStorage,
        ILogger<EmployeeLifecycleService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<EmployeeLifecycleResult> ArchiveEmployeeAsync(
        string employeeId,
        string reason,
        string performedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return new EmployeeLifecycleResult(false, "رقم الموظف غير محدد.");
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            var employee = await dbContext.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeId, ct);

            if (employee is null)
            {
                return new EmployeeLifecycleResult(false, $"لم يتم العثور على الموظف برقم {employeeId}.");
            }

            if (employee.IsArchived)
            {
                return new EmployeeLifecycleResult(true, $"الموظف {employee.Name} مستبعد مؤقتاً بالفعل.", employee.Name);
            }

            employee.IsArchived = true;
            employee.ArchivedAt = DateTime.UtcNow;
            employee.ArchiveReason = string.IsNullOrWhiteSpace(reason) ? "استبعاد إداري مؤقت" : reason.Trim();
            employee.ArchivedBy = string.IsNullOrWhiteSpace(performedBy) ? "النظام" : performedBy.Trim();

            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "EmployeeArchived",
                EmployeeID = employee.EmployeeID,
                UserId = performedBy,
                Timestamp = DateTimeOffset.UtcNow,
                Success = true,
                FailureReason = $"تم استبعاد الموظف {employee.Name} مؤقتاً من النظام والتقارير. السبب: {employee.ArchiveReason}"
            });

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Employee {EmployeeId} ({Name}) was archived by {User}. Reason: {Reason}",
                employee.EmployeeID, employee.Name, performedBy, employee.ArchiveReason);

            return new EmployeeLifecycleResult(true, $"تم استبعاد الموظف {employee.Name} مؤقتاً بنجاح ولن يظهر في أي تقرير.", employee.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving employee {EmployeeId}", employeeId);
            return new EmployeeLifecycleResult(false, $"حدث خطأ أثناء أرشفة الموظف: {ex.Message}");
        }
    }

    public async Task<EmployeeLifecycleResult> RestoreEmployeeAsync(
        string employeeId,
        string performedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return new EmployeeLifecycleResult(false, "رقم الموظف غير محدد.");
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            var employee = await dbContext.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeId, ct);

            if (employee is null)
            {
                return new EmployeeLifecycleResult(false, $"لم يتم العثور على الموظف برقم {employeeId}.");
            }

            if (!employee.IsArchived)
            {
                return new EmployeeLifecycleResult(true, $"الموظف {employee.Name} نشط في النظام بالفعل.", employee.Name);
            }

            employee.IsArchived = false;
            employee.ArchivedAt = null;
            employee.ArchiveReason = null;
            employee.ArchivedBy = null;

            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "EmployeeRestored",
                EmployeeID = employee.EmployeeID,
                UserId = performedBy,
                Timestamp = DateTimeOffset.UtcNow,
                Success = true,
                FailureReason = $"تمت استعادة الموظف {employee.Name} إلى الخدمة والتقارير بنشاط كامل."
            });

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Employee {EmployeeId} ({Name}) was restored by {User}",
                employee.EmployeeID, employee.Name, performedBy);

            return new EmployeeLifecycleResult(true, $"تم استرجاع الموظف {employee.Name} إلى النظام والتقارير بنجاح.", employee.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring employee {EmployeeId}", employeeId);
            return new EmployeeLifecycleResult(false, $"حدث خطأ أثناء استعادة الموظف: {ex.Message}");
        }
    }

    public async Task<EmployeeLifecycleResult> DeletePermanentlyAsync(
        string employeeId,
        string performedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return new EmployeeLifecycleResult(false, "رقم الموظف غير محدد.");
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            var employee = await dbContext.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EmployeeID == employeeId, ct);

            if (employee is null)
            {
                return new EmployeeLifecycleResult(false, $"لم يتم العثور على الموظف برقم {employeeId}.");
            }

            var empName = employee.Name;
            var empId = employee.Id;
            var empIdStr = employee.EmployeeID;

            // 1. Unlink administrative unit management
            var units = await dbContext.AdministrativeUnits
                .Where(u => u.ManagerEmployeeId == empIdStr)
                .ToListAsync(ct);
            foreach (var u in units)
            {
                u.ManagerEmployeeId = null;
            }

            // 2. Unlink ApplicationUser
            var appUsers = await dbContext.Users
                .Where(u => u.EmployeeID == empIdStr)
                .ToListAsync(ct);
            foreach (var u in appUsers)
            {
                u.EmployeeID = null;
            }

            // 3. Delete Profile Photos and physical files
            var photos = await dbContext.EmployeeProfilePhotos
                .Where(p => p.EmployeeID == empIdStr)
                .ToListAsync(ct);
            foreach (var p in photos)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.StorageKey))
                        await _fileStorage.DeleteAsync(p.StorageKey, ct);
                    if (!string.IsNullOrWhiteSpace(p.ThumbnailStorageKey))
                        await _fileStorage.DeleteAsync(p.ThumbnailStorageKey, ct);
                }
                catch (Exception photoEx)
                {
                    _logger.LogWarning(photoEx, "Failed to clean physical photo for {EmployeeId}", empIdStr);
                }
            }
            dbContext.EmployeeProfilePhotos.RemoveRange(photos);

            // 4. Contact data (Phones, Emails)
            var phones = await dbContext.EmployeePhones
                .Where(p => p.EmployeeID == empIdStr)
                .ToListAsync(ct);
            dbContext.EmployeePhones.RemoveRange(phones);

            var emails = await dbContext.EmployeeEmails
                .Where(e => e.EmployeeID == empIdStr)
                .ToListAsync(ct);
            dbContext.EmployeeEmails.RemoveRange(emails);

            // 5. Passkeys & Devices
            var devices = await dbContext.UserDevices
                .Where(d => d.EmployeeID == empIdStr)
                .ToListAsync(ct);
            dbContext.UserDevices.RemoveRange(devices);

            var passkeys = await dbContext.EmployeePasskeys
                .Where(p => p.EmployeeID == empIdStr)
                .ToListAsync(ct);
            dbContext.EmployeePasskeys.RemoveRange(passkeys);

            // 6. Financial Accounts & Transactions
            var trxs = await dbContext.EmployeeBalanceTransactions
                .Where(t => t.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeBalanceTransactions.RemoveRange(trxs);

            var finAcc = await dbContext.EmployeeFinancialAccounts
                .FirstOrDefaultAsync(a => a.EmployeeId == empId, ct);
            if (finAcc is not null)
            {
                dbContext.EmployeeFinancialAccounts.Remove(finAcc);
            }

            // 7. Allowances
            var allowancePeriods = await dbContext.EmployeeAllowancePeriods
                .Include(p => p.Items)
                .Where(p => p.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeAllowancePeriods.RemoveRange(allowancePeriods);

            // 8. Appreciation Letters
            var letters = await dbContext.AppreciationLetters
                .Where(l => l.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.AppreciationLetters.RemoveRange(letters);

            // 9. Career Movements & Courses
            var movements = await dbContext.EmployeeCareerMovements
                .Where(m => m.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeCareerMovements.RemoveRange(movements);

            var courses = await dbContext.EmployeeCourses
                .Where(c => c.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeCourses.RemoveRange(courses);

            // 10. Leaves, Penalties, Martyrs, Residences
            var leaves = await dbContext.EmployeeLeaves
                .Where(l => l.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeLeaves.RemoveRange(leaves);

            var penalties = await dbContext.EmployeePenalties
                .Where(p => p.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeePenalties.RemoveRange(penalties);

            var martyrs = await dbContext.EmployeeMartyrs
                .Where(m => m.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeMartyrs.RemoveRange(martyrs);

            var residences = await dbContext.EmployeeResidences
                .Where(r => r.EmployeeId == empId)
                .ToListAsync(ct);
            dbContext.EmployeeResidences.RemoveRange(residences);

            // 11. Feedbacks & Dossier Audits
            var feedbacks = await dbContext.EmployeeFeedbacks
                .Where(f => f.EmployeeId == empId)
                .ToListAsync(ct);
            foreach (var f in feedbacks)
            {
                f.EmployeeId = null;
            }

            var dossiers = await dbContext.DossierExportAudits
                .Where(d => d.EmployeeId == empId || d.EmployeeIDNumber == empIdStr)
                .ToListAsync(ct);
            dbContext.DossierExportAudits.RemoveRange(dossiers);

            // 12. Audit log of the permanent deletion
            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "EmployeeHardDeleted",
                EmployeeID = empIdStr,
                UserId = performedBy,
                Timestamp = DateTimeOffset.UtcNow,
                Success = true,
                FailureReason = $"تم حذف الموظف {empName} برقم {empIdStr} نهائياً من قاعدة البيانات مع كافة ارتباطاته وسجلاته."
            });

            // 13. Finally remove the Employee
            dbContext.Employees.Remove(employee);

            await dbContext.SaveChangesAsync(ct);

            _logger.LogWarning("Employee {EmployeeId} ({Name}) was PERMANENTLY deleted by {User}",
                empIdStr, empName, performedBy);

            return new EmployeeLifecycleResult(true, $"تم حذف الموظف {empName} نهائياً من النظام.", empName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {EmployeeId} permanently", employeeId);
            return new EmployeeLifecycleResult(false, $"حدث خطأ أثناء الحذف النهائي للموظف: {ex.Message}");
        }
    }

    public async Task<List<ArchivedEmployeeDto>> GetArchivedEmployeesAsync(
        string? search = null,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        IQueryable<Employee> query = dbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => e.IsArchived)
            .Include(e => e.Formation)
            .Include(e => e.JobTitle)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.EmployeeID.Contains(term) || e.Name.Contains(term));
        }

        return await query
            .OrderByDescending(e => e.ArchivedAt)
            .Select(e => new ArchivedEmployeeDto(
                e.Id,
                e.EmployeeID,
                e.Name,
                e.Formation != null ? e.Formation.Name : null,
                e.JobTitle != null ? e.JobTitle.Name : null,
                e.ArchivedAt,
                e.ArchiveReason,
                e.ArchivedBy))
            .ToListAsync(ct);
    }

    public async Task<int> GetArchivedCountAsync(CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        return await dbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => e.IsArchived)
            .CountAsync(ct);
    }
}
