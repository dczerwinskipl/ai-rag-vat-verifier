# VAT Verifier PoC — Initial Project Scaffold

## Goal

Establish the minimal .NET 10 project scaffold for the VAT invoice line evaluation PoC. The goal is a running API with a typed contract, a swappable evaluation engine interface, AI abstraction wiring, and an integration test harness — all in place before any real evaluation logic is implemented.

## Constraints

- .NET 10 Minimal API only — no layered architecture, no MediatR, no CQRS
- Free and open-source stack throughout
- No authentication, no database, no persistent vector store
- Must run locally on both Windows (RTX 4070Ti Super, 16 GB VRAM) and MacBook Air M3 (16 GB unified)
- Provider swap requirement: Ollama must be replaceable with OpenAI or Claude API by changing only `Program.cs`; no provider-specific types in business logic
- PoC scope: no production SLAs, no production observability

## Chosen architecture

A single .NET 10 Minimal API project (`VatVerifier.Api`) with a flat folder structure. The evaluation engine is abstracted behind `IVatEvaluationEngine`; the real implementation is deferred. AI access is declared via `Microsoft.Extensions.AI` interfaces registered in `Program.cs`. Ollama runs as a Docker container with NVIDIA GPU passthrough. Integration tests use `WebApplicationFactory<Program>` in two tiers: always-run structural tests and skipped AI-gated tests pending the real engine.

### Solution layout: flat Minimal API

No Clean Architecture layers, no MediatR. `src/VatVerifier.Api` holds all application code. `tests/VatVerifier.EvaluationTests` references the API project directly to enable `WebApplicationFactory<Program>` without an HTTP boundary.

Layering is an explicit deferral (see `CLAUDE.md`). Flat structure keeps the project navigable at PoC scale.

### API contract: rich response model

The response carries four orthogonal status fields rather than a single `PASS`/`ALERT` flag:

| Field                  | Type                               | Purpose                                                 |
| :--------------------- | :--------------------------------- | :------------------------------------------------------ |
| `EvaluationSeverity`   | `Ok / Warning / Alert / Critical`  | Consumer-facing outcome                                 |
| `CategoryMatchStatus`  | `Matched / Ambiguous / NotMatched` | Confidence of the category match                        |
| `VatValidationStatus`  | `Match / Mismatch / Unknown`       | Agreement between invoice VAT and expected              |
| `EvaluationReasonCode` | stable enum                        | Machine-readable code for test assertions and reporting |

A single flag would conflate category confidence with VAT correctness. Keeping them separate lets tests pin the exact failure mode and lets future consumers act on the right signal. The response also returns `CategoryCandidates` (top-K scored candidates with expected VAT rates) so callers can inspect the reasoning.

### Evaluation severity mapping

| CategoryMatchStatus | VatValidationStatus         | Severity   | ReasonCode                               |
| :------------------ | :-------------------------- | :--------- | :--------------------------------------- |
| `Matched`           | `Match`                     | `Ok`       | `VatMatched`                             |
| `Ambiguous`         | `Match` (consistent rates)  | `Warning`  | `CategoryAmbiguousButVatConsistent`      |
| `Ambiguous`         | `Unknown` (different rates) | `Alert`    | `CategoryAmbiguousWithDifferentVatRates` |
| `NotMatched`        | `Unknown`                   | `Alert`    | `CategoryNotMatched`                     |
| `Matched`           | `Mismatch`                  | `Critical` | `VatMismatch`                            |

### IVatEvaluationEngine: interface-first, implementation deferred

The interface is registered as a singleton. The current implementation (`NotImplementedVatEvaluationEngine`) returns `EngineNotImplemented` + `Alert` for every request. This allows the API and tests to compile and run before the real engine exists, keeping the `main` branch always green.

### AI abstraction: Microsoft.Extensions.AI + OllamaSharp

`Microsoft.Extensions.AI` provides `IEmbeddingGenerator<string, Embedding<float>>` and `IChatClient` as provider-neutral abstractions used throughout business logic. `OllamaSharp` is the concrete Ollama adapter.

The deprecated `Microsoft.Extensions.AI.Ollama` package is explicitly avoided — `OllamaSharp` is the current supported path.

Provider registration lives exclusively in `Program.cs`. Business logic never references `OllamaSharp` types directly. Swapping to OpenAI or Claude API requires changing only the DI registration — all service code remains unchanged.

### Embedding model: nomic-embed-text-v2-moe

Primary: `nomic-embed-text-v2-moe`. Fallback: `bge-m3`. Both handle multilingual text (Polish invoice descriptions). Both fit within the VRAM budget on both target machines:

- RTX 4070Ti Super (16): comfortable for both models
- M3 16 GB unified: comfortable for both models

Model is configured in `appsettings.json` under `Ai.Ollama.EmbeddingModel` and overridable per environment.

### Configuration: EvaluationOptions + AiOptions

Similarity thresholds are externalised to `appsettings.json` so they can be tuned without recompiling:

| Setting                       | Default | Purpose                                               |
| :---------------------------- | :------ | :---------------------------------------------------- |
| `StrongCandidateThreshold`    | 0.85    | Minimum cosine similarity for `Matched`               |
| `AmbiguousCandidateThreshold` | 0.75    | Minimum similarity for inclusion as a candidate       |
| `CandidateMarginThreshold`    | 0.10    | Maximum gap between top candidates before `Ambiguous` |
| `MaxCandidates`               | 5       | Top-K candidates to return                            |

`AiOptions` carries the provider name, endpoint, and model name — all overridable per environment.

### Category data model

Each seed category: `categoryId`, bilingual `name` (pl/en), `expectedVatRate`, bilingual `description`, `positiveExamples[]`, `negativeExamples[]`, `typicalSuppliers[]`.

Polish and English fields in the same category record support both language embeddings for the same concept. Positive and negative examples are included for future prompt construction or fine-tuning; the initial in-memory similarity search does not use them.

Three seed categories cover the golden dataset: `software_it_services_23`, `alcohol_spirits_23`, `books_5`.

### Test strategy: two-tier integration tests

**Tier 1** (`[Fact]` — always runs): Structural tests assert that the API returns 2xx and echoes `InvoiceLineId` for every case in the dataset. Passes against the stub engine.

**Tier 2** (`[Fact(Skip = "...")]` — AI-gated): Asserts the full expected `Severity`, `CategoryMatchStatus`, `VatValidationStatus`, and `ReasonCode`. Enabled manually once the real engine is wired.

The dataset includes `expected` fields with the target evaluation output for each case. The critical failure mode explicitly covered: false `Ok` / false low severity when VAT is actually mismatched (`case-003`: vodka with 8% VAT → `Critical / VatMismatch`).

### Docker: Ollama with NVIDIA GPU passthrough

`docker/ollama/docker-compose.yml` runs `ollama/ollama:latest` on port `11434` with a named volume for model persistence and NVIDIA GPU reservation. This is the Windows/CUDA path. Mac users running Ollama natively (Metal) do not need Docker.

## Components

- `src/VatVerifier.Api/Program.cs` — DI registration, route mapping
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineRequest.cs` — request record
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineResponse.cs` — response record + all status enums
- `src/VatVerifier.Api/Evaluation/IVatEvaluationEngine.cs` — engine interface
- `src/VatVerifier.Api/Evaluation/NotImplementedVatEvaluationEngine.cs` — stub implementation
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` — similarity threshold config
- `src/VatVerifier.Api/Ai/AiOptions.cs` — Ollama provider config
- `tests/VatVerifier.EvaluationTests/VatEvaluationApiTests.cs` — two-tier integration tests
- `tests/VatVerifier.EvaluationTests/Infrastructure/EvaluationCase.cs` — test case model
- `tests/VatVerifier.EvaluationTests/Infrastructure/DatasetLoader.cs` — JSON loader
- `tests/VatVerifier.EvaluationTests/Datasets/vat-categories.seed.json` — 3 seed categories
- `tests/VatVerifier.EvaluationTests/Datasets/invoice-line-evaluation-cases.json` — 3 golden test cases
- `docker/ollama/docker-compose.yml` — Ollama container (NVIDIA/CUDA)

## Alternatives considered

| Area              | Alternative                                           | Why not chosen                                                                        |
| :---------------- | :---------------------------------------------------- | :------------------------------------------------------------------------------------ |
| AI abstraction    | `Microsoft.Extensions.AI.Ollama` (deprecated package) | Deprecated; `OllamaSharp` is the current supported path                               |
| AI abstraction    | Semantic Kernel                                       | Deferred — heavier abstraction not needed at PoC scale; listed as future path         |
| Evaluation output | Single `PASS`/`ALERT` flag                            | Conflates category confidence with VAT validation; loses test pinning ability         |
| Test isolation    | Mock `IEmbeddingGenerator`                            | Deferred — live `WebApplicationFactory` tests preferred to avoid mock/prod divergence |
| Vector store      | Qdrant, ChromaDB, Weaviate                            | Deferred — in-memory sufficient for PoC                                               |
| Architecture      | Clean Architecture layers                             | Deferred — overhead not justified at PoC scale                                        |

## Open questions

1. **Real engine implementation** — how categories are loaded from JSON, embedded at startup, and searched with cosine similarity. Covered by the next spec.
2. **IEmbeddingGenerator DI registration** — `OllamaSharp` is a package dependency but not yet wired into the DI container in `Program.cs`.
3. **Embedding timing** — at startup (warm, deterministic) vs. lazy at first request (faster startup, cold first call).
4. **IChatClient / generation step** — whether a generation step (LLM answer) is added on top of retrieval, or the PoC stays retrieval-only for the first iteration.
5. **Mac Docker Compose** — current compose uses NVIDIA GPU reservation only; Mac/Metal users running Ollama natively need a note or separate compose variant.
