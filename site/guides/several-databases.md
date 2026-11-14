# Use several databases

<a id="use-several-databases"></a>

## Use several databases

For several databases of one kind, name the database of each file with a `-- database: billing` line at its top, and give the tool a connection for each name.  A repository that uses several marks a connection for each, `[SqlSourceConnection(Database = "billing")]`: see [the recipe](#use-several-databases).  For files written for another kind of database, give their folder its own dialect with a `sqlsource.json` of its own, or one file a `-- dialect:` line at its top: see [the recipe](#folder-with-another-dialect).

In a PostgreSQL project (`"dialect": "postgres"` in its `sqlsource.json`), name the logical database of each file, and give the tool a connection for each name:

```sql postgres
-- database: billing

-- name: ListInvoices
SELECT id, total FROM invoices;
```

```console
$ export SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
$ export SQLSOURCE_CONNECTION_REPORTS="Host=localhost;Database=reports;Username=app;Password=secret"
$ dotnet sqlsource describe
billing (postgres): 4 described, 0 skipped, 0 failed
reports (postgres): 2 described, 0 skipped, 0 failed
```

The files of `reports` start with `-- database: reports`.  A repository that claims files of both marks the connection of each:

```csharp
[SqlSourceGenerate(Source = ".")]
public partial class Accounting(
    [SqlSourceConnection(Database = "billing")] NpgsqlDataSource billing,
    [SqlSourceConnection(Database = "reports")] NpgsqlDataSource reports);
```

Rules: [database](../reference/settings.md#setting-database), [connections](../reference/connections.md#connections), [a repository's connection](../reference/repositories.md#repository-connection).

<a id="repository-on-two"></a>

## One repository on two databases

If one repository class runs queries of two databases, mark a connection for each database.  Here `Users.sql` and `Invoices.sql` are in one folder:

```sql postgres
-- Users.sql
-- database: main

-- name: GetUser -> one-optional
SELECT id, name FROM users WHERE id = @id;
```

```sql postgres
-- Invoices.sql
-- database: billing

-- name: GetInvoice
SELECT id, number, amount, paid_at
FROM billing.invoices
WHERE id = @invoiceId;
```

# [PostgreSQL](#tab/postgresql)

```csharp
[SqlSourceGenerate(Source = ".")]
public partial class AccountRepository(
    [SqlSourceConnection(Database = "main")] NpgsqlDataSource main,
    [SqlSourceConnection(Database = "billing")] NpgsqlDataSource billing);
```

```dotenv
SQLSOURCE_CONNECTION_MAIN="Host=localhost;Database=main;Username=app;Password=secret"
SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
```

# [SQL Server](#tab/sqlserver)

```csharp
[SqlSourceGenerate(Source = ".")]
public partial class AccountRepository(
    [SqlSourceConnection(Database = "main")] string mainConnectionString,
    [SqlSourceConnection(Database = "billing")] string billingConnectionString);
```

```dotenv
SQLSOURCE_CONNECTION_MAIN="Server=localhost,1433;Database=main;User ID=app;Password=secret;TrustServerCertificate=True"
SQLSOURCE_CONNECTION_BILLING="Server=localhost,1433;Database=billing;User ID=app;Password=secret;TrustServerCertificate=True"
```

---

**What you get.**  `GetUser` runs on a connection of `main`, and `GetInvoice` on a connection of `billing`.  The tool describes each file through the connection of its database.

**The rules you need.**

- A `-- database:` line in the file header names the logical database of each query of the file.  A file with no such line belongs to the database named after its dialect, `postgres` or `mssql`.  See [Database](../reference/settings.md#setting-database).
- If the repository queries of a class use more than one database, mark one member for each database.  The build then finds no member by its name or type.  See [The connection](../reference/repositories.md#repository-connection).
- On SQL Server, which has no data source, mark a connection string, a method that returns a connection, or a connection.  See [The connection](../reference/repositories.md#repository-connection).
- The tool needs a connection for each name.  `SQLSOURCE_CONNECTION` alone serves no database when the run has more than one.  See [Connections](../reference/connections.md#connections).

<a id="folder-with-another-dialect"></a>

## Read one folder with another dialect

A project of PostgreSQL files keeps its reports for SQL Server in `Reporting/`.  A settings file in that folder gives it its own dialect:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

```jsonc
// Reporting/sqlsource.json
{
    "dialect": "mssql",
}
```

For one file, a marker in its file header does the same:

```sql mssql
-- dialect: mssql

-- name: MonthlyTotals
SELECT [month], SUM(total) AS total FROM invoices GROUP BY [month];
```

The reports are described against SQL Server, as the database `mssql`, beside the database `postgres` of the other files, so the tool needs a connection for each:

```console
$ export SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
$ export SQLSOURCE_CONNECTION_MSSQL="Server=localhost,1433;Database=reports;User ID=app;Password=secret;TrustServerCertificate=True"
$ dotnet sqlsource describe
```

Add `"output": "sql"` to `Reporting/sqlsource.json`, or `-- output: sql` to a file, when the reports want their SQL alone: the tool then leaves them out and needs no connection for them.

Rules: [configure one folder differently](folders-and-files.md#configure-a-folder), [settings files](../reference/settings-file.md#settings-files), [dialect](../reference/settings.md#setting-dialect), [which place wins](../reference/settings.md#precedence), [connections](../reference/connections.md#connections).

## If it does not work

- **SQLSRC030.**  A member is marked `[SqlSourceConnection]` with no `Database`, and the queries use more than one database.  Set its `Database`.  See [SQLSRC030](../diagnostics.md#sqlsrc030).
- **SQLSRC031, a warning.**  No repository query of the class uses the database of a mark.  Its `Database` is misspelt, or the queries of that database have another output.  See [SQLSRC031](../diagnostics.md#sqlsrc031).
- **SQLSRC028.**  No member is marked for a database that the repository queries use.  Mark one, with its `Database`.  See [SQLSRC028](../diagnostics.md#sqlsrc028).
- **SQLSRC213, from the tool.**  A database has a query to describe and no connection.  Set the variable of its name.  See [SQLSRC213](../diagnostics.md#sqlsrc213).
