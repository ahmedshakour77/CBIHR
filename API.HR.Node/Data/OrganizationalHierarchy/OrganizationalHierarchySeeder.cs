using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Data.OrganizationalHierarchy;

public static class OrganizationalHierarchySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("OrganizationalHierarchySeeder");

        // Seed baseline unit types if none exist
        if (!await context.AdministrativeUnitTypes.AnyAsync())
        {
            var types = new List<AdministrativeUnitType>
            {
                new() { Name = "المحافظ", NameKu = "پارێزگار", Code = "GOV", HierarchyLevel = 1, Description = "رأس الهرم الإداري والقيادي الأعلى للمحافظة" },
                new() { Name = "دائرة", NameKu = "فەرمانگە", Code = "DEPT", HierarchyLevel = 2, Description = "تشكيل رئيسي يتفرع إلى أقسام متخصصة" },
                new() { Name = "مديرية", NameKu = "بەڕێوەبەرایەتی", Code = "DIR", HierarchyLevel = 3, Description = "جهاز إداري وتنفيذي قطاعي" },
                new() { Name = "قسم", NameKu = "بەش", Code = "DIV", HierarchyLevel = 4, Description = "وحدة تنظيمية لإدارة مجال محدد" },
                new() { Name = "شعبة", NameKu = "هۆبە", Code = "SEC", HierarchyLevel = 5, Description = "وحدة تابعة للقسم تتولى مهام تنفيذية مباشرة" },
                new() { Name = "وحدة", NameKu = "یەکە", Code = "UNIT", HierarchyLevel = 6, Description = "أصغر تشكيل وظيفي تخصصي محدد" },
                new() { Name = "مكتب", NameKu = "نووسینگە", Code = "OFF", HierarchyLevel = 7, Description = "وحدة إسناد ومتابعة تابعة للمسؤول" },
                new() { Name = "فرع", NameKu = "لق", Code = "BRN", HierarchyLevel = 8, Description = "امتداد جغرافي أو تشغيلي للتشكيل" }
            };

            await context.AdministrativeUnitTypes.AddRangeAsync(types);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded baseline Administrative Unit Types successfully.");
        }
    }
}
