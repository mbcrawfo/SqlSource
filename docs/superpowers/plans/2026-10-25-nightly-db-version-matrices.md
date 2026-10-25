# Nightly database version matrices: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `nightly.yml` runs the container tests on every supported database major that CI does not, so that CI and the nightly run together cover every supported major.

**Architecture:** two new Linux matrix jobs in `nightly.yml`, one per engine, whose legs copy the floor jobs of `build.yml`.  The SQL Server images get rows in `SqlServerContainer.ImageBuilds`.  A rule in `.github/AGENTS.md` keeps the two workflows in step, and every document that names the pinned images or the tested versions is updated.

**Tech Stack:** GitHub Actions, .NET 10 tests (xunit v3, Testcontainers), `tools/actionlint.sh`.

**Spec:** `docs/superpowers/specs/2026-10-25-nightly-db-version-matrices-design.md`

## Global constraints

- Images, exactly:
  - `postgres:15.19`, `postgres:16.15` and `postgres:17.11`
  - `mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04`
  - `mcr.microsoft.com/mssql/server:2022-CU27-GDR1-ubuntu-22.04`
- Schedule: `cron: '17 6 * * *'`.
- The existing `test` job of `nightly.yml` and all of `build.yml` stay unchanged.
- Per major, run only `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.SqlServer`.
- Actions are pinned to the same SHA and version comment that `build.yml` uses: checkout `3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1` and setup-dotnet `a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0`.
- Workflow lines are at most 120 characters.  `tools/actionlint.sh` and `tools/editorconfig-checker.sh` pass.
- Follow root `AGENTS.md`, `.github/AGENTS.md` and `tests/AGENTS.md`.  Every commit passes `./pre-commit-validation.sh`.

## Review focus

1. **A leg whose image is not the one it is named for.**  A copy-paste slip, such as `Test on PostgreSQL 16` pulling `postgres:17.11`, passes silently.  `major` and `image` must agree in every `include` entry.  `PostgresContainerTests` checks the tag's form, not which leg it belongs to, so this is checked by reading.
2. **The environment variable not reaching the test.**  If `SQLSOURCE_TESTS_*_IMAGE` is set on the wrong step, or misspelled, every leg silently tests the default image.  Task 3 Step 5 checks it from the nightly run's log.
3. **A SQL Server row whose build is a guess.**  The rows of `ImageBuilds` must be read from the running server, never from release notes.  `Server_OfTheImage_IsTheBuildTheMapRecords` proves each row.
4. **A cache key shared by both matrices.**  Each job keys its `setup-dotnet` cache on its own project's `packages.lock.json`, as the floor jobs do (the `.github/AGENTS.md` rule on cache keys).
5. **The PostgreSQL 17 leg failing in the PR's own nightly run.**  It fails until the separate PostgreSQL 17 `ExpressionTests` fix is on `main`.  Task 3 waits for that fix before pushing.

---

### Task 1: `ImageBuilds` rows for SQL Server 2019 and 2022

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs` (the `ImageBuilds` dictionary and its comment)
- Modify: `tests/SqlSource.Tests.SqlServer/README.md` ("The images": "each of the two images", and the places that name the tags)

**Interfaces:**
- Produces: two `ImageBuilds` keys equal to the two SQL Server images in the global constraints.  Task 2's matrix uses those exact strings.

- [ ] **Step 1: Run the project on 2019 and see it fail**

Run: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04 dotnet test --project tests/SqlSource.Tests.SqlServer --configuration Release`

Expected: exactly 3 failures: `Image_OfTheRun_IsInTheMapOfBuilds`, `Server_OfTheImage_IsTheBuildTheMapRecords` and `SessionTests.OpenAsync_Container_GivesTheVersionTheDriverAndTheSettings`, each because the key is missing.  Any other failure is a new problem: stop and report it.

- [ ] **Step 2: Read each image's build from its server**

For each of the two images, start a container and run `SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))`, for example through `docker run` and `/opt/mssql-tools*/bin/sqlcmd`.  Record the two strings.

- [ ] **Step 3: Add the two rows to `ImageBuilds`**

Add one row per image with the build from Step 2, ordered by year with the others.  Change the comment above it from "each image the tests run, the default and the floor" to say it holds every image that CI or the nightly run tests.

- [ ] **Step 4: Update `tests/SqlSource.Tests.SqlServer/README.md`**

In "The images":
- Name the two new images, and say that `.github/workflows/nightly.yml` holds them.
- Say that SQL Server 2019 and 2022 run every night.
- Change "each of the two images" to every image of CI and the nightly run.

Moving a nightly image is like moving the floor: change its tag and its `ImageBuilds` row, and run the tests on it.

- [ ] **Step 5: Run the project on 2019, 2022 and the default, and see it pass**

Run the Step 1 command, then the same with the 2022 image, then `dotnet test --project tests/SqlSource.Tests.SqlServer --configuration Release` with no variable.

Expected: 0 failures on each.

- [ ] **Step 6: Commit**

Run `./pre-commit-validation.sh`, and see it pass.

```bash
git add tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs tests/SqlSource.Tests.SqlServer/README.md
git commit -m "Record the builds of SQL Server 2019 and 2022"
```

---

### Task 2: the nightly matrices, the schedule, the sync rule and the documents

**Files:**
- Modify: `.github/workflows/nightly.yml`
- Modify: `.github/AGENTS.md` (the `workflows/nightly.yml` bullet and the `build.yml` bullet's mention of floor jobs)
- Modify: `.github/workflows/README.md` (the `nightly.yml` table row, "The test images" and "Nightly")
- Modify: `AGENTS.md` (rule 5's list of the places that name each pinned image)
- Modify: `tests/SqlSource.Tests.Postgres/README.md` ("The image")
- Modify: `docs/manual/postgresql.md:23` and `docs/manual/sqlserver.md:26`

**Interfaces:**
- Consumes: the two SQL Server images that Task 1 put in `ImageBuilds`.

- [ ] **Step 1: Change the schedule and the header comment of `nightly.yml`**

The cron becomes `'17 6 * * *'`, with the comment: 06:17 UTC is 01:17 EST and 02:17 EDT, since cron has no time zone, and it stays off the hour.  The header comment drops "no test that needs a container" and says the new jobs run on Linux, together with what CI covers.  Keep the parts about Windows and macOS.

- [ ] **Step 2: Add the jobs `test-postgres` and `test-sqlserver` to `nightly.yml`**

Each job:
- runs on `ubuntu-latest`, with `timeout-minutes: 15` and `strategy.fail-fast: false`;
- has a `matrix.include` of `{ major, image }`: PostgreSQL `15`/`16`/`17`, SQL Server `2019`/`2022`, each with its image from the global constraints;
- is named `Test on PostgreSQL ${{ matrix.major }}` or `Test on SQL Server ${{ matrix.major }}`;
- sets `PROJECT` in its `env`.

The steps are those of `test-postgres-floor` and `test-sqlserver-floor` in `build.yml`, in the same order:
1. Check out, with `persist-credentials: false`.
2. Set up .NET, with `cache-dependency-path` set to the project's `packages.lock.json`.
3. Build.
4. Pull the test image, `docker pull --quiet "$IMAGE"`, with `IMAGE: ${{ matrix.image }}` in the step's `env`.
5. Test, with `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE: ${{ matrix.image }}` in the step's `env`.

Add a comment above each job: CI runs the floor and the default, this runs every supported major between them, and the `.github/AGENTS.md` rule keeps the two in step.  The existing `test` job keeps `shell: bash` in its own `defaults`.  The new jobs are Linux jobs, so they need none.

- [ ] **Step 3: Lint**

Run: `tools/actionlint.sh && tools/editorconfig-checker.sh`

Expected: both exit 0 with no findings.

- [ ] **Step 4: Write the sync rule in `.github/AGENTS.md`**

Replace the `workflows/nightly.yml` bullet with two bullets:
- The existing rule that a new test project needing no container is named in the Test step of the `test` job.
- **`build.yml` and `nightly.yml` together cover every supported major of each engine, each once.**
  - The floor jobs of `build.yml` and its default-image run cover the floor and the default.  The matrices of `nightly.yml` cover every supported major between them, each pinned as rule 5 of the root `AGENTS.md` says.
  - A pull request that moves a default or a floor, or that adds or drops a supported major, changes the other workflow in the same pull request.  For example, when the default moves to PostgreSQL 19, PostgreSQL 18 joins the nightly matrix.
  - A SQL Server image added to a matrix gets its row of `SqlServerContainer.ImageBuilds`.

In the `build.yml` bullet, nothing changes but the reference, if it would otherwise read as if the floor jobs were the only image pins outside the script.

- [ ] **Step 5: Update the other documents**

- `.github/workflows/README.md`:
  - The `nightly.yml` row adds the two matrices on `ubuntu-latest`.
  - "The test images" says the nightly jobs pin each image in their matrix.
  - "Nightly" says it now runs the container projects on Linux, on PostgreSQL 15 to 17 and SQL Server 2019 and 2022, and that CI and the nightly run together cover every supported major.  The rest stays.
- Root `AGENTS.md` rule 5: after the floor images, add the nightly images: the three PostgreSQL tags are in `.github/workflows/nightly.yml` and `tests/SqlSource.Tests.Postgres/README.md`, and the two SQL Server tags in `.github/workflows/nightly.yml`, `tests/SqlSource.Tests.SqlServer/README.md` and their rows of `ImageBuilds`.  Nothing is recorded on them.  Moving one changes the places that name it and runs the tests on it.
- `tests/SqlSource.Tests.Postgres/README.md`, "The image": name the three nightly images, held by `.github/workflows/nightly.yml`.  Moving one is like moving the floor.
- The manuals: "SqlSource is tested on 14 and 18" and "tested on 2017 and 2025" become that every supported major is tested, the oldest and the newest on every change and the others every night.  Keep the rest of each line.

- [ ] **Step 6: Commit**

Run `./pre-commit-validation.sh`, and see it pass.

```bash
git add .github AGENTS.md tests/SqlSource.Tests.Postgres/README.md docs/manual/postgresql.md docs/manual/sqlserver.md
git commit -m "Run the container tests on every supported major every night"
```

---

### Task 3: try it and open the pull request

**Files:** none changed, unless the review or the run finds something.

- [ ] **Step 1: Wait for the PostgreSQL 17 fix**

Check whether the pull request of the session "Fix PostgreSQL 17 version gates in ExpressionTests" is merged into `main`, with `gh pr list --state merged --search "PostgreSQL 17"` or by asking the parent session.  If it is not merged, stop and say so.  Pushing now would show a red PostgreSQL 17 leg that Auto-fix might try to mend, in parallel with that session.

- [ ] **Step 2: Bring the branch up to date**

Sync with `main`: the `sync_with_base_branch` tool in a worktree the app made, or else `git fetch origin && git merge origin/main`.  Then run `./pre-commit-validation.sh`, and see it pass.

- [ ] **Step 3: Check the version**

Run `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If there is a tag and `VersionPrefix` in `Directory.Build.props` is not greater than it, ask the user what the new version should be.  On 2026-10-25 there was no tag.

- [ ] **Step 4: Push and open the PR**

The PR body says:
- what the change does;
- that the PR's own nightly run tries the new jobs;
- the local results on each image;
- that the cron changed.

End the body with the attribution line.  Bind the PR with the `ccd_pr` tools and turn on Auto-fix (`CLAUDE.local.md`).

- [ ] **Step 5: Read the PR's nightly run**

Expected: `Test on PostgreSQL 15`, `16` and `17` and `Test on SQL Server 2019` and `2022` pass, and the Windows and macOS jobs pass as before.  In each leg's Test log, check that the server version the tests report is that leg's major (Review focus 2).
