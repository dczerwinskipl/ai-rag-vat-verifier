# Review Report: KSeF-Aligned Invoice Model

## Meta

| Field | Value |
|:---|:---|
| Spec | `docs/spec/01-ksef-aligned-invoice-model/spec.md` |
| Summary | `docs/spec/01-ksef-aligned-invoice-model/implementation-summary.md` |
| Date | 2026-06-04 |
| Verdict | PASS |

## Verdict

All findings from the previous two reviews have been resolved or acknowledged. No CRITICAL or WARNING findings remain. The `HasRateVariants` consolidation is clean: pipeline steps are now pure classifiers, `TryApplyRateVariantDegradation` in the engine is the single post-processing point, and the found category remains visible in `CategoryCandidates` without any output model change. Two minor INFO observations are noted for completeness.

## Spec compliance

| Component / Decision | Expected | Found | Status |
|:---|:---|:---|:---|
| All original spec components | Per spec.md | Verified in prior reviews; no regressions introduced | Followed |
| `ICategoryEmbeddingStore` — `bool hasRateVariants` replaced with `IReadOnlyList<decimal>? rateVariantRates` | Stores actual variant rates, not just a flag | `StoredCategory.RateVariantRates` — null for no-variant categories, populated list for rate-variant categories | Followed |
| `ICategoryEmbeddingStore.FindByCategoryId` | O(1) lookup; replaces `store.GetAll().FirstOrDefault` linear scan | `_byCategoryId` dictionary populated in `Store()` | Followed |
| `GtuFastPathStep` — pure classifier | Returns `Matched` only; no rate-variant check inline | Simplified: always `Matched`, score 1.0 | Followed |
| `EmbeddingClassificationStep` — pure classifier | Returns classifier result; no rate-variant check inline | Removed: calls `EvaluationResponseFactory.ForClassification` directly | Followed |
| `TryApplyRateVariantDegradation` — single post-processing point | One place where `Matched` → `Ambiguous` degradation occurs | Engine lines 68–79; uses `FindByCategoryId` (O(1)) | Followed |
| `ForRateVariantDegradation` — accurate `ExpectedVatRates` | `variantRates = [8m, 23m]` for construction | Factory line 30–41; `CategoryCandidates` from matched result preserved | Followed |
| `EvaluationResponseFactory` — directly testable | `public static` | Made `public`; 8 factory tests in `EvaluationResponseFactoryTests` | Followed |
| Summary "Files changed" — complete | All 20 source files listed | Updated to include 6 pipeline/factory files from Deviation 2 | Followed |
| Confidence log exclusion for rate-variant degradation | Score 1.0 from GTU fast-path excluded from low-confidence log | `LogLowConfidenceIfNeeded` line 88: `if (topScore >= 1.0) return;` — rate-variant ambiguous results still carry score 1.0 after degradation | Followed |

## Constraint checks

| Constraint | Result | Notes |
|:---|:---|:---|
| No Semantic Kernel | ✓ Pass | No new references in any changed file |
| No direct Ollama HTTP | ✓ Pass | No new HTTP calls in pipeline or engine files |
| MEA abstractions used | ✓ Pass | `IEmbeddingGenerator` used in `EmbeddingClassificationStep`; `OllamaApiClient` stays in composition root |
| No database packages | ✓ Pass | No new packages added |
| No auth packages | ✓ Pass | Unchanged |
| No MediatR / CQRS | ✓ Pass | Unchanged |
| No Clean Architecture layers | ✓ Pass | `Pipeline/` is a sub-namespace within `Evaluation/`; no new `.csproj` |

## Deviation log audit

| Deviation # | Confirmed by | Statement valid | Status |
|:---|:---|:---|:---|
| 1 — `Store()` + `StoredCategory` + `FindByGtuCode` return type | user | Multi-word, substantive; spec writer revised the plan | Valid |
| 2 — Pipeline refactor, factory, ILogger, rate-variant post-processor | user | Direct, specific, multi-sentence user request | Valid |

## Code quality findings

| File | Finding | Severity |
|:---|:---|:---|
| `implementation-summary.md` test count | "Tests passed: 29" in the Test results table reflects the state at original implementation. Current non-AI test count is 57 due to subsequent external additions. The summary is a historical record, not a live report — the discrepancy is expected and documented in the post-review section. | INFO |
| `Evaluation/EmbeddingVatEvaluationEngine.cs` lines 64–65 | Fallback ("No pipeline step produced a result") remains unreachable — `EmbeddingClassificationStep` always returns non-null. Intentional defensive code. | INFO |

## Findings summary

| Severity | Count |
|:---|:---|
| CRITICAL | 0 |
| WARNING | 0 |
| INFO | 2 |

## Resolution of all prior findings

| Prior finding | Severity | Resolution |
|:---|:---|:---|
| Summary "Files changed" stale | WARNING | ✓ Fixed — all 20 files now listed |
| O(n) `store.GetAll().FirstOrDefault` in engine | INFO | ✓ Fixed — `FindByCategoryId` O(1) dictionary lookup |
| `EvaluationResponseFactory` not directly tested | INFO | ✓ Fixed — 8 `EvaluationResponseFactoryTests` covering all factory paths |
| `HasRateVariants` logic duplicated across two pipeline steps | INFO (user-raised) | ✓ Fixed — single `TryApplyRateVariantDegradation` post-processor in engine |
| Task-reference comments `// Step 4:` / `// Step 5:` | INFO | ✓ Fixed in prior pass |
| `EvaluateAsync` method too long (66 lines) | INFO | ✓ Fixed in prior pass (now 31 lines) |
| `UnitOfMeasure` not wired into query text | INFO | ✓ Fixed in prior pass |
| case-001 `edgeCaseType` mislabeled | INFO | ✓ Fixed in prior pass |
| `SplitPaymentRequired` not acted on | INFO | Intentional deferral — no v1 behavior defined in spec |
| Deviation 1 indirect confirmation statement | INFO | Documentation observation — no code action required |
