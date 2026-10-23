# TD-0074 Duplicated Tests Across the Engine Projects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve `docs/tech-debt/TD-0074-duplicated-tests-across-the-engine-projects.md`: one copy of each helper and test that the engine projects, the driver tests and the tool's tests now hold twice or more, in the library both engines' projects reference, so that phase 7's tests in both end-to-end projects build on one harness.

**Architecture:** Helpers that need nothing of the tool move to `tests/SqlSource.Testing` (`EngineImage`, `ModelReflection`, `SqlTexts`, `CultureScope`, members of `GeneratorHarness` and `CachingScene`), which stays within Roslyn 4.8.0 because `tests/SqlSource.Tests.RoslynFloor` references it.  Helpers that need the tool move to `tests/SqlSource.Tool.Testing` (the realistic corpus's harness, the second-tier helpers of the container tests, `RecordingLog`, `HookedExchange`, `Failures`, `Programs.Waiter`).  Each engine project keeps only what reads its own database, its own generated types or its own driver.  Large classes of `tests/SqlSource.Tool.Tests` are split by subject over a static helper, as `PostgresSession*` already are.

**Tech Stack:** C# / .NET 10, xUnit v3 (Microsoft.Testing.Platform), Shouldly, Testcontainers, Roslyn 4.8.0 (floor) and 5.9.0 (tool).

**Spec:** `docs/tech-debt/TD-0074-duplicated-tests-across-the-engine-projects.md` (Problem and Proposed fix), with the unit's instructions: all eight items; `EngineImage` takes in `SqlServerStart` of TD-0072; `ModelReflection` orders by `MetadataToken`, which is TD-0075's item 3, so TD-0075 is rewritten without it; the shared helpers serve both engines; helpers that `Generator/` uses stay within Roslyn 4.8.0; `GeneratedSourceTests`, `SqlFileParserTests` and `DatabaseRunsTests` are not split (td-0039 does that); edits outside `tests/` stay small.

## Two pull requests

The whole change is about 190 files, over the limit of 150 (root `AGENTS.md`, "Pull requests" 3).  It lands as two pull requests from this branch, in order:

- **Part 1 of 2 — the engine projects** (Tasks 1–4): `EngineImage`, `ModelReflection`, the realistic harness and the second-tier helpers of the container projects.  It rewrites TD-0074 to items 3–6 and 8 (the ones left), and TD-0075 without its item 3.
- **Part 2 of 2 — the driver and tool tests** (Tasks 5–9): the describers' fakes, the split classes, the driver tests' file and type, the caching assertion and the small copies.  It deletes TD-0074.

Before asking to push Part 1, `git diff --name-only origin/main | wc -l` must be at most 150; Part 2 is asked for only after Part 1 merges, with the branch synced to `main`, and is checked the same way.  Each title ends in `(part k of 2)`.

## Where the code differs from the item, and the choices

- **Item 1, `ShapeQueryTests`.**  Its four tests read each project's own generated models (`SearchAccountsView`, `ShapeQueries`, …), which are different types in each assembly, and differ otherwise by the two lines that open a connection of each driver.  No helper can take them without reflection over every model, which would hide what each test reads.  They stay per engine; the other three second-tier classes get helpers.
- **Item 1, `RealisticModelsTests`.**  Its tests of `OutputModelOf` and `InputModelOf` name each project's generated models and stay per engine; its tests of `PropertiesOf` and `IsNullable` move to `ModelReflectionTests` (Task 2).
- **Item 2, Npgsql 8.**  `tests/SqlSource.Tests.Postgres.Npgsql8/AllTypesQueryTests.cs` has no `PropertiesOf`, but reads the same properties inline into a dictionary; it uses `ModelReflection` too.
- **Item 2, the list of `all_types`.**  `SqlServerAllTypesRows` cannot name `SqlVector<float>` (it references no SqlClient), so it holds the 32 properties of `all_types`, with their types and nullability; the three of `new_types` stay in both tests.
- **Item 3, counts.**  The `Sample` source has six text-identical copies of the `SqlLocation.Direct` form and two builders; the item's "13 in 11" also counts the bare form, which `ModelRuns.Source` already is.  Every copy that is text-identical to `GeneratorHarness.SampleSource(...)` or to `ModelRuns.Source` uses it.
- **Item 4.**  The six dictionaries are of `ParseInputs`, keyed by path, and the five blocks of `ParsedFile`, keyed by file name; the other file's reason is `Cached` in some and `Unchanged` in others.  The helper takes that reason, so no assertion is weakened.
- **Item 7, `SqlServerStart`.**  `EngineImage.StartAsync` is how every container of the tests starts, both engines' and both `AppContainer`s'; for SQL Server it starts through `SqlServerStart`, whose retry and its tests stay as they are.  The tests of the run's own image (`Image_OfTheRun_*`, `Server_OfTheImage_*`, `ImageBuilds_*`) stay in each container's test class, since they describe that run; the gates' tests become one class.
- **Item 8, names.**  The item counts 37 test names without three parts; the code has 39 (the item counted fewer projects).  All 39 are renamed; no exception is added to `tests/AGENTS.md`.

## Global Constraints

- `tests/SqlSource.Testing` stays on Roslyn 4.8.0 (no `VersionOverride`) and C# 12 in what it hands the compiler; a new package there is one RoslynFloor already has.
- `tests/SqlSource.Tests.Postgres.Npgsql8` and `tests/SqlSource.Tests.SqlServer.SqlClient61` reference nothing that brings Npgsql 10 or SqlClient 7: no `SqlSource.Tool.Testing`.
- The two `public const string X = "...";` lines of `DefaultImages.cs` keep their shape: `tools/pull-test-images.sh`, `tools/record-fixtures.sh` and `tools/describe-end-to-end.sh` read them with `sed`.
- Library types stay `internal`; every test class is `sealed`; test names are `Method_Scenario_Outcome`; assertions are Shouldly's; namespaces follow folders.
- A file of `tests/SqlSource.Tests/Generator/` starts with the shared line and its name ends in `Tests.cs`.
- No test changes what it asserts except where a task says so; a move keeps every assertion.
- No file of `src/` changes.
- Every AGENTS.md and readme that names a moved, split or deleted file or type is updated in the same commit (`tests/AGENTS.md`, `tests/README.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`).
- Every commit passes `./pre-commit-validation.sh` on the exact tree.  Part 1 changes the describers' tests of both container projects, so before its push also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer`; Part 2 changes `SessionTests` and `DriverLogTests` of both, so it runs them again.
- Do not split `GeneratedSourceTests`, `SqlFileParserTests` or `DatabaseRunsTests`; the theories that dispatch on a string (TD-0039 item 6) move as they are.

## Review Focus

1. A run on a floor image (`SQLSOURCE_TESTS_*_IMAGE` set): `EngineImage.Image` is the variable's value, `SkipUnlessRecordedMajor` names the engine's own product and majors, and every container starts on it.  Pinned in Task 1 by `EngineImageTests` and by the floor runs.
2. A model whose properties the runtime returns out of declaration order: every helper that compares an order reads `ModelReflection.PropertiesOf`, ordered by `MetadataToken`.  Pinned in Task 2 by a test over a class declared in a non-alphabetical order.
3. A realistic case added to one corpus without its class, or a claim of a folder of the other engine: `RealisticClaims.AllOf(corpus)` reads that corpus's own `RealisticQueries.cs`.  Pinned in Task 3 by the corpus tests that stay in each project.
4. Recording fixtures (`SQLSOURCE_RECORD_FIXTURES` set) after the move: `FixtureCaseRuns` still writes `capture.json` and `expected.json` and compares nothing.  Pinned in Task 4 by a test of `FixtureCaseRuns` over a temporary case with a fake describer.
5. `RecordingLog` written from several threads, as SqlClient does: no event is lost.  Pinned in Task 5 by a test that writes from parallel tasks.

---

## Part 1 of 2

### Task 1: `EngineImage`, the containers' gates on a version (item 7)

**Files:**
- Create: `tests/SqlSource.Testing/EngineImage.cs`
- Create: `tests/SqlSource.Tests/EngineImageTests.cs`
- Modify: `tests/SqlSource.Testing/SqlSource.Testing.csproj` (add `xunit.v3.assert` if `Assert.SkipWhen` is not reachable; its lock file and those of the projects that reference it)
- Modify: `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `tests/SqlSource.Tests.Postgres/PostgresContainerTests.cs`, `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`, `tests/SqlSource.Tests.SqlServer/SqlServerContainerTests.cs`, `tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs`, `tests/SqlSource.Tests.SqlServer.SqlClient61/AppContainer.cs`, `tests/SqlSource.Tool.Tests/RealisticTestCases.cs`, every caller of a container's static `SkipBelow`, `SkipUnlessRecordedMajor`, `RecordedMajors`, `TagOf` or `Image`
- Docs: `tests/AGENTS.md`, `tests/README.md`, both container readmes

**Interfaces:**
- Produces, in namespace `SqlSource.Testing`:
  ```csharp
  internal sealed class EngineImage
  {
      public static EngineImage Postgres { get; }   // "PostgreSQL", "SQLSOURCE_TESTS_POSTGRES_IMAGE", DefaultImages.Postgres, [14, 18]
      public static EngineImage SqlServer { get; }  // "SQL Server", "SQLSOURCE_TESTS_SQLSERVER_IMAGE", DefaultImages.SqlServer, [14, 17]
      public string Product { get; }
      public string Variable { get; }
      public string Default { get; }
      public IReadOnlyList<int> RecordedMajors { get; }
      public string Image { get; }                  // the variable's value when not empty, the default otherwise; read each time
      public static string TagOf(string image);
      public static void SkipBelow(int server, int major, string reason);
      public void SkipUnlessRecordedMajor(int server, string what);  // "{what} are pinned for {Product} 14 and 18 alone."
      public Task<TContainer> StartAsync<TContainer>(Func<TContainer> build, CancellationToken token) where TContainer : IContainer;
  }
  ```
  `StartAsync` starts through `SqlServerStart.StartAsync` for `SqlServer`, and for `Postgres` builds, starts and, if the start throws, disposes before rethrowing.
- The containers keep their instance members `Major`, `SkipBelow(int major, string reason)` and `SkipUnlessRecordedMajor(string what)`, which call `EngineImage`; their static `RecordedMajors`, `Image`, `ImageVariable`, `TagOf` and static gates are removed, and callers use `EngineImage.X`.  `SqlServerContainer.ImageBuilds` stays.  `PostgresContainer.NoOldAndNewInReturning` stays.

- [ ] **Step 1: Tests.**  `EngineImageTests` (sealed, no collection) holds, for each engine through `[Theory]` rows or one fact per engine: `SkipBelow_ServerOfThatMajor_DoesNotSkip`, `SkipBelow_ServerBeforeThatMajor_SkipsWithTheReason` (message ends with `"a reason"`), `SkipUnlessRecordedMajor_RecordedMajor_DoesNotSkip` (14 and 18 for PostgreSQL, 14 and 17 for SQL Server), `SkipUnlessRecordedMajor_OtherMajor_SkipsNamingTheRecordedOnes` (`"The things are pinned for PostgreSQL 14 and 18 alone."`, `"The things are pinned for SQL Server 14 and 17 alone."`), `TagOf_PinnedImage_IsWhatFollowsTheLastColon` (`"postgres:18.6"` → `"18.6"`, `"mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"` → `"2025-CU9-GDR1-ubuntu-24.04"`), `Default_OfEachEngine_IsItsDefaultImage`.  A theory row names the engine by a `bool` or by `nameof`, not a free string.
- [ ] **Step 2: Build fails**: `dotnet build tests/SqlSource.Tests` (no `EngineImage`).
- [ ] **Step 3: Implement** `EngineImage`; move the five gate tests out of both container test classes (keep `SkipBelow_OfTheContainer_AsksTheServersMajor` and the `Image_OfTheRun_*`, `Server_OfTheImage_*` and `ImageBuilds_*` tests there, now over `EngineImage.X.Image` and `EngineImage.TagOf`); start all four containers through `EngineImage.X.StartAsync` (the two `AppContainer`s on `EngineImage.X.Default`); `RealisticTestCases` takes its majors from `EngineImage.X.RecordedMajors`.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tests --filter "FullyQualifiedName~EngineImageTests"`, then the four container projects and `tests/SqlSource.Tool.Tests`: pass.
- [ ] **Step 5: Docs.**  `tests/AGENTS.md`: the gates are `EngineImage`'s, every container starts through `EngineImage.StartAsync` (SQL Server's through `SqlServerStart`), the recorded majors are `EngineImage.X.RecordedMajors` (the noise budgets' rule names them); `tests/README.md` lists `EngineImage` in `tests/SqlSource.Testing`; the image sections of both container readmes name `EngineImage.Image` and the variable.
- [ ] **Step 6: Validate and commit**: `./pre-commit-validation.sh`, commit "Move the containers' gates on a version into EngineImage".

### Task 2: `ModelReflection` (item 2, and TD-0075's item 3)

**Files:**
- Create: `tests/SqlSource.Testing/EndToEnd/ModelReflection.cs`, `tests/SqlSource.Tests/ModelReflectionTests.cs`
- Modify: `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs` (the list), both `EndToEnd/ModelShapeTests.cs`, both end-to-end `EndToEnd/AllTypesQueryTests.cs`, `tests/SqlSource.Tests.Postgres/EndToEnd/DapperRecipeTests.cs` (its `ColumnOf`), `tests/SqlSource.Tests.Postgres.Npgsql8/AllTypesQueryTests.cs`, `tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueryTests.cs`, both `Realistic/Generation/RealisticModels.cs` (until Task 3 moves it: `PropertiesOf` and `IsNullable` call `ModelReflection`), both `Realistic/Generation/RealisticModelsTests.cs` (drop the two tests of `Sample`)
- Modify: `docs/tech-debt/TD-0075-tests-that-depend-on-the-clock-or-the-runtime.md` and its row in `docs/tech-debt/README.md` (item 3 gone, the rest renumbered; Impact and Trigger no longer name the order of properties or TD-0074's helper)
- Docs: `tests/AGENTS.md` (a column of SQL Server's `all_types` changes `SqlServerAllTypesRows`, and no longer the lists of the two test classes), `tests/README.md`

**Interfaces:**
- Produces, in namespace `SqlSource.Testing.EndToEnd`:
  ```csharp
  internal enum ModelShape { Record, SealedRecord, Class, SealedClass }
  internal static class ModelReflection
  {
      public static IReadOnlyList<PropertyInfo> PropertiesOf(Type model);                       // public instance, ordered by MetadataToken
      public static (string Name, Type Type, bool Nullable)[] TypedPropertiesOf(Type model);    // in that order
      public static string[] NamesOf(Type model);                                               // in that order
      public static bool IsNullable(PropertyInfo property);                                     // Nullable<T>, or a reference annotated nullable
      public static ModelShape ShapeOf(Type model);                                             // a record has "<Clone>$"
      public static Type? ModelNamed(Assembly assembly, string fullName);
      public static void ShouldBeModel(Type model, string fullName, ModelShape shape, params (string Name, Type Type, bool Nullable)[] properties);
      public static string ColumnOf(string property);                                           // Int4ArrayValue -> int4_array_value
  }
  ```
  `SqlServerAllTypesRows.AllTypesProperties`: `IReadOnlyList<(string Name, Type Type, bool Nullable)>`, the 32 properties of `AllTypesDto` in their order (`("Id", typeof(int), false)` … `("SysnameValue", typeof(string), true)`), copied from `ModelShapeTests`.

- [ ] **Step 1: Tests** in `ModelReflectionTests`: `PropertiesOf_ClassDeclaredOutOfAlphabeticalOrder_IsInDeclarationOrder` (a private class declaring `Zeta`, `Alpha`, `Mu` gives `["Zeta", "Alpha", "Mu"]`), `IsNullable_Property_IsWhetherItCanHoldNull` (moved from `RealisticModelsTests`: `[false, true, false, true]` over `int`, `int?`, `string`, `string?`), `TypedPropertiesOf_Class_IsEachNameTypeAndNullability`, `ShapeOf_EachKind_IsItsShape` (four private types), `ModelNamed_TypeThatIsNotThere_IsNull`, `ColumnOf_Property_IsItsSnakeCase` (`"Int4ArrayValue"` → `"int4_array_value"`, `"SqlVariantValue"` → `"sql_variant_value"`), `ShouldBeModel_OtherShape_Fails`.
- [ ] **Step 2: Build fails.**
- [ ] **Step 3: Implement** `ModelReflection`; replace every private copy listed under Files with it; `SqlServer` `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn` and `SqlClient61`'s `Build_AllTypes_HasTheSameTypesAsSqlClient7` compare `TypedPropertiesOf(typeof(AllTypesDto))` with `SqlServerAllTypesRows.AllTypesProperties` (SqlClient61 now checks nullability too); `Npgsql8` reads its five types through `TypedPropertiesOf`.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` (filter `ModelReflectionTests`), then the four container projects: pass.
- [ ] **Step 5: TD-0075 and docs** as listed.
- [ ] **Step 6: Validate and commit** "Read a model by reflection through ModelReflection, in declaration order".

### Task 3: The realistic corpus's harness (item 1, first tier)

**Files:**
- Create in `tests/SqlSource.Tool.Testing/Realistic/`: `RealisticCorpus.cs`, `RealisticClaims.cs` (with `RealisticClaim`), `RealisticModels.cs`, `RealisticInputs.cs`, `RealisticDescribe.cs`
- Create in `tests/SqlSource.Tool.Tests/Realistic/`: `RealisticClaimsTests.cs` (the five tests of `Parse`, moved), `RealisticInputsTests.cs` (the tests of `ModelOf` and `ValueOf`, moved from both `RealisticParametersTests`), `RealisticCorpusTests.cs` (paths and engine of each corpus)
- Delete in both engine projects: `Realistic/Generation/RealisticClaims.cs`, `Realistic/Generation/RealisticModels.cs`, `Realistic/Describer/RealisticDescribe.cs`
- Modify in both engine projects: `Realistic/RealisticCases.cs` (becomes the binding of the engine: its `Corpus`, `Models` and `Describe`), `RepositoryPaths.cs` (realistic paths gone), the container (`RealisticSchema` and `RealisticSeed` from the corpus), `EndToEnd/CheckTests.cs`, every file of `Realistic/` that reads them, `Realistic/Generation/RealisticParameters.cs` (keeps `SelectOf`, `InputModelAsync` and `ArgumentsOf`), `Realistic/Generation/RealisticParametersTests.cs` (keeps the tests of those)
- Modify: `tests/SqlSource.Tool.Tests/RealisticTestCases.cs` (`Postgres` and `SqlServer` are `RealisticCorpus.X.Engine`)
- Docs: `tests/AGENTS.md` (the list of what both corpora share; the tests that need no server), `tests/README.md` (`Realistic/` of the library), both container readmes' realistic-corpus sections

**Interfaces:**
- Produces, in namespace `SqlSource.Tool.Testing.Realistic`:
  ```csharp
  internal sealed class RealisticCorpus
  {
      public static RealisticCorpus Postgres { get; }    // engine postgres, EngineImage.Postgres's product and majors; tests/SqlSource.Tests.Postgres/Realistic
      public static RealisticCorpus SqlServer { get; }   // mssql; tests/SqlSource.Tests.SqlServer/Realistic
      public RealisticEngine Engine { get; }
      public string Folder { get; }  public string Schema { get; }  public string Seed { get; }
      public string Cases { get; }   public string Queries { get; }   // Generation/RealisticQueries.cs
      public IReadOnlyList<string> Names { get; }        // read afresh each time, as RealisticCases.Names
      public IReadOnlyList<RealisticCase> All { get; }   // read afresh each time
      public RealisticCase ReadNamed(string name);
      public RealisticCase Read(string folder);
  }
  internal sealed record RealisticClaim(string TypeName, string Case);
  internal static partial class RealisticClaims
  {
      public static IReadOnlyList<RealisticClaim> Parse(string source);
      public static IReadOnlyList<RealisticClaim> AllOf(RealisticCorpus corpus);
      public static RealisticClaim Of(RealisticCorpus corpus, RealisticCase found);
      public static string ClaimedBy(RealisticCorpus corpus, RealisticCase found, int count);
  }
  internal sealed class RealisticModels(Assembly assembly, string @namespace, DatabaseEngine engine)
  {
      public const string InputSuffix = "Args";
      public const string OutputSuffix = QuerySettings.DefaultOutputModelSuffix;
      public static void SkipUnlessHasModels(RealisticCase found);
      public static SidecarEntry EntryOf(RealisticCase found);
      public Type OutputModelOf(SidecarEntry entry);
      public Type? InputModelOf(SidecarEntry entry);
      public void ShouldBeAsNullableAsItsTruth(RealisticCase found);   // the body of RealisticModelNullabilityTests
  }
  internal static class RealisticInputs
  {
      public static object? ValueOf(object value);
      public static object ModelOf(Type input, IReadOnlyList<SidecarParameter> parameters, IReadOnlyList<object?> values);
  }
  internal sealed class RealisticDescribe(RealisticCorpus corpus, string database, Func<IDescriber> describer)
  {
      public RealisticCase Start(string name, int major);
      public Task<(IReadOnlyList<SidecarColumn> Columns, IReadOnlyList<DescribedParameter> Parameters)> DescribedAsync(RealisticCase found, string connection);
      public DescribeRequest RequestOf(RealisticCase found);
      public SidecarEntry EntryOf(RealisticCase found, ServerInfo server, QueryDescription description);
      public Task<Outcome<QueryDescription, DescribeFailure>> DescribeOnceAsync(RealisticCase found, string connection);
      public Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(RealisticCase found, string connection);  // opens through Describers.OpenAsync
  }
  ```
- Each engine project's `Realistic/RealisticCases.cs` holds only the binding:
  ```csharp
  internal static class RealisticCases
  {
      public static RealisticCorpus Corpus => RealisticCorpus.Postgres;
      public static RealisticModels Models { get; } = new(typeof(RealisticCases).Assembly, "SqlSource.Tests.Postgres.Realistic.Generation", DatabaseEngine.PostgreSql);
      public static RealisticDescribe Describe { get; } = new(Corpus, PostgresContainer.RealisticDatabase, static () => new PostgresDescriber());
  }
  ```
  and every caller reads `RealisticCases.Corpus.ReadNamed(name)`, `RealisticCases.Models.OutputModelOf(...)`, `RealisticCases.Describe.Start(...)`.

- [ ] **Step 1: Move the tests.**  Create the three test classes of `tests/SqlSource.Tool.Tests/Realistic/` (the moved tests unchanged but for the type they call; `RealisticCorpusTests`: `Cases_OfEachCorpus_IsItsProjectsCasesFolder`, `Engine_OfEachCorpus_HasTheEnginesRecordedMajors`).  Delete them from both engine projects.
- [ ] **Step 2: Build fails** (`dotnet build tests/SqlSource.Tool.Tests`).
- [ ] **Step 3: Implement** the five types, the bindings, and the callers.  `RealisticModelNullabilityTests` and `RealisticClaimsTests`' corpus tests (`All_Claims_*`) stay in each project, each body one call.  The cache of `RealisticDescribe` is an instance field.
- [ ] **Step 4: Run** `tests/SqlSource.Tool.Tests` (filter `Realistic`), then both container projects: pass with the same counts of passed and skipped tests as before the change (compare with a run on `origin/main`).
- [ ] **Step 5: Docs** as listed.
- [ ] **Step 6: Validate and commit** "Move the realistic corpus's harness into SqlSource.Tool.Testing".

### Task 4: The second-tier helpers of the container projects, and Part 1's close (item 1, second tier)

**Files:**
- Create in `tests/SqlSource.Tool.Testing/`: `Fixtures/FixtureCaseRuns.cs` (with `Recording`), `ToolProcess.cs`, `ErrorPaths.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCaseRunsTests.cs`
- Delete: `tests/SqlSource.Tests.Postgres/Recording.cs`, `tests/SqlSource.Tests.SqlServer/Recording.cs`
- Modify in both engine projects: `Describer/FixtureCasesTests.cs`, `Describer/ToolProcessTests.cs`, `EndToEnd/ErrorPathTests.cs`
- Modify: `docs/tech-debt/TD-0074-duplicated-tests-across-the-engine-projects.md` (rewritten to items 3, 4, 5, 6 and 8, with their fixes; item 1's `ShapeQueryTests` named as kept, with the reason) and its row
- Docs: `tests/AGENTS.md`, `tests/README.md`, `tools/README.md` if it names `FixtureCasesTests.RecordVariable`

**Interfaces:**
- Produces:
  ```csharp
  namespace SqlSource.Tool.Testing.Fixtures;
  internal static class FixtureCaseRuns
  {
      public const string RecordVariable = "SQLSOURCE_RECORD_FIXTURES";
      public static bool IsRecording { get; }           // the variable set and not empty
      // Describes the case against the server and compares with its expected.json, one serverVersion; or, when
      // recording, writes its capture.json and expected.json and compares nothing.
      public static Task DescribeOrRecordAsync(FixtureCase fixture, string engine, Func<FixtureCase, IDescriber> describer, string connection);
      public static string WithOneServerVersion(string sidecar);
  }
  namespace SqlSource.Tool.Testing;
  internal static class ToolProcess
  {
      // Runs the built tool's "describe" on the project with one --connection, through Programs.ToolStart, and checks
      // exit 0, an empty error, the one line of output, and one entry of that name and engine in the .sql file's sidecar.
      public static Task DescribeOneQueryAsync(FixtureProjects fixtures, string project, string connection, string output, string sqlFile, string query, string engine);
  }
  internal sealed record ErrorPathEngine(
      string Table, string CreateTable, string AddColumn, string MissingTable,
      Func<string, Task> ExecuteAsync, Action<CliRun> UseDescribers);
  internal sealed class ErrorPaths(ErrorPathEngine engine, string connection) : IDisposable
  {
      public Task QueryEditedAfterDescribeAsync();       // exit 2, SQLSRC219
      public Task ColumnAddedAfterDescribeAsync();       // exit 2, SQLSRC219
      public Task QueryTheServerRefusesAsync();          // exit 1, SQLSRC225, no sidecar
  }
  ```
  Each container test keeps its `[Fact]`, collection and name, and its body calls the helper with its engine's values (the PostgreSQL `ErrorPathEngine` adds a `PostgresDescriber`, SQL Server's sets `ReleasedDescribers`).

- [ ] **Step 1: Test** `FixtureCaseRunsTests.WithOneServerVersion_Entry_ReplacesTheVersionWithAStar` and `WithOneServerVersion_EntryWithoutAVersion_Fails` (the helper's string logic, with no server).
- [ ] **Step 2: Build fails.**
- [ ] **Step 3: Implement** the three helpers and the six test classes' bodies.
- [ ] **Step 4: Run** both container projects; then `tools/record-fixtures.sh` is not run, but `SQLSOURCE_RECORD_FIXTURES=1 dotnet test --project tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~FixtureCasesTests"` followed by `git status --porcelain tests/SqlSource.Tool.Tests/Fixtures/Describers` shows only `capture.json` and `expected.json` files changed or none (then `git checkout` them back).
- [ ] **Step 5: TD-0074 rewritten** to what remains; docs as listed.
- [ ] **Step 6: Validate** (`./pre-commit-validation.sh` and both floor-image runs) **and commit** "Share the fixture, tool-process and error-path tests of both container projects".
- [ ] **Step 7: Count**: `git diff --name-only origin/main | wc -l` is at most 150.  Part 1 ends here: review, push request, pull request "(part 1 of 2)".

## Part 2 of 2

### Task 5: Fakes and helpers of the describers' tests (item 6)

**Files:**
- Create in `tests/SqlSource.Tool.Testing/`: `RecordingLog.cs`, `HookedExchange.cs`, `Failures.cs`
- Create: `tests/SqlSource.Tool.Tests/RecordingLogTests.cs`, `tests/SqlSource.Tool.Tests/HookedExchangeTests.cs`
- Delete: `tests/SqlSource.Tool.Tests/RecordingLog.cs`, `tests/SqlSource.Tool.Tests/ClockedExchange.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresEventCases.cs`, `tests/SqlSource.Tool.Tests/SqlServerScript.cs`, every user of `RecordingLog` and `ClockedExchange`, `tests/SqlSource.Tests.SqlServer/Describer/DriverLogTests.cs` (`ConcurrentLog`, `EnabledExchange`, `Member`, `Number` gone), `tests/SqlSource.Tests.SqlServer/Describer/SessionTests.cs` (`TokenExchange`), `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs` (`AfterLiveExchange`, `ThrowsAfterTheServerAnswered`), both `Describer/Sessions.cs` (`TextsOf` gone), `Describing/Postgres/PostgresSessionTests.cs`, `Describing/SqlServer/SqlServerSessionTests.cs`, `Describing/SqlServer/SqlServerErrorsTests.cs` (`MessageOf` gone)
- Docs: `tests/README.md` (the list of `tests/SqlSource.Tool.Testing`)

**Interfaces:**
- Produces, in namespace `SqlSource.Tool.Testing`:
  ```csharp
  internal sealed class RecordingLog : IRunLog          // thread-safe: a Lock around a list
  {
      public bool Writes => true;
      public IReadOnlyList<(string Name, LogMember[] Members)> Events { get; }   // a copy, taken under the lock
      public void Write(string name, params LogMember[] members);
      public static LogValue Member(LogMember[] members, string name);           // the one member of that name
      public static string[] NamesOf(LogMember[] members);
  }
  internal sealed class HookedExchange(
      Func<string, object?, CancellationToken, Task>? before = null,
      Func<string, object?, CancellationToken, Task>? after = null,
      IDescribeExchange? inner = null) : IDescribeExchange   // inner defaults to LiveExchange.Instance
  internal static class Failures
  {
      public static IReadOnlyList<string> TextsOf(DescribeFailure failure);   // as both Sessions.TextsOf
      public static string MessageOf(DescribeFailure failure);                // the descriptor's format with the arguments, invariant
  }
  ```
  `ClockedExchange(inner, clock, perCall)` becomes `new HookedExchange(before: ... clock.Advance(perCall) ..., inner: inner)` in its two users.  `ThrowsAfterTheServerAnswered` is an `after` that records the count and throws.

- [ ] **Step 1: Tests**: `RecordingLogTests.Write_FromParallelTasks_KeepsEveryEvent` (1,000 writes over `Parallel.ForAsync`, `Events.Count` is 1,000), `Member_NameOfOneMember_IsItsValue`, `Member_NameOfNoMember_Throws`; `HookedExchangeTests.AskAsync_BeforeAndAfter_RunAroundTheInnerCall` (an order list `["before", "live", "after"]`), `AskAsync_NoInner_CallsTheLiveFunction`; `Failures` through the existing tests that call it.
- [ ] **Step 2: Build fails.**
- [ ] **Step 3: Implement** and replace every copy.
- [ ] **Step 4: Run** `tests/SqlSource.Tool.Tests` and both container projects: pass.
- [ ] **Step 5: Validate and commit** "Share the describers' test log, exchange hook and failure texts".

### Task 6: Split the classes that cover several subjects (item 5)

**Files:**
- Split `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` into, in the same folder: `NullabilityWalkTests` (tables, views, foreign tables, ungated queries, the check of a reference), `NullabilityWalkExpressionTests` (both blocks of expressions under joins), `NullabilityWalkNotNullTests`, `NullabilityWalkSystemColumnTests`, `NullabilityWalkAggregateTests`, `NullabilityWalkSubPlanTests`, `NullabilityWalkNameTests` (aliases, bare names, targets, `OLD`/`NEW`, `MERGE` at the root), `NullabilityWalkUnusablePlanTests` (unusable plans and the shape of a walked column), `NullabilityWalkSubqueryTests` (subqueries and CTEs), `NullabilityWalkPartitionTests`; `Run_ExpressionOverASetOperation_KeepsNoOrigin` and its rows move to `NullabilityWalkSetOperationTests`.  `Run`, `RunAs`, `RunUngated`, `Scope`, `LeftJoin`, `OfA`, `OfB`, `OrdersO`, `RunOverOrders` and `RunTyped` move to `tests/SqlSource.Tool.Tests/PostgresWalkColumns.cs`; a fixture one class alone uses moves with it.
- Split `Describing/SqlServer/SqlServerSessionTests.cs` by call: `SqlServerSessionTests` (open), `SqlServerSessionParameterTests` (parameters, probe, declared), `SqlServerSessionColumnTests` (columns and their declarations), `SqlServerSessionTableTests` (table and the match); create `tests/SqlSource.Tool.Tests/SqlServerSessionCases.cs` (`IntRow`, `Name100Row`, `Name100`, `NoRows`, `NameColumn`, `NicknameColumn`, `Call`, `OpenAsync`, `ColumnsAnsweringAsync`, `TableAnsweringAsync`).
- Split `Describing/SqlServer/SqlServerEventsTests.cs` by event: `SqlServerEventsTests` (parameters, probe, declared), `SqlServerEventsColumnTests` (columns, column), `SqlServerEventsTableTests` (table, table match), `SqlServerEventsSessionTests` (through a whole session); create `tests/SqlSource.Tool.Tests/SqlServerEventCases.cs` (`CallTime`, `Refused`, `ErrorsOf`, `ParameterMap`, `ColumnRowMap`).  `Int`, `Lost`, `IdColumn` and `Rowstat`, which both families declare, go to `SqlServerScript`.
- Move the 20 whole-run tests of `Describing/RedactionTests.cs` to `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs` with their scene and helpers; the 13 tests of `Redaction.Of` and `HoldsToken` stay.
- Move the tests of where the log goes out of `Logging/RunLogTests.cs` (the 14 listed in the survey: `Run_LogPathWithAnEqualsSign_*`, `Run_LogOptionAndVariable_*`, `Run_LogVariableAlone_*`, `Run_NoLogOptionAndNoVariable_*`, `Run_SecondRunWithTheSameLog_*`, `Run_LogThatCannotBeWritten_*`, `Run_LogThatIsAFileOfTheRun_*`, `Run_WrongCommandLineWithLog_*`, `Run_NoDescribeWithTheLogVariable_*`, `Run_LogWithAnEmptyValue_*`, `Run_LogGivenTwice_*`, `Run_LogThatStopsBeingWritable_*`, `Run_LogWhoseCloseFails_*`) to `Logging/RunLogDestinationTests.cs`, with `FullStream` and `UnclosableStream`.
- Docs: `tests/AGENTS.md` (the split families named with `SqlServerSession*` and `SqlServerEvents*`), `tests/README.md` (the helpers of `Describing/SqlServer/`)

- [ ] **Step 1:** Record the test counts: `dotnet test --project tests/SqlSource.Tool.Tests` before the split; note passed and skipped.
- [ ] **Step 2:** Split, one class per commit-ready step, moving test bodies unchanged.
- [ ] **Step 3: Run**: the same counts of passed and skipped, and `git grep -c "\[Fact\]\|\[Theory\]"` over the new and old files sums to the old total.
- [ ] **Step 4: Validate and commit** "Split the walk, SQL Server session and events, redaction and log tests by subject".

### Task 7: The driver tests' file and type (item 3)

**Files:**
- Create: `tests/SqlSource.Testing/SqlTexts.cs` (moved from `tests/SqlSource.Tool.Tests/SqlTexts.cs`, namespace `SqlSource.Testing`)
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, `tests/SqlSource.Testing/Generator/CachingScene.cs`, the ten classes of `Generator/` that declare `Users`, the 17 files with the text, the users of the bare and `Direct` `Sample` sources, `GeneratorParameterLevelTests`, `SettingLevelTests`, `TypeOverrideLevelTests` and `QueryTypesTests` (their `const Source`, equal to `ModelRuns.Source`, goes), the 15 users of `SqlTexts` in `tests/SqlSource.Tool.Tests` (a `using SqlSource.Testing;`)
- Docs: `tests/README.md` (`SqlTexts` in `tests/SqlSource.Testing`), `tests/SqlSource.Tests/Generator/AGENTS.md` if it lists the harness's members

**Interfaces:**
- Produces: `SqlSource.Testing.SqlTexts` (unchanged members); `GeneratorHarness.UsersFile` (`SqlFile`, `new(UsersPath, SqlTexts.GetUser)`); `GeneratorHarness.SampleSource(string? arguments)` returning `"using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct" + (arguments is null ? "" : ", " + arguments) + ")]\npublic partial class Sample;\n"`.

- [ ] **Step 1:** Replace only text-identical copies; a copy whose text differs keeps it.  `git grep -c 'name: GetUser\\nSELECT 1;\\n"' tests` afterwards finds `SqlTexts.cs` alone (concatenations such as `"-- name: GetUser\n" + marker` stay).
- [ ] **Step 2: Run** `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests`: pass with the same counts.
- [ ] **Step 3: Validate and commit** "Give the driver tests one Users file, one Sample source and SqlTexts".

### Task 8: The caching tests' assertion (item 4)

**Files:** Modify `tests/SqlSource.Testing/Generator/CachingScene.cs`, `tests/SqlSource.Tests/Generator/CachingOptionsTests.cs`, `tests/SqlSource.Tests/Generator/CachingParseTests.cs`.

**Interfaces:**
- Produces: `public void ShouldHaveReadOnly(GeneratorDriverRunResult result, InMemoryAdditionalText file, IncrementalStepRunReason otherInput)` on `CachingScene`: `StepReasons.ParseInputs(result)` is `{file.Path: Modified, other.Path: otherInput}` and the `ParsedFile` step's reasons by file name are `{file's name: Modified, other's name: Cached}`, where `other` is whichever of `Users` and `Orders` is not `file`.

- [ ] **Step 1:** Replace each of the six `ParseInputs` dictionaries with the call, passing the reason the copy wrote; where a copy had no `ParsedFile` block, the run must still pass with the helper's (the stronger assertion), or that copy keeps its dictionary and the reason is noted in the commit message.
- [ ] **Step 2: Run** `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`, filter `Caching`: pass.
- [ ] **Step 3: Validate and commit** "Say once in CachingScene that only one file is read again".

### Task 9: The small copies, and the close (item 8)

**Files:**
- Create: `tests/SqlSource.Testing/CultureScope.cs`, `tests/SqlSource.Tests/CultureScopeTests.cs`
- Modify: `tests/SqlSource.Tool.Testing/Programs.cs` (`Waiter`), `tests/SqlSource.Tool.Tests/ProgramsTests.cs`, `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs` (`Wait`; `WaitWithAChild` stays), `tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs`, `tests/SqlSource.Tool.Tests/Realistic/RealisticCaseReaderTests.cs` (a private `CaseFolder(TempFolder temp, string name, string? query = Query, string? json = null)` for the 14 arrangements), `tests/SqlSource.Tests/Snapshot/SidecarJsonBuilderTests.cs`, `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs`, `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs`, the files of the 39 names
- Delete: `docs/tech-debt/TD-0074-duplicated-tests-across-the-engine-projects.md`, its row; remove `TD-0074` from code and docs (old plans keep theirs)
- Docs: `tests/README.md`, `tests/AGENTS.md`

**Interfaces:**
- `Programs.Waiter(int seconds)` returns `(string Program, string[] Arguments)`: `("ping", ["-n", "<seconds>", "127.0.0.1"])` on Windows, `("sleep", ["<seconds>"])` elsewhere.  `TerminationTests` writes `string.Join(' ', [program, .. arguments])` and, on Windows, ` &gt; nul`.
- `internal sealed class CultureScope(CultureInfo culture) : IDisposable` in namespace `SqlSource.Testing`: sets `CultureInfo.CurrentCulture`, and its `Dispose` puts back the one it found.  Test: `Dispose_AfterAnotherCulture_PutsBackTheOneItFound`.
- The 39 names get a scenario: the `Method` is what the test calls, and a test of a value names the value as `Method` and what it is read under as `Scenario`, such as `Patience_OfEveryWait_IsTwoMinutes`.  `git grep -nE "(void|Task) [A-Z][A-Za-z0-9]*_[A-Za-z0-9]+\(" -- tests` then finds no test.

- [ ] **Step 1: Test** `CultureScopeTests`; build fails; implement; pass.
- [ ] **Step 2:** `Waiter`, `CaseFolder`, the three cultures, the names.
- [ ] **Step 3: Resolve TD-0074** as `docs/tech-debt/AGENTS.md` says: `git grep -n "TD-0074" -- . ':!docs/superpowers'` finds nothing.
- [ ] **Step 4: Run** every test project; validate; commit "Share the small copies of the tests, name every test in three parts, and resolve TD-0074".
- [ ] **Step 5: Count** at most 150 files against `origin/main` (after Part 1 merged).  Part 2 ends: review, push request, pull request "(part 2 of 2)".
