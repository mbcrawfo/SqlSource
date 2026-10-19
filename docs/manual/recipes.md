# Recipes

Short answers to "how do I...", each a sample you can copy and a link to the page that has the rules.  If a sample does not do what you expect, the page it links to says why.

- [Read one folder with another dialect](#folder-with-another-dialect)
- [Leave migration scripts out](#leave-migration-scripts-out)
- [Use several databases](#use-several-databases)
- [Check the sidecars in CI](#check-sidecars-in-ci)
- [Describe again after a schema change](#after-a-migration)
- [Use the queries with Dapper or your own ADO.NET code](#dapper-and-adonet)
- [Run the models through Dapper](#models-through-dapper)
- [Choose a table name at run time](#table-name-at-run-time)
- [Let a test project see the internals of the main project](#test-project-sees-internals)

<a id="folder-with-another-dialect"></a>

## Read one folder with another dialect

A project of PostgreSQL files keeps its reports for SQL Server in `Reporting/`.  Item metadata gives the folder its own dialect, and its own output, since the tool cannot describe SQL Server queries yet:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" SqlSourceOutput="sql" />
</ItemGroup>
```

For one file, markers in its file header do the same:

```sql mssql
-- dialect: mssql
-- output: sql

-- name: MonthlyTotals
SELECT [month], SUM(total) AS total FROM invoices GROUP BY [month];
```

Rules: [configure one folder differently](settings.md#configure-a-folder), [dialect](settings.md#setting-dialect), [which place wins](settings.md#precedence).

<a id="leave-migration-scripts-out"></a>

## Leave migration scripts out

```text
App/
    App.csproj
    Queries/
        UserRepository.cs      [SqlSourceGenerate] on UserRepository
        Users.sql              belongs to UserRepository
    Migrations/
        001_create_users.sql   not claimed by any type
```

A `.sql` file that no type claims is not read, so a folder of migration scripts beside your queries does no harm.  To keep the scripts away from the compiler altogether, remove them from the files the package hands it:

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

Rules: [which files belong to a type](generator.md#files-of-a-type), [which `.sql` files are included](settings.md#included-files).

<a id="use-several-databases"></a>

## Use several databases

In a PostgreSQL project (`SqlSourceDialect` set to `postgres`), name the logical database of each file, and give the tool a connection for each name:

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

The files of `reports` start with `-- database: reports`.

Rules: [database](settings.md#setting-database), [connections](tool.md#connections).

<a id="check-sidecars-in-ci"></a>

## Check the sidecars in CI

In a PostgreSQL project (`SqlSourceDialect` set to `postgres`), this GitHub Actions job starts PostgreSQL, creates the schema, and fails when a sidecar that is checked in does not match:

```yaml
jobs:
  sidecars:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:18
        env:
          POSTGRES_DB: app
          POSTGRES_PASSWORD: ci
        ports:
          - 5432:5432
        options: --health-cmd pg_isready --health-interval 5s --health-timeout 5s --health-retries 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet tool restore
      - run: dotnet restore
      - run: ./scripts/migrate.sh # creates your schema; replace with your own step
      - run: dotnet sqlsource describe --check
        env:
          SQLSOURCE_CONNECTION: Host=localhost;Database=app;Username=postgres;Password=ci
```

Rules: [check in CI](tool.md#check-in-ci), [connections](tool.md#connections) for the variable, [exit codes](tool.md#exit-codes), [SQLSRC219](diagnostics.md#sqlsrc219).

<a id="after-a-migration"></a>

## Describe again after a schema change

In a PostgreSQL project (`SqlSourceDialect` set to `postgres`), after a migration:

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
$ git add '*.sql.json'
$ git commit -m "Describe the queries again after the migration"
```

Rules: [after a schema change](tool.md#after-a-schema-change), [describing again](tool.md#describing-again).

<a id="dapper-and-adonet"></a>

## Use the queries with Dapper or your own ADO.NET code

The members SqlSource generates are plain strings, so any library can run them.  Dapper is a separate package (`dotnet add package Dapper`); ADO.NET needs none:

```csharp
using System.Data;
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    // Dapper
    public Task<GetUserDto> Get(int id) =>
        connection.QuerySingleAsync<GetUserDto>(Sql.GetUser, new { id });

    // ADO.NET
    public async Task<string?> GetName(int id)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = Sql.GetUser;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.GetString(reader.GetOrdinal("name")) : null;
    }
}
```

With `GetUser` as `SELECT id, name FROM users WHERE id = @id;`, and `GetUserDto` as its generated [model](generator.md#models).  SqlSource never opens a connection or binds a value: Dapper opens a closed connection for you, ADO.NET needs it open, and you give the database the value of each `@name` yourself.

Rules: [constants](generator.md#constants), [query parameters](parameters-and-tokens.md#query-parameters).

<a id="models-through-dapper"></a>

## Run the models through Dapper

```sql postgres
-- name: GetOrder
SELECT o.id, o.placed_at, c.name AS customer_name
FROM orders o
JOIN customers c ON c.id = o.customer_id
WHERE o.id = @orderId;
```

```csharp
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class OrderRepository(DbConnection connection)
{
    public Task<GetOrderDto?> Get(long orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderDto>(Sql.GetOrder, new GetOrderParams(orderId));
}
```

```csharp
// Once, when the application starts, before the first query
DefaultTypeMap.MatchNamesWithUnderscores = true;
```

The input model is the parameter object, and the output model is the type of a row.  Three things make Dapper read them:

- **Columns in snake_case need `DefaultTypeMap.MatchNamesWithUnderscores`**, as above: Dapper matches `customer_name` to `CustomerName` only with it.
- **Write the query's parameters in camelCase**, `@orderId` and not `@order_id`, since Dapper binds a property of the input model only to a parameter of its name, ignoring case.  A query that keeps `@order_id` gives Dapper its parameters by hand: `new { order_id = orderId }`.
- **Sorted models are classes.**  Dapper matches a record's constructor to the columns by position and name, so a record whose properties are sorted no longer matches.  A class is matched by name:

  ```xml
  <PropertyGroup>
      <SqlSourceGeneratorParameters>sort-output</SqlSourceGeneratorParameters>
      <SqlSourceOutputModelType>class</SqlSourceOutputModelType>
  </PropertyGroup>
  ```

A column whose alias ends in `!` or `?`, which sets its property's nullability, keeps the mark in its name, and Dapper does not match it to the property: give such a column an alias without the mark when you read it through Dapper, or read the row yourself.

Rules: [models](generator.md#models), [property names](generator.md#property-names), [the order of the properties](generator.md#property-order), [shape of a model](settings.md#setting-model-type), and [known issues](generator.md#known-issues) for parameters in snake_case and for a column whose name ends in `!` or `?`.

<a id="table-name-at-run-time"></a>

## Choose a table name at run time

```sql
-- name: CountRows
SELECT COUNT(*) FROM {{table:orders}};
```

```csharp
var table = kind == Kind.Archived ? "orders_archive" : "orders";
var sql = Sql.CountRows(table);
// SELECT COUNT(*) FROM orders_archive;
```

A query with a token is a method that returns the SQL with the token replaced.  The default after `:` is only a sample that the tool describes the query with, and the method still takes the table name; a query whose output is `sql` needs no default.  Pass only text that your own code chooses, as above, and never a value that a user typed: nothing is escaped.

Rules: [tokens](parameters-and-tokens.md#tokens), [token methods](generator.md#token-methods), [defaults](parameters-and-tokens.md#token-defaults).

<a id="test-project-sees-internals"></a>

## Let a test project see the internals of the main project

```xml
<!-- App.csproj -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

```csharp
// App.Tests, which references the SqlSource package too
using SqlSource;

[SqlSourceGenerate(Path = "Queries")]
internal static partial class TestQueries;
```

Rules: [projects that share internals](generator.md#projects-that-share-internals), and [known issues](generator.md#known-issues) for an editor that still shows the warning.
