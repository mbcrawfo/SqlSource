# Settings

A setting is a choice you make for a whole project, for some of its files or for one query: which dialect the SQL is written in, which comments to keep, which database the tool asks about a query.  Every setting is defined here, with the places it can be written and the one place that wins when it is written in several.

```sql
-- dialect: postgres
-- generator: keep-comments

-- name: ListUsers
-- output: sql
SELECT id, name FROM users;
```

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reports/**/*.sql" SqlSourceGeneratorParameters="keep-comments" />
</ItemGroup>
```

```csharp
[SqlSourceGenerate(Path = "Reports", SqlLocation = SqlLocation.Direct)]
internal static partial class Reports;
```

The first block writes settings as markers in a `.sql` file, the second as an MSBuild property and as item metadata, the third as properties of the attribute.  Which of the three a setting accepts is in [the tables](#all-settings).  To read one folder of a project differently from the rest, see [configure one folder differently](#configure-a-folder).

<a id="all-settings"></a>

## All settings

Each name links to its section, which gives its values and its rules.  **Written as** lists every place the setting can be written: an MSBuild property of the project, item metadata of some files, a property of the attribute, a marker in the **file header** (the part of a `.sql` file before its first `-- name:` line), and a marker **inside a query** (under a `-- name:` line), as [the marker index](sql-files.md#marker-index) shows.  A place it does not list cannot hold it.

### Settings that work today

| Setting | Values | Written as | Status |
|----|----|----|----|
| [Output](#setting-output) | `sql`, `models`, `codegen`.  Default `codegen` | `SqlSourceOutput` property and metadata; `Output` on the attribute; `-- output:` in the file header and inside a query | Works today |
| [Database](#setting-database) | A name of letters, digits, `-`, `_` and `.`.  Default: the name of the file's dialect | `SqlSourceDatabase` property and metadata; `-- database:` in the file header and inside a query | Works today, for the tool alone |
| [Dialect](#setting-dialect) | A dialect's name, with options for `mysql` and `mariadb`: see [the dialects page](dialects.md#choosing-a-dialect).  Default `ansi` | `SqlSourceDialect` property and metadata; `-- dialect:` in the file header | Works today |
| [Generator parameters](#setting-generator-parameters) | Words separated by spaces: `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix`, `no-array-pool`, `default`.  Default: none | `SqlSourceGeneratorParameters` property and metadata; `Parameters` on the attribute; `-- generator:` in the file header and inside a query | Works today |
| [Input and output model suffix](#setting-model-suffix) | Characters of a C# identifier.  Defaults `Params` and `Dto` | `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix` property and metadata; `InputModelSuffix`, `OutputModelSuffix` on the attribute; `-- input-model-suffix:`, `-- output-model-suffix:` in the file header | Works today |
| [Model namespace](#setting-model-namespace) | A namespace.  Default: the namespace of the type | `SqlSourceModelNamespace` property and metadata; `ModelNamespace` on the attribute; `-- model-namespace:` in the file header | Works today |
| [Input and output model type](#setting-model-type) | `record`, `sealed record`, `class`, `sealed class`.  Default `sealed record` | `SqlSourceInputModelType`, `SqlSourceOutputModelType` property and metadata; `InputModelType`, `OutputModelType` on the attribute; `-- input-model-type:`, `-- output-model-type:` in the file header and inside a query | Works today |
| [One query's model name](#setting-model-name) | A type's name, or its full name | `-- input-model:`, `-- output-model:` inside a query | Works today |
| [Type overrides](#setting-type-overrides) | Entries `key=type`, each a database type and the C# type it gets, or `key=default`.  Default: none | `SqlSourceTypeOverrides` property and metadata; `-- type-override:` in the file header and inside a query | Works today |
| [Column types](#setting-column-types) | Entries `key=type`, each a column of a table or view, `table.column` or `schema.table.column`, and the C# type it gets, or `key=default`.  Default: none | `SqlSourceColumnTypes` property and metadata; `-- column:` in the file header, and inside a query for a column of the query's result | Works today |
| [`SqlLocation`](#setting-sql-location) | `Nested`, `Direct`.  Default `Nested` | `SqlLocation` on the attribute | Works today |
| [Nested class](#setting-nested-class) | A C# identifier that is not a keyword.  Default `Sql` | `SqlSourceNestedClass` property; `NestedClass` on the attribute | Works today |
| [`Path`](#setting-path) | A folder, or one `.sql` file, relative to the folder of the C# file.  Default: that folder | `Path` on the attribute | Works today |
| [Collection type](#setting-collection-type) | A collection interface or type, such as `IReadOnlyList` or `List`: see [its section](#setting-collection-type).  Default `Array` | `SqlSourceCollectionType` property and metadata; `CollectionType` on the attribute; `-- collection-type:` in the file header and inside a query | Works today |
| [`MethodLocation`](#setting-method-location) | `ExtensionClass`, `Public`, `Internal`, `Private`.  Default `ExtensionClass` | `MethodLocation` on the attribute | Works today |

A value from a list is matched ignoring case, hyphens and spaces, so `CodeGen`, `codegen` and `code-gen` are one value, and so are `SealedRecord` and `sealed record`.  On the attribute you write the member of the enum that the package adds to your project, as in `Output = GeneratorOutput.Sql`.

<a id="precedence"></a>

## Which place wins

A setting written in several places has the value of the most specific place that gives one.  For `Output` the places are:

| Order | Place | Written as |
|----|----|----|
| 1 | A marker inside the query | `-- output: sql` |
| 2 | A marker in the file header | `-- output: models` |
| 3 | The attribute of the type that claims the file | `[SqlSourceGenerate(Output = GeneratorOutput.CodeGen)]` |
| 4 | The item metadata of the file | `<AdditionalFiles Update="Reports/**/*.sql" SqlSourceOutput="models" />` |
| 5 | The MSBuild property of the project | `<SqlSourceOutput>sql</SqlSourceOutput>` |
| 6 | The default | `codegen` |

A query with `-- output: sql` has the output `sql`, whatever the other five say.  A query without it takes the file header's `models`, and so on down the table.  To give the files of one folder their own values, see [configure one folder differently](#configure-a-folder).

The rules:

- **Each setting is decided on its own.**  A query that writes `-- output: sql` still takes its file header's `-- database:`.
- **A place that gives no value is skipped.**  A property, metadata or attribute string that is empty or white space gives none.  `Path` is the exception: see [Path](#setting-path).
- **A setting has only the places its row of [the tables](#all-settings) lists.**  A property or item metadata that SqlSource does not list is not read.  A marker where its row says it cannot stand is [SQLSRC116](diagnostics.md#sqlsrc116), except a `-- dialect:` inside a query or after SQL, which is [SQLSRC115](diagnostics.md#sqlsrc115): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).
- **The attribute belongs to one type.**  Two types that claim one file each use their own attribute, and each gets its own result.

Four exceptions, stated plainly:

- **A list of generator parameters replaces the one below it whole.**  It never adds to it.  See [generator parameters](#setting-generator-parameters).
- **A dialect value replaces the one below it whole**, options included.  The dialect has no attribute and no in-query level: a file has one dialect, which is the marker in its file header, else its item metadata, else the property, else `ansi`.  See [dialect](#setting-dialect).
- **`SqlLocation`, `MethodLocation` and `Path` exist only on the attribute.**  They have nothing to choose between.  The nested class has two places, the attribute over the property.
- **A type override, or a column type, replaces only the keys it names.**  Each place adds its entries to those of the places below it, an entry for a key, a database type or a column, replaces the entry below it for that key, and `default` takes the key out.  Neither has an attribute level.  See [type overrides](#setting-type-overrides) and [column types](#setting-column-types).

<a id="a-value-that-is-wrong"></a>

### A value that is wrong

| Written as | Reported as | What happens |
|----|----|----|
| A marker | [SQLSRC111](diagnostics.md#sqlsrc111), a dialect included.  A generator parameter that does not exist is [SQLSRC109](diagnostics.md#sqlsrc109); a value that contradicts another in the same place is [SQLSRC112](diagnostics.md#sqlsrc112) | The file produces no members until it is fixed |
| An MSBuild property or item metadata | [SQLSRC014](diagnostics.md#sqlsrc014), and for a dialect [SQLSRC011](diagnostics.md#sqlsrc011) | The error has no file and line.  The setting is not set, so the place below it wins; a dialect that is wrong is read as `ansi` instead, as [the dialect](#setting-dialect) says |
| A property of the attribute | [SQLSRC006](diagnostics.md#sqlsrc006) | The type produces no members until it is fixed |

<a id="setting-output"></a>

## Output

```sql
-- output: models

-- name: ListUsers
SELECT id, name FROM users;

-- name: ResetAuditLog
-- output: sql
TRUNCATE TABLE audit_log;
```

`Output` says how much SqlSource produces for a query.  The default is `codegen`.

| Value | A query gets |
|----|----|
| `sql` | Its SQL: a constant, or a method where the query has tokens |
| `models` | Its SQL, and its [models](generator.md#models): types for its parameters and for a row of its result |
| `codegen` | Its models, and a [method](generator.md#query-methods) that runs the query, which holds its SQL in place of a constant |

On the attribute: `[SqlSourceGenerate(Output = GeneratorOutput.Sql)]`.  As a marker it stands in the file header, for every query of the file, and inside a query.

**`Output` decides which queries must be described.**  `sqlsource describe` asks a database about the queries whose output is `models` or `codegen`, and the build checks that each of them has a current entry in its file's [sidecar](concepts.md#sidecar): a query that has none is [SQLSRC241](diagnostics.md#sqlsrc241), and one that changed since it was described is [SQLSRC244](diagnostics.md#sqlsrc244).  A query whose output is `sql` needs no description, no entry, no connection, and belongs to no [logical database](#setting-database).  On the attribute, write the value out literally, as in `Output = GeneratorOutput.Models`, so that the tool can read it; a constant or an expression there is [SQLSRC208](diagnostics.md#sqlsrc208).  Details are under [the generator's known issues](generator.md#known-issues).  A file claimed by two types, or listed by two projects, is described when either needs it, and the build of a project checks the entries that any of its types needs, once for the file.

The tool can describe only some dialects: see [the database support table](dialects.md#database-support).  The default output is `codegen` and the default dialect is `ansi`, so a project that sets neither gets [SQLSRC209](diagnostics.md#sqlsrc209) for every file, in its build and from the tool.  Set the dialect and describe the queries, or set the output to `sql` for queries that want only their SQL.

A query whose output is `sql` or `models` gets its constant or token method.  A query whose output is `models` or `codegen`, and whose entry in its sidecar is current, also gets its [models](generator.md#models); one whose output is `sql` gets none.  A query whose output is `codegen` gets no constant and no token method: its [method](generator.md#query-methods) holds its SQL and runs it, when the query meets the conditions of [which queries get a method](generator.md#which-methods).  A project that runs the SQL itself, through Dapper or its own ADO.NET code, sets `models`.

<a id="setting-database"></a>

## Database

```sql
-- database: billing

-- name: ListInvoices
SELECT id, total FROM invoices;

-- name: ListEvents
-- database: events
SELECT id, kind FROM events;
```

`Database` names the [logical database](concepts.md#logical-database) a query belongs to: the one the `sqlsource` tool asks about it, through a connection you give it by that name.  It is not the dialect.  Two logical databases can share a dialect.

- The value is one word of letters, digits, `-`, `_` and `.`.  The rule is narrow because the tool makes the name of an environment variable of it: `billing` is read from `SQLSOURCE_CONNECTION_BILLING`.  Names are compared ignoring case, so `Billing` and `billing` are one database, spelled as the tool first meets it.
- Without a value, a query belongs to the database named after its file's dialect: `postgres` for a PostgreSQL file, `mssql` for a SQL Server file.
- It can be a marker in the file header or inside a query, item metadata, or an MSBuild property.  It has no attribute property.
- One name is one database for a whole run, with one connection and one dialect.  Two files that give one name two dialects are [SQLSRC211](diagnostics.md#sqlsrc211).
- Only the tool reads this setting.  The build does not, so a wrong value in a property or in item metadata is reported by the tool and not by the build.

To give the tool a connection for a name, see [the command reference](tool.md#command-reference).

<a id="setting-dialect"></a>

## Dialect

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" />
</ItemGroup>
```

```sql mysql
-- dialect: mysql

-- name: FindByNote
SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
```

The [dialect](concepts.md#dialect) is the set of rules by which SqlSource reads a `.sql` file.  It does not connect to anything.  The default is `ansi`.  The dialect names, the options of `mysql` and `mariadb`, and what each dialect changes are on [the dialects page](dialects.md).

- **Three places.**  A `-- dialect:` marker in the file, else the `SqlSourceDialect` metadata of the file's item, else the `SqlSourceDialect` property.  Where two item lines match one file, MSBuild applies them in order and the later one wins.
- **A file has one dialect.**  The marker goes in the file header, before the first SQL, and not inside a query: anywhere else it is [SQLSRC115](diagnostics.md#sqlsrc115).  It takes effect on the line after it, so comments above it are read by the dialect that the metadata or the property gives.
- **A value replaces the one below it whole.**  A file with `-- dialect: mysql` in a project that sets `mysql,ansi-quotes` is read as plain `mysql`.
- **A value that is not a dialect** is [SQLSRC111](diagnostics.md#sqlsrc111) in a marker and [SQLSRC011](diagnostics.md#sqlsrc011) in a property or item metadata.  A file whose property or metadata is wrong is read as `ansi` meanwhile.
- **The tool reads the dialect too.**  It decides which kind of database the tool asks about a query, and whether there is one to ask: `postgres` and `mssql` can be described.  See [database support](dialects.md#database-support).

<a id="setting-generator-parameters"></a>

## Generator parameters

```sql
-- generator: keep-comments

-- name: Report
SELECT /* the database logs this comment */ id FROM users;

-- name: Total
-- generator: no-token-validation
SELECT COUNT(*) FROM {{table:users}};
```

A [generator parameter](concepts.md#generator-parameter) is a word that changes what SqlSource does with a query.  A list of them is written in a `-- generator:` line, in `Parameters` on the attribute, or in `SqlSourceGeneratorParameters` as a property or as item metadata.  Each parameter is a departure from a default, so a query with no list gets every default.

| Parameter | Effect |
|----|----|
| `keep-comments` | Comments and blank lines stay in the generated SQL: see [what reaches the generated SQL](sql-files.md#generated-sql) |
| `no-token-validation` | The method of a query with tokens does not check its arguments: see [validation](parameters-and-tokens.md#token-validation) |
| `sort-input`, `sort-output` | The properties of the query's input model, or of its output model, are sorted by name: see [the order of the properties](generator.md#property-order) |
| `no-table-models` | A query that reads a whole table gets a row model named after the query, not after the table: see [table models](generator.md#table-models) |
| `async-method-suffix` | The name of a query's [method](generator.md#method-signature) ends with `Async`: `GetUserAsync` for `GetUser` |
| `no-array-pool` | A [`many`](generator.md#method-shapes) method that returns an array, `IEnumerable<T>`, `IReadOnlyCollection<T>` or `IReadOnlyList<T>` reads its rows into buffers borrowed from .NET's shared array pool.  Once the pool holds buffers of the sizes it needs, the method allocates no collection for a result of up to about 1,000 rows but the one it returns.  Under `no-array-pool` it reads them into a `List<T>` that it allocates, as hand-written code usually does.  Use it to keep the pool out of a project, or to compare the two.  **Coming in a later release.**  `List<T>`, `IList<T>`, `ICollection<T>` and `ImmutableArray<T>` will read their rows through the pool too.  See [what works today](README.md#what-works-today). |
| `default` | The empty list: every default.  `default` must be the only word in its list, though it may be repeated |

Words are matched ignoring case.

**A list replaces; it never adds.**  In the example, `Total` has the list `no-token-validation` and nothing else: it does not keep its comments, though the file header says `keep-comments`.  A query that wants both writes both, `-- generator: keep-comments no-token-validation`.  `-- generator: default` gives a query every default, whatever its file says.

The list for a query is the first of these that gives one, whole:

1. The `-- generator:` lines inside the query.
2. The `-- generator:` lines in the file header.
3. `Parameters` on the attribute of the type that claims the file.
4. The `SqlSourceGeneratorParameters` metadata of the file's item.
5. The `SqlSourceGeneratorParameters` property.

Several `-- generator:` lines in one place are one list: two lines in a query add up.  A list never adds to the list of another place.

In a property, in metadata and on the attribute the list is words separated by spaces or line breaks.  `default` beside another word there makes the whole value wrong: [SQLSRC014](diagnostics.md#sqlsrc014) in a property or metadata, [SQLSRC006](diagnostics.md#sqlsrc006) on the attribute.  A word that is not a generator parameter is reported the same way.  In a property or metadata the other words still apply; on the attribute the type gets no members until the value is fixed.  In a marker an empty `-- generator:` line is [SQLSRC110](diagnostics.md#sqlsrc110), and `default` beside another word in one place is [SQLSRC112](diagnostics.md#sqlsrc112).

<a id="setting-model-suffix"></a>

## Suffix of a model's name

```sql
-- input-model-suffix: Args
-- output-model-suffix: Row

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

The suffix ends the name of the [model](generator.md#models) generated for a query's parameters (input) and for a row of its result (output).  The defaults are `Params` and `Dto`, so `GetUser` gets `GetUserParams` and `GetUserDto`; above, it gets `GetUserArgs` and `GetUserRow`.

- The value is one or more characters that a C# identifier can contain, such as letters, digits and `_`.  A space or a period is not one.
- It stands in the file header only, not inside a query.  It can also be item metadata, an MSBuild property, or a property of the attribute.
- It ends a [table model's](generator.md#table-models) name too, `UsersRow`, and not a name that `-- input-model:` or `-- output-model:` gives.  Two equal suffixes give a query's two models one name, which is [SQLSRC257](diagnostics.md#sqlsrc257).

The rules of the names are on [the generator page](generator.md#model-names).

<a id="setting-model-namespace"></a>

## Namespace of the models

```sql
-- model-namespace: App.Models

-- name: ListUsers
SELECT id, name FROM users;
```

The namespace the generated [models](generator.md#models) go in.  The default is the namespace of the type that claims the file.  Above, the models of `ListUsers` are in `App.Models`.

- The value is C# identifiers joined by periods, none of them a reserved keyword.
- It stands in the file header only.  It can also be item metadata, an MSBuild property (`SqlSourceModelNamespace`), or `ModelNamespace` on the attribute.
- A name with a period that `-- input-model:` or `-- output-model:` gives is a full name, and ignores this setting.
- Two types in two namespaces that claim one file, or read one table, get a model each, one in each namespace, unless this setting gives them one.
- A namespace that is, or starts with, the full name of a type of your project gives models whose names cannot be used: [SQLSRC258](diagnostics.md#sqlsrc258).

The rules are on [the generator page](generator.md#model-namespace).

<a id="setting-model-type"></a>

## Shape of a model

```sql
-- output-model-type: sealed class

-- name: ListUsers
-- input-model-type: record
SELECT id, name FROM users WHERE id > @min;
```

The shape of the [model](generator.md#models) generated for a query's parameters (input) and for a row of its result (output): `record`, `sealed record`, `class` or `sealed class`.  The default is `sealed record` for both.  A record is positional; a class has a property with `get` and `set` for each column or parameter.  Above, every output model of the file is a `sealed class`, and the input model of `ListUsers` a `record`.

- It stands in the file header and inside a query, as two markers.  It can also be item metadata, an MSBuild property, or a property of the attribute: `InputModelType = GeneratorModelType.Class`.
- Two queries that share a model must give it one shape, or the later is [SQLSRC257](diagnostics.md#sqlsrc257).

What each shape looks like is on [the generator page](generator.md#model-shape).  To read rows through Dapper into sorted models, make them classes: see [the recipe](recipes.md#models-through-dapper).

<a id="setting-model-name"></a>

## Name of one query's model

```sql
-- name: GetUser
-- input-model: UserArgs
-- output-model: App.Models.UserRow
SELECT id, name FROM users WHERE id = @id;
```

`-- input-model:` and `-- output-model:` name the [model](generator.md#models) of one query's parameters and of its result rows, whole, with no suffix.  The value is a type's name, or its full name: identifiers joined by periods, none of them a reserved keyword.  Above, `GetUser` gets `UserArgs`, in the namespace its models would have without the marker, and `UserRow` in the namespace `App.Models`, whatever the [model namespace](#setting-model-namespace) says.

- It stands inside a query only.  It has no property, no item metadata and no attribute property, since a name is for one query.
- `-- input-model:` on a query with no parameters is [SQLSRC119](diagnostics.md#sqlsrc119), and `-- output-model:` on a query whose statement returns no rows is [SQLSRC259](diagnostics.md#sqlsrc259).
- `-- output-model:` wins over the name of a [table model](generator.md#table-models).
- Two queries that give one name share one model when their models are the same, and are [SQLSRC257](diagnostics.md#sqlsrc257) otherwise: see [shared models](generator.md#shared-models).  A name that cannot be used, such as that of a type of your project, is [SQLSRC258](diagnostics.md#sqlsrc258).

<a id="setting-collection-type"></a>

## Collection type

```sql
-- name: ListUsers
-- collection-type: IReadOnlyList
SELECT id, name FROM users;
```

The type that the [method](generator.md#query-methods) of a query whose result shape is `many` returns its rows in.  The default is `Array`.  Above, `ListUsers` returns `Task<IReadOnlyList<ListUsersDto>>`.

| Value | Returns | Holding |
|----|----|----|
| `Array` (the default) | `T[]` | An array |
| `IEnumerable` | `IEnumerable<T>` | An array |
| `IReadOnlyCollection` | `IReadOnlyCollection<T>` | An array |
| `IReadOnlyList` | `IReadOnlyList<T>` | An array |
| `ICollection` | `ICollection<T>` | A `List<T>` |
| `IList` | `IList<T>` | A `List<T>` |
| `List` | `List<T>` | A `List<T>` |
| `ImmutableArray` | `ImmutableArray<T>` | An `ImmutableArray<T>` |
| `ImmutableList` | `ImmutableList<T>` | An `ImmutableList<T>` |
| `IImmutableList` | `IImmutableList<T>` | An `ImmutableList<T>` |

`T` is the query's output model.  Every collection is filled before the method's task completes, `IEnumerable` included, so reading it runs no query.

- It stands in the file header and inside a query.  It can also be item metadata, an MSBuild property, or `CollectionType = GeneratorCollectionType.IReadOnlyList` on the attribute.
- It changes nothing for a query whose shape is not `many`, and nothing under the output `sql` or `models`.

<a id="setting-type-overrides"></a>

## Type overrides

```xml
<PropertyGroup>
    <SqlSourceTypeOverrides>
        timestamptz=System.DateTimeOffset
        public.user_status=MyApp.UserStatus
        int4[]=System.Collections.Generic.List&lt;int?&gt;
    </SqlSourceTypeOverrides>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Legacy/**/*.sql" SqlSourceTypeOverrides="date=System.DateTime" />
</ItemGroup>
```

```sql postgres
-- type-override: numeric = double

-- name: ListEvents
-- type-override: timestamptz = default
SELECT id, happened_at, amount FROM events;
```

A type override gives a database type the C# type of your choice, in place of the one that the type map of [PostgreSQL](postgresql.md#type-map) or [SQL Server](sqlserver.md#types) gives it: a `timestamp with time zone` as a `System.DateTimeOffset`, a `numeric` as a `double`, an enum of your schema as a C# enum.  Each entry is `key=type`: the key names the database type, and the type is the C# type that every column and parameter of that database type gets.  `key=default` gives the type back to the type map.

In the example, every query of the project gets `timestamptz` as a `System.DateTimeOffset`, `public.user_status` as `MyApp.UserStatus`, and `int4[]` as a list.  The files under `Legacy/` get `date` as a `System.DateTime` too.  Every query of the `.sql` file gets `numeric` as a `double`, and `ListEvents` gets `timestamptz` from the type map, a `System.DateTime`, while it keeps the project's other entries.

To give one column, or one parameter, a C# type of its own, where two columns of one database type need two C# types, see [column types](#setting-column-types) and [`-- param:`](parameters-and-tokens.md#param-marker).  Either wins over the type override.

Each property of a generated [model](generator.md#models) has the C# type of its column or parameter, the type override's included.  A [query method](generator.md#query-methods) reads and sends a column or parameter as that type, through your [converters](#converters) when the type has them.

**Four places, merged by type.**  The type override is the `SqlSourceTypeOverrides` property, the `SqlSourceTypeOverrides` metadata of a file's item, and the `-- type-override:` marker in the file header and inside a query.  It has no attribute property.  For each query the entries add up from the property, then the metadata, then the file header, then the query, and an entry for a type replaces the entry of a place below for that type alone.  It replaces the entry below for its key however that key is spelled, and its own spelling is the one kept: a query's `public."Status"=MyApp.Status` over the project's `public.Status=MyApp.Status` leaves a type `status`, which the project's entry mapped too, to the type map, since the quoted key maps `Status` alone.  So a query that needs one type otherwise names that type, and keeps the rest of the project's entries.  This is the exception to the rule that the most specific place wins whole: see [which place wins](#precedence).  `default` takes the type out of the entries of every place below, and a key with no entry anywhere has the type map's C# type.  Nothing removes every entry at once.

**The key** is a database type:

- **A type of PostgreSQL's catalog is its one-word name**, as [the type table](postgresql.md#type-table) gives it: `timestamptz`, `int4`, `float8`, `bpchar`.  The one-word spellings `integer`, `int`, `smallint`, `bigint`, `real`, `float`, `boolean`, `decimal` and `character` are read as the names they stand for: `float` is `float8`, and `character` is `bpchar`.  A spelling of several words, `timestamp with time zone` or `double precision`, is not a key: write the one-word name, which every built-in type has.  The name may be in double quotes when it is written exactly, in lower case: `"int4"` is `int4`, while `"Int4"` and `"integer"` are not keys.  `char` is the one-byte type `"char"`, and so is the key `"char"`; a `char(n)` column is a `bpchar`.
- **Any other type is written with its schema**, `public.user_status`, as the tool records it in the sidecar, and with the quotes its schema or name was created with: `public."Status"`, `"my schema"."order-status"`.  A key with a schema is not checked against the types SqlSource knows, so a type it does not know can be named.  `pg_catalog.int4` is `int4`.
- **The types of extensions** that the type table names, `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube`, are matched by their name alone in any schema.  `public.citext` matches the `citext` of `public` alone, and wins over `citext` there.
- **An array** is its element's key and `[]`, `int4[]`.  The catalog's name of an array is read as the same key: `_int4` for a type that SqlSource knows, and with its schema for any type of `pg_catalog`, so `pg_catalog._pg_class` is `pg_class[]`.  `_int4` and `int4[]` are one type, and two entries for them are two entries for one type.  Write an array of a type outside `pg_catalog` with its schema, its element's name and `[]`, `public.user_status[]`, and only so.  An entry for the database's name of that array, `public._user_status`, maps nothing, and is [SQLSRC261](diagnostics.md#sqlsrc261) at a query that uses the array.  Such a key still maps a type whose own name starts with `_` and that is no array.  An entry for the array, `int4[]`, wins over one for its element, `int4`.  Without one, an array gets its element's C# type with `[]`, so `int4=long` makes an `int4[]` column a `long[]`.
- **A domain** has a key of its own, `public.email`, which wins over its base type's.  Without one, a domain gets its base type's C# type, so an entry for `text` maps every domain over `text` too.
- **A range and a multirange** have only their own key.  An entry for `int4` does not change an `int4range`.
- A key's length, precision and scale are not part of it: `numeric` matches `numeric(18,2)`, and `nvarchar` matches `nvarchar(max)`.
- **A SQL Server type is its name**, as [its type table](sqlserver.md#type-table) gives it: `int`, `nvarchar`, `datetime2`, `uniqueidentifier`.  SQL Server's other one-word names `integer`, `character`, `dec` and `rowversion` are read as `int`, `char`, `decimal` and `timestamp`.  A name of several words, `double precision`, is not a key.  `sys.` before a name is SQL Server's system schema, as `pg_catalog.` is PostgreSQL's: `sys.int` is SQL Server's `int` alone, and `pg_catalog.int4`, or `int4`, PostgreSQL's alone.
- **An alias type of SQL Server**, made with `CREATE TYPE dbo.Email FROM nvarchar(320)`, is written with its schema, `dbo.Email`, each part plain or in double quotes, as a PostgreSQL type outside `pg_catalog` is.  Its own key wins over its base type's.  Without one, it gets its base type's C# type, so an entry for `nvarchar` maps it too.
- **A name that both databases have is one key, and maps each database's own type of that name.**  `int=long` maps PostgreSQL's `int4` and SQL Server's `int`, and `date=System.DateOnly` the `date` of both.  Some such names are different types: `bit` is a bit string on PostgreSQL and a `bool` on SQL Server, `timestamp` a time on PostgreSQL and a row version on SQL Server, and `char` PostgreSQL's one-byte type and SQL Server's string.  In a project with files of both, an entry that is meant for one database's type is written with that database's system schema, `pg_catalog.timestamp` or `sys.timestamp`, or set in the metadata of that database's files, as [configure one folder differently](#configure-a-folder) shows.  A marker is read by its file's dialect alone, so it never reaches the other database's type.
- **A part of a key, its schema or its name, is plain or in double quotes.**  A plain part holds letters, digits, `_` and `$`.  A part in quotes holds any character, with a quote inside it doubled, as SQL writes a name: `"my schema"."order-status"`, `public."say ""hi"""`.  A `.` stands between the schema and the name, and `[]` after an array's.  Put a name in quotes when it has a capital letter, or any character that a plain part does not hold, white space included.  A part in quotes may be written with escapes, as PostgreSQL writes a name: `"my schema".U&"Order\000AItem"` names a type whose name is `Order`, a line break and `Item`.  Inside `U&"..."`, `\` and four hex digits, or `\+` and six, is the character of that code, from 1 to 10FFFF, and a character beyond FFFF is one `\+` escape or the escapes of its two surrogates; `\\` is a backslash, and a quote is doubled.  It is the same part as the quotes of its text, so `public.U&"st\0061tus"` is `public."status"`.  `UESCAPE` is not read, and an escape that is not one of these makes the key not valid.  In a project file, `&` is written `&amp;`, as XML requires: `"my schema".U&amp;"Order\000AItem"`.
- **A part in quotes is compared exactly, case included, and a plain part ignoring case.**  So `public."Status"` maps the type `Status` alone, while `public.Status` maps `Status` and `status` alike.  `public.Status` and `public.status` are two keys.  `public.status` and `public."status"` are one key, and so are `public.Status` and `public."Status"`: two entries for them are two entries for one type.
- **Of the entries for one type whose keys differ only by case, the most specific place's wins, as for any type.**  Among the entries of one place, the one whose key is spelled exactly as the type's name wins over one that matches it only ignoring case, so in one place `public.Status` and `public.status` each map their own type.  A plain key at a higher place replaces the entries below for every type it matches: a query's `public.status=default` takes both `Status` and `status` out of the project's entries.  To change one of two types whose names differ only by case, quote it: a query's `public."status"=default` takes `status` alone out, and `Status` keeps the project's entry.

**The type** is a C# type that a property can have:

- **A keyword**, `int`, `string`, `double` and the others of the built-in types; **a full name from its namespace**, `System.DateTimeOffset` or `MyApp.Ids.UserKey`, which may start with `global::`; or **a short name**, without its namespace, `DateTimeOffset` or `UserKey`.  A nested type is written with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.
- **A short name is the one type of that name** and number of type arguments, in any namespace, that your project and its references have and your project can see: a type of a reference counts when it is `public`, or `internal` in a reference that gives your project its internals with `InternalsVisibleTo`.  A type nested in another, and a `file` type, are not found by one.  Your project's own type wins over a reference's of the same full name.  The generated code writes the type's full name, so the `using` lines of your files do not matter, and a short name and the full name of its type are one type, to [shared models](generator.md#shared-models) and to converters.  A type argument may be a short name too, in any name: `System.Collections.Generic.List<UserKey>`, or `List<UserKey>`.  `global::UserKey` is a type of the global namespace alone.
- **A generic type** names its type arguments, each of which may end in `?`: `System.Collections.Generic.List<int?>`.  White space inside `<>` and `[]` is allowed.  In a project file, `<` and `>` are written `&lt;` and `&gt;`, as XML requires.
- **An array** is the element and its ranks: `long[]`, `int?[]`, `int[,]`.
- **No `?` at the end of the whole type.**  Whether a property can be null is decided by its column, by which columns can be null on [PostgreSQL](postgresql.md#nullability) and on [SQL Server](sqlserver.md#nullability), or by its parameter's [`-- param:` marker](parameters-and-tokens.md#param-marker), so `int4=int?` is not valid.  A tuple, `dynamic`, `void`, a pointer, and anything after the type are not valid either.
- **The type must exist and be usable.**  A type that the project and its references do not have, a full name that two references have, or a short name of two types in two namespaces, is [SQLSRC250](diagnostics.md#sqlsrc250), which then names them; one the project cannot see is [SQLSRC251](diagnostics.md#sqlsrc251); and one that no property can have, such as a `static` class, `System.Nullable<int>`, a type marked `[Obsolete]` as an error or a generic type whose type argument breaks a constraint, as `System.Collections.Generic.List<System.Nullable<string>>`, is [SQLSRC252](diagnostics.md#sqlsrc252).  Each is reported at the name of a query whose output is `models` or `codegen` and whose entry in its sidecar is current, once for the query: when the first of its columns and parameters that has no C# type, parameters before columns, is one whose type, or whose array's element or domain's base, has the entry's key.  An entry that no query uses is never an error, so a project can set its entries before it has the queries.

**In a property or metadata**, the value is a list of entries separated by spaces or line breaks, so a property can hold one entry on each line, as in the example.  No space stands around the `=`: `numeric = double` is three entries, none of them valid.  An entry may break across lines inside `<>`, since a space or a line break inside `<>` or `[]` is part of its entry; so a `<` or a `[` that is never closed makes the rest of the value one entry that is not valid, and the entries after it do not apply.  The same holds for the double quotes of a key: a space inside them is part of the entry, so `"my schema"."order status"=MyApp.OrderStatus` is one entry, and a quote that is never closed makes the rest of the value one entry that is not valid.  A run of white space inside the quotes is read as one space, so write a tab, a line break or a second space there with an escape: see [values written in MSBuild](#values-written-in-msbuild).  A value serves PostgreSQL and SQL Server alike: an entry whose key names a type of either is valid, and maps the type of each database that has that name.  Of two entries for one key in one value, the last wins, with its spelling of the key, so a project can add to the value that a `Directory.Build.props` gives it:

```xml
<PropertyGroup>
    <SqlSourceTypeOverrides>$(SqlSourceTypeOverrides) timestamptz=System.DateTime</SqlSourceTypeOverrides>
</PropertyGroup>
```

In an item's metadata written as an attribute, a quote is written `&quot;`, as XML requires inside an attribute in double quotes, or the attribute is put in single quotes:

```xml
<ItemGroup>
    <AdditionalFiles Update="Orders/**/*.sql" SqlSourceTypeOverrides='public."Status"=MyApp.OrderStatus' />
</ItemGroup>
```

An entry that is not valid is [SQLSRC014](diagnostics.md#sqlsrc014), and the other entries of the value still apply.

**In a marker**, a `-- type-override:` line holds one entry, with or without spaces around its `=`.  The lines of one place add up, one for each type.  Two lines that give one type two C# types in the file header, or inside one query, are [SQLSRC112](diagnostics.md#sqlsrc112); the types are compared by how they are named, so `int` and `System.Int32` are two, and so are `UserKey` and `MyApp.UserKey`, while `System.Int32` and `global::System.Int32` are one.  Its key is read by the file's dialect: a SQL Server type's name in an `mssql` file, and a PostgreSQL type's in a file of any other dialect, so `int4` in an `mssql` file, or `nvarchar` in a `postgres` one, is not valid.  A type of a schema of your own, `dbo.Email`, is read in either.  An entry that is not valid is [SQLSRC111](diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read the property or the metadata, which change nothing that is described.  A `-- type-override:` marker that is not valid is an error for the tool as for the build, and the tool describes no query of its file until it is fixed.

<a id="converters"></a>

### Converters

```csharp
using SqlSource;

namespace MyApp;

public readonly record struct Email(string Value)
{
    [SqlSourceConverter]
    public static Email FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(Email email) => email.Value;
}
```

```sql postgres
-- type-override: public.email=MyApp.Email

-- name: FindUser -> one-optional
-- param: @email public.email
SELECT id, email FROM users WHERE email = @email;
```

A type override, a [column type](#setting-column-types) or a `-- param:` marker names a C# type, and the driver reads and sends only the types it knows.  A converter is how a [query method](generator.md#query-methods) reads and sends a C# type of your choice, one the driver does not know among them, through a type the driver reads: a `static` method marked `[SqlSourceConverter]` that takes one value and returns it converted.  Above, `email` is a domain over `text`, which Npgsql reads as a `string`.  PostgreSQL gives a parameter compared with a column of a domain the domain's base type, `text` here, so [`-- param:`](parameters-and-tokens.md#param-marker) declares `@email` as the domain, which the type override then gives the type `Email`.  SQL Server gives a parameter compared with, or written to, a column of an alias type that alias type, so with an alias type `dbo.Email` and `dbo.Email=MyApp.Email`, a SQL Server query needs no `-- param:`.  The method of `FindUser` reads the column `email` as a `string` and gives it to `FromDatabase`, and gives the `Email` of `@email` to `ToDatabase` and sends the `string` it returns.  `SqlSourceConverterAttribute` is generated into your project, beside `[SqlSourceGenerate]`, in the namespace `SqlSource`.

**A converter converts from its argument's type to its return type**, whatever its name.  A column needs the converter from the type the driver reads to its property's type, and a parameter the one back, so a query that has both, as `FindUser` does, needs both.  A query that needs the direction a type has no converter for, while the type has the other, is [SQLSRC280](diagnostics.md#sqlsrc280).  A method that takes and returns one type serves both directions: `System.DateTime AsUtc(System.DateTime value)` under `timestamp=System.DateTime` converts each `timestamp` as it is read and as it is sent.

**The type the driver reads** is the C# type that the type table of [PostgreSQL](postgresql.md#type-table) or [SQL Server](sqlserver.md#type-table) gives the database type, under the version of the driver your project references, with no type override or column type applied.  The converter takes or returns that type exactly:

- **A domain or an alias type is read as its base type**: a converter of a domain over `text` takes and returns a `string`, and one of a domain over `timestamptz` a `System.DateTime`, even where `timestamptz=System.DateTimeOffset` is in effect.  An array is read as an array of its element's type, `string[]` for `email[]`.
- **A converter of another type is not used.**  A type of yours whose converters are all of other types makes the query [SQLSRC279](diagnostics.md#sqlsrc279), which names the type to convert from and to.  A converter of an `int` never reads a `bigint` column, which the driver reads as a `long`: SqlSource never narrows or widens a value.
- **The type can change with the driver.**  Npgsql 8 and 9 read a `date` as a `System.DateTime`, and Npgsql 10 as a `System.DateOnly`, as [which version of Npgsql](postgresql.md#which-npgsql) says.  So after an update of Npgsql, a type of yours that a type override gives a `date`, or another type that the update changes, can need converters of another type, and SQLSRC279 says which.
- **A database type that the driver reads as no C# type**, a composite type or `regclass` for one, has nothing to convert from: a type override or a column type that gives it a type with converters is SQLSRC279.  Give a composite the type that the application's data source maps it to, with no converter.

**Where a converter is found.**  SqlSource finds a converter in two places:

- **in your project's own code**, in any type: on the type itself, as `Email`'s above, or in a class of conversions;
- **on the type that a type override, a column type or a `-- param:` marker names**, when that type is declared in a referenced assembly, such as a library of your ids.  The library references SqlSource for the attribute, and its own build reports its converters' errors.

A converter in any other type of a referenced assembly is not found.  When your project and a library's type both convert between the same two types, your project's converter is used, with no error, so that a project can replace a library's conversion.  A library's converter is used only when your project can call it: when it is `public`, or `internal` in a library that gives your project its internals with `InternalsVisibleTo`.  A library's converter that is not one, one your project cannot call among them, is [SQLSRC277](diagnostics.md#sqlsrc277) at each query whose method would use it, since your project cannot change it.

**A converter is a `static` method of one argument that returns a value**, `public` or `internal`, in a type that is not generic.  A method marked `[SqlSourceConverter]` that is not one is SQLSRC277, which lists each rule, in any project, whatever its output.  Its argument may be written with `?`, `Email?` or `string?`: it takes the type without the `?`, since it is never given null.  Its return type is never written with `?`.  A converter marked `[Obsolete]` as a warning is used, and the generated code turns the warning off; one marked `[Obsolete]` as an error is SQLSRC277.  Two converters between the same two types in one direction are [SQLSRC278](diagnostics.md#sqlsrc278), at both.

**Null never reaches a converter.**  A column that holds a null gives its property null when the property can be null, and throws as any column does when it cannot: see [how a row is read](generator.md#reading-rows).  A parameter whose value is null is sent as the database's `NULL`.  So one pair of converters serves a property that can be null and one that cannot.

**An array of PostgreSQL whose C# type is an array of a type of yours**, chosen for the element, as `email[]` under `public.email=MyApp.Email`, or for the array, as `public.user_id[]=MyApp.UserId?[]`, is converted element by element by the element's converters: a `MyApp.Email[]` column is read as a `string[]`, and each element given to `FromDatabase`.  A `NULL` element stays null, the converter not called for it, when the element is of a reference type or written with `?`, as `MyApp.UserId?`.  A `NULL` element of a value type written without `?`, as a `MyApp.Email[]` holds, throws `InvalidCastException`, "Column 'emails' holds a null element.".  The array is converted whole instead when a type override or a column type gives the array a type of yours that is not an array, `public.email[]=MyApp.EmailList`, or an array type that has converters of its own: those converters, between that type and `string[]`, are used, and the element's are not.

**Converters apply only where a type override, a column type or a `-- param:` marker chose the type, and only in query methods.**  A column or parameter whose type the type table gives is never converted, so a converter between `System.DateTimeOffset` and a type of yours changes no `timetz` column.  A query whose output is `models` calls no converter, and gets none of their errors at its name; to read it through Dapper, see [the recipe](recipes.md#models-through-dapper).  A type override to a type the driver reads for some database type, such as `string`, `System.DateTimeOffset` or `System.DateOnly`, is read and sent as the driver does when no converter is between it and the type the driver reads.

**Without a converter**, the query method asks the driver for the type that was chosen.  That is right for a type the driver was taught, a C# enum after Npgsql's `MapEnum<T>()` for one, a composite after `MapComposite<T>()` or a type of a plugin, and for a type it reads already, such as `System.DateTimeOffset` for a `timestamptz`.  A type of yours that the driver does not know, with no converter, throws the driver's `InvalidCastException` when the query runs.

<a id="setting-column-types"></a>

## Column types

```xml
<PropertyGroup>
    <SqlSourceColumnTypes>
        users.id=UserId
        posts.created_by_user_id=UserId
        billing.invoices.id=MyApp.Billing.InvoiceId
    </SqlSourceColumnTypes>
</PropertyGroup>
```

```sql postgres
-- column: posts.id -> PostId

-- name: ListPosts
-- column: total -> PostCount
-- param: @author -> UserId
SELECT p.id, p.created_by_user_id, count(*) AS total
FROM posts AS p
WHERE p.created_by_user_id = @author
GROUP BY p.id;
```

A column type gives one column of a table or view the C# type of your choice, in every query that reads it, where a [type override](#setting-type-overrides) gives every column of one database type the same C# type.  So `users.id` can be a `UserId` and `posts.id` a `PostId`, though both are `bigint`.  Above, `ListPostsDto` has `Id` a `PostId`, by the file header, `CreatedByUserId` a `UserId`, by the project, and `Total`, which no table gives, a `PostCount`, by its query's marker.  A parameter is no column of a table, so its [`-- param:` marker](parameters-and-tokens.md#param-marker) gives `ListPostsParams.Author` its type.  The C# type is written as a type override's is, a keyword, a full name or a short name: see [the type](#setting-type-overrides).

**The key is the column a value is read from**: `table.column`, which matches that column of a table or view of that name in any schema, or `schema.table.column`, which matches that schema's alone.  A query's column is read from a table's column when it is that column, under an alias or not, and not when it is an expression of it: `p.id AS post_id` is `posts.id`, and `count(*)`, `p.id + 1` and a column of a `UNION` are no table's column, so no key matches them.  Give such a column its type with a `-- column:` marker inside its query.  A column read from a view is the view's, so a view's columns need entries of their own: `users.id` does not reach the `id` of a view over `users`.

- **A part of a key is plain or in double quotes**, by the rules of a type override's key: a plain part holds letters, digits, `_` and `$` and is compared ignoring case, and a part in quotes holds any character, with a quote inside it doubled, is compared exactly, and may be written with escapes, `U&"..."`, its `&` written `&amp;` in a project file.  So `users.id` matches `Users.ID` too, and `"Users".id` the table `Users` alone.
- **A key serves both databases.**  A value in MSBuild maps the columns of PostgreSQL's queries and SQL Server's alike, and a marker those of its file.

**Four places, merged by key.**  The column types are the `SqlSourceColumnTypes` property, the `SqlSourceColumnTypes` metadata of a file's item, a `-- column:` marker in the file header, and a `-- column:` marker inside a query, which names a column of the query's result.  There is no attribute property.  For each column the query's marker wins, then the file header's entries, then the metadata's, then the property's, and as for the type override, an entry for a key replaces the entries of a place below for that key alone, and `default` takes the column out of the entries of every place below.  Of the entries that match one column, the one of the most specific place wins; within one place, one with a schema wins over one without, and then one spelled exactly as the column's names over one that matches it only ignoring case.

**Over the type override.**  A column's C# type is, in order: the column types, its query's `-- column: name -> type` first and then those of the other places by the column it is read from; then the type override, by its database type; then the type map.  So a column type at any place wins over a type override at any place: naming a column is more specific than naming a type.  `-- column: name -> default` frees one column of a query for the type override.  A column type never reaches a parameter: a parameter's C# type is its [`-- param:` marker](parameters-and-tokens.md#param-marker)'s, then the type override's, then the type map's.

**The nullability stays the column's**, as [which properties can be null](generator.md#model-nullability) decides it: `users.id=UserId` gives a column that can be null the type `UserId?`.

**What the type does.**  A type that a column type or a `-- param:` marker gives is one you chose, as a type override's is.  A [query method](generator.md#query-methods) reads and sends it through your [converters](#converters) when it has them, so a `UserId` over a `bigint` column is read as a `long` and given to `UserId`'s converter, and a PostgreSQL array mapped to an array of a type of yours, `posts.tag_ids=TagId[]`, is converted as [converters](#converters) says of arrays.  A type that the project cannot find or use is [SQLSRC250](diagnostics.md#sqlsrc250) to [SQLSRC252](diagnostics.md#sqlsrc252), whose message names the key, or the marker, and the place.

**In a property or metadata**, the value is a list of entries separated by spaces or line breaks, each `key=type` with no space around its `=`, by the type override's rules for white space, `<>`, `[]` and the double quotes of a key: see [in a property or metadata](#setting-type-overrides).  An entry that is not valid is [SQLSRC014](diagnostics.md#sqlsrc014), and the other entries of the value still apply.  Of two entries for one key in one value, the last wins, so a project can add to the value that a `Directory.Build.props` gives it: `$(SqlSourceColumnTypes) users.id=MyApp.UserId`.

**In the file header**, a `-- column:` line holds one entry, `key -> type` or `key -> default`, with or without spaces around its `->`, for every query of the file.  It gives a type and nothing else: a line without `->`, one that says `null` or `not null`, and one whose key has one part are [SQLSRC111](diagnostics.md#sqlsrc111).  Two lines that give one key two C# types are [SQLSRC112](diagnostics.md#sqlsrc112); the types are compared by how they are written, so `UserId` and `MyApp.UserId` are two.

**Inside a query**, a `-- column:` line names a column of the query's result, as the query returns it, and gives it a C# type after `->`: `-- column: total -> PostCount`.  It wins over the column types of the other places and over the type override, and `-> default` takes the column out of the column types, so that the type override, or else the type map, gives its type.  The same marker sets the column's nullability, before the `->` or alone: `-- column: note null -> NoteText`.  Its name is one part, so a name with a `.` outside quotes is [SQLSRC111](diagnostics.md#sqlsrc111): a column's source is mapped in the file header.  The rules of its name, of its nullability and of two markers for one column are on [the generator page](generator.md#column-marker).  In a file with no `-- name:` line, which is one query and takes both kinds, a key of two or three parts is the file header's kind, and a name of one part the query's.

The `sqlsource` tool does not read the property or the metadata, and a `-- column:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.

<a id="setting-sql-location"></a>

## Where the SQL goes

```csharp
[SqlSourceGenerate(Output = GeneratorOutput.Models, SqlLocation = SqlLocation.Direct)]
public static partial class UserQueries;
```

`SqlLocation` is a property of the attribute and nothing else.  It says where the members generated for the type's queries whose output is `sql` or `models` go.  Each member is a `const string`, or a static method when the query has tokens.  A query whose output is `codegen` has a method in their place, which [`MethodLocation`](#setting-method-location) places.

| `SqlLocation` | Generated members | Used as |
|----|----|----|
| `SqlLocation.Nested` (the default) | Public members in a `private static class` nested in the type, named `Sql` unless [the nested class](#setting-nested-class) names it | `Sql.GetUser`, inside the type only |
| `SqlLocation.Direct` | Public members on the type itself | `UserQueries.GetUser`, wherever the type is visible |

With `Nested`, a type that already has a member with the nested class's name is [SQLSRC007](diagnostics.md#sqlsrc007): rename it, give the nested class another name, or use `Direct`.  The members are described on [the generator page](generator.md).

<a id="setting-nested-class"></a>

## Name of the nested class

```xml
<PropertyGroup>
    <SqlSourceNestedClass>Queries</SqlSourceNestedClass>
</PropertyGroup>
```

```csharp
[SqlSourceGenerate(Output = GeneratorOutput.Models, NestedClass = "Statements")]
public partial class UserRepository
{
    public string Get() => Statements.GetUser;
}
```

Under `SqlLocation.Nested`, this names the private class that holds the type's members, those of its queries whose output is `sql` or `models`.  `NestedClass` on the attribute names it for one type, and the `SqlSourceNestedClass` property for every type of the project that does not set it.  Without either, the class is `Sql`.  The setting has no item metadata and no marker, since a type's queries can come from several files.

The value is one C# identifier that is not a keyword: `Queries` is fine, and `class`, `My-Sql` or `App.Sql` is not.  A value that is not valid is [SQLSRC006](diagnostics.md#sqlsrc006) on the attribute, and [SQLSRC014](diagnostics.md#sqlsrc014) as the property.  Under `SqlLocation.Direct` there is no nested class, and the setting has no effect, even on the same attribute.

<a id="setting-method-location"></a>

## Where the methods go

```csharp
[SqlSourceGenerate(MethodLocation = MethodLocation.Internal)]
internal static partial class UserQueries;
```

`MethodLocation` is a property of the attribute and nothing else, as `SqlLocation` is.  It says where the [methods](generator.md#query-methods) that run the type's queries, those whose output is `codegen`, go.

| `MethodLocation` | The methods are | Called as |
|----|----|----|
| `ExtensionClass` (the default) | In a generated static class beside the type, named after it with `Extensions` after it, as extension methods of `DbConnection` | `connection.GetUser(...)` |
| `Public` | On the type, as public static methods | `UserQueries.GetUser(connection, ...)` |
| `Internal` | On the type, as internal static methods | `UserQueries.GetUser(connection, ...)` |
| `Private` | On the type, as private static methods | `GetUser(connection, ...)`, inside the type only |

There is no property, item metadata or marker for it.  `SqlSourceMethodLocation` in a project file or `-- method-location:` in a `.sql` file is not read.  The class and its accessibility, and the names a method cannot have, are on [the generator page](generator.md#method-location).

<a id="setting-path"></a>

## Path

```csharp
[SqlSourceGenerate]                                // every .sql file beside this C# file
public partial class UserRepository;

[SqlSourceGenerate(Path = "Queries/Users.sql")]    // one file
public partial class UserQueries;

[SqlSourceGenerate(Path = "../Queries")]           // the .sql files of a folder
public partial class SharedQueries;
```

`Path` is a property of the attribute and nothing else.  It says which `.sql` files belong to the type.

- Without `Path`, the type gets every `.sql` file in the folder of the C# file that carries the attribute.  Subfolders are not searched.
- A `Path` that ends in `.sql` names one file.  Any other `Path` names a folder, whose own `.sql` files the type gets.
- `Path` is relative to the folder of the C# file, never to the project.  Both `/` and `\` separate folders.  Paths are compared ignoring case, so a project builds the same on every operating system: two `.sql` files of a type whose paths differ only by case are [SQLSRC013](diagnostics.md#sqlsrc013).
- An empty `Path`, `Path = ""`, is the same as no `Path`.  A `Path` of white space is not: it is read as the name of a folder, like any other `Path`, and so matches nothing.
- A `Path` that matches nothing is [SQLSRC004](diagnostics.md#sqlsrc004), and a folder with no `.sql` file when there is no `Path` is [SQLSRC005](diagnostics.md#sqlsrc005).
- Two types may use the same file.  A `.sql` file that no type uses is ignored, so a folder of migration scripts elsewhere in the project does no harm.

This is separate from [which files the compiler is given](#included-files), which comes first: a type can only claim a file that the project includes.  The rest of the attribute is on [the generator page](generator.md).

<a id="configure-a-folder"></a>

## Configure one folder differently

A project reads its `.sql` files as PostgreSQL and keeps the comments of its queries.  One folder, `Legacy/`, holds MySQL files, and their queries take token fragments that are sometimes empty, so the folder turns the token check off.  Item metadata gives a folder its own settings:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
    <SqlSourceGeneratorParameters>keep-comments</SqlSourceGeneratorParameters>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Legacy/**/*.sql"
        SqlSourceDialect="mysql"
        SqlSourceGeneratorParameters="keep-comments no-token-validation" />
</ItemGroup>
```

Every file under `Legacy/` is read as `mysql`, keeps its comments, and skips the token check.  Every other file is untouched: it is read as `postgres`, keeps its comments, and checks its tokens.  Three things to notice:

- The folder's list says `keep-comments` again.  A list replaces the project's whole, so a folder that wants what the project asks for repeats it.  Without it, the files of `Legacy/` would lose their comments.
- The dialect is a value of its own too.  Had the project set `mysql,ansi-quotes` as its dialect, this folder would still be read as plain `mysql`.
- A file's own marker still wins: a `-- generator: default` or a `-- dialect: mariadb` in one of the files overrides both lines above.  See [which place wins](#precedence).

`Update` can stand anywhere in your project file, since the package has already listed the `.sql` files.  A project that [lists its own files](#included-files) gives the metadata where it lists them:

```xml
<ItemGroup>
    <AdditionalFiles Include="Legacy/**/*.sql" SqlSourceDialect="mysql" />
</ItemGroup>
```

Only the settings whose row in [the tables](#all-settings) lists metadata can be given this way.  To change a setting for the files of one type, use the type's attribute.

<a id="included-files"></a>

## Which `.sql` files are included

The package hands every `.sql` file under the project's folder to the compiler, as an `AdditionalFiles` item: that is how a source generator sees a file that is not C#.  The output and intermediate folders are left out, and so are folders whose names start with a dot, as the SDK leaves them out of your C# files.  The `sqlsource` tool reads the same list of `.sql` files.  A `.sql` file outside the project's folder is not included; list it, and its sidecar, yourself.

The package hands the [sidecar](concepts.md#sidecar) beside a `.sql` file to the compiler too, the file `Users.sql.json` for `Users.sql`, under the same rules.  The build reads the sidecar of each `.sql` file that a type claims, and reports a query that must be described and has no current entry in it: see [Output](#setting-output).

To leave some files out:

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

To turn the default off and list the files yourself:

```xml
<PropertyGroup>
    <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Include="Queries/**/*.sql" />
    <AdditionalFiles Include="Queries/**/*.sql.json" />
</ItemGroup>
```

Turning the default off turns it off for sidecars too, so list them as well, as the example does.  A file the project includes is only handed to the compiler.  Which of them belong to a type is the type's [`Path`](#setting-path), and a file no type claims is ignored.

<a id="values-written-in-msbuild"></a>

## Values written in MSBuild

The package removes the white space around the value of each property and each item metadata in [the tables](#all-settings), and makes one space of each run of white space inside it, a line break among it.  So a value may stand on a line of its own, and a value of several words may be written over several lines:

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>
        keep-comments
        no-token-validation
    </SqlSourceGeneratorParameters>
</PropertyGroup>
```

A line break inside one word puts a space there.  `App.` and `Models` on two lines are `App. Models`, which is not a namespace and is reported as [SQLSRC014](diagnostics.md#sqlsrc014).

In a [type override](#setting-type-overrides), and in the [column types](#setting-column-types), white space separates the entries, so an entry has no space around its `=`.  Inside the double quotes of a key, too, a run of white space is made one space, and a value is cut at a `;` or a `#`, as the next paragraph says.  So write such a character of a schema or a name with an escape, in [the form with escapes](#setting-type-overrides) of a part in quotes.  A line break, a tab or any other white space but a single space, a control character, the second of two spaces, a `;` and a `#` are each written `\` and four hex digits: `\0009` for a tab, `\0020` for the second of two spaces, `\003B` for `;`, `\0023` for `#` and `\000A` for a line break, which a `-- type-override:` marker needs too, since its line ends there.  When such a type has no C# type, the build's error gives its key written so.

**Do not use `;` or `#` in a value.**  The compiler hands a generator only the part of a value before the first `;` or `#`, and nothing reports the rest.  `keep-comments;no-token-validation` is read as `keep-comments`.  Separate words with spaces or line breaks.

<a id="known-issues"></a>

## Known issues

- **A `.sql` file that one of your own build targets adds is not fully seen.**  This is about a target in your project that adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as one that writes a file or copies it in.  The build compiles the file, but two things go wrong.  `sqlsource` leaves out the file's queries, so it describes none of them, and the build reports [SQLSRC241](diagnostics.md#sqlsrc241) for each that must be described.  And the settings you gave the file as item metadata, its dialect for one, are dropped when the target runs late in the build, so the file is read with the project's settings.  To make both work, tell your target to run before the package reads its settings, wherever the target is declared:

  ```xml
  <Target Name="AddGeneratedSql" BeforeTargets="SqlSourceTrimMetadataOfFiles">
      <ItemGroup>
          <AdditionalFiles Include="obj/generated/*.sql" SqlSourceDialect="postgres" />
      </ItemGroup>
  </Target>
  ```

  Change the name, the path and the metadata to your own.  `BeforeTargets="SqlSourceTrimMetadataOfFiles"` is the line that matters.
- **A type override's key cannot be written in brackets.**  T-SQL writes a name `[dbo].[Email]`, and a key written so is not valid: [SQLSRC014](diagnostics.md#sqlsrc014) in a property or metadata, and [SQLSRC111](diagnostics.md#sqlsrc111) in a marker.  Write `dbo.Email`, which matches the type's name in any case, or `"dbo"."Email"`, which matches its case exactly; the message shows both.  See [type overrides](#setting-type-overrides).
- **A C# type that is generic can give its model a warning for a `?` inside a type argument**, in a type override, a column type or a `-- param:` marker.  A type argument written with a `?` where a constraint forbids null is [SQLSRC252](diagnostics.md#sqlsrc252) at the query, but a `?` inside a type argument is not.  An override such as `MyApp.Names<System.Collections.Generic.List<string?>>`, where `Names<T>` requires `where T : System.Collections.Generic.IEnumerable<string>`, is accepted, and the generated model gets the compiler's warning CS8631.  Give the type arguments the nullability that the constraints ask for.  See [type overrides](#setting-type-overrides).
- **A type made by Vogen or StronglyTypedId needs converters written for it.**  SqlSource converts a type only through methods marked `[SqlSourceConverter]`, and never through a type's `Value` property and its `From` method or constructor, so such a type with no converter is given to the driver as it is, which throws `InvalidCastException`.  Write the two converters in a class of your project, each calling the type's own members:

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

  See [converters](#converters).
- **A type has one conversion in a project.**  The converters between a C# type and the type the driver reads serve every column and parameter of that C# type, whatever chose it, and no file or query can choose others for the same two types.  To convert a database type, or a column, another way in one file or query, give it there a C# type of its own, with its own converters.  See [converters](#converters).
