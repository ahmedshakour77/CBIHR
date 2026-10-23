using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

public interface ILookupEntity
{
    int Id { get; set; }
    string Name { get; set; }
}

public sealed class Formation : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(300)] public required string Name { get; set; }
}

public sealed class JobFamily : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
    [MaxLength(50)] public string? Code { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class JobTitle : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(50)] public string? Code { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
    public int? JobFamilyId { get; set; }
    public JobFamily? JobFamily { get; set; }
    public int? GradeId { get; set; }
    public Grade? Grade { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public sealed class PositionTitle : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
}

public sealed class Stage : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(50)] public string? Code { get; set; }
    public int? StageNumber { get; set; }
}

public sealed class Grade : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
    [MaxLength(50)] public string? Code { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class EducationLevel : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
}

public sealed class Specialization : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(300)] public required string Name { get; set; }
}

public sealed class Gender : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(50)] public required string Name { get; set; }
}

public sealed class Religion : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
}

public sealed class Nationality : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
}

public sealed class EmployeeStatus : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
}

public sealed class MaritalStatus : ILookupEntity
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Name { get; set; }
}

