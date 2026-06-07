namespace VatVerifier.Api.Embeddings;

public interface ICategoryEmbeddingStore
{
    Task ReadyAsync { get; }
    void Store(string categoryId, float[] positiveVector, float[][] negativeVectors, float[] supplierVector,
        string name, decimal expectedVatRate,
        IReadOnlyList<string>? gtuCodes, IReadOnlyList<decimal>? rateVariantRates);
    void MarkReady();
    void MarkFailed(Exception ex);
    IReadOnlyList<StoredCategory> GetAll();
    StoredCategory? FindByGtuCode(string gtuCode);
    StoredCategory? FindByCategoryId(string categoryId);
}

public sealed record StoredCategory(
    string CategoryId,
    string Name,
    float[] PositiveVector,
    float[][] NegativeVectors,
    float[] SupplierVector,
    decimal ExpectedVatRate,
    IReadOnlyList<string>? GtuCodes,
    IReadOnlyList<decimal>? RateVariantRates);
