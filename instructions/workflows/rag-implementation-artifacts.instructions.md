---
name: rag-implementation-artifacts
description: Artifact schemas for the RAG Implementer — implementation-summary.md and deviation-log.md formats, field definitions, and status vocabulary
metadata:
  type: contract
---

# RAG Implementer — Artifact Schemas

## Artifact 1: `implementation-summary.md`

**Path:** `docs/spec/<slug>/implementation-summary.md`  
**When created:** At the end of every implementer run, regardless of outcome  
**Consumed by:** Review agents, the spec writer (for deviation feedback), and the user

### Schema

```markdown
# Implementation Summary: [human-readable feature title]

## Meta

| Field | Value |
|:---|:---|
| Spec | `docs/spec/<slug>/spec.md` |
| Plan | `docs/spec/<slug>/implementation-plan.md` |
| Date | YYYY-MM-DD |
| Status | Complete \| Partial \| Blocked |
| Review | PASS \| PASS_WITH_EXCEPTIONS \| FAIL \| Not run |

## Steps

| # | Title | Status | Files changed |
|:---|:---|:---|:---|
| 1 | [title from plan] | Done \| Skipped \| Failed | [comma-separated file list] |

## Files changed

[Bullet list of all created or modified files, relative to repo root]

## Test results

| Metric | Baseline | Final |
|:---|:---|:---|
| Build | PASS \| FAIL | PASS \| FAIL |
| Tests passed | N | N |
| Tests failed | N | N |
| Regressions | N | N |

## Test coverage

| Step # | Feature / behavior | Coverage |
|:---|:---|:---|
| 1 | [what was built] | Covered \| Partial \| Missing |

## Deviations

[None] or: See `deviation-log.md` — [N] deviations, all confirmed.

## Review checks

| Check | Result | Notes |
|:---|:---|:---|
| Build passes | ✓ \| ✗ | |
| All tests pass | ✓ \| ✗ | N passed, M failed |
| New features have tests | ✓ \| ✗ | |
| No unconfirmed deviations | ✓ \| ✗ | |
| No regressions | ✓ \| ✗ | |

## Outstanding issues

[None] or bullet list of accepted exceptions with reason.
Each entry: `- [check name]: [what was accepted] — accepted by user on [date]`
```

### Status vocabulary

| Status | Meaning |
|:---|:---|
| `Complete` | All plan steps executed; build passes; all tests pass; review is PASS or PASS_WITH_EXCEPTIONS |
| `Partial` | Some steps skipped or failed; remainder complete; build passes; review reflects gaps |
| `Blocked` | Implementation stopped before all steps due to unresolved spec issue, persistent build failure, or user instruction to halt |

### Review vocabulary

| Value | Meaning |
|:---|:---|
| `PASS` | All five review checks pass; no outstanding issues |
| `PASS_WITH_EXCEPTIONS` | One or more checks have accepted-with-reason exceptions; no critical items unresolved |
| `FAIL` | One or more critical review items unresolved |
| `Not run` | Implementation did not reach the review step |

---

## Artifact 2: `deviation-log.md`

**Path:** `docs/spec/<slug>/deviation-log.md`  
**When created:** Only when at least one deviation was confirmed during implementation  
**Required fields:** every entry MUST have `Confirmed by` before the summary can be marked `PASS` or `PASS_WITH_EXCEPTIONS`

### Schema

```markdown
# Deviation Log: [human-readable feature title]

## Deviation 1

| Field | Value |
|:---|:---|
| Step | [plan step number and title] |
| Gate triggered | Spec Validation Gate \| Deviation Confirmation Gate |
| Original plan said | [exact quote or close summary] |
| Implemented instead | [what was actually done] |
| Reason | [technical or spec-quality reason] |
| Confirmed by | user |
| Confirmation statement | "[verbatim user statement approving the change]" |
| Date | YYYY-MM-DD |

## Deviation N

[repeat block]
```

### Validation rules for review agents

A review agent checking this log MUST flag the following as CRITICAL:

- Any entry missing `Confirmed by`
- Any entry where `Confirmation statement` is empty or contains only a single word (e.g., "yes", "ok")
- An `implementation-summary.md` that references deviations but no `deviation-log.md` exists
- A `deviation-log.md` that exists but is not referenced in `implementation-summary.md`
