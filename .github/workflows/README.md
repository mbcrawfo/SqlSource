# Workflows

The GitHub Actions workflows of the repository.  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) says when a contributor meets them; [`AGENTS.md`](../AGENTS.md) holds the rules of changing them.  [`docs/publishing.md`](../../docs/publishing.md) covers versioning and releasing.

## The workflows

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main` or an `integration/*` branch, pushes to them | Calls `build.yml` and ends with the `ci` job |
| `build.yml` | Called by `ci.yml` and `publish.yml` | Seven jobs that start at once and need none of the others: `Lint`, the Docker-only checks; two halves of `format.sh --check`, `style` with `csharpier` and `analyzers`; a `Release` build and the tests with coverage, the two PostgreSQL projects and the two SQL Server projects on their default images among them, the tests of `tests/DatabaseTests/Postgres` alone again on PostgreSQL 14, and of `tests/DatabaseTests/SqlServer` alone again on SQL Server 2017, one job each; and `dotnet pack` of the two packages with the three package checks |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the two packages to nuget.org, and creates a GitHub release for a tag |
| `coverage-comment.yml` | A successful `ci.yml` run for a pull request | Posts the coverage report as a comment on the pull request, or updates the comment it posted before |
| `nightly.yml` | Every night, manual runs, pull requests that change the file | Restores, builds in `Release` and runs the tests of every project but `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61` and `tests/DatabaseTests/QueryMethodAllocation` on `windows-latest` and `macos-latest`; and on `ubuntu-latest`, the tests of `tests/DatabaseTests/Postgres` on PostgreSQL 15, 16 and 17 and of `tests/DatabaseTests/SqlServer` on SQL Server 2019 and 2022, one job each, and the allocation tests of `tests/DatabaseTests/QueryMethodAllocation` on each engine's default image, `allocation-postgres` and `allocation-sqlserver`, alone and without coverage |

## The test images

Each floor job of `build.yml` names its floor image once, as `POSTGRES_FLOOR_IMAGE` or `SQLSERVER_FLOOR_IMAGE` of the job's `env`, pulls it in a step of its own, "Pull test image", and runs the tests of one project on it.  Each leg of the matrices `test-postgres` and `test-sqlserver` of `nightly.yml` names its image in its `matrix.include`, pulls it in its own "Pull test image" step and runs the tests of one project on it, the same way.  The `build` job's step "Pull test images", and that of the two allocation jobs of `nightly.yml`, run `tools/pull-test-images.sh` after the build and before the first step that runs a test: it pulls the default image of each engine, and the log shows the seconds of each pull, so that the time of a pull is apart from the time of a test.  The pull does not run in the background during the build: tried that way, it saved no time, because it slowed the build by about as long as it took.

## Artifacts

Each run of `build.yml` uploads two artifacts: `packages`, the two `.nupkg` files, from the `package` job, and `coverage`, from the `build` job, the HTML, Cobertura and markdown reports.  The run's job summary also holds the coverage summary and, for each test project a step ran, its counts and its failed and skipped tests.  It lists no passed tests: a step's summary holds 1 MiB, the reporter cuts its output there, and GitHub then shows little or none of that step's summary.  The passed tests of the solution alone are past the limit.

This leaves a limitation of the reporter, which has no option to shorten or cap what it lists: each failed test is listed with its message and stack trace, about 3 KB, so a step where some hundreds of tests fail, as when a project's container does not start, passes 1 MiB again and its summary is lost.  The step's log still holds every failure.

## Continuous integration

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.

`coverage-comment.yml` always runs the version on `main`, never the one a pull request changes.  A change to it takes effect, and can first be tried, after it is merged.  A manual run of `publish.yml` on a branch runs the file but skips every job, because its `build` job runs only for a tag or `main`.

## Nightly

`ci.yml` runs on Linux alone.  `nightly.yml` is what runs the tests on Windows and macOS, where paths, quoting, exit codes and the kill of a process differ, and what runs the container tests on the supported database majors that `build.yml` does not: PostgreSQL 15, 16 and 17 and SQL Server 2019 and 2022, on Linux, and the only workflow that runs the allocation tests of `tests/DatabaseTests/QueryMethodAllocation`: its jobs `allocation-postgres` and `allocation-sqlserver` each run one engine's, on the default image, without coverage, which allocates in the process they measure, and a red one says a generated method may allocate more than a hand-written one ([how to read it](../../tests/DatabaseTests/QueryMethodAllocation/README.md)).  `build.yml` tests each engine's floor and default, so that the two workflows together test every supported major; [`AGENTS.md`](../AGENTS.md) holds the rule that keeps them so.  Its Windows and macOS job runs no check that needs Docker, none of `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61` and `tests/DatabaseTests/QueryMethodAllocation`, whose containers its runners cannot start, and packs nothing.  The workflow is no required check: a failure is mended in a pull request of its own.  To try a branch on both systems and every database major before it is merged, start the workflow by hand:

```bash
gh workflow run nightly.yml --ref <branch>
```

GitHub sends the result of a scheduled run to whoever last changed the schedule, and turns the schedule of a public repository off after 60 days without activity in the repository; it is turned on again on the workflow's page under Actions.
