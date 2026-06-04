# Architecture Diagrams — Initial Scaffold

## Current state: stub pipeline

The API is wired but the evaluation engine is not implemented. Every request returns `EngineNotImplemented`.

```mermaid
flowchart LR
    Client["HTTP Client"] --> API[".NET 10 Minimal API\nPOST /invoice-lines/evaluate"]
    API --> Engine["IVatEvaluationEngine\n(NotImplementedVatEvaluationEngine)"]
    Engine --> Response["EvaluateInvoiceLineResponse\nSeverity: Alert\nReasonCode: EngineNotImplemented"]
    Response --> Client
```

## Intended target state: in-memory embedding pipeline

The intended next step wires a real engine backed by in-memory category embeddings. This is the pipeline the seed data, thresholds, and interface are designed for.

```mermaid
flowchart LR
    Client["HTTP Client"] --> API[".NET 10 Minimal API\nPOST /invoice-lines/evaluate"]
    API --> Engine["VatEvaluationEngine\nimplements IVatEvaluationEngine"]
    Engine --> Embed["IEmbeddingGenerator\n(OllamaSharp → Ollama)"]
    Embed --> OllamaEmbed["Ollama\nlocalhost:11434\nnomic-embed-text-v2-moe"]
    Engine --> Search["In-memory cosine search\nTop-K candidates"]
    Search --> Classify["Severity classification\n(EvaluationOptions thresholds)"]
    Classify --> Response["EvaluateInvoiceLineResponse\n(real severity + reason code)"]
    Response --> Client
```

## Component diagram

```mermaid
graph TD
    API[".NET 10 Minimal API\nProgram.cs"] --> Engine["IVatEvaluationEngine"]
    API --> EmbedSvc["IEmbeddingGenerator\n(OllamaSharp)"]
    EmbedSvc --> Ollama["Ollama\nlocalhost:11434\n(Docker Compose)"]
    Ollama --> EmbedModel["nomic-embed-text-v2-moe\n(fallback: bge-m3)"]
    Engine --> EmbedSvc
    Engine --> VectorStore["In-memory vector store\n(Microsoft.Extensions.AI.VectorData)"]
    VectorStore --> Categories["vat-categories.seed.json\n(loaded at startup)"]
    Tests["VatVerifier.EvaluationTests\n(WebApplicationFactory)"] --> API
    Tests --> Dataset["invoice-line-evaluation-cases.json"]
```

## Severity decision flow

```mermaid
flowchart TD
    Start["Top-K candidates scored"] --> CheckMatch{"Top score ≥ 0.85\nAND margin ≥ 0.10?"}
    CheckMatch -->|Yes| Matched["CategoryMatchStatus: Matched"]
    CheckMatch -->|No| CheckAmbiguous{"Any score ≥ 0.75?"}
    CheckAmbiguous -->|No| NotMatched["CategoryMatchStatus: NotMatched\n→ Alert / CategoryNotMatched"]
    CheckAmbiguous -->|Yes| Ambiguous["CategoryMatchStatus: Ambiguous"]
    Matched --> CheckVat{"Invoice VAT =\nexpected VAT?"}
    CheckVat -->|Yes| Ok["Severity: Ok\nVatValidationStatus: Match\nReasonCode: VatMatched"]
    CheckVat -->|No| Critical["Severity: Critical\nVatValidationStatus: Mismatch\nReasonCode: VatMismatch"]
    Ambiguous --> CheckConsistent{"All candidate\nVAT rates equal?"}
    CheckConsistent -->|Yes| CheckVatConsistent{"Invoice VAT matches\nconsistent rate?"}
    CheckVatConsistent -->|Yes| Warning["Severity: Warning\nVatValidationStatus: Match\nReasonCode: CategoryAmbiguousButVatConsistent"]
    CheckVatConsistent -->|No| AlertConsistent["Severity: Alert\nVatValidationStatus: Mismatch\nReasonCode: CategoryAmbiguousButVatConsistent"]
    CheckConsistent -->|No| AlertDiff["Severity: Alert\nVatValidationStatus: Unknown\nReasonCode: CategoryAmbiguousWithDifferentVatRates"]
```
