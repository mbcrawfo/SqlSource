# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.

`describe` finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It asks the database of each such query for its parameters and columns and writes what it learns into a sidecar, a `.sql.json` file beside the `.sql` file, which is committed with it.

**This version has no describer for any database yet.**  It reports each query that must be described as [`SQLSRC216`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216), and everything else of the command is in place.

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing"
$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
$ dotnet sqlsource describe src/App/Queries/Users.sql
$ dotnet sqlsource describe --database billing --force
```

A query belongs to a logical database: the one its `-- database:` marker names, or the `SqlSourceDatabase` metadata of its file, or the property of that name, or else the name of its dialect.  A connection is given for that name, on the command line or in the environment, and never in the project file, since it holds credentials:

| Source | For the database `billing` |
|----|----|
| `--connection <name>=<connection string>` | `--connection billing="Host=localhost;Database=billing"` |
| The variable of the name | `SQLSOURCE_CONNECTION_BILLING`: the name in upper case, with every character that is not a letter or a digit written as `_` |
| `SQLSOURCE_CONNECTION` | Used when the run has exactly one database |

The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  The tool never prints a connection string.

A query whose entry in the sidecar is current, because neither its SQL nor its database changed since this version of the tool described it, is skipped, and a database whose queries are all current needs no connection.  `--force` describes every query of the run again: it is the command to run after a change to the schema, which the tool cannot see.  `--database`, which may be given several times, restricts the run to the queries of the databases it names, and a path that ends in `.sql` to that file; a restricted run leaves every other sidecar as it was.  A sidecar is written only when every query of its file that needs an entry has one, and a run ends with one line for each database:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

The exit code is `0`, or `1` when anything was reported as an error.

`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on. 

The tool reads `[SqlSourceGenerate]` from the source without compiling it, so `Path` and `Output` must be written as literals: `Path = "Queries"`, `Output = GeneratorOutput.Models`.

The tool asks MSBuild about each project, so it needs the .NET SDK, and a project must have been restored: the tool does not restore, and reports a project that was not.  The SqlSource package of a project and the tool should be of one version.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
