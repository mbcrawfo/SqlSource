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

Each described query whose output is `repository` or `methods` gets a method that runs it through Npgsql: see [query methods](../../query-methods.md#query-methods).  In the sample, `kind` is an enum `event_kind`, `payload` is a `jsonb` and `happened_at` is a `timestamptz`.  So the method sends the `string` of `@kind` as an `event_kind` and the `string` of `@payload` as a `jsonb`.  It sends the `DateTime` of `@happenedAt` as a `timestamptz`.  The SQL needs no cast.

- **A repository method runs on a connection from its [connection source](../../repository-connections.md#repository-connection).**  The best source is an `NpgsqlDataSource`, as in the sample.  The data source that your application builds has its plugins and mappings, and it gives each method a connection that has them.
- **A connection string makes a bare `NpgsqlConnection`**, which has none of these plugins and mappings: see [the limit](#limit-connection-string-repository).
- **A method of the output `methods` runs on the `DbConnection` that you give it**, such as an `NpgsqlConnection`, or the connection that an `NpgsqlDataSource` gives.
- **The project must reference Npgsql 8.0 or later.**  With no Npgsql, the query is [SQLSRC273](../../../diagnostics.md#sqlsrc273).  An older Npgsql is [SQLSRC247](../../../diagnostics.md#sqlsrc247).  See [supported versions](../index.md#supported-versions).

<a id="method-parameters"></a>

## How a parameter is sent

A query method sends a `string` for a `jsonb` parameter as a `jsonb`.  It sends each parameter as the PostgreSQL type that the parameter was described with, or as the type that its [`-- param:` marker](../../parameters.md#param-marker) declares.  It never sends a type chosen from the C# type of the value.  So each of these reaches the server as the type of the parameter:

- a `string` for a `jsonb`, an `xml`, an `ltree` or an enum;
- a `uint` for an `oid`;
- a `decimal` for a `money`;
- a `DateTimeOffset` at any offset for a `timetz`.

A null is sent as a `NULL` of the type of the parameter.  So `@p IS NULL` works with no cast once the parameter has a type, from another use or from a `-- param:` marker, as [Parameters](index.md#parameters) shows.  How a method names a parameter and sends a null is on [the Query methods page](../../query-methods.md#method-parameters).  These rules hold for some types:

- **A `timestamptz` parameter takes a `DateTimeOffset` at offset 0, or a `DateTime` whose `Kind` is `Utc`.**  A `timestamp` parameter takes a `DateTime` of another `Kind`.  Any other value throws `ArgumentException`, and the query does not run.  Nothing is converted in the time zone of the session.
- **A `DateTime` of the wrong `Kind` leaves the connection as it was.**  The wrong `Kind` is `Unspecified` for a `timestamptz`, or `Utc` for a `timestamp`.  The connection stays open, and a transaction on it stays as it was.
- **A `DateTimeOffset` at another offset also closes the connection.**  A transaction open on it is lost, and the next command on it throws `InvalidOperationException`.  To go on, open a new connection.  Send `value.ToUniversalTime()` to a `timestamptz`: a value at offset 0 leaves the connection open.
- **Npgsql 8 and 10 handle these values in the same way**, with Npgsql.NodaTime's plugin or without it.
- **A parameter declared as a domain is sent as the base type of the domain.**  So the `CHECK` of the domain is not applied to it.  If you compare the parameter with a column of the domain, a value that the domain refuses finds no row.  The server checks the value only when it is written to a column of the domain.
- **The server refuses a `string` that is longer than the `varchar(n)` column it is written to.**  The error is `22001`.  The server never cuts the value to fit.
- **A parameter that has a [converter](../../converters.md#converters) is converted to the type that [the table](types.md#type-table) gives.**  For a domain, that is the type of the base type of the domain.  Then the parameter is sent as its database type too.
- **An array parameter takes any sequence of its element type**: see [array parameters](#array-parameters).
- **A [list parameter](../../list-parameters.md#list-parameters) sends each value as a parameter of the type of the values.**  It sends the label of an enum as that enum.  It sends its test for null, `@ids IS NULL`, as a `boolean` parameter that has the name of the list.  A query takes at most 65,535 parameters in all, as [the limit](../../list-parameters.md#list-limit) counts them.
- **A parameter of a [JSON](libraries.md#json) type, or of a class of yours in one, is sent as its `json` or `jsonb`.**  Npgsql sends a `JsonElement` and a `JsonDocument`, and on Npgsql 10 a `JsonNode`, a `JsonObject` or a `JsonArray`.  On Npgsql 8 and 9 those three are sent as the text of their JSON.  A class is sent as the text of its JSON on every version.
- **A class by [the JSON opt-in](../../library-types-and-json.md#json-opt-in) is sent as the text of its JSON, as the own type of the parameter.**  For example, a `text` parameter is sent as a `text`.

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

In a query method, an array parameter takes any `IEnumerable<T>` of its element type.  In the sample, `FindUsersByIdsParams.Ids` is an `IEnumerable<int>`, and the caller gives it a `HashSet<int>`.  The parameter takes, for example, each of these:

- an array;
- a `List<T>`;
- a `HashSet<T>`;
- an `ImmutableArray<T>`;
- the result of a LINQ query.

The same holds for an array of a library's type, such as NodaTime's or NetTopologySuite's.  It holds for a parameter that can be null too, an `IEnumerable<T>?`, whose null is sent as `NULL`.

- **A sequence is read once.**  An array, a `List<T>`, an `ImmutableArray<T>`, a `ReadOnlyCollection<T>` and any other `IList<T>` are sent as they are.  The method copies these into an array once, as it sends them:
  - a `HashSet<T>`;
  - a type that is only an `ICollection<T>` or an `IReadOnlyList<T>`;
  - a lazy sequence.

  So a lazy sequence is enumerated once, and never twice.  The order of a `HashSet<T>` is the set's own, and so is the order of the array that it gives.
- **A null element is sent as a `NULL` element.**  So the element type must be one that can be null: see [what to know about some types](types.md#type-notes).
- **The `models` output keeps `T[]`.**  An input model of a query whose output is `models` holds an array as an array.  So a library such as Dapper can send it as one.  If a query whose output is `repository` or `methods` and a query whose output is `models` share a model name, the two models differ.  That is [SQLSRC257](../../../diagnostics.md#sqlsrc257), which names both property types.  Name the models apart with [`-- output-model:`](../../models.md#setting-model-name) or `-- input-model:`.
- **A C# type that a `-- param:` marker gives is taken as written.**  `-- param: @ids -> int[]` makes the property an `int[]`.
- **A [type override](../../type-mapping.md#setting-type-overrides) to a type that is not an array of one rank is taken as written too.**  `int4[]=System.Collections.Generic.List<int>` makes the property that `List<int>`.
- **A type override to an array still becomes an `IEnumerable<T>` of its element**, as the type of the type table does.  `int4[]=long[]` gives an `IEnumerable<long>`.  So does `int4=long`, which makes the array a `long[]`.
- **These are not sequences, and stay as they are:**
  - `bytea`, a `byte[]`;
  - a multidimensional array, such as `int[,]`;
  - `int2vector` and `oidvector`;
  - the multiranges, which are sent as one value.
- **An input model of a query with a method holds an `IEnumerable<T>` where the query has an array.**  So a caller that reads the array back from the model, such as `ids.Length`, gets an `IEnumerable<T>`.  Code that constructs the model with an array still compiles.  Where you need an array, copy the sequence into one, `ids.ToArray()`.
- **`= ANY(@ids)` sends one array, and `IN @ids` sends a list.**  Tell the forms apart by these rules:
  - `id = ANY(@ids)` sends the sequence as one array.  So the query has one form whatever the count of values, and no limit of parameters applies to it.
  - `id IN @ids`, with no parentheses, is a [list parameter](../../list-parameters.md#list-parameters).  It sends one parameter for each value, [padded](../../list-parameters.md#list-padding) to a few sizes, up to 65,535 parameters in all.  It is written as on SQL Server, so the text of one query runs on both.  Its property is an `IEnumerable<T>` in the output `models` too, where the property of an array is a `T[]`.
  - `IN (@ids)` compares with one array, and is not what you want.

<a id="method-results"></a>

## What a method reads and throws

- **Give each `methods` method the transaction of a connection that has one.**  Npgsql runs a command in the transaction of its connection even when the method is not given the transaction.  Still pass it, so that the same code runs on every database: see [the connection](../../connection-methods.md#method-connection).
- **A repository method takes no transaction**: see [transactions and timeouts](../../repositories.md#repository-transactions).  It runs in no transaction of yours, except in one of these cases:
  - On a [borrowed connection](../../repository-connections.md#repository-connection) in which your code began a transaction, Npgsql runs the method in that transaction.
  - Inside a `TransactionScope`, a connection opened inside the scope takes part in the transaction of the scope, so the method runs in it.  This is not so for a connection whose connection string turns enlisting off.
- **A column is read as the C# type of its property**, which [the types](types.md#type-map) give it.  Npgsql reads some types only once it is taught them, such as an enum mapped to a C# enum, or the type of a plugin.  Such a type needs the teaching on the data source, such as `MapEnum<T>()`.
- **A type of yours that Npgsql never reads takes [converters](../../converters.md#converters) instead.**  The column is read as the type that [the table](types.md#type-table) gives, a domain as its base type, and then converted.
- **Every type that [the table](types.md#type-table) gives a C# type is sent and read on Npgsql 8, 9 and 10.**  Each is sent and read as the C# type in the column of that version.  SqlSource is tested on Npgsql 8 and 10.
- **Npgsql 8 and 9 do not read `cube`.**  It is sent and read on Npgsql 10.
- **The types of NodaTime and NetTopologySuite are sent and read on each version of Npgsql too.**  A type of [NodaTime](libraries.md#nodatime) is sent and read with its `Npgsql.NodaTime`, and a type of [NetTopologySuite](libraries.md#nettopologysuite) with its `Npgsql.NetTopologySuite`.
- **NodaTime's types need `UseNodaTime()` on the data source.**  A method that reads or sends a [NodaTime](libraries.md#nodatime) type runs only on a connection of a data source whose builder called `UseNodaTime()`, of `Npgsql.NodaTime`.  For a repository, that is the data source that is its connection source, or the one that its marked method opens connections from.  It is never a connection string.  Without the call, Npgsql throws `InvalidCastException` when the query runs: "Reading as 'NodaTime.Instant' is not supported for fields having DataTypeName 'timestamp with time zone'", or the same for writing.  Call it on the builder:

  ```csharp
  var builder = new NpgsqlDataSourceBuilder(connectionString);
  builder.UseNodaTime();
  await using var dataSource = builder.Build();
  ```

- **Entity Framework Core's NodaTime package turns NodaTime on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` brings `Npgsql.NodaTime`.  So the query methods of a project that references it get NodaTime's types.
- **`UseNodaTime()` inside EF Core's `UseNpgsql(...)` teaches only the data source that EF Core builds for itself.**  It teaches no other.  So do one of these:
  - Call `UseNodaTime()` on the builder of the data source whose connections run the query methods.
  - Turn NodaTime off with the [library types](../../library-types-and-json.md#setting-library-types), `"nodaTime": "off"`.
- **NetTopologySuite's types need `UseNetTopologySuite()` on the data source**, as NodaTime's need `UseNodaTime()`.  The call is of `Npgsql.NetTopologySuite`.  Without it, Npgsql throws `InvalidCastException` when the query runs: "Reading as 'NetTopologySuite.Geometries.Geometry' is not supported for fields having DataTypeName 'public.geometry'", or the same for writing.
- **Entity Framework Core's NetTopologySuite package turns NetTopologySuite on.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings `Npgsql.NetTopologySuite`.  As for NodaTime, `UseNetTopologySuite()` inside EF Core's `UseNpgsql(...)` teaches EF Core's own data source alone.  So do one of these:
  - Call `UseNetTopologySuite()` on the builder of the data source whose connections run the query methods.
  - Turn NetTopologySuite off with the [library types](../../library-types-and-json.md#setting-library-types), `"netTopologySuite": "off"`.
- **`UseNodaTime()` changes what Npgsql gives Dapper too.**  On a data source that calls it, every reader gets NodaTime's types.  That is the plugin's doing.  On such a data source:
  - A `timestamptz` arrives as an `Instant`.  So a query whose output is `models` fails when Dapper reads it into its `System.DateTime` property.
  - A `tstzrange` or a `daterange` arrives as an `Interval` or a `DateInterval`.  That throws for an empty range, whatever the type of the property.
- **Such a `models` query needs NodaTime's types, or a data source without the plugin.**  Give it NodaTime's types with the [type override](../../type-mapping.md#setting-type-overrides), `"timestamptz": "NodaTime.Instant"` in a settings file of its folder.  Or read it on a data source without the plugin.
- **System.Text.Json's types, and a class of yours in a [JSON](libraries.md#json) column, need nothing on the data source.**  This holds on Npgsql 8, 9 and 10: you call no `EnableDynamicJson()`.
- **If a column holds a null and its property cannot be null, the method throws Npgsql's `InvalidCastException`**, "Column 'name' is null.".  It throws before any converter is called.  A JSON `null` in such a column is not a null: what it gives is under [JSON](libraries.md#json).
- **`rowcount` is `-1` for a `SELECT`**, and for a `WITH` whose last statement is a `SELECT`, even when its `WITH` updates rows.  See [what each shape returns](../../return-types.md#method-shapes).
- **When the `commandTimeout` of a `methods` method passes, the method throws Npgsql's `NpgsqlException`.**  Its inner exception is a `TimeoutException`.
- **A synchronous method needs a data source without multiplexing.**  Multiplexing is on when the connection string of the data source has `Multiplexing=true`.  On such a data source:
  - A [synchronous method](../../synchronous-methods.md#synchronous-methods) given a connection that has no transaction open throws `NotSupportedException`, and runs nothing.
  - A synchronous method given a connection in a transaction that the caller has begun on it runs.
  - A synchronous [collection method](../../collection-methods.md#collection-methods) runs in the transaction that it begins of its own.  So it runs on such a data source too, except where it begins no transaction.  It begins none under `transaction=off`, or when a `System.Transactions` transaction is open, as inside a `TransactionScope`.
  - A [synchronous repository method](../../repositories.md#synchronous-repository-methods) runs on a connection with no transaction open, so it throws too.
  - On a borrowed connection in which your code began a transaction, a synchronous repository method runs, as above.
  - The synchronous collection method of the repository runs, as above.
- **Give the code that calls synchronous methods a data source without multiplexing**, which is the default.  Or call the asynchronous method that `method-type=both` gives beside the synchronous one.
- **A cancelled method throws `OperationCanceledException`.**  This holds whether the token was cancelled before the call, while the command runs, or while the rows are read.
- **A loop over [streamed rows](../../return-types.md#streaming-rows) that ends early throws the error of a row that it left unread.**  The loop ends early at a `break`, or at an exception in its body.  Then the rows still to come are read.  PostgreSQL can raise a `PostgresException` for one of them, such as `22012` for a division by zero.  That exception is thrown when the loop ends, in place of the exception of the body.  It also aborts the transaction that the query ran in.

  To keep the exception of the body too, see [streamed rows](../../return-types.md#streaming-rows).
- **A query method with parameters needs a connection whose commands take Npgsql's own parameters.**  The commands of an `NpgsqlConnection` take them.  A connection that wraps Npgsql's and creates parameters of its own fails before the query is sent, as [the limit](../../query-methods.md#limit-wrapped-connection) says.

<a id="collection-methods"></a>

## Collection methods

```sql postgres
-- name: AddEvent -> rowcount
-- generator: collection-method transaction=off
INSERT INTO events (kind, payload, happened_at)
VALUES (@kind, @payload, @happenedAt);
```

A query's [collection method](../../collection-methods.md#collection-methods) runs the query once for each item of a collection, 1,000 items to a round trip.  On PostgreSQL it runs the items through Npgsql.  In the sample, `transaction=off` runs them in no transaction of the method's own.  What a failed item leaves in the database depends on the transaction that the method runs in:

| The transaction | When an item fails |
|----|----|
| Its own, by `transaction=on`, the default, or an isolation level | Every item is rolled back, those of earlier round trips too, and nothing is left |
| None, by `transaction=off` | Each round trip's items run in a transaction that PostgreSQL begins for them alone, and commit or roll back together: the failed item's round trip is rolled back, the items before it included, the round trips before it stay committed, and those after it do not run |
| The caller's, a `transaction` given at the output `methods` or an ambient one | The method leaves it to you.  PostgreSQL aborts a transaction at an error, so it takes no other command until you roll it back, and what ran in it is rolled back with it |

- **The method names the failed item, and you add nothing to the connection string.**  It throws [`CollectionItemException`](../../exceptions.md#collection-item-exception), whose `ItemIndex` is the position of the item.  Its `InnerException` is Npgsql's `PostgresException`.  The `SqlState` of that exception is the code of the error, such as `23505` for a unique violation.
- **An error at a commit belongs to no item.**  PostgreSQL checks a constraint declared `DEFERRABLE INITIALLY DEFERRED` when the transaction commits, after every item has run.  So the method throws its error as Npgsql's `PostgresException`, as it is, and the transaction is rolled back.  Under `transaction=off`, that is the commit of each round trip.
- **PostgreSQL has three isolation levels**: `read-committed`, which is its default, `repeatable-read` and `serializable`.  The levels `read-uncommitted` and `snapshot` are [SQLSRC297](../../../diagnostics.md#sqlsrc297).
- **The method sends the SQL without its comments**, even under [`keep-comments`](../../generator-parameters.md#setting-generator-parameters).  So a comment that you keep for the logs of the database reaches them only from the query method.
- **`commandTimeout` is the timeout of each round trip.**  When it passes, the method's own transaction is rolled back.
- **A cancelled method throws `OperationCanceledException`**, as a query method does.  The method's own transaction is rolled back.

<a id="limits"></a>

## Limits

<a id="limit-connection-string-repository"></a>

### `InvalidCastException` for a NodaTime, PostGIS, enum or composite type in a repository made from a connection string

**What you see.**  A repository method compiles, and throws Npgsql's `InvalidCastException` when it runs, such as "Reading as 'NodaTime.Instant' is not supported".  The method reads or sends a type that needs a plugin or a mapping.  It is a NodaTime type, a PostGIS type, an enum or a composite type.  The connection source of the repository is a connection string.

**Why.**  For each call, the methods of such a repository make a new `NpgsqlConnection` of the string.  That connection has none of the setup of your application's data source:

- no plugin, such as NodaTime's or NetTopologySuite's: not `UseNodaTime()` or `UseNetTopologySuite()`;
- none of your own mappings: not `MapEnum<T>()` or `MapComposite<T>()`.

So the repository reads and sends no type that needs a plugin or a mapping, as [what a method reads and throws](#method-results) says.  A SQL Server repository is not affected: see [what `on` needs](../../library-types-and-json.md#setting-library-types).

**What to do.**  Give the repository a connection source that has the plugins and mappings: see [the connection](../../repository-connections.md#repository-connection).  Npgsql itself recommends a data source over a connection string.  Use one of these:

- the `NpgsqlDataSource` that your application builds, with the plugins and mappings;
- a marked method that returns a connection that it opens from that data source.
