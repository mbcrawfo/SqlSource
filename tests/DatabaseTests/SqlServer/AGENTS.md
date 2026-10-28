# AGENTS.md - tests/DatabaseTests/SqlServer

## The container

- **A test of `tests/DatabaseTests/SqlServer` that needs the server is in the collection `UsesSqlServer`**, which starts one container for the run through `tests/DatabaseTests/SqlServer/SqlServerContainer.cs`.  A test that needs no server is not in the collection ([such as](README.md#running)).
- **The container runs on `EngineImage.SqlServer.Image` of `tests/SqlSource.Testing`, the one place a test takes the image from**: the image that `DefaultImages.SqlServer` names, unless the variable `EngineImage.SqlServer.Variable` names another.  Another tag in `tests/` is a sample that no test reads as the default ([the images](README.md#the-images)).
- **The one test that needs a server of its own, `EndToEnd/AllocationTests.cs`, is in the collection `MeasuresAllocations`**, since it counts the bytes that every thread allocates: the collection runs alone, with parallelization off, on a `SqlServerContainer` of its own.
- **A container starts through `EngineImage.SqlServer.StartAsync` of `tests/SqlSource.Testing`, never through the container's own `StartAsync`.**  It goes through `SqlServerStart`, which starts a new one, once, after the crash of TD-0072.  `AppContainer` of `tests/DatabaseTests/SqlServer.SqlClient61` starts its own so too.

## Databases and scripts

- **The project reads the files of its databases from the repository through `RepositoryPaths`**, found through `RepositoryRoot.Path` ([which files](README.md#running)).
- **A script is applied through `EndToEndFiles` of `tests/SqlSource.Testing` with `EndToEndEngine.SqlServer`**, whose `SqlServerBatches` splits it at the lines that hold `GO` alone, never through `sqlcmd`, whose `QUOTED_IDENTIFIER` is off.

## Versions

- **A test whose answer depends on the version branches on `SqlServerContainer.Major` or skips by `SkipBelow` or `SkipUnlessRecordedMajor` of the container**, which call the gates of `EngineImage`.  A realistic case skips by `SkipUnlessRunsOn` and `SkipIfRefused` of `RealisticCase`.
- **A change here is run on both images before it is pushed**, since `pre-commit-validation.sh` runs the default image alone.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
