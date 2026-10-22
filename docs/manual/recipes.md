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

A project of PostgreSQL files keeps its reports for SQL Server in `Reporting/`.  Item metadata gives the folder its own dialect:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" />
</ItemGroup>
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

Add `SqlSourceOutput="sql"` to the metadata, or `-- output: sql` to the file, when the reports want their SQL alone: the tool then leaves them out and needs no connection for them.

Rules: [configure one folder differently](settings.md#configure-a-folder), [dialect](settings.md#setting-dialect), [which place wins](settings.md#precedence), [connections](tool.md#connections).

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

Here `GetUser` is `SELECT id, name FROM users WHERE id = @id;`, and `GetUserDto` is its generated [model](generator.md#models).  SqlSource never opens a connection or binds a value: Dapper opens a closed connection for you, ADO.NET needs it open, and you give the database the value of each `@name` yourself.

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

The input model is the parameter object, and the output model is the type of a row.  What makes Dapper read them, with either database:

- **Columns in snake_case need `DefaultTypeMap.MatchNamesWithUnderscores`**, as above: Dapper matches `customer_name` to `CustomerName` only with it.
- **Write the query's parameters in camelCase**, `@orderId` and not `@order_id`, since Dapper binds a property of the input model only to a parameter of its name, ignoring case.  A query that keeps `@order_id` gives Dapper its parameters by hand: `new { order_id = orderId }`.
- **Sorted models are classes.**  Dapper matches a record's constructor to the columns by position and name, so a record whose properties are sorted no longer matches.  A class is matched by name:

  ```xml
  <PropertyGroup>
      <SqlSourceGeneratorParameters>sort-output</SqlSourceGeneratorParameters>
      <SqlSourceOutputModelType>class</SqlSourceOutputModelType>
  </PropertyGroup>
  ```

- **Set a column's nullability with a `-- column:` marker, not with `!` or `?` at the end of its alias.**  A mark stays in the name of the column, `note_count!`, and Dapper does not match it to the property `NoteCount`: it throws `InvalidOperationException` for a record that it cannot make without the column, and leaves the property of a class as it was made, with no error.  The marker leaves the name alone:

  ```sql postgres
  -- name: GetOrderNoteCount
  -- column: note_count not null
  SELECT o.id, count(l.note) AS note_count
  FROM orders o
  JOIN order_lines l ON l.order_id = o.id
  WHERE o.id = @orderId
  GROUP BY o.id;
  ```

Each database needs more, below: [PostgreSQL](#dapper-with-postgresql) and [SQL Server](#dapper-with-sql-server).

Rules: [models](generator.md#models), [property names](generator.md#property-names), [the order of the properties](generator.md#property-order), [which properties can be null](generator.md#model-nullability), [the `-- column:` marker](generator.md#column-marker), [shape of a model](settings.md#setting-model-type), [the type override](settings.md#setting-type-overrides), and [known issues](generator.md#known-issues) for parameters in snake_case.

<a id="dapper-with-postgresql"></a>

### With PostgreSQL

- **A type override to a type that Npgsql does not give needs a Dapper type handler.**  Dapper fills a property with the type Npgsql gives for its column, or through a handler.  `timestamptz=System.DateTimeOffset` makes a property a `DateTimeOffset` where Npgsql gives a `DateTime`.  A domain mapped to a struct of your own is the same: Npgsql gives the domain's base type, a `string` for a domain over `text`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

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

- **Send a `datetime2` parameter as a `datetime2`.**  Dapper sends a `DateTime` as a `datetime`, which holds a time to 1/300 of a second.  So `13:45:30.1234567` is written to a `datetime2(7)` column as `13:45:30.1233333`, and compared with one, it is another value: `WHERE placed_at = @placedAt` finds no row of the time you gave.  Send it as above, with `TypedParameter` from the next point, or add it with its `DbType` over the other parameters, where the one added takes the place of the one of the same name:

  ```csharp
  var parameters = new DynamicParameters(new { customerId, since });
  parameters.Add("since", since, DbType.DateTime2);
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
  connection.ExecuteAsync(
      Sql.SaveEmbedding,
      new { id, embedding = new TypedParameter(SqlDbType.Vector, new SqlVector<float>(elements)) }
  );
  ```

  A `string` for a `json` column needs nothing: SQL Server converts Dapper's text to `json`.
- **A type override to a type that SqlClient does not give needs a Dapper type handler.**  Dapper fills a record with the type SqlClient gives for each column, or through a handler.  `date=System.DateOnly` makes a property a `DateOnly` where SqlClient gives a `DateTime`, and `time=System.TimeOnly` a `TimeOnly` where it gives a `TimeSpan`.  An alias type mapped to a struct of your own is the same: SqlClient gives the alias type's base type, a `string` for one over `nvarchar`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

  ```csharp
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
- **On a connection with a transaction open, give every call the transaction.**  SqlClient refuses a command without it, with `InvalidOperationException`:

  ```csharp
  await using var transaction = await connection.BeginTransactionAsync();
  var note = await connection.QuerySingleAsync<AddNoteDto>(Sql.AddNote, args, transaction);
  await transaction.CommitAsync();
  ```

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
