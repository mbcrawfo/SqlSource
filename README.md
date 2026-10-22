# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/vpre/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

SqlSource keeps the SQL of a C# project in `.sql` files, out of your code files, and generates the C# that uses it: a constant that holds each query, and for a query that your database has described, a type for its parameters and a type for its rows.  SQL in a file of its own is cleaner than SQL in a C# string: your editor highlights it, SQL linters and formatters work on it, and it is easy to find when you search for a table or a column.  A query that is renamed or removed is a compile error where it is used, and so is a column that your code reads and the query no longer returns, and your editor shows a query's SQL wherever it is used.  With PostgreSQL or SQL Server, the `sqlsource` tool checks every query against your database's schema, and records what the database says of its parameters and columns, from which the types are generated.

**Status:** SqlSource is in early development.  Today it generates a constant for each query, or a method for a query with a `{{token}}` chosen at run time, and its tool can ask a PostgreSQL or SQL Server database about each query's parameters and columns, which SqlSource calls describing the query.  From that description it generates a model for each query's parameters and rows, which turns a column that does not match your code into a build error.  Coming in a later release: a generated method that runs each query.  [What works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today) has the full list.

**Databases:** keeping SQL in `.sql` files and generating constants works with any database: SQL Server and Azure SQL, PostgreSQL, MySQL, MariaDB, SQLite, Oracle, CockroachDB and others.  Describing queries works with PostgreSQL 14 or later, and with SQL Server 2017 or later and Azure SQL, which is accepted and not tested; other databases are not described: see [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support).  So with PostgreSQL or SQL Server, follow the five steps below: by default every query must be described, which step 5 does, and the build reports an error for each query until it is.  With any other database, follow steps 1 to 4 and set the output to `sql`, which gives you a constant for each query and needs no description, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  There are two other ways to use SqlSource: [only generating models](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#only-want-to-generate-models), and [only keeping your SQL out of code files](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files).

## Installation

Until the first release the packages are previews, so the commands include `--prerelease`.  In the folder of each project that has queries:

```console
$ dotnet add package SqlSource --prerelease
```

Once, at the root of your repository, to install the tool (skip the first command if the repository already has a tool manifest):

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

- **`SqlSource`** is the generator.  Add it to each project whose queries you want generated.  It runs in the compiler when you build and adds no runtime dependency: no assembly of it ships with your application, and it is not a dependency of a package you build.  Only the code it generates, and the attribute you mark your types with and its enums, are compiled into your project.
- **`SqlSource.Tool`** adds the `sqlsource` command, which describes your queries ([step 5](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#5-describe-your-queries)).  Commit the `dotnet-tools.json` that `dotnet new tool-manifest` creates, and keep the tool at the version of the `SqlSource` package.  A project that only keeps its SQL out of code files does not need it.

**Supported environments.**  A project that uses SqlSource targets .NET 8 or later and compiles C# 12 or later, the default for .NET 8.  It builds in the .NET 8 SDK or later, and in Visual Studio 2022 17.8 or later; older SDKs and IDEs are not supported, and the live analysis of the Visual Studio and Rider editors has not been checked: see [known issues](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#known-issues).  The tool needs the .NET SDK, .NET 8 or later, and describes PostgreSQL 14 or later, and SQL Server 2017 or later and Azure SQL, which is accepted and not tested.  A project that references Npgsql for its PostgreSQL queries references Npgsql 8 or later, and one that references Microsoft.Data.SqlClient for its SQL Server queries references version 6.1 or later, which a project on EF Core 8 or 9 references itself, since they bring an older one.

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
- **`-- param:`** gives a parameter a type, in your database's own words, and says whether it can be null.  Add one where it helps: here it gives `@name` the type that PostgreSQL cannot work out from `@name IS NULL`, and says that it may be null.  `text` is PostgreSQL's word; SQL Server would say `nvarchar(100)`.  Without one, the database works out the type from how the parameter is used.

More: [the marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), which lists every `-- word:` line that SqlSource reads, [parameters and tokens](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/parameters-and-tokens.md).

### 2. Mark a partial type

In the same folder, `Queries/UserRepository.cs`:

```csharp
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository;
```

The type gets every `.sql` file in the folder of its C# file.  When you build, SqlSource adds a private class `Sql` to it, with a constant for each query: `Sql.GetUser` and `Sql.FindUsers`.  The build also reports errors until you set the dialect in step 3 and describe the queries in step 5, or set the output to `sql`: by default each query must be described by its database.  More: [the generator](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md), for another folder, one file, or members the rest of your code can use.

### 3. Set the dialect

In the project file:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

Databases disagree about where a comment or a string ends, and SqlSource takes the comments out of your SQL, so it reads each file by the rules of your database: its dialect.  The dialect also tells the tool which database to ask.  The default is `ansi`; set the one named for your database, such as `postgres` (PostgreSQL), `mssql` (SQL Server), `mysql` or `sqlite`.  More: [choosing a dialect](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#choosing-a-dialect), [settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) for everything else you can set for a project, a folder or one query.

### 4. Use the queries

The constants are plain strings, so any library can run them: Dapper here (`dotnet add package Dapper`), or your own ADO.NET code.  Once the queries are described, in step 5, the build also generates their models: a record for the parameters of each query, `GetUserParams` and `FindUsersParams`, and a record for its rows, `GetUserDto` and `FindUsersDto`, with a property for each parameter and each column.  This assumes that the table `users` has more columns than these three, as a table of users usually does.  Replace the contents of `Queries/UserRepository.cs` with this:

```csharp
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<GetUserDto> Get(int id) =>
        connection.QuerySingleAsync<GetUserDto>(Sql.GetUser, new GetUserParams(id));

    public Task<IEnumerable<FindUsersDto>> Find(string? name) =>
        connection.QueryAsync<FindUsersDto>(Sql.FindUsers, new FindUsersParams(name));
}
```

A property can be null where its column can, and `FindUsersParams.Name` can be null because its `-- param:` says `null`.  Where you know a column is never null and the database cannot tell, an alias such as `AS "total!"`, or a `-- column: total not null` line when Dapper reads the rows, says so: see which columns can be null for [PostgreSQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#nullability) and for [SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#nullability).  A query that selects every column of a table, in the table's order, gets a row model named after the table instead: if `users` had exactly the columns `id`, `name` and `email`, in that order, both queries would share one model, `UsersDto`, and the code would use it in place of `GetUserDto` and `FindUsersDto`.  For columns written in snake_case, such as `created_at`, set Dapper's `DefaultTypeMap.MatchNamesWithUnderscores` to `true` once when your application starts.  Write the parameters in camelCase, `@userId` and not `@user_id`: Dapper sends a property of `GetUserParams` only to the parameter written as its name, ignoring case but not `_`.  Both are in [running the models through Dapper](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#models-through-dapper).  With SQL Server, send a `datetime2` parameter as a `datetime2`, since Dapper sends a `DateTime` as a `datetime`, which holds a time to 1/300 of a second: see [with SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#dapper-with-sql-server).

> **Coming in a later release.**  SqlSource will generate a method that runs each query, so that `Get` and `Find` are written for you.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

More: [constants](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#constants), [models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#models), [the Dapper and ADO.NET recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#dapper-and-adonet), [running the models through Dapper](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#models-through-dapper).

### 5. Describe your queries

This step works with PostgreSQL and SQL Server: for another database, or for queries that want only their SQL, skip it and set the output to `sql`, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  The tool asks your database what the parameters and columns of each query are, without running the query, and writes the answers into a sidecar file beside the `.sql` file: `Users.sql.json`.

- **That checks every query against your schema**: a table or a column that does not exist, or a parameter whose type cannot be worked out, is an error now and not when the query runs.
- **The build reads the sidecars**, and reports an error for a query that has no entry in its sidecar, or whose entry is older than the query: run `describe` again after you change a query.  The models are built from them, so that the build never needs a database.

Run the tool from the folder of your project or solution, or name it, as in `dotnet sqlsource describe src/App/App.csproj`:

```console
$ export SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
$ dotnet sqlsource describe
postgres (postgres): 2 described, 0 skipped, 0 failed
$ git add Queries/Users.sql.json
```

`postgres`, in the variable's name and first in the summary, is the name SqlSource gives the database your queries belong to: the dialect's name, until you [name your databases](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#use-several-databases).  The name in parentheses is its dialect.  `--connection postgres="..."` gives the connection on the command line instead, though the variable keeps your password out of the shell's history.  For SQL Server files the database's name is `mssql`, so the variable is `SQLSOURCE_CONNECTION_MSSQL`, and the connection string is SqlClient's, `Server=localhost,1433;Database=app;User ID=app;Password=secret`, with `TrustServerCertificate=True` for SQL Server in a local container: see [connecting to SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#connecting).  The tool needs your schema, not your data.  Commit the sidecars with the project: every build reads them, and a copy of the project without them reports an error for each query.  A project that sets `SqlSourceIncludeFiles` to `false` lists its sidecars itself, beside its `.sql` files: see [which `.sql` files are included](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#included-files).

Run `describe` again when you change a query, `describe --force` after a change to the schema, and `describe --check` on a build server to catch a sidecar that is out of date.  Never edit a sidecar by hand.  More: [the tool](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md), [connections](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#connections), [PostgreSQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md), [SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md).

## Only want to generate models?

Set the output to `models`.  A query whose output is `models` gets its constant, or its method when it has a token, and its models, as in step 4, once the tool has described it, as in step 5, and never a method that runs it:

```xml
<PropertyGroup>
    <SqlSourceOutput>models</SqlSourceOutput>
</PropertyGroup>
```

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), which can also be set for one folder, one file or one query, and [models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#models), for their names, shape and properties.

## Just want to keep your SQL out of code files?

Follow steps 1 to 4, set the output to `sql`, and skip the tool.  For a SQL Server project:

```xml
<PropertyGroup>
    <SqlSourceDialect>mssql</SqlSourceDialect>
    <SqlSourceOutput>sql</SqlSourceOutput>
</PropertyGroup>
```

- **`sql` asks for each query's SQL and nothing else**: its constant, or, for a query with a `{{token}}`, a method that fills it in.  It needs no database, no tool and no sidecar.  It tells the tool to leave these queries out, so a run on the project reports nothing for them, and the build to need no sidecar for them and to generate no models.  Without it the default output, `codegen`, asks for every query to be described, and the build reports an error for each file or query that is not.
- **Any dialect works**: `mssql` for SQL Server, `mysql`, `sqlite`, the default `ansi` and the others.  Set the one named for your database, so that SqlSource reads your comments and strings as your database does.

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support).

## FAQ

### I want my query methods to have `Async` names

The generator parameter `async-method-suffix` is accepted today: `<SqlSourceGeneratorParameters>async-method-suffix</SqlSourceGeneratorParameters>` in the project file.  It takes effect when generated query methods arrive.  See [generator parameters](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-generator-parameters) and [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

### How do I change the namespace of the models?

By default the models are in the namespace of the type that claims the query.  Set `<SqlSourceModelNamespace>App.Models</SqlSourceModelNamespace>` in the project file, `ModelNamespace` on the attribute, or `-- model-namespace:` in a `.sql` file.  Two types in two namespaces that read one table each get a model of it, one in each namespace, and the setting gives them one.  A name with a period in an `-- output-model:` line, `App.Rows.UserRow`, is a full name and ignores the setting.  See [namespace of the models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-model-namespace).

### My project uses several databases

For several databases of one kind, name the database of each file with a `-- database: billing` line at its top, and give the tool a connection for each name: see [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#use-several-databases).  For files written for another kind of database, give their folder its own dialect with item metadata in the project file, or one file a `-- dialect:` line at its top: see [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#folder-with-another-dialect).

### How do I keep migration scripts out?

A `.sql` file that no `[SqlSourceGenerate]` type claims is not read, so scripts in a folder of their own do no harm.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#leave-migration-scripts-out) to keep them away from the compiler altogether.

### How do I choose a table name at run time?

Write a token with a default, `{{table:orders}}`, where the name goes.  The query becomes a method that takes the text to put there.  The default is only a sample that the tool describes the query with; a query whose output is `sql` needs none.  Nothing is escaped, so pass only text that your own code chooses, never a value a user typed.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#table-name-at-run-time) and [tokens](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/parameters-and-tokens.md#tokens).

### How do I see the SQL that was generated?

Hover over the member in your editor: its documentation holds the SQL as it was generated, with the markers and comments taken out.  See [documentation in IntelliSense](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#intellisense) and [what reaches the generated SQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#generated-sql).

### My test project sees the internals of my main project

Both projects can use SqlSource.  Each has its own copy of the attribute, and SqlSource turns off the compiler's warning about the two copies.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#test-project-sees-internals).

## NodaTime and other libraries

> **Coming in a later release.**  Generated models and query methods will use the types of libraries such as NodaTime for the database types they cover.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

## Custom type mapping

Each column and parameter of a PostgreSQL query whose output is `models` or `codegen`, and whose entry in its sidecar is current, gets the C# type that Npgsql reads its database type as: see [the PostgreSQL types](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#type-map).  Those of a SQL Server query get the C# type that Microsoft.Data.SqlClient reads their database type as: see [the SQL Server types](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#types).  A project that references no Microsoft.Data.SqlClient, such as a library that leaves the driver to its application, gets the same types, but for `vector`, which needs the reference: see [which version of Microsoft.Data.SqlClient](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#which-sqlclient).  A few PostgreSQL types follow the version of Npgsql that the project references: Npgsql 10 reads `date`, `time`, `cidr`, `daterange` and `datemultirange` as other types than Npgsql 8 and 9 do, and a project that references no Npgsql gives those types their C# types with the type override, `date=System.DateOnly time=System.TimeOnly` for an application on Npgsql 10: see [which version of Npgsql](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#which-npgsql).  The type override gives a database type the C# type of your choice instead, for the project in its file, for some files as item metadata, or in a `.sql` file with `-- type-override:`, and each place changes only the types it names:

```xml
<PropertyGroup>
    <SqlSourceTypeOverrides>timestamptz=System.DateTimeOffset public.user_status=MyApp.UserStatus</SqlSourceTypeOverrides>
</PropertyGroup>
```

Each property of a model has the type its database type maps to.  An entry that is not written correctly, or whose key has no schema and is not a type SqlSource knows, is a build error, and so is a C# type that the project cannot find or use, at each query that uses it.  See [type overrides](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-type-overrides).

## Where to next

| I want to | Go to |
|----|----|
| Configure one part of a project differently | [Configure one folder differently](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#configure-a-folder) |
| Look up a marker or a setting | [The marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), [all settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) |
| Use SQL Server, or another database | [SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md), [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support) |
| Keep the sidecars current after a schema change | [After a schema change](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#after-a-schema-change) |
| Set up CI | [Check in CI](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#check-in-ci), [a GitHub Actions job](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#check-sidecars-in-ci) |
| Look up a build error or a tool error | [Diagnostics](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md) |
| See a whole project that uses SqlSource, with its database | [The PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md), [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md) |
| See everything | [The manual](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md) |

To work on SqlSource itself, see [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md).  SqlSource is released under the [MIT licence](https://github.com/mbcrawfo/SqlSource/blob/main/LICENSE).
