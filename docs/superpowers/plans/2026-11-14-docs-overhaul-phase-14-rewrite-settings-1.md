# Docs overhaul phase 14, rewrite-settings-1: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The five pages of the sidebar group "Settings" (`site/reference/settings.md`, `settings-file.md`, `generator-parameters.md`, `attributes.md`, `msbuild.md`) are in plain language and the shape of a reference page, with one meaning of "setting"; MP-06 and MP-07 are merged, the five MP-10 terms are defined once, and the output and dialect settings carry the owner's reframings.

**Architecture:** A rewrite of moved text, one page per task, in the order of the rewrite guide's section 10 (shape, merge, restatements, sentences, reframings).  No anchor, link target, fenced block, heading level or page changes, but the five h3 headings the spec adds to `settings-file.md`.  Every reworded unit gets a row in `ledger/phase-14.tsv`; the merges are written up in `merges/MP-06.md`, `MP-07.md` and the five MP-10 terms.

**Tech Stack:** Markdown with docfx, the epic's checker (`check/check.sh`), docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-14-rewrite-settings-1-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` (section 3 "Reference pages", section 5) and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`, which every task reads in full first.

## Global Constraints

- `E` is `docs/superpowers/specs/2026-11-12-docs-overhaul`.  The base is `db3285d4adf7542f6e80e96d7f0b8182cd091a9f` (first line of the ledger: `# base <sha>`; rewrite it after a sync with `main`).
- Change only the five pages, `$E/ledger/phase-14.tsv`, `$E/merges/MP-06.md`, `$E/merges/MP-07.md`, and the h2 sections Output, Logical database, Dialect, Settings file and Generator parameter of `$E/merges/MP-10.md`.  This plan is the one other file.
- Anchors: each page keeps exactly its `<a id>` lines of the base.  No `<a id>` is added; MP-06 and MP-07 keep both headings, so none is lifted.
- Fenced blocks: byte-identical, info string included; none added or removed.  A block can move inside its section so that it opens it.  The `console` block of `#running-in-the-build` is the tool's real output.
- Headings: same levels and order.  `grep -c '^## '` of `msbuild.md` and `attributes.md` stays 7 and 6; `grep -c '^### ' settings-file.md` goes from 1 to 6.  A heading quoted outside `site/` keeps its words (`git grep -n -F '"<heading>"' -- . ':!site' ':!docs/superpowers'`).
- Tables: the matrix and the table of places on `settings.md`, and the table of `msbuild.md#properties`, are phase 7's new text that `SettingsReferenceTests` reads: leave them.  A table cell is reworded only when false.
- Language (guide section 3): one instruction a sentence; ≤20 words a step, ≤25 a description (a code span is one word); active voice naming the right actor (you, SqlSource, the build, the tool, the database, the driver, the generated method); conditions first; "must" only for a rule an error holds, with the error named; a list for conditions; ≤6 sentences a paragraph; one rule a bullet with a bold lead-in.  Keep every limiter word: not, only, never, unless, alone, at most, at least, except, whatever.
- Terms (guide 3.3): a type *claims* a file; *can be null* / *never null*; *current* / *out of date*; *describes*; *settings file*; *folder*.
- Reframings (guide section 7): outputs in the order `repository`, `methods`, `models`, `sql`, partial lists too, but text the build or tool prints; no "choose/pick/select a dialect"; no status words; "setting" has one meaning (map section 5, "One definition").
- "The build" and "the tool" are two actors: a wrong `database` in a settings file is reported by the tool alone; a wrong `dialect` or `output` by both.
- A restatement of a rule owned on another phase's page keeps any fact the owner lacks, with a ledger note `no owner: <file>#<anchor>; <fact>`.  Never edit another phase's page.
- Ledger rows (guide section 10): six tab-separated columns `unit disposition to arg words note`, no empty cell (`-`).  A unit is named `site/<old>.md:<line>` as `check.sh origin` prints it, or `@site/reference/<page>.md:<line>` at the base for text with no baseline id.  Dispositions: `move`, `merge-pair` (`arg` `MP-<nn>`), `restated-from` (`arg` the owner `<file>#<anchor>`), `corrected` (`words` the new words, note the source), `new`.  Each line `check.sh rewrite 14` prints for the task's page is accounted for in a note.
- Each commit: `./format.sh`, then `./pre-commit-validation.sh` passes.  Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Where the tree differs from the spec

- None found at planning.  The three corrected sentences of `settings-file.md` (C-01 "only the tool checks", C-02 "holds no key of a settings file", the five properties) and the C-07 bullet of `attributes.md#what-the-package-adds` arrive as the spec expects; each task checks them against the source.
- `settings-file.md`'s units at lines 53 and 57 (`which-files-are-checked`, `settings-file-in-the-build`) show as `new` in `check.sh origin`, since phase 7 corrected them.  The spec names both as needing work, and the spec outranks the guide's "new text stays", so they are rewritten, each with a `move` row whose note names phase 7's correction.

## Review Focus

- **The five caveats of MP-06** (a database-named variable alone is not found under `auto`; a `.env` turns `auto` on whatever it holds; a `.env` above the project's folder turns nothing on and the build says nothing; the four CI variables and `CI=1` unrecognised; a dangling symbolic link counts as there): each is a line of the MP-06 worksheet's facts and stands under `#online-build`.
- **Each row of the output table read alone** is true, `repository` first.
- **The logical database's two uses** (tool: describe connection; build: repository method's connection source) against `MethodPlanner.DatabaseOf`.
- **The `gp-` sections** against `GeneratorParameterList.cs`: names, values, defaults, outputs `repository` first.
- **What the package adds**: three attributes, three exceptions and `Microsoft.CodeAnalysis.EmbeddedAttribute`, owned by `#what-the-package-adds`, restated by `#attribute`.

---

### Task 1: The ledger, and `settings-file.md`

**Files:** Create `$E/ledger/phase-14.tsv`; modify `site/reference/settings-file.md`; `$E/merges/MP-10.md` under `## Settings file`.

- [ ] Write the ledger's first two lines: `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f` and the header `unit	disposition	to	arg	words	note`.
- [ ] Make the five bold lead-ins (`file-name`, `file-contents`, `files-of-a-sql-file`, `which-files-are-checked`, `settings-file-in-the-build`) each an h3 under its existing anchor, in reader's words; no new `<a id>`.
- [ ] Merge the term "Settings file" (`concepts.md` 55) into the sentence after the sample of `#settings-files`: a file named `sqlsource.json`, in any folder, for the `.sql` files of that folder and the folders below it.  Fill the worksheet's facts, conflicts and `### The merged text` under `## Settings file` only.
- [ ] Rewrite `#which-files-are-checked` (310 words, ten sentences) as rules: which C# files reach a settings file, the symbolic-link exception, what a broken file above the project does; keep "which only the tool checks" (C-01, check `MethodPlanner.cs` `DatabaseOf`).
- [ ] Rewrite `#settings-file-in-the-build`: keep "The project file holds no key of a settings file: an MSBuild property or item metadata named after one is not read" and that nothing reports such a property; cut the list of five properties to a restatement linking `msbuild.md#properties` (`restated-from`), after checking that table holds all five (`src/SqlSource/build/SqlSource.props`, `SqlSource.targets`).  "Not a key of a settings file" stays.
- [ ] Check `#file-name` against `tests/SqlSource.Tests/Generator/SettingsFileTests.cs` and `#file-contents` against `schemas/settings-v1.schema.json` and `src/SqlSource/SettingsFiles/SettingsFileKeys.cs`.
- [ ] Write `limit-symbolic-link` in three parts: **What you see.** / **Why.** / **What to do.**; heading unchanged.
- [ ] Rows for every unit; run `$E/check/check.sh rewrite 14` and account for each printed line for this page; the anchor, fence and limit loops of guide section 10 for this page; `git grep -n -i -E "holds no setting|only the tool reads" -- site/reference/settings-file.md` prints nothing.
- [ ] `./format.sh`; `./pre-commit-validation.sh`; commit `docs: rewrite the settings-file page in plain language`.

### Task 2: `generator-parameters.md`

**Files:** modify `site/reference/generator-parameters.md`; `$E/merges/MP-10.md` under `## Generator parameter`; ledger rows.

- [ ] Merge the term "Generator parameter" into the sentence after the sample of `#setting-generator-parameters`: an option that changes what SqlSource does with a query; not a query parameter, with a link to `#three-kinds-of-parameter`.  Fill the worksheet under `## Generator parameter` only.
- [ ] Rewrite the text on places (where a generator parameter can be written, and its precedence) as rules.
- [ ] Give each `gp-` section the order of a setting: an example where one helps (as a sentence: no new block), the values, the default, the effect, its errors.  No fact of the Effect cell is cut; `gp-array-pool` is the only home of its facts.  Where a section names outputs (`gp-collection-method`, `gp-method-type`), `repository` first.  Check names, values and defaults against `src/SqlSource/Settings/GeneratorParameters.cs`, `GeneratorParameterList.cs` and `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`.
- [ ] Leave the parameter table's first column and the h3 headings as `SettingsReferenceTests` and `DiagnosticsDocumentTests` read them.
- [ ] Rows; `check.sh rewrite 14`; the loops; `./format.sh`; `./pre-commit-validation.sh`; commit `docs: rewrite the generator-parameters page in plain language`.

### Task 3: `attributes.md`, with MP-07

**Files:** modify `site/reference/attributes.md`; `$E/merges/MP-07.md`; ledger rows.

- [ ] MP-07: both headings stay.  `#setting-source` owns the rules of `Source` (which files a type claims; SQLSRC004, SQLSRC005, SQLSRC013).  `#files-of-a-type` owns what happens to the files (one set of names, two types that claim one file, a file with an error).  "Two types can claim one file" and "a file no type claims is ignored" each stand once, the other heading restating and linking.  Settle conflicts by `src/SqlSource/Generation/TargetTypeReader.cs` and `src/SqlSource/Generation/Paths/`.  Write the worksheet's facts, conflicts and `## The merged text`; `merge-pair` rows with `arg` `MP-07`.
- [ ] `#what-the-package-adds` owns the list (three attributes, three exceptions, `Microsoft.CodeAnalysis.EmbeddedAttribute`; check `src/SqlSource/Generation/AttributeSource.cs`, `src/SqlSource/Generation/Exceptions/ExceptionSource.cs`); `#attribute` restates it in one sentence and links, with a `restated-from` row whose `arg` is `site/reference/attributes.md#what-the-package-adds` and notes on both units recording the join.
- [ ] Write `limit-source-written-as-text` and `limit-embedded-attribute` in three parts; headings unchanged.
- [ ] Rows; `check.sh rewrite 14`; the loops; `grep -c '^## '` is 6; `./format.sh`; `./pre-commit-validation.sh`; commit `docs: rewrite the attributes page and merge MP-07`.

### Task 4: `settings.md`

**Files:** modify `site/reference/settings.md`; `$E/merges/MP-10.md` under `## Output`, `## Dialect`, `## Logical database`; ledger rows.

- [ ] `#places` and `#all-settings`: rewrite the prose only; leave both tables.
- [ ] `#precedence`: four rules then the three exceptions as bullets, each one rule; the bullet on two types that claim one file split into its rule, its reason, the two things that can still differ, and what to do.  The sentence that names the matrix names one table and keeps its link.  Check against `src/SqlSource/Settings/SettingsLevelReader.cs`, `QuerySettings.cs` and `tests/SqlSource.Tests/Generator/SettingLevelTests.cs`, `TypeOverrideLevelTests.cs`, `GeneratorParameterLevelTests.cs`.
- [ ] `#a-value-that-is-wrong`: keep "of a key, or an entry, written twice, the first applies" (check `src/SqlSource/SettingsFiles/SettingsFileReader.cs`); name the right reporter for each error.
- [ ] `#setting-output`: merge the term (what SqlSource produces for a query; it decides which queries must be described; values `repository`, `methods`, `models`, `sql`) after the sample; the table's rows in that order, each true alone; beside the table, when to set each of the other three in the owner's words; keep "which queries must be described" and SQLSRC209 for a project that sets neither the dialect nor the output, whole.
- [ ] `#setting-database`: merge the term with both uses (tool: the connection it describes a query through; build: the connection source of a repository method; check `MethodPlanner.cs` `DatabaseOf`); keep "The two answer different questions" with both sentences and its example; keep the characters of a name and SQLSRC211 (check `src/SqlSource/DatabaseName.cs`, `tests/SqlSource.Tests/DatabaseNameTests.cs`).
- [ ] `#setting-dialect`: after the sample, "Set it to your database: `postgres` or `mssql`."; every other name goes to `dialects.md`; merge the term (the rules by which SqlSource reads a `.sql` file; it connects to nothing).  The sample keeps its `mysql` block.
- [ ] Fill the three MP-10 worksheet sections.  Rows; `check.sh rewrite 14`; the loops; the `awk` of the spec's "Done when" prints the `repository` row; `./format.sh`; `./pre-commit-validation.sh`; commit `docs: rewrite the settings page and merge three terms`.

### Task 5: `msbuild.md`, with MP-06

**Files:** modify `site/reference/msbuild.md`; `$E/merges/MP-06.md`; ledger rows.

- [ ] MP-06: both headings stay.  `#online-build` owns what turns the tool on: the three values of `SqlSourceOnline`, what `auto` looks for, the builds in CI, where the property is written, and the five caveats of the spec, each in the merged text.  `#running-in-the-build` owns what the build then does (which builds, when it runs again, what it describes, the errors, a build offline, several target frameworks, what it prints, the logs); its "What turns it on" becomes two sentences and a link to `#online-build`.  Keep "only the tool checks that value" (C-01).  Settle by `src/SqlSource/build/SqlSource.targets` (`SqlSourceOnlineValue`, `SqlSourceEnvFileValue`, the condition on `CI`).  Keep "This was checked in Rider on macOS" and the three not checked, and the advice to set `SqlSourceOnline` `true` in your own environment with its reason.  The `console` block is byte-identical.
- [ ] Write the worksheet's facts, conflicts and `## The merged text`; `merge-pair` rows with `arg` `MP-06`.
- [ ] Rewrite `#properties` prose (leave the table), `#build-requirements` (.NET 8, C# 12, .NET SDK 9.0.300, Visual Studio 2022 17.14, SQLSRC021: check the `CompilerApiVersion` check in `SqlSource.targets`), `#included-files`, `#setting-env-file`.
- [ ] Write `limit-file-added-by-a-target` in three parts.
- [ ] Rows; `check.sh rewrite 14`; the loops; `grep -c '^## '` is 7; report the word count (clean cut if over 3,500: `running-in-the-build` with `online-build` and `setting-env-file`); `./format.sh`; `./pre-commit-validation.sh`; commit `docs: rewrite the msbuild page and merge MP-06`.

### Task 6: The checks of the phase

- [ ] Run every check of the rewrite guide's section 10 over the five pages: `check.sh rewrite 14` and `coverage` exit 0; only the phase's files changed; the anchor, fence and limit loops; the worksheets (`grep -L '^## The merged text'` on MP-06 and MP-07 prints nothing; the MP-10 `awk` prints the five terms); the internals grep of `site/AGENTS.md`; the status and output-order searches; the spec's "Done when" greps.
- [ ] Write the word counts, old and new, of each page as a `#` comment line of the ledger.
- [ ] `tools/build-site.sh` exits 0; `dotnet test --project tests/SqlSource.Repository.Tests` passes.
- [ ] Then the outline's review order: the two Opus content reviewers (no-loss, accuracy) with the reviewer's kit, `/branch-review:review --base origin/main` to clean, `./pre-commit-validation.sh --full`.
