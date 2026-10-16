# Settings

A setting is a choice you make for a whole project, for some of its files or for one query: which dialect the SQL is written in, which comments to keep, which database the tool asks about a query.  Every setting is documented here and nowhere else, with the places it can be written and the one place that wins when it is written in several.

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

The first block writes settings as markers in a `.sql` file, the second as an MSBuild property and as item metadata, the third as properties of the attribute.  Which of the three a setting accepts is in the table.  To read one folder of a project differently from the rest, see [configure one folder differently](#configure-a-folder).

## All settings

Each name links to its section.  An empty cell says the setting cannot be written there.  **File header** is the part of a `.sql` file before its first `-- name:` line, and **In a query** is under a `-- name:` line: see [the marker index](sql-files.md#marker-index).

| Setting | Values | MSBuild property | Item metadata | Attribute | File header | In a query | Status |
|----|----|----|----|----|----|----|----|
| [Output](#setting-output) | `sql`, `models`, `codegen`.  Default `codegen` | `SqlSourceOutput` | `SqlSourceOutput` | `Output` | `-- output:` | `-- output:` | Works today for the tool; [no effect yet](README.md#what-works-today) on generated code |
| [Database](#setting-database) | A name of letters, digits, `-`, `_` and `.`.  Default: the name of the file's dialect | `SqlSourceDatabase` | `SqlSourceDatabase` | | `-- database:` | `-- database:` | Works today, for the tool alone |
| [Dialect](#setting-dialect) | `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite`, `oracle`, and options for `mysql` and `mariadb`.  Default `ansi` | `SqlSourceDialect` | `SqlSourceDialect` | | `-- dialect:` | | Works today |
| [Generator parameters](#setting-generator-parameters) | Words separated by spaces: `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix`, `default`.  Default: none | `SqlSourceGeneratorParameters` | `SqlSourceGeneratorParameters` | `Parameters` | `-- generator:` | `-- generator:` | Works today for `keep-comments` and `no-token-validation`; [accepted; no effect yet](README.md#what-works-today) for the other four |
| [Input and output model suffix](#setting-model-suffix) | Characters of a C# identifier.  Defaults `Params` and `Dto` | `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix` | The same names | `InputModelSuffix`, `OutputModelSuffix` | `-- input-model-suffix:`, `-- output-model-suffix:` | | [Accepted; no effect yet](README.md#what-works-today) |
| [Model namespace](#setting-model-namespace) | A namespace.  Default: the namespace of the type | `SqlSourceModelNamespace` | `SqlSourceModelNamespace` | `ModelNamespace` | `-- model-namespace:` | | [Accepted; no effect yet](README.md#what-works-today) |
| [Input and output model type](#setting-model-type) | `record`, `sealed record`, `class`, `sealed class`.  Default `sealed record` | `SqlSourceInputModelType`, `SqlSourceOutputModelType` | The same names | `InputModelType`, `OutputModelType` | `-- input-model-type:`, `-- output-model-type:` | The same markers | [Accepted; no effect yet](README.md#what-works-today) |
| [One query's model name](#setting-model-name) | A type's name, or its full name | | | | | `-- input-model:`, `-- output-model:` | [Accepted; no effect yet](README.md#what-works-today) |
| [Collection type](#setting-collection-type) | `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList`.  Default `Array` | `SqlSourceCollectionType` | `SqlSourceCollectionType` | `CollectionType` | `-- collection-type:` | `-- collection-type:` | [Accepted; no effect yet](README.md#what-works-today) |
| [`SqlLocation`](#setting-sql-location) | `Nested`, `Direct`.  Default `Nested` | | | `SqlLocation` | | | Works today |
| [`MethodLocation`](#setting-method-location) | `ExtensionClass`, `Public`, `Internal`, `Private`.  Default `ExtensionClass` | | | `MethodLocation` | | | [Accepted; no effect yet](README.md#what-works-today) |
| [`Path`](#setting-path) | A folder, or one `.sql` file, relative to the folder of the C# file.  Default: that folder | | | `Path` | | | Works today |

A value from a list is matched ignoring case, hyphens and spaces, so `CodeGen`, `codegen` and `code-gen` are one value, and so are `SealedRecord` and `sealed record`.  On the attribute you write the member of the enum that the package adds to your project, as in `Output = GeneratorOutput.Sql`.

"Accepted; no effect yet" means SqlSource reads the setting and reports a value it does not take, and changes no generated code.  Models and query methods come in a later release.

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
- **A setting has only the places its row of [the table](#all-settings) lists.**  A property or item metadata that SqlSource does not list is not read.  A marker where its row says it cannot stand is [SQLSRC116](diagnostics.md#sqlsrc116), except a `-- dialect:` inside a query or after SQL, which is [SQLSRC115](diagnostics.md#sqlsrc115): see [markers in the wrong place](sql-files.md#markers-in-the-wrong-place).
- **The attribute belongs to one type.**  Two types that claim one file each use their own attribute, and each gets its own result.

Three exceptions, stated plainly:

- **A list of generator parameters replaces the one below it whole.**  It never adds to it.  See [generator parameters](#setting-generator-parameters).
- **A dialect value replaces the one below it whole**, options included.  The dialect has no attribute and no in-query level: a file has one dialect, which is the marker in its file header, else its item metadata, else the property, else `ansi`.  See [dialect](#setting-dialect).
- **`SqlLocation`, `MethodLocation` and `Path` exist only on the attribute.**  They have nothing to choose between.

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
| `models` | Its SQL, and types for its parameters and for its result |
| `codegen` | Its SQL, the types, and a method that runs the query |

On the attribute: `[SqlSourceGenerate(Output = GeneratorOutput.Sql)]`.  As a marker it stands in the file header, for every query of the file, and inside a query.

**Today, `Output` decides which queries the tool describes.**  `sqlsource describe` asks a database about the queries whose output is `models` or `codegen`.  A query whose output is `sql` needs no description, belongs to no [logical database](#setting-database) and needs no connection.  The tool reads the attribute from your C# source, so write the value out as shown: a constant or an expression there is [SQLSRC208](diagnostics.md#sqlsrc208).  A file claimed by two types, or listed by two projects, is described when either needs it.

The tool can describe only some dialects: see [the database support table](dialects.md#database-support).  The default output is `codegen` and the default dialect is `ansi`, so a project that sets neither gets [SQLSRC209](diagnostics.md#sqlsrc209) from the tool for every file.  Set the dialect, or set the output to `sql` for queries that want only their SQL.

> **Coming in a later release.**  `models` and `codegen` will generate types for a query's parameters and result, and a method that runs it.  Today every query gets its constant or token method whatever its output.  See [what works today](README.md#what-works-today).

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
- Without a value, a query belongs to the database named after its file's dialect, `postgres` for a PostgreSQL file.
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
- **The tool reads the dialect too.**  It decides which queries the tool can describe: `postgres` today.  A query of `mssql` that must be described is [SQLSRC216](diagnostics.md#sqlsrc216), since describing SQL Server queries is coming.  See [database support](dialects.md#database-support).

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

| Parameter | Effect | Status |
|----|----|----|
| `keep-comments` | Comments and blank lines stay in the generated SQL: see [what reaches the generated SQL](sql-files.md#generated-sql) | Works today |
| `no-token-validation` | The method of a query with tokens does not check its arguments: see [validation](parameters-and-tokens.md#token-validation) | Works today |
| `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` | None yet | [Accepted; no effect yet](README.md#what-works-today) |
| `default` | The empty list: every default.  It stands alone, or repeated | Works today |

Words are matched ignoring case.

> **Coming in a later release.**  `sort-input`, `sort-output`, `no-table-models` and `async-method-suffix` will shape the models and methods that a later release generates.  See [what works today](README.md#what-works-today).

**A list replaces; it never adds.**  In the example, `Total` has the list `no-token-validation` and nothing else: it does not keep its comments, though the file header says `keep-comments`.  A query that wants both writes both, `-- generator: keep-comments no-token-validation`.  `-- generator: default` gives a query every default, whatever its file says.

The list for a query is the first of these that gives one, whole:

1. The `-- generator:` lines inside the query.
2. The `-- generator:` lines in the file header.
3. `Parameters` on the attribute of the type that claims the file.
4. The `SqlSourceGeneratorParameters` metadata of the file's item.
5. The `SqlSourceGeneratorParameters` property.

Several `-- generator:` lines in one place are one list: two lines in a query add up.  A list never adds to the list of another place.

In a property, in metadata and on the attribute the list is words separated by spaces or line breaks.  `default` beside another word there makes the whole value wrong: [SQLSRC014](diagnostics.md#sqlsrc014) in a property or metadata, [SQLSRC006](diagnostics.md#sqlsrc006) on the attribute.  A word that is not a generator parameter is reported the same way, and the other words still apply.  In a marker an empty `-- generator:` line is [SQLSRC110](diagnostics.md#sqlsrc110), and `default` beside another word in one place is [SQLSRC112](diagnostics.md#sqlsrc112).

<a id="setting-model-suffix"></a>

## Suffix of a model's name

```sql
-- input-model-suffix: Args
-- output-model-suffix: Row

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

The suffix ends the name of the type generated for a query's parameters (input) and for a row of its result (output).  The defaults are `Params` and `Dto`.

- The value is one or more characters that a C# identifier can contain, such as letters, digits and `_`.  A space or a period is not one.
- It stands in the file header only, not inside a query.  It can also be item metadata, an MSBuild property, or a property of the attribute.

> **Coming in a later release.**  The suffix will end the names of the types generated for a query's parameters and result rows.  Today the settings are read and checked, and change no generated code.  See [what works today](README.md#what-works-today).

<a id="setting-model-namespace"></a>

## Namespace of the models

```sql
-- model-namespace: App.Models

-- name: ListUsers
SELECT id, name FROM users;
```

The namespace the generated models go in.  The default is the namespace of the type that claims the file.

- The value is C# identifiers joined by periods, none of them a reserved keyword.
- It stands in the file header only.  It can also be item metadata, an MSBuild property (`SqlSourceModelNamespace`), or `ModelNamespace` on the attribute.

> **Coming in a later release.**  The namespace will say where the generated models go.  Today the setting is read and checked, and changes no generated code.  See [what works today](README.md#what-works-today).

<a id="setting-model-type"></a>

## Shape of a model

```sql
-- output-model-type: sealed class

-- name: ListUsers
-- input-model-type: record
SELECT id, name FROM users WHERE id > @min;
```

The shape of the type generated for a query's parameters (input) and for a row of its result (output): `record`, `sealed record`, `class` or `sealed class`.  The default is `sealed record` for both.

- It stands in the file header and inside a query, as two markers.  It can also be item metadata, an MSBuild property, or a property of the attribute: `InputModelType = GeneratorModelType.Class`.

> **Coming in a later release.**  The shape will say whether a generated model is a record or a class, and whether it is sealed.  Today the settings are read and checked, and change no generated code.  See [what works today](README.md#what-works-today).

<a id="setting-model-name"></a>

## Name of one query's model

```sql
-- name: GetUser
-- input-model: UserArgs
-- output-model: App.Models.UserRow
SELECT id, name FROM users WHERE id = @id;
```

`-- input-model:` and `-- output-model:` name the type of one query's parameters and of its result rows.  The value is a type's name, or its full name: identifiers joined by periods, none of them a reserved keyword.

- It stands inside a query only.  It has no property, no item metadata and no attribute property, since a name is for one query.
- `-- input-model:` on a query with no parameters is [SQLSRC119](diagnostics.md#sqlsrc119).

> **Coming in a later release.**  The markers will name the types of one query's parameters and result rows.  Today they are read and checked, and change no generated code.  See [what works today](README.md#what-works-today).

<a id="setting-collection-type"></a>

## Collection type

```sql
-- name: ListUsers
-- collection-type: IReadOnlyList
SELECT id, name FROM users;
```

The type a generated method will return many rows in.  The values are `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` and `IImmutableList`.  The default is `Array`.

- It stands in the file header and inside a query.  It can also be item metadata, an MSBuild property, or `CollectionType = GeneratorCollectionType.IReadOnlyList` on the attribute.

> **Coming in a later release.**  The collection type will say what a generated method returns many rows in.  Today the setting is read and checked, and changes no generated code.  See [what works today](README.md#what-works-today).

<a id="setting-sql-location"></a>

## Where the SQL goes

```csharp
[SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
public static partial class UserQueries;
```

`SqlLocation` is a property of the attribute and nothing else.  It says where the members generated for the type's queries go.  Each member is a `const string`, or a static method when the query has tokens.

| `SqlLocation` | Generated members | Used as |
|----|----|----|
| `SqlLocation.Nested` (the default) | Public members in a `private static class Sql` nested in the type | `Sql.GetUser`, inside the type only |
| `SqlLocation.Direct` | Public members on the type itself | `UserQueries.GetUser`, wherever the type is visible |

With `Nested`, a type that already has a member named `Sql` is [SQLSRC007](diagnostics.md#sqlsrc007): rename it, or use `Direct`.  The members are described on [the generator page](generator.md).

<a id="setting-method-location"></a>

## Where the methods go

```csharp
[SqlSourceGenerate(MethodLocation = MethodLocation.Internal)]
internal static partial class UserQueries;
```

`MethodLocation` is a property of the attribute and nothing else, as `SqlLocation` is.  It says where the methods that run the queries go.

| `MethodLocation` | The methods will be |
|----|----|
| `ExtensionClass` (the default) | In a generated static class beside the type, as extension methods of `DbConnection` |
| `Public` | On the type, as public static methods |
| `Internal` | On the type, as internal static methods |
| `Private` | On the type, as private static methods |

There is no property, item metadata or marker for it.  `SqlSourceMethodLocation` in a project file or `-- method-location:` in a `.sql` file is not read.

> **Coming in a later release.**  The location will say where the methods that run the queries go.  Today the property is read and checked, and changes no generated code.  See [what works today](README.md#what-works-today).

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

Only the settings with an item metadata column in [the table](#all-settings) can be given this way.  To change a setting for the files of one type, use the type's attribute.

<a id="included-files"></a>

## Which `.sql` files are included

The package hands every `.sql` file under the project's folder to the compiler, as an `AdditionalFiles` item: that is how a source generator sees a file that is not C#.  The output and intermediate folders are left out, and so are folders whose names start with a dot, as the SDK leaves them out of your C# files.  The `sqlsource` tool reads the same list.  A `.sql` file outside the project's folder is not included; list it yourself.

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
</ItemGroup>
```

A file the project includes is only handed to the compiler.  Which of them belong to a type is the type's [`Path`](#setting-path), and a file no type claims is ignored.

## Values written in MSBuild

The package removes the white space around the value of each property and each item metadata in [the table](#all-settings), and makes one space of each run of white space inside it, a line break among it.  So a value may stand on a line of its own, and a value of several words may be written over several lines:

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>
        keep-comments
        no-token-validation
    </SqlSourceGeneratorParameters>
</PropertyGroup>
```

A line break inside one word puts a space there.  `App.` and `Models` on two lines are `App. Models`, which is not a namespace and is reported as [SQLSRC014](diagnostics.md#sqlsrc014).

**Do not use `;` or `#` in a value.**  The compiler hands a generator only the part of a value before the first `;` or `#`, and nothing reports the rest.  `keep-comments;no-token-validation` is read as `keep-comments`.  Separate words with spaces or line breaks.

## Known issues

- **A `.sql` file that one of your own build targets adds is not fully seen.**  This is about a target in your project that adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as one that writes a file or copies it in.  The build compiles the file, and nothing is reported, but two things go wrong.  `sqlsource` leaves out the file's queries, so it describes none of them.  And the settings you gave the file as item metadata, its dialect for one, are dropped when the target runs late in the build, so the file is read with the project's settings.  To make both work, tell your target to run before the package reads its settings, wherever the target is declared:

  ```xml
  <Target Name="AddGeneratedSql" BeforeTargets="SqlSourceTrimMetadataOfFiles">
      <ItemGroup>
          <AdditionalFiles Include="obj/generated/*.sql" SqlSourceDialect="postgres" />
      </ItemGroup>
  </Target>
  ```

  Change the name, the path and the metadata to your own.  `BeforeTargets="SqlSourceTrimMetadataOfFiles"` is the line that matters.
