# Contributing to SqlSource

This repository holds SqlSource, a C# source generator that turns the `.sql` files of a project into typed code, and `sqlsource`, the command that describes those queries against a database.  [`docs/architecture/README.md`](docs/architecture/README.md) explains how their code works.  This page is how to set up, build, test, check and pack them.  Each area has a readme with its procedures: [`tests/README.md`](tests/README.md), [`tools/README.md`](tools/README.md) and [`.github/workflows/README.md`](.github/workflows/README.md).  Versioning and releasing are covered in [docs/publishing.md](docs/publishing.md).

## Required tools

| Tool | Version | Notes |
|----|----|----|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.401 or a later 10.x release | Pinned in `global.json` |
| [Docker](https://docs.docker.com/get-started/get-docker/) | Any current release | Runs the pinned lint images used by the scripts in `tools/`, the PostgreSQL that `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.Postgres.Npgsql8` run against, and the SQL Server that `tests/SqlSource.Tests.SqlServer` and `tests/SqlSource.Tests.SqlServer.SqlClient61` run against |
| `unzip` | Any | `tools/check-package.sh` lists the contents of the packages with it.  Present on macOS and most Linux distributions. |

CSharpier and ReportGenerator are local .NET tools pinned in `dotnet-tools.json`.  Restore them once after cloning:

```bash
dotnet tool restore
```

## Projects

The two projects that ship are:

- `src/SqlSource` is the source generator, and the package `SqlSource`.  [`docs/architecture/generator.md`](docs/architecture/generator.md) explains how it works.
- `src/SqlSource.Tool` is the `sqlsource` command, a console application that references the generator's project and shares its code, and the package `SqlSource.Tool`.  [`docs/architecture/tool.md`](docs/architecture/tool.md) explains how it works.

The seven test projects and the two libraries they share, which [`tests/README.md`](tests/README.md) describes:

- `tests/SqlSource.Tests` runs every test of the generator.
- `tests/SqlSource.Tests.RoslynFloor` compiles the generator driver tests a second time, on the oldest supported Roslyn.
- `tests/SqlSource.Tool.Tests` runs the tests of the tool.
- `tests/SqlSource.Tests.Postgres` runs the PostgreSQL describer against a real server and is the end-to-end project, which builds models and methods from its own queries and runs every described query through its method, and needs Docker.
- `tests/SqlSource.Tests.Postgres.Npgsql8` writes and reads every mapped type through the generated methods, and reads them through Dapper, on Npgsql 8, the oldest supported driver, and needs Docker.
- `tests/SqlSource.Tests.SqlServer` runs the tests of SQL Server against a real server and is SQL Server's end-to-end project, which builds models and query methods from its own queries, runs each query through its method, writes and reads every mapped type through the methods, measures their allocations against hand-written ADO.NET, and reads rows through Dapper for the manual's recipe, and needs Docker.
- `tests/SqlSource.Tests.SqlServer.SqlClient61` reads every mapped type through Dapper, and writes and reads it through the generated methods, on Microsoft.Data.SqlClient 6.1, the oldest supported driver, and needs Docker.
- `tests/SqlSource.Testing` and `tests/SqlSource.Tool.Testing` are the libraries of test helpers, which hold no tests.

`examples/PostgresExamples` shows a user SqlSource with PostgreSQL and Dapper, and `examples/SqlServerExamples` with SQL Server and Dapper; they build with the solution and hold no tests.

## Build, test and run

```bash
dotnet build SqlSource.slnx
dotnet test --solution SqlSource.slnx
```

This runs every test project, the two PostgreSQL projects on the default PostgreSQL image and the two SQL Server projects on the default SQL Server image.  CI also runs `tests/SqlSource.Tests.Postgres` on PostgreSQL 14, the oldest supported:

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres
```

and `tests/SqlSource.Tests.SqlServer` on SQL Server 2017, the oldest supported, through the variable `SQLSOURCE_TESTS_SQLSERVER_IMAGE`:

```bash
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer
```

The default SQL Server image is `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`.  The nightly run, `.github/workflows/nightly.yml`, runs the same two projects on every supported major between the oldest and the default, PostgreSQL 15 to 17 and SQL Server 2019 and 2022, with the images it names in the same variables.  SQL Server's images are built for x64 alone, and on Apple silicon Docker runs them under emulation: the first run takes minutes while each image is pulled.  The SQL Server tests' readme has [the images and how a tag is moved](tests/SqlSource.Tests.SqlServer/README.md#the-images).

The PostgreSQL tests' readme has the procedures of that project: [moving to a new image minor](tests/SqlSource.Tests.Postgres/README.md#the-image), [re-recording the describer's fixtures](tests/SqlSource.Tests.Postgres/README.md#the-describers-fixtures) with `tools/record-fixtures.sh postgres`, [adding a realistic case](tests/SqlSource.Tests.Postgres/README.md#the-realistic-corpus), writing its sidecar with `tools/describe-end-to-end.sh`, and [writing the sidecars of the end-to-end project, the realistic cases and the examples again](tests/SqlSource.Tests.Postgres/README.md#writing-the-sidecars) with `tools/describe-end-to-end.sh`.  The SQL Server tests' readme has [adding a realistic case](tests/SqlSource.Tests.SqlServer/README.md#adding-a-case), [re-recording the describer's fixtures](tests/SqlSource.Tests.SqlServer/README.md#the-describers-fixtures) with `tools/record-fixtures.sh mssql`, whose run with no engine records both, and [writing the sidecars of its end-to-end project, its realistic cases and its examples again](tests/SqlSource.Tests.SqlServer/README.md#writing-the-sidecars) with `tools/describe-end-to-end.sh`, which writes those of both engines in one run.  To [add a fixture project](tests/README.md#fixture-projects), see `tests/README.md`.

To run the tool from its source:

```bash
dotnet run --project src/SqlSource.Tool -- describe
```

To collect coverage and build its report:

```bash
dotnet test --solution SqlSource.slnx --results-directory artifacts/test-results --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-results/*.cobertura.xml' -targetdir:artifacts/coverage -reporttypes:Html '-assemblyfilters:+SqlSource;+SqlSource.Tool'
```

Each test project writes its own Cobertura file, and the report merges them.  It is `artifacts/coverage/index.html`.  The end-to-end `AllocationTests` of the two container projects skip under coverage, since the collector allocates in the process they measure; run them without `--coverage`, as `pre-commit-validation.sh --full` and CI do; a plain run includes them only when the change can alter what they measure.

## Pack

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

This builds in `Release` and writes two packages with one version, `artifacts/packages/SqlSource.<version>-dev.nupkg` and `artifacts/packages/SqlSource.Tool.<version>-dev.nupkg`.  Three scripts then check them, each with the folder as its argument, and [`tools/README.md`](tools/README.md#package-checks) says what each proves:

- `tools/check-package.sh` checks what the two packages hold.
- [`tools/check-package-install.sh`](tools/README.md#check-package-installsh) installs the generator's package into a consumer project, builds it and runs it, and publishes a web project that references it.
- `tools/check-tool-install.sh` installs the tool the way a user does and runs it.

## Checks

Run the checks before committing, and all of them before pushing:

```bash
./pre-commit-validation.sh
./pre-commit-validation.sh --full
```

It verifies formatting, runs the [linters](tools/README.md#linters), builds the solution, runs the tests, checks the two packages, installs the generator's into a project and installs the tool.  It never rewrites files, runs every step even when one fails (the tests and the three package checks are skipped if the build fails), and ends with a summary of what passed, failed and was skipped.

A plain run checks only what the uncommitted change (staged, unstaged and untracked) can affect:

- Formatting is limited to the changed `.cs` files, and skipped when none changed; a change to `format.sh` or to a file that configures the formatters checks everything.
- editorconfig-checker always runs.  shellcheck and shfmt run when a shell script or `.editorconfig` changed, actionlint when a workflow or `tools/actionlint.sh` changed, and the three package checks when a change can alter what is packed or how the packages are checked.
- The build and the tests that need no container always run.  A pair of container test projects, PostgreSQL's or SQL Server's, runs only when the change can reach it, and the allocation tests only when it can change what they measure.  [`tools/test-scopes.txt`](tools/README.md#test-scopes) decides which pair a path can reach.
- A clean tree, a path of a kind the script does not know and a failing `git` command each make the run full.

`--full` runs everything, and every push needs it, on the tree as it will be pushed.  A partial run says so at its end.  `--plan [path ...]` prints what a run would do and why, for the uncommitted change or for the paths given, and runs nothing.

`format.sh` rewrites C# and project files with `dotnet format style`, `dotnet format analyzers` and CSharpier, and `format.sh --check` reports what it would change and rewrites nothing.  Either takes part names, `style`, `analyzers` or `csharpier`, to run only those parts, and `-- file ...` after them to limit the run to those files; CI runs the parts in separate jobs.  [`tools/README.md`](tools/README.md#formatting) has the form.

Both `pre-commit-validation.sh` and `format.sh --check` restore packages in locked mode, as CI does, so a `packages.lock.json` that no longer matches its project fails the check.  After changing a package reference or version, update the lock files and commit them:

```bash
dotnet restore SqlSource.slnx
```

## Continuous integration

CI runs the same checks as `pre-commit-validation.sh --full` on Linux, in parallel jobs.  It pulls the test images in a step of its own, with `tools/pull-test-images.sh`, and runs `tests/SqlSource.Tests.Postgres` again on PostgreSQL 14 and `tests/SqlSource.Tests.SqlServer` again on SQL Server 2017, each in a job of its own.  The `ci` job is the one required check on `main`.  `nightly.yml` runs the tests that need no container on Windows and macOS every night, and `tests/SqlSource.Tests.Postgres` on PostgreSQL 15 to 17 and `tests/SqlSource.Tests.SqlServer` on SQL Server 2019 and 2022 on Linux, and by hand on a branch:

```bash
gh workflow run nightly.yml --ref <branch>
```

[`.github/workflows/README.md`](.github/workflows/README.md) has each workflow, its artifacts and [the nightly run](.github/workflows/README.md#nightly).
