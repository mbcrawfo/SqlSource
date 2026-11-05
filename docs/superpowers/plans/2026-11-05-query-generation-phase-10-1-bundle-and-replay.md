# Phase 10.1, the bundle and `replay` - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The bundle's layout with `bundle.json`, its reader of a zip or a folder and its writer of a zip; `sqlsource replay`, which runs the pipeline from a bundle with no database and says whether it reproduces; its four errors; `Fixtures/Bundles/` and its test; and the renaming's engine-neutral part behind a fixed interface of an engine's part, with no engine's part yet.

**Architecture:** A new folder `src/SqlSource.Tool/Bundles/` holds the bundle (files, `bundle.json`, reader, writer) and `BundleReplay`, which both the `replay` command and the renaming's proof use.  `Bundles/Renaming/` holds the engine-neutral renaming (keyed hash and mapping, the SQL renamer over the generator's lexer, the walker of a capture by field rule, the entry, the token check and the proof) behind `IEngineRenaming`, which 10.2 and 10.3 implement; `EngineRenamings.For` is a switch over `DatabaseEngine` whose two arms are "not built yet" (null).  `ReplayCommand` sits beside `DescribeCommand` at the tool's root.

**Tech Stack:** C# on `net8.0` (the tool), `System.IO.Compression` for the zip, `System.Text.Json` (`JsonNode` for renaming), `System.Security.Cryptography.HMACSHA256`, xUnit v3 + Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md` ("The bundle", "The mapping file", "Renaming", "`replay`", "Diagnostics", "10.1 The bundle and `replay`", "Documentation"), as amended by `docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md`, and the owner decisions in the manager's `notes.md` (R1-F13: the manual's command reference documents `replay` in 10.1).

## Global Constraints

- Root `AGENTS.md` rules apply: `./pre-commit-validation.sh` before each commit, `--full` before each push; docs in the same PR; no deferred minor feedback.
- `captureVersion` stays `1`; the capture's records and `CaptureJson.Options` are not changed.
- `bundleVersion` is `1`.
- Every switch over an enum names each member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`; nothing names an engine but a switch over `DatabaseEngine` (closed sets).
- Everything from outside goes through `ToolHost`; every error through `Reporter` with a `ToolDiagnostics` descriptor; every printed text the tool did not write through `OneLine`; a path in a message is a full path, or `The path at position <n>` when its token holds `=` or `;`.
- A wrong command line is `UsageCheck`'s, with no id, never repeating a token.
- `src/SqlSource.Tool/AGENTS.md` is at its 8 KB limit: amend bullets, add none there; new rules go in `src/SqlSource.Tool/Bundles/AGENTS.md` (each bullet ≤ 80 words, file ≤ 8 KB, maintenance footer).
- User docs (`docs/manual/`) never name the describer, a plan, `EXPLAIN`, a capture file, provenance, or a tech-debt id; the "Bug-report bundles" row stays "Coming" for making a bundle.
- No `Spike10` code is copied whole; `CrudeRenamer*.cs` at `f51ac107` is a reference only.

## Where merged code differs from the spec's assumptions

- **Step 8's check is the proof's capture miss.**  The spec asks that each request's SQL made from the renamed query equal the request renamed token by token.  The replaying exchange keys a call by its whole request, so a renamed request that the pipeline does not make from the renamed query is exactly a capture miss of that call in step 13's replay.  The neutral part reports such a miss as a refusal at the request's SQL (`RefusalStep.RequestSql`, naming the call), and the engine's part derives, through `IEngineRenaming.Derive` and `CopyOf`, the three parts the pipeline makes by its own rule (`AS [n]`, copies of a reused parameter, lists in name order).  No engine function re-implements the pipeline's SQL.
- **A Postgres describer with planner settings** is made by `BundleDescribers.For` with `new PostgresDescriber(settings)`, since the registry holds the one with none; the rule of `Describing/Postgres/AGENTS.md` and `PlannerSettings`' remarks are amended for `replay`.
- **`--log` and `--verbose` are describe's in `Cli`** (`DescribeCommand.LogPathOf`, `IsVerbose`, `Runs`): `Cli` gains a dispatch over the command that runs, so `replay` opens its log and reporter the same way.

## Review Focus

1. **A zip someone re-zipped with a top folder** (`case/query.sql`): expected a clear `SQLSRC288` saying the zip holds no `query.sql` at its root, never `SQLSRC200`.  Test in Task 2.
2. **An unreadable answer in a capture** (a member that is not nullable missing): `CaptureMissException.Unreadable` must be `SQLSRC290`, the same as a missing call, never `SQLSRC200`.  Test in Task 4.
3. **A folder with a bad `explain-settings.txt` line, or a SQL Server folder with one**: `SQLSRC288`, never the describer's `ArgumentException` as `SQLSRC200`.  Test in Task 3.
4. **A replay path that holds `=` or `;`** (or spaces): the message names `The path at position <n>` for `=`/`;`, and a path with spaces reads.  Test in Task 4.
5. **One spelling written two ways in PostgreSQL**: `Users` unquoted and `users` from the catalog get one name, `"Users"` quoted another; a replacement never collides with another (collision lengthens).  Test in Task 6.

---

### Task 1: The four errors of `replay`

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (four descriptors after `WatchFailed`, and `All`)
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `src/SqlSource/Diagnostics/AGENTS.md` ("`SQLSRC200` to `SQLSRC291` are taken.")
- Modify: `docs/manual/diagnostics.md` (index rows, four sections, the range paragraph at line ~256)
- Test: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` (existing tests cover id order and the document)

**Interfaces:**
- Produces: `ToolDiagnostics.BundleCannotBeRead` (`SQLSRC288`), `ToolDiagnostics.BundleOfNewerTool` (`SQLSRC289`), `ToolDiagnostics.BundleCallMissing` (`SQLSRC290`), `ToolDiagnostics.NoDescriberForBundle` (`SQLSRC291`).

| Id | Title | Message format |
|----|----|----|
| `SQLSRC288` | Bundle cannot be read | `The bundle cannot be read: {0}` |
| `SQLSRC289` | Bundle is of a newer version | `The bundle's {0} is {1}, and this version of sqlsource reads {2}` |
| `SQLSRC290` | Bundle does not answer a question of the query | `The bundle has no answer to '{0}' that the query asks: it was made by {1}, and this is sqlsource {2}` |
| `SQLSRC291` | No describer for the bundle's engine | `The bundle is of the engine '{0}', which this version of sqlsource does not describe` |

Each is an error, `NotConfigurable`, help link `sqlsrc288`…`sqlsrc291`, reported at the bundle's full path (or by position, as any path).  `{1}` of `SQLSRC290` is `sqlsource <toolVersion>` from `bundle.json`, or `another version of sqlsource` for a folder without one.

- [ ] **Step 1:** Add the descriptors, `All`, the release-tracking rows and the AGENTS range; build: `dotnet build src/SqlSource` passes RS2000-RS2008.
- [ ] **Step 2:** Add the index rows (a new **Bundles** group or the tool's group, as the page does), one `## SQLSRCnnn` section each with a sample (`console` or `text` fence the samples test accepts) and its fix; amend the range paragraph: tool errors become "200 to 236, 262 to 266, 271 and 285 to 291", and "exits with the code 1, or with 2 when it reported nothing but `SQLSRC219`, or when `replay` gave another answer than the bundle's".
- [ ] **Step 3:** Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "*Diagnostics*"`; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): the errors of replaying a bundle, SQLSRC288 to SQLSRC291`.

### Task 2: The bundle: files, `bundle.json`, reader and writer

**Files:**
- Create: `src/SqlSource.Tool/Bundles/BundleFiles.cs`, `BundleInfo.cs`, `BundleOutcome.cs`, `BundleJson.cs`, `Bundle.cs`, `BundleReader.cs`, `BundleWriter.cs`, `BundleErrors.cs`
- Modify: `src/SqlSource.Tool/ToolHost.cs` (new member `OpenReadFile`), `tests/SqlSource.Tool.Testing/Hosts.cs` and every other place that builds a `ToolHost`
- Test: `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs`, `BundleReaderTests.cs`, `BundleWriterTests.cs`

**Interfaces:**
- Consumes: Task 1's descriptors.
- Produces:
  - `static class BundleFiles { const string Info = "bundle.json", Query = "query.sql", Capture = "capture.json", Expected = "expected.json", ExplainSettings = "explain-settings.txt", MappingSuffix = ".mapping.json"; }`
  - `enum BundleOutcomeKind { Entry, Failure }`; `sealed record BundleOutcome(BundleOutcomeKind Kind, string? Id, string? Step)` with `static BundleOutcome Entry` and `static BundleOutcome Failure(string id, string? step)`; `Step` is `DescribeStepName.Of`'s text, null for a failure to open.
  - `sealed record BundleInfo(int BundleVersion, bool Renamed, string ToolVersion, int FormatVersion, string Engine, string ServerVersion, string? DriverVersion, string Runtime, string OperatingSystem, BundleOutcome Outcome)`.
  - `static class BundleJson { const int Version = 1; string Write(BundleInfo info); Outcome<BundleInfo, ToolDiagnostic> Read(string text, string path); }` — keys camelCase as the record, `outcome` an object `{"kind": "entry"}` or `{"kind": "failure", "id": "SQLSRC226", "step": "describe parameters"}`; indented, `\n`, a final line break, relaxed encoder as `CaptureJson`.  A `bundleVersion` above `1` is `SQLSRC289` before any other member is read; a missing or mistyped member is `SQLSRC288` naming it.
  - `sealed record Bundle(BundleInfo? Info, string Query, string Capture, string? Expected, IReadOnlyList<string> ExplainSettings)`.
  - `static class BundleReader { Outcome<Bundle, ToolDiagnostic> Read(string fullPath, Func<string, Stream?> openReadFile, Func<string, string?> readTextFile); }`
  - `static class BundleWriter { void Write(Bundle bundle, Stream into); }`
  - `ToolHost.OpenReadFile: Func<string, Stream?>` — the file at a full path opened for reading, null when there is none or it is a folder; `ToolHost.Create` gives `ToolHost.OpenRead`.
  - `static class BundleErrors` builds each `ToolDiagnostic` at the bundle's path (the four descriptors), so the command and the tests share the texts.

Rules of the reader: a path that `openReadFile` opens is a zip (`ZipArchive`, read mode); else a folder read through `readTextFile(Path.Combine(path, name))`.  Neither (`query.sql` and `capture.json` both missing in the folder, or no file and no folder) is `SQLSRC288` "it is neither a zip nor a folder that holds query.sql and capture.json".  In a zip every file is at its root: a missing `bundle.json`, `query.sql` or `capture.json` there is `SQLSRC288` naming the file "at the root of the zip".  A folder needs `query.sql` and `capture.json`; `bundle.json` and `expected.json` are optional.  A zip that `ZipArchive` rejects is `SQLSRC288` with its reason.  `explain-settings.txt` lines are trimmed and blank ones dropped, as `FixtureCase.ExplainSettings`.  Text is read as UTF-8.

Rules of the writer: entries in the order `bundle.json`, `query.sql`, `capture.json`, `expected.json` (when not null), `explain-settings.txt` (when any setting, one a line, `\n`); each entry's `LastWriteTime` is `1980-01-01T00:00:00` so two writes of one bundle are the same bytes; `CompressionLevel.Optimal`.

- [ ] **Step 1: Failing tests.**  `BundleJsonTests`: `Write_ThenRead_GivesTheSameInfo` (an entry outcome and a failure with a null step); `Read_BundleVersionTwo_IsSqlsrc289` (`Descriptor.Id.ShouldBe("SQLSRC289")`, arguments `["bundleVersion", "2", "1"]`); `Read_NoEngine_IsSqlsrc288NamingEngine`; `Write_EndsWithOneLineBreak_AndHasNoCarriageReturn`.  `BundleReaderTests` (files in a `TempFolder`): `Read_FolderOfAFixture_GivesItsFiles` (no info, expected present, settings of a `walk-left-hash` case); `Read_ZipWrittenByBundleWriter_GivesTheSameBundle`; `Read_ZipWithATopFolder_IsSqlsrc288AtTheRoot` (reason holds `query.sql` and `root`); `Read_NotAZipNorAFolder_IsSqlsrc288`; `Read_FolderWithoutCapture_IsSqlsrc288`; `Read_FileThatIsNoZip_IsSqlsrc288`.  `BundleWriterTests`: `Write_Twice_GivesTheSameBytes`; `Write_NoExpected_HasNoExpectedEntry`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "SqlSource.Tool.Tests.Bundles"`; expected FAIL (types missing).
- [ ] **Step 3:** Implement the types above and the host member.
- [ ] **Step 4:** Run the same filter, and `--filter-class "*ToolHostTests"`; expected PASS.
- [ ] **Step 5:** Commit `feat(tool): the bundle's layout, bundle.json, and its reader and writer`.

### Task 3: Replaying a bundle

**Files:**
- Create: `src/SqlSource.Tool/Bundles/BundleReplay.cs`, `ReplayOutcome.cs`, `BundleDescribers.cs`, `EntryDifferences.cs`, `ReplayLines.cs`
- Modify: `src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs` (a `Method` property, set by `NoAnswer` and `Unreadable`; null for the other constructors)
- Modify: `src/SqlSource.Tool/Describing/Postgres/PlannerSettings.cs` (remarks: the tool passes none but under `replay`, which reads them from a bundle or fixture), `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` (the `PlannerSettings` rule: `ToolHost.Create` registers the describer with none, and `Bundles/BundleDescribers.cs` alone makes one with a folder's settings)
- Test: `tests/SqlSource.Tool.Tests/Bundles/BundleReplayTests.cs`, `BundleDescribersTests.cs`, `EntryDifferencesTests.cs`, `ReplayLinesTests.cs`

**Interfaces:**
- Consumes: `Bundle`, `BundleInfo`, `BundleErrors` (Task 2).
- Produces:
  - `abstract record ReplayOutcome` with `sealed record Described(SidecarEntry Entry)`, `sealed record Refused(string Id, string? Step)`, `sealed record Missed(string Method, string Reason)`.
  - `static class BundleReplay`:
    - `const string Connection = "replay";` (nothing opens; the describer needs a value)
    - `Outcome<(PlannedQuery Query, SqlDialect Dialect), ToolDiagnostic> Plan(Bundle bundle, string engine, string path, CancellationToken token)` — `query.sql` read by `SqlFileReader` in the engine's dialect, one query, database from its marker or the dialect's canonical name, hash by `SqlQueryHash.Compute`, as `FixtureReplay.Plan`; a parse error, not exactly one query, or a `-- dialect:` of another dialect is `SQLSRC288`.
    - `string EngineOf(Bundle bundle)` — `Info.Engine`, else the capture's `engine`.
    - `Task<ReplayOutcome> RunAsync(Bundle bundle, PlannedQuery query, SqlDialect dialect, DescribeCapture capture, IQueryDescriber describer, IRunLog log, TimeProvider time, CancellationToken token)` — opens over `new ReplayingExchange(capture)` with `Connection`, describes `new DescribeRequest(query.Query.Name, SampleSql.Build(query.Query), query.Query.Parameters)`, builds with `EntryBuilder.Build(query, dialect, database, session.Server, description)`; a failure to open or describe is `Refused(descriptor id, step name or null)`; a `CaptureMissException` thrown by open, describe or dispose is `Missed(method, message)` (catch `CaptureMissException` alone; the method is the one the exception names, kept as a new `CaptureMissException.Method` property set by `NoAnswer` and `Unreadable`).
    - `Outcome<ReplayOutcome?, ToolDiagnostic> ExpectedOf(Bundle bundle, string path)` — `expected.json`'s one entry as `Described` (read by `SidecarReader.Read`; not one entry is `SQLSRC288`), else `bundle.json`'s failure as `Refused`, else null.
  - `static class BundleDescribers { Outcome<IQueryDescriber, ToolDiagnostic> For(SqlDialect dialect, DescriberRegistry registry, IReadOnlyList<string> explainSettings, string engine, string path); }` — `registry.Find(dialect)` null, or an engine name that is no describable dialect, is `SQLSRC291`; with settings, a switch over `SqlDescribableDialects.EngineOf(dialect)`: `PostgreSql` → `new PostgresDescriber(settings)` after `PlannerSettings.Checked` (an `ArgumentException` there is `SQLSRC288` "the line '<line>' of explain-settings.txt is no planner setting", the line through `OneLine`), `SqlServer` → `SQLSRC288` "a SQL Server bundle has no explain-settings.txt".
  - `static class EntryDifferences { IReadOnlyList<string> Of(SidecarEntry expected, SidecarEntry replayed); }` — both written by `SidecarWriter` as a one-entry sidecar with one tool version, parsed as `JsonNode`, compared member by member; each difference is `<path>: <expected JSON> -> <replayed JSON>` with paths as `columns[2].nullableSource`, a missing member `(none)`; empty when equal.
  - `sealed class ReplayLines(IRunLog inner, TextWriter output, bool verbose) : IRunLog` — passes every event to `inner`, and writes one line to `output` for each event whose name, after its engine's prefix, is `walk.node`, `walk.column` or `table-match`, and under `verbose` for every event: `sqlsource: <event name>: <member>=<JSON value> ...` (strings JSON-quoted, through `OneLine`).  `Writes` is true.  The describer is given `new PrefixedLog(lines, engine)`, a private nested log that puts `<engine>.` before each name (no `SessionRunLog`: a replay has no secret).

- [ ] **Step 1: Failing tests.**  `BundleReplayTests`: `RunAsync_EveryFixture_GivesItsExpectedEntry` (theory over `FixtureCases.Of("postgres")` and `Of("mssql")` read with `BundleReader`, describer from `BundleDescribers.For` with `ToolHost.Create().Describers`; `Described.Entry` equals `ExpectedOf`'s); `RunAsync_CaptureWithoutItsExplain_IsMissedNamingExplain`; `RunAsync_AnswerMissingAMemberThatIsNotNullable_IsMissed`; `RunAsync_PrepareAnsweredWith42P18_IsRefusedWithIdAndStep` (a hand-built PostgreSQL capture, the same as Task 5's refused case); `Plan_DialectMarkerOfAnotherDialect_IsSqlsrc288`.  `BundleDescribersTests`: `For_PostgresWithSettings_DescribesWithThem` (a `walk-left-hash` fixture replays); `For_BadSettingLine_IsSqlsrc288`; `For_SqlServerWithSettings_IsSqlsrc288`; `For_EngineWithNoDescriber_IsSqlsrc291` (empty registry, and engine `oracle`).  `EntryDifferencesTests`: `Of_SameEntry_IsEmpty`; `Of_ColumnNullabilityChanged_NamesItsPath`.  `ReplayLinesTests`: `Write_WalkNode_WritesALine`; `Write_OtherEvent_WritesNoLineUnlessVerbose`; `Write_AnyEvent_PassesToTheInnerLog`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "SqlSource.Tool.Tests.Bundles"`; expected FAIL.
- [ ] **Step 3:** Implement.  Make `FixtureReplay` keep its own code (tests' harness is not changed here).
- [ ] **Step 4:** Run the filter and `--filter-class "*CaptureMissExceptionTests"` if it exists; expected PASS.
- [ ] **Step 5:** Commit `feat(tool): replay a bundle through the describer with no database`.

### Task 4: The `replay` command

**Files:**
- Create: `src/SqlSource.Tool/ReplayCommand.cs`
- Modify: `src/SqlSource.Tool/Cli.cs` (dispatch by the command that runs: log path, verbose, reporter's trace, binding), `src/SqlSource.Tool/UsageCheck.cs` only if a second command needs it, `src/SqlSource.Tool/Logging/LoggedArguments.cs` only if `replay`'s path needs the rule of a path
- Test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`, and a case each in `UsageCheckTests.cs` (the rule that joins the remarks of `UsageCheck.cs`)

**Interfaces:**
- Consumes: Tasks 2 and 3.
- Produces: `internal static class ReplayCommand { const string Name = "replay"; Command Create(); bool Runs(ParseResult); void Bind(ParseResult parsed, Usage usage, ToolHost host, Reporter reporter, Func<IRunLog> log); }`.

The command: `dotnet sqlsource replay <bundle> [--verbose] [--log <path>]`, the argument `bundle` exactly one, `--log` and `SQLSOURCE_LOG` as `describe`'s (`RunLogFile.Open`'s refusals hold), `--verbose` described as "Print every event of the replay, not only the decisions of the walk and the table match".  Help of the command: "Runs the pipeline from a bug-report bundle with no database, and says whether it gives the bundle's answer".  It writes, on standard output, in order:

1. `sqlsource: this bundle is not renamed, and must not be posted` when `bundle.json` says `renamed: false`.
2. The event lines of `ReplayLines`.
3. The outcome:
   - Same as expected: `sqlsource: reproduced: the pipeline gives this answer`; exit `0`.
   - Different: `sqlsource: not reproduced`, then `sqlsource:   <difference>` for each of `EntryDifferences.Of`, or `expected <id> at <step>, replayed <id> at <step>` (or an entry against a failure), then `sqlsource: the bundle was made by sqlsource <toolVersion>, sidecar format <formatVersion>; this is sqlsource <Cli.Version>, sidecar format <SidecarFormat.Version>` when it has `bundle.json`; the command returns `2`.
   - No expected answer: `sqlsource: replayed: <the entry's JSON on one line>` or `sqlsource: replayed: <id> at <step>`; exit `0`.
4. A miss is `SQLSRC290` at the bundle's path; reading errors `SQLSRC288`/`SQLSRC289`; no describer `SQLSRC291`; exit `1` by `Cli.ExitCodeOf`.  No `RunAgainLine` (that line stays `describe`'s).

`Cli.ExitCodeOf` already passes on a command's `2` when no error was written; nothing changes there.

- [ ] **Step 1: Failing tests** through `Cli.RunAsync` with the tests' host (`Hosts`): `Replay_EveryFixtureFolder_IsReproduced` (theory over both engines; exit 0, output ends with the reproduced line); `Replay_ExpectedChanged_IsNotReproducedWithTheDifference` (copy a fixture, set one column's `nullable` the other way; exit 2, a line names `columns[<n>].nullable`); `Replay_CaptureWithACallRemoved_IsSqlsrc290` (exit 1, the error names the method); `Replay_ZipOfAFixture_GivesWhatTheFolderGives` (`BundleWriter` with an info, then compare output lines after the raw line); `Replay_RawBundle_SaysSo`; `Replay_BundleVersionTwo_IsSqlsrc289`; `Replay_NoExpectedAnswer_PrintsWhatItGives`; `Replay_Log_WritesTheDescribersEvents` (`run.start`, a `postgres.walk.node`, `run.end` with exit code); `Replay_Verbose_PrintsEveryEvent`; `Replay_PathWithEqualsSign_NamesItsPosition`; `Replay_PathWithSpaces_Reads`; `Replay_TwoBundles_IsAWrongCommandLine`; `Help_ListsReplay`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*ReplayTests" --filter-class "*UsageCheckTests" --filter-class "*CliTests"`; expected FAIL.
- [ ] **Step 3:** Implement `ReplayCommand` and the dispatch in `Cli` (a private method that asks each command's `Runs`, so `OpenLog`, `InvokeAsync` and `MayPoint` read the one that runs).
- [ ] **Step 4:** Run the same filters and `--filter-namespace "SqlSource.Tool.Tests.Commands"`; expected PASS.
- [ ] **Step 5:** Commit `feat(tool): sqlsource replay`.

### Task 5: `Fixtures/Bundles/` and its replay test

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Bundles/postgres/select-left-join/{bundle.json,query.sql,capture.json,expected.json}` (copied from the recorded case, `renamed: false`, `toolVersion` `fixture`, outcome entry), `.../postgres/prepare-type-not-determined/{bundle.json,query.sql,capture.json}` (hand-built: the `open` and `prepare` calls of a case with an untyped parameter, `prepare`'s answer an error `{"sqlState": "42P18", "message": "could not determine data type of parameter $1", ...}` with every member its record declares; outcome `failure` with the id and step that replay gives, which the test confirms), `.../mssql/select-left-join/...` (from the recorded case)
- Create: `tests/SqlSource.Tool.Testing/Fixtures/BundleCases.cs` (the cases of `Fixtures/Bundles/<engine>/`, as `FixtureCases`)
- Create: `tests/SqlSource.Tool.Tests/Fixtures/BundleCasesTests.cs`
- Modify: `.gitattributes` (`tests/SqlSource.Tool.Tests/Fixtures/Bundles/** text eol=lf`), `.editorconfig` (a `[tests/SqlSource.Tool.Tests/Fixtures/Bundles/**]` section as the `Describers` one), `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj` (`None Remove` and `None Include ... PreserveNewest`, and its comment), `tests/README.md` (the bundle fixtures, and the procedure: unzip a bundle into `Fixtures/Bundles/<engine>/<case>/`, keep `bundle.json`, run the test; never recorded again; deleted when the capture's interface moves past it), `src/SqlSource.Tool/Describing/AGENTS.md` (the byte-for-byte rule names `Bundles` too)

**Interfaces:**
- Consumes: `BundleReader`, `BundleReplay`, `BundleDescribers` (Tasks 2-3).
- Produces: `BundleCases.Of(string engine) -> IReadOnlyList<FixtureCase>` over `AppContext.BaseDirectory/Fixtures/Bundles`.

- [ ] **Step 1: Failing test** `BundleCasesTests.Replay_EveryBundleCase_GivesItsExpectedAnswer`: theory over both engines' cases; `expected.json`'s entry equals the replayed one, or `bundle.json`'s failure id and step equal the `Refused`; plus `Of_EachEngine_HasACase` so an empty folder fails.
- [ ] **Step 2:** Run `--filter-class "*BundleCasesTests"`; FAIL.
- [ ] **Step 3:** Add the cases, the wiring and the docs.
- [ ] **Step 4:** Run the filter; PASS.  Run `git ls-files --eol tests/SqlSource.Tool.Tests/Fixtures/Bundles` and see `i/lf` for every file.
- [ ] **Step 5:** Commit `test(tool): bundle fixtures and their replay`.

### Task 6: The mapping: keyed hash, classes and the mapping file

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/NameClass.cs`, `RenamingKey.cs`, `NameMapping.cs`, `MappedName.cs`, `MappingFile.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`, `MappingFileTests.cs`

**Interfaces:**
- Produces:
  - `enum NameClass { Schema, Table, Column, Alias, Function, Type, Index, Parameter, Token, Query, Database, Literal }`; `static class NameClasses { string PrefixOf(NameClass c); string WordOf(NameClass c); }` — prefixes `s_ t_ c_ a_ f_ y_ i_ p_ k_ q_ d_ l_`, words `schema table column alias function type index parameter token query database literal`.
  - `sealed class RenamingKey { static RenamingKey Random(); static RenamingKey Of(ReadOnlySpan<byte> bytes); }` — 32 bytes, `RandomNumberGenerator`; `Of` throws for another length; `ToString` writes no byte.
  - `sealed record MappedName(string Replacement, NameClass Class, string Original)`.
  - `sealed class NameMapping(RenamingKey key, Func<string, bool, string> fold)` with `string Name(string spelling, bool quoted, NameClass cls)`, `void Keep(string spelling, bool quoted)`, `bool TryGet(string spelling, bool quoted, out string replacement)`, `string Literal(string content)`, `bool IsReplacement(string text)`, `IReadOnlyList<MappedName> Names` (in the order made, kept names left out).
  - `static class MappingFile { string Write(IReadOnlyList<MappedName> names); }` — `{"mappingVersion": 1, "names": [{"replacement": "t_1a2b3c4d", "class": "table", "original": "users"}, ...]}`, indented, `\n`.

The hash: `HMACSHA256(key, UTF-8 of "<class word>:<folded spelling>")` in lower-case hex, cut to 8 digits after the prefix; a replacement already given lengthens by 2 digits until free.  A spelling's key is `fold(spelling, quoted)`; its class is the first one given; a `Keep`ed spelling maps to itself and wins over a later `Name` (the engine's row first, as the prototype).  A literal's key is its content, class `Literal`.

- [ ] **Step 1: Failing tests:** `Name_SameKeyAndSpelling_GivesTheSameName`; `Name_OtherKey_GivesAnotherName`; `Name_IsPrefixAndEightLowerHexDigits` (`ShouldMatch("^t_[0-9a-f]{8}$")`); `Name_SameSpellingAnotherClass_KeepsTheFirstClass`; `Name_FoldedAlike_IsOneName` (fold lower-cases unquoted: `Users` and `users` one; `"Users"` quoted another); `Name_Collision_LengthensTheHash` (a fold that maps two spellings to keys whose 8-digit cut is forced equal through a test seam is not possible with HMAC: instead seed the mapping's used set by naming many spellings and assert all replacements distinct and `IsReplacement` true; plus a direct test of the private lengthening through an `internal static string Cut(string hex, int length)`); `Keep_ThenName_StaysVerbatim`; `Literal_IsClassLiteral`; `RenamingKey_ToString_HoldsNoByte`; `MappingFile_Write_ListsEachRenamedName`.
- [ ] **Step 2:** Run `--filter-namespace "SqlSource.Tool.Tests.Bundles.Renaming"`; FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(tool): the renaming's keyed names and mapping file`.

### Task 7: The engine interface and the SQL renamer

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/IEngineRenaming.cs`, `FieldRule.cs`, `IRenamingScope.cs`, `RenamingField.cs`, `RenamingCall.cs`, `RenamingRefusal.cs`, `RenamedText.cs`, `SqlRenamer.cs`, `EngineRenamings.cs`
- Create (tests): `tests/SqlSource.Tool.Tests/Bundles/Renaming/FakeEngineRenaming.cs` (the engine part of `FakeDescriber`, at PostgreSQL's lexer rules), `SqlRenamerTests.cs`, `EngineRenamingsTests.cs`

**Interfaces:**
- Consumes: Task 6.
- Produces (the contract 10.2 and 10.3 implement; its shape is not changed by either):

```csharp
internal interface IEngineRenaming
{
    // The dialect whose SqlDialectRules the generator's lexer reads the SQL by.
    SqlDialect Dialect { get; }

    // The engine's word rule over a Text lexeme.
    bool IsWordStart(char character);
    bool IsWordPart(char character);

    // A spelling as the engine compares it: PostgreSQL lower-cases an unquoted one; SQL Server ignores case.
    string Fold(string spelling, bool quoted);

    // Kept verbatim: a keyword or word of the engine's vocabulary, its own schema or database, a name the
    // planner or the tool makes ("[1]" of AS [n]).  Folded by the engine before it is compared.
    bool IsEngineWord(string spelling, bool quoted);

    // The rule of a member of a call ("open", "prepare", ...) or of the entry (method BundleFiles.Expected), by
    // its path from the call ("request.sql", "answer.rows[].name", "answer.error.message") or from the entry's
    // type record ("columns[].type.name").  Null: the member has no rule (a string with none is a refusal).
    FieldRule? RuleOf(string method, string path);

    // Whether a row of an answer is the engine's own, whose names are kept: a type of pg_catalog, a row of sys
    // or of a system database.
    bool IsEngineRow(string method, string path, JsonObject row);

    // A copy of a reused parameter made by the pipeline, "@before_1" of "@before": the parameter and the text
    // after it, or null.  Asked for every parameter name that is not one of the query's.
    (string Parameter, string Suffix)? CopyOf(string name, IReadOnlySet<string> queryParameters);

    // Names only the engine finds, before the query's SQL is read: a plan's name keys.  After the catalog fields.
    void Register(RenamingCall call, IRenamingScope scope);

    // A string of a FieldRuleKind.Own member renamed: a plan, an error's message, a setting's value.
    string RenameOwn(RenamingField field, IRenamingScope scope);

    // The words of a renamed Own member, for the token check (a plan string through PlanTokens).
    IEnumerable<string> WordsOf(RenamingField field);

    // After every string is renamed: what the pipeline derives by its own rule (a list in name order sorted again,
    // a position mapped through RenamedText.MapPosition).  May refuse through the scope.
    void Derive(RenamingCall call, IRenamingScope scope);
}
```

  - `enum FieldRuleKind { Drop, Fixed, Closed, Sql, TypeSpelling, Label, Parameter, Name, WholeName, Own }`; `readonly record struct FieldRule(FieldRuleKind Kind, NameClass Class = NameClass.Alias)` with statics `Drop`, `Fixed`, `Closed`, `Sql`, `TypeSpelling`, `Label`, `Parameter`, `Own`, and `Name(NameClass)`, `WholeName(NameClass)`.  `Fixed` is a text of the tool or the engine, `Closed` a closed code: both kept and not checked.  `Sql` new words are `Alias`, `TypeSpelling`'s `Type`; a word before `(` is a `Function`.  `WholeName` is one name of the whole string (a user's assembly-qualified CLR type), kept when `IsEngineWord`.
  - `interface IRenamingScope { string Name(string spelling, bool quoted, NameClass cls); void Keep(string spelling, bool quoted); bool IsMapped(string spelling, bool quoted); string Literal(string content); RenamedText Sql(string sql, NameClass newWords); string Parameter(string name); void Refuse(RenamingRefusal refusal); }`
  - `sealed record RenamingField(int Call, string Method, string Path, string Value, JsonObject? Row)`; `sealed record RenamingCall(int Index, string Method, JsonObject Original, JsonObject Renamed)` (the call's `request` and `answer` under each).
  - `enum RefusalStep { RequestSql, PlanString, Token, Replay }` (the design's steps 8, 9, 12, 13); `sealed record RenamingRefusal(RefusalStep Step, string Place, string? Token)` — `Place` is `<file>` or `capture.json call <n> '<method>' <path>`.
  - `sealed record RenamedText(string Text, ImmutableArray<int> Offsets) { int MapPosition(int position); }` — `Offsets[i]` is the renamed code-point index of original code point `i` (one past the end included); `MapPosition` takes and gives a 1-based position, as PostgreSQL's.
  - `sealed class SqlRenamer(IEngineRenaming engine, NameMapping mapping, IReadOnlySet<string> queryParameters)` with `RenamedText Rename(string sql, NameClass newWords, string place, Action<RenamingRefusal> refuse)` and `RenamedText RenameQueryFile(string text, Action<RenamingRefusal> refuse)`.
  - `static class EngineRenamings { IEngineRenaming? For(DatabaseEngine engine); }` — `PostgreSql => null, // Built by 10.2.` and `SqlServer => null, // Built by 10.3.` on lines of their own, `_ => throw UnexpectedEnumValueException.Of(engine)`.

The SQL renamer, lexeme by lexeme of `SqlLexer.Lex(sql, SqlDialectRules` of `engine.Dialect`) (a lex error is a refusal `Token` at the place):
- `Text`: words by the engine's rule; a word is kept if `IsEngineWord`, else `mapping.Name(word, quoted: false, cls)`; SQL Server's `@@name` and the `@`/`#` prefixes of a word are kept before its renamed rest; a one-letter `E N B X U` (or `U&`) right before a `Quoted` lexeme is the literal's prefix and kept; a number (digit, then word parts and `.`) kept; white space kept; punctuation kept when it is in the closed set ``!%&()*+,-./:;<=>?@[]^`{|}~\$#'"`` (ASCII only); any other character is a refusal `Token` naming it.
- `Quoted`: opened by `'` (with a prefix in the text before), or a dollar tag: a literal, `'` + `mapping.Literal(content)` + `'` in the same form (`$tag$` kept for a dollar one, the tag being the engine's text); opened by `"`, `U&"` or `[`: a name, kept when `IsEngineWord(inner, quoted: true)`, else the same delimiters around `mapping.Name(inner, quoted: true, NameClass.Alias)`; a name ending in `!` or `?` keeps the mark after its renamed stem, and `?column?` is kept whole; a name of digits alone is kept only through `IsEngineWord`.
- `Parameter`: its prefix kept; the name through `engine.CopyOf` when it is no query parameter (renamed `<renamed parameter><suffix>`), else `mapping.Name(name, quoted: false, NameClass.Parameter)`.
- `LineComment` becomes `--`; `BlockComment` `/*` + its line breaks + `*/`; `Hint` keeps its opener and `*/` and renames the words between as `Text`.
- `RenameQueryFile`: as `Rename`, but a line comment that is a marker keeps its marker with its value renamed: `-- name:` `Query` (its result shape word kept), `-- dialect:` verbatim, `-- database:` `Database` (kept when it is the dialect's canonical name), `-- param:` the parameter as above, its type as `TypeSpelling`, its `null`/`not null` kept, and its `-> <C# type>` dropped; `-- token:` the token's name `Token` and its default as SQL; `-- token-ignore:` its text as SQL.  Any other marker is a refusal `Token` naming the marker, since a bundle's `query.sql` holds none (10.4 writes only these).  Token placeholders `{{name}}` and `{{name:default}}` in the SQL: the name `Token`, the default as SQL.

- [ ] **Step 1: Failing tests** (`SqlRenamerTests`, with `FakeEngineRenaming` whose vocabulary is `select from where join on as and or not null left count` and the schema `public`): `Rename_Words_KeepsEngineWordsAndRenamesTheRest` (`SELECT u.id FROM users u` → `SELECT a_… .a_… FROM a_… a_…` with `users` and `u` distinct, `SELECT`/`FROM` verbatim); `Rename_StringLiteral_KeepsItsForm` (`E'x'`, `N'x'`, `U&'x'`, `$q$x$q$` each `<form>l_…`); `Rename_SameLiteralTwice_IsOneName`; `Rename_Comments_AreEmptiedKeepingLineBreaks`; `Rename_Hint_KeepsItsFormAndRenamesWords`; `Rename_QuotedName_KeepsDelimitersAndNullabilityMark` (`"name!"`, `[x?]`, `"?column?"`); `Rename_Numbers_AreKept`; `Rename_CopyOfAReusedParameter_IsRenamedThroughIt` (a fake `CopyOf` for `_<n>`); `Rename_NonAsciiPunctuation_IsRefused` (`SELECT 1 ≠ 2` → `Token` `≠`); `MapPosition_AfterALongerName_MovesByTheDifference`; `RenameQueryFile_Markers_KeepTheirKindAndRenameTheirValues`; `RenameQueryFile_UnknownMarker_IsRefused`.  `EngineRenamingsTests.For_EachEngine_IsNotBuiltYet` (both null).
- [ ] **Step 2:** Run `--filter-namespace "SqlSource.Tool.Tests.Bundles.Renaming"`; FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(tool): the engine part's interface and the renaming of SQL`.

### Task 8: Renaming a capture and an entry, the token check and the proof

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/CaptureRenamer.cs`, `EntryRenamer.cs`, `TokenCheck.cs`, `RenamingProof.cs`, `BundleRenamer.cs`, `RenamedBundle.cs`
- Modify (tests): `FakeEngineRenaming.cs` (the field table of `FakeDescriberCalls.cs`' `open` and `describe`, and of `PostgresType`'s members in the entry)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/BundleRenamerTests.cs`, `TokenCheckTests.cs`, `CaptureRenamerTests.cs`, `EntryRenamerTests.cs`

**Interfaces:**
- Consumes: Tasks 3, 6, 7.
- Produces:
  - `sealed record RenamedBundle(Bundle Bundle, IReadOnlyList<MappedName> Mapping)`.
  - `static class BundleRenamer { Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(Bundle raw, IEngineRenaming engine, IQueryDescriber describer, RenamingKey key, CancellationToken token); }` — needs `raw.Info` with `Renamed: false` (`ArgumentException` otherwise: 10.4 reports `--scrub` of a renamed bundle before it calls).

The steps, in this order, the first refusal ending the run:
1. **Drop** (`CaptureRenamer`): every member whose `RuleOf` is `Drop`, at any depth, from each call's request and answer.
2. **Register**: walk each answer, then each request: a `Name` member is `Keep` in a row `IsEngineRow`, else `Name`; then `engine.Register` for each call; then the query's parameters (lexer `Parameter` lexemes of `query.sql`) as `Parameter` names.
3. **Rename `query.sql`** with `RenameQueryFile`.
4. **Rename every string** of each call by its rule (`Fixed`/`Closed` kept; `Sql` and `TypeSpelling` through the SQL renamer; `Label` a literal; `Parameter` through the scope; `Name` kept in an engine row unless mapped, else named; `WholeName` one name or kept; `Own` through `engine.RenameOwn`); a string with no rule is a refusal `Token` at its place; numbers, booleans and nulls kept.  Then `engine.Derive` for each call.
5. **The entry** (`EntryRenamer`), when `Expected` is not null: the neutral members by this table, the type record's through `engine.RuleOf(BundleFiles.Expected, path)`: the query's key and `name` → `Query`; `columns[].name`, `columns[].origin.column` → `Column`; `columns[].origin.table`, `matchesTable.table` → `Table`; `columns[].origin.schema`, `matchesTable.schema` → `Schema`; `parameters[].name` → `Parameter`; `database` → `Database` (kept when the dialect's name); the provenance values, `resultKind`, `serverVersion`, `nullableSource`, `typeSource` and the like `Fixed`.  Its `hash` is `BundleReplay.Plan`'s of the renamed `query.sql`.  Written back with `SidecarWriter` as a one-entry sidecar with the raw `expected.json`'s tool version.
6. **Token check** (`TokenCheck.Check`): every string of the renamed capture's calls by its rule, `query.sql`, and the entry's strings: `Sql`/`TypeSpelling`/`query.sql` split by the lexer and the word rule, `Own` by `engine.WordsOf`, `Name`/`Parameter`/`Label`/`WholeName` whole (a mark `!` or `?` aside); each token must be `mapping.IsReplacement`, `engine.IsEngineWord`, a number, or one punctuation character of the closed set; a quoted literal must hold a replacement.  The first that is none is a refusal `Token` naming its place and the token.  `Fixed` and `Closed` are not checked.
7. **Proof** (`RenamingProof.ProveAsync`): the renamed bundle replayed by `BundleReplay.RunAsync` with `describer`; `Missed` is a refusal `RequestSql` at `capture.json call '<method>'` (step 8's check); an outcome other than the renamed `expected.json`'s entry (`SidecarEntry` equality, whole) or the raw failure's id and step is a refusal `Replay` with its first difference as the token.
8. The renamed bundle: `bundle.json` `Renamed: true`, the rest of its info as the raw one's; `query.sql`, `capture.json` (`CaptureJson.Write(DescribeCapture)`), `expected.json` renamed; `explain-settings.txt` as it is.

- [ ] **Step 1: Failing tests** with `FakeDescriber` (dialect PostgreSql) told a `TypedRows` description for a query `-- name: GetUsers\nSELECT u.id, u.tags FROM users u WHERE u.status = 'active' AND u.id = @id;`, captured through `CapturingExchange` into a raw `Bundle` with an `expected.json` from `FixtureReplay`'s steps: `RenameAsync_FakeBundle_ReplaysToItsRenamedEntry`; `RenameAsync_FakeBundle_LeaksNoOriginalName` (none of `users`, `GetUsers`, `status`, `active`, `tags` in any file of the renamed bundle, by ordinal ignoring case, as whole words); `RenameAsync_TwoKeys_GiveOtherNamesAndTheSameEntryShape`; `RenameAsync_MappingHoldsEachPair`; `RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace` (a fake rule table missing `answer.description.columns[].name`); `RenameAsync_DerivedRequestNotMade_IsRefusedAtTheRequestSql` (a fake `Derive` that changes a request); `RenameAsync_EntryThatDiffers_IsRefusedAtReplay` (a fake `RuleOf` that renames a `Fixed` provenance value); `RenameAsync_RefusedQuery_KeepsItsFailure` (`FakeDescriber` told a failure); `RenameAsync_RenamedBundle_Throws`.  `TokenCheckTests`: `Check_UnknownWord_IsRefusedNamingIt`; `Check_ReplacementsAndEngineWords_Pass`; `Check_LiteralThatIsNoReplacement_IsRefused`.  `CaptureRenamerTests.Drop_RemovesAtEveryDepth`; `EntryRenamerTests.Rename_HashIsOfTheRenamedQuery`.
- [ ] **Step 2:** Run `--filter-namespace "SqlSource.Tool.Tests.Bundles.Renaming"`; FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS, and the whole `tests/SqlSource.Tool.Tests` project.
- [ ] **Step 5:** Commit `feat(tool): the renaming's engine-neutral steps, its token check and its proof`.

### Task 9: Documentation and the rules

**Files:**
- Create: `docs/architecture/bundles.md` — the bundle (layout, `bundle.json`, reader, writer), `replay` (the command's steps, its lines, exit codes, its errors), `Fixtures/Bundles/`, and **the renaming's contract**: the steps in order with who does each (neutral or engine), `IEngineRenaming` member by member with what each must and must not do, `FieldRule` kinds, the refusal steps, why step 8 is the proof's miss, `EngineRenamings.For` with its two "Built by 10.2/10.3" arms, and an empty `## PostgreSQL` and `## SQL Server` section each saying "Built in sub-phase 10.2 / 10.3", which those units fill.
- Create: `src/SqlSource.Tool/Bundles/AGENTS.md` — rules: the interface's shape is fixed by its contract in `bundles.md` and changes only by agreement of both engines' parts; a renaming refuses and never guesses or blanks; a member with no rule is a refusal; everything an engine decides is behind `EngineRenamings.For`'s switch; `BundleReplay` is the one replay of a bundle, which `replay` and the proof share; a bundle reads no log; the maintenance footer.
- Modify: `docs/architecture/tool.md` (the "Not built yet / Bug-report commands" section replaced by a pointer to `bundles.md`; a `Bundles/` and `Bundles/Renaming/` row in the folders table; the command line and host paragraphs name `replay` and `OpenReadFile`), `docs/architecture/README.md` (a glossary entry for bundle, beside capture), `docs/architecture/postgres-describer.md` line 48 (a capture's members bind the renaming's list of fields, `bundles.md`), `src/SqlSource.Tool/Logging/RunEvents.cs` and `LogMember.cs` comments, `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs` summary ("the file of a fixture and of a bundle"), `src/SqlSource.Tool/AGENTS.md` (amend, no new bullet: the exit-code bullet "`2` is for `SQLSRC219` and for `replay`'s answer that is not the bundle's", the events bullet "read by people and by `replay`'s lines"), `docs/manual/tool.md` (the command reference's synopsis gains `dotnet sqlsource replay <bundle> [--verbose] [--log <path>]`; a section `<a id="replay"></a> ### Checking a bundle with replay`, example first: what a user given a bundle (zip or unzipped folder) runs and sees, the three outcomes, that it needs no database or connection, that a bundle not renamed says so; the exit codes table: `2` also "`replay` gave another answer than the bundle's"; `--log` and `--verbose` for `replay`), `docs/manual/README.md` (the "Bug-report bundles" row: "Coming", its cell "Making a bundle is coming; [checking one with `replay`](tool.md#replay) works today"), `CONTRIBUTING.md` only if it lists the tool's commands.
- Test: `tests/SqlSource.Tests/Documentation/` (links, anchors, samples, AGENTS limits) through the validation.

- [ ] **Step 1:** Write the pages and edits; grep the manual pages changed for `describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0` and rewrite each hit.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Documentation"` and `--filter-class "*DiagnosticsDocumentTests"`; PASS.
- [ ] **Step 3:** Commit `docs: the bundle, replay and the renaming's contract`.

### Task 10: Validate

- [ ] **Step 1:** Run `./format.sh` (the repository's formatter, `CONTRIBUTING.md`), then `./pre-commit-validation.sh --full` in the foreground; expected PASS.  No describer changed, so no floor-image run.
- [ ] **Step 2:** Commit any formatting change.
