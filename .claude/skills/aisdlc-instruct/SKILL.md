---
name: "aisdlc-instruct"
description: "Writes or refines a single instruction file of a specified archetype. Use to fill in [TODO] markers, improve an existing file, or create a new one from scratch."
argument-hint: "[ARCHETYPE] [path-to-file] [-- requirements text]  —  ARCHETYPE is one of: RULES, GUARDRAIL, WORKFLOW, CONTRACT, KNOWLEDGE, MANIFEST"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

---

## Archetype reference

| Archetype | Single responsibility | What belongs here | Key authoring rules |
|:---|:---|:---|:---|
| RULES | Agent identity and methodological constraints | Purpose, decision levels, non-goals, boundaries delegated elsewhere | MUST state non-goals explicitly. MUST list which decisions the agent makes autonomously vs. escalates. MUST NOT contain workflow steps or domain-specific runtime values. |
| GUARDRAIL | Behavioral boundary requiring human approval | Allowed list, approval-required list, confirmation procedure, post-confirmation actions | Use two-phase gate: (1) Critical gate — hard stop, user MUST approve before agent proceeds; (2) Warning gate — user MAY accept, defer, or accept with noted exceptions; agent proceeds either way. A gate that only says "stop and ask yes/no" MUST be replaced with this pattern. |
| WORKFLOW | Ordered process steps | Step number, goal, action, minimum output, referenced files | Steps MUST be algorithms, not data. MUST NOT contain method names, IDs, column names, or requirement text — those are runtime values read from artifacts. Each step MUST declare its minimum output. |
| CONTRACT | Artifact schema and validation | Field definitions, status vocabulary, per-artifact rules, forbidden combinations | Status vocabulary MUST be an exhaustive closed list. Forbidden combinations MUST be stated as explicit "MUST NOT coexist" rules, not as general warnings. |
| KNOWLEDGE | Domain facts and project context | Factual statements, sources, known limitations | Each statement MUST be a verifiable fact, not a preference or instruction. Attribute each fact to a source when possible. MUST NOT include instructions on how to use the knowledge — those belong in RULES or WORKFLOW. |
| MANIFEST | Routing index | Always-load section, conditional-load sections, forbidden combos, adapter-only section | Always-load MUST NOT include files with step-specific content, long reference tables, or detailed examples. Every listed file MUST exist. Conditional-load entries MUST state the condition explicitly. Forbidden combos MUST name the specific files that must not coexist in the same load context. |

**Authoring modal verbs:** MUST (required), MUST NOT (forbidden), SHOULD (preferred),
MAY (optional). Avoid: "consider", "be careful", "where appropriate" — state the condition
and required action explicitly.

**Single responsibility rule:** A file that would change for two different reasons MUST
be split into two files.

**Always-load budget (MANIFEST):** A file MUST NOT be in always-load if it contains
step-specific content, long reference tables, or detailed examples.

---

## Step 1 — Identify target

Parse $ARGUMENTS:
- First token: archetype (RULES, GUARDRAIL, WORKFLOW, CONTRACT, KNOWLEDGE, MANIFEST)
- Tokens before `--` (if present), or all remaining tokens: path to the target file
- Tokens after `--` (if present): inline requirements text

If the file exists, read it in full before making any changes.

If the file does not exist and arguments lack enough detail to generate a meaningful
file, ask what the file should contain before proceeding.

---

## Step 2 — Identify gaps

Classify each gap as **blocking** or **non-blocking**:

- **Blocking** — prevents writing a coherent file at all (e.g., no purpose for RULES,
  no steps for WORKFLOW, no approval conditions for GUARDRAIL).
- **Non-blocking** — content is missing but the structure can be written around it.

**If the file exists:**
- List all `[TODO]` markers present
- Identify incomplete or missing sections per the archetype rules above
- Classify each as blocking or non-blocking

**If the file does not exist:**
- Derive content from the arguments and inline requirements text
- Classify each unresolvable area as blocking or non-blocking

---

## Step 3 — Resolve or ask

1. Resolve any gap if the information is present in the arguments, inline requirements
   text, or derivable from the file.
2. For **blocking** gaps only: ask the user in a single message. Do not ask one
   question per gap. Do not ask about non-blocking gaps.
3. For **non-blocking** gaps: write a specific `[TODO: ...]` marker immediately.

**Specific TODO rule:** every unresolved marker MUST name exactly what information is
needed.

Correct: `[TODO: List the specific conditions that require human confirmation]`
Wrong: `[TODO]`

Do not invent domain-specific content. Ask only for blocking gaps.

---

## Step 4 — Decide: patch or controlled regeneration

Before writing anything, choose the operation.

**Minimal patch** (default) — update only the target section(s). Choose when:
- Fewer than ~30–40% of the file would change
- The result is coherent without touching other sections

**Controlled regeneration** — reconstruct the file from a full read. Choose when:
- More than ~30–40% of the file would change
- Patching would leave duplicated or contradictory instructions
- The requested archetype does not match the current file structure
- The user explicitly asked to rewrite, normalize, or clean up the file

**Controlled regeneration rules:** read the current file in full first; preserve all
user-provided domain facts, decisions, examples, TODO descriptions, and naming unless
they conflict with the requested change; remove only what the change makes obsolete.

Record the choice — it is reported in Step 7.

---

## Step 5 — Apply conventions

Write or update the file following the authoring rules for the identified archetype.

- Preserve all existing content that is correct and complete
- Do not add content beyond what resolves the stated gaps
- Do not change the archetype of an existing file
- Do not delete sections unless explicitly instructed

If the archetype in the arguments conflicts with content already in the file, stop and
ask which is correct before proceeding.

**MANIFEST special handling:**
- Before writing: verify that every file listed exists on disk
- If any listed file does not exist: report it and do not write the MANIFEST until the
  user confirms how to handle the missing reference
- After writing: report which commands, skills, or adapters may need to reference this
  MANIFEST if it is new or renamed

**New file cross-reference report:** if creating a new instruction file of any archetype,
report which manifests, commands, skills, or adapters may need to reference it. Do not
silently create a file with no known entry point.

**Existing file reference report:** if changing a file that is already referenced by a
manifest, command, skill, or adapter, report those references so the user can validate
they still point correctly.

---

## Step 6 — Validate

After writing, run the following checks. Fix any failure before reporting.

- [ ] File matches the requested archetype (single responsibility, correct content category)
- [ ] File has one responsibility (would not change for two different reasons)
- [ ] No generic `[TODO]` without a description remains
- [ ] Every remaining `[TODO: ...]` names exactly what information is needed
- [ ] Modal verbs follow the convention (MUST, MUST NOT, SHOULD, MAY)
- [ ] No domain-specific values were invented (method names, IDs, column names, requirement text)
- [ ] No unrelated sections were added
- [ ] Existing correct content was preserved
- [ ] For MANIFEST: all listed files exist; always-load entries pass the budget rule
- [ ] For new files: cross-reference report was prepared

---

## Step 7 — Report

After writing the file, produce a structured summary:

```
Operation:        create | patch | controlled regeneration
File:             [path]
Archetype:        [archetype]
Sections changed: [list of headings changed, or "n/a" for new file]
TODOs remaining:  [count and one-line description of each, or "none"]
Validation:       PASS | PASS_WITH_WARNINGS | FAIL
Related files that may need updating:
  [manifest, command, skill, or adapter path — or "none identified"]
```

For PASS_WITH_WARNINGS or FAIL: list each issue under Validation.
