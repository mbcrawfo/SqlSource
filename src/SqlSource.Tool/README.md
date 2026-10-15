# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.

`describe` finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It asks the database of each such query for its parameters and columns and writes what it learns into a sidecar, a `.sql.json` file beside the `.sql` file, which is committed with it.

This version describes the queries of the `postgres` dialect, as the section on PostgreSQL below says.  A query of `mssql` that must be described is [`SQLSRC216`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216).

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing"
$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
$ dotnet sqlsource describe src/App/Queries/Users.sql
$ dotnet sqlsource describe --database billing --force
$ dotnet sqlsource describe --check
$ dotnet sqlsource describe --log sqlsource.log
```

A query belongs to a logical database: the one its `-- database:` marker names, or the `SqlSourceDatabase` metadata of its file, or the property of that name, or else the name of its dialect.  A connection is given for that name, on the command line or in the environment, and never in the project file, since it holds credentials:

| Source | For the database `billing` |
|----|----|
| `--connection <name>=<connection string>` | `--connection billing="Host=localhost;Database=billing"` |
| The variable of the name | `SQLSOURCE_CONNECTION_BILLING`: the name in upper case, with every character that is not a letter or a digit written as `_` |
| `SQLSOURCE_CONNECTION` | Used when the run has exactly one database that can be described |

The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  The tool prints no connection string that it was given as one, takes the variables out of the environment of the `dotnet msbuild` it runs to read your projects, and writes no connection string into a sidecar: a description that holds the value ends the run with [`SQLSRC200`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200).  Put the value of `--connection` in quotes: a shell splits it at a space.  On a command line with `--connection`, a path that holds `=` or `;` is refused, since it may be a part of a connection string that the shell split; give the connection in its environment variable instead, or run the command from the path's own directory.

A query whose entry in the sidecar is current, because neither its SQL nor its database changed since this version of the tool described it, is skipped, and a database whose queries are all current needs no connection.  `--force` describes every query of the run again: it is the command to run after a change to the schema, which the tool cannot see.  `--database`, which may be given several times, restricts the run to the queries of the databases it names, and a path that ends in `.sql` to that file; a restricted run leaves every other sidecar as it was.  A sidecar is written only when every query of its file that needs an entry has one.  A run with no filter, in which nothing was reported as an error, deletes the sidecar of a file none of whose queries needs an entry any more.  Beside that the tool removes one thing from a project: a temporary file, `<name>.sql.json.<id>.tmp`, that a run left beside a sidecar when it was killed while it wrote it.  A later run removes it once it is a minute old, for the sidecars it writes or, with no filter, in every folder that holds a `.sql` file of the run; `--check` removes none.  A run ends with one line for each database it ran on, and one with zeros for each name of `--database` that no query has:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars that are there, and changes no file.  A sidecar that is out of date is [`SQLSRC219`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc219), which says what differs and where.  The `help:` line under it names the command that mends it: `describe --force` for a difference that the database reports, since plain `describe` skips a query whose SQL is as it was; an update of the `SqlSource.Tool` package for a sidecar of a higher format version; `describe` for every other.  It compares what changes generated code and not the versions of the tool or of the server, so a sidecar that a developer wrote passes on a server of another patch.  Every database of the run needs a connection.  With `--database`, `--project` or a `.sql` path it compares what `describe` with the same filters would change, and nothing else, with one exception: an entry for a query that is gone, in a sidecar of a database that `--database` does not name, is not reported, though `describe --database` would drop it when it rewrites the file.  Its summary counts each query as the same, as one that differs, or as one that could not be described:

```console
billing (postgres): 15 same, 1 differ, 0 failed
```

The exit code is `0`; `1` when an error other than a sidecar out of date was reported, when the command line is wrong, or when the run was cancelled, by Ctrl+C, `SIGTERM` or `SIGHUP`; and `2` when `--check` found a sidecar out of date and nothing else failed.

## PostgreSQL

The connection string is Npgsql's, `Host=localhost;Database=billing;Username=app;Password=...`, with the keywords of [its documentation](https://www.npgsql.org/doc/connection-string-parameters.html).  The tool supports PostgreSQL 14 and later, and is tested on 14 and 18.

The tool needs a session of its own: it prepares a statement on the server and asks about it in later steps, which only works when every step reaches the same backend.  Connect directly to the server, not through a pooler in transaction mode such as PgBouncer's, which hands each transaction to a backend of its own, and without `Multiplexing=true`, which shares connections between commands.  Hosted providers that pool connections offer a direct endpoint beside the pooled one.  Otherwise the run reports [`SQLSRC236`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc236).  The code the tool generates runs through whatever connection your application uses.

The server prepares only `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `VALUES` and `TABLE`, so a `CALL` or a statement of data definition in a `.sql` file is [`SQLSRC233`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc233) unless the query has `-- output: sql`, which keeps the SQL as it is and generates a method that runs it without describing it.

The tool opens one session for each database and describes its queries one at a time, each through a prepared statement of its own.  It sends the query's SQL with `$1`, `$2` in the places of its parameters as a `PREPARE`, and the server gives each parameter its type.  It takes the columns, with their names and their types with length, precision and scale, from a description of the prepared statement, and asks the catalog where each column comes from: its table and column, whether that is an identity or a computed column, and whether it is `NOT NULL`.  It checks that Npgsql, given the SQL as you wrote it with its `@name` parameters, reads the same columns, since that is the SQL that generated code is to send: [`SQLSRC231`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc231) says what differed, and [`SQLSRC232`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc232) names a type that Npgsql cannot bind.  For a query that returns rows and has an outer join or a grouping set, or `MERGE`, `OLD` or `NEW`, it asks the server for the plan of the prepared statement and reads from it which tables a join fills with `NULL`.  It finds those by their words, `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE`, `GROUPING`, `OLD`, `NEW` and `MERGE`, so `left(name, 3)` asks for a plan too.

A parameter's type is the one the server infers from its uses.  A `-- param:` marker, `-- param: @name type`, gives it a type in the database's own words, which the server resolves and checks against every use of the parameter, and the sidecar records as `declared`; the marker is in the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#parameters).  A query whose use of a parameter fits a value of any type, `SELECT @p IS NULL`, is [`SQLSRC226`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc226): cast the parameter where it is used, `@p::int`, or declare its type.  The server takes a parameter's type from its first use, so the optional filter `(@name IS NULL OR name = @name)` is [`SQLSRC227`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc227): declare the type, `-- param: @name text`, or turn the condition round, `(name = @name OR @name IS NULL)`.

A column's `name` is as the server returned it, so an alias with an override suffix, `AS "total!"` or `AS "total?"`, keeps its `!` or `?` in the sidecar.  The tool records the suffix and does not apply it: the column's `nullable` is what the tool found.

A column's `nullable` is what the tool could prove, and its `nullableSource` says how:

| `nullable` | `nullableSource` | When |
|----|----|----|
| `false` | `catalog` | The column is `NOT NULL` in its table, and the query has none of the words above or its plan shows that no outer join fills the table with `NULL` |
| `true` | `catalog` | The column of a table can hold `NULL`: it is not `NOT NULL`, its `NOT NULL` is `NOT VALID`, or the table has or had inheritance children, has a foreign table among its partitions, or has a `DO INSTEAD` rule on `INSERT`, `UPDATE` or `DELETE`, each of which can give a `NULL` that the column's own `NOT NULL` does not show |
| `true` | `outer-join` | An outer join of the plan fills the column's table or view with `NULL` |
| `true` | `heuristic` | A column of a table in a query with one of the words above, for which the plan gave no answer |
| `null` | `view` | A column of a view, a materialized view or a foreign table, whose `NOT NULL` PostgreSQL does not record or does not enforce |
| `null` | `no-origin` | A column that comes from no table: an expression, a column of a `UNION`, a system column |

So `null` is unknown, and the tool cannot prove a column `NOT NULL` when it is of a view or a foreign table.  Nor can it in a query with one of the words above when the column comes through a grouping set, `ROLLUP`, `CUBE` or `GROUPING SETS`, or through a subquery or a `WITH` query that the planner keeps apart, which are `true` with `heuristic`, as are [a few other shapes](https://github.com/mbcrawfo/SqlSource/blob/main/docs/tech-debt/TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md).  An entry's `plan` is `not-needed` for a query with rows and none of the words, `walked` when the tool used the plan, and `unavailable` when it had no plan it could use: the server would not plan the query, or the plan holds a grouping set or a shape the tool does not read, a `UNION ALL` for one.  Every column then keeps the heuristic's answer, `true` for a column of a table, and the `postgres.walk` event of the log says why.

The tool runs no query of yours.  It prepares each, describes the prepared statement without executing it, and asks for its plan with `EXPLAIN` and without `ANALYZE`, in a transaction that it rolls back.  It leaves the session as it found it: it frees each statement it prepared, and the settings it changes for a plan are `SET LOCAL` and end with that transaction.  Planning a query with one of the words above does run each function marked `IMMUTABLE` that the query calls with constant arguments, so such a function with an effect outside the transaction, a sequence it advances for one, has that effect ([TD-0047](https://github.com/mbcrawfo/SqlSource/blob/main/docs/tech-debt/TD-0047-explain-evaluates-immutable-functions.md)).

## Options and requirements

`--verbose` says what the run decides as it goes, a line for each project, file, database, query and sidecar, each starting with `sqlsource:`.  Without it the tool prints its errors and the lines of its summary, and two other lines: the one that says no project of the unit uses SqlSource, and the one that ends a failed run, below.

`--log <path>` writes what the run did to a file, one JSON object on a line: what ran and on what, each project, file, database and query with what the run decided for it, each error, and how the run ended.  `SQLSOURCE_LOG` gives the path when the option does not.  The file is overwritten by each run, and the tool refuses a name that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, so that a slip never empties a file of your project.  The console stays as it is without the option.  A log holds no connection string: the value of each `--connection` is written as `***` with its name, and so is a value that a database's driver repeats in a message.  With a log, the PostgreSQL describer writes the driver's own messages too, as `postgres.driver` events: the SQL it sends and its transactions, and of everything else only its warnings and errors, and never the exception that a failed connection throws, which names the host (that text is in the `SQLSRC224` error and its `diagnostic` event).  The driver names a command in its own words, so the description of a prepared statement, which executes nothing, is `Executing command: EXECUTE sqlsource_1` there.  A log does hold the paths of your files, the names of your queries and databases, what the server or the driver said of a database that could not be opened, which can name its host and its user, and the rest of your command line as you typed it, so read it before you attach it to an issue.  A run that reported an error other than a sidecar out of date and wrote no log ends with one line that says so: `sqlsource: run again with --log <file> for a record of what the run did`.

`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on.

The tool reads `[SqlSourceGenerate]` from the source without compiling it, so `Path` and `Output` must be written as literals: `Path = "Queries"`, `Output = GeneratorOutput.Models`.

The tool asks MSBuild about each project, so it needs the .NET SDK, and a project must have been restored: the tool does not restore, and reports a project that was not.  The SqlSource package of a project and the tool should be of one version.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
