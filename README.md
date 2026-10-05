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
| [jq](https://jqlang.org/download/) | Any current release | Only for Claude Code: the commit hook in `.claude/hooks/` uses it to read the command being run |

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

### Coverage

```bash
dotnet test --solution SqlSource.slnx --results-directory artifacts/test-results --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

```bash
dotnet reportgenerator -reports:artifacts/test-results/coverage.cobertura.xml -targetdir:artifacts/coverage -reporttypes:Html -assemblyfilters:+SqlSource
```

The report is `artifacts/coverage/index.html`.

### Package

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

This builds in `Release` and writes `artifacts/packages/SqlSource.<version>-dev.nupkg`.

## Checks

Run every check before committing:

```bash
./pre-commit-validation.sh
```

It verifies formatting, runs the linters, builds the solution and runs the tests.  It never rewrites files, runs every step even when one fails (the tests are skipped if the build fails), and ends with a summary of what passed and failed.

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

## Versioning

`VersionPrefix` in `Directory.Build.props` is the `major.minor.patch` of the next release, and the only version number edited by hand.  The workflows add the rest.  With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version |
|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` |
| `main` | `0.1.0-beta.123` | `0.1.0.123` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` |
| Local | `0.1.0-dev` | `0.1.0.0` |

The informational version is the package version followed by `+<commit sha>`.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.

## Continuous integration

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml`, posts the coverage report as a pull request comment, and ends with the `ci` job |
| `build.yml` | Called by the other two | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, and `dotnet pack` |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the package to nuget.org, and creates a GitHub release for a tag |

Each run uploads two artifacts: `packages`, the `.nupkg`, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.  Pull requests from forks and from Dependabot get no coverage comment; see `docs/tech-debt/TD-0002-no-coverage-comment-on-fork-pull-requests.md`.

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.

## Releasing

Packages go to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).  The repository holds no API key.

### Release

1. Set `VersionPrefix` in `Directory.Build.props` to the version being released, and merge that to `main`.
2. Tag the merged commit and push the tag:

   ```bash
   git tag v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

3. `publish.yml` builds the tag, fails if the tag is not `v<VersionPrefix>`, pushes `SqlSource.0.2.0.nupkg` to nuget.org and creates the GitHub release.
4. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.

### Beta

Run `publish.yml` by hand on `main`, from the Actions tab or with:

```bash
gh workflow run publish.yml --ref main
```

It publishes `<VersionPrefix>-beta.<run>`.  A run started from any other branch builds and publishes nothing.

### External configuration

| Where | Setting | Value |
|----|----|----|
| nuget.org trusted publishing policy | Repository owner and repository | `mbcrawfo`, `SqlSource` |
| | Workflow file | `publish.yml` |
| | Environment | `nuget` |
| GitHub environment `nuget` | Deployment branches and tags | Branch `main`, tags `v*` |
| | Required reviewers | None |
| | Allow administrators to bypass | Off |
| | Variable `NUGET_USER` | The nuget.org profile name, not the email address |
| GitHub ruleset `main` | Required status check | `ci` |
