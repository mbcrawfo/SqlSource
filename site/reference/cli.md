# The sqlsource command

This page says how to install the `sqlsource` tool, and lists its commands, options, environment variables and exit codes.  See also [what `describe` does](describing.md), [how a build runs it](online-build.md#running-in-the-build), [the bundles of `diagnose` and `replay`](bundles.md) and [how the tool reports errors](errors.md#the-tools-errors).

The tool's `diagnose` command makes a bug-report bundle of one query, which you can attach to an issue.  Its `replay` command checks a bundle with no database.  See [reporting a wrong model](../guides/report-a-bug.md#report-a-bug) and [checking a bundle with `replay`](bundles.md#replay).

<a id="tool"></a>

## The tool

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
```

The tool is the `sqlsource` command of the `SqlSource.Tool` package.  It asks your database about your queries, and writes the answers into the [sidecar files](sidecars.md#sidecar-files) beside your `.sql` files.  In the sample, the command line gives the connection of the database `billing`, and the tool describes four queries.

- **The `describe` command connects to your database.**  It asks the database what the parameters and the columns of each of your queries are.
- **You commit the sidecars with the project.**  Then a build needs no database.
- **On a machine that has a connection, the build [runs `describe` itself](online-build.md#running-in-the-build)** before it compiles.

The tool describes the queries whose [output](settings.md#setting-output) is `repository`, `methods` or `models`, and the build checks each of them against its sidecar.  From the sidecars, the generator builds the [models](models.md#models) of these queries, and the [method](query-methods.md#query-methods) of a query whose output is `repository` or `methods`.  See [which queries are described](describing.md#which-queries).

<a id="install-the-tool"></a>

## Install the tool

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

Install the tool once, as a local tool of your repository.  Run the two commands in the folder at the root of the repository.  If the repository already has a tool manifest, skip the first command.

- **The first command creates the tool manifest, a `dotnet-tools.json` file.**  The manifest records the tool and its version.  Commit that file.
- **Anyone who clones the repository, and a build server, get the same version with `dotnet tool restore`.**  A build that [runs the tool](online-build.md#running-in-the-build) starts this version too.
- **Keep the tool at the version of the `SqlSource` package that your projects use.**  The two are released together, under one version number.  When you update the package, update the tool with `dotnet tool update SqlSource.Tool`.
- **A package and a tool of different versions can stop the tool from reading your project** ([SQLSRC206](../diagnostics.md#sqlsrc206)).
- **A project whose queries all have the output `sql` needs no tool.**  That is a project that uses SqlSource only to keep its SQL out of its code files.

The `sqlsource` command describes your queries: see [the step that describes your first query](../start/first-query.md#describe).  To run the tool, give it a connection to your database: see [connections](connections.md#connections).  What else the tool needs is under [requirements](#requirements).

<a id="command-reference"></a>

## Command reference

```text
dotnet sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                          [--connection <name>=<connection string>]...
                          [--env-file <path>] [--force | --check] [--verbose]
                          [--log <path>] [--watch] [--configuration <name>]
dotnet sqlsource diagnose --query <file>:<name> [<path>] [--project <path>]...
                          [--connection <name>=<connection string>]...
                          [--env-file <path>] --out <path> [--no-obfuscate]
                          [--verbose] [--log <path>] [--configuration <name>]
dotnet sqlsource diagnose --scrub <bundle> --out <path>
dotnet sqlsource replay <bundle> [--verbose] [--log <path>]
```

`dotnet sqlsource --help` prints the options of the tool.  `--help` after a command prints the options of that command, as `dotnet sqlsource diagnose --help` does.  `dotnet sqlsource --version` prints the version of the tool.

The arguments and the first table of options below are those of `describe`.  For `diagnose`, see [making a bundle with `diagnose`](bundles.md#diagnose).  For `replay`, see [checking a bundle with `replay`](bundles.md#replay).

<a id="arguments"></a>

### Arguments

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx
$ dotnet sqlsource describe src/App/Queries/Users.sql src/App/Queries/Orders.sql
```

- **The project or solution to run on.**  It is a `.sln`, `.slnx` or `.csproj` file, or a folder that holds exactly one of them.  Without it, the tool runs on the current folder.  You can give only one.
- **The tool looks for the project or solution in that folder alone**, not in the folders below it.  If the folder holds none, the tool reports [SQLSRC201](../diagnostics.md#sqlsrc201).  If it holds more than one, the tool reports [SQLSRC202](../diagnostics.md#sqlsrc202).
- **In a solution, the tool runs on every C# project that uses the SqlSource package.**  It leaves the other projects out, and says nothing about them.
- **A project with several target frameworks uses the package when any of them does.**  The tool then reads each framework that uses it: see [which queries are described](describing.md#which-queries).
- **Every C# project of the solution must be restored**, a project with no queries too.  If one is not, the tool reports [SQLSRC220](../diagnostics.md#sqlsrc220).
- **A path that ends in `.sql` restricts the run to that file.**  You can give several.
- **Each `.sql` path must be a file that a type of the run claims.**  If it is not, the tool reports [SQLSRC212](../diagnostics.md#sqlsrc212), and the rest of the run goes on.
- **A relative path starts from the current folder.**
- **A path through a symbolic link names the file the link leads to.**  This holds for a `.sql` path, the file of `--query` and a `--project` path.  It also holds when you reach the current folder through a link, as in a shell's `$PWD`.

<a id="paths"></a>

### How an error names a path

An error names a path that you typed in full, unless the path may be a secret typed in the wrong place.  Then it names the path by its position on the command line, and the log holds `***` in its place:

```console
$ dotnet sqlsource describe S3cret
sqlsource : error SQLSRC203: The path at position 2 is not a .sln, .slnx or .csproj file, or a directory that holds one
```

A path is named in full when it holds neither `=` nor `;` and one of these is true:

- it is empty, as an unset variable of a shell gives;
- it ends in `.sln`, `.slnx`, `.csproj`, `.sql`, `.env`, `.log`, `.json`, `.jsonl` or `.zip`, in any case;
- it names a file or a folder that exists when the run starts, or, for `--log` and `--out`, whose files the tool writes, a folder;
- it holds `/` or `\`, and the first folder it names exists, as `src/Typo/App` does when `src` exists.

The rule holds for every place a path goes: the path of `describe` or `diagnose`, a `.sql` path, and the values of `--project`, `--log`, `--env-file` and `--manifest`; the file of `--query`, `--scrub` and `--out` of `diagnose`; and the bundle of `replay`.  The value `none` of `--env-file` is always written.  Under `--online`, the paths of `--manifest`, `--log` and `--env-file` are the build's own, and are always named in full.

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
| `--manifest <path>` | For the build's use: describes the project that the build's record of it names, without reading the project again.  May be given once for each target framework of the project; records of two projects are [SQLSRC206](../diagnostics.md#sqlsrc206).  It takes no project or solution, `.sql` path, `--project`, `--database`, `--configuration` or `--watch`.  See [running in the build](online-build.md#running-in-the-build) |
| `--online <auto\|true>` | For the build's use, with `--manifest`: takes a `--manifest`, `--log` or `--env-file` path that holds `=` or `;`, as the build's own may, and prints and logs it in full, writes each error on one line, writes no log for `SQLSOURCE_LOG`, and under `auto` says it is offline, and exits with `0`, when no database with a query to describe has a connection.  See [running in the build](online-build.md#running-in-the-build) |
| `--watch` | Describes once, then again each time a file of the run changes, until stopped: see [describe on save](describing.md#describe-on-save).  Not with `--check` or `--manifest` |
| `--configuration <name>` | Of `describe` and `diagnose`: reads the projects in this configuration, as `dotnet build -c` does, so that a `[SqlSourceGenerate]` inside `#if` is read with that configuration's constants.  Without it, each project's default, `Debug` unless the variable `Configuration` names another.  A name that holds `=` or `;` is refused.  Not with `--manifest`, whose build chose its configuration.  See [`Source` written as text](attributes.md#limit-source-written-as-text) |

If a run has `--project`, `--database` or a `.sql` path, it writes only the sidecars of the files whose queries it selected.  It deletes no sidecar.

The tool has six more options: `--help` and `--version`, and four that `diagnose` alone takes.

| Option | What it does |
|----|----|
| `--help` | Prints the options of the tool, or of a command when it follows one, such as `dotnet sqlsource diagnose --help` |
| `--version` | Prints the version of the tool, as `dotnet sqlsource --version` |
| `--query <file>:<name>` | Of `diagnose`: the query to report, by its `.sql` file and its name.  See [making a bundle with `diagnose`](bundles.md#diagnose) |
| `--out <path>` | Of `diagnose`: where to write the bundle, a zip, with its mapping beside it.  See [making a bundle with `diagnose`](bundles.md#diagnose) |
| `--no-obfuscate` | Of `diagnose`: writes a raw bundle, with your names and SQL as they are, and no mapping.  See [making a bundle with `diagnose`](bundles.md#diagnose) |
| `--scrub <bundle>` | Of `diagnose`: renames a raw bundle with no database, and writes it and its mapping at `--out`.  See [making a bundle with `diagnose`](bundles.md#diagnose) |

<a id="environment-variables"></a>

### Environment variables

| Variable | What it does |
|----|----|
| `SQLSOURCE_CONNECTION_<NAME>` | The connection of the database `<name>`: see [connections](connections.md#connections) |
| `SQLSOURCE_CONNECTION` | The connection of a run with exactly one database |
| `SQLSOURCE_LOG` | The path of the log, when `--log` is not given.  In a build, any value writes a log of each project's run: see [running in the build](online-build.md#running-in-the-build) |
| `SQLSOURCE_DEBUG` | Any value that is not empty adds the stack trace to [SQLSRC200](../diagnostics.md#sqlsrc200), for a bug report |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `true` or `1`, in any case, makes `--watch` look at the folders every second instead of asking the system: see [describe on save](describing.md#describe-on-save) |

- **A connection variable or a log variable that is empty, or holds only white space, is not set.**
- **You can also set each connection variable in a [`.env` file](connections.md#env-file).**  If the environment sets the variable too, the environment wins.
- **The tool reads `SQLSOURCE_LOG`, `SQLSOURCE_DEBUG` and `DOTNET_USE_POLLING_FILE_WATCHER` from the environment alone.**

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

`--verbose` prints a line for each project, file, database, query and sidecar.  Each line starts with `sqlsource:`, and says what the run decided for that thing.  Use `--verbose` to see why a query was skipped, or why a project was left out.

- **The line of a database names where its connection came from, and never the connection.**  It names one of these:
  - `--connection`;
  - the name of a variable;
  - for a connection from a [`.env` file](connections.md#env-file), the variable and the file, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`.
- **The line of a project with several target frameworks names each framework that uses SqlSource**, as `net8.0;net10.0, 2 .sql files`.  It counts a `.sql` file that two frameworks include once.
- **Under `--watch`, `--verbose` says the same of each pass, and nothing more.**
- **Without `--verbose`, the tool prints its errors and the summary.**  It also prints the line that ends a failed run, or the line that says that no project uses SqlSource.

<a id="logs"></a>

### Logs

```console
$ dotnet sqlsource describe --log sqlsource.log
```

`--log <path>`, or the variable `SQLSOURCE_LOG`, writes a record of the run to a file.  Each line of the file is one JSON object.

The log is your own record of what the run did.  It holds your names, your SQL and what your server said.  Never attach a log to an issue.  Send it privately, when a maintainer asks for it.  To an issue, attach a bundle, in which your names and strings are renamed: see [reporting a wrong model](../guides/report-a-bug.md#report-a-bug).

- **Each run overwrites the file.**  So the tool refuses a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx` ([SQLSRC218](../diagnostics.md#sqlsrc218)).  Then a slip on the command line never empties a file of your project.
- **The tool also refuses a `--log` path that holds `=` or `;`.**  Such a path is more likely a connection string typed in the wrong place.  A path from `SQLSOURCE_LOG` is not held to that rule.
- **A `--watch` writes one file, from its start until it is stopped.**  The file holds every pass, and where each pass begins and ends.
- **A run that reports an error and writes no log ends with a line:** `sqlsource: run again with --log <file> for a record of what the run did`.  These runs do not print the line:
  - a `--check` whose only errors are sidecars out of date;
  - a `--watch`;
  - `diagnose` and `replay`;
  - a run that is stopped with Ctrl+C, `SIGTERM` or `SIGHUP`;
  - the run of a build, which is given no `--log`.  For the logs of a build, see [running in the build](online-build.md#running-in-the-build).

**Read a log before you send it.**  The tool writes each connection value as `***` wherever it finds the value whole:

- the value of each `--connection` on the command line;
- the value of each connection, from the command line or a variable, wherever the database driver repeats it in a message.

It finds the value as written, with the spaces at its ends trimmed, or as the driver writes it again.  The tool also writes these as `***`:

- each password of a connection string, where it stands apart from other letters, digits and underscores, and is at least four characters long;
- a shorter password, or one that is the name of its logical database or of its dialect, where a message gives it as the value of a password, as in `Password=pg;`;
- in the same way, the password that the PostgreSQL driver reads from `PGPASSWORD`, or from the entry of a password file that it would use for the connection, when the connection string gives none;
- in the record of your command line, every value of `--database` and `--connection`;
- a path that an error names by its position, as [how an error names a path](#paths) says.

These can appear in the log as written ([a limit](connections.md#limit-short-password)):

- a secret typed where a path goes that reads as a path, such as one that ends in `.sql`;
- a password shorter than four characters, where a message does not give it as the value of a password;
- a password that is the name of its logical database or of its dialect, such as `postgres`, where a message does not give it as the value of a password;
- the password of a PostgreSQL password file that is a pipe, such as `PGPASSFILE=<(...)` in a shell;
- the login, in an error of SQL Server about a query, such as an error for a database that the login may not open.

Search the log for your password before you share it.  The log also holds:

- the version of the tool, of .NET and of the operating system, and the rest of your command line;
- the paths of your projects and files, and the names of your queries and databases;
- the SQL of your queries as the tool sent it to the database, and what the server answered, its errors and warnings included;
- for a database that could not be opened, what went wrong, in the tool's own words, which name neither the host nor the user.  With it is the SQLSTATE of the server, or the error number of SQL Server;
- each error, and how the run ended, with its exit code.

If the tool cannot finish the log, as when the disk is full, it reports [SQLSRC218](../diagnostics.md#sqlsrc218) after the run has ended.  The tool then exits with `1`.  The last line of the log can still hold the exit code that the run had before.

<a id="requirements"></a>

### Requirements

- **The .NET SDK, and not only a runtime.**  The tool asks MSBuild about your projects through `dotnet msbuild`.  The tool runs on .NET 8 or later.
- **Restored projects.**  The tool does not restore your projects.  Run `dotnet restore` first, or the tool reports [SQLSRC220](../diagnostics.md#sqlsrc220).
- **The same version as the `SqlSource` package** of your projects ([SQLSRC206](../diagnostics.md#sqlsrc206)).
- **A database that the tool can reach, for each logical database with a query to describe.**  The database is of a version that the tool supports: see the versions of [PostgreSQL](databases/index.md#supported-versions) and [SQL Server](databases/index.md#supported-versions).

<a id="summary"></a>

## The summary

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

Every run ends with one line for each database that it ran on, as in the sample.  Its words mean:

- **described**: the queries the database was asked about in this run.
- **skipped**: the queries whose sidecar entry was current.
- **failed**: the queries that could not be described.  Each has an error above the summary.
- **no connection**: the database has no connection and needed none, since all its queries were current.

Under `--check`, the counts are `same`, `differ` and `failed`.

A name given to `--database` that no query of the run has gets a line of zeros, so that you see a misspelt name.

- **If the name is close to a database of the run, the line names it**, in the spelling that you gave.
- **If it is not, the line says `--database at position <n>`**, with the position of the value on the command line.  A value typed after `--database` by mistake can be a secret.

If no project of a solution uses SqlSource, a run on the solution prints `sqlsource: no project of '<path>' uses SqlSource`, and no summary.
