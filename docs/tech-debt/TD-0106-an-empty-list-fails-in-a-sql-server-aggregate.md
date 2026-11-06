# TD-0106 - An empty list fails in a SQL Server aggregate

## Problem

A query method sends an empty or a null list as one parameter in a subquery that holds no row, `IN (SELECT @statuses_0 WHERE 1 = 0)` ([`ListSqlWriter`](../../src/SqlSource/Generation/Emitting/QueryMethods/ListSqlWriter.cs), `EmptyOf` and the `count == 0` branch of `AppendSteps`).  SQL Server refuses a subquery in three places where a list of values is accepted:

- inside an aggregate's argument, `SUM(CASE WHEN status IN @statuses THEN 1 ELSE 0 END)`, `COUNT(IIF(id NOT IN @ids, 1, NULL))` or a `HAVING` of one: Msg 130;
- in a `GROUP BY` expression: Msg 144;
- in an `OUTPUT` clause: Msg 10705.

So the method throws `SqlException` for an empty or a null list, a null one on a property that cannot be null included, which is sent as empty, while every list that holds a value runs.  The build and the describe succeed, since the sample is `IN (@statuses)`.  A window aggregate, `PARTITION BY`, a `MERGE`'s `WHEN MATCHED AND` and an `UPDATE`'s `SET` accept the empty form.  PostgreSQL accepts it everywhere.

## Why it exists

No scalar empty form keeps `NOT IN` true for every row: `IN (NULL)` is never true, but `NOT IN (NULL)` is never true either, where an empty list must match every row.  So the empty form has to be a subquery, and the owner chose to record the limit and document a workaround rather than refuse such a query at parse time, which a closed list of aggregate names could not do for a user-defined aggregate.

## Impact

Low.  It needs a SQL Server `codegen` query with a list inside an aggregate, a `GROUP BY` or an `OUTPUT` clause, called with an empty or a null list.  The manual's known issue gives the workaround: test the list in a `WITH` query or a derived table and aggregate its result, which `CountAccountsWithStatusesTestedFirst` of `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Lists.sql` runs.  `CountStatuses_Empty_IsRefusedByTheServer` of `ListQueryTests` pins the failure.

## Proposed fix

Under `mssql`, report a list that stands inside the parentheses of an aggregate call, in a `GROUP BY` list or in an `OUTPUT` clause at parse time, with a diagnostic that names the workaround, from the words around the use in `SqlListUses`; keep the known issue for a user-defined aggregate, which no list of names covers.  Or have the method write such a use, for an empty list, as the constant the test gives for no match, which needs the expression's structure.  Then change the pinning test to the new behaviour, and remove the known issues from `docs/manual/sqlserver.md` and `docs/manual/parameters-and-tokens.md`.

## Trigger

A report of the `SqlException`, or a parse of the words around a list use that already finds the call it is inside.
