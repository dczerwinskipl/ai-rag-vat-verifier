# Deviation Log: Discriminative Embedding Scoring

## Deviation 1

| Field | Value |
|:---|:---|
| Step | Step 3 — Update `CategoryEmbeddingWarmupService` |
| Gate triggered | Spec Validation Gate |
| Original plan said | "Use an empty-string placeholder for categories with no negatives; the resulting zero-ish vector will produce neg_sim ≈ 0 (no penalty)." |
| Implemented instead | `Array.Empty<float>()` is stored as the `NegativeVector` sentinel. `CategoryEmbeddingWarmupService.BuildNegativeVectorsAsync` skips the `GenerateAsync` call entirely for categories with no negative examples and stores an empty array directly. The scoring step guards with `NegativeVector.Length == 0` and skips the penalty. |
| Reason | `TensorPrimitives.CosineSimilarity<float>` throws `ArgumentException ("Input span arguments must not be empty")` when given a 0-length span. Generating an embedding for an empty string does not reliably produce a zero vector — it depends on the model tokeniser. The sentinel approach is deterministic, avoids the unnecessary Ollama round-trip, and produces exactly the intended behaviour (no penalty) without relying on model-specific numerics. |
| Confirmed by | user |
| Confirmation statement | "GO with your recommendations" (approved implementation approach at plan acceptance stage, 2026-06-05) |
| Date | 2026-06-05 |
