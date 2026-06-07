namespace VatVerifier.Api.Data;

public sealed record CategorySeedEntry(
    string CategoryId,
    LocalisedText Name,
    decimal ExpectedVatRate,
    LocalisedText Description,
    IReadOnlyList<string> PositiveExamples,
    IReadOnlyList<string> NegativeExamples,
    IReadOnlyList<string> TypicalSuppliers,
    IReadOnlyList<string>? GtuCodes = null,
    IReadOnlyList<RateVariant>? RateVariants = null);

public sealed record LocalisedText(string Pl, string En);

public sealed record RateVariant(decimal Rate, string Condition);
