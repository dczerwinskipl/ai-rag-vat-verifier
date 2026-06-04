namespace VatVerifier.Api.Evaluation;

public sealed class EvaluationOptions
{
    public double StrongCandidateThreshold { get; init; } = 0.85;
    public double AmbiguousCandidateThreshold { get; init; } = 0.75;
    public double CandidateMarginThreshold { get; init; } = 0.10;
    public int MaxCandidates { get; init; } = 5;
}
