# Library types and JSON

<a id="setting-library-types"></a>

## Library types

```jsonc
// sqlsource.json, in the folder of a project that references Npgsql.NodaTime
{
    "root": true,
    "dialect": "postgres",
    "libraryTypes": {
        "nodaTime": "off",
    },
}
```

```jsonc
// Scheduling/sqlsource.json
{
    "libraryTypes": {
        "nodaTime": "default",
    },
}
```

```sql postgres
-- name: ListShifts
-- library-types: nodatime=off
SELECT id, starts_at FROM shifts;
```

The library types say whether the query methods read and send the types of a library, for the database types that the library covers.  A library's types replace the types of the type map.  There are three libraries:

- **NodaTime**, named `nodaTime`.  On PostgreSQL it gives a `timestamptz` the type `NodaTime.Instant`, a `date` a `NodaTime.LocalDate`, and more, as [NodaTime on PostgreSQL](databases/postgresql/libraries.md#nodatime) lists.  On SQL Server it gives a `datetime2` the type `NodaTime.LocalDateTime`, a `date` a `NodaTime.LocalDate`, and more, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) lists.
- **NetTopologySuite**, named `netTopologySuite`.  It gives `NetTopologySuite.Geometries.Geometry` to PostGIS's `geometry` and `geography` on PostgreSQL, as [NetTopologySuite on PostgreSQL](databases/postgresql/libraries.md#nettopologysuite) says.  It gives the same type to `geography` and `geometry` on SQL Server, as [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite) says.
- **System.Text.Json**, named `json`.  It gives the type `System.Text.Json.JsonElement` to PostgreSQL's `json` and `jsonb` and to SQL Server's `json`, as JSON on [PostgreSQL](databases/postgresql/libraries.md#json) and on [SQL Server](databases/sqlserver/libraries.md#json) says.

In the samples:

- The project references `Npgsql.NodaTime`, so NodaTime would be on for all of it.
- The settings file of the project turns NodaTime off.
- The files under `Scheduling/` give it back to its default, on.
- The query `ListShifts` turns it off again.  So its `starts_at`, a `timestamptz`, stays a `System.DateTime`.

**The state** of a library is `on`, `off` or `default`.  The state is matched ignoring case, hyphens and spaces, as the name of the library is.  `default` takes the library out of the entries of every place below, so that its default applies.  In the same way, a type override's `default` gives a type back to the type map.

**The default** applies where no place names the library, or where the nearest place that names it says `default`.

- **On PostgreSQL, NodaTime is `on` when the project references `Npgsql.NodaTime`, and `off` otherwise.**  `Npgsql.NodaTime` is the plugin that lets Npgsql read and send NodaTime's types.  The project can reference it directly, or through another package, such as Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`.
- **On PostgreSQL, a project that references `NodaTime` alone has NodaTime off.**
- **On PostgreSQL, NetTopologySuite follows the same rule with `Npgsql.NetTopologySuite`.**  Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings it.
- **On PostgreSQL, `json` is `off`.**
- **On SQL Server, all three libraries are `off`, whatever the project references.**

**Only for query methods.**  A library's types reach only a query whose [output](settings.md#setting-output) is `repository` or `methods`, for each type that claims the query.

- **A query whose output is `models` keeps the types of the type map in its models**, whatever the library types say.  For such a query, the library types are read and checked, and do nothing else.
- **To give such a query the types of a library, name them with the type override or the column types.**  Rows read through Dapper get their types so.
- **One model can get two sets of types, which is [SQLSRC257](../diagnostics.md#sqlsrc257).**  This happens to a model that a query with a method, with the library on, shares with a `models` query.  It also happens to a model that two queries with methods share, one with the library on and one with it off.  Name the models apart with [`-- output-model:`](models.md#setting-model-name), or turn the library on or off alike for both.

**Below your own choices.**  A library gives a column or parameter its type only where no marker, column type or type override gives one.  See [the order in which a C# type is chosen](type-mapping.md#property-types).  So a project that maps `timestamptz` with the type override keeps its type with NodaTime on.  `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default`, gives `timestamptz` back to the library there.

**Only what you did not choose.**  The library types decide the type of a column or parameter only where nothing else gives it one.  You can choose a type with the type override, a column type or a marker.  Such a type is read and sent the same whether its library is on or not.  For example:

- On SQL Server, `datetime2=NodaTime.Instant` is converted for a project that references NodaTime, with NodaTime off, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) says.
- On SQL Server, `geography=NetTopologySuite.Geometries.Point` is read and sent for a project that references `NetTopologySuite.IO.SqlServerBytes`, with NetTopologySuite off.
- A `JsonDocument`, or a class of yours, that you give a `jsonb` column is read and sent the same with `json` on or off.

**The places.**  The library types can be written in these places:

- `libraryTypes` in the settings files of the folder of a `.sql` file and of the folders above it;
- the `-- library-types:` marker in the file header;
- the `-- library-types:` marker inside a query.

**In a settings file**, `libraryTypes` is a JSON object with a member for each library: `"nodaTime": "on"` or `"netTopologySuite": "off"`.

- **A member whose library or state is not one is [SQLSRC019](../diagnostics.md#sqlsrc019)**, and the other members still apply.
- **Two members for one library, such as `"nodaTime"` and `"NodaTime"`, are [SQLSRC020](../diagnostics.md#sqlsrc020)**, and the first member applies.

**In a marker**, a `-- library-types:` line holds one entry or more, separated by white space.  Each entry is `name=state`, with no space around its `=`: `-- library-types: nodatime=on nettopologysuite=off json=on`.

- **The lines of one place add up**, one for each library, and the same state twice is fine.
- **Two states for one library are [SQLSRC112](../diagnostics.md#sqlsrc112)**, in the file header or inside one query.
- **A line with no entry, or an entry that is not valid, is [SQLSRC111](../diagnostics.md#sqlsrc111).**

**Every place, merged by library.**  For each query, the entries add up in this order: the farthest settings file to the nearest, then the file header, then the query.  An entry for a library replaces the entry of a place below for that library alone, as the entries of the [type override](type-mapping.md#setting-type-overrides) do.  This is one of the exceptions to the rule that the most specific place wins whole: see [which place wins](settings.md#precedence).  One value serves both databases: the dialect of a file decides which database's types apply.

**What `on` needs.**  The project must reference what the library needs on the database of the query.  If it does not, an entry that says `on` for a query whose output is `repository` or `methods` is [SQLSRC281](../diagnostics.md#sqlsrc281), at the entry.  The query keeps the types of the type map.

On PostgreSQL:

- **NodaTime needs the plugin `Npgsql.NodaTime`, and NetTopologySuite needs the plugin `Npgsql.NetTopologySuite`.**
- **A query method that reads NodaTime's types needs `UseNodaTime()` on the data source of the application**: see [PostgreSQL's query methods](databases/postgresql/query-methods.md#method-results).
- **A query method that reads NetTopologySuite's types needs `UseNetTopologySuite()` on the data source**, in the same way.

On SQL Server:

- **NodaTime needs `NodaTime`.**  The query methods convert NodaTime's types themselves, and nothing is registered in the application.
- **NetTopologySuite needs `NetTopologySuite.IO.SqlServerBytes`, and NetTopologySuite 2.5.0 or later referenced by name.**  `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, which is [SQLSRC282](../diagnostics.md#sqlsrc282).
- **A query method that reads NetTopologySuite's types needs nothing registered.**

On both databases:

- **`json` needs System.Text.Json, which every .NET project has.**  `JsonElement` needs nothing registered on either database.
- **A query method that gets NodaTime's types from the library types needs NodaTime 3.1.9 or later.**  So does a query method that converts a NodaTime type of your choice on SQL Server.  An older NodaTime is [SQLSRC282](../diagnostics.md#sqlsrc282), and the query gets no method.
- **A query method that uses NetTopologySuite needs NetTopologySuite 2.5.0 or later.**  On SQL Server it also needs `NetTopologySuite.IO.SqlServerBytes` 2.1.0 or later.  An older one is SQLSRC282 too.

**The tool.**  The `sqlsource` tool does not read `libraryTypes`.  A `-- library-types:` marker changes nothing that is described: adding, changing or removing one leaves the sidecar entry of the query current.

<a id="json-class"></a>

## A class in a JSON column

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "columnTypes": {
        "orders.metadata": "OrderMetadata",
        "orders.tags": "System.Collections.Generic.List<Tag>",
    },
}
```

```sql postgres
-- name: SetMetadata
-- param: @metadata -> OrderMetadata
UPDATE orders SET metadata = @metadata WHERE id = @id;
```

```csharp
namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority);

public sealed record Tag(string Name);
```

You can give a class of your project to a column or parameter of a type that holds JSON.  A type override, a column type or a marker gives it.  The query methods then read and send it through System.Text.Json's serializer.  You write no converter, and register nothing on the data source.

In the samples, `metadata` and `tags` are `jsonb` columns:

- Each query method that reads `metadata` gives its text to `JsonSerializer.Deserialize<OrderMetadata>`.
- `SetMetadata` sends `@metadata` as the text of `JsonSerializer.Serialize`, `{"Channel":"web","Priority":1}`, as a `jsonb`.
- A marker maps the parameter `@metadata`, since no column type reaches a parameter.

The rules:

- **A class is a type that is not one of System.Text.Json, and not one that the driver reads for some database type.**  The types of [PostgreSQL](databases/postgresql/types.md#type-table) and [SQL Server](databases/sqlserver/types.md#type-table) list the types that the driver reads.  A class can be a class or a record of yours, a struct, or a collection, such as `List<Tag>` or `Tag[]`, which is serialized whole.
- **A `JsonElement`, a `string` or a `long` given to a JSON column is not a class**, and is read as before.
- **A type that holds JSON is one of these, whether the `json` of the [library types](#setting-library-types) is on or off:**
  - PostgreSQL's `json` and `jsonb`, and a domain over one of them;
  - SQL Server's `json`, and an alias type over it.
- **A column or parameter of another string type holds JSON only by [the JSON opt-in](#json-opt-in).**  A string type is one that the type map reads as a `string`, such as `text` or `nvarchar(max)`.
- **Without the opt-in, a class given to such a column keeps the rules of [converters](converters.md#converters).**  Its converters are used.  Without them, the driver is given the class, and throws `InvalidCastException`.  So an `Email` of yours over an `nvarchar` is never written as JSON.
- **An array of PostgreSQL's JSON type is read and sent element by element**, such as a `jsonb[]` given an `OrderMetadata[]`.  A `NULL` element stays null for a class.  It throws `InvalidCastException` for a struct written without `?`, as for an array that a [converter](converters.md#converters) converts.
- **The serializer uses System.Text.Json's own default options.**  Property names are as your class writes them, `Channel`, and are matched with their case.
- **So JSON whose names have another case reads as a class whose properties have their default values, with no error.**  An example is `{"channel":"web"}`, written in camelCase by other code, read as an `OrderMetadata`.  For other options, such as camelCase names, write a converter: see [the recipe](../guides/json.md#class-in-a-json-column).
- **A JSON `null`, the text `null`, is not a SQL `NULL`.**
- **A JSON `null` read into a property of a class that cannot be null throws `InvalidCastException`**, "Column 'metadata' holds a JSON null.".  In a property that can be null, it is null.
- **An element of an array follows the same rule.**  In an array whose elements cannot be null it throws the same exception, and in an `OrderMetadata?[]` it is null.
- **A struct of yours follows the same rule.**  A JSON `null` is null in a `Point?` property or a `Point?[]` element.  It throws the same `InvalidCastException` in a `Point` property or a `Point[]` element.
- **A SQL `NULL` never reaches the serializer.**  It gives its property null, or throws as any column does: see [how a row is read](query-methods.md#reading-rows).
- **Text that is not the JSON of your class throws System.Text.Json's `JsonException` when it is read**, such as an array where an object is expected.
- **Your converters replace the serializer.**  A [converter](converters.md#converters) of yours between `string` and the class is used in place of the serializer, in its direction.  So you choose the options: see [the recipe](../guides/json.md#class-in-a-json-column).
- **A SQL `NULL` never reaches such a converter, but a JSON `null` does**, as the text `null`.  What the converter gives for it is the converter's choice.
- **A class with a converter of one direction alone is [SQLSRC280](../diagnostics.md#sqlsrc280)** for a query that needs the other direction.
- **A class whose converters are all of other types, such as `long`, is [SQLSRC279](../diagnostics.md#sqlsrc279)**, and is not serialized.
- **In a trimmed or AOT project, the compiler warns IL2026 and IL3050** at the calls of the serializer in the generated methods.  The serializer reads and writes your class by reflection.
- **An application whose publish turns System.Text.Json's reflection off throws `InvalidOperationException` when the method runs.**  A trimmed or AOT application turns it off by default.  Write converters that use a `JsonSerializerContext` that System.Text.Json's source generator writes: see [the recipe](../guides/json.md#class-in-a-json-column).
- **Only query methods use the serializer.**  A query whose output is `models` has no method, and its models keep the class as the type of their property.  To read them through Dapper, you need a type handler of yours, as for any type of yours.

<a id="json-opt-in"></a>

## The JSON opt-in

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "mssql",
    "columnTypes": {
        "orders.metadata": { "type": "OrderMetadata", "json": true },
        "orders.created_by": "UserId",
    },
}
```

```sql mssql
-- column: orders.tags -> json List<Tag>

-- name: SetMetadata
-- param: @metadata -> json OrderMetadata
UPDATE dbo.orders SET metadata = @metadata WHERE id = @id;

-- name: GetPreferences -> one
-- column: preferences -> json CustomerPreferences
SELECT id, preferences FROM dbo.customers WHERE id = @id;
```

The JSON opt-in says that a column or a parameter of a string type holds JSON.  The query methods then read and send the class of yours that it is given through System.Text.Json's serializer.  A class in a `json` or `jsonb` column is read and sent the same way: see [a class in a JSON column](#json-class).

In the samples, `orders.metadata`, `orders.tags` and `customers.preferences` are `nvarchar(max)` columns that hold JSON.  A database before SQL Server 2025 keeps JSON so, since it has no `json` type.

- `orders.metadata` is an `OrderMetadata` in every query of the project.
- `orders.tags` is a `List<Tag>` in every query of the file.
- `@metadata` is an `OrderMetadata`.
- The column `preferences` of `GetPreferences` is a `CustomerPreferences`.
- Each of these is read and sent as JSON.
- `orders.created_by` is a `UserId`, which its [converters](converters.md#converters) read and send.

The rules:

- **In a settings file, the value of a column type can be an object.**  `type` is its C# type.  `json`, `true` or `false`, says whether the column holds JSON.  `false`, or no `json`, is the plain mapping, as the string `"OrderMetadata"` is.
- **In a marker, the word `json` and white space come after `->`, before the C# type.**  This holds in the `-- column:` of the file header, in the `-- column:` of a query and in a `-- param:`.  The word is matched ignoring case.
- **`-> json` alone gives the column a type named `json`.**
- **The opt-in is not in the type override**, which maps a database type and not a column that holds JSON.  A domain over `text` that holds JSON is mapped by its columns, or by the markers of each query.
- **The opt-in applies to a column or parameter that the type map reads as a `string`.**  Examples are PostgreSQL's `text` and `varchar`, and SQL Server's `nvarchar(max)`.  Such a column is then read and sent as a `json` column of the same class is.  A parameter is still sent as its own database type.
- **On a `json` or `jsonb` column, the opt-in is allowed and changes nothing.**
- **Its C# type is a class of yours**, as [a class in a JSON column](#json-class) says.  An array or a collection of one is serialized whole.
- **In a query whose output is `repository` or `methods`, the opt-in on another database type is [SQLSRC283](../diagnostics.md#sqlsrc283).**  Examples are a column or parameter of `int` or `text[]`.  So is the opt-in with a type of System.Text.Json, or with a type that the driver reads, such as `JsonElement` or `string`.  The query then gets no method.
- **A query whose output is `models` takes the type of the opt-in for its property**, and reads nothing more of it.
- **A [converter](converters.md#converters) of yours between `string` and the class** replaces the serializer here too.
- **Two places that differ only in `json` are [SQLSRC112](../diagnostics.md#sqlsrc112).**  They are one of these pairs:
  - two `-- param:` markers of one parameter;
  - two `-- column:` markers of one column in a query;
  - two `-- column:` lines of one key in the file header.
- **`-> json default`, and an object whose `json` is `true` and whose type is `default`, are not valid.**  In a marker this is [SQLSRC111](../diagnostics.md#sqlsrc111), and in a settings file it is [SQLSRC019](../diagnostics.md#sqlsrc019).
- **SQLSRC019 also reports an object with a member other than `type` and `json`.**
- **Nothing is described again.**  Adding or removing the opt-in, at any place, leaves the sidecar entry of the query current.  So it needs no `dotnet sqlsource describe`.
