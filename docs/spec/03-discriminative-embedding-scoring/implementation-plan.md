# Implementation Plan: Discriminative Embedding Scoring

## Prerequisites

- Ollama running locally with `qwen3-embedding:0.6b` pulled
- All 24 currently passing AI test cases confirmed green before starting (`dotnet test --filter "Category=AI"`)
- No new NuGet packages required — changes are entirely within existing abstractions

## Steps

### Step 1 — Extend `StoredCategory` and update `ICategoryEmbeddingStore`

**What:** Add `NegativeVector` (`float[]`) and `SupplierVector` (`float[]`) to the `StoredCategory` record. Rename the existing `Vector` field to `PositiveVector` for clarity. Update `ICategoryEmbeddingStore.Store()` to accept all three vectors. Update `InMemoryCategoryEmbeddingStore` to implement the new signature.

**Files:**
- `src/VatVerifier.Api/Embeddings/ICategoryEmbeddingStore.cs` — `StoredCategory` record + `Store()` signature
- `src/VatVerifier.Api/Embeddings/InMemoryCategoryEmbeddingStore.cs` — `Store()` implementation

**Accepts when:** `StoredCategory` has `PositiveVector`, `NegativeVector`, `SupplierVector`; `Store()` compiles with updated signature; all existing callers of `c.Vector` updated to `c.PositiveVector`; `GtuFastPathStep` still compiles (it only reads `GtuCodes` and `ExpectedVatRate`).

---

### Step 2 — Extend `EvaluationOptions`

**What:** Add four new configuration fields to `EvaluationOptions`:

```csharp
public double NegativePenaltyWeight    { get; init; } = 0.30;
public double DescriptionChannelWeight { get; init; } = 0.70;
public double SupplierChannelWeight    { get; init; } = 0.30;
public int    RrfK                     { get; init; } = 60;
```

**Files:**
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs`

**Accepts when:** All fields present with defaults; existing fields unchanged; the section deserialises correctly when `appsettings.json` does not specify the new keys.

---

### Step 3 — Update `CategoryEmbeddingWarmupService`

**What:** Replace the single `BuildCategoryText` call and single `GenerateAsync` batch with three separate batch calls:

1. **Positive texts** (one per category):
   ```
   "{Name.En}: {Description.En}\n\nExamples: {string.Join(", ", PositiveExamples)}"
   ```
2. **Negative texts** (one per category): `string.Join(", ", NegativeExamples)`. Use an empty-string placeholder for categories with no negatives; the resulting zero-ish vector will produce `neg_sim ≈ 0` (no penalty).
3. **Supplier texts** (one per category): `string.Join(", ", TypicalSuppliers)`.

All three batches are submitted as separate `GenerateAsync` calls. Store results via the updated `Store()`.

**Files:**
- `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs`

**Accepts when:** Warmup completes without error; log line `"Category embeddings ready"` appears; all categories have all three vectors stored.

---

### Step 4 — Implement negative penalty scoring in `EmbeddingClassificationStep`

**What:** In the per-category scoring loop, compute:

```csharp
var posSim = (double)TensorPrimitives.CosineSimilarity(queryDescVector, c.PositiveVector);
var negSim = (double)TensorPrimitives.CosineSimilarity(queryDescVector, c.NegativeVector);
var adjScore = posSim - _options.NegativePenaltyWeight * negSim;
```

Skip the penalty (`negSim = 0`) when the negative vector is a zero vector. Use `adjScore` to build the description-channel ranked list. The `ScoredCategory.Score` passed to the classifier is `adjScore` (preserves cosine similarity scale for threshold comparisons).

**Files:**
- `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs`

**Accepts when:** `adjScore` is used for description-channel ranking; categories with no negatives produce `adjScore == posSim`; no change to `CosineSimilarityClassifier` required.

---

### Step 5 — Add supplier channel and wRRF fusion to `EmbeddingClassificationStep`

**What:** Add a second `GenerateAsync` call for the supplier signal:

```csharp
var supplierText = BuildSupplierText(request);  // "supplierName | supplierIndustry"
var supplierEmbeddings = await embeddingGenerator.GenerateAsync([supplierText], ...);
var supplierVector = supplierEmbeddings[0].Vector.ToArray();
```

Supplier text fallback: if both `SupplierName` and `SupplierIndustry` are null/empty, set `supplierVector = null` and assign `supplier_rank = categories.Count` (last rank) to all categories.

Build two ranked lists and compute wRRF:

```csharp
// desc_ranked: sort by adjScore descending (0-based index = rank)
// supplier_ranked: sort by cosim(supplierVector, c.SupplierVector) descending

var k = _options.RrfK;
var wDesc = _options.DescriptionChannelWeight;
var wSupp = _options.SupplierChannelWeight;

foreach (var category in categories)
{
    var finalScore = wDesc * (1.0 / (k + descRank[category]))
                   + wSupp * (1.0 / (k + supplierRank[category]));
    // ScoredCategory.Score = adjScore (not finalScore) — for threshold comparison
}

// Sort by finalScore descending, pass to classifier
```

**Files:**
- `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs`

**Accepts when:** Two `GenerateAsync` calls per non-GTU request (visible in logs); the vodka-from-bookstore query (`"Wódka Chopin 0,7l"` + `"Empik SA | bookstore"`) ranks `alcohol_spirits_23` above `books_5` in the fused result; GTU fast-path cases are unaffected (they never reach this step).

---

### Step 6 — Threshold calibration

**What:** Run the full AI test suite against a live Ollama instance. Inspect actual `adjScore` values for the 8 previously failing cases and the 24 previously passing cases. Adjust `StrongCandidateThreshold`, `AmbiguousCandidateThreshold`, and `CandidateMarginThreshold` in `EvaluationOptions` defaults to pass all 32 test cases.

Add low-confidence logging already in `EmbeddingVatEvaluationEngine` to observe score distributions during the calibration run.

**Files:**
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` (default values only)

**Accepts when:** `dotnet test --filter "Category=AI"` returns 32/32 passing; no previously passing case regresses.

---

## Notes

- Steps 1–2 are pure structural changes with no runtime behaviour impact; they can be committed independently as a safe refactor.
- Steps 3–5 form one atomic scoring redesign and must be implemented together. Running the updated warmup (Step 3) without the updated scoring step (Steps 4–5) will silently produce incorrect results at runtime without failing any structural tests.
- Step 6 requires a live Ollama instance; threshold values cannot be derived analytically from code inspection alone.
- The `EmbeddingClassificationStep` will now make two sequential `GenerateAsync` calls per non-GTU request, doubling Ollama round-trip latency for the embedding path. This is acceptable for a PoC with no SLA.
- `NegativePenaltyWeight = 0.30` and the wRRF weights are starting estimates. If Step 6 reveals instability in borderline cases (e.g., advertising insert, dietary supplement), adjust `NegativePenaltyWeight` first before touching the wRRF weights.
