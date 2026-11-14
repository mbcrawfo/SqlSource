# AGENTS.md - tests/DatabaseTests/SqlServer.SyncAsync

[`README.md`](README.md) says how the comparisons run, how to read a failure and what is not covered.

## Queries and types

- **The queries are `tests/DatabaseTests/SqlServer/EndToEnd/`'s, copied into `obj/Mirror/` by the target `SyncAsyncMirrorQueries` of the `.csproj`**, never linked and never copied by hand: a linked `.sql` file keeps the settings files of its own folder, and `method-type=both` comes from this project's `sqlsource.json` above `obj/` ([why](README.md#the-queries)).
- **No sidecar is written here**: the mirror copies the end-to-end project's, which `tools/describe-end-to-end.sh` writes.
- **The claims mirror the end-to-end project's, one file each**: a claim added or moved there is added or moved here in the same change.
- **`Types/` and `Json/Types/` are this project's copies of the end-to-end types, in their namespaces**, since a test project compiles no file of another: a type the queries name, added or changed there, is copied here in the same change.

## Scenarios

- **Every generated pair has its scenarios in `Scenarios/<File>Scenarios.cs`, listed in `Scenarios/AllScenarios.cs`**, and a collection method an `Empty`, a `ChunkBoundary` and a `LaterChunkFailure` one: `Find_GeneratedPairs_EachHasItsScenarios` fails otherwise, and only the nightly runs it.
- **A scenario holds no expected value**: the asynchronous method's outcome is what the synchronous one's is compared with, and the end-to-end tests assert values.
- **A scenario that writes sets `Writes` and names the tables it touches in `Tables`**, so that each run restores its own copy of the seed; one that only reads sets neither, since a restore costs about half a second.
- **A scenario whose SQL before the call changes a setting of the session, such as `SET XACT_ABORT ON`, runs on the source `unpooled`**, so that the pool hands the setting to no other scenario.
- **An insert of `Queries/Batches.sql` is given `SqlServerDatabases.BatchNotes` by its token `notes`**, never a temporary table, which a closed connection drops before the runner reads it.
- **Every test that needs the server is in `UsesSqlServer`**; `KnownDifferencesTests` and `LibraryRenderingTests` need none and stay outside it, so that they run without Docker.
- **A value of a library whose own `ToString` drops part of it, as a `SqlGeography`'s drops its SRID, is written by `LibraryRendering`, with a row in `LibraryRenderingTests`, and the NodaTime and NetTopologySuite rows stay the same as the other `SyncAsync` project's**, since the harness references neither library and a test project compiles no file of another.

## Known differences

- **`KnownDifferences.cs` holds a driver's limit alone, with its reason and a note of its effect in `site/reference/databases/sqlserver/query-methods.md`**, and a row in the readme's table.
- **One that changes what a caller's `catch` sees or what is left in the database is added only once the owner has ruled it the driver's limit**, and never to hide a difference that the generated methods could avoid.

## Project

- **The project is a test project only under `RunSyncAsync=true`**, through `IsTestingPlatformApplication` in the `.csproj`, and only the `sync-async-sqlserver` job of `.github/workflows/nightly.yml` passes it; run it by the command in the [readme](README.md#running).

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
