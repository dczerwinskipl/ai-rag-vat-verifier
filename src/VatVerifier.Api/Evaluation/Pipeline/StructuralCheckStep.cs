using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation.Pipeline;

internal sealed class StructuralCheckStep : IEvaluationStep
{
    private static readonly IReadOnlySet<string> ReverseChargeMandatoryGtuCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GTU_10" };

    public Task<EvaluateInvoiceLineResponse?> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        // P_18=false on a GTU code that mandates reverse charge means the invoice is structurally wrong
        if (request.ReverseChargeApplied == false
            && request.GtuCode is not null
            && ReverseChargeMandatoryGtuCodes.Contains(request.GtuCode)
            && request.InvoiceVatRate > 0)
            return Task.FromResult<EvaluateInvoiceLineResponse?>(
                EvaluationResponseFactory.ForReverseChargeMissing(request));

        // P_18=true but a positive VAT rate is also a structural contradiction
        if (request.ReverseChargeApplied == true && request.InvoiceVatRate > 0)
            return Task.FromResult<EvaluateInvoiceLineResponse?>(
                EvaluationResponseFactory.ForReverseChargeUnexpected(request));

        return Task.FromResult<EvaluateInvoiceLineResponse?>(null);
    }
}
