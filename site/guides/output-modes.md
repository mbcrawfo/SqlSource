# Choose what is generated (output modes)

Choose the output of a query, `repository`, `methods`, `models` or `sql`, and so what SqlSource generates for it.

**Use this when** you see `-- output:` or `"output"`, and ask which one to use.  Most projects keep the default, `repository`, and never set it.

<a id="the-four-outputs"></a>

## The four outputs

`repository` is the default, and the one to use.

| Output | You get | Use it when |
|----|----|----|
| `repository` | The models, and a [method of your class](../reference/repositories.md#repository-methods) that gets its own connection | You call the query from your application |
| `methods` | The models, and a method that takes the connection, a transaction and a command timeout | Your own code runs the query, above all in a transaction, or with a command timeout |
| `models` | The SQL as a constant, and the models | Dapper or your own ADO.NET code runs the query |
| `sql` | The SQL as a constant, and nothing else | You want only the SQL, or your database cannot be described |

`models` and `sql` are fallbacks, mainly for a project that runs its SQL through Dapper or its own ADO.NET code.  See [Output](../reference/settings.md#setting-output).

**The rules you need.**

- A repository method takes no transaction and no command timeout.  See [Transactions and timeouts](../reference/repositories.md#repository-transactions).
- A `methods` method is by default an `internal` extension method of `DbConnection`, called as `connection.GetUser(...)`.  See [Where the methods go](../reference/connection-methods.md#method-location).
- Under `models` and `sql`, a query with a token gets a method that returns its SQL in place of a constant.  See [Token methods](../reference/members.md#token-methods).

<a id="set-the-output"></a>

## Set the output of a query, a file or a folder

Use this when one query, one file or one folder needs another output.

For one query, write `-- output:` inside the query:

```sql postgres
-- name: RenameUser -> rowcount
-- summary: Changes the name of one user.
-- output: methods
UPDATE users
SET name = @name
WHERE id = @userId;
```

For every query of a file, write it in the file header:

```sql postgres
-- output: sql

-- name: CountUsers
-- summary: The number of users.
SELECT count(*) FROM users;
```

For the `.sql` files of a folder and of its subfolders, write `output` in a `sqlsource.json` in that folder:

```jsonc
// Models/sqlsource.json
{
  "output": "models",
}
```

**What you get.**  `RenameUser` is an extension method of `DbConnection`, and the other queries of its file keep their repository methods.  `CountUsers` is the constant `Sql.CountUsers`.

**The rules you need.**

- If the output is set in several places, the nearest place to the query wins.  See [Which place wins](../reference/settings.md#precedence).
- One file can hold queries of several outputs.  See [Output](../reference/settings.md#setting-output).
- A constant is in a private class `Sql` nested in your class, so only your class's own code can use it.  See [Where the members go](../reference/member-placement.md#locations).

<a id="which-need-a-database"></a>

## Which outputs need a database

Read this to know when you need the `sqlsource` tool and a database.

`repository`, `methods` and `models` need each query described: the `sqlsource` tool asks your database about the query, and writes the answer to a sidecar file.  `sql` needs no tool, no sidecar and no connection.  See [Output](../reference/settings.md#setting-output).

In a project whose queries are described, give a query `-- output: sql` in two cases:

- **Your database cannot describe the statement**, such as `CALL` on PostgreSQL (SQLSRC233) or `CREATE PROCEDURE` on SQL Server (SQLSRC225).  Run its constant with your own code.  See what can be described on [PostgreSQL](../reference/databases/postgresql/index.md#what-can-be-described) and on [SQL Server](../reference/databases/sqlserver/index.md#what-can-be-described).
- **A method cannot send the query's parameter**, such as a table-valued parameter on SQL Server.  Send the parameter with your own code.  See [Lists](../reference/databases/sqlserver/query-methods.md#list-parameters).

**The rules you need.**

- A query gets models only when its output is not `sql` and its entry in its sidecar is current.  See [Which queries get models](../reference/models.md#which-models).

See also [Run queries in a transaction](../start/transactions.md#queries-in-a-transaction), [Use Dapper or your own ADO.NET code](dapper.md) and [Use MySQL, SQLite, Oracle or another database](other-databases.md).

## If it does not work

- **SQLSRC241 or SQLSRC244.**  A query that must be described has no current entry in its sidecar.  Run `dotnet sqlsource describe`.  See [SQLSRC241](../diagnostics.md#sqlsrc241) and [SQLSRC244](../diagnostics.md#sqlsrc244).
- **SQLSRC209.**  Only PostgreSQL and SQL Server can be described.  Set the dialect to `postgres` or `mssql`, or the output to `sql`.  See [SQLSRC209](../diagnostics.md#sqlsrc209) and [Database support](../reference/databases/index.md#database-support).
