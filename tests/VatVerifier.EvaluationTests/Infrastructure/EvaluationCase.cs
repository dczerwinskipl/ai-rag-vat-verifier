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

    public EvaluationCase() { }

    public EvaluationCase(string id, string name, EvaluateInvoiceLineRequest input, ExpectedEvaluation expected)
        => (Id, Name, Input, Expected) = (id, name, input, expected);

    public void Serialize(IXunitSerializationInfo info) =>
        info.AddValue("v", JsonSerializer.Serialize(this, JsonOptions));

    public void Deserialize(IXunitSerializationInfo info)
    {
        var src = JsonSerializer.Deserialize<EvaluationCase>(info.GetValue<string>("v")!, JsonOptions)!;
        (Id, Name, Input, Expected) = (src.Id, src.Name, src.Input, src.Expected);
    }

    public override string ToString() => Name;
}

public sealed record ExpectedEvaluation(
    EvaluationSeverity Severity,
    CategoryMatchStatus CategoryMatchStatus,
    VatValidationStatus VatValidationStatus,
    EvaluationReasonCode ReasonCode);
