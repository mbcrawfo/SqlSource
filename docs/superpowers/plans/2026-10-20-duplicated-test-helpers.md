# Duplicated helpers of the model and container tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0064: one shared helper for each thing the model tests and the container tests now write several times, two large classes split by subject, and the names and magic strings the review found, with no test lost.

**Architecture:** Shared helpers go to the two libraries of `tests/` by the rule of `tests/AGENTS.md`: what needs nothing of the tool, and what `tests/SqlSource.Tests.RoslynFloor` compiles, to `tests/SqlSource.Testing` (Roslyn 4.8.0, C# 12); what needs the tool to `tests/SqlSource.Tool.Testing`.  The helpers that phase 6's `tests/SqlSource.Tests.SqlServer` will need are engine-neutral where that costs nothing: the database seeding takes a `DbDataSource`, the describer's open is an extension of `IQueryDescriber`, and the request builder takes the dialect's rules.  Nothing SQL Server-specific is built.

**Tech Stack:** C# on .NET 10, xunit v3, Shouldly, Roslyn 4.8.0 for `tests/SqlSource.Testing`, Npgsql 10 and Testcontainers for the container tests.

**Spec:** [`docs/tech-debt/TD-0064-duplicated-helpers-of-the-model-and-container-tests.md`](../../tech-debt/TD-0064-duplicated-helpers-of-the-model-and-container-tests.md), its Problem and Proposed fix sections, read beside [TD-0039](../../tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md) and the "Phase 6 - SQL Server" section of `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`.

## Decisions

- **Item 1: `tests/SqlSource.Testing/Generator/ModelRuns.cs`**, not a support folder of `Generator/`.  The library is already where the harness of `Generator/` lives, both projects reference it, and `TestLayoutTests` stays as it is.  The same library takes the helpers of item 7, `CachingScene` and `StepReasons`.
- **One pull request**, about ninety files, under the limit of 150.  The items touch disjoint helpers but many of the same test files (item 8 reaches most of `Generator/`), so parts would conflict with each other and gain no review that one does not.
- **Where the code differs from the TD:**
  - `ModelTests` is now about 1,370 lines (the TD says 1,180): record-arrays (TD-0062) added the group of a record with an array column.  That group stays in `ModelTests`; only the obsolete and experimental group moves to `ModelWarningTests`.
  - `TypeOverrideLevelTests` gained the SQLSRC261 tests of array-keys (TD-0059), with helpers of its own (`EnumType`, `Composite`, `ArrayOf`, `ArrayEntryError`, `TextsOf`).  Those stay in the class: no other class uses them.  Its `Run` takes a list of files with their entries and a property; it calls `ModelRuns.Run` and keeps the line that builds the file list.
  - `/app/Repo/Users.sql` is declared eleven times and written about ninety times (the TD says seventy) in twenty-three files of `tests/SqlSource.Tests`; the normalised form `app/Repo/Users.sql` is in `DialectSettingTests`, `MSBuildSettingsTests` and `TypeEmitterTests`.
  - `EndToEndTool` already passes `CancellationToken.None`, which TD-0065 item 3 records.  `CliRun.RunAsync` passes the test's token, so deleting `EndToEndTool` for `CliRun` (item 4) resolves TD-0065 item 3 too.  TD-0065 says so and keeps the review's numbers of its other items; none of its other items is taken on.
- **Not done here:** `GeneratorRun.ShouldBeClean()` and the three private `AssertClean` are TD-0039 item 5's, and the split of `SqlFileParserTests` is TD-0039 item 5's; item 2 below changes the facts it names inside the one class.  TD-0039 stays open, and its count of `AssertClean` stays true.

## Global Constraints

- `tests/SqlSource.Testing` uses only API that Roslyn 4.8.0 has and hands the compiler C# 12 at most; it references no package but those of its `.csproj` (no Npgsql reference, only the `PackageDownload`).
- Every file of `tests/SqlSource.Tests/Generator/` starts with `// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.` and its name ends in `Tests.cs`.
- Every test class is `sealed`; test names are `Method_Scenario_Outcome`; assertions are Shouldly's.
- Types of the two libraries stay `internal`.
- Every test that runs the generator passes an `OutputKind`; the model helpers pass `OutputKind.Models`.
- No test is lost: the count of each project is the baseline's, unless a task below says it changes and by how much.  Baseline on `b9a05e8`: `SqlSource.Tests` 3551, `SqlSource.Tests.RoslynFloor` 643, `SqlSource.Tool.Tests` 3131 (1 skipped), `SqlSource.Tests.Postgres` 1343 (81 skipped), `SqlSource.Tests.Postgres.Npgsql8` 3; 8671 in all.
- Every commit passes `./pre-commit-validation.sh`; before the push, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` passes too.
- An `AGENTS.md` or readme that names a moved, renamed or deleted file or type changes in the same commit.
- Comments match the files around them: one plain sentence or two above a member saying what it is, in the house style ("An entry of the query for its current hash: ...").

## Review Focus

- **A moved test that no longer runs.**  A test left without `[Fact]`, or a class that is not `public`, compiles and runs nothing.  Each task that moves tests compares the count of its project with the baseline.
- **A helper whose default differs from the copy it replaces.**  `Column` defaults to `nullable: false` and `Int4`; `Parameter` to `Int4` and `nullable: null`; `QueryTypesTests` passes a `null` type on purpose (a parameter without a type), so it keeps its own `Parameter` and `Column`, which call `TestSidecars` directly.
- **The step-name guard of `CachingTests`.**  `AllReasons` fails a test on a step that is not among the tracked steps.  After the split, every class still reads its reasons through `StepReasons`, and `tests/AGENTS.md` names the classes that make the step names mean something.
- **A real-host run that reads the test process's environment.**  `CliRun.Real` must not read `CliRun.Environment` or use `FakeProcessRunner`; it is the real `dotnet msbuild` and `Environment.GetEnvironmentVariable`, as `EndToEndTool` was.
- **The seed's order.**  Schema before seed, and the realistic seed's NOT NULL hole comment kept where the realistic database is made.

---

### Task 1: `ModelRuns` and the seven model test classes (item 1)

**Files:**
- Create: `tests/SqlSource.Testing/Generator/ModelRuns.cs`
- Create: `tests/SqlSource.Tests/Generator/ModelRunsTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs`, `ModelNameTests.cs`, `ModelTests.cs`, `QueryTypesTests.cs`, `SharedModelTests.cs`, `TableModelTests.cs`, `TypeOverrideLevelTests.cs` (the helpers at each class's end, and the `Source`, `Int4`, `Text` fields at its start)
- Modify: `tests/SqlSource.Tests/Generator/AGENTS.md` (the list of harness files), `tests/README.md` (the row of `tests/SqlSource.Testing`)

**Interfaces:**
- Produces, in namespace `SqlSource.Testing.Generator`, `internal static class ModelRuns`:
  - `public const string Source` — the text that five classes declare today: `using SqlSource;` and `namespace App { [SqlSourceGenerate] public partial class Sample; }`.
  - `public static readonly PostgresType Int4` — `TestSidecars.Postgres(name: "integer", internalName: "int4")`; `public static readonly PostgresType Text` — `TestSidecars.Postgres(name: "text", internalName: "text")`.
  - `public static GeneratorRun Run(string source, SqlFile[] files, IReadOnlyDictionary<string, string?>? properties = null, MetadataReference[]? references = null)` — `GeneratorHarness.Run(OutputKind.Models, [new SourceFile(GeneratorHarness.SourcePath, source)], files, references: references ?? [NpgsqlReferences.Npgsql10], dialect: "postgres", properties: properties)`.
  - `public static GeneratorRun Run(SqlFile file, params SidecarEntry[] entries)` and `Run(SqlFile file, SidecarEntry[] entries, IReadOnlyDictionary<string, string?>? properties)` — `Run(Source, [file, Sidecars.Of(file, entries)], properties)`.
  - `public static SidecarEntry Entry(SqlFile file, string name, SidecarParameter[] parameters, SidecarColumn[]? columns, SidecarTable? matchesTable = null)` — the body every class has now: `Sidecars.HashOf(file, name, SqlDialect.PostgreSql)`, `"postgres"`, `"main"`, `"18.0"`, `Rows` when `columns` is not null and `None` when it is, and each parameter and column renumbered by its place with `with { Ordinal = ordinal }`.
  - `public static SidecarParameter Parameter(string name, PostgresType? type = null, bool? nullable = null)` — `TestSidecars.Parameter(0, name, type ?? Int4, nullable)`.
  - `public static SidecarColumn Column(string name, PostgresType? type = null, bool? nullable = false, SidecarOrigin? origin = null, bool? identity = null, bool? computed = null)` — `TestSidecars.Column(0, name, type ?? Int4, nullable, origin: origin, identity: identity, computed: computed)`.
  - `public static PostgresType Catalog(string internalName)` — `TestSidecars.Postgres(name: internalName, internalName: internalName)`.
  - `public static string[] ModelFiles(GeneratorRun run)` — the hint names that hold `"-Model."`, ordinal order.
  - `public static FileModels ModelsOf(GeneratorRun run)` — `StepOutputs.Single<FileModels>(run, TrackingNames.FileModels)`.

- [ ] **Step 1: Write the failing tests** in `ModelRunsTests` (the shared first line, `public sealed class ModelRunsTests`):
  - `Entry_ParametersAndColumns_AreNumberedByTheirPlace`: `Entry(file, "GetUser", [Parameter("a"), Parameter("b")], [Column("x"), Column("y")])` has parameter ordinals `[0, 1]`, column ordinals `[0, 1]`, `ResultKind` `Rows`, and `Hash` equal to `Sidecars.HashOf(file, "GetUser", SqlDialect.PostgreSql)`.
  - `Entry_NoColumns_HasNoRows`: with `columns: null`, `ResultKind` is `None` and `Columns` is null or empty as `TestSidecars.Entry` gives it.
  - `ModelFiles_Run_AreTheModelsHintNamesInOrder`: a run of a file with an entry that has rows and a parameter gives two names, each holding `"-Model."`, in ordinal order.
- [ ] **Step 2:** `dotnet test --project tests/SqlSource.Tests -- --filter-class "*ModelRunsTests"` fails to compile: `ModelRuns` does not exist.
- [ ] **Step 3:** Write `ModelRuns` as the Interfaces block says, with a header comment saying it is the harness of the model tests of `Generator/`, within Roslyn 4.8.0 like the rest of the folder.
- [ ] **Step 4:** The three tests pass.
- [ ] **Step 5:** In each of the seven classes, delete the copies and use `ModelRuns` (`using static SqlSource.Testing.Generator.ModelRuns;` where the class keeps no member of the same name; `ModelRuns.X` where it does).  A class keeps a private helper only where its body differs from the shared one:
  - `ColumnMarkerTests` and `ModelNameTests` keep a `Run` of two lines that calls `ModelRuns.Run` and asserts `CompilationErrors` and `GeneratedCodeWarnings` empty; `ColumnMarkerTests`'s two `Entry` overloads become calls of the shared `Entry` (its argument order changes at each call: name, parameters, columns).
  - `QueryTypesTests` keeps its `Driver`, its own `Parameter` and `Column` (a `null` type is a test's input there), and passes `references` to `ModelRuns.Run`; its `Catalog` goes.
  - `TableModelTests` passes `matchesTable` to the shared `Entry`; its `Column(name)` is `Column(name)` of `ModelRuns`.
  - `TypeOverrideLevelTests` keeps its own `Entry(file, name, params PostgresType[] columns)` (columns named `c0`, `c1`), written over the shared `Entry`; its `Catalog` and `Int4` go.
  - `ModelTests`, `SharedModelTests`, `TableModelTests` keep `AssertClean` (TD-0039).
- [ ] **Step 6:** `dotnet build SqlSource.slnx` and `dotnet test --project tests/SqlSource.Tests` and `--project tests/SqlSource.Tests.RoslynFloor` pass; the counts are 3554 and 646 (the baseline and the three new tests, which RoslynFloor compiles too).
- [ ] **Step 7:** In `Generator/AGENTS.md`, the harness list names `ModelRuns`; in `tests/README.md`, the row of `tests/SqlSource.Testing` names `ModelRuns`, the harness of the model tests.  Run `./pre-commit-validation.sh`; commit `Share the model tests' harness in ModelRuns`.

### Task 2: `CachingTests` split by step, and `ModelWarningTests` (item 7)

**Files:**
- Create: `tests/SqlSource.Testing/Generator/CachingScene.cs`, `tests/SqlSource.Testing/Generator/StepReasons.cs`
- Create, in `tests/SqlSource.Tests/Generator/`: `CachingParseTests.cs`, `CachingOptionsTests.cs`, `CachingSidecarTests.cs`, `CachingCheckTests.cs`, `CachingMappedTypeTests.cs`, `CachingTypeTests.cs`, `CachingModelTests.cs`, `ModelWarningTests.cs`
- Delete: `tests/SqlSource.Tests/Generator/CachingTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/ModelTests.cs` (the group from `Run_GlobalClassSortedWithAnObsoleteType_CompilesWithoutAWarning` to `Run_TypeWithAMalformedAttribute_Compiles` leaves), `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `tests/README.md`, `tests/SqlSource.Tests/TestLayoutTests.cs` only if it names `CachingTests`

**Interfaces:**
- Consumes: `ModelRuns` of Task 1 where a caching test of models builds a sidecar entry.
- Produces, in `SqlSource.Testing.Generator`:
  - `internal sealed class CachingScene` — the fixture that `CachingTests` holds in fields today: the sources (`UsersSourcePath`, `UsersSource`, `Sources`), the files `Users`, `Orders`, `Payments` and their sidecars, the first runs (`FirstRun`, `FirstRunWithSidecars`, `FirstRunWithTypes`, `FirstRunOfModels`), `ModelsCompilation`, `OptionsWithTypeOverrides`, `TypedSidecar`, `CurrentSidecar`, `ToSqlFile`, and the compilation edits (`WithMethodBodyEdited`, `WithParametersOnUserQueries`, `WithArgumentsOnUserQueries`).  Each keeps its present name, signature and comment; an instance member stays instance, a static one static.
  - `internal static class StepReasons` — the readers of a run: `Run`, the step-output enumerator, `Reasons<T>`, `ParseInputs`, `AllReasons`, `OutputReasons`, `Checks`, `NeedsKey`, `ModelReasons`, `HintNameOf`, `ModelsRemoved`, `TypesOf`, with their comments.  `AllReasons` keeps its guard: a step that is not among the tracked steps fails the test.

- [ ] **Step 1:** Move the fixture and the readers into the two library files; their accessibility becomes `public` within the `internal` types.
- [ ] **Step 2:** Move the tests, unchanged, into the classes by their subject.  Each class holds `private readonly CachingScene _scene = new();` when it needs the instance fixture, `using static SqlSource.Testing.Generator.StepReasons;`, and a header comment of one sentence naming its step:
  - `CachingParseTests`: the edits of `.sql` and C# files and the files added (`Run_SqlFileEdited_ParsesOnlyThatFileAndEmitsOnlyTheTypeThatUsesIt` to `Run_SqlFileAddedToATypesFolder_EmitsOnlyThatTypeAgain`, and `Run_CSharpEdit_RunsTheProbeAndNothingAfterIt`);
  - `CachingOptionsTests`: the generator parameters, the dialect and `keep-comments` (`Run_GeneratorParametersPropertyChanged_EmitsEveryTypeAgainAndParsesNoFile` to `Run_MetadataOfOneFileGainsKeepComments_ParsesOnlyThatFile`);
  - `CachingSidecarTests`: `Run_SidecarEdited_ReadsOnlyThatSidecarAndParsesNoFile` to `Run_SameSidecarListedTwice_KeepsOneSidecar`;
  - `CachingCheckTests`: `Run_SqlFileEdited_ChecksOnlyThatFile` to `Run_ProjectAtSql_ComputesNoHash`, with the comment of TD-0019 kept on its test;
  - `CachingMappedTypeTests`: `Run_CSharpEdit_ResolvesTheMappedTypesAndNothingAfterThem` to `Run_SqlEditThatLeavesTheMarkersAlone_ChangesNoMappedType`;
  - `CachingTypeTests`: `Run_CSharpEdit_TypesNoQuery` to `Run_NpgsqlReferenceAdded_TypesEveryFileAgain`;
  - `CachingModelTests`: `Run_SqlFileEditedChangingOneModel_EmitsOnlyThatModelAgain` to `Run_ProjectWithoutModels_LooksUpNoName`.
- [ ] **Step 3:** Move the obsolete and experimental group of `ModelTests` into `ModelWarningTests`, with the helpers only it uses moved too and those both use taken from `ModelRuns`; its header says it holds the warnings a model gives for the obsolete and experimental types it names.
- [ ] **Step 4:** `dotnet test` of `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`: the counts are those after Task 1, 3554 and 646, and `git grep -c "\[Fact\]\|\[Theory\]"` over the new files and `ModelTests.cs` sums to what `CachingTests.cs` and `ModelTests.cs` held.
- [ ] **Step 5:** `tests/AGENTS.md`: the rule of step names reads that the classes named `Caching*Tests` of `tests/SqlSource.Tests/Generator/` make the step names mean something, read through `StepReasons` of `tests/SqlSource.Testing/Generator/`, and a new step that does real work gets a case in the class of its step.  `Generator/AGENTS.md` and `tests/README.md` name `CachingScene` and `StepReasons` with the harness.  Run `./pre-commit-validation.sh`; commit `Split CachingTests by step and move the warnings of a model's types`.

### Task 3: `GeneratorHarness.UsersPath` (item 8, first half)

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`
- Modify: every file of `tests/SqlSource.Tests` that `git grep -l '/app/Repo/Users.sql\|"app/Repo/Users.sql"' -- tests/SqlSource.Tests` lists

**Interfaces:**
- Produces: `GeneratorHarness.UsersPath = "/app/Repo/Users.sql"` and `GeneratorHarness.UsersNormalizedPath = "app/Repo/Users.sql"`, `public const string` beside `SourcePath`, each with a comment: the path of the `.sql` file most tests use, and that path as the generator normalises it, relative to the project's folder.

- [ ] **Step 1:** Add the two constants.
- [ ] **Step 2:** Delete each private constant of that value (`UsersPath`, `Path`, `SqlPath`) and use `GeneratorHarness.UsersPath`; replace each literal, in a constant interpolated string where the literal is part of one (`$"{GeneratorHarness.UsersPath}(1,10)-(1,17)"`, which is a constant in C# 10 and later).  A local alias of one line (`private const string UsersPath = GeneratorHarness.UsersPath;`) is not kept.
- [ ] **Step 3:** `git grep -n 'app/Repo/Users.sql' -- tests/SqlSource.Tests` lists nothing; `TypeEmitterTests`'s `TestModels.Array("app/Repo/Users.sql")` uses `UsersNormalizedPath` too.
- [ ] **Step 4:** The counts of `tests/SqlSource.Tests` and RoslynFloor are unchanged since Task 1.  Run `./pre-commit-validation.sh`; commit `Name the path of Users.sql once`.

### Task 4: The parser's facts of a marker without a value and of a marker outside a query (item 2)

**Files:**
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`

- [ ] **Step 1:** The theory `Parse_MarkerWithoutAValue_IsInvalidAtTheMarker(string word)` becomes `(string word, bool inAQuery)`: the text is `"-- name: Q\n"` when `inAQuery`, then `"-- " + word + ":\nSELECT 1\n"`, and the error is `InvalidMarkerValue` at `"-- " + word + ":"` with the argument `word + ":"`.  Rows: `("output", true)`, `("database", true)`, `("type-override", true)`, `("token", true)`, `("param", true)`, `("column", true)`, `("dialect", false)`.  Delete `Parse_TokenMarkerWithoutAValue_IsInvalidAtTheMarker`, `Parse_ParamMarkerWithoutAValue_IsInvalid`, `Parse_ColumnMarkerWithoutAValue_IsInvalid` and `Parse_DialectMarkerWithoutAValue_IsAnErrorAtTheMarker`.
- [ ] **Step 2:** One theory `Parse_MarkerOutsideAQuery_IsNotAllowedThere(string marker, string word)`: the text is `marker + "\n-- name: Q\nSELECT 1\n"` (the SQL of each deleted fact where it used its token or parameter: `"SELECT {{a}}"`, `"SELECT @a"`), and the error is `MarkerNotAllowedHere` at `marker`, with `word` and `"inside a query"`.  Rows from the four facts it replaces, `Parse_TokenMarkerInThePreamble_IsNotAllowedThere`, `Parse_ParamMarkerInThePreamble_IsNotAllowedThere`, `Parse_TokenIgnoreMarkerInThePreamble_IsNotAllowedThere` and `Parse_ColumnMarkerInTheFileHeader_IsNotAllowedThere`, each with its marker as it is written there; a row gives the SQL as a third parameter where the facts' SQL differ.
- [ ] **Step 3:** One name for the outcome "`InvalidMarkerValue` at the whole marker": `IsInvalidAtTheMarker`.  Rename `Parse_NameMarkerWithoutAName_IsAnErrorAtTheMarker` and `Parse_TokenIgnoreMarkerWithoutAName_IsInvalidAtTheMarker` to it if their error is that one; a test whose error is at the value, not the marker, ends in `IsInvalid`; a test of another error keeps its name.
- [ ] **Step 4:** `tests/SqlSource.Tests` has the same count as after Task 3: four facts become four rows of the first theory and four facts four rows of the second.  Run `./pre-commit-validation.sh`; commit `Give the parser's facts of a marker's place and value one theory each`.

### Task 5: `OpenAsync` of a describer and one `DescribeRequests` (items 3 and 8, second half)

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Describers.cs`, `tests/SqlSource.Tool.Testing/DescribeRequests.cs`
- Create: `tests/SqlSource.Tool.Tests/DescribeRequestsTests.cs`
- Delete: `tests/SqlSource.Tool.Tests/PostgresRequests.cs`, `tests/SqlSource.Tests.Postgres/Describer/Requests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`, `tests/SqlSource.Tool.Tests/PostgresScript.cs`, `tests/SqlSource.Tool.Testing/Fixtures/FixtureReplay.cs`, `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs`, `CapturingExchangeTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`, and every caller of `PostgresRequests` and `Requests` (`git grep -l "PostgresRequests\.\|Requests\.Of\|Requests\.Query" -- tests`)
- Modify: `tests/README.md` (the list of `tests/SqlSource.Tool.Testing` and of the helpers of `Describing/Postgres/`), `tests/AGENTS.md` (the rule that a container test opens through `Sessions`)

**Interfaces:**
- Produces, in `SqlSource.Tool.Testing`:
  - `internal static class Describers` with `public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(this IQueryDescriber describer, string database, string connection, IDescribeExchange? exchange = null, IRunLog? log = null, TimeProvider? time = null)` — `describer.OpenAsync(new OpenRequest(database, connection, exchange ?? LiveExchange.Instance, time ?? TimeProvider.System), log ?? NullRunLog.Instance, TestContext.Current.CancellationToken)`.  Its comment says a session opened as a run opens one, for a test: the live exchange, the machine's clock and no log, unless the test brings its own.  `time` is the one default that TD-0065 item 4 will change.
  - `internal static class DescribeRequests` with `public const string Query = "Q"`; `public static DescribeRequest Of(string sql, params (string Name, string Type)[] declared)` (today's `Requests.Of`, lexed by `SqlDialectRules.PostgreSql`); `public static DescribeRequest Of(SqlDialectRules rules, string sql, params (string Name, string Type)[] declared)` (the same by the given rules; the first calls it); `public static DescribeRequest Listed(string sql, params SqlQueryParameter[] parameters)` (today's `PostgresRequests.Of`: the parameters as given); `Inferred(string name)` and `Declared(string name, string type)` as in `PostgresRequests`.

- [ ] **Step 1: Write the failing tests** of `DescribeRequestsTests` at the root of `tests/SqlSource.Tool.Tests`:
  - `Of_SqlWithAParameterTwice_ListsItOnceInTheOrderItFirstAppears`: `Of("SELECT @b, @a, @B")` has parameters named `["b", "a"]`, each inferred, and the name `"Q"`.
  - `Of_DeclaredParameterTheSqlLacks_IsListedAfterTheSqlsOwn`: `Of("SELECT @a", ("z", "int4"))` has `["a", "z"]`, `z` declared with the type `int4`.
  - `Of_DeclaredParameterTheSqlHolds_IsDeclaredInItsPlace`: `Of("SELECT @a", ("A", "text"))` has one parameter, `a`, declared with `text`.
  - `Listed_Parameters_AreKeptAsGiven`: `Listed("SELECT 1", Declared("x", "int4"), Inferred("y"))` has `x` then `y`.
- [ ] **Step 2:** The tests fail to compile.
- [ ] **Step 3:** Write `Describers` and `DescribeRequests`; delete `Requests` and `PostgresRequests`, and point their callers at `DescribeRequests` (`Requests.Of` → `DescribeRequests.Of`, `Requests.Query` → `DescribeRequests.Query`, `PostgresRequests.Of` → `DescribeRequests.Listed`, `PostgresRequests.Inferred`/`Declared` → `DescribeRequests.Inferred`/`Declared`).
- [ ] **Step 4:** `Sessions.OpenAsync`, `PostgresScript.OpenAsync` (`new PostgresDescriber(explainSettings ?? []).OpenAsync(Database, connection, exchange, log)`), `FixtureReplay.DescribeThroughAsync`, and the inline opens of `ReplayingExchangeTests`, `CapturingExchangeTests` and the four of `DescriberTests` open through the extension.  `DescriberTests` line 178, which builds an `OpenRequest` to assert on it, keeps its `new OpenRequest`.  `git grep -n "new OpenRequest(" -- tests` lists that line alone.
- [ ] **Step 5:** The counts: `tests/SqlSource.Tool.Tests` is 3135 (four new tests), `tests/SqlSource.Tests.Postgres` 1343.  Docs: `tests/README.md` lists `Describers` and `DescribeRequests` among the helpers of `tests/SqlSource.Tool.Testing`, and drops `PostgresRequests.cs` from the helpers of `Describing/Postgres/`.  Run `./pre-commit-validation.sh`; commit `Open a describer and build a request one way for every test project`.

### Task 6: `CliRun.Real`, and `EndToEndTool` deleted (item 4)

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/CliRun.cs`
- Delete: `tests/SqlSource.Tests.Postgres/EndToEnd/EndToEndTool.cs`
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/CheckTests.cs`, `tests/SqlSource.Tests.Postgres/EndToEnd/ErrorPathTests.cs`
- Modify: `docs/tech-debt/TD-0065-tests-that-depend-on-the-process-the-clock-or-the-repository.md`, `tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md` where they name `EndToEndTool`

**Interfaces:**
- Produces: `public static CliRun Real(string workingDirectory)` on `CliRun`: a run whose host is `Hosts.Real(workingDirectory, Folder.CreateFolder("tmp"))` with the same overrides `CreateHost` applies today (writers, describers, exchange, clock, file maker).  `Environment` and `Processes` are not read by such a run; their comments say so.  A private constructor taking the working directory or null holds the mode; `new CliRun()` is unchanged.

- [ ] **Step 1:** Add `Real` and the branch in `CreateHost`.
- [ ] **Step 2:** `ErrorPathTests.DescribeAsync` and `CheckTests.CheckAsync` make `using var run = CliRun.Real(directory);`, add `new PostgresDescriber()` to `run.Describers` and return `await run.RunAsync(args)`.  Delete `EndToEndTool`.
- [ ] **Step 3:** `dotnet test --project tests/SqlSource.Tests.Postgres` passes with 1343; `CheckTests` and `ErrorPathTests` pass by name.
- [ ] **Step 4:** TD-0065: item 3 is resolved by TD-0064, since `CliRun.Real` runs on the test's token; the opening sentence says which items remain and that the numbers of the rest are the review's, as TD-0039 does; the fix and impact lines of item 3 go.  Its row in `docs/tech-debt/README.md` stays true or is edited to.  Run `./pre-commit-validation.sh`; commit `Run the end-to-end tool through CliRun on the real host`.

### Task 7: `EndToEndFiles.CreateAsync` over `DbDataSource` (item 5)

**Files:**
- Modify: `tests/SqlSource.Testing/EndToEndFiles.cs`, `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs`, `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs` (drop `EndToEndSchema` and `EndToEndSeed`) and their callers
- Modify: `tests/README.md` (the row of `tests/SqlSource.Testing`, which describes `EndToEndFiles`)

**Interfaces:**
- Produces, on `EndToEndFiles`, with `System.Data.Common` and no Npgsql:
  - `public static async Task RunAsync(DbDataSource source, string path, CancellationToken cancellationToken)` — reads the file and runs it as one command.
  - `public static async Task CreateAsync(DbDataSource server, string database, Func<string, DbDataSource> sourceOf, IReadOnlyList<string> files, CancellationToken cancellationToken)` — runs `CREATE DATABASE {database}` on `server`, then each file in order on `sourceOf(database)`, which it disposes.
  - `public static Task CreateAsync(DbDataSource server, string database, Func<string, DbDataSource> sourceOf, CancellationToken cancellationToken)` — the end-to-end database from `[Schema(database), Seed(database)]`.

- [ ] **Step 1:** Write the three methods, each with a comment; the class comment says the databases of both PostgreSQL projects are built through it, and that it needs no driver.
- [ ] **Step 2:** `PostgresContainer.InitializeAsync` runs the fixture schema with `RunAsync`, makes `realistic` with the four-argument `CreateAsync` over `[RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed]`, keeping the comment of the seed's NOT NULL hole above it, and makes each end-to-end database with the three-argument one, `sourceOf` being `name => NpgsqlDataSource.Create(ConnectionStringOf(name))`.  `AppContainer` makes `app` the same way.  No loop over files is left in either.
- [ ] **Step 3:** `dotnet test` of `tests/SqlSource.Tests.Postgres` (1343) and `tests/SqlSource.Tests.Postgres.Npgsql8` (3) pass.  Run `./pre-commit-validation.sh`; commit `Build every test database through EndToEndFiles`.

### Task 8: The realistic corpus's skips and committed entry (item 6)

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/RealisticCase.cs`, `Realistic/Describer/RealisticDescribe.cs`, `Realistic/Describer/RealisticDescriptionTests.cs`, `Realistic/Generation/RealisticDapperTests.cs`, `Realistic/Generation/RealisticModelRefusalTests.cs`, `Realistic/Generation/RealisticModels.cs`

**Interfaces:**
- Produces, on `RealisticCase`:
  - `public void SkipUnlessRunsOn(int major)` — `Assert.SkipUnless(RunsOn(major), $"{this} needs PostgreSQL {MinimumMajor} or later, and the server is {major}.")`, with `CultureInfo.InvariantCulture` if the file formats numbers so.
  - `public void SkipIfRefused()` — `Assert.SkipWhen(Refusal is not null, $"{this} is refused as {Refusal}.")`.
- Consumes: `RealisticModels.EntryOf(RealisticCase)`, which exists.

- [ ] **Step 1:** Add the two methods.
- [ ] **Step 2:** `RealisticDescribe.Start`, `RealisticDapperTests.SkipUnlessRead` and `RealisticModelRefusalTests` skip by `SkipUnlessRunsOn`; `RealisticDescribe.DescribedAsync` and `RealisticModels.SkipUnlessHasModels` by `SkipIfRefused`.  `RealisticDescriptionTests.DescribeAsync_Case_IsItsCommittedEntry` reads its committed entry with `RealisticModels.EntryOf(found)`.
- [ ] **Step 3:** `tests/SqlSource.Tests.Postgres` passes with 1343 and 81 skipped, on the default image and on `postgres:14.24`, where the skipped count is the baseline's on 14 (record it before the change from a run on `b9a05e8` if no record exists).  Run `./pre-commit-validation.sh`; commit `Skip a realistic case one way for each reason`.

### Task 9: Names that break `Method_Scenario_Outcome` (item 9)

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/ModelShapeTests.cs`, `AccountQueryTests.cs`, `NoteQueryTests.cs`, `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`, and any document or comment that `git grep` finds naming an old name

- [ ] **Step 1:** Rename:
  - `ModelShapeTests`: the method is the build that generates the models, so `Models_X_Y` becomes `Build_X_Y`, `ConstantQueries_SqlOutput_HasTheConstantAndNoSidecar` becomes `Build_ConstantQueriesAtSql_HasTheConstantAndNoSidecar`, and `InvoiceQueries_DirectLocation_HasTheConstantsOnTheType` becomes `Build_InvoiceQueriesWithADirectLocation_HasTheConstantsOnTheType`.
  - `AccountQueryTests`: `QueryAsync_ListClosedBeforeWithANullParameter_ReadsEveryAccount`, `QueryAsync_ListClosedBeforeADate_ReadsTheClosedAccount`.
  - `NoteQueryTests`: `QueryAsync_ListAccountNotesOfAnAccountWithNoNote_HasNulls`.
  - `CliTests`: `Interrupt_FirstThenSecondTime_CancelsTheRunThenLetsTheProcessEnd`, `Terminate_FirstThenSecondTime_WaitsForTheRunThenLetsTheProcessEndAtOnce`.
  - `NullabilityWalkTests`: `Run_AliasAtAModifyTableThatIsNotTheTargetsWhereOldOrNewMayBeNamed_KeepsTheHeuristic`, `Run_AliasAtAModifyTableThatIsNotTheTargetsWhereNeitherIsNamed_TakesTheCatalogsAnswer`.
- [ ] **Step 2:** `git grep -nE "(void|Task) [A-Za-z0-9]+_[A-Za-z0-9]+_[A-Za-z0-9]+_[A-Za-z0-9]+\(" -- tests` lists none of these; the counts are unchanged.  Run `./pre-commit-validation.sh`; commit `Name the end-to-end and command tests by Method_Scenario_Outcome`.

### Task 10: TD-0064 resolved, and the count compared

**Files:**
- Delete: `docs/tech-debt/TD-0064-duplicated-helpers-of-the-model-and-container-tests.md`
- Modify: `docs/tech-debt/README.md` (its row goes), `docs/tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md` (its link to TD-0064, if any, and any count this work changed), `tests/AGENTS.md`, `tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md`

- [ ] **Step 1:** `git grep -n "TD-0064\|EndToEndTool\|PostgresRequests\|CachingTests\b\|Requests\.cs" -- . ':!docs/superpowers'` lists nothing but this plan's history.
- [ ] **Step 2:** Read `tests/AGENTS.md`, `tests/README.md` and `tests/SqlSource.Tests.Postgres/README.md` whole against the tree; each names the helpers where they now are.
- [ ] **Step 3:** Run the count script over every project and compare with the baseline: `SqlSource.Tests` 3554, `SqlSource.Tests.RoslynFloor` 646, `SqlSource.Tool.Tests` 3135, `SqlSource.Tests.Postgres` 1343, `SqlSource.Tests.Postgres.Npgsql8` 3.  Compare the sorted names too: every name of the baseline is present, or is in the renames of Tasks 4 and 9, or is a moved class's (same method name, new class).
- [ ] **Step 4:** `./pre-commit-validation.sh` and the PostgreSQL 14 run pass; commit `Resolve TD-0064`.
