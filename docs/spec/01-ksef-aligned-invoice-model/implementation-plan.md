# Implementation Plan: KSeF-Aligned Invoice Model

## Prerequisites

- Ollama running locally (Docker Compose in `docker/ollama/`) with `qwen3-embedding:0.6b` pulled
- Existing evaluation pipeline compiling and base integration tests passing
- `EmbeddingVatEvaluationEngine` implemented (pre-existing from bootstrap)

---

## Steps

### Step 0 — Reorganize Evaluation namespace

**What:** Split the flat `Evaluation/` folder into four focused namespaces. The warmup service is an application lifecycle concern (`Startup/`), not a RAG concern. Embedding store and classifier live in their own namespaces. The engine and options stay in `Evaluation/` as the public evaluation boundary.

**Target structure:**

```
src/VatVerifier.Api/
  Evaluation/       — engine, options, public interface (unchanged location)
  Classification/   — classifier interface + cosine implementation (new)
  Embeddings/       — embedding store interface + in-memory impl (new)
  Startup/          — hosted warmup service (new)
```

**Files to move and re-namespace:**

| From | To | New namespace |
|:---|:---|:---|
| `Evaluation/ICategoryClassifier.cs` | `Classification/ICategoryClassifier.cs` | `VatVerifier.Api.Classification` |
| `Evaluation/CosineSimilarityClassifier.cs` | `Classification/CosineSimilarityClassifier.cs` | `VatVerifier.Api.Classification` |
| `Evaluation/ICategoryEmbeddingStore.cs` | `Embeddings/ICategoryEmbeddingStore.cs` | `VatVerifier.Api.Embeddings` |
| `Evaluation/InMemoryCategoryEmbeddingStore.cs` | `Embeddings/InMemoryCategoryEmbeddingStore.cs` | `VatVerifier.Api.Embeddings` |
| `Evaluation/CategoryEmbeddingWarmupService.cs` | `Startup/CategoryEmbeddingWarmupService.cs` | `VatVerifier.Api.Startup` |

**Files that stay in `Evaluation/` (no move, no namespace change):**
- `IVatEvaluationEngine.cs`
- `NotImplementedVatEvaluationEngine.cs`
- `EmbeddingVatEvaluationEngine.cs`
- `EvaluationOptions.cs`

**Using statement updates required after the moves:**

| File | Add usings |
|:---|:---|
| `Evaluation/EmbeddingVatEvaluationEngine.cs` | `using VatVerifier.Api.Classification;` and `using VatVerifier.Api.Embeddings;` |
| `src/VatVerifier.Api/Program.cs` | `using VatVerifier.Api.Classification;`, `using VatVerifier.Api.Embeddings;`, `using VatVerifier.Api.Startup;` |
| `tests/VatVerifier.EvaluationTests/ClassifierUnitTests.cs` | Replace `using VatVerifier.Api.Evaluation;` with `using VatVerifier.Api.Classification;` and `using VatVerifier.Api.Embeddings;` |

**Accepts when:** `dotnet build` passes with zero errors after the moves; all 12 existing tests pass without modification to test logic.

---

### Step 1 — Enrich request contract and reason codes

**What:** Add four optional KSeF signal fields to `EvaluateInvoiceLineRequest`; add three new `EvaluationReasonCode` values for structural violations.

**Files:**
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineRequest.cs` — add `GtuCode?`, `UnitOfMeasure?`, `ReverseChargeApplied?`, `SplitPaymentRequired?`
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineResponse.cs` — add `ReverseChargeMissing`, `ReverseChargeUnexpected`, `GtuCategoryMismatch` to `EvaluationReasonCode`

**Accepts when:** Project builds with no errors; existing tests still pass; new fields appear in OpenAPI schema (nullable, not required).

---

### Step 2 — Enrich category seed model

**What:** Add `GtuCodes` and `RateVariants` to `CategorySeedEntry`; add a `RateVariant` record; update the seed JSON with GTU codes for all existing categories and rate variants for construction.

**Files:**
- `src/VatVerifier.Api/Data/CategorySeedEntry.cs` — add `IReadOnlyList<string>? GtuCodes` and `IReadOnlyList<RateVariant>? RateVariants`; add `record RateVariant(decimal Rate, string Condition)`
- `src/VatVerifier.Api/Data/vat-categories.seed.json` — add `gtuCodes` and `rateVariants` to all existing entries; add at least two new categories: `construction_services` (GTU_10, rate variants 8%/23%) and `pharmaceuticals_8` (GTU_09)

**GTU mapping reference for seed update:**

| Category | GTU code(s) |
|:---|:---|
| `alcohol_spirits_23` | `GTU_01` |
| `software_it_services_23` | `GTU_12` |
| `books_5` | *(no GTU)* |
| `construction_services` (new) | `GTU_10` |
| `pharmaceuticals_8` (new) | `GTU_09` |

**Accepts when:** Seed JSON deserializes without errors (add a startup validation assertion); new fields present and non-null for all entries that have GTU codes.

---

### Step 3 — Update test case schema and add new cases

**What:** Add `metadata` block to all three existing test cases; add at minimum four new cases covering: GTU fast-path match, reverse charge missing, GTU code contradicting description, construction rate variant.

**Files:**
- `tests/VatVerifier.EvaluationTests/Datasets/invoice-line-evaluation-cases.json` — update existing cases + add new cases

**Minimum new cases to add:**

| Case ID | Description | Edge case type | `criticalFailureRisk` |
|:---|:---|:---|:---|
| `case-004` | GTU_01 (alcohol) with invoiceVatRate=8 → Critical | `vatMismatchConfidentCategory` | `true` |
| `case-005` | Construction subcontract, reverseChargeApplied=false, invoiceVatRate=23 → Critical | `reverseChargeNotApplied` | `true` |
| `case-006` | GTU_12 (software) description says "Szkolenie BHP" — GTU trusted, VAT matches → Ok | `gtuCategoryMismatch` | `false` |
| `case-007` | Usługi budowlane, no GTU, construction with rate variants → Ambiguous/Alert | `constructionRateVariant` | `false` |

**Accepts when:** Test runner loads and parses all cases without error; `metadata` fields are present on new cases.

---

### Step 4 — Add GTU fast-path to the evaluation engine

**What:** Before invoking embedding, check if the request contains a `GtuCode`. If a stored category has that code in its `GtuCodes` list, use that entry directly as the matched category (skip cosine similarity). If the GTU code is present but matches no stored entry, fall through to embedding normally.

**Background — why `Store()` must change:** `ICategoryEmbeddingStore.Store()` currently accepts only `categoryId, vector, name, expectedVatRate`. GTU codes and rate-variant presence are never passed in, so `FindByGtuCode` would have no data to search. The fix: extend `Store()` and `StoredCategory` to carry these fields, and update `CategoryEmbeddingWarmupService` (which already has the full `CategorySeedEntry`) to pass them through.

**Files:**
- `src/VatVerifier.Api/Embeddings/ICategoryEmbeddingStore.cs`
  - Update `Store()`: add `IReadOnlyList<string>? gtuCodes` and `bool hasRateVariants` parameters
  - Update `StoredCategory`: add `IReadOnlyList<string>? GtuCodes` and `bool HasRateVariants` fields
  - Add `FindByGtuCode(string gtuCode)` returning `StoredCategory?` — **not** `CategorySeedEntry?`; returning `StoredCategory?` keeps the `Embeddings` interface free of `Data` namespace imports
- `src/VatVerifier.Api/Embeddings/InMemoryCategoryEmbeddingStore.cs`
  - Implement updated `Store()` and an internal `Dictionary<string, StoredCategory>` keyed by GTU code for O(1) lookup
  - Implement `FindByGtuCode`: returns the matching `StoredCategory` or `null`
- `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs`
  - Update the `store.Store(...)` call to pass `entry.GtuCodes` and `entry.RateVariants?.Count > 0`
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs`
  - Add GTU pre-check before the embedding call: if `request.GtuCode` is set and `store.FindByGtuCode` returns a match, construct a synthetic `ScoredCategory` with score `1.0` and skip `GenerateAsync`
  - When GTU-resolved category has `HasRateVariants = true` and no condition is determinable, force `Ambiguous` classification (consistent with spec open question on rate variants)
- `tests/VatVerifier.EvaluationTests/ClassifierUnitTests.cs`
  - Update the two existing `InMemoryCategoryEmbeddingStoreTests` tests: calls to `Store("cat-1", [1f, 0f], "Software", 23m)` gain two new trailing arguments `gtuCodes: null, hasRateVariants: false`
  - Add a new `EmbeddingVatEvaluationEngineGtuTests` class with a unit test that proves embedding is bypassed: inject a `ThrowingEmbeddingGenerator` stub (private inner class that throws `InvalidOperationException` from `GenerateAsync`); pre-populate the store with GTU_01 registered and `MarkReady()`; call `EvaluateAsync` with `gtuCode: "GTU_01"` and `invoiceVatRate: 8`; assert no exception thrown, `CategoryMatchStatus = Matched`, `Severity = Critical`

**Accepts when:**
- Integration test `case-004` (GTU_01, vodka at 8%) returns `Critical`/`VatMismatch`/`Matched`
- New `EmbeddingVatEvaluationEngineGtuTests` unit test passes: engine returns `Matched` without calling `GenerateAsync` when GTU resolves
- All pre-existing `InMemoryCategoryEmbeddingStoreTests` pass after signature update

---

### Step 5 — Add structural violation checks

**What:** Before GTU lookup and embedding, check reverse charge consistency. These checks produce a `Critical` result without consulting categories or embeddings.

**Rules:**
1. `ReverseChargeApplied = false` AND `request.GtuCode == "GTU_10"` (construction subcontracts) AND `invoiceVatRate > 0` → `ReverseChargeMissing`, `Critical`
2. `ReverseChargeApplied = true` AND `invoiceVatRate > 0` → `ReverseChargeUnexpected`, `Critical`

**Files:**
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` — add structural check before GTU and embedding paths

**Accepts when:** `case-005` (reverse charge missing on construction subcontract) returns `Critical`/`ReverseChargeMissing`; engine returns early without embedding for structural violations.

---

### Step 6 — Add confidence tracking log

**What:** After severity mapping, if `CategoryMatchStatus = Ambiguous` or the top candidate score is below the configured threshold, append the evaluation result to `low-confidence-evaluations.json`. This file is the input feed for the `rag-eval-tuner` agent.

**Log entry shape (NDJSON — one JSON object per line, true append-friendly):**
```json
{"timestamp":"2026-06-04T10:00:00Z","invoiceLineId":"line-002","description":"Chopin","topCandidateScore":0.71,"categoryMatchStatus":"Ambiguous","severity":"Alert","reasonCode":"CategoryAmbiguousWithDifferentVatRates"}
```

For GTU fast-path results, `topCandidateScore` is `1.0` (synthetic exact-match score).

**Files:**
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` — add `ConfidenceThreshold` (default: 0.75) and `ConfidenceLogPath` (default: `low-confidence-evaluations.json`)
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` — append log entry after severity mapping when confidence condition is met

**Accepts when:** Running `case-002` (Chopin, Ambiguous) writes an entry to the log file; log file is valid NDJSON; no exception when log path is not configured (silently skip).

---

## Notes

- **Step 0 is a prerequisite for all remaining steps.** Complete it first; all subsequent steps use the new paths.
- Steps 1–3 are pure model/data changes and can be done without Ollama running. Steps 4–6 require Ollama for full integration test coverage of the embedding path.
- The GTU fast-path (Step 4) must be verified via a **unit test** (not via the integration test suite with WebApplicationFactory), because `WebApplicationFactory<Program>` always starts the full app and the warmup service will connect to Ollama if it is running. The `ThrowingEmbeddingGenerator` unit test in `ClassifierUnitTests.cs` provides this guarantee.
- The `Store()` signature change in Step 4 cascades to existing `InMemoryCategoryEmbeddingStoreTests` — two calls must be updated to add `gtuCodes: null, hasRateVariants: false`. This is part of Step 4 scope.
- Step 5, Rule 1 uses the GTU code from the request (not post-classification category) because structural checks run before category matching. If `GtuCode` is null, the check is skipped for Rule 1 (per spec open question: only explicit flags trigger checks).
- `RateVariant.Condition` is free text in v1 and is not evaluated programmatically. The `construction_services` category remains `Ambiguous` when no GTU code is present.
- The confidence log path is configurable via `appsettings.json` under `Evaluation:ConfidenceLogPath`. The `rag-eval-tuner` agent reads the output of Step 6.
