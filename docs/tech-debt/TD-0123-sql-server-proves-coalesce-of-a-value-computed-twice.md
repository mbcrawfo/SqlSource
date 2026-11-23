# TD-0123 - The SQL Server describer proves `COALESCE` not null over a value computed twice

## Problem

[`ExpressionProofs`](../../src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs) proves `COALESCE` not null when one argument is proved and none is of a CLR type.  SQL Server expands `COALESCE(a, b)` to `CASE WHEN a IS NOT NULL THEN a ELSE b END` and evaluates `a` once for each reference.  When `a` is not deterministic, the test can see a value and the result a `NULL`, so a column the describer answers `nullable` false, with the `nullableSource` `expression`, holds `NULL`.  This is a false non-nullable, the worst outcome of a describer.

Measured through the describer on 2025 (CU9-GDR1), over 500 rows:

| Query | Answer | `NULL` rows |
|----|----|----|
| `SELECT COALESCE(CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END, 0) AS c FROM t` | `nullable` false, `expression` | 124 |
| The same with a correlated `(SELECT TOP (1) CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END FROM t AS o WHERE o.id = r.id)` as the argument | `nullable` false, `expression` | 126 |
| `SELECT COALESCE(CAST(LAG(b) OVER (ORDER BY NEWID()) AS int), 0) AS c FROM t`, `b` nullable | `nullable` false, `expression` | 125 |
| `SELECT COALESCE(CONVERT(int, LEAD(b) OVER (ORDER BY NEWID())), 0) AS c FROM t`, `b` nullable | `nullable` false, `expression` | 123 |
| `SELECT COALESCE(CAST(YEAR(LAG(d) OVER (ORDER BY NEWID())) AS int), 0) AS c FROM t`, `d` nullable | `nullable` false, `expression` | 133 |
| `SELECT COALESCE(CONVERT(date, DATEADD(DAY, 1, LEAD(d) OVER (ORDER BY NEWID()))), '2020-01-01') AS c FROM t`, `d` nullable | `nullable` false, `expression` | 125 |
| `SELECT COALESCE(CAST(LAG(d) OVER (ORDER BY NEWID()) AT TIME ZONE 'UTC' AS datetime2(0)), '2020-01-01') AS c FROM t`, `d` nullable | `nullable` false, `expression` | 132 |
| `SELECT COALESCE(SUM(b) OVER (ORDER BY NEWID() ROWS BETWEEN 1 FOLLOWING AND 1 FOLLOWING), 0) AS c FROM t`, `b` nullable | `nullable` false, `expression` | 121 |
| `SELECT COALESCE(MAX(b) OVER (ORDER BY NEWID() ROWS BETWEEN 1 FOLLOWING AND 1 FOLLOWING), 0) AS c FROM t`, `b` nullable | `nullable` false, `expression` | 126 |
| `SELECT COALESCE(SUM(dbo.f(id)) OVER (PARTITION BY id), 0) AS c FROM t`, `dbo.f` reading a view over `NEWID()` | `nullable` false, `expression` | 126 |

Run alone on 2017 (CU31-GDR19) and 2025, five runs of 2,000 rows each:

| Argument of `COALESCE(..., 0)` | `NULL` rows of 10,000, 2017 | 2025 |
|----|----|----|
| The `NEWID()` expression | 2,510 | 2,485 |
| The correlated subquery | 2,449 | 2,512 |
| The `NEWID()` expression, the `COALESCE` inside `CAST(... AS bigint)` | 2,535 | 2,496 |
| A computed column, or a `CROSS APPLY` column, of the same expression | 0 | 0 |

Two paths reach an argument that holds `OVER`, an aggregate or a user's function.  A `CAST` or `CONVERT` hides its operand, since its type proof reads its target alone.  `SUM`, `MAX` and `MIN` with `OVER` need no `CAST`: `SUM`'s type proof is fixed, and `MAX` and `MIN` take their operand's.  `COALESCE(LAG(b) OVER (ORDER BY NEWID()), 0)` itself stays nullable.

`ISNULL` of the same expression gave no `NULL`, since it evaluates its argument once.  A view's column, a derived table's and a CTE's, read by the equivalent `CASE`, gave none either, but no plan promises that.

## Why it exists

The proof of `COALESCE` was written from what the function returns, one argument that is not null, and not from how SQL Server evaluates it.  No case of the realistic corpus or of `ProofTests` has a non-deterministic argument.  It was found while proving `CASE WHEN x IS NULL THEN e ELSE x END`, which reads `x` twice in the same way and so requires `x` to be a stored column or answered not null (`Proof.Stored`).

## Impact

High.  A generated property that cannot be null meets a `NULL`, and a query method throws.  It needs a non-deterministic expression or subquery written inside the `COALESCE`, which is rare.  The manual's [limit](../../site/reference/databases/sqlserver/nullability.md#limit-coalesce-computed-twice) tells a user to write `ISNULL`.

## Proposed fix

Hold every argument of `COALESCE` but the last, which is read once, to a condition that keeps its second read from being `NULL`: proved not null itself, or a stored column (`Proof.Stored`), or an expression shown to give one value in a row.  Before choosing that last rule:

- Measure which arguments the realistic corpus and the end-to-end queries use, so that `COALESCE(MAX(x), 0)`, `COALESCE(a * 2, 0)` and a `COALESCE` over a view's column are not made noise without need, and move the SQL Server `Budgets` of `RealisticNullabilityNoiseTests` by what the change costs.
- A window aggregate in an argument is evaluated twice, as the table shows.  Measure on a server whether an aggregate without `OVER` or a deterministic function is evaluated once.
- Check `GREATEST` and `LEAST`, which were not measured for this.
- Make the type proof of `CAST` and `CONVERT` read their operand too, or the condition above misses what they wrap.

Add the queries above to `ValuesReadTwice` of `ProofTests`, written over `dbo.proof_rows` and `dbo.proof_dates`: its test holds the describe alone, since over those few rows a random query may show no `NULL`.  Remove the limit from the manual.

## Trigger

The owner ruled on 2026-11-22 that this is fixed by its own change, ahead of the next proofs of TD-0071.
