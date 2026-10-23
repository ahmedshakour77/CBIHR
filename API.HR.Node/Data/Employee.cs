using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data;

public sealed class Employee
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string EmployeeID { get; set; }

    [MaxLength(300)]
    public required string Name { get; set; }

    [MaxLength(50)]
    public string? NationalId { get; set; }

    [MaxLength(50)]
    public string? UnifiedEmployeeNumber { get; set; }

    [MaxLength(150)]
    public string? MotherName { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? SecondName { get; set; }

    [MaxLength(100)]
    public string? ThirdName { get; set; }

    [MaxLength(100)]
    public string? FourthName { get; set; }

    public int? FormationId { get; set; }
    public Formation? Formation { get; set; }

    public int? AdministrativeUnitId { get; set; }
    public OrganizationalHierarchy.AdministrativeUnit? AdministrativeUnit { get; set; }

    public int? JobTitleId { get; set; }
    public JobTitle? JobTitle { get; set; }

    public int? PositionTitleId { get; set; }
    public PositionTitle? PositionTitle { get; set; }

    public int? StageId { get; set; }
    public Stage? Stage { get; set; }

    public int? GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? EducationLevelId { get; set; }
    public EducationLevel? EducationLevel { get; set; }

    public int? SpecializationId { get; set; }
    public Specialization? Specialization { get; set; }

    public DateOnly? LastJobTitleDate { get; set; }

    /// <summary>تاريخ آخر علاوة سنوية نالها الموظف</summary>
    public DateOnly? LastAllowanceDate { get; set; }

    public DateOnly? HireDate { get; set; }

    public int? GenderId { get; set; }
    public Gender? Gender { get; set; }

    public int? ReligionId { get; set; }
    public Religion? Religion { get; set; }

    public int? NationalityId { get; set; }
    public Nationality? Nationality { get; set; }

    [MaxLength(300)]
    public string? FirstDepartment { get; set; }

    public DateOnly? BirthDate { get; set; }

    public int? EmployeeStatusId { get; set; }
    public EmployeeStatus? EmployeeStatus { get; set; }

    public int? MaritalStatusId { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }

    /// <summary>
    /// رصيد الإجازات المتبقي للموظف (بالأيام)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? LeaveBalance { get; set; }

    /// <summary>
    /// هل تم استبعاد الموظف / أرشفته مؤقتاً؟
    /// الموظف المؤرشف لا يظهر في أي تقرير أو لوحة تحكم أو إحصائية أو دليل للموظفين.
    /// </summary>
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAt { get; set; }

    [MaxLength(500)]
    public string? ArchiveReason { get; set; }

    [MaxLength(100)]
    public string? ArchivedBy { get; set; }

    public ICollection<EmployeeLeave> Leaves { get; set; } = new List<EmployeeLeave>();
    public ICollection<EmployeePenalty> Penalties { get; set; } = new List<EmployeePenalty>();
    public ICollection<AppreciationLetter> AppreciationLetters { get; set; } = new List<AppreciationLetter>();
    public ICollection<EmployeeMartyr> Martyrs { get; set; } = new List<EmployeeMartyr>();
    public ICollection<EmployeeResidence> Residences { get; set; } = new List<EmployeeResidence>();
    public ICollection<EmployeeVehicle> Vehicles { get; set; } = new List<EmployeeVehicle>();

    public Financial.EmployeeFinancialAccount? FinancialAccount { get; set; }
    public ICollection<Financial.EmployeeBalanceTransaction> FinancialTransactions { get; set; } = new List<Financial.EmployeeBalanceTransaction>();
    public ICollection<EmployeeNationalCard> NationalCards { get; set; } = new List<EmployeeNationalCard>();
    public ICollection<EmployeePassport> Passports { get; set; } = new List<EmployeePassport>();
}
