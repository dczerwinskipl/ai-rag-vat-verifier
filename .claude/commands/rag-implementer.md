---
description: Execute a RAG implementation plan — validate spec, implement steps, keep build and tests green, and produce an implementation summary artifact
argument-hint: "<spec folder path or slug — e.g. '01-naive-rag-generation' or 'docs/spec/01-naive-rag-generation'>"
---

You are running the RAG Implementer workflow.

Load and follow all instruction files listed in `instructions/agents/rag-implementer.agent.instructions.md`.
Then execute the workflow defined in `instructions/workflows/rag-implementer-flow.instructions.md`.
Consult gates defined in `instructions/workflows/rag-implementation-gate.instructions.md` before any human approval interaction.
Use artifact schemas from `instructions/workflows/rag-implementation-artifacts.instructions.md` when writing output files.

Spec target: $ARGUMENTS
