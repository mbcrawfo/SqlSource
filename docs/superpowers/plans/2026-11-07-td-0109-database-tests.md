# TD-0109 database tests (items 7, 14, 18, 20, 44, 46, 48) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve items 7, 14, 18, 20, 44, 46 and 48 of TD-0109 in the container tests: one shared body for each per-engine copy, the PostgreSQL floor image as a constant, the SQL Server batch tests in a class of their own, one rolled-back transaction helper, and the missing case of item 20.  Test-only; no test loses what it checks.

**Architecture:** Shared bodies go where the repository's layering allows: driver-free helpers in `tests/DatabaseTests/TestHarness/EndToEnd/` (items 14, 44), the realistic checks in `tests/SqlSource.Tool.Testing/Realistic/` beside the existing `Realistic*Checks` (item 7), with each engine passing its driver's difference through a small `IRealisticSeed`.  SQL Server's driver-floor project gets its own `EndToEndSources.RolledBackAsync` (item 48), since it cannot reference the end-to-end project.

**Tech Stack:** C# / .NET 10, xUnit v3, Shouldly, Npgsql, Microsoft.Data.SqlClient (7 and 6.1), Dapper, Testcontainers.

**Spec:** `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 7, 14, 18, 20, 44, 46, 48 (Problem and Proposed fix).

## Where the merged code differs from the spec

- Line numbers in the spec have moved (td-0105, td-0106, td-0090 changed the files); find each site by name.
- Item 46: td-0090 added batch tests, so the class now ends with 15 tests of queries of several statements (`StreamThenSelect` … `StreamNumbersThenMark_BreakPartway_…`), and `StreamNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackAfterTwoRows` (a NULL then a failed conversion, two statements) sits earlier in the file.  All 16 move.  `Accounts`, `ManyRows` and `DivideByZero` are used by both classes, so they move to a static helper (tests/AGENTS.md: classes split from one share through a static helper, no base class); `ConversionFailed` and `ArithmeticOverflow` are used by the batch tests alone and move with them.
- Item 7: the two engines open their sample's connection differently (PostgreSQL a pooled connection per sample from an `NpgsqlDataSource`, SQL Server one `SqlConnection` per case).  The shared check opens one connection per sample for both; for SQL Server that is a pooled connection per sample instead of per case, which changes no assertion.
- Item 7: SQL Server's method test also catches `SqlNullValueException` (in `System.Data.SqlTypes`, not the driver); the shared check catches `InvalidCastException or SqlNullValueException` for both engines, and names the exception's type in the failure, as SQL Server's does.

## Global Constraints

- Test-only change: nothing under `src/` changes.  No test loses what it checks.
- Every test class is `sealed`; assertions are Shouldly's; names are `Method_Scenario_Outcome`.  Leave test names alone (items 19, 54 belong to 109-names) unless the change rewrites the test anyway.
- Library types stay `internal`.  `TestHarness` brings no driver, no Dapper, nothing of the tool.  `Postgres.Npgsql8` and `SqlServer.SqlClient61` reference `src/SqlSource`, `tests/SqlSource.Repository` and `TestHarness` alone.
- Model reflection goes through `ModelReflection` of `TestHarness`.
- Copies that the projects' AGENTS.md call copies (SqlClient61's `JsonQueryTests`, `ListQueryTests`; Npgsql8's) change with their originals.
- Before each commit: `./format.sh`, then `./pre-commit-validation.sh` passes.  Run every container project changed (`dotnet test --project tests/DatabaseTests/<Project>`), and on the floor image too for a describer project's tests: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` / `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- `tests/AGENTS.md` is at its 8 KB limit: put any new rule in the deepest folder's AGENTS.md.  An AGENTS.md or README that names a moved or renamed file, type or test is updated in the same commit.
- Comments say what is true now, in the repository's voice (plain present tense, no history).
- Never record a problem as tech debt, deferred or a follow-up: report it to the controller.

## Review Focus

1. A shared check that hides an engine difference (connection lifetime, transaction type, NULL exception) behind a default — each engine's realistic Dapper and method tests must still run green on both images.
2. The moved batch tests' collection and constructor — `StreamingBatchTests` must be in `UsesSqlServer` and every one of the 16 tests still runs (count before and after).
3. `VariantCopies.ShouldShareHash` reading the wrong folder (an engine's `RepositoryPaths.EndToEndQueries` must be passed in, the harness has none of its own).
4. `ModelReflection` replacement of `PropertiesOf` — the name set must still be compared whole, not as a subset.
5. Item 20's planned request — the test must assert that the plan's list is empty, so a parser that started listing `@x` fails here.

---

### Task 1: The PostgreSQL floor image as a constant (item 18)

**Files:**
- Modify: `tests/DatabaseTests/TestHarness/DefaultImages.cs`
- Modify: `tests/DatabaseTests/Postgres/Renaming/VocabularyTests.cs` (`FloorImage`, `Header`)
- Modify: `AGENTS.md` (rule 5, the floor bullet), `tests/DatabaseTests/Postgres/README.md` (the image section and "The renaming's vocabulary")

**Interfaces:**
- Produces: `DefaultImages.PostgresFloor` (`public const string`, `"postgres:14.24"`).

- [ ] **Step 1:** Add `public const string PostgresFloor = "postgres:14.24";` to `DefaultImages`, with a comment: the oldest supported image, which CI runs; the renaming's vocabulary holds its words beside the default's; moving it is done by rule 5 of the root `AGENTS.md`.  The scripts' `sed` reads `const string Postgres = "` and does not match this line; keep it so.
- [ ] **Step 2:** In `VocabularyTests`, delete `FloorImage` and use `DefaultImages.PostgresFloor`; the header line that names `postgres:14.24` interpolates it (`$"…VocabularyTests.cs on {DefaultImages.PostgresFloor} and on the default image,"`), so the header the test compares with the committed list is unchanged.
- [ ] **Step 3:** Root `AGENTS.md` rule 5's floor bullet: `postgres:14.24` is also in `DefaultImages.PostgresFloor` of `tests/DatabaseTests/TestHarness/DefaultImages.cs`.  Keep the file under 16 KB (`AgentsFileTests`).  `tests/DatabaseTests/Postgres/README.md`: where it says moving the floor bumps its tag in `CONTRIBUTING.md`, `build.yml` and the root `AGENTS.md`, add `DefaultImages.PostgresFloor`.
- [ ] **Step 4:** Run `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.Renaming.VocabularyTests"` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; expected: `Vocabulary_OfThisImage_IsInTheCommittedList` passes on both.  Run `dotnet test --project tests/SqlSource.Repository.Tests`; expected: pass.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: the PostgreSQL floor image is DefaultImages.PostgresFloor (TD-0109 item 18)`.

### Task 2: A DECLAREd variable through the plan (item 20)

**Files:**
- Modify: `tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs` (`DescribeAsync_DeclaredVariable_IsNoParameter`)

- [ ] **Step 1:** Rewrite the test to build its request with `Planned("mssql", "DECLARE @x int = 1; SELECT id FROM dbo.users WHERE id = @x")` (of `DescribeRequests`), assert `request.Parameters.ShouldBeEmpty()` (the plan lists no parameter for the variable), then `ShouldDescribeAsync(request)` and `description.Parameters.ShouldBeEmpty()` (the server reports none).  Comment: the request is the plan's, so the parser's reading of the `DECLARE` and the sample the run sends are both run.
- [ ] **Step 2:** Run `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-method "*DeclaredVariable*"` on the default image and on the floor image.  Expected: pass.  If it fails because the tool lists `@x` or the server reports it, stop and report the failure to the controller (it may be a describer bug); do not change `src/`.
- [ ] **Step 3:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: the DECLAREd variable test builds its request through the plan (TD-0109 item 20)`.

### Task 3: Driver-free end-to-end helpers in the harness (items 14, 44)

**Files:**
- Create: `tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs`, `JsonElements.cs`, `CountingSequence.cs`, `VariantCopies.cs`
- Modify: `TestHarness/EndToEnd/ModelReflection.cs`, `AllTypesRows.cs`, `SqlServerAllTypesRows.cs`
- Modify: the four `ListQueryTests.cs` (`Postgres/EndToEnd`, `SqlServer/EndToEnd`, `Postgres.Npgsql8`, `SqlServer.SqlClient61`), the four `JsonQueryTests.cs` (`Postgres/EndToEnd/Json`, `SqlServer/EndToEnd/Json`, `Postgres.Npgsql8`, `SqlServer.SqlClient61`), `Postgres/EndToEnd/AllTypesQueryTests.cs`, `Postgres.Npgsql8/AllTypesQueryTests.cs`, both `EndToEnd/VariantCopyTests.cs`

**Interfaces:**
- Produces (all `internal static`, namespace `SqlSource.DatabaseTests.TestHarness.EndToEnd` or the folder's existing one):
  - `ListArguments.CollectionOf(string kind, int[] values) : IEnumerable<int>` (kinds `array`, `List`, `HashSet`, `lazy`); `ListArguments.IdsOf(int count) : int[]` and `ListArguments.Unused` (`const int`, 1000), the first id no account has.
  - `JsonElements.ElementOf([StringSyntax(Json)] string json) : JsonElement`; `JsonElements.ShouldBeSame(JsonElement? read, JsonElement? written, string name)`.
  - `CountingSequence(int[] items) : IEnumerable<int>`, `internal sealed`, as the two copies have it.
  - `ModelReflection.ShouldHaveProperties(Type model, IEnumerable<string> names) : IReadOnlyList<PropertyInfo>`: asserts the model's property names, ordered by ordinal, equal `names` ordered by ordinal (message `$"The properties of {model}"`), and returns `PropertiesOf(model)`.
  - `VariantCopies.ShouldShareHash(string queriesFolder, string file, string original, string copy)`: the body of today's `Copy_CommittedEntry_HasTheHashOfItsOriginal`, reading `Path.Combine(queriesFolder, file + ".json")`.

- [ ] **Step 1:** Create the helpers above and call them from every copy; delete the private copies.  Each test class's own `Unused` uses go to `ListArguments.Unused` (or keep a class constant equal to it only if other tests of the class name it in a way that reads better; one value either way).  The `AllTypesRows` / `SqlServerAllTypesRows` private `PropertiesOf` become calls of `ModelReflection.ShouldHaveProperties(row.GetType(), …Keys)`.  `Postgres.Npgsql8/AllTypesQueryTests.cs`'s two `GetProperties()` become `ModelReflection.NamesOf(…)`, keeping what they compare.  Each `VariantCopyTests` keeps its rows and comment and becomes one line: `VariantCopies.ShouldShareHash(RepositoryPaths.EndToEndQueries, file, original, copy)`.
- [ ] **Step 2:** Run `dotnet test` on each of the six projects changed (`Postgres`, `SqlServer`, `Postgres.Npgsql8`, `SqlServer.SqlClient61` by `--project tests/DatabaseTests/<name>`, filtered to `ListQueryTests`, `JsonQueryTests`, `AllTypesQueryTests`, `VariantCopyTests` and the all-types tests that use `AllTypesRows`), and confirm the test counts of those classes are what they were before the change.  Expected: pass, same counts.
- [ ] **Step 3:** If `tests/DatabaseTests/*/AGENTS.md` or a README names one of the deleted helpers, update it.  `./format.sh`, `./pre-commit-validation.sh`, commit `test: the end-to-end projects share their driver-free helpers and VariantCopies (TD-0109 items 14, 44)`.

### Task 4: One rolled-back transaction helper (item 48)

**Files:**
- Create: `tests/DatabaseTests/SqlServer.SqlClient61/EndToEndSources.cs`
- Modify: `SqlServer/EndToEnd/Json/JsonQueryTests.cs`, `SqlServer.SqlClient61/JsonQueryTests.cs`, `NodaTimeQueryTests.cs`, `NetTopologySuiteQueryTests.cs`, `MethodAllTypesTests.cs`; `SqlServer.SqlClient61/AGENTS.md` if it needs the new file named

**Interfaces:**
- Consumes: `EndToEndSources.RolledBackAsync(SqlConnection connection, Func<DbTransaction, Task> write, CancellationToken cancellationToken)` of `SqlServer/EndToEnd/EndToEndSources.cs`.
- Produces: `internal static class EndToEndSources` in `SqlSource.DatabaseTests.SqlServer.SqlClient61` with the same `RolledBackAsync` signature and comment, so the copied tests read alike.

- [ ] **Step 1:** In both `JsonQueryTests`, delete `BeginAsync` and run each of its ten tests' bodies inside `await EndToEndSources.RolledBackAsync(connection, async transaction => { … }, token)`; a method that needs `SqlTransaction` takes a cast inside the lambda only if the generated method's parameter demands it (it takes `DbTransaction`).  The two files stay copies of each other but for their namespaces.
- [ ] **Step 2:** In `NodaTimeQueryTests`, `NetTopologySuiteQueryTests` and `MethodAllTypesTests` of SqlClient61, delete the private `RolledBackAsync` and call `EndToEndSources.RolledBackAsync`; `MethodAllTypesTests`' calls create their connection (`await using var connection = new SqlConnection(app.ConnectionString);`, or a private `Connection()` as the JSON tests have) and pass it.
- [ ] **Step 3:** Run `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "*JsonQueryTests"` and `dotnet test --project tests/DatabaseTests/SqlServer.SqlClient61`.  Expected: pass, same counts as before.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit `test: SQL Server's tests roll back through one RolledBackAsync per project (TD-0109 item 48)`.

### Task 5: The batch tests in StreamingBatchTests (item 46)

**Files:**
- Create: `tests/DatabaseTests/SqlServer/EndToEnd/StreamingBatchTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/StreamingRows.cs`
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/StreamingQueryTests.cs`; `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md` and `tests/DatabaseTests/SqlServer/README.md` where they name the streaming tests

**Interfaces:**
- Produces: `internal static class StreamingRows` with `Accounts` (`CollectedAccount[]`, the seed's three), `ManyRows` (`const int`, 1_000_000) and `DivideByZero` (`const int`, 8134), each with its comment; `public sealed class StreamingBatchTests(SqlServerDatabases sqlServer)` in `[Collection(UsesSqlServer.Name)]`, holding `ConversionFailed` (245) and `ArithmeticOverflow` (220).

- [ ] **Step 1:** Count the tests of `StreamingQueryTests` (`dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "SqlSource.DatabaseTests.SqlServer.EndToEnd.StreamingQueryTests"`; note the total).
- [ ] **Step 2:** Move the 16 tests of queries of several statements, unchanged, with their comments: `StreamNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackAfterTwoRows` and every test from `StreamThenSelect_TwoStatements_YieldsTheRowsOfTheFirst` to the end of the file.  Split the class comment: the lifecycle and the failing-row part stay; the several-statements part (the iterator reads the rest of the batch, XACT_ABORT, rollbacks, `TransactionRolledBackException`) goes to `StreamingBatchTests`.  Both classes read the shared constants from `StreamingRows`.
- [ ] **Step 3:** Run both classes; expected: pass, and their counts add up to Step 1's total.
- [ ] **Step 4:** Update the docs that name where these tests are; `./format.sh`, `./pre-commit-validation.sh`, commit `test: SQL Server's batch streaming tests are StreamingBatchTests (TD-0109 item 46)`.

### Task 6: The realistic Dapper, method and noise checks (item 7)

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Realistic/IRealisticSeed.cs`, `RealisticDapperChecks.cs`, `RealisticMethodChecks.cs`, `RealisticNoiseChecks.cs`
- Create: `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticSeed.cs`, `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticSeed.cs`
- Modify: both engines' `Realistic/Generation/RealisticDapperTests.cs`, `RealisticMethodTests.cs`, `Realistic/Describer/RealisticNullabilityNoiseTests.cs`, `Realistic/Describer/RealisticNullabilitySafetyTests.cs`, `Realistic/Describer/RealisticNullabilityOracle.cs`; `tests/SqlSource.Tool.Testing/Realistic/RealisticNoise.cs`; both `Realistic/AGENTS.md`; any README or `docs/architecture/` page that names these bodies

**Interfaces:**
- Consumes: `RealisticCorpus`, `RealisticModels` (`OutputModelOf`, `InputModelOf`, `MethodOf`, `SkipUnlessReadThroughDapper`, `SkipUnlessReadThroughMethod`, `EntryOf`), `RealisticGeneration.SqlOf`, `RealisticInputs.ArgumentsOf`, `RealisticCalls.InvokeAsync`/`NullColumnsOf`, `RealisticDescribe`, `RealisticSampleRun`, `RealisticNoise.NoisyOf`/`Verdict`.
- Produces:
  - `internal interface IRealisticSeed : IAsyncDisposable` — `Task<DbConnection> OpenAsync(CancellationToken token)` (an open connection to "realistic", which the caller disposes); `Task<object> InputModelAsync(DbConnection connection, Type input, IReadOnlyList<SidecarParameter> parameters, IReadOnlyDictionary<string, string> sample)` (the sample's input model, read before a transaction begins on `connection`); `Task RollBackAfterAsync(DbTransaction transaction)` (a rollback after a failure, which does not take the failure's place).
  - Each engine's `internal sealed class RealisticSeed(<Engine>Databases databases) : IRealisticSeed`: PostgreSQL holds `EndToEndSources.Realistic(postgres)`, opens from it, reads the input model through it (ignoring `connection`) and disposes it; SQL Server opens a new `EndToEndSources.Realistic(sqlServer)` per call, reads the input model on `connection`, and disposes nothing.  Both roll back through their `RealisticNullabilityOracle.RollBackAfterAsync`.
  - Each engine's `RealisticNullabilityOracle.SampleRunOn(string connection) : RealisticSampleRun` — the lambda the safety tests write today, used by the safety tests and the method checks.
  - `internal sealed class RealisticDapperChecks(RealisticCorpus corpus, RealisticModels models, int major, Func<IRealisticSeed> seedOf)` with `Task ShouldReadEveryRowIntoItsModelAsync(string name)`.
  - `internal sealed class RealisticMethodChecks(RealisticCorpus corpus, RealisticModels models, RealisticDescribe describe, int major, string connection, Func<IRealisticSeed> seedOf, RealisticSampleRun run)` with `Task ShouldReadEveryRowIntoItsModelAsync(string name)`.
  - `internal sealed class RealisticNoiseChecks(RealisticCorpus corpus, RealisticDescribe describe, int major, string connection)` with `static void ShouldHoldOneBudgetForEachRecordedMajor(IReadOnlyDictionary<int, int> budgets, IEnumerable<int> recordedMajors)` and `Task ShouldBeTheBudgetAsync(IReadOnlyDictionary<int, int> budgets)`.

- [ ] **Step 1:** Write the checks from today's bodies.  Dapper: per sample, `await using var connection = await seed.OpenAsync(token)`, input arguments read through the seed, a transaction begun on that connection and given to `QueryAsync`, rolled back with `CancellationToken.None`; at least one row over the samples.  Method: as PostgreSQL's, with the catch `when (thrown is InvalidCastException or SqlNullValueException)`, the failure `$"{found} sample {k} threw {thrown.GetType().Name}: {thrown.Message}\n" + NullColumns…`, the rollback through the seed after a failure, and the null columns from `run(request, parameters, sample)` then `RealisticCalls.NullColumnsOf`.  Noise: SQL Server's body, which lists the noisy columns on a failure, for both engines; the over-budget hint text is the one both have today.  The classes' comments carry what today's test classes say of their bodies, the engines' notes (why the input is read before the transaction) going to `IRealisticSeed` and each `RealisticSeed`.
- [ ] **Step 2:** Make each engine's three test classes one-line tests over the checks (the noise test keeps its `Budgets` field and its `SkipUnlessRecordedMajor` line, as `tests/DatabaseTests/*/Realistic/AGENTS.md` asks), and the safety tests use `RealisticNullabilityOracle.SampleRunOn`.  Delete `RealisticNoise.CountOf` if nothing calls it any more, and if a test of `tests/SqlSource.Tool.Tests/Realistic/RealisticNoiseTests.cs` calls it, point that test at `NoisyOf(...).Count` so it checks the same.
- [ ] **Step 3:** Run `dotnet test --project tests/DatabaseTests/Postgres -- --filter-namespace "SqlSource.DatabaseTests.Postgres.Realistic*"` and the same for `SqlServer`, each on the default and on the floor image, and `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "SqlSource.Tool.Tests.Realistic"`.  Expected: pass, with the same test counts as before the change.
- [ ] **Step 4:** Name `RealisticDapperChecks`, `RealisticMethodChecks` and `RealisticNoiseChecks` in both `Realistic/AGENTS.md` beside the other `Realistic*Checks` (both files under 8 KB), and update any README or architecture page that describes these bodies as per-engine.  `./format.sh`, `./pre-commit-validation.sh`, commit `test: the realistic Dapper, method and noise bodies are shared checks (TD-0109 item 7)`.

### Task 7: Record the fixes in TD-0109

**Files:**
- Modify: `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md`

- [ ] **Step 1:** Strike items 7, 14, 18, 20, 44, 46 and 48 in Problem (`~~…~~  Fixed: <what is true now>`) and their Proposed fix lines (`~~…~~  Done.`), as items 8 to 10 are.  Item 20's fix line is the shared "20. to 41." line: leave it unstruck while item 32 is open.  Rewrite the Impact paragraph so it names only what is still open (it names items 7, 18, 20 and 32 today: keep item 32 alone).  Touch no other item.  If no item is left open after a sync, delete the file and its row and search for `TD-0109`, as `docs/tech-debt/AGENTS.md` says.
- [ ] **Step 2:** `./pre-commit-validation.sh`, commit `docs: TD-0109 items 7, 14, 18, 20, 44, 46 and 48 are fixed`.
