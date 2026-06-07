using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Embeddings;
using VatVerifier.Api.Evaluation;

namespace VatVerifier.Api.Evaluation.Pipeline;

internal sealed class EmbeddingClassificationStep(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ICategoryEmbeddingStore store,
    ICategoryClassifier classifier,
    IOptions<EvaluationOptions> options,
    ILogger<EmbeddingClassificationStep> logger) : IEvaluationStep
{
    private readonly EvaluationOptions _opts = options.Value;

    public async Task<EvaluateInvoiceLineResponse?> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        var categories = store.GetAll();

        // --- Channel 1: description embedding with negative penalty ---
        var descEmbeddings = await embeddingGenerator.GenerateAsync(
            [request.Description], cancellationToken: cancellationToken);
        var descVector = descEmbeddings[0].Vector.ToArray();

        var descScores = categories
            .Select(c =>
            {
                var posSim = (double)TensorPrimitives.CosineSimilarity<float>(descVector, c.PositiveVector);
                // Max-similarity across individual negative vectors eliminates centroid dilution:
                // a single highly-similar negative example produces the full penalty it deserves.
                var negSim = c.NegativeVectors.Length > 0
                    ? c.NegativeVectors.Max(nv => (double)TensorPrimitives.CosineSimilarity<float>(descVector, nv))
                    : 0.0;
                return (category: c, posSim, negSim, adjScore: posSim - _opts.NegativePenaltyWeight * negSim);
            })
            .ToList();

        // desc_rank: index in descending-adjScore order (0 = best)
        var descRanked = descScores
            .OrderByDescending(x => x.adjScore)
            .Select((x, rank) => (x.category.CategoryId, rank))
            .ToDictionary(x => x.CategoryId, x => x.rank);

        // --- Channel 2: supplier embedding ---
        var supplierText = BuildSupplierText(request);
        var (supplierRanked, supplierSims) = await BuildSupplierDataAsync(supplierText, categories, cancellationToken);

        // --- wRRF fusion ---
        var k = _opts.RrfK;
        var wDesc = _opts.DescriptionChannelWeight;
        var wSupp = _opts.SupplierChannelWeight;

        var enriched = descScores
            .Select(x =>
            {
                var dRank = descRanked[x.category.CategoryId];
                var sRank = supplierRanked[x.category.CategoryId];
                var sSim = supplierSims.GetValueOrDefault(x.category.CategoryId);
                var fusedScore = wDesc * (1.0 / (k + dRank)) + wSupp * (1.0 / (k + sRank));
                return (
                    cat: new ScoredCategory(x.category.CategoryId, x.category.Name, x.adjScore, x.category.ExpectedVatRate),
                    x.posSim, x.negSim, x.adjScore, dRank, sRank, sSim, fusedScore);
            })
            .OrderByDescending(x => x.fusedScore)
            .ToList();

        var fused = enriched.Select(x => x.cat).ToList();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            var lines = enriched.Take(10).Select((x, i) =>
                $"  #{i + 1:D2} {x.cat.CategoryId,-35} pos={x.posSim:F4} neg={x.negSim:F4} adj={x.adjScore:F4} " +
                $"dRk={x.dRank:D2} sRk={x.sRank:D2} sSim={x.sSim:F4} fused={x.fusedScore:F6}");
            logger.LogDebug("[{Id}] Scoring breakdown:\n{Lines}", request.InvoiceLineId, string.Join("\n", lines));
        }

        var classification = classifier.Classify(fused, request);
        return EvaluationResponseFactory.ForClassification(classification, request);
    }

    private static string BuildSupplierText(EvaluateInvoiceLineRequest r)
    {
        var name = r.SupplierName?.Trim();
        var industry = r.SupplierIndustry?.Trim();

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(industry))
            return string.Empty;

        return string.IsNullOrEmpty(industry) ? name! :
               string.IsNullOrEmpty(name) ? industry! :
               $"{name} | {industry}";
    }

    private async Task<(Dictionary<string, int> ranks, Dictionary<string, double> sims)> BuildSupplierDataAsync(
        string supplierText,
        IReadOnlyList<StoredCategory> categories,
        CancellationToken ct)
    {
        var lastRank = categories.Count - 1;
        if (string.IsNullOrEmpty(supplierText))
        {
            var ranks = categories.ToDictionary(c => c.CategoryId, _ => lastRank);
            var sims = categories.ToDictionary(c => c.CategoryId, _ => 0.0);
            return (ranks, sims);
        }

        var supplierEmbeddings = await embeddingGenerator.GenerateAsync([supplierText], cancellationToken: ct);
        var supplierVector = supplierEmbeddings[0].Vector.ToArray();

        if (supplierVector.Length == 0)
        {
            var ranks = categories.ToDictionary(c => c.CategoryId, _ => lastRank);
            var sims = categories.ToDictionary(c => c.CategoryId, _ => 0.0);
            return (ranks, sims);
        }

        var withSims = categories
            .Select(c => (c.CategoryId,
                sim: c.SupplierVector.Length > 0
                    ? (double)TensorPrimitives.CosineSimilarity<float>(supplierVector, c.SupplierVector)
                    : 0.0))
            .ToList();

        var ranked = withSims
            .OrderByDescending(x => x.sim)
            .Select((x, rank) => (x.CategoryId, rank))
            .ToDictionary(x => x.CategoryId, x => x.rank);

        var simsDict = withSims.ToDictionary(x => x.CategoryId, x => x.sim);
        return (ranked, simsDict);
    }
}
