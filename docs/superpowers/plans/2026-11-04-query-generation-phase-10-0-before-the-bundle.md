# Query generation 10.0: before the bundle - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Answer, by replaying the describers' fixtures and querying the floor and default images, the five questions of sub-phase 10.0 and every "Verify first" item of the phase 10 design, write the answers as `docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md`, and amend the design and the epic outline where a finding changes a detail.

**Architecture:** One pull request of documents.  The prototype is explicit xUnit tests, `[Fact(Explicit = true)]`, in a folder `Spike10/` of `tests/SqlSource.Tool.Tests`, `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer`, since they need the tool's internals, and they write results files to the scratchpad.  It stays uncommitted while it is built; task 6 commits it once, validated, and removes it in the next commit, so that the merged tree does not hold it and the branch's history, which the merge keeps, does.  The document names that commit and the command that reruns each finding.

**Tech Stack:** .NET SDK 10.0.401, xUnit v3 4.0.1 on Microsoft.Testing.Platform, the tool's own replaying exchange and describers; `postgres:14.24` and `postgres:18.6`; `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` and the default SQL Server image of `tests/DatabaseTests/TestHarness/DefaultImages.cs`; Docker 29.8 on Apple silicon.

**Spec:** [`docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md`](../specs/2026-11-04-query-generation-phase-10-bug-reports-design.md), section "10.0 Before the bundle", every "Verify first" item, and the renaming steps 1 to 13 the findings feed; the epic's [phase 10 rows and section](../specs/2026-10-07-query-generation-epic-design.md#phase-10---bug-reports).

## Global Constraints

- `$SCRATCH` is `/private/tmp/claude-501/-Users-michael-Projects-SqlSource--claude-worktrees-file-settings-placement-1427b9/a4a44705-0c3d-4c28-96df-ceaeb257883a/scratchpad`.  Every result goes to `$SCRATCH/spike10/results/`, every generated case folder to `$SCRATCH/spike10/cases/`; nothing of either is committed.  A test reads `$SCRATCH` from the environment variable `SPIKE10_OUT`, and fails when it is unset.
- Every prototype test is `[Fact(Explicit = true)]` or `[Theory(Explicit = true)]`, so `dotnet test --solution SqlSource.slnx` and `./pre-commit-validation.sh` never run it.  It runs with `SPIKE10_OUT=$SCRATCH dotnet test --project <project> -- --filter-class "<class>" --explicit only`.
- The prototype changes no file outside its `Spike10/` folders.  It reads the tool's code and calls it; it never edits `src/`.
- The prototype builds with no warning and is formatted by `./format.sh`, since task 6 commits it through `./pre-commit-validation.sh`.
- The fixtures are the 65 PostgreSQL and 26 SQL Server cases of `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/`, read through `FixtureCases.Of(engine)` of `tests/SqlSource.Tool.Testing/Fixtures/`; a PostgreSQL case is described with `new PostgresDescriber(fixture.ExplainSettings)`, a SQL Server one with `new SqlServerDescriber()`, as `DescriberFixtureTests.DescriberOf` does.
- A results file is tab-separated text, one finding a line, with a header line; each task also writes `<task>-summary.md`, at most 150 lines, which the document is written from.
- Owner decisions bind every finding: bundles are posted on public issues; no raw log; a server error keeps its code and the parameters it names; never blank out what cannot be made safe; a user's name spelled as a keyword or engine word stays verbatim; approach A.
- A finding that would change a Decided statement of the design is reported to the controller, never written into the design by an implementer.

## Where merged code differs from the spec

- The realistic corpora (`tests/DatabaseTests/<engine>/Realistic/Cases/`, 61 PostgreSQL and 58 SQL Server cases) hold `query.sql`, `case.json` and a sidecar, and no capture.  Item 3 asks whether `overloads` covers their functions, so task 1 captures them on the default images into `$SCRATCH/spike10/cases/realistic-<engine>/`, in a fixture's layout, and the other tasks read them beside the fixtures.
- SQL Server has no catalog of its keywords or of its intrinsic functions (`LEN`, `DATEADD`, `COALESCE`); task 4 takes them from the documentation and checks each against both images.
- `SqlServerDescriber` takes no planner settings, and SQL Server's fixtures have no `explain-settings.txt`.

The document records these in its "What the later sub-phases take from this" section.

## Review Focus

- A member that no fixture changes but that the code reads, such as a field read only on an error path: task 2 lists every member from the records with its readers in the code, and a member the replay never moved is "read by the code" or "not read", never "not read" by the replay alone.
- A plan key whose value is a name in one fixture and an expression in another (`Alias`, `Relation Name`, `Output`, `Sort Key`): task 3 lists strings by key with what they read as.
- A name of the user's that is also a word of the engine's (`name`, `count`, `status`, `value`): task 5's prototype keeps it verbatim and counts how often the fixtures hit it.
- A request whose SQL the pipeline makes from the query (PostgreSQL's `$n` rewrite and `check`'s `@name` text, SQL Server's copies of a reused parameter and its `operands` slices): task 5 reports each request that differs from the one renamed token by token.
- A PostgreSQL error position: task 3 lists every read of `Position`, and task 5 every fixture whose capture holds one.

---

### Task 1: The harness, and the realistic corpora captured

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Spike10/SpikeCases.cs`, `SpikeReplay.cs`, `SpikeOutcome.cs`, `SpikeHarnessTests.cs`
- Create: `tests/DatabaseTests/Postgres/Spike10/RealisticCaptureTests.cs`, `tests/DatabaseTests/SqlServer/Spike10/RealisticCaptureTests.cs`

**Interfaces:**
- Produces, in namespace `SqlSource.Tool.Tests.Spike10`:
  - `SpikeOutcome`: a record of what one replay gave, `Kind` one of `Entry`, `Failure`, `Miss`, `Unreadable`, `Thrown`, with `Text` (the sidecar text for `Entry`; `"<id> <step>"` for `Failure`; `"<method> <request>"` for `Miss`; the message for `Unreadable` and `Thrown`).  Two outcomes are the same when both members are equal.
  - `SpikeReplay.RunAsync(FixtureCase fixture) -> Task<SpikeOutcome>`: `FixtureReplay`'s steps (`Plan`, open, describe, `EntryBuilder.Build`, `SidecarWriter.Write`) over a `ReplayingExchange` of the case's capture, but returning a failure's `Descriptor.Id` and `Step`, a `CaptureMissException` as `Miss` or `Unreadable` by its kind, and any other exception as `Thrown`, instead of throwing.
  - `SpikeCases.All() -> IReadOnlyList<FixtureCase>`: the fixtures of both engines, then the case folders of `$SCRATCH/spike10/cases/realistic-postgres/` and `realistic-mssql/` when they exist (through `FixtureCases.InFolder`; the folder's name after `realistic-` is the engine, so each is copied or read as `<...>/realistic-<engine>/<engine>/<case>`, the layout `InFolder` needs).
  - `SpikeCases.Copy(FixtureCase fixture, string name, string? query = null, string? capture = null) -> FixtureCase`: a new case folder under `$SCRATCH/spike10/cases/work/<engine>/<name>/` holding the case's files, with `query.sql` or `capture.json` replaced when given; `explain-settings.txt` and `expected.json` copied as they are.
  - `SpikeCases.Out(string file) -> string`: the path of a results file, from `SPIKE10_OUT`.

- [ ] **Step 1: The baseline test.**  `SpikeHarnessTests.Baseline_EveryCase_ReplaysToItsExpectedEntry` (explicit): for each case of `SpikeCases.All()`, `RunAsync` gives `Kind == Entry` and `Text == fixture.Expected`; writes `results/t1-baseline.tsv` (`engine case kind`) and fails naming every case that differs.  And `Baseline_CaptureWithACallRemoved_IsAMiss`: a copy of `postgres/walk-left` with its `columns` call removed gives `Miss` naming `columns`.
- [ ] **Step 2: Run it before `SpikeReplay` exists** and see it fail to build; implement the four files; run it and see the 91 fixtures pass.
- [ ] **Step 3: Capture the realistic corpora.**  `RealisticCaptureTests.Capture_EveryCase_WritesAFixtureFolder` (explicit) in each engine's project: for each case of the corpus with a sidecar, describe it on the default image as `RealisticCases.Describe` does, but through `FixtureReplay.DescribeThroughAsync` with a `CapturingExchange` over the live exchange, and write `$SCRATCH/spike10/cases/realistic-<engine>/<engine>/<case>/` with the case's `query.sql`, `capture.json` (`CaptureJson.Write(capture)`, no secret; the containers' connections hold none) and `expected.json` (the text `DescribeThroughAsync` returned).  A case the describer refuses, or whose query does not parse as a fixture's (`FixtureReplay.Plan` throws), is listed in `results/t1-realistic-<engine>-skipped.tsv` with why, and not written.
- [ ] **Step 4: Run** both capture tests, then step 1's baseline again: every realistic folder written replays to its own `expected.json`.  Write `results/t1-summary.md`: the counts, each skipped case and why.

### Task 2: Finding 1, the fields the pipeline reads

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Spike10/FieldMutationTests.cs`, `PlanKeyMutationTests.cs`, `JsonMutations.cs`

**Interfaces:**
- Consumes: task 1's `SpikeCases`, `SpikeReplay`, `SpikeOutcome`.
- Produces: `results/t2-members-<engine>.tsv` with the columns `method side path cases changedBy outcomes reader verdict`, and `results/t2-plan-keys.tsv` with `key cases changedBy outcomes reader verdict`.

- [ ] **Step 1: The member mutations.**  `FieldMutationTests.Mutate_EveryMemberOfEveryCall_RecordsWhatChanges`: for each case and each call of its capture, for each member of the request and of the answer at any depth, by its path with array indices written `[]` (`answer.tables[].columns[].notNull`), apply each mutation that fits its JSON kind, one at a time, and compare `RunAsync` of the copy with the baseline:
  - `removed`: the member deleted from its object;
  - `string`: the value with `_x` appended, and the empty string;
  - `number`: the value plus one;
  - `bool`: negated;
  - `null`: left null (recorded as "always null in this case");
  - an array: its last element removed, and, for an array of objects, its first two elements swapped.
  A member of the request is expected to change the outcome to a `Miss` of its own call whenever its value is not null; record it anyway.  The outcome is `same`, `entry`, `failure`, `miss:<method>` (a later call, or its own), `unreadable` or `thrown`.
- [ ] **Step 2: The plan's keys.**  `PlanKeyMutationTests.Mutate_EveryKeyOfThePlan_RecordsWhatChanges`: for each case with an `explain` answer, parse its `plan` string, and for each key name found anywhere in it: every occurrence removed; every occurrence's value changed (`string` + `_x`, number + 1, bool negated, an array's last element removed); the plan written back with the same serializer options the server's text needs for `JsonDocument` to read it (`Utf8JsonWriter` with indentation off is enough).  Record by key name.
- [ ] **Step 3: The readers in the code.**  For each record member of `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs` and `SqlServer/SqlServerCalls.cs`, and for each plan key that `src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs` and its callers read by name, list the files and members that read it (by searching the engine's folder for `.<Member>` and for the key's string, excluding the record's own file and the capture's writers), into the `reader` column; "none" when nothing reads it.
- [ ] **Step 4: The verdict.**  `read` when a mutation changed an outcome or the code reads it; `read, never moved` when only the code reads it (name the reader); `not read` when neither.  A request member is `key` (part of the replay's key) when not null.  Write `results/t2-summary.md`: per engine, per call, the members kept and dropped as step 1 of the design's renaming would keep them, and every member whose verdict differs between the replay and the code, with why.

### Task 3: Findings 2 and 5, the plan's strings and the messages

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Spike10/PlanStringTests.cs`

**Interfaces:**
- Consumes: task 1's `SpikeCases`; task 2's `results/t2-plan-keys.tsv` for which keys the walk reads.
- Produces: `results/t3-plan-strings.tsv` (`key strings readable unreadable kinds examples walkReads`), `results/t3-messages.md`.

- [ ] **Step 1: Every plan string through `PlanTokens`.**  `PlanStringTests.Read_EveryStringOfEveryPlan_RecordsWhatReads`: every string value of every plan, and every string element of an array value (`Output`, `Sort Key`, `Group Key`), by key: whether `PlanTokens.Read` gives tokens or null, the kinds of its tokens, and up to five examples of each outcome; the `walkReads` column from task 2's verdict of the key.  The summary lists every unreadable string of a key the walk reads, which is a refusal of the design's step 9, and every key whose strings hold `String` tokens, which the literal rule renames.
- [ ] **Step 2: Finding 5, by reading.**  In `src/SqlSource.Tool/Describing/`, every read of `PostgresServerError.Message`, `.Detail`, `.Hint`, `.Position`, of `PostgresBindError.Message`, of `SqlServerErrorRow.Message` and of `SqlServerDescribeError.Message`, with the file, the member and what it decides; and every constant text the tool writes into an answer and compares later (start from `PostgresLive.StatementNotListed`, `PostgresLive.TransactionRefused`, `ConnectionErrors.MultiplexingRefused`, `ConnectionErrors.UnreadableTypes` of both engines, and search `ConnectionErrors`, `ConnectionTexts` and every `new PostgresServerError(` and `new SqlServerErrorRow(` for others).  For each read, whether it reaches the entry or a `DescribeFailure` (`Arguments`, `ServerLines`), which the bundle's `bundle.json` failure does not hold beyond id and step.  And every `error` of every fixture's and realistic case's capture: its call, its code, whether it has a position, a detail or a hint.  Write `results/t3-messages.md`, and say whether each statement of the design's "Both describers read a server message's text in one place" holds.

### Task 4: Finding 3, the vocabulary

**Files:**
- Create: `tests/DatabaseTests/Postgres/Spike10/VocabularyTests.cs`, `tests/DatabaseTests/SqlServer/Spike10/VocabularyTests.cs`, `tests/SqlSource.Tool.Tests/Spike10/CoverageTests.cs`

**Interfaces:**
- Consumes: task 1's `SpikeCases`.
- Produces: `results/t4-pg-<14|18>-keywords.tsv` (`word catcode catdesc`), `results/t4-pg-<14|18>-catalog.tsv` (`kind name`: `function`, `operator`, `type` of `pg_catalog`, and `type` of `information_schema`), `results/t4-mssql-<2017|default>-catalog.tsv` (`kind name`: `type` of `sys.types` with `is_user_defined = 0`, `function` of the documented list that the server knows), `results/t4-mssql-keywords.tsv` (`word list`: `reserved`, `odbc`, `future`), `results/t4-coverage.tsv`.

- [ ] **Step 1: PostgreSQL.**  `VocabularyTests.Dump_Vocabulary_WritesTheLists` on `postgres:14.24` and on the default image (`SQLSOURCE_TESTS_POSTGRES_IMAGE`, run twice): `SELECT word, catcode, catdesc FROM pg_get_keywords()`; the distinct `proname` of `pg_proc`, `oprname` of `pg_operator` and `typname` of `pg_type` in `pg_catalog`, and `typname` of `information_schema`'s types.  The diff between 14 and 18 of each list.
- [ ] **Step 2: SQL Server.**  The reserved, ODBC and future keywords from Microsoft's "Reserved keywords (Transact-SQL)" page, and the built-in scalar, aggregate, ranking and rowset functions from its "Functions" reference, fetched once and saved under `$SCRATCH/spike10/sources/` with the URL and date.  `VocabularyTests.Dump_Vocabulary_WritesTheLists` on the 2017 floor and the default image: `sys.types` where `is_user_defined = 0`; and each documented function `F` checked by `sys.dm_exec_describe_first_result_set(N'SELECT F()', NULL, 0)`: error `195` ("is not a recognized built-in function name") means the server does not know it, any other answer that it does.  Record each image's list.
- [ ] **Step 3: Coverage.**  `CoverageTests.Words_OfEveryCase_ByWhereTheyCome` (explicit): every word of every case's `query.sql` (split by the generator's lexer and the engine's word rule) and of every plan string (by `PlanTokens`), with its count of cases, and whether it is: a keyword of the engine (by its list, with PostgreSQL's catcode); a name of the case's catalog answers (a table, column, schema or type of the user's); a function or operator asked in the case's `overloads` request, and whether its answer listed it as not `pg_catalog`'s alone; in the committed lists of steps 1 and 2; a name the planner makes (`*SELECT* <k>`, `*VALUES*`, `column<n>`, `InitPlan <k>`, `SubPlan <k>` and the like, matched by form); or none of these.  `results/t4-coverage.tsv` and `results/t4-summary.md`: the words of no class (each a token a renamed bundle would refuse or rename), every function or operator of the cases that `overloads` did not ask about, the user names that are also engine words, and the size in bytes and lines of each committed list, raw and gzipped.

### Task 5: Finding 4, a crude renaming of every case, replayed

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Spike10/CrudeRenamer.cs`, `CrudeRenamerPostgres.cs`, `CrudeRenamerSqlServer.cs`, `CrudeRenamingTests.cs`

**Interfaces:**
- Consumes: task 1's harness; task 2's member verdicts (which members a renamed capture keeps); task 4's lists (keywords and catalog names kept verbatim).
- Produces: `CrudeRenamer.Rename(FixtureCase fixture) -> (FixtureCase Renamed, IReadOnlyDictionary<string, string> Mapping)`; `results/t5-renaming.tsv` (`engine case outcome provenance requestDiffs notes`) and `results/t5-summary.md`.

- [ ] **Step 1: The renamer.**  A name is replaced by `<prefix>` and the first eight hex digits of HMAC-SHA256 over `<class>:<folded spelling>` with a fixed key, the design's classes and prefixes (step 3), one spelling one name (step 4), folded as the engine folds (step 5).  Names come from the catalog fields of the answers, by field (step 2), then from the words of `query.sql` that are no keyword, no word of task 4's lists and no name the planner makes (those become `a_` aliases); a string literal of any form the lexer reads becomes `'l_...'` of the same form, an enum label the literal of its spelling (step 6); comments are emptied (step 7).  `query.sql` is renamed lexeme by lexeme with the generator's lexer (`DescriberSql.Lex` and the engine's `SqlDialectRules`), its markers' names too; every string of every request and answer is renamed by the same mapping, token by token, the SQL by the lexer and the plan's strings by `PlanTokens` (step 9); an error's text becomes the design's step 10 text.  The members task 2 found not read are dropped from the copy.  `expected.json` is renamed by the same mapping, and its `hash` is left as it is.
- [ ] **Step 2: The test.**  `CrudeRenamingTests.Rename_EveryCase_ReplaysWithTheSameProvenance` (explicit): for each case of `SpikeCases.All()`, rename it, replay the copy, and record: `Miss` with the request the pipeline made beside the one renamed in the capture and their first difference; a failure; or an entry whose `provenance` of each column and parameter (`nullableSource`, `plan`, `tableMatch`, `typeSource`) equals the original's, and whose other members equal the renamed `expected.json` but for `hash`.  Also: whether any name or literal of the engine's `schema.sql` or of the case's `query.sql` that the renamer did not keep as the engine's is found in any file of the copy.
- [ ] **Step 3: Iterate without guessing.**  A case that fails is studied, and the renamer changed only where the failure shows a rule of the design applied wrongly; a failure that a rule of the design causes is kept, and reported with the rule.  For SQL Server, the `operands` requests and the copies of a reused parameter made from the renamed query are compared with the renamed capture's, and the summary says whether the proofs read the renamed select lists as they read the originals (the same proofs, by the entry's provenance).
- [ ] **Step 4: Run** the test and write `results/t5-summary.md`: per engine, how many cases replay with the same provenance; each case that does not, with its cause and the design step it touches; each request that differs and why; each leaked token; and what the renaming had to know that the design does not say.

### Task 6: The spike document, the amendments, and the prototype in the history (controller)

**Files:**
- Create: `docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md`
- Modify: `docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 10.0 row's status, and the phase 10 rows if a finding changes them)

- [ ] **Step 1: The document**, in 7.0's and 9.0's shape: what ran (versions, images, cases); one section for each of the five findings, written from the results' summaries, with finding 1's list per engine, call, request and answer member, and plan key, each marked read or not read with how it was established; "What the later sub-phases take from this", with the owner decisions, the design's amendments, the exact values and lists 10.1 to 10.4 use, and where merged code differs from the design; and "The prototype": the commit that holds it, and the command for each finding.
- [ ] **Step 2: Amend the design** where a finding changes a detail, each amendment marked "(10.0, 2026-11-04)", and turn each "Verify first" the spike answered into the answer.  A finding that changes a Decided statement goes to the owner first.
- [ ] **Step 3: The epic outline**: the 10.0 row's status, and the phase 10 rows where a finding changed them.
- [ ] **Step 4: Commit the prototype, then remove it.**  `./format.sh`; `./pre-commit-validation.sh`; commit the `Spike10/` folders alone with the message `spike(10.0): the prototype of the spike, removed in the next commit`; remove them and commit with the document.  Write the first commit's sha into the document's "The prototype" section, validate, and commit.
- [ ] **Step 5: Hand-over notes**: `10-0-notes.md` in the shared notes folder, with the facts 10.1 to 10.3 need (the member lists' location, the vocabulary's source, the prototype's commit).
