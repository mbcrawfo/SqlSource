# Test cleanup phase 82: tests/DatabaseTests/Postgres/Describer, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/Describer`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 21
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`
- `tests/DatabaseTests/Postgres/Describer/ExpressionTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.Describer-1-02

- test: `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_ExpressionOfAnIntervalWithFields_KeepsTheFields` | rows: all
- action: delete
- evidence: read (ColumnTests.cs:93-103 describes `SELECT '1 day'::interval day to second(3) AS span` and asserts the name `interval day to second(3)`, the precision 3 and no origin.  The row of `DescribeAsync_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier` at ColumnTests.cs:74, `("interval day to second(3)", "interval day to second(3)", null, 3, null)`, describes the same cast, of a NULL instead of a literal, and asserts the same name and the facets (length, precision, scale) = (null, 3, null).  Both are casts in a select list, which the row description alone types.  ColumnTests.cs:375-382 (`DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity`) and ColumnTests.cs:397 already assert that an expression has no origin.  The comment above the test restates the theory's rule.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred found nothing that names the test); blind: no
- cover: `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier` (row `"interval day to second(3)"`, line 74); `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs` | `ColumnTests.DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity`
- proposal: Delete the fact.  The theory's interval row already proves the spelling and the facets of the same cast.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ColumnTests.cs:93-103 describes `'1 day'::interval day to second(3)` and asserts no origin, the name `interval day to second(3)` and precision 3.  The theory row at ColumnTests.cs:74 describes `NULL::interval day to second(3)`, also a cast in the select list with no origin, and asserts the same name and `(Length, Precision, Scale) = (null, 3, null)` (:85-90), which is more than the fact.  Both take their typmod from the row description.  A literal rather than NULL as the cast's operand changes nothing the describer reads.  The fact's one extra assertion, `Origin == null`, is proved for expressions by `DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity` (:375-382).  It is proved for casts by :397, where `NULL::email_t AS n` and `email::text` have no origin.  The comment at :93 restates the rule and records no limit, regression or decision.  My own grep found the test's name nowhere outside its file.  `ColumnTests` is named only in docs/architecture/postgres-nullability.md:110, for `DescribeAsync_ReturningOldAndNew_KeepsTheHeuristic`, a different test.

### F-DatabaseTests.Postgres.Describer-1-03

- test: `tests/DatabaseTests/Postgres/Describer/ExpressionTests.cs` | `ExpressionTests.DescribeAsync_PartitionedTableAggregate_IsAnswered` | rows: all
- action: delete
- evidence: read (ExpressionTests.cs:243-250 describes `SELECT count(*) AS n FROM scratch_parts` and asserts `[NotNullExpression]`.  Its comment says "A partitioned table plans as an Append, which "partitions" tells from a UNION's".  `ScanTests.DescribeAsync_AggregateOverAPartitionedTable_IsAnswered` at ScanTests.cs:92-102 describes `SELECT count(*) AS n, 'p' AS k FROM scratch_parts p WHERE p.kind = @kind`, with the comment "A partitioned table is no set operation: an aggregate over it is answered".  It asserts the same answer for the count, a walked plan and the constant.  Its plan keeps the Append of every partition (ScanTests.cs:74).  So it proves the same rule on the same table and asserts more.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred found nothing that names either test); blind: no
- cover: `tests/DatabaseTests/Postgres/Describer/ScanTests.cs` | `ScanTests.DescribeAsync_AggregateOverAPartitionedTable_IsAnswered`
- proposal: Delete the fact.  ScanTests' aggregate over the same partitioned table proves that its Append is not read as a set operation.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ExpressionTests.cs:243-250 describes `SELECT count(*) AS n FROM scratch_parts` and asserts `[NotNullExpression]`.  ScanTests.cs:92-102 describes `SELECT count(*) AS n, 'p' AS k FROM scratch_parts p WHERE p.kind = @kind`.  It asserts `NotNullExpression` for the count, plus a constant and `plan == walked` (ScanTests.cs:16-22).  It uses the same table (ScratchSchema.cs:23-28) and the same engine.  ScanTests.cs:74 records that a parameter condition still leaves every partition in the plan.  The one difference that could matter is the alias: unaliased members print as `scratch_parts_1` and `scratch_parts_2`, aliased ones as `p_1` and `p_2`.  `PlanPartitions.ShapeOf` and `PrefixOf` (src/SqlSource.Tool/Describing/Postgres/Walk/PlanPartitions.cs:48-73, 113-118) read both alike, as a prefix plus `_<digits>`.  `ParentsOf` (:82-110) strips the member's own alias, and a bare `count(*)` prints no member output in either query, so the unaliased form is not a separate path.  Neither test has a comment that records a limit, regression or decision.  My own grep found neither test's name outside its file, and found neither class in AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred.  The cover is on Postgres, and I read and confirmed it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.Describer-1-02 | delete | `ColumnTests.DescribeAsync_ExpressionOfAnIntervalWithFields_KeepsTheFields` | `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`: `ColumnTests.DescribeAsync_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier` (row `"interval day to second(3)"`, line 74); `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`: `ColumnTests.DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity` |
| F-DatabaseTests.Postgres.Describer-1-03 | delete | `ExpressionTests.DescribeAsync_PartitionedTableAggregate_IsAnswered` | `tests/DatabaseTests/Postgres/Describer/ScanTests.cs`: `ScanTests.DescribeAsync_AggregateOverAPartitionedTable_IsAnswered` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.
This phase changes describer tests: run `tests/DatabaseTests/Postgres` on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `tests/DatabaseTests/SqlServer` on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` too, for the projects it touches, as `AGENTS.md` asks before a push.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/postgres-nullability.md`: 110
- `docs/architecture/sqlserver-describer.md`: 171, 377

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
