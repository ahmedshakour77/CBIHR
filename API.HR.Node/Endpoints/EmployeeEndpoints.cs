using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using API.HR.Node.Services.Employees;

namespace API.HR.Node.Endpoints;

public static class EmployeeEndpoints
{
    private const int MaximumPageSize = 200;

    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees")
            .WithTags("Employees")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapGet("", GetEmployees)
            .WithName("GetEmployees")
            .WithSummary("Gets a paginated list of employees.")
            // Requires fine-grained Employees.View permission (or Admin bypass)
            .RequireAuthorization(Permissions.EmployeesView);

        group.MapGet("/by-contact", GetEmployeeByContact)
            .WithName("GetEmployeeByContact")
            .WithSummary("Gets employee info bound to a specific email address or phone number.")
            .RequireAuthorization(Permissions.EmployeesView);

        group.MapPost("/by-contact", LookupEmployeeByContactBody)
            .WithName("LookupEmployeeByContactBody")
            .WithSummary("Gets employee info bound to a specific email address or phone number via JSON body.")
            .RequireAuthorization(Permissions.EmployeesView);

        group.MapGet("/me", GetCurrentEmployee)
            .WithName("GetCurrentEmployee")
            .WithSummary("Gets the employee profile bound to the currently authenticated user account.");

        group.MapGet("/me/overview", GetCurrentEmployeeOverview)
            .WithName("GetCurrentEmployeeOverview")
            .WithSummary("Gets comprehensive dashboard overview and profile metrics for the mobile application.");

        group.MapGet("/{employeeID}", GetEmployeeByID)
            .WithName("GetEmployeeByID")
            .WithSummary("Gets an employee by employee ID.");

        group.MapGet("/archived", GetArchivedEmployees)
            .WithName("GetArchivedEmployees")
            .WithSummary("Gets all temporarily archived employees.")
            .RequireAuthorization(Permissions.EmployeesView);

        group.MapPost("/{employeeID}/archive", ArchiveEmployee)
            .WithName("ArchiveEmployee")
            .WithSummary("Temporarily excludes/archives an employee from all reports and active directories.")
            .RequireAuthorization(Permissions.EmployeesEdit);

        group.MapPost("/{employeeID}/restore", RestoreEmployee)
            .WithName("RestoreEmployee")
            .WithSummary("Restores an archived employee back to active service and reports.")
            .RequireAuthorization(Permissions.EmployeesEdit);

        group.MapDelete("/{employeeID}", DeleteEmployeePermanently)
            .WithName("DeleteEmployeePermanently")
            .WithSummary("Permanently deletes an archived employee record and all related documents.")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return endpoints;
    }

    private static async Task<IResult> GetEmployees(
        ApplicationDbContext dbContext,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? formation = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return Results.BadRequest(new { message = "Page must be greater than zero." });
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            return Results.BadRequest(new
            {
                message = $"Page size must be between 1 and {MaximumPageSize}."
            });
        }

        IQueryable<Employee> query = dbContext.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(employee =>
                employee.EmployeeID.Contains(term) ||
                employee.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(formation))
        {
            var value = formation.Trim();
            query = query.Where(employee => employee.Formation != null && employee.Formation.Name == value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var value = status.Trim();
            query = query.Where(employee => employee.EmployeeStatus != null && employee.EmployeeStatus.Name == value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var employees = await query
            .OrderBy(employee => employee.EmployeeID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(employee => new EmployeeResponse(
                employee.Formation == null ? string.Empty : employee.Formation.Name,
                employee.EmployeeID,
                employee.Name,
                employee.JobTitle == null ? string.Empty : employee.JobTitle.Name,
                employee.PositionTitle == null ? string.Empty : employee.PositionTitle.Name,
                employee.Stage == null ? string.Empty : employee.Stage.Name,
                employee.Grade == null ? string.Empty : employee.Grade.Name,
                employee.EducationLevel == null ? string.Empty : employee.EducationLevel.Name,
                employee.Specialization == null ? string.Empty : employee.Specialization.Name,
                employee.LastJobTitleDate,
                employee.HireDate,
                employee.Gender == null ? string.Empty : employee.Gender.Name,
                employee.Religion == null ? string.Empty : employee.Religion.Name,
                employee.Nationality == null ? string.Empty : employee.Nationality.Name,
                employee.FirstDepartment ?? string.Empty,
                employee.BirthDate,
                employee.EmployeeStatus == null ? string.Empty : employee.EmployeeStatus.Name,
                dbContext.EmployeeProfilePhotos.Any(p => p.EmployeeID == employee.EmployeeID && p.IsActive),
                dbContext.EmployeeProfilePhotos.Where(p => p.EmployeeID == employee.EmployeeID && p.IsActive).Select(p => (int?)p.Version).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<EmployeeResponse>(
            employees,
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    private static async Task<IResult> GetEmployeeByID(
        string employeeID,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Employees may read their own record; users with Employees.View or Admin may read anyone's.
        var hasViewPerm = principal.IsInRole("Admin") ||
                          principal.HasPermission(Permissions.EmployeesView) ||
                          principal.HasPermission(Permissions.EmployeesEdit);

        if (!hasViewPerm)
        {
            var ownId = principal.FindFirst("employee_id")?.Value
                ?? (await userManager.GetUserAsync(principal))?.EmployeeID;
            if (!string.Equals(ownId, employeeID, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound(new { message = "Employee was not found." });
            }
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeID == employeeID)
            .Select(employee => new EmployeeResponse(
                employee.Formation == null ? string.Empty : employee.Formation.Name,
                employee.EmployeeID,
                employee.Name,
                employee.JobTitle == null ? string.Empty : employee.JobTitle.Name,
                employee.PositionTitle == null ? string.Empty : employee.PositionTitle.Name,
                employee.Stage == null ? string.Empty : employee.Stage.Name,
                employee.Grade == null ? string.Empty : employee.Grade.Name,
                employee.EducationLevel == null ? string.Empty : employee.EducationLevel.Name,
                employee.Specialization == null ? string.Empty : employee.Specialization.Name,
                employee.LastJobTitleDate,
                employee.HireDate,
                employee.Gender == null ? string.Empty : employee.Gender.Name,
                employee.Religion == null ? string.Empty : employee.Religion.Name,
                employee.Nationality == null ? string.Empty : employee.Nationality.Name,
                employee.FirstDepartment ?? string.Empty,
                employee.BirthDate,
                employee.EmployeeStatus == null ? string.Empty : employee.EmployeeStatus.Name,
                dbContext.EmployeeProfilePhotos.Any(p => p.EmployeeID == employee.EmployeeID && p.IsActive),
                dbContext.EmployeeProfilePhotos.Where(p => p.EmployeeID == employee.EmployeeID && p.IsActive).Select(p => (int?)p.Version).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

        return employee is null
            ? Results.NotFound(new { message = "Employee was not found." })
            : Results.Ok(employee);
    }

    private static async Task<IResult> GetEmployeeByContact(
        ApplicationDbContext dbContext,
        [FromQuery] string? email = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? contact = null,
        CancellationToken cancellationToken = default)
    {
        return await LookupEmployeeByContactInternal(dbContext, email, phone, contact, cancellationToken);
    }

    private static async Task<IResult> LookupEmployeeByContactBody(
        [FromBody] EmployeeContactLookupRequest request,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return await LookupEmployeeByContactInternal(dbContext, request.Email, request.Phone, request.Contact, cancellationToken);
    }

    private static async Task<IResult> LookupEmployeeByContactInternal(
        ApplicationDbContext dbContext,
        string? email,
        string? phone,
        string? contact,
        CancellationToken cancellationToken)
    {
        var targetEmail = !string.IsNullOrWhiteSpace(email)
            ? email.Trim()
            : (!string.IsNullOrWhiteSpace(contact) && contact.Contains('@') ? contact.Trim() : null);

        var targetPhone = !string.IsNullOrWhiteSpace(phone)
            ? phone.Trim()
            : (!string.IsNullOrWhiteSpace(contact) && !contact.Contains('@') ? contact.Trim() : null);

        var rawContact = !string.IsNullOrWhiteSpace(contact) ? contact.Trim() : null;

        if (string.IsNullOrWhiteSpace(targetEmail) && string.IsNullOrWhiteSpace(targetPhone) && string.IsNullOrWhiteSpace(rawContact))
        {
            return Results.BadRequest(new
            {
                message = "At least one search parameter ('email', 'phone', or 'contact') must be provided."
            });
        }

        string? matchedEmployeeId = null;

        // 1. Search in EmployeeEmails table
        if (!string.IsNullOrWhiteSpace(targetEmail))
        {
            var normEmail = targetEmail.ToLower();
            matchedEmployeeId = await dbContext.EmployeeEmails
                .AsNoTracking()
                .Where(e => e.Email == normEmail)
                .Select(e => e.EmployeeID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 2. Search in EmployeePhones table
        if (string.IsNullOrEmpty(matchedEmployeeId) && !string.IsNullOrWhiteSpace(targetPhone))
        {
            matchedEmployeeId = await dbContext.EmployeePhones
                .AsNoTracking()
                .Where(p => p.PhoneNumber == targetPhone)
                .Select(p => p.EmployeeID)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 3. Search in ApplicationUser (User accounts bound to an EmployeeID)
        if (string.IsNullOrEmpty(matchedEmployeeId))
        {
            var searchTerm = targetEmail ?? targetPhone ?? rawContact;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normSearch = searchTerm.ToLower();
                var upperSearch = searchTerm.ToUpper();
                matchedEmployeeId = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.EmployeeID != null && (
                        (u.Email != null && u.Email == normSearch) ||
                        u.PhoneNumber == searchTerm ||
                        (u.UserName != null && u.UserName == normSearch) ||
                        u.NormalizedEmail == upperSearch ||
                        u.NormalizedUserName == upperSearch))
                    .Select(u => u.EmployeeID)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        // 4. Fallback search with rawContact across both EmployeeEmails and EmployeePhones
        if (string.IsNullOrEmpty(matchedEmployeeId) && !string.IsNullOrWhiteSpace(rawContact))
        {
            var normContact = rawContact.ToLower();
            matchedEmployeeId = await dbContext.EmployeeEmails
                .AsNoTracking()
                .Where(e => e.Email == normContact)
                .Select(e => e.EmployeeID)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await dbContext.EmployeePhones
                    .AsNoTracking()
                    .Where(p => p.PhoneNumber == rawContact)
                    .Select(p => p.EmployeeID)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(matchedEmployeeId))
        {
            return Results.NotFound(new
            {
                message = "No employee bound to the provided email or phone number was found."
            });
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(emp => emp.EmployeeID == matchedEmployeeId)
            .Select(employee => new EmployeeResponse(
                employee.Formation == null ? string.Empty : employee.Formation.Name,
                employee.EmployeeID,
                employee.Name,
                employee.JobTitle == null ? string.Empty : employee.JobTitle.Name,
                employee.PositionTitle == null ? string.Empty : employee.PositionTitle.Name,
                employee.Stage == null ? string.Empty : employee.Stage.Name,
                employee.Grade == null ? string.Empty : employee.Grade.Name,
                employee.EducationLevel == null ? string.Empty : employee.EducationLevel.Name,
                employee.Specialization == null ? string.Empty : employee.Specialization.Name,
                employee.LastJobTitleDate,
                employee.HireDate,
                employee.Gender == null ? string.Empty : employee.Gender.Name,
                employee.Religion == null ? string.Empty : employee.Religion.Name,
                employee.Nationality == null ? string.Empty : employee.Nationality.Name,
                employee.FirstDepartment ?? string.Empty,
                employee.BirthDate,
                employee.EmployeeStatus == null ? string.Empty : employee.EmployeeStatus.Name,
                dbContext.EmployeeProfilePhotos.Any(p => p.EmployeeID == employee.EmployeeID && p.IsActive),
                dbContext.EmployeeProfilePhotos.Where(p => p.EmployeeID == employee.EmployeeID && p.IsActive).Select(p => (int?)p.Version).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Results.NotFound(new
            {
                message = "The bound employee record was not found."
            });
        }

        return Results.Ok(employee);
    }

    private static async Task<IResult> GetCurrentEmployee(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null || string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            return Results.Json(new
            {
                message = "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Enforce Remote Force Logout / Active Device Check
        var hasActiveDevice = await dbContext.UserDevices
            .AnyAsync(d => d.EmployeeID == user.EmployeeID && d.IsActive, cancellationToken);

        if (!hasActiveDevice)
        {
            return Results.Json(new
            {
                message = "تم إلغاء اقتران هذا الجهاز أو إغلاق الجلسة من قبل إدارة النظام."
            }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(emp => emp.EmployeeID == user.EmployeeID)
            .Select(employee => new EmployeeResponse(
                employee.Formation == null ? string.Empty : employee.Formation.Name,
                employee.EmployeeID,
                employee.Name,
                employee.JobTitle == null ? string.Empty : employee.JobTitle.Name,
                employee.PositionTitle == null ? string.Empty : employee.PositionTitle.Name,
                employee.Stage == null ? string.Empty : employee.Stage.Name,
                employee.Grade == null ? string.Empty : employee.Grade.Name,
                employee.EducationLevel == null ? string.Empty : employee.EducationLevel.Name,
                employee.Specialization == null ? string.Empty : employee.Specialization.Name,
                employee.LastJobTitleDate,
                employee.HireDate,
                employee.Gender == null ? string.Empty : employee.Gender.Name,
                employee.Religion == null ? string.Empty : employee.Religion.Name,
                employee.Nationality == null ? string.Empty : employee.Nationality.Name,
                employee.FirstDepartment ?? string.Empty,
                employee.BirthDate,
                employee.EmployeeStatus == null ? string.Empty : employee.EmployeeStatus.Name,
                dbContext.EmployeeProfilePhotos.Any(p => p.EmployeeID == employee.EmployeeID && p.IsActive),
                dbContext.EmployeeProfilePhotos.Where(p => p.EmployeeID == employee.EmployeeID && p.IsActive).Select(p => (int?)p.Version).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Results.NotFound(new
            {
                message = "سجل الموظف غير موجود بالنظام."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains("Admin") || principal.IsInRole("Admin");
        var userClaims = await userManager.GetClaimsAsync(user);
        var permissions = userClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        if (isAdmin)
        {
            var defaultAdminPerms = new[] { "VIEW_ALL_EMPLOYEES", "MANAGE_DOCUMENTS", "VERIFY_DOCUMENTS", "EXPORT_REPORTS" };
            foreach (var p in defaultAdminPerms)
            {
                if (!permissions.Contains(p)) permissions.Add(p);
            }
        }

        return Results.Ok(new
        {
            employeeID = employee.EmployeeID,
            name = employee.Name,
            roles,
            isAdmin,
            permissions,
            formation = employee.Formation,
            jobTitle = employee.JobTitle,
            positionTitle = employee.PositionTitle,
            stage = employee.Stage,
            grade = employee.Grade,
            educationLevel = employee.EducationLevel,
            specialization = employee.Specialization,
            lastJobTitleDate = employee.LastJobTitleDate,
            hireDate = employee.HireDate,
            gender = employee.Gender,
            religion = employee.Religion,
            nationality = employee.Nationality,
            firstDepartment = employee.FirstDepartment,
            birthDate = employee.BirthDate,
            employeeStatus = employee.EmployeeStatus,
            hasProfilePhoto = employee.HasProfilePhoto,
            profilePhotoVersion = employee.ProfilePhotoVersion
        });
    }

    private static async Task<IResult> GetCurrentEmployeeOverview(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            return Results.Json(new
            {
                message = "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var emp = await dbContext.Employees.AsNoTracking()
            .Include(e => e.Formation)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.Grade)
            .Include(e => e.Stage)
            .Include(e => e.EducationLevel)
            .Include(e => e.Specialization)
            .Include(e => e.EmployeeStatus)
            .Include(e => e.MaritalStatus)
            .Include(e => e.Gender)
            .FirstOrDefaultAsync(e => e.EmployeeID == user.EmployeeID, cancellationToken);

        if (emp is null)
        {
            return Results.NotFound(new { message = "سجل الموظف غير موجود." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentResidence = await dbContext.EmployeeResidences.AsNoTracking()
            .Include(r => r.Governorate)
            .Where(r => (r.EmployeeIdNumber == emp.EmployeeID || r.EmployeeId == emp.Id) && (r.ToDate == null || r.ToDate >= today))
            .OrderByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                governorate = r.Governorate != null ? r.Governorate.Name : null,
                r.HousingType,
                r.District,
                r.Mahalla,
                r.Zuqaq,
                r.Dar,
                r.ComplexName,
                r.BuildingNumber,
                r.FloorNumber,
                r.ApartmentNumber,
                r.Latitude,
                r.Longitude,
                r.LocationUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        var hasPhoto = await dbContext.EmployeeProfilePhotos
            .AnyAsync(p => p.EmployeeID == emp.EmployeeID && p.IsActive, cancellationToken);

        var leavesCount = await dbContext.EmployeeLeaves.CountAsync(l => l.EmployeeId == emp.Id, cancellationToken);
        var coursesCount = await dbContext.EmployeeCourses.CountAsync(c => c.EmployeeId == emp.Id, cancellationToken);
        var lettersCount = await dbContext.AppreciationLetters.CountAsync(a => a.EmployeeId == emp.Id, cancellationToken);
        var movementsCount = await dbContext.EmployeeCareerMovements.CountAsync(m => m.EmployeeId == emp.Id, cancellationToken);
        var unreadNotifications = await dbContext.UserNotifications.CountAsync(n => n.UserId == user.Id && !n.IsRead, cancellationToken);
        var pendingTasks = await dbContext.WorkflowTasks.CountAsync(t => t.AssignedUserId == user.Id && t.Status == Data.Workflows.WorkflowTaskStatus.Pending, cancellationToken);

        return Results.Ok(new
        {
            employee = new
            {
                employeeId = emp.EmployeeID,
                name = emp.Name,
                formation = emp.Formation?.Name,
                jobTitle = emp.JobTitle?.Name,
                positionTitle = emp.PositionTitle?.Name,
                grade = emp.Grade?.Name,
                stage = emp.Stage?.Name,
                educationLevel = emp.EducationLevel?.Name,
                specialization = emp.Specialization?.Name,
                status = emp.EmployeeStatus?.Name,
                nationalId = emp.NationalId,
                unifiedEmployeeNumber = emp.UnifiedEmployeeNumber,
                motherName = emp.MotherName,
                maritalStatus = emp.MaritalStatus?.Name,
                gender = emp.Gender?.Name,
                hireDate = emp.HireDate,
                lastJobTitleDate = emp.LastJobTitleDate,
                birthDate = emp.BirthDate,
                leaveBalance = emp.LeaveBalance ?? 0m
            },
            residence = currentResidence,
            photo = new
            {
                hasPhoto,
                url = hasPhoto ? $"/api/employees/{emp.EmployeeID}/profile-photo" : null,
                thumbnailUrl = hasPhoto ? $"/api/employees/{emp.EmployeeID}/profile-photo/thumbnail" : null
            },
            stats = new
            {
                leavesCount,
                coursesCount,
                lettersCount,
                movementsCount,
                unreadNotifications,
                pendingTasks
            }
        });
    }

    private static async Task<IResult> ArchiveEmployee(
        string employeeID,
        [FromBody] ArchiveEmployeeRequest? request,
        IEmployeeLifecycleService lifecycleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var performedBy = user.Identity?.Name ?? "User";
        var result = await lifecycleService.ArchiveEmployeeAsync(employeeID, request?.Reason ?? "استبعاد إداري مؤقت", performedBy, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> RestoreEmployee(
        string employeeID,
        IEmployeeLifecycleService lifecycleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var performedBy = user.Identity?.Name ?? "User";
        var result = await lifecycleService.RestoreEmployeeAsync(employeeID, performedBy, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> DeleteEmployeePermanently(
        string employeeID,
        IEmployeeLifecycleService lifecycleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var performedBy = user.Identity?.Name ?? "Admin";
        var result = await lifecycleService.DeletePermanentlyAsync(employeeID, performedBy, ct);
        return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> GetArchivedEmployees(
        [FromQuery] string? search,
        IEmployeeLifecycleService lifecycleService,
        CancellationToken ct)
    {
        var list = await lifecycleService.GetArchivedEmployeesAsync(search, ct);
        return Results.Ok(list);
    }
}

public sealed record ArchiveEmployeeRequest(string? Reason);


