using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Evaluation;

namespace VatVerifier.Api.Classification;

public sealed class CosineSimilarityClassifier(
    IOptions<EvaluationOptions> options,
    ILogger<CosineSimilarityClassifier>? logger = null) : ICategoryClassifier
{
    private readonly EvaluationOptions _options = options.Value;

    public ClassificationResult Classify(IReadOnlyList<ScoredCategory> rankedCandidates, EvaluateInvoiceLineRequest request)
    {
        // Take the fused top-N, then re-sort by adjScore so threshold/margin comparisons
        // are consistent regardless of how the supplier channel may have reordered the list.
        var top = rankedCandidates
            .Take(_options.MaxCandidates)
            .OrderByDescending(c => c.Score)
            .ToList();

        if (top.Count == 0)
            return new ClassificationResult(CategoryMatchStatus.NotMatched, []);

        var best = top[0];
        var second = top.Count > 1 ? top[1] : null;
        var margin = second is null ? double.MaxValue : best.Score - second.Score;
        var strongMarginMet = margin >= _options.CandidateMarginThreshold;

        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug(
                "[{Id}] Classifier: best={Best}({BestScore:F4}) second={Second}({SecondScore:F4}) " +
                "margin={Margin:F4} marginThr={MarginThr:F4} strongThr={StrongThr:F4} ambigThr={AmbigThr:F4}",
                request.InvoiceLineId,
                best.CategoryId, best.Score,
                second?.CategoryId ?? "none", second?.Score ?? 0.0,
                margin == double.MaxValue ? 0.0 : margin,
                _options.CandidateMarginThreshold,
                _options.StrongCandidateThreshold,
                _options.AmbiguousCandidateThreshold);
        }

        if (best.Score >= _options.StrongCandidateThreshold && strongMarginMet)
        {
            logger?.LogDebug("[{Id}] → Matched (score≥{Thr:F4}, margin≥{Margin:F4})",
                request.InvoiceLineId, _options.StrongCandidateThreshold, _options.CandidateMarginThreshold);
            return new ClassificationResult(CategoryMatchStatus.Matched, top);
        }

        // Ambiguous only when a second independent candidate also clears the floor.
        if (best.Score >= _options.AmbiguousCandidateThreshold &&
            second is not null && second.Score >= _options.AmbiguousCandidateThreshold)
        {
            logger?.LogDebug("[{Id}] → Ambiguous (both≥{Thr:F4})",
                request.InvoiceLineId, _options.AmbiguousCandidateThreshold);
            return new ClassificationResult(CategoryMatchStatus.Ambiguous, top);
        }

        logger?.LogDebug("[{Id}] → NotMatched (best={BestScore:F4} < thr={Thr:F4})",
            request.InvoiceLineId, best.Score, _options.AmbiguousCandidateThreshold);
        return new ClassificationResult(CategoryMatchStatus.NotMatched, top);
    }
}
