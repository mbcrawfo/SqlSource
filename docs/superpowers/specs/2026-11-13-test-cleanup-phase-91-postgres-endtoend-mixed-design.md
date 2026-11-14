# Test cleanup phase 91: tests/DatabaseTests/Postgres/EndToEnd, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/EndToEnd`
- Findings: 13 (12 delete, 1 fold)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 182
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs`
- `tests/DatabaseTests/Postgres/README.md`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.EndToEnd-1-01

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsArray_EveryAccount_ReturnsAnArray` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:21-30 calls `ListAsArray`, of `Queries/Collections.sql`, `-- collection-type: Array` with the default array pool, on an open connection and asserts the three rows; `RowBufferQueryTests.cs:37-42` calls `NumbersAsArray`, of `Queries/RowBuffers.sql`, the same marker and the same default pool, and `RowBufferReads.cs` asserts `ShouldBeOfType<CountedNumber[]>` and the values at seven counts, even counts on an open connection and odd on a closed one; the declared type is held by `MethodShapeTests.ReturnType_OfEachCollectionType_IsThatType`, MethodShapeTests.cs:39.  No AGENTS.md, readme, `docs/tech-debt` or `docs/deferred` names the test; `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md:24` says a check of what every collection type returns goes in `RowBufferReads`; README.md:150 only lists the class among those that run queries); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test; the method and its query stay, since `MethodShapeTests` and `StreamingQueryTests` read them.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence:38-40), so judged by reading under the named-cover rule.  `ListAsArray` (Queries/Collections.sql:4-8) and `NumbersAsArray` (Queries/RowBuffers.sql:7-11) are both `-- collection-type: Array` markers over the project's `IReadOnlyList` (Postgres/sqlsource.json), neither sets `array-pool`, which is on by default (src/SqlSource/Settings/GeneratorParameterList.cs:72), so both read through the same pooled row buffer.  The cover, RowBufferQueryTests.cs:35-42 with TestHarness/EndToEnd/RowBufferReads.cs:63-90, asserts `ShouldBeOfType<CountedNumber[]>` and the rows at seven counts, even on an open connection, as the deleted test (CollectionQueryTests.cs:20-30) uses.  The two-column model and the parameterless method are no part of the collection behaviour, and StreamingQueryTests.cs:43,249,342,420 also assert `ListAsArray`'s three accounts.  The declared type stays in MethodShapeTests.cs:36-49, which also keeps the marker-over-settings precedence that tests/DatabaseTests/Postgres/README.md:123 describes.  Searches: README.md:150 only lists the class; EndToEnd/AGENTS.md:24 places checks of what every collection type returns in `RowBufferReads`; no AGENTS.md, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names the test; only `docs/superpowers/` plans and specs do, which are no record.  No comment above the test; the class comment (CollectionQueryTests.cs:10-13) is descriptive.

### F-DatabaseTests.Postgres.EndToEnd-1-02

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsIEnumerable_EveryAccount_ReturnsAnIEnumerable` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:33-42 asserts `ShouldBeOfType<CollectedAccount[]>` and the rows on a closed connection; RowBufferQueryTests.cs:46-51 with `RowBufferReads.cs` asserts `ShouldBeOfType<CountedNumber[]>` and the rows for the same marker and default pool, on open and closed connections; searches as in F-DatabaseTests.Postgres.EndToEnd-1-01 found no record of the test); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:32-42 asserts `ShouldBeOfType<CollectedAccount[]>` and the rows on a closed connection.  RowBufferQueryTests.cs:44-51 runs `NumbersAsIEnumerable` (same marker, default pool) through `ShouldReadOneToTheCountAsync<CountedNumber[]>`, RowBufferReads.cs:82-84, at odd counts on a closed connection.  Same type assertion, same generated path, same engine.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-03

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsIReadOnlyCollection_EveryAccount_ReturnsAnIReadOnlyCollection` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:45-54 asserts `ShouldBeOfType<CollectedAccount[]>` and the rows; RowBufferQueryTests.cs:55-62 with `RowBufferReads.cs` asserts the array type and the rows for the same marker and default pool, on open and closed connections; no record of the test found); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:44-54 asserts the array type and the rows on an open connection; RowBufferQueryTests.cs:53-62 asserts the same type through RowBufferReads.cs:82 and the rows at every count, open and closed, for the same marker and default pool.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-04

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsIReadOnlyList_EveryAccount_ReturnsAnIReadOnlyList` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:57-66 asserts `ShouldBeOfType<CollectedAccount[]>` and the rows; RowBufferQueryTests.cs:66-72 with `RowBufferReads.cs` asserts the array type and the rows for the same marker and default pool, on open and closed connections; no record of the test found); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:56-66 asserts the array type and the rows on a closed connection; RowBufferQueryTests.cs:64-72 asserts the same through RowBufferReads.cs:82-84, open and closed.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-05

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsICollection_EveryAccount_ReturnsAnICollection` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:69-78 asserts `ShouldBeOfType<List<CollectedAccount>>` and the rows; RowBufferQueryTests.cs:94-100 with `RowBufferReads.ShouldReadOneToTheCountIntoTheCallersListAsync` asserts `ShouldBeOfType<List<CountedNumber>>`, the rows, and more (capacity, the caller's list alone) for the same marker and default pool, on open and closed connections; no record of the test found); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsICollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:68-78 asserts `ShouldBeOfType<List<CollectedAccount>>` and the rows; RowBufferQueryTests.cs:92-100 through `ShouldReadOneToTheCountIntoTheCallersListAsync` (RowBufferReads.cs:36-46, 82-85, 94-105) asserts `List<CountedNumber>`, the rows, and capacity and ownership besides, open and closed.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-06

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsIList_EveryAccount_ReturnsAnIList` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:81-90 asserts `ShouldBeOfType<List<CollectedAccount>>` and the rows; RowBufferQueryTests.cs:85-90 with `RowBufferReads.ShouldReadOneToTheCountIntoTheCallersListAsync` asserts the List type and the rows, and more, for the same marker and default pool, on open and closed connections; no record of the test found); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsIList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:80-90 asserts the List type and the rows on a closed connection; RowBufferQueryTests.cs:83-90 asserts the List type, the rows and more (RowBufferReads.cs:36-46, 94-105) at every count, open and closed.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-07

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsList_EveryAccount_ReturnsAList` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:93-102 asserts the rows; RowBufferQueryTests.cs:76-81 with `RowBufferReads.ShouldReadOneToTheCountIntoTheCallersListAsync` asserts the List type, the rows, and more, for the same marker and default pool, on open and closed connections; no record of the test found); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:92-102 asserts only the rows (the List type is the declared one); RowBufferQueryTests.cs:74-81 asserts the List type, the rows and the caller's-list rules, open and closed, for the same marker and default pool.  Searches as in 01 found no record.

### F-DatabaseTests.Postgres.EndToEnd-1-08

- test: `tests/DatabaseTests/Postgres/EndToEnd/CollectionQueryTests.cs` | `CollectionQueryTests.ListAsImmutableArray_EveryAccount_ReturnsAnImmutableArray` | rows: all
- action: delete
- evidence: read (CollectionQueryTests.cs:105-114 asserts the rows; RowBufferQueryTests.cs:104-109 with `RowBufferReads.ShouldReadOneToTheCountIntoAnImmutableArrayAsync` asserts the rows, that the array is never the default and `Empty` for no rows, for the same marker and default pool, on open and closed connections; no record of the test found.  `ListAsImmutableList` and `ListAsIImmutableList`, which read through no row buffer, have no such cover and stay); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs` | `RowBufferQueryTests.NumbersAsImmutableArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`
- proposal: Delete the test, leaving `CollectionQueryTests` with the two immutable-list tests, and update the class comment and the README's description of it.
- verdict: confirmed
- verifier's reason: CollectionQueryTests.cs:104-114 asserts the rows of `ListAsImmutableArray`; RowBufferQueryTests.cs:102-109 through `ShouldReadOneToTheCountIntoAnImmutableArrayAsync` (RowBufferReads.cs:49-59, 109-119) asserts the type, the rows, never-default and `Empty` at count 0, open and closed.  `ListAsImmutableList` and `ListAsIImmutableList` (CollectionQueryTests.cs:116-138) are not among RowBufferQueryTests' eight types and rightly stay.  Searches as in 01 found no record; the proposal's comment and README updates are the right follow-through.
- condition: Update the class comment of `CollectionQueryTests` and the description of it in `tests/DatabaseTests/Postgres/README.md`, as the proposal says.

### F-DatabaseTests.Postgres.EndToEnd-1-10

- test: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` | `AllTypesQueryTests.Int4Array_Null_IsNull` | rows: all
- action: delete
- evidence: read (AllTypesQueryTests.cs:309-321 inserts row 1's values with `Int4ArrayValue = null` through `InsertEveryType` and asserts the RETURNING row's array is null; `EveryTypeTests.InsertEveryType_NullRow_ReadsEveryNullBack`, EveryTypeTests.cs:50-73, inserts row 2, whose `Int4ArrayValue` is null with every other column, through the same method and `AllTypesRows.ShouldBeTheNullRow` asserts every property but the id null, in the RETURNING row and on a read back.  The README, tests/DatabaseTests/Postgres/README.md:167, lists null among the kinds sent and records no limit or decision; no AGENTS.md, `docs/tech-debt` or `docs/deferred` names the test); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs` | `EveryTypeTests.InsertEveryType_NullRow_ReadsEveryNullBack`
- proposal: Delete the test, and drop "and null" from the README's sentence on `AllTypesQueryTests` or say there that `EveryTypeTests` sends it.
- verdict: confirmed
- verifier's reason: AllTypesQueryTests.cs:309-321 inserts row 1 with `Int4ArrayValue = null` and asserts the RETURNING array is null.  The null arm of the binding is the `is { } value ? ... : null` match (ParameterBinder.cs:136-148), independent of the other columns.  `EveryTypeTests.InsertEveryType_NullRow_ReadsEveryNullBack` (EveryTypeTests.cs:50-73) sends row 2, whose `Int4ArrayValue` is null, through the same `InsertEveryType`, and `AllTypesRows.ShouldBeTheNullRow` (AllTypesRows.cs:169-175) asserts every property but the id null, in RETURNING and on a read back.  No comment above the test; README.md:167 lists "and null" descriptively, which the proposal updates; no AGENTS.md, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names it.
- condition: Drop "and null" from the sentence on `AllTypesQueryTests` at `tests/DatabaseTests/Postgres/README.md:167`, or say there that `EveryTypeTests` sends it; the same sentence F-DatabaseTests.Postgres.EndToEnd-1-09 changes.

### F-DatabaseTests.Postgres.EndToEnd-2-01

- test: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs` | `SynchronousMethodTests.TouchAccountSync_OneRowInATransaction_Returns1` | rows: all
- action: delete
- evidence: read (no mutation scope ran the slice.  SynchronousMethodTests.cs:29-42 calls `TouchAccountSync(new TouchAccountSyncArgs(1), transaction)` on an open connection in a transaction, then asserts `count == 1`, that the row's xmin is the current transaction's, and `State == Open`, then rolls back.  SynchronousMethodTests.cs:242-259, `TouchAccountSync_InATransaction_IsUndoneByItsRollback`, makes the same call on the same row, connection and transaction and asserts `count == 1`, that the xmin the transaction reads differs from the one before (the write was made and seen in the transaction), `State == Open`, and also that the rollback restores the old xmin, which shows the write was in the caller's transaction.  So the cover asserts everything this test does, and more.  There is no comment above this test.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name finds nothing.  tests/DatabaseTests/Postgres/README.md:156 describes the class only in general terms ("each shape with the rows it allows ... a transaction whose rollback undoes the write"), and the cover still holds both of those.); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs` | `SynchronousMethodTests.TouchAccountSync_InATransaction_IsUndoneByItsRollback`
- proposal: Delete the test, along with `UpdatedInThisTransaction` (SynchronousMethodTests.cs:376-386) if `TouchAccountQuietlySync_InATransaction_UpdatesTheRow` is its only remaining caller.  `TouchAccountSync_NoRowOnAClosedConnection_Returns0AndIsClosedAgain` keeps the zero-row case of the shape.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence: "## Mutation scopes: None"), so step 2 is skipped and the named-cover rule holds.  The test (tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs:29-42) calls `TouchAccountSync(new TouchAccountSyncArgs(1), transaction)` on an open connection of `EndToEndSources.App` and asserts `count == 1`, `UpdatedInThisTransaction` (xmin equals the current transaction's id, :377-386) and `State == Open`.  The cover, `TouchAccountSync_InATransaction_IsUndoneByItsRollback` (:242-259), is on the same engine, source, row 1 and kind of transaction, and asserts `count == 1`, xmin read in the transaction differs from the one before (:252, :256), `State == Open` (:257), and that the rollback restores the old xmin (:258).  A write outside the caller's transaction would have committed and left a new xmin after the rollback, so the cover fails on everything the test's xmin check fails on.  Nothing mutation cannot see rests on the test alone: no wording, wait, budget or engine behaviour the cover does not also exercise.  No comment stands above the test (:29); the group comment (:14-20) describes the class only.  `grep -rn` over every `*.md` of the repository finds the test's name nowhere; the class is named only by tests/DatabaseTests/Postgres/README.md:156, which describes "a transaction whose rollback undoes the write" in general terms that the cover still holds, and by the plans of docs/superpowers/plans/, which are not records of a limit, regression or decision.  `git log -S` shows the test came with the class (5b6c2935), not as a regression.  One correction to the proposal: `UpdatedInThisTransaction` must stay, since `TouchAccountQuietlySync_InATransaction_UpdatesTheRow` (:65) still calls it; only the test goes.
- condition: The verifier corrects the proposal: the helper `UpdatedInThisTransaction` stays, since `TouchAccountQuietlySync_InATransaction_UpdatesTheRow` still calls it; only the test goes.

### F-DatabaseTests.Postgres.EndToEnd-2-02

- test: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` | rows: all ("read committed" :420, "repeatable read" :421, "serializable" :422)
- action: delete
- evidence: read (no mutation scope ran the slice.  On PostgreSQL the copies at each level differ from `InsertNoteSync` (Batches.sql:19-21, `transaction=on`) in one place only: the argument that `BatchWriter.AppendBegin` passes to `BeginTransaction`, `global::System.Data.IsolationLevel.<level>` (src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs:142-146, 155-167).  The read and restore of the session's isolation level is SQL Server's alone (`RestoresSessionIsolation`, ExecutionCapabilityWriter.cs:28-29, 227-250).  Chunking, the catch, the rollback and the `CollectionItemException` at item 1,200 are the same code as for `InsertNoteSync`, which `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing` (SynchronousBatchTests.cs:383-397) proves with the same input, `DuplicateInTheSecondChunk()`, and the same four assertions: ItemIndex 1200, 23505 inside, no notes left, no transaction left open.  That each copy begins its transaction at its level is proved by `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` (:561-575), which reads `transaction_isolation` back.  The comment above the theory (:418), "The same failure under each isolation level that PostgreSQL has", describes the rows and records no limit, regression or decision.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds the name nowhere.  tests/DatabaseTests/Postgres/README.md:157 lists "a duplicate key in the second chunk under the method's own transaction, each isolation level and `transaction=off`" as part of what the class covers.  The theory runs three 1,500-item batches, 0.102 s, the slowest of the class.  BatchQueryTests.cs:397-402 has the same asynchronous theory, which is outside this slice.  SQL Server's theory is not affected, because its levels also restore the session's level.); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`; `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`
- proposal: Delete the theory and remove "each isolation level" from the duplicate-key clause of tests/DatabaseTests/Postgres/README.md:157.  The isolation copies and their `VariantCopyTests` rows stay, since `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` still uses them.  Raise the asynchronous twin in `BatchQueryTests` with the same reasoning in its own slice.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice, so the finding stands on reading.  The theory (tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs:418-436) runs `DuplicateInTheSecondChunk()` (:702-707) through `InsertAtLevel` (:688-699) and asserts ItemIndex 1200, inner 23505, `CountNotes == 0` and `ShouldBeInNoTransaction`; the cover `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing` (:381-397) makes the same four assertions on the same input, same temporary table and same engine.  The level copies (tests/DatabaseTests/Postgres/EndToEnd/Queries/Batches.sql:103-146) are `-> one`, the shape of `InsertNoteSync` (:19), and differ in the generated method only by the isolation argument of `AppendBegin` (src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs:142-146, 155-167) and the columns of their output model; `ExecutionCapabilityRules.OfEngine` (src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityRules.cs:110-112) gives every PostgreSQL batch the same capability, `FindsFailedItemByCompletion`, and `RestoresSessionIsolation` only to SQL Server (:121-125), so chunking, the catch, the rollback and the item's exception are one code path.  That each copy begins its transaction at its level is held by `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` (:560-575), which reads `transaction_isolation` back and calls `ShouldBeInNoTransaction`.  What the engine does is the only thing mutation cannot see here: a duplicate key in one session raises 23505 and rolls back at every level, with no concurrency in the test to make serializable differ, so the theory guards no engine behaviour the cover does not.  The comment above it (:418) and the group comment (:14-25) describe the rows and record no limit, regression or decision; `grep -rn` over every `*.md` finds the name nowhere, and tests/DatabaseTests/Postgres/README.md:157 lists "each isolation level" only as coverage, which the proposal edits.  `git log -S` shows the theory came with the class (7c289a6e), from the plan's coverage list (docs/superpowers/plans/2026-11-11-sync-methods-part-2.md:89), not as a regression.
- condition: Wording corrected (run-notes, mirrors): the evidence says SQL Server's theory is not affected because its levels restore the session's level, but SQL Server's `InsertNoteSync` (`transaction=on`) restores it too, as F-engines-09 shows.  The delete stands on PostgreSQL's own reading.  Remove "each isolation level" from the duplicate-key clause of `tests/DatabaseTests/Postgres/README.md:157`.

### F-engines-08

- test: `tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs` | `RunTimeTests.TouchAccount_RowLockedPastACommandTimeoutOfOneSecond_Throws` | rows: all
- action: delete
- evidence: read (twin fault of F-DatabaseTests.SqlServer.EndToEnd-2-02; the Postgres.EndToEnd-2 slice has no finding on it.  RunTimeTests.cs:58-89 holds the row of account `LockedAccountId` `FOR UPDATE`, runs the asynchronous `TouchAccount` with `commandTimeout: 1` on an `AppWithoutCommandTimeout` connection through `Eventually.WithinAsync`, and asserts an `NpgsqlException` whose inner exception is a `TimeoutException`, and the connection closed.  `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows` (SynchronousMethodTests.cs:261-306) makes the same asynchronous call first, as `expected`, under the same lock (`RunTimeTests.HoldAsync`) and source, and asserts the same `NpgsqlException` type and `expected.InnerException.ShouldBeOfType<TimeoutException>()` (:304); it does not assert the asynchronous connection's state, which is closed by the same `finally` that `RunTimeTests.GetAccountInRange_ClosedConnectionAndAnException_IsClosedAgain` proves.  The timeout costs 1.0 s per run and the cover pays it again (2.0 s).  No AGENTS.md, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test; tests/DatabaseTests/Postgres/README.md:158 credits `RunTimeTests` with the `commandTimeout`, and :156 already says the synchronous test throws the asynchronous method's exception; the comment above it (:58-60) explains the setup.  No mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs` | `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows`
- proposal: Add `asyncConnection.State.ShouldBe(ConnectionState.Closed)` to the cover, delete the test, and move the `commandTimeout` clause of README.md:158 to `SynchronousMethodTests`, as F-DatabaseTests.SqlServer.EndToEnd-2-02 does for SQL Server.
- verdict: confirmed
- verifier's reason: No mutation scope.  tests/DatabaseTests/Postgres/EndToEnd/RunTimeTests.cs:58-89 holds account 2 `FOR UPDATE` through `HoldAsync`, runs the asynchronous `TouchAccount(commandTimeout: 1)` on an `AppWithoutCommandTimeout` connection under `Eventually.WithinAsync`, and asserts `NpgsqlException`, an inner `TimeoutException` and the connection closed.  `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows` (SynchronousMethodTests.cs:266-306) makes the same asynchronous call first, as `expected`, from the same source kind, under the same lock (`RunTimeTests.HoldAsync`, same SQL and id), asserts it throws `NpgsqlException` (:281-291) and `expected.InnerException.ShouldBeOfType<TimeoutException>()` (:304); only `asyncConnection.State` is not asserted, which the proposal adds, and which `RunTimeTests.GetAccountInRange_ClosedConnectionAndAnException_IsClosedAgain` (:154-166) also holds for the same `finally`.  With that line the cover asserts every fact of the test.  Records: the comment above the test (:58-60) explains the setup; the class comment records the lock-not-timer choice, which the cover keeps.  `grep` over the AGENTS.md files, readmes, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds the name nowhere; it is only in the plan docs/superpowers/plans/2026-11-06-td-0112-pg-run-time-tests-on-locks.md:55, which moved it onto a lock, a decision the cover also follows.  README.md:158 credits `RunTimeTests` with the timeout and :156 already says the synchronous test throws the asynchronous one's exception, so the proposal's readme move is right.  Twin of F-DatabaseTests.SqlServer.EndToEnd-2-02, confirmed.
- condition: Add `asyncConnection.State.ShouldBe(ConnectionState.Closed)` to the cover before deleting, and move the `commandTimeout` clause of `tests/DatabaseTests/Postgres/README.md:158` to `SynchronousMethodTests`.

### F-DatabaseTests.Postgres.EndToEnd-1-09

- test: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` | `AllTypesQueryTests.Int4Array_EachKindOfSequence_RoundTrips` | rows: `("Array", true)` line 265; `("Lazy", true)` line 269
- action: fold
- evidence: read (the generated binding of an array parameter is `value as int[] ?? Enumerable.ToArray(value)`, src/SqlSource/Generation/Emitting/ParameterBinder.cs:161-162.  The "Array" row sends `int[] {1, 2, 3}` through `InsertEveryType` and asserts it back; `EveryTypeTests.InsertEveryType_FullRow_ReadsEveryValueBack`, EveryTypeTests.cs:26-48, sends row 1's `int[]`, which is `{1, 2, 3}` (TestHarness/EndToEnd/AllTypesRows.cs:127), through the same method and asserts it in the RETURNING row and on a read back.  The "Lazy" row sends a LINQ `Select` over a range, which is no array and goes through `ToArray`; `Int4Array_LazySequence_IsEnumeratedOnce`, AllTypesQueryTests.cs:293-307, sends a `CountingSequence`, also no array, through the same method and asserts the values read back and one enumeration.  The comment above the theory and the README, tests/DatabaseTests/Postgres/README.md:167, describe the kinds sent and record no limit or decision; no AGENTS.md, `docs/tech-debt` or `docs/deferred` names the test); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs` | `EveryTypeTests.InsertEveryType_FullRow_ReadsEveryValueBack`; `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` | `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce`
- proposal: Drop the rows "Array" and "Lazy" and their arms of `SequenceOf`, keeping List, ImmutableArray and HashSet.
- verdict: confirmed
- verifier's reason: The binding is `value as int[] ?? Enumerable.ToArray(value)` (src/SqlSource/Generation/Emitting/ParameterBinder.cs:159-162).  The "Array" row (AllTypesQueryTests.cs:256, 265) sends `int[] {1,2,3}` through `InsertEveryType` with row 1's other values; `EveryTypeTests.InsertEveryType_FullRow_ReadsEveryValueBack` (EveryTypeTests.cs:25-48) sends row 1 through `ArgsFrom` (AllTypesRows.cs:180), whose `Int4ArrayValue` is the `int[] {1,2,3}` read back from the table (AllTypesRows.cs:127), through the same method, and asserts it in RETURNING and on a read back: the same input, the same arm.  The "Lazy" row (line 260, 269) is no `int[]` and takes the `ToArray` arm, which the List, ImmutableArray and HashSet rows take too, and `Int4Array_LazySequence_IsEnumeratedOnce` (lines 293-307) sends a non-array `CountingSequence` through it and asserts values and one enumeration.  The comment above the theory (lines 251-252) and README.md:167 describe the kinds and record no limit or decision; no AGENTS.md, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names the test (TD-0089 names only SQL Server's `ExecuteAsync_LeftOutColumnAlone_FailsToWriteItsValue`).  A fold that drops rows of a theory cannot be measured by a filter, so this is confirmed by reading; the cleanup phase's own guard proves it.  Note for the writer: tests/DatabaseTests/Postgres/README.md:167 lists "an array" and "a lazy sequence" among the kinds `AllTypesQueryTests` sends, so that sentence changes in the same change, which the proposal does not say.
- condition: The verifier adds: `tests/DatabaseTests/Postgres/README.md:167` lists "an array" and "a lazy sequence" among the kinds `AllTypesQueryTests` sends, so that sentence changes in the same change.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.EndToEnd-1-01 | delete | `CollectionQueryTests.ListAsArray_EveryAccount_ReturnsAnArray` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-02 | delete | `CollectionQueryTests.ListAsIEnumerable_EveryAccount_ReturnsAnIEnumerable` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-03 | delete | `CollectionQueryTests.ListAsIReadOnlyCollection_EveryAccount_ReturnsAnIReadOnlyCollection` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-04 | delete | `CollectionQueryTests.ListAsIReadOnlyList_EveryAccount_ReturnsAnIReadOnlyList` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-05 | delete | `CollectionQueryTests.ListAsICollection_EveryAccount_ReturnsAnICollection` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsICollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-06 | delete | `CollectionQueryTests.ListAsIList_EveryAccount_ReturnsAnIList` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsIList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-07 | delete | `CollectionQueryTests.ListAsList_EveryAccount_ReturnsAList` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-08 | delete | `CollectionQueryTests.ListAsImmutableArray_EveryAccount_ReturnsAnImmutableArray` | `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`: `RowBufferQueryTests.NumbersAsImmutableArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes` |
| F-DatabaseTests.Postgres.EndToEnd-1-09 | fold | `AllTypesQueryTests.Int4Array_EachKindOfSequence_RoundTrips` (rows: `("Array", true)` line 265; `("Lazy", true)` line 269) | `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs`: `EveryTypeTests.InsertEveryType_FullRow_ReadsEveryValueBack`; `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`: `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce` |
| F-DatabaseTests.Postgres.EndToEnd-1-10 | delete | `AllTypesQueryTests.Int4Array_Null_IsNull` | `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs`: `EveryTypeTests.InsertEveryType_NullRow_ReadsEveryNullBack` |
| F-DatabaseTests.Postgres.EndToEnd-2-01 | delete | `SynchronousMethodTests.TouchAccountSync_OneRowInATransaction_Returns1` | `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs`: `SynchronousMethodTests.TouchAccountSync_InATransaction_IsUndoneByItsRollback` |
| F-DatabaseTests.Postgres.EndToEnd-2-02 | delete | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` | `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`; `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` |
| F-engines-08 | delete | `RunTimeTests.TouchAccount_RowLockedPastACommandTimeoutOfOneSecond_Throws` | `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs`: `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Edits the findings and their verdicts ask for:

- F-DatabaseTests.Postgres.EndToEnd-1-08: Update the class comment of `CollectionQueryTests` and the description of it in `tests/DatabaseTests/Postgres/README.md`, as the proposal says.
- F-DatabaseTests.Postgres.EndToEnd-1-09: The verifier adds: `tests/DatabaseTests/Postgres/README.md:167` lists "an array" and "a lazy sequence" among the kinds `AllTypesQueryTests` sends, so that sentence changes in the same change.
- F-DatabaseTests.Postgres.EndToEnd-1-10: Drop "and null" from the sentence on `AllTypesQueryTests` at `tests/DatabaseTests/Postgres/README.md:167`, or say there that `EveryTypeTests` sends it; the same sentence F-DatabaseTests.Postgres.EndToEnd-1-09 changes.
- F-DatabaseTests.Postgres.EndToEnd-2-02: Wording corrected (run-notes, mirrors): the evidence says SQL Server's theory is not affected because its levels restore the session's level, but SQL Server's `InsertNoteSync` (`transaction=on`) restores it too, as F-engines-09 shows.  The delete stands on PostgreSQL's own reading.  Remove "each isolation level" from the duplicate-key clause of `tests/DatabaseTests/Postgres/README.md:157`.
- F-engines-08: Add `asyncConnection.State.ShouldBe(ConnectionState.Closed)` to the cover before deleting, and move the `commandTimeout` clause of `tests/DatabaseTests/Postgres/README.md:158` to `SynchronousMethodTests`.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/Postgres/README.md`: 150, 156, 157, 158, 167
- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 10
- `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`: 44

## Left out

- The asynchronous twin, PostgreSQL's `BatchQueryTests.InsertNoteAtALevel_DuplicateKeyInTheSecondChunk_LeavesNothing`, has the same rows and no finding: the proposal leaves it to a later review, and this phase does not change it.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
