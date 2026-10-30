# Settings

A setting is a choice you make for a whole project, for some of its files or for one query: which dialect the SQL is written in, which comments to keep, which database the tool asks about a query.  Every setting is defined here, with the places it can be written and the one place that wins when it is written in several.

```sql
-- dialect: postgres
-- generator: keep-comments

-- name: ListUsers
-- output: sql
SELECT id, name FROM users;
```

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "generatorParameters": ["keep-comments"],
}
```

```csharp
[SqlSourceGenerate(Path = "Reports", SqlLocation = SqlLocation.Direct)]
internal static partial class Reports;
```

The first block writes settings as markers in a `.sql` file, the second in a [settings file](#settings-files), `sqlsource.json`, which applies to the `.sql` files of its folder and of every folder below it, the third as properties of the attribute.  Which of the three a setting accepts is in [the tables](#all-settings).  To read one folder of a project differently from the rest, see [configure one folder differently](#configure-a-folder).

<a id="all-settings"></a>

## All settings

Each name links to its section, which gives its values and its rules.  **Written as** lists every place the setting can be written: a key of a [settings file](#settings-files), a property of the attribute, a marker in the **file header** (the part of a `.sql` file before its first `-- name:` line), and a marker **inside a query** (under a `-- name:` line), as [the marker index](sql-files.md#marker-index) shows.  A place it does not list cannot hold it.

### Settings that work today

| Setting | Values | Written as | Status |
|----|----|----|----|
| [Output](#setting-output) | `sql`, `models`, `codegen`.  Default `codegen` | `output` in a settings file; `Output` on the attribute; `-- output:` in the file header and inside a query | Works today |
| [Database](#setting-database) | A name of letters, digits, `-`, `_` and `.`.  Default: the name of the file's dialect | `database` in a settings file; `-- database:` in the file header and inside a query | Works today, for the tool alone |
| [Dialect](#setting-dialect) | A dialect's name, with options for `mysql` and `mariadb`: see [the dialects page](dialects.md#choosing-a-dialect).  Default `ansi` | `dialect` in a settings file; `-- dialect:` in the file header | Works today |
| [Generator parameters](#setting-generator-parameters) | Words separated by spaces, or an array of them in a settings file: `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix`, `no-array-pool`, `default`.  Default: none | `generatorParameters` in a settings file; `Parameters` on the attribute; `-- generator:` in the file header and inside a query | Works today |
| [Input and output model suffix](#setting-model-suffix) | Characters of a C# identifier.  Defaults `Params` and `Dto` | `inputModelSuffix`, `outputModelSuffix` in a settings file; `InputModelSuffix`, `OutputModelSuffix` on the attribute; `-- input-model-suffix:`, `-- output-model-suffix:` in the file header | Works today |
| [Model namespace](#setting-model-namespace) | A namespace.  Default: the namespace of the type | `modelNamespace` in a settings file; `ModelNamespace` on the attribute; `-- model-namespace:` in the file header | Works today |
| [Input and output model type](#setting-model-type) | `record`, `sealed record`, `class`, `sealed class`.  Default `sealed record` | `inputModelType`, `outputModelType` in a settings file; `InputModelType`, `OutputModelType` on the attribute; `-- input-model-type:`, `-- output-model-type:` in the file header and inside a query | Works today |
| [One query's model name](#setting-model-name) | A type's name, or its full name | `-- input-model:`, `-- output-model:` inside a query | Works today |
| [Type overrides](#setting-type-overrides) | Entries `"key": "type"`, each a database type and the C# type it gets, or `"key": "default"`.  Default: none | `typeOverrides` in a settings file; `-- type-override:` in the file header and inside a query | Works today |
| [Column types](#setting-column-types) | Entries `"key": "type"`, each a column of a table or view, `table.column` or `schema.table.column`, and the C# type it gets, or `"key": "default"`.  Default: none | `columnTypes` in a settings file; `-- column:` in the file header, and inside a query for a column of the query's result | Works today |
| [Library types](#setting-library-types) | Entries `"library": "state"`, the library `nodaTime` and the state `on`, `off` or `default`.  Default: on for PostgreSQL where the project references `Npgsql.NodaTime`, and off otherwise | `libraryTypes` in a settings file; `-- library-types:` in the file header and inside a query | Works today |
| [`SqlLocation`](#setting-sql-location) | `Nested`, `Direct`.  Default `Nested` | `SqlLocation` on the attribute | Works today |
| [Nested class](#setting-nested-class) | A C# identifier that is not a keyword.  Default `Sql` | `nestedClass` in a settings file; `NestedClass` on the attribute | Works today |
| [`Path`](#setting-path) | A folder, or one `.sql` file, relative to the folder of the C# file.  Default: that folder | `Path` on the attribute | Works today |
| [Collection type](#setting-collection-type) | A collection interface or type, such as `IReadOnlyList` or `List`, or `IAsyncEnumerable` to stream the rows: see [its section](#setting-collection-type).  Default `Array` | `collectionType` in a settings file; `CollectionType` on the attribute; `-- collection-type:` in the file header and inside a query | Works today |
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
| 4 | The nearest settings file that sets it | `"output": "models"` in `Reports/sqlsource.json` |
| 5 | Each farther settings file, nearest first | `"output": "sql"` in the `sqlsource.json` of the project's folder |
| 6 | The default | `codegen` |

A query with `-- output: sql` has the output `sql`, whatever the other five say.  A query without it takes the file header's `models`, and so on down the table.  The settings files of a `.sql` file are the `sqlsource.json` of its folder and those of the folders above it, nearest first, up to the first that says `"root": true`: see [settings files](#settings-files).  To give the files of one folder their own values, see [configure one folder differently](#configure-a-folder).

The rules:

- **Each setting is decided on its own.**  A query that writes `-- output: sql` still takes its file header's `-- database:`.
- **A place that gives no value is skipped.**  A settings file that does not name the setting gives none, and so does a string of a settings file or of the attribute that is empty or white space.  `Path` is the exception: see [Path](#setting-path).
- **A setting has only the places its row of [the tables](#all-settings) lists.**  A key of a settings file that is not in them, nor `root` or `$schema`, is [SQLSRC016](diagnostics.md#sqlsrc016).  A marker where its row says it cannot stand is [SQLSRC116](diagnostics.md#sqlsrc116), except a `-- dialect:` inside a query or after SQL, which is [SQLSRC115](diagnostics.md#sqlsrc115): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).
- **The attribute belongs to one type.**  Two types that claim one file each use their own attribute, and each gets its own result.

Four exceptions, stated plainly:

- **A list of generator parameters replaces the one below it whole.**  It never adds to it.  See [generator parameters](#setting-generator-parameters).
- **A dialect value replaces the one below it whole**, options included.  The dialect has no attribute and no in-query level: a file has one dialect, which is the marker in its file header, else the nearest of its settings files that sets one, else `ansi`.  See [dialect](#setting-dialect).
- **`SqlLocation`, `MethodLocation` and `Path` exist only on the attribute.**  They have nothing to choose between.  The nested class has two, the attribute over the settings files, which are those of the folder of the type's C# file.
- **A type override, a column type, or a library of the library types, replaces only the keys it names.**  Each place adds its entries to those of the places below it, each settings file a place of its own, so a folder's file adds to the entries of the files above it.  An entry for a key, a database type, a column or a library, replaces the entry below it for that key, and `default` takes the key out.  None of them has an attribute level.  See [type overrides](#setting-type-overrides), [column types](#setting-column-types) and [library types](#setting-library-types).

<a id="a-value-that-is-wrong"></a>

### A value that is wrong

| Written as | Reported as | What happens |
|----|----|----|
| A marker | [SQLSRC111](diagnostics.md#sqlsrc111), a dialect included.  A generator parameter that does not exist is [SQLSRC109](diagnostics.md#sqlsrc109); a value that contradicts another in the same place is [SQLSRC112](diagnostics.md#sqlsrc112) | The file produces no members until it is fixed |
| A settings file | [SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC020](diagnostics.md#sqlsrc020), at the file, line and column of the mistake: see [settings files](#settings-files) | A value that is wrong is not set by that file, so a farther settings file or the default gives it, for a dialect too.  The rest of the file still applies, and so do the other words of a list, unless one is `default` beside another, and the other entries of `typeOverrides`, `columnTypes` or `libraryTypes`.  Of a key, or an entry, written twice, the first applies.  A file that is not a JSON object gives nothing |
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
- It can be a marker in the file header or inside a query, or `database` in a settings file.  It has no attribute property.
- One name is one database for a whole run, with one connection and one dialect.  Two files that give one name two dialects are [SQLSRC211](diagnostics.md#sqlsrc211).
- Only the tool reads this setting.  The build does not, so a wrong `database` in a settings file is reported by the tool and not by the build.

To give the tool a connection for a name, see [the command reference](tool.md#command-reference).

<a id="setting-dialect"></a>

## Dialect

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

```jsonc
// Reporting/sqlsource.json
{
    "dialect": "mssql",
}
```

```sql mysql
-- dialect: mysql

-- name: FindByNote
SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
```

The [dialect](concepts.md#dialect) is the set of rules by which SqlSource reads a `.sql` file.  It does not connect to anything.  The default is `ansi`.  The dialect names, the options of `mysql` and `mariadb`, and what each dialect changes are on [the dialects page](dialects.md).

- **Three places.**  A `-- dialect:` marker in the file, else the `dialect` of the nearest settings file of the file's folder, or of a folder above it, that sets one, else `ansi`.  Above, the files under `Reporting/` are read as `mssql`, and every other file of the project as `postgres`.
- **A file has one dialect.**  The marker goes in the file header, before the first SQL, and not inside a query: anywhere else it is [SQLSRC115](diagnostics.md#sqlsrc115).  It takes effect on the line after it, so comments above it are read by the dialect that the settings files give.
- **A value replaces the one below it whole.**  A file with `-- dialect: mysql` under a settings file that sets `"mysql,ansi-quotes"` is read as plain `mysql`, and so is a folder whose settings file sets `"mysql"` under one that sets `"mysql,ansi-quotes"`.
- **A value that is not a dialect** is [SQLSRC111](diagnostics.md#sqlsrc111) in a marker and [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file.  The files under a settings file whose `dialect` is wrong are read meanwhile by the dialect of a farther settings file, or as `ansi` when none sets one.
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

A [generator parameter](concepts.md#generator-parameter) is a word that changes what SqlSource does with a query.  A list of them is written in a `-- generator:` line, in `Parameters` on the attribute, or in `generatorParameters` in a settings file.  Each parameter is a departure from a default, so a query with no list gets every default.

| Parameter | Effect |
|----|----|
| `keep-comments` | Comments and blank lines stay in the generated SQL: see [what reaches the generated SQL](sql-files.md#generated-sql) |
| `no-token-validation` | The method of a query with tokens does not check its arguments: see [validation](parameters-and-tokens.md#token-validation) |
| `sort-input`, `sort-output` | The properties of the query's input model, or of its output model, are sorted by name: see [the order of the properties](generator.md#property-order) |
| `no-table-models` | A query that reads a whole table gets a row model named after the query, not after the table: see [table models](generator.md#table-models) |
| `async-method-suffix` | The name of a query's [method](generator.md#method-signature) ends with `Async`: `GetUserAsync` for `GetUser` |
| `no-array-pool` | A [`many`](generator.md#method-shapes) method that returns a collection reads its rows into buffers borrowed from .NET's shared array pool, unless it returns an `ImmutableList<T>` or an `IImmutableList<T>`.  Once the pool holds buffers of the sizes it needs, a method that reads through the pool allocates no collection for a result of up to about 1,000 rows but the one it returns.  Under `no-array-pool`, every such method reads its rows into a collection that it allocates and grows, as hand-written code usually does, and as a method that returns one of the two immutable lists always does.  Use it to keep the pool out of a project, or to compare the two.  A method that [streams its rows](#streaming-rows) holds no collection, so it is the same with or without `no-array-pool` |
| `default` | The empty list: every default.  `default` must be the only word in its list, though it may be repeated |

Words are matched ignoring case.

**A list replaces; it never adds.**  In the example, `Total` has the list `no-token-validation` and nothing else: it does not keep its comments, though the file header says `keep-comments`.  A query that wants both writes both, `-- generator: keep-comments no-token-validation`.  `-- generator: default` gives a query every default, whatever its file says.

The list for a query is the first of these that gives one, whole:

1. The `-- generator:` lines inside the query.
2. The `-- generator:` lines in the file header.
3. `Parameters` on the attribute of the type that claims the file.
4. `generatorParameters` in the nearest settings file of the file's folder, or of a folder above it, that sets it.
5. `generatorParameters` in each farther settings file, nearest first.

Several `-- generator:` lines in one place are one list: two lines in a query add up.  A list never adds to the list of another place.

On the attribute the list is words separated by spaces or line breaks.  In a settings file it is an array of strings, one word in each: `"generatorParameters": ["keep-comments", "no-token-validation"]`.  `[]` is the empty list, every default, and so is `["default"]`; an element that is empty or white space is skipped.  `default` beside another word makes the whole value wrong: [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file, whose list is then not set, so a farther one applies, and [SQLSRC006](diagnostics.md#sqlsrc006) on the attribute.  A word that is not a generator parameter is reported the same way.  In a settings file the other words of the list still apply, and a list that has words, none of them a generator parameter, is not set, so a farther one applies; on the attribute the type gets no members until the value is fixed.  In a marker an empty `-- generator:` line is [SQLSRC110](diagnostics.md#sqlsrc110), and `default` beside another word in one place is [SQLSRC112](diagnostics.md#sqlsrc112).

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
- It stands in the file header only, not inside a query.  It can also be `inputModelSuffix` or `outputModelSuffix` in a settings file, or a property of the attribute.
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
- It stands in the file header only.  It can also be `modelNamespace` in a settings file, or `ModelNamespace` on the attribute.
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

- It stands in the file header and inside a query, as two markers.  It can also be `inputModelType` or `outputModelType` in a settings file, or a property of the attribute: `InputModelType = GeneratorModelType.Class`.
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

- It stands inside a query only.  It has no key in a settings file and no attribute property, since a name is for one query.
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
| `IAsyncEnumerable` | `IAsyncEnumerable<T>` | Nothing: each row is read as the loop asks for it |

`T` is the query's output model.  Every collection but `IAsyncEnumerable` is filled before the method's task completes, `IEnumerable` included, so reading it runs no query.

- It stands in the file header and inside a query.  It can also be `collectionType` in a settings file, or `CollectionType = GeneratorCollectionType.IReadOnlyList` on the attribute.
- It changes nothing for a query whose shape is not `many`, and nothing under the output `sql` or `models`.

<a id="streaming-rows"></a>

### Streaming rows

```sql postgres
-- name: StreamUsers
-- collection-type: IAsyncEnumerable
SELECT id, name FROM users ORDER BY id;
```

```csharp
// Generated, shown without global::
public static IAsyncEnumerable<StreamUsersDto> StreamUsers(
    this DbConnection connection,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)

// Your code
await foreach (var user in connection.StreamUsers(cancellationToken: cancellationToken))
{
    await writer.WriteLineAsync(user.Name);
}
```

The method of a `many` query whose collection type is `IAsyncEnumerable` returns `IAsyncEnumerable<T>`, and gives your loop each row as it is read.  So the loop starts on the first row while the rest are still to come, and the rows are never all held at once.  The method takes the arguments that every [query method](generator.md#method-signature) takes, and its name ends with `Async` under `async-method-suffix`, as every method's does.

- **The call checks the arguments and runs nothing.**  A null `connection` or `parameters` throws when the method is called, and so does a token's text that is blank, unless [`no-token-validation`](parameters-and-tokens.md#token-validation) is in effect.  The connection is opened, the values of the `parameters` model read and the query sent when the loop asks for the first row, not at the call, and each loop over the result runs the query again with the values the model holds then.  An enumerator that is disposed before it asks for a row opens nothing.  On SQL Server, a transaction that has completed is refused when the loop asks for the first row, not at the call, and the connection is not opened: see [what a method reads and throws](sqlserver.md#method-results).
- **The connection stays open until the loop ends.**  A closed connection is opened for the loop and closed again when the loop ends: when it finishes, at a `break`, at an exception in its body, or when the enumerator is disposed.  An open connection stays open.  While the loop runs, the connection is busy with the query, so another command on it in the loop's body is refused by the driver: Npgsql throws `NpgsqlOperationInProgressException`, and Microsoft.Data.SqlClient, on a connection without `MultipleActiveResultSets`, `InvalidOperationException`.  Run such a command on another connection, or read the rows into a collection first.
- **Leaving the loop early reads the rest of the rows.**  At a `break`, or an exception in the loop's body, the rows still to come, and on SQL Server the statements after them, are read and discarded before the loop ends, so on a large result that takes as long as reading them.  The query is not cancelled, and a transaction is left as it was, unless the database raises an error for one of those rows, such as a division by zero, or SQL Server for one of those statements.  That error is thrown when the loop ends, whatever ended it: at the `break`, and in place of an exception that the loop's body threw, so that an error in the query's rows, which may have changed your transaction, always reaches your code.  On PostgreSQL it also aborts a transaction that the query ran in, as any error does, so the transaction's next command is refused until it is rolled back; on SQL Server it may have rolled the transaction back, depending on the error and on `XACT_ABORT`: see [what a method reads and throws](sqlserver.md#method-results).  To keep the body's exception, catch it in the loop's body, keep it in a variable and `break`.  Around the loop, catch the database's exception, `PostgresException` or `SqlException`, and throw the exception you kept in its place if there is one, or else throw the database's exception again.  After the loop, throw the exception you kept if there is one, since a `break` with no later row failing ends the loop with no exception at all.  Or have the query return only the rows the loop needs, with a `WHERE` or a `LIMIT` or `TOP`, so that no row is left to fail.
- **Each row is read as every `many` method reads it**, with the same checks and [converters](#converters).  An error at a row that the loop reads, such as a null in a column whose property cannot be null, or an error that the database raises for that row, is thrown by the loop when it reaches that row, after the rows before it: see [how a row is read](generator.md#reading-rows).  A row that the loop leaves unread is not checked or converted: of its errors, only one that the database raises can reach your code, as the item above says.  On SQL Server, when such a check or converter throws for a row in a transaction, and an error that the rest of the query raises then rolls the transaction back, the loop throws `TransactionRolledBackException` with that exception as its `InnerException`: see [what a method reads and throws](sqlserver.md#method-results).
- **Either token cancels.**  The method's `cancellationToken`, and a token given to `WithCancellation`, each cancel the loop, which throws what the driver throws when a method is cancelled: see [PostgreSQL](postgresql.md#method-results) and [SQL Server](sqlserver.md#method-results).
- **On SQL Server, a query of several statements** gives the rows of its first result set, and its other statements run before the loop ends.  An error that one of them raises is thrown after the last row, or at a `break`, as the item above says, and depending on the error and on `XACT_ABORT` it may have rolled back the transaction the query ran in: see [what a method reads and throws](sqlserver.md#method-results).

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

A type override gives a database type the C# type of your choice, in place of the one that the type map of [PostgreSQL](postgresql.md#type-map) or [SQL Server](sqlserver.md#types) gives it: a `timestamp with time zone` as a `System.DateTimeOffset`, a `numeric` as a `double`, an enum of your schema as a C# enum.  Each entry is a key and a type, `"key": "type"` in a settings file and `key=type` in a marker: the key names the database type, and the type is the C# type that every column and parameter of that database type gets.  The type `default` gives the database type back to the type map.

In the example, every query of the project gets `timestamptz` as a `System.DateTimeOffset`, `public.user_status` as `MyApp.UserStatus`, and `int4[]` as a list.  The files under `Legacy/` get `date` as a `System.DateTime` too.  Every query of the `.sql` file gets `numeric` as a `double`, and `ListEvents` gets `timestamptz` from the type map, a `System.DateTime`, while it keeps the project's other entries.

To give one column, or one parameter, a C# type of its own, where two columns of one database type need two C# types, see [column types](#setting-column-types) and [`-- param:`](parameters-and-tokens.md#param-marker).  Either wins over the type override.

Each property of a generated [model](generator.md#models) has the C# type of its column or parameter, the type override's included.  A [query method](generator.md#query-methods) reads and sends a column or parameter as that type, through your [converters](#converters) when the type has them.

**Every place, merged by type.**  The type override is `typeOverrides` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- type-override:` marker in the file header and inside a query.  It has no attribute property.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a type replaces the entry of a place below for that type alone.  A settings file's entries reach only the `.sql` files of its folder and the folders below it.  An entry replaces the entry below for its key however that key is spelled, and its own spelling is the one kept: a query's `public."Status"=MyApp.Status` over `"public.Status": "MyApp.Status"` in the project's settings file leaves a type `status`, which the project's entry mapped too, to the type map, since the quoted key maps `Status` alone.  So a query that needs one type otherwise names that type, and keeps the rest of the project's entries.  This is the exception to the rule that the most specific place wins whole: see [which place wins](#precedence).  `default` takes the type out of the entries of every place below, and a key with no entry anywhere has the type map's C# type.  Nothing removes every entry at once.

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
- **A name that both databases have is one key, and maps each database's own type of that name.**  A settings file's `"int": "long"` maps PostgreSQL's `int4` and SQL Server's `int`, and `"date": "System.DateOnly"` the `date` of both.  Some such names are different types: `bit` is a bit string on PostgreSQL and a `bool` on SQL Server, `timestamp` a time on PostgreSQL and a row version on SQL Server, and `char` PostgreSQL's one-byte type and SQL Server's string.  In a project with files of both, an entry that is meant for one database's type is written with that database's system schema, `pg_catalog.timestamp` or `sys.timestamp`, or set in a settings file of the folder that holds that database's files, as [configure one folder differently](#configure-a-folder) shows.  A marker is read by its file's dialect alone, so it never reaches the other database's type.
- **A part of a key, its schema or its name, is plain or in double quotes.**  A plain part holds letters, digits, `_` and `$`.  A part in quotes holds any character, with a quote inside it doubled, as SQL writes a name: `"my schema"."order-status"`, `public."say ""hi"""`.  A `.` stands between the schema and the name, and `[]` after an array's.  Put a name in quotes when it has a capital letter, or any character that a plain part does not hold, white space included.  A part in quotes may be written with escapes, as PostgreSQL writes a name: `"my schema".U&"Order\000AItem"` names a type whose name is `Order`, a line break and `Item`.  Inside `U&"..."`, `\` and four hex digits, or `\+` and six, is the character of that code, from 1 to 10FFFF, and a character beyond FFFF is one `\+` escape or the escapes of its two surrogates; `\\` is a backslash, and a quote is doubled.  It is the same part as the quotes of its text, so `public.U&"st\0061tus"` is `public."status"`.  `UESCAPE` is not read, and an escape that is not one of these makes the key not valid.
- **A part in quotes is compared exactly, case included, and a plain part ignoring case.**  So `public."Status"` maps the type `Status` alone, while `public.Status` maps `Status` and `status` alike.  `public.Status` and `public.status` are two keys.  `public.status` and `public."status"` are one key, and so are `public.Status` and `public."Status"`: two entries for them are two entries for one type.
- **Of the entries for one type whose keys differ only by case, the most specific place's wins, as for any type.**  Among the entries of one place, the one whose key is spelled exactly as the type's name wins over one that matches it only ignoring case, so in one place `public.Status` and `public.status` each map their own type.  A plain key at a higher place replaces the entries below for every type it matches: a query's `public.status=default` takes both `Status` and `status` out of the project's entries.  To change one of two types whose names differ only by case, quote it: a query's `public."status"=default` takes `status` alone out, and `Status` keeps the project's entry.

**The type** is a C# type that a property can have:

- **A keyword**, `int`, `string`, `double` and the others of the built-in types; **a full name from its namespace**, `System.DateTimeOffset` or `MyApp.Ids.UserKey`, which may start with `global::`; or **a short name**, without its namespace, `DateTimeOffset` or `UserKey`.  A nested type is written with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.
- **A short name is the one type of that name** and number of type arguments, in any namespace, that your project and its references have and your project can see: a type of a reference counts when it is `public`, or `internal` in a reference that gives your project its internals with `InternalsVisibleTo`.  A type nested in another, and a `file` type, are not found by one.  Your project's own type wins over a reference's of the same full name.  The generated code writes the type's full name, so the `using` lines of your files do not matter, and a short name and the full name of its type are one type, to [shared models](generator.md#shared-models) and to converters.  A type argument may be a short name too, in any name: `System.Collections.Generic.List<UserKey>`, or `List<UserKey>`.  `global::UserKey` is a type of the global namespace alone.  A short name that finds a type of an assembly your project has only through a package it does not reference itself is the warning [SQLSRC284](diagnostics.md#sqlsrc284), since it may not be the type you meant; write the full name to use it without the warning.
- **A generic type** names its type arguments, each of which may end in `?`: `System.Collections.Generic.List<int?>`.  White space inside `<>` and `[]` is allowed.
- **An array** is the element and its ranks: `long[]`, `int?[]`, `int[,]`.
- **No `?` at the end of the whole type.**  Whether a property can be null is decided by its column, by which columns can be null on [PostgreSQL](postgresql.md#nullability) and on [SQL Server](sqlserver.md#nullability), or by its parameter's [`-- param:` marker](parameters-and-tokens.md#param-marker), so `int4=int?` is not valid.  A tuple, `dynamic`, `void`, a pointer, and anything after the type are not valid either.
- **The type must exist and be usable.**  A type that the project and its references do not have, a full name that two references have, or a short name of two types in two namespaces, is [SQLSRC250](diagnostics.md#sqlsrc250), which then names them; one the project cannot see is [SQLSRC251](diagnostics.md#sqlsrc251); and one that no property can have, such as a `static` class, `System.Nullable<int>`, a type marked `[Obsolete]` as an error or a generic type whose type argument breaks a constraint, as `System.Collections.Generic.List<System.Nullable<string>>`, is [SQLSRC252](diagnostics.md#sqlsrc252).  Each is reported at the name of a query whose output is `models` or `codegen` and whose entry in its sidecar is current, once for the query: when the first of its columns and parameters that has no C# type, parameters before columns, is one whose type, or whose array's element or domain's base, has the entry's key.  An entry that no query uses is never an error, so a project can set its entries before it has the queries.

**In a settings file**, `typeOverrides` is a JSON object with a member for each entry: its key is the database type and its value the C# type, `"timestamptz": "System.DateTimeOffset"`.  The white space around the key and around the type is ignored.  White space inside them is read as it is written, so a space inside `<>` or `[]`, `"System.Collections.Generic.Dictionary<string, int>"`, or inside the double quotes of a key, `"my schema"`, is part of the entry.  The key is a JSON string, so each `"` of it is written `\"` and each `\` is written `\\`, as JSON requires: `"\"my schema\".\"order status\""`, and `"\"my schema\".U&\"Order\\000AItem\""` for a part with escapes.  Any other character, a `&` among them, is written as it is, and a tab or a line break inside the quotes of a key may be written `\t` or `\n`, as JSON writes them.  An error that quotes a key that holds such a character writes the character as an escape: `"my schema".U&"Order\000AItem"`.  An object serves PostgreSQL and SQL Server alike: a member whose key names a type of either is valid, and maps the type of each database that has that name.  Two members of one object whose keys are one type's, `"integer"` and `"int4"`, or `"timestamptz"` and `"TIMESTAMPTZ"`, are [SQLSRC020](diagnostics.md#sqlsrc020), and the first applies.  A member that is not valid is [SQLSRC019](diagnostics.md#sqlsrc019), and the other members still apply.

**In a marker**, a `-- type-override:` line holds one entry, with or without spaces around its `=`.  The lines of one place add up, one for each type.  Two lines that give one type two C# types in the file header, or inside one query, are [SQLSRC112](diagnostics.md#sqlsrc112); the types are compared by how they are named, so `int` and `System.Int32` are two, and so are `UserKey` and `MyApp.UserKey`, while `System.Int32` and `global::System.Int32` are one.  Its key is read by the file's dialect: a SQL Server type's name in an `mssql` file, and a PostgreSQL type's in a file of any other dialect, so `int4` in an `mssql` file, or `nvarchar` in a `postgres` one, is not valid.  A type of a schema of your own, `dbo.Email`, is read in either.  An entry that is not valid is [SQLSRC111](diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read `typeOverrides`, which changes nothing that is described.  A `-- type-override:` marker that is not valid is an error for the tool as for the build, and the tool describes no query of its file until it is fixed.

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

A converter in any other type of a referenced assembly is not found.  When your project and a library's type both convert between the same two types, your project's converter is used, with no error, so that a project can replace a library's conversion.

**SqlSource's own conversions.**  On SQL Server, the query methods of a project that references NodaTime convert NodaTime's types themselves, from and to the types Microsoft.Data.SqlClient reads, as [NodaTime on SQL Server](sqlserver.md#nodatime) lists, whether the [library types](#setting-library-types) turn NodaTime on or not.  A converter of yours between the same two types, in the same direction, is used in place of SqlSource's, with no error, and SqlSource's is kept for the other direction: so `static NodaTime.Instant FromDatabase(System.DateTime value)` reads each `datetime2` that is an `Instant` your way, and an `Instant` parameter is still sent by SqlSource's conversion.  On PostgreSQL SqlSource converts nothing, as Npgsql's plugin reads and sends NodaTime's types.  A library's converter is used only when your project can call it: when it is `public`, or `internal` in a library that gives your project its internals with `InternalsVisibleTo`.  A library's converter that is not one, one your project cannot call among them, is [SQLSRC277](diagnostics.md#sqlsrc277) at each query whose method would use it, since your project cannot change it.

**A converter is a `static` method of one argument that returns a value**, `public` or `internal`, in a type that is not generic.  A method marked `[SqlSourceConverter]` that is not one is SQLSRC277, which lists each rule, in any project, whatever its output.  Its argument may be written with `?`, `Email?` or `string?`: it takes the type without the `?`, since it is never given null.  Its return type is never written with `?`.  A converter marked `[Obsolete]` as a warning is used, and the generated code turns the warning off; one marked `[Obsolete]` as an error is SQLSRC277.  Two converters between the same two types in one direction are [SQLSRC278](diagnostics.md#sqlsrc278), at both.

**Null never reaches a converter.**  A column that holds a null gives its property null when the property can be null, and throws as any column does when it cannot: see [how a row is read](generator.md#reading-rows).  A parameter whose value is null is sent as the database's `NULL`.  So one pair of converters serves a property that can be null and one that cannot.

**An array of PostgreSQL whose C# type is an array of a type of yours**, chosen for the element, as `email[]` under `public.email=MyApp.Email`, or for the array, as `public.user_id[]=MyApp.UserId?[]`, is converted element by element by the element's converters: a `MyApp.Email[]` column is read as a `string[]`, and each element given to `FromDatabase`.  A `NULL` element stays null, the converter not called for it, when the element is of a reference type or written with `?`, as `MyApp.UserId?`.  A `NULL` element of a value type written without `?`, as a `MyApp.Email[]` holds, throws `InvalidCastException`, "Column 'emails' holds a null element.".  The array is converted whole instead when a type override or a column type gives the array a type of yours that is not an array, `public.email[]=MyApp.EmailList`, or an array type that has converters of its own: those converters, between that type and `string[]`, are used, and the element's are not.

**Converters apply only where a type override, a column type, a `-- param:` marker or a library of the [library types](#setting-library-types) chose the type, and only in query methods.**  A column or parameter whose type the type table gives is never converted, so a converter between `System.DateTimeOffset` and a type of yours changes no `timetz` column.  A type that a library gives is converted only by a converter of the direction it needs, from the type the driver reads for a column and to it for a parameter, and is otherwise read and sent as the library has it, with no error: on PostgreSQL, a converter from `System.DateTime` to `NodaTime.Instant` reads each `timestamptz` that NodaTime gives an `Instant`, in place of Npgsql's plugin, and a parameter is still sent by the plugin when there is no converter back, while a converter between `NodaTime.Instant` and a type of yours changes no such `timestamptz`.  A query whose output is `models` calls no converter, and gets none of their errors at its name; to read it through Dapper, see [the recipe](recipes.md#models-through-dapper).  A type override to a type the driver reads for some database type, such as `string`, `System.DateTimeOffset` or `System.DateOnly`, is read and sent as the driver does when no converter is between it and the type the driver reads.

**Without a converter**, the query method asks the driver for the type that was chosen.  That is right for a type the driver was taught, a C# enum after Npgsql's `MapEnum<T>()` for one, a composite after `MapComposite<T>()` or a type of a plugin, and for a type it reads already, such as `System.DateTimeOffset` for a `timestamptz`.  A type of yours that the driver does not know, with no converter, throws the driver's `InvalidCastException` when the query runs.

<a id="setting-column-types"></a>

## Column types

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "columnTypes": {
        "users.id": "UserId",
        "posts.created_by_user_id": "UserId",
        "billing.invoices.id": "MyApp.Billing.InvoiceId",
    },
}
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

A column type gives one column of a table or view the C# type of your choice, in every query that reads it, where a [type override](#setting-type-overrides) gives every column of one database type the same C# type.  So `users.id` can be a `UserId` and `posts.id` a `PostId`, though both are `bigint`.  Above, `ListPostsDto` has `Id` a `PostId`, by the file header, `CreatedByUserId` a `UserId`, by the settings file, and `Total`, which no table gives, a `PostCount`, by its query's marker.  A parameter is no column of a table, so its [`-- param:` marker](parameters-and-tokens.md#param-marker) gives `ListPostsParams.Author` its type.  The C# type is written as a type override's is, a keyword, a full name or a short name: see [the type](#setting-type-overrides).

**The key is the column a value is read from**: `table.column`, which matches that column of a table or view of that name in any schema, or `schema.table.column`, which matches that schema's alone.  A query's column is read from a table's column when it is that column, under an alias or not, and not when it is an expression of it: `p.id AS post_id` is `posts.id`, and `count(*)`, `p.id + 1` and a column of a `UNION` are no table's column, so no key matches them.  Give such a column its type with a `-- column:` marker inside its query.  A column read from a view is the view's, so a view's columns need entries of their own: `users.id` does not reach the `id` of a view over `users`.

- **A part of a key is plain or in double quotes**, by the rules of a type override's key: a plain part holds letters, digits, `_` and `$` and is compared ignoring case, and a part in quotes holds any character, with a quote inside it doubled, is compared exactly, and may be written with escapes, `U&"..."`.  In a settings file each `"` of a key is written `\"`, as JSON requires: `"\"Users\".id"`.  So `users.id` matches `Users.ID` too, and `"Users".id` the table `Users` alone.
- **A key serves both databases.**  A settings file's entry maps the columns of PostgreSQL's queries and SQL Server's alike, and a marker those of its file.

**Every place, merged by key.**  The column types are `columnTypes` in the settings files of a `.sql` file's folder and of the folders above it, a `-- column:` marker in the file header, and a `-- column:` marker inside a query, which names a column of the query's result.  There is no attribute property.  For each column the query's marker wins, then the file header's entries, then those of the settings files, nearest first, and as for the type override, an entry for a key replaces the entries of a place below for that key alone, and `default` takes the column out of the entries of every place below.  Of the entries that match one column, the one of the most specific place wins; within one place, one with a schema wins over one without, and then one spelled exactly as the column's names over one that matches it only ignoring case.

**Over the type override.**  A column's C# type is, in order: the column types, its query's `-- column: name -> type` first and then those of the other places by the column it is read from; then the type override, by its database type; then the type map.  So a column type at any place wins over a type override at any place: naming a column is more specific than naming a type.  `-- column: name -> default` frees one column of a query for the type override.  A column type never reaches a parameter: a parameter's C# type is its [`-- param:` marker](parameters-and-tokens.md#param-marker)'s, then the type override's, then the type map's.

**The nullability stays the column's**, as [which properties can be null](generator.md#model-nullability) decides it: `"users.id": "UserId"` gives a column that can be null the type `UserId?`.

**What the type does.**  A type that a column type or a `-- param:` marker gives is one you chose, as a type override's is.  A [query method](generator.md#query-methods) reads and sends it through your [converters](#converters) when it has them, so a `UserId` over a `bigint` column is read as a `long` and given to `UserId`'s converter, and a PostgreSQL array mapped to an array of a type of yours, `"posts.tag_ids": "TagId[]"`, is converted as [converters](#converters) says of arrays.  A type that the project cannot find or use is [SQLSRC250](diagnostics.md#sqlsrc250) to [SQLSRC252](diagnostics.md#sqlsrc252), whose message names the key, or the marker, and the place.

**In a settings file**, `columnTypes` is a JSON object with a member for each entry, `"users.id": "UserId"`, written by the type override's rules for a settings file: see [type overrides](#setting-type-overrides).  Two members of one object whose keys are one column's, `"users.id"` and `"\"users\".id"`, are [SQLSRC020](diagnostics.md#sqlsrc020), and the first applies.  A member that is not valid is [SQLSRC019](diagnostics.md#sqlsrc019), and the other members still apply.

**In the file header**, a `-- column:` line holds one entry, `key -> type` or `key -> default`, with or without spaces around its `->`, for every query of the file.  It gives a type and nothing else: a line without `->`, one that says `null` or `not null`, and one whose key has one part are [SQLSRC111](diagnostics.md#sqlsrc111).  Two lines that give one key two C# types are [SQLSRC112](diagnostics.md#sqlsrc112); the types are compared by how they are written, so `UserId` and `MyApp.UserId` are two.

**Inside a query**, a `-- column:` line names a column of the query's result, as the query returns it, and gives it a C# type after `->`: `-- column: total -> PostCount`.  It wins over the column types of the other places and over the type override, and `-> default` takes the column out of the column types, so that the type override, or else the type map, gives its type.  The same marker sets the column's nullability, before the `->` or alone: `-- column: note null -> NoteText`.  Its name is one part, so a name with a `.` outside quotes is [SQLSRC111](diagnostics.md#sqlsrc111): a column's source is mapped in the file header.  The rules of its name, of its nullability and of two markers for one column are on [the generator page](generator.md#column-marker).  In a file with no `-- name:` line, which is one query and takes both kinds, a key of two or three parts is the file header's kind, and a name of one part the query's.

The `sqlsource` tool does not read `columnTypes`, and a `-- column:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.

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

The library types say whether the query methods read and send a library's types, in place of the types of the type map, for the database types the library covers.  The one library today is NodaTime, named `nodaTime`: on PostgreSQL it gives a `timestamptz` the type `NodaTime.Instant`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on PostgreSQL](postgresql.md#nodatime) lists, and on SQL Server it gives a `datetime2` the type `NodaTime.LocalDateTime`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on SQL Server](sqlserver.md#nodatime) lists.  Above, the project references `Npgsql.NodaTime`, so NodaTime would be on for all of it; the project's settings file turns it off, the files under `Scheduling/` give it back to its default, on, and the query `ListShifts` turns it off again, so its `starts_at`, a `timestamptz`, stays a `System.DateTime`.

> **Coming in a later release.**  The library types will also know NetTopologySuite and System.Text.Json.  See [what works today](README.md#what-works-today).

**The state** of a library is `on`, `off` or `default`, matched ignoring case, hyphens and spaces, as the library's name is.  `default` takes the library out of the entries of every place below, so that its default applies, as a type override's `default` gives a type back to the type map.

**The default**, where no place names the library or the nearest that does says `default`:

- **PostgreSQL**: `on` when the project references `Npgsql.NodaTime`, the plugin that lets Npgsql read and send NodaTime's types, directly or through another package such as Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`, and `off` otherwise.  A project that references `NodaTime` alone has it off.
- **SQL Server**: `off`, whether the project references `NodaTime` or not.

**Every place, merged by library.**  The library types are `libraryTypes` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- library-types:` marker in the file header and inside a query.  There is no attribute property.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a library replaces the entry of a place below for that library alone, as the [type override](#setting-type-overrides)'s entries do.  One value serves both databases: a file's dialect decides which database's types apply.

**Only for query methods.**  A library's types reach only a query whose [output](#setting-output) is `codegen`, for each type that claims it.  A query whose output is `models` keeps the type map's types in its models, whatever the library types say: there they are read and checked, and do nothing else.  To give such a query a library's types, as for rows read through Dapper, name them with the type override or the column types.  A model that a `codegen` query, with the library on, and a `models` query share, or two `codegen` queries, one with the library on and one with it off, has two sets of types and is [SQLSRC257](diagnostics.md#sqlsrc257): name the models apart with [`-- output-model:`](#setting-model-name), or turn the library on or off alike for both.

**Below your own choices.**  A column's or parameter's C# type is its query's `-- column:` marker's or its `-- param:` marker's, then the [column types](#setting-column-types)', then the [type override](#setting-type-overrides)'s, then the library's, then the type map's: see [property types](generator.md#property-types).  So a project that maps `timestamptz` with the type override keeps its type with NodaTime on, and `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default`, gives `timestamptz` back to the library there.

**What `on` needs.**  The project references what the library needs on the database of the query: on PostgreSQL `Npgsql.NodaTime`, and on SQL Server `NodaTime`.  An entry that says `on` for a `codegen` query of a project without it is [SQLSRC281](diagnostics.md#sqlsrc281), at the entry, and the query keeps the type map's types.  A query method that reads NodaTime's types on PostgreSQL needs `UseNodaTime()` on the application's data source: see [PostgreSQL's query methods](postgresql.md#method-results).  On SQL Server the query methods convert NodaTime's types themselves, and nothing is registered in the application.  A query method that gets NodaTime's types from the library types, or on SQL Server converts a NodaTime type of your choice, needs NodaTime 3.1.9 or later: an older one is [SQLSRC282](diagnostics.md#sqlsrc282), and the query gets no method.

**Only what you did not choose.**  The library types decide the type of a column or parameter that nothing else gives one.  A type you choose with the type override, a column type or a marker is read and sent the same whether its library is on or not: on SQL Server, `datetime2=NodaTime.Instant` is converted for a project that references NodaTime with NodaTime off, as [NodaTime on SQL Server](sqlserver.md#nodatime) says.

**In a settings file**, `libraryTypes` is a JSON object with a member for each library, `"nodaTime": "on"`.  A member whose library or state is not one is [SQLSRC019](diagnostics.md#sqlsrc019), and the other members still apply.  Two members for one library, `"nodaTime"` and `"NodaTime"`, are [SQLSRC020](diagnostics.md#sqlsrc020), and the first applies.

**In a marker**, a `-- library-types:` line holds one entry or more, each `name=state` with no space around its `=`, separated by white space: `-- library-types: nodatime=on`.  The lines of one place add up, one for each library, and the same state twice is fine.  Two states for one library in the file header, or inside one query, are [SQLSRC112](diagnostics.md#sqlsrc112).  A line with no entry, or an entry that is not valid, is [SQLSRC111](diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read `libraryTypes`, and a `-- library-types:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.

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

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "nestedClass": "Queries",
}
```

```csharp
[SqlSourceGenerate(Output = GeneratorOutput.Models, NestedClass = "Statements")]
public partial class UserRepository
{
    public string Get() => Statements.GetUser;
}
```

Under `SqlLocation.Nested`, this names the private class that holds the type's members, those of its queries whose output is `sql` or `models`.  `NestedClass` on the attribute names it for one type, and `nestedClass` in a settings file for each type that does not set it and whose C# file, the one that carries `[SqlSourceGenerate]`, is in the settings file's folder or a folder below it, the nearest such file winning.  Without either, the class is `Sql`.  The setting is read from the settings files of the type's C# file, not of its `.sql` files, and has no marker, since a type's queries can come from several files.

The value is one C# identifier that is not a keyword: `Queries` is fine, and `class`, `My-Sql` or `App.Sql` is not.  A value that is not valid is [SQLSRC006](diagnostics.md#sqlsrc006) on the attribute, and [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file, where a farther settings file or the default then gives the name.  Under `SqlLocation.Direct` there is no nested class, and the setting has no effect, even on the same attribute.

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

There is no settings file key or marker for it.  `methodLocation` in a settings file is [SQLSRC016](diagnostics.md#sqlsrc016), and `-- method-location:` in a `.sql` file is not read.  The class and its accessibility, and the names a method cannot have, are on [the generator page](generator.md#method-location).

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

A project reads its `.sql` files as PostgreSQL and keeps the comments of its queries.  One folder, `Legacy/`, holds MySQL files, and their queries take token fragments that are sometimes empty, so the folder turns the token check off.  A [settings file](#settings-files) in a folder gives it its own settings:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "generatorParameters": ["keep-comments"],
}
```

```jsonc
// Legacy/sqlsource.json
{
    "dialect": "mysql",
    "generatorParameters": ["keep-comments", "no-token-validation"],
}
```

Every file under `Legacy/` is read as `mysql`, keeps its comments, and skips the token check.  Every other file is untouched: it is read as `postgres`, keeps its comments, and checks its tokens.  Three things to notice:

- The folder's list says `keep-comments` again.  A list replaces the project's whole, so a folder that wants what the project asks for repeats it.  Without it, the files of `Legacy/` would lose their comments.
- The dialect is a value of its own too.  Had the project's file set `"mysql,ansi-quotes"` as its dialect, this folder would still be read as plain `mysql`.
- A file's own marker still wins: a `-- generator: default` or a `-- dialect: mariadb` in one of the files overrides both settings files.  See [which place wins](#precedence).

The file in `Legacy/` applies to the `.sql` files of its subfolders too, `Legacy/Reports/` among them, unless a settings file nearer to them sets the same setting.  It applies to a type, through the folder of the C# file that carries the type's attribute, only for the [nested class](#setting-nested-class): every other setting of a query comes from the settings files of its `.sql` file, whichever type claims it.

Only the settings whose row in [the tables](#all-settings) lists a settings file can be given this way.  To change a setting for the files of one type, use the type's attribute.

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

<a id="settings-files"></a>

## Settings files

```jsonc
// sqlsource.json, in the project's folder
{
    "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
    // No settings file in a folder above this one applies to the project.
    "root": true,
    "dialect": "postgres",
    "output": "models",
    "generatorParameters": ["keep-comments", "sort-output"],
    "typeOverrides": {
        "timestamptz": "System.DateTimeOffset",
    },
    "columnTypes": {
        "users.id": "MyApp.UserId",
    },
}
```

A settings file is a file named `sqlsource.json`, in any folder.  It sets the settings of the `.sql` files of its folder and of every folder below it.  The one above, in the project's folder, reads every `.sql` file of the project as PostgreSQL, gives each query its models and no method, keeps comments and sorts the output models' properties, and gives every `timestamptz` the C# type `System.DateTimeOffset` and every `users.id` column `MyApp.UserId`.  A setting it does not name keeps its default, and a marker or the attribute still wins over it, as [which place wins](#precedence) says.

**Its name** is `sqlsource.json`, in lower case.  A file named in another case, such as `SqlSource.json`, is not read when the project file lists it itself as an `AdditionalFiles` item.  When it is not listed, the build finds it and reads it on macOS and Windows, which compare names ignoring case, and does not find it on Linux, so a project built on both would read different settings.

**What it holds** is one JSON object, whose members are the settings.  It may hold `//` and `/* */` comments, and a comma after the last member of an object or the last element of an array.

- **The keys** are those of [the tables](#all-settings), `root` and `$schema`, matched exactly, case included: `Dialect` is not a key.  A key that is not one is [SQLSRC016](diagnostics.md#sqlsrc016), whose section lists them all, and a key written twice is [SQLSRC017](diagnostics.md#sqlsrc017).
- **Each value is a JSON string**, except `root`, which is `true` or `false`, `generatorParameters`, an array of strings, and `typeOverrides`, `columnTypes` and `libraryTypes`, each an object whose values are strings.  A value of another JSON type, `null` among them, is [SQLSRC018](diagnostics.md#sqlsrc018).
- **A string is read as JSON writes it.**  The white space around it is ignored, and every character inside it is kept, a `;` or a `#` among them; there is no escape but JSON's own.  A string that is empty or white space sets nothing, and is no error.  To leave a setting to the files above, or to its default, leave its key out.
- **`$schema`** is for your editor, which then checks the keys and values as you write them and offers them as you type: `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`.  SqlSource reads nothing of it.

**The settings files of a `.sql` file** are the `sqlsource.json` of its folder and those of each folder above it, to the top of the drive, nearest first.  For each setting the nearest that sets it wins, so a file in a folder sets only what differs from the folders above it; the entries of `typeOverrides`, `columnTypes` and `libraryTypes` add up instead, key by key, as [type overrides](#setting-type-overrides) says.  A file that says `"root": true` is the last: no `sqlsource.json` in a folder above it applies to the files below it.  Put one in the project's folder, or at the top of the repository, so that a settings file above it, elsewhere on the machine, never changes your build.  `"root": false`, or no `root`, lets the search go on.  A file whose text is not JSON ([SQLSRC015](diagnostics.md#sqlsrc015)) gives nothing, its `root` included, so the search goes past it to the folders above.

- **A `.sql` file that the project lists from another folder**, outside the project's folder, takes the settings files of its own folder and the folders above it, not those of the project's folder.
- **A type's [nested class](#setting-nested-class)** is the one setting taken from the settings files of a C# file: those of the folder of the C# file that carries `[SqlSourceGenerate]`.
- **A settings file in a folder that holds no `.sql` file and no C# file of the project**, in it or in a folder below it, is not read, and applies to nothing.

**Which files are checked.**  The build reads each settings file that the search of some `.sql` file or C# file of the project reaches, whether or not a type claims that `.sql` file, and reports each mistake in it at its file, line and column.  A `sqlsource.json` beyond the first `"root": true` is never read, so it cannot fail a build.  The C# files that the build writes itself, into its output and intermediate folders, reach no settings file, wherever those folders are.  Nor do the C# files that a NuGet package adds to your project, as Polyfill does, from the folder where NuGet keeps its packages, so a broken `sqlsource.json` above that folder, in your home folder, say, is not read on their account.  Your own `.sql` and C# files still reach such a file when your project is in a folder below it, unless a `"root": true` stands between them, and its mistakes then fail the build and `sqlsource describe`: put `"root": true` in the settings file of your project's folder, or of your repository's.  A C# file that your project lists itself from another folder still reaches the settings files of its own folder.  A value that is wrong is not set by its file, so a farther settings file, or the default, gives the setting meanwhile.  The `sqlsource` tool reads the same settings files.  It reports the mistakes of [SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC018](diagnostics.md#sqlsrc018) as the build does, and a value of `dialect`, `output` or `database` that is not valid, once in a run however many projects reach the file.  The build reports a value that is not valid for every key but `database`, which only the tool reads.

**In the build**, SqlSource keeps a settings file out of the output and publish folders where the SDK would copy it, as the Web SDK copies every `.json` file of a web project.  A copy rule of your own still copies it: a rule written for `appsettings.json`, such as `<None Update="**/*.json" CopyToOutputDirectory="PreserveNewest" />`, copies every `sqlsource.json` of the project too.  So name the files such a rule copies, as `Update="appsettings*.json"` does, or follow it with `<None Update="**/sqlsource.json" CopyToOutputDirectory="Never" CopyToPublishDirectory="Never" />`.  Adding, deleting, moving or editing one makes the next build compile again, and an IDE that builds the project reads the same files.  The project file holds no setting: an MSBuild property or item metadata named after a setting is not read, and nothing reports it, so move such a value to a settings file.  The one MSBuild property of SqlSource that a project sets is [`SqlSourceIncludeFiles`](#included-files), which says which `.sql` files are included.

See: [SQLSRC015](diagnostics.md#sqlsrc015), a file that is not a JSON object; [SQLSRC016](diagnostics.md#sqlsrc016), a key that is not known; [SQLSRC017](diagnostics.md#sqlsrc017), a key written twice; [SQLSRC018](diagnostics.md#sqlsrc018), a value of the wrong JSON type; [SQLSRC019](diagnostics.md#sqlsrc019), a value that is not valid; [SQLSRC020](diagnostics.md#sqlsrc020), a key repeated in `typeOverrides`, `columnTypes` or `libraryTypes`.

<a id="known-issues"></a>

## Known issues

- **A `.sql` file that one of your own build targets adds is not described.**  This is about a target in your project that adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as one that writes a file or copies it in.  The build compiles the file, but `sqlsource` leaves out the file's queries, so it describes none of them, and the build reports [SQLSRC241](diagnostics.md#sqlsrc241) for each that must be described.  To make it work, tell your target to run before the package lists the project's files, wherever the target is declared:

  ```xml
  <Target Name="AddGeneratedSql" BeforeTargets="SqlSourceListFiles">
      <ItemGroup>
          <AdditionalFiles Include="obj/generated/*.sql" />
      </ItemGroup>
  </Target>
  ```

  Change the name and the path to your own.  `BeforeTargets="SqlSourceListFiles"` is the line that matters.  In a project that is not of the .NET SDK, or one that sets `AfterMicrosoftNETSdkTargets` without keeping its value, a target that runs later also leaves the file without the settings of a `sqlsource.json` that only it reaches, such as the one beside it, with no error; the same line fixes that too.
- **A type override's key cannot be written in brackets.**  T-SQL writes a name `[dbo].[Email]`, and a key written so is not valid: [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file, and [SQLSRC111](diagnostics.md#sqlsrc111) in a marker.  Write `dbo.Email`, which matches the type's name in any case, or `"dbo"."Email"`, which matches its case exactly; the message shows both.  See [type overrides](#setting-type-overrides).
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
