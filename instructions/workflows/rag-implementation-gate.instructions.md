---
name: rag-implementation-gate
description: Behavioral boundaries for the RAG Implementer — defines the four human approval gates triggered by spec issues, deviations, build failures, and review findings
metadata:
  type: guardrail
---

# RAG Implementer — Gates

## Gate 1: Spec Validation Gate (CRITICAL)

**Triggers from:** Workflow Step 2

**Trigger condition:** One or more `BLOCKING` or `AMBIGUOUS` issues found in `implementation-plan.md` during validation.

**Hard stop:** Do not write any code until the user has chosen an option.

### Required action

Present findings in this format:

```
## Spec validation issues

### Issue [N] — [BLOCKING | AMBIGUOUS]
**Step affected:** Step [number] — [step title]
**Problem:** [what exactly is wrong or unclear]
**Impact:** [what cannot be done as written]
```

Then present the following options — the user MUST choose one per issue:

**Option A — Handoff to spec writer**
Stop now. Do not write any code. Run `/rag-spec-writer` with the issue described to revise the spec. Rerun the implementer once the spec is fixed.

**Option B — Skip this step**
Mark the step as `Skipped (spec issue)` in the implementation summary. Continue with remaining steps. The skipped step will appear as an open item in the review.

**Option C — Implement with documented deviation**
The agent will implement its best interpretation. You must explicitly confirm what will be done differently. Your confirmation statement becomes the deviation record — a generic "yes" is not accepted.

To confirm: state the following in your reply:
> "Confirmed: for step [N], instead of [original plan], implement [your approved approach]. Reason: [why]."

The agent will record this verbatim in `deviation-log.md` before writing code.

**The user may choose different options for different issues.** Do not proceed until every `BLOCKING` and `AMBIGUOUS` issue has an assigned option.

---

## Gate 2: Deviation Confirmation Gate (CRITICAL)

**Triggers from:** Workflow Step 4e

**Trigger condition:** During implementation, the agent realizes the plan is wrong, incomplete, or technically incorrect — and continuing faithfully to the plan would produce broken code, a build failure, or a materially wrong outcome.

**Hard stop:** Do not write the deviant code. Stop immediately and surface the issue.

### Required action

Present the situation in this format:

```
## Deviation required — Step [N]: [step title]

**What the plan says:** [quote or close summary of the plan instruction]
**Why it cannot be followed:** [specific technical reason — missing dependency, wrong type, API mismatch, etc.]
**What I propose instead:** [the agent's specific alternative approach]
**Scope of change:** [files affected, interfaces changed, behavior difference]
```

Then ask for explicit confirmation:

> "To proceed with this change, reply with: 'Confirmed: [brief restatement of the approved approach].' I will record this in deviation-log.md before continuing."

**A generic "yes", "ok", or "go ahead" is not accepted.** The confirmation must name the change. This is required because the review step checks deviation-log.md for a signed-off entry.

If the user rejects the proposal:
- Option A: rollback any partial changes, mark step as skipped
- Option B: provide alternative approach — agent reassesses and re-presents before writing

---

## Gate 3: Build Failure Gate (CRITICAL)

**Triggers from:** Workflow Step 4c

**Trigger condition:** `dotnet build` fails after a step's changes, and the agent cannot resolve the error within 2 self-directed attempts.

**Hard stop:** Do not continue to the next step with a broken build.

### Required action

Present:

```
## Build failure — Step [N]: [step title]

**Error:** [dotnet build error output, trimmed to relevant lines]
**Changes made in this step:** [list of files modified]
**Fix attempts:** [what was tried and why it didn't resolve the issue]
```

Options for the user:

**Option A — Rollback this step**
Revert all changes from this step. Mark step as `Failed` in the summary. Continue with the next step if possible.

**Option B — Debug together**
Pause and describe the error in detail. User guides the fix. Agent applies the fix, then re-runs build before continuing.

**Option C — Skip remaining steps**
Mark all remaining steps as `Skipped (build broken)`. Write the summary with current state and open the review gate.

---

## Gate 4: Review Gate (WARNING)

**Triggers from:** Workflow Step 8

**Trigger condition:** One or more inline review checks fail.

**Soft stop:** The agent presents findings. The user chooses whether to fix, accept, or defer — the agent records the decision.

### Review check failure responses

**Build fails:** treat as Gate 3 (CRITICAL). Do not mark as a warning.

**Tests fail:**
Present: which tests fail, whether they are regressions or new failures, and what caused them.
Options: A) fix now, B) accept with documented reason (added to summary as open item), C) escalate to spec writer if test failure reveals a spec error.

**Missing test coverage:**
Present: which steps have no tests.
Options: A) agent writes tests now, B) accept as known gap (added to summary as open item with reason).

**Unconfirmed deviation:**
This is CRITICAL — a `deviation-log.md` entry without a `Confirmed by` field means a change was made without approval. This MUST be resolved before the summary is marked complete:
- Either get user confirmation retroactively (user states confirmation, agent adds it to log)
- Or revert the deviant code

**Regression detected:**
Present: which previously-passing tests now fail.
This is CRITICAL. Do not finalize the summary until regressions are resolved or explicitly accepted.

### Review gate resolution

After the user responds to each finding:
- Record accepted exceptions in the summary under `## Outstanding issues`
- Update the review check table with final status
- If all critical items resolved: mark `Review: PASS`
- If any critical items accepted-with-exception: mark `Review: PASS_WITH_EXCEPTIONS`
- If any critical items unresolved: mark `Review: FAIL`
