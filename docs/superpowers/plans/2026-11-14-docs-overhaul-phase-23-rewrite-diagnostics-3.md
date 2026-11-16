# Docs overhaul phase 23: rewrite-diagnostics-3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sections `SQLSRC226` to `SQLSRC260` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide, with every fact kept.

**Architecture:** One file of prose, `site/diagnostics.md`, edited only between `## SQLSRC226` and the line before `## SQLSRC261`, and one ledger file that accounts for each difference the checker prints.  Five tasks, one per area of `reference/errors.md#errors-by-area`, each ending with the checker passing for its sections.

**Tech Stack:** Markdown (docfx), the epic's checker `check/check.sh` (.NET file-based app), `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-23-rewrite-diagnostics-3-design.md`, with `diagnostics-guide.md` and `rewrite-guide.md` of `docs/superpowers/specs/2026-11-12-docs-overhaul/`.

## Global Constraints

- Change no line outside the range; change no other file of `site/`, and not `site/AGENTS.md`.
- Keep each heading, bold title line, "Reported by" line and "Database" line word for word; keep every fenced block byte for byte and its info string; no block inside a list; no heading of any level inside a section.
- Keep each sentence that phase 2 added where it stands (the paragraphs directly under the "Reported by" lines of `SQLSRC241`, `246`, `249` to `256`, `258` to `260`); remove a duplicate from the section's own text, keeping the detail.
- Shape: what happened, (why), (samples of the mistake), `**Fix:**` with a numbered list (bullets under "do one of these" for alternatives), (sample of the fix), (notes as bullets that open with a bold sentence), (severity paragraph), `See:` last.
- `SQLSRC243` has no "Fix" part.  `SQLSRC231` keeps a fix for each database, as the last item of its cause.
- Two databases are two causes, `**On PostgreSQL.**` then `**On SQL Server.**`, never tabs.
- Outputs in the section's own words are "`repository` or `methods`" and "`repository`, `methods` or `models`"; text the build or the tool prints keeps its order.
- `SQLSRC235` and `SQLSRC247` gain `[Supported versions](reference/databases/index.md#supported-versions)` as the first link of the `See:` line; no other `See:` line changes.
- Steps at most 20 words and one instruction; descriptions at most 25 words; keep every limiter word ("not", "only", "never", "unless", "alone", "at most", "at least", "except", "whatever").

## Review Focus

- A list of conditions read as "all of" against "one of": `SQLSRC250` (three ways a name is not found), `SQLSRC252` (each refused kind), `SQLSRC257` (each condition of "the same").
- A sentence true of one database now read as true of both: `SQLSRC226`, `228`, `231`, `235`, `246`.
- A code span or numeral lost in a long list: `SQLSRC228`, `232`, `246`, `252`; the checker's counted sets must match.
- What an error costs, per section: under `repository` or `methods` no method; under `models` the constant or token method; `SQLSRC257` keeps the earlier query's model.
- The severity paragraphs: `SQLSRC240` and `245` warnings, `248` information with its four places; no other section gains one.

---

### Task 1: Describing a query, SQLSRC226 to SQLSRC236

**Files:**
- Modify: `site/diagnostics.md` (sections `SQLSRC226` to `SQLSRC236`)
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-23.tsv` (`# base <merge-base sha>`, header, one row per section as a range of baseline lines, `move` to `site/diagnostics.md#sqlsrcnnn`, notes for each checker difference)

- [ ] **Step 1:** Rewrite the eleven sections in the shape; check reasons, `help:` texts and steps against `ToolDiagnostics.cs`, `PostgresErrors.cs`, `ConnectionErrors.cs`, `NpgsqlCheck.cs`, `SqlServerErrors.cs` and `DescribeFailures.cs`.
- [ ] **Step 2:** Add the `supported-versions` link to the `See:` line of `SQLSRC235`, with a `new` row.
- [ ] **Step 3:** Run `check.sh rewrite 23`; write a note for each difference it prints for these sections.

### Task 2: The sidecar, SQLSRC237 to SQLSRC245

- [ ] **Step 1:** Rewrite the nine sections; `SQLSRC237`'s fix is ordered steps with the schema first; `SQLSRC243` keeps its text and gets no fix; `SQLSRC245` keeps its two-projects paragraph whole in meaning.  Check against `SidecarChecker.cs`, `SqlDiagnostics.cs` and `SidecarTests.cs`.
- [ ] **Step 2:** Run `check.sh rewrite 23`; write the notes.

### Task 3: The C# types, SQLSRC246 to SQLSRC252

- [ ] **Step 1:** Rewrite the seven sections; `SQLSRC246` keeps its ten families, each with its own fix and key; `SQLSRC247` gains the `supported-versions` link with a `new` row.  Check against `TypeDiagnostics.cs`, `QueryTyper.cs`, `NpgsqlProbe.cs` and their tests.
- [ ] **Step 2:** Run `check.sh rewrite 23`; write the notes.

### Task 4: The models, SQLSRC253 to SQLSRC260

- [ ] **Step 1:** Rewrite the eight sections; the mended `sql postgres` query stands after `**Fix:**`, the `sql error` block before it.  Check against `ModelCollector.cs`, `ModelReader.cs` and the model tests.
- [ ] **Step 2:** Run `check.sh rewrite 23`; write the notes.

### Task 5: Checks and counts

- [ ] **Step 1:** Run every check of the brief's "Done when": the outside-range diff, the two counts (35 and 35), the `supported-versions` awk, the internals grep, the status and output-order searches, `check.sh rewrite 23`, `check.sh coverage`, `tools/build-site.sh`, `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] **Step 2:** Write the word counts, before and after, in a `#` line of the ledger.
- [ ] **Step 3:** `./format.sh`, `./pre-commit-validation.sh`, commit.
- [ ] **Step 4:** Content reviews (no-loss, accuracy) by two Opus subagents, fixes, then `/branch-review:review` to clean, then `./pre-commit-validation.sh --full`.
