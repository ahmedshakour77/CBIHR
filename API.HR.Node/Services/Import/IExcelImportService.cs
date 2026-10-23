using API.HR.Node.Contracts;

namespace API.HR.Node.Services.Import;

public interface IExcelImportService
{
    Task<ExcelParseResult<EmployeeImportRow>> ParseEmployeesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<EmployeeImportResponse> ImportEmployeesAsync(EmployeeImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<AppreciationLetterImportRow>> ParseAppreciationLettersExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<AppreciationLetterImportResponse> ImportAppreciationLettersAsync(AppreciationLetterImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<LeaveImportRow>> ParseLeavesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<LeaveImportResponse> ImportLeavesAsync(LeaveImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<CareerMovementImportRow>> ParseCareerMovementsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<CareerMovementImportResponse> ImportCareerMovementsAsync(CareerMovementImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<CourseImportRow>> ParseCoursesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<CourseImportResponse> ImportCoursesAsync(CourseImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<EmployeeCivilDataImportRow>> ParseCivilDataExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<EmployeeCivilDataImportResponse> ImportCivilDataAsync(EmployeeCivilDataImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<EmployeeEmailAccountImportRow>> ParseEmailAccountsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<EmployeeEmailAccountImportResponse> ImportEmailAccountsAsync(EmployeeEmailAccountImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<BankingAllowanceImportRow>> ParseBankingAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ImportBankingAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<BankingAllowanceImportRow>> ParseEngineeringAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ImportEngineeringAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<BankingAllowanceImportRow>> ParseLegalAllowancesExcelAsync(Stream fileStream, decimal defaultPercentage = 35m, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ImportLegalAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<BankingAllowanceImportRow>> ParseCertificateAllowancesExcelAsync(Stream fileStream, Dictionary<string, decimal>? customRates = null, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ImportCertificateAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ApplyCertificateAllowancesToAllEmployeesAsync(DateOnly? effectiveStartDate = null, string? orderNumber = null, string? changeReason = null, Dictionary<string, decimal>? customRates = null, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<BankingAllowanceImportRow>> ParseNonPracticeAllowancesExcelAsync(Stream fileStream, decimal defaultPercentage = 20m, CancellationToken cancellationToken = default);
    Task<BankingAllowanceImportResponse> ImportNonPracticeAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<LeaveBalanceImportRow>> ParseLeaveBalancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<LeaveBalanceImportResponse> ImportLeaveBalancesAsync(LeaveBalanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<CraftAndSocialAllowanceImportRow>> ParseCraftAndSocialAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<CraftAndSocialAllowanceImportResponse> ImportCraftAndSocialAllowancesAsync(CraftAndSocialAllowanceImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<EmployeePenaltyImportRow>> ParsePenaltiesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<EmployeePenaltyImportResponse> ImportPenaltiesAsync(EmployeePenaltyImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<MaritalStatusImportRow>> ParseMaritalStatusExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<MaritalStatusImportResponse> ImportMaritalStatusAsync(MaritalStatusImportRequest request, CancellationToken cancellationToken = default);

    Task<MartyrsImportPreviewResponse> ParseMartyrsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<MartyrsImportResult> ImportMartyrsAsync(MartyrsImportRequest request, string username, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<FinancialAccountImportRow>> ParseFinancialAccountsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<FinancialAccountImportResponse> ImportFinancialAccountsAsync(FinancialAccountImportRequest request, string? username = null, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<JobTitleImportRow>> ParseJobTitlesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<JobTitleImportResponse> ImportJobTitlesAsync(JobTitleImportRequest request, CancellationToken cancellationToken = default);

    Task<ExcelParseResult<LastAllowanceDateImportRow>> ParseLastAllowanceDateExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<LastAllowanceDateImportResponse> ImportLastAllowanceDateAsync(LastAllowanceDateImportRequest request, CancellationToken cancellationToken = default);

    byte[] GenerateEmployeesTemplate();
    byte[] GenerateAppreciationLettersTemplate();
    byte[] GenerateLeavesTemplate();
    byte[] GenerateLeaveBalancesTemplate();
    byte[] GenerateCareerMovementsTemplate();
    byte[] GenerateCoursesTemplate();
    byte[] GenerateCivilDataTemplate();
    byte[] GenerateEmailAccountsTemplate();
    byte[] GenerateBankingAllowancesTemplate();
    byte[] GenerateEngineeringAllowancesTemplate();
    byte[] GenerateLegalAllowancesTemplate();
    byte[] GenerateCertificateAllowancesTemplate();
    byte[] GenerateNonPracticeAllowancesTemplate();
    byte[] GenerateCraftAndSocialAllowancesTemplate();
    byte[] GenerateMaritalStatusTemplate();
    byte[] GeneratePenaltiesTemplate();
    byte[] GenerateMartyrsTemplate();
    byte[] GenerateFinancialAccountsTemplate();
    byte[] GenerateJobTitlesTemplate();
    byte[] GenerateLastAllowanceDateTemplate();
}
