using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public interface ICategoryClassifier
{
    ClassificationResult Classify(IReadOnlyList<ScoredCategory> rankedCandidates, EvaluateInvoiceLineRequest request);
}

public sealed record ScoredCategory(string CategoryId, string Name, double Score, decimal ExpectedVatRate);

public sealed record ClassificationResult(CategoryMatchStatus Status, IReadOnlyList<ScoredCategory> TopCandidates);
