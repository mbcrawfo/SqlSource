# AGENTS.md - tests/DatabaseTests

[`README.md`](README.md) says what each project of this folder runs and how to run it on another image.

## Names

- **The folder and the project file carry the short name, and the assembly and the root namespace the prefix**: `Postgres/Postgres.csproj` builds `SqlSource.DatabaseTests.Postgres`, and every namespace in it starts with that.  `Directory.Build.props` of this folder sets both from the project file's name; no project file sets them.
- **The `project` lines of `tools/test-scopes.txt` are exactly the test projects of this folder; `TestHarness`, a library, has none, and `TestScopes` of `tests/SqlSource.Testing` derives the same assembly name from each**, so a new project here gets its `project` line and its folder's scope lines, and a `RunningProjectTests` copied from a sibling, in the same change ([why](../README.md#reading-the-repository)).

## Containers

- **A test that needs the server is in the project's collection** (`UsesPostgres`, `UsesSqlServer` or `UsesApp`, and `MeasuresAllocations` for the two `AllocationTests`), whose fixture is the project's public sealed subclass of the harness's `PostgresContainer` or `SqlServerContainer`, started once for the run through `EngineImage`; the project gives it its driver's `DbProviderFactory`, its image and its databases with their scripts, and the harness brings no driver.
- **A change here is run on both of the engine's images before it is pushed**, since `pre-commit-validation.sh` runs the default image alone ([the commands](README.md#running)).

## References

- **`Postgres.Npgsql8` and `SqlServer.SqlClient61` reference `src/SqlSource`, `tests/SqlSource.Testing` and `TestHarness` alone**, since they pin the oldest supported driver: a reference to the tool, to `tests/SqlSource.Tool.Testing` or to the engine's describer project is a package downgrade.
- **`TestHarness` references `src/SqlSource` and `tests/SqlSource.Testing` alone**, with no driver, no Dapper and nothing of the tool, so every project here, the driver-floor ones included, references it; a helper that needs a driver stays in the project.
- **`Postgres` and `SqlServer` reference `tests/SqlSource.Tool.Testing` too**, for the describer, fixture and realistic-corpus helpers, and are the two projects the tool's fixtures and corpora are recorded through.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
