# Use MySQL, SQLite, Oracle or another database

Keep the SQL of a MySQL, MariaDB, SQLite, Oracle, CockroachDB or DuckDB project in `.sql` files, as C# constants.

**Use this when** your database is not PostgreSQL or SQL Server.  You get the SQL alone, and run it with your own code.

1. [Add the package](#install).
2. [Write a query](#write-a-query).
3. [Mark a class](#mark-a-class).
4. [Set the dialect and the output `sql`](#dialect-and-output).
5. [Run the constant](#run-the-constant).

<a id="install"></a>

## Add the package

**Step 1.**  In your project's folder, add the package, and your database's driver:

```console
$ dotnet add package SqlSource
$ dotnet add package Microsoft.Data.Sqlite
```

A project whose queries all have the output `sql` needs no `sqlsource` tool.  See [The tool](../reference/cli.md#tool).

<a id="write-a-query"></a>

## Write a query

**Step 2.**  Save your queries as `Queries/UserQueries.sql`:

```sql sqlite
-- name: CountUsers
-- summary: The number of users.
SELECT count(*) FROM users;

-- name: CountRows
-- summary: The number of rows of a table that the code chooses.
SELECT count(*) FROM {{table}};
```

**The rules you need.**

- `-- name:` starts a query, and its name is a C# identifier.  See [Queries and names](../reference/sql-files.md#queries-and-names).
- On MySQL and MariaDB, a marker needs white space after `--`, and a `#` comment is never a marker.  See [Markers under `mysql`](../reference/dialects.md#markers-under-mysql).

<a id="mark-a-class"></a>

## Mark a class

**Step 3.**  Save a partial class of the file's name as `Queries/UserQueries.cs`:

```csharp
// Queries/UserQueries.cs
using SqlSource;

[SqlSourceGenerate]
public sealed partial class UserQueries;
```

**What you get.**  The class gets `UserQueries.sql`, the file named for it in its folder.  The class must be `partial`.  See [The attribute](../reference/attributes.md#attribute) and [Source](../reference/attributes.md#setting-source).

<a id="dialect-and-output"></a>

## Set the dialect of your database and the output `sql`

- **Any dialect works**: `mssql` for SQL Server, `mysql`, `sqlite`, the default `ansi` and the others.  Set the one named for your database, so that SqlSource reads your comments and strings as your database does.

**Step 4.**  Save this `sqlsource.json`:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "sqlite",
    "output": "sql",
}
```

Use `mysql`, `mariadb`, `sqlite`, `oracle` (also for Firebird) or `cockroachdb` for that database, `postgres` for DuckDB, and `ansi` for any other.

**The rules you need.**

- The dialect tells SqlSource where a comment or a string ends.  See [Choosing a dialect](../reference/dialects.md#choosing-a-dialect).
- The output `sql` gives each query its SQL alone, and needs no sidecar and no connection.  See [Output](../reference/settings.md#setting-output).

<a id="run-the-constant"></a>

## Run the constant

**Step 5.**  Run the SQL with your driver:

```csharp
// Queries/UserQueries.cs
using Microsoft.Data.Sqlite;
using SqlSource;

[SqlSourceGenerate]
public sealed partial class UserQueries(SqliteConnection connection)
{
    public long CountUsers() => Count(Sql.CountUsers);

    public long CountRows(string table) => Count(Sql.CountRows(table));

    private long Count(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
```

**What you get.**  `Sql.CountUsers` is a `const string`.  `Sql.CountRows(table)` returns the SQL with the token filled in.

**The rules you need.**

- `Sql` is a private class nested in `UserQueries`, so only its own code can use it.  See [Where the members go](../reference/members.md#locations).
- The SQL is as you wrote it, without its markers and comments.  A query parameter such as `@id` stays in it: give the database its value yourself.  See [Constants](../reference/members.md#constants) and [What reaches the generated SQL](../reference/sql-files.md#generated-sql).
- Nothing in a token is escaped.  Pass only text that your own code chooses.  See [Token methods](../reference/members.md#token-methods).

<a id="what-you-do-not-get"></a>

## What you do not get

You get no models and no method that runs the query.  Nothing checks a query against your schema, so a wrong table or column fails only when the query runs.

**The rules you need.**

- Only PostgreSQL and SQL Server can be described.  On another dialect, an output other than `sql` is [SQLSRC209](../diagnostics.md#sqlsrc209), `models` included.  See [Databases that are not described](../reference/databases/other.md#not-described).
- If your database is DuckDB, set the dialect to `postgres` and the output to `sql`.  The tool cannot describe a DuckDB query.  See [DuckDB](../reference/databases/other.md#duckdb).

## If it does not work

- **SQLSRC101 or SQLSRC102 on valid SQL, or SQL missing from a constant.**  SqlSource reads a few constructs differently from your database.  See [What is still read differently](../reference/dialects.md#still-read-differently).
