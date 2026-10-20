# The tool

The `SqlSource.Tool` package adds the `sqlsource` command to a project.  Its `describe` command connects to your database, asks it what the parameters and columns of each of your queries are, and writes the answers into [sidecar files](#sidecar-files) beside your `.sql` files.  You commit the sidecars with the project, so that the build never needs a database.

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
```

The generator writes a constant or a token method for every query, with the tool or without it.  The tool is for the queries that must be described, those whose [output](settings.md#setting-output) is `models` or `codegen`, the default: the build checks each of them against its file's sidecar, and reports one that has no current entry there.  A project whose queries all have the output `sql` needs no tool.

The generator builds the [models](generator.md#models) of each described query from the sidecars that the tool writes.

> **Coming in a later release.**  The generator will build query methods from the sidecars that the tool writes.  See [what works today](README.md#what-works-today).

This page has two parts: [the workflow](#install-the-tool), from installing the tool to checking the sidecars in CI, and [the command reference](#command-reference).

<a id="install-the-tool"></a>

## Install the tool

Install it as a local tool of your repository, from the folder at its root.  Until the first release the package is a preview, so the command includes `--prerelease`:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

The first command creates the tool manifest, a `dotnet-tools.json` file, which records the tool and its version.  Commit that file.  Anyone who clones the repository, and a build server, then gets the same version with `dotnet tool restore`.  To run the tool, give it a connection to your database: see [connections](#connections).

Keep the tool at the version of the `SqlSource` package your projects use: the two are released together under one version number.  When you update the package, update the tool with `dotnet tool update SqlSource.Tool --prerelease`.  A package and a tool of different versions can stop the tool from reading your project ([SQLSRC206](diagnostics.md#sqlsrc206)).  What else the tool needs is under [requirements](#requirements).

<a id="which-queries"></a>

## Which queries are described

```sql postgres
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: ResetAuditLog
-- output: sql
TRUNCATE TABLE audit_log;
```

`describe` finds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it runs on, and describes each query whose [output](settings.md#setting-output) is `models` or `codegen`.  `codegen` is the default, so `GetUser` above is described.  `ResetAuditLog` asks for its SQL alone, and the tool leaves it out: it needs no database and no connection.

A query is described through its [dialect](concepts.md#dialect).  The tool can ask PostgreSQL today, which is the dialect `postgres`; the [database support table](dialects.md#database-support) lists every dialect.  A query that must be described and whose dialect cannot be is reported by the tool, and by the build:

- A file of a dialect the tool does not describe, every row marked No in [database support](dialects.md#database-support), such as `ansi`, `mysql` or `sqlite`, is [SQLSRC209](diagnostics.md#sqlsrc209).  The default dialect is `ansi`, so a project that sets neither the dialect nor the output gets it for every file.
- A file of `mssql` passes that check, since SQL Server queries will be described, and each of its queries that must be described is [SQLSRC216](diagnostics.md#sqlsrc216) until then.  The build reports such a file as [SQLSRC243](diagnostics.md#sqlsrc243).  See [SQL Server](sqlserver.md).

Either way, set the output of those queries to `sql`, as [the output setting](settings.md#setting-output) says; for SQL Server files too, though SQLSRC209's message also names `mssql`.  For SQLSRC209 you can instead set the dialect, when the files are PostgreSQL.

The tool reads `[SqlSourceGenerate]` from your C# source without compiling it, which limits how `Path` and `Output` can be written there: see [the generator's known issues](generator.md#known-issues).

<a id="connections"></a>

## Connections

```console
$ export SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
$ dotnet sqlsource describe --connection reports="Host=localhost;Database=reports;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
reports (postgres): 2 described, 0 skipped, 0 failed
```

Each query that is described belongs to a [logical database](concepts.md#logical-database): the name that the [database setting](settings.md#setting-database) gives it, such as `billing`, or the default that the setting's section gives.  The tool connects to each logical database through the connection you give it under that name.

A connection is given in one of three ways.  The first that gives one wins:

| Order | Source | For the database `billing` |
|----|----|----|
| 1 | `--connection <name>=<connection string>` on the command line | `--connection billing="Host=localhost;Database=billing"` |
| 2 | The environment variable of the name | `SQLSOURCE_CONNECTION_BILLING` |
| 3 | `SQLSOURCE_CONNECTION` | Used only when the run has exactly one database |

- **The variable of a name** is `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`: `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  Two names that give one variable, such as `app-v2` and `app_v2`, are [SQLSRC214](diagnostics.md#sqlsrc214): give both on the command line, or rename one.
- **`SQLSOURCE_CONNECTION`** is for a project with one database.  With more than one database in the run it is used for none of them, though a database that is in the run only through queries with errors of their own does not count ([SQLSRC215](diagnostics.md#sqlsrc215)): set the variable of each name instead.
- **A variable that is empty or holds only white space** is not set.
- **A connection never goes in the project file**, since it holds credentials.
- **Put the value of `--connection` in quotes.**  A shell splits it at a space.  On a command line that has `--connection`, any other argument that holds `=` or `;` is refused, since it may be the rest of a connection string that the shell split.  If it is a project or `.sql` path that holds `=` or `;`, run the command from that file's folder and give the name alone, or give the connection in its variable instead.
- **A database that needs nothing described needs no connection.**  When every query of a database has a current sidecar entry, the tool asks the database nothing, and without a connection its summary line says `no connection`.  `--force` and `--check` describe every query, so every database of the run needs one then.
- **A database with a query to describe and no connection** is [SQLSRC213](diagnostics.md#sqlsrc213).  Its `help:` line names the option and the variable to set, and lists any `--connection` names that matched no database of the run but are close to one, so that a misspelt name is seen.  A name that is far from every database is counted by the position of its value on the command line and never printed, since it may be a connection string typed without its name.

The tool keeps connection strings to itself as far as it can.  In what it prints or writes to a [log](#logs), it writes `***` in the place of the whole value of a connection wherever it finds it, and of each password of the connection string where it stands apart from other letters, digits and underscores.  It refuses to write a connection's value into a sidecar.  When a database cannot be opened, the error says what went wrong in the tool's own words and names neither the host nor the user.  A short password or a changed value, or a connection string typed in the wrong place on the command line, can still appear: see [known issues](#known-issues).  The tool also takes the connection variables out of the environment of the `dotnet msbuild` it runs to read your projects.  The connection string itself is the database's own: for PostgreSQL, see [connecting](postgresql.md#connecting).

<a id="sidecar-files"></a>

## Sidecar files

A sidecar is the tool's record of what the database said about each query's parameters and columns.  It is a JSON file named after the `.sql` file, beside it: `Users.sql.json` for `Users.sql`.

```text
Queries/
    Users.sql
    Users.sql.json      written by sqlsource describe
```

**Commit the sidecars with the project.**  The build reads them in place of a database, so that every developer and every build server builds without a connection.  A copy of the project without them reports [SQLSRC241](diagnostics.md#sqlsrc241) for each query that must be described.  Only the person who changes a query, or the schema, needs a database to describe it again.  By default the build reads a sidecar and copies it nowhere: like your `.sql` files, it is not in the build output or in what `dotnet publish` writes, in a web project too.  A project that copies its sidecars itself, with `CopyToOutputDirectory` or `CopyToPublishDirectory` on their items, still gets the copy; in a web project that setting goes in `Directory.Build.targets`.

Rider shows each sidecar under its `.sql` file, with nothing to set.  To show each sidecar under its `.sql` file in Visual Studio Code, which nests files by a setting of its own, add this to your settings:

```json
{
    "explorer.fileNesting.enabled": true,
    "explorer.fileNesting.patterns": {
        "*.sql": "${capture}.sql.json"
    }
}
```

**Never edit a sidecar by hand.**  The tool writes it whole.  To change what a sidecar says, change the query or the schema and run `describe` again.  A sidecar with a merge conflict is no exception: run `describe` after the merge, and it writes the file again.

To keep the sidecars current:

| When | Run |
|----|----|
| You add, change, rename or remove a query, a `-- param:` marker or a token's default | `dotnet sqlsource describe` |
| The schema of the database changes | `dotnet sqlsource describe --force`: see [after a schema change](#after-a-schema-change) |
| You update the tool | `dotnet sqlsource describe`, which describes every query again |
| On a build server, to catch a sidecar that someone forgot | `dotnet sqlsource describe --check`: see [check in CI](#check-in-ci) |

The build tells you when a sidecar is not current.  A query without an entry is [SQLSRC241](diagnostics.md#sqlsrc241), and one that changed since its entry was written is [SQLSRC244](diagnostics.md#sqlsrc244).  An entry that names no query that must be described is the warning [SQLSRC245](diagnostics.md#sqlsrc245), and a sidecar that another version of the tool wrote the warning [SQLSRC240](diagnostics.md#sqlsrc240).  Each of the first three goes away when you run `describe` and commit what it writes.  SQLSRC240 goes away once the tool and the `SqlSource` package have one version and `describe` has run with that tool.  A sidecar with a merge conflict in it is [SQLSRC237](diagnostics.md#sqlsrc237).

The format of a sidecar is described by its JSON Schema, [`sidecar-v1.schema.json`](../../schemas/sidecar-v1.schema.json), for the curious.  Nothing in this manual depends on it.

<a id="describing-again"></a>

## Describing again

`describe` describes only what changed.  A query whose sidecar entry is current is skipped, and is not sent to the database.  An entry stops being current when any of these change:

- The query's SQL.  A change to a comment alone does not count.
- A `-- param:` marker of the query, or the default of one of its tokens.
- The query's dialect, or its [logical database](#connections).
- The version of the tool: a sidecar written by another version is described again whole.  Until it is, the build warns of it ([SQLSRC240](diagnostics.md#sqlsrc240)).

The tool cannot see a change to the schema, so a query whose SQL is as it was is skipped even when its table changed.  That is what `--force` is for.

A sidecar is written whole or not at all.  When a query of a file fails, or a filter leaves one out whose entry is not current, the file's sidecar is left as it was, and [SQLSRC217](diagnostics.md#sqlsrc217) says so if the run described any of its queries.  Fix the query, or describe the whole file, and run `describe` again.

When no query of a file needs an entry any more, because each was removed or now has the output `sql`, the tool deletes the file's sidecar.  It does so only in a run with no `--project`, `--database` or `.sql` path, and only when the run reported no error, so that a run over part of the project never removes what another part needs.

A run that is stopped while it writes a sidecar leaves the old file or the new one, never part of either.  It can leave a temporary file beside it, named like `Users.sql.json.<id>.tmp`; the next `describe` of the whole project removes it once it is a minute old.

<a id="after-a-schema-change"></a>

## After a schema change

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
```

`--force` describes every query of the run again, current or not.  Run it after a migration, or any other change to the database that a query reads: a column that became nullable, a type that changed, a table that was renamed.  Then commit the sidecars it changed.  A query that the change broke, such as one that reads a column you dropped, is reported with what the server said ([SQLSRC225](diagnostics.md#sqlsrc225)).

To describe again only the queries of one database, add `--database billing`.

<a id="check-in-ci"></a>

## Check in CI

```console
$ dotnet tool restore
$ dotnet restore
$ SQLSOURCE_CONNECTION_BILLING="$BILLING_CONNECTION" dotnet sqlsource describe --check
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': the query has changed since it was described
    help: run 'dotnet sqlsource describe' and commit the sidecars
    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc219
billing (postgres): 15 same, 1 differ, 0 failed
```

`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars in the repository, and changes no file.  A sidecar that is missing or out of date is [SQLSRC219](diagnostics.md#sqlsrc219), which names the query and what differs, and its `help:` line names the command that fixes it.  The build already reports an entry that is missing or older than its query; `--check` also finds one that a change to the schema made wrong, which the build cannot see.  For a whole GitHub Actions job, see [the recipe](recipes.md#check-sidecars-in-ci).

- **It needs a database with your schema**, such as one the job creates and migrates, and a connection for every database of the run.
- **It compares what changes generated code**: the queries, their parameters and columns, their types and whether they can be null.  It does not compare the versions of the tool or of the server, so a sidecar that a developer wrote passes against a server of another patch release.
- **Its exit code tells the two failures apart.**  `2` means a sidecar is out of date and nothing else went wrong: someone forgot to describe.  `1` means the check could not be made, such as a database that could not be opened.  See [exit codes](#exit-codes).
- **With `--database`, `--project` or a `.sql` path**, it compares only the queries that `describe` with the same filters would describe.  With `--database` it can miss a leftover entry that such a `describe` would drop: see [known issues](#known-issues).  A check with no filter has no such gap.

<a id="summary"></a>

## The summary

Every run ends with one line for each database it ran on:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

- **described**: the queries the database was asked about in this run.
- **skipped**: the queries whose sidecar entry was current.
- **failed**: the queries that could not be described.  Each has an error above the summary.
- **no connection**: the database has no connection and needed none, since all its queries were current.

Under `--check` the counts are `same`, `differ` and `failed`.  A name given to `--database` that no query of the run has gets a line of zeros, so that a misspelt name is seen.  The line names it when it is close to a database of the run, in the spelling you gave, and otherwise says `--database at position <n>`, with the position of the value on the command line, since a value typed after `--database` by mistake may be a secret.  A run on a solution in which no project uses SqlSource prints `sqlsource: no project of '<path>' uses SqlSource` and no summary.

<a id="command-reference"></a>

## Command reference

```text
dotnet sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                          [--connection <name>=<connection string>]...
                          [--force | --check] [--verbose] [--log <path>]
```

`dotnet sqlsource --help` and `dotnet sqlsource describe --help` print the options, and `dotnet sqlsource --version` the version of the tool.

<a id="arguments"></a>

### Arguments

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx
$ dotnet sqlsource describe src/App/Queries/Users.sql src/App/Queries/Orders.sql
```

- **The project or solution to run on**: a `.sln`, `.slnx` or `.csproj` file, or a folder that holds exactly one of them.  Without it, the current folder.  Only one may be given.  The tool looks in that folder alone, not in the folders below it: none is [SQLSRC201](diagnostics.md#sqlsrc201) and more than one [SQLSRC202](diagnostics.md#sqlsrc202).
- **In a solution**, the tool runs on every C# project that uses the SqlSource package, and leaves the others out without a word.  Every C# project of the solution must have been restored, one with no queries too ([SQLSRC220](diagnostics.md#sqlsrc220)).
- **A path that ends in `.sql`** restricts the run to that file.  Several may be given.  It must be a file that a type of the run claims ([SQLSRC212](diagnostics.md#sqlsrc212)); the rest of the run goes on.

A relative path is taken from the current folder.

<a id="options"></a>

### Options

| Option | What it does |
|----|----|
| `--project <path>` | Runs on this project of the solution and not on the others.  May be given several times.  A path that is not a C# project of the solution is [SQLSRC207](diagnostics.md#sqlsrc207) |
| `--database <name>` | Describes only the queries of this [logical database](#connections).  May be given several times.  Every other sidecar is left as it was |
| `--connection <name>=<connection string>` | The connection of a database: see [connections](#connections).  May be given several times, once for each name |
| `--force` | Describes every query of the run again, current or not: see [after a schema change](#after-a-schema-change) |
| `--check` | Describes every query of the run, compares with the sidecars, writes nothing: see [check in CI](#check-in-ci).  With `--force` too, it is `--check` |
| `--verbose` | Prints what the run decides as it goes: see [`--verbose`](#verbose) |
| `--log <path>` | Writes a record of the run to a file; a path that holds `=` or `;` is refused: see [logs](#logs) |

A run with `--project`, `--database` or a `.sql` path writes only the sidecars of the files whose queries it selected, and deletes none.

<a id="environment-variables"></a>

### Environment variables

| Variable | What it does |
|----|----|
| `SQLSOURCE_CONNECTION_<NAME>` | The connection of the database `<name>`: see [connections](#connections) |
| `SQLSOURCE_CONNECTION` | The connection of a run with exactly one database |
| `SQLSOURCE_LOG` | The path of the log, when `--log` is not given |
| `SQLSOURCE_DEBUG` | Any value that is not empty adds the stack trace to [SQLSRC200](diagnostics.md#sqlsrc200), for a bug report |

A connection or log variable that is empty or holds only white space is not set.

<a id="exit-codes"></a>

### Exit codes

| Code | When |
|----|----|
| `0` | The run reported no error |
| `1` | An error was reported other than a sidecar out of date; the command line is wrong; the log could not be written; or the run was stopped with Ctrl+C, `SIGTERM` or `SIGHUP` |
| `2` | `--check` found a sidecar out of date, and nothing else went wrong |

A run that is stopped writes no further sidecar.  A second Ctrl+C ends the process at once.

<a id="verbose"></a>

### `--verbose`

```console
$ dotnet sqlsource describe --verbose
sqlsource: unit '/work/App/App.csproj', a project, found in the working directory
sqlsource: project '/work/App/App.csproj': net10.0, 1 .sql files
sqlsource: file '/work/App/Queries/Users.sql': postgres, ready, claimed by /work/App/Queries/UserQueries.cs(3)
sqlsource: database 'postgres' (postgres): connection from SQLSOURCE_CONNECTION_POSTGRES
sqlsource: database 'postgres': server 18.6
sqlsource: query 'GetUser' in '/work/App/Queries/Users.sql': described in 25 ms
sqlsource: sidecar '/work/App/Queries/Users.sql.json': written
postgres (postgres): 1 described, 0 skipped, 0 failed
```

`--verbose` prints a line for each project, file, database, query and sidecar, saying what the run decided for it, each starting with `sqlsource:`.  Use it to see why a query was skipped or a project left out.  Without it the tool prints its errors, the summary, and the line that ends a failed run or says that no project uses SqlSource.

<a id="logs"></a>

### Logs

```console
$ dotnet sqlsource describe --log sqlsource.log
```

`--log <path>`, or the variable `SQLSOURCE_LOG`, writes a record of the run to a file, one JSON object on each line.  It is what to attach to an issue when the tool does something you did not expect.

- **The file is overwritten by each run.**  The tool refuses a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, so that a slip on the command line never empties a file of your project ([SQLSRC218](diagnostics.md#sqlsrc218)).  It refuses a `--log` path that holds `=` or `;` too, which is more likely a connection string typed in the wrong place than a path; a path from `SQLSOURCE_LOG` is not held to that.
- **A run that reports an error and writes no log** ends with the line `sqlsource: run again with --log <file> for a record of what the run did`.  A `--check` whose only errors are sidecars out of date does not print it.

**Read a log before you share it.**  The tool writes each connection value as `***` wherever it finds it whole: the value of each `--connection` on the command line, and the value of each connection, from the command line or a variable, wherever the database driver repeats it in a message.  It writes each password of a connection string as `***` too, where it stands apart from other letters, digits and underscores and is at least four characters long.  A value that was changed, such as the value with a space trimmed, can appear with its passwords hidden and the rest of it as written.  In the record of your command line, every value of `--database` and `--connection` is written `***`.  A value typed where a path goes (the path of `describe`, a `.sql` path or a `--project` value) is written `***` too when it holds `=` or `;`, as a connection string does.  A value typed there or after `--log` with neither, such as a password typed alone, can appear as written, and so can a password shorter than four characters, one that is the name of its logical database or of its dialect, such as `postgres`, and one that the driver reads from the environment, such as `PGPASSWORD`, or from a password file ([known issues](#known-issues)): search the log for your password before you share it.  The log also holds:

- the version of the tool, of .NET and of the operating system, and the rest of your command line;
- the paths of your projects and files, and the names of your queries and databases;
- the SQL of your queries as the tool sent it to the database, and what the server answered, its errors and warnings included;
- for a database that could not be opened, what went wrong, in the tool's own words and with the server's SQLSTATE, which names neither its host nor its user;
- each error, and how the run ended.

<a id="requirements"></a>

### Requirements

- **The .NET SDK**, and not only a runtime.  The tool asks MSBuild about your projects through `dotnet msbuild`.  It runs on .NET 8 or later.
- **Restored projects.**  The tool does not restore: run `dotnet restore` first ([SQLSRC220](diagnostics.md#sqlsrc220)).
- **The same version as the `SqlSource` package** of your projects ([SQLSRC206](diagnostics.md#sqlsrc206)).
- **A database the tool can reach**, of a version it supports, for each logical database with a query to describe.  See [PostgreSQL](postgresql.md#connecting).

<a id="known-issues"></a>

## Known issues

- **A project path or a `.sql` path through a symbolic link is not found.**  When the current folder is reached through a symbolic link and you give a full path that goes through the link, the tool compares it with the real path and finds no match: `--project` reports [SQLSRC207](diagnostics.md#sqlsrc207), and a `.sql` path [SQLSRC212](diagnostics.md#sqlsrc212), though the file is there.  The message shows both spellings.  Give the path relative to the current folder, or the full real path.
- **A project with several target frameworks is read for its first framework alone.**  A `.sql` file or a C# file that the project includes only for another framework, and a `[SqlSourceGenerate]` under an `#if` that only another framework defines, are not seen, and their queries are not described: the build for that framework reports [SQLSRC241](diagnostics.md#sqlsrc241) for each that must be.  A project that references SqlSource for a later framework only is left out of a solution's run without a word, and is [SQLSRC204](diagnostics.md#sqlsrc204) when you name it; its build reports SQLSRC241 in the same way.  Include the `.sql` files and the attributed types for every framework, or for the first one listed.
- **A `.sql` file that one of your own build targets adds is not described.**  See [the known issues of settings](settings.md#known-issues) for what goes wrong and the one line that fixes it.
- **A `.sql` file that two projects list** is described with the settings of the first project that claims it.  See [the known issues of `.sql` files](sql-files.md#known-issues).
- **A connection string or a password can appear in the output or a log when it is not there whole.**  The tool writes a connection value as `***` where it finds the whole value, and each password of it where it stands apart from other letters, digits and underscores.  A message of the database driver that quotes the value changed, such as with a space trimmed, is printed and logged with its passwords hidden and the rest as it is, and in rare cases a log holds a whole value split over two of its fields.  A password shorter than four characters is not hidden, nor is one that is the name of its logical database or of its dialect, such as `postgres`, words that the tool prints anyway, nor one that the driver reads from the environment, such as `PGPASSWORD`, or from a password file and not from the connection string.  A connection string typed where a path or a `--project` goes, or after `--database` or `--connection` without its `name=`, is not printed.  In the log, every value of `--database` and `--connection` is written `***`, and so is a path, a `--project` value included, that holds `=` or `;`; a `--log` path that holds either is refused.  A secret with neither, typed alone where a path, a `--project` value or a `--log` path goes, is printed as a path and logged as typed.  Give each connection as `--connection <name>="..."` or in its variable, and search what you share, a log above all, for your password first.
- **`--check --database` can pass a sidecar that `describe --database` would still rewrite.**  When a sidecar holds an entry for a query that was removed from its file, or whose output is now `sql`, and that entry is not of a database the filter names (it is another database's, or it names no database), `--check --database billing` does not report it, while `describe --database billing` rewrites the file to drop it.  The build warns of such an entry ([SQLSRC245](diagnostics.md#sqlsrc245)).  Run `--check` without `--database` in CI, which reports the entry; the next `describe` that writes the file drops it.
