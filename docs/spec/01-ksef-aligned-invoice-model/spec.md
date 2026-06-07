# KSeF-Aligned Invoice Model

## Goal

Enrich the invoice line evaluation model with structural signals from the KSeF FA(3) schema to improve VAT classification accuracy. The current model exposes only free-text description, supplier name, optional industry, and a VAT rate — too sparse to catch structural violations (reverse charge missing, GTU code contradicting description) or to short-circuit embedding for clear-cut classifications. This spec defines the enriched input model, an improved category seed structure, and a metadata-rich test case schema that supports both the evaluation engine and the `eval-tuner` agent being developed in parallel.

## Constraints

- .NET 10 Minimal API; no Clean Architecture layers, no database, no external vector store
- All new input fields are optional — existing callers and test cases are not broken
- Embedding and LLM remain behind `IEmbeddingGenerator` / `IChatClient` abstractions (OllamaSharp under the hood)
- No KSeF XML parsing or schema compliance in scope — only the fields that carry classification signal
- PoC: no production SLAs, no auth, no persistent log store (append-only JSON file is sufficient)
- Free/OSS stack only; no managed cloud services required to run

## Chosen architecture

Four dimensions were decided: a KSeF-signals request model, GTU-enriched category seed, a metadata-rich test case schema, and confidence tracking as the feed for the parallel eval-tuner agent.

### Invoice Input Model: Signals pack (Option B)

Four optional fields are added to `EvaluateInvoiceLineRequest`, each carrying a distinct classification signal:

| Field | KSeF source | Signal |
|:---|:---|:---|
| `GtuCode?` | `FaWiersz` GTU codes | Controlled 13-code vocabulary; maps near-deterministically to a product/service category |
| `UnitOfMeasure?` | `P_8A` | `szt.`/`kg`/`l` = goods; `godz.`/`usł.` = services; helps disambiguate embedding candidates |
| `ReverseChargeApplied?` | `P_18` | If `true`, VAT on this line MUST be `0` or `NP`; any positive rate is `Critical` without needing embedding |
| `SplitPaymentRequired?` | `P_18A` | Signals the item is on the statutory sensitive-goods list; narrows likely category |

When the request comes from a KSeF XML parser, all four fields are natively available. For manual/test inputs they remain optional; `null` means "not provided."

### Category Seed: GTU codes + rate variants (Option A + partial C)

Two additions to `CategorySeedEntry`:

- `GtuCodes?: string[]` — which GTU codes map to this category. Enables a fast exact-match path before embedding is invoked. The GTU vocabulary has only 13 codes, so maintenance cost is negligible.
- `RateVariants?: RateVariant[]` — used for categories with rate-conditional logic. Currently needed only for `construction_services`: 8% (residential, ≤150 m²) vs. 23% (commercial). The `condition` field is free text; it is used in test case rationale and as LLM context, not for programmatic rule matching in v1.

PKWiU prefix mapping is deferred — rarely included in practice, high maintenance, good candidate for a later spec once we have real invoice data to validate against.

### Test Case Schema: Flat JSON with metadata block (Option C)

The existing flat JSON array format is preserved. Each case gains an optional `metadata` block:

```json
"metadata": {
  "edgeCaseType": "<taxonomy code>",
  "rationale": "<why this case is interesting and what it tests>",
  "criticalFailureRisk": true | false
}
```

`criticalFailureRisk: true` marks cases where a wrong engine result would be a false OK or false low severity. These are the most important test cases; they should fail loudly and block at CI even before the full AI pipeline is enabled.

`edgeCaseType` vocabulary maps to the taxonomy in `polish-vat-domain.instructions.md`:

| Type | Edge case |
|:---|:---|
| `linguisticAmbiguity` | Polish brand/word with multiple product meanings |
| `vatMismatchConfidentCategory` | Clear category match, wrong rate on invoice |
| `supplierItemMismatch` | Supplier industry contradicts item sold |
| `borderlineCategory` | Item genuinely belongs to two categories |
| `bilingualDescription` | Same item described in PL vs EN |
| `missingCategory` | No reasonable match in seed data |
| `rateIdenticalAmbiguity` | Ambiguous but all possible categories share the same rate |
| `exemptMisclassified` | Medical/educational service charged at a positive VAT rate |
| `reverseChargeNotApplied` | B2B construction subcontract without reverse charge |
| `constructionRateVariant` | Same description, different rate depending on building type |
| `splitPaymentThreshold` | High-value sensitive goods without split payment flag |
| `gtuCategoryMismatch` | GTU code contradicts the description or category |

### Adaptive quality improvement: Confidence tracking (Option B, feeds eval-tuner)

The evaluation engine logs every result with `CategoryMatchStatus = Ambiguous` or a top candidate score below a configurable threshold to an append-only JSON file (`low-confidence-evaluations.json`). This file is the input feed for the `eval-tuner` agent being built in parallel. The agent reads failing/ambiguous cases and proposes seed JSON patches (better descriptions, new positive/negative examples).

Confidence tracking does not require a database — a simple `System.IO.File.AppendAllText` on each qualifying result is sufficient for the PoC.

## Components

Namespace layout after reorganization:

```
Evaluation/     IVatEvaluationEngine, EmbeddingVatEvaluationEngine, EvaluationOptions
Classification/ ICategoryClassifier, CosineSimilarityClassifier, ScoredCategory, ClassificationResult
Embeddings/     ICategoryEmbeddingStore, InMemoryCategoryEmbeddingStore, StoredCategory
Startup/        CategoryEmbeddingWarmupService
```

New or modified:

- `Contracts/EvaluateInvoiceLineRequest` — add `GtuCode?`, `UnitOfMeasure?`, `ReverseChargeApplied?`, `SplitPaymentRequired?`
- `Contracts/EvaluationReasonCode` — add `ReverseChargeMissing`, `ReverseChargeUnexpected`, `GtuCategoryMismatch`
- `Data/CategorySeedEntry` — add `GtuCodes?` (`IReadOnlyList<string>?`) and `RateVariants?` (`IReadOnlyList<RateVariant>?`)
- `Data/RateVariant` (new record) — `decimal Rate`, `string Condition`
- `Data/vat-categories.seed.json` — add `gtuCodes` and `rateVariants` to existing entries; add new categories for coverage gaps
- `Embeddings/ICategoryEmbeddingStore` — update `Store()` signature; add `FindByGtuCode()`; update `StoredCategory`
- `Embeddings/InMemoryCategoryEmbeddingStore` — implement GTU index
- `Startup/CategoryEmbeddingWarmupService` — pass GTU codes and rate-variant flag to `Store()`
- `Evaluation/EmbeddingVatEvaluationEngine` — add GTU fast-path lookup and structural violation checks before embedding
- `Evaluation/EvaluationOptions` — add `ConfidenceThreshold` (default: 0.75) and `ConfidenceLogPath`
- `invoice-line-evaluation-cases.json` — add `metadata` to existing cases; add new GTU, reverse-charge, and construction-variant cases
- `low-confidence-evaluations.json` (runtime output, NDJSON) — append-only log consumed by eval-tuner agent

## New reason codes

| Code | Condition |
|:---|:---|
| `ReverseChargeMissing` | Request has `ReverseChargeApplied = false` and category implies reverse charge mandatory |
| `ReverseChargeUnexpected` | Request has `ReverseChargeApplied = true` but invoice VAT rate is positive |
| `GtuCategoryMismatch` | GTU code on the request maps to a different category than the embedding match |

## Alternatives considered

| Option | Reason not chosen |
|:---|:---|
| GTU only (Dim 1, Option A) | Misses the reverse-charge structural check — a Critical case detectable without embedding |
| Invoice context wrapper (Dim 1, Option C) | Right shape for a future iteration but premature; adds object nesting before we know what invoice-level fields we need |
| PKWiU prefix patterns (Dim 2, Option B) | Complex taxonomy, rarely present in practice; deferred to future spec |
| Rate variants only, no GTU (Dim 2, Option C only) | GTU codes provide the fast-path lookup value independently of rate variants |
| Grouped test cases (Dim 3, Option B) | Breaks existing test runner; taxonomy is better as per-case metadata |
| Manual seed only, no confidence tracking (Dim 4, Option A) | Confidence log is a 30-minute addition that enables the eval-tuner agent with no ongoing cost |

## Open questions

- **Reverse charge detection heuristic:** When `ReverseChargeApplied` is null (not provided), should the engine infer reverse charge likelihood from the category (e.g., `construction_services` with a B2B supplier)? Deferred to implementation — for v1, only explicit `false` flags trigger the check.
- **Rate variant condition matching:** `RateVariant.Condition` is free text in v1. Should the engine use LLM or keyword matching to classify `commercial` vs. `residential` from the description? Deferred — the field exists in the model but v1 treats all rate-variant categories as `Ambiguous` unless the condition is determinable from GTU or unit.
- **Confidence log size:** The log grows unboundedly in a long-running process. For PoC this is acceptable; a future spec should add rotation or a trim step on startup.
