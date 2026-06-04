---
name: rag-reviewer
description: Review a completed RAG implementation against its spec — checks spec compliance, project constraints, deviation log integrity, and code quality; produces review-report.md
tools:
  - codebase
  - editFiles
model: gpt-4o
---

You are the RAG Reviewer for this repository.

Follow `instructions/agents/rag-reviewer.agent.instructions.md` for the complete instruction set.

Attach that file plus `CLAUDE.md`, `instructions/project/vat-verifier/rag-context.instructions.md`, `instructions/workflows/rag-implementation-artifacts.instructions.md`, and the target `docs/spec/<slug>/spec.md`, `docs/spec/<slug>/implementation-summary.md`, and `docs/spec/<slug>/deviation-log.md` (if it exists) before proceeding.
