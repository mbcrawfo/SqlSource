# SqlSource

C# SQL query source generator.

## Supported environments

The generator is compiled against Roslyn 4.8.0, so it loads in the .NET 8 SDK and later and in Visual Studio 2022 17.8 and later.  .NET 8 is the explicit floor for support; older SDKs and IDEs are not supported.

This is what a project that *uses* the generator needs.  Working on the generator itself needs the tools below.

## Required tools

| Tool | Version | Notes |
|----|----|----|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.401 or a later 10.0 release | Pinned in `global.json` |
| [Docker](https://docs.docker.com/get-started/get-docker/) | Any current release | Runs the pinned lint images used by the scripts in `tools/` |

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

## Checks

Run every check before committing:

```bash
./pre-commit-validation.sh
```

It verifies formatting, runs the linters, builds the solution and runs the tests.  It never rewrites files, runs every step even when one fails, and ends with a summary of what passed and failed.

Both `pre-commit-validation.sh` and `format.sh --check` restore packages in locked mode, as CI does, so a `packages.lock.json` that no longer matches its project fails the check.  After changing a package reference or version, update the lock files and commit them:

```bash
dotnet restore SqlSource.slnx
```

| Script | Does |
|----|----|
| `format.sh` | Rewrites C# and project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier |
| `format.sh --check` | Reports what `format.sh` would change, and rewrites nothing |
| `pre-commit-validation.sh` | `format.sh --check`, the four linters below, the build and the tests |

The scripts in `tools/` each run a linter from a pinned Docker image:

| Script | Checks |
|----|----|
| `tools/editorconfig-checker.sh` | Every file against `.editorconfig` |
| `tools/shellcheck.sh` | Shell scripts, with ShellCheck |
| `tools/shfmt.sh --check` | Shell script formatting; without `--check` it rewrites the files |
| `tools/actionlint.sh` | GitHub Actions workflows |
