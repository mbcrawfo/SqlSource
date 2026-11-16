# Docs overhaul phase 22: rewrite-diagnostics-2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sections `SQLSRC122` to `SQLSRC225` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide, with every fact kept and no line outside the range changed.

**Architecture:** One page, one range of 32 sections, rewritten section by section in four slices by area.  Each slice is checked with `check.sh rewrite 22`, and `ledger/phase-22.tsv` gives one `move` row per section (its baseline lines as one range) plus `new` rows for the four added `See:` lines and a `corrected` row for SQLSRC209.

**Tech Stack:** Markdown (docfx 2.81), the epic's checker (`check/check.sh`, .NET 10), xUnit tests of `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-22-rewrite-diagnostics-2-design.md`, with `docs/superpowers/specs/2026-11-12-docs-overhaul/diagnostics-guide.md` and `rewrite-guide.md`.

## Global Constraints

- Range: from the line `## SQLSRC122` to the line before `## SQLSRC226`.  No line outside it changes.
- Heading, bold title line, "Reported by" and "Database" lines: word for word.
- Every fenced block byte-identical (the brief lists none to change); no block inside a list; no heading of any level inside a section; no line starting with `## ` but the section heading.
- Shape: what happened, (why), (sample of the mistake), `**Fix:**` + numbered steps (or "do one of these." + bullets), (sample of the fix), (notes as bullets with a bold opening sentence), `See:` last.  `SQLSRC216` has no Fix.
- New `See:` lines: 221 `reference/sidecars.md#sidecar-files`; 222 and 223 `reference/cli.md#arguments`; 225 `reference/databases/postgresql/index.md#what-describing-does`, then `reference/databases/sqlserver/index.md#what-can-be-described`.  Link text is each heading as it stands on its page.  Each is a `new` ledger row.
- Lists of outputs in the section's own sentences: `repository` first (`repository`, `methods`, `models`, `sql`).  Text the build or the tool prints keeps its order.
- Tables of 219 (8, 3 rows) and 224 (18, 12 rows): no cell changes.
- 224 and 225: PostgreSQL then SQL Server, as `**On PostgreSQL.**` / `**On SQL Server.**` causes; never merged, never tabs.
- Owner ruling (p10 Q2): SQLSRC209's "a project that sets neither gets this error for every file" becomes "each file that a class reads", with a `corrected` row.
- Terms: "folder" not "directory" in prose; "out of date" not "stale"; "the build"/"the tool" as actors; "must" only where an error holds the rule.
- No internals (`site/AGENTS.md` grep); no status words.
- Files changed: `site/diagnostics.md`, `ledger/phase-22.tsv`, and this plan (the repository's place for plans).

## Review Focus

- A limiter word ("not", "only", "never", "unless", "alone", "at most", "except") lost when a long sentence is split.
- A cost that moves to the wrong cause (SQLSRC123's first/third vs second/fourth reasons; 209/210 by output).
- A PostgreSQL fact that now reads as true of SQL Server, or the reverse (224, 225).
- An output list reordered inside printed text (a message in a code span, a `console` block).
- An id, `Msg` number or SQLSTATE dropped or moved to another condition.

---

### Task 1: Ledger and baseline

**Files:** Create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-22.tsv`.

- [ ] Write `# base <merge-base of origin/main and HEAD>`, a `# Words` comment (filled in Task 6), the header `unit	disposition	to	arg	words	note`.
- [ ] Run `check.sh rewrite 22` on the untouched tree: expect exit 0 and no section printed.
- [ ] Record the range's word count before (`awk … | wc -w`): 11,321 at base `db3285d4`.

### Task 2: `.sql` files area, SQLSRC122–127

**Files:** Modify `site/diagnostics.md` (sections 122–127).

- [ ] Rewrite each section in the shape.  122: two groups (`repository`; then `sql` or `models` → keep each output with its rule, reorder inside lists).  123: four reasons in order, bullets with bold openings; costs per reason; marker order `-- visibility:`, else `-- member-location:`, else name; three scenes keep their `console` blocks in order, each after the paragraph it shows.  126: keep the three bold bullets.  127: Fix then the `sql mssql` block after it.
- [ ] Add one `move` row per section: `site/diagnostics.md:<first>-<last>` baseline range → `site/diagnostics.md#sqlsrcnnn`.
- [ ] Run `check.sh rewrite 22`; note each printed difference.

### Task 3: The tool, SQLSRC200–212

**Files:** Modify `site/diagnostics.md` (sections 200–212).

- [ ] Rewrite each section; the "path that holds `=` or `;`" sentence becomes a note in 201–203, 207, 212.  208 keeps its `See:` line.  209: apply the owner ruling; keep both build and tool.  210: both build and tool.
- [ ] Ledger rows as in Task 2; `corrected` row for 209's sentence with the new words and the source (`ToolDiagnostics.cs` descriptor; error raised only for a `.sql` file a class reads).
- [ ] Run `check.sh rewrite 22`.

### Task 4: The tool, SQLSRC213–223

**Files:** Modify `site/diagnostics.md` (sections 213–223).

- [ ] 213: the order of sources (command line, then the variable of the name, then `SQLSOURCE_CONNECTION`; environment before the `.env` file), the three `help:` forms each as a code span.  216: no Fix, short sentences, title kept.  218: three lists in order sidecar, log, bundle; Fix in the guide's form.  219: tables unchanged; exit codes 2 and 1.  221–223: the new `See:` lines.
- [ ] Ledger rows; `new` rows for the three `See:` lines.
- [ ] Run `check.sh rewrite 22`.

### Task 5: Describing a query, SQLSRC224–225

**Files:** Modify `site/diagnostics.md` (sections 224–225).

- [ ] 224: tables stay at the left margin after the sentence that names each; `**On PostgreSQL.**` before the first, `**On SQL Server.**` before the second.  225: PostgreSQL causes as bold-opening paragraphs, each with its `console` block after it; SQL Server half with its two blocks and eight bullets; new `See:` line with two links.
- [ ] Ledger rows; `new` row for 225's `See:` line.
- [ ] Run `check.sh rewrite 22`: compare `Msg` numbers and SQLSTATEs as sets (no change).

### Task 6: Account and check

- [ ] A note for each difference `check.sh rewrite 22` prints; it exits 0.  `check.sh coverage` exits 0.
- [ ] The outside-range diff (diagnostics guide) prints nothing; headings and `See:` counts over the range both 32; fence-line counts equal.
- [ ] Internals grep, status grep and output-order grep over the range: each hit is printed text or a title.
- [ ] Word count after into the `# Words` comment.
- [ ] `./format.sh`, `tools/build-site.sh` (no warning), `dotnet test --project tests/SqlSource.Repository.Tests`, `./pre-commit-validation.sh`; commit.

### Task 7: Reviews (outline order)

- [ ] Two Opus content reviewers (no-loss, accuracy) with the reviewer's kit; fix findings; re-run Task 6 checks.
- [ ] `/branch-review:review --base origin/main` to clean.
- [ ] `./pre-commit-validation.sh --full` on the tree as pushed.
