# AGENTS.md - tests/DatabaseTests

[`README.md`](README.md) says what each project of this folder runs and how to run it on another image.

## Names

- **The folder and the project file carry the short name, and the assembly and the root namespace the prefix**: `Postgres/Postgres.csproj` builds `SqlSource.DatabaseTests.Postgres`, and every namespace in it starts with that.  `Directory.Build.props` of this folder sets both from the project file's name; no project file sets them.
- **The `project` lines of `tools/test-scopes.txt` are exactly the projects of this folder, and `TestScopes` of `tests/SqlSource.Testing` derives the same assembly name from each**, so a new project here gets its `project` line and its folder's scope lines, and a `RunningProjectTests` copied from a sibling, in the same change ([why](../README.md#reading-the-repository)).

## Containers

- **Every project here needs Docker, and a test that needs the server is in the project's collection** (`UsesPostgres`, `UsesSqlServer` or `UsesApp`, and `MeasuresAllocations` for the two `AllocationTests`), which starts one container for the run through `EngineImage` of `tests/SqlSource.Testing`; a test that needs none stays out of it.
- **A change here is run on both of the engine's images before it is pushed**, since `pre-commit-validation.sh` runs the default image alone ([the commands](README.md#running)).

## References

- **`Postgres.Npgsql8` and `SqlServer.SqlClient61` reference `src/SqlSource` and `tests/SqlSource.Testing` alone**, since they pin the oldest supported driver: a reference to the tool, to `tests/SqlSource.Tool.Testing` or to the engine's describer project is a package downgrade.
- **`Postgres` and `SqlServer` reference `tests/SqlSource.Tool.Testing` too**, for the describer, fixture and realistic-corpus helpers, and are the two projects the tool's fixtures and corpora are recorded through.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
