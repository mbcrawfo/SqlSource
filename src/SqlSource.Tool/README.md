# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.

`describe` finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It asks the database of each such query for its parameters and columns and writes what it learns into a sidecar, a `.sql.json` file beside the `.sql` file, which is committed with it.

**This version has no describer for any database yet.**  It reports each query that must be described as [`SQLSRC216`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216), and everything else of the command is in place.

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
| `SQLSOURCE_CONNECTION` | Used when the run has exactly one database |

The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  The tool prints no connection string that it was given as one, and writes none into a sidecar: a description that holds the value ends the run with [`SQLSRC200`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200).  Put the value of `--connection` in quotes: a shell splits it at a space.  On a command line with `--connection`, a path that holds `=` or `;` is refused, since it may be a part of a connection string that the shell split; give the connection in its environment variable instead, or run the command from the path's own directory.

A query whose entry in the sidecar is current, because neither its SQL nor its database changed since this version of the tool described it, is skipped, and a database whose queries are all current needs no connection.  `--force` describes every query of the run again: it is the command to run after a change to the schema, which the tool cannot see.  `--database`, which may be given several times, restricts the run to the queries of the databases it names, and a path that ends in `.sql` to that file; a restricted run leaves every other sidecar as it was.  A sidecar is written only when every query of its file that needs an entry has one.  A run with no filter, in which nothing was reported as an error, deletes the sidecar of a file none of whose queries needs an entry any more: it is the one thing the tool removes from a project.  A run ends with one line for each database it ran on, and one with zeros for each name of `--database` that no query has:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars that are there, and changes no file.  A sidecar that is out of date is [`SQLSRC219`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc219), which says what differs and where, and which command mends it: `describe --force` for a difference that the database reports, since plain `describe` skips a query whose SQL is as it was.  It compares what changes generated code and not the versions of the tool or of the server, so a sidecar that a developer wrote passes on a server of another patch.  Every database of the run needs a connection.  With `--database`, `--project` or a `.sql` path it compares what `describe` with the same filters would change, and nothing else.  Its summary counts each query as the same, as one that differs, or as one that could not be described:

```console
billing (postgres): 15 same, 1 differ, 0 failed
```

The exit code is `0`; `1` when anything else was reported as an error, when the command line is wrong, or when the run was cancelled; and `2` when `--check` found a sidecar out of date and nothing else failed.

`--verbose` says what the run decides as it goes, a line for each project, file, database, query and sidecar, each starting with `sqlsource:`.  Without it the tool prints its errors and the lines of its summary, and nothing else.

`--log <path>` writes what the run did to a file, one JSON object on a line: what ran and on what, each project, file, database and query with what the run decided for it, each error, and how the run ended.  `SQLSOURCE_LOG` gives the path when the option does not.  The file is overwritten by each run, and the console stays as it is without the option.  A log holds no connection string: the value of each `--connection` is written as `***` with its name, and so is a value that a database's driver repeats in a message.  It does hold the paths of your files and the names of your queries and databases, so read it before you attach it to an issue.  A run that reported an error and wrote no log ends with one line that says so: `sqlsource: run again with --log <file> for a record of what the run did`.

`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on.

The tool reads `[SqlSourceGenerate]` from the source without compiling it, so `Path` and `Output` must be written as literals: `Path = "Queries"`, `Output = GeneratorOutput.Models`.

The tool asks MSBuild about each project, so it needs the .NET SDK, and a project must have been restored: the tool does not restore, and reports a project that was not.  The SqlSource package of a project and the tool should be of one version.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
