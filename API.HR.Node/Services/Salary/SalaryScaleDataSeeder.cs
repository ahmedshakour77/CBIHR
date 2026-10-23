using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Salary;

public static class SalaryScaleDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // 1. Ensure "الدرجة الخاصة أ" exists and is properly named in Grades
        var khasaA = await db.Grades.FirstOrDefaultAsync(g => g.Name.Contains("الخاصة أ") || g.Name.Contains("الخاصه أ") || g.Code == "الخاصة أ" || g.Id == 14);
        if (khasaA != null)
        {
            khasaA.Name = "الدرجة الخاصة أ";
            khasaA.Code = "الخاصة أ";
            khasaA.SortOrder = 3;
        }
        else
        {
            khasaA = new Grade { Name = "الدرجة الخاصة أ", Code = "الخاصة أ", SortOrder = 3 };
            db.Grades.Add(khasaA);
        }
        await db.SaveChangesAsync();

        // 2. Update Code and SortOrder for all Grades
        var grades = await db.Grades.ToListAsync();
        foreach (var g in grades)
        {
            var clean = g.Name.Trim();
            if (clean.Contains("العليا أ") || clean == "العليا أ")
            {
                g.Code = "العليا أ";
                g.SortOrder = 1;
            }
            else if (clean.Contains("العليا ب") || clean == "العليا ب")
            {
                g.Code = "العليا ب";
                g.SortOrder = 2;
            }
            else if (clean.Contains("الخاصة أ") || clean == "الخاصة أ")
            {
                g.Code = "الخاصة أ";
                g.SortOrder = 3;
            }
            else if (clean.Contains("الخاصة ب") || clean == "الخاصة ب")
            {
                g.Code = "الخاصة ب";
                g.SortOrder = 4;
            }
            else if (clean.Contains("اولى") || clean.Contains("الأولى") || clean.Contains("الاولى") || clean == "1")
            {
                g.Code = "1";
                g.SortOrder = 5;
            }
            else if (clean.Contains("ثانية") || clean.Contains("الثانية") || clean == "2")
            {
                g.Code = "2";
                g.SortOrder = 6;
            }
            else if (clean.Contains("ثالثة") || clean.Contains("الثالثة") || clean == "3")
            {
                g.Code = "3";
                g.SortOrder = 7;
            }
            else if (clean.Contains("رابعة") || clean.Contains("الرابعة") || clean == "4")
            {
                g.Code = "4";
                g.SortOrder = 8;
            }
            else if (clean.Contains("خامسة") || clean.Contains("الخامسة") || clean == "5")
            {
                g.Code = "5";
                g.SortOrder = 9;
            }
            else if (clean.Contains("سادسة") || clean.Contains("السادسة") || clean == "6")
            {
                g.Code = "6";
                g.SortOrder = 10;
            }
            else if (clean.Contains("سابعة") || clean.Contains("السابعة") || clean == "7")
            {
                g.Code = "7";
                g.SortOrder = 11;
            }
            else if (clean.Contains("ثامنة") || clean.Contains("الثامنة") || clean == "8")
            {
                g.Code = "8";
                g.SortOrder = 12;
            }
            else if (clean.Contains("تاسعة") || clean.Contains("التاسعة") || clean == "9")
            {
                g.Code = "9";
                g.SortOrder = 13;
            }
            else if (clean.Contains("عاشرة") || clean.Contains("العاشرة") || clean == "10")
            {
                g.Code = "10";
                g.SortOrder = 14;
            }
        }
        await db.SaveChangesAsync();

        // 3. Update Code and StageNumber for all Stages
        var stages = await db.Stages.ToListAsync();
        foreach (var s in stages)
        {
            var clean = s.Name.Trim();
            if (clean.Contains("حادية عشر") || clean.Contains("الحادية عشر") || clean == "11")
            {
                s.Code = "11";
                s.StageNumber = 11;
            }
            else if (clean.Contains("عاشرة") || clean.Contains("العاشرة") || clean == "10")
            {
                s.Code = "10";
                s.StageNumber = 10;
            }
            else if (clean.Contains("تاسعة") || clean.Contains("التاسعة") || clean == "9")
            {
                s.Code = "9";
                s.StageNumber = 9;
            }
            else if (clean.Contains("ثامنة") || clean.Contains("الثامنة") || clean == "8")
            {
                s.Code = "8";
                s.StageNumber = 8;
            }
            else if (clean.Contains("سابعة") || clean.Contains("السابعة") || clean == "7")
            {
                s.Code = "7";
                s.StageNumber = 7;
            }
            else if (clean.Contains("سادسة") || clean.Contains("السادسة") || clean == "6")
            {
                s.Code = "6";
                s.StageNumber = 6;
            }
            else if (clean.Contains("خامسة") || clean.Contains("الخامسة") || clean == "5")
            {
                s.Code = "5";
                s.StageNumber = 5;
            }
            else if (clean.Contains("رابعة") || clean.Contains("الرابعة") || clean == "4")
            {
                s.Code = "4";
                s.StageNumber = 4;
            }
            else if (clean.Contains("ثالثة") || clean.Contains("الثالثة") || clean == "3")
            {
                s.Code = "3";
                s.StageNumber = 3;
            }
            else if (clean.Contains("ثانية") || clean.Contains("الثانية") || clean == "2")
            {
                s.Code = "2";
                s.StageNumber = 2;
            }
            else if (clean.Contains("أولى") || clean.Contains("الأولى") || clean.Contains("اولى") || clean.Contains("الاولى") || clean == "1")
            {
                s.Code = "1";
                s.StageNumber = 1;
            }
        }
        await db.SaveChangesAsync();

        // 4. Check if SalaryScales already populated
        var existingCount = await db.SalaryScales.CountAsync();
        if (existingCount >= 154)
        {
            return;
        }

        // Define the Scale Data based on the attached document
        // Grade Code, Allowance, ServiceYears, Stage1 Base Salary, SortOrder
        var scaleDefinitions = new (string GradeCode, decimal? Allowance, int? ServiceYears, decimal Stage1Salary, int SortOrder)[]
        {
            ("العليا أ", null, null, 4000m, 1),
            ("العليا ب", 83m, null, 2413m, 2),
            ("الخاصة أ", 83m, null, 2000m, 3),
            ("الخاصة ب", 83m, null, 1500m, 4),
            ("1", 20m, null, 910m, 5),
            ("2", 17m, 5, 723m, 6),
            ("3", 10m, 5, 600m, 7),
            ("4", 8m, 5, 509m, 8),
            ("5", 6m, 5, 429m, 9),
            ("6", 6m, 4, 362m, 10),
            ("7", 6m, 4, 296m, 11),
            ("8", 3m, 4, 260m, 12),
            ("9", 3m, 4, 210m, 13),
            ("10", 3m, 4, 170m, 14),
        };

        var allGrades = await db.Grades.ToListAsync();
        var allStages = await db.Stages.ToListAsync();

        var existingScales = await db.SalaryScales.ToListAsync();

        foreach (var def in scaleDefinitions)
        {
            var grade = allGrades.FirstOrDefault(g => g.Code == def.GradeCode);
            if (grade == null) continue;

            for (int stageNum = 1; stageNum <= 11; stageNum++)
            {
                var stage = allStages.FirstOrDefault(s => s.StageNumber == stageNum || s.Code == stageNum.ToString());
                if (stage == null) continue;

                var existing = existingScales.FirstOrDefault(s => s.GradeId == grade.Id && s.StageId == stage.Id);
                decimal baseSalary;

                if (def.GradeCode == "العليا أ")
                {
                    baseSalary = 4000m; // All 11 stages are 4000
                }
                else
                {
                    baseSalary = def.Stage1Salary + ((stageNum - 1) * (def.Allowance ?? 0m));
                }

                if (existing != null)
                {
                    existing.GradeCode = def.GradeCode;
                    existing.StageCode = stageNum.ToString();
                    existing.StageNumber = stageNum;
                    existing.GradeSortOrder = def.SortOrder;
                    existing.BaseSalary = baseSalary;
                    existing.AnnualAllowance = def.Allowance;
                    existing.RequiredServiceYears = def.ServiceYears;
                }
                else
                {
                    db.SalaryScales.Add(new SalaryScale
                    {
                        GradeId = grade.Id,
                        StageId = stage.Id,
                        GradeCode = def.GradeCode,
                        StageCode = stageNum.ToString(),
                        StageNumber = stageNum,
                        GradeSortOrder = def.SortOrder,
                        BaseSalary = baseSalary,
                        AnnualAllowance = def.Allowance,
                        RequiredServiceYears = def.ServiceYears
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
