# AI integration starting point

Preferred approach:

- use `Microsoft.Extensions.AI` abstractions in application code
- use `OllamaSharp` as the local Ollama provider
- do not reference deprecated `Microsoft.Extensions.AI.Ollama`
- keep provider registration in `Program.cs` / composition root

Target local embedding model:

```bash
ollama pull nomic-embed-text-v2-moe
```

Fallback model if the above is too slow or unavailable:

```bash
ollama pull bge-m3
```

Do not implement a custom vector database yet. For the first iteration, load categories from JSON, generate embeddings at startup or test setup, and keep them in memory.
