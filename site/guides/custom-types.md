# Use your own types: ids, enums, value objects

<a id="type-override"></a>

## Give a database type a C# type

Each column and parameter of a PostgreSQL query whose output is `models`, `methods` or `repository`, and whose entry in its sidecar is current, gets the C# type that Npgsql reads its database type as, or NodaTime's or NetTopologySuite's in the query methods of a project that references `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, and System.Text.Json's where it turns JSON on: see [the PostgreSQL types](../reference/databases/postgresql/types.md#type-map).  Those of a SQL Server query get the C# type that Microsoft.Data.SqlClient reads their database type as, or NodaTime's, NetTopologySuite's or System.Text.Json's in the query methods of a project that turns that library on: see [the SQL Server types](../reference/databases/sqlserver/types.md#types).  A project at the output `models` that references no Microsoft.Data.SqlClient, such as a library that leaves the driver to its application, gets the same types, but for `vector`, which needs the reference: see [which version of Microsoft.Data.SqlClient](../reference/databases/sqlserver/types.md#which-sqlclient).  A few PostgreSQL types follow the version of Npgsql that the project references: Npgsql 10 reads `date`, `time`, `cidr`, `daterange` and `datemultirange` as other types than Npgsql 8 and 9 do, and a project that references no Npgsql gives those types their C# types with the type override, `"date": "System.DateOnly"` and `"time": "System.TimeOnly"` in a settings file's `typeOverrides` for an application on Npgsql 10: see [which version of Npgsql](../reference/databases/postgresql/types.md#which-npgsql).  The type override gives a database type the C# type of your choice instead, for the `.sql` files under a settings file, or in a `.sql` file with `-- type-override:`, and each place changes only the types it names:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "postgres",
    "typeOverrides": {
        "timestamptz": "System.DateTimeOffset",
        "public.user_status": "MyApp.UserStatus",
    },
}
```

Each property of a model has the type its database type maps to, and a query method asks the driver for that type, so it must be one the driver reads, or one that your [converters](../reference/converters.md#converters) convert to and from one it reads; a class of yours in a JSON column is read and sent through System.Text.Json's serializer instead, as [a class in a JSON column](../reference/library-types-and-json.md#json-class) says, and on SQL Server the query methods convert NodaTime's, NetTopologySuite's and System.Text.Json's types themselves, as [SqlSource's own conversions](../reference/converters.md#converters) says.  For the driver's part, see [troubleshooting](troubleshooting.md#invalidcastexception).  An entry that is not written correctly, or whose key has no schema and is not a type SqlSource knows, is a build error, and so is a C# type that the project cannot find or use, at each query that uses it.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).

<a id="one-column"></a>

## Give one column or one parameter its own type

Where two columns of one database type need two C# types, `users.id` a `UserId` and `posts.id` a `PostId` though both are `bigint`, map the column a value is read from with `columnTypes` in a settings file, or with `-- column:` in a `.sql` file, and give a parameter its type on its `-- param:` line.  Either wins over the type override:

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "postgres",
    "columnTypes": {
        "users.id": "UserId",
        "posts.id": "PostId",
    },
}
```

```sql fragment
-- param: @author -> UserId
```

A C# type may be written by its short name, `UserId`, wherever SqlSource takes one, when the project and its references have one type of that name that the project can see; the generated code writes its full name.  See [column types](../reference/model-properties.md#setting-column-types) and [giving a parameter a type](../reference/parameters.md#param-marker).

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

The column types give the columns `customers.id` and `orders.customer_id` the type `CustomerId` in every query that reads them, and the `-- param:` marker gives it to `@id`, which is no column.  The other `bigint` columns of the project keep their `long`, and the same entries work on SQL Server.  A computed column, such as `max(customer_id)`, takes the type from a [`-- column:` marker](../reference/model-properties.md#column-marker) inside its query.

Where the schema makes the ids a type of their own, a domain, `CREATE DOMAIN customer_id AS bigint`, or on SQL Server an alias type, map that type with the type override, and on PostgreSQL declare each parameter compared with such an id as the domain, since PostgreSQL gives it the domain's base type, `bigint`, otherwise:

```sql postgres
-- name: GetCustomer -> one-optional
-- type-override: public.customer_id=MyApp.CustomerId
-- param: @id public.customer_id
SELECT id, name FROM customers WHERE id = @id;
```

Rules: [column types](../reference/model-properties.md#setting-column-types), [giving a parameter a type](../reference/parameters.md#param-marker), [converters](../reference/converters.md#converters), [type overrides](../reference/type-mapping.md#setting-type-overrides).

<a id="vogen"></a>

## An id type made by Vogen or StronglyTypedId

If your id type is made by Vogen or StronglyTypedId, write two converters for it.  Without them, the query method throws `InvalidCastException`.

1. **Declare your type**, here with Vogen:

   ```csharp
   using Vogen;

   [ValueObject<long>]
   public readonly partial struct CustomerId;
   ```

2. **Give the column and the parameter your type.**  Map the column in `sqlsource.json`, and the parameter in its `-- param:` line:

   ```jsonc
   // sqlsource.json, in the project's folder
   {
       "root": true,
       "columnTypes": {
           "customers.id": "CustomerId",
       },
   }
   ```

   ```sql postgres
   -- name: GetCustomer -> one-optional
   -- param: @id -> CustomerId
   SELECT id, name FROM customers WHERE id = @id;
   ```

3. **Write two converters** in a class of your project.  Each calls a member of your type:

   ```csharp
   using SqlSource;

   internal static class IdConversions
   {
       [SqlSourceConverter]
       public static CustomerId ToCustomerId(long value) => CustomerId.From(value);

       [SqlSourceConverter]
       public static long FromCustomerId(CustomerId id) => id.Value;
   }
   ```

   For a type of StronglyTypedId, call its constructor, `new CustomerId(value)`, in place of `From`.

4. **Call the method** with your type:

   ```csharp
   GetCustomerDto? customer = await customers.GetCustomer(new GetCustomerParams(CustomerId.From(42)));
   ```

**What you get.**  `GetCustomerDto.Id` and `GetCustomerParams.Id` are a `CustomerId`.  The method reads `id` as a `long`, and gives it to `ToCustomerId`.  It sends `@id` as the `long` that `FromCustomerId` returns.

**The rules you need.**

- SqlSource converts a type only through methods marked `[SqlSourceConverter]`.  It never calls the `Value`, `From` or constructor of your type itself.  See [the limit](../reference/converters.md#limit-vogen-and-stronglytypedid).
- A converter converts from the type of its argument to its return type, whatever its name.  A column needs the converter to your type, and a parameter the converter back.  See [Converters](../reference/converters.md#converters).
- A converter takes or returns exactly the type that the driver reads: a `long` for a `bigint`, an `int` for an `integer`.  A converter of another type is not used.  See [Converters](../reference/converters.md#converters).
- Null never reaches a converter, so one pair serves `CustomerId` and `CustomerId?`.  See [Converters](../reference/converters.md#converters).
- A query whose output is `models` calls no converter.  To read it through Dapper, see [Run the models through Dapper](dapper.md#models-through-dapper).  See [Converters](../reference/converters.md#converters).

<a id="enum"></a>

## Read a column as a C# enum

If you want a C# enum for a column, map the column to your enum.  PostgreSQL and SQL Server need different steps, and a missing step gives `InvalidCastException`.

**On PostgreSQL**, map the enum type of your schema to your C# enum with a type override:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "typeOverrides": {
        "public.order_status": "MyApp.OrderStatus",
    },
}
```

```csharp
namespace MyApp;

public enum OrderStatus
{
    Pending,
    Shipped,
    Cancelled,
}
```

Then map the enum in Npgsql, on the data source that your application builds:

```csharp
var builder = new NpgsqlDataSourceBuilder(connectionString);
builder.MapEnum<OrderStatus>();
await using var dataSource = builder.Build();
```

**On SQL Server**, which has no enum types, declare the enum over the type of its column.  Here `priority` is a `tinyint`, so the enum is over `byte`:

```sql mssql
-- name: ListTicketsByPriority
-- type-override: tinyint = MyApp.Priority
SELECT id, title, priority
FROM dbo.tickets
WHERE priority = @priority;
```

```csharp
namespace MyApp;

public enum Priority : byte
{
    Low = 1,
    High = 2,
}
```

**What you get.**  On PostgreSQL, each `order_status` column and parameter is an `OrderStatus`.  On SQL Server, the column `priority` and the parameter `@priority` of the query are a `Priority`.

**The rules you need.**

- A type override gives each column and parameter of a database type your C# type.  A `-- type-override:` line inside a query changes that query only.  See [Type overrides](../reference/type-mapping.md#setting-type-overrides).
- On PostgreSQL, the method asks Npgsql for your enum, so the data source that the methods run on must call `MapEnum<T>()`.  See [What a method reads and throws](../reference/databases/postgresql/query-methods.md#method-results).
- On PostgreSQL, a repository made from a connection string has none of the mappings of your data source.  Give it your data source.  See [the limit](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- On SQL Server, an enum is read only from a column of its own underlying type: an enum over `int`, the default, from an `int`, and one over `byte` from a `tinyint`.  See [Reading a row](../reference/databases/sqlserver/query-methods.md#reading-a-row).

## If it does not work

- **`InvalidCastException` when the query runs.**  The driver was given a type that it cannot read: a type of yours with no converters, or a PostgreSQL enum that the data source does not map.  Write the converters, or call `MapEnum<T>()`.  See [`InvalidCastException`](troubleshooting.md#invalidcastexception).
- **`InvalidCastException` for an enum, on SQL Server.**  The enum is not over the type of its column, such as an enum over `int` for a `tinyint`.  Declare it over the type of the column.  See [Reading a row](../reference/databases/sqlserver/query-methods.md#reading-a-row).
- **SQLSRC279.**  A converter takes or returns another type than the driver reads, such as an `int` for a `bigint`.  Change it to the type that the error names.  See [SQLSRC279](../diagnostics.md#sqlsrc279).
