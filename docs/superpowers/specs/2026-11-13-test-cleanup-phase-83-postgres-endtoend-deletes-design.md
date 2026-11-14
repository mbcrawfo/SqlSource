# Test cleanup phase 83: tests/DatabaseTests/Postgres/EndToEnd, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/EndToEnd`
- Findings: 4 (4 delete)
- Kind: one kind, delete
- Changed lines: about 42
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/NoteQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.EndToEnd-1-12

- test: `tests/DatabaseTests/Postgres/EndToEnd/NoteQueryTests.cs` | `NoteQueryTests.ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds` | rows: all
- action: delete
- evidence: read (NoteQueryTests.cs:52-69 calls `ListAccountNotes` and asserts each row's `(Id, AccountId)`, `AccountId(1..3)` and carol's null; `ListAccountNotes_AnAccountWithNoNote_HasNulls`, NoteQueryTests.cs:18-47, calls the same method on the same seed and asserts every whole row by record equality, those same `AccountId` values and carol's null `AccountId` included.  The one difference is a closed connection against an open one, which every other class of the folder also runs.  The comment above the test explains the converter and records no limit or decision; no AGENTS.md, readme, `docs/tech-debt` or `docs/deferred` names the test); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/NoteQueryTests.cs` | `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls`
- proposal: Delete the test, moving its comment on reading `AccountId` through `FromDatabase` and never passing carol's NULL to it onto the cover.
- verdict: confirmed
- verifier's reason: NoteQueryTests.cs:52-69 asserts each row's `(Id, AccountId)` from `ListAccountNotes`; `ListAccountNotes_AnAccountWithNoNote_HasNulls` (NoteQueryTests.cs:18-47) calls the same method on the same seed and asserts every whole `ListAccountNotesDto` by record equality, the four `Id`s and `AccountId`s as `AccountId` values (Types/AccountId.cs:6-9) and carol's null `AccountId` included.  A converter that ran on carol's NULL or read the ids wrongly fails the cover as it fails this test.  The only difference, a closed connection, is exercised by every other query class of the folder.  The comment above the test (lines 49-51) explains the converter and records no limit, regression or decision; no AGENTS.md, readme, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names it.

### F-DatabaseTests.Postgres.EndToEnd-1-13

- test: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_FolderOverride_MakesTimestamptzADateTime` | rows: all
- action: delete
- evidence: read (ModelShapeTests.cs:426-428 asserts that `Billing.GetInvoiceRow` contains `("PaidAt", DateTime?, true)`; `Build_OutputTypeByFolderFile_IsASealedClass`, ModelShapeTests.cs:406-424, asserts by `ShouldBeModel` that the same model has exactly its properties, `("PaidAt", DateTime?, true)` among them, and `Build_TableOfAnotherSchema_IsNamedWithItsSchema`, line 368-380, the same of `BillingInvoicesRow`.  No comment above it, and no AGENTS.md, readme, `docs/tech-debt` or `docs/deferred` names it); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_OutputTypeByFolderFile_IsASealedClass`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ModelShapeTests.cs:426-428 asserts `GetInvoiceRow` contains `("PaidAt", DateTime?, true)`.  `Build_OutputTypeByFolderFile_IsASealedClass` (lines 406-424) asserts through `ShouldBeModel`, which compares the whole property list (TestHarness/EndToEnd/ModelReflection.cs:75-85), that `GetInvoiceRow` has exactly its four properties, `("PaidAt", DateTime?, true)` among them; without the folder's override (Queries/Billing/sqlsource.json) it would be `DateTimeOffset?` by the project's and the cover fails.  `Build_TableOfAnotherSchema_IsNamedWithItsSchema` (lines 368-380) holds the same for `BillingInvoicesRow`.  No comment above the test; README.md:144 describes `ModelShapeTests` generally; only `docs/superpowers/plans/` names it.

### F-DatabaseTests.Postgres.EndToEnd-1-14

- test: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_QueryOverride_ReturnsTimestamptzToDateTime` | rows: all
- action: delete
- evidence: read (ModelShapeTests.cs:430-432 asserts that `AllTypesDto` contains `("TimestamptzValue", DateTime?, true)`; `Build_AllTypes_IsASealedRecordOfEveryColumn`, ModelShapeTests.cs:550-632, asserts by `ShouldBeModel` every property of `AllTypesDto` exactly, `("TimestamptzValue", typeof(DateTime?), true)` at line 576 among them.  No comment above it, and no AGENTS.md, readme, `docs/tech-debt` or `docs/deferred` names it); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ModelShapeTests.cs:430-432 asserts `AllTypesDto` contains `("TimestamptzValue", DateTime?, true)`.  `Build_AllTypes_IsASealedRecordOfEveryColumn` (lines 550-632) asserts the whole list by `ShouldBeModel`, `("TimestamptzValue", typeof(DateTime?), true)` at line 576; without the query's `-- type-override: timestamptz = default` (Queries/AllTypes.sql:9) it would be the project's `DateTimeOffset?` and the cover fails.  `Build_ProjectFileOverride_MakesTimestamptzADateTimeOffset` stays.  No comment above it; no AGENTS.md, readme, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names it.

### F-DatabaseTests.Postgres.EndToEnd-1-15

- test: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_AllTypes_HasNpgsql10sTypes` | rows: all
- action: delete
- evidence: read (ModelShapeTests.cs:434-444 asserts five properties of `AllTypesDto`: `DateValue` `DateOnly?`, `TimeValue` `TimeOnly?`, `CidrValue` `IPNetwork?`, `DaterangeValue` and `DatemultirangeValue`; `Build_AllTypes_IsASealedRecordOfEveryColumn` asserts all five exactly, lines 572, 573, 584, 619 and 625.  Its counterpart `Build_AllTypes_HasNpgsql8sTypes` of `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs:39` stays, since that project has no whole-model test; no comment above this one, and no AGENTS.md, readme, `docs/tech-debt` or `docs/deferred` names it); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs` | `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ModelShapeTests.cs:434-444 asserts five properties of `AllTypesDto`; the cover's exact list holds `DateValue` `DateOnly?` (572), `TimeValue` `TimeOnly?` (573), `CidrValue` `IPNetwork?` (584), `DaterangeValue` `NpgsqlRange<DateOnly>?` (619) and `DatemultirangeValue` `NpgsqlRange<DateOnly>[]` (625), same types and nullability.  The Npgsql 8 counterpart (tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs:36-48) is a driver-floor test that stays; its comment speaks of "Npgsql 10's" types, not of this test as a record.  No comment above this test; nothing outside `docs/superpowers/plans/` names it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.EndToEnd-1-12 | delete | `NoteQueryTests.ListAccountNotes_EveryRow_ReadsTheIdsAsAccountIds` | `tests/DatabaseTests/Postgres/EndToEnd/NoteQueryTests.cs`: `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls` |
| F-DatabaseTests.Postgres.EndToEnd-1-13 | delete | `ModelShapeTests.Build_FolderOverride_MakesTimestamptzADateTime` | `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`: `ModelShapeTests.Build_OutputTypeByFolderFile_IsASealedClass` |
| F-DatabaseTests.Postgres.EndToEnd-1-14 | delete | `ModelShapeTests.Build_QueryOverride_ReturnsTimestamptzToDateTime` | `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`: `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn` |
| F-DatabaseTests.Postgres.EndToEnd-1-15 | delete | `ModelShapeTests.Build_AllTypes_HasNpgsql10sTypes` | `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`: `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/Postgres/README.md`: 7, 144, 150, 167
- `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`: 45

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
