namespace API.HR.Node.Services.Pdf;

public interface IEmployeeDossierPdfService
{
    Task<byte[]> GenerateDossierPdfAsync(int employeeId, string? baseUrl = null, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateDossierPdfByEmployeeIdNumberAsync(string employeeIdNumber, string? baseUrl = null, CancellationToken cancellationToken = default);
}
