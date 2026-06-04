using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation;

public interface IVatEvaluationEngine
{
    Task<EvaluateInvoiceLineResponse> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken);
}
