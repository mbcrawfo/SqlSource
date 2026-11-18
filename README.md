# SqlSource

[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain) [![NuGet](https://img.shields.io/nuget/vpre/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)

[Get started](https://mbcrawfo.github.io/SqlSource/start/install.html) | [Guides](https://mbcrawfo.github.io/SqlSource/guides/lists.html) | [Reference](https://mbcrawfo.github.io/SqlSource/reference/sql-files.html) | [Errors](https://mbcrawfo.github.io/SqlSource/diagnostics.html) | [Example projects](https://github.com/mbcrawfo/SqlSource/tree/main/examples)

SqlSource is a source generator that reads your SQL queries, stored in `.sql` files, and generates C# models and methods to run the SQL.  It's inspired by [sqlc](https://github.com/sqlc-dev/sqlc) and [sqlx](https://github.com/transact-rs/sqlx).

Why use SqlSource?

* Your SQL is stored in `.sql` files so that it's IDE friendly (syntax highlighting, refactoring tools, etc), easier to search, and works with SQL linters and formatters.
* Strongly typed input and output for SQL queries.  Catch mismatches between your SQL and C# at compile time.
* Generated repository classes.  No hand-written code aside from the SQL queries.
* No reflection, no dynamic runtime mapping.  The output of SqlSource is the same ADO.Net code that you would write by hand.

## Quick start

**Note:** The quick start demonstrates a simple Postgres setup.  Full guides for all supported databases are in the [get started](https://mbcrawfo.github.io/SqlSource/start/install.html) documentation.

### Requirements

* Your project must target .Net 8+, use C# 12+, and compile using .Net SDK 9.0.300 or newer.
* If using Visual Studio, VS 2022 17.14 or newer is required.
* PostgreSQL 14+ or SQL Server 2017+.  Using another database?  You can use SqlSource to [keep your queries in .sql files](https://mbcrawfo.github.io/SqlSource/guides/other-databases.html), but model and method generation is not supported.

### Installation

SqlSource consists of two components: a source generator for your C# projects, and a tool that reads type and nullability information for your queries.

```console
# Run this for each project that will define SQL queries.
$ dotnet add package SqlSource

# Run these commands from the root of your repository.
# Skip the tool-manifest creation if your repository already has a dotnet-tools.json.
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

A project that uses SqlSource also needs the appropriate database driver package installed (`Npgsql` or `Microsoft.Data.SqlClient`).

### Configure your database dialect

Create the file `sqlsource.json` at the root of your repository so that SqlSource knows your database type:

```json
{
    "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
    "root": true,
    "dialect": "postgres"
}
```

The root [sqlsource.json](https://mbcrawfo.github.io/SqlSource/reference/settings-file.html) defines default settings for the projects in your repository.  You can add additional `sqlsource.json` files in nested folder (without the `root` property) for project or folder-specific settings.  Most SqlSource can also be set or overridden in an individual `.sql` file.

### Create a partial repository class

SqlSource can only inject code into a partial class.  Add the following to `UserRepository.cs`:

```csharp
using Npgsql;
using SqlSource;

[SqlSourceGenerate]
public sealed partial class UserRepository(NpgsqlDataSource dataSource);
```

SqlSource automatically detects fields, properties, or primary-constructor parameters whose type extends `DbDataSource`, as well as `string` members named `ConnectionString`/`connectionString`/`_connectionString`, and use them to open connections.  It can also be [configured to use connection factories and other sources](https://mbcrawfo.github.io/SqlSource/guides/repository-connection.html).

### Write your queries

Create `UserRepository.sql` beside the `UserRepository.cs` file.  By default the generator looks for a SQL file matching the class name, but you can tell it to [read a different file or a folder containing multiple files](https://mbcrawfo.github.io/SqlSource/reference/attributes.html#source).

```sql postgres
-- name: GetUser -> one-optional
SELECT id, name, email
FROM users
WHERE id = @userId;
```

Every query must be marked with a `-- name:` comment, which sets the name of the method and its input/output models.  The annotation `-> one-optional` indicates that this query returns 1 row or none, so the return type of the generated method is nullable.

### Describe the queries

The `dotnet sqlsource` tool connects to your database to learn the types and nullability of your query parameters and results.  Running the tool creates `UserRepository.sql.json`, a sidecar file.  Be sure to commit the sidecar files to git.

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
postgres (postgres): 1 described, 0 skipped, 0 failed
```

Running `dotnet sqlsource describe` from your solution folder will cover all projects in the solution.  Alternatively, you can run it from an individual project's folder.  You can use a `.env` file or environment variables to [provide connection strings to the tool](https://mbcrawfo.github.io/SqlSource/reference/connections.html).  If you set the location of the `.env` in your `.csproj` files (it's recommended to use [Directory.Build.props](https://mbcrawfo.github.io/SqlSource/start/first-query.html#connection)), running `dotnet build` will describe your queries automatically.

### Use the repository

Run `dotnet build` to build your project so that SqlSource can use the sidecar file to generate the `GetUser` method and its models.  They're now ready to use in your code:

```csharp
await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=app;Username=app;Password=secret");
var users = new UserRepository(dataSource);
GetUserDto? user = await users.GetUser(new GetUserParams(1));
```
