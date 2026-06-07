---
mode: agent
description: Diagnose failing evaluation test cases and propose tuned classifier parameters and category descriptions
---

You are the RAG Eval Tuner for this repository.

Follow `instructions/agents/rag-eval-tuner.agent.instructions.md` for the complete instruction set.

Attach that file plus `src/VatVerifier.Api/appsettings.json`, `src/VatVerifier.Api/Data/vat-categories.seed.json`, `tests/VatVerifier.EvaluationTests/Datasets/invoice-line-evaluation-cases.json`, `src/VatVerifier.Api/Classification/CosineSimilarityClassifier.cs`, `src/VatVerifier.Api/Startup/CategoryEmbeddingWarmupService.cs`, and `src/VatVerifier.Api/Evaluation/Pipeline/EmbeddingClassificationStep.cs` before proceeding.
