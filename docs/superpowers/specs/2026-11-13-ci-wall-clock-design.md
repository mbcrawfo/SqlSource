# Cutting the wall-clock time of CI

## Intent

A CI run of a pull request takes 10 to 17 minutes, and every change waits on it.  The owner wants it at 7 to 8 minutes
without testing less on a pull request than is needed, so that the wait between a push and a green check is shorter.

## What the runs show

Ten runs of `ci.yml` from 2026-11-12 and 2026-11-13, read from the jobs API and the logs:

- The run takes as long as the `build` job of `build.yml`, "Build, test and report coverage": 10.2 to 17.3 minutes.
  Every other job ends within 4.5 minutes.
- In that job, setup and restore take about 30 seconds, `dotnet build` of the solution 2 to 3 minutes, the image pulls
  about 18 seconds, the PostGIS build about 33 seconds, and the one `dotnet test --solution` step 7 to 12.5 minutes.
- That step runs the eight test projects with coverage, four at a time, on a runner with 4 vCPUs, and they starve each
  other.  `tests/DatabaseTests/Postgres` takes 3m47s there and about 1 minute alone in `test-postgres-floor`;
  `tests/DatabaseTests/SqlServer` 4m25s against about 80 seconds.  `SqlSource.Tool.Tests` (8m) and `RoslynFloor`
  (7.5m) take slots first, and `SqlSource.Tests` starts four and a half minutes in and runs five minutes more.
- Alone, in nightly's sequential loop, `SqlSource.Tests` takes about 5 minutes and `SqlSource.Tool.Tests` 4 to 5.  These
  two are the floor of any arrangement that does not split a project.
- The repository is public, so standard runners cost nothing; the free plan runs 20 jobs at once.

## Goals

- The median CI run of a pull request takes 8 minutes or less, from the first job's start to the last job's end.
- Every test that CI runs today still runs on every pull request, but `tests/SqlSource.Tests.RoslynFloor`'s.
- Every project of the solution still compiles on every pull request, RoslynFloor included: the examples, whose build
  checks their sidecars, and `QueryMethodAllocation` and the two `SyncAsync` projects, whose generated code compiles
  only there, run no test in CI but a generator change can break their build.
- The `coverage` artifact keeps its name and its files, `SummaryGithub.md` among them, so `coverage-comment.yml` is
  unchanged, and the run's summary still shows the coverage summary.
- A failure in one test job still hides nothing in the others.

## Non-goals

- Splitting a test project across runners (sharding).  The owner ruled it out: 7 to 8 minutes is enough.
- Larger or paid runners.
- Making any test faster.
- Changing what `pre-commit-validation.sh` runs locally.  It still runs every project of the solution, RoslynFloor too.
- The Windows and macOS jobs of `nightly.yml`, beyond what RoslynFloor's move asks.

## Anti-goals

- A test project that is added to the solution and that no CI job runs, with nothing failing to say so.  Today
  `dotnet test --solution` finds every project; a list of projects per job does not.
- A cache that makes a job slower than the work it replaces.
- A PostGIS build that fails inside the tests again, hiding apt's output (see
  `2026-11-12-postgis-image-prebuild-design.md`).

## Approaches

- **One job per test project, each building only what it tests (chosen).**  The `build` job gives way to four test jobs
  and a coverage job that merges their reports.  Each test job builds its own projects, as the floor jobs do today,
  in about 1.5 minutes against 3 for the solution, and has 4 vCPUs to itself.  Estimated run: 1.5 minutes of build,
  5 of `SqlSource.Tests`, setup and the coverage job, about 7 to 8 minutes.  It keeps the rule that each job pays for
  its own build.  It costs more runner minutes, which are free here, and a list of test projects that someone keeps.
- **Build once, then fan out the tests.**  One job builds the solution and uploads its output for the test jobs.  It
  uses fewer CPU minutes, but puts the 3-minute build and the transfer of a large output in front of every test, about
  9 minutes, and makes every test job wait on another.
- **Keep one job and tune it.**  Order `SqlSource.Tests` first, cap the parallel projects, drop coverage where it adds
  little.  The fewest changes, but the step stays bound by 4 vCPUs for about 15 minutes of standalone test time; an
  estimated 10 minutes or more.

## Decided

### The jobs of `build.yml`

- **`lint`, `site`, `format-style` and `format-analyzers` are unchanged.**
- **The `package` job also builds the whole solution** (the designer's call), so that every project still compiles on
  every pull request, as the `build` step of `pre-commit-validation.sh` does, and no list of projects to compile is
  kept.  The job takes about 2 minutes today; with a 3-minute build it stays within the 5 minutes of the slowest test
  job.  Whether `dotnet pack` then reuses that build with `--no-build` is the builder's call, as long as the packages
  are built as today, with the version from the environment.
- **The `build` job is replaced by four test jobs**, each with its own checkout, .NET setup, NuGet cache, build of its
  projects alone, test step with `--coverage --coverage-output-format cobertura`, and upload of its Cobertura files as
  an artifact of its own:
  - `SqlSource.Tests`.
  - `SqlSource.Tool.Tests`.
  - PostgreSQL: `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8` on the default image, with
    the image pull, the PostGIS step and the check after the tests that the `build` job has today.
  - SQL Server: `tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61` on the default image,
    with its pull.
  - `tests/SqlSource.Repository.Tests` (about 9 seconds) rides in one of the two database jobs.
- **A test job that runs two projects runs the second even when the first failed**, and fails when either did, as
  nightly's loop does.
- **A `coverage` job `needs` the four test jobs**, runs unless the run was cancelled, downloads the Cobertura files
  that exist, runs `reportgenerator` with today's arguments, appends `SummaryGithub.md` to its summary, and uploads the
  `coverage` artifact.  It is the one job of `build.yml` that needs another, and the rule in `.github/AGENTS.md` that
  says none does is changed to name it as the exception.
- **`test-postgres-floor` and `test-sqlserver-floor` are unchanged** but for the Docker caching below.
- **The NuGet cache keys follow the rule at the head of `build.yml`**: each job's key covers the lock files of what it
  restores, with `dotnet-tools.json` when it restores the tools, and no two jobs that restore different packages share
  a key.

### Job count and overlapping runs

- A CI run goes from 9 jobs to 13.  The free plan runs 20 jobs at once across the account, and 16 of the 71 runs from
  2026-11-07 to 2026-11-13 overlapped another, mostly a pull request's run and the push to `main` after its merge.  Two
  overlapping runs can then queue a few jobs until `lint`, `site` and the format jobs of the first, which end within 4
  minutes, free their slots.
- The designer's call: accept that, and measure the median on runs as they come, overlapping or not.  If queueing shows
  in the measured runs, the builder folds the shortest jobs together rather than giving up a test job.

### When the goal is missed

- The designer's call: the pull request lands when it lowers the median run, with the measured times of at least three
  runs, before and after, in its description, even if the median is over 8 minutes.  No test loses its coverage and no
  job is dropped to meet the number without the owner's word; the builder reports the gap and what would close it.

### RoslynFloor moves to nightly

- No job of `build.yml` runs `tests/SqlSource.Tests.RoslynFloor`'s tests, and its coverage leaves the report.  It still
  compiles, in the `package` job's build of the solution.  Its tests are
  those of `tests/SqlSource.Tests/Generator/`, which `SqlSource.Tests` runs on the current Roslyn, so the report loses
  almost nothing.
- `nightly.yml` already runs it on Windows and macOS.  A small Linux job is added to `nightly.yml` that builds and runs
  it alone, without coverage, so that it still runs on the system CI runs on.

### Docker caching

- **The PostGIS image is cached.**  Its name already holds everything that makes it what it is: the postgres tag, the
  PostGIS package and a hash of the Dockerfile.  A job that builds it keys an `actions/cache` entry on that name, loads
  the saved image on a hit, and on a miss builds it as today and saves it.  This applies to the PostgreSQL test job and
  `test-postgres-floor`.  The jobs of `nightly.yml` that build it use the same action with the cache turned off, since a
  key that is hit is never built again: they keep a Dockerfile that no longer builds from going unnoticed for longer
  than a day (a change made in review).
  - A failed build still fails its own step, with Docker's whole output, before any test runs.
  - "Check the tests built no PostGIS image" stays after the tests in every job that has it.
  - A loaded image is not BuildKit's build cache, so `docker build` after a load could rebuild.  The step must not
    build when the loaded image of that name exists; `tools/build-postgis-image.sh` says today that it always builds,
    and its header and `tools/README.md` change with it.
- **The pulls of the engine images are cached only where it measures faster.**  Today the two pulls take about 18
  seconds together.  A saved SQL Server image is about 1.5 GB, and restoring and loading it may take longer than the
  pull from MCR.  The builder tries a cache of each engine's images, compares the step times over at least three runs
  of a pull request, keeps a cache only where it wins, and records the numbers in the pull request.
- The repository's cache holds 10 GB, and the least recently used entries go first.  NuGet caches, PostGIS images of
  four PostgreSQL majors and any engine images share it.  The pull request states the sizes of the new entries.

### Guarding the list of test projects

- A test in `tests/SqlSource.Repository.Tests` fails when a test project of `SqlSource.slnx` is neither run by a job of
  `build.yml` nor named as a project that only `nightly.yml` runs.  RoslynFloor, `QueryMethodAllocation` and the two
  `SyncAsync` projects are named so.  This replaces what `--solution` gave for free.  Compiling needs no guard, since
  the `package` job builds the solution.

### Documents changed in the same pull request

- `.github/workflows/README.md`: the table row of `build.yml`, the test images section, the artifacts section, and the
  nightly section for RoslynFloor's Linux job.
- `.github/AGENTS.md`: the rule that no job needs another, the steps with no counterpart in the script, now in the new
  jobs, and RoslynFloor running only in nightly.
- The comment at the head of `build.yml`, which says every job starts at once, and the comment of the `package` job.
- `tools/AGENTS.md` and `tools/README.md`, where they name the `build` job, `pull-test-images.sh`'s callers or what
  `build-postgis-image.sh` does.
- `tests/README.md` and `tests/DatabaseTests/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md` and
  `tests/DatabaseTests/SqlServer/README.md`, where they say which job runs what.
- `AGENTS.md` at the root, rule 2 of Commits, which says CI runs the same checks as the script.
- Every other file that `git grep` finds naming the `build` job, "Build, test and report coverage" or RoslynFloor in
  CI.

## Left to the builder

- The job IDs and display names, and which database job takes `SqlSource.Repository.Tests`.
- How the coverage job names and finds the Cobertura artifacts, as long as the `coverage` artifact keeps its shape.
- Whether the PostGIS cache lives in the workflow or in a mode of `tools/build-postgis-image.sh`, and how a loaded image
  is told from a missing one.
- The form of the test that guards the project list: how it reads the workflow and where it lists nightly's projects.
- Whether the SQL Server and PostgreSQL floor jobs and the new database jobs share steps through a matrix.  Keeping
  them as separate jobs is the default.
