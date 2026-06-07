namespace VatVerifier.Api.Embeddings;

public sealed class InMemoryCategoryEmbeddingStore : ICategoryEmbeddingStore
{
    private readonly TaskCompletionSource<bool> _readySource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<StoredCategory> _categories = [];
    private readonly Dictionary<string, StoredCategory> _byGtuCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StoredCategory> _byCategoryId = new(StringComparer.OrdinalIgnoreCase);

    public Task ReadyAsync => _readySource.Task;

    public void Store(string categoryId, float[] positiveVector, float[][] negativeVectors, float[] supplierVector,
        string name, decimal expectedVatRate,
        IReadOnlyList<string>? gtuCodes, IReadOnlyList<decimal>? rateVariantRates)
    {
        var entry = new StoredCategory(categoryId, name, positiveVector, negativeVectors, supplierVector,
            expectedVatRate, gtuCodes, rateVariantRates);
        _categories.Add(entry);
        _byCategoryId[categoryId] = entry;

        if (gtuCodes is not null)
            foreach (var code in gtuCodes)
                _byGtuCode[code] = entry;
    }

    public void MarkReady() => _readySource.TrySetResult(true);

    public void MarkFailed(Exception ex) => _readySource.TrySetException(ex);

    public IReadOnlyList<StoredCategory> GetAll() => _categories;

    public StoredCategory? FindByGtuCode(string gtuCode) =>
        _byGtuCode.TryGetValue(gtuCode, out var entry) ? entry : null;

    public StoredCategory? FindByCategoryId(string categoryId) =>
        _byCategoryId.TryGetValue(categoryId, out var entry) ? entry : null;
}
