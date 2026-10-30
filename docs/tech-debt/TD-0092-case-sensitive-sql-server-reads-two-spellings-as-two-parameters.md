# TD-0092: a case-sensitive SQL Server reads two spellings of a parameter as two

## Problem

SqlSource reads two names that differ only in case as one parameter, by [`SqlParameterName`](../../src/SqlSource/Parsing/SqlParameterName.cs)'s `Comparer`, `Comparison` and `IndexOf`, and the manual says so: `@Id` and `@id` are one parameter.  A query's list then holds the name once, as the SQL first writes it, and the generated method binds that spelling alone.  A SQL Server whose instance collation is case-sensitive reads a variable's name by that collation, so there `@id` is another variable, which nothing declares.

A query such as `UPDATE dbo.t SET a = @Id WHERE b = @id` lists one parameter, `Id`.  The describe passes: [`ParameterCopies.Rename`](../../src/SqlSource.Tool/Describing/SqlServer/ParameterCopies.cs) counts `Id` twice ignoring case and sends copies, `CompareLists` maps both back to `Id`, and `columns` sends the SQL as written, which for a statement with no result set answers no rows and no error.  The generated method then throws `Msg 137`, "Must declare the scalar variable "@id"", at run time.  A query with a result set fails its `columns` describe with `11521` instead.

Observed by a probe on `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04` started with `MSSQL_COLLATION=Latin1_General_CS_AS`:

- `EXEC sp_executesql N'SELECT @Id AS v', N'@Id int', @Id = 1` returns `1`.
- `EXEC sp_executesql N'SELECT @id AS v', N'@Id int', @Id = 1` returns `Msg 137`.
- `sp_describe_undeclared_parameters N'SELECT @Id + 1 AS a, @id + 1 AS b'` returns two rows, `@Id` and `@id`.
- `sys.dm_exec_describe_first_result_set(N'UPDATE dbo.zz_t SET a = @Id WHERE b = @id', N'@Id int', 2)` returns no rows and no `error_number`.

No test pins it: no fixture runs a case-sensitive instance collation.

## Why it exists

The rule of one parameter ignoring case was written for the drivers, which bind such names as one, and for the default instance collations, which ignore case.  It was found by the review of TD-0079's item 5, which put the comparison of parameter names in one place without changing it.

## Impact

Low.  It needs a SQL Server whose instance collation is case-sensitive and a query that writes one parameter in two spellings.  When it happens, a statement with no result set builds and describes cleanly and fails only when it runs.

## Proposed fix

Report an `mssql` query that writes one parameter in two spellings, in the parser or in the describer, so that the problem surfaces before run time, and remove the known issue of `docs/manual/sqlserver.md`.  A test on a SQL Server container with a case-sensitive instance collation describes and runs such a query.

## Trigger

A user's report of `Msg 137` from a generated method, or a test fixture that runs a case-sensitive instance collation.
