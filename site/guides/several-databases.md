# Use several databases

Query a second database from one project, and give the tool two connection strings, one for each database.

**Use this when** the queries of one project run against two or more databases.  Use it too when the files of one folder are written for another kind of database.

<a id="use-several-databases"></a>

## Use several databases

This PostgreSQL project sets `"dialect": "postgres"` in its `sqlsource.json`.  Name the logical database of each file with a line such as `-- database: billing` at its top:

```sql postgres
-- database: billing

-- name: ListInvoices
SELECT id, total FROM invoices;
```

The files of `reports` start with `-- database: reports`.  Give the tool a connection for each name.  Then describe the queries:

```console
$ export SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
$ export SQLSOURCE_CONNECTION_REPORTS="Host=localhost;Database=reports;Username=app;Password=secret"
$ dotnet sqlsource describe
billing (postgres): 4 described, 0 skipped, 0 failed
reports (postgres): 2 described, 0 skipped, 0 failed
```

A repository that claims files of both databases marks the connection of each:

```csharp
[SqlSourceGenerate(Source = ".")]
public partial class Accounting(
    [SqlSourceConnection(Database = "billing")] NpgsqlDataSource billing,
    [SqlSourceConnection(Database = "reports")] NpgsqlDataSource reports);
```

**What you get.**  The tool describes the files of each database through the connection of its name.  Its summary has a line for each database.  `Accounting` runs the queries of each database on the data source marked for it.

**The rules you need.**

- **Several databases can share one dialect.**  Here `billing` and `reports` are both `postgres`.  See [database](../reference/settings.md#setting-database).
- **The variable of a name is `SQLSOURCE_CONNECTION_` and the name in upper case**, with each character that is not a letter or a digit written as `_`: `SQLSOURCE_CONNECTION_BILLING_V2` for `billing-v2`.  See [connections](../reference/connections.md#connections).
- **The build reads the database of each query too.**  It chooses the connection of each repository method by it.  See [database](../reference/settings.md#setting-database).
- **A repository that uses several databases marks one connection for each**, as `[SqlSourceConnection(Database = "billing")]`.  See [One repository on two databases](#repository-on-two) and [a repository's connection](../reference/repositories.md#repository-connection).
- **For files written for another kind of database, set their dialect.**  See [Read one folder with another dialect](#folder-with-another-dialect).

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

A project of PostgreSQL files keeps its reports for SQL Server in `Reporting/`.  A settings file in that folder sets the dialect of its files:

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

To set the dialect of one file, write a `-- dialect:` marker in its file header:

```sql mssql
-- dialect: mssql

-- name: MonthlyTotals
SELECT [month], SUM(total) AS total FROM invoices GROUP BY [month];
```

Give the tool a [connection](../reference/connections.md#connections) for each of the two databases, `postgres` and `mssql`:

```console
$ export SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
$ export SQLSOURCE_CONNECTION_MSSQL="Server=localhost,1433;Database=reports;User ID=app;Password=secret;TrustServerCertificate=True"
$ dotnet sqlsource describe
```

**What you get.**  SqlSource reads the files under `Reporting/` as `mssql`, and every other file of the project as `postgres`.  It reads a file with the marker as `mssql` too.  The tool describes the reports against SQL Server, as the database `mssql`, beside the database `postgres` of the other files.

**The rules you need.**

- **A settings file sets the dialect of the `.sql` files of its folder and of the folders below it.**  See [settings files](../reference/settings-file.md#settings-files) and [configure one folder differently](folders-and-files.md#configure-a-folder).
- **A `-- dialect:` marker in the file header wins over every settings file.**  See [dialect](../reference/settings.md#setting-dialect) and [which place wins](../reference/settings.md#precedence).
- **If the reports need only their SQL, set their output to `sql`**, as `"output": "sql"` in `Reporting/sqlsource.json`.  The tool then leaves them out, and needs no connection for them.  See [output](../reference/settings.md#setting-output).

## If it does not work

- **SQLSRC030.**  A member is marked `[SqlSourceConnection]` with no `Database`, and the queries use more than one database.  Set its `Database`.  See [SQLSRC030](../diagnostics.md#sqlsrc030).
- **SQLSRC031, a warning.**  No repository query of the class uses the database of a mark.  Its `Database` is misspelt, or the queries of that database have another output.  See [SQLSRC031](../diagnostics.md#sqlsrc031).
- **SQLSRC028.**  No member is marked for a database that the repository queries use.  Mark one, with its `Database`.  See [SQLSRC028](../diagnostics.md#sqlsrc028).
- **SQLSRC213, from the tool.**  A database has a query to describe and no connection.  Set the variable of its name.  See [SQLSRC213](../diagnostics.md#sqlsrc213).
