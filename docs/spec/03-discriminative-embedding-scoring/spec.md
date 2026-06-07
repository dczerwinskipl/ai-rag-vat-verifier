# Discriminative Embedding Scoring

## Goal

Replace the current single-blended-vector category classifier with a three-part scoring upgrade: separate positive and negative centroids per category with a penalty scoring formula, a two-channel Weighted Reciprocal Rank Fusion (wRRF) that combines a description channel with a supplier/industry channel, and recalibrated classification thresholds. The change resolves 8 confirmed failing integration test cases across four failure clusters: negative example semantic pollution in the category embedding, supplier context contamination of the query vector, close-cluster food category confusion, and insufficient ambiguity detection for rate-identical service categories.

## Constraints

- .NET 10 Minimal API — no layered architecture, no MediatR, no CQRS
- Embeddings via Ollama + OllamaSharp; `IEmbeddingGenerator<string, Embedding<float>>` abstraction must be preserved throughout
- In-memory category store — no external vector database
- Integration tests call the API directly without mocking embeddings
- AI/evaluation tests are gated behind `[Trait("Category", "AI")]`; no new test infrastructure changes required
- All 24 currently passing AI test cases must remain green after changes

## Root cause analysis

Four distinct failure clusters explain the 8 failing tests:

**Cluster A — Negative example semantic pollution (cases 010, 011, 018, 030)**

`BuildCategoryText` currently concatenates negative examples as `"Not this category: ..."` into the same text that is embedded as the category vector. Embedding models do not interpret natural-language negation — the text `"Not this category: Gaz techniczny argon"` is semantically indistinguishable from just `"Gaz techniczny argon"`. The effect is the opposite of intent: negative examples pull the category vector toward those items. Concretely:

- `fuel_23` absorbs `"Gaz techniczny argon"` → argon queries score high on fuel → case-010 becomes Ambiguous instead of NotMatched
- `books_5` absorbs `"Wódka Chopin 0,7l"` → vodka queries score high on books → case-011 becomes Ambiguous instead of Matched/alcohol
- `food_basic_5` absorbs `"Wino czerwone Merlot"` → wine queries score high on food → case-018 may become Ambiguous instead of Matched/alcohol
- `digital_publications_5` absorbs `"Wkładka reklamowa do gazety"` → advertising insert scores high on publications → case-030 lands Matched/publications instead of Ambiguous

**Cluster B — Supplier context contamination (case 011)**

`BuildQueryText` concatenates description + supplier name + industry into one string. For `"Wódka Chopin 0,7l | Empik SA | bookstore"`, the `"Empik SA | bookstore"` suffix pulls the query vector toward `books_5` whose description and typical suppliers are semantically close to that text. The description signal is clear but is drowned out.

**Cluster C — Close-cluster food category confusion (cases 017, 019, 020)**

`food_basic_5` and `food_beverages_8` are semantically very similar. With the current blended vector approach, both categories occupy overlapping embedding space. Unambiguous products like `"Mleko UHT 3,2% 1L"` (an explicit positive in `food_basic_5`) do not consistently reach `StrongCandidateThreshold = 0.85`, landing as Ambiguous (→ Alert) instead of Matched (→ Critical on VAT mismatch).

**Cluster D — Rate-identical ambiguity not surfacing (case 009)**

`"Doradztwo i konsultacje biznesowe"` should score above `AmbiguousCandidateThreshold` for both `software_it_services_23` and `consulting_legal_23` with a margin under `CandidateMarginThreshold`. The blended-vector noise shifts the relative scores enough that one category wins by sufficient margin, and the test gets Matched instead of Ambiguous → Warning.

## Chosen architecture

Scoring for a category `c` given a request becomes a two-channel, three-step operation:

```
pos_sim(q, c)      = cosim(embed(description), c.PositiveVector) − α × cosim(embed(description), c.NegativeVector)
supplier_sim(q, c) = cosim(embed(supplierName + " | " + supplierIndustry), c.SupplierVector)

desc_rank[c]     = rank of c by pos_sim descending
supplier_rank[c] = rank of c by supplier_sim descending

final_score(c)   = w_desc × (1 / (k + desc_rank[c])) + w_supplier × (1 / (k + supplier_rank[c]))
```

Candidates are sorted by `final_score` and passed to `CosineSimilarityClassifier` with `pos_sim` as the confidence score (preserving absolute threshold semantics). `final_score` determines ordering; `pos_sim` determines the Matched / Ambiguous / NotMatched boundary.

### Negative example handling: Separate centroid + penalty score

Negative examples are embedded into their own mean vector (`NegativeVector`) in a separate batch call during warmup. They are completely removed from the positive embedding text. The penalty `α × neg_sim` is subtracted from `pos_sim` before ranking.

This directly eliminates Cluster A failures. Categories with no negative examples use `neg_sim = 0` (backward compatible). Penalty weight `α` is exposed in `EvaluationOptions` as `NegativePenaltyWeight` (default `0.30`).

### Category vector construction: Positive centroid

The positive centroid is the mean embedding of (a) the category description text and (b) all positive examples, submitted as a single batch to `IEmbeddingGenerator`. Negative examples and typical suppliers are submitted in separate batch calls and stored as `NegativeVector` and `SupplierVector` in `StoredCategory`.

The positive centroid accurately reflects the category's intended semantic space without boundary-item pollution.

### Query composition: Weighted Reciprocal Rank Fusion

Two independent embeddings are generated per request:

- **Description channel**: embeds `request.Description` only. `UnitOfMeasure` is dropped (adds noise without discriminative value).
- **Supplier channel**: embeds `"<SupplierName> | <SupplierIndustry>"`. Falls back to last rank for all categories when both fields are absent.

Each channel produces an independent ranked list. wRRF fusion:

```
final_score(c) = 0.70 × (1 / (60 + desc_rank[c])) + 0.30 × (1 / (60 + supplier_rank[c]))
```

The description channel carries 70% of the signal weight, ensuring that a decisive product description (e.g., `"Wódka Chopin 0,7l"`) dominates even when the supplier context is misleading (bookstore). The supplier channel provides a principled tiebreaker for genuinely ambiguous descriptions. Both weights and `RrfK` are configurable in `EvaluationOptions`.

### Classification threshold calibration: Empirical re-calibration after scoring changes

Current thresholds (`StrongCandidateThreshold = 0.85`, `AmbiguousCandidateThreshold = 0.75`, `CandidateMarginThreshold = 0.10`) were implicitly tuned against the broken blended-vector scoring. After implementing the scoring changes, a single calibration pass against the full AI test suite is required to set appropriate values. The classifier continues to use `pos_sim` (not the wRRF score) as the confidence value for threshold comparison, so the threshold scale remains in the `[0, 1]` cosine similarity range.

## Components

Components to add or modify:

| Component | Change |
|:---|:---|
| `StoredCategory` record | Add `NegativeVector` (`float[]`) and `SupplierVector` (`float[]`); rename `Vector` → `PositiveVector` |
| `ICategoryEmbeddingStore` | Update `Store()` signature to accept all three vectors |
| `InMemoryCategoryEmbeddingStore` | Implement updated `Store()` |
| `CategoryEmbeddingWarmupService` | Three separate batch `GenerateAsync` calls per startup (positives, negatives, suppliers) |
| `EmbeddingClassificationStep` | Two `GenerateAsync` calls per request; negative penalty; wRRF fusion; pass `pos_sim` as score to classifier |
| `EvaluationOptions` | Add `NegativePenaltyWeight` (0.30), `DescriptionChannelWeight` (0.70), `SupplierChannelWeight` (0.30), `RrfK` (60) |
| `GtuFastPathStep` | No changes — bypasses embedding entirely |
| `CosineSimilarityClassifier` | No API changes — receives pre-sorted candidates with `pos_sim` score values |

## Alternatives considered

| Option | Reason not chosen |
|:---|:---|
| Strip negatives entirely (no penalty vector) | Loses boundary signal; `argon` still shares industrial vocabulary with `fuel_23` and would score above Ambiguous threshold |
| Per-example negative hard gate | Binary cliff; risks false zeroing on partial negative overlaps with legitimate matches |
| Description-only query (no supplier channel) | Loses supplier tiebreaker for genuinely ambiguous descriptions where supplier industry is decisive |
| Per-example max-pooling for positives | Higher memory and compute per category; useful future upgrade but over-engineered for current PoC |
| Per-category thresholds | Brittle maintenance — every new category requires manual threshold setting |
| Symmetric (unweighted) RRF | Description and supplier ranked equally; for supplier-item mismatches (bookstore + vodka), both channels cancel each other rather than letting description dominate |

## Open questions

- `NegativePenaltyWeight = 0.30` and `DescriptionChannelWeight = 0.70` are calibrated starting estimates. Both must be validated by running the full AI test suite after implementation and inspecting score distributions (Step 6 of the implementation plan).
- If `SupplierName` and `SupplierIndustry` are both null/empty, the supplier channel assigns last rank to all categories. This is conservative and prevents absent context from distorting the fusion.
- `UnitOfMeasure` is dropped from both channels. If edge cases emerge where unit signals category (e.g., `"l."` for fuel), it can be added to the supplier channel in a follow-up.
