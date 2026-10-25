# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/vpre/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

SqlSource keeps the SQL of a C# project in `.sql` files, out of your code files, and generates the C# that uses it: for a query that your database has described, a type for its parameters, a type for its rows and a method that runs it, and for any other query a constant that holds it.  SQL in a file of its own is cleaner than SQL in a C# string: your editor highlights it, SQL linters and formatters work on it, and it is easy to find when you search for a table or a column.  A query that is renamed or removed is a compile error where it is used, and so is a column that your code reads and the query no longer returns, and your editor shows a query's SQL wherever it is used.  With PostgreSQL or SQL Server, the `sqlsource` tool checks every query against your database's schema, and records what the database says of its parameters and columns, from which the types and the methods are generated.

**Status:** SqlSource is in early development.  Its tool asks a PostgreSQL or SQL Server database about each query's parameters and columns, which SqlSource calls describing the query.  From that description the build generates a model for each query's parameters and rows, which turns a column that does not match your code into a build error, and a method that runs the query and reads its rows into the model.  For a query you run yourself it generates a constant, or a method that fills in a `{{token}}` chosen at run time.  Coming in a later release: converters for types the database driver does not read, and each parameter sent as its database type.  [What works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today) has the full list.

**Databases:** keeping SQL in `.sql` files and generating constants works with any database: SQL Server and Azure SQL, PostgreSQL, MySQL, MariaDB, SQLite, Oracle, CockroachDB and others.  Describing queries works with PostgreSQL 14 or later, and with SQL Server 2017 or later and Azure SQL, which is accepted and not tested; other databases are not described: see [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support).  So with PostgreSQL or SQL Server, follow the five steps below: by default every query must be described, which step 5 does, and the build reports an error for each query until it is.  With any other database, follow steps 1 to 3 and set the output to `sql`, which gives you a constant for each query and needs no description, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  There are two other ways to use SqlSource: [only generating models](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#only-want-to-generate-models), and [only keeping your SQL out of code files](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files).

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

- **`SqlSource`** is the generator.  Add it to each project whose queries you want generated.  It runs in the compiler when you build and adds no runtime dependency: no assembly of it ships with your application, and it is not a dependency of a package you build.  Only the code it generates, and the attribute you mark your types with, its enums and the exception its methods throw, are compiled into your project.
- **`SqlSource.Tool`** adds the `sqlsource` command, which describes your queries ([step 5](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#5-describe-your-queries)).  Commit the `dotnet-tools.json` that `dotnet new tool-manifest` creates, and keep the tool at the version of the `SqlSource` package.  A project that only keeps its SQL out of code files does not need it.

**Supported environments.**  A project that uses SqlSource targets .NET 8 or later and compiles C# 12 or later, the default for .NET 8.  It builds in the .NET 8 SDK or later, and in Visual Studio 2022 17.8 or later; older SDKs and IDEs are not supported, and the live analysis of the Visual Studio and Rider editors has not been checked: see [known issues](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#known-issues).  The tool needs the .NET SDK, .NET 8 or later, and describes PostgreSQL 14 or later and SQL Server 2017 or later, and accepts Azure SQL, which is not tested.  A project that references Npgsql for its PostgreSQL queries references Npgsql 8 or later, and one that references Microsoft.Data.SqlClient for its SQL Server queries references version 6.1 or later; the methods that run the queries need that reference, and a project on EF Core 8 or 9 adds it itself, since EF Core brings an older one.

## Getting started

### 1. Write your queries

Save this as `Queries/Users.sql`:

```sql postgres
-- name: GetUser -> one-optional
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

- **`-- name:`** starts a query and names it.  The name becomes the name of a C# method, so it is a C# identifier.  **`-> one-optional`** after it is the query's result shape: its method returns one row, or null when there is none.  A query without one, such as `FindUsers`, returns every row.
- **`-- summary:`** documents the query.  Your editor shows it, with the SQL, where the query is used.
- **`@id`** is a query parameter.  It stays in the SQL as written, and you give its value when you run the query.
- **`-- param:`** gives a parameter a type, in your database's own words, and says whether it can be null.  Add one where it helps: here it gives `@name` the type that PostgreSQL cannot work out from `@name IS NULL`, and says that it may be null.  `text` is PostgreSQL's word; SQL Server would say `nvarchar(100)`.  Without one, the database works out the type from how the parameter is used.

More: [the marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), which lists every `-- word:` line that SqlSource reads, [result shapes](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#result-shapes), [parameters and tokens](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/parameters-and-tokens.md).

### 2. Mark a partial type

In the same folder, `Queries/UserRepository.cs`:

```csharp
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository;
```

The type gets every `.sql` file in the folder of its C# file.  Once the queries are described, in step 5, the build generates a method for each query that runs it, `GetUser` and `FindUsers`.  Until then it reports an error for each query: by default each query must be described by its database, which needs the dialect of step 3 and the tool of step 5.  More: [the generator](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md), for another folder, one file, or methods on the type itself.

### 3. Set the dialect

In the project file:

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
```

Databases disagree about where a comment or a string ends, and SqlSource takes the comments out of your SQL, so it reads each file by the rules of your database: its dialect.  The dialect also tells the tool which database to ask.  The default is `ansi`; set the one named for your database, such as `postgres` (PostgreSQL), `mssql` (SQL Server), `mysql` or `sqlite`.  More: [choosing a dialect](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#choosing-a-dialect), [settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) for everything else you can set for a project, a folder or one query.

### 4. Use the queries

Once the queries are described, in step 5, the build generates their models, a record for the parameters of each query, `GetUserParams` and `FindUsersParams`, and a record for its rows, `GetUserDto` and `FindUsersDto`, with a property for each parameter and each column.  It also generates a method for each query, an extension method of `DbConnection`, which opens the connection when it is closed, sends the parameters, runs the query and reads its rows into the models.  The methods run through your database's driver, which the project references: `dotnet add package Npgsql`, or `Microsoft.Data.SqlClient` for SQL Server.  This assumes that the table `users` has more columns than these three, as a table of users usually does.  Replace the contents of `Queries/UserRepository.cs` with this:

```csharp
using System.Data.Common;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<GetUserDto?> Get(int id) =>
        connection.GetUser(new GetUserParams(id));

    public Task<FindUsersDto[]> Find(string? name) =>
        connection.FindUsers(new FindUsersParams(name));
}
```

`GetUser` returns the user, or null when there is none, and `FindUsers` every row, in an array.  Each method also takes a transaction, a timeout and a cancellation token, and leaves a connection that is already open as it is.  A property can be null where its column can, and `FindUsersParams.Name` can be null because its `-- param:` says `null`.  Where you know a column is never null and the database cannot tell, an alias such as `AS "total!"`, or a `-- column: total not null` line, says so, and the method throws if the column is null after all: see which columns can be null for [PostgreSQL](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#nullability) and for [SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#nullability).  A query that selects every column of a table, in the table's order, gets a row model named after the table instead: if `users` had exactly the columns `id`, `name` and `email`, in that order, both queries would share one model, `UsersDto`, and the code would use it in place of `GetUserDto` and `FindUsersDto`.  Each parameter is sent by its value, and the driver chooses its database type from its C# type, which a few types do not survive: with SQL Server, a `DateTime` is sent as a `datetime`, which holds a time to 1/300 of a second, so a `datetime2` value is rounded.  See [the known issues](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#known-issues).

More: [query methods](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#query-methods), [models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#models), [running queries in a transaction](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#queries-in-a-transaction).

### 5. Describe your queries

This step works with PostgreSQL and SQL Server: for another database, or for queries that want only their SQL, skip it and set the output to `sql`, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  The tool asks your database what the parameters and columns of each query are, without running the query, and writes the answers into a sidecar file beside the `.sql` file: `Users.sql.json`.

- **That checks every query against your schema**: a table or a column that does not exist, or a parameter whose type cannot be worked out, is an error now and not when the query runs.
- **The build reads the sidecars**, and reports an error for a query that has no entry in its sidecar, or whose entry is older than the query: run `describe` again after you change a query.  The models and the methods are built from them, so that the build never needs a database.

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

Set the output to `models`, to run the queries yourself, with Dapper or your own ADO.NET code.  A query whose output is `models` gets its models, once the tool has described it, as in step 5, and a constant that holds its SQL, or a method that returns it when the query has a token, in place of the method that runs it:

```xml
<PropertyGroup>
    <SqlSourceOutput>models</SqlSourceOutput>
</PropertyGroup>
```

The constants are in a private class `Sql` of the type, `Sql.GetUser` and `Sql.FindUsers`, and are plain strings, so any library can run them: Dapper here (`dotnet add package Dapper`).

```csharp
using System.Data.Common;
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    public Task<GetUserDto?> Get(int id) =>
        connection.QuerySingleOrDefaultAsync<GetUserDto>(Sql.GetUser, new GetUserParams(id));

    public Task<IEnumerable<FindUsersDto>> Find(string? name) =>
        connection.QueryAsync<FindUsersDto>(Sql.FindUsers, new FindUsersParams(name));
}
```

Dapper reads the rows by the names of their columns.  For columns written in snake_case, such as `created_at`, set Dapper's `DefaultTypeMap.MatchNamesWithUnderscores` to `true` once when your application starts.  Write the parameters in camelCase, `@userId` and not `@user_id`: Dapper sends a property of `GetUserParams` only to the parameter written as its name, ignoring case but not `_`.  Set a column's nullability with a `-- column:` line, not a mark at the end of its alias, which changes the column's name.  All three are in [running the models through Dapper](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#models-through-dapper).  With SQL Server, send a `datetime2` parameter as a `datetime2`, since Dapper sends a `DateTime` as a `datetime`: see [with SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#dapper-with-sql-server).

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), which can also be set for one folder, one file or one query, [constants](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#constants), and [models](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#models), for their names, shape and properties.

## Just want to keep your SQL out of code files?

Follow steps 1 to 3, set the output to `sql`, and skip the tool.  For a SQL Server project:

```xml
<PropertyGroup>
    <SqlSourceDialect>mssql</SqlSourceDialect>
    <SqlSourceOutput>sql</SqlSourceOutput>
</PropertyGroup>
```

- **`sql` asks for each query's SQL and nothing else**: its constant, or, for a query with a `{{token}}`, a method that fills it in, which you run as [Only want to generate models?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#only-want-to-generate-models) shows, without the models.  It needs no database, no tool and no sidecar.  It tells the tool to leave these queries out, so a run on the project reports nothing for them, and the build to need no sidecar for them and to generate no models.  Without it the default output, `codegen`, asks for every query to be described, and for a method that runs it, and the build reports an error for each file or query that is not.
- **Any dialect works**: `mssql` for SQL Server, `mysql`, `sqlite`, the default `ansi` and the others.  Set the one named for your database, so that SqlSource reads your comments and strings as your database does.

More: [output](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-output), [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support).

## FAQ

### I want my query methods to have `Async` names

Add the generator parameter `async-method-suffix`: `<SqlSourceGeneratorParameters>async-method-suffix</SqlSourceGeneratorParameters>` in the project file, and `GetUser`'s method is `GetUserAsync`.  It can also be given for one type, folder, file or query.  See [generator parameters](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-generator-parameters).

### A query method throws `InvalidCastException` or `SqlNullValueException`

The driver could not read a column, or send a parameter, as its C# type.  Most often a [type override](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-type-overrides) names a type the driver was never taught, such as a C# enum for a PostgreSQL enum without Npgsql's `MapEnum<T>()` on the data source, or a type of your own: teach the driver the type, or map the database type to one it reads.  When the message is "Column 'name' is null.", or, on SQL Server, the exception is Microsoft.Data.SqlClient's `SqlNullValueException`, "Data is Null.", the column held a null though its property cannot be null: give the column a nullable property, by its alias or a `-- column:` line, or keep the null out in the query.  A few types cannot be sent by their value: see [the known issues](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#known-issues).

> **Coming in a later release.**  A converter of your own will turn a type that the driver does not read into one that it does, and each parameter will be sent as its database type.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

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

Both projects can use SqlSource.  Each has its own copy of the attribute, and SqlSource turns off the compiler's warning about the two copies.  Each also has its own `UnexpectedRowCountException`, so the test project catches `InvalidOperationException`, from which it derives, for a method of the main project.  See [the recipe](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#test-project-sees-internals).

## NodaTime and other libraries

> **Coming in a later release.**  Generated models and query methods will use the types of libraries such as NodaTime for the database types they cover.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

## Custom type mapping

Each column and parameter of a PostgreSQL query whose output is `models` or `codegen`, and whose entry in its sidecar is current, gets the C# type that Npgsql reads its database type as: see [the PostgreSQL types](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#type-map).  Those of a SQL Server query get the C# type that Microsoft.Data.SqlClient reads their database type as: see [the SQL Server types](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#types).  A project at the output `models` that references no Microsoft.Data.SqlClient, such as a library that leaves the driver to its application, gets the same types, but for `vector`, which needs the reference: see [which version of Microsoft.Data.SqlClient](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md#which-sqlclient).  A few PostgreSQL types follow the version of Npgsql that the project references: Npgsql 10 reads `date`, `time`, `cidr`, `daterange` and `datemultirange` as other types than Npgsql 8 and 9 do, and a project that references no Npgsql gives those types their C# types with the type override, `date=System.DateOnly time=System.TimeOnly` for an application on Npgsql 10: see [which version of Npgsql](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/postgresql.md#which-npgsql).  The type override gives a database type the C# type of your choice instead, for the project in its file, for some files as item metadata, or in a `.sql` file with `-- type-override:`, and each place changes only the types it names:

```xml
<PropertyGroup>
    <SqlSourceTypeOverrides>timestamptz=System.DateTimeOffset public.user_status=MyApp.UserStatus</SqlSourceTypeOverrides>
</PropertyGroup>
```

Each property of a model has the type its database type maps to, and a query method asks the driver for that type, so it must be one the driver reads: see [the FAQ](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#a-query-method-throws-invalidcastexception-or-sqlnullvalueexception).  An entry that is not written correctly, or whose key has no schema and is not a type SqlSource knows, is a build error, and so is a C# type that the project cannot find or use, at each query that uses it.  See [type overrides](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#setting-type-overrides).

## Where to next

| I want to | Go to |
|----|----|
| Run a query, in a transaction, and know what its method returns and throws | [Query methods](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/generator.md#query-methods) |
| Configure one part of a project differently | [Configure one folder differently](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#configure-a-folder) |
| Look up a marker or a setting | [The marker index](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sql-files.md#marker-index), [all settings](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/settings.md#all-settings) |
| Use SQL Server, or another database | [SQL Server](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/sqlserver.md), [database support](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/dialects.md#database-support) |
| Keep the sidecars current after a schema change | [After a schema change](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#after-a-schema-change) |
| Set up CI | [Check in CI](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#check-in-ci), [a GitHub Actions job](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/recipes.md#check-sidecars-in-ci) |
| Look up a build error or a tool error | [Diagnostics](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md) |
| See a whole project that uses SqlSource, with its database | [The PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md), [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md) |
| See everything | [The manual](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md) |

To work on SqlSource itself, see [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md).  SqlSource is released under the [MIT licence](https://github.com/mbcrawfo/SqlSource/blob/main/LICENSE).
