# Other databases

SqlSource can describe the queries of the databases marked Yes in the table of [database support](index.md#database-support), and of no other.  This page is for each database that the table marks No.

For the steps to use SqlSource with one of these databases, with the output `sql`, see [Use MySQL, SQLite, Oracle or another database](../../guides/other-databases.md).

<a id="not-described"></a>

## Databases that are not described

A query whose output is `repository`, `methods` or `models` must be described.  If its dialect is one of the rows marked No, the query has no database to ask.  It is then [SQLSRC209](../../diagnostics.md#sqlsrc209), from the tool and in the build.

The output for these databases is `sql`.  It asks for the SQL alone and needs no database, so SQLSRC209 goes away.  See [the output setting](../settings.md#setting-output).

- **The default output is `repository`.**  So a project that sets neither the dialect nor the output gets SQLSRC209 for each file that a class reads.
- **Under the output `models`, the query still gets its constant or token method.**
- **Under the output `methods`, the query gets neither a method nor a constant.**

<a id="which-dialect"></a>

## Which dialect to set

To find the dialect for your database, see [the dialect of each database](../dialects.md#choosing-a-dialect).

<a id="duckdb"></a>

## DuckDB

DuckDB shares the dialect `postgres` with PostgreSQL, but it cannot be described.  This is true of any database that only shares a dialect with a database marked Yes.  SqlSource reads its queries by the rules of that dialect, and the tool cannot describe them.

The tool still tries to describe a file read as `postgres` whose output is `repository`, `methods` or `models`.  The build also asks for the entries of that file.  So give a DuckDB file the output `sql`: see [the output setting](../settings.md#setting-output).
