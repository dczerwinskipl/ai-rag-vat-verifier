---
name: rag-implementer-flow
description: Ordered workflow for the RAG Implementer — spec validation, step execution with build/test checks, deviation handling, and implementation summary with inline review
metadata:
  type: workflow
---

# RAG Implementer — Workflow

## Prerequisites

Before Step 1, confirm:
- `instructions/agents/rag-implementer.agent.instructions.md` loaded
- `instructions/workflows/rag-implementation-gate.instructions.md` loaded
- `instructions/workflows/rag-implementation-artifacts.instructions.md` loaded

---

## Step 1 — Locate spec folder

If `$ARGUMENTS` is non-empty: treat it as the spec folder path or slug.
- Full path: use directly
- Slug only: resolve to `docs/spec/<matching-slug>/`
- If multiple matches: list them and ask the user to choose

If `$ARGUMENTS` is empty: Glob `docs/spec/*/`, list results, ask user to select.

Read `spec.md` and `implementation-plan.md` from the resolved folder. If either is missing, block with:
> "Cannot proceed — `[file]` not found in `[path]`. Run `/rag-spec-writer` first or provide the correct path."

**Minimum output:** paths of `spec.md` and `implementation-plan.md` confirmed in one line.

---

## Step 2 — Spec validation

Before writing any code, validate that every step in `implementation-plan.md` is actionable.

For each step in the plan, check:
1. **References are resolvable** — any file, class, interface, or package the step names must already exist or be created by a prior step in the same plan
2. **Instructions are unambiguous** — the step must be specific enough to implement with one reasonable interpretation
3. **Consistent with project constraints** — the step must not violate `CLAUDE.md` or `instructions/project/vat-verifier/rag-context.instructions.md` (e.g., step must not introduce Semantic Kernel if it is deferred)
4. **Prerequisites are met** — anything required before the step (Ollama running, model pulled, package available) is either verifiable or explicitly noted in the plan

Classify each issue found:
- `BLOCKING` — step cannot be executed as written
- `AMBIGUOUS` — step has multiple valid interpretations; agent must choose one, which requires user confirmation
- `NOTE` — informational; agent can proceed but user should be aware

If any `BLOCKING` or `AMBIGUOUS` issues exist → trigger **Spec Validation Gate** (see `rag-implementation-gate.instructions.md`).

If only `NOTE` items: list them briefly and continue.

If no issues: state "Spec validation passed." and continue.

---

## Step 3 — Baseline check

Before implementing anything, verify the project is in a clean state:

```bash
dotnet build
dotnet test
```

If build fails: report the error. Do not proceed — this is a pre-existing problem, not caused by this implementation.

If tests fail: report which tests fail. Do not proceed without user acknowledgment. The user must either fix the pre-existing failures or explicitly accept that those tests were already failing before this run.

If both pass: record baseline results in memory for comparison in Step 7.

**Minimum output:** "Baseline: build PASS, N tests PASS" or a list of pre-existing failures with user acknowledgment.

---

## Step 4 — Execute implementation steps

For each step in `implementation-plan.md`, in order:

### 4a — Read the step

Read the step description, list of files to create or modify, and acceptance criterion.

### 4b — Implement

Make the code changes. Follow `CLAUDE.md` constraints:
- No Semantic Kernel, no DB, no auth, no background jobs, no Clean Architecture layers
- Use `Microsoft.Extensions.AI` abstractions (`IChatClient`, `IEmbeddingGenerator`)
- Use `OllamaSharp` for Ollama; do not call Ollama HTTP endpoints directly

### 4c — Build check

After each step's changes are complete:

```bash
dotnet build
```

If build fails:
1. Attempt to fix the error (up to 2 tries)
2. If still failing → trigger **Build Failure Gate** (see `rag-implementation-gate.instructions.md`)

### 4d — Record

Note for the summary: step number, title, files created or modified, build result.

### 4e — Deviation detection

If at any point during Steps 4a–4d you realize the plan as written is wrong or cannot be followed exactly:

STOP immediately. Do not write any deviant code. Trigger **Deviation Confirmation Gate** (see `rag-implementation-gate.instructions.md`).

Only after user confirmation of the deviation: continue and record in `deviation-log.md` before proceeding to the next step.

---

## Step 5 — Full test run

After all plan steps are complete (or all remaining steps are confirmed-skipped):

```bash
dotnet test --verbosity normal
```

Capture:
- Total tests run
- Tests passed / failed / skipped
- Any test that was previously passing but now fails (regression)

If any test fails: stop and report. Do not write the implementation summary until failures are resolved or the user explicitly accepts the failing tests with a documented reason.

---

## Step 6 — Test coverage verification

For each plan step that was implemented (not skipped), verify that at least one test exists that exercises the new or modified behavior:

1. Read the step's acceptance criterion from the plan
2. Grep the test project for tests that plausibly cover this path (by class name, method name, or endpoint path)
3. Classify coverage as:
   - `Covered` — at least one test found
   - `Partial` — test exists but does not reach the new code path specifically
   - `Missing` — no test found

Steps classified as `Missing` are flagged for the review. The agent SHOULD write a test for `Missing` coverage before finalizing, unless the step is categorized as "infrastructure only" (e.g., DI registration, configuration reading).

If writing a new test: apply the same build + run cycle from Step 4c/5.

---

## Step 7 — Write implementation summary

Write `docs/spec/<slug>/implementation-summary.md` using the schema in `rag-implementation-artifacts.instructions.md`.

Populate from:
- Step 4d records (steps + files changed)
- Step 5 test results
- Step 6 coverage results
- Any deviation records from `deviation-log.md`
- Baseline from Step 3 for comparison

---

## Step 8 — Update README

After the implementation summary is written, update the two project-state sections in `README.md` to reflect the actual current state of the repository. These sections describe the project to developers — they must stay accurate after every implementation run.

### 8a — Project layout

Read the current "Project layout" section from `README.md`. Then scan the actual file system:
- Glob `src/**/*.cs` and `src/**/` for new directories or files added during this implementation
- Glob `tests/**/*.cs` and `tests/**/` for new test files or directories

Update the ASCII tree in "Project layout" to include any new directories or notable files that were added. Preserve the existing style and depth. Remove entries that no longer exist. Do not list every file — keep it at the level of directories and one or two representative files per directory, matching the original style.

### 8b — Configuration

Read `src/VatVerifier.Api/appsettings.json`. Compare its current structure to the JSON snippet in the "Configuration" section of `README.md`.

If new configuration sections or keys were added during this implementation, update the snippet to reflect the current structure. Preserve key comments or descriptions already present in the README section. Do not include values that are credentials or environment-specific.

### 8c — Write

If either section changed, write the updated `README.md`. If neither section needed updating, note "README up to date" and continue.

---

## Step 9 — Inline review checks

Run the following checks and record pass/fail in the summary:

| Check | Pass condition |
|:---|:---|
| Build passes | `dotnet build` exits 0 with no errors |
| All tests pass | `dotnet test` exits 0; no failures; count matches or exceeds baseline |
| New features have tests | No plan step classified `Missing` in Step 6 (or each missing has a documented exception) |
| No unconfirmed deviations | Either no deviation-log.md exists, or every entry in it has a `Confirmed by` field |
| No regressions | Tests that passed in baseline still pass |
| README up to date | Project layout and Configuration sections reflect current `src/` structure and `appsettings.json` |

If all checks pass: add `Review: PASS` to the summary.

If any check fails → trigger **Review Gate** (see `rag-implementation-gate.instructions.md`).

---

## Step 10 — Present summary

Show the user the path to `implementation-summary.md` and the review check table.

If review passed:
> "Implementation complete. Review passed. Summary: `docs/spec/<slug>/implementation-summary.md`"

If review has open items:
> "Implementation complete but review flagged [N] issues. See summary for details. Resolve before handing to a review agent."

---

## Minimum outputs per invocation

A complete run produces:

1. Source code and test changes in `src/` and `tests/` directories
2. `docs/spec/<slug>/implementation-summary.md`
3. `docs/spec/<slug>/deviation-log.md` — only if one or more deviations were confirmed
4. Updated `README.md` (Project layout and Configuration sections) — or a note that it was already up to date
5. A final build and test run confirming green state (or a documented exception)
