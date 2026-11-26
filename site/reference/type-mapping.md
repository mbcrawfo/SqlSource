# How a C# type is chosen, and type overrides

<a id="property-types"></a>

## How a C# type is chosen

```sql postgres
-- column: users.id -> UserId

-- name: GetUser
-- column: nickname -> Nickname
SELECT id, nickname, created_at, born
FROM users
WHERE id = @id;
```

A property has the C# type of its column or parameter.  In the sample, the project maps `"date": "System.DateOnly"` with the type override, and references `Npgsql.NodaTime`.  Each property gets its type from one place:

- `Id` is a `UserId`, by the column type of the file header.
- `Nickname` is a `Nickname`, by the marker of its query.
- `CreatedAt`, a `timestamptz`, is a `NodaTime.Instant` by NodaTime under `repository` or `methods`, and a `System.DateTime` under `models`.
- `Born`, a `date`, is a `System.DateOnly`, by the type override.

A column has the C# type of the first of these places that gives one:

1. the `-- column: name -> Type` marker of its query: see [the `-- column:` marker](model-properties.md#column-marker);
2. the [column types](model-properties.md#setting-column-types), by the column of a table or view that it is read from;
3. the [type override](#setting-type-overrides), by its database type;
4. a library of the [library types](library-types-and-json.md#setting-library-types) that is on for the query: NodaTime, NetTopologySuite or System.Text.Json.  Only a query whose output is `repository` or `methods` gets this step;
5. the type map of [PostgreSQL](databases/postgresql/types.md#type-map) or [SQL Server](databases/sqlserver/types.md#types).

A parameter has the C# type that its [`-- param:` marker](parameters.md#param-marker) gives after `->`.  Without a C# type after `->`, it has the type of the type override, then of the library, then of the type map.

- **A marker, the column types and the type override each give a type that you chose.**  A query method reads and sends such a type through your [converters](converters.md#converters) when it has them.  A class of yours in a column that holds JSON goes through System.Text.Json's serializer: see [a class in a JSON column](library-types-and-json.md#json-class).
- **On PostgreSQL, Npgsql reads and sends a library's type**, through its plugin for NodaTime or NetTopologySuite.  On SQL Server, the query method itself reads and sends it.  On both, a converter of yours is used in its place, in the direction that it converts, except for NetTopologySuite on SQL Server.
- **The driver reads and sends a type of the type map.**
- **On SQL Server, the query method reads and sends a type of NetTopologySuite, whatever place chose it.**  No converter of yours replaces that: see [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite).
- **In a query whose output is `repository` or `methods`, a PostgreSQL array parameter is an `IEnumerable<T>` of its element.**  It replaces `T[]`: an `int4[]` gives an `IEnumerable<int>`.  See [array parameters](databases/postgresql/query-methods.md#array-parameters).  The property of a column, and of a parameter of a query whose output is `models`, stays `T[]`.
- **A [list parameter](list-parameters.md#list-parameters) is an `IEnumerable<T>` of the C# type of its values**, under `repository`, `methods` and `models`.  A list parameter is written after `IN` with no parentheses.  A list of `integer` is an `IEnumerable<int>`.  Its values get their C# type as a parameter does, and their type is never nullable.
- **A [column type](model-properties.md#setting-column-types) never reaches a query parameter**, even one compared with the column that it maps.

<a id="setting-type-overrides"></a>

## Type overrides

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
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

A type override gives a database type the C# type of your choice.  It replaces the C# type that the type map of [PostgreSQL](databases/postgresql/types.md#type-map) or [SQL Server](databases/sqlserver/types.md#types) gives.  Examples are a `timestamp with time zone` as a `System.DateTimeOffset`, a `numeric` as a `double`, and an enum of your schema as a C# enum.  Each entry is a key and a type: `"key": "type"` in a settings file, and `key=type` in a marker.  The key names the database type.  The type is the C# type that every column and parameter of that database type gets.

In the samples:

- Every query of the project gets `timestamptz` as a `System.DateTimeOffset`, `public.user_status` as `MyApp.UserStatus`, and `int4[]` as a list.
- The files under `Legacy/` also get `date` as a `System.DateTime`.
- Every query of the `.sql` file gets `numeric` as a `double`.
- `ListEvents` gets `timestamptz` from the type map, a `System.DateTime`, and keeps the other entries of the project.

A [column type](model-properties.md#setting-column-types) or a [`-- param:`](parameters.md#param-marker) marker gives one column, or one parameter, a C# type of its own.  Use one where two columns of one database type need two C# types.  Both come before the type override in [the order in which a C# type is chosen](#property-types).

For a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](../diagnostics.md#sqlsrc232).

Each property of a generated [model](models.md#models) has the C# type of its column or parameter, the type override's included.  A [query method](query-methods.md#query-methods) reads and sends a column or parameter as that type.  It uses your [converters](converters.md#converters) when the type has them.

**The key** names a database type.  On PostgreSQL:

- **A type of PostgreSQL's catalog is its one-word name**, as [the type table](databases/postgresql/types.md#type-table) gives it: `timestamptz`, `int4`, `float8`, `bpchar`.
- **The one-word spellings `integer`, `int`, `smallint`, `bigint`, `real`, `float`, `boolean`, `decimal` and `character` are read as the names they stand for.**  `float` is `float8`, and `character` is `bpchar`.
- **A spelling of several words is not a key.**  For `timestamp with time zone` or `double precision`, write the one-word name.  Every built-in type has one.
- **A name of the catalog, written without a schema, can be in double quotes only when it is written exactly, in lower case.**  `"int4"` is `int4`, but `"Int4"` and `"integer"` are not keys.
- **`char` is the one-byte type `"char"`, and so is the key `"char"`.**  A `char(n)` column is a `bpchar`.
- **Any other type is written with its schema**, `public.user_status`, as the tool records it in the sidecar.  Keep the quotes that its schema or name was created with: `public."Status"`, `"my schema"."order-status"`.
- **`pg_catalog.` before a name is PostgreSQL's system schema.**  `pg_catalog.int4`, or `int4`, is PostgreSQL's `int4` alone.
- **The types of extensions that the type table names are matched by their name alone, in any schema.**  They are `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube`.  `public.citext` matches only the `citext` of `public`, and wins over `citext` there.
- **An array is the key of its element and `[]`**: `int4[]`.
- **The catalog's name of an array is read as the same key.**  For a type that SqlSource knows, the name alone is read so: `_int4` is `int4[]`.  For any type of `pg_catalog`, the name with its schema is read so: `pg_catalog._pg_class` is `pg_class[]`.
- **`_int4` and `int4[]` are one type.**  Two entries for them are two entries for one type.
- **An array of a type outside `pg_catalog` is written only with its schema, the name of its element and `[]`**: `public.user_status[]`.  An entry for the database's name of that array, `public._user_status`, maps nothing.  It is [SQLSRC261](../diagnostics.md#sqlsrc261) at a query that uses the array.  Such a key still maps a type whose own name starts with `_` and that is not an array.
- **An entry for an array wins over an entry for its element**: `int4[]` over `int4`.  Without one, an array gets the C# type of its element with `[]`.  So `int4=long` makes an `int4[]` column a `long[]`.
- **In a query method, an array parameter whose C# type is an array of one rank is an `IEnumerable<T>` of the element.**  The array type can come from the type table, from a library or from an entry here.  A parameter that an entry maps to another type, such as a `List<int>`, is that type.  See [array parameters](databases/postgresql/query-methods.md#array-parameters).
- **A domain has a key of its own**, `public.email`, which wins over the key of its base type.  Without one, a domain gets the C# type of its base type.  So an entry for `text` maps every domain over `text` too.
- **A range and a multirange have only their own key.**  An entry for `int4` does not change an `int4range`.

On SQL Server:

- **A SQL Server type is its name**, as [its type table](databases/sqlserver/types.md#type-table) gives it: `int`, `nvarchar`, `datetime2`, `uniqueidentifier`.
- **SQL Server's other one-word names are read as the names they stand for.**  `integer`, `character`, `dec` and `rowversion` are read as `int`, `char`, `decimal` and `timestamp`.
- **A name of several words, such as `double precision`, is not a key.**
- **`sys.` before a name is SQL Server's system schema, as `pg_catalog.` is PostgreSQL's.**  `sys.int` is SQL Server's `int` alone.
- **An alias type is written with its schema.**  `CREATE TYPE dbo.Email FROM nvarchar(320)` makes an alias type whose key is `dbo.Email`.  Each part is plain or in double quotes, as for a PostgreSQL type outside `pg_catalog`.
- **The key of an alias type wins over the key of its base type.**  Without one, an alias type gets the C# type of its base type.  So an entry for `nvarchar` maps it too.

On both databases:

- **A key with a schema is not checked against the types that SqlSource knows.**  So it can name a type that SqlSource does not know.
- **The length, precision and scale of a type are not part of its key.**  `numeric` matches `numeric(18,2)`, and `nvarchar` matches `nvarchar(max)`.
- **A name that both databases have is one key, and maps the type of that name on each database.**  In a settings file, `"int": "long"` maps PostgreSQL's `int4` and SQL Server's `int`.  `"date": "System.DateOnly"` maps the `date` of both.
- **Some such names are different types on the two databases.**  `bit` is a bit string on PostgreSQL and a `bool` on SQL Server.  `timestamp` is a time on PostgreSQL and a row version on SQL Server.  `char` is PostgreSQL's one-byte type and SQL Server's string.
- **In a project with files of both, an entry can be meant for one database's type.**  Write its key with the system schema of that database, `pg_catalog.timestamp` or `sys.timestamp`.  Or set the entry in a settings file of the folder that holds the files of that database, as [configure one folder differently](../guides/folders-and-files.md#configure-a-folder) shows.
- **A marker is read only by the dialect of its file**, so it never reaches the type of the other database.
- **A part of a key, its schema or its name, is plain or in double quotes.**  A plain part holds letters, digits, `_` and `$`.  A part in quotes holds any character.  A quote inside it is doubled, as SQL writes a name: `"my schema"."order-status"`, `public."say ""hi"""`.
- **A `.` stands between the schema and the name, and `[]` comes after the name of an array.**
- **Put a name in quotes when it has a capital letter, or any character that a plain part does not hold**, white space included.
- **A part in quotes can be written with escapes, as PostgreSQL writes a name.**  `"my schema".U&"Order\000AItem"` names a type whose name is `Order`, a line break and `Item`.
- **Inside `U&"..."`, `\` and four hex digits, or `\+` and six, is the character of that code**, from 1 to 10FFFF.  A character beyond FFFF is one `\+` escape, or the escapes of its two surrogates.  `\\` is a backslash, and a quote is doubled.
- **A part with escapes is the same part as the quotes of its text.**  So `public.U&"st\0061tus"` is `public."status"`.
- **`UESCAPE` is not read.**  An escape that is not one of these makes the key not valid.
- **A part in quotes is compared exactly, case included.  A plain part is compared ignoring case.**  So `public."Status"` maps the type `Status` alone, while `public.Status` maps `Status` and `status` alike.
- **`public.Status` and `public.status` are two keys.**  `public.status` and `public."status"` are one key, and so are `public.Status` and `public."Status"`.  Two entries for them are two entries for one type.
- **Of the entries for one type whose keys differ only by case, the entry of the most specific place wins**, as for any type.
- **In one place, a key spelled exactly as the name of the type wins over a key that matches it only ignoring case.**  So in one place, `public.Status` and `public.status` each map their own type.
- **A plain key at a higher place replaces the entries below it for every type that it matches.**  A query's `public.status=default` takes both `Status` and `status` out of the entries of the project.
- **To change one of two types whose names differ only by case, quote it.**  A query's `public."status"=default` takes `status` alone out, and `Status` keeps the entry of the project.

**The type** is a C# type that a property can have:

- **A keyword**: `int`, `string`, `double` and the other built-in types.
- **A full name, with its namespace**: `System.DateTimeOffset` or `MyApp.Ids.UserKey`.  It can start with `global::`.
- **A short name, without its namespace**: `DateTimeOffset` or `UserKey`.
- **A nested type is written with its full name**, with a `.` before its own name, as in C#: `MyApp.Outer.Inner`.
- **A short name is the one type of that name, and of that number of type arguments, that your project can see.**  The type can be in any namespace, of your project or of its references.  A type of a reference counts when it is `public`, or `internal` in a reference that gives your project its internals with `InternalsVisibleTo`.
- **A short name does not find a type nested in another type, or a `file` type.**
- **Your project's own type wins over a reference's type of the same full name.**
- **A short name and the full name of its type are one type**, to [shared models](shared-models.md#shared-models) and to converters.  The generated code writes the full name of the type, so the `using` lines of your files do not matter.
- **A type argument can be a short name too, in any name**: `System.Collections.Generic.List<UserKey>`, or `List<UserKey>`.
- **`global::UserKey` is a type of the global namespace alone.**
- **A short name can find a type of an assembly that your project has only through a package that it does not reference itself.**  That is the warning [SQLSRC284](../diagnostics.md#sqlsrc284), since the type may not be the one you meant.  Write the full name to use the type without the warning.
- **A generic type names its type arguments**, and each of them can end in `?`: `System.Collections.Generic.List<int?>`.  White space inside `<>` and `[]` is allowed.
- **An array is the element and its ranks**: `long[]`, `int?[]`, `int[,]`.
- **The whole type has no `?` at its end.**  Whether a property can be null is decided by its column, or by the [`-- param:` marker](parameters.md#param-marker) of its parameter.  For which columns can be null, see [PostgreSQL](databases/postgresql/nullability.md#nullability) and [SQL Server](databases/sqlserver/nullability.md#nullability).  So `int4=int?` is not valid.
- **A tuple, `dynamic`, `void`, a pointer, and anything after the type are not valid either.**
- **The type must exist, and be the name of one type.**  If the project and its references do not have the type, the error is [SQLSRC250](../diagnostics.md#sqlsrc250).  So is a full name that two references have, and a short name of two types in two namespaces.  SQLSRC250 names the types.
- **The project must be able to see the type.**  If it cannot, the error is [SQLSRC251](../diagnostics.md#sqlsrc251).
- **A property must be able to have the type.**  If it cannot, the error is [SQLSRC252](../diagnostics.md#sqlsrc252).  Examples are a `static` class, `System.Nullable<int>` and a type marked `[Obsolete]` as an error.  Another is a generic type whose type argument breaks a constraint, such as `System.Collections.Generic.List<System.Nullable<string>>`.

**The value `default`** gives the database type back to the type map.  In a query method, a library of the [library types](library-types-and-json.md#setting-library-types) that is on gives it its type instead.

**The default.**  A database type with no entry in any place has the C# type of the type map, or of a library in a query method.

**The places.**  The type override can be written in these places:

- `typeOverrides` in the settings files of the folder of a `.sql` file and of the folders above it;
- the `-- type-override:` marker in the file header;
- the `-- type-override:` marker inside a query.

**In a settings file**, `typeOverrides` is a JSON object with a member for each entry.  The key of the member is the database type, and its value is the C# type: `"timestamptz": "System.DateTimeOffset"`.

- **White space around the key and around the type is ignored.**  White space inside them is part of the entry, as written.  Examples are a space inside `<>` or `[]`, as in `"System.Collections.Generic.Dictionary<string, int>"`, and a space inside the double quotes of a key, as in `"my schema"`.
- **The key is a JSON string, so write each `"` of it as `\"`, and each `\` as `\\`**, as JSON requires.  Examples are `"\"my schema\".\"order status\""`, and `"\"my schema\".U&\"Order\\000AItem\""` for a part with escapes.
- **Write any other character as it is, a `&` among them.**  A tab or a line break inside the quotes of a key can be written `\t` or `\n`, as JSON writes them.
- **An error that quotes a key that holds such a character writes the character as an escape**: `"my schema".U&"Order\000AItem"`.
- **One object serves PostgreSQL and SQL Server alike.**  A member whose key names a type of either database is valid.  It maps the type of each database that has that name.
- **Two members of one object whose keys name one type are [SQLSRC020](../diagnostics.md#sqlsrc020)**, and the first member applies.  Examples are `"integer"` and `"int4"`, and `"timestamptz"` and `"TIMESTAMPTZ"`.
- **A member that is not valid is [SQLSRC019](../diagnostics.md#sqlsrc019)**, and the other members still apply.

**In a marker**, a `-- type-override:` line holds one entry, with or without spaces around its `=`.

- **The lines of one place add up**, one for each type.
- **Two lines that give one type two C# types are [SQLSRC112](../diagnostics.md#sqlsrc112)**, in the file header or inside one query.  The C# types are compared by how they are named.  So `int` and `System.Int32` are two types, and so are `UserKey` and `MyApp.UserKey`.  `System.Int32` and `global::System.Int32` are one type.
- **The key is read by the dialect of the file.**  In an `mssql` file it is the name of a SQL Server type.  In a file of any other dialect it is the name of a PostgreSQL type.  So `int4` in an `mssql` file is not valid, and nor is `nvarchar` in a `postgres` file.
- **A type of a schema of your own, `dbo.Email`, is read in either dialect.**
- **An entry that is not valid is [SQLSRC111](../diagnostics.md#sqlsrc111).**

**Every place, merged by type.**  For each query, the entries add up in this order: the farthest settings file to the nearest, then the file header, then the query.  An entry for a type replaces the entry of a place below for that type alone.  This is one of the exceptions to the rule that the most specific place wins whole: see [which place wins](settings.md#precedence).

- **The entries of a settings file reach only the `.sql` files of its folder and of the folders below it.**
- **An entry replaces the entry below for its key, however that key is spelled, and keeps its own spelling.**  For example, a query has `public."Status"=MyApp.Status`, and the project's settings file has `"public.Status": "MyApp.Status"`.  The quoted key maps `Status` alone.  So in that query, a type `status`, which the project's entry mapped too, has the C# type of the type map.
- **A query that needs one type otherwise names that type**, and keeps the rest of the entries of the project.
- **`default` takes the type out of the entries of every place below.**
- **Nothing removes every entry at once.**

**When an error is reported.**  For an entry of the type override, the build reports SQLSRC250, SQLSRC251 or SQLSRC252 at the name of a query.  It does so only when all of these are true:

- The output of the query is `repository`, `methods` or `models`.
- The entry of the query in its sidecar is current.
- The first column or parameter of the query with no C# type, parameters first, has the key of the entry in one of these ways:
  - as its own type;
  - as the element of its array;
  - as the base type of its domain.

Through these conditions, a query gets one such error at most.  SQLSRC251 is also reported at each query of a `public` model that has a property of a type that is not `public`.  It is reported once for each such property: see [SQLSRC251](../diagnostics.md#sqlsrc251).  An entry that no query uses is never an error, so a project can set its entries before it has the queries.

**The tool.**  The `sqlsource` tool does not read `typeOverrides`, which changes nothing that is described.  A `-- type-override:` marker that is not valid is an error for the tool, as it is for the build.  Until the marker is fixed, the tool describes no query of its file.

<a id="limits"></a>

## Limits

<a id="limit-key-in-brackets"></a>

### SQLSRC019 or SQLSRC111 for a type override's key written `[dbo].[Email]`

**What you see.**  A type override's key written in brackets, `[dbo].[Email]`, is not valid.  In a settings file it is [SQLSRC019](../diagnostics.md#sqlsrc019), and in a marker it is [SQLSRC111](../diagnostics.md#sqlsrc111).

**Why.**  T-SQL writes a name in brackets, but a type override's key cannot be written in brackets.

**What to do.**  Write `dbo.Email`, which matches the name of the type in any case.  Or write `"dbo"."Email"`, which matches its case exactly.  The message shows both.  See [type overrides](#setting-type-overrides).

