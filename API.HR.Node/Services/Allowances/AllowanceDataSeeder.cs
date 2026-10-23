using API.HR.Node.Data;
using API.HR.Node.Data.Allowances;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Allowances;

public static class AllowanceDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.AllowanceTypes.AnyAsync())
        {
            var needsSave = false;

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "BANKING"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "BANKING",
                    Name = "المخصصات المصرفية",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 25m,
                    Description = "المخصصات المصرفية ومسؤولية العمل المصرفي المعتمدة في البنك المركزي العراقي",
                    SortOrder = 0
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "ENGINEERING"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "ENGINEERING",
                    Name = "المخصصات الهندسية",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 35m,
                    Description = "المخصصات الهندسية المعتمدة للملاكات الهندسية والفنية",
                    SortOrder = 1
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "LEGAL"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "LEGAL",
                    Name = "المخصصات القانونية",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 35m,
                    Description = "المخصصات القانونية المعتمدة بنسبة 35% للملاكات والحقوقيين",
                    SortOrder = 2
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "CERT"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "CERT",
                    Name = "مخصصات الشهادة",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 45m,
                    Description = "مخصصات الشهادة الأكاديمية والمؤهل العلمي بحسب سلم الشهادات المعتمد",
                    SortOrder = 3
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "NON_PRACTICE"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "NON_PRACTICE",
                    Name = "مخصصات عدم مزاولة مهنة",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 20m,
                    Description = "مخصصات عدم مزاولة المهنة المعتمدة للملاكات التخصصية كنسبة مئوية من الراتب الاسمي",
                    SortOrder = 4
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "CRAFT"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "CRAFT",
                    Name = "مخصصات الحرفة",
                    CalculationType = AllowanceCalculationType.Percentage,
                    DefaultPercentage = 15m,
                    Description = "مخصصات الحرفة المعتمدة للملاكات المهنية والحرفية كنسبة مئوية من الراتب الاسمي",
                    SortOrder = 5
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "MARRIAGE"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "MARRIAGE",
                    Name = "المخصصات الزوجية",
                    CalculationType = AllowanceCalculationType.Fixed,
                    DefaultAmount = 50000m,
                    Description = "مخصصات مقطوعة تمنح للموظف المتزوج بموجب عقد زواج رسمي مصدق (50,000 د.ع)",
                    SortOrder = 6
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "CHILDREN"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "CHILDREN",
                    Name = "مخصصات الأطفال والأسرة",
                    CalculationType = AllowanceCalculationType.Fixed,
                    DefaultAmount = 10000m,
                    Description = "مخصصات مقطوعة تمنح عن كل طفل مستحق قانوناً (10,000 د.ع لكل طفل)",
                    SortOrder = 7
                });
                needsSave = true;
            }

            if (!await db.AllowanceTypes.AnyAsync(t => t.Code == "HAZARD"))
            {
                db.AllowanceTypes.Add(new AllowanceType
                {
                    Code = "HAZARD",
                    Name = "مخصصات خطورة المهنة وبيئة العمل",
                    CalculationType = AllowanceCalculationType.Fixed,
                    DefaultAmount = 50000m,
                    Description = "مخصصات خطورة مقطوعة للعاملين في البيئات الخطرة والأمنية",
                    SortOrder = 8
                });
                needsSave = true;
            }

            if (needsSave)
            {
                await db.SaveChangesAsync();
            }
            return;
        }

        var types = new List<AllowanceType>
        {
            new()
            {
                Code = "BANKING",
                Name = "المخصصات المصرفية",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 25m,
                Description = "المخصصات المصرفية ومسؤولية العمل المصرفي المعتمدة في البنك المركزي العراقي",
                SortOrder = 0
            },
            new()
            {
                Code = "CERT",
                Name = "مخصصات الشهادة الأكاديمية",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 45m,
                Description = "دكتوراه 100%، ماجستير 75%، دبلوم عالي 65%، بكالوريوس 45%، دبلوم فني 35%، إعدادية 25%",
                SortOrder = 1
            },
            new()
            {
                Code = "HAZARD",
                Name = "مخصصات خطورة المهنة وبيئة العمل",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 20m,
                Description = "تمنح للعاملين في البيئات الخطرة والأمنية والعمليات النقدية (20% - 50%)",
                SortOrder = 2
            },
            new()
            {
                Code = "POSITION",
                Name = "مخصصات المنصب والمسؤولية الإدارية",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 20m,
                Description = "مدير عام 30%، معاون مدير عام 25%، مدير قسم 20%، مسؤول شعبة 15%",
                SortOrder = 3
            },
            new()
            {
                Code = "PROFESSIONAL",
                Name = "مخصصات مهنية / نقابية / تخصصية",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 15m,
                Description = "المخصصات التخصصية للمهندسين والقانونيين والمحاسبين والمهن التخصصية (15%)",
                SortOrder = 4
            },
            new()
            {
                Code = "GEOGRAPHIC",
                Name = "مخصصات الموقع الجغرافي والنأي",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 10m,
                Description = "تمنح للموظفين العاملين في فروع المحافظات أو المناطق النائية (10% - 15%)",
                SortOrder = 5
            },
            new()
            {
                Code = "MARRIAGE",
                Name = "المخصصات الزوجية",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 50000m,
                Description = "مخصصات مقطوعة تمنح للموظف المتزوج بموجب عقد زواج رسمي مصدق (50,000 د.ع)",
                SortOrder = 6
            },
            new()
            {
                Code = "CHILDREN",
                Name = "مخصصات الأطفال والأسرة",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 10000m,
                Description = "مخصصات مقطوعة تمنح عن كل طفل مستحق قانوناً (10,000 د.ع لكل طفل)",
                SortOrder = 7
            },
            new()
            {
                Code = "TRANSPORT",
                Name = "مخصصات النقل والانتقال",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 30000m,
                Description = "مخصصات بدل نقل شهرية مقطوعة للعاملين الذين لا تتوفر لهم خطوط نقل مخصصة (30,000 د.ع)",
                SortOrder = 8
            },
            new()
            {
                Code = "HOSPITALITY",
                Name = "مخصصات الضيافة والتمثيل",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 50000m,
                Description = "مخصصات مقطوعة مخصصة للعناوين الإدارية والقيادية والتنسيق الخارجي",
                SortOrder = 9
            },
            new()
            {
                Code = "LIVING",
                Name = "مخصصات غلاء المعيشة / تحسين الدخل",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 50000m,
                Description = "مخصصات إضافية مقطوعة لتحسين المستوى المعيشي (50,000 د.ع)",
                SortOrder = 10
            },
            new()
            {
                Code = "OTHER_PERCENT",
                Name = "مخصصات نسبية أخرى",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 10m,
                Description = "مخصصات خاصة بموجب قرار مجلس الوزراء أو أمر إداري خاص كنسبة مئوية",
                SortOrder = 11
            },
            new()
            {
                Code = "OTHER_FIXED",
                Name = "مخصصات مقطوعة أخرى",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 25000m,
                Description = "مخصصات مقطوعة بموجب أوامر إدارية أو قرارات خاصة",
                SortOrder = 12
            },
            new()
            {
                Code = "CRAFT",
                Name = "مخصصات الحرفة",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 15m,
                Description = "مخصصات الحرفة المعتمدة للملاكات المهنية والحرفية كنسبة مئوية من الراتب الاسمي",
                SortOrder = 13
            }
        };

        db.AllowanceTypes.AddRange(types);
        await db.SaveChangesAsync();
    }
}
