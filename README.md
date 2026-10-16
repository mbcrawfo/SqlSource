# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/v/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

SqlSource keeps the SQL of a C# project in `.sql` files, out of your code files, and generates the C# that uses it.  Each query in a `.sql` file becomes a constant that holds its SQL, and SqlSource is growing to generate, from the same file, a typed model for each query's parameters and rows and a method that runs the query.

SQL in a file of its own is cleaner than SQL in a C# string: your editor highlights it, SQL linters and formatters work on it, and it is easy to find when you search for a table or a column.  A query that is renamed or removed is a compile error where it is used.  Generated models will turn a column whose name or type does not match your code into a build error instead of a runtime one, and generated query methods will run as fast as hand-written ADO.NET.

**Status:** SqlSource is in early development.  Today it generates a constant for each query, or a method for a query with a part chosen at run time, and its tool describes PostgreSQL queries.  Generated models and query methods are coming in a later release: [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today) has the full list.

Generating queries is the main way to use SqlSource, and this page follows it.  There are two others: [only generating models](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#only-want-to-generate-models), and [only keeping your SQL out of code files](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files).

## Installation

Until the first release the packages are previews, so the commands include `--prerelease`:

```console
$ dotnet add package SqlSource --prerelease
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

- **`SqlSource`** is the generator.  It runs in the compiler when you build, and every project with `.sql` files needs it.  It is a development dependency: it adds nothing to your application's output and nothing to the dependencies of a package you build.
- **`SqlSource.Tool`** adds the `sqlsource` command, which asks your database about your queries ([step 5](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#5-describe-your-queries)).  Install it once, as a local tool at the root of your repository, and commit the `dotnet-tools.json` that `dotnet new tool-manifest` creates.  Keep it at the version of the `SqlSource` package.  A project that only keeps its SQL out of code files does not need it.

**Supported environments.**  A project that uses SqlSource targets .NET 8 or later and compiles C# 12 or later, the default for .NET 8.  It builds in the .NET 8 SDK or later, and in Visual Studio 2022 17.8 or later; older SDKs and IDEs are not supported.  The tool needs the .NET SDK, .NET 8 or later, and describes PostgreSQL 14 or later.

## Getting started

### 1. Write your queries

Save this as `Queries/Users.sql`:

```sql postgres
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name, email
FROM users
WHERE id = @id;

-- name: FindUsers
-- summary: Finds the users with a name, or every user when the name is null.
-- param: @name text null
SELECT id, name, email
FROM users
WHERE @name IS NULL OR name = @name
ORDER BY name;
```

- **`-- name:`** starts a query and names it.  The name becomes the name of a C# member, so it is a C# identifier.
- **`-- summary:`** documents the query.  Your editor shows it, with the SQL, where the query is used.
- **`@id`** is a query parameter.  It stays in the SQL as written, and you give its value when you run the query.
- **`-- param:`** gives a parameter a type, in your database's own words, and says whether it can be null.  Add one where it helps: here it says that `@name` may be null.  Without one, the database works out the type from how the parameter is used.

More: [the marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), [parameters and tokens](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/parameters-and-tokens.md).

### 2. Mark a partial type

In the same folder, `Queries/UserRepository.cs`:

```csharp
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository;
```

The type gets every `.sql` file in the folder of its C# file.  When you build, SqlSource adds a private class `Sql` to it, with a constant for each query: `Sql.GetUser` and `Sql.FindUsers`.  More: [the generator](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md), for another folder, one file, or members the rest of your code can use.

### 3. Set the dialect

In the project file:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

Databases disagree about where a comment or a string ends, and SqlSource takes the comments out of your SQL, so it reads each file by the rules of your database: its dialect.  The dialect also tells the tool which database to ask.  The default is `ansi`; set the one named for your database, such as `postgres`, `mssql`, `mysql` or `sqlite`.  More: [choosing a dialect](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#choosing-a-dialect), [settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) for everything else you can set for a project, a folder or one query.

### 4. Use the queries

Fill in the type from step 2:

```csharp
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<User> Get(int id) =>
        connection.QuerySingleAsync<User>(Sql.GetUser, new { id });

    public Task<IEnumerable<User>> Find(string? name) =>
        connection.QueryAsync<User>(Sql.FindUsers, new { name });
}
```

The constants are plain strings, so any library can run them: Dapper here, which is a package of its own, or your own ADO.NET code.  Hover over `Sql.GetUser` to see its summary and its SQL.

> **Coming in a later release.**  SqlSource will generate the `User` type from the query's columns and a method that runs each query, so that `Get` and `Find` are written for you.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

More: [constants](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#constants), [the Dapper and ADO.NET recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#dapper-and-adonet).

### 5. Describe your queries

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
postgres (postgres): 2 described, 0 skipped, 0 failed
$ git add Queries/Users.sql.json
```

The tool asks your database what the parameters and columns of each query are, without running the query, and writes the answers into a sidecar file beside the `.sql` file: `Users.sql.json`.  It needs your schema, not your data.  `postgres=` names the database the connection is for, which is the dialect's name until you [name your databases](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#use-several-databases).  Commit the sidecars with the project: the generated models and query methods will be built from them, so that the build never needs a database.  Nothing in a build reads them today.

Run `describe` again when you change a query, `describe --force` after a change to the schema, and `describe --check` on a build server to catch a sidecar that is out of date.  Never edit a sidecar by hand.  More: [the tool](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md), [connections](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#connections), [PostgreSQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md).

## Only want to generate models?

Set the output to `models`.  Today a query whose output is `models` gets its constant, as in step 4, and the tool describes it, as in step 5:

```xml
<PropertyGroup>
    <SqlSourceOutput>models</SqlSourceOutput>
</PropertyGroup>
```

> **Coming in a later release.**  A query whose output is `models` will get a type for its parameters and a type for its rows, and no method that runs it.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), which can also be set for one folder, one file or one query.

## Just want to keep your SQL out of code files?

Follow steps 1 to 4, set the output to `sql`, and skip the tool.  For a MySQL project:

```xml
<PropertyGroup>
    <SqlSourceDialect>mysql</SqlSourceDialect>
    <SqlSourceOutput>sql</SqlSourceOutput>
</PropertyGroup>
```

- **`sql` asks for each query's SQL and nothing else**: its constant, or its method where it has tokens.  It needs no database, no tool and no sidecar.  If the tool is run on the project anyway, it leaves these queries out and reports nothing.
- **Any dialect works**, the default `ansi` and those of databases the tool cannot describe, such as MySQL and SQLite, included.  Set the one named for your database all the same, so that SqlSource reads your comments and strings as your database does.

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support).

## FAQ

### I want my query methods to have `Async` names

The generator parameter `async-method-suffix` is accepted today: `<SqlSourceGeneratorParameters>async-method-suffix</SqlSourceGeneratorParameters>` in the project file.  It takes effect when generated query methods arrive.  See [generator parameters](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-generator-parameters) and [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

### How do I change the namespace of the models?

The setting is accepted today: `<SqlSourceModelNamespace>App.Models</SqlSourceModelNamespace>` in the project file, `ModelNamespace` on the attribute, or `-- model-namespace:` in a `.sql` file.  It takes effect when generated models arrive.  See [namespace of the models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-model-namespace) and [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

### My project uses several databases

Name the database of each file with a `-- database: billing` line at its top, and give the tool a connection for each name.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#use-several-databases).

### Some of my files are for another database

Give their folder its own dialect with item metadata in the project file, or one file with a `-- dialect:` line at its top.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#folder-with-another-dialect).

### How do I keep migration scripts out?

A `.sql` file that no `[SqlSourceGenerate]` type claims is not read, so scripts in a folder of their own do no harm.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#leave-migration-scripts-out) to keep them away from the compiler altogether.

### How do I choose a table name at run time?

Write a token, `{{table}}`, where the name goes.  The query becomes a method that takes the text to put there.  Nothing is escaped, so pass only text that your own code chooses, never a value a user typed.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#table-name-at-run-time) and [tokens](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/parameters-and-tokens.md#tokens).

### How do I see the SQL that was generated?

Hover over the member in your editor: its documentation holds the SQL as it was generated, with the markers and comments taken out.  See [documentation in IntelliSense](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#intellisense) and [what reaches the generated SQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#generated-sql).

### My test project sees the internals of my main project

Both projects can use SqlSource.  Each has its own copy of the attribute, and SqlSource turns off the compiler's warning about the two copies.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#test-project-sees-internals).

## NodaTime and other libraries

> **Coming in a later release.**  Generated models and query methods will use the types of libraries such as NodaTime for the database types they cover.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

## Custom type mapping

> **Coming in a later release.**  You will be able to choose the C# type that a database type becomes in generated models and query methods.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

## Where to next

| I want to | Go to |
|----|----|
| Configure one part of a project differently | [Configure one folder differently](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#configure-a-folder) |
| Look up a marker or a setting | [The marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), [all settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) |
| Use a database other than PostgreSQL | [Database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support) |
| Keep the sidecars current after a schema change | [After a schema change](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#after-a-schema-change) |
| Set up CI | [Check in CI](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#check-in-ci) |
| Look up a build error or a tool error | [Diagnostics](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md) |
| See everything | [The manual](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md) |

To work on SqlSource itself, see [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md).  SqlSource is released under the [MIT licence](https://github.com/mbcrawfo/SqlSource/blob/main/LICENSE).
