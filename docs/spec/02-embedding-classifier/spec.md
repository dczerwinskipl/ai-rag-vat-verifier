# In-Memory Embedding Category Classifier

## Goal

Replace the stub `NotImplementedVatEvaluationEngine` with a working implementation that uses `qwen3-embedding:0.6b` (via Ollama) to vectorise invoice line descriptions and classify them against pre-computed category embeddings stored in memory. Categories are embedded at startup in parallel; per-request classification uses cosine similarity with the threshold configuration already present in `EvaluationOptions`.

## Constraints

- .NET 10 Minimal API; no layers, no CQRS, no MediatR
- `OllamaSharp` as the Ollama client; `Microsoft.Extensions.AI` abstractions throughout — provider swap must be possible by changing DI registration only
- In-memory only: no external vector database, no persistent store
- Free/OSS models only; must run on RTX 4070Ti Super (12 GB VRAM) and MacBook Air M3 (16 GB RAM) without VRAM pressure
- No new structural patterns beyond what CLAUDE.md permits

## Chosen architecture

At startup, a `CategoryEmbeddingWarmupService` (BackgroundService) loads `vat-categories.seed.json`, builds a combined text string per category, batch-embeds via `IEmbeddingGenerator<string, Embedding<float>>`, and stores the resulting `float[]` vectors in an `InMemoryCategoryEmbeddingStore` singleton. A `TaskCompletionSource<bool>` held by the store signals when all embeddings are ready. Per request, `EmbeddingVatEvaluationEngine` awaits the gate, embeds the invoice line, computes cosine similarity against every stored category vector using `TensorPrimitives.CosineSimilarity()`, and delegates threshold logic to an `ICategoryClassifier`. The classifier returns a match status and top candidates; the engine maps those to the VAT validation check and produces the response.

### Embedding model: qwen3-embedding:0.6b

Qwen3-Embedding (0.6B) is a dedicated embedding model from the Qwen3 family, available in Ollama as `qwen3-embedding:0.6b`. At 0.6B parameters it fits on both target machines with minimal VRAM. It handles multilingual text including Polish. The 4B variant can be substituted on the Windows desktop for higher accuracy by changing `appsettings.json` only — no code changes required.

### Similarity calculation: TensorPrimitives.CosineSimilarity()

`System.Numerics.Tensors.TensorPrimitives.CosineSimilarity()` is built into .NET 10 — no extra NuGet package is needed. It is SIMD-accelerated and sufficient for in-memory scanning of a small number of category vectors. An external vector database (e.g., Qdrant) is not introduced in this spec; it remains a future option if category count grows significantly.

### Startup warm-up: IHostedService + TaskCompletionSource gate

`CategoryEmbeddingWarmupService : BackgroundService` submits all category texts to `IEmbeddingGenerator` as a single batch and stores the resulting vectors. A `TaskCompletionSource<bool>` held by `InMemoryCategoryEmbeddingStore` is set to completed (or faulted on failure) when the work finishes. `EmbeddingVatEvaluationEngine` awaits `store.ReadyAsync` before processing any request — the first real request is not penalised by embedding latency. If Ollama is unavailable at startup the store enters a faulted state and the engine returns `EvaluationReasonCode.InsufficientData`, keeping deterministic tests green.

### Text composition: name + description + positive + negative examples

**Category embedding text:**
```
{name.en}: {description.en}

Examples: {positiveExamples joined by ", "}
Not this category: {negativeExamples joined by ", "}
```

**Invoice line embedding text:**
```
{description} | {supplierName}[ | {supplierIndustry}]
```
Supplier industry is appended only when non-null and non-empty. The format strings are isolated in a single static helper so they can be tuned without touching the engine.

Including negative examples encodes contrast signal. Whether negation in embedding space improves the Chopin ambiguity case is an open question — it is easy to test by toggling the helper.

### Classification strategy: ICategoryClassifier (pure cosine, hybrid-ready)

An `ICategoryClassifier` interface accepts a sorted list of `ScoredCategory` records and the original request, and returns a `ClassificationResult` (match status + top candidates). The first implementation, `CosineSimilarityClassifier`, applies the thresholds from `EvaluationOptions`:

- Score ≥ `StrongCandidateThreshold` (0.85) and margin over second candidate ≥ `CandidateMarginThreshold` (0.10) → `Matched`
- At least one candidate ≥ `AmbiguousCandidateThreshold` (0.75) but not strongly matched → `Ambiguous`
- No candidate ≥ `AmbiguousCandidateThreshold` → `NotMatched`

A future `HybridClassifier` (supplier industry keyword boosting + cosine) can wrap or replace this implementation without changing `EmbeddingVatEvaluationEngine`.

## Components

**New files:**
- `src/VatVerifier.Api/Data/vat-categories.seed.json` — canonical seed file (moved from tests; tests keep their own copy for now)
- `src/VatVerifier.Api/Data/CategorySeedEntry.cs` — record for JSON deserialisation
- `src/VatVerifier.Api/Evaluation/ICategoryEmbeddingStore.cs` — interface for the in-memory store
- `src/VatVerifier.Api/Evaluation/InMemoryCategoryEmbeddingStore.cs` — singleton implementation with readiness gate
- `src/VatVerifier.Api/Evaluation/CategoryEmbeddingWarmupService.cs` — BackgroundService; populates the store at startup
- `src/VatVerifier.Api/Evaluation/ICategoryClassifier.cs` — classification strategy interface + `ScoredCategory` + `ClassificationResult` records
- `src/VatVerifier.Api/Evaluation/CosineSimilarityClassifier.cs` — threshold-based implementation
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` — replaces `NotImplementedVatEvaluationEngine`

**Modified files:**
- `src/VatVerifier.Api/Program.cs` — register embedding generator, store, classifier, warmup service; swap engine registration
- `src/VatVerifier.Api/appsettings.json` — update `EmbeddingModel` to `qwen3-embedding:0.6b`
- `src/VatVerifier.Api/VatVerifier.Api.csproj` — add seed JSON as content item
- `docker/ollama/pull-models.ps1` — pull `qwen3-embedding:0.6b`

## Alternatives considered

| Option | Reason not chosen |
|:---|:---|
| `nomic-embed-text-v2-moe` | User chose Qwen3-Embedding family |
| `bge-m3` | Good multilingual alternative; user chose qwen3-embedding |
| `Microsoft.Extensions.VectorData` | Extra packages and setup overhead; TensorPrimitives adequate at PoC scale |
| Qdrant in Docker | Infrastructure dependency unjustified for <10 categories in a PoC |
| `Lazy<Task<T>>` startup pattern | First caller bears full embedding latency; IHostedService gate is cleaner |
| Positive examples only in category text | User explicitly requested both positive and negative examples |
| Hybrid classifier from the start | Pure cosine provides a working, testable baseline; hybrid is the next step |

## Open questions

- Does including negative examples in the category embedding text improve or hurt cosine scores for the Chopin ambiguity case (case-002)? Measure by toggling `negativeExamples` in the text helper.
- Is `qwen3-embedding:0.6b` accurate enough for the golden dataset, or does the 4B variant need to be used on at least the Windows desktop?
- Will supplier industry boosting be necessary to pass case-002 (`"Chopin"` at `"Empik"`, retail supplier), or do embedding scores alone produce an `Ambiguous` result as expected?
