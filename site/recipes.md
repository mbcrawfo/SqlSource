# Recipes

Short answers to "how do I...", each a sample you can copy and a link to the page that has the rules.  If a sample does not do what you expect, the page it links to says why.

- [Read one folder with another dialect](#folder-with-another-dialect)
- [Leave migration scripts out](#leave-migration-scripts-out)
- [Use several databases](#use-several-databases)
- [Check the sidecars in CI](#check-sidecars-in-ci)
- [Describe again after a schema change](#after-a-migration)
- [Start the watcher with your editor](#start-the-watcher-with-your-editor)
- [Run queries in a transaction](#queries-in-a-transaction)
- [Insert many rows in one call](#insert-many-rows)
- [Run a query from code that does not await](#run-without-awaiting)
- [Give a repository its connection from a service of your own](#connection-from-a-provider)
- [Make a SQL Server repository from a connection string](#sql-server-repository)
- [Give an id a type of its own](#strongly-typed-id)
- [Read a class of your own from a JSON column](#class-in-a-json-column)
- [Use the queries with Dapper or your own ADO.NET code](#dapper-and-adonet)
- [Run the models through Dapper](#models-through-dapper)
- [Choose a table name at run time](#table-name-at-run-time)
- [Filter by an optional list of values](#filter-by-a-list)
- [Let a test project see the internals of the main project](#test-project-sees-internals)
- [Share the exceptions between two data projects and an app](#share-the-exceptions)

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

Rules: [configure one folder differently](settings.md#configure-a-folder), [settings files](settings.md#settings-files), [dialect](settings.md#setting-dialect), [which place wins](settings.md#precedence), [connections](tool.md#connections).

<a id="leave-migration-scripts-out"></a>

## Leave migration scripts out

```text
App/
    App.csproj
    Queries/
        UserRepository.cs      [SqlSourceGenerate] on UserRepository
        UserRepository.sql     belongs to UserRepository
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

Rules: [database](settings.md#setting-database), [connections](tool.md#connections), [a repository's connection](generator.md#repository-connection).

<a id="check-sidecars-in-ci"></a>

## Check the sidecars in CI

In a PostgreSQL project (`"dialect": "postgres"` in its `sqlsource.json`), this GitHub Actions job starts PostgreSQL, creates the schema, and fails when a sidecar that is checked in does not match:

```yaml
jobs:
  sidecars:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:18.6
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

In a SQL Server project (`"dialect": "mssql"` in its `sqlsource.json`), the service and the connection change; the steps are the same, and the step that creates your schema creates the database too:

```yaml
    services:
      mssql:
        image: mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: Ci-Passw0rd
        ports:
          - 1433:1433
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P Ci-Passw0rd -Q 'SELECT 1'"
          --health-interval 5s --health-timeout 5s --health-retries 20
```

```yaml
      - run: dotnet sqlsource describe --check
        env:
          SQLSOURCE_CONNECTION: Server=localhost,1433;Database=app;User ID=sa;Password=Ci-Passw0rd;TrustServerCertificate=True
```

`TrustServerCertificate=True` is for the job's own server, whose certificate nothing trusts: see [connecting to SQL Server](sqlserver.md#connecting).

Name the server's exact version in the image, a PostgreSQL minor or a SQL Server cumulative update, and move it when you move your database: a tag such as `-latest` moves to a new server without a word, and a new server can describe a query otherwise than the one your sidecars were written on.

Rules: [check in CI](tool.md#check-in-ci), [connections](tool.md#connections) for the variable, [exit codes](tool.md#exit-codes), [SQLSRC219](diagnostics.md#sqlsrc219).

<a id="after-a-migration"></a>

## Describe again after a schema change

In a PostgreSQL project (`"dialect": "postgres"` in its `sqlsource.json`), after a migration:

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
$ git add '*.sql.json'
$ git commit -m "Describe the queries again after the migration"
```

Rules: [after a schema change](tool.md#after-a-schema-change), [describing again](tool.md#describing-again).

<a id="start-the-watcher-with-your-editor"></a>

## Start the watcher with your editor

Have your editor start `dotnet sqlsource describe --watch` for you, so the sidecars stay current as you save.  The rules are in [describe on save](tool.md#describe-on-save), and what your editor does with a sidecar the watch writes is in [what your editor shows](tool.md#what-your-editor-shows).

### VS Code

```jsonc
// .vscode/tasks.json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "sqlsource watch",
      "type": "shell",
      "command": "dotnet sqlsource describe --watch",
      "isBackground": true,
      "runOptions": { "runOn": "folderOpen" },
      "presentation": { "reveal": "silent", "panel": "dedicated" },
      "problemMatcher": {
        "owner": "sqlsource",
        "source": "sqlsource",
        "fileLocation": ["autoDetect", "${workspaceFolder}"],
        "pattern": {
          "regexp": "^(?!sqlsource )(\\S.*?)(?:\\((\\d+),(\\d+)\\))?\\s?: (error|warning) (SQLSRC\\d+): (.*)$",
          "file": 1, "line": 2, "column": 3, "severity": 4, "code": 5, "message": 6
        },
        "background": {
          "activeBegin": true,
          "beginsPattern": "^sqlsource: change detected; describing$",
          "endsPattern": "^sqlsource: watching for changes; press Ctrl\\+C to stop$"
        }
      }
    }
  ]
}
```

- **It starts when you open the folder.**  The first time, VS Code asks whether to allow automatic tasks in every folder you trust, and keeps your answer; it never runs one in a folder it does not trust.  The watch runs in a terminal panel of its own.  If it does not start when you open the folder, run **Tasks: Manage Automatic Tasks** from the Command Palette, choose **Allow Automatic Tasks** and open the folder again, or start it once with **Tasks: Run Task** and `sqlsource watch`.
- **An error at a file is listed in the Problems panel**, at that file, and at the query's line and column when it has one, from the error's first line; the lines under it, such as `see:`, stay in the terminal.  An error about no file, such as [SQLSRC200](diagnostics.md#sqlsrc200), stays in the watch's terminal, which the task does not bring forward: look there when a pass reports errors and the panel shows none.
- **Open the folder that holds your solution or project.**  If you open a folder above it, add `"options": { "cwd": "${workspaceFolder}/src/App" }` to the task, with the path of that folder.

This recipe has not been checked in VS Code.

### Rider

1. **Run > Edit Configurations**, add a **Shell Script** configuration, choose **Script text** and give it `dotnet sqlsource describe --watch`, with the solution's folder as its working directory.
2. **Settings > Tools > Startup Tasks**, add that configuration.

The watch then starts when the solution opens, in a terminal pane, where the path of each error is a link to its file and line.  Checked in Rider 2026.2 on macOS.

### Visual Studio

Open **View > Terminal**, the Developer PowerShell, and run `dotnet sqlsource describe --watch` in the solution's folder.  It runs until you stop it with Ctrl+C or close the terminal.  This has not been checked in Visual Studio.

Rules: [describe on save](tool.md#describe-on-save), [what your editor shows](tool.md#what-your-editor-shows).

<a id="queries-in-a-transaction"></a>

## Run queries in a transaction

```sql postgres
-- name: GetAccount -> one-optional
SELECT id, owner, balance FROM accounts WHERE id = @accountId;

-- name: GetBalance -> one
-- output: methods
SELECT balance FROM accounts WHERE id = @accountId FOR UPDATE;

-- name: SetBalance -> none
-- output: methods
UPDATE accounts SET balance = @balance WHERE id = @accountId;
```

```csharp
[SqlSourceGenerate]
public partial class AccountRepository(NpgsqlDataSource dataSource)
{
    public async Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var account = await connection.GetBalance(
            new GetBalanceParams(accountId), transaction, cancellationToken: cancellationToken);
        await connection.SetBalance(
            new SetBalanceParams(account.Balance - amount, accountId), transaction, cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}

// Adds Withdraw to the generated interface, beside GetAccount
public partial interface IAccountRepository
{
    Task Withdraw(int accountId, decimal amount, CancellationToken cancellationToken = default);
}
```

A repository method takes no transaction, so the queries that run in one take the output `methods`, whose methods take the connection and the transaction, and a method of your own on the repository opens the connection, begins the transaction and calls them.  `GetAccount` runs alone, and stays a repository method.  Give every method the transaction: [the connection](generator.md#method-connection) says why.  A query that needs a command timeout takes `methods` in the same way, and its `commandTimeout`.

Rules: [transactions and timeouts](generator.md#repository-transactions), [the connection](generator.md#method-connection), [the signature](generator.md#method-signature), [the interface](generator.md#repository-interface).

<a id="insert-many-rows"></a>

## Insert many rows in one call

```sql postgres
-- name: AddUser -> one
-- generator: collection-method
INSERT INTO users (name, email)
VALUES (@name, @email)
RETURNING id;
```

```csharp
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// elsewhere
var newUsers = signups.Select(signup => new AddUserParams(signup.Name, signup.Email));
AddUserDto[] added = await users.AddUser(newUsers, cancellationToken);
```

`-- generator: collection-method` gives `AddUser` a second method, which takes the input model of every user, adds them all in one transaction of its own, 1,000 to a round trip, and returns their ids in the order of the users.  When the database refuses one, such as a user whose row breaks a constraint, it throws `CollectionItemException`, whose `ItemIndex` is that user's position, and no user is added.  The same query runs on SQL Server with `OUTPUT inserted.id` before `VALUES` in place of `RETURNING id`.  At the output `methods`, the method also takes a transaction that your code began, and runs in it.

Rules: [collection methods](generator.md#collection-methods), [transactions](generator.md#collection-transactions), [`CollectionItemException`](generator.md#collection-item-exception).

<a id="run-without-awaiting"></a>

## Run a query from code that does not await

```sql postgres
-- output: methods

-- name: GetUserName -> one-optional
-- generator: method-type=both
SELECT name
FROM users
WHERE id = @id;
```

```csharp
// A command of a console tool, whose API is synchronous
public int Run(int id)
{
    using var connection = dataSource.OpenConnection();
    GetUserNameDto? user = connection.GetUserName(new GetUserNameParams(id));
    Console.WriteLine(user?.Name ?? "no such user");
    return user is null ? 1 : 0;
}

// Elsewhere, code that awaits
GetUserNameDto? user = await connection.GetUserNameAsync(new GetUserNameParams(id), cancellationToken: cancellationToken);
```

`-- generator: method-type=both` gives `GetUserName` a synchronous method, `GetUserName`, which returns the row or null and takes no cancellation token, beside its asynchronous method, `GetUserNameAsync`.  `method-type=sync` gives it the synchronous method alone.  A query with [`collection-method`](generator.md#collection-methods) gets its collection method the same way, synchronous, asynchronous or both.  Write it in the file header, or in `generatorParameters` of the folder's `sqlsource.json`, for every query it reaches.  On PostgreSQL, the data source it runs on has no `Multiplexing=true`.

```csharp
// The same query at the default output, repository, without -- output: methods
public int Run(int id)
{
    GetUserNameDto? user = users.GetUserName(new GetUserNameParams(id));
    Console.WriteLine(user?.Name ?? "no such user");
    return user is null ? 1 : 0;
}
```

A repository gets the same methods, and its interface a member for each, so `users` can be the interface.  Each gets its connection from the repository: a repository whose only connection is a method that returns a task marks a second one that returns none, as [the next recipe](#connection-from-a-provider) does.

Rules: [synchronous methods](generator.md#synchronous-methods), [synchronous repository methods](generator.md#synchronous-repository-methods), [generator parameters](settings.md#setting-generator-parameters).

<a id="connection-from-a-provider"></a>

## Give a repository its connection from a service of your own

```csharp
public interface IConnectionStrings
{
    string Get(string name);
}

[SqlSourceGenerate]
public partial class UserRepository(IConnectionStrings connectionStrings)
{
    [SqlSourceConnection]
    private string AppDatabase => connectionStrings.Get("app");
}
```

```csharp
public interface IConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);

    DbConnection Open();
}

[SqlSourceGenerate]
public partial class OrderRepository(IConnectionFactory connections)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        connections.OpenAsync(cancellationToken);

    // For the synchronous methods of method-type=sync or both
    [SqlSourceConnection]
    private DbConnection Open() => connections.Open();
}
```

A member that the build does not find by its name or type is marked `[SqlSourceConnection]`.  `UserRepository`'s property gives a connection string, from which each method makes a connection and disposes it; on PostgreSQL that is a bare connection, without your data source's plugins or mappings, as [known issues](generator.md#known-issues) says.  `OrderRepository`'s methods are factories: each repository method calls one, opens the connection it returns when it is closed, and disposes it, an asynchronous method `OpenAsync` and a [synchronous one](generator.md#synchronous-repository-methods) `Open`.  A type whose queries are all asynchronous needs only `OpenAsync`.  A property or field of a `DbConnection` is borrowed instead, and never disposed.

Rules: [the connection](generator.md#repository-connection).

<a id="sql-server-repository"></a>

## Make a SQL Server repository from a connection string

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "mssql",
}
```

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);
```

```csharp
// Program.cs
builder.Services.AddSingleton<IOrderRepository>(
    new OrderRepository(builder.Configuration.GetConnectionString("App")!));
```

Microsoft.Data.SqlClient has no data source, so a SQL Server repository takes the connection string, found by its name, `connectionString`.  Each method makes a `SqlConnection` of it, opens it, runs the query and disposes the connection, which returns it to Microsoft.Data.SqlClient's pool.  The code that uses the repository takes the generated `IOrderRepository`.

Rules: [the connection](generator.md#repository-connection), [the interface](generator.md#repository-interface), [connecting to SQL Server](sqlserver.md#connecting).

<a id="strongly-typed-id"></a>

## Give an id a type of its own

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "columnTypes": {
        "customers.id": "CustomerId",
        "orders.customer_id": "CustomerId",
    },
}
```

```sql postgres
-- name: GetCustomer -> one-optional
-- param: @id -> CustomerId
SELECT id, name FROM customers WHERE id = @id;
```

```csharp
using SqlSource;

namespace MyApp;

public readonly record struct CustomerId(long Value)
{
    [SqlSourceConverter]
    public static CustomerId FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(CustomerId id) => id.Value;
}
```

```csharp
GetCustomerDto? customer = await customers.GetCustomer(new GetCustomerParams(customerId));
// customer.Id and GetCustomerParams.Id are CustomerId
```

The column types give the columns `customers.id` and `orders.customer_id` the type `CustomerId` in every query that reads them, and the `-- param:` marker gives it to `@id`, which is no column.  The other `bigint` columns of the project keep their `long`, and the same entries work on SQL Server.  A computed column, such as `max(customer_id)`, takes the type from a [`-- column:` marker](generator.md#column-marker) inside its query.

Where the schema makes the ids a type of their own, a domain, `CREATE DOMAIN customer_id AS bigint`, or on SQL Server an alias type, map that type with the type override, and on PostgreSQL declare each parameter compared with such an id as the domain, since PostgreSQL gives it the domain's base type, `bigint`, otherwise:

```sql postgres
-- name: GetCustomer -> one-optional
-- type-override: public.customer_id=MyApp.CustomerId
-- param: @id public.customer_id
SELECT id, name FROM customers WHERE id = @id;
```

Rules: [column types](settings.md#setting-column-types), [giving a parameter a type](reference/parameters.md#param-marker), [converters](settings.md#converters), [type overrides](settings.md#setting-type-overrides).

<a id="class-in-a-json-column"></a>

## Read a class of your own from a JSON column

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "columnTypes": {
        "orders.metadata": "OrderMetadata",
    },
}
```

```sql postgres
-- name: GetOrder -> one
SELECT id, metadata FROM orders WHERE id = @id;

-- name: SetMetadata
-- param: @metadata -> OrderMetadata
UPDATE orders SET metadata = @metadata WHERE id = @id;
```

```csharp
namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority);
```

```csharp
GetOrderDto order = await orders.GetOrder(new GetOrderParams(orderId));
// order.Metadata is an OrderMetadata, read from the jsonb column metadata
await orders.SetMetadata(new SetMetadataParams(order.Metadata with { Priority = 2 }, orderId));
```

`metadata` is a `jsonb`, so the query methods read and send the `OrderMetadata` through System.Text.Json's serializer, with nothing to register on the data source.  The serializer's default options write the names of the properties as the record has them, `{"Channel":"web","Priority":2}`, and match them with their case, so JSON that other code wrote in camelCase, `{"channel":"web"}`, reads as an `OrderMetadata` whose properties have their default values, `null` and `0`, with no error.

**With options of your own**, such as camelCase names, give the class two converters between it and `string`, which the query methods call in place of the serializer:

```csharp
using System;
using System.Text.Json;
using SqlSource;

namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [SqlSourceConverter]
    public static OrderMetadata FromDatabase(string json) =>
        JsonSerializer.Deserialize<OrderMetadata>(json, Options)
        ?? throw new InvalidCastException("The order's metadata is a JSON null.");

    [SqlSourceConverter]
    public static string ToDatabase(OrderMetadata metadata) => JsonSerializer.Serialize(metadata, Options);
}
```

A SQL `NULL` never reaches a converter, but a JSON `null` does, as the text `null`, for which `Deserialize` returns null: the converter decides what it gives.

**In a trimmed or AOT project**, the serializer's calls warn, IL2026 and IL3050, and fail when System.Text.Json's reflection is off.  Give the converters a context that System.Text.Json's source generator writes:

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlSource;

namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority)
{
    [SqlSourceConverter]
    public static OrderMetadata FromDatabase(string json) =>
        JsonSerializer.Deserialize(json, OrderJson.Default.OrderMetadata)
        ?? throw new InvalidCastException("The order's metadata is a JSON null.");

    [SqlSourceConverter]
    public static string ToDatabase(OrderMetadata metadata) =>
        JsonSerializer.Serialize(metadata, OrderJson.Default.OrderMetadata);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(OrderMetadata))]
internal sealed partial class OrderJson : JsonSerializerContext;
```

On SQL Server, the same works for a `json` column.  A column of a string type that holds JSON, such as an `nvarchar(max)` before SQL Server 2025, says so with the JSON opt-in, `"orders.metadata": { "type": "OrderMetadata", "json": true }`, and a parameter with `-- param: @metadata -> json OrderMetadata`.

Rules: [a class in a JSON column](settings.md#json-class), [the JSON opt-in](settings.md#json-opt-in), [converters](settings.md#converters), JSON on [PostgreSQL](postgresql.md#json) and [SQL Server](sqlserver.md#json).

<a id="dapper-and-adonet"></a>

## Use the queries with Dapper or your own ADO.NET code

Under the output `models`, or `sql`, a query is a constant, a plain string, so any library can run it.  Under `methods` or the default, `repository`, it has a [method](generator.md#query-methods) in place of the constant, so a file whose queries you run yourself sets the output to `models`.  Dapper is a separate package (`dotnet add package Dapper`); ADO.NET needs none:

```sql
-- output: models

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

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

`GetUserDto` is the generated [model](generator.md#models) of `GetUser`.  Under `models` SqlSource opens no connection and binds no value: Dapper opens a closed connection for you, ADO.NET needs it open, and you give the database the value of each `@name` yourself.

Rules: [constants](generator.md#constants), [query parameters](reference/parameters.md#query-parameters), [output](settings.md#setting-output).

<a id="models-through-dapper"></a>

## Run the models through Dapper

```sql postgres
-- output: models

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

The input model is the parameter object, and the output model is the type of a row.  The output `models` gives the queries their constants and models, and no [method](generator.md#query-methods), which the default, `repository`, or `methods` would give in place of the constant.  What makes Dapper read them, with either database:

- **Columns in snake_case need `DefaultTypeMap.MatchNamesWithUnderscores`**, as above: Dapper matches `customer_name` to `CustomerName` only with it.
- **Write the query's parameters in camelCase**, `@orderId` and not `@order_id`, since Dapper binds a property of the input model only to a parameter of its name, ignoring case.  A query that keeps `@order_id` gives Dapper its parameters by hand: `new { order_id = orderId }`.
- **Sorted models are classes.**  Dapper matches a record's constructor to the columns by position and name, so a record whose properties are sorted no longer matches.  A class is matched by name:

  ```jsonc
  // sqlsource.json, in the project's folder
  {
      "root": true,
      "generatorParameters": ["sort-output"],
      "outputModelType": "class",
  }
  ```

- **Set a column's nullability with a `-- column:` marker, not with `!` or `?` at the end of its alias.**  A mark stays in the name of the column, `display_name!`, and Dapper does not match it to the property `DisplayName`: it throws `InvalidOperationException` for a record that it cannot make without the column, and leaves the property of a class as it was made, with no error.  The marker leaves the name alone:

  ```sql postgres
  -- name: GetCustomerLabel
  -- column: display_name not null
  SELECT c.id, initcap(c.name) AS display_name
  FROM customers c
  WHERE c.id = @customerId;
  ```

- **A [list parameter](reference/parameters.md#list-parameters), `IN @ids`, works on SQL Server alone, and only without a null test.**  Dapper sends one parameter for each value of a list on SQL Server, and a null list as an empty one, but it rewrites every `@ids` of the query: `@ids IS NULL` is an error from two values up, and true for an empty list as for a null one.  On PostgreSQL it sends a list as one array: write `id = ANY(@ids)` there, an [array parameter](postgresql.md#array-parameters).

Each database needs more, below: [PostgreSQL](#dapper-with-postgresql) and [SQL Server](#dapper-with-sql-server).

Rules: [models](generator.md#models), [property names](generator.md#property-names), [the order of the properties](generator.md#property-order), [which properties can be null](generator.md#model-nullability), [the `-- column:` marker](generator.md#column-marker), [shape of a model](settings.md#setting-model-type), [the type override](settings.md#setting-type-overrides), and [known issues](generator.md#known-issues) for parameters in snake_case.

<a id="dapper-with-postgresql"></a>

### With PostgreSQL

- **A type override to a type that Npgsql does not give needs a Dapper type handler.**  Dapper fills a property with the type Npgsql gives for its column, or through a handler.  A settings file's `"timestamptz": "System.DateTimeOffset"` makes a property a `DateTimeOffset` where Npgsql gives a `DateTime`.  A domain mapped to a struct of your own is the same: Npgsql gives the domain's base type, a `string` for a domain over `text`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

  ```csharp
  using System.Data;
  using Dapper;

  public sealed class UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
  {
      public override DateTimeOffset Parse(object value) =>
          value is DateTime dateTime
              ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
              : (DateTimeOffset)value;

      public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value;
  }
  ```

  ```csharp
  SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
  ```

  A `DateTimeOffset` parameter is sent as a `timestamptz`, which takes an offset of 0 alone.  Another offset throws `ArgumentException`, so pass `value.ToUniversalTime()`.

  A type of yours that has [converters](settings.md#converters) for the query methods needs no more than a handler that calls them, here for a domain over `text` mapped to `Email`, whose converters take and return a `string`:

  ```csharp
  using System.Data;
  using Dapper;

  public sealed class EmailHandler : SqlMapper.TypeHandler<Email>
  {
      public override Email Parse(object value) => Email.FromDatabase((string)value);

      public override void SetValue(IDbDataParameter parameter, Email value) => parameter.Value = Email.ToDatabase(value);
  }
  ```

  ```csharp
  SqlMapper.AddTypeHandler(new EmailHandler());
  ```

- **A PostgreSQL enum mapped to a C# enum needs `MapEnum<T>()` on the data source.**  An enum read as a `string`, the default, needs nothing:

  ```csharp
  var builder = new NpgsqlDataSourceBuilder(connectionString);
  builder.MapEnum<OrderStatus>();
  await using var dataSource = builder.Build();
  ```

  Dapper sends a C# enum parameter as its number, which a column of the enum refuses; send it as the next point says.
- **Some parameters Dapper cannot send.**  Dapper refuses a parameter of these types, or sends it as a type that the column refuses:
  - the unsigned numbers: `oid`, `xid`, `cid`, `xid8`, `regtype`, `regconfig` and `oidvector`;
  - the network addresses: `inet`, `cidr`, `macaddr` and `macaddr8`;
  - the geometric types: `point`, `line`, `lseg`, `box` and `circle`;
  - the ranges: `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` and `daterange`;
  - `tid`, `pg_lsn`, `tsquery`, `int2vector` and `timetz`;
  - a `string` for `json`, `jsonb`, `jsonpath`, `xml`, `refcursor`, `ltree`, `lquery`, `ltxtquery`, an enum or an array of an enum;
  - a C# enum, as above.

  For a `string`, cast the query parameter in the SQL, `@payload::jsonb`, and Dapper's text is read as the type.  For the others, give Dapper the parameters by hand, and that one as a `SqlMapper.ICustomQueryParameter` that adds an `NpgsqlParameter` with its `DataTypeName`, since Dapper refuses an `NpgsqlParameter` given as a value:

  ```csharp
  using System.Data;
  using Dapper;
  using Npgsql;

  public sealed class TypedParameter(string dataTypeName, object? value) : SqlMapper.ICustomQueryParameter
  {
      public void AddParameter(IDbCommand command, string name) =>
          command.Parameters.Add(
              new NpgsqlParameter { ParameterName = name, DataTypeName = dataTypeName, Value = value ?? DBNull.Value }
          );
  }
  ```

  ```csharp
  connection.QueryAsync<FindSessionsDto>(Sql.FindSessions, new { address = new TypedParameter("inet", address) });
  ```

<a id="dapper-with-sql-server"></a>

### With SQL Server

```sql mssql
-- name: ListCustomerOrdersSince
SELECT id, placed_at, total
FROM dbo.orders
WHERE customer_id = @customerId AND placed_at >= @since;
```

```csharp
connection.QueryAsync<ListCustomerOrdersSinceDto>(
    Sql.ListCustomerOrdersSince,
    new { customerId, since = new TypedParameter(SqlDbType.DateTime2, since) }
);
```

- **Send a `datetime2` parameter as a `datetime2`.**  Dapper sends a `DateTime` as a `datetime`, which holds a time to 1/300 of a second.  So `13:45:30.1234567` is written to a `datetime2(7)` column as `13:45:30.1233333`, and compared with one, it is another value: `WHERE placed_at = @placedAt` finds no row of the time you gave.  Send it as above, with `TypedParameter` from the next point, or give Dapper the input model in `DynamicParameters` and add that parameter with its `DbType`, under the parameter's name, where it takes the place of the model's property of that name, whatever its case:

  ```csharp
  var args = new ListCustomerOrdersSinceParams(customerId, since);
  var parameters = new DynamicParameters(args);
  parameters.Add("since", args.Since, DbType.DateTime2);
  ```

- **Some parameters Dapper cannot send.**  Dapper sends a parameter of these types as another type, or refuses it, or the column refuses it:
  - `datetime2`, which Dapper sends as a `datetime`, as above;
  - `rowversion`, which SQL Server fills and refuses a value for: leave it out of the columns of an `INSERT`;
  - `vector`, whose `SqlVector<float>` Dapper refuses with `NotSupportedException`.

  For `datetime2` and `vector`, give Dapper the parameters by hand, and that one as a `SqlMapper.ICustomQueryParameter` that adds a `SqlParameter` with its `SqlDbType`:

  ```csharp
  using System;
  using System.Data;
  using Dapper;
  using Microsoft.Data.SqlClient;

  public sealed class TypedParameter(SqlDbType sqlDbType, object? value) : SqlMapper.ICustomQueryParameter
  {
      public void AddParameter(IDbCommand command, string name) =>
          command.Parameters.Add(
              new SqlParameter
              {
                  ParameterName = name,
                  SqlDbType = sqlDbType,
                  Value = value ?? DBNull.Value,
              }
          );
  }
  ```

  ```csharp
  using Microsoft.Data;
  using Microsoft.Data.SqlTypes;

  connection.ExecuteAsync(
      Sql.SaveEmbedding,
      new { id, embedding = new TypedParameter(SqlDbTypeExtensions.Vector, new SqlVector<float>(elements)) }
  );
  ```

  `SqlDbTypeExtensions` is Microsoft.Data.SqlClient's, in the namespace `Microsoft.Data`, so the call builds on .NET 8 and later, where `SqlDbType.Vector` is in .NET 10 alone.

  A `string` for a `json` column needs nothing: SQL Server converts Dapper's text to `json`.
- **A type override to a type that SqlClient does not give needs a Dapper type handler.**  Dapper fills a property with the type SqlClient gives for its column, or through a handler.  A settings file's `"date": "System.DateOnly"` makes a property a `DateOnly` where SqlClient gives a `DateTime`, and `"time": "System.TimeOnly"` a `TimeOnly` where it gives a `TimeSpan`.  An alias type mapped to a struct of your own is the same: SqlClient gives the alias type's base type, a `string` for one over `nvarchar`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

  ```csharp
  using System;
  using System.Data;
  using Dapper;

  public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
  {
      public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

      public override void SetValue(IDbDataParameter parameter, DateOnly value)
      {
          parameter.DbType = DbType.Date;
          parameter.Value = value;
      }
  }
  ```

  ```csharp
  using System;
  using System.Data;
  using Dapper;

  public sealed class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly>
  {
      public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);

      public override void SetValue(IDbDataParameter parameter, TimeOnly value)
      {
          parameter.DbType = DbType.Time;
          parameter.Value = value;
      }
  }
  ```

  ```csharp
  SqlMapper.AddTypeHandler(new DateOnlyHandler());
  SqlMapper.AddTypeHandler(new TimeOnlyHandler());
  ```

  A `DateOnly` parameter is then sent as a `date`, and a `TimeOnly` as a `time`.

  A type of yours that has [converters](settings.md#converters) for the query methods needs no more than a handler that calls them, here for an alias type over `nvarchar` mapped to `Email`, whose converters take and return a `string`:

  ```csharp
  using System.Data;
  using Dapper;

  public sealed class EmailHandler : SqlMapper.TypeHandler<Email>
  {
      public override Email Parse(object value) => Email.FromDatabase((string)value);

      public override void SetValue(IDbDataParameter parameter, Email value) => parameter.Value = Email.ToDatabase(value);
  }
  ```

  ```csharp
  SqlMapper.AddTypeHandler(new EmailHandler());
  ```

- **On a connection with a transaction open, give every call the transaction.**  SqlClient refuses a command without it, with `InvalidOperationException`:

  ```csharp
  await using var transaction = await connection.BeginTransactionAsync();
  var note = await connection.QuerySingleAsync<AddNoteDto>(Sql.AddNote, args, transaction);
  await transaction.CommitAsync();
  ```

<a id="table-name-at-run-time"></a>

## Choose a table name at run time

```sql postgres
-- name: CountRows -> one
SELECT COUNT(*) AS total FROM {{table:orders}};
```

```csharp
var table = kind == Kind.Archived ? "orders_archive" : "orders";
var counted = await reports.CountRows(table);
// runs SELECT COUNT(*) AS total FROM orders_archive; counted.Total is the count
```

The query's method takes the text of the token, and runs the query with the token replaced.  Under the output `sql` or `models`, the query is instead a method that returns the SQL, `Sql.CountRows(table)`, for your own code to run.  The default after `:` is only a sample that the tool describes the query with, and the method still takes the table name; a query whose output is `sql` needs no default.  Pass only text that your own code chooses, as above, and never a value that a user typed: nothing is escaped.

Rules: [tokens](reference/tokens.md#tokens), [the signature](generator.md#repository-signature), [token methods](generator.md#token-methods), [defaults](reference/tokens.md#token-defaults).

<a id="filter-by-a-list"></a>

## Filter by an optional list of values

```sql postgres
-- name: ListOrdersByStatuses
SELECT id, status, placed_at
FROM orders
WHERE (@statuses IS NULL OR status IN @statuses)
ORDER BY placed_at DESC;
```

```csharp
string[] notCancelled = ["pending", "shipped"];
var shown = await orders.ListOrdersByStatuses(new ListOrdersByStatusesParams(notCancelled));
var everyOrder = await orders.ListOrdersByStatuses(new ListOrdersByStatusesParams(null));
```

`@statuses`, written after `IN` with no parentheses, is a list, and `ListOrdersByStatusesParams.Statuses` an `IEnumerable<string>?`, which takes an array, a `List<string>` or any other sequence.  The method sends a parameter for each status.  A null list returns every order, by the test `@statuses IS NULL`, and an empty one returns none.  The same query runs on SQL Server as it is.  On PostgreSQL, `status = ANY(@statuses)` sends the list as one array instead.

Rules: [list parameters](reference/parameters.md#list-parameters), [array parameters](postgresql.md#array-parameters).

<a id="test-project-sees-internals"></a>

## Let a test project see the internals of the main project

```xml
<!-- App.csproj -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

```csharp
// App.Tests, which references the SqlSource package too, with "output": "methods" in the sqlsource.json of
// Queries, since a static type has no repository methods
using SqlSource;

[SqlSourceGenerate(Source = "Queries")]
internal static partial class TestQueries;
```

```csharp
// App.Tests catches the exception that a method of App throws by its type
await Should.ThrowAsync<UnexpectedRowCountException>(() => users.GetActiveUser(new GetActiveUserParams(email)));
```

The test project uses App's `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, whether it uses SqlSource or not, and the attributes of each project do not conflict.  When App uses the exceptions of a [shared project](#share-the-exceptions), the shared project grants `InternalsVisibleTo` to App.Tests too.

Rules: [projects that share internals](generator.md#projects-that-share-internals).

<a id="share-the-exceptions"></a>

## Share the exceptions between two data projects and an app

```xml
<!-- Data.Shared.csproj: references the SqlSource package, and has no query -->
<PropertyGroup>
    <!-- .NET 6 or later, alone or beside an older target such as netstandard2.0 -->
    <TargetFramework>net8.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
    <InternalsVisibleTo Include="Data.Orders" />
    <InternalsVisibleTo Include="Data.Users" />
    <InternalsVisibleTo Include="App" />
    <!-- and each test project of a data project -->
    <InternalsVisibleTo Include="Data.Orders.Tests" />
    <InternalsVisibleTo Include="Data.Users.Tests" />
</ItemGroup>
```

```xml
<!-- Data.Orders.csproj and Data.Users.csproj: each references the SqlSource package and has its queries -->
<ItemGroup>
    <ProjectReference Include="../Data.Shared/Data.Shared.csproj" />
    <InternalsVisibleTo Include="App" />
</ItemGroup>
```

```csharp
// App, which sees the internals of all three
catch (UnexpectedRowCountException exception)
{
    // thrown by a method of Data.Orders or of Data.Users
}
```

Each data project uses the shared project's exceptions instead of writing its own, so the app sees one copy and catches what a method of either throws by its type.  Grant `InternalsVisibleTo` from Data.Shared to every project that catches the exceptions, or that uses SqlSource and sees a data project's internals, the data projects' test projects among them: one that does not see Data.Shared's copy gets its own, and its `catch` compiles but misses what a data project throws.  Without the shared project, an app that uses SqlSource and sees the internals of both gets [SQLSRC022](diagnostics.md#sqlsrc022).  Data.Shared targets .NET 6 or later, or adds such a target to its `TargetFrameworks`, so that its `CollectionItemException` passes on `SqlState`, `IsTransient` and `BatchCommand`; on `netstandard2.0` or `netstandard2.1` alone, a data project gets [SQLSRC038](diagnostics.md#sqlsrc038).

Rules: [projects that share internals](generator.md#projects-that-share-internals), [SQLSRC022](diagnostics.md#sqlsrc022), [SQLSRC038](diagnostics.md#sqlsrc038).
