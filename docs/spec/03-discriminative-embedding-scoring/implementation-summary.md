# Implementation Summary: Discriminative Embedding Scoring

## Meta

| Field | Value |
|:---|:---|
| Spec | `docs/spec/03-discriminative-embedding-scoring/spec.md` |
| Plan | `docs/spec/03-discriminative-embedding-scoring/implementation-plan.md` |
| Date | 2026-06-05 |
| Status | Partial |
| Review | PASS_WITH_EXCEPTIONS |

## Steps

| # | Title | Status | Files changed |
|:---|:---|:---|:---|
| 1 | Extend `StoredCategory` and update `ICategoryEmbeddingStore` | Done | `src/VatVerifier.Api/Embeddings/ICategoryEmbeddingStore.cs`, `src/VatVerifier.Api/Embeddings/InMemoryCategoryEmbeddingStore.cs` |
| 2 | Extend `EvaluationOptions` | Done | `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` |
| 3 | Update `CategoryEmbeddingWarmupService` | Done (see deviation) | `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs` |
| 4 | Implement negative penalty scoring | Done | `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs` |
| 5 | Add supplier channel and wRRF fusion | Done | `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs`, `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` |
| 6 | Threshold calibration | Skipped | — |

## Files changed

- `src/VatVerifier.Api/Embeddings/ICategoryEmbeddingStore.cs` — `StoredCategory` renamed `Vector` → `PositiveVector`; added `NegativeVector`, `SupplierVector`; `Store()` signature updated
- `src/VatVerifier.Api/Embeddings/InMemoryCategoryEmbeddingStore.cs` — `Store()` implementation updated
- `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs` — added `NegativePenaltyWeight`, `DescriptionChannelWeight`, `SupplierChannelWeight`, `RrfK`
- `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs` — three-batch embedding generation (positive, negative, supplier); `Array.Empty<float>()` sentinel for categories with no negative examples
- `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs` — two-channel scoring: adjusted cosine (positive − α×negative) + supplier wRRF fusion; `IOptions<EvaluationOptions>` injection added
- `src/VatVerifier.Api/Evaluation/EmbeddingVatEvaluationEngine.cs` — updated `EmbeddingClassificationStep` constructor call with `options` parameter
- `src/VatVerifier.Api/AssemblyAttributes.cs` — **created**; `InternalsVisibleTo("VatVerifier.EvaluationTests")` to expose `internal` step class to unit tests
- `src/VatVerifier.Api/appsettings.json` — added `NegativePenaltyWeight`, `DescriptionChannelWeight`, `SupplierChannelWeight`, `RrfK` to `Evaluation` section
- `tests/VatVerifier.EvaluationTests/ClassifierUnitTests.cs` — updated all `store.Store()` call sites to 3-vector signature; added `EmbeddingClassificationStepTests` with `StubEmbeddingGenerator` and 4 unit tests
- `README.md` — updated `Configuration` section (new keys + table) and `Project layout` section
- `docs/spec/03-discriminative-embedding-scoring/deviation-log.md` — **created**

## Test results

| Metric | Baseline | Final |
|:---|:---|:---|
| Build | PASS | PASS |
| Tests passed | 61 | 61 |
| Tests failed | 0 | 0 |
| Regressions | 0 | 0 |

Note: counts are for structural tests only. AI/evaluation tests (require live Ollama) were not re-run; validation of the 8 previously failing cases is pending threshold calibration (Step 6).

## Test coverage

| Step # | Feature / behaviour | Coverage |
|:---|:---|:---|
| 1 | `StoredCategory` 3-vector record | Covered — `ClassifierUnitTests` updated for new `Store()` signature |
| 2 | `EvaluationOptions` new fields | Covered — unit tests use `DefaultOpts()` with all 4 new fields |
| 3 | Warmup three-batch embedding | Missing — warmup service has no unit tests (integration-only) |
| 4 | Negative penalty reduces score | Covered — `NegativePenalty_ReducesScoreForNegativeSimilarCategory` |
| 4 | No penalty when negative vector is empty | Covered — `NoPenalty_WhenNegativeVectorIsEmpty` |
| 5 | Description channel dominates misleading supplier | Covered — `DescriptionChannel_DominatesWhenSupplierMisleads` |
| 5 | Missing supplier → neutral rank for all | Covered — `EmptySupplier_AssignsNeutralRankToAllCategories` |
| 6 | Threshold calibration against live Ollama | Missing — deferred |

## Deviations

See `deviation-log.md` — 1 deviation, confirmed.

## Review checks

| Check | Result | Notes |
|:---|:---|:---|
| Build passes | ✓ | Clean build, no warnings |
| All tests pass | ✓ | 61/61 structural tests pass |
| New features have tests | ✓ | 4 unit tests for scoring; warmup service coverage missing (accepted) |
| No unconfirmed deviations | ✓ | 1 deviation, documented and confirmed |
| No regressions | ✓ | 0 regressions |

## Outstanding issues

- **Step 6 deferred (threshold calibration)**: Requires live Ollama with `qwen3-embedding:0.6b`. The 8 previously failing AI evaluation tests cannot be verified until calibration runs. Current `appsettings.json` thresholds (0.45 / 0.38 / 0.09) were set for the pre-discriminative scoring model and may need adjustment after the scoring function changed. Run `dotnet test --filter "Category=AI"` with Ollama running and tune thresholds to pass all 32 cases. See plan Step 6 for calibration guidance.
- **Warmup service coverage missing (accepted)**: `CategoryEmbeddingWarmupService` has no unit tests. It is a hosted service wiring layer; correctness is validated through integration tests. Accepted: no action required.
