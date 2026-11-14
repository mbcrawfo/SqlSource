# Test cleanup phase 81: tests/DatabaseTests/Postgres, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 34
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/PostgresDatabasesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres-1-01

- test: `tests/DatabaseTests/Postgres/PostgresDatabasesTests.cs` | `PostgresDatabasesTests.InitializeAsync_EndToEndDatabases_AreSeeded` | rows: all
- action: delete
- evidence: read (the test asserts that `PostgresDatabases.EndToEndDatabases` equals `[AppDatabase, BillingDatabase]` (lines 47-50), which restates its declaration at `tests/DatabaseTests/Postgres/PostgresDatabases.cs:31`, a property that no other code reads (`grep -rn EndToEndDatabases` finds only the declaration and this test), and then counts 2 rows of `all_types`, 3 of `accounts` and 3 of `billing.invoices` (lines 52-54).  On the same engine and the same databases, `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn` asserts `rows.Count.ShouldBe(2)` and the values of both rows (`tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs:98-100`), `AccountQueryTests.ListAccounts_EveryAccount_ReadsTheTableModel` asserts the three seeded accounts with every value (`tests/DatabaseTests/Postgres/EndToEnd/AccountQueryTests.cs:22-27`, `:50`), and `InvoiceQueryTests.ListInvoices_EveryInvoice_ReadsTheTableModel` asserts the three seeded invoices with every value (`tests/DatabaseTests/Postgres/EndToEnd/InvoiceQueryTests.cs:28-61`); none of the three is skipped on any image.  A seed script that fails stops `InitializeAsync` and so every test of the collection.  The mutation evidence says `None`, so judged by reading.  No comment sits above the test; the class comment (line 12-13) says only that the end-to-end databases are seeded.  `grep -rn` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names `PostgresDatabasesTests` once, `tests/DatabaseTests/Postgres/README.md:25`, for the image pin and the server's version, which are other tests; the test's name is only in a plan, `docs/superpowers/plans/2026-10-19-query-generation-phase-5-4-end-to-end.md:118`, as the failing first step of that phase); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` | `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn`; `tests/DatabaseTests/Postgres/EndToEnd/AccountQueryTests.cs` | `AccountQueryTests.ListAccounts_EveryAccount_ReadsTheTableModel`; `tests/DatabaseTests/Postgres/EndToEnd/InvoiceQueryTests.cs` | `InvoiceQueryTests.ListInvoices_EveryInvoice_ReadsTheTableModel`
- proposal: Delete the test, and with it `CountAsync`, which only it calls; `PostgresDatabases.EndToEndDatabases` is then read by nothing, which the cleanup either removes or leaves to the maintainability review.  The SQL Server twin in `SqlServerDatabasesTests` is that engine's and is not part of this finding.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence `## Mutation scopes`: None), so judged by reading under the named-cover rule.  The test (`tests/DatabaseTests/Postgres/PostgresDatabasesTests.cs:44-55`) restates `EndToEndDatabases` (`tests/DatabaseTests/Postgres/PostgresDatabases.cs:31`), which `grep -rn EndToEndDatabases tests src tools docs site` finds read by no code but this test (the SQL Server twin is its own engine's, and plans only).  The three counts are each proved, with every value, on the same engine and the same databases by the named covers, which I read: `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn` asserts `rows.Count.ShouldBe(2)` over `SELECT * FROM all_types ORDER BY id` (`tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs:98`, `EndToEnd/Queries/AllTypes.sql:7-12`); `AccountQueryTests.ListAccounts_EveryAccount_ReadsTheTableModel` asserts the three seeded accounts exactly over an unfiltered `SELECT * FROM accounts` (`EndToEnd/AccountQueryTests.cs:22-27`, `:50`, `EndToEnd/Queries/Accounts.sql:11-15`); `InvoiceQueryTests.ListInvoices_EveryInvoice_ReadsTheTableModel` asserts the three invoices by value over an unfiltered `SELECT * FROM billing.invoices` (`EndToEnd/InvoiceQueryTests.cs:28-61`, `EndToEnd/Queries/Billing/Invoices.sql:6-10`).  The covers reach the same databases, `EndToEndSources.App` and `.Billing` opening `ConnectionStringOf(AppDatabase)` and `(BillingDatabase)` (`EndToEnd/EndToEndSources.cs:22-24`, `:73-74`), and none of them skips on any image (no `Skip` in the three files).  Records: no comment above the test; the class comment (`PostgresDatabasesTests.cs:12-13`) only lists what the class checks.  My grep over the AGENTS.md files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names the class only at `tests/DatabaseTests/Postgres/README.md:25`, for the image pin and server version (other tests of the class), and `docs/superpowers/specs/2026-11-05-repository-tests-design.md:81` for those image-pin tests too; the test's own name appears only in a plan (`docs/superpowers/plans/2026-10-19-query-generation-phase-5-4-end-to-end.md:118`) as a first TDD step, which records no limit, regression or decision.  Nothing of what mutation cannot see rests on this test alone.

### F-DatabaseTests.Postgres-1-02

- test: `tests/DatabaseTests/Postgres/PostgresDatabasesTests.cs` | `PostgresDatabasesTests.InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButIdOfRow2` | rows: all
- action: delete
- evidence: read (the test counts, at the server, the columns of `to_jsonb(t)` that are null in each row of `all_types` and asserts 0 for id 1 and every column but one for id 2 (lines 60-75).  `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn` (`tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs:89-103`), on the same engine and database, reads `SELECT * FROM all_types ORDER BY id` (`tests/DatabaseTests/Postgres/EndToEnd/Queries/AllTypes.sql:7-12`) and asserts row 1 through `AllTypesRows.ShouldBeTheFullRow`, a non-null expected value for every property (`tests/DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs:30-145`, `:149-166`), and row 2 through `ShouldBeTheNullRow`, null in every property but `Id` (`:169-175`); both first require the model's properties to be exactly the keys of `FullRow` (`tests/DatabaseTests/TestHarness/EndToEnd/ModelReflection.cs:45-53`), so a column added to the table without a value in row 1 fails there as here, and `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md` requires a new column of `all_types` to change `AllTypesRows.cs` with the schema and seed.  The cover asserts each value, which this test does not.  The mutation evidence says `None`, so judged by reading.  No comment sits above the test, and `grep -rn` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names neither the test nor, for this check, its class; it was written with the databases in `bf8d940a`, before `AllTypesRows` held the rows' values); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` | `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn`
- proposal: Delete the test.  `InitializeAsync_AllTypesRegconfig_IsTheFixedOidOfSimple` stays, as the record of the seed's choice of `simple` made in review (`6f4ac0a3`) and explained in its comment.
- verdict: confirmed
- verifier's reason: Judged by reading (evidence: no mutation scope).  The test (`tests/DatabaseTests/Postgres/PostgresDatabasesTests.cs:57-76`) counts at the server the null fields of `to_jsonb(t)` per row of `all_types`: 0 for id 1, every column but one for id 2.  The cover `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn` (`tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs:89-103`), same engine and database, reads `SELECT *` of the table and calls `AllTypesRows.ShouldBeTheFullRow` on row 1, which first requires the model's properties to equal the keys of `FullRow` (`tests/DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs:149-151`, `ModelReflection.cs:45-53`) and then a non-null expected value for each (`AllTypesRows.cs:156-165`, `:200-208`), and `ShouldBeTheNullRow` on row 2, null in every property but `Id` (`AllTypesRows.cs:168-175`): the same two facts, and each value besides.  The one gap between them, a column added to the schema and seed while the sidecar is left stale so the model lacks it, is caught on the same engine by `EndToEnd/CheckTests` (`--check` over the real project, per `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`), and that AGENTS.md requires a new column of `all_types` to change `AllTypesRows.cs` with the schema and seed.  Records: no comment above the test; my grep of the documents names neither the test nor this check (the class appears only at `tests/DatabaseTests/Postgres/README.md:25` for the image pin).  The neighbouring `InitializeAsync_AllTypesRegconfig_IsTheFixedOidOfSimple` (`PostgresDatabasesTests.cs:78-89`), the commented record of the `simple` choice, is rightly left out of the finding.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres-1-01 | delete | `PostgresDatabasesTests.InitializeAsync_EndToEndDatabases_AreSeeded` | `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`: `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn`; `tests/DatabaseTests/Postgres/EndToEnd/AccountQueryTests.cs`: `AccountQueryTests.ListAccounts_EveryAccount_ReadsTheTableModel`; `tests/DatabaseTests/Postgres/EndToEnd/InvoiceQueryTests.cs`: `InvoiceQueryTests.ListInvoices_EveryInvoice_ReadsTheTableModel` |
| F-DatabaseTests.Postgres-1-02 | delete | `PostgresDatabasesTests.InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButIdOfRow2` | `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`: `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/Postgres/README.md`: 25

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
