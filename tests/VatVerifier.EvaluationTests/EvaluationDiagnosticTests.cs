using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VatVerifier.Api.Contracts;
using VatVerifier.EvaluationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace VatVerifier.EvaluationTests;

/// <summary>
/// Runs all evaluation cases and writes a detailed diagnostic report showing per-category
/// embedding scores, classification decisions, and pass/fail status.
/// Run with: dotnet test --filter "Category=Diagnostic"
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class EvaluationDiagnosticTests : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DiagnosticFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public EvaluationDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
        _factory = new DiagnosticFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GenerateDiagnosticReport()
    {
        var cases = (await DatasetLoader.LoadAsync("invoice-line-evaluation-cases.json"))
            .OrderBy(c => c.Id)
            .ToList();

        var results = new List<CaseResult>();

        foreach (var tc in cases)
        {
            _factory.LogProvider.Clear();

            var httpResp = await _client.PostAsJsonAsync("/invoice-lines/evaluate", tc.Input, JsonOpts);
            httpResp.EnsureSuccessStatusCode();

            var response = await httpResp.Content.ReadFromJsonAsync<EvaluateInvoiceLineResponse>(JsonOpts);

            // Small delay so async log entries flush before we snapshot
            await Task.Delay(50);

            var logs = _factory.LogProvider.Entries.ToList();
            results.Add(new CaseResult(tc, response, logs));
        }

        // Write markdown report
        var reportPath = Path.Combine(AppContext.BaseDirectory, "diagnostic-report.md");
        var markdown = BuildReport(results);
        await File.WriteAllTextAsync(reportPath, markdown, Encoding.UTF8);

        // Summary to test output
        var failing = results.Where(r => !r.IsPass).ToList();
        var passing = results.Where(r => r.IsPass).ToList();

        _output.WriteLine($"=== DIAGNOSTIC REPORT ===");
        _output.WriteLine($"Total: {results.Count}  Passing: {passing.Count}  Failing: {failing.Count}");
        _output.WriteLine($"Report: {reportPath}");
        _output.WriteLine("");

        foreach (var r in failing)
        {
            _output.WriteLine($"FAIL {r.Case.Id} — {r.Case.Name}");
            _output.WriteLine($"  Expected : {r.Case.Expected.Severity}/{r.Case.Expected.CategoryMatchStatus}/{r.Case.Expected.VatValidationStatus}/{r.Case.Expected.ReasonCode}");
            _output.WriteLine($"  Actual   : {r.Response?.Severity}/{r.Response?.CategoryMatchStatus}/{r.Response?.VatValidationStatus}/{r.Response?.ReasonCode}");
            foreach (var log in r.Logs)
                _output.WriteLine($"  {log}");
            _output.WriteLine("");
        }
    }

    private static string BuildReport(List<CaseResult> results)
    {
        var sb = new StringBuilder();
        var failing = results.Where(r => !r.IsPass).ToList();
        var passing = results.Where(r => r.IsPass).ToList();

        sb.AppendLine("# VAT Evaluator — Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine($"**Total**: {results.Count} | **Passing**: {passing.Count} | **Failing**: {failing.Count}");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Status | ID | Name | Expected Sev | Actual Sev | Expected Match | Actual Match |");
        sb.AppendLine("|--------|-----|------|-------------|------------|----------------|--------------|");
        foreach (var r in results)
        {
            var icon = r.IsPass ? "✓" : "✗";
            sb.AppendLine(
                $"| {icon} | {r.Case.Id} | {r.Case.Name[..Math.Min(r.Case.Name.Length, 55)]} | " +
                $"{r.Case.Expected.Severity} | {r.Response?.Severity} | " +
                $"{r.Case.Expected.CategoryMatchStatus} | {r.Response?.CategoryMatchStatus} |");
        }

        // Detailed failing cases
        if (failing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Failing Cases — Detail");

            foreach (var r in failing)
            {
                sb.AppendLine();
                sb.AppendLine($"### {r.Case.Id} — {r.Case.Name}");
                sb.AppendLine();
                sb.AppendLine("**Input**");
                sb.AppendLine($"- Description: `{r.Case.Input.Description}`");
                sb.AppendLine($"- Supplier: {r.Case.Input.SupplierName} | {r.Case.Input.SupplierIndustry}");
                sb.AppendLine($"- Invoice VAT: {r.Case.Input.InvoiceVatRate}%");
                if (r.Case.Input.GtuCode is not null) sb.AppendLine($"- GTU: {r.Case.Input.GtuCode}");
                sb.AppendLine();
                sb.AppendLine("**Result**");
                sb.AppendLine($"| Field | Expected | Actual |");
                sb.AppendLine($"|-------|----------|--------|");
                sb.AppendLine($"| Severity | {r.Case.Expected.Severity} | {r.Response?.Severity} |");
                sb.AppendLine($"| CategoryMatchStatus | {r.Case.Expected.CategoryMatchStatus} | {r.Response?.CategoryMatchStatus} |");
                sb.AppendLine($"| VatValidationStatus | {r.Case.Expected.VatValidationStatus} | {r.Response?.VatValidationStatus} |");
                sb.AppendLine($"| ReasonCode | {r.Case.Expected.ReasonCode} | {r.Response?.ReasonCode} |");
                sb.AppendLine();

                if (r.Logs.Count > 0)
                {
                    sb.AppendLine("**Engine Trace**");
                    sb.AppendLine("```");
                    foreach (var log in r.Logs)
                        sb.AppendLine(log);
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine("_(no debug logs captured — may be a GTU/structural fast-path case)_");
                }
            }
        }

        // Also include passing cases with their scores (for reference / regression guard)
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Passing Cases — Score Reference");
        sb.AppendLine();
        sb.AppendLine("Included so you can verify that tuning changes don't regress already-passing cases.");
        sb.AppendLine();

        foreach (var r in passing)
        {
            if (r.Logs.Count == 0) continue; // GTU fast-path, skip
            sb.AppendLine($"<details><summary>{r.Case.Id} — {r.Case.Name}</summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var log in r.Logs)
                sb.AppendLine(log);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class CaseResult(
        EvaluationCase testCase,
        EvaluateInvoiceLineResponse? response,
        List<string> logs)
    {
        public EvaluationCase Case { get; } = testCase;
        public EvaluateInvoiceLineResponse? Response { get; } = response;
        public List<string> Logs { get; } = logs;

        public bool IsPass =>
            Response is not null &&
            Response.Severity == Case.Expected.Severity &&
            Response.CategoryMatchStatus == Case.Expected.CategoryMatchStatus &&
            Response.VatValidationStatus == Case.Expected.VatValidationStatus &&
            Response.ReasonCode == Case.Expected.ReasonCode;
    }

    private sealed class DiagnosticFactory : WebApplicationFactory<Program>
    {
        public CapturingLoggerProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices((ctx, services) =>
            {
                services.AddLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                    logging.AddProvider(LogProvider);
                });
            });
        }
    }
}
