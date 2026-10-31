# Tools

This folder holds the scripts that `pre-commit-validation.sh` and `.github/workflows/build.yml` run, the consumer project that one of them installs the package into, the file that says which database tests a change can skip, a script that pulls the test images, and two recording scripts, which are not checks.  [`CONTRIBUTING.md`](../CONTRIBUTING.md) says when to run them; [`AGENTS.md`](AGENTS.md) holds the traps of changing them.

## Linters

Each linter script runs its tool from a pinned Docker image, so they need Docker:

| Script | Checks |
|----|----|
| `tools/editorconfig-checker.sh` | Every file against `.editorconfig` |
| `tools/shellcheck.sh` | Shell scripts, with ShellCheck |
| `tools/shfmt.sh --check` | Shell script formatting; without `--check` it rewrites the files |
| `tools/actionlint.sh` | GitHub Actions workflows |

## Package checks

The three package checks look at what `dotnet pack SqlSource.slnx --output artifacts/packages` wrote.  Each takes that folder as its argument.

### `check-package.sh`

```bash
tools/check-package.sh artifacts/packages
```

This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props`, `build/SqlSource.targets` and `build/SqlSource.AfterSdk.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly, Roslyn, the library that reads a solution, Npgsql with `Npgsql.NetTopologySuite`, NetTopologySuite and `NetTopologySuite.IO.PostGis`, which its PostgreSQL describer registers to bind a PostGIS parameter, and Microsoft.Data.SqlClient beside it, SqlClient's copies for Unix and Windows and its native SNI for Windows under `runtimes/`, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.

### `check-package-install.sh`

```bash
tools/check-package-install.sh artifacts/packages
```

This installs the package the way a consumer does.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.

The project's files are these.  `Consumer.csproj` and `Directory.Build.targets` are its MSBuild files, `sqlsource.json` is its settings file, `Queries.cs` has the two types that the generator fills from the `.sql` files of `Queries/`, and `Program.cs` prints one line for each thing it checks.  The project uses a constant, a method with tokens, the generator parameters and the dialect, each in the settings file and in a file header with the markers `-- generator:` and `-- dialect:`, a dialect with an option, and `Parameters` on the attribute, so it fails when the generator, either MSBuild file or the settings file does not reach a consumer.  The settings file sets `"root": true`, since the copy is outside the repository, and `output` to `Sql`, so its queries need no sidecar.

Its `Directory.Build.targets` has a target that adds a `.sql` file while the build runs, whose header names its dialect.  The SDK imports that file after the targets of a package, so the project also fails when a file that a target adds does not reach the generator.

The script then asks the project what the `sqlsource` tool asks one.  `SqlSourceImported` must be `true`, which shows that NuGet imported the package's props.  The target `SqlSourceWriteManifest`, run by name, must write a manifest that starts with its version, lists a `.sql` file, holds a `Settings` line with the full path of the copy's `sqlsource.json`, and does not list `Queries/AddedByATarget.sql`, the file that `Directory.Build.targets` adds from a hook of the build.  A target that is run by name does not run that hook, which is [TD-0025](../docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md); the build of the consumer compiled the file, and `expected-output.txt` has its query.

It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.

Last, it writes a web project, of `Microsoft.NET.Sdk.Web`, that references the package and has a `.sql` file, its sidecar and a settings file, `sqlsource.json`, publishes it, and fails when any of them is in its `bin` folder or in what `dotnet publish` wrote.  The Web SDK copies a `.json` file to both unless the package says otherwise, and the consumer, of the plain SDK, copies none.  The project claims no file, so the sidecar can be empty, and the settings file sets nothing.  Without an argument the script packs first, as the other does.

### `check-tool-install.sh`

```bash
tools/check-tool-install.sh artifacts/packages
```

This installs the tool the way a user does: `dotnet tool install` into a temporary folder outside the repository, from a feed that holds nothing else.  It then runs `sqlsource --version` and compares it with the package's version, runs `sqlsource --help`, and runs `sqlsource describe` in an empty folder, which must exit with 1 and report `SQLSRC201`.  Then it writes a project of one `mssql` query beside SqlSource's MSBuild files, copied from `src/SqlSource/build/`, with a settings file that sets `"root": true`, the dialect `mssql` and the database `orders`, never restored, and describes it with a connection to the port 1 of the machine, which refuses: the run must exit with 1 and report `SQLSRC224` with the text that SqlClient could not reach the server, and no `SQLSRC200`.  SqlClient is loaded only to connect, and the copy outside `runtimes/` is a stub that throws on any use, which the tool reports as `SQLSRC224` with another text, so this shows that the copy for the platform loads.  A tool's package carries everything the tool loads, so this is what shows that all of it is there.

Last, it runs `sqlsource --version` again with `DOTNET_ROLL_FORWARD` set to `LatestMajor`, which starts the tool on the newest runtime the machine has.  The tool targets .NET 8 and rolls forward on a machine that has only a later one, and this proves it wherever the script runs.  Without an argument the script packs first.

## Formatting

`format.sh` is at the root of the repository, and `pre-commit-validation.sh` runs it as `format.sh --check`.

```bash
./format.sh [--check] [style|analyzers|csharpier ...] [-- file ...]
```

Without `--check` it rewrites files in place.  The part names choose which of `dotnet format style`, `dotnet format analyzers` and CSharpier run, always in that order, and all three run when none is given.  Files after `--`, as paths from the repository root, limit all three parts to those files: the two `dotnet format` parts take them as `--include`, and CSharpier checks them in place of the tree.  That is how a pre-commit run checks only the `.cs` files that changed.  `--` with no file after it is a usage error.

## Test scopes

`tools/test-scopes.txt` says which database test projects a change to a path can affect.  `pre-commit-validation.sh` reads it to skip the PostgreSQL pair of test projects (`tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8`) or the SQL Server pair (`tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61`) when nothing in the uncommitted change reaches them, and the tests read it to check that a skip is safe.  `--full` ignores it, and so does CI.

A line is `<scope> <pattern>`:

- The scope is `none` (neither pair), `postgres`, `sqlserver` or `both`.
- The pattern is a path from the repository root, with `/` as the separator.  `*` matches any run of characters, `/` included, and every other character is literal.  A pattern with `?`, `[`, `]`, `\`, `(`, `)`, `!`, `+` or `@` in it is refused, since bash would read them as glob characters, as an escape or as an extended glob operator.
- Blank lines and lines that start with `#` are ignored.
- The first line whose pattern matches a path decides that path's scope, so an exception goes before the broad line it carves out of.  A path that no line matches is `both`.
- The pairs that run are the union over every changed path.  A change to the file itself runs both.
- A `project <postgres|sqlserver> <path>` line names a container project's `.csproj` and the engine that runs it.  The four container projects are the only ones listed, and every other test project always runs.

To change it, edit the file and run the tests; `tests/SqlSource.Tests/Validation/` checks:

- Every line parses and every pattern matches at least one file of the repository, so a renamed folder cannot leave a dead line behind (`TestScopesFileTests`).
- Every file that a container project includes from outside its own folder, a linked file, a referenced project or an import, has a scope that runs that project (`ContainerProjectInputsTests`).  The check reads the project's `.csproj`, every `Directory.Build.props` and `Directory.Build.targets` from its folder up to the root, and every project under `tests/` that it references, directly or not, with the `Directory.Build` files above that project, and every file under `tests/` that an import of any of those names, `tests/Fixtures.props` among them.  A project that links a file whose scope is `none` or the other engine's fails there.
- In a container project, `RepositoryRoot.Path` throws when the path, or a file below it, has a scope that would let a change to it skip that project (`RepositoryRootTests`).  A read that bypasses `Path` is not caught, which is why [`tests/AGENTS.md`](../tests/AGENTS.md) forbids it.  [`tests/README.md`](../tests/README.md#reading-the-repository) says what to do when `Path` throws.

`pre-commit-validation.sh --plan <path> ...` prints what a run would do for those paths and runs nothing, which shows the effect of a line.

## Pulling the test images

`tools/pull-test-images.sh [image ...]` is a step of the `build` job of `build.yml` and of the `allocation-postgres` and `allocation-sqlserver` jobs of `nightly.yml`, and no check, and `pre-commit-validation.sh` does not run it.  It runs `docker pull` once for the default image of each engine, which it reads from the lines of `tests/DatabaseTests/TestHarness/DefaultImages.cs` that declare `Postgres` and `SqlServer`, and once for each image given as an argument, and prints `<image>: <n> s` for each.  The `build` job of the workflow runs it with no argument, after its build; each floor job pulls its own floor image with `docker pull`.  A person can run it to have every image before a first run of the tests, which on Apple silicon saves minutes of the first test.

## Recording scripts

None of these is a check, and a person runs them.  `pre-commit-validation.sh` and `build.yml` run none of them.  Each reads the default image of the engine it records from the line of `tests/DatabaseTests/TestHarness/DefaultImages.cs` that declares it, and fails when it finds none: both read `Postgres` and `SqlServer`.

- `tools/record-fixtures.sh [postgres|mssql]` records the cases of one describer's fixtures, or of both when it is given no engine; any other argument is a usage error.  It records on the default image of the engine, or on the image of `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` when the variable is set.  The procedures are [the PostgreSQL describer's fixtures](../tests/DatabaseTests/Postgres/README.md#the-describers-fixtures) and [the SQL Server describer's](../tests/DatabaseTests/SqlServer/README.md#the-describers-fixtures).
- `tools/describe-end-to-end.sh` writes every sidecar of both end-to-end projects, of both engines' realistic cases and of both example projects, `examples/PostgresExamples` and `examples/SqlServerExamples`, from nothing, on the default image of each engine alone; [PostgreSQL's end-to-end project](../tests/DatabaseTests/Postgres/README.md#writing-the-sidecars) and [SQL Server's](../tests/DatabaseTests/SqlServer/README.md#writing-the-sidecars) have the procedure.  It runs no test, since a project does not build until its sidecars are current, and has a part for each engine, which starts a container of its own with `docker run` and removes it.  PostgreSQL's applies `Realistic/schema.sql` and `Realistic/seed.sql` of `tests/DatabaseTests/Postgres` to a database `realistic`, the schemas and seeds of `app` and `billing`, and the examples' schema to a database `examples`, with `psql`.  It also builds the PostGIS image of the tests with `docker build`, from `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` on the default image's tag, with the PostGIS package that `PostgisContainer.Package` of the harness names and under the name that `PostgisContainer` gives it, keeps it, and starts a second container of it for the schema and seed of `gis`, which the end-to-end project's `EndToEnd/NetTopologySuite/` names.  SQL Server's starts its image with `--platform linux/amd64`, which an arm64 host runs under emulation, and applies `Realistic/schema.sql` and `Realistic/seed.sql` of `tests/DatabaseTests/SqlServer` to a database `realistic`, then the schemas and seeds of `app` and `billing` of its `EndToEnd/`, with `new-types.sql` after the seed of `app`, for the queries of `EndToEnd/Queries/`, `EndToEnd/NodaTime/Queries/` and `EndToEnd/NetTopologySuite/Queries/`, and the examples' schema to a database `examples`, with the image's `sqlcmd`, given `-I` so that `QUOTED_IDENTIFIER` is on, as SqlClient has it.  Each part then runs the tool from its source with `describe --force`.
