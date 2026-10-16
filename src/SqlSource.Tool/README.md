# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource/blob/main/README.md), a C# source generator for the SQL queries you keep in `.sql` files.  `sqlsource describe` connects to your database, asks it what the parameters and columns of each query are, and writes the answers into sidecar files beside your `.sql` files, which you commit with your project.  It describes PostgreSQL queries today.

## Install

Install it as a local tool, at the version of the `SqlSource` package your projects use.  Until the first release the package is a preview, so the command includes `--prerelease`:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

## Use

The tool connects through the connection string you give it, with `--connection` as on the first line, or in an environment variable such as `SQLSOURCE_CONNECTION_POSTGRES`:

```console
$ dotnet sqlsource describe --connection postgres="Host=localhost;Database=app;Username=app;Password=secret"
$ dotnet sqlsource describe --force
$ dotnet sqlsource describe --check
```

- `describe` describes each query that is new or changed since its sidecar was written, and writes the sidecars.
- `describe --force` describes every query again: run it after a change to the database's schema.
- `describe --check` writes nothing, and fails when a sidecar is out of date: run it on a build server.

[The tool's manual page](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md) has the whole workflow, every option and the exit codes.  [The SqlSource readme](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) says what SqlSource does and how to start.
