# Query generation, phase 5.7: retire the corpus's expectations and close phase 5 - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The realistic corpus's 117 `expected-pg14.txt` and `expected-pg18.txt` files, `tools/record-realistic.sh` and the comparison with them give way to a check of each committed sidecar entry whole; the user documentation of phase 5 is read whole and made true; the epic outline closes phase 5.

**Architecture:** The new check describes each case with models, as the corpus's other checks already do through `RealisticDescribe.DescribeOnceAsync`, builds the sidecar entry that a run writes from that description with the tool's own `EntryBuilder`, and compares it with the committed `query.sql.json`'s entry as the sidecar writer writes both, with only the server's version and the key's position set equal.  So every field of the entry is compared, the provenance fields and `origin` among them, which `--check` ignores.  The rest is deletion and documents.

**Tech Stack:** C# on .NET 10, xunit v3, Shouldly, Npgsql 10, Testcontainers; Markdown.

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-7-retire-expectations-and-close-design.md`](../specs/2026-10-18-query-generation-phase-5-7-retire-expectations-and-close-design.md).  Read it, the root `AGENTS.md`, `tests/AGENTS.md`, `tools/AGENTS.md`, `docs/manual/AGENTS.md` and `tests/SqlSource.Tests.Postgres/README.md` before starting a task.

## Where this plan follows the merged code, not the spec

The spec was written before 5.3 to 5.6 merged.  A search and a spike on 2026-10-19 settled these:

1. **The whole-entry check is proven before it is planned.**  A throwaway test built each case's entry by `FixtureReplay.Plan` and `EntryBuilder.Build`, on the database `realistic`, and compared it with the committed entry, with `NameSpan` and `ServerVersion` set equal.  All 55 cases with models matched on `postgres:18.6`, and the 54 that run on 14 matched on `postgres:14.24`, `merge-returning` skipping by its `minimumMajor`.  So the provenance and `origin` that the expectations proved are the same on 14 as the sidecars hold, and no case needs `checkedFrom`.
2. **55 cases have models, not 56.**  5.6 made `two-columns-named-id` a refusal of the generator, `SQLSRC255`, by a `models` member of its `case.json`.  Like the three cases the describer refuses, it has no sidecar, and the whole-entry check skips it.  Its description on both majors was in its expectations alone; after the retirement the describer's checks still describe it (`DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays`, which checks its columns against the truth, the safety test and the noise budget), and `RealisticModelRefusalTests` builds its entry in memory and checks the refusal.  What is no longer compared for it is its kind, plan, table match, parameter types and column types, nullability, sources and origins as a recorded whole.  The pull request says so.
3. **The members `checkedFrom`, `dapper` and `models` stay** as 5.6 made them.  `checkedFrom` is what leaves a case out of the whole-entry check on an earlier major, by `RealisticCase.IsCheckedOn`, as it leaves it out of `--check`.
4. **The list of files that name the expectations or the script** is the spec's plus `.gitattributes`, `tests/AGENTS.md`, `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticDescribeTests.cs` (the theory of `ExpectationOn` and the test of `Render`), and `CONTRIBUTING.md`, which names the script.  `docs/superpowers/` names them too, and is not edited: specs and plans are not rewritten, but for the epic outline.  `tests/README.md` and `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs` say "expectation" of `all_types`' rows, which are not these files, and stay.
5. **`RealisticModelRefusalTests` builds the same entry** that the new check builds.  The helper that builds it goes in `RealisticDescribe`, and that test uses it, so that the entry of a case is built one way.
6. **`RenderFailure` stays.**  Its text is in the failure messages of `ColumnsOf`, `DescribeAsync` and `RealisticModelRefusalTests`; `Render`, the `Expectation` enum, `ExpectationOn`, `RefuseMessage` and the helpers only `Render` uses go.
7. **The documents of phase 5 changed in 5.6.**  It says that phase 8's generated methods run every case, those with `dapper` too, and keeps the Dapper run until then; the docs read in task 3 keep that.

## Global Constraints

- No change to the describer, the generator, a `case.json`, a `query.sql`, a sidecar, the truth, the oracle, the noise budget `{14: 53, 18: 54}` or its `Budgets`.
- The whole-entry check compares every field of the entry but `NameSpan` and `ServerVersion`, provenance included, and runs on PostgreSQL 14 and 18; a case it leaves out on a major is one that `IsCheckedOn` leaves out.
- A refused case keeps its `refusal` id in `case.json`, checked on both majors by `DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays`; its arguments, help and server lines are no longer compared.
- The user documentation names no internals: run the manual's `grep` of `docs/manual/AGENTS.md` over every user page changed.  The two packed readmes keep absolute links and no raw HTML.
- The release (`VersionPrefix`, the tag, `AnalyzerReleases.Shipped.md`) is the owner's; nothing here changes it.  The repository has no `v*` tag on 2026-10-19.
- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  Before a push, also `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.
- Test names are `Method_Scenario_Outcome`, assertions Shouldly's, every test class `sealed`.
- At most 150 files in the pull request.  The plan's count is about 145: 118 deleted, about 17 changed by the retirement, the plan and the epic outline, and the pages of task 3.  Task 3 counts again before it starts; if the total would pass 150, stop and split as the spec says: tasks 3 and 4 first, as a pull request of their own, and tasks 1 and 2 after.

## Review Focus

1. **A hand-edited provenance field** of a committed sidecar, `plan`, `tableMatch`, `typeSource`, `nullableSource` or `origin`, which `--check` passes: the whole-entry check must fail on it.  Task 1 proves it with a throwaway edit, recorded in the pull request.
2. **PostgreSQL 14.**  `merge-returning` skips by its `minimumMajor`; every other case with models is compared.  The 14.24 run before the push proves it.
3. **A failure that names nothing.**  The check's message names the case and shows both entries as text, so a difference reads as a diff of the sidecar.
4. **A case without models** (the three refused by the describer and `two-columns-named-id`) skips with a reason, and is not counted as passing.
5. **A user of the first release** reading the README's getting started from the top: every step must work today, with no step that says models are still to come.  Task 3 reads it so.

---

### Task 1: 5.7 In progress, and the whole-entry check

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 5.7: `In progress`)
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticDescribe.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticDescriptionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticModelRefusalTests.cs`
- Create: `docs/superpowers/plans/2026-10-19-query-generation-phase-5-7-retire-expectations-and-close.md` (this plan)

**Interfaces:**
- Produces, in `RealisticDescribe`:
  - `public static SidecarEntry EntryOf(RealisticCase found, ServerInfo server, QueryDescription description)`: the entry a run writes for the case's one query, by `FixtureReplay.Plan(found.AsFixture, ...)` and `EntryBuilder.Build(planned, dialect, PostgresContainer.RealisticDatabase, server, description)`.
- Produces, in `RealisticDescriptionTests`: the theory `DescribeAsync_Case_IsItsCommittedEntry(string name)`.

- [ ] **Step 1: Write the theory `DescribeAsync_Case_IsItsCommittedEntry`** over `RealisticCasesTests.Cases`:
  - `RealisticDescribe.Start(name, postgres.Major)`; `postgres.SkipUnlessRecordedMajor("The committed entries")`.
  - Skips, with a reason that names the case, when `!found.HasModels` ("has no sidecar") and when `!found.IsCheckedOn(postgres.Major)` ("its sidecar is checked from PostgreSQL {CheckedFrom.Major}").
  - Describes by `DescribeOnceAsync`; a refusal fails through `ColumnsOf`'s message.
  - Builds the entry by `EntryOf(found, new ServerInfo("*", null, null, []), description)`; reads the committed one from `found.SidecarPath` with `SidecarReader.Read`, which must hold one entry.
  - Compares `Text(described)` with `Text(committed with { ServerVersion = "*" })`, where `Text` is `SidecarWriter.Write` of a `Sidecar(SidecarFormat.Version, "*", [entry])`; the writer does not write `NameSpan`.  The message names the case and says to run `tools/describe-end-to-end.sh` after reading the diff.
- [ ] **Step 2: Add `EntryOf` to `RealisticDescribe`** and make `RealisticModelRefusalTests.SidecarOfAsync` use it.
- [ ] **Step 3: Run** `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-method '*DescribeAsync_Case_IsItsCommittedEntry'`.  Expected: 55 passed, 4 skipped.
- [ ] **Step 4: Prove it fails on provenance.**  Change `"plan": "walked"` to `"plan": "unavailable"` in `Realistic/Cases/aggregate-cte-left-joined/query.sql.json` and `"nullableSource": "outer-join"` of its `account_id` to `"heuristic"`; run the theory for that case and `CheckTests`: the theory fails naming the case, `--check` passes.  `git checkout` the sidecar.  Note both outputs for the pull request.
- [ ] **Step 5: Set row 5.7 of the epic's table to `In progress`.**  Commit with this plan: "Check each realistic case's committed entry whole".

### Task 2: Retire the expectations, the script and their documents

**Files:**
- Delete: the 117 `tests/SqlSource.Tests.Postgres/Realistic/Cases/*/expected-pg{14,18}.txt` and `tools/record-realistic.sh`.
- Modify: `RealisticDescriptionTests.cs` (delete `DescribeAsync_Case_IsItsRecordedDescription`, `DescribeAsync_KnownCase_IsDescribedAndRendered`, `RecordVariable`, `RecordScript`, `KnownCase`; the class comment says what it checks now), `RealisticDescribe.cs` (delete `Expectation`, `ExpectationOn`, `RefuseMessage`, `Render`, `None`, `NullableOf`, `OriginOf`; comments), `RealisticDescribeTests.cs` (delete the theory of `ExpectationOn` and `Render_Description_WritesALineForEachFact`; rename `Render_RefusalWithHelp_...` to `RenderFailure_RefusalWithHelp_WritesTheHelpBetweenItsArgumentsAndTheServer` and call `RenderFailure`), `RealisticCase.cs` (delete `ExpectedPath`; `Files` is the query, the truth and the sidecar when it has models; comments), `RealisticCasesTests.cs` (the `Files_` theory loses the expectations and is renamed `Files_Case_AreTheQueryTheTruthAndTheSidecar`; class comment), `RealisticNullabilityNoiseTests.cs` (the message points to the cases' sidecars, `query.sql.json`, where it pointed to `expected-pg{major}.txt`; nothing else), `Recording.cs` and `PostgresContainer.cs` (comments).
- Modify: `AGENTS.md` (rule 5), `.editorconfig` (delete the section of the expectations), `.gitattributes` (the reason of the `Cases/**` line), `CONTRIBUTING.md`, `tests/AGENTS.md`, `tools/AGENTS.md`, `tools/README.md`, `tests/SqlSource.Tests.Postgres/README.md` (the image section and the realistic corpus section), `docs/tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md`.

- [ ] **Step 1: Delete the files and the code** above.  `git grep -n -E "expected-pg|record-realistic|SQLSOURCE_RECORD_REALISTIC|ExpectedPath|RecordedDescription" -- ':!docs/superpowers'` finds nothing.
- [ ] **Step 2: The documents.**
  - Rule 5 of `AGENTS.md`: a new minor records again the describer's fixtures and the sidecars of `tools/describe-end-to-end.sh` for the default image; for `postgres:14.24` nothing is recorded, and its tag is also in `.github/workflows/build.yml`, `CONTRIBUTING.md` and `tests/SqlSource.Tests.Postgres/README.md`.
  - `tests/SqlSource.Tests.Postgres/README.md`: the image section says the same; the realistic corpus section describes the folder without expectations, `RealisticDescriptionTests` as the refusal and the whole committed entry, the recording paragraph gives way to "a change to what the describer answers, or to the schema, the seed or a case, writes the sidecars again with `tools/describe-end-to-end.sh` and reads the diff", `minimumMajor` no longer says "no expectation", and "to add a case" drops the script.  It says that a refused case's arguments, help and server lines are compared nowhere, and that a case with `checkedFrom` is described on the earlier major by no committed record.
  - `tests/AGENTS.md`: the corpus's bullet drops the expectations, the `.editorconfig` sentence and the byte-for-byte reason.
  - `tools/AGENTS.md`: "the three recording scripts" become two, and the rule of `record-realistic.sh` goes.  `tools/README.md`: its line goes.  `CONTRIBUTING.md`: "and recording it with `tools/record-realistic.sh`" goes.
  - `TD-0051`: the cases each kind is seen in are in their sidecars.
- [ ] **Step 3: Run** `./pre-commit-validation.sh`, then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.  Commit: "Retire the realistic corpus's expectations".

### Task 3: The user documentation of phase 5, read whole

An audit on 2026-10-19 read `README.md`, `src/SqlSource.Tool/README.md` and every page of `docs/manual/` whole, ran the manual's finders of callouts, of sentences of what is to come and of internals, and read the items of `docs/tech-debt` and `docs/deferred` that name phase 5.  The sub-phases had already done each row of the manual's phase map: no callout of phase 5 is left (the sixteen left are phases 6 to 8), no sentence says models are still to come, and the "What works today" table and `settings.md`'s Status cells are true.  What is left is below.  `SQLSRC248` is information, not a warning, in the code and in the manual; like `SQLSRC240` and `SQLSRC245` a project can configure it, so the documents stay as they are.  `docs/architecture/` has no placeholder of phase 5 left.

**Files:**
- Modify: `README.md`, `src/SqlSource.Tool/README.md`, `docs/manual/generator.md`, `docs/manual/postgresql.md`
- Modify: `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, `docs/tech-debt/TD-0056-mapped-generic-constraints-are-not-checked.md`, `docs/tech-debt/TD-0059-array-of-another-schema-has-two-keys.md`

- [ ] **Step 1: The README's common path.**
  - Step 4, after the sentence on `MatchNamesWithUnderscores`: Dapper binds a property of a parameter model only to a parameter written as its name, ignoring case but not `_`, so write parameters in camelCase, `@userId` and not `@user_id`; and a row model with an array column is read through Dapper only as a class.  Both link the recipe of running the models through Dapper.
  - Step 4, after "A property can be null where its column can": where you know a column is never null, `AS "total!"` or, with Dapper, a `-- column: total not null` line says so; link `postgresql.md#nullability`.
  - Step 5's first bullet and the tool readme's first paragraph lose "Today", which reads as if checking were the only and passing benefit.
  - "Custom type mapping": the types follow the Npgsql the project references, and Npgsql 10 reads `date`, `time`, `cidr`, `daterange` and `datemultirange` as other types than 8 and 9; a project that references no Npgsql names those in the override, `date=System.DateOnly time=System.TimeOnly` for Npgsql 10.  Link `postgresql.md#which-npgsql`.
  - "Supported environments": the models of PostgreSQL queries need Npgsql 8 or later where the project references Npgsql.
  - The status line, the introduction, step 5, "Only want to generate models?", "Just want to keep your SQL out of code files?", the FAQ on the models' namespace and "Where to next", which links the examples, are true and stay.  So do "Until the first release the packages are previews" and `--prerelease`, which the release's own pull request takes out by `docs/publishing.md`.
- [ ] **Step 2: `generator.md`.**  The bullet of `global::`: a built-in type is written as its keyword, `int` or `string`, and every other from `global::`; "No generated model gives a compiler warning or error" holds but for a known issue, the next bullet.  Table models: a view, a materialized view and a foreign table count as a table.
- [ ] **Step 3: The known issue of TD-0056**, whose trigger, 5.3, has come: in `postgresql.md`'s Known issues, a type override to a generic type whose type argument breaks the type's constraints, such as `List<Nullable<string>>`, is a compiler error in the generated model and not an `SQLSRC` error; check the type's constraints.  TD-0056's Impact loses "Low today: no generated code uses a mapped type", and its Proposed fix's sentence about adding the known issue goes.
- [ ] **Step 4: The items whose triggers name phase 5.**  TD-0034's trigger named phase 5's release, which this closes, and TD-0059's the models of 5.3, which shipped: both stay open, since the spec changes no code, and each trigger says what still prompts the fix; the pull request names both for the owner.  TD-0057, TD-0058, TD-0060, TD-0062, D-0003 and D-0004 name phase 5 and have triggers still to come; they stay as they are.
- [ ] **Step 5:** Run the manual's grep of internals over the changed pages, then `./pre-commit-validation.sh` (its link and sample tests read the pages); commit: "Read the user documentation of phase 5 whole".

### Task 4: Close phase 5 in the epic outline

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1:** Row 5.7 `Done`, its Delivers in the past where it says what was retired.  The paragraph under the table: phase 5 is closed.
- [ ] **Step 2:** In "Phase 5 - models", after Technical notes and before Testing, a `### Settled` section, as phases 2 and 3 have: what each sub-phase settled that the outline had recommended or left open, and each finding that changed what it said, by sub-phase, each item in a sentence or two that links nothing new and says where in the outline the full text stands.  It takes the place of `### Recommended`, whose two sentences it absorbs: the Models and Types in C# items are settled by the sub-phases' "Calls made", and the props' include and the file filter are settled by 5.1.  Its content:
  - 5.1: `SQLSRC237` to `SQLSRC245`; `SQLSRC240` and `SQLSRC245` configurable warnings, the owner's; `SQLSRC209` at the first query that needs an entry; `SQLSRC242` at the query; `SQLSRC243` once for a file; a file-level failure suppresses the file's query-level errors; Rider nests the sidecar, Visual Studio unchecked.
  - 5.2: the base map researched on Npgsql 8, 9 and 10; the Npgsql 8 marker `NpgsqlSlimDataSourceBuilder`; the date and time options dropped for the override; the override's names, levels and per-key merge, the owner's; a project without Npgsql needs the override for a type the drivers box differently; `SQLSRC246` to `SQLSRC252`, `SQLSRC248` a configurable information diagnostic; enums need no opt-in; arrays and `Array Nullability Mode`.
  - 5.3: `SQLSRC253` to `SQLSRC260`; the `-- column:` marker, the owner's; class models' `= default!`; a shared model's accessibility; the conflict rules; Dapper and camelCase parameters with `D-0004`; TD-0060 accepted.
  - 5.4: the end-to-end project, the script that starts its own container, seeds, the Npgsql 8 project, the owner's; TD-0062 and the rule that an array model read through Dapper is a class, the owner's.
  - 5.5: the examples project, referencing Npgsql and Dapper, its sidecars by the repository's script, no check that they are current.
  - 5.6: 55 cases; `two-columns-named-id` refused by the generator, the owner's; the corpus's database and `timestamptz=default` by metadata, the owner's; `checkedFrom`, `dapper` and `models`.
  - 5.7: the expectations retired for the whole-entry check; what a refused case and a case with `checkedFrom` lose.
- [ ] **Step 3:** The Testing section of the epic's Decisions: the last bullet of "Recommended, the realistic corpus" says the expectations were retired by 5.7, where it says they will be.  Phase 5's Testing says the corpus's committed entries are compared whole on 14 and 18.
- [ ] **Step 4:** `./pre-commit-validation.sh`; commit: "Close phase 5 in the epic outline".
