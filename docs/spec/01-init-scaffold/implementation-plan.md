# Implementation Plan: Initial Project Scaffold

This plan describes the steps that constitute the initial scaffold (already completed). It serves as a reference for the decisions made and as a reproducible setup guide.

## Prerequisites

- .NET 10 SDK (`global.json` pins `10.0.100`, `rollForward: latestFeature`)
- Docker Desktop (for Ollama on Windows via NVIDIA GPU)
- VS 2022 17.14+ or Rider (`.slnx` solution format support)
- Ollama models pulled: `ollama pull nomic-embed-text-v2-moe` (fallback: `ollama pull bge-m3`)

## Steps

### Step 1 — Solution and project structure

**What:** Create the `.slnx` solution with two projects: `src/VatVerifier.Api` (Minimal API) and `tests/VatVerifier.EvaluationTests` (xunit). Add `global.json` pinning .NET 10. Add a project reference from tests to API.  
**Files:** `VatVerifier.slnx`, `global.json`, `src/VatVerifier.Api/VatVerifier.Api.csproj`, `tests/VatVerifier.EvaluationTests/VatVerifier.EvaluationTests.csproj`  
**Accepts when:** `dotnet build` succeeds on both projects from the solution root.

### Step 2 — API packages and health endpoint

**What:** Add `Microsoft.Extensions.AI`, `OllamaSharp`, and `Microsoft.AspNetCore.OpenApi` to the API project. Scaffold `Program.cs` with `AddOpenApi()`, `MapOpenApi()`, and `GET /health`.  
**Files:** `src/VatVerifier.Api/VatVerifier.Api.csproj`, `src/VatVerifier.Api/Program.cs`  
**Accepts when:** `GET /health` returns `{"status":"ok"}`; OpenAPI endpoint responds in development mode.

### Step 3 — Evaluation contract types

**What:** Define `EvaluateInvoiceLineRequest`, `EvaluateInvoiceLineResponse`, `CategoryCandidateDto`, and all four status enums (`EvaluationSeverity`, `CategoryMatchStatus`, `VatValidationStatus`, `EvaluationReasonCode`). Types only — no logic.  
**Files:** `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineRequest.cs`, `src/VatVerifier.Api/Contracts/EvaluateInvoiceLineResponse.cs`  
**Accepts when:** All enum values match the decision model in `instructions/CLAUDE.md`; project compiles.

### Step 4 — IVatEvaluationEngine interface and stub

**What:** Define `IVatEvaluationEngine` with a single `EvaluateAsync` method. Implement `NotImplementedVatEvaluationEngine` returning `EngineNotImplemented` + `Alert`. Register as singleton in `Program.cs`. Map `POST /invoice-lines/evaluate`.  
**Files:** `src/VatVerifier.Api/Evaluation/IVatEvaluationEngine.cs`, `src/VatVerifier.Api/Evaluation/NotImplementedVatEvaluationEngine.cs`, `src/VatVerifier.Api/Program.cs`  
**Accepts when:** `POST /invoice-lines/evaluate` returns `200` with `reasonCode: "EngineNotImplemented"`.

### Step 5 — Configuration schema

**What:** Define `AiOptions` (provider name, Ollama endpoint, embedding model) and `EvaluationOptions` (four similarity thresholds). Register both via `Configure<T>` in `Program.cs`. Populate `appsettings.json` with defaults.  
**Files:** `src/VatVerifier.Api/Ai/AiOptions.cs`, `src/VatVerifier.Api/Evaluation/EvaluationOptions.cs`, `src/VatVerifier.Api/appsettings.json`, `src/VatVerifier.Api/Program.cs`  
**Accepts when:** `IOptions<AiOptions>` and `IOptions<EvaluationOptions>` can be injected and reflect `appsettings.json` values.

### Step 6 — Category seed data

**What:** Create `vat-categories.seed.json` with three initial categories. Each entry: `categoryId`, bilingual `name` and `description`, `expectedVatRate`, `positiveExamples`, `negativeExamples`, `typicalSuppliers`. Configure the test project to copy `Datasets/**/*.json` to the output directory.  
**Files:** `tests/VatVerifier.EvaluationTests/Datasets/vat-categories.seed.json`, `tests/VatVerifier.EvaluationTests/VatVerifier.EvaluationTests.csproj`  
**Accepts when:** All three categories have both `pl`/`en` fields and a numeric `expectedVatRate`; file is valid JSON.

### Step 7 — Golden dataset and test infrastructure

**What:** Create `invoice-line-evaluation-cases.json` with three golden cases: confident match (`Ok`), ambiguous item (`Alert / CategoryAmbiguousWithDifferentVatRates`), and VAT mismatch (`Critical / VatMismatch`). Add `EvaluationCase`, `ExpectedEvaluation` records and `DatasetLoader` using `System.Text.Json` with `JsonStringEnumConverter`.  
**Files:** `tests/VatVerifier.EvaluationTests/Datasets/invoice-line-evaluation-cases.json`, `tests/VatVerifier.EvaluationTests/Infrastructure/EvaluationCase.cs`, `tests/VatVerifier.EvaluationTests/Infrastructure/DatasetLoader.cs`  
**Accepts when:** `DatasetLoader.LoadAsync("invoice-line-evaluation-cases.json")` returns 3 cases in a test run.

### Step 8 — Integration tests (two-tier)

**What:** Add `VatEvaluationApiTests` using `IClassFixture<WebApplicationFactory<Program>>`. Tier 1 (`[Fact]`): asserts 2xx and `InvoiceLineId` echoed for every dataset case. Tier 2 (`[Fact(Skip = "Enable after the first real evaluation engine is implemented.")]`): asserts full expected `Severity`, `CategoryMatchStatus`, `VatValidationStatus`, `ReasonCode`.  
**Files:** `tests/VatVerifier.EvaluationTests/VatEvaluationApiTests.cs`  
**Accepts when:** Tier 1 passes with the stub engine; Tier 2 appears as skipped in test output.

### Step 9 — Docker Compose for Ollama

**What:** Add `docker/ollama/docker-compose.yml` with `ollama/ollama:latest`, port mapping `11434:11434`, named volume `ollama`, and NVIDIA GPU device reservation.  
**Files:** `docker/ollama/docker-compose.yml`  
**Accepts when:** `docker compose up` in `docker/ollama/` starts the container; `curl http://localhost:11434/api/tags` returns a valid JSON response.

## Notes

- The stub engine is intentional — it keeps `main` always buildable and Tier 1 always green before any AI logic exists.
- `IEmbeddingGenerator` is not yet registered in `Program.cs`. `OllamaSharp` is a package dependency but DI wiring is left for the next spec (real engine implementation).
- Mac users running Ollama natively (Metal) do not need Docker. Set `Ai__Ollama__Endpoint` to their local instance (`http://localhost:11434` by default).
- `[Fact(Skip = ...)]` is the only gating mechanism needed at this stage — no test categories, build flags, or environment variables required.
- `public partial class Program;` at the end of `Program.cs` is required for `WebApplicationFactory<Program>` to resolve the entry point from the test project.
