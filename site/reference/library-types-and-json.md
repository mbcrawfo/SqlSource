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

The library types say whether the query methods read and send a library's types, in place of the types of the type map, for the database types the library covers.  There are three libraries.  NodaTime, named `nodaTime`, gives on PostgreSQL a `timestamptz` the type `NodaTime.Instant`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on PostgreSQL](databases/postgresql/libraries.md#nodatime) lists, and on SQL Server a `datetime2` the type `NodaTime.LocalDateTime`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) lists.  NetTopologySuite, named `netTopologySuite`, gives `NetTopologySuite.Geometries.Geometry` to PostGIS's `geometry` and `geography` on PostgreSQL, as [NetTopologySuite on PostgreSQL](databases/postgresql/libraries.md#nettopologysuite) says, and to `geography` and `geometry` on SQL Server, as [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite) says.  System.Text.Json, named `json`, gives PostgreSQL's `json` and `jsonb` and SQL Server's `json` the type `System.Text.Json.JsonElement`, as JSON on [PostgreSQL](databases/postgresql/libraries.md#json) and on [SQL Server](databases/sqlserver/libraries.md#json) says.  Above, the project references `Npgsql.NodaTime`, so NodaTime would be on for all of it; the project's settings file turns it off, the files under `Scheduling/` give it back to its default, on, and the query `ListShifts` turns it off again, so its `starts_at`, a `timestamptz`, stays a `System.DateTime`.

**The state** of a library is `on`, `off` or `default`, matched ignoring case, hyphens and spaces, as the library's name is.  `default` takes the library out of the entries of every place below, so that its default applies, as a type override's `default` gives a type back to the type map.

**The default**, where no place names the library or the nearest that does says `default`:

- **PostgreSQL**: for NodaTime, `on` when the project references `Npgsql.NodaTime`, the plugin that lets Npgsql read and send NodaTime's types, directly or through another package such as Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`, and `off` otherwise.  A project that references `NodaTime` alone has it off.  For NetTopologySuite the same with `Npgsql.NetTopologySuite`, which Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings.  `json` is `off`.
- **SQL Server**: `off` for all three, whatever the project references.

**Every place, merged by library.**  The library types are `libraryTypes` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- library-types:` marker in the file header and inside a query.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a library replaces the entry of a place below for that library alone, as the [type override](type-mapping.md#setting-type-overrides)'s entries do.  One value serves both databases: a file's dialect decides which database's types apply.

**Only for query methods.**  A library's types reach only a query whose [output](settings.md#setting-output) is `methods` or `repository`, for each type that claims it.  A query whose output is `models` keeps the type map's types in its models, whatever the library types say: there they are read and checked, and do nothing else.  To give such a query a library's types, as for rows read through Dapper, name them with the type override or the column types.  A model that a query with a method, with the library on, and a `models` query share, or two queries with methods, one with the library on and one with it off, has two sets of types and is [SQLSRC257](../diagnostics.md#sqlsrc257): name the models apart with [`-- output-model:`](models.md#setting-model-name), or turn the library on or off alike for both.

**Below your own choices.**  A column's or parameter's C# type is its query's `-- column:` marker's or its `-- param:` marker's, then the [column types](model-properties.md#setting-column-types)', then the [type override](type-mapping.md#setting-type-overrides)'s, then the library's, then the type map's: see [property types](type-mapping.md#property-types).  So a project that maps `timestamptz` with the type override keeps its type with NodaTime on, and `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default`, gives `timestamptz` back to the library there.

**What `on` needs.**  The project references what the library needs on the database of the query: on PostgreSQL the plugin, `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, and on SQL Server `NodaTime`, or for NetTopologySuite `NetTopologySuite.IO.SqlServerBytes` with NetTopologySuite 2.5.0 or later referenced by name, since `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, which is [SQLSRC282](../diagnostics.md#sqlsrc282); for `json`, System.Text.Json, which every .NET project has.  An entry that says `on` for a `methods` or `repository` query of a project without it is [SQLSRC281](../diagnostics.md#sqlsrc281), at the entry, and the query keeps the type map's types.  A query method that reads NodaTime's types on PostgreSQL needs `UseNodaTime()` on the application's data source: see [PostgreSQL's query methods](databases/postgresql/query-methods.md#method-results).  On SQL Server the query methods convert NodaTime's types themselves, and nothing is registered in the application.  `JsonElement` needs nothing registered on either database.  A query method that gets NodaTime's types from the library types, or on SQL Server converts a NodaTime type of your choice, needs NodaTime 3.1.9 or later: an older one is [SQLSRC282](../diagnostics.md#sqlsrc282), and the query gets no method.  One that reads NetTopologySuite's types on PostgreSQL needs `UseNetTopologySuite()` on the data source in the same way, and on SQL Server nothing.  One that uses NetTopologySuite needs NetTopologySuite 2.5.0 or later, and on SQL Server also `NetTopologySuite.IO.SqlServerBytes` 2.1.0 or later: an older one is SQLSRC282 too.

**Only what you did not choose.**  The library types decide the type of a column or parameter that nothing else gives one.  A type you choose with the type override, a column type or a marker is read and sent the same whether its library is on or not: on SQL Server, `datetime2=NodaTime.Instant` is converted for a project that references NodaTime with NodaTime off, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) says, `geography=NetTopologySuite.Geometries.Point` is read and sent for one that references `NetTopologySuite.IO.SqlServerBytes` with NetTopologySuite off, and a `JsonDocument` or a class of yours that you give a `jsonb` column is read and sent the same with `json` on or off.

**In a settings file**, `libraryTypes` is a JSON object with a member for each library, `"nodaTime": "on"` or `"netTopologySuite": "off"`.  A member whose library or state is not one is [SQLSRC019](../diagnostics.md#sqlsrc019), and the other members still apply.  Two members for one library, `"nodaTime"` and `"NodaTime"`, are [SQLSRC020](../diagnostics.md#sqlsrc020), and the first applies.

**In a marker**, a `-- library-types:` line holds one entry or more, each `name=state` with no space around its `=`, separated by white space: `-- library-types: nodatime=on nettopologysuite=off json=on`.  The lines of one place add up, one for each library, and the same state twice is fine.  Two states for one library in the file header, or inside one query, are [SQLSRC112](../diagnostics.md#sqlsrc112).  A line with no entry, or an entry that is not valid, is [SQLSRC111](../diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read `libraryTypes`, and a `-- library-types:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.

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

A column or parameter of a type that holds JSON, given a class of your project by a type override, a column type or a marker, is read and sent by the query methods through System.Text.Json's serializer, with no converter to write and nothing to register on the data source.  Above, `metadata` and `tags` are `jsonb` columns: each query method that reads `metadata` gives its text to `JsonSerializer.Deserialize<OrderMetadata>`, and `SetMetadata` sends `@metadata`, which a parameter's marker maps since no column type reaches a parameter, as the text of `JsonSerializer.Serialize`, `{"Channel":"web","Priority":1}`, as a `jsonb`.

- **A class** is a type that is not one of System.Text.Json and not one that the driver reads for some database type, as the types of [PostgreSQL](databases/postgresql/types.md#type-table) and [SQL Server](databases/sqlserver/types.md#type-table) list them: a class or a record of yours, a struct, or a collection, `List<Tag>` or `Tag[]`, which is serialized whole.  So a `JsonElement`, a `string` or a `long` given to a JSON column is read as before.
- **A type that holds JSON** is PostgreSQL's `json` and `jsonb`, a domain over one, and SQL Server's `json` and an alias type over it, with the [library types](#setting-library-types)' `json` on or off.  A column or parameter of another type that the type map reads as a `string`, such as `text` or `nvarchar(max)`, holds JSON only by [the JSON opt-in](#json-opt-in).  Without it, a class given to such a column keeps the rules of [converters](converters.md#converters): its converters are used, and without them the driver is given the class, which throws `InvalidCastException`.  So an `Email` of yours over an `nvarchar` is never written as JSON.
- **An array of PostgreSQL's JSON type**, `jsonb[]` given an `OrderMetadata[]`, is read and sent element by element.  A `NULL` element stays null for a class, and throws `InvalidCastException` for a struct written without `?`, as for an array that a [converter](converters.md#converters) converts.
- **The default options** are System.Text.Json's own: property names as your class writes them, `Channel`, and matched with their case.  So JSON whose names are another case, `{"channel":"web"}`, written by other code in camelCase, reads as an `OrderMetadata` whose properties have their default values, with no error.  For other options, such as camelCase names, write a converter: see [the recipe](../recipes.md#class-in-a-json-column).
- **A JSON `null`**, the text `null`, is not a SQL `NULL`.  Read into a property of a class that cannot be null, it throws `InvalidCastException`, "Column 'metadata' holds a JSON null.", and into one that can be null it is null.  An element of an array that cannot be null throws the same, and one of `OrderMetadata?[]` is null.  A struct of yours follows the same rule: a JSON `null` is null in a `Point?` property or a `Point?[]` element, and throws the same `InvalidCastException` in a `Point` property or a `Point[]` element.  A SQL `NULL` never reaches the serializer, and gives its property null, or throws as any column does: see [how a row is read](query-methods.md#reading-rows).
- **Text that is not your class's JSON**, such as an array for an object, throws System.Text.Json's `JsonException` when it is read.
- **Your converters replace the serializer.**  A [converter](converters.md#converters) of yours between `string` and the class is used in place of the serializer, in its direction, so that you choose the options: see [the recipe](../recipes.md#class-in-a-json-column).  A SQL `NULL` never reaches it, but a JSON `null` does, as the text `null`, and what it gives for that is the converter's choice.  A class with a converter of one direction alone is [SQLSRC280](../diagnostics.md#sqlsrc280) for a query that needs the other, and one whose converters are all of other types, such as `long`, is [SQLSRC279](../diagnostics.md#sqlsrc279), and is not serialized.
- **In a trimmed or AOT project** the serializer reads and writes your class by reflection, so the compiler warns, IL2026 and IL3050, at its calls in the generated methods, and an application whose publish turns System.Text.Json's reflection off, as a trimmed or AOT one does by default, throws `InvalidOperationException` when the method runs.  Write converters that use a `JsonSerializerContext` that System.Text.Json's source generator writes: see [the recipe](../recipes.md#class-in-a-json-column).
- **Only query methods use the serializer.**  A query whose output is `models` has no method, and its models keep the class as their property's type: read through Dapper, it needs a type handler of yours, as any type of yours does.

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

The JSON opt-in says that a column or a parameter of a string type holds JSON, so that the class of yours it is given is read and sent through System.Text.Json's serializer, as a class in a `json` or `jsonb` column is: see [a class in a JSON column](#json-class).  Above, `orders.metadata`, `orders.tags` and `customers.preferences` are `nvarchar(max)` columns that hold JSON, as a database before SQL Server 2025, which has no `json` type, keeps it.  `orders.metadata` is an `OrderMetadata` in every query of the project, `orders.tags` a `List<Tag>` in every query of the file, `@metadata` an `OrderMetadata` and the column `preferences` of `GetPreferences` a `CustomerPreferences`, each read and sent as JSON, while `orders.created_by` is a `UserId`, read and sent by its [converters](converters.md#converters).

- **Where it is written.**  In a settings file, a column type's value is an object: `type` is its C# type, and `json`, `true` or `false`, says whether the column holds JSON.  `false`, or no `json`, is the plain mapping, as the string `"OrderMetadata"` is.  In a marker, the word `json` and white space come before the C# type, after `->`: in the file header's `-- column:`, in a query's `-- column:` and in a `-- param:`.  The word is matched ignoring case.  `-> json` alone gives the column a type named `json`.
- **It is not in the type override**, which maps a database type and not a column that holds JSON: a domain over `text` that holds JSON is mapped by its columns, or by each query's markers.
- **It applies to a column or parameter that the type map reads as a `string`**, such as PostgreSQL's `text` and `varchar` and SQL Server's `nvarchar(max)`, which is then read and sent as a `json` column of the same class is, a parameter being sent as its own database type.  On a `json` or `jsonb` column it is allowed and changes nothing.
- **Its C# type is a class of yours**, as [a class in a JSON column](#json-class) says, and an array or a collection of one is serialized whole.  The opt-in on a column or parameter of another database type, such as `int` or `text[]`, or with a type of System.Text.Json or one that the driver reads, such as `JsonElement` or `string`, is [SQLSRC283](../diagnostics.md#sqlsrc283) at a query whose output is `methods` or `repository`, and the query gets no method.  A query whose output is `models` takes the opt-in's type for its property and reads no more of it.
- **A [converter](converters.md#converters) of yours between `string` and the class** replaces the serializer here too.
- **Two places that differ only in `json`**, two `-- param:` markers of one parameter, two `-- column:` markers of one column in a query or two `-- column:` lines of one key in the file header, are [SQLSRC112](../diagnostics.md#sqlsrc112).  `-> json default`, and an object whose `json` is `true` and whose type is `default`, are not valid: [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker, and [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file, which also reports an object with another member than `type` and `json`.
- **Nothing is described again.**  Adding or removing the opt-in, at any place, leaves the query's sidecar current, so it needs no `dotnet sqlsource describe`.
