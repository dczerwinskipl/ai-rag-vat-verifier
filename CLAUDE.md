# Claude Code instructions

## Goal

Build a small .NET 10 Minimal API PoC for VAT invoice line evaluation.

This is not a product and not a Clean Architecture exercise. Keep the structure simple.

## Current scope

Implement only the evaluation starting point:

- API endpoint: `POST /invoice-lines/evaluate`
- JSON datasets for categories and evaluation cases
- integration tests through the API
- local embeddings through Ollama
- AI abstraction through `Microsoft.Extensions.AI`

## Do not add yet

- authentication
- authorization
- database
- vector database
- background jobs
- UI
- CQRS
- MediatR
- Clean Architecture layers
- production-grade observability
- Semantic Kernel
- agents

## Decision model

Do not return only `PASS` / `ALERT`.

Return separate statuses:

- `CategoryMatchStatus`: `Matched`, `Ambiguous`, `NotMatched`
- `VatValidationStatus`: `Match`, `Mismatch`, `Unknown`
- `EvaluationSeverity`: `Ok`, `Warning`, `Alert`, `Critical`
- `EvaluationReasonCode`: stable reason code for tests and reporting

Basic intended mapping:

- confident category + VAT matches: `Ok`
- ambiguous categories with same VAT and invoice VAT matches: `Warning`
- ambiguous categories with different VAT rates: `Alert`
- confident category + VAT mismatch: `Critical`
- no category match: `Alert`

## Category classification

Input side:

- invoice line description
- supplier name
- supplier industry
- invoice VAT rate

Reference side:

- categories from `vat-categories.seed.json`
- each category has PL/EN name and description
- examples should include positive and negative examples
- category maps to expected VAT rate

First implementation should be in-memory.

## Embeddings

Use local Ollama as the first provider.

Preferred model:

```text
qwen3-embedding:0.6b
```

Fallback (requires Ollama 0.5+, works without the 0.6 upgrade):

```text
nomic-embed-text-v2-moe
```

Use `Microsoft.Extensions.AI` abstractions where practical. Use `OllamaSharp` as the concrete local provider. Do not use deprecated `Microsoft.Extensions.AI.Ollama`.

## Test strategy

The integration tests load JSON cases and call the API.

Keep two modes:

- regular deterministic tests that always run
- AI/evaluation tests that can be enabled after the engine is implemented and Ollama is running

The most important failure is a false OK/false low severity when VAT is actually mismatched.
