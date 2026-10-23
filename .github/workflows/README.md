# Workflows

The GitHub Actions workflows of the repository.  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) says when a contributor meets them; [`AGENTS.md`](../AGENTS.md) holds the rules of changing them.  [`docs/publishing.md`](../../docs/publishing.md) covers versioning and releasing.

## The workflows

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml` and ends with the `ci` job |
| `build.yml` | Called by `ci.yml` and `publish.yml` | Seven jobs that start at once and need none of the others: `Lint`, the Docker-only checks; two halves of `format.sh --check`, `style` with `csharpier` and `analyzers`; a `Release` build and the tests with coverage, the two PostgreSQL projects and the two SQL Server projects on their default images among them; the tests of `tests/SqlSource.Tests.Postgres` alone again on PostgreSQL 14, and of `tests/SqlSource.Tests.SqlServer` alone again on SQL Server 2017, one job each; and `dotnet pack` of the two packages with the three package checks |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the two packages to nuget.org, and creates a GitHub release for a tag |
| `coverage-comment.yml` | A successful `ci.yml` run for a pull request | Posts the coverage report as a comment on the pull request, or updates the comment it posted before |
| `nightly.yml` | Every night, manual runs, pull requests that change the file | Restores, builds in `Release` and runs the tests of every project but `tests/SqlSource.Tests.Postgres`, `tests/SqlSource.Tests.Postgres.Npgsql8`, `tests/SqlSource.Tests.SqlServer` and `tests/SqlSource.Tests.SqlServer.SqlClient61` on `windows-latest` and `macos-latest` |

## The test images

Each floor job of `build.yml` names its floor image once, as `POSTGRES_FLOOR_IMAGE` or `SQLSERVER_FLOOR_IMAGE` of the job's `env`, pulls it in a step of its own, "Pull test image", and runs the tests of one project on it.  The `build` job's step "Pull test images" runs `tools/pull-test-images.sh` after the build and before the first step that runs a test: it pulls the default image of each engine, and the log shows the seconds of each pull, so that the time of a pull is apart from the time of a test.  The pull does not run in the background during the build, because that slowed the build by more than the pull took.

## Artifacts

Each run of `build.yml` uploads two artifacts: `packages`, the two `.nupkg` files, from the `package` job, and `coverage`, from the `build` job, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.

## Continuous integration

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.

`coverage-comment.yml` always runs the version on `main`, never the one a pull request changes.  A change to it takes effect, and can first be tried, after it is merged.  A manual run of `publish.yml` on a branch runs the file but skips every job, because its `build` job runs only for a tag or `main`.

## Nightly

`ci.yml` runs on Linux alone.  `nightly.yml` is what runs the tests on Windows and macOS, where paths, quoting, exit codes and the kill of a process differ.  It runs no check that needs Docker, none of `tests/SqlSource.Tests.Postgres`, `tests/SqlSource.Tests.Postgres.Npgsql8`, `tests/SqlSource.Tests.SqlServer` and `tests/SqlSource.Tests.SqlServer.SqlClient61`, whose containers its runners cannot start, and packs nothing, and it is no required check: a failure is mended in a pull request of its own.  To try a branch on both systems before it is merged, start the workflow by hand:

```bash
gh workflow run nightly.yml --ref <branch>
```

GitHub sends the result of a scheduled run to whoever last changed the schedule, and turns the schedule of a public repository off after 60 days without activity in the repository; it is turned on again on the workflow's page under Actions.
