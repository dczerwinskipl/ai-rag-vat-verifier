namespace VatVerifier.Api.Contracts;

public sealed record EvaluateInvoiceLineResponse(
    string InvoiceLineId,
    EvaluationSeverity Severity,
    CategoryMatchStatus CategoryMatchStatus,
    VatValidationStatus VatValidationStatus,
    decimal InvoiceVatRate,
    IReadOnlyCollection<decimal> ExpectedVatRates,
    EvaluationReasonCode ReasonCode,
    IReadOnlyCollection<CategoryCandidateDto> CategoryCandidates,
    string Message);

public sealed record CategoryCandidateDto(
    string CategoryId,
    string Name,
    double Score,
    decimal ExpectedVatRate);

public enum EvaluationSeverity
{
    Ok,
    Warning,
    Alert,
    Critical
}

public enum CategoryMatchStatus
{
    Matched,
    Ambiguous,
    NotMatched
}

public enum VatValidationStatus
{
    Match,
    Mismatch,
    Unknown
}

public enum EvaluationReasonCode
{
    VatMatched,
    CategoryAmbiguousButVatConsistent,
    CategoryAmbiguousWithDifferentVatRates,
    VatMismatch,
    CategoryNotMatched,
    InsufficientData,
    EngineNotImplemented
}
