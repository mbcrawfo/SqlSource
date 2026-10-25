# D-0006 - Multiple result sets on SQL Server

## Planned

The [query generation epic](../superpowers/specs/2026-10-07-query-generation-epic-design.md) scopes one result set per query and leaves multiple result sets out of the epic.  [The spike of phase 6](../superpowers/specs/2026-10-20-query-generation-phase-6-sql-server-spike.md) found that SQL Server's describe procedure answers a batch of several statements with its first result set and says nothing of the others, so a query that returns two result sets, `SELECT ...; SELECT ...`, is described as if it returned one, and one that runs a statement before its `SELECT` is described by the `SELECT` alone.  PostgreSQL's describer refuses a query of several statements; the choices for SQL Server were to refuse such a query by counting its statements with Microsoft's T-SQL parser, ScriptDom, to allow it and describe its first result set, or to allow it only when the other statements return no rows.

## Delivered instead

[Sub-phase 6.2](../superpowers/specs/2026-10-20-query-generation-phase-6-2-columns-and-table-match-design.md) describes a SQL Server query by its first result set, as the server does: the `columns` call of [`SqlServerSession`](../../src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs) asks `sys.dm_exec_describe_first_result_set`, which describes the first result set of a batch and says nothing of the others, and the fixture `select-first-result-set` holds it.  The manual's [SQL Server page](../manual/sqlserver.md#what-can-be-described) says that only a query's first result set is described and read into its model, and lists it among its known issues.  From [sub-phase 8.1](../superpowers/specs/2026-10-24-query-generation-phase-8-1-methods-design.md), the generated method of such a query, which [`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) writes, reads the rows of its first result set alone, into that model, with one reader under `CommandBehavior.Default`; disposing the reader runs the batch's other statements and drops their rows, and the [Query methods section of the SQL Server page](../manual/sqlserver.md#query-methods) says so.

## Why deferred

The owner chose to allow a batch of several statements, which T-SQL code often is, `SET NOCOUNT ON;` before a `SELECT` or an `UPDATE` before the `SELECT` that reads its effect, over adding a T-SQL parser to the tool to refuse it; and multiple result sets are out of the epic.

## Remaining work

Models, and a method that reads more than the first, for a query that returns several result sets: a way to find each result set's shape, which the procedure does not give, by ScriptDom or by describing each statement apart; an output model per result set and their names; the sidecar's shape for several column lists, which bumps `formatVersion` or adds a key by the format's rules; and the tests against the container.

## Trigger

Users of SQL Server asking for queries that return several result sets, or a decision to support stored procedures.
