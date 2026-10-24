using API.HR.Node.Contracts;
using API.HR.Node.Security;
using API.HR.Node.Services.Import;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class ExcelImportEndpoints
{
    public static IEndpointRouteBuilder MapExcelImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/import")
            .WithTags("ExcelImport")
            .RequireAuthorization();

        group.MapPost("/employees/excel", async (
            IFormFile file,
            [FromQuery] string? mode,
            [FromQuery] bool? createMissingLookups,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى تحديد ملف إكسل صالح." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm"))
            {
                return Results.BadRequest(new { message = "الملف يجب أن يكون بصيغة Excel (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseEmployeesExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف الموظفين.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new EmployeeImportRequest(
                    Mode: mode ?? "upsert",
                    Employees: parseResult.Rows,
                    CreateMissingLookups: createMissingLookups ?? true);

                var response = await importService.ImportEmployeesAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("ImportEmployeesFromExcel")
        .WithSummary("استيراد بيانات الموظفين عبر رفع ملف Excel (.xlsx)")
        .RequireAuthorization(AuthorizationPolicies.ImportEmployees);

        group.MapPost("/appreciation-letters/excel", async (
            IFormFile file,
            [FromQuery] string? mode,
            [FromQuery] bool? createMissingLookups,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى تحديد ملف إكسل صالح." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm"))
            {
                return Results.BadRequest(new { message = "الملف يجب أن يكون بصيغة Excel (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseAppreciationLettersExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف كتب الشكر.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new AppreciationLetterImportRequest(
                    Mode: mode ?? "upsert",
                    Letters: parseResult.Rows,
                    CreateMissingLookups: createMissingLookups ?? true);

                var response = await importService.ImportAppreciationLettersAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("ImportAppreciationLettersFromExcel")
        .WithSummary("استيراد كتب الشكر والتقدير عبر رفع ملف Excel (.xlsx)")
        .RequireAuthorization(AuthorizationPolicies.ImportAppreciationLetters);

        group.MapGet("/template/employees", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateEmployeesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_سجلات_الموظفين.xlsx");
        })
        .WithName("DownloadEmployeesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد الموظفين المعتمد");

        group.MapGet("/template/appreciation-letters", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateAppreciationLettersTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_كتب_الشكر.xlsx");
        })
        .WithName("DownloadAppreciationLettersExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد كتب الشكر المعتمد");

        group.MapGet("/template/leaves", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateLeavesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_إجازات_الموظفين.xlsx");
        })
        .WithName("DownloadLeavesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد إجازات الموظفين المعتمد");

        group.MapGet("/template/leave-balances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateLeaveBalancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_أرصدة_الإجازات.xlsx");
        })
        .WithName("DownloadLeaveBalancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد أرصدة إجازات الموظفين المعتمد");

        group.MapGet("/template/civil-data", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCivilDataTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استكمال_بيانات_البطاقة_الموحدة.xlsx");
        })
        .WithName("DownloadCivilDataExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استكمال بيانات البطاقة الموحدة للموظفين");

        group.MapGet("/template/email-accounts", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateEmailAccountsTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_البريد_المؤسسي_للموظفين.xlsx");
        })
        .WithName("DownloadEmailAccountsExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد البريد المؤسسي للموظفين المعتمد");

        group.MapGet("/template/banking-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateBankingAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_المخصصات_المصرفية.xlsx");
        })
        .WithName("DownloadBankingAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد المخصصات المصرفية والمسؤولية");

        group.MapGet("/template/engineering-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateEngineeringAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_المخصصات_الهندسية.xlsx");
        })
        .WithName("DownloadEngineeringAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد المخصصات الهندسية");

        group.MapGet("/template/legal-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateLegalAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_المخصصات_القانونية.xlsx");
        })
        .WithName("DownloadLegalAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد المخصصات القانونية");

        group.MapGet("/template/certificate-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCertificateAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_مخصصات_الشهادة.xlsx");
        })
        .WithName("DownloadCertificateAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد مخصصات الشهادة الأكاديمية");

        group.MapGet("/template/non-practice-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateNonPracticeAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_مخصصات_عدم_مزاولة_المهنة.xlsx");
        })
        .WithName("DownloadNonPracticeAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد مخصصات عدم مزاولة المهنة");

        group.MapGet("/template/craft-social-allowances", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateCraftAndSocialAllowancesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_مخصصات_الحرفة_والاجتماعية_والخطورة.xlsx");
        })
        .WithName("DownloadCraftAndSocialAllowancesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد مخصصات الحرفة والاجتماعية والخطورة");

        group.MapGet("/template/marital-status", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateMaritalStatusTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_الحالة_الاجتماعية.xlsx");
        })
        .WithName("DownloadMaritalStatusExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد الحالة الاجتماعية للموظفين");

        group.MapGet("/template/penalties", (IExcelImportService importService) =>
        {
            var bytes = importService.GeneratePenaltiesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_العقوبات_الانضباطية.xlsx");
        })
        .WithName("DownloadPenaltiesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد العقوبات الانضباطية");

        group.MapGet("/template/martyrs", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateMartyrsTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_ذوي_الشهداء.xlsx");
        })
        .WithName("DownloadMartyrsExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد ذوي الشهداء");

        group.MapGet("/template/financial-accounts", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateFinancialAccountsTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_البيانات_المالية_والحسابات.xlsx");
        })
        .WithName("DownloadFinancialAccountsExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد البيانات المالية والرصيد والقروض والسلف");

        group.MapGet("/template/job-titles", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateJobTitlesTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_العناوين_والعوائل_الوظيفية.xlsx");
        })
        .WithName("DownloadJobTitlesExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد وتصنيف العناوين والعوائل والدرجات الوظيفية");

        group.MapPost("/job-titles/excel", async (
            IFormFile file,
            [FromQuery] bool? autoLinkGrades,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى تحديد ملف إكسل صالح." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm"))
            {
                return Results.BadRequest(new { message = "الملف يجب أن يكون بصيغة Excel (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseJobTitlesExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف العناوين الوظيفية.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new JobTitleImportRequest(
                    Rows: parseResult.Rows,
                    AutoLinkGrades: autoLinkGrades ?? true);

                var response = await importService.ImportJobTitlesAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "حدث خطأ أثناء حفظ العناوين الوظيفية المستوردة",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(AuthorizationPolicies.ImportJobTitles)
        .WithName("ImportJobTitlesExcel")
        .WithSummary("استيراد وتحديث وتصنيف العناوين الوظيفية والعوائل والدرجات من ملف Excel");

        group.MapGet("/template/last-allowance-date", (IExcelImportService importService) =>
        {
            var bytes = importService.GenerateLastAllowanceDateTemplate();
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_تاريخ_اخر_علاوة_سنوية.xlsx");
        })
        .WithName("DownloadLastAllowanceDateExcelTemplate")
        .WithSummary("تحميل نموذج إكسل استيراد تاريخ آخر علاوة سنوية للموظفين");

        group.MapPost("/last-allowance-date/excel", async (
            IFormFile file,
            IExcelImportService importService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "يرجى تحديد ملف إكسل صالح." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xlsm"))
            {
                return Results.BadRequest(new { message = "الملف يجب أن يكون بصيغة Excel (.xlsx)." });
            }

            using var stream = file.OpenReadStream();
            var parseResult = await importService.ParseLastAllowanceDateExcelAsync(stream, ct);

            if (parseResult.HasErrors)
            {
                return Results.BadRequest(new
                {
                    message = "فشل تحليل أعمدة أو بيانات ملف تاريخ العلاوة السنوية.",
                    errors = parseResult.ParseErrors
                });
            }

            if (parseResult.Rows.Count == 0)
            {
                return Results.BadRequest(new { message = "لم يتم العثور على أي صفوف بيانات صالحة للاستيراد." });
            }

            try
            {
                var request = new LastAllowanceDateImportRequest(
                    Rows: parseResult.Rows);

                var response = await importService.ImportLastAllowanceDateAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "حدث خطأ أثناء حفظ تواريخ العلاوة السنوية المستوردة",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(AuthorizationPolicies.ImportLastAllowanceDate)
        .WithName("ImportLastAllowanceDateExcel")
        .WithSummary("استيراد وتحديث تاريخ آخر علاوة سنوية للموظفين من ملف Excel");

        return endpoints;
    }
}
