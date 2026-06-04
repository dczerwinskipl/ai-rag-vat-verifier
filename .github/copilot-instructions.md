# VAT Verifier PoC — Workspace Instructions

This repository is a .NET 10 Minimal API PoC for Polish VAT invoice line evaluation.
These instructions are loaded automatically into every Copilot Chat session in this workspace.

## Project purpose

Classify invoice lines against a VAT category catalogue using local embeddings (Ollama),
compare the declared VAT rate to the expected rate, and return a structured verdict.

## Stack — what to use

- .NET 10 Minimal API — single project, flat folder structure
- `Microsoft.Extensions.AI` abstractions in all business logic: `IEmbeddingGenerator<string, Embedding<float>>`, `IChatClient`
- `OllamaSharp` as the concrete Ollama provider
- Provider registration in `Program.cs` only — no provider-specific types outside the composition root
- In-memory category store for the PoC — no persistent vector database

## Stack — what NOT to use

| Forbidden | Reason |
|:---|:---|
| `Microsoft.Extensions.AI.Ollama` package | Deprecated; `OllamaSharp` is the supported path |
| Semantic Kernel | Deferred — not in PoC scope |
| Entity Framework Core / Dapper / any SQL | No database in PoC scope |
| MediatR / IMediator / IRequest | No CQRS in PoC scope |
| Authentication / Authorization packages | No auth in PoC scope |
| Clean Architecture layer projects (`.Domain`, `.Application`, `.Infrastructure`) | Not needed at PoC scale |
| Background jobs | Out of scope |

## Evaluation response model

Return four separate status fields — never a single pass/fail flag:

| Field | Values |
|:---|:---|
| `EvaluationSeverity` | `Ok` / `Warning` / `Alert` / `Critical` |
| `CategoryMatchStatus` | `Matched` / `Ambiguous` / `NotMatched` |
| `VatValidationStatus` | `Match` / `Mismatch` / `Unknown` |
| `EvaluationReasonCode` | stable enum — `VatMatched`, `VatMismatch`, `CategoryNotMatched`, etc. |

## Agent workflows

Use the following prompt files for structured tasks:

| Prompt | Purpose |
|:---|:---|
| `/rag-spec-writer` | Research RAG options, produce spec + architecture diagram + implementation plan |
| `/rag-implementer` | Execute an implementation plan step by step |
| `/rag-reviewer` | Review a completed implementation against its spec |
| `/rag-test-data-writer` | Generate Polish VAT invoice test data |

Attach `CLAUDE.md` and relevant `instructions/` files for full workflow context.
