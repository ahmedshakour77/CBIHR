using API.HR.Node.Data.OrganizationalHierarchy;

namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed record HierarchyEmployeeDto
{
    public int Id { get; init; }
    public string EmployeeID { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public string? PositionTitle { get; init; }
    public int? AdministrativeUnitId { get; init; }
    public string? AdministrativeUnitName { get; init; }
    public bool IsManager { get; init; }
}

public sealed record HierarchyTreeNodeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? NameKu { get; init; }
    public string Code { get; init; } = string.Empty;
    public int UnitTypeId { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string? TypeNameKu { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public int HierarchyLevel { get; init; }
    public int? ParentUnitId { get; init; }
    public string? ParentName { get; init; }
    public string? ManagerTitle { get; init; }
    public string? ManagerEmployeeId { get; init; }
    public string? ManagerName { get; init; }
    public int OrderIndex { get; init; }
    public bool IsActive { get; init; }
    public int SubUnitsCount { get; init; }
    public int EmployeesCount { get; init; }
    public List<HierarchyEmployeeDto> Employees { get; init; } = new();
    public List<HierarchyTreeNodeDto> Children { get; init; } = new();
}

public sealed record LineageStepDto
{
    public int UnitId { get; init; }
    public string UnitName { get; init; } = string.Empty;
    public string? UnitNameKu { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string? TypeNameKu { get; init; }
    public int HierarchyLevel { get; init; }
    public int Depth { get; init; }
    public string LineagePath { get; init; } = string.Empty;
}

public interface IOrganizationalHierarchyService
{
    // Unit Types
    Task<List<AdministrativeUnitType>> GetUnitTypesAsync();
    Task<AdministrativeUnitType> CreateUnitTypeAsync(string name, string? nameKu, string code, int hierarchyLevel, string? description);
    Task<bool> UpdateUnitTypeAsync(int id, string name, string? nameKu, string code, int hierarchyLevel, string? description, bool isActive);
    Task<bool> DeleteUnitTypeAsync(int id);

    // Units & Tree
    Task<List<HierarchyTreeNodeDto>> GetHierarchyTreeAsync();
    Task<List<LineageStepDto>> GetLineageToRootAsync(int unitId);
    Task<List<AdministrativeUnit>> GetAllUnitsFlatAsync();
    Task<AdministrativeUnit?> GetUnitByIdAsync(int id);
    Task<AdministrativeUnit> CreateUnitAsync(string name, string? nameKu, string code, int unitTypeId, int? parentUnitId, string? managerTitle, string? managerEmployeeId, string? notes);
    Task<bool> UpdateUnitAsync(int id, string name, string? nameKu, string code, int unitTypeId, int? parentUnitId, string? managerTitle, string? managerEmployeeId, bool isActive, string? notes);
    Task<bool> DeleteUnitAsync(int id);
    Task<int> ClearAllUnitsAsync();

    // Staff / Employees in Units
    Task<List<HierarchyEmployeeDto>> GetEmployeesByUnitAsync(int unitId);
    Task<List<HierarchyEmployeeDto>> SearchEmployeesAsync(string? search = null, int limit = 30);
    Task<bool> AssignEmployeeToUnitAsync(string employeeId, int? unitId);
    Task<bool> AssignEmployeesToUnitAsync(IEnumerable<string> employeeIds, int? unitId);
}
