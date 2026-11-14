# PostgreSQL: running queries

<a id="query-methods"></a>

## Query methods

```sql postgres
-- name: AddEvent -> one
INSERT INTO events (kind, payload, happened_at)
VALUES (@kind, @payload, @happenedAt)
RETURNING id;
```

```csharp
[SqlSourceGenerate]
public partial class EventRepository(NpgsqlDataSource dataSource);

// elsewhere
AddEventDto added = await events.AddEvent(
    new AddEventParams("signup", """{"plan": "free"}""", DateTime.UtcNow));
```

Each described query whose output is `repository` or `methods` gets a method that runs it through Npgsql: see [query methods](../../../generator.md#query-methods).  A repository method runs on a connection from its [connection source](../../../generator.md#repository-connection), best an `NpgsqlDataSource`, as above: the data source your application builds, with its plugins and mappings, gives each method a connection that has them.  A connection string makes a bare `NpgsqlConnection` that has none of them, as [the limit](#limit-connection-string-repository) says.  A method at the output `methods` runs on the `DbConnection` it is given, such as `NpgsqlConnection` or the one an `NpgsqlDataSource` gives.  The project references Npgsql 8.0 or later ([SQLSRC273](../../../diagnostics.md#sqlsrc273), [SQLSRC247](../../../diagnostics.md#sqlsrc247)).  Above, `kind` is an enum `event_kind`, `payload` a `jsonb` and `happened_at` a `timestamptz`, so the method sends the `string` of `@kind` as an `event_kind`, that of `@payload` as a `jsonb` and the `DateTime` of `@happenedAt` as a `timestamptz`, with no cast in the SQL.

<a id="method-parameters"></a>

## How a parameter is sent

Each parameter is sent as the PostgreSQL type it was described with, or the type its [`-- param:` marker](../../parameters.md#param-marker) declares, and never as a type chosen from the C# type of its value.  A `string` for a `jsonb`, an `xml`, an `ltree` or an enum, a `uint` for an `oid`, a `decimal` for a `money` and a `DateTimeOffset` at any offset for a `timetz` each reach the server as that type.  A null is sent as a `NULL` of that type, so `@p IS NULL` works with no cast once the parameter has a type, from another use or a `-- param:` marker, as in [Parameters](index.md#parameters).  How a method names a parameter and sends a null is on [the generator page](../../../generator.md#method-parameters).  For some types:

- **A `timestamptz` parameter takes a `DateTimeOffset` at offset 0, or a `DateTime` whose `Kind` is `Utc`**, and a `timestamp` parameter a `DateTime` of another `Kind`.  Any other throws `ArgumentException`, the query does not run, and nothing is converted in the session's time zone.  A `DateTime` of the wrong `Kind`, an `Unspecified` one for a `timestamptz` or a `Utc` one for a `timestamp`, leaves the connection open and a transaction on it as it was.  A `DateTimeOffset` at another offset also closes the connection: a transaction open on it is lost, and the next command on it throws `InvalidOperationException`, so open a new connection to go on.  Npgsql 8 and 10 do the same, with Npgsql.NodaTime's plugin or without it.  Send `value.ToUniversalTime()` to a `timestamptz`; a value at offset 0 leaves the connection open.
- **A parameter declared as a domain is sent as the domain's base type**, so the domain's `CHECK` is not applied to it.  Compared with a column of the domain, a value that the domain refuses finds no row, and the server checks it only when it is written to such a column.
- **A `string` longer than the `varchar(n)` column it is written to is refused by the server**, with `22001`, and never cut to fit.
- **A parameter that has a [converter](../../../settings.md#converters)** is converted to the type of [the table](types.md#type-table), a domain's base type for a domain, and sent as its database type too.
- **An array parameter** takes any sequence of its element type: see [array parameters](#array-parameters).
- **A [list parameter](../../parameters.md#list-parameters)** sends each value as a parameter of the values' type, an enum's label as that enum, and its test for null, `@ids IS NULL`, as a `boolean` of the list's name.  A query takes at most 65,535 parameters in all, which [the limit](../../parameters.md#list-limit) counts.
- **A parameter of a [JSON](libraries.md#json) type, or a class of yours in one,** is sent as its `json` or `jsonb`: a `JsonElement`, a `JsonDocument`, and on Npgsql 10 a `JsonNode`, a `JsonObject` or a `JsonArray`, by Npgsql, and on Npgsql 8 and 9 those three, and a class on every version, as their JSON's text.  A class by [the JSON opt-in](../../../settings.md#json-opt-in) is sent as its JSON's text, as the parameter's own type: a `text` parameter as a `text`.

<a id="array-parameters"></a>

## Array parameters

```sql postgres
-- name: FindUsersByIds
SELECT id, name FROM users WHERE id = ANY(@ids);
```

```csharp
HashSet<int> ids = [3, 1, 2];
FindUsersByIdsDto[] found = await users.FindUsersByIds(new FindUsersByIdsParams(ids));
```

In a query method, an array parameter takes any `IEnumerable<T>` of its element type: an array, a `List<T>`, a `HashSet<T>`, an `ImmutableArray<T>` or the result of a LINQ query.  Above, `FindUsersByIdsParams.Ids` is an `IEnumerable<int>`.  The same holds for an array of a library's type, such as NodaTime's or NetTopologySuite's, and for a parameter that can be null, an `IEnumerable<T>?`, which is sent as `NULL`.

- **A sequence is read once.**  An array, a `List<T>`, an `ImmutableArray<T>`, a `ReadOnlyCollection<T>` and any other `IList<T>` are sent as they are.  A `HashSet<T>`, a type that is only an `ICollection<T>` or an `IReadOnlyList<T>`, and a lazy sequence are copied into an array once, as the method sends them, so a lazy sequence is enumerated once and never twice.  The order of a `HashSet<T>` is the set's own, and so is the order of the array it gives.
- **A null element** is sent as a `NULL` element, so the element type must be one that can be null: see [what to know about some types](types.md#type-notes).
- **The `models` output keeps `T[]`.**  An input model of a query whose output is `models` holds an array as an array, so that a library such as Dapper can send it as one.  A model of one name shared by a query whose output is `methods` or `repository` and one whose output is `models` therefore differs, and is [SQLSRC257](../../../diagnostics.md#sqlsrc257), which names both property types.  Name the models apart with [`-- output-model:`](../../../settings.md#setting-model-name) or `-- input-model:`.
- **A C# type that a `-- param:` marker gives is taken as written.**  `-- param: @ids -> int[]` makes the property an `int[]`.  So is a [type override](../../../settings.md#setting-type-overrides) to a type that is not an array of one rank: `int4[]=System.Collections.Generic.List<int>` makes it that `List<int>`.  An override to an array still becomes an `IEnumerable<T>` of its element, as the type table's does: `int4[]=long[]`, or `int4=long`, which makes the array a `long[]`, gives an `IEnumerable<long>`.
- **These are not sequences, and stay as they are:** `bytea`, a `byte[]`; a multidimensional array such as `int[,]`; `int2vector` and `oidvector`; and the multiranges, which are sent as one value.
- **A caller that reads the array back from an input model** of a query with a method, `ids.Length` for instance, sees an `IEnumerable<T>` now, though constructing the model with an array still compiles.  Copy the sequence into an array, `ids.ToArray()`, where you need one.
- **`= ANY(@ids)` or `IN @ids`.**  `id = ANY(@ids)` sends the sequence as one array, so the query has one form whatever the count of values, and no limit of parameters applies to it.  `id IN @ids`, with no parentheses, is a [list parameter](../../parameters.md#list-parameters): it sends one parameter for each value, [padded](../../parameters.md#list-padding) to a few sizes, up to 65,535 parameters in all, and is written as on SQL Server, so one query's text runs on both.  Its property is an `IEnumerable<T>` in the output `models` too, where an array's is a `T[]`.  `IN (@ids)` compares with one array, and is not what you want.

<a id="method-results"></a>

## What a method reads and throws

- **Give each `methods` method the transaction** of a connection that has one; a repository method takes none, and runs in no transaction of yours.  Npgsql runs a command in its connection's transaction even when the method is not given it, but Microsoft.Data.SqlClient does not, so code that passes it runs on both: see [the connection](../../../generator.md#method-connection).
- **A column is read as its property's C# type**, the one [the types](types.md#type-map) give it, so a type that Npgsql reads only once it is taught, an enum mapped to a C# enum or a plugin's type, needs the teaching on the data source, as `MapEnum<T>()`.  A type of yours that Npgsql never reads takes [converters](../../../settings.md#converters) instead: the column is read as the type of [the table](types.md#type-table), a domain as its base type, and converted.
- **Every type that [the table](types.md#type-table) gives a C# type is sent and read on Npgsql 8, 9 and 10**, as the C# type of the version's column, and SqlSource is tested on Npgsql 8 and 10.  `cube`, which Npgsql 8 and 9 do not read, is sent and read on Npgsql 10.  So is each type of [NodaTime](libraries.md#nodatime), on each version with its `Npgsql.NodaTime`, and of [NetTopologySuite](libraries.md#nettopologysuite), with its `Npgsql.NetTopologySuite`.
- **NodaTime's types need `UseNodaTime()` on the data source.**  A method that reads or sends a [NodaTime](libraries.md#nodatime) type runs only on a connection of a data source whose builder called `UseNodaTime()`, of `Npgsql.NodaTime`: for a repository, the data source that is its connection source, or that its factory method opens connections from, never a connection string.  Without it, Npgsql throws `InvalidCastException`, "Reading as 'NodaTime.Instant' is not supported for fields having DataTypeName 'timestamp with time zone'", or the same for writing, when the query runs:

  ```csharp
  var builder = new NpgsqlDataSourceBuilder(connectionString);
  builder.UseNodaTime();
  await using var dataSource = builder.Build();
  ```

- **Entity Framework Core's NodaTime package turns NodaTime on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` brings `Npgsql.NodaTime`, so the query methods of a project that references it get NodaTime's types.  `UseNodaTime()` inside EF Core's `UseNpgsql(...)` teaches the data source that EF Core builds for itself, and no other: call `UseNodaTime()` on the builder of the data source whose connections run the query methods, or turn NodaTime off with the [library types](../../../settings.md#setting-library-types), `"nodaTime": "off"`.
- **NetTopologySuite's types need `UseNetTopologySuite()` on the data source**, of `Npgsql.NetTopologySuite`, as NodaTime's need `UseNodaTime()`.  Without it, Npgsql throws `InvalidCastException`, "Reading as 'NetTopologySuite.Geometries.Geometry' is not supported for fields having DataTypeName 'public.geometry'", or the same for writing, when the query runs.
- **Entity Framework Core's NetTopologySuite package turns NetTopologySuite on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings `Npgsql.NetTopologySuite`, and `UseNetTopologySuite()` inside EF Core's `UseNpgsql(...)` teaches EF Core's own data source alone, as for NodaTime: call `UseNetTopologySuite()` on the builder of the data source whose connections run the query methods, or turn NetTopologySuite off with the [library types](../../../settings.md#setting-library-types), `"netTopologySuite": "off"`.
- **`UseNodaTime()` changes what Npgsql gives Dapper too.**  On a data source that calls it, every reader gets NodaTime's types: a `timestamptz` arrives as an `Instant`, so a query whose output is `models`, read through Dapper into its `System.DateTime` property, fails, and a `tstzrange` or a `daterange` arrives as an `Interval` or a `DateInterval`, which throws for an empty range, whatever the property's type.  That is the plugin's doing.  Give such queries NodaTime's types with the [type override](../../../settings.md#setting-type-overrides), `"timestamptz": "NodaTime.Instant"` in a settings file of their folder, or read them on a data source without the plugin.
- **System.Text.Json's types and a class of yours in a [JSON](libraries.md#json) column need nothing on the data source**, on Npgsql 8, 9 and 10: no `EnableDynamicJson()`.
- **A column whose property cannot be null** throws Npgsql's `InvalidCastException`, "Column 'name' is null.", when it holds a null, before any converter is called.  A JSON `null` in such a column is no null, and what it gives is under [JSON](libraries.md#json).
- **`rowcount` is `-1` for a `SELECT`**, and for a `WITH` whose last statement is a `SELECT`, though its `WITH` updates rows: see [what each shape returns](../../../generator.md#method-shapes).
- **`commandTimeout`**, of a `methods` method, that passes throws Npgsql's `NpgsqlException`, whose inner exception is a `TimeoutException`.
- **A synchronous method needs a data source without multiplexing.**  With `Multiplexing=true` in the data source's connection string, a [synchronous method](../../../generator.md#synchronous-methods) given a connection that has no transaction open throws `NotSupportedException` and runs nothing, while one given a connection in a transaction that the caller has begun on it runs.  A synchronous [collection method](../../../generator.md#collection-methods) runs in the transaction it begins of its own, so it runs on such a data source too, except where it begins none: under `transaction=off`, or when a `System.Transactions` transaction is open, as inside a `TransactionScope`.  A [synchronous repository method](../../../generator.md#synchronous-repository-methods) runs on a connection with no transaction open, but for a borrowed connection in which your code began one, so it throws on such a data source too, while its synchronous collection method runs, as above.  Give the code that calls synchronous methods a data source without multiplexing, which is the default, or call the asynchronous method that `method-type=both` gives beside it.
- **A cancelled method throws `OperationCanceledException`**, whether the token was cancelled before the call, while the command runs or while the rows are read.
- **A loop over [streamed rows](../../../settings.md#streaming-rows) that ends early throws the error of a row it left unread.**  At a `break`, or an exception in the loop's body, the rows still to come are read, and a `PostgresException` that PostgreSQL raises for one of them, such as `22012` for a division by zero, is thrown when the loop ends, in place of the body's exception, and aborts the transaction the query ran in.  To keep the body's exception too, see [streamed rows](../../../settings.md#streaming-rows).
- **The connection's commands must take Npgsql's own parameters**, as those of `NpgsqlConnection` do.  A connection that wraps Npgsql's and creates parameters of its own is a [known issue](../../../generator.md#known-issues).

<a id="collection-methods"></a>

## Collection methods

```sql postgres
-- name: AddEvent -> rowcount
-- generator: collection-method transaction=off
INSERT INTO events (kind, payload, happened_at)
VALUES (@kind, @payload, @happenedAt);
```

A query's [collection method](../../../generator.md#collection-methods) runs its items through Npgsql, 1,000 to a round trip.  What a failed item leaves in the database depends on the transaction the method runs in:

| The transaction | When an item fails |
|----|----|
| Its own, by `transaction=on`, the default, or an isolation level | Every item is rolled back, those of earlier round trips too, and nothing is left |
| None, by `transaction=off` | Each round trip's items run in a transaction that PostgreSQL begins for them alone, and commit or roll back together: the failed item's round trip is rolled back, the items before it included, the round trips before it stay committed, and those after it do not run |
| The caller's, a `transaction` given at the output `methods` or an ambient one | The method leaves it to you.  PostgreSQL aborts a transaction at an error, so it takes no other command until you roll it back, and what ran in it is rolled back with it |

- **The failed item is named with nothing to add to the connection string.**  Its position is the `ItemIndex` of [`CollectionItemException`](../../../generator.md#collection-item-exception), whose `InnerException` is Npgsql's `PostgresException`, and whose `SqlState`, such as `23505` for a unique violation, is the error's.
- **An error at a commit is no item's.**  A constraint declared `DEFERRABLE INITIALLY DEFERRED` is checked when the transaction commits, after every item has run, so its error is thrown as Npgsql's `PostgresException`, as it is, and the transaction is rolled back.  Under `transaction=off` that is the commit of each round trip.
- **PostgreSQL has three isolation levels**: `read-committed`, its default, `repeatable-read` and `serializable`.  `read-uncommitted` and `snapshot` are [SQLSRC297](../../../diagnostics.md#sqlsrc297).
- **The SQL is sent without its comments**, even under [`keep-comments`](../../../settings.md#setting-generator-parameters), so a comment that you keep for the database's logs reaches them from the query method alone.
- **`commandTimeout`** is that of each round trip, and **a cancelled method throws `OperationCanceledException`**, as a query method does; either way, the method's own transaction is rolled back.

<a id="limits"></a>

## Limits

<a id="limit-connection-string-repository"></a>

### `InvalidCastException` for a NodaTime, PostGIS, enum or composite type in a repository made from a connection string

- **A repository whose connection source is a connection string reads and sends no type that needs a plugin or a mapping.**  Its methods make a new `NpgsqlConnection` of the string for each call, which knows nothing of your application's data source: not `UseNodaTime()`, `UseNetTopologySuite()`, `MapEnum<T>()` or `MapComposite<T>()`.  A method that reads or sends such a type compiles, and throws Npgsql's `InvalidCastException` when it runs, as [what a method reads and throws](#method-results) says.  Give the repository an `NpgsqlDataSource`, which Npgsql recommends over a connection string, built with the plugins and mappings, as its [connection source](../../../generator.md#repository-connection).
