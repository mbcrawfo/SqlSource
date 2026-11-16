# SqlSource.Tool

`sqlsource describe`, part of [SqlSource](https://github.com/mbcrawfo/SqlSource/blob/main/README.md), asks your database about the parameters and columns of each query.  It writes the answers to sidecar files beside your `.sql` files: see [what describe does](https://mbcrawfo.github.io/SqlSource/reference/describing.html#describe).  You commit the sidecar files with your project, since every build reads them.  The tool describes PostgreSQL and SQL Server queries.

## Install

Install it as a local tool, from the root of your repository, at the version of the `SqlSource` package that your projects use.  If your repository already has a tool manifest, leave out the first command.  See [install the tool](https://mbcrawfo.github.io/SqlSource/reference/cli.html#install-the-tool).

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

## Use

Give the tool the connection string of your database in a `.env` file:

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

For SQL Server, the variable is `SQLSOURCE_CONNECTION_MSSQL`, with SqlClient's connection string.  Keep that file out of git, since it holds your password.  Put it in the folder of your project: the tool does not look for a `.env` file in a folder above it.  See [the `.env` file](https://mbcrawfo.github.io/SqlSource/reference/connections.html#env-file).  Then run the tool from the folder of your project:

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe --force
$ dotnet sqlsource describe --check
$ dotnet sqlsource describe --watch
```

- `describe` describes each query that is new or changed, and writes the sidecars.
- `describe --force` describes every query again: run it after a change to your schema.
- `describe --check` writes nothing, and fails when a sidecar is missing or out of date: run it in CI.
- `describe --watch` describes again each time you save a file: see [describe on save](https://mbcrawfo.github.io/SqlSource/guides/describe-on-save.html).

[What describe does](https://mbcrawfo.github.io/SqlSource/reference/describing.html) has the rules of each.  A build with a connection runs `describe` itself before it compiles, and a build in CI never does: see [running in the build](https://mbcrawfo.github.io/SqlSource/reference/online-build.html#running-in-the-build).

If a model is wrong, `diagnose` writes a bug-report bundle of its query, and `replay` checks a bundle: see [report a wrong model](https://mbcrawfo.github.io/SqlSource/guides/report-a-bug.html).

## Documentation

- [Keep your queries up to date](https://mbcrawfo.github.io/SqlSource/start/keep-up-to-date.html)
- [The command reference](https://mbcrawfo.github.io/SqlSource/reference/cli.html#command-reference)
- [Connections and the `.env` file](https://mbcrawfo.github.io/SqlSource/reference/connections.html)
- [Report a wrong model](https://mbcrawfo.github.io/SqlSource/guides/report-a-bug.html)
- [The SqlSource readme](https://github.com/mbcrawfo/SqlSource/blob/main/README.md)
