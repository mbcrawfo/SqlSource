# Docs overhaul, phase 13: rewrite-sql-files Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The four pages of the group ".sql files" are in plain language and in the shape of a reference page, with the five MP-10 terms each defined once and the two reframings made.

**Architecture:** A rewrite of words only, on four pages of `site/reference/`.  Each page is rewritten section by section in the order of the rewrite guide's section 10: shape, merge, restatements, language, reframings.  The ledger `ledger/phase-13.tsv` gives every reworded unit a row, and `merges/MP-10.md` gets its three parts under the five terms of this phase.

**Tech Stack:** Markdown (docfx), the epic's checker `check/check.sh` (.NET file-based app), `tools/build-site.sh`, `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-13-rewrite-sql-files-design.md`, with `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md` and the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.

## Global Constraints

- Change only: `site/reference/sql-files.md`, `parameters.md`, `tokens.md`, `dialects.md`, `E/ledger/phase-13.tsv`, and the five terms' h2 of `E/merges/MP-10.md` (`E` = `docs/superpowers/specs/2026-11-12-docs-overhaul`).
- No `<a id>` added, removed or renamed; no h1, `toc.yml`, test, example or other page changed; no page split.
- Every fenced block byte-identical, info string included; a block may move inside its section so that the section opens with it.  No block added.
- Tables of facts (marker index, result shapes, both tables of `dialects.md`, the comparison table of `tokens.md`, the table of `#names-read-as-parameters`, `#markers-in-the-wrong-place`) change only in the two rows the reframings name: `-- output:` and `-- dialect:`.
- Outputs listed `repository`, `methods`, `models`, `sql`, in any partial list too.
- No "choose/pick/select a dialect"; `## Dialect names` heading over the `choosing-a-dialect` anchor.
- Text an earlier phase wrote `new` stays: the first paragraph of `dialects.md` and of `parameters.md`, and the headings phase 3 made.
- Headings quoted outside `site/` keep their words: "What each dialect changes" (`docs/tech-debt/TD-0004`).
- Language rule of the rewrite guide, section 3; terms of its section 3.3 ("current", "out of date", "can be null", "claims", "describes", "folder").
- `IN @ids` and `IN (@ids)` never tidied; copy the invisible characters of `#query-parameters` (zero-width non-joiner, `Ⅰ`, `‿`) from the page, never retype them.

## Review Focus

- A split bullet whose diagnostic id goes to the wrong half (`#param-marker`: SQLSRC111, 112, 116-118, 226-228; `#list-parameters`: SQLSRC126, 127).
- A limiter word ("only", "not", "never", "unless", "alone", "at most", "at least", "except", "whatever") lost in a split.
- A pointer word ("so does", "one", "it") turned into the wrong noun.
- A restatement that drops a fact its owner lacks: check each against the owner's section at the base.
- "all of these" against "one of these" where a condition becomes a list.

---

### Task 1: `sql-files.md`

**Files:** Modify `site/reference/sql-files.md`; Modify `E/merges/MP-10.md` (Query, Marker, File header).

- [ ] Merge the terms of `#marker`, `#queries-and-names` and `#the-file-header` into one definition each, in the sentence after the sample (the section's form wins: `-- word: rest`, its three examples).  Keep C-08's "one or more members" (check `src/SqlSource/Settings/MethodType.cs`, `src/SqlSource/Generation/Methods/MethodNames.cs`).
- [ ] Rows `-- output:` (`repository` first, still the default) and `-- dialect:` (set to your database, `postgres` or `mssql`; link the dialects page for the other names, the options and the reading rules).
- [ ] Cut the last paragraph of `#markers-in-the-wrong-place` to a restatement of `errors.md#what-an-error-costs`; keep the result-shape table of `#result-shapes` whole, the prose short with its link to `return-types.md#method-shapes`.
- [ ] Each section opens with its sample; one rule a bullet; sentences of at most 25 words.
- [ ] Write "The facts", "Conflicts", "The merged text" under the three terms of `MP-10.md`.

### Task 2: `parameters.md`

**Files:** Modify `site/reference/parameters.md`; Modify `E/merges/MP-10.md` (Query parameter).

- [ ] Merge the term into `#query-parameters`; split the `mssql` name sentence into a list, the zero-width-space exception its own item.
- [ ] Split each bullet of `#param-marker` and `#list-parameters` to one rule each, ids with their own condition.  Restate the order of a C# type (owner `type-mapping.md#property-types`) and when an entry stops being current (owner `sidecars.md#describing-again`); a fact the owner lacks stays, with a `no owner:` note.
- [ ] Keep the last bullet of `#list-parameters` (outputs `models` and `sql`, Dapper) whole in facts.
- [ ] Limit `limit-parameter-taken-for-a-variable` in "What you see", "Why", "What to do".
- [ ] Count the page; above 3,500 words, name the clean cut: `list-parameters`, `list-padding`, `list-limit`.

### Task 3: `tokens.md`

**Files:** Modify `site/reference/tokens.md`; Modify `E/merges/MP-10.md` (Token).

- [ ] Merge the term into `#tokens`, one word: "string concatenation".  The warning stays the first text after the sample, with its four facts.
- [ ] Restate where a token method goes (owner `members.md#token-methods`).
- [ ] Split the paragraph of `#token-defaults` on comments (SQLSRC120) into a list; "A default is a sample of what the caller passes", the rule that the method does not use it kept.
- [ ] Keep the four rows of `#token-or-parameter`.

### Task 4: `dialects.md`

**Files:** Modify `site/reference/dialects.md`.

- [ ] Leave the first paragraph; make the second follow from it.  The defining sentence is one sentence with its link (owner `settings.md#setting-dialect`).
- [ ] `## Dialect names`; "Set the dialect named for your database"; "the dialect names" in the opening.
- [ ] Tables equal to the old ones, cell by cell.

### Task 5: Ledger, checks, reviews

**Files:** Create `E/ledger/phase-13.tsv`.

- [ ] `# base <merge-base>`, a `#` line of word counts, a row for each reworded unit (`move`, `merge-pair` MP-10, `restated-from <owner>`, a `move-split` for no heading).
- [ ] Run `E/check/check.sh rewrite 13` and `coverage`: exit 0; account for every printed line in a note.
- [ ] Run the checks of the rewrite guide's section 10: anchors, fences, limits, MP-10 awk, internals grep, status grep, outputs grep, `grep -c '^## Dialect names'`, the dialect grep of the brief, `tools/build-site.sh`, `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] Two Opus content reviewers (no-loss, accuracy), then `/branch-review:review` to clean, then `./pre-commit-validation.sh --full`.
