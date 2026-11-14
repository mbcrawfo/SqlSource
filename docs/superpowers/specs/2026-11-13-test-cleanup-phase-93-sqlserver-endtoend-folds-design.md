# Test cleanup phase 93: tests/DatabaseTests/SqlServer/EndToEnd, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer/EndToEnd`
- Findings: 3 (3 fold)
- Kind: one kind, fold
- Changed lines: about 13
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer.EndToEnd-2-04

- test: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` | rows: `[InlineData("on")]`, line 345
- action: fold
- evidence: read (the row runs `InsertNoteDefaultLevelSync`, `transaction=on method-type=sync`, Queries/Batches.sql:462-464, through `InsertAtLevel`, SynchronousBatchTests.cs:1016; `transaction=on` is the default, site/sqlserver.md:506, so `InsertNoteSync`, Batches.sql:21-23, which sets no transaction, is the same method over the same insert.  `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`, :323-336, passes the same `DuplicateInTheSecondChunk()` items and makes the same four assertions as the row, :350-357: ItemIndex 1200, the duplicate key's number, an empty table and no open transaction.  The asynchronous twin, `BatchQueryTests.InsertNoteAtALevel_DuplicateKeyInTheSecondChunk_LeavesNothing`, BatchQueryTests.cs:493-498, has no `on` row.  The comment at :338 only says the theory runs "under on" too; no document names the row or the test); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`
- proposal: Drop the `on` row and the words "and under on" from the comment above the theory, as its asynchronous twin has it.
- verdict: confirmed
- verifier's reason: The row `[InlineData("on")]`, SynchronousBatchTests.cs:345, runs `InsertNoteDefaultLevelSync` (:1016), `transaction=on method-type=sync`, Queries/Batches.sql:462-464.  `transaction` is the enum `TransactionMode` (src/SqlSource/Settings/QuerySettings.cs:89), `On` by default (src/SqlSource/Settings/GeneratorParameters.cs:58) and no settings file of tests/DatabaseTests/SqlServer sets it, so `InsertNoteSync`, Batches.sql:21-23, gets the same mode and the same batch code (BatchWriter.cs:48 and :160 treat `On` alike whoever set it); site/sqlserver.md:506 states `on` is the default.  `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`, :323-336, passes the same `DuplicateInTheSecondChunk()` and asserts the same ItemIndex 1200, duplicate-key number, empty table and no open transaction as :354-357.  The differing output model (`IsolatedNote` against `BatchNote`) plays no part in the throw.  The asynchronous twin, BatchQueryTests.cs:492-498, has no `on` row.  No searched document names the row; the comment at :338 only lists it.  A fold that drops a row of a theory cannot be measured by a filter, since the rows share a name: confirmed by reading, and the cleanup phase's own guard proves it.
- condition: F-DatabaseTests.SqlServer.EndToEnd-2-06 names this theory as a second cover; the `on` row this fold drops is not what makes it one, since the `("on", "repeatable read")` row of that finding's own theory stays and keeps the `on` level.
- a cover: `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` is the cover of F-DatabaseTests.SqlServer.EndToEnd-2-06 (phase 93).  The change keeps every assertion and row that makes it so.

### F-DatabaseTests.SqlServer.EndToEnd-2-05

- test: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound` | rows: `("read committed", "read committed")`, `("repeatable read", "repeatable read")` and `("on", "read committed")`, made by `data.Add(level, "read committed")` and `data.Add(level, "repeatable read")` of `LevelsAndSessionLevels`, lines 1037-1038
- action: fold
- evidence: read (the theory asserts that the session is back at the level it was set to before the call, SynchronousBatchTests.cs:772, after a transaction of the method at another level.  In the three rows named the method's transaction runs at the session's own level, `LevelNumber` of both being 2, 3 and 2, :1046-1055, so a method that never set the level back passes them: they assert nothing of the restore.  Their other assertion, that the rows ran at the level, :771, is made by the other row of the same level in this theory and by `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`, :740-756.  The comment at :760 says each level runs from both levels "so that every level differs from one of them", which admits the equal pairs; no document names the test.  12 rows take 1.0 s on unpooled connections); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound` (its rows where the levels differ); `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`
- proposal: Have `LevelsAndSessionLevels` leave out a pair whose `LevelNumber`s are equal, so that every row starts from a level the method's transaction changes, and say so in its comment.
- verdict: confirmed
- verifier's reason: `LevelsAndSessionLevels`, SynchronousBatchTests.cs:1022-1042, pairs each level with "read committed" and "repeatable read"; with `LevelNumber`, :1046-1055, the pairs ("read committed", "read committed"), ("repeatable read", "repeatable read") and ("on", "read committed") start the session at the level the method's transaction runs at (2/2, 3/3, 2/2; docs/architecture/generator.md:699 confirms `on` begins at read committed).  The assertion of the restore, :772, then holds whether or not the method sets the level back, and every fault of the restore that these rows could see (a wrong number, a read after `BeginTransaction`, a skipped `SET`) leaves the session at a level that differs in the other rows of levels 2 and 3, which restore to 2 and 3 too.  The rows' other assertion, :771, is made for the same level by its row from the other session level and by `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`, :740-756.  The comment at :758-761 and :1020-1021 says each level runs from both "so that every level differs from one of them", a design note the proposal keeps true; no searched document names the test (README.md:185 describes the behaviour, which the remaining rows hold).  A fold that drops rows of a theory cannot be measured by a filter, since the rows share a name: confirmed by reading, and the cleanup phase's own guard proves it.

### F-DatabaseTests.SqlServer.EndToEnd-2-06

- test: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` | rows: `("read committed", "read committed")`, `("repeatable read", "repeatable read")` and `("on", "read committed")`, made by `LevelsAndSessionLevels`, lines 1037-1038
- action: fold
- evidence: read (as F-DatabaseTests.SqlServer.EndToEnd-2-05: in the rows named the session's level equals the level the method's transaction runs at, so the assertion that the level is set back after the rollback, SynchronousBatchTests.cs:792, holds without any restore.  Their other assertions, ItemIndex 2 and an empty table, :790-791, are made by the theory's other rows and by `InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing`, :339-358.  No document names the test.  12 rows take 1.0 s on unpooled connections); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` (its rows where the levels differ); `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing`
- proposal: The same change to `LevelsAndSessionLevels` as F-DatabaseTests.SqlServer.EndToEnd-2-05 drops these rows too.
- verdict: confirmed
- verifier's reason: The same `LevelsAndSessionLevels`, SynchronousBatchTests.cs:777, feeds `InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound`, :776-793: in the three equal pairs the session's level after the rollback, :792, equals `before` with no restore at all, so those rows prove nothing of the restore that the differing rows of levels 2 and 3 do not.  Their ItemIndex 2 and empty table, :790-791, are asserted by the same level's other row of this theory and, for the method's own transaction failing, by `InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing`, :339-358.  The comment at :775 records no limit or regression; no searched document names the test.  The proposal is the one change of F-05 to the shared data, which drops these rows too.  A fold that drops rows of a theory cannot be measured by a filter, since the rows share a name: confirmed by reading, and the cleanup phase's own guard proves it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer.EndToEnd-2-04 | fold | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` (rows: `[InlineData("on")]`, line 345) | `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing` |
| F-DatabaseTests.SqlServer.EndToEnd-2-05 | fold | `SynchronousBatchTests.InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound` (rows: `("read committed", "read committed")`, `("repeatable read", "repeatable read")` and `("on", "read committed")`, made by `data.Add(level, "read committed")` and `data.Add(level, "repeatable read")` of `LevelsAndSessionLevels`, lines 1037-1038) | `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound` (its rows where the levels differ); `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` |
| F-DatabaseTests.SqlServer.EndToEnd-2-06 | fold | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` (rows: `("read committed", "read committed")`, `("repeatable read", "repeatable read")` and `("on", "read committed")`, made by `LevelsAndSessionLevels`, lines 1037-1038) | `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` (its rows where the levels differ); `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`: `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/SqlServer/README.md`: 185

## Left out

- F-engines-09 (fold, `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing`): withdrawn, since a confirmed finding names its test as cover; the appendix keeps it as a **keep**.
- F-engines-09, which would drop the other five rows of this theory, is withdrawn (see the appendix): F-DatabaseTests.SqlServer.EndToEnd-2-06 names the theory as its cover.  The asynchronous theories of both engines' `BatchQueryTests` hold the same rows and have no finding; they stay.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
