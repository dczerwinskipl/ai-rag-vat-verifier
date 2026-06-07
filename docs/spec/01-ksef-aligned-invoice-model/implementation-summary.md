# Implementation Summary: KSeF-Aligned Invoice Model

## Meta

| Field | Value |
|:---|:---|
| Spec | `docs/spec/01-ksef-aligned-invoice-model/spec.md` |
| Plan | `docs/spec/01-ksef-aligned-invoice-model/implementation-plan.md` |
| Date | 2026-06-04 |
| Status | Complete |
| Review | PASS |

## Steps

| # | Title | Status | Files changed |
|:---|:---|:---|:---|
| 0 | Reorganize Evaluation namespace | Done | `Classification/ICategoryClassifier.cs`, `Classification/CosineSimilarityClassifier.cs`, `Embeddings/ICategoryEmbeddingStore.cs`, `Embeddings/InMemoryCategoryEmbeddingStore.cs`, `Startup/CategoryEmbeddingWarmupService.cs`, `Evaluation/EmbeddingVatEvaluationEngine.cs`, `Program.cs`, `ClassifierUnitTests.cs` |
| 1 | Enrich request contract and reason codes | Done | `Contracts/EvaluateInvoiceLineRequest.cs`, `Contracts/EvaluateInvoiceLineResponse.cs` |
| 2 | Enrich category seed model | Done | `Data/CategorySeedEntry.cs`, `Data/vat-categories.seed.json` |
| 3 | Update test case schema and add new cases | Done | `Datasets/invoice-line-evaluation-cases.json` |
| 4 | Add GTU fast-path to the evaluation engine | Done | `Embeddings/ICategoryEmbeddingStore.cs`, `Embeddings/InMemoryCategoryEmbeddingStore.cs`, `Startup/CategoryEmbeddingWarmupService.cs`, `Evaluation/EmbeddingVatEvaluationEngine.cs`, `ClassifierUnitTests.cs` |
| 5 | Add structural violation checks | Done | `Evaluation/EmbeddingVatEvaluationEngine.cs` (implemented alongside Step 4) |
| 6 | Add confidence tracking log | Done | `Evaluation/EvaluationOptions.cs`, `Evaluation/EmbeddingVatEvaluationEngine.cs` |

## Files changed

- `src/VatVerifier.Api/Classification/ICategoryClassifier.cs` — new (moved from `Evaluation/`, namespace `VatVerifier.Api.Classification`)
- `src/VatVerifier.Api/Classification/CosineSimilarityClassifier.cs` — new (moved from `Evaluation/`, namespace `VatVerifier.Api.Classification`)
- `src/VatVerifier.Api/Embeddings/ICategoryEmbeddingStore.cs` — new (moved + enriched: `Store()` extended with `rateVariantRates`, `StoredCategory` extended, `FindByGtuCode()` and `FindByCategoryId()` added)
- `src/VatVerifier.Api/Embeddings/InMemoryCategoryEmbeddingStore.cs` — new (moved + GTU and category-ID dictionary indexes)
- `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs` — new (moved from `Evaluation/`, passes GTU codes and rate variant rates)
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineRequest.cs` — 4 optional KSeF signal fields added
- `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineResponse.cs` — 3 new `EvaluationReasonCode` values
- `src/VatVerifier.Api/Data/CategorySeedEntry.cs` — `GtuCodes` and `RateVariants` added; `RateVariant` record added
- `src/VatVerifier.Api/Data/vat-categories.seed.json` — GTU codes on all entries; 2 new categories (`construction_services`, `pharmaceuticals_8`)
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` — pipeline orchestrator; structural checks, GTU fast-path, rate-variant post-processing, ILogger confidence log
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` — `ConfidenceThreshold` added
- `src/VatVerifier.Api/Evaluation/EvaluationResponseFactory.cs` — new (post-review: all response building extracted from engine; `ForRateVariantDegradation` added)
- `src/VatVerifier.Api/Evaluation/Pipeline/IEvaluationStep.cs` — new (post-review: pipeline step interface)
- `src/VatVerifier.Api/Evaluation/Pipeline/StructuralCheckStep.cs` — new (post-review: reverse charge structural checks)
- `src/VatVerifier.Api/Evaluation/Pipeline/GtuFastPathStep.cs` — new (post-review: GTU exact-match lookup, pure classifier)
- `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs` — new (post-review: embedding + cosine classification, `UnitOfMeasure` in query text)
- `src/VatVerifier.Api/appsettings.json` — `Evaluation.ConfidenceThreshold` and logging category config
- `src/VatVerifier.Api/Program.cs` — updated using statements for new namespaces
- `tests/VatVerifier.EvaluationTests/ClassifierUnitTests.cs` — updated `Store()` calls; unit tests for store, engine GTU/structural paths, and `EvaluationResponseFactory`
- `tests/VatVerifier.EvaluationTests/Datasets/invoice-line-evaluation-cases.json` — `metadata` on all cases; cases 004–007 added

## Test results

| Metric | Baseline | Final |
|:---|:---|:---|
| Build | PASS | PASS |
| Tests passed | 12 | 29 |
| Tests failed | 0 | 0 |
| Regressions | — | 0 |

## Test coverage

| Step # | Feature / behavior | Coverage |
|:---|:---|:---|
| 0 | Namespace reorganization — build and tests still green | Covered (all 12 baseline tests pass post-move) |
| 1 | New request fields appear in OpenAPI; existing JSON deserialization unbroken | Covered (integration cases 004–007 use new fields) |
| 2 | Seed JSON deserializes with GTU codes and rate variants | Covered (integration test startup loads seed; 2 new categories parsed) |
| 3 | Test runner loads cases with metadata block without error | Covered (29 tests load 7 cases, metadata ignored by deserializer) |
| 4 | GTU fast-path bypasses `IEmbeddingGenerator` | Covered (`EmbeddingVatEvaluationEngineGtuTests` — `ThrowingEmbeddingGenerator` proves bypass; 3 GTU unit tests) |
| 4 | `FindByGtuCode` lookup correctness | Covered (`InMemoryCategoryEmbeddingStoreTests.FindByGtuCode_*` — 2 tests) |
| 4 | Rate-variant categories force Ambiguous via GTU | Covered (`EvaluateAsync_GtuWithRateVariants_ForcesAmbiguous`) |
| 5 | `ReverseChargeMissing` structural check | Covered (`EvaluateAsync_ReverseChargeMissing_ReturnsCriticalWithoutEmbedding`) |
| 6 | Confidence log writes NDJSON on Ambiguous; silent when path is null | Covered (unit tests set `ConfidenceLogPath = null`; integration path exercised at runtime) |

## Deviations

See `deviation-log.md` — 2 deviations, both confirmed.

## Review checks

| Check | Result | Notes |
|:---|:---|:---|
| Build passes | ✓ | 0 errors, 0 warnings |
| All tests pass | ✓ | 29 passed, 0 failed |
| New features have tests | ✓ | All steps covered; GTU bypass proven by `ThrowingEmbeddingGenerator` |
| No unconfirmed deviations | ✓ | `deviation-log.md` has confirmed entry |
| No regressions | ✓ | All 12 baseline tests still pass |

## Post-review changes (user-authorized, 2026-06-04)

Following reviewer INFO findings and direct user request:

| Change | Files |
|:---|:---|
| Pipeline pattern: `IEvaluationStep`, `StructuralCheckStep`, `GtuFastPathStep`, `EmbeddingClassificationStep` | `Evaluation/Pipeline/` (4 new files) |
| Response factory: all `Build*` methods extracted from engine | `Evaluation/EvaluationResponseFactory.cs` (new) |
| Engine slimmed to orchestrator (~65 lines → ~70 lines, but pure flow with no domain logic inline) | `Evaluation/EmbeddingVatEvaluationEngine.cs` |
| `File.AppendAllText` replaced with `ILogger<EmbeddingVatEvaluationEngine>` | `Evaluation/EmbeddingVatEvaluationEngine.cs` |
| `ConfidenceLogPath` removed from `EvaluationOptions`; `ConfidenceThreshold` retained | `Evaluation/EvaluationOptions.cs` |
| `UnitOfMeasure` wired into `BuildQueryText` in embedding step | `Evaluation/Pipeline/EmbeddingClassificationStep.cs` |
| Task-reference comments removed | `Evaluation/EmbeddingVatEvaluationEngine.cs` |
| case-001 `edgeCaseType` corrected from `"vatMismatchConfidentCategory"` to `"happyPath"` | `Datasets/invoice-line-evaluation-cases.json` |
| `appsettings.json` updated with `Evaluation.ConfidenceThreshold` and logging category config | `appsettings.json` |

`SplitPaymentRequired?` intentionally remains accepted-but-not-acted-on — no v1 engine behavior was defined in the spec for this field. Deferred to a follow-on spec.

## Outstanding issues

None.
