---
name: rag-context
description: Project-specific context for RAG specification work in the VAT Verifier PoC — current stack, hardware constraints, established decisions, and swap-friendliness requirements
metadata:
  type: knowledge
  project: vat-verifier
---

# VAT Verifier — RAG Context

## Current stack (established decisions)

These are already in use and should be treated as defaults unless the spec explicitly overrides them:

| Component                  | Choice                    | Notes                                                                              |
| :------------------------- | :------------------------ | :--------------------------------------------------------------------------------- |
| Runtime                    | .NET 10 Minimal API       | No layered architecture, no MediatR, no CQRS                                       |
| LLM / AI abstraction       | `Microsoft.Extensions.AI` | Use `IEmbeddingGenerator<string, Embedding<float>>` and `IChatClient` abstractions |
| Ollama client              | `OllamaSharp`             | Do NOT use the deprecated `Microsoft.Extensions.AI.Ollama` package                 |
| Embedding model (primary)  | `nomic-embed-text-v2-moe` | Via Ollama                                                                         |
| Embedding model (fallback) | `bge-m3`                  | Via Ollama                                                                         |
| Vector store               | In-memory                 | No external DB yet; use `Microsoft.Extensions.AI.VectorData` in-memory store       |
| Inference server           | Ollama                    | Running locally via Docker Compose (`docker/ollama/`)                              |
| Preferred LLM model family | Qwen2.5                   | Qwen2.5-7B or Qwen2.5-3B for testing; good multilingual support (Polish content)   |

## Explicit deferrals (from CLAUDE.md)

These have been explicitly deferred and MUST NOT be introduced as primary recommendations:

- Semantic Kernel — deferred; list as future path only
- Vector database (e.g., Qdrant, ChromaDB, Weaviate) — deferred for first iteration; may appear as an option for later specs
- Database (any) — not in scope
- Authentication / authorization — not in scope
- Background jobs — not in scope
- UI — not in scope
- Clean Architecture layers — not in scope
- Production observability — not in scope

## Hardware constraints

| Machine         | GPU / CPU        | RAM / VRAM    | Ollama acceleration |
| :-------------- | :--------------- | :------------ | :------------------ |
| Windows desktop | RTX 4070Ti Super | 16 GB VRAM    | CUDA                |
| MacBook Air M3  | Apple M3         | 16 GB unified | Metal               |

**Model sizing guidance:**

- RTX 4070Ti Super: Q4 7B runs fast; 13B Q4 feasible; 32B slow or OOM
- M3 16GB: 3B very fast; 7B runs well; 14B+ causes RAM pressure
- Cross-device default: recommend ≤7B unless user accepts per-machine config

## Provider swap requirement

The application must be able to switch from Ollama to OpenAI or Claude API with minimal code changes. This means:

- Always use `IChatClient` (from `Microsoft.Extensions.AI`) for generation, never call Ollama HTTP directly
- Always use `IEmbeddingGenerator<string, Embedding<float>>` for embeddings, never call Ollama HTTP directly
- Provider registration stays in `Program.cs` or composition root
- No library-specific types should leak into business logic

When evaluating library options, explicitly state the swap cost (how many files change, whether abstractions hold).

## Testing context

This is a PoC for testing and exploration. Specs should assume:

- No production SLAs
- Integration tests call the API directly (no mocking of embeddings or LLM)
- AI/evaluation tests are gated behind a flag (`[Fact(Skip = "...")]` or `[Category("AI")]`)
- The most critical failure is a false OK / false low severity — test cases must cover this

## Domain specifics

Invoice line descriptions may be in Polish. Embedding models and LLMs should handle Polish text adequately. When recommending models, note their multilingual capability explicitly.
