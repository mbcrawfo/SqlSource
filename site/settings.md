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
[SqlSourceGenerate(Source = "Reports")]
internal partial class Reports(NpgsqlDataSource dataSource);
```

The first block writes settings as markers in a `.sql` file, the second in a [settings file](#settings-files), `sqlsource.json`, which applies to the `.sql` files of its folder and of every folder below it, the third as a property of the attribute.  Which of the three a setting accepts is in [the table](#all-settings).  To read one folder of a project differently from the rest, see [configure one folder differently](#configure-a-folder).

<a id="all-settings"></a>

## All settings

Each name links to its section, which gives its values and its rules.  **Written as** lists every place the setting can be written: a key of a [settings file](#settings-files), a property of the attribute, a marker in the **file header** (the part of a `.sql` file before its first `-- name:` line), a marker **inside a query** (under a `-- name:` line), as [the marker index](reference/sql-files.md#marker-index) shows, and, for the two settings of the build and the repository interfaces, a property of the project.  A place it does not list cannot hold it.

| Setting | Values | Written as |
|----|----|----|
| [Output](#setting-output) | `sql`, `models`, `methods`, `repository`.  Default `repository` | `output` in a settings file; `-- output:` in the file header and inside a query |
| [Database](#setting-database) | A name of letters, digits, `-`, `_` and `.`.  Default: the name of the file's dialect | `database` in a settings file; `-- database:` in the file header and inside a query |
| [Dialect](#setting-dialect) | A dialect's name, with options for `mysql` and `mariadb`: see [the dialects page](reference/dialects.md#choosing-a-dialect).  Default `ansi` | `dialect` in a settings file; `-- dialect:` in the file header |
| [Generator parameters](#setting-generator-parameters) | Words separated by spaces, or an array of them in a settings file, each a parameter alone, with `=` and one of its values, or with `=default`, or `default` alone.  The parameters are `keep-comments`, `token-validation`, `sort-input`, `sort-output`, `table-models`, `async-method-suffix`, `array-pool`, `list-padding`, `collection-method`, `transaction` and `method-type`, and each place sets only those it names.  Each but `method-type` takes `on` or `off`, and `transaction` also `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot`; `method-type` takes `async`, `sync` or `both`.  Default: `token-validation`, `table-models`, `array-pool`, `list-padding` and `transaction` on, `method-type` `async`, the others off | `generatorParameters` in a settings file; `-- generator:` in the file header and inside a query |
| [Input and output model suffix](reference/models.md#setting-model-suffix) | Characters of a C# identifier.  Defaults `Params` and `Dto` | `inputModelSuffix`, `outputModelSuffix` in a settings file; `-- input-model-suffix:`, `-- output-model-suffix:` in the file header |
| [Model namespace](reference/models.md#setting-model-namespace) | A namespace.  Default: the namespace of the type | `modelNamespace` in a settings file; `-- model-namespace:` in the file header |
| [Input and output model type](reference/models.md#setting-model-type) | `record`, `sealed record`, `class`, `sealed class`.  Default `sealed record` | `inputModelType`, `outputModelType` in a settings file; `-- input-model-type:`, `-- output-model-type:` in the file header and inside a query |
| [One query's model name](reference/models.md#setting-model-name) | A type's name, or its full name | `-- input-model:`, `-- output-model:` inside a query |
| [Type overrides](#setting-type-overrides) | Entries `"key": "type"`, each a database type and the C# type it gets, or `"key": "default"`.  Default: none | `typeOverrides` in a settings file; `-- type-override:` in the file header and inside a query |
| [Column types](reference/model-properties.md#setting-column-types) | Entries `"key": "type"`, each a column of a table or view, `table.column` or `schema.table.column`, and the C# type it gets, or `"key": "default"`, or `"key": { "type": "type", "json": true }` for a column that holds JSON, the [JSON opt-in](#json-opt-in).  Default: none | `columnTypes` in a settings file; `-- column:` in the file header, and inside a query for a column of the query's result |
| [Library types](#setting-library-types) | Entries `"library": "state"`, the library `nodaTime`, `netTopologySuite` or `json` and the state `on`, `off` or `default`.  Default: on for PostgreSQL where the project references the library's plugin, `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, and off otherwise; `json` off | `libraryTypes` in a settings file; `-- library-types:` in the file header and inside a query |
| [Member location](reference/members.md#setting-member-location) | `extension-class`, `class`, `nested-class`.  Default `nested-class` for the output `sql` or `models`, `extension-class` for `methods`, and `class` for `repository` | `memberLocation` in a settings file; `-- member-location:` in the file header and inside a query |
| [Visibility](reference/members.md#setting-visibility) | `public`, `internal`, `protected`, `private`.  Default `public` for `repository`, and otherwise by the member location, as [its section](reference/members.md#setting-visibility) says | `visibility` in a settings file; `-- visibility:` in the file header and inside a query |
| [Nested class](reference/members.md#setting-nested-class) | A C# identifier that is not a keyword.  Default `Sql` | `nestedClass` in a settings file; `-- nested-class:` in the file header and inside a query |
| [`Source`](#setting-source) | A folder, or one `.sql` file, relative to the folder of the C# file.  Default: the file named for the type in that folder | `Source` on the attribute |
| [Collection type](reference/return-types.md#setting-collection-type) | A collection interface or type, such as `IReadOnlyList` or `List`, or `IAsyncEnumerable` to stream the rows: see [its section](reference/return-types.md#setting-collection-type).  Default `Array` | `collectionType` in a settings file; `-- collection-type:` in the file header and inside a query |
| [Repository interfaces](reference/repositories.md#repository-interfaces) | Whether a repository gets its interface, `true` or `false`, and its name, a C# identifier.  Default: on, named `I` and the type's name | `GenerateInterface` and `InterfaceName` on the attribute; `SqlSourceRepositoryInterfaces`, a property of the project, `false` to turn them off |
| [Running the tool in the build](#online-build) | `auto`, `true`, `false`.  Default `auto` | `SqlSourceOnline`, a property of the project |
| [The build's `.env` file](#setting-env-file) | A path, from the project's folder when it is relative, or `none`.  Default: the `.env` in the project's folder | `SqlSourceEnvFile`, a property of the project |

A value from a list is matched ignoring case, hyphens and spaces, so `Methods`, `methods` and `me-thods` are one value, and so are `SealedRecord` and `sealed record`, and `NestedClass` and `nested-class`.

<a id="precedence"></a>

## Which place wins

A setting written in several places has the value of the most specific place that gives one.  For `Output` the places are:

| Order | Place | Written as |
|----|----|----|
| 1 | A marker inside the query | `-- output: sql` |
| 2 | A marker in the file header | `-- output: models` |
| 3 | The nearest settings file that sets it | `"output": "models"` in `Reports/sqlsource.json` |
| 4 | Each farther settings file, nearest first | `"output": "sql"` in the `sqlsource.json` of the project's folder |
| 5 | The default | `repository` |

A query with `-- output: sql` has the output `sql`, whatever the other four say.  A query without it takes the file header's `models`, and so on down the table.  The settings files of a `.sql` file are the `sqlsource.json` of its folder and those of the folders above it, nearest first, up to the first that says `"root": true`: see [settings files](#settings-files).  To give the files of one folder their own values, see [configure one folder differently](#configure-a-folder).

The rules:

- **Each setting is decided on its own.**  A query that writes `-- output: sql` still takes its file header's `-- database:`.
- **A place that gives no value is skipped.**  A settings file that does not name the setting gives none, and so does a string of a settings file that is empty or white space.  `Source`, on the attribute, is the exception: see [Source](#setting-source).
- **A setting has only the places its row of [the table](#all-settings) lists.**  A key of a settings file that is not in it, nor `root` or `$schema`, is [SQLSRC016](diagnostics.md#sqlsrc016).  A marker where its row says it cannot stand is [SQLSRC116](diagnostics.md#sqlsrc116), except a `-- dialect:` inside a query or after SQL, which is [SQLSRC115](diagnostics.md#sqlsrc115): see [markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place).
- **Every type that claims a file gets the same settings for its queries.**  The places of a query's settings are the query, its file and the settings files of the file's folder, so two types that claim one file take the same settings from it: one output, one suffix, one shape of model, one [member location](reference/members.md#setting-member-location).  Only what follows from the type itself can differ: the namespace of the models, which is the type's own unless the [model namespace](reference/models.md#setting-model-namespace) is set, and the default [visibility](reference/members.md#setting-visibility) of the members of `sql`, `models` and `methods` queries on the type, which follows the type's own accessibility.  A type that needs other settings needs a file of its own.

Three exceptions, stated plainly:

- **A dialect value replaces the one below it whole**, options included.  The dialect has no in-query level: a file has one dialect, which is the marker in its file header, else the nearest of its settings files that sets one, else `ansi`.  See [dialect](#setting-dialect).
- **`Source` exists only on the attribute.**  It has nothing to choose between.
- **A type override, a column type, a library of the library types, or a generator parameter, replaces only what it names.**  Each place adds its entries to those of the places below it, each settings file a place of its own, so a folder's file adds to the entries of the files above it.  An entry for a key, a database type, a column, a library or a generator parameter, replaces the entry below it for that key, and `default` takes the key out.  The generator parameters also take `default` alone, which takes every parameter out, back to its default.  See [type overrides](#setting-type-overrides), [column types](reference/model-properties.md#setting-column-types), [library types](#setting-library-types) and [generator parameters](#setting-generator-parameters).

<a id="a-value-that-is-wrong"></a>

### A value that is wrong

| Written as | Reported as | What happens |
|----|----|----|
| A marker | [SQLSRC111](diagnostics.md#sqlsrc111), a dialect included.  A generator parameter that does not exist is [SQLSRC109](diagnostics.md#sqlsrc109); a value that contradicts another in the same place is [SQLSRC112](diagnostics.md#sqlsrc112) | The file produces no members until it is fixed |
| A settings file | [SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC020](diagnostics.md#sqlsrc020), at the file, line and column of the mistake: see [settings files](#settings-files) | A value that is wrong is not set by that file, so a farther settings file or the default gives it, for a dialect too.  The rest of the file still applies, and so do the other words of `generatorParameters` and the other entries of `typeOverrides`, `columnTypes` or `libraryTypes`.  Of a key, or an entry, written twice, the first applies, and so does the first of two values of one generator parameter.  A file that is not a JSON object gives nothing |

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

`Output` says how much SqlSource produces for a query.  The default is `repository`.

| Value | A query gets |
|----|----|
| `sql` | Its SQL: a constant, or a method where the query has tokens |
| `models` | Its SQL, and its [models](reference/models.md#models): types for its parameters and for a row of its result |
| `methods` | Its models, and a [method that takes a connection](reference/connection-methods.md#methods-that-take-a-connection) and runs the query, which holds its SQL in place of a constant |
| `repository` | Its models, and a [repository method](reference/repositories.md#repository-methods): an instance method of the type that gets its own connection and runs the query, which holds its SQL in place of a constant |

As a marker it stands in the file header, for every query of the file, and inside a query.  It can also be `output` in a settings file.

**`Output` decides which queries must be described.**  `sqlsource describe` asks a database about the queries whose output is `models`, `methods` or `repository`, and the build checks that each of them has a current entry in its file's [sidecar](concepts.md#sidecar): a query that has none is [SQLSRC241](diagnostics.md#sqlsrc241), and one that changed since it was described is [SQLSRC244](diagnostics.md#sqlsrc244).  A query whose output is `sql` needs no description, no entry, no connection, and belongs to no [logical database](#setting-database).  A file claimed by two types, or listed by two projects, is described once, and the build of a project checks its entries once for the file.

The tool can describe only some dialects: see [the database support table](reference/databases/index.md#database-support).  The default output is `repository` and the default dialect is `ansi`, so a project that sets neither gets [SQLSRC209](diagnostics.md#sqlsrc209) for every file, in its build and from the tool.  Set the dialect and describe the queries, or set the output to `sql` for queries that want only their SQL.

A query whose output is `sql` or `models` gets its constant or token method.  A query whose output is `models`, `methods` or `repository`, and whose entry in its sidecar is current, also gets its [models](reference/models.md#models); one whose output is `sql` gets none.  A query whose output is `methods` or `repository` gets no constant and no token method: its [method](reference/query-methods.md#query-methods) holds its SQL and runs it, when the query meets the conditions of [which queries get a method](reference/query-methods.md#which-methods).  A `repository` query's type needs a connection for it, as [the connection](reference/repositories.md#repository-connection) says, and a `methods` query's method takes one, so set `methods` for a query that your own code runs on a connection or in a transaction that it manages.  A project that runs the SQL itself, through Dapper or its own ADO.NET code, sets `models`.

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
- It can be a marker in the file header or inside a query, or `database` in a settings file.
- One name is one database for a whole run, with one connection and one dialect.  Two files that give one name two dialects are [SQLSRC211](diagnostics.md#sqlsrc211).
- Only the tool reads this setting.  The build does not, so a wrong `database` in a settings file is reported by the tool and not by the build.

To give the tool a connection for a name, see [the command reference](reference/cli.md#command-reference).

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

The [dialect](concepts.md#dialect) is the set of rules by which SqlSource reads a `.sql` file.  It does not connect to anything.  The default is `ansi`.  The dialect names, the options of `mysql` and `mariadb`, and what each dialect changes are on [the dialects page](reference/dialects.md).

- **Three places.**  A `-- dialect:` marker in the file, else the `dialect` of the nearest settings file of the file's folder, or of a folder above it, that sets one, else `ansi`.  Above, the files under `Reporting/` are read as `mssql`, and every other file of the project as `postgres`.
- **A file has one dialect.**  The marker goes in the file header, before the first SQL, and not inside a query: anywhere else it is [SQLSRC115](diagnostics.md#sqlsrc115).  It takes effect on the line after it, so comments above it are read by the dialect that the settings files give.
- **A value replaces the one below it whole.**  A file with `-- dialect: mysql` under a settings file that sets `"mysql,ansi-quotes"` is read as plain `mysql`, and so is a folder whose settings file sets `"mysql"` under one that sets `"mysql,ansi-quotes"`.
- **A value that is not a dialect** is [SQLSRC111](diagnostics.md#sqlsrc111) in a marker and [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file.  The files under a settings file whose `dialect` is wrong are read meanwhile by the dialect of a farther settings file, or as `ansi` when none sets one.
- **The tool reads the dialect too.**  It decides which kind of database the tool asks about a query, and whether there is one to ask: `postgres` and `mssql` can be described.  See [database support](reference/databases/index.md#database-support).

<a id="setting-generator-parameters"></a>

## Generator parameters

```sql
-- generator: keep-comments

-- name: Report
SELECT /* the database logs this comment */ id FROM users;

-- name: Total
-- generator: token-validation=off
SELECT COUNT(*) FROM {{table:users}};

-- name: Plain
-- generator: default
SELECT /* the database never sees this comment */ id FROM users;
```

A [generator parameter](concepts.md#generator-parameter) is an option that changes what SqlSource does with a query.  Each takes one of its own words, its values, and a word in a `-- generator:` line, or in `generatorParameters` in a settings file, gives it one.  Most are on or off, the common case, `transaction` also takes an isolation level, and `method-type` takes `async`, `sync` or `both`.  A query that no word reaches gets every default: `token-validation`, `table-models`, `array-pool`, `list-padding` and `transaction` are on, `method-type` is `async`, and the others are off.

| Parameter | Default | Effect |
|----|----|----|
| `keep-comments` | off | Comments and blank lines stay in the generated SQL: see [what reaches the generated SQL](reference/sql-files.md#generated-sql).  A PostgreSQL [collection method](reference/databases/postgresql/query-methods.md#collection-methods) sends its SQL without them all the same |
| `token-validation` | on | The method of a query with tokens checks its arguments, and `token-validation=off` turns the check off: see [validation](reference/tokens.md#token-validation) |
| `sort-input`, `sort-output` | off | The properties of the query's input model, or of its output model, are sorted by name: see [the order of the properties](reference/model-properties.md#property-order) |
| `table-models` | on | A query that reads a whole table gets a row model named after the table, and with `table-models=off` it gets one named after the query: see [table models](reference/models.md#table-models) |
| `async-method-suffix` | off | The name of a query's [method](reference/connection-methods.md#method-signature) ends with `Async`: `GetUserAsync` for `GetUser`.  A synchronous method's never does, and under `method-type=both` an asynchronous method's always does, as [synchronous methods](reference/query-methods.md#synchronous-methods) say |
| `array-pool` | on | A [`many`](reference/return-types.md#method-shapes) method that returns a collection reads its rows into buffers borrowed from .NET's shared array pool, unless it returns an `ImmutableList<T>` or an `IImmutableList<T>`, and so does a [collection method](reference/collection-methods.md#collection-methods) that reads rows, of the shape `many`, `one` or `one-optional`.  Once the pool holds buffers of the sizes it needs, a method that reads through the pool allocates no collection for a result of up to about 1,000 rows but the one it returns.  With `array-pool=off`, every such method reads its rows into a collection that it allocates and grows, as hand-written code usually does, and as a method that returns one of the two immutable lists always does.  Turn it off to keep the pool out of a project, or to compare the two.  A method that [streams its rows](reference/return-types.md#streaming-rows) holds no collection, so it is the same with `array-pool` on or off |
| `list-padding` | on | A query method pads each [list parameter](reference/parameters.md#list-parameters) to one of a few sizes, 1, 2, 4 and so on to 512 and then each multiple of 512, with its last value repeated, so that the database sees a few forms of the query, and keeps a plan of each, whatever the counts: see [padding](reference/parameters.md#list-padding).  With `list-padding=off`, a query method sends each list at its own count of values, so each count is a form of its own, and a list sends a parameter for each of its values alone |
| `collection-method` | off | A query with parameters whose output is `repository` or `methods` also gets a [collection method](reference/collection-methods.md#collection-methods), an overload of its method that runs it for each item of a collection of its input models, 1,000 items to a round trip, and returns what every item returned |
| `transaction` | on | The transaction that a collection method begins of its own: at the database's default isolation level under `on`, at the level that `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot` names, and none under `off`.  A transaction that the caller gives the method, or an ambient one, is used instead, whatever it says: see [transactions](reference/collection-methods.md#collection-transactions).  It changes nothing for a query without `collection-method`, and an isolation level that the query's database does not have is [SQLSRC297](diagnostics.md#sqlsrc297) |
| `method-type` | async | Whether a query whose output is `methods` or `repository` gets asynchronous methods or synchronous ones: under `async` it gets an asynchronous method, under `sync` a [synchronous method](reference/query-methods.md#synchronous-methods) in its place, and under `both` the two.  A query's [collection method](reference/collection-methods.md#collection-methods) follows it in the same way, and so does a repository's [interface](reference/repositories.md#repository-interface), which has a member for each.  It changes nothing at the output `sql` or `models`.  `method-type` takes no `on`, so the word `method-type` alone is [SQLSRC111](diagnostics.md#sqlsrc111): write one of its values |
| `default` | | No parameter of its own: every parameter takes its default at the place that says it, and the place's other words apply over it |

**A word** is `name`, `name=value` or `name=default`, with no space around its `=`, since a `-- generator:` line separates its words by white space.  `name=value` gives the parameter one of its values, `keep-comments=off` or `transaction=serializable`, and `name` alone is `name=on`, which `method-type` does not take.  `name=default` gives the parameter its default, whatever the places below say.  Names and values are matched ignoring case: `Keep-Comments=OFF` is `keep-comments=off`.

**`default` alone gives every parameter its default**, and the other words of its place apply over it, wherever it stands among them.  `-- generator: default keep-comments` is every default with `keep-comments` on, and so is `-- generator: keep-comments default`.  It may be repeated.

**Each place sets parameters over the places below it.**  A place changes only the parameters it names, and every other parameter keeps the value the places below give it.  In the example, the file header turns `keep-comments` on for every query of the file.  `Report` keeps its comments.  `Total` turns `token-validation` off and says nothing of `keep-comments`, so it skips the token check and keeps its comments too.  `Plain` gives every parameter its default, whatever its file says, so it does not keep its comments.

The places, from the farthest to the nearest:

1. `generatorParameters` in each settings file of the file's folder and of the folders above it, up to the first that says `"root": true`, from the farthest to the nearest.
2. The `-- generator:` lines in the file header.
3. The `-- generator:` lines inside the query.

A parameter has the value of the nearest place that names it, and its default when none does.  `default` names every parameter, so nothing below the place that says it matters.

**The lines of one place add up.**  Several `-- generator:` lines in the file header are one place, and so are several inside one query.  A file with no `-- name:` line is one query and one place, so all of its `-- generator:` lines add up, wherever they stand: `keep-comments` at its top and `keep-comments=off` lower down contradict each other, and the second does not override the first.

**In a settings file** the value is an array of strings, one word in each: `"generatorParameters": ["sort-output", "token-validation=off"]`.  `[]` names no parameter, so it changes nothing, and `["default"]` gives every parameter its default, whatever the folders above say.  An element that is empty or white space is skipped.

**A mistake** is reported where it is written:

| Mistake | In a `-- generator:` line | In a settings file |
|----|----|----|
| A word that is not a generator parameter, such as `keep-coments` | [SQLSRC109](diagnostics.md#sqlsrc109) | [SQLSRC019](diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A value that is not one of the parameter's, or `default`, such as `keep-comments=yes`, `transaction=snapshoot` or `method-type` alone, which is `method-type=on`, or `default` with a value | [SQLSRC111](diagnostics.md#sqlsrc111) | [SQLSRC019](diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A line with nothing after the colon | [SQLSRC110](diagnostics.md#sqlsrc110) | |
| One parameter given two different values in one place, such as `keep-comments` and `keep-comments=off`.  `keep-comments=default` beside either is one too, even where the default is the same | [SQLSRC112](diagnostics.md#sqlsrc112), at the later word | [SQLSRC020](diagnostics.md#sqlsrc020), at the later element, and the first applies |

A mistake in a `-- generator:` line leaves its file without members until it is fixed, as [a value that is wrong](#a-value-that-is-wrong) says.  The same value given twice is no mistake, and nor is `default` beside other words.

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

A type override gives a database type the C# type of your choice, in place of the one that the type map of [PostgreSQL](reference/databases/postgresql/types.md#type-map) or [SQL Server](reference/databases/sqlserver/types.md#types) gives it: a `timestamp with time zone` as a `System.DateTimeOffset`, a `numeric` as a `double`, an enum of your schema as a C# enum.  Each entry is a key and a type, `"key": "type"` in a settings file and `key=type` in a marker: the key names the database type, and the type is the C# type that every column and parameter of that database type gets.  The type `default` gives the database type back to the type map, or to a library of the [library types](#setting-library-types) that is on for a query method.

In the example, every query of the project gets `timestamptz` as a `System.DateTimeOffset`, `public.user_status` as `MyApp.UserStatus`, and `int4[]` as a list.  The files under `Legacy/` get `date` as a `System.DateTime` too.  Every query of the `.sql` file gets `numeric` as a `double`, and `ListEvents` gets `timestamptz` from the type map, a `System.DateTime`, while it keeps the project's other entries.

To give one column, or one parameter, a C# type of its own, where two columns of one database type need two C# types, see [column types](reference/model-properties.md#setting-column-types) and [`-- param:`](reference/parameters.md#param-marker).  Either wins over the type override.

For a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](diagnostics.md#sqlsrc232).

Each property of a generated [model](reference/models.md#models) has the C# type of its column or parameter, the type override's included.  A [query method](reference/query-methods.md#query-methods) reads and sends a column or parameter as that type, through your [converters](#converters) when the type has them.

**Every place, merged by type.**  The type override is `typeOverrides` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- type-override:` marker in the file header and inside a query.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a type replaces the entry of a place below for that type alone.  A settings file's entries reach only the `.sql` files of its folder and the folders below it.  An entry replaces the entry below for its key however that key is spelled, and its own spelling is the one kept: a query's `public."Status"=MyApp.Status` over `"public.Status": "MyApp.Status"` in the project's settings file leaves a type `status`, which the project's entry mapped too, to the type map, since the quoted key maps `Status` alone.  So a query that needs one type otherwise names that type, and keeps the rest of the project's entries.  This is one of the exceptions to the rule that the most specific place wins whole: see [which place wins](#precedence).  `default` takes the type out of the entries of every place below, and a key with no entry anywhere has the type map's C# type, or a library's in a query method.  Nothing removes every entry at once.

**The key** is a database type:

- **A type of PostgreSQL's catalog is its one-word name**, as [the type table](reference/databases/postgresql/types.md#type-table) gives it: `timestamptz`, `int4`, `float8`, `bpchar`.  The one-word spellings `integer`, `int`, `smallint`, `bigint`, `real`, `float`, `boolean`, `decimal` and `character` are read as the names they stand for: `float` is `float8`, and `character` is `bpchar`.  A spelling of several words, `timestamp with time zone` or `double precision`, is not a key: write the one-word name, which every built-in type has.  The name may be in double quotes when it is written exactly, in lower case: `"int4"` is `int4`, while `"Int4"` and `"integer"` are not keys.  `char` is the one-byte type `"char"`, and so is the key `"char"`; a `char(n)` column is a `bpchar`.
- **Any other type is written with its schema**, `public.user_status`, as the tool records it in the sidecar, and with the quotes its schema or name was created with: `public."Status"`, `"my schema"."order-status"`.  A key with a schema is not checked against the types SqlSource knows, so a type it does not know can be named.  `pg_catalog.int4` is `int4`.
- **The types of extensions** that the type table names, `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube`, are matched by their name alone in any schema.  `public.citext` matches the `citext` of `public` alone, and wins over `citext` there.
- **An array** is its element's key and `[]`, `int4[]`.  A parameter of a query method whose C# type is an array of one rank, from the type table, a library or an entry here, is an `IEnumerable<T>` of the element, and one that an entry maps to another type, such as a `List<int>`, is that type: see [array parameters](reference/databases/postgresql/query-methods.md#array-parameters).  The catalog's name of an array is read as the same key: `_int4` for a type that SqlSource knows, and with its schema for any type of `pg_catalog`, so `pg_catalog._pg_class` is `pg_class[]`.  `_int4` and `int4[]` are one type, and two entries for them are two entries for one type.  Write an array of a type outside `pg_catalog` with its schema, its element's name and `[]`, `public.user_status[]`, and only so.  An entry for the database's name of that array, `public._user_status`, maps nothing, and is [SQLSRC261](diagnostics.md#sqlsrc261) at a query that uses the array.  Such a key still maps a type whose own name starts with `_` and that is no array.  An entry for the array, `int4[]`, wins over one for its element, `int4`.  Without one, an array gets its element's C# type with `[]`, so `int4=long` makes an `int4[]` column a `long[]`.
- **A domain** has a key of its own, `public.email`, which wins over its base type's.  Without one, a domain gets its base type's C# type, so an entry for `text` maps every domain over `text` too.
- **A range and a multirange** have only their own key.  An entry for `int4` does not change an `int4range`.
- A key's length, precision and scale are not part of it: `numeric` matches `numeric(18,2)`, and `nvarchar` matches `nvarchar(max)`.
- **A SQL Server type is its name**, as [its type table](reference/databases/sqlserver/types.md#type-table) gives it: `int`, `nvarchar`, `datetime2`, `uniqueidentifier`.  SQL Server's other one-word names `integer`, `character`, `dec` and `rowversion` are read as `int`, `char`, `decimal` and `timestamp`.  A name of several words, `double precision`, is not a key.  `sys.` before a name is SQL Server's system schema, as `pg_catalog.` is PostgreSQL's: `sys.int` is SQL Server's `int` alone, and `pg_catalog.int4`, or `int4`, PostgreSQL's alone.
- **An alias type of SQL Server**, made with `CREATE TYPE dbo.Email FROM nvarchar(320)`, is written with its schema, `dbo.Email`, each part plain or in double quotes, as a PostgreSQL type outside `pg_catalog` is.  Its own key wins over its base type's.  Without one, it gets its base type's C# type, so an entry for `nvarchar` maps it too.
- **A name that both databases have is one key, and maps each database's own type of that name.**  A settings file's `"int": "long"` maps PostgreSQL's `int4` and SQL Server's `int`, and `"date": "System.DateOnly"` the `date` of both.  Some such names are different types: `bit` is a bit string on PostgreSQL and a `bool` on SQL Server, `timestamp` a time on PostgreSQL and a row version on SQL Server, and `char` PostgreSQL's one-byte type and SQL Server's string.  In a project with files of both, an entry that is meant for one database's type is written with that database's system schema, `pg_catalog.timestamp` or `sys.timestamp`, or set in a settings file of the folder that holds that database's files, as [configure one folder differently](#configure-a-folder) shows.  A marker is read by its file's dialect alone, so it never reaches the other database's type.
- **A part of a key, its schema or its name, is plain or in double quotes.**  A plain part holds letters, digits, `_` and `$`.  A part in quotes holds any character, with a quote inside it doubled, as SQL writes a name: `"my schema"."order-status"`, `public."say ""hi"""`.  A `.` stands between the schema and the name, and `[]` after an array's.  Put a name in quotes when it has a capital letter, or any character that a plain part does not hold, white space included.  A part in quotes may be written with escapes, as PostgreSQL writes a name: `"my schema".U&"Order\000AItem"` names a type whose name is `Order`, a line break and `Item`.  Inside `U&"..."`, `\` and four hex digits, or `\+` and six, is the character of that code, from 1 to 10FFFF, and a character beyond FFFF is one `\+` escape or the escapes of its two surrogates; `\\` is a backslash, and a quote is doubled.  It is the same part as the quotes of its text, so `public.U&"st\0061tus"` is `public."status"`.  `UESCAPE` is not read, and an escape that is not one of these makes the key not valid.
- **A part in quotes is compared exactly, case included, and a plain part ignoring case.**  So `public."Status"` maps the type `Status` alone, while `public.Status` maps `Status` and `status` alike.  `public.Status` and `public.status` are two keys.  `public.status` and `public."status"` are one key, and so are `public.Status` and `public."Status"`: two entries for them are two entries for one type.
- **Of the entries for one type whose keys differ only by case, the most specific place's wins, as for any type.**  Among the entries of one place, the one whose key is spelled exactly as the type's name wins over one that matches it only ignoring case, so in one place `public.Status` and `public.status` each map their own type.  A plain key at a higher place replaces the entries below for every type it matches: a query's `public.status=default` takes both `Status` and `status` out of the project's entries.  To change one of two types whose names differ only by case, quote it: a query's `public."status"=default` takes `status` alone out, and `Status` keeps the project's entry.

**The type** is a C# type that a property can have:

- **A keyword**, `int`, `string`, `double` and the others of the built-in types; **a full name from its namespace**, `System.DateTimeOffset` or `MyApp.Ids.UserKey`, which may start with `global::`; or **a short name**, without its namespace, `DateTimeOffset` or `UserKey`.  A nested type is written with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.
- **A short name is the one type of that name** and number of type arguments, in any namespace, that your project and its references have and your project can see: a type of a reference counts when it is `public`, or `internal` in a reference that gives your project its internals with `InternalsVisibleTo`.  A type nested in another, and a `file` type, are not found by one.  Your project's own type wins over a reference's of the same full name.  The generated code writes the type's full name, so the `using` lines of your files do not matter, and a short name and the full name of its type are one type, to [shared models](reference/models.md#shared-models) and to converters.  A type argument may be a short name too, in any name: `System.Collections.Generic.List<UserKey>`, or `List<UserKey>`.  `global::UserKey` is a type of the global namespace alone.  A short name that finds a type of an assembly your project has only through a package it does not reference itself is the warning [SQLSRC284](diagnostics.md#sqlsrc284), since it may not be the type you meant; write the full name to use it without the warning.
- **A generic type** names its type arguments, each of which may end in `?`: `System.Collections.Generic.List<int?>`.  White space inside `<>` and `[]` is allowed.
- **An array** is the element and its ranks: `long[]`, `int?[]`, `int[,]`.
- **No `?` at the end of the whole type.**  Whether a property can be null is decided by its column, by which columns can be null on [PostgreSQL](reference/databases/postgresql/nullability.md#nullability) and on [SQL Server](reference/databases/sqlserver/nullability.md#nullability), or by its parameter's [`-- param:` marker](reference/parameters.md#param-marker), so `int4=int?` is not valid.  A tuple, `dynamic`, `void`, a pointer, and anything after the type are not valid either.
- **The type must exist and be usable.**  A type that the project and its references do not have, a full name that two references have, or a short name of two types in two namespaces, is [SQLSRC250](diagnostics.md#sqlsrc250), which then names them; one the project cannot see is [SQLSRC251](diagnostics.md#sqlsrc251); and one that no property can have, such as a `static` class, `System.Nullable<int>`, a type marked `[Obsolete]` as an error or a generic type whose type argument breaks a constraint, as `System.Collections.Generic.List<System.Nullable<string>>`, is [SQLSRC252](diagnostics.md#sqlsrc252).  Each is reported at the name of a query whose output is `models`, `methods` or `repository` and whose entry in its sidecar is current, once for the query: when the first of its columns and parameters that has no C# type, parameters before columns, is one whose type, or whose array's element or domain's base, has the entry's key.  An entry that no query uses is never an error, so a project can set its entries before it has the queries.

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

A type override, a [column type](reference/model-properties.md#setting-column-types) or a `-- param:` marker names a C# type, and the driver reads and sends only the types it knows.  A converter is how a [query method](reference/query-methods.md#query-methods) reads and sends a C# type of your choice, one the driver does not know among them, through a type the driver reads: a `static` method marked `[SqlSourceConverter]` that takes one value and returns it converted.  Above, `email` is a domain over `text`, which Npgsql reads as a `string`.  PostgreSQL gives a parameter compared with a column of a domain the domain's base type, `text` here, so [`-- param:`](reference/parameters.md#param-marker) declares `@email` as the domain, which the type override then gives the type `Email`.  SQL Server gives a parameter compared with, or written to, a column of an alias type that alias type, so with an alias type `dbo.Email` and `dbo.Email=MyApp.Email`, a SQL Server query needs no `-- param:`.  The method of `FindUser` reads the column `email` as a `string` and gives it to `FromDatabase`, and gives the `Email` of `@email` to `ToDatabase` and sends the `string` it returns.  `SqlSourceConverterAttribute` is generated into your project, beside `[SqlSourceGenerate]`, in the namespace `SqlSource`.

**A converter converts from its argument's type to its return type**, whatever its name.  A column needs the converter from the type the driver reads to its property's type, and a parameter the one back, so a query that has both, as `FindUser` does, needs both.  A query that needs the direction a type has no converter for, while the type has the other, is [SQLSRC280](diagnostics.md#sqlsrc280).  A method that takes and returns one type serves both directions: `System.DateTime AsUtc(System.DateTime value)` under `timestamp=System.DateTime` converts each `timestamp` as it is read and as it is sent.

**The type the driver reads** is the C# type that the type table of [PostgreSQL](reference/databases/postgresql/types.md#type-table) or [SQL Server](reference/databases/sqlserver/types.md#type-table) gives the database type, under the version of the driver your project references, with no type override or column type applied.  The converter takes or returns that type exactly:

- **A domain or an alias type is read as its base type**: a converter of a domain over `text` takes and returns a `string`, and one of a domain over `timestamptz` a `System.DateTime`, even where `timestamptz=System.DateTimeOffset` is in effect.  An array is read as an array of its element's type, `string[]` for `email[]`.
- **A converter of another type is not used.**  A type of yours whose converters are all of other types makes the query [SQLSRC279](diagnostics.md#sqlsrc279), which names the type to convert from and to.  A converter of an `int` never reads a `bigint` column, which the driver reads as a `long`: SqlSource never narrows or widens a value.
- **The type can change with the driver.**  Npgsql 8 and 9 read a `date` as a `System.DateTime`, and Npgsql 10 as a `System.DateOnly`, as [which version of Npgsql](reference/databases/postgresql/types.md#which-npgsql) says.  So after an update of Npgsql, a type of yours that a type override gives a `date`, or another type that the update changes, can need converters of another type, and SQLSRC279 says which.
- **A database type that the driver reads as no C# type**, a composite type or `regclass` for one, has nothing to convert from: a type override or a column type that gives it a type with converters is SQLSRC279.  Give a composite the type that the application's data source maps it to, with no converter.

**Where a converter is found.**  SqlSource finds a converter in two places:

- **in your project's own code**, in any type: on the type itself, as `Email`'s above, or in a class of conversions;
- **on the type that a type override, a column type or a `-- param:` marker names**, when that type is declared in a referenced assembly, such as a library of your ids.  The library references SqlSource for the attribute, and its own build reports its converters' errors.

A converter in any other type of a referenced assembly is not found.  When your project and a library's type both convert between the same two types, your project's converter is used, with no error, so that a project can replace a library's conversion.

**SqlSource's own conversions.**  On SQL Server, the query methods of a project that references NodaTime convert NodaTime's types themselves, from and to the types Microsoft.Data.SqlClient reads, as [NodaTime on SQL Server](reference/databases/sqlserver/libraries.md#nodatime) lists, whether the [library types](#setting-library-types) turn NodaTime on or not.  They convert System.Text.Json's types the same way, from and to a `string`, whether the library types turn `json` on or not: on SQL Server `JsonElement`, `JsonDocument`, `JsonNode`, `JsonObject` and `JsonArray`, as [JSON on SQL Server](reference/databases/sqlserver/libraries.md#json) lists, and on Npgsql 8 and 9 `JsonNode`, `JsonObject` and `JsonArray`, which those versions read only after `EnableDynamicJson()`, as [JSON on PostgreSQL](reference/databases/postgresql/libraries.md#json) says.  A converter of yours between the same two types, in the same direction, is used in place of SqlSource's, with no error, and SqlSource's is kept for the other direction: so `static NodaTime.Instant FromDatabase(System.DateTime value)` reads each `datetime2` that is an `Instant` your way, and an `Instant` parameter is still sent by SqlSource's conversion.  On PostgreSQL SqlSource converts nothing else, as Npgsql's plugins read and send NodaTime's and NetTopologySuite's types, and Npgsql the rest of System.Text.Json's.  On SQL Server, the query methods of a project that references `NetTopologySuite.IO.SqlServerBytes` read and send NetTopologySuite's geometries for a `geography` or a `geometry` themselves, as [NetTopologySuite on SQL Server](reference/databases/sqlserver/libraries.md#nettopologysuite) says, and no converter of yours replaces that.  A library's converter is used only when your project can call it: when it is `public`, or `internal` in a library that gives your project its internals with `InternalsVisibleTo`.  A library's converter that is not one, one your project cannot call among them, is [SQLSRC277](diagnostics.md#sqlsrc277) at each query whose method would use it, since your project cannot change it.

**A converter is a `static` method of one argument that returns a value**, `public` or `internal`, in a type that is not generic.  A method marked `[SqlSourceConverter]` that is not one is SQLSRC277, which lists each rule, in any project, whatever its output.  Its argument may be written with `?`, `Email?` or `string?`: it takes the type without the `?`, since it is never given null.  Its return type is never written with `?`.  A converter marked `[Obsolete]` as a warning is used, and the generated code turns the warning off; one marked `[Obsolete]` as an error is SQLSRC277.  Two converters between the same two types in one direction are [SQLSRC278](diagnostics.md#sqlsrc278), at both.

**Null never reaches a converter.**  A column that holds a null gives its property null when the property can be null, and throws as any column does when it cannot: see [how a row is read](reference/query-methods.md#reading-rows).  A parameter whose value is null is sent as the database's `NULL`.  So one pair of converters serves a property that can be null and one that cannot.

A PostgreSQL array parameter in a query method is an `IEnumerable<T>` of its element, as [array parameters](reference/databases/postgresql/query-methods.md#array-parameters) say, and its converters are those of the element.

**An array of PostgreSQL whose C# type is an array of a type of yours**, chosen for the element, as `email[]` under `public.email=MyApp.Email`, or for the array, as `public.user_id[]=MyApp.UserId?[]`, is converted element by element by the element's converters: a `MyApp.Email[]` column is read as a `string[]`, and each element given to `FromDatabase`.  A `NULL` element stays null, the converter not called for it, when the element is of a reference type or written with `?`, as `MyApp.UserId?`.  A `NULL` element of a value type written without `?`, as a `MyApp.Email[]` holds, throws `InvalidCastException`, "Column 'emails' holds a null element.".  The array is converted whole instead when a type override or a column type gives the array a type of yours that is not an array, `public.email[]=MyApp.EmailList`, or an array type that has converters of its own: those converters, between that type and `string[]`, are used, and the element's are not.

**Converters apply only where a type override, a column type, a `-- param:` marker or a library of the [library types](#setting-library-types) chose the type, and only in query methods.**  A column or parameter whose type the type table gives is never converted, so a converter between `System.DateTimeOffset` and a type of yours changes no `timetz` column.  A type that a library gives is converted only by a converter of the direction it needs, from the type the driver reads for a column and to it for a parameter, and is otherwise read and sent as the library has it, with no error: on PostgreSQL, a converter from `System.DateTime` to `NodaTime.Instant` reads each `timestamptz` that NodaTime gives an `Instant`, in place of Npgsql's plugin, and a parameter is still sent by the plugin when there is no converter back, while a converter between `NodaTime.Instant` and a type of yours changes no such `timestamptz`.  A query whose output is `models` calls no converter, and gets none of their errors at its name; to read it through Dapper, see [the recipe](recipes.md#models-through-dapper).  A type override to a type the driver reads for some database type, such as `string`, `System.DateTimeOffset` or `System.DateOnly`, is read and sent as the driver does when no converter is between it and the type the driver reads.

A query whose database's driver the project does not reference, which [SQLSRC273](diagnostics.md#sqlsrc273) is about, uses no converter either, and gets none of `SQLSRC277` to `SQLSRC280`.

**Without a converter**, the query method asks the driver for the type that was chosen.  That is right for a type the driver was taught, a C# enum after Npgsql's `MapEnum<T>()` for one, a composite after `MapComposite<T>()` or a type of a plugin, and for a type it reads already, such as `System.DateTimeOffset` for a `timestamptz`.  A type of yours that the driver does not know, with no converter, throws the driver's `InvalidCastException` when the query runs.  A class of yours in a column that holds JSON is the exception: System.Text.Json's serializer reads and sends it, as [a class in a JSON column](#json-class) says.  So are the types of SqlSource's own conversions, above.

<a id="json-class"></a>

#### A class in a JSON column

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

- **A class** is a type that is not one of System.Text.Json and not one that the driver reads for some database type, as the types of [PostgreSQL](reference/databases/postgresql/types.md#type-table) and [SQL Server](reference/databases/sqlserver/types.md#type-table) list them: a class or a record of yours, a struct, or a collection, `List<Tag>` or `Tag[]`, which is serialized whole.  So a `JsonElement`, a `string` or a `long` given to a JSON column is read as before.
- **A type that holds JSON** is PostgreSQL's `json` and `jsonb`, a domain over one, and SQL Server's `json` and an alias type over it, with the [library types](#setting-library-types)' `json` on or off.  A column or parameter of another type that the type map reads as a `string`, such as `text` or `nvarchar(max)`, holds JSON only by [the JSON opt-in](#json-opt-in).  Without it, a class given to such a column keeps the rules above: its converters are used, and without them the driver is given the class, which throws `InvalidCastException`.  So an `Email` of yours over an `nvarchar` is never written as JSON.
- **An array of PostgreSQL's JSON type**, `jsonb[]` given an `OrderMetadata[]`, is read and sent element by element.  A `NULL` element stays null for a class, and throws `InvalidCastException` for a struct written without `?`, as for an array that a converter converts, above.
- **The default options** are System.Text.Json's own: property names as your class writes them, `Channel`, and matched with their case.  So JSON whose names are another case, `{"channel":"web"}`, written by other code in camelCase, reads as an `OrderMetadata` whose properties have their default values, with no error.  For other options, such as camelCase names, write a converter: see [the recipe](recipes.md#class-in-a-json-column).
- **A JSON `null`**, the text `null`, is not a SQL `NULL`.  Read into a property of a class that cannot be null, it throws `InvalidCastException`, "Column 'metadata' holds a JSON null.", and into one that can be null it is null.  An element of an array that cannot be null throws the same, and one of `OrderMetadata?[]` is null.  A struct of yours follows the same rule: a JSON `null` is null in a `Point?` property or a `Point?[]` element, and throws the same `InvalidCastException` in a `Point` property or a `Point[]` element.  A SQL `NULL` never reaches the serializer, and gives its property null, or throws as any column does: see [how a row is read](reference/query-methods.md#reading-rows).
- **Text that is not your class's JSON**, such as an array for an object, throws System.Text.Json's `JsonException` when it is read.
- **Your converters replace the serializer.**  A [converter](#converters) of yours between `string` and the class is used in place of the serializer, in its direction, so that you choose the options: see [the recipe](recipes.md#class-in-a-json-column).  A SQL `NULL` never reaches it, but a JSON `null` does, as the text `null`, and what it gives for that is the converter's choice.  A class with a converter of one direction alone is [SQLSRC280](diagnostics.md#sqlsrc280) for a query that needs the other, and one whose converters are all of other types, such as `long`, is [SQLSRC279](diagnostics.md#sqlsrc279), and is not serialized.
- **In a trimmed or AOT project** the serializer reads and writes your class by reflection, so the compiler warns, IL2026 and IL3050, at its calls in the generated methods, and an application whose publish turns System.Text.Json's reflection off, as a trimmed or AOT one does by default, throws `InvalidOperationException` when the method runs.  Write converters that use a `JsonSerializerContext` that System.Text.Json's source generator writes: see [the recipe](recipes.md#class-in-a-json-column).
- **Only query methods use the serializer.**  A query whose output is `models` has no method, and its models keep the class as their property's type: read through Dapper, it needs a type handler of yours, as any type of yours does.

<a id="json-opt-in"></a>

### The JSON opt-in

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

The JSON opt-in says that a column or a parameter of a string type holds JSON, so that the class of yours it is given is read and sent through System.Text.Json's serializer, as a class in a `json` or `jsonb` column is: see [a class in a JSON column](#json-class).  Above, `orders.metadata`, `orders.tags` and `customers.preferences` are `nvarchar(max)` columns that hold JSON, as a database before SQL Server 2025, which has no `json` type, keeps it.  `orders.metadata` is an `OrderMetadata` in every query of the project, `orders.tags` a `List<Tag>` in every query of the file, `@metadata` an `OrderMetadata` and the column `preferences` of `GetPreferences` a `CustomerPreferences`, each read and sent as JSON, while `orders.created_by` is a `UserId`, read and sent by its [converters](#converters).

- **Where it is written.**  In a settings file, a column type's value is an object: `type` is its C# type, and `json`, `true` or `false`, says whether the column holds JSON.  `false`, or no `json`, is the plain mapping, as the string `"OrderMetadata"` is.  In a marker, the word `json` and white space come before the C# type, after `->`: in the file header's `-- column:`, in a query's `-- column:` and in a `-- param:`.  The word is matched ignoring case.  `-> json` alone gives the column a type named `json`.
- **It is not in the type override**, which maps a database type and not a column that holds JSON: a domain over `text` that holds JSON is mapped by its columns, or by each query's markers.
- **It applies to a column or parameter that the type map reads as a `string`**, such as PostgreSQL's `text` and `varchar` and SQL Server's `nvarchar(max)`, which is then read and sent as a `json` column of the same class is, a parameter being sent as its own database type.  On a `json` or `jsonb` column it is allowed and changes nothing.
- **Its C# type is a class of yours**, as [a class in a JSON column](#json-class) says, and an array or a collection of one is serialized whole.  The opt-in on a column or parameter of another database type, such as `int` or `text[]`, or with a type of System.Text.Json or one that the driver reads, such as `JsonElement` or `string`, is [SQLSRC283](diagnostics.md#sqlsrc283) at a query whose output is `methods` or `repository`, and the query gets no method.  A query whose output is `models` takes the opt-in's type for its property and reads no more of it.
- **A [converter](#converters) of yours between `string` and the class** replaces the serializer here too.
- **Two places that differ only in `json`**, two `-- param:` markers of one parameter, two `-- column:` markers of one column in a query or two `-- column:` lines of one key in the file header, are [SQLSRC112](diagnostics.md#sqlsrc112).  `-> json default`, and an object whose `json` is `true` and whose type is `default`, are not valid: [SQLSRC111](diagnostics.md#sqlsrc111) in a marker, and [SQLSRC019](diagnostics.md#sqlsrc019) in a settings file, which also reports an object with another member than `type` and `json`.
- **Nothing is described again.**  Adding or removing the opt-in, at any place, leaves the query's sidecar current, so it needs no `dotnet sqlsource describe`.

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

The library types say whether the query methods read and send a library's types, in place of the types of the type map, for the database types the library covers.  There are three libraries.  NodaTime, named `nodaTime`, gives on PostgreSQL a `timestamptz` the type `NodaTime.Instant`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on PostgreSQL](reference/databases/postgresql/libraries.md#nodatime) lists, and on SQL Server a `datetime2` the type `NodaTime.LocalDateTime`, a `date` a `NodaTime.LocalDate`, and so on, as [NodaTime on SQL Server](reference/databases/sqlserver/libraries.md#nodatime) lists.  NetTopologySuite, named `netTopologySuite`, gives `NetTopologySuite.Geometries.Geometry` to PostGIS's `geometry` and `geography` on PostgreSQL, as [NetTopologySuite on PostgreSQL](reference/databases/postgresql/libraries.md#nettopologysuite) says, and to `geography` and `geometry` on SQL Server, as [NetTopologySuite on SQL Server](reference/databases/sqlserver/libraries.md#nettopologysuite) says.  System.Text.Json, named `json`, gives PostgreSQL's `json` and `jsonb` and SQL Server's `json` the type `System.Text.Json.JsonElement`, as JSON on [PostgreSQL](reference/databases/postgresql/libraries.md#json) and on [SQL Server](reference/databases/sqlserver/libraries.md#json) says.  Above, the project references `Npgsql.NodaTime`, so NodaTime would be on for all of it; the project's settings file turns it off, the files under `Scheduling/` give it back to its default, on, and the query `ListShifts` turns it off again, so its `starts_at`, a `timestamptz`, stays a `System.DateTime`.

**The state** of a library is `on`, `off` or `default`, matched ignoring case, hyphens and spaces, as the library's name is.  `default` takes the library out of the entries of every place below, so that its default applies, as a type override's `default` gives a type back to the type map.

**The default**, where no place names the library or the nearest that does says `default`:

- **PostgreSQL**: for NodaTime, `on` when the project references `Npgsql.NodaTime`, the plugin that lets Npgsql read and send NodaTime's types, directly or through another package such as Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`, and `off` otherwise.  A project that references `NodaTime` alone has it off.  For NetTopologySuite the same with `Npgsql.NetTopologySuite`, which Entity Framework Core's `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` brings.  `json` is `off`.
- **SQL Server**: `off` for all three, whatever the project references.

**Every place, merged by library.**  The library types are `libraryTypes` in the settings files of a `.sql` file's folder and of the folders above it, and the `-- library-types:` marker in the file header and inside a query.  For each query the entries add up from the farthest settings file to the nearest, then the file header, then the query, and an entry for a library replaces the entry of a place below for that library alone, as the [type override](#setting-type-overrides)'s entries do.  One value serves both databases: a file's dialect decides which database's types apply.

**Only for query methods.**  A library's types reach only a query whose [output](#setting-output) is `methods` or `repository`, for each type that claims it.  A query whose output is `models` keeps the type map's types in its models, whatever the library types say: there they are read and checked, and do nothing else.  To give such a query a library's types, as for rows read through Dapper, name them with the type override or the column types.  A model that a query with a method, with the library on, and a `models` query share, or two queries with methods, one with the library on and one with it off, has two sets of types and is [SQLSRC257](diagnostics.md#sqlsrc257): name the models apart with [`-- output-model:`](reference/models.md#setting-model-name), or turn the library on or off alike for both.

**Below your own choices.**  A column's or parameter's C# type is its query's `-- column:` marker's or its `-- param:` marker's, then the [column types](reference/model-properties.md#setting-column-types)', then the [type override](#setting-type-overrides)'s, then the library's, then the type map's: see [property types](reference/type-mapping.md#property-types).  So a project that maps `timestamptz` with the type override keeps its type with NodaTime on, and `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default`, gives `timestamptz` back to the library there.

**What `on` needs.**  The project references what the library needs on the database of the query: on PostgreSQL the plugin, `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, and on SQL Server `NodaTime`, or for NetTopologySuite `NetTopologySuite.IO.SqlServerBytes` with NetTopologySuite 2.5.0 or later referenced by name, since `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, which is [SQLSRC282](diagnostics.md#sqlsrc282); for `json`, System.Text.Json, which every .NET project has.  An entry that says `on` for a `methods` or `repository` query of a project without it is [SQLSRC281](diagnostics.md#sqlsrc281), at the entry, and the query keeps the type map's types.  A query method that reads NodaTime's types on PostgreSQL needs `UseNodaTime()` on the application's data source: see [PostgreSQL's query methods](reference/databases/postgresql/query-methods.md#method-results).  On SQL Server the query methods convert NodaTime's types themselves, and nothing is registered in the application.  `JsonElement` needs nothing registered on either database.  A query method that gets NodaTime's types from the library types, or on SQL Server converts a NodaTime type of your choice, needs NodaTime 3.1.9 or later: an older one is [SQLSRC282](diagnostics.md#sqlsrc282), and the query gets no method.  One that reads NetTopologySuite's types on PostgreSQL needs `UseNetTopologySuite()` on the data source in the same way, and on SQL Server nothing.  One that uses NetTopologySuite needs NetTopologySuite 2.5.0 or later, and on SQL Server also `NetTopologySuite.IO.SqlServerBytes` 2.1.0 or later: an older one is SQLSRC282 too.

**Only what you did not choose.**  The library types decide the type of a column or parameter that nothing else gives one.  A type you choose with the type override, a column type or a marker is read and sent the same whether its library is on or not: on SQL Server, `datetime2=NodaTime.Instant` is converted for a project that references NodaTime with NodaTime off, as [NodaTime on SQL Server](reference/databases/sqlserver/libraries.md#nodatime) says, `geography=NetTopologySuite.Geometries.Point` is read and sent for one that references `NetTopologySuite.IO.SqlServerBytes` with NetTopologySuite off, and a `JsonDocument` or a class of yours that you give a `jsonb` column is read and sent the same with `json` on or off.

**In a settings file**, `libraryTypes` is a JSON object with a member for each library, `"nodaTime": "on"` or `"netTopologySuite": "off"`.  A member whose library or state is not one is [SQLSRC019](diagnostics.md#sqlsrc019), and the other members still apply.  Two members for one library, `"nodaTime"` and `"NodaTime"`, are [SQLSRC020](diagnostics.md#sqlsrc020), and the first applies.

**In a marker**, a `-- library-types:` line holds one entry or more, each `name=state` with no space around its `=`, separated by white space: `-- library-types: nodatime=on nettopologysuite=off json=on`.  The lines of one place add up, one for each library, and the same state twice is fine.  Two states for one library in the file header, or inside one query, are [SQLSRC112](diagnostics.md#sqlsrc112).  A line with no entry, or an entry that is not valid, is [SQLSRC111](diagnostics.md#sqlsrc111).

The `sqlsource` tool does not read `libraryTypes`, and a `-- library-types:` marker changes nothing that is described: adding, changing or removing one leaves the query's sidecar current.

<a id="setting-source"></a>

## Source

```csharp
[SqlSourceGenerate]                                  // UserRepository.sql, beside this C# file
public partial class UserRepository;

[SqlSourceGenerate(Source = ".")]                    // every .sql file beside this C# file
public partial class StoreQueries;

[SqlSourceGenerate(Source = "Queries/Users.sql")]    // one file
public partial class UserQueries;

[SqlSourceGenerate(Source = "../Queries")]           // the .sql files of a folder
public partial class SharedQueries;
```

`Source` is a property of the attribute and nothing else.  It says which `.sql` files belong to the type.

- Without `Source`, the type gets the one `.sql` file named for it in the folder of the C# file that carries the attribute: `UserRepository.sql` for `UserRepository`.  The name is the type's own, without its type parameters, so `Repository<T>` gets `Repository.sql`, and a nested type `Outer.Inner` gets `Inner.sql`.  For a partial type, the folder is the one of the file that carries the attribute.
- A `Source` that ends in `.sql` names one file.  Any other `Source` names a folder, whose own `.sql` files the type gets: `Source = "."` is every `.sql` file in the folder of the C# file.  Subfolders are not searched.
- `Source` is relative to the folder of the C# file, never to the project.  Both `/` and `\` separate folders.  Paths are compared ignoring case, so a project builds the same on every operating system: two `.sql` files of a type whose paths differ only by case are [SQLSRC013](diagnostics.md#sqlsrc013).
- An empty `Source`, `Source = ""`, is the same as no `Source`.  A `Source` of white space is not: it is read as the name of a folder, like any other `Source`, and so matches nothing.
- A `Source` that matches nothing is [SQLSRC004](diagnostics.md#sqlsrc004), and no file named for the type when there is no `Source` is [SQLSRC005](diagnostics.md#sqlsrc005).
- Two types may use the same file.  A `.sql` file that no type uses is ignored, so a folder of migration scripts elsewhere in the project does no harm.

This is separate from [which files the compiler is given](#included-files), which comes first: a type can only claim a file that the project includes.  The attribute itself is on [the Attributes page](reference/attributes.md#attribute).

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
    "generatorParameters": ["token-validation=off"],
}
```

Every file under `Legacy/` is read as `mysql`, keeps its comments, and skips the token check.  Every other file is untouched: it is read as `postgres`, keeps its comments, and checks its tokens.  Three things to notice:

- The folder's list names only `token-validation=off`.  It adds to the project's list, so the files of `Legacy/` keep their comments without saying so.  A folder that wants none of the project's list starts its own with `default`: `["default", "token-validation=off"]` would give `Legacy/` every default but the token check.
- The dialect takes the nearest value whole.  Had the project's file set `"mysql,ansi-quotes"` as its dialect, this folder would still be read as plain `mysql`.
- A file's own markers still win.  A `-- generator:` line in one of the files adds its words to the list of both settings files, and `-- generator: default` drops that list, and a `-- dialect: mariadb` overrides both settings files.  See [which place wins](#precedence).

The file in `Legacy/` applies to the `.sql` files of its subfolders too, `Legacy/Reports/` among them, unless a settings file nearer to them sets the same setting.  Every setting of a query comes from the settings files of its `.sql` file, whichever type claims it, and never from those of a type's C# file.

Only the settings whose row in [the table](#all-settings) lists a settings file can be given this way.  To change a setting for one file, write its marker in the file header, which wins over every settings file.

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

Turning the default off turns it off for sidecars too, so list them as well, as the example does.  A build that [runs the tool](reference/msbuild.md#running-in-the-build) compiles a sidecar it has just written for the first time, listed or not; every later build, like a build offline or in CI, compiles only the sidecars you list.  A file the project includes is only handed to the compiler.  Which of them belong to a type is the type's [`Source`](#setting-source), and a file no type claims is ignored.

<a id="online-build"></a>

## Running the tool in the build

```xml
<PropertyGroup>
    <SqlSourceOnline>false</SqlSourceOnline>
</PropertyGroup>
```

`SqlSourceOnline` says whether a build of the project runs `dotnet sqlsource describe` on it before it compiles, as [running in the build](reference/msbuild.md#running-in-the-build) describes.  `false` above keeps the build on the committed sidecars on every machine.

| Value | The build runs the tool |
|----|----|
| `auto`, empty, or not set | When it finds a connection: the environment variable `SQLSOURCE_CONNECTION`, set and not blank, or the build's `.env` file, the one in the project's folder or the one [`SqlSourceEnvFile`](#setting-env-file) names, when it is there.  A `.env` in a folder above the project's is not looked for.  When the tool then finds no connection for any database with a query to describe, it says the build is offline, and the build goes on with the committed sidecars |
| `true` | Whether it finds a connection or not.  A database with a query to describe and no connection is then [SQLSRC213](diagnostics.md#sqlsrc213), and a tool that cannot be started [SQLSRC025](diagnostics.md#sqlsrc025) |
| `false` | Never |

- **The value is matched ignoring case** and the white space around it, so ` True ` is `true`.  Any other value, such as `yes`, is [SQLSRC024](diagnostics.md#sqlsrc024), and the build stops.
- **A build in CI never runs the tool**, whatever the value, so that a job builds from the sidecars that were committed.  A build is in CI when the variable `CI` is `true`, ignoring case, `TF_BUILD` is `True`, or `TEAMCITY_VERSION` or `JENKINS_URL` is set: GitHub Actions, GitLab, Buildkite, Azure Pipelines, TeamCity and Jenkins each set one.  `CI=1` is not recognised.
- **A connection in a variable of a database's name alone**, such as `SQLSOURCE_CONNECTION_BILLING` with no `.env` file, is not found under `auto`.  Set `SqlSourceOnline` to `true` in your own environment, which MSBuild reads as a property of every project, rather than in the project, so that a machine with no database still builds.
- **The build's `.env` file turns `auto` on whatever it holds**, one that holds only your application's settings too: the build looks only whether it is there.  The tool then finds no connection and the build goes on offline, and each build starts the tool again while it finds none.  Set `false` to stop that, or `SqlSourceEnvFile` to `none`.  So does a symbolic link that leads to no file, as [the build's `.env` file](#setting-env-file) says.
- **A `.env` above the project's folder**, at the root of your solution or repository, or in your home folder, does not turn `auto` on, and the build says nothing of it: it builds from the committed sidecars.  To use such a file, name it with [`SqlSourceEnvFile`](#setting-env-file).
- **Where it is written.**  In the project file, in a `Directory.Build.props` above it for every project there, on the command line as `-p:SqlSourceOnline=false`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.

<a id="setting-env-file"></a>

## The build's `.env` file

```xml
<!-- Directory.Build.props, at the root of the repository -->
<Project>
    <PropertyGroup>
        <SqlSourceEnvFile>$(MSBuildThisFileDirectory).env</SqlSourceEnvFile>
    </PropertyGroup>
</Project>
```

`SqlSourceEnvFile` names the [`.env` file](reference/connections.md#env-file) that a build of the project reads its connections from, in place of the `.env` in the project's folder.  The `Directory.Build.props` above gives every project below it the one `.env` beside it, at the root of the repository: `$(MSBuildThisFileDirectory)` is the folder of the file that writes it.

| Value | The build reads |
|----|----|
| Empty, or not set | The `.env` in the project's folder, and never one in a folder above it |
| A path | That file.  A relative path is from the project's folder, so `../.env` is the `.env` of the folder above it |
| `none` | No `.env` file: the connections come from the environment alone |

- **The value is trimmed**, and `none` is matched ignoring case.  To name a file called `none`, write `./none`.
- **It decides whether `auto` runs the tool**, as [running the tool in the build](#online-build) says: the file named turns it on when it is there.  A path where there is no file, or a folder, turns nothing on and is no error.  Under `none` only `SQLSOURCE_CONNECTION` in the environment turns `auto` on.  A symbolic link that leads to no file is the exception: the build counts it as there and the tool reads nothing from it, so each build starts the tool, which goes on offline.  Remove the link, or set `SqlSourceEnvFile` to `none`.
- **A change to the file** runs the tool again at the next build, as a change to the project's own `.env` does.
- **It reaches only the build's run.**  A `describe` or `describe --watch` that you run yourself reads the `.env` in the folder of the project or solution it runs on, and `--env-file` names another, as [the `.env` file](reference/connections.md#env-file) says: with the example above, `dotnet sqlsource describe src/App/App.csproj --env-file .env` at the root of the repository reads the file the build reads.
- **Where it is written.**  In the project file, in a `Directory.Build.props` above it for every project there, on the command line as `-p:SqlSourceEnvFile=none`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.

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

A settings file is a file named `sqlsource.json`, in any folder.  It sets the settings of the `.sql` files of its folder and of every folder below it.  The one above, in the project's folder, reads every `.sql` file of the project as PostgreSQL, gives each query its models and no method, keeps comments and sorts the output models' properties, and gives every `timestamptz` the C# type `System.DateTimeOffset` and every `users.id` column `MyApp.UserId`.  A setting it does not name keeps its default, and a marker still wins over it, as [which place wins](#precedence) says.

**Its name** is `sqlsource.json`, in lower case.  A file named in another case, such as `SqlSource.json`, is not read when the project file lists it itself as an `AdditionalFiles` item.  When it is not listed, the build finds it and reads it on macOS and Windows, which compare names ignoring case, and does not find it on Linux, so a project built on both would read different settings.

**What it holds** is one JSON object, whose members are the settings.  It may hold `//` and `/* */` comments, and a comma after the last member of an object or the last element of an array.

- **The keys** are those of [the table](#all-settings), `root` and `$schema`, matched exactly, case included: `Dialect` is not a key.  A key that is not one is [SQLSRC016](diagnostics.md#sqlsrc016), whose section lists them all, and a key written twice is [SQLSRC017](diagnostics.md#sqlsrc017).
- **Each value is a JSON string**, except `root`, which is `true` or `false`, `generatorParameters`, an array of strings, `typeOverrides` and `libraryTypes`, each an object whose values are strings, and `columnTypes`, an object whose values are strings, or objects of a `type` and a `json` for [the JSON opt-in](#json-opt-in).  A value of another JSON type, `null` among them, is [SQLSRC018](diagnostics.md#sqlsrc018).
- **A string is read as JSON writes it.**  The white space around it is ignored, and every character inside it is kept, a `;` or a `#` among them; there is no escape but JSON's own.  A string that is empty or white space sets nothing, and is no error.  To leave a setting to the files above, or to its default, leave its key out.
- **`$schema`** is for your editor, which then checks the keys and values as you write them and offers them as you type: `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`.  SqlSource reads nothing of it.

**The settings files of a `.sql` file** are the `sqlsource.json` of its folder and those of each folder above it, to the top of the drive, nearest first.  For each setting the nearest that sets it wins, so a file in a folder sets only what differs from the folders above it; the entries of `typeOverrides`, `columnTypes` and `libraryTypes` add up instead, key by key, as [type overrides](#setting-type-overrides) says, and the words of `generatorParameters` parameter by parameter, as [generator parameters](#setting-generator-parameters) says.  A file that says `"root": true` is the last: no `sqlsource.json` in a folder above it applies to the files below it.  Put one in the project's folder, or at the top of the repository, so that a settings file above it, elsewhere on the machine, never changes your build.  `"root": false`, or no `root`, lets the search go on.  A file whose text is not JSON ([SQLSRC015](diagnostics.md#sqlsrc015)) gives nothing, its `root` included, so the search goes past it to the folders above.

- **A `.sql` file that the project lists from another folder**, outside the project's folder, takes the settings files of its own folder and the folders above it, not those of the project's folder.
- **A settings file in a folder that holds no `.sql` file and no C# file of the project**, in it or in a folder below it, is not read, and applies to nothing.

**Which files are checked.**  The build reads each settings file that the search of some `.sql` file or C# file of the project reaches, whether or not a type claims that `.sql` file, and reports each mistake in it at its file, line and column.  A `sqlsource.json` beyond the first `"root": true` is never read, so it cannot fail a build.  The C# files that the build writes itself, into its output and intermediate folders, reach no settings file, wherever those folders are, except in a build that names the project through a symbolic link while those folders are outside the project's folder and set by a relative path: see [known issues](#known-issues).  Nor do the C# files that a NuGet package adds to your project, as Polyfill does, from the folder where NuGet keeps its packages, so a broken `sqlsource.json` above that folder, in your home folder, say, is not read on their account.  Your own `.sql` and C# files still reach such a file when your project is in a folder below it, unless a `"root": true` stands between them, and its mistakes then fail the build and `sqlsource describe`: put `"root": true` in the settings file of your project's folder, or of your repository's.  A C# file that your project lists itself from another folder still reaches the settings files of its own folder.  A value that is wrong is not set by its file, so a farther settings file, or the default, gives the setting meanwhile.  The `sqlsource` tool reads the same settings files.  It reports the mistakes of [SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC018](diagnostics.md#sqlsrc018) as the build does, and a value of `dialect`, `output` or `database` that is not valid, once in a run however many projects reach the file.  The build reports a value that is not valid for every key but `database`, which only the tool reads.

**In the build**, SqlSource keeps a settings file out of the output and publish folders where the SDK would copy it, as the Web SDK copies every `.json` file of a web project.  A copy rule of your own still copies it: a rule written for `appsettings.json`, such as `<None Update="**/*.json" CopyToOutputDirectory="PreserveNewest" />`, copies every `sqlsource.json` of the project too.  So name the files such a rule copies, as `Update="appsettings*.json"` does, or follow it with `<None Update="**/sqlsource.json" CopyToOutputDirectory="Never" CopyToPublishDirectory="Never" />`.  Adding, deleting, moving or editing one makes the next build compile again, and an IDE that builds the project reads the same files.  The project file holds no setting: an MSBuild property or item metadata named after a setting is not read, and nothing reports it, so move such a value to a settings file.  The MSBuild properties of SqlSource that a project sets are [`SqlSourceIncludeFiles`](#included-files), which says which `.sql` files are included, [`SqlSourcePublicExceptions`](reference/exceptions.md#public-exceptions), which makes the exceptions of the query methods public, [`SqlSourceOnline`](#online-build), which says whether the build runs the `sqlsource` tool, and [`SqlSourceEnvFile`](#setting-env-file), which names the `.env` file it reads.

See: [SQLSRC015](diagnostics.md#sqlsrc015), a file that is not a JSON object; [SQLSRC016](diagnostics.md#sqlsrc016), a key that is not known; [SQLSRC017](diagnostics.md#sqlsrc017), a key written twice; [SQLSRC018](diagnostics.md#sqlsrc018), a value of the wrong JSON type; [SQLSRC019](diagnostics.md#sqlsrc019), a value that is not valid; [SQLSRC020](diagnostics.md#sqlsrc020), a key repeated in `typeOverrides`, `columnTypes` or `libraryTypes`, or a generator parameter given two different values in `generatorParameters`.

<a id="known-issues"></a>

## Known issues

- **A build that names the project through a symbolic link can miss a settings file or a `.sql` file.**  When you name the project by a path through a link, as `dotnet build ~/code/App/App.csproj` does where `~/code` leads to `/Volumes/Data/code`, the settings files in the project's folder and in the folders above it up to the link's own, `~/code`, apply.  One in a folder above the link applies only when that folder is also above the folder the link leads to: one in `/` does, and one in `~` does not.  A `.sql` or C# file that your project lists by its full path, through `$(MSBuildThisFileDirectory)` or `$(MSBuildProjectDirectory)`, say, gets only the settings files outside the link, in `~` or `/` here, and none of those in `~/code` or below it, so a query in it may report [SQLSRC209](diagnostics.md#sqlsrc209), and such a `.sql` file is not found by its type, [SQLSRC004](diagnostics.md#sqlsrc004) or [SQLSRC005](diagnostics.md#sqlsrc005), when the type's C# file is listed by a relative path, as the SDK lists it.  And when your build's output or intermediate folder is outside the project's folder, set by a relative path such as `<BaseIntermediateOutputPath>../../obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>`, a broken `sqlsource.json` above your `"root": true` can fail the build.  Name the project by its real path, or build in its folder without naming it, such as `cd "$(pwd -P)"` before `dotnet build`.
- **A `.sql` file that one of your own build targets adds is not described.**  This is about a target in your project that adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as one that writes a file or copies it in.  The build compiles the file, but `sqlsource` leaves out the file's queries, so it describes none of them, and the build reports [SQLSRC241](diagnostics.md#sqlsrc241) for each that must be described.  To make it work, tell your target to run before the package lists the project's files, wherever the target is declared:

  ```xml
  <Target Name="AddGeneratedSql" BeforeTargets="SqlSourceListFiles">
      <ItemGroup>
          <AdditionalFiles Include="obj/generated/*.sql" />
      </ItemGroup>
  </Target>
  ```

  Change the name and the path to your own.  `BeforeTargets="SqlSourceListFiles"` is the line that matters.  A build that [runs the tool](reference/msbuild.md#running-in-the-build) describes such a file without it in a project with one target framework, and leaves it out, as `sqlsource` does, in a project with several.  In a project that is not of the .NET SDK, or one that sets `AfterMicrosoftNETSdkTargets` without keeping its value, a target that runs later also leaves the file without the settings of a `sqlsource.json` that only it reaches, such as the one beside it, with no error; the same line fixes that too.
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
