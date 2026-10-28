# Database tests, piece 2: the driver-free TestHarness, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One library, `tests/DatabaseTests/TestHarness`, that every database test project references, with the database parts of `tests/SqlSource.Testing` and two driver-free container base classes in it, each project supplying its driver's factory, its image and its databases by subclassing, and the harness's unit tests in `tests/SqlSource.Tests/TestHarness/`.

**Architecture:** The files move with `git mv` and change namespace; `SqlSource.Testing` drops Testcontainers.  `PostgresContainer` and `SqlServerContainer` become public abstract classes in the harness that take a `DbProviderFactory` and an image through their constructor and ask the subclass for the databases to build once the server's major is known; connection strings are built with `DbConnectionStringBuilder`, every command runs over a `DbDataSource` of the project's factory, and an internal `TestDatabases` holds the steps the two share.  Each describer project declares `PostgresDatabases` or `SqlServerDatabases`, each driver-floor project an `AppContainer`, and the collection definitions name them.

**Tech Stack:** .NET 10 SDK, xunit v3 on Microsoft.Testing.Platform, Testcontainers 4.16.0, System.Data.Common, bash.

**Spec:** `docs/superpowers/specs/2026-10-28-database-tests-folder-design.md`, section "Piece 2: the harness", with "The layout", "The naming pattern" and "Constraints".

## Where the merged code differs from the spec

- Piece 1 merged as the spec planned; `tests/DatabaseTests/Directory.Build.props` gives the harness its assembly name `SqlSource.DatabaseTests.TestHarness` and its root namespace by being there.
- The spec lists `RowBufferTests`' use of `RowBufferByHand` among the tests that live in `tests/SqlSource.Tests/TestHarness/`.  That test, `Append_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsNameModifierAndDepth` of `Generation/Emitting/RowBufferTests.cs`, tests the emitter's `RowBuffer.Append` and reads `RowBufferByHand.cs` as text, so it stays where the mirror rule of `tests/AGENTS.md` puts it and only its path changes.
- `SqlSource.Testing` carried `xunit.v3.assert` for `EngineImage` alone (`Assert.SkipWhen`, `Assert.SkipUnless`); after the move nothing in it calls `Assert`, so the package goes with Testcontainers.  `xunit.v3.extensibility.core` stays for `TestContext`.
- The `type-override` marker of `tests/DatabaseTests/SqlServer/EndToEnd/Queries/AllTypes.sql` names `Level` by its namespace, which changes; the sidecar records the server's `tinyint` and not the C# type, and the hash covers no comment, so no sidecar changes.  `CheckTests` proves it.

## Global Constraints

- The harness is `tests/DatabaseTests/TestHarness/TestHarness.csproj`, assembly and root namespace `SqlSource.DatabaseTests.TestHarness`, both from `tests/DatabaseTests/Directory.Build.props`; its `EndToEnd/` folder is the namespace `SqlSource.DatabaseTests.TestHarness.EndToEnd`.
- The harness references `src/SqlSource` and `tests/SqlSource.Testing`, and its packages are `Testcontainers.PostgreSql`, `Testcontainers.MsSql`, `Shouldly`, `xunit.v3.assert` and `xunit.v3.extensibility.core`: no driver, no Dapper, nothing of the tool, no Roslyn package of its own.
- Its types stay `internal` but `PostgresContainer` and `SqlServerContainer`, which are `public abstract`.  `InternalsVisibleTo`: `SqlSource.DatabaseTests.Postgres`, `SqlSource.DatabaseTests.Postgres.Npgsql8`, `SqlSource.DatabaseTests.SqlServer`, `SqlSource.DatabaseTests.SqlServer.SqlClient61`, `SqlSource.DatabaseTests.QueryMethodAllocation`, `SqlSource.Tests`, `SqlSource.Tool.Tests`, `SqlSource.Tool.Testing`.
- `SqlSource.Testing` keeps `RepositoryRoot`, `RepositoryWorkingFolder`, `TestScopes`, `ProjectIncludes`, `Generator/`, `SqlTexts`, `CultureScope` and `TestSidecars`, stays on the pinned Roslyn with no `VersionOverride`, and references nothing of the tool.
- `PostgresContainer`'s `Major` is read from `server_version_num`; `SqlServerContainer`'s from `SERVERPROPERTY('ProductMajorVersion')`, as today; both before the databases are built.  `ExecuteAsync`, `ScalarAsync`, `ConnectionStringOf`, `SourceOf`, `Major`, `SkipBelow`, `SkipUnlessRecordedMajor` and `ImageBuilds` keep their meaning.
- No change to what any test checks: the describer projects build `fixtures`, `realistic`, `app` and `billing` from the same files in the same order; the driver-floor projects build `app` alone, SQL Server's with `new-types.sql`; SQL Server's `app` takes `new-types.sql` only on a major of 17 or later in the describer project.
- `tools/test-scopes.txt` gets no line for the harness, which falls to `both`.
- Every commit passes `./pre-commit-validation.sh` first, after `./format.sh`; no check is skipped or edited.
- An `AGENTS.md` has one rule per bullet of at most 80 words and is at most 8192 bytes; `tests/AGENTS.md` is at 8153 today, so its edits are the exact texts of Task 4.
- Plans and specs under `docs/superpowers/` are history and are not rewritten.
- Commit messages are one imperative sentence, as the repository's are, and end with the attribution line the session's instructions give.

## Review Focus

- `DbConnectionStringBuilder` must replace the `Database` the Testcontainers string already carries (`Database=master` for SQL Server, `Database=postgres` for PostgreSQL) and not add a second key, or SqlClient opens `master`: pinned by `TestDatabasesTests` in Task 2, and by `ConnectionString_OfTheContainer_OpensTheFixtureDatabase`, which the describer projects keep.
- PostgreSQL's major read from `server_version_num` (`180006` is 18) must agree with the image: pinned by `Major_OfTheRun_IsTheMajorOfTheImagesTag` in Task 2, on both engines.
- A subclass whose `DatabasesOf` names no database would make `ConnectionString` throw an index error from deep in xunit: the harness throws `InvalidOperationException` naming the subclass; pinned by `TestDatabasesTests.DefaultOf_NoDatabase_Throws` in Task 2.
- `pre-commit-validation.sh` must treat a change to the harness as one that reaches both pairs and the allocation tests, or a broken harness ships past a plain run: pinned by `--plan` in Task 3 and the new row of `Of_RealPath_IsDecidedAsIntended`.
- The three scripts read `DefaultImages.cs` by `sed`; a moved file makes each fail at its first line: pinned by running each script's `sed` by hand in Task 3.
- The harness must bring no driver and nothing of the tool, and `SqlSource.Testing` no Testcontainers, or the next change can add one unseen: pinned by `TestLayoutTests` in Task 3.

---

### Task 1: The harness project and the moved files

**Files:**
- Create: `tests/DatabaseTests/TestHarness/TestHarness.csproj`
- Move: `tests/SqlSource.Testing/DefaultImages.cs`, `EngineImage.cs`, `SqlServerStart.cs`, `EndToEndEngine.cs`, `EndToEndFiles.cs` to `tests/DatabaseTests/TestHarness/`; `tests/SqlSource.Testing/EndToEnd/` (seven files) to `tests/DatabaseTests/TestHarness/EndToEnd/`
- Modify: `tests/SqlSource.Testing/SqlSource.Testing.csproj`, `src/SqlSource/SqlSource.csproj:22-29`, `SqlSource.slnx`, `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj`, the four `.csproj` of `tests/DatabaseTests/`, `Directory.Packages.props:61-70`, `.editorconfig:235-243`, `tests/DatabaseTests/SqlServer/EndToEnd/Queries/AllTypes.sql:7,148`, `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs:26-31`, every `.cs` file that imports `SqlSource.Testing` for a moved type
- Test: the solution builds; `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests` pass

**Interfaces:**
- Produces: the namespaces `SqlSource.DatabaseTests.TestHarness` (`DefaultImages`, `EngineImage`, `IStartingContainer`, `Testcontainer<T>`, `SqlServerStart`, `EndToEndEngine`, `EndToEndFiles`) and `SqlSource.DatabaseTests.TestHarness.EndToEnd` (`AllTypesRows`, `SqlServerAllTypesRows`, `ModelReflection`, `ModelShape`, `CommittedEntries`, `CallAllocations`, `RowBufferByHand<T>`, `Level`), with the same members as today.

This task is one commit: between the move and the last reference the solution does not build.

- [ ] **Step 1: Move the files**

```bash
mkdir -p tests/DatabaseTests/TestHarness
for f in DefaultImages EngineImage SqlServerStart EndToEndEngine EndToEndFiles; do git mv tests/SqlSource.Testing/$f.cs tests/DatabaseTests/TestHarness/$f.cs; done
git mv tests/SqlSource.Testing/EndToEnd tests/DatabaseTests/TestHarness/EndToEnd
```

In the twelve moved files, `namespace SqlSource.Testing;` becomes `namespace SqlSource.DatabaseTests.TestHarness;` and `namespace SqlSource.Testing.EndToEnd;` becomes `namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;`.  `EndToEndEngine.cs` adds `using SqlSource.Testing;` for `RepositoryRoot`.  Comments in the moved files that say `tests/SqlSource.Testing` mean this folder now: `DefaultImages.cs` (the two `AppContainer` sentences stay true until Task 2 rewrites them), `RowBufferByHand.cs` ("RowBufferTests of tests/SqlSource.Tests" stays), `Level.cs`.

- [ ] **Step 2: Write `tests/DatabaseTests/TestHarness/TestHarness.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        The harness of the database tests, which every project of tests/DatabaseTests references: the images and the
        start of a container, the files of the end-to-end databases and the driver-free containers.  A library of the
        tests, never a test project of its own.  It brings no database driver, no Dapper and nothing of the tool, so
        the projects that pin the oldest supported driver can reference it; each project brings its own driver.
    -->
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>false</IsTestProject>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Shouldly" />
        <!-- The containers of both engines: Testcontainers brings no database driver. -->
        <PackageReference Include="Testcontainers.MsSql" />
        <PackageReference Include="Testcontainers.PostgreSql" />
        <!-- Assert.SkipWhen and Assert.SkipUnless, for EngineImage. -->
        <PackageReference Include="xunit.v3.assert" />
        <!-- TestContext and IAsyncLifetime, without what makes a project a test executable. -->
        <PackageReference Include="xunit.v3.extensibility.core" />
    </ItemGroup>
    <ItemGroup>
        <!-- The six projects of this folder, QueryMethodAllocation among them before it exists, and the three that read EngineImage or ModelReflection. -->
        <InternalsVisibleTo Include="SqlSource.DatabaseTests.Postgres" />
        <InternalsVisibleTo Include="SqlSource.DatabaseTests.Postgres.Npgsql8" />
        <InternalsVisibleTo Include="SqlSource.DatabaseTests.QueryMethodAllocation" />
        <InternalsVisibleTo Include="SqlSource.DatabaseTests.SqlServer" />
        <InternalsVisibleTo Include="SqlSource.DatabaseTests.SqlServer.SqlClient61" />
        <InternalsVisibleTo Include="SqlSource.Tests" />
        <InternalsVisibleTo Include="SqlSource.Tool.Testing" />
        <InternalsVisibleTo Include="SqlSource.Tool.Tests" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="../../../src/SqlSource/SqlSource.csproj" />
        <ProjectReference Include="../../SqlSource.Testing/SqlSource.Testing.csproj" />
    </ItemGroup>
</Project>
```

Fold the comment line over 120 characters as the other project files do.  Add `<Project Path="tests/DatabaseTests/TestHarness/TestHarness.csproj" />` to the `/tests/DatabaseTests/` folder of `SqlSource.slnx`, last in its sorted list.

- [ ] **Step 3: Trim `SqlSource.Testing.csproj` and widen the `InternalsVisibleTo` lists**

`tests/SqlSource.Testing/SqlSource.Testing.csproj`: remove the `Testcontainers.MsSql` and `xunit.v3.assert` references with their comments; the `xunit.v3.extensibility.core` comment stays; add `<InternalsVisibleTo Include="SqlSource.DatabaseTests.TestHarness" />` in sorted position.  `src/SqlSource/SqlSource.csproj`: add the same item after `SqlSource.DatabaseTests.SqlServer` (for `SidecarReader`, which `CommittedEntries` reads).  `Directory.Packages.props`: the two Testcontainers comments say the harness starts the container (`<!-- Starts the SQL Server of tests/DatabaseTests/TestHarness, which the SQL Server projects describe against.  It needs Docker. -->`, likewise PostgreSQL), and the xunit comment names the three libraries.

- [ ] **Step 4: Reference the harness from the projects that use it**

Add `<ProjectReference Include="../DatabaseTests/TestHarness/TestHarness.csproj" />` to `tests/SqlSource.Tests`, `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tool.Testing`, and `<ProjectReference Include="../TestHarness/TestHarness.csproj" />` to the four projects of `tests/DatabaseTests/`.  The harness brings Testcontainers, so remove `<PackageReference Include="Testcontainers.PostgreSql" />` from `Postgres.csproj` and `Postgres.Npgsql8.csproj` and `<PackageReference Include="Testcontainers.MsSql" />` from `SqlServer.csproj` and `SqlServer.SqlClient61.csproj`.

- [ ] **Step 5: Change the usings, the marker and the paths**

In every `.cs` file under `tests/` that names a moved type, `using SqlSource.Testing.EndToEnd;` becomes `using SqlSource.DatabaseTests.TestHarness.EndToEnd;`, and a file that uses `EngineImage`, `DefaultImages`, `EndToEndFiles`, `EndToEndEngine`, `SqlServerStart`, `IStartingContainer` or `Testcontainer<T>` adds `using SqlSource.DatabaseTests.TestHarness;`, keeping `using SqlSource.Testing;` only where it still uses `RepositoryRoot`, `TestSidecars`, `CultureScope`, `SqlTexts` or the generator harness (IDE0005 fails the build on an unused using).  The files are those `grep -rlE '\b(EngineImage|DefaultImages|EndToEndFiles|EndToEndEngine|SqlServerStart|IStartingContainer|AllTypesRows|SqlServerAllTypesRows|ModelReflection|ModelShape|CommittedEntries|CallAllocations|RowBufferByHand|Level)\b' --include='*.cs' tests` finds outside the harness.  `AllTypes.sql` line 148 becomes `-- type-override: tinyint = SqlSource.DatabaseTests.TestHarness.EndToEnd.Level` and line 7 says "an enum of tests/DatabaseTests/TestHarness".  `RowBufferTests.HandWrittenCopy` becomes `RepositoryRoot.Path("tests", "DatabaseTests", "TestHarness", "EndToEnd", "RowBufferByHand.cs")`.  `.editorconfig`: the section `[tests/SqlSource.Testing/EndToEndFiles.cs]` becomes `[tests/DatabaseTests/TestHarness/EndToEndFiles.cs]`; the `SqlServerContainer.cs` section stays until Task 2 decides it.

- [ ] **Step 6: Restore, build and check the lock files**

Run: `dotnet restore SqlSource.slnx && dotnet build SqlSource.slnx`
Expected: succeeds with no warning.  `git status --short -- '*.lock.json'` lists `tests/DatabaseTests/TestHarness/packages.lock.json` (new) and the lock files of every project that now references the harness or lost a package; read one diff to see that Testcontainers moved and nothing else changed.

- [ ] **Step 7: Run the tests that need no container**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: both pass, `ContainerProjectInputsTests.Refused_RepositoryScopes_IsEmpty`, `TestLayoutTests` and `RowBufferTests` among them.

- [ ] **Step 8: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: the validation passes; it runs both pairs, since `tests/SqlSource.Testing` and the harness are in scope `both`.

```bash
git add -A tests src SqlSource.slnx Directory.Packages.props .editorconfig
git commit -m "Move the database parts of SqlSource.Testing into tests/DatabaseTests/TestHarness"
```

### Task 2: The driver-free containers and the projects' subclasses

**Files:**
- Create: `tests/DatabaseTests/TestHarness/PostgresContainer.cs`, `tests/DatabaseTests/TestHarness/SqlServerContainer.cs`, `tests/DatabaseTests/TestHarness/TestDatabases.cs`, `tests/DatabaseTests/Postgres/PostgresDatabases.cs`, `tests/DatabaseTests/SqlServer/SqlServerDatabases.cs`, `tests/SqlSource.Tests/TestHarness/TestDatabasesTests.cs`
- Delete: `tests/DatabaseTests/Postgres/PostgresContainer.cs`, `tests/DatabaseTests/SqlServer/SqlServerContainer.cs`
- Rewrite: `tests/DatabaseTests/Postgres.Npgsql8/AppContainer.cs`, `tests/DatabaseTests/SqlServer.SqlClient61/AppContainer.cs`
- Rename: `tests/DatabaseTests/Postgres/PostgresContainerTests.cs` to `PostgresDatabasesTests.cs`, `tests/DatabaseTests/SqlServer/SqlServerContainerTests.cs` to `SqlServerDatabasesTests.cs`
- Modify: `tests/DatabaseTests/TestHarness/EndToEndFiles.cs`, `tests/DatabaseTests/TestHarness/DefaultImages.cs:7-9,15-17`, `.editorconfig:240-243`, every file of `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer` that names its container type
- Test: `tests/DatabaseTests/Postgres`, `Postgres.Npgsql8`, `SqlServer`, `SqlServer.SqlClient61`; `tests/SqlSource.Tests`

**Interfaces:**
- Consumes: Task 1's namespaces.
- Produces, in `SqlSource.DatabaseTests.TestHarness`:

```csharp
public abstract class PostgresContainer(DbProviderFactory factory, string image) : IAsyncLifetime
{
    // The databases to build on a server of that major, in order: each its name and the scripts to run on it, in
    // order.  The first is the container's default database, the one ConnectionString, ExecuteAsync and
    // ScalarAsync use.  Called once, when the container has started.
    protected abstract IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major);

    public int Major { get; }                                // read before DatabasesOf is called
    public string ConnectionString { get; }                  // of the default database
    public string ConnectionStringOf(string database);
    public DbDataSource SourceOf(string database);           // the caller disposes it
    public Task ExecuteAsync(string sql);                    // on the default database
    public Task<object?> ScalarAsync(string sql);
    public void SkipBelow(int major, string reason);
    public void SkipUnlessRecordedMajor(string what);
    public ValueTask InitializeAsync();
    public ValueTask DisposeAsync();
}

public abstract class SqlServerContainer(DbProviderFactory factory, string image) : IAsyncLifetime
{
    // The same members, and:
    public static IReadOnlyDictionary<string, string> ImageBuilds { get; }   // as today
}

internal static class TestDatabases
{
    // The template with its Database replaced: DbConnectionStringBuilder, which both drivers read, SqlClient as
    // a synonym of Initial Catalog.
    public static string ConnectionStringOf(string template, string database);
    // The first database's name, or an InvalidOperationException that names the container type when there is none.
    public static string DefaultOf(IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> databases, Type container);
    public static Task ExecuteAsync(DbDataSource source, string sql, CancellationToken token);
    public static Task<object?> ScalarAsync(DbDataSource source, string sql, CancellationToken token);
    // Creates each database and runs its scripts, through EndToEndFiles.CreateAsync, in order.
    public static Task BuildAsync(DbDataSource server, EndToEndEngine engine, IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> databases, Func<string, DbDataSource> sourceOf, CancellationToken token);
}
```

  and in `EndToEndFiles`: `public static IReadOnlyList<string> SchemaAndSeed(EndToEndEngine engine, string database)`, the `schema.sql` then the `seed.sql` of the database's folder, replacing the `CreateAsync` overload without a file list, whose only callers were the containers.  `RunAsync` becomes private if `CreateAsync` is its last caller.

- Produces, in the projects: `SqlSource.DatabaseTests.Postgres.PostgresDatabases : PostgresContainer` with the constants `Database`, `RealisticDatabase`, `AppDatabase`, `BillingDatabase`, `EndToEndDatabases` and `NoOldAndNewInReturning` that `PostgresContainer` of that project had; `SqlSource.DatabaseTests.SqlServer.SqlServerDatabases : SqlServerContainer` with `Database`, `RealisticDatabase`, `AppDatabase`, `BillingDatabase` and `EndToEndDatabases`; `SqlSource.DatabaseTests.Postgres.Npgsql8.AppContainer : PostgresContainer` and `SqlSource.DatabaseTests.SqlServer.SqlClient61.AppContainer : SqlServerContainer`, each with `ConnectionString` of `app` as today.

- [ ] **Step 1: Write the failing unit tests of `TestDatabases` in `tests/SqlSource.Tests/TestHarness/TestDatabasesTests.cs`**

Namespace `SqlSource.Tests.TestHarness`.  The folder is new; Task 3 fills it.

```csharp
[Theory]
[InlineData("Server=127.0.0.1,1433;Database=master;User Id=sa;Password=p;TrustServerCertificate=True", "fixtures")]
[InlineData("Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=p", "app")]
public void ConnectionStringOf_TemplateWithADatabase_ReplacesItAndKeepsTheRest(string template, string database)
{
    var built = new DbConnectionStringBuilder { ConnectionString = TestDatabases.ConnectionStringOf(template, database) };
    var expected = new DbConnectionStringBuilder { ConnectionString = template };

    built["Database"].ShouldBe(database);
    built.Count.ShouldBe(expected.Count);
    built.Keys.Cast<string>().Count(key => key.Equals("Database", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
}

[Fact]
public void DefaultOf_Databases_IsTheFirstName() =>
    TestDatabases.DefaultOf([("fixtures", []), ("app", [])], typeof(TestDatabasesTests)).ShouldBe("fixtures");

[Fact]
public void DefaultOf_NoDatabase_ThrowsNamingTheContainer() =>
    Should
        .Throw<InvalidOperationException>(() => TestDatabases.DefaultOf([], typeof(TestDatabasesTests)))
        .Message.ShouldContain(nameof(TestDatabasesTests));
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.TestHarness.TestDatabasesTests`
Expected: FAIL to compile, `TestDatabases` does not exist.

- [ ] **Step 3: Write `TestDatabases` and `EndToEndFiles.SchemaAndSeed`**

`TestDatabases.cs` in the harness, with the signatures above.  `ExecuteAsync` and `ScalarAsync` make the command with `source.CreateCommand(sql)`, as the PostgreSQL container does today, which CA2100 does not report; the comment says the statement is a test's own and never a user's.  `BuildAsync` is the loop both containers had.

- [ ] **Step 4: Write the two abstract containers**

`PostgresContainer.cs`: a private `PostgreSqlContainer? _container` set by `InitializeAsync` through `EngineImage.Postgres.StartAsync(new PostgreSqlBuilder(image).Build, token)`, no `WithDatabase`, so the container's own database is `postgres` and every database of the subclass, `fixtures` included, is made by `CREATE DATABASE`.  `InitializeAsync` then, in a `try` that disposes the container and rethrows: makes a server source with `factory.CreateDataSource(_container.GetConnectionString())`, reads `Major` as `Convert.ToInt32(await TestDatabases.ScalarAsync(server, "SELECT current_setting('server_version_num')::int", token), CultureInfo.InvariantCulture) / 10000`, stores `DatabasesOf(Major)` in a field, sets the default database with `TestDatabases.DefaultOf(databases, GetType())`, and calls `TestDatabases.BuildAsync(server, EndToEndEngine.Postgres, databases, SourceOf, token)`.  `ConnectionStringOf(database)` is `TestDatabases.ConnectionStringOf(Container.GetConnectionString(), database)`; `SourceOf` is `factory.CreateDataSource(ConnectionStringOf(database))`; `SkipBelow` and `SkipUnlessRecordedMajor` call `EngineImage` as today; `Container` throws "The container has not started." before the start, as today.  The class comment: one server for every test of the project's collection, the project's subclass supplying its driver's factory, its image and its databases, Testcontainers removing the container when the tests end and its reaper when the process is killed.

`SqlServerContainer.cs`: the same over `MsSqlContainer` and `MsSqlBuilder(image)`, `EngineImage.SqlServer` (which starts through `SqlServerStart`), `EndToEndEngine.SqlServer`, the major from `SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)`, and `ImageBuilds` moved whole with its comment, the path in it now `tests/DatabaseTests/SqlServer/README.md` still.  Its comment on `ConnectionStringOf` keeps "The container's own string trusts the server's certificate."

Then build the harness alone: `dotnet build tests/DatabaseTests/TestHarness`.  If CA2100 is reported, add the `.editorconfig` section `[tests/DatabaseTests/TestHarness/TestDatabases.cs]` with the comment of the old `SqlServerContainer.cs` section; otherwise delete the `[tests/DatabaseTests/SqlServer/SqlServerContainer.cs]` section, since nothing it covered remains.

- [ ] **Step 5: Run the unit tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.TestHarness.TestDatabasesTests`
Expected: PASS.

- [ ] **Step 6: Write the four subclasses and delete the old containers**

`tests/DatabaseTests/Postgres/PostgresDatabases.cs`:

```csharp
public sealed class PostgresDatabases() : PostgresContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Image)
{
    // The constants of the old PostgresContainer, with their comments.
    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
    [
        (Database, [RepositoryPaths.PostgresSchema]),
        (RealisticDatabase, [RealisticCases.Corpus.Schema, RealisticCases.Corpus.Seed]),
        (AppDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, AppDatabase)),
        (BillingDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, BillingDatabase)),
    ];
}
```

with the comment on the seed's reliance on the domain `NOT NULL` hole kept beside the realistic entry, and `RealisticConnectionString` kept as `ConnectionStringOf(RealisticDatabase)`.  `tests/DatabaseTests/SqlServer/SqlServerDatabases.cs` likewise over `SqlClientFactory.Instance` and `EngineImage.SqlServer.Image`, with `[RepositoryPaths.SqlServerSchema]` for `fixtures` and `app` taking `[.. EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, AppDatabase), RepositoryPaths.NewTypesSchema]` when `major >= 17`, with the comment that the table of the types of SQL Server 2025 is made on a major of 17 or later, in the order `tools/describe-end-to-end.sh` applies them.  `git rm` the two old container files.

`tests/DatabaseTests/Postgres.Npgsql8/AppContainer.cs`: `public sealed class AppContainer() : PostgresContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Default)`, `DatabasesOf` returning `[("app", EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, "app"))]`, its comment as today (the default image, ignores the variable).  `tests/DatabaseTests/SqlServer.SqlClient61/AppContainer.cs`: over `SqlClientFactory.Instance` and `EngineImage.SqlServer.Default`, `app` with `[.. SchemaAndSeed, Path.Combine(EndToEndEngine.SqlServer.Folder, "app", "new-types.sql")]` unconditionally, with today's comment that the default image is SQL Server 2025, which has json and vector.  Neither keeps a field or an `InitializeAsync` of its own.

Confirm the factories build: `NpgsqlFactory.Instance.CreateDataSource` exists on Npgsql 8.0.9 and `SqlClientFactory.Instance.CreateDataSource` on Microsoft.Data.SqlClient 6.1.7, which the old `AppContainer` of SqlClient 6.1 already called.  If the Npgsql 8 build fails on it, say so in the hand-back: the fallback is a `Func<string, DbDataSource>` in place of the factory, which changes the protected surface and so is the plan owner's call.

- [ ] **Step 7: Rename the container type in both describer projects**

In `tests/DatabaseTests/Postgres`, every `PostgresContainer` outside the harness becomes `PostgresDatabases` (constructor parameters, `UsesPostgres`, `MeasuresAllocations`, `RealisticCases`, `EndToEndSources`, the 34 `PostgresContainer.` member reads).  In `tests/DatabaseTests/SqlServer`, every `SqlServerContainer` becomes `SqlServerDatabases` except the five `SqlServerContainer.ImageBuilds` reads (`FixtureVersionTests`, `SessionTests`, three in the container tests), which stay on the harness class.  The comments of `UsesPostgres`, `UsesSqlServer` and the two `MeasuresAllocations` name the new types.  `git mv` `PostgresContainerTests.cs` to `PostgresDatabasesTests.cs` and `SqlServerContainerTests.cs` to `SqlServerDatabasesTests.cs`, renaming the classes.  `DefaultImages.cs` lines 7-9 and 15-17: "AppContainer of tests/DatabaseTests/Postgres.Npgsql8 builds on it" becomes "the AppContainer of tests/DatabaseTests/Postgres.Npgsql8 runs on it through EngineImage.Postgres.Default", likewise SQL Server.

- [ ] **Step 8: Add the test of `Major` to each container test class**

In `PostgresDatabasesTests`:

```csharp
// server_version_num is 180006 on 18.6: the major is the tag's first number.
[Fact]
public void Major_OfTheRun_IsTheMajorOfTheImagesTag() =>
    postgres.Major.ShouldBe(int.Parse(EngineImage.TagOf(EngineImage.Postgres.Image).Split('.')[0], CultureInfo.InvariantCulture));
```

In `SqlServerDatabasesTests`, the same against the first number of `SqlServerContainer.ImageBuilds[EngineImage.SqlServer.Image]` (`17.0.5015.4` is 17).

- [ ] **Step 9: Build and run the four projects**

Run: `dotnet build SqlSource.slnx`, then `dotnet test --project tests/DatabaseTests/Postgres`, `Postgres.Npgsql8`, `SqlServer`, `SqlServer.SqlClient61`
Expected: the build has no warning; each project passes, both `CheckTests` among them (no sidecar changed), and the container tests' `ConnectionString_OfTheContainer_Opens…` and `InitializeAsync_…Seeded` tests, which prove the databases are the same.

- [ ] **Step 10: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: passes.

```bash
git add -A tests .editorconfig
git commit -m "Make the containers driver-free in the harness, each project subclassing them with its driver and databases"
```

### Task 3: The harness's unit tests, the layout checks and the script

**Files:**
- Move: `tests/SqlSource.Tests/EngineImageTests.cs`, `tests/SqlSource.Tests/ModelReflectionTests.cs`, `tests/DatabaseTests/SqlServer/SqlServerStartTests.cs`, `tests/DatabaseTests/SqlServer/EndToEndFilesTests.cs` to `tests/SqlSource.Tests/TestHarness/`
- Create: `tests/SqlSource.Tests/TestHarness/FakeStartingContainer.cs`
- Modify: `tests/SqlSource.Tests/TestLayoutTests.cs`, `tests/SqlSource.Tests/Validation/TestScopesFileTests.cs:43-46`, `pre-commit-validation.sh:114`, `tools/describe-end-to-end.sh:24`, `tools/record-fixtures.sh:11,19`, `tools/pull-test-images.sh:5,11`, `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md` (item 9's two lines)
- Test: `tests/SqlSource.Tests`

**Interfaces:**
- Produces: `SqlSource.Tests.TestHarness.FakeStartingContainer : IStartingContainer`, the superset fake of the old `SqlServerStartTests`: `Exception? Failure { get; set; }`, `Exception? LogFailure { get; set; }`, `(string Stdout, string Stderr) Logs { get; init; }`, `int Starts`, `int LogReads`, `int Disposals`, `string Image { get; init; } = "fake:1"`, `TestcontainersStates State { get; init; } = Running`, and a constructor `FakeStartingContainer(Exception? failure = null)`.

- [ ] **Step 1: Move the four test files and write the one fake**

`git mv` each to `tests/SqlSource.Tests/TestHarness/`; their namespace becomes `SqlSource.Tests.TestHarness`.  Write `FakeStartingContainer.cs` from the fake of `SqlServerStartTests`, with the comment that it is the one fake of `IStartingContainer` for the tests of `EngineImage` and `SqlServerStart` (TD-0080 item 9).  Delete both nested fakes: `EngineImageTests` uses `new FakeStartingContainer(new InvalidOperationException("It did not start."))` and `container.Starts.ShouldBe(1)` in place of `Started.ShouldBeTrue()`; `SqlServerStartTests` sets `Image = ImageName` where it builds a fake, so its report assertions hold.  `ModelReflectionTests.ModelNamed_TypeThatIsNotThere_IsNull` passes `"SqlSource.Tests.TestHarness.NotThere"`.

- [ ] **Step 2: Run the moved tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.TestHarness`
Expected: PASS, every test of the five classes.

- [ ] **Step 3: Write the failing layout tests in `TestLayoutTests.cs`**

Two facts, with the comment that the harness is referenced by the projects that pin the oldest supported driver, so it brings no driver, no Dapper and nothing of the tool, and that `SqlSource.Testing` is referenced by RoslynFloor, so it brings no Testcontainers, which the harness carries for it.  `TestHarness_References_BringNoDriverAndNothingOfTheTool` reads `tests/DatabaseTests/TestHarness/TestHarness.csproj` with `XDocument`, asserts its `PackageReference` `Include`s are exactly `["Shouldly", "Testcontainers.MsSql", "Testcontainers.PostgreSql", "xunit.v3.assert", "xunit.v3.extensibility.core"]` (ignoring order) and its `ProjectReference` `Include`s, by file name, exactly `["SqlSource.csproj", "SqlSource.Testing.csproj"]`.  `SqlSourceTesting_PackageReferences_BringNoTestcontainers` asserts no `PackageReference` of `tests/SqlSource.Testing/SqlSource.Testing.csproj` starts with `Testcontainers.`.  Both use `RepositoryRoot.Path(...)`.  Also add to `Of_RealPath_IsDecidedAsIntended` of `TestScopesFileTests`: `[InlineData("tests/DatabaseTests/TestHarness/EngineImage.cs", nameof(TestScope.Both))]`.

- [ ] **Step 4: Run them**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.TestLayoutTests` and `--filter-class SqlSource.Tests.Validation.TestScopesFileTests`
Expected: PASS (the tree already satisfies them; the tests pin it).

- [ ] **Step 5: The script and the tools**

`pre-commit-validation.sh` line 114: `src/SqlSource/* | tests/SqlSource.Testing/* | tests/DatabaseTests/TestHarness/* | tests/DatabaseTests/*/EndToEnd/*) allocation=true ;;`.  Then run `./pre-commit-validation.sh --plan tests/DatabaseTests/TestHarness/EngineImage.cs` and see both pairs and the allocation tests marked to run.  In `tools/describe-end-to-end.sh`, `tools/record-fixtures.sh` and `tools/pull-test-images.sh`, `IMAGES='tests/DatabaseTests/TestHarness/DefaultImages.cs'`, and the comments at `record-fixtures.sh:19` and `pull-test-images.sh:5` name the new path.  Then:

```bash
sed -n 's/.*const string Postgres = "\(.*\)";.*/\1/p' tests/DatabaseTests/TestHarness/DefaultImages.cs
sed -n 's/.*const string SqlServer = "\(.*\)";.*/\1/p' tests/DatabaseTests/TestHarness/DefaultImages.cs
```

Expected: `postgres:18.6` and `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`.  Run `tools/shellcheck.sh` and `tools/shfmt.sh --check`.

- [ ] **Step 6: Strike item 9 of TD-0080**

Item 9 of the problem list becomes: `9. ~~**\`IStartingContainer\` is faked twice**, in \`SqlSource.Tests/EngineImageTests.cs\` and \`DatabaseTests/SqlServer/SqlServerStartTests.cs\`, the second a superset of the first.~~  Fixed by piece 2 of the database tests, which moved both tests to \`SqlSource.Tests/TestHarness/\` with one \`FakeStartingContainer\` beside them, since \`SqlSource.Testing\`, the home proposed below, no longer has Testcontainers.`  Line 9 of the proposed fix becomes `9. ~~One \`FakeStartingContainer\` in \`SqlSource.Testing\`.~~  Done, beside the two tests in \`SqlSource.Tests/TestHarness/\`.`  Nothing else in the file changes.

- [ ] **Step 7: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: passes (full, since the script changed).

```bash
git add -A tests pre-commit-validation.sh tools docs/tech-debt
git commit -m "Gather the harness's unit tests in tests/SqlSource.Tests/TestHarness with one fake of IStartingContainer"
```

### Task 4: The documents

**Files:**
- Modify: `tests/README.md`, `tests/AGENTS.md`, `tests/DatabaseTests/README.md`, `tests/DatabaseTests/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/Postgres/AGENTS.md`, `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer/README.md`, `tests/DatabaseTests/SqlServer/AGENTS.md`, `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`, `AGENTS.md:29,31`, `CONTRIBUTING.md:27,36`, `tools/AGENTS.md:12`, `tools/README.md:89,93`, `docs/tech-debt/TD-0072-sql-server-2025-container-crashes-at-start-in-ci.md:7`
- Test: `tests/SqlSource.Tests/Documentation/`

- [ ] **Step 1: `tests/README.md`**

The opening says "seven test projects and three libraries".  In the libraries table, the `tests/SqlSource.Testing` row loses everything from "`DefaultImages`, the default image of each engine" through "`RowBufferByHand`, …holds to the generated text" and keeps the rest; a new row `tests/DatabaseTests/TestHarness` | A current version, through `src/SqlSource` alone; it brings no Roslyn package | holds those sentences, rewritten as what the harness holds: `DefaultImages`, `EngineImage`, `SqlServerStart` with `IStartingContainer`, `EndToEndEngine`, `EndToEndFiles`, the two abstract containers `PostgresContainer` and `SqlServerContainer`, which a project subclasses with its driver's `DbProviderFactory`, its image and its databases, `TestDatabases`, their shared steps, and in `EndToEnd/` the seven helpers.  Below the table, the paragraph "The two libraries are apart because…" becomes: the three libraries are apart because each is referenced by a project that cannot take what the others bring: `SqlSource.Testing` by RoslynFloor, so it stays on the pinned Roslyn and references nothing of the tool; `TestHarness` by the driver-floor projects, so it brings no database driver, no Dapper and nothing of the tool; `Tool.Testing` references the tool.  Their types stay `internal` but `SettingLevel` and the harness's two containers, which a project's public fixture subclasses.  The mermaid graph gains `Harness["tests/DatabaseTests/TestHarness"]` with `Tests --> Harness`, `ToolTests --> Harness`, `ToolTesting --> Harness`, `Postgres --> Harness`, `Npgsql8 --> Harness`, `SqlServer --> Harness`, `SqlClient61 --> Harness`, `Harness --> Generator`, `Harness --> Testing`.  In "What each test project takes from the libraries", each bullet names the harness for the types it now holds (`tests/SqlSource.Tests` for `TestHarness/`; `tests/SqlSource.Tool.Tests` for `EngineImage`; the four database projects as the spec's piece 2 lists).  "The database projects" section says the four reference the harness.

- [ ] **Step 2: `tests/AGENTS.md`, under 8192 bytes**

Replace these rules with these texts, word for word:

- Line 8: `- **The three libraries, \`tests/SqlSource.Testing\`, \`tests/DatabaseTests/TestHarness\` and \`tests/SqlSource.Tool.Testing\`, hold no tests.**  A helper that one test project alone uses stays in that project; one that a second project comes to need moves into a library.`
- Line 9: `- **A library references only what every project that references it can take** ([why](README.md#the-projects)): \`SqlSource.Testing\` stays on the pinned Roslyn with no \`VersionOverride\` and references nothing of the tool, \`TestHarness\` brings no database driver and nothing of the tool, and \`Tool.Testing\` references the tool.`
- Line 11: `- **The libraries' types stay \`internal\`**, except \`SettingLevel\` of \`tests/SqlSource.Testing\`, which a public theory takes as its row, and the harness's \`PostgresContainer\` and \`SqlServerContainer\`, which each project subclasses as its public fixture.`
- Line 12: `- **A test reads a generated model by reflection through \`ModelReflection\` of \`tests/DatabaseTests/TestHarness\`**, which orders the properties as they are declared, since \`Type.GetProperties()\` does not promise that order.`
- Line 39: the link becomes `[why](DatabaseTests/TestHarness/EndToEnd/CallAllocations.cs)`.

Run `wc -c tests/AGENTS.md`: under 8192.  If not, shorten line 9 by dropping "with no `VersionOverride`", which `README.md#the-projects` says.

- [ ] **Step 3: `tests/DatabaseTests/README.md` and `tests/DatabaseTests/AGENTS.md`**

Readme: the intro's `DefaultImages.cs` link becomes `TestHarness/DefaultImages.cs`, and "the two libraries they all share" becomes "the three libraries"; a new section `## The harness` after "The projects": `TestHarness/` is the library of this folder, `SqlSource.DatabaseTests.TestHarness`, which every project here references; it holds the images, the start of a container, the files of the end-to-end databases and the two driver-free containers, `PostgresContainer` and `SqlServerContainer`, which a project subclasses, giving its driver's `DbProviderFactory`, its image and the databases to build with their scripts in order, the first being the one `ConnectionString`, `ExecuteAsync` and `ScalarAsync` use; it brings no driver, so the two driver-floor projects reference it too; its unit tests are in `tests/SqlSource.Tests/TestHarness/`, since they need no Docker.  The two driver-floor sections' "Its container, `AppContainer`" sentences say it is a subclass of the harness's container.

AGENTS.md: the second Names rule says "exactly the test projects of this folder; `TestHarness`, a library, has none".  The first Containers rule becomes: `- **A test that needs the server is in the project's collection** (\`UsesPostgres\`, \`UsesSqlServer\` or \`UsesApp\`, and \`MeasuresAllocations\` for the two \`AllocationTests\`), whose fixture is the project's public sealed subclass of the harness's \`PostgresContainer\` or \`SqlServerContainer\`, started once for the run through \`EngineImage\`; the project gives it its driver's \`DbProviderFactory\`, its image and its databases with their scripts, and the harness brings no driver.`  References gains: `- **\`TestHarness\` references \`src/SqlSource\` and \`tests/SqlSource.Testing\` alone**, with no driver, no Dapper and nothing of the tool, so every project here, the driver-floor ones included, references it; a helper that needs a driver stays in the project.`  Under 8192 bytes.

- [ ] **Step 4: The per-project documents**

`Postgres/AGENTS.md`: rule 1 names `PostgresDatabases.cs`, a subclass of the harness's `PostgresContainer`; rule 2 "`EngineImage.Postgres.Image` of `tests/DatabaseTests/TestHarness`"; rule 3 drops "`AppContainer` … starts its own so too" and says the harness starts every container so; rule 4 "`EndToEndFiles` of the harness"; the Versions rule "`PostgresDatabases.Major`".  `Postgres/EndToEnd/AGENTS.md` lines 21 and 31: `tests/DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs`, "`CallAllocations` of `tests/DatabaseTests/TestHarness`", link `../../TestHarness/EndToEnd/CallAllocations.cs`.  `Postgres/README.md`: line 19 the `DefaultImages.cs` path; line 25 `PostgresDatabasesTests`; lines 124 and 127 "of `tests/DatabaseTests/TestHarness`" and its links `../TestHarness/EndToEnd/...`.  `SqlServer/AGENTS.md`: rule 1 `SqlServerDatabases.cs`; rule 2 the harness; rule 4 as PostgreSQL's; Databases rule "`EndToEndFiles` of the harness"; Versions "`SqlServerDatabases.Major`".  `SqlServer/EndToEnd/AGENTS.md` lines 25 and 35 likewise.  `SqlServer/README.md`: line 7 drops `EndToEndFilesTests` and `SqlServerStartTests` from the tests that need no server and says `EndToEndFiles` is the harness's; line 25 "`SqlServerStart` of the harness … `SqlServerStartTests` of `tests/SqlSource.Tests/TestHarness/` proves that"; lines 29, 31 the path and `SqlServerDatabasesTests`; line 130 "`Level` of the harness"; lines 156, 159 the harness and its links.  Every relative link to the harness from a project is `../TestHarness/...`, from `EndToEnd/` `../../TestHarness/...`.

- [ ] **Step 5: The other documents**

Root `AGENTS.md` line 29: `SqlServerContainer.ImageBuilds` in `tests/DatabaseTests/TestHarness/SqlServerContainer.cs`; line 31: `tests/DatabaseTests/TestHarness/DefaultImages.cs`.  `CONTRIBUTING.md` line 27: "and the three libraries they share"; line 36: `tests/SqlSource.Testing`, `tests/DatabaseTests/TestHarness` and `tests/SqlSource.Tool.Testing` are the libraries.  `tools/AGENTS.md` line 12: the new path, and the last sentence: "The `AppContainer` of `tests/DatabaseTests/Postgres.Npgsql8` and of `tests/DatabaseTests/SqlServer.SqlClient61` runs on the constant through `EngineImage` and reads no text."  `tools/README.md` lines 89 and 93: the new path.  `TD-0072` line 7: the links `../../tests/DatabaseTests/TestHarness/SqlServerStart.cs` and `../../tests/DatabaseTests/TestHarness/SqlServerContainer.cs`, and the sentence says the harness's `SqlServerContainer`, which both SQL Server projects subclass, starts its container through it; drop the `AppContainer` link.

- [ ] **Step 6: Check the links and the limits**

```bash
for f in $(git diff --name-only HEAD -- '*.md'); do d=$(dirname "$f"); grep -o '](\.[^)#]*' "$f" | sed 's/^](//' | while read -r l; do [ -e "$d/$l" ] || echo "$f -> $l"; done; done
grep -rn "SqlSource.Testing/DefaultImages\|SqlSource.Testing/EngineImage\|SqlSource.Testing/SqlServerStart\|SqlSource.Testing/EndToEnd\|PostgresContainer.cs\|SqlServerContainerTests\|PostgresContainerTests" --include='*.md' --include='*.sh' --include='*.cs' --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . | grep -v "^./docs/superpowers"
```

Expected: no output from either.  Then `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Documentation`: PASS.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add AGENTS.md CONTRIBUTING.md tests tools docs/tech-debt
git commit -m "Describe the three test libraries and point every document at the harness"
```

### Task 5: Full validation, the floor images, the version and the review

Run by the plan owner, not an implementer.

- [ ] **Step 1:** `./pre-commit-validation.sh --full`: passes.
- [ ] **Step 2:** `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`: both pass; the SQL Server run proves `app` without `new-types.sql` on major 14.
- [ ] **Step 3:** `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`: no tag means nothing to compare with `VersionPrefix`.
- [ ] **Step 4:** `/branch-review:review --base origin/main`, to convergence.
- [ ] **Step 5:** `harness-notes.md` in the shared notes folder: the harness's folder and namespaces, the protected surface, the subclass names, the `InternalsVisibleTo` list, `EndToEndFiles.SchemaAndSeed`, `FakeStartingContainer`, and that `tests/AGENTS.md` is within 40 bytes of its limit.
