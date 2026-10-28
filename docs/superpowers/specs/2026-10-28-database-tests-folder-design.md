# The `tests/DatabaseTests` folder: design

## Intent

Every test project that needs Docker, the four that run against a real PostgreSQL or SQL Server, moves under one folder, `tests/DatabaseTests/`, with a readme and an `AGENTS.md` of its own that say what those tests are for and the patterns they follow.  The move is the occasion to give them one shared harness, to gather the two engines' allocation tests into one project that runs serially and only in the nightly workflow, and to take the items of [TD-0080](../../tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md) that are about these projects.

The allocation tests change purpose.  Today they gate every commit, every push and every CI run, and they have been the flakiest tests of the repository.  After this they are a nightly signal: a red nightly job says that a generated method may allocate more than it did, and someone reviews it.

## Goals

- One folder, `tests/DatabaseTests/`, holds every project that starts a container, and nothing else under `tests/` does.
- Each project's assembly and root namespace carries the `SqlSource.DatabaseTests.` prefix while its folder and project file keep the short name, and the pattern is written down once, in the folder's `AGENTS.md`, and enforced by MSBuild rather than by hand.
- One shared library, `TestHarness`, that every database project can reference, the driver-floor projects included: it brings no database driver and nothing of the tool.
- One project, `QueryMethodAllocation`, holds every allocation test of a generated method, on both engines, with one test body and one naming scheme, and runs one test at a time.
- The allocation tests run only in new Linux jobs of `nightly.yml`, one for each engine, and a regression fails the job.  `pre-commit-validation.sh`, with or without `--full`, and `build.yml` run none of them.
- The items of TD-0080 that are about the database projects are fixed: 1, 2, 3, 7, 9, 15 and 18.
- The tree is green after every pull request: every check of `pre-commit-validation.sh --full` passes, and the describer projects pass on their floor images too.

## Non-goals

- No change to what any test checks, beyond the allocation tests' consolidation.  The margins, warm-up counts, pair counts and `Least*Saving` constants stay as they are measured today.
- No change to `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor`, `tests/SqlSource.Tool.Tests` or `tests/SqlSource.Tool.Testing` beyond what the move forces: updated references, and the harness's unit tests, which move into `tests/SqlSource.Tests`.
- No change to the images, their pins or the version matrices.
- Items of TD-0080 that are not about the database projects (4, 5, 6, 8, 10 to 14, 16, 17, 19) stay open.
- No reporting of allocation numbers beyond what a failing assertion's message and the test summary already say.

## Anti-goals

- A project under `tests/DatabaseTests/` that cannot reference the harness because the harness brought a driver it cannot take.
- A rename that `pre-commit-validation.sh`, `tools/test-scopes.txt` or `RepositoryRoot` does not know, so that a container project runs unchecked or is skipped on a change that reaches it.
- An allocation test that runs by accident in `dotnet test --solution SqlSource.slnx`, in the pre-commit script or in CI, or one whose project no longer compiles because nothing builds it.
- Two allocation tests running at once, or two containers measured in one process at once.

## Constraints

- **The driver pins.**  `Postgres.Npgsql8` pins Npgsql 8.0.9 and `SqlServer.SqlClient61` pins Microsoft.Data.SqlClient 6.1.7 with `VersionOverride`; a reference to anything that brings Npgsql 10 or SqlClient 7 is a package downgrade and fails the restore.  `tests/SqlSource.Tool.Testing` references the tool, which brings both, so it cannot be the shared harness and stays where it is.
- **Roslyn.**  `tests/SqlSource.Tests.RoslynFloor` references `tests/SqlSource.Testing`, which must stay on the pinned Roslyn with no `VersionOverride`.  The harness references `SqlSource.Testing` for `RepositoryRoot` and brings no Roslyn package of its own.
- **Project identity.**  `RepositoryRoot.RunningProject` is the name of the entry assembly, and `TestScopes.Projects` keys the container projects by the file name of the `.csproj` without its extension.  After the move the two differ (`Postgres.csproj`, assembly `SqlSource.DatabaseTests.Postgres`), so `TestScopes` applies the same prefix, and `RunningProjectTests` in each project keeps catching a mismatch.
- **The scopes.**  `tools/test-scopes.txt` decides which container projects a change can reach, and `ContainerProjectInputsTests` and `RepositoryRoot.Path` refuse a read or a build input that would let a change skip the project that depends on it.  Every moved path gets its line, and the harness, which every database project depends on, is in scope `both`, which is what an unmatched path gets.
- **Generated code is per project.**  A generated method exists only in the project that claims its `.sql` file, so `QueryMethodAllocation` links the query files and sidecars it measures and claims them itself, as the two driver-floor projects already link `AllTypes.sql` and `Streaming.sql`.  A linked query takes the settings files of the folder it is in, the end-to-end project's.
- **xunit fixtures have no parameters, and are public.**  A collection fixture is constructed by xunit with no arguments and is taken by a public constructor of every test class, so a project hands the harness its driver by subclassing the harness's container, not by constructing it, and the harness's two abstract containers are `public`: the second exception, after `SettingLevel`, to the rule that the libraries' types stay `internal`, which `tests/AGENTS.md` records.
- **The nightly summary holds 1 MiB** and lists failed and skipped tests, so an allocation job reports as the other nightly jobs do, with `--report-github --report-github-summary-include-passed false`.
- **The rules of the root `AGENTS.md`**: every pull request passes `./pre-commit-validation.sh --full` before it is pushed, the describer projects also run on their floor images after a change to them, the version is checked, `branch-review:review` runs when the work is complete, and every document that a change makes wrong is updated in the same pull request.

## Approaches

### The harness

Two ways were considered, after dropping a single library that references both drivers, which the driver-floor projects cannot take.

1. **One driver-free `TestHarness`, containers included.**  The database parts of `tests/SqlSource.Testing` and both container classes move into it, and the containers become driver-free: the project supplies a `DbProviderFactory` and the databases to build, and the harness builds connection strings with `DbConnectionStringBuilder` and runs scripts over `DbDataSource`, as `EndToEndFiles` already does.  Every database project references one library and brings its own driver.  Costs a rewrite of the two containers' internals.
2. **A driver-free core plus a driver-bound `Containers` library.**  The core as above without the containers; a second library references Npgsql 10 and SqlClient 7 and holds the containers as they are.  The two driver-floor projects keep their hand-copied `AppContainer`s.  Less rewriting, but three shared libraries under `tests/` besides `Tool.Testing`, and the floor projects' containers stay copies.

**Chosen: 1**, because the folder exists to make its patterns plain, and "one harness, your project brings its driver" is a rule an `AGENTS.md` states in a line.

### Landing the work

One pull request for the whole change was ruled out: the move alone touches every file of four projects, and a reviewer could not see the harness or the allocation project through it.  **Chosen:** four pieces, each a pull request that leaves the tree green, with the fourth split by TD-0080 item as its size asks.

1. Move and rename.
2. The harness.
3. `QueryMethodAllocation`, with the workflow and script changes.
4. The remaining items of TD-0080 that apply here.

## The layout

```
tests/
  DatabaseTests/
    README.md
    AGENTS.md
    Directory.Build.props         the naming pattern
    TestHarness/                  SqlSource.DatabaseTests.TestHarness, a library, no tests
    Postgres/                     SqlSource.DatabaseTests.Postgres, was tests/SqlSource.Tests.Postgres
    Postgres.Npgsql8/             SqlSource.DatabaseTests.Postgres.Npgsql8
    SqlServer/                    SqlSource.DatabaseTests.SqlServer
    SqlServer.SqlClient61/        SqlSource.DatabaseTests.SqlServer.SqlClient61
    QueryMethodAllocation/        SqlSource.DatabaseTests.QueryMethodAllocation
  SqlSource.Testing/              stays: the generator harness and what every test project shares
  SqlSource.Tool.Testing/         stays: the tool's helpers, the realistic corpus, the fixtures
  SqlSource.Tests/                stays, and gains the harness's unit tests
  SqlSource.Tests.RoslynFloor/    stays
  SqlSource.Tool.Tests/           stays
```

The solution file gets a folder `/tests/DatabaseTests/` holding the six projects.

### The naming pattern

The folder and the project file carry the short name: `Postgres/Postgres.csproj`.  The assembly and the root namespace carry the prefix: `SqlSource.DatabaseTests.Postgres`.  A `Directory.Build.props` in `tests/DatabaseTests/` sets `AssemblyName` and `RootNamespace` to `SqlSource.DatabaseTests.$(MSBuildProjectName)` for every project below it and imports the one above by the literal path `../../Directory.Build.props`, not `GetPathOfFileAbove`, since `ProjectIncludes` refuses an import whose path holds a property it cannot resolve; so no project file repeats the pattern and a new project gets it by being there.  Every namespace in the moved code changes from `SqlSource.Tests.Postgres…` to `SqlSource.DatabaseTests.Postgres…`, likewise for SQL Server, and the `InternalsVisibleTo` items of `src/SqlSource`, `src/SqlSource.Tool`, `tests/SqlSource.Testing` and `tests/SqlSource.Tool.Testing` name the new assemblies.  The folder's `AGENTS.md` states the pattern.

`TestScopes` derives a project's assembly name from the `project` line's path by the same rule, prefix plus file name, so `RunningProjectTests` keeps holding in each container project.

## Piece 1: move and rename

`git mv` of the four projects into their new folders, the `.csproj` files renamed, the namespaces changed, the solution updated, and every place that names an old path or assembly changed.  The known places, each to be grepped again when the piece is built, since the list is read from the tree today:

- `tools/test-scopes.txt`: the four `project` lines and the `postgres` and `sqlserver` pattern lines for the project folders.
- `pre-commit-validation.sh`: the pattern `tests/SqlSource.Tests.*/EndToEnd/*` of `needs_allocation`, the comment that names the projects, and the condition that treats a `.props` or `.targets` of the root or of `tests/` as an allocation input, which now also takes `tests/DatabaseTests/`.  The `--filter-not-class '*.EndToEnd.AllocationTests'` wildcard still matches.
- `.github/workflows/build.yml`: the two `--filter-not-class` type names, the `Test allocations` step's project path and class names, and the `PROJECT` and `cache-dependency-path` of the two floor jobs.
- `.github/workflows/nightly.yml`: `PROJECT` and `cache-dependency-path` of `test-postgres` and `test-sqlserver`, and the comment of the Windows and macOS job.
- `tools/describe-end-to-end.sh` and `tools/record-fixtures.sh`: project paths, the `.csproj` paths and the class names of `FixtureCasesTests`.
- `tests/SqlSource.Tests/TestLayoutTests.cs`, which enumerates `.csproj` files one folder below `tests/`: it enumerates recursively, and still finds exactly RoslynFloor's compile include from outside.  `tests/SqlSource.Tests/Validation/ContainerProjectInputsTests.cs` names the two projects in its expectations.
- `tests/SqlSource.Tool.Testing/Realistic/RealisticCorpus.cs`, which names the two projects by name.
- `RepositoryPaths.EndToEndProject` of each engine project.
- `.gitattributes`, the two `Realistic/Cases/**` lines, and `.editorconfig`, the section for `SqlServerContainer.cs`.
- `Directory.Packages.props` comments.
- Documents: the root `AGENTS.md` (rule 5 names `SqlServerContainer.ImageBuilds` by path, and the floor image commands), `CONTRIBUTING.md`, `tests/README.md`, `tests/AGENTS.md`, `tools/README.md`, `tools/AGENTS.md`, `.github/AGENTS.md`, `.github/workflows/README.md`, `docs/architecture/postgres-describer.md`, `postgres-nullability.md` and `sqlserver-describer.md`, `docs/tech-debt/TD-0080` and any other `docs/tech-debt` or `docs/deferred` page that links into the moved folders, and the readmes and `AGENTS.md` files that move with the projects, whose relative links change depth.  Plans and specs under `docs/superpowers/` are history and are left as they are.

The piece also writes the two new documents:

- **`tests/DatabaseTests/README.md`** says what the folder is for: the tests that prove SqlSource against a real server, where the describer's answers, the generated methods and the drivers meet a database.  It holds the table of the projects that `tests/README.md` has today for these four, with their Roslyn, their driver and what each runs, says that each needs Docker, how to run one project and on another image, and routes to each project's own readme for its procedures.  `tests/README.md` keeps the rest and links here for the database projects.
- **`tests/DatabaseTests/AGENTS.md`** holds the rules of the folder: the naming pattern; that a test that needs a server is in the project's collection and takes its container from the harness; that a project brings its own driver and the harness none; which project may reference `tests/SqlSource.Tool.Testing` (the two describer projects) and which may not (the driver-floor projects); that a change here is run on both images before it is pushed; and that the folder's projects are the ones `tools/test-scopes.txt` names.  The per-project `AGENTS.md` files keep their own rules.  Piece 1 writes it for what piece 1 delivers, and pieces 2 and 3 add their rules.

Piece 1 changes no code but names and paths, and the names include the markers of the `.sql` files: the type overrides of `Notes.sql`, the `input-model` of `Shapes.sql` and the namespaces that `Invoices.sql`, `Methods.sql`, `ExternalRefs.sql` and SQL Server's `AllTypes.sql` mention.  The sidecar's hash covers the SQL without its comments and the declared parameter types, so every committed sidecar stays valid and no `describe-end-to-end.sh` run is needed; `CheckTests` proves it.  Its validation: `./pre-commit-validation.sh --full`, both describer projects on their floor images, and `gh workflow run nightly.yml --ref <branch>` before the pull request is opened, since the nightly's project paths change.

## Piece 2: the harness

### What moves into `TestHarness`

From `tests/SqlSource.Testing`: `DefaultImages`, `EngineImage`, `SqlServerStart` with `IStartingContainer` and its adapter, `EndToEndEngine`, `EndToEndFiles`, and the `EndToEnd/` folder (`AllTypesRows`, `SqlServerAllTypesRows`, `ModelReflection`, `CommittedEntries`, `CallAllocations`, `RowBufferByHand`, `Level`).  `tests/SqlSource.Testing` keeps `RepositoryRoot`, `RepositoryWorkingFolder`, `TestScopes`, `ProjectIncludes`, the `Generator/` harness, `SqlTexts`, `CultureScope` and `TestSidecars`, and drops the Testcontainers package, which it carried for `EngineImage` alone.

From the two describer projects: `PostgresContainer` and `SqlServerContainer`, made driver-free, with `ImageBuilds`.  The two `AppContainer`s of the driver-floor projects are deleted, replaced by subclasses of the harness's containers.

The harness references `src/SqlSource` and `tests/SqlSource.Testing`, and its packages are `Testcontainers.PostgreSql`, `Testcontainers.MsSql`, `Shouldly`, `xunit.v3.assert` and `xunit.v3.extensibility.core`.  It brings no driver, no Dapper and nothing of the tool.  Its types stay `internal` but the two abstract containers, with `InternalsVisibleTo` for the six projects of the folder, `tests/SqlSource.Tests`, `tests/SqlSource.Tool.Tests`, whose `RealisticCorpusTests` reads `EngineImage`, and `tests/SqlSource.Tool.Testing`, whose `Realistic/` reads models through `ModelReflection` and the corpus through `EngineImage`.

### The driver-free containers

Each container is a public abstract class that implements `IAsyncLifetime`, and a project declares a public sealed subclass that supplies, through the protected constructor or abstract members, what the harness cannot know:

- the `DbProviderFactory` of the project's driver, from which the harness makes every `DbDataSource` with `CreateDataSource`;
- the image: `EngineImage.Postgres.Image`, which honours the variable, for the describer projects, or `EngineImage.Postgres.Default`, which ignores it, for the driver-floor and allocation projects, as today;
- the databases to build, each a name and the scripts to run in order, so that the describer projects build `fixtures`, `realistic`, `app` and `billing` as today, and the driver-floor and allocation projects build `app` alone.  SQL Server's `app` takes `new-types.sql` only on a major of 17 or later, and the harness reads the major before it builds the end-to-end databases, as `SqlServerContainer` does today, so the list of a database's scripts may depend on the major.

The harness builds connection strings with `DbConnectionStringBuilder` on the Testcontainers string, setting `Database`, which both drivers read (SqlClient as a synonym of `Initial Catalog`); reads PostgreSQL's major from `server_version_num` rather than Npgsql's `PostgreSqlVersion`; and keeps `ExecuteAsync`, `ScalarAsync`, `ConnectionStringOf`, `SourceOf`, `Major`, `SkipBelow` and `SkipUnlessRecordedMajor` with the same meaning.  A project's own `DbDataSource` with driver settings, such as `EndToEndSources.App` with its `MapEnum`, stays in the project and takes the connection string from the container.  The collection definitions (`UsesPostgres`, `UsesSqlServer`, `UsesApp`, `MeasuresAllocations` until piece 3 removes it) stay in each project and name the project's subclass.

### The harness's unit tests

`EngineImageTests` and `ModelReflectionTests` of `tests/SqlSource.Tests`, `SqlServerStartTests` and `EndToEndFilesTests` of the SQL Server project, and `RowBufferTests`' use of `RowBufferByHand`, need no Docker, and the harness is in scope `both`, so they belong to a project every run includes: `tests/SqlSource.Tests`, in a folder `TestHarness/`, which now references the harness.  With `EngineImageTests` and `SqlServerStartTests` side by side, their two fakes of `IStartingContainer` become one, beside them, which is item 9 of TD-0080, taken here because the move is the fix; the item is struck from TD-0080 in this pull request, with its proposed home, `SqlSource.Testing`, which no longer has Testcontainers.

### Documents of piece 2

`tests/README.md` and `tests/AGENTS.md` now describe three libraries, and the rule "keep the two libraries apart" becomes a rule about which library may reference what: `SqlSource.Testing` stays on the pinned Roslyn and references nothing of the tool, `TestHarness` brings no driver and nothing of the tool, and `Tool.Testing` references the tool.  `TestLayoutTests` and the reference graph of `tests/README.md` change with it.  The root `AGENTS.md` rule 5 points `ImageBuilds` to its new file, and `tools/AGENTS.md`, `tools/README.md` and the three scripts that read `DefaultImages.cs` point to its new path.  `tests/DatabaseTests/AGENTS.md` gains the rule that a container is a subclass of the harness's, started through `EngineImage`, and that the project supplies its driver's factory and its databases.  `.editorconfig`'s sections for `EndToEndFiles.cs` and `SqlServerContainer.cs` move to the new paths.

## Piece 3: `QueryMethodAllocation`

### The project

`tests/DatabaseTests/QueryMethodAllocation/` references `src/SqlSource` as an analyzer and an assembly, the harness, the current Npgsql and Microsoft.Data.SqlClient, both Testcontainers packages, Shouldly and xunit.v3, and imports the package's MSBuild files as the other end-to-end projects do.  It links, as `AdditionalFiles` at their own paths, the query files and sidecars whose methods the allocation tests call, from each engine's `EndToEnd/Queries/`: for PostgreSQL `Accounts.sql`, `RowBuffers.sql` and `Streaming.sql`, for SQL Server `Shapes.sql`, `RowBuffers.sql` and `Streaming.sql`, each with its `.sql.json`.  It claims them in classes of its own, under `Postgres/` and `SqlServer/` folders and namespaces, with the same attributes and generator parameters as the engine projects' claims (`AccountQueries` and `UnpooledAccountQueries`, `RowBufferQueries`, `StreamingQueries`, `ShapeQueries`), so that the generated methods it measures are the ones the engine projects run.  The engine projects keep their claims and their functional tests.  If a linked file's other queries need a package the engine project has and this one does not, such as `Microsoft.SqlServer.Types`, the builder adds the package rather than editing the query file.

Each engine has a container subclass that builds `app` alone on the engine's default image, ignoring the image variable, and a collection that holds it.  The project turns parallelization off for the whole assembly with `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, so collections run one after another and tests within one do too.

### The tests

The two `AllocationTests` classes move in and become one shape, which is item 3 of TD-0080: one helper that takes the generated call, the hand-written call, the margin and the connection state and asserts by the commonest difference of pairs through `CallAllocations`, one helper for the `Least*Saving` tests, and one naming scheme, `Method_Scenario_Outcome`, used by both engines.  Each engine keeps the connection states it measured: SQL Server's tests stay theories over an open and a closed connection, and PostgreSQL's stay on an open connection, since its margin, counts and `Least*Saving` constants were measured there alone and a closed connection would add rows that nothing has measured.  So the shared helper takes the connection state as an argument, and one shape means one helper and one naming scheme, not one set of rows.  The margins, the warm-up and pair counts and the `Least*Saving` constants keep their measured values and their comments, and each engine's hand-written methods stay its own, since they are the driver's calls.  Each test still skips under coverage or a profiler, through `CallAllocations`.  The `MeasuresAllocations` collections and `AllocationTests` classes of the two engine projects are deleted.

### Kept out of every other run

The project is in the solution, so `dotnet build SqlSource.slnx` and `format.sh` cover it, and no run of `dotnet test --solution SqlSource.slnx` executes it: not the pre-commit script, plain or `--full`, and not `build.yml`.  The mechanism is an MSBuild property in its `.csproj` that keeps `dotnet test --solution` from treating it as a test project unless `RunQueryMethodAllocation=true` is passed, which the nightly jobs pass to `dotnet test --project`.  The builder verifies first that the new `dotnet test` of the .NET 10 SDK selects projects by that property, that `--project` with the property runs the tests, and that xunit's entry point still builds; if it does not hold, the fallback is an environment variable, `SQLSOURCE_TESTS_RUN_ALLOCATIONS`, that the nightly jobs set, without which the container fixtures do nothing and every test skips before it touches one, and the choice is recorded in the project's readme.  `tools/test-scopes.txt` gives the project's folder the scope `none`, since the script never runs it and nothing else reads it.

### What is removed

- `pre-commit-validation.sh`: `needs_allocation`, the allocation trigger, decision, filter, step and plan lines, and the comments that describe them.  The script's `--plan` output no longer mentions allocations.
- `build.yml`: the two `--filter-not-class` arguments of the `Test` step and the `Test allocations` step, with the comment that explains them.
- `.github/AGENTS.md`: the rule about the two steps.  `tests/AGENTS.md`: the flaky-tests bullet's account of where the allocation tests run and the sentence that a margin is measured "as CI's `build` job runs it", which now says as the nightly job runs it.  `tests/SqlSource.Tests.Postgres/AGENTS.md` and the SQL Server one, and their `EndToEnd/AGENTS.md` files: the `MeasuresAllocations` and `Allocations` rules, which move to the new project's `AGENTS.md`.  `tools/README.md` and `CONTRIBUTING.md`: what they say of the allocation tests in the script and in CI.

### The nightly jobs

Two new jobs in `nightly.yml`, `allocation-postgres` and `allocation-sqlserver`, on `ubuntu-latest`, `fail-fast: false`, a timeout of 15 minutes, with the steps of `test-postgres`: check out, set up .NET with the project's lock file as the cache path, build the project, pull the engine's default image in a step of its own, and test with `dotnet test --project tests/DatabaseTests/QueryMethodAllocation` filtered to the engine's namespace, `--report-github --report-github-summary-include-passed false`, and the property or variable that lets the tests run.  Each job starts one container and measures one engine.  The jobs run on the schedule, on manual runs and on pull requests that change the workflow, as the file's other jobs do.  `.github/workflows/README.md` and `.github/AGENTS.md` describe them, and the rule that the two workflows together cover every supported major is untouched: the allocation jobs run the default images only.

### Documents of piece 3

- **`tests/DatabaseTests/QueryMethodAllocation/README.md`**: the purpose (a nightly signal of a generated method allocating more than a hand-written one, or a pooled method saving less, not a gate); what a failure means and what to do with it (read the assertion's message, which names the commonest difference and the others, reproduce locally with the command the readme gives, and fix or explain in a pull request); how to run one engine locally; how a margin or a `Least*Saving` is set, measured on both engines, lowered when the code improves and never raised to make a change pass; and why it skips under coverage.
- **`tests/DatabaseTests/QueryMethodAllocation/AGENTS.md`**: the rules now in the engines' `EndToEnd/AGENTS.md` under "Allocations", plus: parallelization stays off, a new allocation test claims its query from the engine project's file and never a copy, and a change to a generated method's calls changes the hand-written method with it.
- `tests/DatabaseTests/README.md` and `tests/README.md` gain the project's row, and `tests/DatabaseTests/AGENTS.md` the rule that the allocation tests live here and run only in the nightly.

## Piece 4: the remaining items of TD-0080

Each in a pull request of its own unless two are small enough to review together; the builder decides the grouping by size.  Each strikes its item from TD-0080 in the same pull request.

- **Item 1**, the realistic test classes copied between the engines (`RealisticModelRefusalTests`, `RealisticClaimsTests`, `RealisticCasesTests`, `RealisticDescriptionTests`, `RealisticNullabilitySafetyTests`, `RealisticModelNullabilityTests`): the bodies move into `tests/SqlSource.Tool.Testing/Realistic/` behind the engine binding that `ErrorPaths` and `ToolProcess` use, and each engine keeps one-line facts.
- **Item 2**, `RowBufferQueryTests` copied whole: the body and its helpers move into the harness's `EndToEnd/` as a driver-free helper that takes the engine's methods as delegates, and each engine keeps one-line facts.
- **Item 7**, the realistic sample dictionary built six times with different comparers: one `RealisticSample.Of(...)` in `tests/SqlSource.Tool.Testing` with the reader's comparer, and a `Substitute(sql, sample, parameters)`.
- **Item 15**, literals one place should hold: `EngineImageTests` and `SqlServerStartTests`, now side by side in `tests/SqlSource.Tests/TestHarness/`, take the image tags from `DefaultImages` or use a tag that is plainly fake; and the two `RepositoryPaths.DescriberFixtures` become one in the harness, keyed by engine, with each project's `RepositoryPaths` keeping what is its own.
- **Item 18**, the fixture build files linked by the same block in three projects: a `tests/Fixtures.props` that `tests/SqlSource.Tool.Tests`, `Postgres` and `SqlServer` import, in scope `both`, with `$(MSBuildThisFileDirectory)`-based includes since the three sit at different depths.  `ProjectIncludes.Inputs` reads only `.csproj` and `Directory.Build.*` files today, so the links would leave the check's sight and `Refused_ScopesWithoutTheFixtureProjectLines_NamesTheLinkedFixtures` would fail: this item teaches `ProjectIncludes` to follow `Import` elements, and that test must still fail without the fixture lines.

Items 3 and 9 are done in pieces 3 and 2.

## Units for the implementation manager

Each unit is one pull request to `main`, delivered by a child session that plans it from this document with `superpowers:writing-plans`.  A prerequisite is code the unit builds on; a merge-order unit is one whose pull request merges first because the two change the same documents.

| Unit | Title | Section | Prerequisites | Merges after |
|----|----|----|----|----|
| `move` | Move the database test projects into `tests/DatabaseTests/` | Piece 1 | none | none |
| `harness` | The driver-free `TestHarness` | Piece 2 | `move` | none |
| `allocation` | `QueryMethodAllocation` and the nightly jobs | Piece 3 | `harness` | none |
| `td-0080-copies` | TD-0080 items 1 and 2: the realistic classes and `RowBufferQueryTests` | Piece 4 | `harness` | `allocation` |
| `td-0080-small` | TD-0080 items 7, 15 and 18: the sample builder, the literals and the fixture links | Piece 4 | `harness` | `td-0080-copies` |

`allocation`, `td-0080-copies` and `td-0080-small` may run side by side once `harness` has merged.  What they share, which each keeps small and the merge order resolves: `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md` (each strikes its items), `tests/README.md`, `tests/DatabaseTests/README.md` and `tests/DatabaseTests/AGENTS.md`.  `allocation` alone deletes `AllocationTests.cs` and `MeasuresAllocations.cs` from the two describer projects; `td-0080-copies` alone changes their `Realistic/` and `RowBufferQueryTests.cs`; `td-0080-small` alone changes their `RepositoryPaths.cs` and `.csproj` files and `tests/SqlSource.Tests/TestHarness/`.  The three touch no file in common but the documents above.

A child splits its unit into parts only when the repository's rules force it; none of these is expected to need that.

## Validation of each piece

Every pull request: `./pre-commit-validation.sh --full` on the tree as it will be pushed, both describer projects on their floor images (`SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`), the version check, and `branch-review:review` until clean.  Pieces 1 and 3 also run `nightly.yml` by hand on the branch, since each changes what the nightly runs; piece 3's run must show the two allocation jobs passing and the build job of CI running no allocation test.

## Left to the builder

- The exact shape of the harness containers' protected surface (constructor arguments or abstract members), and how a database's scripts depend on the major.
- Whether `QueryMethodAllocation`'s PostgreSQL data source maps any enum: none of its linked queries needs one today (`Accounts.sql` reads `account_status` unmapped, as the engine project's test does), so the builder checks when it links the files.
- The names of the harness's folders and of the two engine folders inside `QueryMethodAllocation`.
- The exact helper signatures of the unified allocation tests, and the order of the theories' rows.
- The grouping of piece 4's items into pull requests.
- The wording of the new readmes and `AGENTS.md` files, within the limits that `AgentsFileTests` checks: one rule per bullet of at most 80 words, a file of at most 8 KB.
