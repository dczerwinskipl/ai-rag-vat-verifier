namespace VatVerifier.Api.Evaluation;

public sealed class InMemoryCategoryEmbeddingStore : ICategoryEmbeddingStore
{
    private readonly TaskCompletionSource<bool> _readySource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<StoredCategory> _categories = [];

    public Task ReadyAsync => _readySource.Task;

    public void Store(string categoryId, float[] vector, string name, decimal expectedVatRate)
        => _categories.Add(new StoredCategory(categoryId, name, vector, expectedVatRate));

    public void MarkReady() => _readySource.TrySetResult(true);

    public void MarkFailed(Exception ex) => _readySource.TrySetException(ex);

    public IReadOnlyList<StoredCategory> GetAll() => _categories;
}
