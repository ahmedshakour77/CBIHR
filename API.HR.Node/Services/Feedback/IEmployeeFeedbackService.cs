using System.Security.Claims;
using API.HR.Node.Data.Feedback;

namespace API.HR.Node.Services.Feedback;

public record FeedbackSubmissionDto
{
    public string FeedbackType { get; init; } = "فكرة";
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public Stream? FileStream { get; init; }
    public long? FileSize { get; init; }
}

public record FeedbackSubmissionResult(
    bool Success,
    string? ReferenceNumber = null,
    string? ErrorMessage = null,
    EmployeeFeedback? Feedback = null);

public record FeedbackFilterDto(
    string? Type = null,
    FeedbackStatus? Status = null,
    string? SearchTerm = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 25);

public record FeedbackPagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record FeedbackKpisDto(
    int TotalCount,
    int PendingCount,
    int InProgressCount,
    int ResolvedCount,
    int RejectedCount,
    int IdeasCount,
    int NotesCount,
    int InquiriesCount,
    int ComplaintsCount,
    int OthersCount);

public interface IEmployeeFeedbackService
{
    Task<FeedbackSubmissionResult> SubmitFeedbackAsync(
        FeedbackSubmissionDto dto,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default);

    Task<FeedbackPagedResult<EmployeeFeedback>> GetFeedbacksForAdminAsync(
        FeedbackFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<FeedbackKpisDto> GetFeedbackKpisAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeFeedback>> GetFeedbacksForEmployeeAsync(
        int? employeeId,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<EmployeeFeedback?> GetFeedbackByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateFeedbackStatusAsync(
        int id,
        FeedbackStatus status,
        string? adminNotes,
        ClaimsPrincipal adminUser,
        CancellationToken cancellationToken = default);

    Task<(Stream? Stream, string FileName, string ContentType)?> GetAttachmentStreamAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
