# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource/blob/main/README.md), a C# source generator for the SQL queries you keep in `.sql` files.  `sqlsource describe` connects to your database, asks it what the parameters and columns of each query are, and writes the answers into sidecar files beside your `.sql` files, which you commit with your project.  That checks each query against your schema, and the sidecars record what the database said.  The build reads the sidecars, builds from them a type for each query's parameters and a type for its rows, and reports a query that has no entry in its sidecar, or whose entry is older than the query.  It describes PostgreSQL queries today.

> **Coming in a later release.**  The generator will build query methods from the sidecars.  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).

## Install

Install it as a local tool, from the root of your repository, at the version of the `SqlSource` package your projects use (skip the first command if the repository already has a tool manifest).  Until the first release the package is a preview, so the command includes `--prerelease`:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

## Use

Run it from the folder of your project or solution, or name it: `dotnet sqlsource describe src/App/App.csproj`.  The tool connects through the connection string you give it, in an environment variable as on the first line, or with `--connection postgres="..."` on the command line:

```console
$ export SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
$ dotnet sqlsource describe
$ dotnet sqlsource describe --force
$ dotnet sqlsource describe --check
```

`postgres` is the name of the database your queries belong to, which is the dialect's name unless you name it.

- `describe` describes each query that is new or changed since its sidecar was written, and writes the sidecars.
- `describe --force` describes every query again: run it after a change to the database's schema.
- `describe --check` writes nothing, and fails when a sidecar is out of date: run it on a build server.

[The tool's manual page](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md) has the whole workflow, every option and the exit codes.  [The SqlSource readme](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) says what SqlSource does and how to start.
