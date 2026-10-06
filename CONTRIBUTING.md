# Contributing to SqlSource

How to set up a development environment, build, test and check the code, and what CI runs on a pull request.  Versioning and releasing are covered in [docs/publishing.md](docs/publishing.md).

## Required tools

| Tool | Version | Notes |
|----|----|----|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.401 or a later 10.0 release | Pinned in `global.json` |
| [Docker](https://docs.docker.com/get-started/get-docker/) | Any current release | Runs the pinned lint images used by the scripts in `tools/` |
| [jq](https://jqlang.org/download/) | Any current release | Only for Claude Code: the commit hook in `.claude/hooks/` uses it to read the command being run |
| `unzip` | Any | `tools/check-package.sh` lists the package's contents with it.  Present on macOS and most Linux distributions. |

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

The solution has two test projects:

| Project | Roslyn | Runs |
|----|----|----|
| `tests/SqlSource.Tests` | A current version | Every test |
| `tests/SqlSource.Tests.RoslynFloor` | 4.8.0, the oldest supported | The generator driver tests: the files of `tests/SqlSource.Tests/Generator/`, compiled a second time |

A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both, so it may use only Roslyn API that 4.8.0 has, and the C# it hands to the compiler is C# 12 at most.

`tests/SqlSource.Tests` also uses the generator the way a consumer does: the types in `EndToEnd/` are compiled with the generator loaded, and the project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`, the MSBuild files the package ships.

### Coverage

```bash
dotnet test --solution SqlSource.slnx --results-directory artifacts/test-results --coverage --coverage-output-format cobertura
```

```bash
dotnet reportgenerator '-reports:artifacts/test-results/*.cobertura.xml' -targetdir:artifacts/coverage -reporttypes:Html -assemblyfilters:+SqlSource
```

Each test project writes its own Cobertura file, and the report merges them.  It is `artifacts/coverage/index.html`.

### Package

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

This builds in `Release` and writes `artifacts/packages/SqlSource.<version>-dev.nupkg`.

```bash
tools/check-package.sh artifacts/packages
```

This checks what the package holds: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  Without an argument it packs into a temporary folder first.

## Checks

Run every check before committing:

```bash
./pre-commit-validation.sh
```

It verifies formatting, runs the linters, builds the solution, runs the tests and checks the package.  It never rewrites files, runs every step even when one fails (the tests and the package check are skipped if the build fails), and ends with a summary of what passed and failed.

Both `pre-commit-validation.sh` and `format.sh --check` restore packages in locked mode, as CI does, so a `packages.lock.json` that no longer matches its project fails the check.  After changing a package reference or version, update the lock files and commit them:

```bash
dotnet restore SqlSource.slnx
```

| Script | Does |
|----|----|
| `format.sh` | Builds the generator, which `dotnet format` needs in order to compile the test project, then rewrites C# and project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier |
| `format.sh --check` | Reports what `format.sh` would change, and rewrites nothing |
| `pre-commit-validation.sh` | `format.sh --check`, the four linters below, the build, the tests and `tools/check-package.sh` |

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
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml`, posts the coverage report as a pull request comment, and ends with the `ci` job |
| `build.yml` | Called by the other two | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, `dotnet pack`, and the package check |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the package to nuget.org, and creates a GitHub release for a tag |

Each run uploads two artifacts: `packages`, the `.nupkg`, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.  Pull requests from forks and from Dependabot get no coverage comment; see `docs/tech-debt/TD-0002-no-coverage-comment-on-fork-pull-requests.md`.

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.
