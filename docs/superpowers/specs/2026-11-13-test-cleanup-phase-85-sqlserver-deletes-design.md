# Test cleanup phase 85: tests/DatabaseTests/SqlServer, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer`
- Findings: 3 (3 delete)
- Kind: one kind, delete
- Changed lines: about 39
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer-1-04

- test: `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs` | `SqlServerDatabasesTests.Image_OfTheRun_NamesItsCumulativeUpdate` | rows: all
- action: delete
- evidence: read (SqlServerDatabasesTests.cs:90-91 matches `TagOf(EngineImage.SqlServer.Image)` to PinnedTag; `Image_OfTheRun_IsInTheMapOfBuilds` (:94-95) holds that image to be a key of `SqlServerContainer.ImageBuilds`, and `ImageBuilds_EveryImage_IsPinnedAndNotLatest` (:124-130) matches every key's tag to the same PinnedTag, so whenever both pass this one passes, and a run image of another form already fails the first.  tests/DatabaseTests/SqlServer/README.md:31 says `SqlServerDatabasesTests` fails on a tag of another form, which stays true; no AGENTS.md, docs/architecture, tech-debt or deferred item names the test, and the comment above it gives the rule, not a record.  The PostgreSQL twin has no map, so its tag test is not redundant there); blind: no
- cover: `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs` | `SqlServerDatabasesTests.Image_OfTheRun_IsInTheMapOfBuilds`; `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs` | `SqlServerDatabasesTests.ImageBuilds_EveryImage_IsPinnedAndNotLatest`
- proposal: Delete the test; the two named keep the run's image pinned.
- verdict: confirmed
- verifier's reason: tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs:89-91 matches `TagOf(EngineImage.SqlServer.Image)` to `PinnedTag` (:21); `Image_OfTheRun_IsInTheMapOfBuilds` (:93-95) requires that same image to be a key of `SqlServerContainer.ImageBuilds`, and `ImageBuilds_EveryImage_IsPinnedAndNotLatest` (:123-130) matches the tag of every key, through the same `EngineImage.TagOf` (tests/DatabaseTests/TestHarness/EngineImage.cs:58), to the same `PinnedTag`; so whenever both pass this one passes, on the same engine and in the same class, and an unpinned run image still fails the first.  Search: no AGENTS.md, CONTRIBUTING.md, docs/architecture, tech-debt or deferred item names the test or its class; tests/DatabaseTests/SqlServer/README.md:31 names the class `SqlServerDatabasesTests` as failing on a tag of another form, which stays true since both covers are of that class and the README names them as much as this one.  The comment above the test (:87-88) states the rule of root AGENTS.md rule 5 and points to the README; it records no limit or regression that the covers do not hold.

### F-engines-01

- test: `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs` | `SqlServerDatabasesTests.InitializeAsync_EndToEnd_SeedsEachTable` | rows: all
- action: delete
- evidence: read (twin fault of F-DatabaseTests.Postgres-1-01, which that finding left to this engine; the SqlServer-1 slice has no finding on it.  SqlServerDatabasesTests.cs:42-54 restates `SqlServerDatabases.EndToEndDatabases` (SqlServerDatabases.cs:24), which `grep -rn EndToEndDatabases tests src tools` finds read by nothing but this test, and counts accounts 3, notes 3, all_types 2 and billing.invoices 3.  On the same engine and databases: `AccountQueryTests.ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel` (AccountQueryTests.cs:42-50) asserts the three accounts by value over an unfiltered `SELECT * FROM dbo.accounts` (Queries/Accounts.sql:9-13); `InvoiceQueryTests.ListInvoices_Seed_ReadsTheTableModel` (InvoiceQueryTests.cs:15-63) the three invoices over `SELECT * FROM billing.invoices` (Queries/Billing/Invoices.sql:6-10); `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows` (MethodAllTypesTests.cs:41-51) `rows.Count.ShouldBe(2)` over `SELECT * FROM dbo.all_types` (Queries/AllTypes.sql:9-14); and `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls` (NoteQueryTests.cs:23-54) every row of `accounts LEFT JOIN notes` (Queries/Notes.sql:9-14), three notes and carol's NULLs, where `notes.account_id` is `NOT NULL REFERENCES dbo.accounts` (EndToEnd/app/schema.sql:31-38), so a fourth note would show.  None of the covers skips on any image.  A seed that fails stops `InitializeAsync` and every test of the collection.  No comment above the test; the class comment (SqlServerDatabasesTests.cs:14-16) lists what the class checks.  Searched the AGENTS.md files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/`: the class is named only at tests/DatabaseTests/SqlServer/README.md:31, for the image tests; the test's name is only in the plan docs/superpowers/plans/2026-10-22-query-generation-phase-6-5-end-to-end.md:124, as a step of that phase, no record.  No mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/AccountQueryTests.cs` | `AccountQueryTests.ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel`; `tests/DatabaseTests/SqlServer/EndToEnd/InvoiceQueryTests.cs` | `InvoiceQueryTests.ListInvoices_Seed_ReadsTheTableModel`; `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs` | `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows`; `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs` | `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls`
- proposal: Delete the test and `CountAsync`, which only it calls, as F-DatabaseTests.Postgres-1-01 does on PostgreSQL; `SqlServerDatabases.EndToEndDatabases` is then read by nothing, which the cleanup treats as it treats PostgreSQL's.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence/DatabaseTests.SqlServer-1.md, `## Mutation scopes`: None), so judged by reading under the named-cover rule.  The test (tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs:42-54) restates `EndToEndDatabases` (SqlServerDatabases.cs:24), which `grep -rn EndToEndDatabases tests src tools --include='*.cs'` finds read by no code but this test (and PostgreSQL's own twin), and counts 3, 3, 2 and 3 rows through `CountAsync` (:153-158), which nothing else calls.  I read each cover, all in `UsesSqlServer`, on the same engine and databases (`EndToEndSources.App`/`.Billing` open `AppDatabase`/`BillingDatabase`, EndToEnd/EndToEndSources.cs:28-29, :55-56), none with a skip: `AccountQueryTests.ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel` (AccountQueryTests.cs:42-50) asserts the three accounts exactly over `SELECT * FROM dbo.accounts ORDER BY id` (Queries/Accounts.sql:9-13); `InvoiceQueryTests.ListInvoices_Seed_ReadsTheTableModel` (InvoiceQueryTests.cs:15-62) the three invoices by value over `SELECT * FROM billing.invoices` (Queries/Billing/Invoices.sql:6-10); `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows` (MethodAllTypesTests.cs:41-51) `rows.Count.ShouldBe(2)` over `SELECT * FROM dbo.all_types` (Queries/AllTypes.sql:9-14); `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls` (NoteQueryTests.cs:23-54) every row of `accounts LEFT JOIN notes` (Queries/Notes.sql:9-14), and `notes.account_id` is `NOT NULL REFERENCES dbo.accounts (id)` (EndToEnd/app/schema.sql:31-33), so a fourth note would add a row.  Records: no comment above the test; the class comment (:14-16) lists what the class checks.  My grep of every `.md` outside `artifacts/` names the class only at tests/DatabaseTests/SqlServer/README.md:31, for the image-pin tests, and docs/superpowers/specs/2026-11-05-repository-tests-design.md:81, for the same; the test's name is only in the plan docs/superpowers/plans/2026-10-22-query-generation-phase-6-5-end-to-end.md:124, as a step, no limit, regression or decision.  It is the exact twin of F-DatabaseTests.Postgres-1-01, confirmed on PostgreSQL.  Nothing mutation cannot see rests on it alone: a seed that fails stops the collection's `InitializeAsync`.

### F-engines-02

- test: `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs` | `SqlServerDatabasesTests.InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButTwoOfRow2` | rows: all
- action: delete
- evidence: read (twin fault of F-DatabaseTests.Postgres-1-02; the SqlServer-1 slice has no finding on it.  SqlServerDatabasesTests.cs:56-73 reads `SELECT * FROM dbo.all_types ORDER BY id` and asserts no NULL in row 1, NULL in every column but two of row 2 and a non-null `rowversion_value`.  `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows` (MethodAllTypesTests.cs:41-51), and `AllTypesQueryTests.QueryAsync_ListAllTypes_ReadsBothRows` (AllTypesQueryTests.cs:44-55) through Dapper, read the same `SELECT *` on the same engine and database and call `SqlServerAllTypesRows.ShouldBeTheFullRow` on row 1, which first requires the model's properties to be exactly the keys of `FullRow` (tests/DatabaseTests/TestHarness/EndToEnd/SqlServerAllTypesRows.cs:115-117, ModelReflection.cs:45-53) and then a non-null expected value for each (`ShouldBeValue`, :189-196; the rowversion an 8-byte array, :207-211), and `ShouldBeTheNullRow` on row 2, null in every property but `Id` and the rowversion, which must be 8 bytes (:137-149): the same facts and each value besides.  A column added to the schema with a stale sidecar is caught by `EndToEnd/CheckTests`, and tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md requires a column of `all_types` to change `SqlServerAllTypesRows.cs` with the schema and seed.  The comment above the test (:56-57) describes the seed and records no limit or decision; nothing in the searched documents names the test.  No mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs` | `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows`
- proposal: Delete the test, and `NullColumnsAsync` with it; `InitializeAsync_NewTypes_OnlyOn2025` stays.
- verdict: confirmed
- verifier's reason: Judged by reading (no mutation scope).  The test (SqlServerDatabasesTests.cs:56-73) reads `SELECT * FROM dbo.all_types ORDER BY id` and asserts no NULL in row 1, `FieldCount - 2` NULLs in row 2 and a non-null `rowversion_value`.  The cover `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows` (MethodAllTypesTests.cs:41-51), same engine and database, reads the same `SELECT *` (Queries/AllTypes.sql:9-14) and calls `SqlServerAllTypesRows.ShouldBeTheFullRow` on row 1, which first requires the model's properties to be exactly `FullRow`'s keys (tests/DatabaseTests/TestHarness/EndToEnd/SqlServerAllTypesRows.cs:115-117, ModelReflection.cs:45-53) and then a non-null expected value for every property (`ShouldBeValue`, :189-197), the rowversion an 8-byte array (:207-211); and `ShouldBeTheNullRow` on row 2 (:137-149), null in every property but `Id` and the rowversion, which must be 8 bytes, so non-null.  Every fact of the test, and each value besides.  The one gap, a column added to schema and seed with a stale sidecar so the model lacks it, is caught on the same engine by `EndToEnd/CheckTests.cs`, and tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md:49 requires a column of `all_types` to change schema, seed and `SqlServerAllTypesRows.cs` together.  The comment above the test (:56-57) describes the seed and records nothing; my grep of the documents finds the name nowhere.  `NullColumnsAsync` is called only by this test.  It mirrors F-DatabaseTests.Postgres-1-02, confirmed.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer-1-04 | delete | `SqlServerDatabasesTests.Image_OfTheRun_NamesItsCumulativeUpdate` | `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs`: `SqlServerDatabasesTests.Image_OfTheRun_IsInTheMapOfBuilds`; `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs`: `SqlServerDatabasesTests.ImageBuilds_EveryImage_IsPinnedAndNotLatest` |
| F-engines-01 | delete | `SqlServerDatabasesTests.InitializeAsync_EndToEnd_SeedsEachTable` | `tests/DatabaseTests/SqlServer/EndToEnd/AccountQueryTests.cs`: `AccountQueryTests.ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel`; `tests/DatabaseTests/SqlServer/EndToEnd/InvoiceQueryTests.cs`: `InvoiceQueryTests.ListInvoices_Seed_ReadsTheTableModel`; `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs`: `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows`; `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs`: `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls` |
| F-engines-02 | delete | `SqlServerDatabasesTests.InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButTwoOfRow2` | `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs`: `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/SqlServer/README.md`: 31

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
