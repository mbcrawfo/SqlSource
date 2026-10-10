# Contributing to SqlSource

How to set up a development environment, build, test and check the code, and what CI runs on a pull request.  Versioning and releasing are covered in [docs/publishing.md](docs/publishing.md).

## Required tools

| Tool | Version | Notes |
|----|----|----|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.401 or a later 10.0 release | Pinned in `global.json` |
| [Docker](https://docs.docker.com/get-started/get-docker/) | Any current release | Runs the pinned lint images used by the scripts in `tools/` |
| [jq](https://jqlang.org/download/) | Any current release | Only for Claude Code: the commit hook in `.claude/hooks/` uses it to read the command being run |
| `unzip` | Any | `tools/check-package.sh` lists the contents of the packages with it.  Present on macOS and most Linux distributions. |

CSharpier and ReportGenerator are local .NET tools pinned in `dotnet-tools.json`.  Restore them once after cloning:

```bash
dotnet tool restore
```

## Build and test

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

The solution has two projects that ship and three test projects:

| Project | Is |
|----|----|
| `src/SqlSource` | The source generator, and the package `SqlSource` |
| `src/SqlSource.Tool` | The `sqlsource` command, a console application that references the generator's project and shares its code, and the package `SqlSource.Tool` |

| Project | Roslyn | Runs |
|----|----|----|
| `tests/SqlSource.Tests` | A current version | Every test of the generator |
| `tests/SqlSource.Tests.RoslynFloor` | 4.8.0, the oldest supported | The generator driver tests: the files of `tests/SqlSource.Tests/Generator/`, compiled a second time |
| `tests/SqlSource.Tool.Tests` | A current version | The tests of the tool, which run the whole command in process with their own output, working directory and environment |

A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both, so it may use only Roslyn API that 4.8.0 has, and the C# it hands to the compiler is C# 12 at most.

`tests/SqlSource.Tests` also uses the generator the way a consumer does: the types in `EndToEnd/` are compiled with the generator loaded, and the project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`, the MSBuild files the package ships.  It gets them by path and the generator through a project reference; `tools/check-package-install.sh`, under Package below, is what installs the packed package.

`tests/SqlSource.Tool.Tests/Fixtures/Projects/` holds small projects that are no part of the solution.  `ManifestTargetTests` and `FixtureProjectTests` copy one to a temporary folder outside the repository, with the package's two MSBuild files and `global.json` beside it, and run `dotnet msbuild` on it for real, the second through the tool's own code, so these tests need the SDK that `global.json` pins and take a second or two each.  `FixtureProjectTests` also asks MSBuild about `tests/SqlSource.Tests` where it is.  A fixture is never built or restored, and a test leaves nothing in the repository.  A file under that folder is copied to the test project's output as it is; add a project there as a folder with an `App.csproj` that imports `../build/SqlSource.props` and `../build/SqlSource.targets`.

To run the tool from its source:

```bash
dotnet run --project src/SqlSource.Tool -- describe
```

### Coverage

```bash
dotnet test --solution SqlSource.slnx --results-directory artifacts/test-results --coverage --coverage-output-format cobertura
```

```bash
dotnet reportgenerator '-reports:artifacts/test-results/*.cobertura.xml' -targetdir:artifacts/coverage -reporttypes:Html '-assemblyfilters:+SqlSource;+SqlSource.Tool'
```

Each test project writes its own Cobertura file, and the report merges them.  It is `artifacts/coverage/index.html`.

### Package

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

This builds in `Release` and writes two packages with one version, `artifacts/packages/SqlSource.<version>-dev.nupkg` and `artifacts/packages/SqlSource.Tool.<version>-dev.nupkg`.

```bash
tools/check-package.sh artifacts/packages
```

This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly, Roslyn and the library that reads a solution beside it, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.

```bash
tools/check-package-install.sh artifacts/packages
```

This installs the package the way a consumer does.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.  The project uses a constant, a method with tokens, `SqlSourceGeneratorParameters` and `SqlSourceDialect`, each as a property and as metadata of an item, `Parameters` on the attribute, and the two markers `-- generator:` and `-- dialect:`, so it fails when the generator or either MSBuild file does not reach a consumer.  Its `Directory.Build.targets` sets one of the two properties and has a target that adds a `.sql` file, each with its value on a line of its own, so it also fails when the package trims a value only where its targets are imported.  Two of its lists of generator parameters have one word on each line, with the word that shows as the second, and one item names its dialect on one line and an option of the dialect on the next, so it fails when a value reaches the compiler cut at a line break.  It then asks the project what the `sqlsource` tool asks one: `SqlSourceImported` must be `true`, which shows that NuGet imported the package's props, and the target `SqlSourceWriteManifest`, run by name, must write a manifest that starts with its version, lists a `.sql` file, holds a dialect and a property that the project writes over several lines, trimmed, and does not list the file that `Directory.Build.targets` adds from a hook of the build.  It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.  Without an argument the script packs first, as the other does.

A change to what the package gives a consumer through its MSBuild files adds a line to `tools/package-install/Program.cs` and to the expected output.

```bash
tools/check-tool-install.sh artifacts/packages
```

This installs the tool the way a user does: `dotnet tool install` into a temporary folder outside the repository, from a feed that holds nothing else.  It then runs `sqlsource --version` and compares it with the package's version, runs `sqlsource --help`, and runs `sqlsource describe` in an empty folder, which must exit with 1 and report `SQLSRC201`.  A tool's package carries everything the tool loads, so this is what shows that all of it is there.  The last run sets `DOTNET_ROLL_FORWARD` to `LatestMajor`, which starts the tool on the newest runtime the machine has: the tool targets .NET 8 and rolls forward on a machine that has only a later one, and this proves it wherever the script runs.  Without an argument the script packs first.

## Checks

Run every check before committing:

```bash
./pre-commit-validation.sh
```

It verifies formatting, runs the linters, builds the solution, runs the tests, checks the two packages, installs the generator's into a project and installs the tool.  It never rewrites files, runs every step even when one fails (the tests and the three package checks are skipped if the build fails), and ends with a summary of what passed and failed.

Both `pre-commit-validation.sh` and `format.sh --check` restore packages in locked mode, as CI does, so a `packages.lock.json` that no longer matches its project fails the check.  After changing a package reference or version, update the lock files and commit them:

```bash
dotnet restore SqlSource.slnx
```

| Script | Does |
|----|----|
| `format.sh` | Builds the generator, which `dotnet format` needs in order to compile the test project, then rewrites C# and project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier |
| `format.sh --check` | Reports what `format.sh` would change, and rewrites nothing |
| `pre-commit-validation.sh` | `format.sh --check`, the four linters below, the build, the tests, `tools/check-package.sh`, `tools/check-package-install.sh` and `tools/check-tool-install.sh` |

The scripts in `tools/` each run a linter from a pinned Docker image:

| Script | Checks |
|----|----|
| `tools/editorconfig-checker.sh` | Every file against `.editorconfig` |
| `tools/shellcheck.sh` | Shell scripts, with ShellCheck |
| `tools/shfmt.sh --check` | Shell script formatting; without `--check` it rewrites the files |
| `tools/actionlint.sh` | GitHub Actions workflows |

## Continuous integration

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml` and ends with the `ci` job |
| `build.yml` | Called by `ci.yml` and `publish.yml` | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, `dotnet pack`, and the three package checks |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the two packages to nuget.org, and creates a GitHub release for a tag |
| `coverage-comment.yml` | A successful `ci.yml` run for a pull request | Posts the coverage report as a comment on the pull request, or updates the comment it posted before |

Each run uploads two artifacts: `packages`, the two `.nupkg` files, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.

`coverage-comment.yml` always runs as it is on `main`, never as a pull request changes it.  A change to it takes effect, and can first be tried, after it is merged.

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.
