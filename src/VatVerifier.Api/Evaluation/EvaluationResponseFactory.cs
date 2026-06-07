using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public static class EvaluationResponseFactory
{
    public static EvaluateInvoiceLineResponse ForClassification(
        ClassificationResult classification,
        EvaluateInvoiceLineRequest request) =>
        classification.Status switch
        {
            CategoryMatchStatus.Matched => ForMatchedClassification(classification.TopCandidates, request),
            CategoryMatchStatus.Ambiguous => ForAmbiguousClassification(classification.TopCandidates, request),
            _ => ForNotMatched(classification.TopCandidates, request)
        };

    public static EvaluateInvoiceLineResponse ForReverseChargeMissing(EvaluateInvoiceLineRequest request) =>
        new(request.InvoiceLineId, EvaluationSeverity.Critical, CategoryMatchStatus.Matched,
            VatValidationStatus.Mismatch, request.InvoiceVatRate, [0],
            EvaluationReasonCode.ReverseChargeMissing, [],
            $"Reverse charge must be applied for GTU {request.GtuCode} but invoice shows VAT {request.InvoiceVatRate}%.");

    public static EvaluateInvoiceLineResponse ForReverseChargeUnexpected(EvaluateInvoiceLineRequest request) =>
        new(request.InvoiceLineId, EvaluationSeverity.Critical, CategoryMatchStatus.Matched,
            VatValidationStatus.Mismatch, request.InvoiceVatRate, [0],
            EvaluationReasonCode.ReverseChargeUnexpected, [],
            $"Reverse charge is applied but invoice shows positive VAT rate {request.InvoiceVatRate}%.");

    public static EvaluateInvoiceLineResponse ForRateVariantDegradation(
        EvaluateInvoiceLineResponse matched,
        IReadOnlyList<decimal> variantRates) =>
        new(matched.InvoiceLineId,
            EvaluationSeverity.Alert,
            CategoryMatchStatus.Ambiguous,
            VatValidationStatus.Unknown,
            matched.InvoiceVatRate,
            variantRates,
            EvaluationReasonCode.CategoryAmbiguousWithDifferentVatRates,
            matched.CategoryCandidates,
            "Category matched but has multiple applicable VAT rates — the applicable rate depends on conditions not determinable from available invoice fields.");

    public static EvaluateInvoiceLineResponse ForDegradedState(EvaluateInvoiceLineRequest request, Exception ex) =>
        new(request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.NotMatched,
            VatValidationStatus.Unknown, request.InvoiceVatRate, [],
            EvaluationReasonCode.InsufficientData, [],
            $"Evaluation engine not ready: {ex.Message}");

    private static EvaluateInvoiceLineResponse ForMatchedClassification(
        IReadOnlyList<ScoredCategory> top,
        EvaluateInvoiceLineRequest request)
    {
        var expectedRate = top[0].ExpectedVatRate;
        var candidates = ToCandidateDtos(top);
        return request.InvoiceVatRate == expectedRate
            ? new(request.InvoiceLineId, EvaluationSeverity.Ok, CategoryMatchStatus.Matched,
                VatValidationStatus.Match, request.InvoiceVatRate, [expectedRate],
                EvaluationReasonCode.VatMatched, candidates,
                $"VAT rate {request.InvoiceVatRate}% matches the expected rate for this category.")
            : new(request.InvoiceLineId, EvaluationSeverity.Critical, CategoryMatchStatus.Matched,
                VatValidationStatus.Mismatch, request.InvoiceVatRate, [expectedRate],
                EvaluationReasonCode.VatMismatch, candidates,
                $"VAT mismatch: invoice has {request.InvoiceVatRate}%, expected {expectedRate}%.");
    }

    private static EvaluateInvoiceLineResponse ForAmbiguousClassification(
        IReadOnlyList<ScoredCategory> top,
        EvaluateInvoiceLineRequest request)
    {
        // The Ambiguous condition triggers only when top[0] and top[1] both clear the floor.
        // Check VAT consistency only for those two candidates — lower-ranked items in the top-N
        // that happen to carry a different rate shouldn't escalate the result to Alert.
        var distinctRates = top.Take(2).Select(c => c.ExpectedVatRate).Distinct().ToList();
        var candidates = ToCandidateDtos(top);
        return distinctRates.Count == 1 && request.InvoiceVatRate == distinctRates[0]
            ? new(request.InvoiceLineId, EvaluationSeverity.Warning, CategoryMatchStatus.Ambiguous,
                VatValidationStatus.Match, request.InvoiceVatRate, distinctRates,
                EvaluationReasonCode.CategoryAmbiguousButVatConsistent, candidates,
                "Category is ambiguous but all candidates agree on the VAT rate.")
            : new(request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.Ambiguous,
                VatValidationStatus.Unknown, request.InvoiceVatRate, distinctRates,
                EvaluationReasonCode.CategoryAmbiguousWithDifferentVatRates, candidates,
                "Category is ambiguous with candidates having different VAT rates.");
    }

    private static EvaluateInvoiceLineResponse ForNotMatched(
        IReadOnlyList<ScoredCategory> top,
        EvaluateInvoiceLineRequest request) =>
        new(request.InvoiceLineId, EvaluationSeverity.Alert, CategoryMatchStatus.NotMatched,
            VatValidationStatus.Unknown, request.InvoiceVatRate, [],
            EvaluationReasonCode.CategoryNotMatched, ToCandidateDtos(top), "No matching category found.");

    private static List<CategoryCandidateDto> ToCandidateDtos(IReadOnlyList<ScoredCategory> scored) =>
        scored.Select(c => new CategoryCandidateDto(c.CategoryId, c.Name, c.Score, c.ExpectedVatRate)).ToList();
}
