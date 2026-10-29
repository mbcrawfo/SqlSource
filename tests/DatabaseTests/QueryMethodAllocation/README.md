# tests/DatabaseTests/QueryMethodAllocation

This project holds the allocation tests of the generated methods of both engines: each generated method is held to the bytes a hand-written ADO.NET method of the same calls allocates, and each pooled result member to the bytes it saves against a hand-written baseline of its result.  It is a nightly signal and not a gate.  A red `allocation-postgres` or `allocation-sqlserver` job of `.github/workflows/nightly.yml` says that a generated method may allocate more for each call than a hand-written method of the same calls, or that a pooled method saves less than it did.  No pull request waits for it.

## When a test fails

1. Read the failure and the test's output.  The output says, of the pairs of calls, the commonest difference between the generated method and the hand-written one, and the others after it.  A difference above the margin means that the generated method allocates an object on each call that the hand-written method does not; the commonest difference of a pair is the bytes of that object, and a boxed value is 24 bytes.
2. Reproduce it with the command for that engine, below.
3. Fix the generated code, or explain in the pull request why the generated method must allocate more, and change the hand-written method with it.  Never raise a margin, or lower a `Least*Saving`, to make a change pass.

## Running one engine

The project needs Docker running, and it starts one container of the engine's default image for its tests.  The variables that name another image of an engine are ignored here, since the margins were measured on the default.

```bash
dotnet test --project tests/DatabaseTests/QueryMethodAllocation -p:RunQueryMethodAllocation=true --filter-namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres
dotnet test --project tests/DatabaseTests/QueryMethodAllocation -p:RunQueryMethodAllocation=true --filter-namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer
```

Without `-p:RunQueryMethodAllocation=true`, `dotnet test` finds no test project here.  That is how `dotnet test --solution SqlSource.slnx`, `pre-commit-validation.sh` and `.github/workflows/build.yml` leave the project out, so that only the two nightly jobs run it.  It still builds with the solution.

## The tests

The queries are the end-to-end projects' own, linked from `tests/DatabaseTests/Postgres/EndToEnd/Queries/` and `tests/DatabaseTests/SqlServer/EndToEnd/Queries/` with their sidecars, so that a test measures the method that the end-to-end project runs.  Each engine has its own hand-written methods, since they are the driver's calls, and each class is in a collection that runs alone, on a container of its own.  No two tests run at once, since a measure counts what every thread of the process allocates.

- `Postgres/AllocationTests.cs` measures `GetAccountName`, `ListAccounts` and `ListClosedBefore` of `Accounts.sql`, the numbers queries of `RowBuffers.sql` and `StreamAccounts` of `Streaming.sql`.  Each query has two pairs: its method of `AccountQueries`, which reads its rows into the pooled buffer, against a hand-written method that reads them into `RowBufferByHand` of this project, the same buffer by hand, and its method of `UnpooledAccountQueries`, under `no-array-pool`, against one that reads them into a `List`.  The numbers queries `NumbersAsList`, `NumbersAsIList`, `NumbersAsICollection` and `NumbersAsImmutableArray`, at 3 rows, have two pairs each, against a method that copies the rows of `RowBufferByHand` with `ToList` or `ToImmutableArray`, and, for their `NumbersNoPoolAs` twins, against one that returns a `List` as it is or drains an `ImmutableArray` builder.  The streaming `StreamAccounts` has a pair of its own, against a hand-written static local async iterator that makes the same calls, measured over a loop to the end of the stream.  Three more tests hold the pooled `NumbersAsArray`, `NumbersAsList` and `NumbersAsImmutableArray`, at 300 rows, each to allocating at least a given number of bytes less than the hand-written baseline of its result: a `List` copied to an array, a `List` grown by doubling, and an `ImmutableArray` builder that is drained.  The margin is 64 bytes.  All 18 tests measure an open connection, the state the margin was measured in.
- `SqlServer/AllocationTests.cs` measures `SearchAccounts` of `Shapes.sql`, whose pair against `SearchAccountsUnpooled`, the same query under `no-array-pool`, is built as PostgreSQL's, and the numbers queries of `RowBuffers.sql` and `StreamAccounts` of `Streaming.sql`, in the same pairs.  The three tests at 300 rows are there too.  The margin is 16 bytes, less than the smallest object.  Each of its 14 tests is a theory over a closed and an open connection, 28 tests: a closed connection is opened and closed again by each call, which an open one is not.  The hand-written method makes the same calls on both, the `NextResultAsync` loop that drains the batch after the rows among them, and the `catch` that disposes the reader and checks the transaction after an exception of the method's own around its read, and its streaming iterator has the generated one's `atRow` local and `finally`, and that `catch` around each row's construction.

Each test makes 500 pairs of calls after a warm-up of 200 calls of each method on PostgreSQL and 50 on SQL Server, one call of each method, each measured on its own, and compares the two by the commonest difference, which `CallAllocations` of this project explains.  The project's `Allocations` helper makes the assertions, from a `MeasureSettings` of each class.

## The margins and the savings

A margin or a `Least*SavingInBytesPerCall` is set from what the test measured on both engines' default images, and the comment on each constant says how.  A margin is lowered when the generated code improves, and a saving is raised only when the pool saves more.  Neither is moved to make a change pass: a change that makes a method allocate more is fixed, or explained and its hand-written method changed with it.

## Coverage

A test skips under coverage or a profiler, since the collector allocates in the process it measures on some calls and not others, which would make the commonest difference its bytes ([why](CallAllocations.cs)).  The nightly jobs run without it.
