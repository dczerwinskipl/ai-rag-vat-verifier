using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using VatVerifier.Api.Contracts;
using VatVerifier.EvaluationTests.Infrastructure;
using Xunit;

namespace VatVerifier.EvaluationTests;

public sealed class VatEvaluationApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<EvaluationCase> AllCases()
    {
        var data = new TheoryData<EvaluationCase>();
        foreach (var c in DatasetLoader.LoadAsync("invoice-line-evaluation-cases.json").GetAwaiter().GetResult())
            data.Add(c);
        return data;
    }

    /// <summary>
    /// Runs without Ollama: warmup fails gracefully → engine returns InsufficientData → 200 OK.
    /// Runs with Ollama: returns real classification results.
    /// Each dataset case is a separate test.
    /// </summary>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCases))]
    public async Task Evaluate_ShouldReturnOkResponse(EvaluationCase testCase)
    {
        var httpResponse = await _client.PostAsJsonAsync("/invoice-lines/evaluate", testCase.Input);

        httpResponse.EnsureSuccessStatusCode();
        var response = await httpResponse.Content.ReadFromJsonAsync<EvaluateInvoiceLineResponse>();
        response.Should().NotBeNull();
        response!.InvoiceLineId.Should().Be(testCase.Input.InvoiceLineId);
    }

    /// <summary>
    /// Requires Ollama running with qwen3-embedding:0.6b pulled.
    /// To run: remove the Skip attribute.
    /// CI gateway: dotnet test --filter "Category!=AI"
    /// Each dataset case is a separate test.
    /// </summary>
    [Theory]
    [Trait("Category", "AI")]
    [MemberData(nameof(AllCases))]
    public async Task Evaluate_ShouldMatchExpectedEvaluation(EvaluationCase testCase)
    {
        var httpResponse = await _client.PostAsJsonAsync("/invoice-lines/evaluate", testCase.Input);
        httpResponse.EnsureSuccessStatusCode();

        var response = await httpResponse.Content.ReadFromJsonAsync<EvaluateInvoiceLineResponse>();
        response.Should().NotBeNull();

        response!.Severity.Should().Be(testCase.Expected.Severity, testCase.Name);
        response.CategoryMatchStatus.Should().Be(testCase.Expected.CategoryMatchStatus, testCase.Name);
        response.VatValidationStatus.Should().Be(testCase.Expected.VatValidationStatus, testCase.Name);
        response.ReasonCode.Should().Be(testCase.Expected.ReasonCode, testCase.Name);
    }
}
