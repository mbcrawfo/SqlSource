# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/vpre/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

SqlSource keeps the SQL of a C# project in `.sql` files, out of your code files, and generates the C# that uses it: for a query that your database has described, a type for its parameters, a type for its rows and a method that runs it, and for any other query a constant that holds it.  SQL in a file of its own is cleaner than SQL in a C# string: your editor highlights it, SQL linters and formatters work on it, and it is easy to find when you search for a table or a column.  A query that is renamed or removed is a compile error where it is used, and so is a column that your code reads and the query no longer returns, and your editor shows a query's SQL wherever it is used.  With PostgreSQL or SQL Server, the `sqlsource` tool checks every query against your database's schema, and records what the database says of its parameters and columns, from which the types and the methods are generated.

**Databases:** keeping SQL in `.sql` files and generating constants works with any database: SQL Server and Azure SQL, PostgreSQL, MySQL, MariaDB, SQLite, Oracle, CockroachDB and others.  Describing queries, in which the `sqlsource` tool asks your database about each query's parameters and columns, works with PostgreSQL 14 or later, and with SQL Server 2017 or later and Azure SQL, which is accepted and not tested; other databases are not described: see [database support](https://mbcrawfo.github.io/SqlSource/reference/databases/index.html#database-support).  So with PostgreSQL or SQL Server, follow the five steps below: by default every query must be described, which step 5 does, and the build reports an error for each query until it is.  With any other database, follow steps 1 to 3 and set the output to `sql`, which gives you a constant for each query and needs no description, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  To run the queries yourself, through Dapper or your own ADO.NET code, see [only generating models](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#only-want-to-generate-models).

## Installation

In the folder of each project that has queries:

```console
$ dotnet add package SqlSource
```

Once, at the root of your repository, to install the tool (skip the first command if the repository already has a tool manifest):

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

- **`SqlSource`** is the generator.  Add it to each project whose queries you want generated.  It runs in the compiler when you build and adds no runtime dependency: no assembly of it ships with your application, and it is not a dependency of a package you build.  Only the code it generates, the attributes you mark your types and converters with, and the exceptions its methods throw, are compiled into your project.
- **`SqlSource.Tool`** adds the `sqlsource` command, which describes your queries ([step 5](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#5-describe-your-queries)).  Commit the `dotnet-tools.json` that `dotnet new tool-manifest` creates, and keep the tool at the version of the `SqlSource` package.  A project that only keeps its SQL out of code files does not need it.

**Supported environments.**  A project that uses SqlSource targets .NET 8 or later and compiles C# 12 or later, the default for .NET 8.  It builds in the .NET SDK 9.0.300 or later, or in Visual Studio 2022 17.14 or later; the .NET 8 SDK and Visual Studio before 17.14 are not supported, and a build on one stops with [SQLSRC021](https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc021).  The tool runs on .NET 8 or later, with the .NET SDK that builds your project installed, since it asks MSBuild about your projects; it describes PostgreSQL 14 or later and SQL Server 2017 or later, and accepts Azure SQL, which is not tested.  A project that references Npgsql for its PostgreSQL queries references Npgsql 8 or later, and one that references Microsoft.Data.SqlClient for its SQL Server queries references version 6.1 or later; the methods that run the queries need that reference, and a SQL Server project on EF Core 8 or 9 references Microsoft.Data.SqlClient 6.1 itself, since EF Core brings 5.1.  The methods also need a connection whose commands take the driver's own parameters, which a connection that wraps the driver's may not: see [a limit](https://mbcrawfo.github.io/SqlSource/reference/query-methods.html#limit-wrapped-connection).

## Getting started

### 1. Write your queries

Save this as `Queries/UserRepository.sql`:

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

More: [the marker index](https://mbcrawfo.github.io/SqlSource/reference/sql-files.html#marker-index), which lists every `-- word:` line that SqlSource reads, [the SQL your editor shows](https://mbcrawfo.github.io/SqlSource/reference/members.html#intellisense), [result shapes](https://mbcrawfo.github.io/SqlSource/reference/sql-files.html#result-shapes), [parameters and tokens](https://mbcrawfo.github.io/SqlSource/reference/parameters.html).

### 2. Mark a partial type

In the same folder, `Queries/UserRepository.cs`:

```csharp
using Npgsql;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

The type gets the `.sql` file named for it, `UserRepository.sql`, in the folder of its C# file, and becomes a repository of its queries: once they are described, in step 5, the build gives it a method for each query, `GetUser` and `FindUsers`, which gets its connection from the data source.  The methods run through your database's driver, which the project references: `dotnet add package Npgsql`, or `Microsoft.Data.SqlClient` for SQL Server, which has no data source, so a SQL Server repository takes a `string connectionString` instead.  Until the queries are described the build reports an error for each one: by default each query must be described by its database, which needs the dialect of step 3 and the tool of step 5.  More: [the generator](https://mbcrawfo.github.io/SqlSource/reference/attributes.html#files-of-a-type), for every file of a folder or another file, and [a repository's connection](https://mbcrawfo.github.io/SqlSource/reference/repositories.html#repository-connection), for a connection string, a method of your own, or a database of each kind.

### 3. Set the dialect

Save this as `sqlsource.json`, beside your project file:

```jsonc
// sqlsource.json, beside your project file
{
    // No settings file in a folder above this one applies to the project.
    "root": true,
    "dialect": "postgres",
}
```

A settings file applies to the `.sql` files of its folder and of every folder below it, so this one reads every `.sql` file of the project as PostgreSQL.  Databases disagree about where a comment or a string ends, and SqlSource takes the comments out of your SQL, so it reads each file by the rules of your database: its dialect.  The dialect also tells the tool which database to ask.  The default is `ansi`; set the one named for your database, such as `postgres` (PostgreSQL), `mssql` (SQL Server), `mysql` or `sqlite`.  More: [choosing a dialect](https://mbcrawfo.github.io/SqlSource/reference/dialects.html#choosing-a-dialect), [settings files](https://mbcrawfo.github.io/SqlSource/reference/settings-file.html#settings-files), [settings](https://mbcrawfo.github.io/SqlSource/reference/settings.html#all-settings) for everything else you can set for a project, a folder or one query.

### 4. Use the queries

Once the queries are described, in step 5, the build generates their models, a record for the parameters of each query, `GetUserParams` and `FindUsersParams`, and a record for its rows, `GetUserDto` and `FindUsersDto`, with a property for each parameter and each column.  It also gives `UserRepository` a public method for each query, which opens a connection from the data source, sends the parameters, runs the query, reads its rows into the models and disposes the connection.  This assumes that the table `users` has more columns than these three, as a table of users usually does.  In your application:

```csharp
await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=app;Username=app;Password=secret");
var users = new UserRepository(dataSource);

GetUserDto? user = await users.GetUser(new GetUserParams(42));
FindUsersDto[] named = await users.FindUsers(new FindUsersParams("Ada"));
```

`GetUser` returns the user, or null when there is none, and `FindUsers` every row, in an array.  Each method also takes a cancellation token.  `UserRepository` also gets an interface of its methods, `IUserRepository`, so the code that uses it can take the interface, and a test can give it a fake.  A property can be null where its column can, and `FindUsersParams.Name` can be null because its `-- param:` says `null`.  Where you know a column is never null and the database cannot tell, an alias such as `AS "total!"`, or a `-- column: total not null` line, says so, and the method throws if the column is null after all: see which columns can be null for [PostgreSQL](https://mbcrawfo.github.io/SqlSource/reference/databases/postgresql/nullability.html#nullability) and for [SQL Server](https://mbcrawfo.github.io/SqlSource/reference/databases/sqlserver/nullability.html#nullability).  A query that selects every column of a table, in the table's order, gets a row model named after the table instead: if `users` had exactly the columns `id`, `name` and `email`, in that order, both queries would share one model, `UsersDto`, and the code would use it in place of `GetUserDto` and `FindUsersDto`.  Each parameter is sent as the database type it was described with, not as a type the driver chooses from its C# type, and with SQL Server with that type's length, precision and scale: see [how a parameter is sent](https://mbcrawfo.github.io/SqlSource/reference/query-methods.html#method-parameters).  For `Async` names, `GetUserAsync`, add the generator parameter `async-method-suffix`: see [generator parameters](https://mbcrawfo.github.io/SqlSource/reference/generator-parameters.html#setting-generator-parameters).  For code that does not await, a query gets a synchronous method, in place of its asynchronous one or beside it, on the repository and in its interface, with the generator parameter `method-type=sync` or `method-type=both`: see [synchronous methods](https://mbcrawfo.github.io/SqlSource/reference/query-methods.html#synchronous-methods).

More: [repository methods](https://mbcrawfo.github.io/SqlSource/reference/repositories.html#repository-methods), [models](https://mbcrawfo.github.io/SqlSource/reference/models.html#models), [running queries in a transaction](https://mbcrawfo.github.io/SqlSource/start/transactions.html#queries-in-a-transaction).

### 5. Describe your queries

This step works with PostgreSQL and SQL Server: for another database, or for queries that want only their SQL, skip it and set the output to `sql`, as [Just want to keep your SQL out of code files?](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#just-want-to-keep-your-sql-out-of-code-files) shows.  The tool asks your database what the parameters and columns of each query are, without running the query, and writes the answers into a sidecar file beside the `.sql` file: `UserRepository.sql.json`.

- **That checks every query against your schema**: a table or a column that does not exist, or a parameter whose type cannot be worked out, is an error now and not when the query runs.
- **The build reads the sidecars**, and reports an error for a query that has no entry in its sidecar, or whose entry is older than the query.  The models and the methods are built from them, so that a build needs no database.

Give the tool the connection to your database in a `.env` file in your project's folder, and add `.env` to your `.gitignore`, since it holds your password:

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

Then run the tool from the folder of your project or solution, or name it, as in `dotnet sqlsource describe src/App/App.csproj`:

```console
$ dotnet sqlsource describe
postgres (postgres): 2 described, 0 skipped, 0 failed
$ git add Queries/UserRepository.sql.json
```

`postgres`, in the variable's name and first in the summary, is the name SqlSource gives the database your queries belong to: the dialect's name, until you [name your databases](https://mbcrawfo.github.io/SqlSource/guides/several-databases.html#use-several-databases).  The name in parentheses is its dialect.  The tool reads the `.env` in the folder of the project or solution it runs on, never one in a folder above it, and only its `SQLSOURCE_CONNECTION` variables, so the file can be your application's own too.  To keep one `.env` at the root of your repository, name it to the build with the property `SqlSourceEnvFile` in your `Directory.Build.props`, and to a run of your own with `--env-file`: see [the `.env` file](https://mbcrawfo.github.io/SqlSource/reference/connections.html#env-file).  The same variable set in the environment wins over the file, and `--connection postgres="..."` gives the connection on the command line instead.  For SQL Server files the database's name is `mssql`, so the variable is `SQLSOURCE_CONNECTION_MSSQL`, and the connection string is SqlClient's, `Server=localhost,1433;Database=app;User ID=app;Password=secret`, with `TrustServerCertificate=True` for SQL Server in a local container: see [connecting to SQL Server](https://mbcrawfo.github.io/SqlSource/reference/databases/sqlserver/index.html#connecting).  The tool needs your schema, not your data.  Commit the sidecars with the project: every build reads them, and a copy of the project without them reports an error for each query.  Add `*.sql.json linguist-generated=true` to your `.gitattributes` so that GitHub collapses them in pull requests, and after a merge conflict in one, run `describe` again rather than merging it by hand: see [sidecars in git](https://mbcrawfo.github.io/SqlSource/reference/sidecars.html#sidecars-in-git).  A project that sets `SqlSourceIncludeFiles` to `false` lists its sidecars itself, beside its `.sql` files: see [which `.sql` files are included](https://mbcrawfo.github.io/SqlSource/reference/msbuild.html#included-files).

From then on, `dotnet build` and your IDE's build run `describe` themselves before they compile, on a machine where they find the project's `.env` file or `SQLSOURCE_CONNECTION` in the environment, and only when something changed: a query you change is described at the next build, and one your database rejects is a build error at the query.  Commit the sidecars it writes.  A build with no connection, and every build in CI, reads the committed sidecars instead, and `SqlSourceOnline` turns the build's run on or off for a project: see [running in the build](https://mbcrawfo.github.io/SqlSource/reference/msbuild.html#running-in-the-build).  To describe each query as you save it, leave `dotnet sqlsource describe --watch` running, or have your editor start it: see [describe on save](https://mbcrawfo.github.io/SqlSource/reference/describing.html#describe-on-save), which says when your editor shows what it writes, and [the recipe](https://mbcrawfo.github.io/SqlSource/guides/describe-on-save.html#start-the-watcher-with-your-editor).

Run `describe --force` after a change to the schema, which no build sees, and `describe --check` on a build server to catch a sidecar that is out of date.  Never edit a sidecar by hand.  More: [the tool](https://mbcrawfo.github.io/SqlSource/reference/cli.html), [connections](https://mbcrawfo.github.io/SqlSource/reference/connections.html#connections), [PostgreSQL](https://mbcrawfo.github.io/SqlSource/reference/databases/postgresql/index.html), [SQL Server](https://mbcrawfo.github.io/SqlSource/reference/databases/sqlserver/index.html).

## Only want to generate models?

See [Use Dapper or your own ADO.NET code](https://mbcrawfo.github.io/SqlSource/guides/dapper.html#models).

## Just want to keep your SQL out of code files?

See [Use Dapper or your own ADO.NET code](https://mbcrawfo.github.io/SqlSource/guides/dapper.html#sql-only).

## FAQ

### How do I mock a repository?

See [Test code that uses a repository (mock or fake)](https://mbcrawfo.github.io/SqlSource/start/testing.html).

### How do I run queries in a transaction?

See [Run queries in a transaction (or with a timeout)](https://mbcrawfo.github.io/SqlSource/start/transactions.html#queries-in-a-transaction).

### I got an `InvalidCastException`; what does it mean and how do I fix it?

See [Troubleshooting: it built, but something is wrong](https://mbcrawfo.github.io/SqlSource/guides/troubleshooting.html#invalidcastexception).

### How do I change the namespace of the models?

See [Change names, namespaces and visibility](https://mbcrawfo.github.io/SqlSource/guides/naming-and-placement.html#model-namespace-task).

### My project uses several databases

See [Use several databases](https://mbcrawfo.github.io/SqlSource/guides/several-databases.html#use-several-databases).

### How do I keep migration scripts out?

See [Give a folder its own settings, or leave files out](https://mbcrawfo.github.io/SqlSource/guides/folders-and-files.html#leave-migration-scripts-out).

### How do I choose a table name at run time?

See [Choose a table or a clause at run time](https://mbcrawfo.github.io/SqlSource/guides/dynamic-sql.html#table-name-at-run-time).

### Can I pass a list to `IN`?

See [Pass a list of values (IN)](https://mbcrawfo.github.io/SqlSource/guides/lists.html#list-after-in).

### My test project sees the internals of my main project

See [Use SqlSource in several projects](https://mbcrawfo.github.io/SqlSource/guides/several-projects.html#test-project-sees-internals).

### How do I report a wrong model?

See [Report a wrong model](https://mbcrawfo.github.io/SqlSource/guides/report-a-bug.html#report-a-bug).

## NodaTime and other libraries

See [Use NodaTime and NetTopologySuite](https://mbcrawfo.github.io/SqlSource/guides/nodatime-and-spatial.html).

## Custom type mapping

See [Use your own types: ids, enums, value objects](https://mbcrawfo.github.io/SqlSource/guides/custom-types.html#type-override).

## Where to next

| I want to | Go to |
|----|----|
| Run a query, in a transaction, and know what its method returns and throws | [Query methods](https://mbcrawfo.github.io/SqlSource/reference/query-methods.html#query-methods) |
| Give a repository its connection another way, or use several databases | [A repository's connection](https://mbcrawfo.github.io/SqlSource/reference/repositories.html#repository-connection) |
| Configure one part of a project differently | [Configure one folder differently](https://mbcrawfo.github.io/SqlSource/guides/folders-and-files.html#configure-a-folder) |
| Look up a marker or a setting | [The marker index](https://mbcrawfo.github.io/SqlSource/reference/sql-files.html#marker-index), [all settings](https://mbcrawfo.github.io/SqlSource/reference/settings.html#all-settings) |
| Use SQL Server, or another database | [SQL Server](https://mbcrawfo.github.io/SqlSource/reference/databases/sqlserver/index.html), [database support](https://mbcrawfo.github.io/SqlSource/reference/databases/index.html#database-support) |
| Keep the sidecars current after a schema change | [After a schema change](https://mbcrawfo.github.io/SqlSource/reference/describing.html#after-a-schema-change) |
| Set up CI | [Check in CI](https://mbcrawfo.github.io/SqlSource/reference/describing.html#check-in-ci), [a GitHub Actions job](https://mbcrawfo.github.io/SqlSource/guides/ci.html#check-sidecars-in-ci) |
| Look up a build error or a tool error | [Diagnostics](https://mbcrawfo.github.io/SqlSource/diagnostics.html) |
| See a whole project that uses SqlSource, with its database | [The PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md), [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md) |
| See everything | [The manual](https://mbcrawfo.github.io/SqlSource/) |

To work on SqlSource itself, see [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md).  SqlSource is released under the [MIT licence](https://github.com/mbcrawfo/SqlSource/blob/main/LICENSE).
