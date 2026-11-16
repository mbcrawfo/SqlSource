# Docs overhaul, phase 12: rewrite-guides - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The moved text of 18 guides is in plain language and the fixed shape of a guide, the merge pairs MP-12 and MP-14 to MP-22 are merged, and no fact of the old text is lost.

**Architecture:** One task for each group of pages, each rewriting its pages, finishing the worksheets of its merge pairs and appending its rows to one ledger, `ledger/phase-12.tsv`.  A last task runs the checks of a rewrite over the whole phase.  The content reviews, `branch-review:review` and the full validation follow, in the outline's order.

**Tech Stack:** Markdown pages built by docfx 2.81; the epic's checker `check/check.sh` (.NET file-based app); `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-12-rewrite-guides-design.md`, with the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the content map and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`.  Order of authority: outline, map, the phase document, the rewrite guide.

## Global Constraints

- Change only the 18 pages of the phase document, `ledger/phase-12.tsv` and the worksheets `merges/MP-12.md`, `MP-14.md` to `MP-22.md`.  No `toc.yml`, `AGENTS.md`, test, example or other page.
- No `<a id>` line is added or removed.  Phase 12's pairs land on a page, so no heading is lifted.
- Every fenced block stays byte-identical.  New blocks only from the table "The samples this phase can add", copied byte for byte, each with a `new` ledger row naming its source.
- Text that phase 9 or 10 wrote (`new` in their ledgers) is not reworded, unless a merge or the shape needs it, or it is false (`corrected` row).
- The shape: one opening sentence with the map's search words, then a paragraph that starts `**Use this when**`; in each section the sample, `**What you get.**`, `**The rules you need.**`; `## If it does not work` last, with no `<a id>`.  On `dapper.md` alone `## Limits` follows it and ends the page, and its limit has `**What you see.**`, `**Why.**`, `**What to do.**`.
- `troubleshooting.md` gets no opening and no `## If it does not work`; only `#invalidcastexception` is rewritten, as entries by the message the reader sees.
- Language: section 3 of the rewrite guide, the term table of 3.3, outputs in the order `repository`, `methods`, `models`, `sql`, no status words, no internals.
- A fact whose owner (a page of another phase) lacks it stays on the page, with a ledger note that opens `no owner: <file>#<anchor>; <fact>`.
- Commits: `./pre-commit-validation.sh` passes before each commit; each message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A sentence split into a list that turns "all of these" into "one of these", or loses "only", "not", "never", "unless", "alone", "at most", "at least", "except".
- A quoted message (`InvalidCastException`, "Column 'name' is null.", "Data is Null.", the tool's console output) changed by a letter.
- A restatement that drops a fact its owner lacks, with no `no owner:` note.
- A merged text that drops a fact listed in its worksheet's "The facts".
- A "below", "above" or "this page" that no longer points where it says, after the shape reorders a section.

---

## The ledger

`docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-12.tsv`.  Task 1 creates it with `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f` and the header `unit	disposition	to	arg	words	note`; each task appends the rows of its pages, grouped by page.  Six tab-separated cells, never empty (`-`).  The dispositions of a rewrite are in section 10 of the rewrite guide:

- `move` with `arg` `fact`: the unit's facts stay on its page in new words.
- `merge-pair` with `arg` `MP-<nn>`: each unit of a merge pair's texts.
- `restated-from` with `arg` the owner `<file>#<anchor>`.
- `corrected`: `words` holds the new words; the note names the source.
- `new`: `unit` `-`; an opening, a "Use this when", a "What you get", a "The rules you need", an `## If it does not work` and its entries, a copied sample (note names the file it was copied from), a restatement of README 244 or 250 (note names the unit restated).
- A unit is named by its baseline id from `check.sh origin <page>`, or by `@<file>:<line>` at the base when it is text an earlier phase mended (look it up in `ledger/phase-0{5,6,7,8}.tsv` by its `position` or `@` row).

## Each page task's steps

Each of Tasks 1 to 6 runs these steps for each of its pages, then commits once.

- [ ] **Read** the page, its entry in section 3 of the map ("Guide pages"), its worksheet(s), `check.sh origin site/guides/<page>.md`, and the rows for the page in `ledger/phase-08.tsv` and `ledger/phase-09.tsv` (which text is old, which phase 9 wrote).
- [ ] **Merge** (pairs only): fill "The facts", "Conflicts" (settle each by the source the phase document names) and "The merged text" of the worksheet; the merged text is on the page.
- [ ] **Rewrite** each section of old text: shape, merge, restatements (read the owner at the base; a fact it lacks stays, with `no owner:`), language, reframings.
- [ ] **Write** the opening and `**Use this when**` if the page has none, and the shape's missing parts.
- [ ] **Search** the page: the internals `grep` of `site/AGENTS.md`, and both searches of section 10 of the rewrite guide (status words, output order).
- [ ] **Ledger**: append the page's rows.  Run `check.sh rewrite 12`; it exits 0 only when every page done so far is accounted for; read each line it prints for your pages and make sure a note accounts for it.
- [ ] **Build and test**: `tools/build-site.sh` exits 0; `dotnet test --project tests/SqlSource.Repository.Tests` passes.
- [ ] **Commit** after `./pre-commit-validation.sh` passes: `git add` the pages, the ledger and the worksheets; message `docs: rewrite <pages> in plain language and the guide shape`.

### Task 1: `dapper.md` (MP-12, MP-22, the limit)

**Files:** Modify `site/guides/dapper.md`, `merges/MP-12.md`, `merges/MP-22.md`; create the ledger.

- `#models` and `#sql-only` say what `models` and `sql` are for (a project where Dapper or your own ADO.NET code runs the SQL), worded as tasks.
- MP-22 (`#models`, then `#dapper-and-adonet`): the moved text under the walkthrough names `Sql.GetUser`, `Sql.FindUsers`, `GetUserDto`, `FindUsersDto`, `GetUserParams(id)`, and step 1 of the walkthrough makes `GetUser`, `UsersDto`, `userId` from `Models/Users.sql` (phase 9's AC-1).  The fenced blocks stay byte-identical: reword the prose so that it is true beside both, and correct a false sentence with a `corrected` row.  "Add `output` to the `sqlsource.json` of step 3" is redundant beside the walkthrough: keep its fact once.
- MP-12 (`#models-through-dapper`, and `#limit-parameter-names`): README 163, the recipe 643-702 (the 68-word sentence) and the limit.  Settle by the two `DapperRecipeTests.cs`.
- The two database sections are h2; "below" must still point down the page.
- Add `## If it does not work` before `## Limits`; write the limit in three parts (`grep -c -F '**What you see.**' site/guides/dapper.md` prints 1).
- Facts at risk: `DefaultTypeMap.MatchNamesWithUnderscores`; Dapper matches a parameter "ignoring case but not `_`"; a `datetime2` parameter on SQL Server.  Check against both `DapperRecipeTests.cs` and `examples/*/DapperSetup.cs`.
- Four blocks have twins in `tests/DatabaseTests/*/EndToEnd/` and `examples/`: byte-identical.

### Task 2: `report-a-bug.md` (MP-19) and `troubleshooting.md#invalidcastexception`

**Files:** Modify `site/guides/report-a-bug.md`, `site/guides/troubleshooting.md`, `merges/MP-19.md`.

- MP-19 lands on `#report-a-bug`; settle by `src/SqlSource.Tool/DiagnoseCommand.cs`.  Phase 9's `#what-is-safe` stays as written.
- `#raw-bundle` becomes numbered steps; "do not attach" stays as strong.  SQLSRC293 and SQLSRC294 against `src/SqlSource/Diagnostics/ToolDiagnostics.cs`; what a bundle renames, and that you keep the mapping, against `src/SqlSource.Tool/Bundles/Renaming/` and `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`.
- The console blocks are byte-identical: "will" stays there.
- `troubleshooting.md`: rewrite `#invalidcastexception` alone, as entries keyed by the message the reader sees, each with its cause and a link to the owner of its rule.  The messages letter for letter, checked against `src/SqlSource/Generation/Emitting/QueryMethods/RowReaderWriter.cs`: "Column 'name' is null.", "holds a null element.", "holds a JSON null.", SQL Server's "Data is Null.".

### Task 3: Types: `json.md` (MP-20), `custom-types.md` (MP-21), `nodatime-and-spatial.md`

**Files:** Modify the three pages, `merges/MP-20.md`, `merges/MP-21.md`.

- MP-20 on `json.md`, settled by `src/SqlSource/Generation/Converters/JsonConversions.cs` and `tests/DatabaseTests/Postgres/EndToEnd/Json/`.  New sample for `#turn-json-on`: the `libraryTypes` of `examples/SqlServerExamples/sqlsource.json`.
- MP-21 on `custom-types.md`, settled by `src/SqlSource/Settings/TypeOverrides/` and `src/SqlSource/Generation/Converters/ConverterReader.cs`.  `#enum` keeps its two databases in one h2 (map: one anchor), as two bold-labelled parts (phase 9's AC-6): leave it as phase 9 wrote it.
- `nodatime-and-spatial.md`: phase 9 wrote nothing; every section gets the shape.  New samples: PostgreSQL sections from `examples/PostgresExamples/Methods/DeliveryQueries.sql` and `StoreDataSource.cs`; SQL Server sections from `examples/SqlServerExamples/sqlsource.json` and `Methods/DeliveryQueries.sql`; `#ef-core-and-dapper` from `examples/PostgresExamples/DapperSetup.cs`.  Copy a part byte for byte; a `sql` block's info string is `sql postgres` or `sql mssql`.
- README 244's third sentence ("On both, a type override gives a column a shape") stays under `#nts-sql-server` with the closing "See" sentence; restate it in one sentence under `#nts-postgresql` with that section's own database link (`new` row naming the unit).  README 250 stays at the foot; it may be restated on `json.md` (`new` row).
- Facts at risk: NetTopologySuite 2.5.0 or later "by name" on SQL Server; a library is on by reference on PostgreSQL and off on SQL Server.  Check against `src/SqlSource/Settings/Libraries/`, `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs` and the section SQLSRC282.

### Task 4: Projects: `several-databases.md` (MP-14), `folders-and-files.md` (MP-15), `several-projects.md` (MP-18)

**Files:** Modify the three pages, `merges/MP-14.md`, `MP-15.md`, `MP-18.md`.

- MP-14: settle by `src/SqlSource/Generation/Methods/MethodPlanner.cs`: the build reads the database setting too (C-01).  `#folder-with-another-dialect` sets a dialect; it does not "choose" one.
- MP-15 on `#leave-migration-scripts-out`, settled by `SqlSourceIncludeFiles` in `src/SqlSource/build/SqlSource.props`.
- MP-18: settle by `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs`; the block of `#share-the-exceptions` has a twin there and stays byte-identical.

### Task 5: Workflow: `describe-on-save.md`, `ci.md`

**Files:** Modify the two pages.

- `describe-on-save.md#vs-code`: the task block is read by `EditorRecipeTests`; it stays outside any tab, under its h2.  The three support facts stay: "This recipe has not been checked in VS Code", "Checked in Rider 2026.2 on macOS", "This has not been checked in Visual Studio" (reword around them only if the words stay findable; keep them verbatim if unsure).  Rider's field is "Working directory".
- `ci.md`: `--check` ends with the exit code `2`; a build in CI never describes.  Check against `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs` and `src/SqlSource/build/SqlSource.targets`; each database's job against its own tests.

### Task 6: Queries, Running queries and other databases: `lists.md` (MP-17), `dynamic-sql.md` (MP-16), `many-rows.md`, `sync-and-streaming.md`, `repository-connection.md`, `naming-and-placement.md`, `other-databases.md`

**Files:** Modify the seven pages, `merges/MP-16.md`, `MP-17.md`.

- MP-17 on `lists.md`, settled by `src/SqlSource/Parsing/SqlListUses.cs` and `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs`.  New sample for `#list-after-in`: `ListOrdersByStatuses` of `examples/PostgresExamples/Methods/OrderQueries.sql`, and its call in `OrderQueries.cs`.
- MP-16 on `#table-name-at-run-time`, settled by `src/SqlSource/Parsing/Tokens/` and `tests/SqlSource.Tests/Generator/TokenValidationTests.cs`.  The moved text above phase 9's `#clause` restates rules the new section states: keep each fact once.
- `naming-and-placement.md#model-namespace-task`: new sample, the sample of `reference/models.md#setting-model-namespace`.
- `other-databases.md`: the bullet "Any dialect works" keeps `mysql` and `sqlite` as examples, and says "set".

### Task 7: The checks of the phase

**Files:** Modify `ledger/phase-12.tsv` (word counts comment).

- [ ] `check.sh rewrite 12` and `check.sh coverage` exit 0.
- [ ] `git diff --name-only db3285d4` names only the 18 pages, the ledger, the ten worksheets and this plan.
- [ ] The anchors loop, the fenced-block loop (counts grew only by the listed copies), and the limits loop of section 10 of the rewrite guide.
- [ ] `grep -L -F '**Use this when**' site/guides/*.md` prints `site/guides/AGENTS.md` and `site/guides/troubleshooting.md` alone.
- [ ] `grep -c '^## The merged text' merges/MP-{12,14,15,16,17,18,19,20,21,22}.md` prints 1 for each.
- [ ] The word counts of each page, old and new, from `check.sh rewrite 12`, in a `#` comment line of the ledger.
- [ ] `tools/build-site.sh`, `dotnet test --project tests/SqlSource.Repository.Tests`, `./pre-commit-validation.sh`; commit.

Then: the no-loss and accuracy content reviews (two Opus subagents that did not write the phase), `/branch-review:review --base origin/main` to clean, `./pre-commit-validation.sh --full`.  A fix reruns the later steps.
