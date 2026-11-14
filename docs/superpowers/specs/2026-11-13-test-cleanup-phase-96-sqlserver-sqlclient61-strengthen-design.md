# Test cleanup phase 96: tests/DatabaseTests/SqlServer.SqlClient61, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer.SqlClient61`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer.SqlClient61/SynchronousQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer.SqlClient61-1-01

- test: `tests/DatabaseTests/SqlServer.SqlClient61/SynchronousQueryTests.cs` | `SynchronousQueryTests.SetAccountStatusSync_NoRowOnAClosedConnection_IsClosedAgain` | rows: all
- action: strengthen
- evidence: read (SynchronousQueryTests.cs:31-39: the test calls `SetAccountStatusSync` on a closed connection with id 99, which no account has, and asserts only `connection.State == Closed` afterwards.  The query is `-> none` (tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql:25-30), so it returns nothing and changes no row, and the connection starts closed: a method that never opened the connection or ran the command leaves the same state and passes.  The name promises "closed again", that it was opened and closed, and the class comment (SynchronousQueryTests.cs:8-12) says each shape "runs ... through SqlClient 6.1's synchronous calls"; this is the project's one test of the `none` shape, and unlike the end-to-end project's `SetAccountStatusSync_GivenATransaction_IsUndoneByItsRollback` nothing here observes the update.  The other tests of the class read rows or a count, so they prove the command ran.  Searched the AGENTS.md files, the readmes, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test and its class: tests/DatabaseTests/README.md:49 describes the class only as running each shape's synchronous method, and nothing records this test's form as deliberate); blind: no
- cover: none
- proposal: Assert that the method opened and closed the connection, for example by recording `connection.StateChange` and asserting the states `Open` then `Closed`, so that the test fails when the synchronous `none` method does not run its command on SqlClient 6.1.
- verdict: confirmed
- verifier's reason: The finding reads the test correctly.  tests/DatabaseTests/SqlServer.SqlClient61/SynchronousQueryTests.cs:31-39 builds a `SqlConnection` that is never opened, calls `SetAccountStatusSync(new SetAccountStatusSyncArgs("suspended", 99))`, and asserts only `connection.State == Closed`.  The query, tests/DatabaseTests/SqlServer/EndToEnd/Queries/Methods.sql (`SetAccountStatusSync -> none`, `method-type=sync`), is an `UPDATE ... WHERE id = @accountId` that returns nothing, and id 99 matches no row.  So a method that never opened the connection or ran the command leaves the connection in the same state and passes.  The name ("IsClosedAgain") and the class comment (SynchronousQueryTests.cs:8-12: each shape "reads its rows, or runs, through SqlClient 6.1's synchronous calls") promise more than that.  The class's other tests read rows (GetAccountInRangeSync, FindAccountInRangeSync, ListAccountsWhereSync, :41-73) or a count (TouchAccountSync, :16-28, a `rowcount` shape on an open connection), so they show that their command ran.  This is the project's only test of the `none` shape, and nothing in it shows that its command ran.  The end-to-end project's `SetAccountStatusSync_GivenATransaction_IsUndoneByItsRollback` (tests/DatabaseTests/SqlServer/EndToEnd/SynchronousMethodTests.cs:61-77) does observe the write, but it runs on SqlClient 7, so it is not cover for the 6.1 floor.  Its `SetAccountStatusSync_NoRowOnAClosedConnection_IsClosedAgain` (:80-87) has the same weak form.  No mutation scope ran this slice (evidence: "None"), so the finding rests on reading alone, as it should.  I searched the AGENTS.md files, tests/DatabaseTests/README.md:49 (which says only that the class runs each shape's synchronous method on 6.1), docs/architecture, docs/tech-debt and docs/deferred for the test and its class.  Nothing records the assert-state-only form as deliberate.  `SqlConnection` raises `StateChange`, so the proposal (assert that the states go `Open` then `Closed`) is workable and makes the test fail when the command never runs.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer.SqlClient61`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
