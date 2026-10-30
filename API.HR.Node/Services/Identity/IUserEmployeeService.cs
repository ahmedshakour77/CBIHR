using API.HR.Node.Data;

namespace API.HR.Node.Services.Identity;

public sealed record EmployeeLinkValidationResult(
    bool IsValid,
    bool WasRepaired,
    bool IsTechnicalAccount,
    string? FailureReason,
    string? EmployeeID,
    string? EmployeeName);

public sealed record RepairedUserItem(
    string UserId,
    string UserName,
    string? Email,
    string MatchedEmployeeId,
    string MatchedEmployeeName);

public sealed record UnresolvedUserItem(
    string UserId,
    string UserName,
    string? Email,
    string Reason);

public sealed record BatchRepairReport(
    int TotalUsers,
    int AlreadyLinkedCount,
    int RepairedCount,
    int UnresolvedCount,
    IReadOnlyList<RepairedUserItem> RepairedUsers,
    IReadOnlyList<UnresolvedUserItem> UnresolvedUsers);

public sealed record RelinkResult(
    bool Succeeded,
    string? ErrorMessage,
    string? OldEmployeeId,
    string? NewEmployeeId);

public sealed record ForceLogoutResult(
    bool Succeeded,
    int SessionsRevokedCount,
    bool SecurityStampUpdated,
    DateTimeOffset Timestamp,
    string? ErrorMessage);

public sealed record EmployeePickerItem(
    string EmployeeID,
    string Name,
    string? FormationName,
    string? JobTitleName,
    string? Email);

public interface IUserEmployeeService
{
    /// <summary>
    /// Validates the human employee invariant (User MUST be linked to an existing Employee).
    /// If EmployeeID is missing or orphaned, attempts safe auto-repair via EmployeeEmails matching User.Email.
    /// Technical accounts are explicitly exempted.
    /// </summary>
    Task<EmployeeLinkValidationResult> ValidateOrRepairUserEmployeeLinkAsync(
        ApplicationUser user,
        CancellationToken ct = default);

    /// <summary>
    /// Executes an application-level repair process across all users without DB migration.
    /// Finds unlinked users, matches them deterministically against EmployeeEmails, updates EmployeeID,
    /// and returns an audit report.
    /// </summary>
    Task<BatchRepairReport> RepairOrphanedUsersAsync(
        string? actorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches all ApplicationUser accounts officially linked to the given EmployeeID.
    /// An employee may have multiple accounts (e.g. local, email, domain username).
    /// </summary>
    Task<List<ApplicationUser>> GetUsersForEmployeeAsync(
        string employeeId,
        CancellationToken ct = default);

    /// <summary>
    /// Changes the linked employee for a human user from Employee A to Employee B.
    /// Forbidden to set EmployeeID = null for human users. Updates SecurityStamp and logs audit.
    /// </summary>
    Task<RelinkResult> RelinkUserAsync(
        string userId,
        string newEmployeeId,
        string actorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Forcefully terminates all active sessions for a user, updates security stamp,
    /// marks UserDevice records inactive, and records an audit log.
    /// </summary>
    Task<ForceLogoutResult> ForceLogoutUserAsync(
        string userId,
        string actorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Searches employees for the Employee Picker by ID, Name, Email, or Formation.
    /// </summary>
    Task<List<EmployeePickerItem>> SearchEmployeesAsync(
        string query,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies if an employee exists by EmployeeID.
    /// </summary>
    Task<bool> ValidateEmployeeExistsAsync(
        string employeeId,
        CancellationToken ct = default);
}
