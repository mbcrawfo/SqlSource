# tests/DatabaseTests/QueryMethodAllocation

## Allocations

- **`Postgres/AllocationTests.cs` and `SqlServer/AllocationTests.cs` hold each generated method to a hand-written one that makes the same calls**, within a margin.  A change to the calls a method makes changes the hand-written method with it, never the margin.
- **The streaming `StreamAccounts` has an allocation pair of its own**, against a hand-written static local async iterator, within the same margin.
- **A pooled result member that copies its rows out needs a test against a hand-written baseline of its own**, as `ToArray`, `ToList` and `ToImmutableArray` have, since the pairs against `RowBufferByHand` cannot see what the buffer costs.  Each `Least*SavingInBytesPerCall` is set from a measure, raised when the pool saves more, and never lowered to make a change pass.
- **A test measures through `Allocations` and `MeasureSettings`**, never through `CallAllocations` by itself: `Allocations` is where the assertions are shared.  `CallAllocations` measures by the commonest difference of pairs of calls, never by a total, a median of blocks or the least of the calls ([why](CallAllocations.cs)).

## Project

- **Parallelization stays off**, through `[assembly: Parallelization(Mode = ParallelMode.None)]` in `Allocations.cs` and `DisableParallelization` on each collection, since every test counts what every thread allocates.
- **A new test claims its query from the engine project's file, linked in the `.csproj`**, never a copy, so that it measures the method the engine project runs.
- **Each engine keeps its own connection state and hand-written methods**: `State` in the PostgreSQL class, the theory rows of SQL Server's, since the hand-written methods are the driver's calls.
- **The project is a test project only under `RunQueryMethodAllocation=true`**, through `IsTestingPlatformApplication` in the `.csproj`, and only the `allocation-postgres` and `allocation-sqlserver` jobs of `.github/workflows/nightly.yml` pass it.  The solution's test run, `pre-commit-validation.sh` and `build.yml` leave it out; run it by the command in the [readme](README.md).

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
