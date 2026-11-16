# Docs overhaul phase 24, rewrite-diagnostics-4: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sections `SQLSRC261` to `SQLSRC299` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide, with every fact kept, and no line outside the range changed.

**Architecture:** A rewrite by hand, section by section, in four batches by area.  Each section gets the shape of `diagnostics-guide.md`: title, "Reported by", "Database", what happened, why, samples of the mistake, `**Fix:**` as a numbered list (or "do one of these" bullets), samples of the fix, notes as bold-led bullets, severity, `See:`.  Every unit of the range gets a row in `ledger/phase-24.tsv`; `check.sh rewrite 24` lists each counted difference, and each gets a note.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-24-rewrite-diagnostics-4-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the map, `diagnostics-guide.md` and `rewrite-guide.md`.

## Global Constraints

- Edit only `## SQLSRC261` to the end of `site/diagnostics.md`.  The check of "One file, four phases" with `a="## SQLSRC261"` and `b="## SQLSRC999"` prints nothing.
- Title, "Reported by" and "Database" lines word for word.  Every fenced block byte for byte; none inside a list; none added.
- Outputs listed `repository` first in the section's own sentences; text the build or the tool prints keeps its order (`SQLSRC273`'s message and `console` block).
- Keep every limiter word, numeral, code span, id and quoted reason.  The reasons of `SQLSRC277` (13), `286` (6), `292` (2), `295` (2) and the cases of `SQLSRC274` (8), `276` (3) keep their order.
- `See:` changes only for `SQLSRC267` (`reference/databases/index.md#supported-versions` first) and `SQLSRC293` to `296` (`reference/bundles.md#diagnose` first).  No link to `tool.md` known issues for `SQLSRC294` (C-03).
- No status words; no internals (the grep of `site/AGENTS.md`), but `captureVersion`, `bundleVersion`, `formatVersion` in `SQLSRC289`.
- Edit no other file of `site/`, not `site/AGENTS.md`.  Every commit passes `./pre-commit-validation.sh`.

## Where the tree differs from the spec

- The range is 11,206 words at base `db3285d4` (spec: 10,565 at the baseline, before phase 2's lines).  One pull request: it stays under the 12,000 of the outline.
- No section above `SQLSRC299` exists.
- This plan file is committed beside the work, as every phase's plan is (`docs/superpowers/AGENTS.md`), so "only your files" names it too.

## Review Focus

- A step that drops an alternative or a condition when a long sentence becomes a list ("all of these" against "one of these").
- What each error costs: the list of the spec's "What an error costs" (`272`-`275`, `279`, `280`, `282`, `283`; `277`/`278`; `276`; `281`/`284`; `297`; `298`; `299`).
- "Reported by" against the text: a build section must not say "the tool reports", and the reverse.
- `SQLSRC268` gets no `WarningsNotAsErrors` or `.globalconfig`; it keeps the pointer to `SQLSRC248` and "not in the output of `dotnet build`".
- `SQLSRC293` and `SQLSRC294` keep one fix, word for word; `SQLSRC293` keeps "do not paste this error into an issue" as a sentence of its own before the fix.

---

### Task 1: SQLSRC261 to SQLSRC271 (override, SQL Server, encoding)

**Files:** Modify `site/diagnostics.md` (the range); Create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-24.tsv` (`# base db3285d4…`, header).

- [ ] Rewrite the eleven sections to the shape; `SQLSRC267`'s `See:` line gains `[Supported versions](reference/databases/index.md#supported-versions)` first, with the heading's own text.
- [ ] Add a row for each unit, `move` to `site/diagnostics.md#sqlsrcnnn`, and the `new` row of the link.

### Task 2: SQLSRC272 to SQLSRC284 (methods, converters, libraries, JSON, short names)

- [ ] Rewrite the thirteen sections; "`repository` or `methods`" in the section's own sentences; the eight cases of `274` and thirteen reasons of `277` as bold-led bullets in their order.
- [ ] Ledger rows.

### Task 3: SQLSRC285 to SQLSRC299 (`.env`, watcher, bundles, `diagnose`, collection and synchronous methods)

- [ ] Rewrite the fifteen sections; `SQLSRC293` to `296` gain `[Making a bundle with \`diagnose\`](reference/bundles.md#diagnose)` first in `See:`.
- [ ] Ledger rows, and the four `new` rows.

### Task 4: Checks, counts and reviews

- [ ] Run `check.sh rewrite 24`; note each difference it prints in the ledger; `check.sh coverage` passes.
- [ ] Run the outside-range diff, the counts of headings and `See:` lines (39 and 39), the two `awk` link checks, the internals, status and output-order searches, the anchors and fence-count loops, `tools/build-site.sh`, and `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] Write the word counts, before and after, as a `#` line of the ledger.
- [ ] Content reviews (no-loss, accuracy; Opus subagents), then `/branch-review:review`, then `./pre-commit-validation.sh --full`.
