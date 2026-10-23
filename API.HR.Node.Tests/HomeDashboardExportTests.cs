using System.Reflection;
using System.Text;
using API.HR.Node.Components.Pages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class HomeDashboardExportTests
{
    [Fact]
    public async Task DownloadFailurePreservesSearchAndPageAndProvidesRecovery()
    {
        var js = new ExportJsRuntime { Fail = true };
        var home = CreateHome(js);
        SetProperty(home, "modalSearchTerm", "Test");
        SetField(home, "reportPage", 2);

        await Export(home);

        Assert.Equal("Test", GetProperty(home, "modalSearchTerm"));
        Assert.Equal(2, GetField(home, "reportPage"));
        Assert.Equal(true, GetField(home, "exportFailed"));
        Assert.Equal(false, GetField(home, "isExporting"));
        Assert.Contains("أعد المحاولة", Assert.IsType<string>(GetField(home, "exportFeedback")));
    }

    [Fact]
    public async Task ExportIncludesEverySearchResultAcrossPagesAndItsScope()
    {
        var js = new ExportJsRuntime();
        var home = CreateHome(js);
        SetProperty(home, "modalSearchTerm", "Test");
        SetField(home, "reportPage", 2);
        SetField(home, "loadedScopeDescription", "التشكيل: اختبار");

        await Export(home);

        Assert.Equal(1, js.Calls);
        var csv = Encoding.UTF8.GetString(Convert.FromBase64String(Assert.IsType<string>(js.Arguments![0])));
        Assert.Contains("عدد النتائج,120", csv);
        Assert.Contains("التشكيل: اختبار", csv);
        Assert.Contains("E001", csv);
        Assert.Contains("E120", csv);
        Assert.Equal(2, GetField(home, "reportPage"));
        Assert.Equal(false, GetField(home, "exportFailed"));
    }

    [Fact]
    public async Task InProgressExportCannotStartADuplicateDownload()
    {
        var js = new ExportJsRuntime();
        var home = CreateHome(js);
        SetField(home, "isExporting", true);
        await Export(home);
        Assert.Equal(0, js.Calls);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+964", "\"'+964\"")]
    [InlineData("-1", "\"'-1\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("أحمد علي", "\"أحمد علي\"")]
    [InlineData("", "\"\"")]
    public void CsvCellsThatASpreadsheetWouldRunAsFormulasOpenAsText(string value, string expected) =>
        Assert.Equal(expected, Home.EscapeCsv(value));

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Home CreateHome(ExportJsRuntime js)
    {
        var home = new Home();
        SetProperty(home, "JSRuntime", js);
        SetProperty(home, "Logger", NullLogger<Home>.Instance);
        SetField(home, "currentModalRows", Enumerable.Range(1, 120).Select(i => new Home.QuickReportRow
        {
            EmployeeId = $"E{i:000}", Name = $"Test {i}", Formation = "اختبار", JobTitle = "موظف", DetailValue = "بدون صورة"
        }).ToList());
        return home;
    }
    private static Task Export(Home home) => (Task)typeof(Home).GetMethod("ExportModalReportCsv", PrivateInstance)!.Invoke(home, null)!;
    private static void SetField(Home home, string name, object value) => typeof(Home).GetField(name, PrivateInstance)!.SetValue(home, value);
    private static object? GetField(Home home, string name) => typeof(Home).GetField(name, PrivateInstance)!.GetValue(home);
    private static void SetProperty(Home home, string name, object value) => typeof(Home).GetProperty(name, PrivateInstance)!.SetValue(home, value);
    private static object? GetProperty(Home home, string name) => typeof(Home).GetProperty(name, PrivateInstance)!.GetValue(home);

    private sealed class ExportJsRuntime : IJSRuntime
    {
        internal bool Fail;
        internal int Calls;
        internal object?[]? Arguments;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls++;
            Arguments = args;
            return Fail ? ValueTask.FromException<TValue>(new InvalidOperationException("Simulated download failure")) : ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
