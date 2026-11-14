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
3. the [type override](#setting-type-overrides), by its database type;
4. a library of the [library types](library-types-and-json.md#setting-library-types) that is on for the query, NodaTime, NetTopologySuite or System.Text.Json, for a query whose output is `methods` or `repository` alone;
5. the type map of [PostgreSQL](databases/postgresql/types.md#type-map) or [SQL Server](databases/sqlserver/types.md#types).

A parameter's is its [`-- param:` marker](parameters.md#param-marker)'s C# type after `->`, then the type override's, the library's and the type map's.  Each of the first three is a type you chose, which a query method reads and sends through your [converters](converters.md#converters) when it has them, and a class of yours in a column that holds JSON through System.Text.Json's serializer: see [a class in a JSON column](library-types-and-json.md#json-class).  A library's type is read and sent on PostgreSQL by Npgsql, through its plugin for NodaTime or NetTopologySuite, and by the query method itself on SQL Server, unless your converters convert it, and the type map's is read and sent by the driver.  On SQL Server a type of NetTopologySuite is read and sent by the query method whichever place chose it, and no converter of yours replaces that: see [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite).

A PostgreSQL array parameter of a query whose output is `methods` or `repository` has the type `IEnumerable<T>` of its element, `IEnumerable<int>` for an `int4[]`, in place of `T[]`, and the query method sends any sequence: see [array parameters](databases/postgresql/query-methods.md#array-parameters).  A column's property, and a parameter's in a query whose output is `models`, stay `T[]`.

A [list parameter](parameters.md#list-parameters), written after `IN` with no parentheses, has the type `IEnumerable<T>` of its values' C# type under `models`, `methods` and `repository`: `IEnumerable<int>` for a list of `integer`.  Its values' type is found as a parameter's is, and is never nullable.


<a id="setting-type-overrides"></a>

## Type overrides

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "typeOverrides": {
        "timestamptz": "System.DateTimeOffset",
        "public.user_status": "MyApp.UserStatus",
        "int4[]": "System.Collections.Generic.List<int?>",
    },
}
```

```jsonc
// Legacy/sqlsource.json
{
    "typeOverrides": {
        "date": "System.DateTime",
    },
}
```

```sql postgres
-- type-override: numeric = double

-- name: ListEvents
-- type-override: timestamptz = default
SELECT id, happened_at, amount FROM events;
```

A type override gives a database type the C# type of your choice, in place of the one that the type map of [PostgreSQL](databases/postgresql/types.md#type-map) or [SQL Server](databases/sqlserver/types.md#types) gives it: a `timestamp with time zone` as a `System.DateTimeOffset`, a `numeric` as a `double`, an enum of your schema as a C# enum.  Each entry is a key and a type, `"key": "type"` in a settings file and `key=type` in a marker: the key names the database type, and the type is the C# type that every column and parameter of that database type gets.  The type `default` gives the database type back to the type map, or to a library of the [library types](library-types-and-json.md#setting-library-types) that is on for a query method.

In the example, every query of the project gets `timestamptz` as a `System.DateTimeOffset`, `public.user_status` as `MyApp.UserStatus`, and `int4[]` as a list.  The files under `Legacy/` get `date` as a `System.DateTime` too.  Every query of the `.sql` file gets `numeric` as a `double`, and `ListEvents` gets `timestamptz` from the type map, a `System.DateTime`, while it keeps the project's other entries.

To give one column, or one parameter, a C# type of its own, where two columns of one database type need two C# types, see [column types](model-properties.md#setting-column-types) and [`-- param:`](parameters.md#param-marker).  Either wins over the type override.

For a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](../diagnostics.md#sqlsrc232).

Each property of a generated [model](models.md#models) has the C# type of its column or parameter, the type override's included.  A [query method](query-methods.md#query-methods) reads and sends a column or parameter as that type, through your [converters](converters.md#converters) when the type has them.

**Every place, merged by type.**  The type override is `typeOverrides` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- type-override:` marker in the file header and inside a query.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a type replaces the entry of a place below for that type alone.  A settings file's entries reach only the `.sql` files of its folder and the folders below it.  An entry replaces the entry below for its key however that key is spelled, and its own spelling is the one kept: a query's `public."Status"=MyApp.Status` over `"public.Status": "MyApp.Status"` in the project's settings file leaves a type `status`, which the project's entry mapped too, to the type map, since the quoted key maps `Status` alone.  So a query that needs one type otherwise names that type, and keeps the rest of the project's entries.  This is one of the exceptions to the rule that the most specific place wins whole: see [which place wins](settings.md#precedence).  `default` takes the type out of the entries of every place below, and a key with no entry anywhere has the type map's C# type, or a library's in a query method.  Nothing removes every entry at once.

**The key** is a database type:

- **A type of PostgreSQL's catalog is its one-word name**, as [the type table](databases/postgresql/types.md#type-table) gives it: `timestamptz`, `int4`, `float8`, `bpchar`.  The one-word spellings `integer`, `int`, `smallint`, `bigint`, `real`, `float`, `boolean`, `decimal` and `character` are read as the names they stand for: `float` is `float8`, and `character` is `bpchar`.  A spelling of several words, `timestamp with time zone` or `double precision`, is not a key: write the one-word name, which every built-in type has.  The name may be in double quotes when it is written exactly, in lower case: `"int4"` is `int4`, while `"Int4"` and `"integer"` are not keys.  `char` is the one-byte type `"char"`, and so is the key `"char"`; a `char(n)` column is a `bpchar`.
- **Any other type is written with its schema**, `public.user_status`, as the tool records it in the sidecar, and with the quotes its schema or name was created with: `public."Status"`, `"my schema"."order-status"`.  A key with a schema is not checked against the types SqlSource knows, so a type it does not know can be named.  `pg_catalog.int4` is `int4`.
- **The types of extensions** that the type table names, `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube`, are matched by their name alone in any schema.  `public.citext` matches the `citext` of `public` alone, and wins over `citext` there.
- **An array** is its element's key and `[]`, `int4[]`.  A parameter of a query method whose C# type is an array of one rank, from the type table, a library or an entry here, is an `IEnumerable<T>` of the element, and one that an entry maps to another type, such as a `List<int>`, is that type: see [array parameters](databases/postgresql/query-methods.md#array-parameters).  The catalog's name of an array is read as the same key: `_int4` for a type that SqlSource knows, and with its schema for any type of `pg_catalog`, so `pg_catalog._pg_class` is `pg_class[]`.  `_int4` and `int4[]` are one type, and two entries for them are two entries for one type.  Write an array of a type outside `pg_catalog` with its schema, its element's name and `[]`, `public.user_status[]`, and only so.  An entry for the database's name of that array, `public._user_status`, maps nothing, and is [SQLSRC261](../diagnostics.md#sqlsrc261) at a query that uses the array.  Such a key still maps a type whose own name starts with `_` and that is no array.  An entry for the array, `int4[]`, wins over one for its element, `int4`.  Without one, an array gets its element's C# type with `[]`, so `int4=long` makes an `int4[]` column a `long[]`.
- **A domain** has a key of its own, `public.email`, which wins over its base type's.  Without one, a domain gets its base type's C# type, so an entry for `text` maps every domain over `text` too.
- **A range and a multirange** have only their own key.  An entry for `int4` does not change an `int4range`.
- A key's length, precision and scale are not part of it: `numeric` matches `numeric(18,2)`, and `nvarchar` matches `nvarchar(max)`.
- **A SQL Server type is its name**, as [its type table](databases/sqlserver/types.md#type-table) gives it: `int`, `nvarchar`, `datetime2`, `uniqueidentifier`.  SQL Server's other one-word names `integer`, `character`, `dec` and `rowversion` are read as `int`, `char`, `decimal` and `timestamp`.  A name of several words, `double precision`, is not a key.  `sys.` before a name is SQL Server's system schema, as `pg_catalog.` is PostgreSQL's: `sys.int` is SQL Server's `int` alone, and `pg_catalog.int4`, or `int4`, PostgreSQL's alone.
- **An alias type of SQL Server**, made with `CREATE TYPE dbo.Email FROM nvarchar(320)`, is written with its schema, `dbo.Email`, each part plain or in double quotes, as a PostgreSQL type outside `pg_catalog` is.  Its own key wins over its base type's.  Without one, it gets its base type's C# type, so an entry for `nvarchar` maps it too.
- **A name that both databases have is one key, and maps each database's own type of that name.**  A settings file's `"int": "long"` maps PostgreSQL's `int4` and SQL Server's `int`, and `"date": "System.DateOnly"` the `date` of both.  Some such names are different types: `bit` is a bit string on PostgreSQL and a `bool` on SQL Server, `timestamp` a time on PostgreSQL and a row version on SQL Server, and `char` PostgreSQL's one-byte type and SQL Server's string.  In a project with files of both, an entry that is meant for one database's type is written with that database's system schema, `pg_catalog.timestamp` or `sys.timestamp`, or set in a settings file of the folder that holds that database's files, as [configure one folder differently](../guides/folders-and-files.md#configure-a-folder) shows.  A marker is read by its file's dialect alone, so it never reaches the other database's type.
- **A part of a key, its schema or its name, is plain or in double quotes.**  A plain part holds letters, digits, `_` and `$`.  A part in quotes holds any character, with a quote inside it doubled, as SQL writes a name: `"my schema"."order-status"`, `public."say ""hi"""`.  A `.` stands between the schema and the name, and `[]` after an array's.  Put a name in quotes when it has a capital letter, or any character that a plain part does not hold, white space included.  A part in quotes may be written with escapes, as PostgreSQL writes a name: `"my schema".U&"Order\000AItem"` names a type whose name is `Order`, a line break and `Item`.  Inside `U&"..."`, `\` and four hex digits, or `\+` and six, is the character of that code, from 1 to 10FFFF, and a character beyond FFFF is one `\+` escape or the escapes of its two surrogates; `\\` is a backslash, and a quote is doubled.  It is the same part as the quotes of its text, so `public.U&"st\0061tus"` is `public."status"`.  `UESCAPE` is not read, and an escape that is not one of these makes the key not valid.
- **A part in quotes is compared exactly, case included, and a plain part ignoring case.**  So `public."Status"` maps the type `Status` alone, while `public.Status` maps `Status` and `status` alike.  `public.Status` and `public.status` are two keys.  `public.status` and `public."status"` are one key, and so are `public.Status` and `public."Status"`: two entries for them are two entries for one type.
- **Of the entries for one type whose keys differ only by case, the most specific place's wins, as for any type.**  Among the entries of one place, the one whose key is spelled exactly as the type's name wins over one that matches it only ignoring case, so in one place `public.Status` and `public.status` each map their own type.  A plain key at a higher place replaces the entries below for every type it matches: a query's `public.status=default` takes both `Status` and `status` out of the project's entries.  To change one of two types whose names differ only by case, quote it: a query's `public."status"=default` takes `status` alone out, and `Status` keeps the project's entry.

**The type** is a C# type that a property can have:

- **A keyword**, `int`, `string`, `double` and the others of the built-in types; **a full name from its namespace**, `System.DateTimeOffset` or `MyApp.Ids.UserKey`, which may start with `global::`; or **a short name**, without its namespace, `DateTimeOffset` or `UserKey`.  A nested type is written with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.
- **A short name is the one type of that name** and number of type arguments, in any namespace, that your project and its references have and your project can see: a type of a reference counts when it is `public`, or `internal` in a reference that gives your project its internals with `InternalsVisibleTo`.  A type nested in another, and a `file` type, are not found by one.  Your project's own type wins over a reference's of the same full name.  The generated code writes the type's full name, so the `using` lines of your files do not matter, and a short name and the full name of its type are one type, to [shared models](models.md#shared-models) and to converters.  A type argument may be a short name too, in any name: `System.Collections.Generic.List<UserKey>`, or `List<UserKey>`.  `global::UserKey` is a type of the global namespace alone.  A short name that finds a type of an assembly your project has only through a package it does not reference itself is the warning [SQLSRC284](../diagnostics.md#sqlsrc284), since it may not be the type you meant; write the full name to use it without the warning.
- **A generic type** names its type arguments, each of which may end in `?`: `System.Collections.Generic.List<int?>`.  White space inside `<>` and `[]` is allowed.
- **An array** is the element and its ranks: `long[]`, `int?[]`, `int[,]`.
- **No `?` at the end of the whole type.**  Whether a property can be null is decided by its column, by which columns can be null on [PostgreSQL](databases/postgresql/nullability.md#nullability) and on [SQL Server](databases/sqlserver/nullability.md#nullability), or by its parameter's [`-- param:` marker](parameters.md#param-marker), so `int4=int?` is not valid.  A tuple, `dynamic`, `void`, a pointer, and anything after the type are not valid either.
- **The type must exist and be usable.**  A type that the project and its references do not have, a full name that two references have, or a short name of two types in two namespaces, is [SQLSRC250](../diagnostics.md#sqlsrc250), which then names them; one the project cannot see is [SQLSRC251](../diagnostics.md#sqlsrc251); and one that no property can have, such as a `static` class, `System.Nullable<int>`, a type marked `[Obsolete]` as an error or a generic type whose type argument breaks a constraint, as `System.Collections.Generic.List<System.Nullable<string>>`, is [SQLSRC252](../diagnostics.md#sqlsrc252).  Each is reported at the name of a query whose output is `models`, `methods` or `repository` and whose entry in its sidecar is current, once for the query: when the first of its columns and parameters that has no C# type, parameters before columns, is one whose type, or whose array's element or domain's base, has the entry's key.  An entry that no query uses is never an error, so a project can set its entries before it has the queries.

**In a settings file**, `typeOverrides` is a JSON object with a member for each entry: its key is the database type and its value the C# type, `"timestamptz": "System.DateTimeOffset"`.  The white space around the key and around the type is ignored.  White space inside them is read as it is written, so a space inside `<>` or `[]`, `"System.Collections.Generic.Dictionary<string, int>"`, or inside the double quotes of a key, `"my schema"`, is part of the entry.  The key is a JSON string, so each `"` of it is written `\"` and each `\` is written `\\`, as JSON requires: `"\"my schema\".\"order status\""`, and `"\"my schema\".U&\"Order\\000AItem\""` for a part with escapes.  Any other character, a `&` among them, is written as it is, and a tab or a line break inside the quotes of a key may be written `\t` or `\n`, as JSON writes them.  An error that quotes a key that holds such a character writes the character as an escape: `"my schema".U&"Order\000AItem"`.  An object serves PostgreSQL and SQL Server alike: a member whose key names a type of either is valid, and maps the type of each database that has that name.  Two members of one object whose keys are one type's, `"integer"` and `"int4"`, or `"timestamptz"` and `"TIMESTAMPTZ"`, are [SQLSRC020](../diagnostics.md#sqlsrc020), and the first applies.  A member that is not valid is [SQLSRC019](../diagnostics.md#sqlsrc019), and the other members still apply.

**In a marker**, a `-- type-override:` line holds one entry, with or without spaces around its `=`.  The lines of one place add up, one for each type.  Two lines that give one type two C# types in the file header, or inside one query, are [SQLSRC112](../diagnostics.md#sqlsrc112); the types are compared by how they are named, so `int` and `System.Int32` are two, and so are `UserKey` and `MyApp.UserKey`, while `System.Int32` and `global::System.Int32` are one.  Its key is read by the file's dialect: a SQL Server type's name in an `mssql` file, and a PostgreSQL type's in a file of any other dialect, so `int4` in an `mssql` file, or `nvarchar` in a `postgres` one, is not valid.  A type of a schema of your own, `dbo.Email`, is read in either.  An entry that is not valid is [SQLSRC111](../diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read `typeOverrides`, which changes nothing that is described.  A `-- type-override:` marker that is not valid is an error for the tool as for the build, and the tool describes no query of its file until it is fixed.

<a id="limits"></a>

## Limits

<a id="limit-key-in-brackets"></a>

### SQLSRC019 or SQLSRC111 for a type override's key written `[dbo].[Email]`

- **A type override's key cannot be written in brackets.**  T-SQL writes a name `[dbo].[Email]`, and a key written so is not valid: [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file, and [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker.  Write `dbo.Email`, which matches the type's name in any case, or `"dbo"."Email"`, which matches its case exactly; the message shows both.  See [type overrides](#setting-type-overrides).

<a id="limit-nullable-type-argument"></a>

### A compiler warning in a model for a `?` inside a type argument

- **A C# type that is generic can give its model a warning for a `?` inside a type argument**, in a type override, a column type or a `-- param:` marker.  A type argument written with a `?` where a constraint forbids null is [SQLSRC252](../diagnostics.md#sqlsrc252) at the query, but a `?` inside a type argument is not.  An override such as `MyApp.Names<System.Collections.Generic.List<string?>>`, where `Names<T>` requires `where T : System.Collections.Generic.IEnumerable<string>`, is accepted, and the generated model gets the compiler's warning CS8631.  Give the type arguments the nullability that the constraints ask for.  See [type overrides](#setting-type-overrides).
