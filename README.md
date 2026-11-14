# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/vpre/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

[SqlSource](https://mbcrawfo.github.io/SqlSource/) keeps the SQL of your C# project in `.sql` files, and generates the C# that runs it: a method, and records for its parameters and rows.  SQL in a file of its own is easier to work with: your editor highlights it, SQL tools format it, and a search finds it.  A renamed or removed query, or a column that your code reads and the query no longer returns, is a compile error.  With PostgreSQL or SQL Server, the `sqlsource` tool checks every query against your database's schema.

A query in `Queries/UserRepository.sql`, and the C# that calls it:

```sql postgres
-- name: GetUser -> one-optional
-- summary: One user, or null.
SELECT id, name, email
FROM users
WHERE id = @userId;
```

```csharp
await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=app;Username=app;Password=secret");
var users = new UserRepository(dataSource);
GetUserDto? user = await users.GetUser(new GetUserParams(1));
```

[Your first query](https://mbcrawfo.github.io/SqlSource/start/first-query.html) makes this pair, for PostgreSQL and for SQL Server.

## Supported databases

- PostgreSQL 14 or later and SQL Server 2017 or later get typed models and methods for your queries: see [supported versions](https://mbcrawfo.github.io/SqlSource/reference/databases/index.html#supported-versions).
- Azure SQL is accepted, and not tested.
- Any other database gets your SQL as constants: see [another database](https://mbcrawfo.github.io/SqlSource/guides/other-databases.html).

## Install

In the folder of each project that has queries, add the `SqlSource` package:

```console
$ dotnet add package SqlSource
```

Add the driver package of your database to the same project: `dotnet add package Npgsql` for PostgreSQL, or `dotnet add package Microsoft.Data.SqlClient` for SQL Server.

Once, at the root of your repository, install the tool.  If your repository already has a tool manifest, leave out the first command.

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

Your project targets .NET 8 or later, and you build it with the .NET SDK 9.0.300 or later: see [what the build needs](https://mbcrawfo.github.io/SqlSource/reference/msbuild.html#build-requirements).  [Install](https://mbcrawfo.github.io/SqlSource/start/install.html) gives each part in full.

## Use

1. [Write a query](https://mbcrawfo.github.io/SqlSource/start/first-query.html#write-a-query) in a `.sql` file, such as `Queries/UserRepository.sql`.
2. [Mark a partial class](https://mbcrawfo.github.io/SqlSource/start/first-query.html#mark-a-partial-class) of the same name, in the same folder, with `[SqlSourceGenerate]`.
3. [Name your database](https://mbcrawfo.github.io/SqlSource/start/first-query.html#name-your-database) in a `sqlsource.json` file beside your project file: `"dialect": "postgres"` or `"dialect": "mssql"`.
4. [Put your connection string](https://mbcrawfo.github.io/SqlSource/start/first-query.html#connection) in a `.env` file in the folder of your project, and build.  The build [describes the query](https://mbcrawfo.github.io/SqlSource/start/first-query.html#describe) and writes its sidecar file, which you commit.
5. [Call the method](https://mbcrawfo.github.io/SqlSource/start/first-query.html#call-the-methods), `GetUser`, with its record of parameters, `GetUserParams`.

[The full walkthrough](https://mbcrawfo.github.io/SqlSource/start/first-query.html) gives each file, the commands and the errors you can see on the way.

## Other ways to use it

By default, each query is a method of your class, which opens its own connection.

- **Your own connection, transaction or timeout**: give the query the output `methods`.  Its method takes your connection and your transaction: see [Run queries in a transaction](https://mbcrawfo.github.io/SqlSource/start/transactions.html).
- **Dapper or your own ADO.NET code**: if you cannot use the methods, the output `models` gives you the models and the SQL, and you run the query: see [Use Dapper](https://mbcrawfo.github.io/SqlSource/guides/dapper.html).
- **Only the SQL, on any database**: the output `sql` gives you each query's SQL as a constant, with no tool: see [another database](https://mbcrawfo.github.io/SqlSource/guides/other-databases.html).

## Documentation

- [Get started](https://mbcrawfo.github.io/SqlSource/start/install.html): read in order, from the install to a tested application.
- [Guides](https://mbcrawfo.github.io/SqlSource/guides/lists.html): one task on each page, such as a list after `IN` or a JSON column.
- [Reference](https://mbcrawfo.github.io/SqlSource/reference/sql-files.html): one page for each thing, with all of its rules and settings.
- [Errors](https://mbcrawfo.github.io/SqlSource/diagnostics.html): what each SQLSRC error means, and how to fix it.
- Whole projects with their databases: [the PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) and [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md).

To work on SqlSource itself, see [CONTRIBUTING.md](https://github.com/mbcrawfo/SqlSource/blob/main/CONTRIBUTING.md).  SqlSource is released under the [MIT licence](https://github.com/mbcrawfo/SqlSource/blob/main/LICENSE).
