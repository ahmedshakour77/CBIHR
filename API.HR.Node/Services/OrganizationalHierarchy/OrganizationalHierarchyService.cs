using API.HR.Node.Data;
using API.HR.Node.Data.OrganizationalHierarchy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed class OrganizationalHierarchyService : IOrganizationalHierarchyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrganizationalHierarchyService> _logger;

    public OrganizationalHierarchyService(ApplicationDbContext context, ILogger<OrganizationalHierarchyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<AdministrativeUnitType>> GetUnitTypesAsync()
    {
        return await _context.AdministrativeUnitTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.HierarchyLevel)
            .ToListAsync();
    }

    public async Task<List<AdministrativeUnit>> GetAllUnitsFlatAsync()
    {
        return await _context.AdministrativeUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Include(u => u.ParentUnit)
            .OrderBy(u => u.UnitType != null ? u.UnitType.HierarchyLevel : 99)
            .ThenBy(u => u.OrderIndex)
            .ToListAsync();
    }

    public async Task<AdministrativeUnit?> GetUnitByIdAsync(int id)
    {
        return await _context.AdministrativeUnits
            .Include(u => u.UnitType)
            .Include(u => u.ParentUnit)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<HierarchyTreeNodeDto>> GetHierarchyTreeAsync()
    {
        var allUnits = await _context.AdministrativeUnits
            .AsNoTracking()
            .Include(u => u.UnitType)
            .Include(u => u.ParentUnit)
            .OrderBy(u => u.OrderIndex)
            .ToListAsync();

        var unitIds = allUnits.Select(u => u.Id).ToList();
        var managerIds = allUnits
            .Where(u => !string.IsNullOrWhiteSpace(u.ManagerEmployeeId))
            .Select(u => u.ManagerEmployeeId!)
            .Distinct()
            .ToList();

        var relevantEmployees = await _context.Employees
            .AsNoTracking()
            .Where(e => (e.AdministrativeUnitId.HasValue && unitIds.Contains(e.AdministrativeUnitId.Value)) || managerIds.Contains(e.EmployeeID))
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.AdministrativeUnit)
            .ToListAsync();

        var employeeById = relevantEmployees.ToDictionary(e => e.EmployeeID, e => e.Name);

        var employeesByUnit = relevantEmployees
            .Where(e => e.AdministrativeUnitId.HasValue)
            .GroupBy(e => e.AdministrativeUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e =>
            {
                var unit = allUnits.FirstOrDefault(u => u.Id == e.AdministrativeUnitId);
                var isManager = !string.IsNullOrEmpty(unit?.ManagerEmployeeId) && unit.ManagerEmployeeId == e.EmployeeID;
                return new HierarchyEmployeeDto
                {
                    Id = e.Id,
                    EmployeeID = e.EmployeeID,
                    Name = e.Name,
                    JobTitle = e.JobTitle?.Name,
                    PositionTitle = e.PositionTitle?.Name,
                    AdministrativeUnitId = e.AdministrativeUnitId,
                    AdministrativeUnitName = unit?.Name,
                    IsManager = isManager
                };
            }).ToList());

        var nodesDict = allUnits.ToDictionary(u => u.Id, u =>
        {
            var unitEmployees = employeesByUnit.TryGetValue(u.Id, out var emps) ? emps : new List<HierarchyEmployeeDto>();
            var managerName = !string.IsNullOrWhiteSpace(u.ManagerEmployeeId) && employeeById.TryGetValue(u.ManagerEmployeeId, out var mName)
                ? mName
                : null;

            return new HierarchyTreeNodeDto
            {
                Id = u.Id,
                Name = u.Name,
                NameKu = u.NameKu,
                Code = u.Code,
                UnitTypeId = u.UnitTypeId,
                TypeName = u.UnitType?.Name ?? "غير محدد",
                TypeNameKu = u.UnitType?.NameKu,
                TypeCode = u.UnitType?.Code ?? string.Empty,
                HierarchyLevel = u.UnitType?.HierarchyLevel ?? 99,
                ParentUnitId = u.ParentUnitId,
                ParentName = u.ParentUnit?.Name,
                ManagerTitle = u.ManagerTitle,
                ManagerEmployeeId = u.ManagerEmployeeId,
                ManagerName = managerName,
                OrderIndex = u.OrderIndex,
                IsActive = u.IsActive,
                SubUnitsCount = 0,
                EmployeesCount = unitEmployees.Count,
                Employees = unitEmployees,
                Children = new List<HierarchyTreeNodeDto>()
            };
        });

        var rootNodes = new List<HierarchyTreeNodeDto>();

        foreach (var unit in allUnits)
        {
            if (unit.ParentUnitId.HasValue && nodesDict.TryGetValue(unit.ParentUnitId.Value, out var parentNode))
            {
                parentNode.Children.Add(nodesDict[unit.Id]);
            }
            else
            {
                rootNodes.Add(nodesDict[unit.Id]);
            }
        }

        // Calculate recursive sub-units count
        CalculateSubUnitsCount(rootNodes);

        return rootNodes;
    }

    private static int CalculateSubUnitsCount(List<HierarchyTreeNodeDto> nodes)
    {
        int total = 0;
        foreach (var node in nodes)
        {
            var childCount = CalculateSubUnitsCount(node.Children);
            node.GetType().GetProperty(nameof(HierarchyTreeNodeDto.SubUnitsCount))?.SetValue(node, childCount);
            total += 1 + childCount;
        }
        return total;
    }

    /// <summary>
    /// يسترجع سلسلة التبعية الصاعدة من الوحدة المحددة وصولاً إلى رأس الهرم (المحافظ)
    /// مرتبة من رأس الهرم (العمق الأعلى) إلى الوحدة المحددة (العمق 0).
    /// </summary>
    public async Task<List<LineageStepDto>> GetLineageToRootAsync(int unitId)
    {
        var closures = await _context.AdministrativeUnitClosures
            .AsNoTracking()
            .Where(c => c.DescendantId == unitId)
            .Include(c => c.Ancestor)
                .ThenInclude(a => a!.UnitType)
            .OrderByDescending(c => c.Depth)
            .ToListAsync();

        return closures.Select(c => new LineageStepDto
        {
            UnitId = c.AncestorId,
            UnitName = c.Ancestor?.Name ?? $"وحدة #{c.AncestorId}",
            UnitNameKu = c.Ancestor?.NameKu,
            TypeName = c.Ancestor?.UnitType?.Name ?? "غير محدد",
            TypeNameKu = c.Ancestor?.UnitType?.NameKu,
            HierarchyLevel = c.Ancestor?.UnitType?.HierarchyLevel ?? 99,
            Depth = c.Depth,
            LineagePath = c.LineagePath
        }).ToList();
    }

    public async Task<AdministrativeUnit> CreateUnitAsync(
        string name,
        string? nameKu,
        string code,
        int unitTypeId,
        int? parentUnitId,
        string? managerTitle,
        string? managerEmployeeId,
        string? notes)
    {
        var unit = new AdministrativeUnit
        {
            Name = name.Trim(),
            NameKu = nameKu?.Trim(),
            Code = code.Trim(),
            UnitTypeId = unitTypeId,
            ParentUnitId = parentUnitId,
            ManagerTitle = managerTitle?.Trim(),
            ManagerEmployeeId = managerEmployeeId?.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AdministrativeUnits.Add(unit);
        await _context.SaveChangesAsync();

        // Populate Closure Table
        var closures = new List<AdministrativeUnitClosure>();

        if (parentUnitId.HasValue)
        {
            var parentAncestors = await _context.AdministrativeUnitClosures
                .Where(c => c.DescendantId == parentUnitId.Value)
                .OrderBy(c => c.Depth)
                .ToListAsync();

            foreach (var pa in parentAncestors)
            {
                closures.Add(new AdministrativeUnitClosure
                {
                    AncestorId = pa.AncestorId,
                    DescendantId = unit.Id,
                    Depth = pa.Depth + 1,
                    LineagePath = $"{pa.LineagePath}{unit.Id}/"
                });
            }
        }

        // Self closure (Depth = 0)
        closures.Add(new AdministrativeUnitClosure
        {
            AncestorId = unit.Id,
            DescendantId = unit.Id,
            Depth = 0,
            LineagePath = parentUnitId.HasValue ? $".../{unit.Id}/" : $"/{unit.Id}/"
        });

        _context.AdministrativeUnitClosures.AddRange(closures);
        await _context.SaveChangesAsync();

        // If manager is assigned and has no administrative unit linked, link them to this unit
        if (!string.IsNullOrWhiteSpace(unit.ManagerEmployeeId))
        {
            var mgr = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeID == unit.ManagerEmployeeId);
            if (mgr != null && mgr.AdministrativeUnitId == null)
            {
                mgr.AdministrativeUnitId = unit.Id;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Auto-linked manager {ManagerId} to unit {UnitId}", unit.ManagerEmployeeId, unit.Id);
            }
        }

        _logger.LogInformation("Created administrative unit '{Name}' with ID {Id}", unit.Name, unit.Id);
        return unit;
    }

    public async Task<bool> UpdateUnitAsync(
        int id,
        string name,
        string? nameKu,
        string code,
        int unitTypeId,
        int? parentUnitId,
        string? managerTitle,
        string? managerEmployeeId,
        bool isActive,
        string? notes)
    {
        var unit = await _context.AdministrativeUnits.FindAsync(id);
        if (unit == null) return false;

        bool parentChanged = unit.ParentUnitId != parentUnitId;

        unit.Name = name.Trim();
        unit.NameKu = nameKu?.Trim();
        unit.Code = code.Trim();
        unit.UnitTypeId = unitTypeId;
        unit.ManagerTitle = managerTitle?.Trim();
        unit.ManagerEmployeeId = managerEmployeeId?.Trim();
        unit.IsActive = isActive;
        unit.Notes = notes?.Trim();

        if (parentChanged)
        {
            // Prevent cyclical hierarchy (cannot set parent to self or any descendant)
            if (parentUnitId.HasValue)
            {
                bool isDescendant = await _context.AdministrativeUnitClosures
                    .AnyAsync(c => c.AncestorId == id && c.DescendantId == parentUnitId.Value);

                if (id == parentUnitId.Value || isDescendant)
                {
                    throw new InvalidOperationException("لا يمكن جعل الوحدة تابعة لنفسها أو لأحد فروعها التابعة (دورة غير صالحة).");
                }
            }

            unit.ParentUnitId = parentUnitId;

            // Remove existing ancestor closures for this unit and its descendants
            var descendantIds = await _context.AdministrativeUnitClosures
                .Where(c => c.AncestorId == id)
                .Select(c => c.DescendantId)
                .ToListAsync();

            var oldClosures = await _context.AdministrativeUnitClosures
                .Where(c => descendantIds.Contains(c.DescendantId) && !descendantIds.Contains(c.AncestorId))
                .ToListAsync();

            _context.AdministrativeUnitClosures.RemoveRange(oldClosures);
            await _context.SaveChangesAsync();

            // Insert new ancestor closures from new parent
            if (parentUnitId.HasValue)
            {
                var newParentAncestors = await _context.AdministrativeUnitClosures
                    .Where(c => c.DescendantId == parentUnitId.Value)
                    .ToListAsync();

                var myDescendants = await _context.AdministrativeUnitClosures
                    .Where(c => c.AncestorId == id)
                    .ToListAsync();

                var newClosures = new List<AdministrativeUnitClosure>();
                foreach (var pa in newParentAncestors)
                {
                    foreach (var d in myDescendants)
                    {
                        newClosures.Add(new AdministrativeUnitClosure
                        {
                            AncestorId = pa.AncestorId,
                            DescendantId = d.DescendantId,
                            Depth = pa.Depth + d.Depth + 1,
                            LineagePath = $"{pa.LineagePath.TrimEnd('/')}/{d.LineagePath.TrimStart('/')}"
                        });
                    }
                }
                _context.AdministrativeUnitClosures.AddRange(newClosures);
            }
        }

        await _context.SaveChangesAsync();

        // If manager is assigned and has no administrative unit linked, link them to this unit
        if (!string.IsNullOrWhiteSpace(unit.ManagerEmployeeId))
        {
            var mgr = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeID == unit.ManagerEmployeeId);
            if (mgr != null && mgr.AdministrativeUnitId == null)
            {
                mgr.AdministrativeUnitId = unit.Id;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Auto-linked manager {ManagerId} to unit {UnitId}", unit.ManagerEmployeeId, unit.Id);
            }
        }

        _logger.LogInformation("Updated administrative unit '{Name}' (ID {Id})", unit.Name, unit.Id);
        return true;
    }

    public async Task<bool> DeleteUnitAsync(int id)
    {
        var unit = await _context.AdministrativeUnits.FindAsync(id);
        if (unit == null) return false;

        bool hasChildren = await _context.AdministrativeUnits.AnyAsync(u => u.ParentUnitId == id);
        if (hasChildren)
        {
            throw new InvalidOperationException("لا يمكن حذف الوحدة الإدارية لأنها تحتوي على تشكيلات أو فروع تابعة لها.");
        }

        // Closures with DescendantId will cascade delete; remove any remaining where unit is Ancestor
        var closures = await _context.AdministrativeUnitClosures
            .Where(c => c.AncestorId == id || c.DescendantId == id)
            .ToListAsync();
        _context.AdministrativeUnitClosures.RemoveRange(closures);

        _context.AdministrativeUnits.Remove(unit);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted administrative unit ID {Id}", id);
        return true;
    }

    public async Task<AdministrativeUnitType> CreateUnitTypeAsync(
        string name,
        string? nameKu,
        string code,
        int hierarchyLevel,
        string? description)
    {
        var type = new AdministrativeUnitType
        {
            Name = name.Trim(),
            NameKu = nameKu?.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            HierarchyLevel = hierarchyLevel,
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.AdministrativeUnitTypes.Add(type);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created administrative unit type '{Name}' with code '{Code}'", type.Name, type.Code);
        return type;
    }

    public async Task<bool> UpdateUnitTypeAsync(
        int id,
        string name,
        string? nameKu,
        string code,
        int hierarchyLevel,
        string? description,
        bool isActive)
    {
        var type = await _context.AdministrativeUnitTypes.FindAsync(id);
        if (type == null) return false;

        type.Name = name.Trim();
        type.NameKu = nameKu?.Trim();
        type.Code = code.Trim().ToUpperInvariant();
        type.HierarchyLevel = hierarchyLevel;
        type.Description = description?.Trim();
        type.IsActive = isActive;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated administrative unit type '{Name}' (ID {Id})", type.Name, type.Id);
        return true;
    }

    public async Task<bool> DeleteUnitTypeAsync(int id)
    {
        var type = await _context.AdministrativeUnitTypes.FindAsync(id);
        if (type == null) return false;

        bool hasUnits = await _context.AdministrativeUnits.AnyAsync(u => u.UnitTypeId == id);
        if (hasUnits)
        {
            throw new InvalidOperationException("لا يمكن حذف هذا النوع لوجود وحدات إدارية مرتبطة به حالياً.");
        }

        _context.AdministrativeUnitTypes.Remove(type);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted administrative unit type ID {Id}", id);
        return true;
    }

    public async Task<int> ClearAllUnitsAsync()
    {
        var closures = await _context.AdministrativeUnitClosures.ToListAsync();
        _context.AdministrativeUnitClosures.RemoveRange(closures);

        var units = await _context.AdministrativeUnits.ToListAsync();
        int count = units.Count;
        _context.AdministrativeUnits.RemoveRange(units);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Cleared all administrative units and closures ({Count} units deleted).", count);
        return count;
    }

    public async Task<List<HierarchyEmployeeDto>> GetEmployeesByUnitAsync(int unitId)
    {
        var unit = await _context.AdministrativeUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId);

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.AdministrativeUnitId == unitId)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.AdministrativeUnit)
            .OrderBy(e => e.Name)
            .ToListAsync();

        return employees.Select(e => new HierarchyEmployeeDto
        {
            Id = e.Id,
            EmployeeID = e.EmployeeID,
            Name = e.Name,
            JobTitle = e.JobTitle?.Name,
            PositionTitle = e.PositionTitle?.Name,
            AdministrativeUnitId = e.AdministrativeUnitId,
            AdministrativeUnitName = unit?.Name,
            IsManager = !string.IsNullOrEmpty(unit?.ManagerEmployeeId) && unit.ManagerEmployeeId == e.EmployeeID
        }).ToList();
    }

    public async Task<List<HierarchyEmployeeDto>> SearchEmployeesAsync(string? search = null, int limit = 30)
    {
        IQueryable<Employee> query = _context.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.EmployeeID.Contains(term));
        }

        var results = await query
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.AdministrativeUnit)
            .OrderBy(e => e.Name)
            .Take(limit)
            .ToListAsync();

        return results.Select(e => new HierarchyEmployeeDto
        {
            Id = e.Id,
            EmployeeID = e.EmployeeID,
            Name = e.Name,
            JobTitle = e.JobTitle?.Name,
            PositionTitle = e.PositionTitle?.Name,
            AdministrativeUnitId = e.AdministrativeUnitId,
            AdministrativeUnitName = e.AdministrativeUnit?.Name,
            IsManager = false
        }).ToList();
    }

    public async Task<bool> AssignEmployeeToUnitAsync(string employeeId, int? unitId)
    {
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeID == employeeId);
        if (emp == null) return false;

        emp.AdministrativeUnitId = unitId;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Assigned employee {EmployeeID} to AdministrativeUnit {UnitId}", employeeId, unitId);
        return true;
    }

    public async Task<bool> AssignEmployeesToUnitAsync(IEnumerable<string> employeeIds, int? unitId)
    {
        var ids = employeeIds.Distinct().ToList();
        var employees = await _context.Employees.Where(e => ids.Contains(e.EmployeeID)).ToListAsync();
        if (employees.Count == 0) return false;

        foreach (var emp in employees)
        {
            emp.AdministrativeUnitId = unitId;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Assigned {Count} employees to AdministrativeUnit {UnitId}", employees.Count, unitId);
        return true;
    }
}

