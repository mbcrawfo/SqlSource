# Use Dapper or your own ADO.NET code

Generate only the models and the SQL constants of your queries, and run them with Dapper or your own ADO.NET code.

**Use this when** Dapper, or ADO.NET code of your own, runs your SQL, and you want SqlSource to give you only the models and the SQL.  If SqlSource can run the queries for you, keep the default output, `repository`: see [The four outputs](output-modes.md#the-four-outputs).

<a id="models"></a>

## Models for Dapper, start to end

**Before you start.**  Add the package and Dapper in your project's folder.  Install the tool once, at the root of your repository.  If the repository has a tool manifest, skip `dotnet new tool-manifest`.

```console
$ dotnet add package SqlSource
$ dotnet add package Dapper
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

Add your database's driver too:

# [PostgreSQL](#tab/postgresql)

```console
$ dotnet add package Npgsql
```

# [SQL Server](#tab/sqlserver)

```console
$ dotnet add package Microsoft.Data.SqlClient
```

---

1. **Write a query.**  Save it as `Models/Users.sql`:

   ```sql postgres
   -- name: GetUser
   -- summary: One user, every column of the table.
   SELECT *
   FROM users
   WHERE id = @userId;
   ```

2. **Mark a partial class**, in `Models/StoreQueries.cs`.  `Source = "."` gives it every `.sql` file of its folder:

   ```csharp
   using SqlSource;

   [SqlSourceGenerate(Source = ".")]
   public sealed partial class StoreQueries;
   ```

3. **Set the dialect of your database and the output.**  Save this as `sqlsource.json`, beside your project file:

# [PostgreSQL](#tab/postgresql)

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "postgres",
    "output": "models",
}
```

# [SQL Server](#tab/sqlserver)

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "mssql",
    "output": "models",
}
```

---

For the SQL alone, set the output to `sql`, and go on at [Only the SQL](#sql-only).

4. **Give the tool your connection.**  Write it in a `.env` file in your project's folder.  Add `.env` to your `.gitignore`.  `SQLSOURCE_CONNECTION` serves a project with one database:

# [PostgreSQL](#tab/postgresql)

```dotenv
SQLSOURCE_CONNECTION="Host=localhost;Port=55432;Database=examples;Username=examples;Password=examples"
```

# [SQL Server](#tab/sqlserver)

```dotenv
SQLSOURCE_CONNECTION="Server=localhost,51433;Database=examples;User ID=sa;Password=Examples-Passw0rd;TrustServerCertificate=True"
```

---

5. **Describe the query.**  The tool asks your database about it, and writes the answer to `Models/Users.sql.json`.  Commit that file:

   ```console
   $ dotnet sqlsource describe
   $ git add Models/Users.sql.json
   ```

6. **Run the query through Dapper**, into its models:

   ```csharp
   // Models/StoreQueries.cs
   using System.Data.Common;
   using System.Threading.Tasks;
   using Dapper;
   using SqlSource;

   [SqlSourceGenerate(Source = ".")]
   public sealed partial class StoreQueries(DbConnection connection)
   {
       // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
       public Task<UsersDto?> GetUserAsync(int userId) =>
           connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
   }
   ```

   Once, when your application starts, set `DefaultTypeMap.MatchNamesWithUnderscores = true`, as [Run the models through Dapper](#models-through-dapper) shows.

More: [the tool](../reference/cli.md#install-the-tool), [dialect](../reference/settings.md#setting-dialect), [connections](../reference/connections.md#connections), [`.env`](../reference/connections.md#env-file), connecting to [PostgreSQL](../reference/databases/postgresql/index.md#connecting) and [SQL Server](../reference/databases/sqlserver/index.md#connecting), [describe](../reference/describing.md#describe).

**What you get.**  Once the tool has described `GetUser`, in step 5, the query gets its models and a constant, in place of a method that runs it.  `Sql.GetUser` is the constant: it holds the SQL of the query.  `GetUserParams` is the input model.  `UsersDto` is the output model: it is the model of the table `users`, since the query reads the whole table.  A query with a [token](../reference/members.md#token-methods) gets a method that returns its SQL, in place of the constant.

The output `models` is the `output` of the settings file of step 3.  This sample and each `sqlsource.json` below show that one file again, not a file of their own:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "postgres",
    "output": "models",
}
```

Another type gets the same from its own file.  Here `UserRepository` claims `UserRepository.sql`, which holds the queries `GetUser` and `FindUsers`, and runs them through Dapper:

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

**The rules you need.**

- To run the queries with Dapper or your own ADO.NET code, set the output to `models`.  You can also set it for one folder, one file or one query.  See [Output](../reference/settings.md#setting-output).
- The constants are in a private class `Sql` of the type.  They are plain strings, so any library can run them.  See [Constants](../reference/members.md#constants).
- By default, a query that does not read a whole table gets an output model named after the query, such as `GetUserDto`.  See [Table models](../reference/models.md#table-models).  For the names, the shape and the properties of the models, see [Models](../reference/models.md#models).

<a id="dapper-and-adonet"></a>

## Use the queries with Dapper or your own ADO.NET code

To run the queries of a file with Dapper or with your own ADO.NET code, set the output of the file to `models`:

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

**What you get.**  `Sql.GetUser` is a constant, a plain string, so any library can run it.  `GetUserDto` is the [model](../reference/models.md#models) of the rows of this `GetUser`, which reads two columns of `users`.  The `GetUser` of step 1 reads the whole table, so its model is the model of the table, `UsersDto`.

**The rules you need.**

- Under the output `models` or `sql`, a query is a constant.  Under `repository`, the default, or `methods`, it has a [method](../reference/query-methods.md#query-methods) in place of the constant.  So a file whose queries you run yourself sets the output to `models`.  See [Constants](../reference/members.md#constants) and [Output](../reference/settings.md#setting-output).
- Under `models`, SqlSource opens no connection.  Dapper opens a closed connection for you.  ADO.NET needs the connection open.
- Under `models`, SqlSource binds no value.  You give the database the value of each `@name` yourself.  See [Query parameters](../reference/parameters.md#query-parameters).
- ADO.NET needs no package.  Dapper is a separate package, which you added before step 1.

<a id="models-through-dapper"></a>

## Run the models through Dapper

Dapper reads the rows by the names of their columns.  Here Dapper runs a query of orders whose output is `models`, and one setting lets Dapper match the columns:

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

**What you get.**  `GetOrderParams` is the input model: Dapper takes it as the parameter object.  `GetOrderDto` is the output model: the type of a row.  `Sql.GetOrder` is the constant.  The output `models` gives the query no [method](../reference/query-methods.md#query-methods).  `repository`, the default, or `methods` would give it a method in place of the constant.  See [Models](../reference/models.md#models).

**The rules you need.**  Each rule holds with either database.

- **Columns in snake_case need `DefaultTypeMap.MatchNamesWithUnderscores`.**  For columns such as `created_at` and `customer_name`, set it to `true` once, when your application starts.  Dapper matches `customer_name` to `CustomerName` only with it.  See [Property names](../reference/model-properties.md#property-names).
- **Write the query's parameters in camelCase**, `@orderId` and not `@order_id`, as `@userId` is in step 1.  Dapper sends a property of the input model, such as `OrderId` of `GetOrderParams`, only to the parameter written as its name.  It ignores case, but not `_`.  If a query keeps `@order_id`, give Dapper its parameters by hand: `new { order_id = orderId }`.  See [the limit](#limit-parameter-names).
- **Sorted models are classes.**  Dapper matches the constructor of a record to the columns by position and name.  So a record whose properties are sorted no longer matches.  Dapper matches a class by name:

  ```jsonc
  // sqlsource.json, in the project's folder
  {
      "root": true,
      "generatorParameters": ["sort-output"],
      "outputModelType": "class",
  }
  ```

  See [The order of the properties](../reference/model-properties.md#property-order) and [Shape of a model](../reference/models.md#setting-model-type).

- **Set the nullability of a column with a `-- column:` marker, not with `!` or `?` at the end of its alias.**  A mark changes the name of the column: the mark stays in the name, `display_name!`.  So Dapper does not match the column to the property `DisplayName`.  For a record that it cannot make without the column, Dapper throws `InvalidOperationException`.  For a class, Dapper leaves the property as the class made it, with no error.  The marker leaves the name alone:

  ```sql postgres
  -- name: GetCustomerLabel
  -- column: display_name not null
  SELECT c.id, initcap(c.name) AS display_name
  FROM customers c
  WHERE c.id = @customerId;
  ```

  See [The `-- column:` marker](../reference/model-properties.md#column-marker) and [Which properties can be null](../reference/model-properties.md#model-nullability).

- **A [list parameter](../reference/parameters.md#list-parameters), `IN @ids`, works on SQL Server alone, and only without a null test.**  On SQL Server, Dapper sends one parameter for each value of a list, and a null list as an empty one.  But Dapper rewrites every `@ids` of the query.  So `@ids IS NULL` is an error from two values up, and it is true for an empty list, as for a null one.  On PostgreSQL, Dapper sends a list as one array.  Write `id = ANY(@ids)` there, an [array parameter](../reference/databases/postgresql/query-methods.md#array-parameters).

Each database needs more, below: [PostgreSQL](#dapper-with-postgresql) and [SQL Server](#dapper-with-sql-server).  On both, a [type override](../reference/type-mapping.md#setting-type-overrides) to a type that the driver does not give needs a Dapper type handler.  On SQL Server, send a `datetime2` parameter as a `datetime2`, since Dapper sends a `DateTime` as a `datetime`.

<a id="dapper-with-postgresql"></a>

## With PostgreSQL

On PostgreSQL, some types need more than the rules above: a type override, an enum, and some parameters.

**The rules you need.**

- **A type override to a type that Npgsql does not give needs a Dapper type handler.**  Dapper fills a property with the type that Npgsql gives for its column, or through a handler.  For example, a settings file's `"timestamptz": "System.DateTimeOffset"` makes a property a `DateTimeOffset`, where Npgsql gives a `DateTime`.  A domain mapped to a struct of your own is the same: Npgsql gives the base type of the domain, such as a `string` for a domain over `text`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

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

  A type of yours that has [converters](../reference/converters.md#converters) for the query methods needs only a handler that calls them.  Here the type is `Email`, mapped from a domain over `text`, and its converters take and return a `string`:

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

  Dapper sends a C# enum parameter as its number, and a column of the enum refuses a number.  Send it as the next rule says.
- **Some parameters Dapper cannot send.**  Dapper refuses a parameter of these types, or sends it as a type that the column refuses:
  - the unsigned numbers: `oid`, `xid`, `cid`, `xid8`, `regtype`, `regconfig` and `oidvector`;
  - the network addresses: `inet`, `cidr`, `macaddr` and `macaddr8`;
  - the geometric types: `point`, `line`, `lseg`, `box` and `circle`;
  - the ranges: `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` and `daterange`;
  - `tid`, `pg_lsn`, `tsquery`, `int2vector` and `timetz`;
  - a `string` for `json`, `jsonb`, `jsonpath`, `xml`, `refcursor`, `ltree`, `lquery`, `ltxtquery`, an enum or an array of an enum;
  - a C# enum, as above.

  For a `string`, cast the query parameter in the SQL, as in `@payload::jsonb`.  PostgreSQL then reads the text from Dapper as that type.  For the others, give Dapper the parameters by hand.  Give that one parameter as a `SqlMapper.ICustomQueryParameter` that adds an `NpgsqlParameter` with its `DataTypeName`.  You cannot give Dapper the `NpgsqlParameter` itself, since Dapper refuses an `NpgsqlParameter` given as a value:

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

**What you get.**  `since` is sent as a `datetime2`, so the query compares `placed_at` with the exact time you gave.  `TypedParameter` is the class of the second rule below.

**The rules you need.**

- **Send a `datetime2` parameter as a `datetime2`.**  Dapper sends a `DateTime` as a `datetime`, which holds a time to 1/300 of a second.  So `13:45:30.1234567` is written to a `datetime2(7)` column as `13:45:30.1233333`.  Compared with such a column, it is another value: `WHERE placed_at = @placedAt` finds no row of the time you gave.  Send it as above, with `TypedParameter` from the next rule.  Or give Dapper the input model in `DynamicParameters`, and add that parameter with its `DbType`, under the name of the parameter.  It then takes the place of the property of that name in the model, whatever its case:

  ```csharp
  var args = new ListCustomerOrdersSinceParams(customerId, since);
  var parameters = new DynamicParameters(args);
  parameters.Add("since", args.Since, DbType.DateTime2);
  ```

- **Some parameters Dapper cannot send.**  For a parameter of these types, Dapper sends another type, or refuses it, or the column refuses it:
  - `datetime2`, which Dapper sends as a `datetime`, as above;
  - `rowversion`, which SQL Server fills and refuses a value for: leave it out of the columns of an `INSERT`;
  - `vector`, whose `SqlVector<float>` Dapper refuses with `NotSupportedException`.

  For `datetime2` and `vector`, give Dapper the parameters by hand.  Give that one parameter as a `SqlMapper.ICustomQueryParameter` that adds a `SqlParameter` with its `SqlDbType`:

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

  `SqlDbTypeExtensions` is part of Microsoft.Data.SqlClient, in the namespace `Microsoft.Data`.  So the call builds on .NET 8 and later.  `SqlDbType.Vector` is in .NET 10 alone.

  A `string` for a `json` column needs nothing: SQL Server converts the text from Dapper to `json`.
- **A type override to a type that SqlClient does not give needs a Dapper type handler.**  Dapper fills a property with the type that SqlClient gives for its column, or through a handler.  For example, a settings file's `"date": "System.DateOnly"` makes a property a `DateOnly`, where SqlClient gives a `DateTime`.  `"time": "System.TimeOnly"` makes a property a `TimeOnly`, where SqlClient gives a `TimeSpan`.  An alias type mapped to a struct of your own is the same: SqlClient gives the base type of the alias type, such as a `string` for one over `nvarchar`.  Add a handler for each, once, beside `MatchNamesWithUnderscores`:

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

  Then a `DateOnly` parameter is sent as a `date`, and a `TimeOnly` as a `time`.

  A type of yours that has [converters](../reference/converters.md#converters) for the query methods needs only a handler that calls them.  Here the type is `Email`, mapped from an alias type over `nvarchar`, and its converters take and return a `string`:

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

- **On a connection with a transaction open, give every call the transaction.**  Without it, SqlClient refuses the command with `InvalidOperationException`:

  ```csharp
  await using var transaction = await connection.BeginTransactionAsync();
  var note = await connection.QuerySingleAsync<AddNoteDto>(Sql.AddNote, args, transaction);
  await transaction.CommitAsync();
  ```

<a id="sql-only"></a>

## Only the SQL

To get the SQL of each query and nothing else, for Dapper or your own ADO.NET code to run, use the output `sql`.  Follow steps 1 to 3, set the output to `sql`, and skip the tool.  For a SQL Server project:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "mssql",
    "output": "sql",
}
```

**What you get.**  Each query gets its constant.  A query with a `{{token}}` gets a method that fills the token in, in place of the constant.  You run it as [Models for Dapper, start to end](#models) shows, without the models.

**The rules you need.**

- `sql` needs no database, no tool and no sidecar.  See [Output](../reference/settings.md#setting-output).
- The tool leaves out each query whose output is `sql`, so a run on the project reports nothing for it.  The build needs no sidecar for it, and generates no models for it.
- Without `sql`, the default output, `repository`, asks for every query to be described, and for a method that runs it.  The build then reports an error for each file or query that is not described.  See [Database support](../reference/databases/index.md#database-support).

## If it does not work

- **Dapper sends no value for a parameter such as `@account_id`.**  Dapper sends a property only to a parameter of its name, and it does not ignore `_`.  Write the parameter in camelCase.  See [the limit](#limit-parameter-names).
- **`InvalidOperationException`, or a property left as the class made it, for a column whose alias ends in `!` or `?`.**  The mark stays in the name of the column.  Use a `-- column:` marker.  See [Run the models through Dapper](#models-through-dapper).
- **`@ids IS NULL` is an error on SQL Server, for a list of two values or more.**  Dapper rewrites every `@ids` of the query.  Leave out the null test.  See [Run the models through Dapper](#models-through-dapper).
- **`ArgumentException` for a `DateTimeOffset` parameter on PostgreSQL.**  A `timestamptz` takes an offset of 0 alone.  Pass `value.ToUniversalTime()`.  See [With PostgreSQL](#dapper-with-postgresql).
- **Dapper refuses a parameter, or the column refuses it.**  Dapper cannot send some types.  Cast the parameter in the SQL, or give it as a `TypedParameter`, as the section of your database says.  See [With PostgreSQL](#dapper-with-postgresql) and [With SQL Server](#dapper-with-sql-server).
- **A `datetime2` loses digits on SQL Server, or `WHERE` finds no row of the time you gave.**  Dapper sends a `DateTime` as a `datetime`.  Send it as a `datetime2`.  See [With SQL Server](#dapper-with-sql-server).
- **`InvalidOperationException` on SQL Server, on a connection with a transaction open.**  SqlClient refuses a command without the transaction.  Give every call the transaction.  See [With SQL Server](#dapper-with-sql-server).

<a id="limits"></a>

## Limits

<a id="limit-parameter-names"></a>

### Dapper sends no value for a parameter such as `@account_id`

**What you see.**  A query that writes `@account_id` gets no value from the property `AccountId` of its input model.

**Why.**  An input model binds through Dapper only when the query writes its parameters as the properties of the model are named.  Dapper sends a property of a parameter object only to a parameter that the SQL writes as `@` and the name of the property.  It ignores case, but not `_`.  So the property `AccountId` binds to `@accountId`, and a query that writes `@account_id` gets no value from it.

**What to do.**  Do one of these:

1. Write the parameters of the query in camelCase, such as `@accountId`.
2. Give Dapper the parameters yourself, as in `new { account_id = args.AccountId }`.

See [Run the models through Dapper](#models-through-dapper).
