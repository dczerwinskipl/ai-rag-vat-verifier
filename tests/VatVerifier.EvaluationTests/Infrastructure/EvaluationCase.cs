using System.Text.Json;
using System.Text.Json.Serialization;
using VatVerifier.Api.Contracts;
using Xunit.Abstractions;

namespace VatVerifier.EvaluationTests.Infrastructure;

public sealed class EvaluationCase : IXunitSerializable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EvaluateInvoiceLineRequest Input { get; set; } = null!;
    public ExpectedEvaluation Expected { get; set; } = null!;
    /// <summary>Set to non-null to mark a case as a known model limitation — the assertion test is skipped.</summary>
    public string? KnownLimitation { get; set; }

    public EvaluationCase() { }

    public EvaluationCase(string id, string name, EvaluateInvoiceLineRequest input, ExpectedEvaluation expected,
        string? knownLimitation = null)
        => (Id, Name, Input, Expected, KnownLimitation) = (id, name, input, expected, knownLimitation);

    public void Serialize(IXunitSerializationInfo info) =>
        info.AddValue("v", JsonSerializer.Serialize(this, JsonOptions));

    public void Deserialize(IXunitSerializationInfo info)
    {
        var src = JsonSerializer.Deserialize<EvaluationCase>(info.GetValue<string>("v")!, JsonOptions)!;
        (Id, Name, Input, Expected, KnownLimitation) = (src.Id, src.Name, src.Input, src.Expected, src.KnownLimitation);
    }

    public override string ToString() => Name;
}

public sealed record ExpectedEvaluation(
    EvaluationSeverity Severity,
    CategoryMatchStatus CategoryMatchStatus,
    VatValidationStatus VatValidationStatus,
    EvaluationReasonCode ReasonCode);
