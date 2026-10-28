# Database tests, piece 4a: TD-0080 items 1 and 2, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The six realistic test classes that the two engines copy keep one-line facts in each engine and have one body each in `tests/SqlSource.Tool.Testing/Realistic/`, and `RowBufferQueryTests`, copied whole, keeps one-line facts in each engine and has its body and helpers in the harness's `EndToEnd/`, with no test checking anything different.

**Architecture:** The shared bodies follow `ErrorPaths` and `ToolProcess` of `tests/SqlSource.Tool.Testing`: a class of `Should…` methods that takes what differs by engine as arguments, the corpus and `RealisticDescribe` the engine's `RealisticCases` already binds, the server's major and connection string from the fixture, a delegate for what reads the database through a driver, and the engine test class constructs it and keeps the `[Fact]`s and `[Theory]`s with their names.  `RowBufferQueryTests`' body becomes `RowBufferReads<TConnection, TRow>` in the harness, generic over the driver's connection and the generated model, taking a factory of closed connections and the generated methods as delegates.

**Tech Stack:** .NET 10 SDK, xunit v3 on Microsoft.Testing.Platform, Shouldly, Roslyn (`MetadataReference`), bash.

**Spec:** `docs/superpowers/specs/2026-10-28-database-tests-folder-design.md`, section "Piece 4: the remaining items of TD-0080", items 1 and 2, with "Intent", "Goals", "Constraints" and "The layout"; `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 1 and 2.

## Where the merged code differs from the spec

- Pieces 1 to 3 merged as the spec planned: the harness is `tests/DatabaseTests/TestHarness` (`SqlSource.DatabaseTests.TestHarness`, folder `EndToEnd/` the namespace `SqlSource.DatabaseTests.TestHarness.EndToEnd`), and `tests/SqlSource.Tool.Testing` references it.  `RealisticCorpus`, `RealisticDescribe` and `RealisticModels` of `tests/SqlSource.Tool.Testing/Realistic/` are already the engine binding of the corpus, held by each engine's `Realistic/RealisticCases.cs`; the bodies build on them rather than on a new record.
- `RealisticModelNullabilityTests` is already one line in each engine, calling `RealisticModels.ShouldBeAsNullableAsItsTruth` of `tests/SqlSource.Tool.Testing`: item 1 names it, and it needs no change.
- TD-0080 item 1 names six classes; the oracle's answer, `NullabilityRun`, is a record copied between the two `RealisticNullabilityOracle.cs` that the moved body of `RealisticNullabilitySafetyTests` reads, so it moves to `tests/SqlSource.Tool.Testing/Realistic/` with that body and the two oracles return it.  Nothing else of the oracles or of `RealisticSamples` moves: they read the database through the driver.
- The engines' `RealisticCasesTests.Cases`, the `TheoryData<string>` every theory over the corpus takes, stays in each engine: it is one line, and `MemberData` needs a member of the test assembly.
- The spec's "takes the engine's methods as delegates" for item 2: PostgreSQL's test took its connection from an `NpgsqlDataSource` made and disposed in each test, SQL Server's from `new SqlConnection`; the harness's helper takes a `Func<TConnection>` of a closed connection and opens it itself for an even count, so PostgreSQL's test class holds the data source and disposes it, as `ErrorPathTests` holds `ErrorPaths`.  An even count still runs on an open connection and an odd one on a closed one.
- Item 7 (`td-0080-small`, which merges after this) will change `RealisticSamples.Substitute` to take the described parameters; the one call of it outside the tests of the helpers is now the delegate in PostgreSQL's `RealisticNullabilitySafetyTests.cs`, which that unit adapts.

## Global Constraints

- No test checks anything different afterwards: every test keeps its class, its name, its attributes and its collection, and its assertions and skips are the moved body's, in the same order.  The tests pass on both engines' default and floor images.
- `tests/SqlSource.Tool.Testing` gets no new package or project reference; it references `src/SqlSource.Tool`, `src/SqlSource`, `tests/SqlSource.Testing` and the harness today.  The harness gets no new package or project reference: `TestLayoutTests.TestHarness_References_BringNoDriverAndNothingOfTheTool` of `tests/SqlSource.Tests` pins its list.
- The libraries' types stay `internal` (`tests/AGENTS.md`).
- No `.csproj`, no `RepositoryPaths.cs`, nothing under `tests/SqlSource.Tests/TestHarness/`, `.github/` or `pre-commit-validation.sh` changes: the sibling units own them.  Of the shared documents, only `tests/README.md` changes, and only the sentences that name the helpers moved; `tests/DatabaseTests/README.md` and `tests/DatabaseTests/AGENTS.md` name none of them and stay as they are.
- `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`: items 1 and 2 are struck by editing their lines, as item 9 was, and nothing else is added to the file.
- An `AGENTS.md` has one rule per bullet of at most 80 words and is at most 8192 bytes; `tests/AGENTS.md` is at its limit and is not touched.
- Every commit passes `./format.sh` then `./pre-commit-validation.sh` first; no check is skipped or edited.  A change under `tests/SqlSource.Tool.Testing/Realistic/` or the harness is scope `both`, so a plain run runs both engine pairs.
- Plans and specs under `docs/superpowers/` are history and are not rewritten.
- Commit messages are one imperative sentence, as the repository's are, and end with the attribution line the session's instructions give.

## Review Focus

- A shared body that reads the generic row type's equality: `pooledRows.ShouldBe(expected)` compares generated `CountedNumber` records by value in both engines today; the harness's `RowBufferReads<TConnection, TRow>` keeps `TRow` unconstrained and the engine passes `static n => new CountedNumber(n)`, so the comparison is the same records.  Pinned by the engines' `RowBufferQueryTests` at every count in Task 4.
- The runtime-type assertion of each collection type (`ShouldBeOfType<TRows>()` on rows declared `IReadOnlyList<T>` and found to be an array) is kept by keeping the delegates typed `Task<IEnumerable<TRow>>` and `Rows<T>` boxing the declared type; a helper generic on the declared type would make the assertion vacuous.  Pinned by the array, interface and list rows of Task 4.
- `RealisticDescribe.EntryAsync` must open the session as `Sessions.OpenAsync(connection, database: RealisticDatabase)` did: `new PostgresDescriber()` is `PlannerSettings.None`, and `SqlServerDescriber.OpenAsync` with no token is what the engine's `Sessions` passed, so the in-memory sidecar is described the same way.  Pinned by `Run_CaseWithRefusedModels_GivesItsOneModelError` on PostgreSQL, whose `two-columns-named-id` is the one case with `models`, in Task 2.
- The skip of `DescribeAsync_Case_IsItsCommittedEntry` on an unrecorded major must keep its reason, which `EngineImage.SkipUnlessRecordedMajor(major, what)` writes from the engine's recorded majors; the shared body reaches it through `RealisticCorpus.Image`.  Pinned by the floor-image runs of Task 5, where the major is recorded, and by the message text in Task 2's body.
- PostgreSQL's `RealisticNullabilityChecks` delegate builds the types dictionary once per sample where the test built it once per case; the dictionary is built from the same described parameters, so the substitution is the same.  Pinned by `Execute_Case_ReturnsNoNullWhereEitherSaysNotNull` over every PostgreSQL case in Task 3.

---

### Task 1: The bodies that need no server: the corpus's folders and its claims

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Realistic/RealisticFolderChecks.cs`, `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs`
- Modify: `tests/DatabaseTests/Postgres/Realistic/RealisticCasesTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/RealisticCasesTests.cs`, `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticClaimsTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticClaimsTests.cs`
- Test: the four modified classes

**Interfaces:**
- Produces: `internal static class RealisticFolderChecks` in `SqlSource.Tool.Testing.Realistic` with `public static void ShouldHoldAKnownCase(RealisticCorpus corpus, string knownCase)` and `public static void ShouldHoldItsFilesAlone(RealisticCorpus corpus, string name)`; `internal static class RealisticClaimChecks` in the same namespace with `public static void ShouldBeTheCasesWithModelsOnceAtEachOutput(RealisticCorpus corpus, string name)`, `public static void ShouldBeItsClassAtEachOutput(RealisticCorpus corpus, string name, string queryType, string methodType)`, `public static void ShouldAllBeAtModels(RealisticCorpus corpus)`, `public static void ShouldAllBeAtCodeGen(RealisticCorpus corpus)` and `public static void ShouldNameOnlyCasesThatExist(RealisticCorpus corpus)`.

- [ ] **Step 1: Write `RealisticFolderChecks`**

The bodies of `All_Cases_HoldAKnownCase` and `Folder_Case_HoldsItsFilesAlone` of either engine's `RealisticCasesTests.cs`, with `RealisticCases.Corpus` replaced by the `corpus` argument and `KnownCase` by `knownCase`.  The constant `FinderFile = ".DS_Store"` and its comment move here.  The class comment: the checks of a corpus's `Cases/` folder that both engines' `RealisticCasesTests` run, that every folder is a case holding its files alone.

- [ ] **Step 2: Write `RealisticClaimChecks`**

The bodies of the five tests of either engine's `RealisticClaimsTests.cs`, with `RealisticCases.Corpus` replaced by `corpus`; `ShouldBeItsClassAtEachOutput` reads the case named `name` and asserts the `TypeName` at `ModelsOutput` is `queryType` and at `CodeGenOutput` is `methodType`.  The class comment: the checks of a corpus's claims that both engines' `RealisticClaimsTests` run; the reading of a claim is tested in `tests/SqlSource.Tool.Tests/Realistic/`.

- [ ] **Step 3: Make the four engine classes one-line facts**

Each test keeps its attributes and name and becomes an expression body calling the check with `RealisticCases.Corpus`.  PostgreSQL's `RealisticCasesTests` keeps `KnownCase = "aggregate-cte-left-joined"`, SQL Server's `"customers-search-paged"`, and both keep `Cases`; PostgreSQL's `RealisticClaimsTests` passes `"cte-chain"`, `"CteChainQuery"`, `"CteChainMethod"`, SQL Server's `"category-tree-recursive"`, `"CategoryTreeRecursiveQuery"`, `"CategoryTreeRecursiveMethod"`.  Drop the usings the bodies took with them (`System`, `System.IO`, `System.Linq`, `Shouldly` where nothing left uses them) and add `using SqlSource.Tool.Testing.Realistic;` where it is missing.  The class comments say the bodies are the checks of `tests/SqlSource.Tool.Testing/Realistic/`.

- [ ] **Step 4: Run the four classes**

Run: `./format.sh`, then `dotnet test --project tests/DatabaseTests/Postgres --filter-class SqlSource.DatabaseTests.Postgres.Realistic.RealisticCasesTests --filter-class SqlSource.DatabaseTests.Postgres.Realistic.Generation.RealisticClaimsTests` and the same for `tests/DatabaseTests/SqlServer` with the `SqlServer` namespaces.
Expected: every test passes; no container starts, since neither class is in the collection.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes, both engine pairs run.

```bash
git add tests/SqlSource.Tool.Testing/Realistic/RealisticFolderChecks.cs tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs tests/DatabaseTests/Postgres/Realistic tests/DatabaseTests/SqlServer/Realistic
git commit -m "Move the bodies of RealisticCasesTests and RealisticClaimsTests into tests/SqlSource.Tool.Testing"
```

---

### Task 2: The description and the refusal of models, against the server

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticCorpus.cs`, `tests/SqlSource.Tool.Testing/Realistic/RealisticDescribe.cs`
- Create: `tests/SqlSource.Tool.Testing/Realistic/RealisticDescriptionChecks.cs`, `tests/SqlSource.Tool.Testing/Realistic/RealisticRefusalChecks.cs`
- Modify: `tests/DatabaseTests/Postgres/Realistic/Describer/RealisticDescriptionTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticDescriptionTests.cs`, `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelRefusalTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelRefusalTests.cs`
- Test: the four modified classes, on each engine's default image

**Interfaces:**
- Produces: `RealisticCorpus.Image`, `public EngineImage Image { get; }`, the image the corpus was made from; `RealisticDescribe.Corpus`, `public RealisticCorpus Corpus { get; }`; `RealisticDescribe.EntryAsync`, `public async Task<SidecarEntry> EntryAsync(RealisticCase found, string connection)`.  `internal sealed class RealisticDescriptionChecks(RealisticDescribe describe, int major, string connection)` with `public Task ShouldBeRefusedOnlyAsItsCaseSaysAsync(string name)` and `public Task ShouldBeItsCommittedEntryAsync(string name)`.  `internal sealed class RealisticRefusalChecks(RealisticDescribe describe, int major, string connection, IReadOnlyList<MetadataReference> references)` with `public Task ShouldGiveItsOneModelErrorAsync(string name)`.
- Consumes: `RealisticDescribe.Start`, `DescribeOnceAsync`, `RequestOf`, `EntryOf`; `EngineImage.SkipUnlessRecordedMajor(int server, string what)`; `RealisticEngine.Product`.

- [ ] **Step 1: Expose the image and the corpus**

In `RealisticCorpus`, the private constructor keeps its `image` as `Image`, with a comment that the describer's checks skip through its gates.  In `RealisticDescribe`, add `public RealisticCorpus Corpus { get; } = corpus;`.

- [ ] **Step 2: Add `RealisticDescribe.EntryAsync`**

The entry that a run writes for a case that is not refused, described in a session of its own on the corpus's database, for a sidecar held in memory: opens through `describer().OpenAsync(database, connection)` and throws `InvalidOperationException` as `DescribeAsync` does when no session opened, describes `RequestOf(found)` with `TestContext.Current.CancellationToken`, calls `Assert.Fail($"{found} was refused:\n{RealisticDescriptions.RenderFailure(described.Failure)}")` when refused, and returns `EntryOf(found, session.Server, described.Value)`.  Pull the opening into a private `OpenAsync(RealisticCase found, string connection)` that `DescribeAsync` shares, so the message is written once.  This is the body of each engine's private `SidecarOfAsync` but its `Sidecars.Of`, which the caller keeps.

- [ ] **Step 3: Write `RealisticDescriptionChecks`**

The bodies of `DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays` and `DescribeAsync_Case_IsItsCommittedEntry` of either engine's `RealisticDescriptionTests.cs`, with `RealisticCases.Describe` as `describe`, `postgres.Major` as `major`, `postgres.RealisticConnectionString` as `connection`, `postgres.SkipUnlessRecordedMajor("The committed entries")` as `describe.Corpus.Image.SkipUnlessRecordedMajor(major, "The committed entries")`, and `"PostgreSQL"` in the `IsCheckedOn` skip message as `describe.Corpus.Engine.Product`.  `SidecarScript`, `AnyServer`, `AnyVersion` and `TextOf` move with them.  The class comment is the test class's, saying both engines' `RealisticDescriptionTests` run it.

- [ ] **Step 4: Write `RealisticRefusalChecks`**

The body of `Run_CaseWithRefusedModels_GivesItsOneModelError` of either engine's `RealisticModelRefusalTests.cs`, with `RealisticCases.Corpus` as `describe.Corpus`, `postgres.Major` as `major`, `references: [MetadataReference.CreateFromFile(...)]` as `references: references`, and `await SidecarOfAsync(found, query)` as `Sidecars.Of(query, await describe.EntryAsync(found, connection))`.  `CasesSettingsPath`, `QueryPath` and `Source` move with it, with their comments.  The class comment is the test class's first four sentences.

- [ ] **Step 5: Make the four engine classes one-line facts**

Each keeps its `[Collection]`, its fixture parameter, its attributes and its names, holds `private readonly RealisticDescriptionChecks _checks = new(RealisticCases.Describe, postgres.Major, postgres.RealisticConnectionString);` (SQL Server's from `sqlServer`), and each theory is `=> _checks.Should…Async(name)`.  `RealisticModelRefusalTests` holds `RealisticRefusalChecks` with `[MetadataReference.CreateFromFile(typeof(NpgsqlConnection).Assembly.Location)]` on PostgreSQL and `[SqlClientReferences.SqlClient7]` on SQL Server.  Drop `Sessions`, `GeneratorHarness` and the other usings nothing left uses; the class comments keep their first sentence and say the body is the check of `tests/SqlSource.Tool.Testing/Realistic/`; SQL Server's refusal comment keeps "No case of the corpus has one today, so every case skips, until one comes to need it."

- [ ] **Step 6: Run the four classes on the default images**

Run: `./format.sh`, then `dotnet test --project tests/DatabaseTests/Postgres --filter-class SqlSource.DatabaseTests.Postgres.Realistic.Describer.RealisticDescriptionTests --filter-class SqlSource.DatabaseTests.Postgres.Realistic.Generation.RealisticModelRefusalTests` and the same for `tests/DatabaseTests/SqlServer`.
Expected: every row passes or skips as before; on PostgreSQL `Run_CaseWithRefusedModels_GivesItsOneModelError` passes for `two-columns-named-id` and skips every other case; on SQL Server every row of it skips.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/SqlSource.Tool.Testing/Realistic tests/DatabaseTests/Postgres/Realistic tests/DatabaseTests/SqlServer/Realistic
git commit -m "Move the bodies of RealisticDescriptionTests and RealisticModelRefusalTests into tests/SqlSource.Tool.Testing"
```

---

### Task 3: The nullability's safety, with the oracle as the engine's delegate

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Realistic/NullabilityRun.cs`, `tests/SqlSource.Tool.Testing/Realistic/RealisticNullabilityChecks.cs`
- Modify: `tests/DatabaseTests/Postgres/Realistic/Describer/RealisticNullabilityOracle.cs:14`, `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracle.cs:15`, `tests/DatabaseTests/Postgres/Realistic/Describer/RealisticNullabilitySafetyTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilitySafetyTests.cs`
- Test: the two safety classes, the two `RealisticNullabilityOracleTests` and the two `RealisticMethodTests`, which read the oracle's answer, on each engine's default image

**Interfaces:**
- Produces: `internal sealed record NullabilityRun(IReadOnlyList<string> Names, IReadOnlyList<bool> SawNull, int Rows)` in `SqlSource.Tool.Testing.Realistic`, with the comment of the engines' copies; `internal delegate Task<NullabilityRun> RealisticSampleRun(DescribeRequest request, IReadOnlyList<DescribedParameter> parameters, IReadOnlyDictionary<string, string> sample)`, in `RealisticNullabilityChecks.cs`, the run of a case's query over the seed with one sample bound, by the engine's oracle; `internal sealed class RealisticNullabilityChecks(RealisticDescribe describe, int major, string connection, RealisticSampleRun run)` with `public Task ShouldBeNotNullOnlyWhereTheTruthIsAsync(string name)` and `public Task ShouldReturnNoNullWhereEitherSaysNotNullAsync(string name)`.

- [ ] **Step 1: Move `NullabilityRun`**

Delete the record from both oracles and write it in `NullabilityRun.cs` in `SqlSource.Tool.Testing.Realistic`; add `using SqlSource.Tool.Testing.Realistic;` to both oracles and to any test that names the record (`grep -rn NullabilityRun tests/DatabaseTests`).

- [ ] **Step 2: Write `RealisticNullabilityChecks`**

The bodies of `DescribeAsync_Case_IsNotNullOnlyWhereTheTruthIs` and `Execute_Case_ReturnsNoNullWhereEitherSaysNotNull` of SQL Server's `RealisticNullabilitySafetyTests.cs`, with `RealisticCases.Describe` as `describe`, `sqlServer.Major` as `major`, `sqlServer.RealisticConnectionString` as `connection`, and the oracle's call as `var run = await this.run(request, parameters, sample);`, and the private `ColumnOf(RealisticCase found, int index)` with them.  The class comment is PostgreSQL's class comment, which says that neither check reads the committed sidecar, and that the engine's oracle runs each sample.

- [ ] **Step 3: Make the two engine classes one-line theories**

Each holds `private readonly RealisticNullabilityChecks _checks` built from `RealisticCases.Describe`, the fixture's `Major` and `RealisticConnectionString`, and the engine's run.  PostgreSQL's run is

```csharp
(request, parameters, sample) =>
    RealisticNullabilityOracle.RunAsync(
        postgres.RealisticConnectionString,
        RealisticSamples.Substitute(
            request.Sql,
            sample,
            parameters
                .Where(static parameter => parameter.Type is not null)
                .ToDictionary(static parameter => parameter.Name, static parameter => parameter.Type!.Name)
        )
    )
```

and SQL Server's `(request, parameters, sample) => RealisticNullabilityOracle.RunAsync(sqlServer.RealisticConnectionString, RealisticSamples.Batch(request.Sql, sample, parameters))`.  SQL Server's comment on the second theory, that the query runs once for each sample with the sample's values bound at the described types, stays on its theory.

- [ ] **Step 4: Run the six classes on the default images**

Run: `./format.sh`, then for each engine `dotnet test --project tests/DatabaseTests/<Engine> --filter-class <ns>.Realistic.Describer.RealisticNullabilitySafetyTests --filter-class <ns>.Realistic.Describer.RealisticNullabilityOracleTests --filter-class <ns>.Realistic.Generation.RealisticMethodTests`.
Expected: every row passes or skips as before.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/SqlSource.Tool.Testing/Realistic tests/DatabaseTests/Postgres/Realistic tests/DatabaseTests/SqlServer/Realistic
git commit -m "Move the body of RealisticNullabilitySafetyTests into tests/SqlSource.Tool.Testing, with the oracle as the engine's delegate"
```

---

### Task 4: `RowBufferQueryTests`' body in the harness

**Files:**
- Create: `tests/DatabaseTests/TestHarness/EndToEnd/RowBufferReads.cs`
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/RowBufferQueryTests.cs`
- Test: the two modified classes, on each engine's default image

**Interfaces:**
- Produces, in `SqlSource.DatabaseTests.TestHarness.EndToEnd`:

```csharp
internal sealed class RowBufferReads<TConnection, TRow>(Func<TConnection> connect, Func<int, TRow> row)
    where TConnection : DbConnection
{
    public static TheoryData<int> Counts => [0, 1, 16, 17, 1024, 1025, 3000];

    // The rows of a method that declares another type, as the IEnumerable that the readers return.
    public static async Task<IEnumerable<TRow>> Rows<T>(Task<T> rows)
        where T : IEnumerable<TRow> => await rows;

    // Array, IEnumerable, IReadOnlyCollection and IReadOnlyList: the pooled method and its twin return a TRows of the numbers.
    public Task ShouldReadOneToTheCountAsync<TRows>(
        int count,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> unpooled
    );

    // List, IList and ICollection: as above with TRows a List<TRow>, then the pooled list is the caller's alone.
    public Task ShouldReadOneToTheCountIntoTheCallersListAsync(
        int count,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> unpooled
    );

    // ImmutableArray: as above with TRows an ImmutableArray<TRow> that is never the default, and Empty for no rows.
    public Task ShouldReadOneToTheCountIntoAnImmutableArrayAsync(
        int count,
        Func<TConnection, CancellationToken, Task<ImmutableArray<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<ImmutableArray<TRow>>> unpooled
    );
}
```

- [ ] **Step 1: Write `RowBufferReads`**

The class comment is the test class's, saying both end-to-end projects' `RowBufferQueryTests` run it with their generated methods, and that an even count runs on a connection the helper opened and an odd one on the closed connection `connect` gives.  The three public `Should…` methods call one private `ShouldReadOneToTheCountAsync<TRows>(count, pooled, unpooled, afterwards)`, the engines' private method with `await using var connection = connect();` and `if (count % 2 == 0) { await connection.OpenAsync(token); }` for the connection, and `Enumerable.Range(1, count).Select(row).ToArray()` as `expected`.  The list method passes `(rows, connection, token) => ShouldBeTheCallersAloneAsync(rows, count, () => pooled(connection, token))`, and `ShouldBeTheCallersAloneAsync` is the engines' private method over `List<TRow>`, building its expectation with `row`.  The immutable method wraps each delegate with the engines' private `ImmutableRowsNeverDefault(Task<ImmutableArray<TRow>> rows, int count)`, kept private.  Usings: `System`, `System.Collections.Generic`, `System.Collections.Immutable`, `System.Data.Common`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`, `Shouldly`, `Xunit`.

- [ ] **Step 2: Make the two engine classes one-line theories**

Each file adds `using Reads = SqlSource.DatabaseTests.TestHarness.EndToEnd.RowBufferReads<NpgsqlConnection, CountedNumber>;` (SQL Server's over `SqlConnection`), after its other usings as `using OutputKind = …` is placed in `tests/SqlSource.Tests/Generator/`, and `using SqlSource.DatabaseTests.TestHarness.EndToEnd;` is not needed.  Every theory keeps its name, `[Theory]` and `[MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]`, and is an expression body: the four array-and-interface tests call `_reads.ShouldReadOneToTheCountAsync<CountedNumber[]>(count, (connection, token) => Reads.Rows(connection.NumbersAsArray(new(count), cancellationToken: token)), …)` with the `NumbersAsIEnumerable` pair passed without `Rows`, as today; the three list tests call `ShouldReadOneToTheCountIntoTheCallersListAsync` with their two `Reads.Rows(…)` delegates; the immutable test passes `connection.NumbersAsImmutableArray(new(count), cancellationToken: token)` and its twin as they are.

PostgreSQL's class is `public sealed class RowBufferQueryTests : IDisposable` with an explicit constructor taking `PostgresDatabases postgres` that sets `_source = EndToEndSources.App(postgres)` and `_reads = new(_source.CreateConnection, static n => new CountedNumber(n))`, and `public void Dispose() => _source.Dispose();`.  SQL Server's keeps its primary constructor and `private readonly Reads _reads = new(() => EndToEndSources.App(sqlServer), static n => new CountedNumber(n));`.  The class comment keeps its first sentence and the sizes of the counts, and says the body is `RowBufferReads` of the harness.

- [ ] **Step 3: Run the two classes on the default images**

Run: `./format.sh`, then `dotnet test --project tests/DatabaseTests/Postgres --filter-class SqlSource.DatabaseTests.Postgres.EndToEnd.RowBufferQueryTests` and the same for SQL Server.
Expected: 56 rows pass on each engine.

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes, the allocation tests among it, since the harness's `EndToEnd/` changed.

```bash
git add tests/DatabaseTests/TestHarness/EndToEnd/RowBufferReads.cs tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs tests/DatabaseTests/SqlServer/EndToEnd/RowBufferQueryTests.cs
git commit -m "Move the body of RowBufferQueryTests into the harness as RowBufferReads, taking each engine's methods as delegates"
```

---

### Task 5: The documents, TD-0080, the full validation and the floor images

**Files:**
- Modify: `tests/README.md:27,43,115,117`, `tests/DatabaseTests/Postgres/Realistic/AGENTS.md`, `tests/DatabaseTests/SqlServer/Realistic/AGENTS.md`, `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md:50`, `tests/DatabaseTests/SqlServer/README.md:39`, `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md:7-8,69`
- Test: `tests/SqlSource.Tests/Documentation/`

- [ ] **Step 1: `tests/README.md`**

In the harness's row of the libraries' table, after `RowBufferByHand`'s clause, add `RowBufferReads`, the body of both end-to-end `RowBufferQueryTests`, which reads each of the eight collection types of the pooled row buffer through the engine's generated methods at counts on each side of the buffer's sizes.  In the `Realistic/` bullet of `tests/SqlSource.Tool.Testing`, after `RealisticDescribe`, add: and the bodies of the checks both engines' realistic tests run, `RealisticFolderChecks`, `RealisticClaimChecks`, `RealisticDescriptionChecks`, `RealisticNullabilityChecks`, which takes the engine's oracle as a delegate and whose answer is `NullabilityRun`, and `RealisticRefusalChecks`.  In the two "what each test project takes" bullets of `Postgres` and `SqlServer`, add `RowBufferReads` after `RowBufferByHand`.

- [ ] **Step 2: The four `AGENTS.md`**

In each engine's `Realistic/AGENTS.md`, add a sub-bullet after the first ("What both engines' corpora share…"): the bodies of `RealisticCasesTests`, `RealisticClaimsTests`, `RealisticDescriptionTests`, `RealisticNullabilitySafetyTests` and `RealisticModelRefusalTests` are the `Realistic*Checks` of `tests/SqlSource.Tool.Testing/Realistic/`, each test one line that calls its check with the corpus, `RealisticCases.Describe`, the fixture's major and connection string, and the engine's oracle or generator references; a check that differs by engine takes the difference as an argument, never a copy.  In each engine's `EndToEnd/AGENTS.md`, under "Queries and tests", add: `RowBufferQueryTests` is the eight generated methods and their twins handed to `RowBufferReads` of `tests/DatabaseTests/TestHarness`, which holds the body, so a check of what every collection type returns goes there.  Each bullet at most 80 words.

- [ ] **Step 3: The two engine readmes**

In the first paragraph of "The realistic corpus" of each, where it lists what both engines' corpora share in `tests/SqlSource.Tool.Testing/Realistic/`, add "and the bodies of the checks below, which each project's test classes call with its corpus, its server and its oracle" before the clause on its tests.

- [ ] **Step 4: Strike items 1 and 2 of TD-0080**

Wrap the text of items 1 and 2 of "Problem" in `~~…~~`, each followed on its line by: Fixed by piece 4 of the database tests, which moved the bodies into `SqlSource.Tool.Testing/Realistic/` (item 1) and `DatabaseTests/TestHarness/EndToEnd/RowBufferReads.cs` (item 2) and left one-line facts in each engine.  Strike "1. and 2." of "Proposed fix" the same way, as item 9's is.  Nothing else changes.

- [ ] **Step 5: Check the links and the documentation tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Documentation`
Expected: passes; `AgentsFileTests` finds every rule under 80 words and each file under 8 KB.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/README.md tests/DatabaseTests docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md
git commit -m "Say where the bodies of the realistic checks and of RowBufferQueryTests are, and strike items 1 and 2 of TD-0080"
```

- [ ] **Step 7: The full validation and the floor images**

Run: `./pre-commit-validation.sh --full`
Expected: passes.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`
Run: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`
Expected: both pass, the realistic and row buffer classes among them.
