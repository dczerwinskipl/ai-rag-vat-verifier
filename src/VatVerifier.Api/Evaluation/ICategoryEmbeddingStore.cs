namespace VatVerifier.Api.Evaluation;

public interface ICategoryEmbeddingStore
{
    Task ReadyAsync { get; }
    void Store(string categoryId, float[] vector, string name, decimal expectedVatRate);
    void MarkReady();
    void MarkFailed(Exception ex);
    IReadOnlyList<StoredCategory> GetAll();
}

public sealed record StoredCategory(string CategoryId, string Name, float[] Vector, decimal ExpectedVatRate);
