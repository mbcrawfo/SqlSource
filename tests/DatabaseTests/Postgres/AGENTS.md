# AGENTS.md - tests/DatabaseTests/Postgres

## The container

- **A test of `tests/DatabaseTests/Postgres` that needs the server is in the collection `UsesPostgres`**, which starts one container for the run through `tests/DatabaseTests/Postgres/PostgresDatabases.cs`, a subclass of the harness's `PostgresContainer`.  The tests that need none are not in it ([which they are](README.md#running)).
- **The container runs on `EngineImage.Postgres.Image` of `tests/DatabaseTests/TestHarness`, the one place a test takes the image from**: the image that `DefaultImages.Postgres` names, unless the variable `EngineImage.Postgres.Variable` names another.  Another tag in `tests/` is a sample that no test reads as the default ([the image](README.md#the-image)).
- **A container starts through `EngineImage.Postgres.StartAsync`, never through the container's own `StartAsync`**, since it disposes a container whose start threw, which xunit does not.  The harness starts every container so.
- **Its databases are `fixtures`, `realistic`, and `app` and `billing` for `EndToEnd/`**, built from `EndToEnd/app/` and `EndToEnd/billing/`, each through `EndToEndFiles` of the harness (schema first, then seed), as the Npgsql 8 project builds its own.
- **`gis`, of `EndToEnd/gis/`, is the one database of `PostgisDatabases.cs`, the PostGIS container of the collection `UsesPostgis`, on `EngineImage.Postgres.Image`**: a query of PostGIS's types goes in `EndToEnd/NetTopologySuite/`, never into `app`, whose container has no PostGIS.
- **The project reads `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/` from the repository** through `DescriberFixtures.Postgres` of the harness, found through `RepositoryRoot.Path`, and not from an output folder.

## Versions

- **A test whose answer depends on the version branches on `PostgresDatabases.Major`, or skips by `SkipBelow` or `SkipUnlessRecordedMajor` of the container**, which call the gates of `EngineImage`.  A realistic case skips by `SkipUnlessRunsOn` and `SkipIfRefused` of `RealisticCase`, in `tests/SqlSource.Tool.Testing/Realistic/`.
- **A change here is run on both images before it is pushed**, since `pre-commit-validation.sh` runs the default image alone.

## Statements and objects

- **A test runs one statement of its own on the database `fixtures` through `PostgresDatabases.ExecuteAsync` or `ScalarAsync`.**  One that holds a transaction, runs several commands on one source, reads rows, binds a parameter or uses another connection keeps its own data source.
- **An object that a test leaves on the server has its text in `Describer/ScratchSchema.cs`, made by SQL that can run again**; one that changes what other tests see is dropped by its test in a `finally`.

## Sessions and requests

- **A test opens, describes and captures through `Describer/Sessions.cs`**, which opens through `Describers.OpenAsync` of `tests/SqlSource.Tool.Testing`, and builds its requests with `DescribeRequests` of the same library.
- **Two kinds of test are the exceptions**: the tests of the live calls below a session (`ModifierCallTests`, `ExplainTests`, `RelationsCallTests`, `OverloadsCallTests`, `HiddenTypesCallTests`, `ShadowingOperatorsCallTests`, and the calls `TransactionTests` makes below a session), and `FixtureCasesTests`, which describes through `FixtureCaseRuns.ShouldDescribeOrRecordAsync` of `tests/SqlSource.Tool.Testing/Fixtures/` ([what it does](README.md#the-describers-fixtures)).
- **The three tests of `ErrorPathTests` and the one of `ToolProcessTests` are the bodies of `ErrorPaths` and `ToolProcess` in `tests/SqlSource.Tool.Testing`**, called with the engine's own statements, connection and describers.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
