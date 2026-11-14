# Docs overhaul, phase 11: readmes - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut `README.md` and `src/SqlSource.Tool/README.md` to the outlines of the content map's section 7, land `ReadmeTests` and the `README.md` restatement of `SupportedVersionsTests`, and give the readme rules their final words.

**Architecture:** Documentation and one test class.  The readmes restate the site (`site/index.md`, `site/start/*.md`, the reference owners) and link it; the ledger `ledger/phase-11.tsv` gives every readme unit that leaves or stays its last disposition.  `ReadmeTests` reads the readmes and `site/start/install.md` through `UserDocs`.

**Tech Stack:** Markdown, xUnit + Shouldly (.NET), the epic's checker (`check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-11-readmes-design.md`; the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map's sections 4 and 7 win over it.

## Where the merged code differs from the spec (follow the code)

- **No `--prerelease`, no sentence about previews** (owner, p01 Q1 and Q4).  The spec's `## Install` sentence "The packages are published as previews…", its `--prerelease` rule row and its "Done when" count of 3 and 2 are replaced: `grep -c -e '--prerelease'` prints 0 for both readmes.  The badge stays `nuget/vpre`.
- **`docs/publishing.md` step 3** already only switches the badge (p01).  The spec's added sentence ("so the flag leaves all three in one commit") has no flag to speak of, so step 3 is left as it is.  The step's file list no longer exists; `git grep -- --prerelease` is checked to find nothing in user docs.
- **Root rule 4's bullet on commands** already says "no `--prerelease`, and no sentence about previews" (p01); it stays.
- **The sample pair** is that of `site/index.md` today: the five-line `GetUser` query with `@userId` and `-- summary: One user, or null.`, and the three C# lines.
- `## Install` also names the driver commands `dotnet add package Npgsql` and `dotnet add package Microsoft.Data.SqlClient` in prose, as `site/start/install.md` has them: the README's sample needs the driver, and `ReadmeTests` holds them.

## Global Constraints

- `README.md`: six headings, in order: `# SqlSource`, `## Supported databases`, `## Install`, `## Use`, `## Other ways to use it`, `## Documentation`.
- `src/SqlSource.Tool/README.md`: four headings: `# SqlSource.Tool`, `## Install`, `## Use`, `## Documentation`.
- Every link absolute; site links `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html[#anchor]`, or the root with no anchor; repository links `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`.  No raw HTML, no table, no link to a heading of its own, no fenced block in a list.
- Plain language: at most 20 words in a step, 25 in a description.  No "step N", "FAQ", "Getting started", "Where to next", "Only want", "Just want".
- "PostgreSQL 14 or later", "SQL Server 2017 or later", "Azure SQL is accepted, and not tested" (never "supported").  No driver version.
- `ReadmeTests` caps in bytes: 8,000 (`README.md`) and 4,000 (tool readme), each between 1.15 and 1.5 times its file, else 1.25 times rounded up to the next 500.
- Root `AGENTS.md` < 16,384 bytes; other `AGENTS.md` < 8,192; bullets ≤ 80 words.

## Review Focus

- A command written in prose (an inline code span) in a readme that differs from `install.md`: `ReadmeTests` must read inline spans, not only fenced blocks.  Covered by a reading test of made-up text.
- A command with a `$ ` prompt or doubled spaces: normalised before comparing.  Covered by the reading test.
- A readme whose commands are all lost by a reader bug: the guard (at least 3 and 2) fails the test.
- A command inside a docfx tab of `install.md`: counts, since tabs hold ordinary fenced blocks.  Covered by the real files (Npgsql is in a tab).
- A non-install code span such as `dotnet sqlsource describe`: ignored.  Covered by the reading test.

---

### Task 1: `ReadmeTests` and the `README.md` restatement

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocs.cs` (add `CodeSpanList` to `Document`, filled with each inline code span's text outside fenced blocks, and `public static IEnumerable<(int Line, string Text)> CodeSpans(string file)`; a `BlocksOf(string file)` accessor for one file's fenced blocks)
- Create: `tests/SqlSource.Repository.Tests/Documentation/ReadmeTests.cs`
- Modify: `tests/SqlSource.Repository.Tests/Documentation/SupportedVersionsTests.cs` (`[InlineData("README.md")]` on `Restatement_OfTheMinimumServerVersions_IsTheTables`)

**Interfaces:**
- Produces: `ReadmeTests.Commands(IEnumerable<(string Info, string Text)> blocks, IEnumerable<string> spans) -> List<string>`, the normalised install and update commands.

- [ ] **Step 1: Tests.**  `Size_OfEachReadme_IsUnderItsCap` (theory: `README.md`/8000, tool/4000; message says to move text to the site and link it).  `InstallCommands_OfEachReadme_AreOnTheInstallPage` (theory: `README.md`/3, tool/2 minimum; each command of the readme is among `install.md`'s).  `Commands_OfMadeUpText_AreTheInstallAndUpdateCommands`: a `console` block `$ dotnet add package SqlSource` and `$  dotnet  tool install   SqlSource.Tool`, a `csharp` block holding `dotnet tool restore` (ignored), spans `dotnet add package Npgsql`, `dotnet sqlsource describe` (ignored), ` dotnet tool update SqlSource.Tool ` → `["dotnet add package SqlSource", "dotnet tool install SqlSource.Tool", "dotnet add package Npgsql", "dotnet tool update SqlSource.Tool"]`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Repository.Tests --filter "FullyQualifiedName~ReadmeTests|FullyQualifiedName~SupportedVersionsTests"`; the size test fails on today's `README.md` (42 KB), the restatement test fails on `README.md` only if it lacks the minimums.
- [ ] **Step 3: Implement** the reading: a command is a line of a block whose info string's first word is `console`, `bash`, `sh` or `shell`, or a span; strip a leading `$ `, trim, collapse spaces; keep it when it starts with `dotnet add package `, `dotnet new tool-manifest`, `dotnet tool install `, `dotnet tool update ` or `dotnet tool restore`.
- [ ] **Step 4: See each new test fail** (after Task 2): add `--version 1.0.0` to one `README.md` command; lower a cap below its file; change 2017 to 2016 in `README.md`.  Restore each.
- [ ] **Step 5: Commit** with Task 2 (the size test fails until the cut).

### Task 2: `README.md`

**Files:** Modify `README.md`.

- [ ] **Step 1:** Write the file to the spec's step 2 tables: the badges of line 3 unchanged; one paragraph of four sentences restating `site/index.md`, linking the site's root; the sample pair byte for byte from `site/index.md`, then a link to `start/first-query.html`; `## Supported databases` (three bullets, links `reference/databases/index.html#supported-versions`, `guides/other-databases.html`); `## Install` (the add command block, the driver commands in prose, the tool block, the requirements line, links `start/install.html`, `reference/msbuild.html#build-requirements`); `## Use` (five numbered steps in the order of `first-query.md`, anchors `#write-a-query`, `#mark-a-partial-class`, `#name-your-database`, `#connection` + `#describe`, `#call-the-methods`, then "The full walkthrough"); `## Other ways to use it` (`methods` first for a connection, transaction or timeout; `models` and `sql` as fallbacks); `## Documentation` (five list lines); line 203 unchanged.
- [ ] **Step 2:** Run the spec's "Done when" greps for `README.md`, and `diff` the two fenced blocks against `site/index.md`'s.
- [ ] **Step 3:** `dotnet test tests/SqlSource.Repository.Tests`: passes.  Commit Tasks 1 and 2.

### Task 3: `src/SqlSource.Tool/README.md`

- [ ] **Step 1:** Write it to the spec's step 3 table: three sentences linking the SqlSource readme and `reference/describing.html#describe`; `## Install` (one sentence, the two commands, link `reference/cli.html#install-the-tool`); `## Use` (the `.env` block of line 19, the folder sentence, the four `describe` commands, four one-line bullets, the build sentence, one line for `diagnose` and `replay`; links `reference/connections.html#env-file`, `reference/describing.html`, `guides/describe-on-save.html`, `reference/msbuild.html#running-in-the-build`, `guides/report-a-bug.html`); `## Documentation` (five links).
- [ ] **Step 2:** Greps of "Done when"; set the caps by `wc -c`; run the tests; commit.

### Task 4: The rules

- Root `AGENTS.md` rule 4: the three bullets of the spec's step 7, word for word.
- `site/AGENTS.md`: the `README.md` bullet becomes "**The two packed readmes** hold the outlines of root rule 4, and state nothing that a page of this folder does not own."; the "Database that can be described" row's "the README's list" stays true (the README lists the databases).
- `site/start/AGENTS.md`: the `SupportedVersionsTests` bullet also names `README.md`, and gains the `ReadmeTests` sentence.
- `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`: `ReadmeTests` and the `README.md` restatement; the readme-link bullet already gives the folder form.
- `tests/README.md`: the size and the commands of the two readmes in the row of `tests/SqlSource.Repository.Tests`.
- Grep the repository but `docs/superpowers` for "FAQ", "five steps", "Getting started", "Where to next", and old `README.md#` anchors; rewrite each hit that is false.
- Check sizes; run `AgentsFileTests`; commit.

### Task 5: The ledger

**Files:** Create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-11.tsv`, `# base ff35b6b7394010c819c8d3487a98d54186a89ccc`.

Rows, by the spec's "The ledger":
- What stays: `README.md:3` move; `README.md:5` move-split into four `move` parts (rewritten here; `fact`), the rest restated on `site/index.md`; `README.md:15`, `README.md:21` (block, as phase 1 corrected it) move; `README.md:305` move.
- Owner only: `@`-rows for README 101 s7, 126 s5 and s10 as they stand at the base, `restated-from` their owners with `to` `-`.
- Navigation: README 9 s3-5 (the base's `@README.md:7/3`), the `More:` lines that phase 10 left in the README, the rows 292-303: `move`/`restated-from` with the note "navigation".
- Each heading that goes: `move-split`, heading only.
- Each phase-8 pointer: `@README.md:<line>` rows, removed, with their phase-8 row named.
- Every row of the map of the tool readme but line 7 sentence 2.
- One `new` row for each section of both new files.
- Each fenced block that changes or goes.
- Run `check.sh coverage` and `check.sh rewrite 11`; every unit and section it lists has a row; commit.

### Task 6: Checks and reviews

- `dotnet pack SqlSource.slnx --output artifacts/packages`; `tools/check-package.sh artifacts/packages`; `unzip -p … README.md | diff` for both packages.
- `tools/build-site.sh` with no warning.
- `./format.sh`, `./pre-commit-validation.sh`.
- The two content reviewers (Opus subagents: no-loss, accuracy), fixes; then `/branch-review:review --base origin/main` to clean; then `./pre-commit-validation.sh --full`.
- `p11-notes.md` in the shared notes folder.
