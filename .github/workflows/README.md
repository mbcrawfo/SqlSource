# Workflows

The GitHub Actions workflows of the repository.  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) says when a contributor meets them; [`AGENTS.md`](../AGENTS.md) holds the rules of changing them.  [`docs/publishing.md`](../../docs/publishing.md) covers versioning and releasing.

## The workflows

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml` and ends with the `ci` job |
| `build.yml` | Called by `ci.yml` and `publish.yml` | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, the tests of `tests/SqlSource.Tests.Postgres` again on PostgreSQL 14, `dotnet pack`, and the three package checks |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the two packages to nuget.org, and creates a GitHub release for a tag |
| `coverage-comment.yml` | A successful `ci.yml` run for a pull request | Posts the coverage report as a comment on the pull request, or updates the comment it posted before |
| `nightly.yml` | Every night, manual runs, pull requests that change the file | Restores, builds in `Release` and runs the tests of every project but `tests/SqlSource.Tests.Postgres` on `windows-latest` and `macos-latest` |

## Artifacts

Each run of `build.yml` uploads two artifacts: `packages`, the two `.nupkg` files, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.

## Continuous integration

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.

`coverage-comment.yml` always runs the version on `main`, never the one a pull request changes.  A change to it takes effect, and can first be tried, after it is merged.  `publish.yml` cannot be tried from a branch either: it is first exercised on `main`.

## Nightly

`ci.yml` runs on Linux alone.  `nightly.yml` is what runs the tests on Windows and macOS, where paths, quoting, exit codes and the kill of a process differ.  It runs no check that needs Docker, not `tests/SqlSource.Tests.Postgres`, whose container its runners cannot start, and packs nothing, and it is no required check: a failure is mended in a pull request of its own.  To try a branch on both systems before it is merged, start the workflow by hand:

```bash
gh workflow run nightly.yml --ref <branch>
```

GitHub sends the result of a scheduled run to whoever last changed the schedule, and turns the schedule of a public repository off after 60 days without activity in the repository; it is turned on again on the workflow's page under Actions.
