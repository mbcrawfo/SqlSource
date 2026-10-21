# Tools

This folder holds the scripts that `pre-commit-validation.sh` and `.github/workflows/build.yml` run, the consumer project that one of them installs the package into, a script that pulls the test images, and two recording scripts, which are not checks.  [`CONTRIBUTING.md`](../CONTRIBUTING.md) says when to run them; [`AGENTS.md`](AGENTS.md) holds the traps of changing them.

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

This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly, Roslyn, the library that reads a solution, Npgsql and Microsoft.Data.SqlClient beside it, SqlClient's copies for Unix and Windows and its native SNI for Windows under `runtimes/`, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.

### `check-package-install.sh`

```bash
tools/check-package-install.sh artifacts/packages
```

This installs the package the way a consumer does.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.

The project's files are these.  `Consumer.csproj` and `Directory.Build.targets` are its MSBuild files, `Queries.cs` has the two types that the generator fills from the `.sql` files of `Queries/`, and `Program.cs` prints one line for each thing it checks.  The project uses a constant, a method with tokens, `SqlSourceGeneratorParameters` and `SqlSourceDialect`, each as a property and as metadata of an item, `Parameters` on the attribute, and the two markers `-- generator:` and `-- dialect:`, so it fails when the generator or either MSBuild file does not reach a consumer.  It sets `SqlSourceOutput` to `Sql`, so its queries need no sidecar.

Its `Directory.Build.targets` sets one of the two properties and has a target that adds a `.sql` file, each with its value on a line of its own, so it also fails when the package trims a value only where its targets are imported.  The SDK imports that file after the targets of a package.  Two of the project's lists of generator parameters have one word on each line, with the word that shows as the second, and one item names its dialect on one line and an option of the dialect on the next, so it fails when a value reaches the compiler cut at a line break.

The script then asks the project what the `sqlsource` tool asks one.  `SqlSourceImported` must be `true`, which shows that NuGet imported the package's props.  The target `SqlSourceWriteManifest`, run by name, must write a manifest that starts with its version, lists a `.sql` file, holds a dialect and a property that the project writes over several lines, trimmed, and does not list `Queries/AddedByATarget.sql`, the file that `Directory.Build.targets` adds from a hook of the build.  A target that is run by name does not run that hook, which is [TD-0025](../docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md); the build of the consumer compiled the file, and `expected-output.txt` has its query.

It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.

Last, it writes a web project, of `Microsoft.NET.Sdk.Web`, that references the package and has a `.sql` file and its sidecar, publishes it, and fails when either file is in its `bin` folder or in what `dotnet publish` wrote.  The Web SDK copies a `.json` file to both unless the package says otherwise, and the consumer, of the plain SDK, copies neither.  The project claims no file, so the sidecar can be empty.  Without an argument the script packs first, as the other does.

### `check-tool-install.sh`

```bash
tools/check-tool-install.sh artifacts/packages
```

This installs the tool the way a user does: `dotnet tool install` into a temporary folder outside the repository, from a feed that holds nothing else.  It then runs `sqlsource --version` and compares it with the package's version, runs `sqlsource --help`, and runs `sqlsource describe` in an empty folder, which must exit with 1 and report `SQLSRC201`.  A tool's package carries everything the tool loads, so this is what shows that all of it is there.

The last run sets `DOTNET_ROLL_FORWARD` to `LatestMajor`, which starts the tool on the newest runtime the machine has.  The tool targets .NET 8 and rolls forward on a machine that has only a later one, and this proves it wherever the script runs.  Without an argument the script packs first.

## Pulling the test images

`tools/pull-test-images.sh [image ...]` is a step of `build.yml` and no check, and `pre-commit-validation.sh` does not run it.  It runs `docker pull` once for the default image of each engine, which it reads from the lines of `tests/SqlSource.Testing/DefaultImages.cs` that declare `Postgres` and `SqlServer`, and once for each image given as an argument, and prints `<image>: <n> s` for each.  The workflow gives it the two floor images.  A person can run it to have every image before a first run of the tests, which on Apple silicon saves minutes of the first test.

## Recording scripts

None of these is a check, and a person runs them.  `pre-commit-validation.sh` and `build.yml` run none of them.  Each reads the default image of the engine it records from the line of `tests/SqlSource.Testing/DefaultImages.cs` that declares it, and fails when it finds none: `record-fixtures.sh` reads `Postgres` and `SqlServer`, and `describe-end-to-end.sh` reads `Postgres` alone.

- `tools/record-fixtures.sh [postgres|mssql]` records the cases of one describer's fixtures, or of both when it is given no engine; any other argument is a usage error.  It records on the default image of the engine, or on the image of `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` when the variable is set.  The procedures are [the PostgreSQL describer's fixtures](../tests/SqlSource.Tests.Postgres/README.md#the-describers-fixtures) and [the SQL Server describer's](../tests/SqlSource.Tests.SqlServer/README.md#the-describers-fixtures).
- `tools/describe-end-to-end.sh` writes every sidecar of the end-to-end queries, of the realistic cases and of `examples/PostgresExamples`, from nothing, on the default image alone; [the end-to-end project](../tests/SqlSource.Tests.Postgres/README.md#writing-the-sidecars) has the procedure.  It runs no test, since the project does not build until its sidecars are current: it starts a container itself with `docker run`, applies `Realistic/schema.sql` and `Realistic/seed.sql` of `tests/SqlSource.Tests.Postgres` to a database `realistic`, the schemas and seeds of `app` and `billing`, and the examples' schema to a database `examples`, with `psql`, and runs the tool from its source with `describe --force`.
