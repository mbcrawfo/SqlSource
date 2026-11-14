# Your first query

This tutorial is a quick start for PostgreSQL and SQL Server: at its end, one SQL query runs from your C# code.

The steps use a table `users` with the columns `id`, `name`, `email` and `created_at`, none of which can be null.  If your database has no such table, create it with one row:

# [PostgreSQL](#tab/postgresql)

```sql postgres
CREATE TABLE users (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL,
    email text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO users (name, email) VALUES ('Ada', 'ada@example.com');
```

# [SQL Server](#tab/sqlserver)

```sql mssql
CREATE TABLE users (
    id int IDENTITY PRIMARY KEY,
    name nvarchar(100) NOT NULL,
    email nvarchar(200) NOT NULL,
    created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

INSERT INTO users (name, email) VALUES ('Ada', 'ada@example.com');
```

---

<a id="write-a-query"></a>

## 1. Write a query

In your project, create the file `Queries/UserRepository.sql` with this query:

```sql postgres
-- name: GetUser -> one-optional
-- summary: One user, or null.
SELECT id, name, email
FROM users
WHERE id = @userId;
```

- `-- name: GetUser` starts a query and gives it its name.  `-> one-optional` says that the query returns one row, or none.
- `-- summary:` documents the query.  Your editor shows the summary where you call the query.
- `@userId` is a query parameter.  You give its value when you call the query.

Nothing changes yet.  The build uses the file after the next step.

<a id="mark-a-partial-class"></a>

## 2. Mark a partial class

In the same folder, create the file `Queries/UserRepository.cs`:

# [PostgreSQL](#tab/postgresql)

```csharp
using Npgsql;
using SqlSource;

[SqlSourceGenerate]
public sealed partial class UserRepository(NpgsqlDataSource dataSource);
```

# [SQL Server](#tab/sqlserver)

```csharp
using SqlSource;

[SqlSourceGenerate]
public sealed partial class UserRepository(string connectionString);
```

Microsoft.Data.SqlClient has no data source, so the class takes a connection string.

---

The class reads the `.sql` file that has its name and is in its folder: `UserRepository.sql`.  The build gives the class one method for each query of that file.  Each method opens a connection from the data source or the connection string of the class.

Build the project with `dotnet build`.  **The build fails, and that is expected.**  It reports [SQLSRC209](../diagnostics.md#sqlsrc209) at the query, because it does not know your database yet.  Steps 3 to 5 remove the error.

<a id="name-your-database"></a>

## 3. Name your database

Save this file as `sqlsource.json`, beside your project file:

# [PostgreSQL](#tab/postgresql)

```jsonc
// sqlsource.json, beside your project file
{
    // No settings file in a folder above this one applies to the project.
    "root": true,
    "dialect": "postgres",
}
```

# [SQL Server](#tab/sqlserver)

```jsonc
// sqlsource.json, beside your project file
{
    // No settings file in a folder above this one applies to the project.
    "root": true,
    "dialect": "mssql",
}
```

---

The file tells SqlSource which database you use.  It applies to the `.sql` files of its folder and of every folder below it.

Build again.  The error is now [SQLSRC241](../diagnostics.md#sqlsrc241): the query has no description yet.  Steps 4 and 5 add one.

Without this file, the build reports SQLSRC209 for each `.sql` file that a class reads.

<a id="connection"></a>

## 4. Give the tool your connection

In the folder of your project, beside the project file, create a file named `.env`:

# [PostgreSQL](#tab/postgresql)

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

# [SQL Server](#tab/sqlserver)

```dotenv
SQLSOURCE_CONNECTION_MSSQL="Server=localhost,1433;Database=app;User ID=app;Password=secret;TrustServerCertificate=True"
```

Use `TrustServerCertificate=True` only for a server of your own, such as SQL Server in a local container.  Without it, the tool cannot open such a server.  The tool cannot sign in with Microsoft Entra ID: give a SQL Server login and its password.

---

Add `.env` to your `.gitignore` file, since it holds your password.

Put the `.env` file in the folder of your project.  The tool does not read a `.env` file in a folder above that one.  If your `.env` file is at the root of your repository, the tool does not read it, and reports [SQLSRC213](../diagnostics.md#sqlsrc213).  To keep the file at the root, name it in two places.  For the build, add this file at the root of your repository:

```xml
<!-- Directory.Build.props, at the root of the repository -->
<Project>
    <PropertyGroup>
        <SqlSourceEnvFile>$(MSBuildThisFileDirectory).env</SqlSourceEnvFile>
    </PropertyGroup>
</Project>
```

For the tool, give the path of the file from the folder of your project.  If your project is one folder below the root, run `dotnet sqlsource describe --env-file ../.env` in step 5.

<a id="describe"></a>

## 5. Describe

To describe a query, the tool asks your database what the query's parameters and columns are.  It does not run the query.  It writes the answer to `UserRepository.sql.json`, the sidecar file of `UserRepository.sql`.

Run the tool from the folder of your project.  Then add the sidecar to git:

# [PostgreSQL](#tab/postgresql)

```console
$ dotnet sqlsource describe
postgres (postgres): 1 described, 0 skipped, 0 failed
$ git add Queries/UserRepository.sql.json
```

# [SQL Server](#tab/sqlserver)

```console
$ dotnet sqlsource describe
mssql (mssql): 1 described, 0 skipped, 0 failed
$ git add Queries/UserRepository.sql.json
```

---

The first word of the line is the name that SqlSource gives your database.  The word in parentheses is its dialect.

Build again: the build succeeds.  Commit the sidecar with your code, since every build reads it.  With the `.env` file in place, `dotnet build` also describes a new query for you before it compiles.

- If the tool reports [SQLSRC220](../diagnostics.md#sqlsrc220), the project is not restored.  Run `dotnet restore`.  Then run the tool again.
- If the tool reports [SQLSRC224](../diagnostics.md#sqlsrc224), it could not open your database.  Check that the database runs.  Check that the connection string names it, a login and the right password.

<a id="call-the-methods"></a>

## 6. Call the methods

The build generated three things from your query:

- `GetUserParams`, a record with one property for each query parameter: `UserId`.
- `GetUserDto`, a record with one property for each column: `Id`, `Name` and `Email`.
- `GetUser`, a method of `UserRepository` that runs the query.

Call the method from your application, such as in `Program.cs`:

# [PostgreSQL](#tab/postgresql)

```csharp
using Npgsql;

await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=app;Username=app;Password=secret");
var users = new UserRepository(dataSource);

GetUserDto? user = await users.GetUser(new GetUserParams(1));
Console.WriteLine(user);
```

# [SQL Server](#tab/sqlserver)

```csharp
var users = new UserRepository("Server=localhost,1433;Database=app;User ID=app;Password=secret;TrustServerCertificate=True");

GetUserDto? user = await users.GetUser(new GetUserParams(1));
Console.WriteLine(user);
```

---

Run it:

```console
$ dotnet run
GetUserDto { Id = 1, Name = Ada, Email = ada@example.com }
```

`GetUser` returns null when no user has that id.

If your table `users` has only the columns `id`, `name` and `email`, in that order, the model is `UsersDto`.  A query that reads every column of a table gets a model named after the table.  Write `UsersDto` in your code in place of `GetUserDto`.

## If it does not work

- [SQLSRC209](../diagnostics.md#sqlsrc209) for each file that a class reads: the project has no `sqlsource.json`.  Add the file of step 3.
- [SQLSRC241](../diagnostics.md#sqlsrc241): the query is not described yet.  Do steps 4 and 5.
- [SQLSRC213](../diagnostics.md#sqlsrc213): the tool found no connection.  Put the `.env` file in the folder of your project, as step 4 says.
- [SQLSRC220](../diagnostics.md#sqlsrc220): the project is not restored.  Run `dotnet restore`.
- [SQLSRC224](../diagnostics.md#sqlsrc224): the tool could not open your database.  Check the connection string.  On SQL Server in a local container, add `TrustServerCertificate=True`.
- `GetUserDto` is not found: your table has only the query's columns, so the model is `UsersDto`.

## What you have now

A query in `Queries/UserRepository.sql`, its committed sidecar, and a class `UserRepository` whose method `GetUser` returns a row of your database.

Rules: [queries and names](../reference/sql-files.md#queries-and-names), [result shapes](../reference/sql-files.md#result-shapes), [summaries](../reference/sql-files.md#summaries), [query parameters](../reference/parameters.md#query-parameters), [the attribute](../reference/attributes.md#attribute), [which file a class reads](../reference/attributes.md#setting-source), [the connection](../reference/repositories.md#repository-connection), [the dialect](../reference/settings.md#setting-dialect), [the logical database](../reference/settings.md#setting-database), [settings files](../reference/settings-file.md#settings-files), [the `.env` file](../reference/connections.md#env-file), [the build's `.env` file](../reference/msbuild.md#setting-env-file), connecting to [PostgreSQL](../reference/databases/postgresql/index.md#connecting) and [SQL Server](../reference/databases/sqlserver/index.md#connecting), [what describe does](../reference/describing.md#describe), [which queries are described](../reference/describing.md#which-queries), [sidecar files](../reference/sidecars.md#sidecar-files), [repository methods](../reference/repositories.md#repository-methods), [model names](../reference/models.md#model-names), [table models](../reference/models.md#table-models).

Next: [How SqlSource works](how-it-works.md)
