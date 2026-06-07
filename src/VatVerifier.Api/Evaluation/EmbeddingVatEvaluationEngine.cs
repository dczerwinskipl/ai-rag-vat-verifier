using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Embeddings;
using VatVerifier.Api.Evaluation.Pipeline;

namespace VatVerifier.Api.Evaluation;

public sealed class EmbeddingVatEvaluationEngine : IVatEvaluationEngine
{
    private readonly ICategoryEmbeddingStore _store;
    private readonly IReadOnlyList<IEvaluationStep> _pipeline;
    private readonly ILogger<EmbeddingVatEvaluationEngine> _logger;
    private readonly double _confidenceThreshold;

    public EmbeddingVatEvaluationEngine(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ICategoryEmbeddingStore store,
        ICategoryClassifier classifier,
        IOptions<EvaluationOptions> options,
        ILogger<EmbeddingVatEvaluationEngine> logger,
        ILoggerFactory loggerFactory)
    {
        _store = store;
        _logger = logger;
        _confidenceThreshold = options.Value.ConfidenceThreshold;
        _pipeline =
        [
            new StructuralCheckStep(),
            new GtuFastPathStep(store),
            new EmbeddingClassificationStep(embeddingGenerator, store, classifier, options,
                loggerFactory.CreateLogger<EmbeddingClassificationStep>())
        ];
    }

    public async Task<EvaluateInvoiceLineResponse> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.ReadyAsync.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EvaluationResponseFactory.ForDegradedState(request, ex);
        }

        foreach (var step in _pipeline)
        {
            var result = await step.EvaluateAsync(request, cancellationToken);
            if (result is not null)
            {
                result = TryApplyRateVariantDegradation(result) ?? result;
                LogLowConfidenceIfNeeded(result);
                return result;
            }
        }

        return EvaluationResponseFactory.ForDegradedState(request,
            new InvalidOperationException("No pipeline step produced a result."));
    }

    private EvaluateInvoiceLineResponse? TryApplyRateVariantDegradation(EvaluateInvoiceLineResponse response)
    {
        if (response.CategoryMatchStatus != CategoryMatchStatus.Matched) return null;

        var topCategoryId = response.CategoryCandidates.FirstOrDefault()?.CategoryId;
        if (topCategoryId is null) return null;

        var stored = _store.FindByCategoryId(topCategoryId);
        if (stored?.RateVariantRates is not { Count: > 0 } variantRates) return null;

        return EvaluationResponseFactory.ForRateVariantDegradation(response, variantRates);
    }

    private void LogLowConfidenceIfNeeded(EvaluateInvoiceLineResponse response)
    {
        if (!response.CategoryCandidates.Any()) return;

        var topScore = response.CategoryCandidates.Max(c => c.Score);

        // Score of exactly 1.0 is a GTU exact-match — deterministic, not a confidence concern
        if (topScore >= 1.0) return;

        if (response.CategoryMatchStatus != CategoryMatchStatus.Ambiguous
            && topScore >= _confidenceThreshold) return;

        _logger.LogInformation(
            "Low confidence evaluation: invoiceLineId={InvoiceLineId} score={Score:F4} categoryMatchStatus={CategoryMatchStatus} severity={Severity} reasonCode={ReasonCode}",
            response.InvoiceLineId,
            topScore,
            response.CategoryMatchStatus,
            response.Severity,
            response.ReasonCode);
    }
}
