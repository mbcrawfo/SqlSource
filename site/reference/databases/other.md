# Other databases

SqlSource can describe the queries of the databases marked Yes in the table of [database support](index.md#database-support), and of no other.  This page is for a database of every row marked No.

<a id="not-described"></a>

## Databases that are not described

A query that must be described and whose dialect has no database to ask, every row marked No, is [SQLSRC209](../../diagnostics.md#sqlsrc209), from the tool and in the build.  The default output is `repository`, which must be described, so a project that sets neither the dialect nor the output gets it for every file.  It goes away when the query's output is `sql`, which asks for the SQL alone and needs no database: see [the output setting](../../settings.md#setting-output).  Under the output `models` the query's constant or token method is generated either way; under `methods` it gets neither a method nor a constant.

<a id="which-dialect"></a>

## Which dialect to set

To find the dialect for your database, see [choosing a dialect](../dialects.md#choosing-a-dialect).

<a id="duckdb"></a>

## DuckDB

A database that merely shares a dialect with one of them, such as DuckDB with `postgres`, is read by that dialect's rules and cannot be described.  The tool still tries to describe a file read as `postgres` whose output is `models`, `methods` or `repository`, and the build asks for its entries, so give a DuckDB file the output `sql`: see [the output setting](../../settings.md#setting-output).
