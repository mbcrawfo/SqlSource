# The sqlsource command

This page says how to install the `sqlsource` tool, and lists its commands, options, environment variables and exit codes.  See also [what `describe` does](describing.md), [how a build runs it](msbuild.md#running-in-the-build), [the bundles of `diagnose` and `replay`](bundles.md) and [how the tool reports errors](errors.md#the-tools-errors).

The tool's pages have two parts: [the workflow](#install-the-tool), from installing the tool and running it in the build to checking the sidecars in CI and reporting a model that is wrong, and [the command reference](#command-reference).  The tool's `diagnose` command makes a bug-report bundle of one query that you can attach to an issue, and its `replay` command checks a bundle with no database: see [reporting a wrong model](../guides/report-a-bug.md#report-a-bug) and [checking a bundle with `replay`](bundles.md#replay).

<a id="tool"></a>

## The tool

The `SqlSource.Tool` package adds the `sqlsource` command to a project.  Its `describe` command connects to your database, asks it what the parameters and columns of each of your queries are, and writes the answers into [sidecar files](sidecars.md#sidecar-files) beside your `.sql` files.  You commit the sidecars with the project, so that a build needs no database.  On a machine that has a connection, the build [runs `describe` itself](msbuild.md#running-in-the-build) before it compiles.

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
```

The generator writes a constant or a token method for every query whose output is `sql` or `models`, with the tool or without it.  The tool is for the queries that must be described, those whose [output](../settings.md#setting-output) is `models`, `methods` or `repository`, the default: the build checks each of them against its file's sidecar, and reports one that has no current entry there.  A project whose queries all have the output `sql` needs no tool.

The generator builds the [models](../generator.md#models) of each described query from the sidecars that the tool writes, and the [method](../generator.md#query-methods) that runs a query whose output is `methods` or `repository`.

<a id="install-the-tool"></a>

## Install the tool

Install it as a local tool of your repository, from the folder at its root.

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

The first command creates the tool manifest, a `dotnet-tools.json` file, which records the tool and its version.  Commit that file.  Anyone who clones the repository, and a build server, then gets the same version with `dotnet tool restore`.  A build that [runs the tool](msbuild.md#running-in-the-build) starts this version too.  To run the tool, give it a connection to your database: see [connections](connections.md#connections).

Keep the tool at the version of the `SqlSource` package your projects use: the two are released together under one version number.  When you update the package, update the tool with `dotnet tool update SqlSource.Tool`.  A package and a tool of different versions can stop the tool from reading your project ([SQLSRC206](../diagnostics.md#sqlsrc206)).  What else the tool needs is under [requirements](#requirements).

<a id="command-reference"></a>

## Command reference

```text
dotnet sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                          [--connection <name>=<connection string>]...
                          [--env-file <path>] [--force | --check] [--verbose]
                          [--log <path>] [--watch]
dotnet sqlsource diagnose --query <file>:<name> [<path>] [--project <path>]...
                          [--connection <name>=<connection string>]...
                          [--env-file <path>] --out <path> [--no-obfuscate]
                          [--verbose] [--log <path>]
dotnet sqlsource diagnose --scrub <bundle> --out <path>
dotnet sqlsource replay <bundle> [--verbose] [--log <path>]
```

`dotnet sqlsource --help`, and `--help` after a command, such as `dotnet sqlsource diagnose --help`, print the options, and `dotnet sqlsource --version` the version of the tool.  The arguments and options below are those of `describe`; `diagnose` is in [making a bundle with `diagnose`](bundles.md#diagnose), and `replay` in [checking a bundle with `replay`](bundles.md#replay).

<a id="arguments"></a>

### Arguments

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx
$ dotnet sqlsource describe src/App/Queries/Users.sql src/App/Queries/Orders.sql
```

- **The project or solution to run on**: a `.sln`, `.slnx` or `.csproj` file, or a folder that holds exactly one of them.  Without it, the current folder.  Only one may be given.  The tool looks in that folder alone, not in the folders below it: none is [SQLSRC201](../diagnostics.md#sqlsrc201) and more than one [SQLSRC202](../diagnostics.md#sqlsrc202).
- **In a solution**, the tool runs on every C# project that uses the SqlSource package, and leaves the others out without a word.  Every C# project of the solution must have been restored, one with no queries too ([SQLSRC220](../diagnostics.md#sqlsrc220)).
- **A path that ends in `.sql`** restricts the run to that file.  Several may be given.  It must be a file that a type of the run claims ([SQLSRC212](../diagnostics.md#sqlsrc212)); the rest of the run goes on.

A relative path is taken from the current folder.

<a id="options"></a>

### Options

| Option | What it does |
|----|----|
| `--project <path>` | Runs on this project of the solution and not on the others.  May be given several times.  A path that is not a C# project of the solution is [SQLSRC207](../diagnostics.md#sqlsrc207) |
| `--database <name>` | Describes only the queries of this [logical database](connections.md#connections).  May be given several times.  Every other sidecar is left as it was |
| `--connection <name>=<connection string>` | The connection of a database: see [connections](connections.md#connections).  May be given several times, once for each name |
| `--env-file <path>` | Reads the connections from this file in place of the `.env` in the folder of the project or solution, or from no file when it is `none`; a path that holds `=` or `;` is refused: see [a `.env` file](connections.md#env-file) |
| `--force` | Describes every query of the run again, current or not: see [after a schema change](describing.md#after-a-schema-change) |
| `--check` | Describes every query of the run, compares with the sidecars, writes nothing: see [check in CI](describing.md#check-in-ci).  With `--force` too, it is `--check` |
| `--verbose` | Prints what the run decides as it goes: see [`--verbose`](#verbose) |
| `--log <path>` | Writes a record of the run to a file; a path that holds `=` or `;` is refused: see [logs](#logs) |
| `--manifest <path>` | For the build's use: describes the project that the build's record of it names, without reading the project again.  It takes no project or solution, `.sql` path, `--project`, `--database` or `--watch`.  See [running in the build](msbuild.md#running-in-the-build) |
| `--online <auto\|true>` | For the build's use, with `--manifest`: takes a `--manifest`, `--log` or `--env-file` path that holds `=` or `;`, as the build's own may, and prints and logs it in full, writes each error on one line, writes no log for `SQLSOURCE_LOG`, and under `auto` says it is offline, and exits with `0`, when no database with a query to describe has a connection.  See [running in the build](msbuild.md#running-in-the-build) |
| `--watch` | Describes once, then again each time a file of the run changes, until stopped: see [describe on save](describing.md#describe-on-save).  Not with `--check` or `--manifest` |

A run with `--project`, `--database` or a `.sql` path writes only the sidecars of the files whose queries it selected, and deletes none.

<a id="environment-variables"></a>

### Environment variables

| Variable | What it does |
|----|----|
| `SQLSOURCE_CONNECTION_<NAME>` | The connection of the database `<name>`: see [connections](connections.md#connections) |
| `SQLSOURCE_CONNECTION` | The connection of a run with exactly one database |
| `SQLSOURCE_LOG` | The path of the log, when `--log` is not given.  In a build, any value writes a log of each project's run: see [running in the build](msbuild.md#running-in-the-build) |
| `SQLSOURCE_DEBUG` | Any value that is not empty adds the stack trace to [SQLSRC200](../diagnostics.md#sqlsrc200), for a bug report |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `true` or `1`, in any case, makes `--watch` look at the folders every second instead of asking the system: see [describe on save](describing.md#describe-on-save) |

A connection or log variable that is empty or holds only white space is not set.  Each connection variable can also be set in a [`.env` file](connections.md#env-file), and the environment wins over it; `SQLSOURCE_LOG`, `SQLSOURCE_DEBUG` and `DOTNET_USE_POLLING_FILE_WATCHER` are read from the environment alone.

<a id="exit-codes"></a>

### Exit codes

| Code | When |
|----|----|
| `0` | The run reported no error; a `--watch` was stopped with Ctrl+C, `SIGTERM` or `SIGHUP`, whatever its passes reported; or `diagnose` wrote its bundle, of a query that was refused too |
| `1` | An error was reported other than a sidecar out of date; the command line is wrong; the log could not be written; `diagnose` wrote no bundle, or wrote the bundle and not its mapping; or a run without `--watch` was stopped with Ctrl+C, `SIGTERM` or `SIGHUP` |
| `2` | `--check` found a sidecar out of date, and nothing else went wrong; or `replay` gave another answer than the bundle's |

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

`--verbose` prints a line for each project, file, database, query and sidecar, saying what the run decided for it, each starting with `sqlsource:`.  A database's line names where its connection came from, and never the connection: `--connection`, a variable's name, or, for a connection from a [`.env` file](connections.md#env-file), the variable and the file, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`.  Use it to see why a query was skipped or a project left out.  Under `--watch` it says the same of each pass, and nothing more.  Without it the tool prints its errors, the summary, and the line that ends a failed run or says that no project uses SqlSource.

<a id="logs"></a>

### Logs

```console
$ dotnet sqlsource describe --log sqlsource.log
```

`--log <path>`, or the variable `SQLSOURCE_LOG`, writes a record of the run to a file, one JSON object on each line.  It is your own record of what the run did, with your names, your SQL and what your server said in it: never attach it to an issue, and send it privately when a maintainer asks for it.  What an issue attaches is a bundle, in which your names and strings are renamed: see [reporting a wrong model](../guides/report-a-bug.md#report-a-bug).

- **The file is overwritten by each run.**  The tool refuses a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, so that a slip on the command line never empties a file of your project ([SQLSRC218](../diagnostics.md#sqlsrc218)).  It refuses a `--log` path that holds `=` or `;` too, which is more likely a connection string typed in the wrong place than a path; a path from `SQLSOURCE_LOG` is not held to that.
- **A `--watch` writes one file from its start until it is stopped**, with every pass in it and where each begins and ends.
- **A run that reports an error and writes no log** ends with the line `sqlsource: run again with --log <file> for a record of what the run did`.  A `--check` whose only errors are sidecars out of date does not print it, nor does a `--watch`, nor `diagnose` or `replay`, nor the run of a build, which is given no `--log`: see [running in the build](msbuild.md#running-in-the-build) for its logs.

**Read a log before you send it.**  The tool writes each connection value as `***` wherever it finds it whole: the value of each `--connection` on the command line, and the value of each connection, from the command line or a variable, wherever the database driver repeats it in a message, as written, with the spaces at its ends trimmed, or as the driver writes it again.  It writes each password of a connection string as `***` too, where it stands apart from other letters, digits and underscores and is at least four characters long, and so the password that the PostgreSQL driver reads from `PGPASSWORD` or a password file when the connection string gives none.  In the record of your command line, every value of `--database` and `--connection` is written `***`.  A value typed where a path goes (the path of `describe` or `diagnose`, a `.sql` path, a `--project` value, or a value of `diagnose`'s `--query` or `--scrub`) is written `***` too when it holds `=` or `;`, as a connection string does.  A value typed there, or after `--log` or `--env-file`, with neither, such as a password typed alone, can appear as written, and so can a password shorter than four characters, and one that is the name of its logical database or of its dialect, such as `postgres` ([known issues](connections.md#limit-short-password)): search the log for your password before you share it.  The log also holds:

- the version of the tool, of .NET and of the operating system, and the rest of your command line;
- the paths of your projects and files, and the names of your queries and databases;
- the SQL of your queries as the tool sent it to the database, and what the server answered, its errors and warnings included;
- for a database that could not be opened, what went wrong, in the tool's own words and with the server's SQLSTATE, or SQL Server's error number, which names neither its host nor its user;
- each error, and how the run ended, with its exit code.  A log that cannot be finished, as when the disk is full, is an error of its own once the run has ended ([SQLSRC218](../diagnostics.md#sqlsrc218)): the tool then exits with `1`, and the log's last line may still hold the exit code the run had before.

<a id="requirements"></a>

### Requirements

- **The .NET SDK**, and not only a runtime.  The tool asks MSBuild about your projects through `dotnet msbuild`.  It runs on .NET 8 or later.
- **Restored projects.**  The tool does not restore: run `dotnet restore` first ([SQLSRC220](../diagnostics.md#sqlsrc220)).
- **The same version as the `SqlSource` package** of your projects ([SQLSRC206](../diagnostics.md#sqlsrc206)).
- **A database the tool can reach**, of a version it supports, for each logical database with a query to describe.  See [PostgreSQL](databases/index.md#supported-versions) and [SQL Server](databases/index.md#supported-versions).

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

<a id="limits"></a>

## Limits

<a id="limit-path-through-a-symbolic-link"></a>

### SQLSRC207 or SQLSRC212 for a path through a symbolic link

- **A project path or a `.sql` path through a symbolic link is not found.**  When the current folder is reached through a symbolic link and you give a full path that goes through the link, the tool compares it with the real path and finds no match: `--project` reports [SQLSRC207](../diagnostics.md#sqlsrc207), and a `.sql` path [SQLSRC212](../diagnostics.md#sqlsrc212), though the file is there.  The message shows both spellings.  Give the path relative to the current folder, or the full real path.
