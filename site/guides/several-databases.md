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
