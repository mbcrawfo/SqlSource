# The spike of phase 6, SQL Server - appendix

Date: 2026-10-20

Appendix to phase 6 of the [query generation epic](2026-10-07-query-generation-epic-design.md) and its sub-phases' specs.  Before the sub-phases were specified, a throwaway set of `sqlcmd` scripts ran the two describe procedures against SQL Server 2017 and 2025 in containers on an Apple silicon Mac under OrbStack's emulation, to settle what the outline listed as unverified for this phase's spike.  The scripts were not kept; the schema, the probes and what they answered are here.

The images were `mcr.microsoft.com/mssql/server:2017-latest`, which reported `14.0.3560.2`, and `2025-latest`, which reported `17.0.5015.4`.  Every answer below is the same on both unless a row says otherwise.  The scripts ran through `sqlcmd`, whose driver speaks an older TDS than SqlClient; where that changes an answer, a SqlClient probe was run as well and the section says so.  The container tests of 6.1 and 6.2 describe every row again through SqlClient, and a row that differs there is corrected in this appendix.

## The images on Apple silicon

Both images run under emulation.  A first start, the pull included, took 193 seconds for 2017 and 266 for 2025; a start with the image already pulled took 5 and 8 seconds to the first answered query.  So the floor stays SQL Server 2017, as the outline decided, and the container tests can run in `pre-commit-validation.sh` on every commit, as the owner asked: the cost is the pull, once.

The tags MCR publishes name a cumulative update and an operating system, and some a GDR or an on-demand hotfix over the update: `2017-CU31-GDR19-ubuntu-18.04`, `2019-CU32-ubuntu-20.04`, `2022-CU27-ubuntu-22.04`, `2025-CU9-ubuntu-24.04`, `2025-CU9-GDR1-ubuntu-24.04`.  `-latest` and a bare year move; the owner asked that the tests pin a tag that names its update.

`sqlcmd` runs with `QUOTED_IDENTIFIER` off unless it is given `-I`, and then refuses a table with a persisted computed column, `Msg 1934`.  ADO.NET connections have it on.

## Schema

```sql
CREATE TYPE dbo.Email FROM nvarchar(320) NOT NULL;
CREATE TYPE dbo.IdList AS TABLE (id int NOT NULL);
CREATE SCHEMA billing;
CREATE TABLE dbo.users (
  id int IDENTITY PRIMARY KEY, name nvarchar(100) NOT NULL, email dbo.Email, nickname nvarchar(50) NULL,
  created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), deleted_at datetime2 NULL,
  name_len AS LEN(name), name_len_p AS CAST(LEN(name) AS int) PERSISTED NOT NULL, rv rowversion);
CREATE TABLE dbo.orders (id int IDENTITY PRIMARY KEY, user_id int NOT NULL REFERENCES dbo.users(id), total decimal(18,2) NOT NULL, note varchar(200) NULL);
CREATE TABLE billing.invoices (id bigint NOT NULL PRIMARY KEY, order_id int NOT NULL, amount money NOT NULL);
CREATE TABLE dbo.victim (id int);
CREATE TABLE dbo.trig (id int NOT NULL PRIMARY KEY, v int NOT NULL);
CREATE TRIGGER dbo.trig_after ON dbo.trig AFTER INSERT AS SET NOCOUNT ON;
CREATE VIEW dbo.user_orders AS SELECT u.id AS user_id, u.name, o.id AS order_id, o.total FROM dbo.users u LEFT JOIN dbo.orders o ON o.user_id = u.id;
CREATE TABLE dbo.prices (id int NOT NULL PRIMARY KEY, amount decimal(10,2) NOT NULL,
  valid_from datetime2 GENERATED ALWAYS AS ROW START NOT NULL, valid_to datetime2 GENERATED ALWAYS AS ROW END NOT NULL,
  PERIOD FOR SYSTEM_TIME (valid_from, valid_to)) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.prices_history));
-- and dbo.all_types, one NOT NULL column of every type below
```

## Types

`SELECT * FROM dbo.all_types` through `sys.dm_exec_describe_first_result_set(@tsql, NULL, 1)`:

| Column declared as | `system_type_name` | `max_length` | `precision` | `scale` | `user_type_schema.name` |
|----|----|----|----|----|----|
| `bigint`, `int`, `smallint`, `tinyint` | as declared | 8, 4, 2, 1 | 19, 10, 5, 3 | 0 | |
| `bit` | `bit` | 1 | 1 | 0 | |
| `decimal(18,4)`, `numeric(10,0)` | as declared | 9, 9 | 18, 10 | 4, 0 | |
| `money`, `smallmoney` | as declared | 8, 4 | 19, 10 | 4 | |
| `float`, `float(24)`, `real` | `float`, `real`, `real` | 8, 4, 4 | 53, 24, 24 | 0 | |
| `date` | `date` | 3 | 10 | 0 | |
| `time`, `time(3)` | `time(7)`, `time(3)` | 5, 4 | 16, 12 | 7, 3 | |
| `datetime`, `smalldatetime` | as declared | 8, 4 | 23, 16 | 3, 0 | |
| `datetime2`, `datetime2(0)` | `datetime2(7)`, `datetime2(0)` | 8, 6 | 27, 19 | 7, 0 | |
| `datetimeoffset`, `datetimeoffset(3)` | `datetimeoffset(7)`, `datetimeoffset(3)` | 10, 9 | 34, 30 | 7, 3 | |
| `char(10)`, `varchar(50)`, `varchar(max)`, `text` | as declared | 10, 50, -1, -1 | 0 | 0 | |
| `nchar(10)`, `nvarchar(50)`, `nvarchar(max)`, `ntext` | as declared | 20, 100, -1, -1 (bytes) | 0 | 0 | |
| `binary(16)`, `varbinary(50)`, `varbinary(max)`, `image` | as declared | 16, 50, -1, -1 | 0 | 0 | |
| `uniqueidentifier`, `xml`, `sql_variant` | as declared | 16, -1, 8016 | 0 | 0 | |
| `rowversion` | `timestamp` | 8 | 0 | 0 | |
| `hierarchyid`, `geography`, `geometry` | as declared | 892, -1, -1 | 0 | 0 | `sys.hierarchyid` and so on, with `assembly_qualified_type_name` naming `Microsoft.SqlServer.Types` 11.0.0.0 |
| `sysname` | `nvarchar(128)` | 256 | 0 | 0 | `sys.sysname` |
| `dbo.Email` | `nvarchar(320)` | 640 | 0 | 0 | `dbo.Email` |
| `json` (2025) | `json` through SqlClient; `varchar(max)` through `sqlcmd` | -1 | 0 | 0 | |
| `vector(3)` (2025) | `vector(3)` through SqlClient; `varchar(max)` through `sqlcmd` | 20 through SqlClient | 0 | 0 | |

Through `sqlcmd`, both procedures report `json` and `vector` as `varchar(max)`, columns and parameters alike, as the outline's technical notes expected.  A file-based probe through Microsoft.Data.SqlClient 6.1.7 and 7.1.1 against the same 2025 server got `json` and `vector(3)` from `sys.dm_exec_describe_first_result_set`, and `@j json` and `@v vector(3)` from `sp_describe_undeclared_parameters` for an `INSERT` into the two columns: the server reports the types to a client whose TDS knows them.  Read through either driver, `json` is `GetFieldType` and `GetValue` `System.String`, `GetDataTypeName` `json`; `vector` is `Microsoft.Data.SqlTypes.SqlVector<float>` for both, `GetDataTypeName` `vector`.

## Parameters

`sp_describe_undeclared_parameters @tsql`, `suggested_system_type_name`:

| Query | Answer |
|----|----|
| `SELECT TOP (@n) ...` | `bigint` |
| `... OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY` | `bigint`, `bigint` |
| `WHERE name LIKE @p` | `nvarchar(4000)` |
| `WHERE name = @p + N'x'` | `nvarchar(4000)` |
| `WHERE id IN (@a, @b)` | `int`, `int` |
| `WHERE email = @email` | `nvarchar(320)`, user type `dbo.Email` |
| `WHERE total > @t`, `total` being `decimal(18,2)` | `decimal(38,19)` |
| `WHERE rv = @rv` | `timestamp` |
| `WHERE deleted_at < @since` | `datetime2(7)` |
| `WHERE name = @p COLLATE Latin1_General_BIN` | `nvarchar(100)` |
| `INSERT ... VALUES (@name, @nick)`, `UPDATE ... SET nickname = @nick WHERE id = @id` | the columns' types |
| `WHERE CAST(@p AS int) = id`, `SELECT ISNULL(@p, 0)` | `int` |
| inside `EXISTS (...)`, inside a CTE | the compared column's type |
| `DATEADD(day, @days, SYSUTCDATETIME())` | **`int` on 2017, `bigint` on 2025** |
| **`WHERE @p IS NULL`** | **`int`** |
| `SELECT @p AS x` | error 11506, "two possibilities are 'sql_variant' and 'xml'" |
| `SELECT COALESCE(@p, name)` | error 11502, "cannot be deduced in this context" |
| `WHERE (@name IS NULL OR name = @name)`, `WHERE id = @p AND name = @p` | error 11508, "used more than once" |
| `WHERE id IN (SELECT id FROM @ids)` | error 11501, compile errors: an undeclared table variable |

A parameter declared in `@params` is left out of the procedure's answer.

### The copies of a reused parameter

The outline's workaround renames each occurrence, `@name_1`, `@name_2`, and accepts the answer when every typed copy agrees, ignoring a copy "in a context that types nothing, `@name_1 IS NULL`".  No copy comes back untyped: `@name_1 IS NULL` is `int`, `name = @name_2` is `nvarchar(100)`, and the two disagree on the pattern the workaround exists for.

What tells the two apart: declare one copy as `xml` and ask `sys.dm_exec_describe_first_result_set` whether the batch still binds.  `xml` converts to almost nothing, and the procedure's own answer for `SELECT @p` shows that `xml` and `sql_variant` are the types that any context admits.

| The copy declared `xml` | Answer |
|----|----|
| `@name_1` in `(@name_1 IS NULL OR name = @name_2)` | binds |
| `@p_1` in `(@p_1 IS NOT NULL AND id = @p_2)` | binds |
| `@p_1` in `CASE WHEN @p_1 IS NULL THEN 1 ELSE 0 END` | binds |
| `@name_2` in `(@name_1 IS NULL OR name = @name_2)` | 402, "nvarchar and xml are incompatible in the equal to operator" |
| `id = CAST(@p_1 AS int)` | 529, "explicit conversion from xml to int is not allowed" |
| `ISNULL(@p_1, 0)` | 206, operand type clash |
| `name LIKE @p_1` | 8116 |
| `COALESCE(@p_1, name) = name` | 402 |
| `id = @p_1` | 206 |
| `@p_1 = 1 OR @p_1 IS NULL` | 206 |

So a copy that binds as `xml` constrains nothing and is ignored, and a copy that does not is typed by its context.

A second round asked the same through `sp_describe_undeclared_parameters @tsql, @params`, every copy declared, which raises 11501 when the batch does not bind and is indifferent to the shape of a result:

| Declarations | Answer |
|----|----|
| `UPDATE users SET nickname = N'x' WHERE (@n_1 IS NULL OR name = @n_2)`, `@n_1 xml, @n_2 nvarchar(100)` | binds |
| the same, `@n_1 nvarchar(100), @n_2 xml` | 11501 |
| the same, `@n_1 int, @n_2 nvarchar(100)` | binds |
| `SELECT id FROM users WHERE name = @n_1`, `@n_1 int` | binds: `int` converts to a string, so `int` alone tells nothing |
| `UPDATE docs SET doc = @d_1 WHERE (@d_2 IS NULL OR id = 1)`, `doc` being `xml`: `@d_1 int, @d_2 xml` | 11501 |
| the same, `@d_1 xml, @d_2 int` and `@d_1 xml, @d_2 xml` | binds |
| `IF @a_1 = 1 SELECT id ... WHERE (@a_2 IS NULL) ELSE SELECT name ...`, `@a_1 int, @a_2 xml` | binds: the two shapes do not matter to this procedure |
| the same, `@a_1 xml, @a_2 int` | 11501 |

So a copy that binds declared `xml` and binds declared `int` constrains nothing; a copy in a context of `xml` fails as `int`, and one in any other typed context fails as `xml`.

## Declarations

| `@params` | Answer of `sys.dm_exec_describe_first_result_set(N'SELECT @e AS e', @params, 1)` |
|----|----|
| `@e dbo.Email` | `nvarchar(320)` |
| `@e dbo.Nope` | 2715 "Cannot find data type dbo.Nope", 2724, then 11501 |
| `@e int; DROP TABLE dbo.victim` | 102 "Incorrect syntax near ';'", then 11501; the table is still there |
| `@ids dbo.IdList READONLY` with `SELECT id FROM @ids` | `id int`: a table type is accepted, and is out of the epic all the same |

## Columns and nullability

`sys.dm_exec_describe_first_result_set(@tsql, @params, 1)`; `n` is `is_nullable`, and the origin is `source_schema.source_table.source_column`.  Under browse mode 1 the procedure adds the key columns of every table the query reads, and every `rowversion` column, as hidden columns, `is_hidden = 1`; they are left out below.

| Query | Columns, `n`, origin |
|----|----|
| `users u LEFT JOIN orders o` | `u.id` 0; `o.id` 1; `o.total` 1 |
| `RIGHT JOIN` | the left side 1, the right side 0 |
| `FULL JOIN`, with `ISNULL(u.id, o.user_id)` | every column 1, the `ISNULL` too |
| `LEFT JOIN ... WHERE o.total > 0` | `o.id` 1: the server does not reduce the join |
| `OUTER APPLY (SELECT TOP 1 ...)` | the applied columns 1 |
| `CROSS APPLY (SELECT TOP 1 ...)` | the applied columns 0 |
| `MAX(total)`, `SUM(total)`, `COUNT(*)`, `COUNT_BIG(*)` with no `GROUP BY` | all 1, **`COUNT(*)` included** |
| the same with `GROUP BY user_id` | the key 0 with its origin, the aggregates 1 |
| a scalar subquery of `MAX`, of `COUNT(*)` | both 1 |
| `CASE WHEN ... THEN name END`, with `ELSE N'x'`, `IIF(..., name, N'y')` | 1, 0, 0 |
| `ISNULL(nickname, N'-')`, `COALESCE(nickname, name)`, `COALESCE(nickname, deleted_at)`, `NULLIF(name, N'')`, `CONCAT(nickname, N'x')` | 0, **1**, 1, 1, 0 |
| `CAST(id AS varchar(10))`, `TRY_CAST(...)`, `CONVERT(nvarchar(20), created_at)`, `id + 1`, `id / 0` | **all 1** |
| `ROW_NUMBER() OVER (...)`, `LAG(id) OVER (...)`, `SUM(id) OVER ()` | **all 1** |
| `UNION` of `users.id, name` and `orders.id, note` | `id` 0, the second 1, no origin |
| `UNION ALL` of two `NOT NULL` ids | 0 |
| `GROUP BY ROLLUP(user_id)`, `GROUPING SETS ((user_id), (id))`, `CUBE(user_id)` | every key 1, with its origin |
| `SELECT * FROM dbo.user_orders`, the view over the outer join | `user_id` 0, `name` 0, `order_id` 1, `total` 1; origin the **base table** under mode 1 |
| a CTE over the outer join, a derived table | as the outer join, origins kept |
| `id, name_len, name_len_p, rv, email` | `id` identity; both computed columns 1, **`name_len_p` being `PERSISTED NOT NULL`**; `rv` 0; `email` 0 |
| `... FROM dbo.prices FOR SYSTEM_TIME ALL` | 0, **no origin** |
| `SELECT 1, id` | the first column's `name` is null |
| `SELECT u.id, o.id` | two columns named `id` |
| `CASE WHEN EXISTS (...) THEN 1 ELSE 0 END` | 0 |
| `FOR JSON PATH` | one column `JSON_F52E2B61-18A1-11d1-B105-00805F49916B`, `nvarchar(max)`, 1 |
| `OPENJSON(...) WITH (a int, b nvarchar(10))` | 1, 1 |
| `STRING_SPLIT(...)` | `value` 0 |
| `PIVOT (SUM(total) ...)` | 1 |
| `N'x'`, `1`, `NULL`, `CAST(NULL AS int)`, `1.5`, `SYSUTCDATETIME()`, `NEWID()` | 0, 0, 1, 1, 0, 0, **1** |
| `master.sys.databases` | origins in the system tables, and dozens of hidden columns that differ between 2017 and 2025; under mode 2 `source_database` is `mssqlsystemresource`, and `msdb` for `msdb.dbo.sysjobs`, where a table of the session's database gives the session's |
| `JSON_VALUE(...)`, `ISJSON(...)` | `nvarchar(4000)` 1, `int` 1 |

`INSERT`, `UPDATE`, `DELETE` and `MERGE` with `OUTPUT`:

| Statement | Columns, `n` |
|----|----|
| `INSERT ... OUTPUT inserted.id, inserted.name, inserted.nickname, inserted.created_at` | 0, 0, 1, 0, with origins |
| `UPDATE ... OUTPUT deleted.nickname, inserted.nickname, inserted.id` | 1, 1, 0 |
| `DELETE ... OUTPUT deleted.id, deleted.total` | 0, 0 |
| `MERGE` with `WHEN MATCHED`, `WHEN NOT MATCHED` and `WHEN NOT MATCHED BY SOURCE THEN DELETE`, `OUTPUT $action, inserted.id, inserted.total, deleted.id, deleted.total, s.user_id` | `$action` 0; **every `inserted` and `deleted` column 1**; the source's column 1 |
| `MERGE` with `WHEN NOT MATCHED` alone, `OUTPUT $action, inserted.id, inserted.total` | 0, 0, 0 |
| `UPDATE o ... OUTPUT inserted.id, u.name FROM orders o LEFT JOIN users u` | 0, **1** |
| `DELETE o OUTPUT deleted.id, u.name FROM orders o LEFT JOIN users u` | 0, **1** |
| `INSERT dbo.trig ... OUTPUT inserted.id`, a table with an `AFTER` trigger | 334 "cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause", then 11501 |
| `WITH s AS (...) INSERT ... OUTPUT inserted.id SELECT ...` | 0 |
| `DELETE ... OUTPUT deleted.id INTO dbo.victim`, `UPDATE` with no `OUTPUT`, `SELECT ... INTO #x` | no rows: no result set |

A computed column's nullability is the server's reading of its expression, not its declaration: `ISNULL(id, 0)` is 0, `(1) PERSISTED NOT NULL` is 0, `CAST(LEN(name) AS int) PERSISTED NOT NULL` is 1, while `sys.columns` says not null for all three.

Every place where `NULL` can come out was reported nullable; no column was found reported not null that can hold `NULL`.  What over-reports is listed in bold: `COUNT(*)`, `ROW_NUMBER()`, a cast, arithmetic, `NEWID()`, `COALESCE` with a non-null argument, and computed columns, a `PERSISTED NOT NULL` one included.

### Browse mode 2

`sys.dm_exec_describe_first_result_set(@tsql, @params, 2)` analyses the query as if a cursor were prepared on it:

| Query | Mode 1 | Mode 2 |
|----|----|----|
| `SELECT * FROM dbo.user_orders` | origins in `dbo.users` and `dbo.orders` | origins `dbo.user_orders.user_id` and so on: **the view** |
| any query | hidden key and `rowversion` columns of every table read | one hidden column, `ROWSTAT int` |
| identity and computed, nullability, `OUTPUT` | as mode 1 | as mode 1 |
| `FOR SYSTEM_TIME ALL` | no origin | no origin |

SqlClient under browse mode 2 answered every row of the two tables above as `sqlcmd` under mode 1 did, on both 2017 and 2025, but for the origins of a view's columns, which name the view; the columns of an `OUTPUT` clause have their table's column as origin, and `$action` none.  The hidden column's name, through SqlClient on both servers, is `ROWSTAT` followed by a NUL character, `ROWSTAT\0`.

## Statements and safety

| Query | Answer | Effect |
|----|----|----|
| `SELECT 1 AS a; SELECT 2 AS b` | `a` alone | |
| `SELECT 1 AS a; DROP TABLE dbo.victim` | `a` | the table is still there |
| `DROP TABLE dbo.victim; SELECT 1 AS a` | `a` | the table is still there |
| `WAITFOR DELAY '00:00:05'; SELECT 1 AS a` | `a`, at once | |
| `SHUTDOWN WITH NOWAIT` | no rows | the server kept running |
| `USE master; SELECT 1 AS a`, `SET ANSI_NULLS OFF; SELECT id ...` | described | |
| `DECLARE @x int = 1; SELECT id ... WHERE id = @x` | described; `@x` is a variable | |
| `CREATE TABLE #y (a int NOT NULL); SELECT a FROM #y` | 11525, error type 10, `TEMPORARY_TABLE`; through `sp_describe_undeclared_parameters`, 208 "Invalid object name '#y'" then 11501 | |
| `SELECT a FROM #z`, `#z` not existing | 208, then 11529 | |
| `IF @a = 1 SELECT id ... ELSE SELECT name ...` | 11509, error type 3, `CONFLICTING_RESULTS` | |
| `SELECT @p + 1 AS x`, `@p` undeclared | 11521, error type 8, `UNDECLARED_PARAMETER` | |
| `SELECT nope FROM dbo.users` | 207, then 11501 | |
| `SELEC 1` | 2812 "Could not find stored procedure 'SELEC'", then 11529 | |
| a `GO` line inside the text | 102 "Incorrect syntax near 'GO'", then 11501 | |
| `EXEC(N'SELECT 1 AS a')` | `a`: a literal string is analysed | |
| `DECLARE @s nvarchar(max) = N'...'; EXEC(@s)` | 11513, error type 4, `DYNAMIC_SQL`; `sp_describe_undeclared_parameters` accepts it | |
| `IF @a = 1 SELECT id ... ELSE SELECT name ...` through `sp_describe_undeclared_parameters` | `@a int`: the procedure does not look at result shapes | |
| `EXEC sp_who` | the procedure's columns: a procedure's first result set is analysed statically; `cmd` is `nchar(16)` on 2017 and `nchar(26)` on 2025 | |

The procedure is documented as static analysis, and nothing above ran: no table was dropped, no delay was waited, and the server was not shut down.  A failure comes back as rows, an error with its `error_type` first and `11501` or `11529` last.

A batch of several statements is described by its first result set and no other.  The owner chose, for SQL Server, to describe such a query by its first result set and to defer multiple result sets as `D-0006`; the alternative, counting statements with ScriptDom, was not taken.
