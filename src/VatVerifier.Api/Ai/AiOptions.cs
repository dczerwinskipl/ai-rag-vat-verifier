namespace VatVerifier.Api.Ai;

public sealed class AiOptions
{
    public string Provider { get; init; } = "Ollama";
    public OllamaOptions Ollama { get; init; } = new();
}

public sealed class OllamaOptions
{
    public string Endpoint { get; init; } = "http://localhost:11434";
    public string EmbeddingModel { get; init; } = "qwen3-embedding:0.6b";
}
