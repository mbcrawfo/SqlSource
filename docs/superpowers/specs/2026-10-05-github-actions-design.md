# GitHub Actions CI and release - design

Date: 2026-10-05

## Goal

Give the repository continuous integration and a release path:

- Every pull request to `main` and every push to `main` runs all of the repository's checks, builds, tests, and produces a NuGet package as an artifact.
- Code coverage is reported in the job summary, uploaded as an artifact, and posted on the pull request.
- Every build is stamped with a version made of a hand-maintained `major.minor.patch` and the GitHub Actions run number.
- A full release is published to nuget.org from a tag, and a beta can be published by hand, both through nuget.org trusted publishing.

## Decisions

| Decision | Choice | Reason |
|----|----|----|
| Source of `major.minor.patch` | `VersionPrefix` in `Directory.Build.props`, edited by hand | The required scheme (run-number build, fixed suffixes, tag-triggered releases) already defines most of the version; MinVer, Nerdbank.GitVersioning and release-please would mostly be overridden |
| Guard against a stale `VersionPrefix` | An `AGENTS.md` rule to compare it with the tags before opening a pull request, and a tag check in `publish.yml` | The file and the tags are two sources that must agree |
| Version shape | Run number in both the package version and the assembly version | nuget.org versions are immutable, so pushed betas need a unique part |
| Where published packages are built | Rebuilt inside `publish.yml` | One path for tag releases and manual betas, with no dependence on artifact retention |
| Coverage on pull requests | One sticky comment posted by `ci.yml` | No extra workflow and no third-party service |
| Publish guard | GitHub environment `nuget`, limited to `main` and `v*` tags, no approval step | The nuget.org policy is bound to the environment, so a run from another branch cannot obtain a key |
| GitHub Release | Created for each tag release | A changelog page per version at no ongoing cost |
| Sharing logic between workflows | Reusable workflow `build.yml` | One definition, every check a visible step, linted by `tools/actionlint.sh` |

## Out of scope

- Dependabot or Renovate configuration.
- A minimum coverage threshold that fails the build.
- Prerelease tags such as `v0.2.0-rc.1`.  Betas come only from the manual dispatch.
- Symbol packages.
- A check that a release tag points at a commit on `main`.

## Versioning

### Scheme

With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version | Informational version |
|----|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` | `0.1.0-pr-42.123+<sha>` |
| `main` | `0.1.0-beta.123` | `0.1.0.123` | `0.1.0-beta.123+<sha>` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` | `0.1.0+<sha>` |
| Local | `0.1.0-dev` | `0.1.0.0` | `0.1.0-dev+<sha>` |

Run numbers are counted per workflow.  A beta published by `publish.yml` therefore carries that workflow's run number, not the number on the `ci.yml` artifact for the same commit.

### `Directory.Build.props`

The hardcoded `AssemblyVersion` and `InformationalVersion` are replaced by:

- `VersionPrefix`, set to `0.1.0`.  It is the only hand-edited number.
- `BuildNumber`, defaulting to `0` when not set.
- `AssemblyVersion` and `FileVersion`, both `$(VersionPrefix).$(BuildNumber)`.
- `VersionSuffix`, defaulting to `dev` when it is empty and `$(CI)` is not `true`.  On a runner the workflow always sets it, to a value or to the empty string.
- `ContinuousIntegrationBuild`, `true` when `$(CI)` is `true`.

The package version and `InformationalVersion` are left to the SDK: it composes `VersionPrefix[-VersionSuffix]` and appends `+<commit sha>` to the informational version.

### How the workflows supply the values

`build.yml` sets `BuildNumber` and `VersionSuffix` as job-level environment variables.  MSBuild reads environment variables as properties, so the format check, the build, the tests and `dotnet pack --no-build` all see the same values, with no per-command flags to keep in agreement.

`BuildNumber` is `github.run_number`.  `VersionSuffix` is the `version-suffix` input of `build.yml`, chosen by the caller.

## Packaging

### `src/SqlSource/SqlSource.csproj`

- Metadata: `PackageId` `SqlSource`, a description, authors, `PackageLicenseExpression` `MIT`, the repository and project URLs, tags, and the repository `README.md` as the package readme.
- Analyzer layout: the built DLL is packed into `analyzers/dotnet/cs`.  `IncludeBuildOutput` is `false`, so there is no `lib/` folder.  `DevelopmentDependency` is `true`, and the package declares no dependencies, so nothing flows into a consumer's output.
- `dotnet pack` must succeed under the existing warnings-as-errors policy.  A NuGet pack warning is resolved within the rules documented in `Directory.Build.props`, not with a blanket `NoWarn`.

### `tests/SqlSource.Tests/SqlSource.Tests.csproj`

References the packages already pinned in `Directory.Packages.props`: `Microsoft.Testing.Extensions.CodeCoverage` and `GitHubActionsTestLogger`.  Both `packages.lock.json` files are updated and committed.

## Workflows

All three files live in `.github/workflows`.  Every action is pinned to a full commit SHA with its version in a trailing comment.  The default token permission is `contents: read`; a job adds only what it needs.

### `build.yml`

Reusable, triggered only by `workflow_call`.  Input: `version-suffix`, a string that may be empty.  One job on `ubuntu-latest`, building in the `Release` configuration:

1. Check out the code.  Set up the .NET SDK from `global.json` with NuGet caching keyed on the lock files.  Run `dotnet tool restore` and a locked `dotnet restore`.
2. Run the checks, each as its own step, every one running even when an earlier one failed:
   - `./format.sh --check`
   - `tools/editorconfig-checker.sh`
   - `tools/shellcheck.sh`
   - `tools/shfmt.sh --check`
   - `tools/actionlint.sh`
3. `dotnet build`.
4. `dotnet test` without rebuilding, only when the build passed, collecting coverage in Cobertura format and reporting through the GitHub Actions test logger.
5. `dotnet pack` without rebuilding.
6. ReportGenerator produces the `Html`, `Cobertura` and `MarkdownSummaryGithub` reports, limited to the `SqlSource` assembly.  The markdown is appended to the job summary.
7. Upload two artifacts: `packages`, holding the `.nupkg`, and `coverage`, holding the three reports.

### `ci.yml`

Triggers: `pull_request` targeting `main`, and `push` to `main`.  A newer run for the same pull request cancels the older one.

- `build` calls `build.yml`.  `version-suffix` is `pr-<number>.<run number>` for a pull request and `beta.<run number>` for a push.
- `coverage-comment` runs after `build` succeeds, for pull requests only, with `pull-requests: write`.  It is skipped when the head repository is a fork and when the actor is Dependabot, because those runs get a read-only token.  It downloads the `coverage` artifact and uses the `gh` CLI to create the coverage comment, or to update the existing one, which it finds by a hidden HTML marker.  It never checks out code.

### `publish.yml`

Triggers: `push` of a `v*` tag, and `workflow_dispatch` with no inputs.  The file name is fixed by the nuget.org trusted publishing policy.

- `build` calls `build.yml`.  `version-suffix` is empty for a tag and `beta.<run number>` for a dispatch.  The job is skipped unless the ref is a tag or `main`.
- `publish` runs after `build`, in the `nuget` environment, with `id-token: write`:
  1. Download the `packages` artifact.
  2. On a tag, fail unless the artifact is exactly one file named `SqlSource.<tag without the v>.nupkg`.  This is the check that the tag equals `VersionPrefix`.
  3. `NuGet/login` with `vars.NUGET_USER` exchanges the OIDC token for a temporary API key.
  4. `dotnet nuget push` with `--skip-duplicate`, so that re-running after a partial failure is safe.
- `release` runs after `publish`, for tags only, with `contents: write`.  It runs `gh release create` for the tag with generated notes and the `.nupkg` attached.

The login and the push stay in `publish.yml` itself, not in a reusable workflow, so that the OIDC claims match the policy.

The tag check runs after the build, not before it.  A mismatched tag costs one build, in exchange for checking the real artifact.

### External configuration

Already in place, and recorded in `README.md` for reference:

- nuget.org trusted publishing policy: owner `mbcrawfo`, repository `SqlSource`, workflow file `publish.yml`, environment `nuget`.
- GitHub environment `nuget`: deployment limited to the branch `main` and tags matching `v*`, no required reviewers, administrators cannot bypass, and a variable `NUGET_USER` holding the nuget.org profile name.

## Guidance and documentation

### `.github/AGENTS.md` (new)

- Every action is pinned to a full commit SHA, with the version in a trailing comment.  A tag or a branch is never used as a ref.
- When adding an action, look up its latest release and pin that.  Do not copy a SHA from another repository or from memory.
- `publish.yml` keeps its file name, and its publish job keeps the `nuget` environment: both are part of the nuget.org policy.  The login and push stay in that file.
- The check steps in `build.yml` mirror `pre-commit-validation.sh`.  A check added to one is added to the other in the same commit.
- Version values reach MSBuild as job-level environment variables, not as `-p:` flags.
- The maintenance footer, because the file names files.

### Root `AGENTS.md`

A new rule under "Pull requests": before creating a pull request, run `git fetch --tags origin` and compare `VersionPrefix` in `Directory.Build.props` with the highest `v*` tag.  If `VersionPrefix` is not greater than that tag, ask the user what the new version should be, and set it in the same pull request.  When the repository has no `v*` tags there is nothing to compare.

### `README.md`

- The version scheme table.
- How to pack and how to collect coverage locally.
- An overview of the three workflows.
- The release procedure: set `VersionPrefix`, merge, then push the tag `v<version>`.  A beta is published with "Run workflow" on `publish.yml` from `main`.
- The external configuration above.

### `docs/tech-debt/TD-0002`

Pull requests from forks and from Dependabot get no coverage comment, because their token is read-only.  The job summary and the artifact still carry the report.  The proposed fix is a separate `workflow_run` workflow that downloads the coverage artifact and comments with write access, hardened to treat the artifact as untrusted input.

## Verification

- `./pre-commit-validation.sh` exits zero on the finished tree.  Its actionlint step covers the three new workflows.
- `dotnet msbuild -getProperty` on `src/SqlSource/SqlSource.csproj` returns the versions in the scheme table for each case: local, pull request, `main` and tag.
- `dotnet pack` produces a `.nupkg` whose only assembly is `analyzers/dotnet/cs/SqlSource.dll`, with no `lib/` folder and no dependencies, and the DLL carries the expected assembly, file and informational versions.
- On the pull request that adds the workflows, `ci.yml` runs: every check passes, the job summary shows test results and coverage, the `packages` and `coverage` artifacts exist, and the coverage comment appears and is updated in place by a second push.
- After the merge, the `main` run produces a `-beta.<run>` package artifact.
- `publish.yml` cannot run until it is on `main`.  Its first test is a manual beta push after the merge; the tag path is first exercised by the first release.
