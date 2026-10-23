using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Employees;

public sealed class EmployeeVehicleService : IEmployeeVehicleService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<EmployeeVehicleService> _logger;

    public EmployeeVehicleService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<EmployeeVehicleService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<List<EmployeeVehicle>> GetVehiclesByEmployeeIdAsync(int employeeId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.EmployeeVehicles
            .AsNoTracking()
            .Include(v => v.Employee)
            .Where(v => v.EmployeeId == employeeId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmployeeVehicle>> GetVehiclesByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.EmployeeVehicles
            .AsNoTracking()
            .Include(v => v.Employee)
            .Where(v => v.Employee != null && v.Employee.EmployeeID == employeeCode)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<EmployeeVehicle>> GetAllVehiclesAsync(
        string? search = null,
        string? location = null,
        bool? isAuthorized = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.EmployeeVehicles
            .AsNoTracking()
            .Include(v => v.Employee)
            .AsQueryable();

        if (isAuthorized.HasValue)
        {
            query = query.Where(v => v.IsAuthorized == isAuthorized.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim().ToLowerInvariant();
            query = loc switch
            {
                "main" or "المبنى الرئيسي" => query.Where(v => v.AllowMainBuilding),
                "jadriya" or "الجادرية" => query.Where(v => v.AllowJadriya),
                "erbil" or "أربيل" or "اربيل" => query.Where(v => v.AllowErbil),
                "mosul" or "الموصل" => query.Where(v => v.AllowMosul),
                "basra" or "البصرة" => query.Where(v => v.AllowBasra),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(v =>
                v.PlateNumber.Contains(s) ||
                (v.PlateLetter != null && v.PlateLetter.Contains(s)) ||
                v.Governorate.Contains(s) ||
                v.Brand.Contains(s) ||
                v.Model.Contains(s) ||
                (v.Color != null && v.Color.Contains(s)) ||
                (v.Vin != null && v.Vin.Contains(s)) ||
                (v.DriverName != null && v.DriverName.Contains(s)) ||
                (v.DriverPhone != null && v.DriverPhone.Contains(s)) ||
                (v.Employee != null && (v.Employee.Name.Contains(s) || v.Employee.EmployeeID.Contains(s)))
            );
        }

        return await query
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<EmployeeVehicle?> GetVehicleByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.EmployeeVehicles
            .AsNoTracking()
            .Include(v => v.Employee)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<EmployeeVehicle> AddVehicleAsync(EmployeeVehicle vehicle, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        vehicle.CreatedAtUtc = DateTimeOffset.UtcNow;
        vehicle.UpdatedAtUtc = null;

        db.EmployeeVehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Added new vehicle ID {VehicleId} for Employee ID {EmployeeId}", vehicle.Id, vehicle.EmployeeId);
        return vehicle;
    }

    public async Task<EmployeeVehicle> UpdateVehicleAsync(EmployeeVehicle vehicle, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.EmployeeVehicles.FirstOrDefaultAsync(v => v.Id == vehicle.Id, ct);
        if (existing == null)
        {
            throw new KeyNotFoundException($"المركبة برقم {vehicle.Id} غير موجودة");
        }

        existing.EmployeeId = vehicle.EmployeeId;
        existing.Brand = vehicle.Brand;
        existing.Model = vehicle.Model;
        existing.Color = vehicle.Color;
        existing.PlateNumber = vehicle.PlateNumber;
        existing.PlateLetter = vehicle.PlateLetter;
        existing.Governorate = vehicle.Governorate;
        existing.Vin = vehicle.Vin;
        existing.ManufacturingYear = vehicle.ManufacturingYear;
        existing.IsDriverSelf = vehicle.IsDriverSelf;
        existing.DriverName = vehicle.DriverName;
        existing.DriverPhone = vehicle.DriverPhone;
        existing.IsAuthorized = vehicle.IsAuthorized;
        existing.AllowMainBuilding = vehicle.AllowMainBuilding;
        existing.AllowJadriya = vehicle.AllowJadriya;
        existing.AllowErbil = vehicle.AllowErbil;
        existing.AllowMosul = vehicle.AllowMosul;
        existing.AllowBasra = vehicle.AllowBasra;
        existing.Notes = vehicle.Notes;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated vehicle ID {VehicleId}", vehicle.Id);
        return existing;
    }

    public async Task<bool> DeleteVehicleAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.EmployeeVehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (existing == null) return false;

        db.EmployeeVehicles.Remove(existing);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted vehicle ID {VehicleId}", id);
        return true;
    }

    public async Task<bool> ToggleAuthorizationAsync(int id, bool isAuthorized, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.EmployeeVehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (existing == null) return false;

        existing.IsAuthorized = isAuthorized;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Toggled authorization for vehicle ID {VehicleId} to {IsAuthorized}", id, isAuthorized);
        return true;
    }

    public async Task<VehicleKpiSummary> GetKpiSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var vehicles = await db.EmployeeVehicles
            .AsNoTracking()
            .Select(v => new
            {
                v.IsAuthorized,
                v.AllowMainBuilding,
                v.AllowJadriya,
                v.AllowErbil,
                v.AllowMosul,
                v.AllowBasra
            })
            .ToListAsync(ct);

        int total = vehicles.Count;
        int auth = vehicles.Count(v => v.IsAuthorized);
        int unauth = total - auth;
        int main = vehicles.Count(v => v.IsAuthorized && v.AllowMainBuilding);
        int jadriya = vehicles.Count(v => v.IsAuthorized && v.AllowJadriya);
        int erbil = vehicles.Count(v => v.IsAuthorized && v.AllowErbil);
        int mosul = vehicles.Count(v => v.IsAuthorized && v.AllowMosul);
        int basra = vehicles.Count(v => v.IsAuthorized && v.AllowBasra);

        return new VehicleKpiSummary(total, auth, unauth, main, jadriya, erbil, mosul, basra);
    }
}
