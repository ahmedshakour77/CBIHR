using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.HR.Node.Services.Identity;

public sealed class UserEmployeeService : IUserEmployeeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<UserEmployeeService> _logger;

    public UserEmployeeService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        ILogger<UserEmployeeService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<EmployeeLinkValidationResult> ValidateOrRepairUserEmployeeLinkAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        if (SystemAccounts.IsTechnicalAccount(user.UserName) || SystemAccounts.IsTechnicalAccount(user.Email))
        {
            return new EmployeeLinkValidationResult(
                IsValid: true,
                WasRepaired: false,
                IsTechnicalAccount: true,
                FailureReason: null,
                EmployeeID: user.EmployeeID,
                EmployeeName: "حساب تقني معتمد للنظام");
        }

        // 1. If user already has an EmployeeID, verify it exists in Employees table
        if (!string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            var existingEmp = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID == user.EmployeeID, ct);

            if (existingEmp != null)
            {
                await EnsureDefaultRoleIfNoneAsync(user, ct);
                return new EmployeeLinkValidationResult(
                    IsValid: true,
                    WasRepaired: false,
                    IsTechnicalAccount: false,
                    FailureReason: null,
                    EmployeeID: existingEmp.EmployeeID,
                    EmployeeName: existingEmp.Name);
            }

            _logger.LogWarning("User {UserId} has dangling EmployeeID '{EmployeeID}' not found in Employees table.",
                user.Id, user.EmployeeID);
        }

        // 2. Attempt automatic repair via EmployeeEmails matching User.Email
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return new EmployeeLinkValidationResult(
                IsValid: false,
                WasRepaired: false,
                IsTechnicalAccount: false,
                FailureReason: "هذا الحساب غير مرتبط بسجل موظف، ولا يتوفر بريد إلكتروني لإجراء المطابقة الآلية.",
                EmployeeID: null,
                EmployeeName: null);
        }

        var normalizedEmail = user.Email.Trim().ToLower();
        var candidateEmployeeIds = await _dbContext.EmployeeEmails
            .AsNoTracking()
            .Where(ee => ee.Email.Trim().ToLower() == normalizedEmail)
            .Select(ee => ee.EmployeeID)
            .Distinct()
            .ToListAsync(ct);

        if (candidateEmployeeIds.Count == 1)
        {
            var matchedEmpId = candidateEmployeeIds[0];
            var targetEmp = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeID == matchedEmpId, ct);

            if (targetEmp != null)
            {
                var oldEmpId = user.EmployeeID;
                user.EmployeeID = targetEmp.EmployeeID;
                var updateRes = await _userManager.UpdateAsync(user);

                if (updateRes.Succeeded)
                {
                    await _auditLogger.LogAsync(
                        action: "USER_EMPLOYEE_LINKED",
                        employeeID: targetEmp.EmployeeID,
                        photoId: null,
                        success: true,
                        user: null,
                        ipAddress: null,
                        userAgent: null,
                        failureReason: null,
                        correlationId: $"AutoRepair: Old='{oldEmpId}' -> New='{targetEmp.EmployeeID}', User='{user.UserName}'",
                        cancellationToken: ct);

                    _logger.LogInformation("Successfully auto-repaired user '{UserName}' linkage to employee '{EmployeeID}' ({Name})",
                        user.UserName, targetEmp.EmployeeID, targetEmp.Name);

                    await EnsureDefaultRoleIfNoneAsync(user, ct);

                    return new EmployeeLinkValidationResult(
                        IsValid: true,
                        WasRepaired: true,
                        IsTechnicalAccount: false,
                        FailureReason: null,
                        EmployeeID: targetEmp.EmployeeID,
                        EmployeeName: targetEmp.Name);
                }

                _logger.LogError("Failed to persist auto-repaired EmployeeID for user '{UserName}': {Errors}",
                    user.UserName, string.Join(", ", updateRes.Errors.Select(e => e.Description)));
            }
        }
        else if (candidateEmployeeIds.Count > 1)
        {
            _logger.LogWarning("Multiple employee records match email '{Email}' for user '{UserName}'. Cannot guess linkage.",
                user.Email, user.UserName);

            return new EmployeeLinkValidationResult(
                IsValid: false,
                WasRepaired: false,
                IsTechnicalAccount: false,
                FailureReason: "تم العثور على أكثر من سجل موظف يطابق هذا البريد الإلكتروني. يرجى مراجعة إدارة الموارد البشرية لربط الحساب يدوياً.",
                EmployeeID: null,
                EmployeeName: null);
        }

        return new EmployeeLinkValidationResult(
            IsValid: false,
            WasRepaired: false,
            IsTechnicalAccount: false,
            FailureReason: "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية.",
            EmployeeID: null,
            EmployeeName: null);
    }

    public async Task<BatchRepairReport> RepairOrphanedUsersAsync(
        string? actorUserId,
        CancellationToken ct = default)
    {
        var allUsers = await _userManager.Users.ToListAsync(ct);
        var totalUsers = allUsers.Count;

        var alreadyLinkedCount = 0;
        var repairedList = new List<RepairedUserItem>();
        var unresolvedList = new List<UnresolvedUserItem>();

        foreach (var user in allUsers)
        {
            if (SystemAccounts.IsTechnicalAccount(user.UserName) || SystemAccounts.IsTechnicalAccount(user.Email))
            {
                alreadyLinkedCount++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(user.EmployeeID))
            {
                var exists = await _dbContext.Employees
                    .AsNoTracking()
                    .AnyAsync(e => e.EmployeeID == user.EmployeeID, ct);

                if (exists)
                {
                    alreadyLinkedCount++;
                    continue;
                }
            }

            // User is unlinked or linked to invalid EmployeeID. Attempt repair.
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                unresolvedList.Add(new UnresolvedUserItem(
                    user.Id,
                    user.UserName ?? "—",
                    user.Email,
                    "لا يحتوي الحساب على بريد إلكتروني"));
                continue;
            }

            var normalizedEmail = user.Email.Trim().ToLower();
            var matches = await _dbContext.EmployeeEmails
                .AsNoTracking()
                .Where(ee => ee.Email.Trim().ToLower() == normalizedEmail)
                .Select(ee => ee.EmployeeID)
                .Distinct()
                .ToListAsync(ct);

            if (matches.Count == 1)
            {
                var candidateEmpId = matches[0];
                var emp = await _dbContext.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EmployeeID == candidateEmpId, ct);

                if (emp != null)
                {
                    var oldEmp = user.EmployeeID;
                    user.EmployeeID = emp.EmployeeID;
                    var res = await _userManager.UpdateAsync(user);

                    if (res.Succeeded)
                    {
                        await _userManager.UpdateSecurityStampAsync(user);

                        await _auditLogger.LogAsync(
                            action: "USER_EMPLOYEE_LINKED",
                            employeeID: emp.EmployeeID,
                            photoId: null,
                            success: true,
                            user: null,
                            ipAddress: null,
                            userAgent: null,
                            failureReason: null,
                            correlationId: $"BatchRepair: Actor='{actorUserId}', User='{user.UserName}', Old='{oldEmp}'",
                            cancellationToken: ct);

                        repairedList.Add(new RepairedUserItem(
                            user.Id,
                            user.UserName ?? "—",
                            user.Email,
                            emp.EmployeeID,
                            emp.Name));
                        continue;
                    }
                }
            }
            else if (matches.Count > 1)
            {
                unresolvedList.Add(new UnresolvedUserItem(
                    user.Id,
                    user.UserName ?? "—",
                    user.Email,
                    $"مطابقة متعددة غير محسومة ({matches.Count} موظفين مختلفين)"));
                continue;
            }

            unresolvedList.Add(new UnresolvedUserItem(
                user.Id,
                user.UserName ?? "—",
                user.Email,
                "لم يُعثر على موظف مسجل بهذا البريد الإلكتروني"));
        }

        return new BatchRepairReport(
            TotalUsers: totalUsers,
            AlreadyLinkedCount: alreadyLinkedCount,
            RepairedCount: repairedList.Count,
            UnresolvedCount: unresolvedList.Count,
            RepairedUsers: repairedList,
            UnresolvedUsers: unresolvedList);
    }

    public async Task<List<ApplicationUser>> GetUsersForEmployeeAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return [];
        }

        return await _userManager.Users
            .Where(u => u.EmployeeID == employeeId)
            .ToListAsync(ct);
    }

    public async Task<RelinkResult> RelinkUserAsync(
        string userId,
        string newEmployeeId,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newEmployeeId))
        {
            return new RelinkResult(false, "لا يمكن فك ارتباط المستخدم أو تعيين رقم موظف فارغ.", null, null);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new RelinkResult(false, "حساب المستخدم غير موجود.", null, null);
        }

        if (SystemAccounts.IsTechnicalAccount(user.UserName))
        {
            return new RelinkResult(false, "لا يمكن تغيير ربط الحسابات التقنية الخاصة بالنظام.", null, null);
        }

        var newEmployee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeID == newEmployeeId, ct);

        if (newEmployee == null)
        {
            return new RelinkResult(false, $"الموظف بالرقم '{newEmployeeId}' غير موجود في النظام.", null, null);
        }

        var oldEmployeeId = user.EmployeeID;
        user.EmployeeID = newEmployee.EmployeeID;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            return new RelinkResult(false, $"فشل تحديث ارتباط المستخدم: {errors}", oldEmployeeId, newEmployeeId);
        }

        // Invalidate previous sessions and update security stamp so new claims take effect
        await ForceLogoutUserAsync(user.Id, actorUserId, ct);

        await _auditLogger.LogAsync(
            action: "USER_EMPLOYEE_RELINKED",
            employeeID: newEmployee.EmployeeID,
            photoId: null,
            success: true,
            user: null,
            ipAddress: null,
            userAgent: null,
            failureReason: null,
            correlationId: $"Actor: {actorUserId}, TargetUser: {user.UserName}, OldEmployeeID: {oldEmployeeId ?? "NONE"}, NewEmployeeID: {newEmployee.EmployeeID}",
            cancellationToken: ct);

        _logger.LogInformation("User '{UserName}' relinked from Employee '{OldEmp}' to Employee '{NewEmp}' by actor '{Actor}'.",
            user.UserName, oldEmployeeId, newEmployee.EmployeeID, actorUserId);

        return new RelinkResult(true, null, oldEmployeeId, newEmployeeId);
    }

    /// <summary>
    /// Forces user logout across all sessions, tokens, and registered devices.
    /// <para>
    /// <b>Force Logout Guarantees &amp; Token Semantics:</b>
    /// <list type="bullet">
    ///   <item><description><b>Active UserDevices:</b> Revoked immediately (<c>IsActive = false</c>) in the database.</description></item>
    ///   <item><description><b>SecurityStamp:</b> Regenerated immediately via <c>UpdateSecurityStampAsync</c>.</description></item>
    ///   <item><description><b>Refresh Tokens:</b> Rejected immediately on the next token refresh request because ASP.NET Core Identity bearer endpoint validates the user's current security stamp.</description></item>
    ///   <item><description><b>Cookie Sessions:</b> Invalidated on next validation cycle per <c>SecurityStampValidatorOptions.ValidationInterval</c> (configured to 1 minute).</description></item>
    ///   <item><description><b>Existing Access Tokens:</b> Self-contained signed bearer tokens remain valid until expiration (configured to 15 minutes) unless per-request server-side revocation lookup is enforced.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public async Task<ForceLogoutResult> ForceLogoutUserAsync(
        string userId,
        string actorUserId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var revokedDevicesCount = 0;

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new ForceLogoutResult(false, 0, false, now, "المستخدم غير موجود.");
            }

            // 1. Mark all active UserDevices for this specific user as inactive (strictly by UserId)
            var activeDevices = await _dbContext.UserDevices
                .Where(d => d.UserId == user.Id && d.IsActive)
                .ToListAsync(ct);

            foreach (var dev in activeDevices)
            {
                dev.IsActive = false;
            }

            revokedDevicesCount = activeDevices.Count;
            if (revokedDevicesCount > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            // 2. Update ASP.NET Core Identity Security Stamp
            // This invalidates Identity login cookies on the next validator run and immediately invalidates refresh tokens
            var updateStampResult = await _userManager.UpdateSecurityStampAsync(user);
            var stampUpdated = updateStampResult.Succeeded;

            if (!stampUpdated)
            {
                var stampErrors = string.Join(", ", updateStampResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update security stamp for user {UserId} during force logout. Errors: {Errors}", userId, stampErrors);

                await _auditLogger.LogAsync(
                    action: "USER_FORCE_LOGOUT",
                    employeeID: user.EmployeeID ?? "SYSTEM",
                    photoId: null,
                    success: false,
                    user: null,
                    ipAddress: null,
                    userAgent: null,
                    failureReason: "SecurityStampUpdateFailed",
                    correlationId: $"Actor: {actorUserId}, TargetUser: {user.UserName}, DevicesRevoked: {revokedDevicesCount}, StampUpdated: false",
                    cancellationToken: ct);

                return new ForceLogoutResult(false, revokedDevicesCount, false, now, "تعذر تحديث الختم الأمني للمستخدم.");
            }

            // 3. Log audit event
            await _auditLogger.LogAsync(
                action: "USER_FORCE_LOGOUT",
                employeeID: user.EmployeeID ?? "SYSTEM",
                photoId: null,
                success: true,
                user: null,
                ipAddress: null,
                userAgent: null,
                failureReason: null,
                correlationId: $"Actor: {actorUserId}, TargetUser: {user.UserName}, DevicesRevoked: {revokedDevicesCount}, StampUpdated: true",
                cancellationToken: ct);

            _logger.LogInformation(
                "Force logout executed for user '{UserName}' by actor '{Actor}'. Devices revoked: {RevokedCount}, Stamp updated: true.",
                user.UserName, actorUserId, revokedDevicesCount);

            return new ForceLogoutResult(true, revokedDevicesCount, true, now, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing force logout for user {UserId} by actor {ActorId}", userId, actorUserId);
            return new ForceLogoutResult(false, revokedDevicesCount, false, now, "تعذر إنهاء جلسات المستخدم. يرجى مراجعة سجل النظام.");
        }
    }

    public async Task<ForceLogoutResult> ForceLogoutEmployeeAllUsersAsync(
        string employeeId,
        string actorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return new ForceLogoutResult(false, 0, false, DateTimeOffset.UtcNow, "رقم الموظف غير محدد.");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var users = await GetUsersForEmployeeAsync(employeeId, ct);
            var totalRevoked = 0;
            var allUpdated = true;

            foreach (var u in users)
            {
                var res = await ForceLogoutUserAsync(u.Id, actorUserId, ct);
                totalRevoked += res.SessionsRevokedCount;
                if (!res.Succeeded || !res.SecurityStampUpdated) allUpdated = false;
            }

            // Also deactivate any leftover active devices tagged with this EmployeeID
            var orphanDevices = await _dbContext.UserDevices
                .Where(d => d.EmployeeID == employeeId && d.IsActive)
                .ToListAsync(ct);

            foreach (var d in orphanDevices)
            {
                d.IsActive = false;
            }

            if (orphanDevices.Count > 0)
            {
                totalRevoked += orphanDevices.Count;
                await _dbContext.SaveChangesAsync(ct);
            }

            await _auditLogger.LogAsync(
                action: "EMPLOYEE_ALL_USERS_FORCE_LOGOUT",
                employeeID: employeeId,
                photoId: null,
                success: allUpdated,
                user: null,
                ipAddress: null,
                userAgent: null,
                failureReason: allUpdated ? null : "PartialFailureInUserSecurityStamps",
                correlationId: $"Actor: {actorUserId}, TargetEmployee: {employeeId}, UsersAffected: {users.Count}, DevicesRevoked: {totalRevoked}, AllUpdated: {allUpdated}",
                cancellationToken: ct);

            return new ForceLogoutResult(
                Succeeded: allUpdated,
                SessionsRevokedCount: totalRevoked,
                SecurityStampUpdated: allUpdated,
                Timestamp: now,
                ErrorMessage: allUpdated ? null : "تعذر إنهاء وتحديث الختم الأمني لبعض حسابات الموظف.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing employee all users force logout for employee {EmployeeId} by actor {ActorId}", employeeId, actorUserId);
            return new ForceLogoutResult(false, 0, false, DateTimeOffset.UtcNow, "تعذر إنهاء جلسات مستخدمي الموظف. يرجى مراجعة سجل النظام.");
        }
    }

    public async Task<List<EmployeePickerItem>> SearchEmployeesAsync(
        string query,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await _dbContext.Employees
                .AsNoTracking()
                .Where(e => !e.IsArchived)
                .OrderBy(e => e.EmployeeID)
                .Take(maxResults)
                .Select(e => new EmployeePickerItem(
                    e.EmployeeID,
                    e.Name,
                    e.Formation != null ? e.Formation.Name : null,
                    e.JobTitle != null ? e.JobTitle.Name : null,
                    _dbContext.EmployeeEmails
                        .Where(ee => ee.EmployeeID == e.EmployeeID)
                        .OrderByDescending(ee => ee.IsPrimary)
                        .Select(ee => ee.Email)
                        .FirstOrDefault()))
                .ToListAsync(ct);
        }

        var q = query.Trim().ToLower();

        // Find employee IDs with matching emails
        var matchedEmpIdsByEmail = await _dbContext.EmployeeEmails
            .AsNoTracking()
            .Where(ee => ee.Email.ToLower().Contains(q))
            .Select(ee => ee.EmployeeID)
            .Distinct()
            .Take(maxResults)
            .ToListAsync(ct);

        return await _dbContext.Employees
            .AsNoTracking()
            .Where(e => !e.IsArchived &&
                        (e.EmployeeID.ToLower().Contains(q) ||
                         e.Name.ToLower().Contains(q) ||
                         (e.Formation != null && e.Formation.Name.ToLower().Contains(q)) ||
                         matchedEmpIdsByEmail.Contains(e.EmployeeID)))
            .OrderBy(e => e.EmployeeID)
            .Take(maxResults)
            .Select(e => new EmployeePickerItem(
                e.EmployeeID,
                e.Name,
                e.Formation != null ? e.Formation.Name : null,
                e.JobTitle != null ? e.JobTitle.Name : null,
                _dbContext.EmployeeEmails
                    .Where(ee => ee.EmployeeID == e.EmployeeID)
                    .OrderByDescending(ee => ee.IsPrimary)
                    .Select(ee => ee.Email)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<bool> ValidateEmployeeExistsAsync(string employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId)) return false;
        return await _dbContext.Employees.AsNoTracking().AnyAsync(e => e.EmployeeID == employeeId, ct);
    }

    public async Task<SameEmployeeCheckResult> CheckSameEmployeePrivilegeEscalationAsync(
        string callerUserId,
        string targetUserId,
        ClaimsPrincipal callerPrincipal,
        CancellationToken ct = default)
    {
        // 1. Admin bypass: Full admins are trusted to manage permissions across all accounts strictly based on roles
        var isAdmin = PermissionResolver.IsAdmin(callerPrincipal);

        if (isAdmin)
        {
            return new SameEmployeeCheckResult(true, null, false, null, null);
        }

        // 2. Direct self-modification prevention
        if (string.Equals(callerUserId, targetUserId, StringComparison.OrdinalIgnoreCase))
        {
            await _auditLogger.LogAsync(
                action: "UNAUTHORIZED_SELF_ROLE_MODIFICATION_ATTEMPT",
                employeeID: callerPrincipal.FindFirst("employee_id")?.Value ?? callerUserId,
                photoId: null,
                success: false,
                user: callerPrincipal,
                ipAddress: null,
                userAgent: null,
                failureReason: "Caller attempted to modify their own roles/permissions directly.",
                correlationId: $"Caller: {callerUserId}, Target: {targetUserId}",
                cancellationToken: ct);

            return new SameEmployeeCheckResult(false, "لا يمكنك تعديل أدوارك أو صلاحياتك بنفسك لتفادي رفع الصلاحيات غير المصرّح به.", true, null, null);
        }

        // 3. Resolve caller EmployeeID
        string? callerEmpId = callerPrincipal.FindFirst("employee_id")?.Value;
        if (string.IsNullOrWhiteSpace(callerEmpId))
        {
            var callerUser = await _userManager.FindByIdAsync(callerUserId);
            callerEmpId = callerUser?.EmployeeID;
        }

        // 4. Resolve target EmployeeID
        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        var targetEmpId = targetUser?.EmployeeID;

        // 5. If both accounts belong to the same employee (and caller is not Admin), block!
        if (!string.IsNullOrWhiteSpace(callerEmpId) &&
            !string.IsNullOrWhiteSpace(targetEmpId) &&
            string.Equals(callerEmpId, targetEmpId, StringComparison.OrdinalIgnoreCase))
        {
            await _auditLogger.LogAsync(
                action: "UNAUTHORIZED_SAME_EMPLOYEE_PRIVILEGE_ESCALATION_ATTEMPT",
                employeeID: callerEmpId,
                photoId: null,
                success: false,
                user: callerPrincipal,
                ipAddress: null,
                userAgent: null,
                failureReason: $"Non-admin caller '{callerPrincipal.Identity?.Name}' (User: {callerUserId}) attempted to modify permissions of target user '{targetUser?.UserName}' (User: {targetUserId}) sharing EmployeeID '{callerEmpId}'.",
                correlationId: $"CallerUser: {callerUserId}, TargetUser: {targetUserId}, SharedEmployee: {callerEmpId}",
                cancellationToken: ct);

            _logger.LogWarning(
                "SECURITY ALERT: Blocked privilege escalation attempt. Caller user {CallerUserId} attempted to modify permissions of user {TargetUserId} with shared EmployeeID {EmpId}",
                callerUserId, targetUserId, callerEmpId);

            return new SameEmployeeCheckResult(
                false,
                "لا يمكنك تعديل أدوار أو صلاحيات حساب آخر مرتبط بنفس سجلك الوظيفي لمنع رفع الصلاحيات غير المصرّح به.",
                true,
                callerEmpId,
                targetEmpId);
        }

        return new SameEmployeeCheckResult(true, null, false, callerEmpId, targetEmpId);
    }

    private async Task EnsureDefaultRoleIfNoneAsync(ApplicationUser user, CancellationToken ct = default)
    {
        if (SystemAccounts.IsTechnicalAccount(user.UserName)) return;
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            try
            {
                await _userManager.AddToRoleAsync(user, "Employee");
                _logger.LogInformation("Assigned default role 'Employee' to user '{UserName}'", user.UserName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign default 'Employee' role to user '{UserName}'", user.UserName);
            }
        }
    }
}
