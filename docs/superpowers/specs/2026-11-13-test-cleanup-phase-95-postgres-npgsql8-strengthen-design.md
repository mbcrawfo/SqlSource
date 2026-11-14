# Test cleanup phase 95: tests/DatabaseTests/Postgres.Npgsql8, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres.Npgsql8`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres.Npgsql8/NetTopologySuiteQueryTests.cs`
- `tests/DatabaseTests/Postgres.Npgsql8/SynchronousQueryTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/NetTopologySuite/NetTopologySuiteQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.Npgsql8-1-03

- test: `tests/DatabaseTests/Postgres.Npgsql8/NetTopologySuiteQueryTests.cs` | `NetTopologySuiteQueryTests.InsertSpatialValues_GeometryArrayLazySequence_RoundTrips` | rows: all
- action: strengthen
- evidence: read (the comment at lines 199-200 says the LINQ sequence "is copied once on its way to the server", but the sequence built at lines 210-213 counts nothing and the test asserts only the elements and SRIDs read back (lines 224-227); a method that enumerated the `Where` sequence twice would send the same elements and the test would pass, while the int4 and list parameters each have a counting test of their own, `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce` and `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`.  The mutation evidence says `None`.  `tests/DatabaseTests/Postgres.Npgsql8/AGENTS.md` says the class is a copy of `tests/DatabaseTests/Postgres/EndToEnd/NetTopologySuite/NetTopologySuiteQueryTests.cs`, which has the same test at line 201); blind: no
- cover: none
- proposal: Count the enumerations of the geometry sequence, by a counter in its `Select` or a counting sequence of geometries, and assert one, here and in the end-to-end copy.
- verdict: confirmed
- verifier's reason: The comment at `tests/DatabaseTests/Postgres.Npgsql8/NetTopologySuiteQueryTests.cs:199-200` says the sequence "is copied once on its way to the server", and the test (lines 202-228) builds a `Select(...).Where(...)` sequence that counts nothing (lines 210-213) and asserts only the elements and SRIDs read back (lines 224-227).  The `geometry[]` property is bound through the same `as IList<E> ?? Enumerable.ToArray(value)` (`src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:181-193`), and a binding that enumerated the deterministic sequence twice would send the same elements, so the comment's promise could be removed and the test would pass.  The end-to-end copy (`tests/DatabaseTests/Postgres/EndToEnd/NetTopologySuite/NetTopologySuiteQueryTests.cs:199-228`) has the same comment and the same assertions, so strengthening both, as the copy rule asks, is right.  The finding reads the test rightly.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-engines-10

- test: `tests/DatabaseTests/Postgres.Npgsql8/SynchronousQueryTests.cs` | `SynchronousQueryTests.TouchAccountQuietlySync_NoRowOnAClosedConnection_IsClosedAgain` | rows: all
- action: strengthen
- evidence: read (twin fault of F-DatabaseTests.SqlServer.SqlClient61-1-01; the Postgres.Npgsql8-1 slice has no finding on it.  SynchronousQueryTests.cs:29-38 builds a connection that is never opened, calls `TouchAccountQuietlySync(new TouchAccountQuietlySyncArgs(99))`, the project's one method of the `none` shape (`-> none`, `method-type=sync`, tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql:20-23, linked), an update that id 99 matches in no row, and asserts only `connection.State == Closed`: a method that never opened the connection or ran its command passes.  The name promises "closed again", and the class comment (:8-11) that each shape "reads its rows, or runs, through Npgsql 8's synchronous calls"; the class's other tests read rows or a count, so they show their command ran, and the end-to-end `TouchAccountQuietlySync_InATransaction_UpdatesTheRow` runs on Npgsql 10, no cover for the floor.  No document records the form as deliberate.  No mutation scope ran the slice.); blind: no
- cover: none
- proposal: Record `connection.StateChange` and assert `Open` then `Closed`, as F-DatabaseTests.SqlServer.SqlClient61-1-01 proposes for SqlClient 6.1, so that the test fails when the synchronous `none` method does not run its command on Npgsql 8.
- verdict: confirmed
- verifier's reason: tests/DatabaseTests/Postgres.Npgsql8/SynchronousQueryTests.cs:29-38 builds a connection from `source.CreateConnection()` that is never opened, calls `TouchAccountQuietlySync(new TouchAccountQuietlySyncArgs(99))` (`-> none`, `method-type=sync`, an `UPDATE` that id 99 matches nowhere, tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql:20-23), and asserts only `State == Closed`, which a method that did nothing also leaves: the finding reads the test rightly against its name and the class comment (:8-11, each shape "reads its rows, or runs, through Npgsql 8's synchronous calls").  The class's other tests read rows or a count; the end-to-end synchronous tests run on Npgsql 10, no cover for the floor.  One note for the cleanup: recording `StateChange` Open then Closed proves the method opened the connection, not that it ran its command; asserting that the command ran too (for example by its effect, or the server's view of the session) would meet the class comment fully.  Twin of F-DatabaseTests.SqlServer.SqlClient61-1-01, confirmed.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`, `dotnet test --project tests/DatabaseTests/Postgres.Npgsql8`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
