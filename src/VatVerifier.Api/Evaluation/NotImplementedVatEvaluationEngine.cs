using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public sealed class NotImplementedVatEvaluationEngine : IVatEvaluationEngine
{
    public Task<EvaluateInvoiceLineResponse> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        var response = new EvaluateInvoiceLineResponse(
            InvoiceLineId: request.InvoiceLineId,
            Severity: EvaluationSeverity.Alert,
            CategoryMatchStatus: CategoryMatchStatus.NotMatched,
            VatValidationStatus: VatValidationStatus.Unknown,
            InvoiceVatRate: request.InvoiceVatRate,
            ExpectedVatRates: [],
            ReasonCode: EvaluationReasonCode.EngineNotImplemented,
            CategoryCandidates: [],
            Message: "Evaluation engine is not implemented yet.");

        return Task.FromResult(response);
    }
}
