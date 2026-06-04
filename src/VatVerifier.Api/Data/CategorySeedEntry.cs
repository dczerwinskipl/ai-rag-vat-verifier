namespace VatVerifier.Api.Data;

public sealed record CategorySeedEntry(
    string CategoryId,
    LocalisedText Name,
    decimal ExpectedVatRate,
    LocalisedText Description,
    IReadOnlyList<string> PositiveExamples,
    IReadOnlyList<string> NegativeExamples,
    IReadOnlyList<string> TypicalSuppliers);

public sealed record LocalisedText(string Pl, string En);
