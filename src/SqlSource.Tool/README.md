# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource/blob/main/README.md), a C# source generator for the SQL queries you keep in `.sql` files.  `sqlsource describe` connects to your database, asks it what the parameters and columns of each query are, and writes the answers into sidecar files beside your `.sql` files, which you commit with your project.  That checks each query against your schema, and the sidecars record what the database said.  The build reads the sidecars, builds from them a type for each query's parameters, a type for its rows and a method that runs the query, and reports a query that has no entry in its sidecar, or whose entry is older than the query.  It describes PostgreSQL and SQL Server queries.

## Install

Install it as a local tool, from the root of your repository, at the version of the `SqlSource` package your projects use (skip the first command if the repository already has a tool manifest).  Until the first release the package is a preview, so the command includes `--prerelease`:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

## Use

Give it the connection string of your database in a `.env` file at the root of your repository, and keep that file out of git, since it holds your password:

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

The same variable in the environment wins over the file, and `--connection postgres="..."` gives the connection on the command line instead.  Then run the tool from the folder of your project or solution, or name it: `dotnet sqlsource describe src/App/App.csproj`.

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe --force
$ dotnet sqlsource describe --check
$ dotnet sqlsource describe --watch
```

`postgres` is the name of the database your queries belong to, which is the dialect's name unless you name it: for SQL Server it is `mssql`, read from `SQLSOURCE_CONNECTION_MSSQL`.

- `describe` describes each query that is new or changed since its sidecar was written, and writes the sidecars.
- `describe --force` describes every query again: run it after a change to the database's schema.
- `describe --check` writes nothing, and fails when a sidecar is out of date: run it on a build server.
- `describe --watch` describes again each time you save a `.sql` file or another file of your project, until you stop it with Ctrl+C: leave it running while you work, or have your editor start it, as [describe on save](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#describe-on-save) says.

On a machine with that `.env` file, or `SQLSOURCE_CONNECTION` in the environment, `dotnet build` runs `describe` itself before it compiles, and reports a query the database rejects as a build error at the query; a build in CI never does: see [running in the build](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#running-in-the-build).

To report a query whose model is wrong, two more commands:

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
$ dotnet sqlsource replay ../report.zip
```

- `diagnose` describes the one query again and writes a bug-report bundle of it, a zip with your names and strings renamed, to attach to an issue, and beside it a mapping of the new names, which you keep to yourself: see [reporting a wrong model](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#report-a-bug).
- `replay` runs SqlSource from a bundle with no database, and says whether it gives the bundle's answer: see [checking a bundle with `replay`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#replay).

[The tool's manual page](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md) has the whole workflow, every option and the exit codes.  [The SqlSource readme](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) says what SqlSource does and how to start.
