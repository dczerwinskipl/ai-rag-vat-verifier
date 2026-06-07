# Deviation Log: KSeF-Aligned Invoice Model

## Deviation 2

| Field | Value |
|:---|:---|
| Step | Post-review — engine refactoring (user-authorized) |
| Gate triggered | Deviation Confirmation Gate |
| Original plan said | `EmbeddingVatEvaluationEngine` contains all evaluation logic, `File.AppendAllText` for confidence logging, `ConfidenceLogPath` in options |
| Implemented instead | Engine split into pipeline steps (`StructuralCheckStep`, `GtuFastPathStep`, `EmbeddingClassificationStep`); response building extracted to `EvaluationResponseFactory`; `File.AppendAllText` replaced with `ILogger<EmbeddingVatEvaluationEngine>`; `ConfidenceLogPath` removed from `EvaluationOptions`; `UnitOfMeasure` wired into embedding query text; `BuildQueryText` moved to `EmbeddingClassificationStep`; case-001 `edgeCaseType` corrected to `"happyPath"` |
| Reason | Reviewer identified engine had too many responsibilities (INFO findings). User explicitly requested pipeline pattern, response factory extraction, and ILogger-based confidence logging replacing manual file writes. |
| Confirmed by | user |
| Confirmation statement | "EmbeddingVatEvaluationEngine got 2 much responsibilities in a single class; we could have strategy pattern or chain of responsibility or any other pipeline-like... BuildResponse could be methods on result objects OR extension methods... I would also expect to have better solution to write logs then File append manually... written using log with some category and correct logging settings to redirect those confidence to separate file" |
| Date | 2026-06-04 |

## Deviation 1

| Field | Value |
|:---|:---|
| Step | Step 4 — Add GTU fast-path to the evaluation engine |
| Gate triggered | Spec Validation Gate |
| Original plan said | `FindByGtuCode(string gtuCode)` returns `CategorySeedEntry?`; `Store()` signature unchanged |
| Implemented instead | `Store()` extended with `IReadOnlyList<string>? gtuCodes` and `bool hasRateVariants`; `StoredCategory` extended with matching fields; `FindByGtuCode` returns `StoredCategory?` (not `CategorySeedEntry?`); `CategoryEmbeddingWarmupService` updated to pass both new parameters; GTU bypass verified via `ThrowingEmbeddingGenerator` unit test rather than "disable Ollama" integration test |
| Reason | The original `Store()` had no parameter to deliver GTU codes into the store, making `FindByGtuCode` unimplementable as specified. Returning `StoredCategory?` keeps the `Embeddings` interface free of `Data` namespace imports. The "disable Ollama" acceptance criterion is not achievable via `WebApplicationFactory<Program>` because the full app always starts and the warmup service connects to Ollama if running. |
| Confirmed by | user |
| Confirmation statement | "if you can, please handoff to spec writer" — spec writer revised the plan; implementer resumed after plan was updated with the confirmed approach |
| Date | 2026-06-04 |
