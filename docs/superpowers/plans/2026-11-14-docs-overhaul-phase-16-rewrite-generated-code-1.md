# Docs overhaul phase 16, rewrite-generated-code-1: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Four pages of the group "Generated code", `site/reference/members.md`, `models.md`, `model-properties.md` and `return-types.md`, are in plain language and in the shape of a reference page, and the merge pairs MP-01 to MP-04 and the MP-10 terms Generator and Token method are merged.

**Architecture:** A rewrite of moved text, page by page, by the rewrite guide.  Each page task rewrites its page, finishes its worksheets, and adds its rows to `ledger/phase-16.tsv`.  No anchor, link target, fenced block, h1, `toc.yml` or file outside the phase's own files changes.

**Tech Stack:** Markdown (docfx), the epic's checker (`check/check.sh`, a .NET file-based app), docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-16-rewrite-generated-code-1-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`.

## Global Constraints

- `E` is `docs/superpowers/specs/2026-11-12-docs-overhaul`.  The base is `db3285d4adf7542f6e80e96d7f0b8182cd091a9f` (`main` after phase 11, merged into this branch).
- Change only: the four pages, `$E/ledger/phase-16.tsv`, `$E/merges/MP-01.md` to `MP-04.md`, the two terms Generator and Token method of `$E/merges/MP-10.md`, and this plan.
- Every `<a id>` line stays, once; a merge moves the second text's `<a id>` up above the one merged heading, first text's anchor first.  No new `<a id>`, no heading added; five headings go: one on `members.md`, four on `models.md`.
- Every fenced block is byte-identical, info string included.  A block can move inside its section; the brief lists none to change.
- Every fact stays: limiter words (not, only, never, unless, alone, at most, at least, except, whatever), numerals, ids with their own conditions, code spans with their case, messages letter for letter, every table row.
- The language rule of rewrite guide section 3; terms of its table 3.3 ("claims", "can be null", "current", "describes", "settings file", "folder").  "Shape" for `inputModelType`/`outputModelType`.
- Outputs in the order `repository`, `methods`, `models`, `sql`, for every list or part of one, but text the build or tool prints.
- A rule another section owns (brief, "Rules that another section owns") is cut to one or two sentences and a link, after checking the owner holds each fact; a fact the owner lacks stays and gets a `no owner:` note.
- No internals (`site/AGENTS.md` grep), no status words (rewrite guide section 10 search).

## Where the tree differs from the spec

- `repositories.md:133`'s heading is phase 17's (sent there by the manager); this phase does not touch it.
- The brief's word counts are of the map; at the base the pages are 3,920, 3,423, 2,740 and 1,767 words by `wc -w`.  The checker's counts are the ones reported.

## The ledger

`$E/ledger/phase-16.tsv`: line 1 `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f`, then `unit	disposition	to	arg	words	note`.  Units are named as `check.sh origin <page>` prints them: a baseline id (`site/generator.md:109`) where it gives one, else `@site/reference/<page>.md:<line>` at the base.  A row can hold a range.  Dispositions: `move` (reworded), `merge-pair` with `MP-<nn>` in `arg`, `restated-from` with the owner `<file>#<anchor>` in `arg`, `corrected` with the new words in `words` and the source in the note.  A lifted heading is a `move-split` row with `-` in `to` and a note naming the heading it was lifted into.  A `#` comment line gives each page's word count, old > new.

## Review Focus

- `members.md#setting-visibility`: the two lists against `PlacementChecks.cs`, "all of these" vs "one of these", case by case.
- MP-02 and MP-03: each rule of "Where the texts differ" is in the merged text (SQLSRC257 for equal suffixes; the suffix ends a table model's name; `UserDto`/`UserDTO`; two namespaces claiming one file or reading one table; `file`, `required`, `scoped`, `extension`; the global namespace).
- `return-types.md#streaming-rows`: each half of a split sentence names its database, checked against both `StreamingQueryTests.cs`.
- "Alone": SQLSRC123 for one method's models leaves out that method alone; a placement error leaves the whole file out.
- A `private` method of a `public` type still has public models: the two rules stay two.

---

### Task 1: `members.md`, MP-01, MP-10 Generator and Token method

**Files:** Modify `site/reference/members.md`, `$E/merges/MP-01.md`, `$E/merges/MP-10.md` (under `## Generator` and `## Token method` only), create `$E/ledger/phase-16.tsv`.

- [ ] **Step 1:** Merge MP-01: one heading "Where the members go" with `<a id="locations">` then `<a id="setting-member-location">` above it; the h3 `placement-for-a-file` stays under it.  Setting order: the setting's sample first, the values table (`class` row first, then `extension-class`, then `nested-class`), default, places, precedence, errors; then the facts about the code.  Check defaults and SQLSRC122 against `QueryPlacement.cs`, `PlacementChecks.cs`, `tests/SqlSource.Tests/Generator/PlacementTests.cs`.  The pointer "The names a member cannot have … are under Where the members go" goes, as said twice.
- [ ] **Step 2:** `#generator`: merge the MP-10 term into one definition after the sample (generator: the part of the `SqlSource` package that runs in the compiler, reads the `.sql` files, writes C# beside your code, needs no database).  Cut the outputs rule to a restatement linking `settings.md#setting-output`, `repository` first, keeping what `methods` is for; cut .NET 8 / C# 12 to a restatement linking `msbuild.md#build-requirements`, keeping SQLSRC003 and SQLSRC012 here with `no owner:` notes.
- [ ] **Step 3:** `#setting-visibility`: "What does not fit" and "What fits, with no diagnostic" as lists, one case an item, "all of these"/"one of these" said; defaults with `repository` first.  Check each case against `PlacementChecks.cs` and `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`.
- [ ] **Step 4:** `#token-methods`: merge the MP-10 term (signature and place owned here; what a token is owned by `tokens.md#tokens`).  Rewrite `#setting-nested-class` (check names against `NestedClassTests.cs`), `#constants`, `#intellisense` by the language rule.
- [ ] **Step 5:** Finish MP-01 (`## The facts`, `## Conflicts`, `## The merged text`) and MP-10's two terms (`### The facts`, `### Conflicts`, `### The merged text`); ledger rows for every unit of the page; run `check.sh rewrite 16` and account for every line it prints; anchor and fence loops; internals and status greps; `git grep` order search.
- [ ] **Step 6:** `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 2: `models.md`, MP-02, MP-03, MP-04

**Files:** Modify `site/reference/models.md`, `$E/merges/MP-02.md` to `MP-04.md`, `$E/ledger/phase-16.tsv`.

- [ ] **Step 1:** MP-02 "Names": `model-names`, `setting-model-suffix`, `setting-model-name` above the one h2; bold lead-ins "The suffix" and "The name of one query's model"; settle against `ModelCollector.cs`, `ModelNameChecker.cs`, `TypeModels.cs`.
- [ ] **Step 2:** MP-03 "Namespace": `model-namespace`, `setting-model-namespace`; check "or read one table" in `TypeModels.cs` before writing it; settle against `ModelPlace.cs`, `SettingValue.cs`.
- [ ] **Step 3:** MP-04 "Shape": `model-shape`, `setting-model-type`; settle against `src/SqlSource/Settings/ModelKind.cs`, `src/SqlSource/Generation/Models/ModelKinds.cs`.
- [ ] **Step 4:** Rewrite the other sections; `#which-models` lists outputs `repository` first; `#shared-models` against `SharedModelTests.cs`, `#table-models` against `TableModelTests.cs`.  Each pointer of a setting section to its own merged section goes.
- [ ] **Step 5:** Finish the three worksheets; ledger rows; checks as Task 1 step 5; `grep -c -E "^#{2,} " site/reference/models.md` is four fewer than at the base.
- [ ] **Step 6:** `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 3: `model-properties.md`

**Files:** Modify `site/reference/model-properties.md`, `$E/ledger/phase-16.tsv`.

- [ ] **Step 1:** `#column-marker`: one rule a bullet; the last bullet's header-line rule restated to `#setting-column-types` on this page (add any fact it lacks there).  Check against `src/SqlSource/Parsing/Markers/SqlColumnDeclaration.cs`, `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`.
- [ ] **Step 2:** `#setting-column-types`: key form and matching against `src/SqlSource/Settings/ColumnTypes/ColumnTypeKey.cs`, `ColumnTypeTests.cs`; "Over the type override" restated to `type-mapping.md#property-types`.  `#model-nullability`'s why restated to each database's `nullability.md#nullability`.  `#property-names` against `PropertyNames.cs` (SQLSRC254).
- [ ] **Step 3:** Ledger rows; checks; `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 4: `return-types.md`

**Files:** Modify `site/reference/return-types.md`, `$E/ledger/phase-16.tsv`.

- [ ] **Step 1:** `#method-shapes`: shape words and their errors restated to `sql-files.md#result-shapes`; what each shape returns stays; outputs `repository` first.
- [ ] **Step 2:** `#setting-collection-type` against `src/SqlSource/Settings/CollectionKind.cs` (default `Array`).  `#streaming-rows`: one rule a bullet; each half of a split sentence names its database, checked against both `tests/DatabaseTests/*/EndToEnd/StreamingQueryTests.cs`.  "The rows still to come" is not status language.
- [ ] **Step 3:** Ledger rows; checks; `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 5: The phase's checks and counts

- [ ] **Step 1:** `check.sh rewrite 16` and `check.sh coverage` exit 0; `git diff --name-only db3285d4` names only this phase's files; the anchor, fence and limit loops; `awk` of MP-10 prints the h2 of Generator and Token method; `grep -L '^## The merged text'` over MP-01 to MP-04 prints nothing; `grep -c -E "^#+ Where the members go$" site/reference/members.md` prints 1; `tools/build-site.sh`; `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] **Step 2:** Word counts in a `#` line of the ledger; `p16-notes.md` in the shared notes folder for facts later phases need.
- [ ] **Step 3:** Two Opus content reviewers (no-loss, accuracy), fixes; `/branch-review:review --base origin/main` to clean; `./pre-commit-validation.sh --full`.
