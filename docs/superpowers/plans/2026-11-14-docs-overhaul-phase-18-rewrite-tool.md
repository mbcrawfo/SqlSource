# Docs overhaul phase 18 (rewrite-tool) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The five pages of the group "The sqlsource tool" are in plain language and in the shape of a reference page; MP-08 and MP-11 are merged; the terms Tool, Describe and Sidecar of MP-10 are merged; every fact about what the tool hides, writes and stops on is as exact as before.

**Architecture:** One task for each page, in the order of the sidebar, each a rewrite of the words only (no anchor, no link target, no fenced block but the one MP-11 removes).  Each task adds its rows to `ledger/phase-18.tsv` and passes `check.sh rewrite 18` for its page before it commits.  A last task counts the words and runs every check of section 10 of the rewrite guide.

**Tech Stack:** Markdown (docfx), the epic's checker (`check/check.sh`, .NET 10 file-based app), `tools/build-site.sh`, `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-18-rewrite-tool-design.md`, with the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`, which wins on nothing the brief settles.  Read both whole before a task.

## Global Constraints

`E` is `docs/superpowers/specs/2026-11-12-docs-overhaul`.  The base is `db3285d4adf7542f6e80e96d7f0b8182cd091a9f` (`origin/main` at the start).

- Change only: `site/reference/{cli,describing,connections,sidecars,bundles}.md`, `$E/ledger/phase-18.tsv`, `$E/merges/MP-08.md`, `$E/merges/MP-11.md`, and the h2 sections Sidecar, Tool and Describe of `$E/merges/MP-10.md`.  No `toc.yml`, `AGENTS.md`, test, example or other page.
- No `<a id>` line added, removed or renamed; no link target changed; no h1 changed; heading order and levels stay.
- Every fenced block byte-identical, but the README's copy of the two install commands in `cli.md#install-the-tool` (MP-11), which goes with a ledger row that names it.
- **Where the code differs from the brief (owner rulings):**
  - **No `--prerelease`** anywhere (owner, p01 Q1/Q4; `site/AGENTS.md`).  The brief's line 43 and its Done-when `grep -c -- "--prerelease" ... prints 2 or more` are void: the check is that it prints `0`.
  - **SQLSRC209 (owner, p10 Q2):** `describing.md#which-queries` says a project that sets neither "gets it for every file".  SQLSRC209 is reported only for a `.sql` file that a class reads: narrow to "each file that a class reads", with a `corrected` row whose note names the owner's ruling and the source.
- The language rule of section 3 of the rewrite guide; the term table of 3.3 (*current*/*out of date*, *describes*, *settings file*, *folder*, *claims*).
- Outputs listed `repository`, `methods`, `models`, `sql` in every list, partial lists too, but text the tool prints.
- No status words; support facts stay ("checked with Rider on macOS alone", "This has not been checked", `limit-saves-not-seen`, `limit-editors-not-checked`).
- Write effects, never internals: the `grep` of `site/AGENTS.md` on each page; `sidecars.md` keeps its one schema link and names no field.
- A limit names no tech-debt or deferred id, and has **What you see.** / **Why.** / **What to do.**
- A fact whose owner is on another phase's page and that the owner lacks stays here, with a `no owner:` note (rewrite guide section 8).
- Before rewording a heading: `git grep -n -F '"<the heading>"' -- . ':!site' ':!docs/superpowers'`; if quoted, keep it.
- Ledger rows: six tab-separated columns `unit disposition to arg words note`, `-` for nothing; old units named `site/tool.md:<line>` etc. as `check.sh origin` prints them; text not in the inventory named `@site/reference/<page>.md:<line>` at the base.

## Review Focus

- A secret shown as hidden: "whole", "alone", "shorter than four" lost in `cli.md#logs` or `connections.md#limit-short-password`.  Pinned by Task 1 and 3 checks against `Redaction.cs`, `ConnectionSecret.cs`, `SecretTests.cs`.
- A connection used when it should not be: "only" and "exactly one" of `SQLSOURCE_CONNECTION`; the variable-name rule (upper case, every other character rewritten) rounded.  Task 3 checks `ConnectionVariables.cs`, `ConnectionSources.cs`.
- A merge step out of order in `sidecars.md#sidecars-in-git`.  Task 4 compares each procedure's commands and order with the old paragraph.
- A watch trigger dropped or invented in `describing.md#describe-on-save`.  Task 2 checks the vertical list group by group against `WatchSet.cs`, `RepositoryFolders.cs`.
- A limit whose "what to do" cannot be done today.  Each task checks its limits.

---

### Task 1: `cli.md`, MP-11 and the term Tool

**Files:**
- Modify: `site/reference/cli.md`, `$E/merges/MP-11.md`, `$E/merges/MP-10.md` (h2 Tool only)
- Create: `$E/ledger/phase-18.tsv`, first line `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f`, then the header `unit	disposition	to	arg	words	note`

- [ ] **Step 1:** `$E/check/check.sh origin site/reference/cli.md`; read the old text of each unit (`git show 28e20e3c:site/tool.md`) and the earlier ledgers' rows for this page (`grep -n 'reference/cli.md' $E/ledger/phase-*.tsv`).
- [ ] **Step 2:** Rewrite `#tool`: the MP-10 term merged into the section's opening, defining the tool as the `sqlsource` command of `SqlSource.Tool` that asks your database about your queries and writes the sidecars; the two paragraphs on which queries need the tool cut to a restatement of `settings.md#setting-output` and `describing.md#which-queries`, outputs in order `repository`, `methods`, `models`.
- [ ] **Step 3:** Merge MP-11 in `#install-the-tool`: one console block of the two commands (remove the README's copy), the union of facts, "a project whose queries all have the output `sql` needs no tool" kept whole, "step 5" reworded to what the step does with its link to `../start/first-query.md#describe` kept, no `--prerelease`.  Write the worksheet's "The facts", "Conflicts" (settle by `docs/publishing.md`) and "The merged text".
- [ ] **Step 4:** Rewrite `#command-reference` and its h3 sections, `#summary` and the limit.  Table rows and first columns stay (`CliReferenceTests`); the six phase-5 option rows and their lead sentence stay as they are; `--manifest`/`--online` stay "for the build's use"; `#requirements` states no version and links `databases/index.md#supported-versions`.  Check facts against `src/SqlSource.Tool/Cli.cs`, `SharedOptions.cs`, `ExitCodeTests.cs`, `TerminationTests.cs`, `Redaction.cs`, `RunSummary.cs`, `SummaryTests.cs`.
- [ ] **Step 5:** Write MP-10's Tool parts ("The facts", "Conflicts", "The merged text"); ledger rows for every unit of the page.
- [ ] **Step 6:** Verify: `$E/check/check.sh rewrite 18` prints no unaccounted line for `cli.md` (other pages not yet started fail; accept only those); `grep -c -- '--prerelease' site/reference/cli.md` → `0`; `grep -c -i 'step 5' site/reference/cli.md` → `0`; the anchors loop of guide section 10 prints nothing for the page; `dotnet test --project tests/SqlSource.Repository.Tests --filter "FullyQualifiedName~CliReferenceTests"` passes.
- [ ] **Step 7:** `./pre-commit-validation.sh`, then commit `docs: rewrite the reference of the sqlsource command in plain language`.

### Task 2: `describing.md`, the term Describe and SQLSRC209

**Files:** Modify `site/reference/describing.md`, `$E/merges/MP-10.md` (h2 Describe only), `$E/ledger/phase-18.tsv`.

- [ ] **Step 1:** `check.sh origin site/reference/describing.md`; old text `site/tool.md` 33-50, 250-351, 632, 635-637.
- [ ] **Step 2:** `#describe`: merge the term into phase 5's sentence: to ask a database what the parameters and columns of a query are, without running it, and to record the answer.
- [ ] **Step 3:** `#which-queries`: first sentences restate `settings.md#setting-output` (owner); the files and types the tool reads stay; "the tool describes the queries of `postgres` and `mssql`" (never "choose a dialect"); outputs in order; apply SQLSRC209's narrowing with a `corrected` row (source: SQLSRC209's emitter in `src/`).
- [ ] **Step 4:** `#describe-on-save`: the 118-word bullet becomes a vertical list of the six groups, each with its limit kept ("up to the root of your repository", "and still gone when the pass starts"); check every group against `src/SqlSource.Tool/Watching/WatchSet.cs`, `RepositoryFolders.cs` and `tests/SqlSource.Tool.Tests/Watching/`.  The support facts stay.  `#check-in-ci`: the sample job is `guides/ci.md`'s; what `--check` reports and that it writes nothing, by `CheckTests.cs`.  `describe`, `describe --force`, `describe --check`, `describe --watch` never shortened.
- [ ] **Step 5:** The four limits in three parts; MP-10 Describe parts; ledger rows.
- [ ] **Step 6:** Verify as Task 1 step 6 for this page (no `--prerelease`/`step 5` checks); commit `docs: rewrite the reference of describe in plain language`.

### Task 3: `connections.md`

**Files:** Modify `site/reference/connections.md`, `$E/ledger/phase-18.tsv`.

- [ ] **Step 1:** `check.sh origin`; old text `site/tool.md` 54-127, 638.
- [ ] **Step 2:** `#connections`: the order of the four sources and the variable-name rule copied exactly, `SQLSOURCE_CONNECTION` "only" with "exactly one" database; "a connection never goes in the project file or a settings file" with its reason.  Check `ConnectionSources.cs`, `ConnectionVariables.cs`, SQLSRC213/214/215.
- [ ] **Step 3:** `#env-file`: kept whole as the owner (the project's or solution's folder, never one above); `SqlSourceEnvFile` restated from `msbuild.md#setting-env-file`; format and SQLSRC285/286 by `DotEnvSearch.cs`, `DotEnvParser.cs`, `DotEnvRunTests.cs`.  Console/env blocks unchanged.
- [ ] **Step 4:** `#limit-short-password` in three parts, "shorter than four characters" kept, by `Redaction.cs`, `ConnectionSecret.cs`, `SecretTests.cs`; ledger rows.
- [ ] **Step 5:** Verify; commit `docs: rewrite the reference of connections in plain language`.

### Task 4: `sidecars.md`, MP-08 and the term Sidecar

**Files:** Modify `site/reference/sidecars.md`, `$E/merges/MP-08.md`, `$E/merges/MP-10.md` (h2 Sidecar only), `$E/ledger/phase-18.tsv`.

- [ ] **Step 1:** `check.sh origin`; old text `site/tool.md` 131-206, 634, `site/sql-files.md` 200, `site/diagnostics.md` 315.
- [ ] **Step 2:** `#sidecar-files`: merge the term: the file `Users.sql.json` beside `Users.sql`; the tool writes it, you commit it, you never edit it by hand (with the reason); the build reads it in place of a database.  One link to `schemas/sidecar-v1.schema.json`, no field named.
- [ ] **Step 3:** `#sidecars-in-git`: three numbered procedures with the common first step; commands and order checked against the old paragraphs.  `#describing-again` kept whole as the owner of "current", checked against `SqlQueryHash.cs`, `SidecarChecker.cs`, `SidecarTests.cs`, and "written whole or not at all" against `SidecarStore.cs`.
- [ ] **Step 4:** MP-08: one limit in three parts, two symptoms (entries vanish, or SQLSRC245); no tech-debt id; worksheet parts filled (settle by the open tech-debt item TD-0033); the lifted text is two bullets under one heading, so no heading is lifted.
- [ ] **Step 5:** MP-10 Sidecar parts; ledger rows; verify; commit `docs: rewrite the reference of sidecar files in plain language`.

### Task 5: `bundles.md`

**Files:** Modify `site/reference/bundles.md`, `$E/ledger/phase-18.tsv`.

- [ ] **Step 1:** `check.sh origin`; old text `site/tool.md` 394-405, 571-625.
- [ ] **Step 2:** Opening restates `guides/report-a-bug.md` for the procedure.  `#bundle-contents` in the reader's words: the query, what the database answered, what SqlSource made of the answer; each list a vertical list.  `#diagnose`, `#replay`: the control-character forms `\u001B`, `\u000A`, `\n`, `\t` exact; check `src/SqlSource.Tool/Bundles/Renaming/`, `ReplayLines.cs`, `ReplayTests.cs`, `DiagnoseTests.cs`.
- [ ] **Step 3:** Ledger rows; verify; commit `docs: rewrite the reference of bug-report bundles in plain language`.

### Task 6: Counts and the checks of a rewrite

- [ ] **Step 1:** `$E/check/check.sh rewrite 18` exits 0; `check.sh coverage` exits 0; write the per-page word counts (old, new) as a `#` line of the ledger.
- [ ] **Step 2:** Run every check of section 10's table: only our files changed (`git diff --name-only db3285d4`), anchors loop empty, fence counts equal but `cli.md` one fewer block, limits loop `1 1 1`, `4 4 4`, `1 1 1`, `1 1 1`, `0 0 0`, `grep -L '^## The merged text' $E/merges/MP-0{8,11}.md` empty (MP-11 has two digits: `MP-11.md`), the MP-10 awk prints Sidecar, Tool and Describe among its terms, the internals grep, the status search, the output-order search, `tools/build-site.sh`, `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] **Step 3:** `./format.sh`, `./pre-commit-validation.sh`, commit.
