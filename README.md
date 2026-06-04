# VAT Verifier PoC

Small .NET 10 Minimal API PoC for automated Polish VAT invoice line evaluation.
The engine classifies invoice lines against a category catalogue, compares the declared VAT rate to the expected rate, and returns a structured verdict — not a single pass/fail flag.

## What it does

```
Invoice line (description, supplier, declared VAT rate)
        │
        ▼
Embedding model (Ollama / nomic-embed-text-v2-moe)
        │  cosine similarity against in-memory category embeddings
        ▼
Category match  ──────────────────────────────────────────────────┐
  Matched / Ambiguous / NotMatched                                │
        │                                                         │
        ▼                                                         │
VAT validation                                              Category candidates
  Match / Mismatch / Unknown                               (top-K with scores)
        │
        ▼
Evaluation verdict
  Severity  : Ok / Warning / Alert / Critical
  ReasonCode: stable enum for assertions and reporting
```

### Severity mapping

| Category match | VAT validation             | Severity   | ReasonCode                               |
| :------------- | :------------------------- | :--------- | :--------------------------------------- |
| `Matched`      | `Match`                    | `Ok`       | `VatMatched`                             |
| `Ambiguous`    | `Match` (consistent rates) | `Warning`  | `CategoryAmbiguousButVatConsistent`      |
| `Ambiguous`    | `Unknown` (mixed rates)    | `Alert`    | `CategoryAmbiguousWithDifferentVatRates` |
| `NotMatched`   | `Unknown`                  | `Alert`    | `CategoryNotMatched`                     |
| `Matched`      | `Mismatch`                 | `Critical` | `VatMismatch`                            |

## Requirements

- .NET 10 SDK
- [Ollama](https://ollama.com) — either via Docker (Windows/Linux) or native (macOS)
- Embedding model: `nomic-embed-text-v2-moe` (fallback: `bge-m3`)
- Docker with NVIDIA GPU support if running Ollama in a container on Windows

## Set up Ollama

**Windows / Linux (Docker):**

```bash
cd docker/ollama
docker compose up -d
./pull-models.ps1   # PowerShell
# or
./pull-models.sh    # bash / WSL
```

**macOS (native Ollama):**

```bash
ollama pull nomic-embed-text-v2-moe
```

The model is multilingual and handles mixed Polish/English invoice text.
Fallback if the primary model is unavailable:

```bash
ollama pull bge-m3
```

## Run the API

```bash
dotnet run --project src/VatVerifier.Api
```

Health check:

```bash
curl http://localhost:5000/health
```

Evaluate an invoice line:

```bash
curl -X POST http://localhost:5000/invoice-lines/evaluate \
  -H "Content-Type: application/json" \
  -d '{
    "invoiceLineId": "line-001",
    "description": "Usługa programistyczna maj 2026",
    "supplierName": "Example Software House Sp. z o.o.",
    "supplierIndustry": "IT services",
    "invoiceVatRate": 23
  }'
```

Sample response (once the evaluation engine is implemented):

```json
{
  "invoiceLineId": "line-001",
  "severity": "Ok",
  "categoryMatchStatus": "Matched",
  "vatValidationStatus": "Match",
  "invoiceVatRate": 23,
  "expectedVatRates": [23],
  "reasonCode": "VatMatched",
  "categoryCandidates": [
    { "categoryId": "software_it_services_23", "name": "Software / IT services", "score": 0.91, "expectedVatRate": 23 }
  ],
  "message": "VAT rate matches the expected rate for the matched category."
}
```

## Run tests

```bash
dotnet test
```

Tests run in two tiers:

| Tier | When it runs | What it asserts |
| :--- | :----------- | :-------------- |
| Structural | Always | API returns 2xx and echoes `InvoiceLineId` for every dataset case |
| AI / evaluation | Skipped by default | Full `Severity`, `CategoryMatchStatus`, `VatValidationStatus`, `ReasonCode` |

Enable the AI tests once the evaluation engine is implemented and Ollama is running (remove the `Skip` attribute in `VatEvaluationApiTests.cs`).

The critical test case that must never produce a false low-severity result: an invoice line whose category is confidently matched but whose declared VAT rate does not match the expected rate → `Critical / VatMismatch`.

## Configuration

`appsettings.json` exposes evaluation thresholds and AI provider settings:

```json
"Evaluation": {
  "StrongCandidateThreshold": 0.85,
  "AmbiguousCandidateThreshold": 0.75,
  "CandidateMarginThreshold": 0.10,
  "MaxCandidates": 5
},
"Ai": {
  "Provider": "Ollama",
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text-v2-moe"
  }
}
```

## Swapping AI providers

The application uses `Microsoft.Extensions.AI` abstractions throughout.
Provider registration lives only in `Program.cs`. Switching from Ollama to OpenAI or another provider requires changing that one file — all evaluation logic is provider-neutral.

## Project layout

```
src/
  VatVerifier.Api/
    Program.cs                        # DI, route mapping
    Contracts/                        # Request / response records, enums
    Evaluation/                       # IVatEvaluationEngine + stub implementation
    Ai/                               # AiOptions, embedding wiring notes
tests/
  VatVerifier.EvaluationTests/
    Datasets/
      vat-categories.seed.json        # Category catalogue (3 seed entries)
      invoice-line-evaluation-cases.json  # Golden test cases
docker/
  ollama/                             # Docker Compose for Ollama (NVIDIA/CUDA)
```

## AI coding assistants

This repository is set up for use with either Claude Code or GitHub Copilot.

### Claude Code

Custom commands are available in `.claude/commands/`:

| Command | Purpose |
| :------ | :------ |
| `implement-vat-evaluator` | Implements the real evaluation engine (embeddings + cosine similarity) |
| `rag-spec-writer` | Drafts a RAG architecture spec |
| `rag-implementer` | Executes an implementation plan step by step |
| `rag-reviewer` | Reviews a completed RAG implementation |
| `rag-test-data-writer` | Generates additional test cases for the golden dataset |

Run a command:

```text
/implement-vat-evaluator
```

### GitHub Copilot

Open the repository in VS Code or a JetBrains IDE with the Copilot extension enabled.
The `CLAUDE.md` file at the root documents scope, constraints, and the decision model — paste its contents into a Copilot Chat session as context when starting a new task:

```
@workspace /explain
```

Or attach `CLAUDE.md` directly to a chat message.

Key constraints to include in any Copilot prompt for this repo:
- .NET 10 Minimal API, no Clean Architecture layers
- `Microsoft.Extensions.AI` abstractions, `OllamaSharp` as the local provider
- No `Microsoft.Extensions.AI.Ollama` (deprecated)
- Provider registration in `Program.cs` only — no provider types in business logic

## Next step

Implement the real evaluation engine:

```text
/implement-vat-evaluator
```

This wires `IEmbeddingGenerator<string, Embedding<float>>` to the in-memory category catalogue, performs cosine similarity search at request time, and maps results to the four-field verdict.
