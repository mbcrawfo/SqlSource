# Use Dapper or your own ADO.NET code

<a id="models"></a>

## Models for Dapper, start to end

Set the output to `models`, to run the queries yourself, with Dapper or your own ADO.NET code.  A query whose output is `models` gets its models, once the tool has described it, as in step 5, and a constant that holds its SQL, or a method that returns it when the query has a token, in place of the method that runs it.  Add `output` to the `sqlsource.json` of step 3: this sample and those below show that one file again, not a file of their own:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "postgres",
    "output": "models",
}
```

The constants are in a private class `Sql` of the type, `Sql.GetUser` and `Sql.FindUsers`, and are plain strings, so any library can run them: Dapper here (`dotnet add package Dapper`).

```csharp
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<GetUserDto?> Get(int id) =>
        connection.QuerySingleOrDefaultAsync<GetUserDto>(Sql.GetUser, new GetUserParams(id));

    public Task<IEnumerable<FindUsersDto>> Find(string? name) =>
        connection.QueryAsync<FindUsersDto>(Sql.FindUsers, new FindUsersParams(name));
}
```

More: [output](../reference/settings.md#setting-output), which can also be set for one folder, one file or one query, [constants](../reference/members.md#constants), and [models](../reference/models.md#models), for their names, shape and properties.

<a id="dapper-and-adonet"></a>

## Use the queries with Dapper or your own ADO.NET code

Under the output `models`, or `sql`, a query is a constant, a plain string, so any library can run it.  Under `methods` or the default, `repository`, it has a [method](../reference/query-methods.md#query-methods) in place of the constant, so a file whose queries you run yourself sets the output to `models`.  Dapper is a separate package (`dotnet add package Dapper`); ADO.NET needs none:

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

`GetUserDto` is the generated [model](../reference/models.md#models) of `GetUser`.  Under `models` SqlSource opens no connection and binds no value: Dapper opens a closed connection for you, ADO.NET needs it open, and you give the database the value of each `@name` yourself.

Rules: [constants](../reference/members.md#constants), [query parameters](../reference/parameters.md#query-parameters), [output](../reference/settings.md#setting-output).

<a id="models-through-dapper"></a>

## Run the models through Dapper

Dapper reads the rows by the names of their columns.  For columns written in snake_case, such as `created_at`, set Dapper's `DefaultTypeMap.MatchNamesWithUnderscores` to `true` once when your application starts.  Write the parameters in camelCase, `@userId` and not `@user_id`: Dapper sends a property of `GetUserParams` only to the parameter written as its name, ignoring case but not `_`.  Set a column's nullability with a `-- column:` line, not a mark at the end of its alias, which changes the column's name.  All three are in [running the models through Dapper](#models-through-dapper).  With SQL Server, send a `datetime2` parameter as a `datetime2`, since Dapper sends a `DateTime` as a `datetime`: see [with SQL Server](#dapper-with-sql-server).

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

The input model is the parameter object, and the output model is the type of a row.  The output `models` gives the queries their constants and models, and no [method](../reference/query-methods.md#query-methods), which the default, `repository`, or `methods` would give in place of the constant.  What makes Dapper read them, with either database:

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

- **A [list parameter](../reference/parameters.md#list-parameters), `IN @ids`, works on SQL Server alone, and only without a null test.**  Dapper sends one parameter for each value of a list on SQL Server, and a null list as an empty one, but it rewrites every `@ids` of the query: `@ids IS NULL` is an error from two values up, and true for an empty list as for a null one.  On PostgreSQL it sends a list as one array: write `id = ANY(@ids)` there, an [array parameter](../reference/databases/postgresql/query-methods.md#array-parameters).

Each database needs more, below: [PostgreSQL](#dapper-with-postgresql) and [SQL Server](#dapper-with-sql-server).

Rules: [models](../reference/models.md#models), [property names](../reference/model-properties.md#property-names), [the order of the properties](../reference/model-properties.md#property-order), [which properties can be null](../reference/model-properties.md#model-nullability), [the `-- column:` marker](../reference/model-properties.md#column-marker), [shape of a model](../reference/models.md#setting-model-type), [the type override](../reference/type-mapping.md#setting-type-overrides), and [a limit](#limit-parameter-names) for parameters in snake_case.

<a id="dapper-with-postgresql"></a>

## With PostgreSQL

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

  A type of yours that has [converters](../reference/converters.md#converters) for the query methods needs no more than a handler that calls them, here for a domain over `text` mapped to `Email`, whose converters take and return a `string`:

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

## With SQL Server

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

  A type of yours that has [converters](../reference/converters.md#converters) for the query methods needs no more than a handler that calls them, here for an alias type over `nvarchar` mapped to `Email`, whose converters take and return a `string`:

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

<a id="sql-only"></a>

## Only the SQL

Follow steps 1 to 3, set the output to `sql`, and skip the tool.  For a SQL Server project:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "mssql",
    "output": "sql",
}
```

- **`sql` asks for each query's SQL and nothing else**: its constant, or, for a query with a `{{token}}`, a method that fills it in, which you run as [Models for Dapper, start to end](#models) shows, without the models.  It needs no database, no tool and no sidecar.  It tells the tool to leave these queries out, so a run on the project reports nothing for them, and the build to need no sidecar for them and to generate no models.  Without it the default output, `repository`, asks for every query to be described, and for a method that runs it, and the build reports an error for each file or query that is not.

More: [output](../reference/settings.md#setting-output), [database support](../reference/databases/index.md#database-support).

<a id="limits"></a>

## Limits

<a id="limit-parameter-names"></a>

### Dapper sends no value for a parameter such as `@account_id`

- **An input model binds through Dapper only when the query writes its parameters as the model's properties are named.**  Dapper sends a property of a parameter object only to a parameter that the SQL writes as `@` and the property's name, ignoring case but not `_`.  So the property `AccountId` binds to `@accountId`, and a query that writes `@account_id` gets no value from it.  Write the query's parameters in camelCase, or give Dapper the parameters yourself, as in `new { account_id = args.AccountId }`: see [the recipe](#models-through-dapper).
