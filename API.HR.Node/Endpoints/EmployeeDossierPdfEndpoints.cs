using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Pdf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.HR.Node.Endpoints;

public static class EmployeeDossierPdfEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeDossierPdfEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Mobile: Download current employee's dossier PDF
        endpoints.MapGet("/api/employees/me/dossier-pdf", GetMyDossierPdf)
            .WithTags("Employee Dossier PDF")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer)
            .WithName("GetMyEmployeeDossierPdf")
            .WithSummary("توليد وتحميل الأضبارة الرقمية المعتمدة للموظف الحالي بصيغة PDF عالية الدقة لتطبيق الهاتف.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var group = endpoints.MapGroup("/api/employees/{employeeID}/dossier-pdf")
            .WithTags("Employee Dossier PDF")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapGet("", GetEmployeeDossierPdf)
            .WithName("GetEmployeeDossierPdf")
            .WithSummary("Generates and downloads an official institutional employee dossier as a vector PDF via QuestPDF.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetMyDossierPdf(
        IEmployeeDossierPdfService pdfService,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true })
            return Results.Unauthorized();

        var isStaff = user.IsInRole("Admin") || user.IsInRole("HRManager") || user.IsInRole("FormationManager") || user.IsInRole("Supervisor")
            || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && (c.Value == "Admin" || c.Value == "HRManager" || c.Value == "FormationManager" || c.Value == "Supervisor"));

        if (!isStaff)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "تنزيل خط الخدمة والملف الوظيفي متاح للكادر الإداري والمخولين فقط.");
        }

        var claimEmpId = user.FindFirst("employee_id")?.Value;
        if (string.IsNullOrWhiteSpace(claimEmpId))
        {
            var appUser = await userManager.GetUserAsync(user);
            claimEmpId = appUser?.EmployeeID;
        }

        if (string.IsNullOrWhiteSpace(claimEmpId))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "هذا الحساب غير مرتبط برقم موظف.");
        }

        return await GetEmployeeDossierPdf(claimEmpId, pdfService, userManager, httpContext, cancellationToken);
    }

    private static async Task<IResult> GetEmployeeDossierPdf(
        string employeeID,
        IEmployeeDossierPdfService pdfService,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true })
            return Results.Unauthorized();

        var isStaff = user.IsInRole("Admin") || user.IsInRole("HRManager") || user.IsInRole("FormationManager") || user.IsInRole("Supervisor")
            || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && (c.Value == "Admin" || c.Value == "HRManager" || c.Value == "FormationManager" || c.Value == "Supervisor"));

        if (!isStaff)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "تنزيل خط الخدمة والملف الوظيفي متاح للكادر الإداري والمخولين فقط.");
        }

        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";

        try
        {
            var pdfBytes = await pdfService.GenerateDossierPdfByEmployeeIdNumberAsync(employeeID, baseUrl, cancellationToken);
            var safeFilename = $"الملف_الوظيفي_{employeeID}.pdf";

            return Results.File(pdfBytes, "application/pdf", safeFilename);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Employee with ID '{employeeID}' not found." });
        }
    }
}
