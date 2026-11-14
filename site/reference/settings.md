# Settings: every setting, and where you write it

A setting is a named choice that changes what SqlSource generates or does, such as which dialect the SQL is written in, which comments to keep or which database the tool asks about a query, and that you write in one of five places.  A setting can apply to a whole project, to some of its files, to one type or to one query.  This page shows the five places, lists every setting with the places that can hold it, and says which place wins when a setting is written in several.  The output, the database and the dialect are defined below.  Every other setting is defined on the page of the thing it changes.

The settings file, `sqlsource.json`, has [a page of its own](settings-file.md): its name, what it holds, and which `.sql` files it reaches.

<a id="places"></a>

## The places a setting is written

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

```xml
<PropertyGroup>
    <SqlSourcePublicExceptions>true</SqlSourcePublicExceptions>
</PropertyGroup>
```

| Place | Written as | Applies to |
|----|----|----|
| A marker inside a query | A `-- word:` line under a `-- name:` line | That query |
| A marker in the file header | A `-- word:` line before the first `-- name:` line | Every query of the file |
| A key of a settings file | A member of `sqlsource.json` | The `.sql` files of its folder and of every folder below it |
| A property of an attribute | `[SqlSourceGenerate(Source = "...")]` | The type, or the member, that carries the attribute |
| An MSBuild property of the project | An element of the project file or of a `Directory.Build.props` above it, `-p:` on the command line, or a variable of the environment | The whole project |

A file with no `-- name:` line is one query, and takes the markers of the file header and those of a query.

The first block writes settings as markers in a `.sql` file, the second in a [settings file](settings-file.md#settings-files), `sqlsource.json`, which applies to the `.sql` files of its folder and of every folder below it, the third as a property of the attribute, and the fourth as an MSBuild property of the project.  Which of the five places a setting accepts is in [the table](#all-settings).  To read one folder of a project differently from the rest, see [configure one folder differently](../guides/folders-and-files.md#configure-a-folder).

These are not settings, and have no row in [the table of all settings](#all-settings):

- The markers that describe one query's own content: `-- name:`, `-- summary:`, `-- param:`, `-- token:` and `-- token-ignore:`.
- The keys `root` and `$schema` of a settings file.
- `[SqlSourceConverter]`, which has no property.
- The options and environment variables of the `sqlsource` command: see [the command reference](cli.md#command-reference).
- The MSBuild properties that the package sets for itself.

<a id="all-settings"></a>

## All settings

Each name links to its section, which gives its values and its rules.  The other columns give every place the setting can be written: a key of a [settings file](settings-file.md#settings-files); a marker, and whether it can stand in the **file header** (the part of a `.sql` file before its first `-- name:` line) and **inside a query** (under a `-- name:` line), as [the marker index](sql-files.md#marker-index) shows; a property of an attribute; and a property of the project, one of `SqlSourceRepositoryInterfaces`, `SqlSourceOnline`, `SqlSourceEnvFile`, `SqlSourceIncludeFiles` and `SqlSourcePublicExceptions`.  A place it does not list cannot hold it.  **Default** is the value when no place gives one.

| Setting | `sqlsource.json` key | Marker | In the file header | Inside a query | Attribute property | MSBuild property | Default |
|----|----|----|----|----|----|----|----|
| [Output](#setting-output) | `output` | `-- output:` | yes | yes | | | `repository` |
| [Database](#setting-database) | `database` | `-- database:` | yes | yes | | | The name of the file's dialect |
| [Dialect](#setting-dialect) | `dialect` | `-- dialect:` | yes | no | | | `ansi` |
| [Generator parameters](generator-parameters.md#setting-generator-parameters) | `generatorParameters` | `-- generator:` | yes | yes | | | `token-validation`, `table-models`, `array-pool`, `list-padding` and `transaction` on, `method-type` `async`, the others off |
| [Input and output model suffix](models.md#setting-model-suffix) | `inputModelSuffix`, `outputModelSuffix` | `-- input-model-suffix:`, `-- output-model-suffix:` | yes | no | | | `Params` and `Dto` |
| [Model namespace](models.md#setting-model-namespace) | `modelNamespace` | `-- model-namespace:` | yes | no | | | The namespace of the type |
| [Input and output model type](models.md#setting-model-type) | `inputModelType`, `outputModelType` | `-- input-model-type:`, `-- output-model-type:` | yes | yes | | | `sealed record` |
| [One query's model name](models.md#setting-model-name) | | `-- input-model:`, `-- output-model:` | no | yes | | | The query's name and the model suffix |
| [Collection type](return-types.md#setting-collection-type) | `collectionType` | `-- collection-type:` | yes | yes | | | `Array` |
| [Type overrides](type-mapping.md#setting-type-overrides) | `typeOverrides` | `-- type-override:` | yes | yes | | | None |
| [Column types](model-properties.md#setting-column-types) | `columnTypes` | `-- column:` | yes | yes, for a column of the query's result | | | None |
| [Library types](library-types-and-json.md#setting-library-types) | `libraryTypes` | `-- library-types:` | yes | yes | | | On for PostgreSQL where the project references the library's plugin, `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`, and off otherwise; `json` off |
| [Member location](members.md#setting-member-location) | `memberLocation` | `-- member-location:` | yes | yes | | | `nested-class` for the output `sql` or `models`, `extension-class` for `methods`, and `class` for `repository` |
| [Visibility](members.md#setting-visibility) | `visibility` | `-- visibility:` | yes | yes | | | `public` for `repository`, and otherwise by the member location, as [its section](members.md#setting-visibility) says |
| [Nested class](members.md#setting-nested-class) | `nestedClass` | `-- nested-class:` | yes | yes | | | `Sql` |
| [`Source`](attributes.md#setting-source) | | | no | no | `Source` | | The file named for the type in the folder of the C# file |
| [Repository interfaces](repositories.md#repository-interfaces) | | | no | no | `GenerateInterface`, `InterfaceName` | `SqlSourceRepositoryInterfaces` | On, named `I` and the type's name |
| [A repository's connection](repositories.md#repository-connection) | | | no | no | `Database` of `[SqlSourceConnection]` | | None: the build finds the connection source by its name or type |
| [Running the tool in the build](msbuild.md#online-build) | | | no | no | | `SqlSourceOnline` | `auto` |
| [The build's `.env` file](msbuild.md#setting-env-file) | | | no | no | | `SqlSourceEnvFile` | The `.env` in the project's folder |
| [Which `.sql` files are included](msbuild.md#included-files) | | | no | no | | `SqlSourceIncludeFiles` | On: every `.sql` file under the project's folder |
| [Public exceptions](exceptions.md#public-exceptions) | | | no | no | | `SqlSourcePublicExceptions` | Off: the exceptions are `internal` |

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

A query with `-- output: sql` has the output `sql`, whatever the other four say.  A query without it takes the file header's `models`, and so on down the table.  The settings files of a `.sql` file are the `sqlsource.json` of its folder and those of the folders above it, nearest first, up to the first that says `"root": true`: see [settings files](settings-file.md#settings-files).  To give the files of one folder their own values, see [configure one folder differently](../guides/folders-and-files.md#configure-a-folder).

The rules:

- **Each setting is decided on its own.**  A query that writes `-- output: sql` still takes its file header's `-- database:`.
- **A place that gives no value is skipped.**  A settings file that does not name the setting gives none, and so does a string of a settings file that is empty or white space.  `Source`, on the attribute, is the exception: see [Source](attributes.md#setting-source).
- **A setting has only the places its row of [the table](#all-settings) lists.**  A key of a settings file that is not in it, nor `root` or `$schema`, is [SQLSRC016](../diagnostics.md#sqlsrc016).  A marker where its row says it cannot stand is [SQLSRC116](../diagnostics.md#sqlsrc116), except a `-- dialect:` inside a query or after SQL, which is [SQLSRC115](../diagnostics.md#sqlsrc115): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).
- **Every type that claims a file gets the same settings for its queries.**  The places of a query's settings are the query, its file and the settings files of the file's folder, so two types that claim one file take the same settings from it: one output, one suffix, one shape of model, one [member location](members.md#setting-member-location).  Only what follows from the type itself can differ: the namespace of the models, which is the type's own unless the [model namespace](models.md#setting-model-namespace) is set, and the default [visibility](members.md#setting-visibility) of the members of `sql`, `models` and `methods` queries on the type, which follows the type's own accessibility.  A type that needs other settings needs a file of its own.

Three exceptions, stated plainly:

- **A dialect value replaces the one below it whole**, options included.  The dialect has no in-query level: a file has one dialect, which is the marker in its file header, else the nearest of its settings files that sets one, else `ansi`.  See [dialect](#setting-dialect).
- **`Source` exists only on the attribute.**  It has nothing to choose between.
- **A type override, a column type, a library of the library types, or a generator parameter, replaces only what it names.**  Each place adds its entries to those of the places below it, each settings file a place of its own, so a folder's file adds to the entries of the files above it.  An entry for a key, a database type, a column, a library or a generator parameter, replaces the entry below it for that key, and `default` takes the key out.  The generator parameters also take `default` alone, which takes every parameter out, back to its default.  See [type overrides](type-mapping.md#setting-type-overrides), [column types](model-properties.md#setting-column-types), [library types](library-types-and-json.md#setting-library-types) and [generator parameters](generator-parameters.md#setting-generator-parameters).

<a id="a-value-that-is-wrong"></a>

### A value that is wrong

| Written as | Reported as | What happens |
|----|----|----|
| A marker | [SQLSRC111](../diagnostics.md#sqlsrc111), a dialect included.  A generator parameter that does not exist is [SQLSRC109](../diagnostics.md#sqlsrc109); a value that contradicts another in the same place is [SQLSRC112](../diagnostics.md#sqlsrc112) | The file produces no members until it is fixed |
| A settings file | [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC020](../diagnostics.md#sqlsrc020), at the file, line and column of the mistake: see [settings files](settings-file.md#settings-files) | A value that is wrong is not set by that file, so a farther settings file or the default gives it, for a dialect too.  The rest of the file still applies, and so do the other words of `generatorParameters` and the other entries of `typeOverrides`, `columnTypes` or `libraryTypes`.  Of a key, or an entry, written twice, the first applies, and so does the first of two values of one generator parameter.  A file that is not a JSON object gives nothing |

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
| `models` | Its SQL, and its [models](models.md#models): types for its parameters and for a row of its result |
| `methods` | Its models, and a [method that takes a connection](connection-methods.md#methods-that-take-a-connection) and runs the query, which holds its SQL in place of a constant |
| `repository` | Its models, and a [repository method](repositories.md#repository-methods): an instance method of the type that gets its own connection and runs the query, which holds its SQL in place of a constant |

As a marker it stands in the file header, for every query of the file, and inside a query.  It can also be `output` in a settings file.

**`Output` decides which queries must be described.**  `sqlsource describe` asks a database about the queries whose output is `models`, `methods` or `repository`, and the build checks that each of them has a current entry in its file's [sidecar](../concepts.md#sidecar): a query that has none is [SQLSRC241](../diagnostics.md#sqlsrc241), and one that changed since it was described is [SQLSRC244](../diagnostics.md#sqlsrc244).  A query whose output is `sql` needs no description, no entry, no connection, and belongs to no [logical database](#setting-database).  A file claimed by two types, or listed by two projects, is described once, and the build of a project checks its entries once for the file.

The tool can describe only some dialects: see [the database support table](databases/index.md#database-support).  The default output is `repository` and the default dialect is `ansi`, so a project that sets neither gets [SQLSRC209](../diagnostics.md#sqlsrc209) for every file, in its build and from the tool.  Set the dialect and describe the queries, or set the output to `sql` for queries that want only their SQL.

A query whose output is `sql` or `models` gets its constant or token method.  A query whose output is `models`, `methods` or `repository`, and whose entry in its sidecar is current, also gets its [models](models.md#models); one whose output is `sql` gets none.  A query whose output is `methods` or `repository` gets no constant and no token method: its [method](query-methods.md#query-methods) holds its SQL and runs it, when the query meets the conditions of [which queries get a method](query-methods.md#which-methods).  A `repository` query's type needs a connection for it, as [the connection](repositories.md#repository-connection) says, and a `methods` query's method takes one, so set `methods` for a query that your own code runs on a connection or in a transaction that it manages.  A project that runs the SQL itself, through Dapper or its own ADO.NET code, sets `models`.

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

`Database` names the [logical database](../concepts.md#logical-database) a query belongs to: the one the `sqlsource` tool asks about it, through a connection you give it by that name.  It is not the dialect.  Two logical databases can share a dialect.

- The value is one word of letters, digits, `-`, `_` and `.`.  The rule is narrow because the tool makes the name of an environment variable of it: `billing` is read from `SQLSOURCE_CONNECTION_BILLING`.  Names are compared ignoring case, so `Billing` and `billing` are one database, spelled as the tool first meets it.
- Without a value, a query belongs to the database named after its file's dialect: `postgres` for a PostgreSQL file, `mssql` for a SQL Server file.
- It can be a marker in the file header or inside a query, or `database` in a settings file.
- One name is one database for a whole run, with one connection and one dialect.  Two files that give one name two dialects are [SQLSRC211](../diagnostics.md#sqlsrc211).
- The tool reads the setting to choose the connection it describes a query through.  The build reads it to choose the connection source of a [repository method](repositories.md#repository-connection).  A `database` value that is not valid in a settings file is reported by the tool alone.

To give the tool a connection for a name, see [connections](connections.md#connections).

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

The [dialect](../concepts.md#dialect) is the set of rules by which SqlSource reads a `.sql` file.  It does not connect to anything.  The default is `ansi`.  The dialect names, the options of `mysql` and `mariadb`, and what each dialect changes are on [the dialects page](dialects.md).

- **Three places.**  A `-- dialect:` marker in the file, else the `dialect` of the nearest settings file of the file's folder, or of a folder above it, that sets one, else `ansi`.  Above, the files under `Reporting/` are read as `mssql`, and every other file of the project as `postgres`.
- **A file has one dialect.**  The marker goes in the file header, before the first SQL, and not inside a query: anywhere else it is [SQLSRC115](../diagnostics.md#sqlsrc115).  It takes effect on the line after it, so comments above it are read by the dialect that the settings files give.
- **A value replaces the one below it whole.**  A file with `-- dialect: mysql` under a settings file that sets `"mysql,ansi-quotes"` is read as plain `mysql`, and so is a folder whose settings file sets `"mysql"` under one that sets `"mysql,ansi-quotes"`.
- **A value that is not a dialect** is [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker and [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file.  The files under a settings file whose `dialect` is wrong are read meanwhile by the dialect of a farther settings file, or as `ansi` when none sets one.
- **The tool reads the dialect too.**  It decides which kind of database the tool asks about a query, and whether there is one to ask: `postgres` and `mssql` can be described.  See [database support](databases/index.md#database-support).
