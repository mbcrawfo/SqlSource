# How a C# type is chosen, and type overrides

<a id="property-types"></a>

## Property types

```sql postgres
-- column: users.id -> UserId

-- name: GetUser
-- column: nickname -> Nickname
SELECT id, nickname, created_at, born
FROM users
WHERE id = @id;
```

A property has the C# type of its column or parameter.  Above, in a project that maps `"date": "System.DateOnly"` with the type override and references `Npgsql.NodaTime`, `Id` is a `UserId` by the file header's column type, `Nickname` a `Nickname` by its query's marker, `CreatedAt`, a `timestamptz`, a `NodaTime.Instant` by NodaTime under `methods` or `repository` and a `System.DateTime` under `models`, and `Born`, a `date`, a `System.DateOnly` by the type override.  A column's C# type is the first of these that gives one:

1. its query's `-- column: name -> Type` marker: see [the `-- column:` marker](model-properties.md#column-marker);
2. the [column types](model-properties.md#setting-column-types), by the column of a table or view it is read from;
3. the [type override](../settings.md#setting-type-overrides), by its database type;
4. a library of the [library types](../settings.md#setting-library-types) that is on for the query, NodaTime, NetTopologySuite or System.Text.Json, for a query whose output is `methods` or `repository` alone;
5. the type map of [PostgreSQL](databases/postgresql/types.md#type-map) or [SQL Server](databases/sqlserver/types.md#types).

A parameter's is its [`-- param:` marker](parameters.md#param-marker)'s C# type after `->`, then the type override's, the library's and the type map's.  Each of the first three is a type you chose, which a query method reads and sends through your [converters](../settings.md#converters) when it has them, and a class of yours in a column that holds JSON through System.Text.Json's serializer: see [a class in a JSON column](../settings.md#json-class).  A library's type is read and sent on PostgreSQL by Npgsql, through its plugin for NodaTime or NetTopologySuite, and by the query method itself on SQL Server, unless your converters convert it, and the type map's is read and sent by the driver.  On SQL Server a type of NetTopologySuite is read and sent by the query method whichever place chose it, and no converter of yours replaces that: see [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite).

A PostgreSQL array parameter of a query whose output is `methods` or `repository` has the type `IEnumerable<T>` of its element, `IEnumerable<int>` for an `int4[]`, in place of `T[]`, and the query method sends any sequence: see [array parameters](databases/postgresql/query-methods.md#array-parameters).  A column's property, and a parameter's in a query whose output is `models`, stay `T[]`.

A [list parameter](parameters.md#list-parameters), written after `IN` with no parentheses, has the type `IEnumerable<T>` of its values' C# type under `models`, `methods` and `repository`: `IEnumerable<int>` for a list of `integer`.  Its values' type is found as a parameter's is, and is never nullable.
