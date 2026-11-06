# AGENTS.md - tests/SqlSource.Tool.Tests

## Where a test goes

- **The fakes and helpers that only the tool's tests use stay at this project's root**, in the namespace `SqlSource.Tool.Tests`, which every sub-folder sees without a `using`.  So do the tests of `ToolHost`, of `Outcome` and of a helper.
- **The tests of the `Fixtures/` and `Realistic/` harnesses of `tests/SqlSource.Tool.Testing` mirror their folder here**, in `Fixtures/` and `Realistic/`; the tests of its other helpers stay at the root.
- **The split classes share their cases through a static helper, and no base class**: `PostgresSession*`, `PostgresEvents*`, `NullabilityWalk*`, `SqlServerSession*` and `SqlServerEvents*` through theirs, and `DatabaseRuns*` through `DatabaseRunsScene`, composed as `DescribeScene` is ([the helpers](../README.md#the-projects)).

## Processes and the environment

- **No test of the tool waits for a time to pick an order, or changes the test process unseen.**
- **A test that needs the answers of `tests/SqlSource.Tool.Testing/FakeProcessRunner.cs` in an order, or several open at once, holds them with `ProcessGate.cs`**, and not with `Task.Delay`.
- **A test that changes the environment of the test process is in the collection `ChangesTheEnvironment`**, which runs alone, and puts back what it changed.
- **A test runs `dotnet build` of a fixture through `FixtureProjects.BuildAsync`**, which clears the variables that would take the build online, keep it offline as in CI or have MSBuild log every variable, whatever the developer's shell or the runner holds.
- **A test of the online build gives the build the stand-in of the tool through `StandIn.CommandSwitch` and tells it what to do through `StandIn.Tell`**, never a script, which would not run on Windows.
- **A test of what the tool does outside a repository, in a `TempFolder` with no `.git` of its own, starts with `OutsideARepository.SkipUnless`**, since the tool climbs to a `.git` above the temporary folder where a machine has one.
- **A test that starts a program of the operating system skips with `Programs.SkipUnlessOnPath`** where the program is missing, and one that needs a program that waits takes it from `Programs.Waiter`, whichever the operating system.

## Fixture projects and bundles

- **`Fixtures/Projects/` holds projects that are never built or restored.**  They are not compiled into the test project, and a test that leaves an `obj` in the repository, or a file in a fixture, is a bug ([fixture projects](../README.md#fixture-projects)).
- **`Fixtures/Projects/OddPaths` holds a folder whose name has `;`, `=`, `%`, a quote and a non-ASCII letter on purpose.**
- **`tools/check-package-install.sh` replays `Fixtures/Bundles/postgres/select-left-join` with the packed tool**, so moving, renaming or changing that case changes the script and `needs_packages` of `pre-commit-validation.sh` in the same commit.

## Descriptors

- **`AnalyzerReleases.Unshipped.md` lists the one descriptor the tests bring themselves**; a new one adds a row there.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
