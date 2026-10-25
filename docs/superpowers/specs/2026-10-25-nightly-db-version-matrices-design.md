# Nightly: every supported database major

## Intent

SqlSource says it describes PostgreSQL 14 or later and SQL Server 2017 or later.  CI tests only the floor and the default of each engine (PostgreSQL 14 and 18, SQL Server 2017 and 2025), so nothing tests the majors between them.  The nightly run gets one Linux job per engine that runs that engine's container tests on every supported major CI leaves out.  Between them, CI and the nightly run then cover every supported major.

## Goals

- `nightly.yml` runs `tests/SqlSource.Tests.Postgres` on PostgreSQL 15, 16 and 17, and `tests/SqlSource.Tests.SqlServer` on SQL Server 2019 and 2022.  Each image is pinned to its latest minor or CU.
- `.github/AGENTS.md` gets a rule that `build.yml` and `nightly.yml` together cover every supported major of each engine.
- The schedule moves to about 01:00 EST.

## Non-goals

- The existing nightly `test` job (Windows and macOS, the projects that need no container) stays as it is.  Linux stays out of it, because CI covers Linux.
- No container tests on Windows or macOS.  GitHub's hosted macOS runners have no Docker, and its Windows runners run only Windows containers.  Remote Docker hosts, tunnels and native database installs were considered and rejected.
- `build.yml` is not changed.
- `tests/SqlSource.Tests.Postgres.Npgsql8` and `tests/SqlSource.Tests.SqlServer.SqlClient61` do not run per major.  They test the oldest supported client libraries, not server versions, and CI runs them on the default images.

## Constraints

- Rule 5 of the root `AGENTS.md`: every image is pinned to a major and a minor for PostgreSQL, and to `<year>-CU<n>[-GDR<n>]-ubuntu-<version>` for SQL Server.  Every SQL Server image the tests run has a row in `SqlServerContainer.ImageBuilds` (`tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs`).  Without one, `SqlServerContainerTests` and `SessionTests` fail.
- The rules of `.github/AGENTS.md`: actions are pinned to the SHAs `build.yml` already uses, lines are at most 120 characters, and `tools/actionlint.sh` passes.
- A pull request that changes `nightly.yml` runs the whole workflow, the new jobs included, so the change is tried before it is merged.

## Approaches

- **A. Two matrix jobs in `nightly.yml`, with the images listed inline. Chosen.**  It is the smallest change and repeats the pattern of the floor jobs in `build.yml`.  The new rule in `.github/AGENTS.md` is the only thing that keeps the two workflows in sync.
- B. A reusable `workflow_call` workflow that runs one project on one image, called by the floor jobs in `build.yml` and by the nightly matrices.  It removes about 20 duplicated lines, but it changes `build.yml` and the CI job names.
- C. One list of supported images that both workflows read their matrices from, with a test that fails when a major is missing.  It enforces the sync, but it has the most moving parts and rewrites the floor jobs.

## Decisions

- **Images**, checked on 2026-10-25 against Docker Hub and MCR as the latest of each major:
  - `postgres:15.19`, `postgres:16.15` and `postgres:17.11`
  - `mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04`
  - `mcr.microsoft.com/mssql/server:2022-CU27-GDR1-ubuntu-22.04`
- **Jobs.**  Two new jobs in `nightly.yml`, one per engine, on `ubuntu-latest`, each with `fail-fast: false` and a `matrix.include` of `{ major, image }`.  Each leg is named after its version, such as `Test on PostgreSQL 16`.  The steps are those of `test-postgres-floor` and `test-sqlserver-floor` in `build.yml`: check out, set up .NET with the cache keyed on that project's `packages.lock.json`, build the project, pull the image in a step of its own, and test with `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` set to it.
- **Schedule.**  `cron: '17 6 * * *'`, 06:17 UTC, which keeps the off-the-hour minute.  That is 01:17 EST, or 02:17 during daylight saving time, since cron has no time zone.  The comment says so.
- **`ImageBuilds`** gets rows for the 2019 and 2022 images, with the build each server reports from `SERVERPROPERTY('ProductVersion')`.  Its comment changes from "the default and the floor" to every image the tests run in CI or the nightly run.
- **The rule in `.github/AGENTS.md`.**  It replaces the current `nightly.yml` bullet or sits next to it.  It says:
  - CI covers each engine's floor and default, and the matrices in `nightly.yml` cover every supported major between them.  Together they cover every supported major, each once.
  - Whoever moves a default or a floor, or adds a major, changes the other workflow in the same pull request.  For example, when the default moves to PostgreSQL 19, PostgreSQL 18 joins the nightly matrix.
  - A SQL Server image added to a matrix gets its `ImageBuilds` row.
- **Documentation that names the images or the tested versions, mine to update:**
  - The header comment of `nightly.yml`.
  - `.github/workflows/README.md`: the table row and the Nightly section, which now says the nightly run does run containers, on Linux.
  - Rule 5 of the root `AGENTS.md`: the places that name each pinned image.
  - The image sections of `tests/SqlSource.Tests.Postgres/README.md` and `tests/SqlSource.Tests.SqlServer/README.md`.
  - "tested on 14 and 18" in `docs/manual/postgresql.md` and "tested on 2017 and 2025" in `docs/manual/sqlserver.md`, which become: every supported major is tested, the floor and the newest on every change and the others every night.

## Dependency

PostgreSQL 17 fails two tests in `tests/SqlSource.Tests.Postgres/Describer/ExpressionTests.cs` whose version gates put at 18 a plan change that came earlier.  A separate session fixes them in a PR of its own.  The PostgreSQL 17 leg passes only once that PR is merged, so this change merges after it, or is rebased onto it before its own nightly run is read.  PostgreSQL 15 and 16 passed locally.  SQL Server 2019 and 2022 failed locally only on the missing `ImageBuilds` rows.

## Left to the builder

- The exact build strings of the two `ImageBuilds` rows, read from each server.
- Each job's `timeout-minutes`; 15 is the floor jobs' value.
- The wording of comments and documentation, within the rules above.
