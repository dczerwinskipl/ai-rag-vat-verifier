using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Embeddings;

namespace VatVerifier.Api.Evaluation.Pipeline;

internal sealed class GtuFastPathStep(ICategoryEmbeddingStore store) : IEvaluationStep
{
    public Task<EvaluateInvoiceLineResponse?> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GtuCode is null)
            return Task.FromResult<EvaluateInvoiceLineResponse?>(null);

        var match = store.FindByGtuCode(request.GtuCode);
        if (match is null)
            return Task.FromResult<EvaluateInvoiceLineResponse?>(null);

        // Score 1.0 distinguishes an exact GTU lookup from embedding cosine similarity scores
        var candidate = new ScoredCategory(match.CategoryId, match.Name, 1.0, match.ExpectedVatRate);
        var classification = new ClassificationResult(CategoryMatchStatus.Matched, [candidate]);

        return Task.FromResult<EvaluateInvoiceLineResponse?>(
            EvaluationResponseFactory.ForClassification(classification, request));
    }
}
