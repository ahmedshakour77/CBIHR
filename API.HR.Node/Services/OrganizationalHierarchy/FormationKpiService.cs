using System.Security.Claims;
using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed class FormationKpiService : IFormationKpiService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FormationKpiService> _logger;

    public FormationKpiService(ApplicationDbContext db, ILogger<FormationKpiService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool IsAdminOrGlobal, int? LockedFormationId, List<FormationOptionDto> AvailableFormations)> GetUserFormationAccessAsync(ClaimsPrincipal user)
    {
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("HRManager");

        // 1. If Admin or HRManager, full access across all formations
        if (isAdmin)
        {
            var formations = await _db.Formations
                .AsNoTracking()
                .Select(f => new FormationOptionDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    EmployeeCount = _db.Employees.Count(e => e.FormationId == f.Id && !e.IsArchived)
                })
                .OrderByDescending(f => f.EmployeeCount)
                .ThenBy(f => f.Name)
                .ToListAsync();

            return (true, null, formations);
        }

        // 2. Non-admin: Determine the formation bound to this user
        int? userFormationId = null;

        // Check formation claim first
        var claimVal = user.FindFirst("formation_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimVal) && int.TryParse(claimVal, out var cid))
        {
            userFormationId = cid;
        }

        // If not in claims, check database user record
        if (!userFormationId.HasValue)
        {
            var userName = user.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(userName))
            {
                var appUser = await _db.Users
                    .AsNoTracking()
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.UserName == userName || u.Email == userName);

                if (appUser != null)
                {
                    userFormationId = appUser.FormationId ?? appUser.Employee?.FormationId;
                }
            }
        }

        // Fallback to employee_id claim
        if (!userFormationId.HasValue)
        {
            var empId = user.FindFirst("employee_id")?.Value;
            if (!string.IsNullOrWhiteSpace(empId))
            {
                var emp = await _db.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EmployeeID == empId);
                userFormationId = emp?.FormationId;
            }
        }

        if (userFormationId.HasValue)
        {
            var lockedFormation = await _db.Formations
                .AsNoTracking()
                .Where(f => f.Id == userFormationId.Value)
                .Select(f => new FormationOptionDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    EmployeeCount = _db.Employees.Count(e => e.FormationId == f.Id && !e.IsArchived)
                })
                .ToListAsync();

            return (false, userFormationId, lockedFormation);
        }

        // Default empty if completely unbound
        return (false, null, new List<FormationOptionDto>());
    }

    public async Task<FormationKpiReportDto> GetFormationKpiReportAsync(int? formationId)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        string formationTitle = "كافة تشكيلات البنك المركزي";
        if (formationId.HasValue)
        {
            var formationEntity = await _db.Formations.AsNoTracking().FirstOrDefaultAsync(f => f.Id == formationId.Value);
            formationTitle = formationEntity?.Name ?? $"التشكيل #{formationId.Value}";
        }

        // Query employees
        var query = _db.Employees
            .AsNoTracking()
            .Include(e => e.Formation)
            .Include(e => e.Grade)
            .Include(e => e.Stage)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.EducationLevel)
            .Include(e => e.Gender)
            .Include(e => e.EmployeeStatus)
            .Include(e => e.AppreciationLetters)
            .Include(e => e.Penalties)
            .Include(e => e.Vehicles)
            .Where(e => !e.IsArchived);

        if (formationId.HasValue)
        {
            query = query.Where(e => e.FormationId == formationId.Value);
        }

        var employees = await query.ToListAsync();
        var totalStaff = employees.Count;

        if (totalStaff == 0)
        {
            return new FormationKpiReportDto
            {
                FormationId = formationId,
                FormationName = formationTitle,
                GeneratedAt = now
            };
        }

        // Gender breakdown
        var maleCount = employees.Count(e => e.Gender?.Name != null && e.Gender.Name.Contains("ذكر"));
        var femaleCount = employees.Count(e => e.Gender?.Name != null && (e.Gender.Name.Contains("أنثى") || e.Gender.Name.Contains("انثى")));
        if (maleCount + femaleCount < totalStaff)
        {
            // Allocate remainder if gender lookup differed
            var unassigned = totalStaff - (maleCount + femaleCount);
            maleCount += unassigned;
        }
        var malePct = Math.Round((double)maleCount / totalStaff * 100, 1);
        var femalePct = Math.Round((double)femaleCount / totalStaff * 100, 1);

        // Active vs others
        var activeCount = employees.Count(e => e.EmployeeStatus == null || e.EmployeeStatus.Name.Contains("مستمر") || e.EmployeeStatus.Name.Contains("فعال"));
        if (activeCount == 0) activeCount = totalStaff;

        // Contract staff (موظفو العقود بحسب حالة الموظف)
        var contractCount = employees.Count(e => e.EmployeeStatus != null &&
            (e.EmployeeStatus.Name.Contains("عقد") || e.EmployeeStatus.Name.Contains("عقود") || e.EmployeeStatus.Name.Contains("مؤقت") || e.EmployeeStatus.Name.Contains("أجر")));
        var contractPct = totalStaff > 0 ? Math.Round((double)contractCount / totalStaff * 100, 1) : 0;

        // New hires this year
        var newHiresThisYear = employees.Count(e => e.HireDate.HasValue && e.HireDate.Value.Year == now.Year);

        // Approaching retirement (within 2 years of 60, i.e., age >= 58)
        var approachingRetirement = employees.Count(e =>
        {
            if (!e.BirthDate.HasValue) return false;
            var age = CalculateYears(e.BirthDate.Value, today);
            return age >= 58;
        });

        // Eligible for next title / allowance (service in stage >= 1 year or in grade >= 4-5 years)
        var eligibleForPromotion = employees.Count(e =>
        {
            if (e.LastJobTitleDate.HasValue && CalculateYears(e.LastJobTitleDate.Value, today) >= 4) return true;
            if (e.LastAllowanceDate.HasValue && CalculateYears(e.LastAllowanceDate.Value, today) >= 1) return true;
            return false;
        });

        // Letters & Penalties
        var totalAppreciationLetters = employees.Sum(e => e.AppreciationLetters.Count);
        var totalPenalties = employees.Sum(e => e.Penalties.Count);
        var cleanDisciplineCount = employees.Count(e => e.Penalties.Count == 0);
        var cleanDisciplinePct = Math.Round((double)cleanDisciplineCount / totalStaff * 100, 1);

        // Leaves & Service tenure averages
        var totalLeaveDays = employees.Sum(e => e.LeaveBalance ?? 0m);
        var avgLeaveDays = Math.Round((double)totalLeaveDays / totalStaff, 1);

        var ages = employees.Where(e => e.BirthDate.HasValue).Select(e => CalculateYears(e.BirthDate!.Value, today)).ToList();
        var avgAge = ages.Count > 0 ? Math.Round(ages.Average(), 1) : 0;

        var services = employees.Where(e => e.HireDate.HasValue).Select(e => CalculateYears(e.HireDate!.Value, today)).ToList();
        var avgService = services.Count > 0 ? Math.Round(services.Average(), 1) : 0;

        // Digital photo tracking & Leave tracking IDs
        var empIntIds = employees.Select(e => e.Id).ToList();
        var empStringIds = employees.Select(e => e.EmployeeID).ToList();
        var photosCount = await _db.EmployeeProfilePhotos
            .AsNoTracking()
            .Where(p => empStringIds.Contains(p.EmployeeID) && p.IsActive)
            .Select(p => p.EmployeeID)
            .Distinct()
            .CountAsync();
        var photosPct = Math.Round((double)photosCount / totalStaff * 100, 1);

        // Vehicles & Gates Breakdown
        var allVehicles = employees.SelectMany(e => e.Vehicles).ToList();
        var totalVehicles = allVehicles.Count;
        var authorizedVehicles = allVehicles.Count(v => v.IsAuthorized);
        var mainBldAccess = allVehicles.Count(v => v.AllowMainBuilding);
        var jadriyaAccess = allVehicles.Count(v => v.AllowJadriya);
        var erbilAccess = allVehicles.Count(v => v.AllowErbil);
        var mosulAccess = allVehicles.Count(v => v.AllowMosul);
        var basraAccess = allVehicles.Count(v => v.AllowBasra);

        var vehiclesStats = new FormationVehicleStatDto
        {
            TotalVehicles = totalVehicles,
            AuthorizedVehicles = authorizedVehicles,
            MainBuildingAccess = mainBldAccess,
            JadriyaAccess = jadriyaAccess,
            ErbilAccess = erbilAccess,
            MosulAccess = mosulAccess,
            BasraAccess = basraAccess
        };

        // 1. Age Pyramid Series
        var agePyramid = BuildAgePyramid(employees, today, totalStaff);

        // 2. Education Distribution
        var eduDistribution = BuildEducationDistribution(employees, totalStaff);

        // 3. Grade Distribution (1 to 10)
        var gradeDistribution = BuildGradeDistribution(employees, totalStaff);

        // 4. Service Tenure Distribution
        var tenureDistribution = BuildTenureDistribution(employees, today, totalStaff);

        // 5. Leave Types Distribution
        var leaveTypesDistribution = await BuildLeaveTypesDistributionAsync(empIntIds);

        // 6. Top Job Titles
        var topJobTitles = employees
            .GroupBy(e => e.JobTitle?.Name ?? "غير محدد")
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => new DistributionItemDto
            {
                Key = g.Key,
                Label = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((double)g.Count() / totalStaff * 100, 1)
            })
            .ToList();

        // 7. Staff Roster
        var roster = employees
            .OrderBy(e => e.Name)
            .Select(e =>
            {
                var age = e.BirthDate.HasValue ? (int)CalculateYears(e.BirthDate.Value, today) : 0;
                var sYears = e.HireDate.HasValue ? Math.Round(CalculateYears(e.HireDate.Value, today), 1) : 0;
                var hasAuthVeh = e.Vehicles.Any(v => v.IsAuthorized);

                return new FormationStaffRosterItemDto
                {
                    EmployeeID = e.EmployeeID,
                    Name = e.Name,
                    JobTitle = e.JobTitle?.Name,
                    PositionTitle = e.PositionTitle?.Name,
                    FormationName = e.Formation?.Name,
                    GradeName = e.Grade?.Name,
                    StageName = e.Stage?.Name,
                    EducationLevel = e.EducationLevel?.Name,
                    Gender = e.Gender?.Name,
                    Age = age,
                    ServiceYears = sYears,
                    HireDate = e.HireDate,
                    LastJobTitleDate = e.LastJobTitleDate,
                    LastAllowanceDate = e.LastAllowanceDate,
                    AppreciationCount = e.AppreciationLetters.Count,
                    PenaltyCount = e.Penalties.Count,
                    VehicleCount = e.Vehicles.Count,
                    HasAuthorizedVehicle = hasAuthVeh,
                    LeaveBalance = e.LeaveBalance,
                    Status = e.EmployeeStatus?.Name ?? "مستمر",
                    ApproachingRetirement = age >= 58
                };
            })
            .ToList();

        return new FormationKpiReportDto
        {
            FormationId = formationId,
            FormationName = formationTitle,
            GeneratedAt = now,
            TotalStaff = totalStaff,
            ActiveStaff = activeCount,
            MaleCount = maleCount,
            FemaleCount = femaleCount,
            MalePercentage = malePct,
            FemalePercentage = femalePct,
            NewHiresThisYear = newHiresThisYear,
            ApproachingRetirementCount = approachingRetirement,
            EligibleForPromotionCount = eligibleForPromotion,
            TotalAppreciationLetters = totalAppreciationLetters,
            TotalPenalties = totalPenalties,
            CleanDisciplinePercentage = cleanDisciplinePct,
            TotalLeaveBalanceDays = totalLeaveDays,
            AvgLeaveBalanceDays = avgLeaveDays,
            AvgServiceYears = avgService,
            AvgAgeYears = avgAge,
            DigitalPhotoCount = photosCount,
            DigitalPhotoPercentage = photosPct,
            ContractStaffCount = contractCount,
            ContractStaffPercentage = contractPct,
            AgePyramid = agePyramid,
            EducationDistribution = eduDistribution,
            GradeDistribution = gradeDistribution,
            ServiceTenureDistribution = tenureDistribution,
            LeaveTypesDistribution = leaveTypesDistribution,
            TopJobTitles = topJobTitles,
            VehiclesStats = vehiclesStats,
            StaffRoster = roster
        };
    }

    private static double CalculateYears(DateOnly start, DateOnly end)
    {
        var years = end.Year - start.Year;
        if (end.DayOfYear < start.DayOfYear) years--;
        return Math.Max(0, years);
    }

    private static List<DemographicBarItemDto> BuildAgePyramid(List<Employee> emps, DateOnly today, int total)
    {
        var brackets = new (string label, int min, int max)[]
        {
            ("دون 24 سنة", 0, 23),
            ("24 - 25 سنة", 24, 25),
            ("26 - 27 سنة", 26, 27),
            ("28 - 29 سنة", 28, 29),
            ("30 - 31 سنة", 30, 31),
            ("32 - 33 سنة", 32, 33),
            ("34 - 35 سنة", 34, 35),
            ("36 - 37 سنة", 36, 37),
            ("38 - 39 سنة", 38, 39),
            ("40 - 41 سنة", 40, 41),
            ("42 - 43 سنة", 42, 43),
            ("44 - 45 سنة", 44, 45),
            ("46 - 47 سنة", 46, 47),
            ("48 - 49 سنة", 48, 49),
            ("50 - 51 سنة", 50, 51),
            ("52 - 53 سنة", 52, 53),
            ("54 - 55 سنة", 54, 55),
            ("56 - 57 سنة", 56, 57),
            ("58 - 59 سنة", 58, 59),
            ("60 سنة فما فوق", 60, 150)
        };

        var result = new List<DemographicBarItemDto>();
        foreach (var b in brackets)
        {
            var inBracket = emps.Where(e =>
            {
                if (!e.BirthDate.HasValue) return false;
                var age = CalculateYears(e.BirthDate.Value, today);
                return age >= b.min && age <= b.max;
            }).ToList();

            var male = inBracket.Count(e => e.Gender?.Name != null && e.Gender.Name.Contains("ذكر"));
            var female = inBracket.Count - male;
            var pct = total > 0 ? Math.Round((double)inBracket.Count / total * 100, 1) : 0;

            result.Add(new DemographicBarItemDto
            {
                Label = b.label,
                MaleCount = male,
                FemaleCount = female,
                Percentage = pct
            });
        }
        return result;
    }

    private static List<DistributionItemDto> BuildEducationDistribution(List<Employee> emps, int total)
    {
        var colors = new[] { "var(--accent)", "var(--ok)", "var(--warn)", "var(--series-4, #8b5cf6)", "var(--series-5, #06b6d4)", "var(--series-6, #f97316)", "var(--muted)" };
        var grouped = emps
            .GroupBy(e => NormalizeEducation(e.EducationLevel?.Name))
            .OrderByDescending(g => g.Count())
            .ToList();

        var list = new List<DistributionItemDto>();
        int idx = 0;
        foreach (var g in grouped)
        {
            list.Add(new DistributionItemDto
            {
                Key = g.Key,
                Label = g.Key,
                Count = g.Count(),
                Percentage = total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0,
                ColorToken = colors[idx % colors.Length]
            });
            idx++;
        }
        return list;
    }

    private static string NormalizeEducation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "غير محدد";
        var r = raw.Trim();
        if (r.Contains("دكتوراه")) return "دكتوراه";
        if (r.Contains("ماجستير")) return "ماجستير";
        if (r.Contains("دبلوم عالي")) return "دبلوم عالي";
        if (r.Contains("بكالوريوس")) return "بكالوريوس";
        if (r.Contains("دبلوم")) return "دبلوم";
        if (r.Contains("إعدادي") || r.Contains("اعدادي")) return "إعدادية";
        if (r.Contains("متوسط")) return "متوسطة";
        if (r.Contains("ابتدائ") || r.Contains("ابتدائي")) return "ابتدائية";
        return r;
    }

    private static List<DistributionItemDto> BuildGradeDistribution(List<Employee> emps, int total)
    {
        var grades = Enumerable.Range(1, 10).Select(i => $"الدرجة {i}").ToList();
        var colors = new[] { "#3b82f6", "#6366f1", "#8b5cf6", "#ec4899", "#f43f5e", "#f97316", "#eab308", "#10b981", "#14b8a6", "#06b6d4" };

        var list = new List<DistributionItemDto>();
        for (int i = 1; i <= 10; i++)
        {
            int gradeNum = i;
            var c = emps.Count(e =>
            {
                if (e.Grade == null) return false;
                if (e.Grade.SortOrder == gradeNum) return true;
                if (int.TryParse(e.Grade.Code, out var gc) && gc == gradeNum) return true;
                return e.Grade.Name.Contains(gradeNum.ToString()) || (gradeNum == 1 && (e.Grade.Name.Contains("الأولى") || e.Grade.Name.Contains("الاولى")));
            });

            list.Add(new DistributionItemDto
            {
                Key = $"Grade{gradeNum}",
                Label = grades[i - 1],
                Count = c,
                Percentage = total > 0 ? Math.Round((double)c / total * 100, 1) : 0,
                ColorToken = colors[i - 1]
            });
        }
        return list;
    }

    private static List<DistributionItemDto> BuildTenureDistribution(List<Employee> emps, DateOnly today, int total)
    {
        var brackets = new (string label, int min, int max, string color)[]
        {
            ("أقل من 5 سنوات", 0, 4, "#10b981"),
            ("5 إلى 9 سنوات", 5, 9, "#3b82f6"),
            ("10 إلى 14 سنة", 10, 14, "#6366f1"),
            ("15 إلى 19 سنة", 15, 19, "#8b5cf6"),
            ("20 إلى 24 سنة", 20, 24, "#f59e0b"),
            ("25 سنة فما فوق", 25, 100, "#ef4444")
        };

        var list = new List<DistributionItemDto>();
        foreach (var b in brackets)
        {
            var count = emps.Count(e =>
            {
                if (!e.HireDate.HasValue) return false;
                var tenure = CalculateYears(e.HireDate.Value, today);
                return tenure >= b.min && tenure <= b.max;
            });

            list.Add(new DistributionItemDto
            {
                Key = b.label,
                Label = b.label,
                Count = count,
                Percentage = total > 0 ? Math.Round((double)count / total * 100, 1) : 0,
                ColorToken = b.color
            });
        }
        return list;
    }

    private async Task<List<DistributionItemDto>> BuildLeaveTypesDistributionAsync(List<int> empIds)
    {
        if (empIds.Count == 0) return new List<DistributionItemDto>();

        var leaves = await _db.EmployeeLeaves
            .AsNoTracking()
            .Include(l => l.LeaveType)
            .Where(l => empIds.Contains(l.EmployeeId))
            .GroupBy(l => l.LeaveType != null ? l.LeaveType.Name : "أخرى")
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Count)
            .Take(6)
            .ToListAsync();

        var totalLeaves = leaves.Sum(l => l.Count);
        return leaves.Select(l => new DistributionItemDto
        {
            Key = l.Type,
            Label = l.Type,
            Count = l.Count,
            Percentage = totalLeaves > 0 ? Math.Round((double)l.Count / totalLeaves * 100, 1) : 0
        }).ToList();
    }
}
