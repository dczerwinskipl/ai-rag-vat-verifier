namespace VatVerifier.Api.Evaluation;

public sealed class EvaluationOptions
{
    public double StrongCandidateThreshold { get; init; } = 0.85;
    public double AmbiguousCandidateThreshold { get; init; } = 0.75;
    public double CandidateMarginThreshold { get; init; } = 0.10;
    public int MaxCandidates { get; init; } = 5;
    public double ConfidenceThreshold { get; init; } = 0.75;
    public double NegativePenaltyWeight { get; init; } = 0.30;
    public double DescriptionChannelWeight { get; init; } = 0.70;
    public double SupplierChannelWeight { get; init; } = 0.30;
    public int RrfK { get; init; } = 60;
}
