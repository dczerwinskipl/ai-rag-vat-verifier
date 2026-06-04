using Microsoft.Extensions.Options;
using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public sealed class CosineSimilarityClassifier(IOptions<EvaluationOptions> options) : ICategoryClassifier
{
    private readonly EvaluationOptions _options = options.Value;

    public ClassificationResult Classify(IReadOnlyList<ScoredCategory> rankedCandidates, EvaluateInvoiceLineRequest request)
    {
        var top = rankedCandidates.Take(_options.MaxCandidates).ToList();

        if (top.Count == 0)
            return new ClassificationResult(CategoryMatchStatus.NotMatched, []);

        var best = top[0];
        var second = top.Count > 1 ? top[1] : null;

        var strongMarginMet = second is null || best.Score - second.Score >= _options.CandidateMarginThreshold;

        if (best.Score >= _options.StrongCandidateThreshold && strongMarginMet)
            return new ClassificationResult(CategoryMatchStatus.Matched, top);

        if (best.Score >= _options.AmbiguousCandidateThreshold)
            return new ClassificationResult(CategoryMatchStatus.Ambiguous, top);

        return new ClassificationResult(CategoryMatchStatus.NotMatched, top);
    }
}
