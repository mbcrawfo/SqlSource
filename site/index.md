# SqlSource

SqlSource keeps the SQL of your C# project in `.sql` files, out of your code files, and generates the C# that runs it.  For each query, the build gives you a method, a record for its parameters and a record for its rows.  SQL in a file of its own is easier to work with than SQL in a C# string: your editor highlights it, SQL tools format it, and a search finds it.  A renamed query, or a column that your code reads and the query no longer returns, is a compile error.  With PostgreSQL or SQL Server, the `sqlsource` tool checks every query against your database's schema.

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

<a id="databases"></a>

## Does it work with my database?

| Your database | What you get |
|----|----|
| [PostgreSQL 14 or later](reference/databases/postgresql/index.md) | Typed models and methods for your queries |
| [SQL Server 2017 or later](reference/databases/sqlserver/index.md) | Typed models and methods for your queries |
| [Azure SQL](reference/databases/index.md#azure-sql) | Accepted, and not tested |
| [Any other database](guides/other-databases.md) | Your SQL as constants |

The versions are those of [supported versions](reference/databases/index.md#supported-versions).

**Start here: [Install](start/install.md)**

You use Dapper: start with [Use Dapper](guides/dapper.md).  Your database is not PostgreSQL or SQL Server: start with [another database](guides/other-databases.md).

<a id="find-what-you-need"></a>

## What do you want to do?

| I want to | Go to |
|----|----|
| Run queries in a transaction, or with a timeout | [Run queries in a transaction (or with a timeout)](start/transactions.md) |
| Pass a list to `IN` | [Pass a list of values (IN)](guides/lists.md) |
| Stop a property being nullable | [Fix a nullable property](guides/nullability.md) |
| Mock a repository in a test | [Test code that uses a repository (mock or fake)](start/testing.md) |
| Insert many rows in one call | [Insert or update many rows in one call (bulk)](guides/many-rows.md) |
| Use an enum or an id type of my own | [Use your own types: ids, enums, value objects](guides/custom-types.md) |
| Read a JSON column | [Read and write JSON columns](guides/json.md) |
| Use a second database | [Use several databases](guides/several-databases.md) |
| Keep the sidecars right in CI | [Check the sidecars in CI](guides/ci.md) |
| Describe as I save | [Describe queries as you save (watch)](guides/describe-on-save.md) |
| Choose a table name at run time | [Choose a table or a clause at run time](guides/dynamic-sql.md) |
| Use Dapper | [Use Dapper or your own ADO.NET code](guides/dapper.md) |
| Look up a setting and where to write it | [All settings](reference/settings.md#all-settings) |
| Look up a `-- word:` line | [The marker index](reference/sql-files.md#marker-index) |
| Look up a command or an option | [The command reference](reference/cli.md#command-reference) |
| It built, but something is wrong | [Troubleshooting: it built, but something is wrong](guides/troubleshooting.md) |

<a id="errors"></a>

## A build error?

- Each SQLSRC error has a link to its section of [the list of errors](diagnostics.md), which says what it means and how to fix it.
- [When something goes wrong](start/errors.md) shows how to read an error, and the errors you meet first.

<a id="the-three-parts"></a>

## The three parts

- **[Get started](start/install.md)** is read in order, from the install to a tested application.  [How SqlSource works](start/how-it-works.md) gives the words that the manual uses.
- **[Guides](guides/lists.md)** give one task on each page, such as a list after `IN` or a JSON column.
- **[Reference](reference/sql-files.md)** has one page for each thing, with all of its rules, for when you look something up.

To see whole projects with their databases, open [the PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) and [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md).
