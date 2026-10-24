using API.HR.Node.Contracts;
using API.HR.Node.Security;
using API.HR.Node.Services.Salary;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API.HR.Node.Endpoints;

public static class SalaryScaleEndpoints
{
    public static IEndpointRouteBuilder MapSalaryScaleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/salary-scale")
            .WithTags("Salary Scale")
            .RequireAuthorization(Permissions.SalariesView);

        group.MapGet("/matrix", async (ISalaryScaleService service, CancellationToken ct) =>
        {
            var matrix = await service.GetMatrixAsync(ct);
            return Results.Ok(matrix);
        })
        .WithName("GetSalaryScaleMatrix")
        .WithSummary("الحصول على مصفوفة السلم الوظيفي وجدول الدرجات والمراحل مع أعداد الموظفين");

        group.MapGet("/", async (ISalaryScaleService service, CancellationToken ct) =>
        {
            var scales = await service.GetAllScalesAsync(ct);
            return Results.Ok(scales);
        })
        .WithName("GetAllSalaryScales")
        .WithSummary("الحصول على قائمة السلم الوظيفي الكاملة (154 مدخلاً)");

        group.MapGet("/lookup", async (string gradeCode, string stageCode, ISalaryScaleService service, CancellationToken ct) =>
        {
            var scale = await service.GetScaleByCodesAsync(gradeCode, stageCode, ct);
            return scale != null ? Results.Ok(scale) : Results.NotFound();
        })
        .WithName("GetSalaryScaleByCodes")
        .WithSummary("البحث في السلم الوظيفي بالاعتماد على الحقل النصي للدرجة والمرحلة");

        group.MapPost("/seed", async (ISalaryScaleService service, CancellationToken ct) =>
        {
            await service.EnsureSeededAsync(ct);
            return Results.Ok(new { message = "تمت تهيئة ومزامنة السلم الوظيفي بنجاح" });
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("SeedSalaryScale")
        .WithSummary("تهيئة ومزامنة السلم الوظيفي في قاعدة البيانات للمدير");

        return endpoints;
    }
}
