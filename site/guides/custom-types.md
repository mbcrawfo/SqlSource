# Use your own types: ids, enums, value objects

Give a column an enum, a strongly typed id, a Vogen type or another type of yours, and avoid `InvalidCastException` with a converter.

**Use this when** a column or a parameter needs a C# type of yours, such as an id type or an enum.  Use it too when two columns of one database type need two C# types.

<a id="type-override"></a>

## Give a database type a C# type

To give every column and parameter of one database type a C# type of your choice, write a type override in a settings file:

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

**What you get.**  In the `.sql` files under this settings file, each `timestamptz` column and parameter is a `System.DateTimeOffset`, and each `public.user_status` a `MyApp.UserStatus`.  The other database types keep their C# types.

**The rules you need.**

- **Without a type override, a column or a parameter gets the C# type that the driver reads its database type as.**  In a query method it can get the type of a library instead: NodaTime, NetTopologySuite or System.Text.Json.  On PostgreSQL, NodaTime is on when the project references `Npgsql.NodaTime`, and NetTopologySuite when it references `Npgsql.NetTopologySuite`.  On SQL Server, and for System.Text.Json, a library is on only when you turn it on.  See the types of [PostgreSQL](../reference/databases/postgresql/types.md#type-map) and [SQL Server](../reference/databases/sqlserver/types.md#types), and [library types](../reference/library-types-and-json.md#setting-library-types).
- **On PostgreSQL, a few C# types follow the version of Npgsql.**  A project that references no Npgsql gives those database types their C# types with a type override.  See [which version of Npgsql](../reference/databases/postgresql/types.md#which-npgsql).
- **On SQL Server, a project at the output `models` that references no Microsoft.Data.SqlClient gets the C# types of a project that references it, but for `vector`.**  `vector` needs the reference.  See [which version of Microsoft.Data.SqlClient](../reference/databases/sqlserver/types.md#which-sqlclient).
- **A type override can be in a settings file, for the `.sql` files under it, or in a `.sql` file, with `-- type-override:`.**  Each place changes only the types it names.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).
- **The C# type must be one that the driver reads, or one that your [converters](../reference/converters.md#converters) convert to and from one that it reads.**  A query method asks the driver for the C# type of each property.  For the error of the driver, see [troubleshooting](troubleshooting.md#invalidcastexception).
- **A class of yours in a JSON column needs no converters.**  The query methods read and send it through System.Text.Json's serializer.  See [a class in a JSON column](../reference/library-types-and-json.md#json-class).
- **On SQL Server, the query methods convert the types of NodaTime, NetTopologySuite and System.Text.Json themselves.**  See [SqlSource's own conversions](../reference/converters.md#converters).
- **An entry that is not written correctly is a build error.**  So is an entry whose key has no schema and is not a type that SqlSource knows.  A C# type that the project cannot find or use is a build error at each query that uses it.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).

<a id="one-column"></a>

## Give one column or one parameter its own type

Two columns of one database type can need two C# types: `users.id` a `UserId` and `posts.id` a `PostId`, though both are `bigint`.  Map each column with `columnTypes` in a settings file:

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

Give a parameter its type on its `-- param:` line:

```sql fragment
-- param: @author -> UserId
```

**What you get.**  `users.id` is a `UserId` and `posts.id` a `PostId`, in every query that reads them.  The parameter `@author` is a `UserId`.

**The rules you need.**

- **A column type maps the column that a value is read from.**  You can also map it with `-- column:` in a `.sql` file.  See [column types](../reference/model-properties.md#setting-column-types).
- **A column type and a `-- param:` line each win over the type override.**  See [giving a parameter a type](../reference/parameters.md#param-marker).
- **You can write a C# type by its short name, `UserId`, wherever SqlSource takes one.**  The project and its references must have one type of that name that the project can see.  The generated code writes its full name.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).

<a id="strongly-typed-id"></a>

## Give an id a type of its own

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
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

**What you get.**  `GetCustomerDto.Id` and `GetCustomerParams.Id` are a `CustomerId`.  The column types give the columns `customers.id` and `orders.customer_id` the type `CustomerId` in every query that reads them.  The `-- param:` marker gives it to `@id`, which is no column.  The other `bigint` columns of the project keep their `long`.

Here is the same query, with the ids as a domain on PostgreSQL.  It maps the domain, and declares `@id` as the domain:

```sql postgres
-- name: GetCustomer -> one-optional
-- type-override: public.customer_id=MyApp.CustomerId
-- param: @id public.customer_id
SELECT id, name FROM customers WHERE id = @id;
```

**The rules you need.**

- **A parameter is no column, so it takes its type from its `-- param:` marker.**  See [giving a parameter a type](../reference/parameters.md#param-marker).
- **The same entries work on SQL Server.**  See [column types](../reference/model-properties.md#setting-column-types).
- **A computed column, such as `max(customer_id)`, takes its type from a [`-- column:` marker](../reference/model-properties.md#column-marker) inside its query.**
- **`CustomerId` needs its two converters.**  A column needs the one from the `long` that the driver reads, and a parameter the one back.  See [converters](../reference/converters.md#converters).
- **If your schema makes the ids a type of their own, map that type with the type override.**  That type is a domain on PostgreSQL, `CREATE DOMAIN customer_id AS bigint`, or an alias type on SQL Server.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).
- **On PostgreSQL, declare each parameter that is compared with such an id as the domain.**  Otherwise PostgreSQL gives the parameter the base type of the domain, `bigint`.

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
       "dialect": "postgres",
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
- On SQL Server, an enum is read only from a column of its own underlying type.  An enum over `int`, the default, reads an `int`, and one over `byte` a `tinyint`.  See [Reading a row](../reference/databases/sqlserver/query-methods.md#reading-a-row).

## If it does not work

- **`InvalidCastException` when the query runs.**  The driver was given a type that it cannot read.  It is a type of yours with no converters, or a PostgreSQL enum that the data source does not map.  Write the converters, or call `MapEnum<T>()`.  See [`InvalidCastException`](troubleshooting.md#invalidcastexception).
- **`InvalidCastException` for an enum, on PostgreSQL, though your data source calls `MapEnum<T>()`.**  A repository made from a connection string makes connections that have none of the mappings of your data source.  Give the repository your data source.  See [a repository made from a connection string](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- **`InvalidCastException` for an enum, on SQL Server.**  The enum is not over the type of its column, such as an enum over `int` for a `tinyint`.  Declare it over the type of the column.  See [Reading a row](../reference/databases/sqlserver/query-methods.md#reading-a-row).
- **SQLSRC279.**  A converter takes or returns another type than the driver reads, such as an `int` for a `bigint`.  Change it to the type that the error names.  See [SQLSRC279](../diagnostics.md#sqlsrc279).
- **A build error at an entry of a type override, or at a query that uses it.**  The entry is not written correctly, or its key has no schema and names no type that SqlSource knows.  Or the project cannot find or use its C# type.  Correct the entry.  See [type overrides](../reference/type-mapping.md#setting-type-overrides).
- **On PostgreSQL, a parameter compared with an id over a domain does not get your id type.**  PostgreSQL gives the parameter the base type of the domain.  Declare the parameter as the domain, such as `-- param: @id public.customer_id`.  See [Give an id a type of its own](#strongly-typed-id).
