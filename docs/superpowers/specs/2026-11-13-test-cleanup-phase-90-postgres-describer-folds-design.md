# Test cleanup phase 90: tests/DatabaseTests/Postgres/Describer, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/Describer`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 11
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/Describer/NpgsqlCheckTests.cs`
- `tests/DatabaseTests/Postgres/Describer/SetOperationTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.Describer-1-08

- test: `tests/DatabaseTests/Postgres/Describer/NpgsqlCheckTests.cs` | `NpgsqlCheckTests.DescribeAsync_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck` | rows: `"SELECT id, name FROM users"` (line 42), `"UPDATE users SET name = @name WHERE id = @id"` (line 43), `"INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id"` (line 44)
- action: fold
- evidence: read (NpgsqlCheckTests.cs:35-49 asserts only that the describe has no failure.  The class comment at lines 11-12 says "Every describe of ParameterTests and of ColumnTests that succeeds runs the check too, so each of those cases is one that passes it".  The three named rows hold nothing the lexer could read differently from Npgsql: no string, quote, operator, cast or slice, unlike the theory's other rows.  `ColumnTests.DescribeAsync_UpdateWithoutReturning_IsNone` (ColumnTests.cs:259-269) describes the same UPDATE text and fails on any failure (Sessions.cs:78-80).  `ColumnTests.DescribeAsync_InsertReturning_IsRowsAndInsertsNothing` (ColumnTests.cs:247-257) describes the same INSERT with one column more in its RETURNING.  `ColumnTests.DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes` (ColumnTests.cs:437-449) describes `SELECT id FROM users`, a query with no parameter, and asserts that `check` ran and that nothing failed.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred found the class only in src/SqlSource.Tool/Describing/Postgres/AGENTS.md, for its canary test `CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing`, not for this theory); blind: no
- cover: `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_UpdateWithoutReturning_IsNone`; `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_InsertReturning_IsRowsAndInsertsNothing`; `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes`
- proposal: Drop the three rows, so that the theory keeps the texts where the lexer and Npgsql could disagree.  The class comment already says that the describes of ColumnTests pass the check.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  The theory (NpgsqlCheckTests.cs:35-49) asserts only that no failure occurs.  `check` runs on every describe that gets that far, with or without parameters (PostgresSession.cs:259 and the comment at :612-615).  `Sessions.ShouldDescribeAsync` fails on any failure (Sessions.cs:71-81).  For row :43, `ColumnTests.DescribeAsync_UpdateWithoutReturning_IsNone` (ColumnTests.cs:259-269) describes the identical UPDATE.  For row :44, `ColumnTests.DescribeAsync_InsertReturning_IsRowsAndInsertsNothing` (ColumnTests.cs:247-257) differs only by `, placed_at` in RETURNING, which changes nothing the lexer or Npgsql reads.  For row :42, `ColumnTests.DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes` (ColumnTests.cs:436-449) describes `SELECT id FROM users` with no parameter, and asserts that `check` ran and nothing failed.  The class comment (NpgsqlCheckTests.cs:11-12) itself says the describes of ColumnTests pass the check.  The three rows hold no string, quote, operator, cast, slice or comment, so they add no case where the readers could disagree.  The theory's comment (:33-34) is about `@name` in strings and comments, not these rows.  My own grep found the class only in tests/AGENTS.md:55 (about sharing a name) and in src/SqlSource.Tool/Describing/Postgres/AGENTS.md, which cites `CheckAsync_TextNpgsqlWouldSplit_...`, not this theory.  It is a fold that drops rows of a theory, so a filter cannot measure it; this verdict rests on reading, and the cleanup phase's own guard proves it.

### F-DatabaseTests.Postgres.Describer-2-03

- test: `tests/DatabaseTests/Postgres/Describer/SetOperationTests.cs` | `SetOperationTests.DescribeAsync_UnionAllWhoseBranchNamesCrossThePlaces_StaysUnknown` | rows: `"SELECT o.note AS label, o.note AS note FROM orders o " + "UNION ALL SELECT u.name AS note, u.email AS label FROM users u", 0` (line 274) and `..., 1` (line 279)
- action: fold
- evidence: read (the two rows at SetOperationTests.cs:274-283 hold the same SQL, so each describes the same query and asserts the same walk, `[UnknownNoOrigin, UnknownNoOrigin]` at :291; they differ only in the column whose NULL `ReturnsNullAsync` proves at :292.  `DescribeAsync_UnionAllInASubqueryReadInAnotherOrder_IsAnsweredByItsMembersPlaces` at :327-328 already proves two columns of one query in one run.  The comment above the test (:268-272) explains why both columns can be NULL and calls no row deliberate; no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test or `SetOperationTests`); blind: no
- cover: `tests/DatabaseTests/Postgres/Describer/SetOperationTests.cs` | `SetOperationTests.DescribeAsync_UnionAllWhoseBranchNamesCrossThePlaces_StaysUnknown` (the merged row)
- proposal: Make the two rows one, whose premise checks `ReturnsNullAsync` for column 0 and for column 1, so the query is described once and both premises stay; the third row (line 284) is unchanged.
- verdict: confirmed
- verifier's reason: The rows at SetOperationTests.cs:274-278 and :279-283 hold the same SQL and differ only in `column`.  Each one describes the query and asserts the same walk, `[UnknownNoOrigin, UnknownNoOrigin]` at :291.  The only difference is the premise `ReturnsNullAsync(Rows, sql, column)` at :292, which checks column 0 for one row and column 1 for the other.  A single case that checks both columns, as :327-328 already does for `crossed`, keeps both premises and the walk.  It loses nothing on the same engine.  The comment above the test (:268-272) names only the third row (:284-288) as what the names would mislead.  It calls neither of the first two rows deliberate, and the fold leaves the third row unchanged.  A search of the AGENTS.md files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds neither the test nor the class.  The rows share one name, so no filter can measure the fold.  It is confirmed by reading, and the cleanup phase's own guard proves it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.Describer-1-08 | fold | `NpgsqlCheckTests.DescribeAsync_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck` (rows: `"SELECT id, name FROM users"` (line 42), `"UPDATE users SET name = @name WHERE id = @id"` (line 43), `"INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id"` (line 44)) | `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`: `ColumnTests.DescribeAsync_UpdateWithoutReturning_IsNone`; `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`: `ColumnTests.DescribeAsync_InsertReturning_IsRowsAndInsertsNothing`; `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`: `ColumnTests.DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes` |
| F-DatabaseTests.Postgres.Describer-2-03 | fold | `SetOperationTests.DescribeAsync_UnionAllWhoseBranchNamesCrossThePlaces_StaysUnknown` (rows: `"SELECT o.note AS label, o.note AS note FROM orders o " + "UNION ALL SELECT u.name AS note, u.email AS label FROM users u", 0` (line 274) and `..., 1` (line 279)) | `tests/DatabaseTests/Postgres/Describer/SetOperationTests.cs`: `SetOperationTests.DescribeAsync_UnionAllWhoseBranchNamesCrossThePlaces_StaysUnknown` (the merged row) |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.
This phase changes describer tests: run `tests/DatabaseTests/Postgres` on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `tests/DatabaseTests/SqlServer` on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` too, for the projects it touches, as `AGENTS.md` asks before a push.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `src/SqlSource.Tool/Describing/Postgres/AGENTS.md`: 41
- `tests/AGENTS.md`: 55

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
