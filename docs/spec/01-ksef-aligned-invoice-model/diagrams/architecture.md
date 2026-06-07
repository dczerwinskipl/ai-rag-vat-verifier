# Architecture Diagrams — KSeF-Aligned Invoice Model

## Classification pipeline flowchart

Shows the evaluation path for a single invoice line. The GTU fast-path short-circuits embedding when a GTU code is present and matches a seed entry. Structural checks run independently of category matching and can produce `Critical` results before embedding is invoked.

```mermaid
flowchart TD
    Request["EvaluateInvoiceLineRequest\n(description, supplierName, supplierIndustry?,\ninvoiceVatRate, gtuCode?, unitOfMeasure?,\nreverseChargeApplied?, splitPaymentRequired?)"]

    Request --> StructCheck["Structural Checks\n(reverse charge consistency,\nsplit payment threshold)"]
    StructCheck -->|"Violation found\n(P_18 / P_18A mismatch)"| EarlyResult["Critical result\nReverseChargeMissing /\nReverseChargeUnexpected"]

    StructCheck -->|"No violation"| GtuCheck{"GTU code\nprovided?"}

    GtuCheck -->|"Yes"| GtuLookup["GTU Fast Lookup\n(seed.gtuCodes[])"]
    GtuLookup -->|"Seed entry found"| GtuVatCheck["VAT Validation\n(invoiceRate vs expectedRate\nor rateVariants)"]
    GtuLookup -->|"No seed entry\nfor this GTU"| Embed

    GtuCheck -->|"No"| Embed["Embed Description\n(Ollama: qwen3-embedding:0.6b\nvia IEmbeddingGenerator)"]
    Embed --> Similarity["Cosine Similarity\nagainst ICategoryEmbeddingStore\n(in-memory)"]
    Similarity --> TopK["Top-K Candidates\n(threshold: 0.75)"]
    TopK --> CategoryDecision{"Confident\nmatch?"}
    CategoryDecision -->|"Single above threshold"| MatchedVatCheck["VAT Validation\n(invoiceRate vs expectedRate)"]
    CategoryDecision -->|"Multiple above threshold\nor scores close"| AmbiguousCheck["Ambiguous —\ncheck if all candidates\nshare same VAT rate"]
    CategoryDecision -->|"None above threshold"| NotMatched["CategoryNotMatched\n→ Alert"]

    GtuVatCheck --> SeverityMap
    MatchedVatCheck --> SeverityMap
    AmbiguousCheck --> SeverityMap

    SeverityMap["Severity Mapping\n(Ok / Warning / Alert / Critical)\n+ ReasonCode assignment"]

    SeverityMap --> ConfidenceGate{"Score below\nthreshold or\nAmbiguous?"}
    ConfidenceGate -->|"Yes"| ConfLog["Append to\nlow-confidence-evaluations.json\n(eval-tuner feed)"]
    ConfidenceGate -->|"No"| Response
    ConfLog --> Response["EvaluateInvoiceLineResponse"]
    EarlyResult --> Response
    NotMatched --> Response
```

## Component diagram

Shows the .NET components, Ollama endpoints, and the confidence log consumed by the eval-tuner agent.

```mermaid
graph TD
    API[".NET 10 Minimal API\nPOST /invoice-lines/evaluate"]

    API --> Engine["EmbeddingVatEvaluationEngine\n(IVatEvaluationEngine)"]

    Engine --> StructChecker["Structural Checker\n(reverse charge, split payment)"]
    Engine --> GtuResolver["GTU Fast-Path Resolver\n(seed.gtuCodes lookup)"]
    Engine --> Classifier["CosineSimilarityClassifier\n(ICategoryClassifier)"]
    Engine --> SeverityMapper["Severity + ReasonCode Mapper"]
    Engine --> ConfTracker["Confidence Tracker\n(low-confidence-evaluations.json)"]

    Classifier --> EmbedGen["IEmbeddingGenerator\n(OllamaSharp adapter)"]
    Classifier --> EmbedStore["ICategoryEmbeddingStore\n(InMemoryCategoryEmbeddingStore)"]

    EmbedGen --> Ollama["Ollama\nlocalhost:11434"]
    Ollama --> EmbedModel["qwen3-embedding:0.6b\n(fallback: nomic-embed-text-v2-moe)"]

    EmbedStore --> SeedData["CategorySeedEntry[]\n(vat-categories.seed.json)\nenriched: gtuCodes, rateVariants"]

    Warmup["CategoryEmbeddingWarmupService\n(IHostedService)"] --> EmbedGen
    Warmup --> EmbedStore

    ConfTracker --> ConfLog[("low-confidence-evaluations.json\n(append-only)")]
    ConfLog --> EvalTuner["eval-tuner agent\n(external — reads log,\nproposes seed patches)"]
```

## Sequence diagram — GTU fast-path evaluation

Illustrates the happy path where a GTU code is present, bypassing embedding entirely.

```mermaid
sequenceDiagram
    participant Client
    participant API as Minimal API
    participant Engine as EmbeddingVatEvaluationEngine
    participant Struct as Structural Checker
    participant GTU as GTU Fast-Path
    participant Seed as CategorySeedEntry[]
    participant Severity as Severity Mapper

    Client->>API: POST /invoice-lines/evaluate\n{ gtuCode: "GTU_01", invoiceVatRate: 8 }
    API->>Engine: EvaluateAsync(request)

    Engine->>Struct: Check P_18 / P_18A flags
    Struct-->>Engine: No violation

    Engine->>GTU: Resolve gtuCode "GTU_01"
    GTU->>Seed: Find entry where gtuCodes contains "GTU_01"
    Seed-->>GTU: alcohol_spirits_23 (expectedVatRate: 23)
    GTU-->>Engine: Matched: alcohol_spirits_23

    Engine->>Severity: invoiceRate=8 vs expectedRate=23
    Severity-->>Engine: VatValidationStatus=Mismatch\nSeverity=Critical\nReasonCode=VatMismatch

    Engine-->>API: EvaluateInvoiceLineResponse
    API-->>Client: 200 OK { severity: "Critical", reasonCode: "VatMismatch", ... }
```
