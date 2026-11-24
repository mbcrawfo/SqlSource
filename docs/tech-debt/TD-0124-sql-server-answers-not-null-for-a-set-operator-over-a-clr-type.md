# TD-0124 - SQL Server answers not null for a set operator over a CLR type

## Problem

SQL Server answers `is_nullable` 0 for a column that combines a `NOT NULL` value of a CLR type with a `NOT NULL` string, though the string can convert to that type's Null.  The describer keeps the server's answer where it says not null ([the proofs](../architecture/sqlserver-describer.md#proofs-of-not-null) only narrow a nullable one), so the column gets a property that cannot be null, and the query returns a `NULL` in it.

The cause is the conversion.  A set operator converts each branch to the type of highest precedence, which a CLR type has, and a string reaches that type through its `Parse`.  `geometry` and `geography` parse the text `NULL`, in any case, as their Null.  The server judges the column from its branches, each not null, and does not allow for the conversion.  A `CASE` over the same two values is answered nullable, so the server's rule for a set operator differs from its rule for a `CASE`.

Measured on SQL Server 2017 (CU31-GDR19) and 2025 (CU9-GDR1), the same on both, with `solid geometry NOT NULL`, `place geography NOT NULL`, `node hierarchyid NOT NULL` and `wkt nvarchar(20) NOT NULL` holding `N'NULL'`, over one row:

| Query | `is_nullable` | Type | Rows | `NULL` rows |
|----|----|----|----|----|
| `SELECT solid AS c FROM dbo.sh UNION ALL SELECT 'NULL'` | 0 | `geometry` | 2 | 1 |
| `SELECT solid AS c FROM dbo.sh UNION ALL SELECT wkt FROM dbo.sh` | 0 | `geometry` | 2 | 1 |
| `SELECT wkt AS c FROM dbo.sh UNION ALL SELECT solid FROM dbo.sh` | 0 | `geometry` | 2 | 1 |
| `SELECT place AS c FROM dbo.sh UNION ALL SELECT 'NULL'` | 0 | `geography` | 2 | 1 |
| The `UNION ALL` of `solid` and `wkt` in a derived table | 0 | `geometry` | 2 | 1 |
| The same in a CTE | 0 | `geometry` | 2 | 1 |
| The same in a view | 0 | `geometry` | 2 | 1 |
| `SELECT v.c FROM dbo.sh CROSS APPLY (VALUES (sh.solid), (sh.wkt)) AS v (c)` | 0 | `geometry` | 2 | 1 |
| `SELECT CASE WHEN id = 1 THEN wkt ELSE solid END AS c FROM dbo.sh` | 1 | `geometry` | 1 | 1 |

`UNION`, `INTERSECT` and `EXCEPT` of `geometry` are refused by the server, since the type cannot be compared.  `hierarchyid` is answered not null too under `UNION ALL`, `UNION`, `INTERSECT` and `EXCEPT`, but its `Parse` raises a .NET error for the text `NULL`, so that string returns no row.

Not measured: SQL Server 2019 and 2022; a user's CLR type, whose `Parse` may answer its Null for any string and which may be comparable, so that `UNION`, `INTERSECT` and `EXCEPT` could reach it; another string that `hierarchyid` might parse as its Null; a string parameter as the other branch; and a set operator nested more than one level deep.

## Why it exists

The describer relies on the server where it says not null, and this answer is the server's.  It was found by a test of the proofs of a set operator, whose own proofs refuse a column of a CLR type (`SqlServerTypes.MayBeClr`) and so do not cause it.  The owner chose to record it and fix it in a unit of its own, since a fix must reach the shapes above that hide the set operator from the query's text.

## Impact

High.  A false non-null is the worst outcome of the describer.  A query method throws when it reads the `NULL`, and a model read through Dapper holds a null in a property that cannot be null, with no error at build time.  It needs a spatial column combined with a string that holds `NULL` in some case, so it is rare.  The manual's [limit](../../site/reference/databases/sqlserver/nullability.md#limit-clr-set-operator) says what a user can do.

## Proposed fix

Keep the server's not null for a column of a CLR type (`SqlServerTypes.MayBeClr`) only when its row shows a stored column of a table: a `sourceColumn`, a `sourceType` of `U` and `isComputedColumn` false, as `OperandsQuery.AnswersOf` reads `Stored`.  The server names no source column for a column of a set operator, so this reaches a derived table and a CTE without reading the text, and a view's column has a `sourceType` that is no table.  Measure first that a `VALUES` column names no source.  Answer every other such column nullable, and keep [the table match](../architecture/sqlserver-describer.md#the-table-match) consistent with it.  A `NOT NULL` CLR column read through a view then becomes nullable, which is only noise.  Test each row of the table above, and the unmeasured shapes, on every supported major in `ProofTests`.

## Trigger

The unit `td-0071-clr-setop`, or a user who reports a `NULL` in a spatial column whose property cannot be null.
