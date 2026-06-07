using VatVerifier.Api.Contracts;

namespace VatVerifier.Api.Evaluation.Pipeline;

internal interface IEvaluationStep
{
    Task<EvaluateInvoiceLineResponse?> EvaluateAsync(
        EvaluateInvoiceLineRequest request,
        CancellationToken cancellationToken);
}
