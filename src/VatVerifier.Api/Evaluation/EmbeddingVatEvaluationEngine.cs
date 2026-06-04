using System.Numerics.Tensors;
using System.Text;
using Microsoft.Extensions.AI;
using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public sealed class EmbeddingVatEvaluationEngine(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ICategoryEmbeddingStore store,
    ICategoryClassifier classifier) : IVatEvaluationEngine
{
    public async Task<EvaluateInvoiceLineResponse> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.ReadyAsync.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BuildDegradedResponse(request, ex);
        }

        var queryText = BuildQueryText(request);
        var embeddings = await embeddingGenerator.GenerateAsync([queryText], cancellationToken: cancellationToken);
        var queryVector = embeddings[0].Vector.ToArray();

        var scored = store.GetAll()
            .Select(c => new ScoredCategory(
                c.CategoryId,
                c.Name,
                (double)TensorPrimitives.CosineSimilarity<float>(queryVector, c.Vector),
                c.ExpectedVatRate))
            .OrderByDescending(s => s.Score)
            .ToList();

        var classification = classifier.Classify(scored, request);
        return BuildResponse(request, classification);
    }

    private static EvaluateInvoiceLineResponse BuildResponse(EvaluateInvoiceLineRequest request, ClassificationResult classification)
    {
        var top = classification.TopCandidates;
        var candidateDtos = top
            .Select(c => new CategoryCandidateDto(c.CategoryId, c.Name, c.Score, c.ExpectedVatRate))
            .ToList();

        return classification.Status switch
        {
            CategoryMatchStatus.Matched => BuildMatchedResponse(request, top, candidateDtos),
            CategoryMatchStatus.Ambiguous => BuildAmbiguousResponse(request, top, candidateDtos),
            _ => new EvaluateInvoiceLineResponse(
                request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.NotMatched,
                VatValidationStatus.Unknown, request.InvoiceVatRate, [],
                EvaluationReasonCode.CategoryNotMatched, candidateDtos, "No matching category found.")
        };
    }

    private static EvaluateInvoiceLineResponse BuildMatchedResponse(
        EvaluateInvoiceLineRequest request,
        IReadOnlyList<ScoredCategory> top,
        IReadOnlyList<CategoryCandidateDto> candidateDtos)
    {
        var expectedRate = top[0].ExpectedVatRate;
        if (request.InvoiceVatRate == expectedRate)
            return new EvaluateInvoiceLineResponse(
                request.InvoiceLineId, EvaluationSeverity.Ok, CategoryMatchStatus.Matched,
                VatValidationStatus.Match, request.InvoiceVatRate, [expectedRate],
                EvaluationReasonCode.VatMatched, candidateDtos,
                $"VAT rate {request.InvoiceVatRate}% matches the expected rate for this category.");

        return new EvaluateInvoiceLineResponse(
            request.InvoiceLineId, EvaluationSeverity.Critical, CategoryMatchStatus.Matched,
            VatValidationStatus.Mismatch, request.InvoiceVatRate, [expectedRate],
            EvaluationReasonCode.VatMismatch, candidateDtos,
            $"VAT mismatch: invoice has {request.InvoiceVatRate}%, expected {expectedRate}%.");
    }

    private static EvaluateInvoiceLineResponse BuildAmbiguousResponse(
        EvaluateInvoiceLineRequest request,
        IReadOnlyList<ScoredCategory> top,
        IReadOnlyList<CategoryCandidateDto> candidateDtos)
    {
        var distinctRates = top.Select(c => c.ExpectedVatRate).Distinct().ToList();

        if (distinctRates.Count == 1 && request.InvoiceVatRate == distinctRates[0])
            return new EvaluateInvoiceLineResponse(
                request.InvoiceLineId, EvaluationSeverity.Warning, CategoryMatchStatus.Ambiguous,
                VatValidationStatus.Match, request.InvoiceVatRate, distinctRates,
                EvaluationReasonCode.CategoryAmbiguousButVatConsistent, candidateDtos,
                "Category is ambiguous but all candidates agree on the VAT rate.");

        return new EvaluateInvoiceLineResponse(
            request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.Ambiguous,
            VatValidationStatus.Unknown, request.InvoiceVatRate, distinctRates,
            EvaluationReasonCode.CategoryAmbiguousWithDifferentVatRates, candidateDtos,
            "Category is ambiguous with candidates having different VAT rates.");
    }

    private static EvaluateInvoiceLineResponse BuildDegradedResponse(EvaluateInvoiceLineRequest request, Exception ex) =>
        new(request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.NotMatched,
            VatValidationStatus.Unknown, request.InvoiceVatRate, [],
            EvaluationReasonCode.InsufficientData, [],
            $"Evaluation engine not ready: {ex.Message}");

    private static string BuildQueryText(EvaluateInvoiceLineRequest r)
    {
        var sb = new StringBuilder($"{r.Description} | {r.SupplierName}");
        if (!string.IsNullOrWhiteSpace(r.SupplierIndustry))
            sb.Append($" | {r.SupplierIndustry}");
        return sb.ToString();
    }
}
