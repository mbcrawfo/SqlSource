# Settings: every setting, and where you write it

A setting is a named choice that changes what SqlSource generates or does, and that you write in one of five places.  For example, a setting says which dialect the SQL is written in, which comments to keep, or which database the tool asks about a query.  A setting can apply to a whole project, to some of its files, to one type or to one query.

This page shows the five places and lists every setting with the places that can hold it.  It says which place wins when a setting is written in several places.  It also defines three settings: the output, the database and the dialect.  Every other setting is defined on the page of the thing it changes.

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

The four samples write settings in all five places:

- The first sample writes markers in a `.sql` file: in the file header, and inside a query.
- The second writes a [settings file](settings-file.md#settings-files), `sqlsource.json`.  A settings file applies to the `.sql` files of its folder and of every folder below it.
- The third writes a property of the attribute.
- The fourth writes an MSBuild property of the project.

| Place | Written as | Applies to |
|----|----|----|
| A marker inside a query | A `-- word:` line under a `-- name:` line | That query |
| A marker in the file header | A `-- word:` line before the first `-- name:` line | Every query of the file |
| A key of a settings file | A member of `sqlsource.json` | The `.sql` files of its folder and of every folder below it |
| A property of an attribute | `[SqlSourceGenerate(Source = "...")]` | The type, or the member, that carries the attribute |
| An MSBuild property of the project | An element of the project file or of a `Directory.Build.props` above it, `-p:` on the command line, or a variable of the environment | The whole project |

A file with no `-- name:` line is one query.  It takes the markers of the file header and the markers of a query.

[The table of all settings](#all-settings) says which of the five places each setting accepts.  To read one folder of a project differently from the rest, see [configure one folder differently](../guides/folders-and-files.md#configure-a-folder).

These are not settings, and have no row in [the table of all settings](#all-settings):

- The markers that describe one query's own content: `-- name:`, `-- summary:`, `-- param:`, `-- token:` and `-- token-ignore:`.
- The keys `root` and `$schema` of a settings file.
- `[SqlSourceConverter]`, which has no property.
- The options and environment variables of the `sqlsource` command: see [the command reference](cli.md#command-reference).
- The MSBuild properties that the package sets for itself.

<a id="all-settings"></a>

## All settings

Each name links to the section of its setting, which gives its values and its rules.  The other columns give every place where you can write the setting:

- A key of a [settings file](settings-file.md#settings-files).
- A marker, as [the marker index](sql-files.md#marker-index) shows.  Two columns say whether the marker can stand in the **file header**, the part of a `.sql` file before its first `-- name:` line, and **inside a query**, under a `-- name:` line.
- A property of an attribute.
- A property of the project: one of `SqlSourceRepositoryInterfaces`, `SqlSourceOnline`, `SqlSourceEnvFile`, `SqlSourceIncludeFiles` and `SqlSourcePublicExceptions`.

A place that a row does not list cannot hold that setting.  **Default** is the value when no place gives one.

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
| [Member location](members.md#setting-member-location) | `memberLocation` | `-- member-location:` | yes | yes | | | `class` for the output `repository`, `extension-class` for `methods`, and `nested-class` for `models` or `sql` |
| [Visibility](members.md#setting-visibility) | `visibility` | `-- visibility:` | yes | yes | | | `public` for `repository`, and otherwise by the member location, as [its section](members.md#setting-visibility) says |
| [Nested class](members.md#setting-nested-class) | `nestedClass` | `-- nested-class:` | yes | yes | | | `Sql` |
| [`Source`](attributes.md#setting-source) | | | no | no | `Source` | | The file named for the type in the folder of the C# file |
| [Repository interfaces](repositories.md#repository-interfaces) | | | no | no | `GenerateInterface`, `InterfaceName` | `SqlSourceRepositoryInterfaces` | On, named `I` and the type's name |
| [A repository's connection](repositories.md#repository-connection) | | | no | no | `Database` of `[SqlSourceConnection]` | | None: the build finds the connection source by its name or type |
| [Running the tool in the build](msbuild.md#online-build) | | | no | no | | `SqlSourceOnline` | `auto` |
| [The build's `.env` file](msbuild.md#setting-env-file) | | | no | no | | `SqlSourceEnvFile` | The `.env` in the project's folder |
| [Which `.sql` files are included](msbuild.md#included-files) | | | no | no | | `SqlSourceIncludeFiles` | On: every `.sql` file under the project's folder |
| [Public exceptions](exceptions.md#public-exceptions) | | | no | no | | `SqlSourcePublicExceptions` | Off: the exceptions are `internal` |

SqlSource ignores case, hyphens and spaces when it matches a value from a list.  So each of these lines is one value:

- `Methods`, `methods` and `me-thods`
- `SealedRecord` and `sealed record`
- `NestedClass` and `nested-class`

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

A query with `-- output: sql` has the output `sql`, whatever the other four places say.  A query without that marker takes `models` from the file header, and so on down the table.

The settings files of a `.sql` file are the `sqlsource.json` of its folder and the `sqlsource.json` of each folder above it, nearest first.  The list ends at the first settings file that says `"root": true`, and includes it: see [settings files](settings-file.md#settings-files).  To give the files of one folder their own values, see [configure one folder differently](../guides/folders-and-files.md#configure-a-folder).

The rules:

- **Each setting is decided on its own.**  A query that writes `-- output: sql` still takes the `-- database:` of its file header.
- **A place that gives no value is skipped.**  A settings file that does not name the setting gives no value.  A string of a settings file that is empty or white space gives no value either.  `Source`, on the attribute, is the exception: see [Source](attributes.md#setting-source).
- **A setting has only the places that its row of [the table of all settings](#all-settings) lists.**  Its errors:
  - A key of a settings file that is not in the table, and is not `root` or `$schema`, is [SQLSRC016](../diagnostics.md#sqlsrc016).
  - A marker where its row says that it cannot stand is [SQLSRC116](../diagnostics.md#sqlsrc116).
  - A `-- dialect:` marker inside a query or after SQL is the exception: it is [SQLSRC115](../diagnostics.md#sqlsrc115).  See [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).
- **Every type that claims a file gets the same settings for the queries of the file.**  The places of the settings of a query are the query, its file and the settings files of the folder of the file.  So two types that claim one file take the same settings from it: one output, one suffix, one shape of model, one [member location](members.md#setting-member-location).

  Only two things can differ, because they follow from the type itself:

  - The namespace of the models.  It is the namespace of the type, unless the [model namespace](models.md#setting-model-namespace) is set.
  - The default [visibility](members.md#setting-visibility) of the members of `methods`, `models` and `sql` queries on the type.  It follows the accessibility of the type.

  If a type needs other settings, give it a file of its own.

There are three exceptions to these rules:

- **A dialect value replaces the one below it whole**, options included.  The dialect has no place inside a query, because a file has one dialect.  The dialect of a file is the marker in its file header, else the nearest of its settings files that sets one, else `ansi`.  See [dialect](#setting-dialect).
- **`Source` exists only on the attribute.**  So it has no other place to choose between.
- **A type override, a column type, a library of the library types, or a generator parameter, replaces only what it names.**  Each place adds its entries to the entries of the places below it.  Each settings file is a place of its own, so the settings file of a folder adds to the entries of the settings files above it.

  An entry replaces the entry below it that has the same key: a database type, a column, a library or a generator parameter.  An entry whose value is `default` takes its key out.  The generator parameters also take `default` alone, which takes every parameter out, back to its default.  See [type overrides](type-mapping.md#setting-type-overrides), [column types](model-properties.md#setting-column-types), [library types](library-types-and-json.md#setting-library-types) and [generator parameters](generator-parameters.md#setting-generator-parameters).

<a id="a-value-that-is-wrong"></a>

### A value that is wrong

| Written as | Reported as | What happens |
|----|----|----|
| A marker | [SQLSRC111](../diagnostics.md#sqlsrc111), a dialect included.  A generator parameter that does not exist is [SQLSRC109](../diagnostics.md#sqlsrc109); a value that contradicts another in the same place is [SQLSRC112](../diagnostics.md#sqlsrc112) | The file produces no members until you fix it |
| A settings file | [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC020](../diagnostics.md#sqlsrc020), at the file, line and column of the mistake: see [settings files](settings-file.md#settings-files) | See the rules under the table |

- **The build and the tool both report the errors of a marker, and [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC018](../diagnostics.md#sqlsrc018).**  The build reports [SQLSRC019](../diagnostics.md#sqlsrc019) for a value of every key but `database`.  The tool reports SQLSRC019 only for a value of `dialect`, `output` or `database`: see [database](#setting-database).  The build alone reports [SQLSRC020](../diagnostics.md#sqlsrc020).  [Which settings files the build checks](settings-file.md#which-files-are-checked) says more.
- **A settings file does not set a value that is wrong.**  So a farther settings file or the default gives the value, for a dialect too.
- **The rest of the settings file still applies.**  So do the other words of `generatorParameters`, and the other entries of `typeOverrides`, `columnTypes` or `libraryTypes`.
- **Of a key, or an entry, written twice, the first applies.**  So does the first of two values of one generator parameter.
- **A settings file that is not a JSON object gives nothing.**

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

The output is the setting that says what SqlSource produces for a query.  SqlSource produces its models and a [query method](query-methods.md#query-methods), its SQL and its [models](models.md#models), or its SQL alone.  By default the query method is a method of a [repository](repositories.md#repository-methods).  The output also decides which queries must be described: the queries that the tool describes, and the queries that the build checks against their sidecar.  The values are `repository`, `methods`, `models` and `sql`, and the default is `repository`.  In the sample, `ResetAuditLog` has the output `sql`, and every other query of the file has `models`.

| Value | A query gets |
|----|----|
| `repository` | Its [models](models.md#models), and a [repository method](repositories.md#repository-methods): an instance method of the type that gets its own connection and runs the query.  The method holds the SQL of the query, in place of a constant |
| `methods` | Its [models](models.md#models), and a [method that takes a connection](connection-methods.md#methods-that-take-a-connection) and runs the query.  The method holds the SQL of the query, in place of a constant |
| `models` | Its SQL, as a constant or as a method where the query has tokens, and its [models](models.md#models): types for its parameters and for a row of its result |
| `sql` | Its SQL alone: a constant, or a method where the query has tokens |

The type of a `repository` query needs a connection for it, as [the connection](repositories.md#repository-connection) says.  Set one of the other three outputs in these cases:

- **Set `methods` for a query that your own code runs on a connection, in a transaction that it manages, or with a command timeout.**  The method of a `methods` query takes the connection.
- **Set `models` for a project that runs the SQL itself.**
- **Set `sql` for a query that needs only its SQL.**

`models` and `sql` are fallbacks, mainly for a project where Dapper or your own ADO.NET code runs the SQL.

Two rules complete the table:

- **A query whose output is `repository`, `methods` or `models` gets its [models](models.md#models) when its entry in its sidecar is current.**  A query whose output is `sql` gets no models.
- **A query whose output is `repository` or `methods` gets no constant and no token method.**  Its [method](query-methods.md#query-methods) holds its SQL and runs it, when the query meets the conditions of [which queries get a method](query-methods.md#which-methods).

You can write the output as a `-- output:` marker in the file header, for every query of the file, or inside a query, or as `output` in a settings file.

**`Output` decides which queries must be described.**  `sqlsource describe` asks a database about each query whose output is `repository`, `methods` or `models`.  The build checks that each of these queries has a current entry in the [sidecar](sidecars.md#sidecar-files) of its file.  A query that has no entry is [SQLSRC241](../diagnostics.md#sqlsrc241).  A query that changed after it was described is [SQLSRC244](../diagnostics.md#sqlsrc244).  A query whose output is `sql` needs no description, no entry and no connection, and belongs to no [logical database](#setting-database).

If two types claim a file, or two projects list it, the tool describes the file once, and the build of a project checks its entries once for the file.

The tool can describe only some dialects: see [the database support table](databases/index.md#database-support).  The default output is `repository` and the default dialect is `ansi`.  So a project that sets neither gets [SQLSRC209](../diagnostics.md#sqlsrc209) for each file that a type claims.  The build and the tool both report it.  To correct it, do one of these:

- Set the dialect to `postgres` or `mssql`, and describe the queries.
- Set the output to `sql` for the queries that need only their SQL.

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

A logical database is a name for one particular database that your queries run against, such as `billing`.  The `database` setting names the logical database that a query belongs to.  In the sample, `ListEvents` belongs to `events`, and every other query of the file to `billing`.  The setting has two uses:

- **The tool reads it to choose the connection that it describes a query through.**  The tool asks that database about the query, through a connection that you give the tool by that name: see [connections](connections.md#connections).  The [sidecar](sidecars.md#sidecar-files) of the file records the name.
- **The build reads it to choose the connection source of a [repository method](repositories.md#repository-connection).**  A [repository](repositories.md#repository-methods) that uses several logical databases marks a connection source for each.

The dialect and the logical database answer different questions.  The dialect is *which kind of SQL* a file is written in; the logical database is *which database* the tool asks about it.  Several logical databases can share a dialect: `billing` and `reports` can both be `postgres`.

- **The value is one word of letters, digits, `-`, `_` and `.`.**  The rule is narrow because the tool makes the name of an environment variable from it: `billing` is read from `SQLSOURCE_CONNECTION_BILLING`.
- **SqlSource ignores case when it compares names.**  So `Billing` and `billing` are one database, spelled as the tool first meets it.
- **Without a value, a query belongs to the database named after the dialect of its file**: `postgres` for a PostgreSQL file, `mssql` for a SQL Server file.  This is the default.
- **You can write it as a `-- database:` marker in the file header or inside a query, or as `database` in a settings file.**
- **One name is one database for a whole run, with one connection and one dialect.**  Two files that give one name two dialects are [SQLSRC211](../diagnostics.md#sqlsrc211).
- **The tool alone reports a `database` value of a settings file that is not valid.**  The build does not report it.

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

Set it to your database: `postgres` or `mssql`.

The dialect is the set of rules by which SqlSource reads a `.sql` file: where a comment or a string starts and ends, which differs from one database to the next.  Each dialect but `ansi` is named after a database.  A dialect connects to nothing.  You can set it for a project, for some of its files, or for one file.  The default is `ansi`.  Every other dialect name, the options of `mysql` and `mariadb`, and what each dialect changes are on [the dialects page](dialects.md).

- **The dialect has three places.**  SqlSource takes the first of these that gives one:
  1. A `-- dialect:` marker in the file.
  2. The `dialect` of the nearest settings file that sets one, in the folder of the file or in a folder above it.
  3. The default, `ansi`.

  In the sample, the files under `Reporting/` are read as `mssql`, and every other file of the project as `postgres`.
- **A file has one dialect.**  The marker must stand in the file header, before the first SQL, and not inside a query.  Anywhere else it is [SQLSRC115](../diagnostics.md#sqlsrc115).
- **The marker takes effect on the line after it.**  So SqlSource reads the comments above the marker by the dialect that the settings files give.
- **A value replaces the one below it whole.**  A file with `-- dialect: mysql` under a settings file that sets `"mysql,ansi-quotes"` is read as plain `mysql`.  A folder whose settings file sets `"mysql"`, under a settings file that sets `"mysql,ansi-quotes"`, is read as plain `mysql` too.
- **A value that is not a dialect is [SQLSRC111](../diagnostics.md#sqlsrc111) in a marker and [SQLSRC019](../diagnostics.md#sqlsrc019) in a settings file.**  The build and the tool both report each of them.  While the `dialect` of a settings file is wrong, the files under it are read by the dialect of a farther settings file, or as `ansi` when none sets one.
- **The tool reads the dialect too.**  The dialect decides which kind of database the tool asks about a query, and whether there is a database to ask.  The tool can describe a file whose dialect is `postgres` or `mssql`.  See [database support](databases/index.md#database-support).
