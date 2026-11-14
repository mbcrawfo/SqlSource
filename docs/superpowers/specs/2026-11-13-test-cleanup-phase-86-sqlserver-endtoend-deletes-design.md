# Test cleanup phase 86: tests/DatabaseTests/SqlServer/EndToEnd, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer/EndToEnd`
- Findings: 3 (3 delete)
- Kind: one kind, delete
- Changed lines: about 28
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs`
- `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer.EndToEnd-2-03

- test: `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs` | `NoteQueryTests.ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds` | rows: all
- action: delete
- evidence: read (NoteQueryTests.cs:59-74 calls `ListAccountNotes` and asserts the pairs `(Id, AccountId)` of the four rows, carol's AccountId null; `ListAccountNotes_AnAccountWithNoNote_HasNulls`, :23-54, calls the same method with no arguments and asserts the whole of every row, the same `AccountId` values and carol's `null` among them.  The one difference is a closed connection in place of an open one, and neither test asserts anything about the connection; the class comment, :14-15, spreads open and closed connections over its reads, and the connection rule is held by `RunTimeArgumentTests`.  No document names the test or says why it is apart; its comment, :56-58, explains the conversion it reads); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs` | `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls`
- proposal: Delete the test and move its comment, that the ids are read through `AccountId`'s FromDatabase and carol's NULL is never given to it, onto the cover, or give the cover the closed connection if the class should keep its half of closed reads.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice.  NoteQueryTests.cs:59-74 calls `ListAccountNotes` on a closed connection and asserts the pairs `(Id, AccountId)` of the four rows, carol's `AccountId` null.  `ListAccountNotes_AnAccountWithNoNote_HasNulls`, :23-54, calls the same method with no arguments and asserts every row whole as `ListAccountNotesDto` records, whose first and fourth members are the same `new AccountId(1)`, `(1)`, `(2)`, `(3)` and carol's `null` (:52), so a failure of `AccountId`'s FromDatabase or a NULL handed to it fails the cover too.  The only difference is the closed connection, which neither test asserts on; the opening of a closed connection is the generated method's common path, held by `RunTimeArgumentTests.ClosedConnection_AfterTheMethod_IsClosed` and by the other closed reads of the class (`CountAccountNotes`, :78-90).  The class comment, :14-16, describes a spread of open and closed reads, not a record; the test's comment, :56-58, explains the conversion, which the proposal moves.  `grep -rn` over the searched documents finds the name nowhere (only docs/superpowers/plans, a phase plan that specified it).

### F-engines-04

- test: `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_FolderOverride_ReturnsDateToDateTime` | rows: all
- action: delete
- evidence: read (twin fault of F-DatabaseTests.Postgres.EndToEnd-1-13; the SqlServer.EndToEnd-2 slice has no finding on it.  ModelShapeTests.cs:495-497 asserts that `Billing.BillingInvoicesRow` contains `("IssuedOn", DateTime, false)`.  `Build_TableOfAnotherSchema_IsNamedWithItsSchema` (:433-445) asserts by `ShouldBeModel`, which compares the whole property list (TestHarness/EndToEnd/ModelReflection.cs:75-85), that the same model has exactly its six properties, `("IssuedOn", typeof(DateTime), false)` among them; without the folder's `"date": "default"` (EndToEnd/Queries/Billing/sqlsource.json) it would be the project's `DateOnly` (as `OpenedOn` is, ModelShapeTests.cs:29) and the cover fails.  No comment above the test; README.md:168 describes `ModelShapeTests` in general; nothing in the searched documents names it.  It needs no server; no mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_TableOfAnotherSchema_IsNamedWithItsSchema`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ModelShapeTests.cs:495-497 (tests/DatabaseTests/SqlServer/EndToEnd/) asserts `BillingInvoicesRow` contains `("IssuedOn", DateTime, false)`.  `Build_TableOfAnotherSchema_IsNamedWithItsSchema` (:433-445) asserts through `ShouldBeModel`, which compares the whole typed property list (TestHarness/EndToEnd/ModelReflection.cs:75-85), the six properties, `("IssuedOn", typeof(DateTime), false)` among them.  The project's sqlsource.json sets `"date": "System.DateOnly"` and Queries/Billing/sqlsource.json `"date": "default"`, so without the folder override the property is `DateOnly` and the cover fails.  No comment above the test; README.md:168 describes `ModelShapeTests` in general only; my grep of every `.md` outside `artifacts/` finds the name nowhere.  No server, no mutation scope.  Twin of F-DatabaseTests.Postgres.EndToEnd-1-13, confirmed.

### F-engines-05

- test: `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_QueryOverride_ReturnsDateToDateTime` | rows: all
- action: delete
- evidence: read (twin fault of F-DatabaseTests.Postgres.EndToEnd-1-14; the SqlServer.EndToEnd-2 slice has no finding on it.  ModelShapeTests.cs:499-501 asserts that `AllTypesDto` contains `("DateValue", DateTime?, true)`.  `Build_AllTypes_IsASealedRecordOfEveryColumn` (:518-525) asserts by `ShouldBeModel` every property of `AllTypesDto` against `SqlServerAllTypesRows.AllTypesProperties`, which holds `("DateValue", typeof(DateTime?), true)` (TestHarness/EndToEnd/SqlServerAllTypesRows.cs:37); without the query's `-- type-override: date = default` (Queries/AllTypes.sql:11) it would be the project's `DateOnly?` and the cover fails.  `Build_QueryOverrideOfSys_ReturnsTimeToTimeSpan` (:347) stays.  No comment above the test; nothing in the searched documents names it.  It needs no server; no mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ModelShapeTests.cs:499-501 asserts `AllTypesDto` contains `("DateValue", DateTime?, true)`.  `Build_AllTypes_IsASealedRecordOfEveryColumn` (:518-525) asserts by `ShouldBeModel` the whole list against `SqlServerAllTypesRows.AllTypesProperties`, which holds `("DateValue", typeof(DateTime?), true)` (TestHarness/EndToEnd/SqlServerAllTypesRows.cs:37); the query's `-- type-override: date = default` (Queries/AllTypes.sql:11) is what makes it so against the project's `System.DateOnly` (tests/DatabaseTests/SqlServer/sqlsource.json), so the cover fails without it.  `Build_QueryOverrideOfSys_ReturnsTimeToTimeSpan` (:346-355) keeps a query override's own test.  No comment above the test; EndToEnd/AGENTS.md:50 only says ModelShapeTests compares with `AllTypesProperties`, which the cover does; the name is in no document.  Twin of F-DatabaseTests.Postgres.EndToEnd-1-14, confirmed.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer.EndToEnd-2-03 | delete | `NoteQueryTests.ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds` | `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs`: `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls` |
| F-engines-04 | delete | `ModelShapeTests.Build_FolderOverride_ReturnsDateToDateTime` | `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs`: `ModelShapeTests.Build_TableOfAnotherSchema_IsNamedWithItsSchema` |
| F-engines-05 | delete | `ModelShapeTests.Build_QueryOverride_ReturnsDateToDateTime` | `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs`: `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/SqlServer/README.md`: 7, 168, 173, 191
- `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`: 50

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
