# Review Report: VAT Verifier PoC — Initial Project Scaffold

## Meta

| Field | Value |
|:---|:---|
| Spec | `docs/spec/01-init-scaffold/spec.md` |
| Summary | `docs/spec/01-init-scaffold/implementation-summary.md` |
| Date | 2026-06-04 |
| Verdict | PASS |

## Verdict

All 13 spec components are present. Every architectural decision is either followed or correctly deferred (with spec acknowledgment). No CLAUDE.md constraints are violated. The one code quality warning from the first review (`AiOptions.cs` namespace mismatch) was resolved; the deviation log confirmation statement was updated to meet gate requirements. A transient removal of `public partial class Program;` was caught during this review pass and immediately reverted — the line is required for `WebApplicationFactory<Program>` to access the `Program` type from the test assembly.

## Spec compliance

| Component / Decision | Expected | Found | Status |
|:---|:---|:---|:---|
| `Program.cs` — DI, routes | `POST /invoice-lines/evaluate` + `GET /health` + engine singleton | Present and correct | ✓ Present |
| `EvaluateInvoiceLineRequest.cs` | Request record with 5 fields | Present | ✓ Present |
| `EvaluateInvoiceLineResponse.cs` + 4 enums | All 4 status enums; `CategoryCandidateDto` | Present | ✓ Present |
| `IVatEvaluationEngine.cs` | Single `EvaluateAsync` method | Present | ✓ Present |
| `NotImplementedVatEvaluationEngine.cs` | Returns `EngineNotImplemented` + `Alert` | Present | ✓ Present |
| `EvaluationOptions.cs` | 4 thresholds with documented defaults | Present; defaults match spec table | ✓ Present |
| `AiOptions.cs` | Namespace `VatVerifier.Api.Ai`; provider + endpoint + model | Present; namespace corrected in prior pass | ✓ Present |
| `VatEvaluationApiTests.cs` | Tier 1 `[Fact]` + Tier 2 `[Fact(Skip=...)]` | Both tiers present | ✓ Present |
| `EvaluationCase.cs` + `DatasetLoader.cs` | Test case model + JSON loader | Present | ✓ Present |
| `vat-categories.seed.json` | 3 categories; bilingual fields; positive/negative examples | Present | ✓ Present |
| `invoice-line-evaluation-cases.json` | 3 golden cases covering Ok / Alert / Critical | Present | ✓ Present |
| `docker-compose.yml` | Ollama with NVIDIA GPU reservation | Present | ✓ Present |
| Flat Minimal API — no layers/MediatR | No layered projects; no MediatR package | Confirmed | ✓ Followed |
| 4-enum response model | `EvaluationSeverity`, `CategoryMatchStatus`, `VatValidationStatus`, `EvaluationReasonCode` | All 4 present | ✓ Followed |
| Severity mapping | Matched+Match→Ok; Ambiguous/diff rates→Alert; Mismatch→Critical | Stub engine — deferred; spec Open Questions acknowledge | ~ Untestable (deferred) |
| MEA + OllamaSharp; no deprecated MEA.Ollama | `OllamaSharp` in csproj; deprecated package absent | Confirmed | ✓ Followed |
| `IEmbeddingGenerator` registered in `Program.cs` | DI registration | Not present — explicitly listed as spec Open Question | ~ Deferred (acceptable) |
| OllamaSharp not in business logic | No provider types outside composition root | Confirmed | ✓ Followed |
| Provider swap via DI root only | Registration in `Program.cs` only | Confirmed | ✓ Followed |
| `nomic-embed-text-v2-moe` in `appsettings.json` | At `Ai.Ollama.EmbeddingModel` | Present | ✓ Followed |
| Category data model (bilingual, examples, suppliers) | `pl`/`en` fields; `positiveExamples`; `negativeExamples`; `typicalSuppliers` | Present in seed file | ✓ Followed |
| Two-tier tests with `WebApplicationFactory<Program>` | `IClassFixture<WebApplicationFactory<Program>>`; `public partial class Program;` | Both present | ✓ Followed |

## Constraint checks

| Constraint | Result | Notes |
|:---|:---|:---|
| No Semantic Kernel | ✓ Pass | Absent from all `.csproj` and `using` statements |
| No direct Ollama HTTP calls | ✓ Pass | Port `11434` appears only in `appsettings.json` as a config value |
| No `OllamaApiClient` in business logic | ✓ Pass | `OllamaSharp` is a package-only dependency; not instantiated in any service class |
| No database packages | ✓ Pass | |
| No auth packages | ✓ Pass | |
| No MediatR / CQRS | ✓ Pass | |
| No Clean Architecture layer projects | ✓ Pass | Only two projects; neither uses `.Domain`/`.Application`/`.Infrastructure` naming |

## Deviation log audit

| Deviation # | Confirmed by | Statement valid | Status |
|:---|:---|:---|:---|
| 1 — Missing `using Xunit;` compile fix | user | Yes — statement names the specific change: add `using Xunit;` to resolve CS0246 on `IClassFixture<>` and `[Fact]` | ✓ Valid |

## Code quality findings

| File | Finding | Severity |
|:---|:---|:---|
| `src/VatVerifier.Api/Program.cs` lines 11–12 | Guidance comment `// Starting point only. / // Next step: register a real category matcher...` — remove once `IEmbeddingGenerator` is registered in the next spec | INFO |

## Findings summary

| Severity | Count |
|:---|:---|
| CRITICAL | 0 |
| WARNING | 0 |
| INFO | 1 |

## Recommended follow-up

- **`Program.cs` comment** — remove the guidance comment once `IEmbeddingGenerator` is registered in the next spec.
- **`public partial class Program;`** — keep this line. It makes the implicit top-level `Program` class public so the test assembly can reference it via `WebApplicationFactory<Program>`. Removing it breaks test compilation. An `<InternalsVisibleTo>` entry in the API `.csproj` is the only alternative.
