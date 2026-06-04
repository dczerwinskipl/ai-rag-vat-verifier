# Implementation Plan: In-Memory Embedding Category Classifier

## Prerequisites

- Ollama running via `docker/ollama/docker-compose.yml` (`docker compose up -d`)
- `qwen3-embedding:0.6b` pulled (run updated `pull-models.ps1` after Step 2)
- `dotnet build` passes before starting (currently green with stub engine)

---

## Steps

### Step 1 — Move seed file and define data model

**What:** Copy `vat-categories.seed.json` to `src/VatVerifier.Api/Data/` (API now owns the canonical copy; the tests keep their existing copy). Define `CategorySeedEntry` to deserialise it.

**Files:**
- `src/VatVerifier.Api/Data/vat-categories.seed.json` (new — copy from `tests/VatVerifier.EvaluationTests/Datasets/`)
- `src/VatVerifier.Api/Data/CategorySeedEntry.cs` (new)
- `src/VatVerifier.Api/VatVerifier.Api.csproj` — add `<Content Include="Data/vat-categories.seed.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`

**CategorySeedEntry shape:**
```csharp
public sealed record CategorySeedEntry(
    string CategoryId,
    LocalisedText Name,
    decimal ExpectedVatRate,
    LocalisedText Description,
    IReadOnlyList<string> PositiveExamples,
    IReadOnlyList<string> NegativeExamples,
    IReadOnlyList<string> TypicalSuppliers);

public sealed record LocalisedText(string Pl, string En);
```

**Accepts when:** `JsonSerializer.Deserialize<List<CategorySeedEntry>>(File.OpenRead(...))` returns 3 entries without error.

---

### Step 2 — Register IEmbeddingGenerator and update model config

**What:** Wire `OllamaApiClient` as `IEmbeddingGenerator<string, Embedding<float>>` in DI. Update `appsettings.json` to use `qwen3-embedding:0.6b`. Update `pull-models.ps1` with the new model name.

**Files:**
- `src/VatVerifier.Api/Program.cs` — add embedding generator registration
- `src/VatVerifier.Api/appsettings.json` — `"EmbeddingModel": "qwen3-embedding:0.6b"`
- `docker/ollama/pull-models.ps1` — default `$MODEL` to `qwen3-embedding:0.6b`

**Registration pattern (OllamaSharp):**
```csharp
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AiOptions>>().Value;
    return new OllamaApiClient(new Uri(opts.Ollama.Endpoint))
        .AsEmbeddingGenerator(opts.Ollama.EmbeddingModel);
});
```

**Accepts when:** `GET /health` returns 200 and the app starts without DI exceptions.

---

### Step 3 — Category embedding store

**What:** Define `ICategoryEmbeddingStore` and implement `InMemoryCategoryEmbeddingStore`. The store holds a dictionary of category ID → `float[]` and a `Task ReadyAsync` backed by `TaskCompletionSource<bool>`.

**Files:**
- `src/VatVerifier.Api/Evaluation/ICategoryEmbeddingStore.cs` (new)
- `src/VatVerifier.Api/Evaluation/InMemoryCategoryEmbeddingStore.cs` (new)

**Interface:**
```csharp
public interface ICategoryEmbeddingStore
{
    Task ReadyAsync { get; }
    void Store(string categoryId, float[] vector, string name, decimal expectedVatRate);
    void MarkReady();
    void MarkFailed(Exception ex);
    IReadOnlyList<StoredCategory> GetAll();
}

public sealed record StoredCategory(string CategoryId, string Name, float[] Vector, decimal ExpectedVatRate);
```

**Accepts when:** Unit-instantiable without DI; `MarkReady()` completes `ReadyAsync`.

---

### Step 4 — Startup warmup service

**What:** Implement `CategoryEmbeddingWarmupService : BackgroundService`. Loads the seed JSON from the app's content root, builds embedding text per category, calls `IEmbeddingGenerator.GenerateAsync` in a single batch, stores vectors, signals readiness. Catches exceptions and calls `store.MarkFailed(ex)`.

**Files:**
- `src/VatVerifier.Api/Evaluation/CategoryEmbeddingWarmupService.cs` (new)

**Text builder (isolated static method):**
```csharp
private static string BuildCategoryText(CategorySeedEntry entry) =>
    $"{entry.Name.En}: {entry.Description.En}\n\n" +
    $"Examples: {string.Join(", ", entry.PositiveExamples)}\n" +
    $"Not this category: {string.Join(", ", entry.NegativeExamples)}";
```

**Accepts when:** App starts, service runs, and `store.ReadyAsync` completes within ~30 s when Ollama is up. When Ollama is down, `ReadyAsync` faults without crashing the app.

---

### Step 5 — Classification abstraction and CosineSimilarityClassifier

**What:** Define `ICategoryClassifier` with `ScoredCategory` and `ClassificationResult` types. Implement `CosineSimilarityClassifier` using `EvaluationOptions` thresholds.

**Files:**
- `src/VatVerifier.Api/Evaluation/ICategoryClassifier.cs` (new)
- `src/VatVerifier.Api/Evaluation/CosineSimilarityClassifier.cs` (new)

**Threshold logic:**
- Input list is pre-sorted descending by score; take up to `MaxCandidates`
- Top score ≥ `StrongCandidateThreshold` AND (top score − second score) ≥ `CandidateMarginThreshold` → `Matched`
- Any candidate ≥ `AmbiguousCandidateThreshold` but not strongly matched → `Ambiguous`
- Otherwise → `NotMatched`

**Interface:**
```csharp
public interface ICategoryClassifier
{
    ClassificationResult Classify(
        IReadOnlyList<ScoredCategory> rankedCandidates,
        EvaluateInvoiceLineRequest request);
}

public sealed record ScoredCategory(string CategoryId, string Name, double Score, decimal ExpectedVatRate);
public sealed record ClassificationResult(CategoryMatchStatus Status, IReadOnlyList<ScoredCategory> TopCandidates);
```

**Accepts when:** Unit tests cover at least: single strong candidate → `Matched`; two near-equal high scores → `Ambiguous`; all scores below threshold → `NotMatched`.

---

### Step 6 — EmbeddingVatEvaluationEngine

**What:** Implement `EmbeddingVatEvaluationEngine : IVatEvaluationEngine`. Awaits `store.ReadyAsync`, embeds the invoice line query text, computes cosine similarity against all stored vectors using `TensorPrimitives.CosineSimilarity()`, sorts results, passes to `ICategoryClassifier`, then maps to VAT validation and response.

**Files:**
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` (new)

**Query text builder:**
```csharp
private static string BuildQueryText(EvaluateInvoiceLineRequest r)
{
    var sb = new StringBuilder($"{r.Description} | {r.SupplierName}");
    if (!string.IsNullOrWhiteSpace(r.SupplierIndustry))
        sb.Append($" | {r.SupplierIndustry}");
    return sb.ToString();
}
```

**VAT mapping logic** (uses existing `EvaluationReasonCode` values):
- `Matched` + invoice VAT ∈ expected rates → `Ok` / `VatMatched`
- `Matched` + invoice VAT ∉ expected rates → `Critical` / `VatMismatch`
- `Ambiguous` + all candidate rates equal + invoice VAT matches → `Warning` / `CategoryAmbiguousButVatConsistent`
- `Ambiguous` + candidate rates differ → `Alert` / `CategoryAmbiguousWithDifferentVatRates`
- `NotMatched` → `Alert` / `CategoryNotMatched`
- Store faulted → `Alert` / `InsufficientData`

**Accepts when:** The three existing test cases return the correct `ReasonCode` when run with Ollama available (see Step 8).

---

### Step 7 — Wire in Program.cs

**What:** Replace `NotImplementedVatEvaluationEngine` registration with the real engine; register store, classifier, and warmup service.

**Files:**
- `src/VatVerifier.Api/Program.cs`

**Registrations to add:**
```csharp
builder.Services.AddSingleton<ICategoryEmbeddingStore, InMemoryCategoryEmbeddingStore>();
builder.Services.AddSingleton<ICategoryClassifier, CosineSimilarityClassifier>();
builder.Services.AddSingleton<IVatEvaluationEngine, EmbeddingVatEvaluationEngine>();
builder.Services.AddHostedService<CategoryEmbeddingWarmupService>();
```

**Accepts when:** `dotnet build` succeeds; `GET /health` returns 200; `POST /invoice-lines/evaluate` returns a valid JSON response (even if Ollama is not running, the response should be `InsufficientData` not a 500).

---

### Step 8 — Enable AI tests and validate golden dataset

**What:** Remove the `Skip` reason from `Evaluate_ShouldMatchExpectedEvaluation_ForGoldenDataset`. Pull `qwen3-embedding:0.6b` via the updated `pull-models.ps1`. Run all tests against a live Ollama instance and verify the three golden cases pass.

**Files:**
- `tests/VatVerifier.EvaluationTests/VatEvaluationApiTests.cs` — remove or update `[Fact(Skip = "...")]`

**Expected outcomes for golden dataset:**

| Case | Expected severity | Expected reason |
|:---|:---|:---|
| case-001 Software dev, 23% VAT | `Ok` | `VatMatched` |
| case-002 "Chopin" at Empik | `Alert` | `CategoryAmbiguousWithDifferentVatRates` |
| case-003 Wódka, 8% VAT | `Critical` | `VatMismatch` |

**Accepts when:** All three golden cases pass with Ollama running. If case-002 fails (embedding scores produce `Matched` instead of `Ambiguous`), tune `StrongCandidateThreshold` / `CandidateMarginThreshold` in `appsettings.json` or consider upgrading to `qwen3-embedding:4b` on the Windows desktop.

> **Note:** Re-apply a mechanism to skip AI tests in CI (where Ollama is not available). Options: environment variable gate (`ENABLE_AI_TESTS=true`), or restore `[Fact(Skip = "...")]` with a note to remove for local runs.

---

## Notes

- **Ordering constraint:** Steps 3 and 4 must follow Step 2 (embedding generator must be registered before warmup service compiles against it). Steps 5 and 6 can proceed in parallel after Step 3 is done.
- **Seed file duplication:** After Step 1, the seed JSON exists in both `src/VatVerifier.Api/Data/` and `tests/.../Datasets/`. This is intentional for the PoC; the test copy is the evaluation harness fixture, the API copy is runtime data. Keep them in sync manually for now.
- **Model pull time:** `qwen3-embedding:0.6b` is ~300 MB. First `docker compose up` on a cold machine will take a minute before the warmup service can proceed.
- **Graceful degradation during tests:** `WebApplicationFactory<Program>` will start the hosted service. If Ollama is not running, `CategoryEmbeddingWarmupService` catches the exception and calls `store.MarkFailed(...)`. The deterministic test (`Evaluate_ShouldReturnSuccessfulResponse_ForEveryDatasetCase`) checks only that a 200 response is returned — it will pass even in degraded state as long as the engine returns a valid `EvaluateInvoiceLineResponse` instead of throwing.
