using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.IdentityDocuments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public static class NationalCardEndpoints
{
    public static IEndpointRouteBuilder MapNationalCardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/national-cards")
            .WithTags("NationalIdentityCards")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // 1. Search card by Document Number or National ID (12-digit)
        group.MapGet("/by-document/{query}", async (
            string query,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var card = await cardService.GetByDocumentOrNationalIdAsync(query, ct);
            if (card is null)
            {
                return Results.NotFound(new { message = "لم يتم العثور على بطاقة وطنية موحدة مطابقة للمعرّف المدخل." });
            }

            var hasCardViewPerm = principal.IsInRole("Admin") ||
                                  principal.HasPermission(Permissions.NationalCardsView) ||
                                  principal.HasPermission(Permissions.NationalCardsEdit);

            // If not admin or holder of NationalCardsView/Edit permission, verify caller owns this card
            if (!hasCardViewPerm)
            {
                var currentEmployee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
                if (currentEmployee is null || currentEmployee.Id != card.EmployeeId)
                {
                    return Results.NotFound(new { message = "لم يتم العثور على السجل أو غير مصرح بعرضه." });
                }
            }

            return Results.Ok(card);
        })
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithName("GetNationalCardByDocumentOrId")
        .WithSummary("البحث عن البطاقة الموحدة بواسطة رقم الوثيقة أو الرقم الوطني الموحد");

        // 2. Stream DG2 Biometric Face Image
        group.MapGet("/{id:int}/biometric-photo", (
            int id,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            INationalCardService cardService,
            CancellationToken ct) => GetNationalCardImageAsync(id, "biometric-photo", principal, userManager, db, cardService, ct))
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithName("GetNationalCardBiometricPhoto")
        .WithSummary("استرجاع وعرض الصورة الشخصية البيومترية المستخرجة من شريحة البطاقة (DG2 Face Image)");

        // 3. Stream Front Image
        group.MapGet("/{id:int}/front-image", (
            int id,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            INationalCardService cardService,
            CancellationToken ct) => GetNationalCardImageAsync(id, "front-image", principal, userManager, db, cardService, ct))
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithName("GetNationalCardFrontImage")
        .WithSummary("استرجاع وعرض صورة واجهة البطاقة الموحدة من الأمام");

        // 4. Stream Back Image
        group.MapGet("/{id:int}/back-image", (
            int id,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            INationalCardService cardService,
            CancellationToken ct) => GetNationalCardImageAsync(id, "back-image", principal, userManager, db, cardService, ct))
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithName("GetNationalCardBackImage")
        .WithSummary("استرجاع وعرض صورة ظهر البطاقة الموحدة من الخلف");

        // 5. Parse and Validate MRZ Text
        group.MapPost("/parse-mrz", (
            [FromBody] MrzParseRequest request,
            INationalCardService cardService) =>
        {
            var result = cardService.ParseMrz(request.MrzText);
            return Results.Ok(result);
        })
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithName("ParseNationalCardMrz")
        .WithSummary("فك تشفير وتدقيق أسطر الـ MRZ لمعيار TD1 (البطاقة الوطنية العراقية)");

        // ==========================================
        // Employee nested endpoints
        // ==========================================
        var employeeGroup = endpoints.MapGroup("/api/employees")
            .WithTags("EmployeeNationalIdentityCard")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        // 6. [API 1: الخدمة الذاتية للمستخدم الحالي] إرسال وتحديث البطاقة للمستخدم نفسه
        employeeGroup.MapPost("/me/national-card", async (
            [FromBody] NationalCardRegisterRequest request,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var employee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
            if (employee is null)
            {
                return Results.Problem(
                    detail: "الحساب الحالي غير مرتبط بسجل موظف في المنظومة. يرجى مراجعة إدارة الموارد البشرية لربط الحساب.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "حساب غير مقترن بموظف");
            }

            try
            {
                var username = principal.Identity?.Name ?? employee.EmployeeID;
                var result = await cardService.RegisterOrUpdateAsync(employee.Id, request, username, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "فشل تسجيل البطاقة الوطنية للمستخدم الحالي");
            }
        })
        .WithName("RegisterMyNationalCard")
        .WithSummary("تسجيل أو تحديث البطاقة الوطنية للمستخدم الحالي (خدمة ذاتية عبر الموبايل)");

        // 7. [API 1: استعراض بطاقة المستخدم الحالي]
        employeeGroup.MapGet("/me/national-card", async (
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var employee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
            if (employee is null)
            {
                return Results.Problem(
                    detail: "الحساب الحالي غير مرتبط بسجل موظف في المنظومة.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "حساب غير مقترن بموظف");
            }

            var card = await cardService.GetByEmployeeIdAsync(employee.Id, ct);
            return card is not null ? Results.Ok(card) : Results.NotFound(new { message = "لم يتم تسجيل بطاقة وطنية موحدة لحسابك بعد." });
        })
        .WithName("GetMyNationalCard")
        .WithSummary("استرجاع بيانات البطاقة الوطنية للمستخدم الحالي (خدمة ذاتية)");

        // Aliases at /api/my-national-card for convenience
        endpoints.MapPost("/api/my-national-card", async (
            [FromBody] NationalCardRegisterRequest request,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var employee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
            if (employee is null)
            {
                return Results.Problem(
                    detail: "الحساب الحالي غير مرتبط بسجل موظف في المنظومة.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "حساب غير مقترن بموظف");
            }

            try
            {
                var username = principal.Identity?.Name ?? employee.EmployeeID;
                var result = await cardService.RegisterOrUpdateAsync(employee.Id, request, username, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "فشل تسجيل البطاقة الوطنية للمستخدم الحالي");
            }
        })
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithTags("NationalIdentityCards")
        .WithName("RegisterMyNationalCardAlias")
        .WithSummary("اسم بديل لتسجيل بطاقة المستخدم الحالي عبر الموبايل");

        endpoints.MapGet("/api/my-national-card", async (
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var employee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
            if (employee is null)
            {
                return Results.Problem(
                    detail: "الحساب الحالي غير مرتبط بسجل موظف في المنظومة.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "حساب غير مقترن بموظف");
            }

            var card = await cardService.GetByEmployeeIdAsync(employee.Id, ct);
            return card is not null ? Results.Ok(card) : Results.NotFound(new { message = "لم يتم تسجيل بطاقة وطنية موحدة لحسابك بعد." });
        })
        .RequireAuthorization(AuthorizationPolicies.ApiBearer)
        .WithTags("NationalIdentityCards")
        .WithName("GetMyNationalCardAlias")
        .WithSummary("اسم بديل لاسترجاع بطاقة المستخدم الحالي");

        // 8. [API 2: للأدمن ومسؤولي الموارد البشرية] تسجيل أو تحديث البطاقة لأي موظف يختاره من القائمة
        employeeGroup.MapPost("/{idOrNumber}/national-card", async (
            string idOrNumber,
            [FromBody] NationalCardRegisterRequest request,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            // Resolve target employee by database ID or employee number
            Employee? targetEmployee;
            if (int.TryParse(idOrNumber, out var numericId))
            {
                targetEmployee = await db.Employees.FirstOrDefaultAsync(e => e.Id == numericId || e.EmployeeID == idOrNumber, ct);
            }
            else
            {
                targetEmployee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == idOrNumber, ct);
            }

            if (targetEmployee is null)
            {
                return Results.NotFound(new { message = $"الموظف المحدد بالمعرّف '{idOrNumber}' غير موجود في النظام." });
            }

            // Authorization: If not Admin/Staff, user can ONLY register for their own employee record
            if (!StaffRoles.IsStaff(principal))
            {
                var currentEmployee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
                if (currentEmployee is null || currentEmployee.Id != targetEmployee.Id)
                {
                    return Results.Problem(
                        detail: "غير مصرح لك بتسجيل أو تحديث البطاقة لموظف آخر. هذه الصلاحية خاصة بمسؤولي النظام (Admin) وإدارة الموارد البشرية. يُرجى استخدام واجهة الخدمة الذاتية /api/employees/me/national-card.",
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "صلاحية غير كافية");
                }
            }

            try
            {
                var username = principal.Identity?.Name ?? "API";
                var result = await cardService.RegisterOrUpdateAsync(targetEmployee.Id, request, username, ct);
                return Results.Created($"/api/employees/{targetEmployee.Id}/national-card", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "فشل تسجيل البطاقة الوطنية الموحدة");
            }
        })
        .WithName("RegisterEmployeeNationalCard")
        .WithSummary("تسجيل أو تحديث البطاقة الوطنية لموظف محدد (متاح للأدمن ومسؤولي الموارد البشرية لاختيار أي موظف)");

        // 9. Get National Card by Employee ID
        employeeGroup.MapGet("/{employeeId:int}/national-card", async (
            int employeeId,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var hasCardViewPerm = principal.IsInRole("Admin") ||
                                  principal.HasPermission(Permissions.NationalCardsView) ||
                                  principal.HasPermission(Permissions.NationalCardsEdit);

            // If not admin or holder of NationalCardsView permission, verify user is requesting their own card
            if (!hasCardViewPerm)
            {
                var currentEmployee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
                if (currentEmployee is null || currentEmployee.Id != employeeId)
                {
                    return Results.NotFound(new { message = "لم يتم العثور على السجل أو غير مصرح بعرضه." });
                }
            }

            var card = await cardService.GetByEmployeeIdAsync(employeeId, ct);
            return card is not null ? Results.Ok(card) : Results.NotFound(new { message = "لم يتم تسجيل بطاقة وطنية موحدة لهذا الموظف بعد." });
        })
        .WithName("GetEmployeeNationalCard")
        .WithSummary("استرجاع بيانات البطاقة الوطنية الموحدة للموظف بواسطة المعرّف الرقمي");

        // 10. Get National Card by Employee Number
        employeeGroup.MapGet("/by-number/{employeeNumber}/national-card", async (
            string employeeNumber,
            INationalCardService cardService,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var hasCardViewPerm = principal.IsInRole("Admin") ||
                                  principal.HasPermission(Permissions.NationalCardsView) ||
                                  principal.HasPermission(Permissions.NationalCardsEdit);

            if (!hasCardViewPerm)
            {
                var currentEmployee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
                if (currentEmployee is null || !string.Equals(currentEmployee.EmployeeID, employeeNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound(new { message = "لم يتم العثور على السجل أو غير مصرح بعرضه." });
                }
            }

            var card = await cardService.GetByEmployeeNumberAsync(employeeNumber, ct);
            return card is not null ? Results.Ok(card) : Results.NotFound(new { message = "لم يتم تسجيل بطاقة وطنية موحدة للموظف بالرقم الوظيفي المحدد." });
        })
        .WithName("GetEmployeeNationalCardByNumber")
        .WithSummary("استرجاع بيانات البطاقة الوطنية الموحدة للموظف بواسطة رقمه الوظيفي");

        return endpoints;
    }

    /// <summary>
    /// استرجاع صورة البطاقة الوطنية مع التحقق الصارم من صلاحية الموظف (NationalCards.View) أو ملكية صاحب البطاقة
    /// </summary>
    private static async Task<IResult> GetNationalCardImageAsync(
        int id,
        string imageType,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        INationalCardService cardService,
        CancellationToken ct)
    {
        var card = await db.EmployeeNationalCards
            .AsNoTracking()
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (card is null)
        {
            return Results.NotFound(new { message = "سجل البطاقة الوطنية غير موجود." });
        }

        var isStaffUser = principal.IsInRole("Admin") ||
                          principal.HasPermission(Permissions.NationalCardsView) ||
                          principal.HasPermission(Permissions.NationalCardsEdit);
        if (!isStaffUser)
        {
            var currentEmployee = await ResolveCurrentEmployeeAsync(principal, userManager, db, ct);
            if (currentEmployee is null ||
                (card.Employee is not null && !string.Equals(card.Employee.EmployeeID, currentEmployee.EmployeeID, StringComparison.OrdinalIgnoreCase)) ||
                card.EmployeeId != currentEmployee.Id)
            {
                return Results.Forbid();
            }
        }

        var result = await cardService.GetImageAsync(id, imageType, ct);
        if (result == null || result.Value.Stream == null)
        {
            var notFoundMsg = imageType switch
            {
                "biometric-photo" => "الصورة البيومترية غير متوفرة لهذه البطاقة.",
                "front-image" => "صورة واجهة البطاقة غير متوفرة.",
                "back-image" => "صورة ظهر البطاقة غير متوفرة.",
                _ => "الصورة غير متوفرة لهذه البطاقة."
            };
            return Results.NotFound(new { message = notFoundMsg });
        }

        return Results.File(result.Value.Stream, result.Value.ContentType);
    }

    /// <summary>
    /// استخراج سجل الموظف المقترن بحساب المستخدم الحالي الموثق
    /// </summary>
    private static async Task<Employee?> ResolveCurrentEmployeeAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var empNumber = principal.FindFirst("employee_id")?.Value;
        if (string.IsNullOrWhiteSpace(empNumber))
        {
            var user = await userManager.GetUserAsync(principal);
            empNumber = user?.EmployeeID;
        }

        if (string.IsNullOrWhiteSpace(empNumber))
        {
            var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var matchedEmail = await db.EmployeeEmails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Email == email, ct);
                if (matchedEmail != null)
                {
                    empNumber = matchedEmail.EmployeeID;
                }
                else
                {
                    var appUser = await db.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email == email || u.UserName == email, ct);
                    empNumber = appUser?.EmployeeID;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(empNumber))
        {
            return null;
        }

        if (int.TryParse(empNumber, out var parsedIntId))
        {
            return await db.Employees.FirstOrDefaultAsync(e => e.Id == parsedIntId || e.EmployeeID == empNumber, ct);
        }

        return await db.Employees.FirstOrDefaultAsync(e => e.EmployeeID == empNumber, ct);
    }
}
