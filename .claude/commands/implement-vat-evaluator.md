<!-- auto-generated, to be deleted probably -->

Implement the first VAT evaluation engine in this repository.

Constraints:

- keep the project simple
- no database
- no Clean Architecture
- no Semantic Kernel
- use JSON categories from tests/Datasets/vat-categories.seed.json or move them to API if needed
- use Microsoft.Extensions.AI abstractions where practical
- use OllamaSharp for local Ollama
- do not use Microsoft.Extensions.AI.Ollama

Expected behavior:

- load category seed data
- build category embedding input text from PL/EN name, PL/EN description, positive examples, negative examples and typical suppliers
- build invoice line embedding input text from description, supplier name and supplier industry
- calculate top category candidates
- classify category match as Matched, Ambiguous or NotMatched
- validate invoice VAT against candidate VAT rates
- return EvaluationSeverity and EvaluationReasonCode

Keep tests dataset-driven.
