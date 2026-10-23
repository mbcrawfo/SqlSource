# CI wall-clock time

## Intent

The `CI` workflow takes 6.5 to 9.5 minutes, because nearly everything runs in one job, one step after another.  The goal is a shorter wait for the `ci` check on a pull request, about 4 minutes, with every check and the coverage report kept as they are.

## Where the time goes today

Measured on recent runs of the `build` job of `.github/workflows/build.yml`:

| Step | Time |
|----|----|
| `format.sh --check` | 1m50 to 3m10, nearly all `dotnet format style` and `dotnet format analyzers`, each of which compiles the solution again |
| Build | about 1m |
| Pull test images | about 40s |
| Test, every project at once | about 2m; `SqlSource.Tests.Postgres` and `SqlSource.Tests.SqlServer` take about 1m40 each |
| Test on PostgreSQL 14, then on SQL Server 2017 | about 40s each |
| Pack and the three package checks | about 30s |

The `lint` job takes 15s and already runs on its own.

## Goals, non-goals, anti-goals

- Goal: the slowest path of a pull request's run is about setup, build, test and coverage report, roughly 4 minutes.
- Goal: every check still runs and still reports every problem in one run.  `coverage`, `packages`, the job summary and the required `ci` check are unchanged for their consumers: `coverage-comment.yml`, `publish.yml` and branch protection.
- Non-goal: splitting the test projects across runners.  That would need the coverage of several jobs merged before upload, and the user chose the simpler workflow over the last minute.
- Anti-goal: a check that silently stops running, or `pre-commit-validation.sh` and `build.yml` no longer agreeing on what is checked.

## Approaches

- **A. Parallel jobs (chosen).**  Each check moves to a job of its own, and all of them start at once.  Each job repeats a build, of about a minute, but the jobs run in parallel and the repository is public, so the extra minutes are free.
- **B. One job that runs things in the background.**  Rejected: every process shares the runner's 4 cores, so each slows the others down, the logs interleave, and the run would land at about 5 to 6 minutes.
- **C. Build once and pass the outputs to the other jobs as an artifact.**  Rejected: the upload and download cost time, and every downstream job waits for the build, so the slowest path is no shorter than in A.

## The design

The jobs of `build.yml`, none of which `needs` another:

| Job | Does |
|----|----|
| lint | Unchanged |
| format (two jobs) | `format.sh --check` with a part named, so that `dotnet format style` and `dotnet format analyzers` run on separate runners.  CSharpier goes with whichever is faster.  Each job builds the generator first, as the script already does |
| build and test | Restores the solution in locked mode, builds it in `Release`, runs every test with coverage, then reports and uploads `coverage`.  It pulls only the two default images, and starts the pull in the background before the build so that the pull overlaps it |
| PostgreSQL 14 | Builds `tests/SqlSource.Tests.Postgres`, pulls `POSTGRES_FLOOR_IMAGE` and runs that project on it |
| SQL Server 2017 | The same for `tests/SqlSource.Tests.SqlServer` on `SQLSERVER_FLOOR_IMAGE` |
| package | Builds and packs in `Release`, runs `check-package.sh`, `check-package-install.sh` and `check-tool-install.sh`, and uploads `packages` |

Decided in the conversation:

- `format.sh` stays the one definition of the format checks.  `format.sh --check` takes optional part names (`style`, `analyzers`, `csharpier`).  With none it runs every part, as it does today, so `pre-commit-validation.sh` and local use are unchanged.  CI calls it once per format job, with that job's parts.
- `ci.yml` keeps calling `build.yml` as a whole and `ci` keeps its name, so `ci` still waits for every job, and `publish.yml` still consumes `packages` only from a run where everything passed.

My calls, open to correction:

- `BuildNumber` and `VersionSuffix` move from the job's `env` to the workflow's `env` in `build.yml`, so that every job, the package job above all, sees the same version.  The rule in `.github/AGENTS.md` that names "job-level" is reworded to match.
- The floor images move to the floor jobs.  Rule 5 of the root `AGENTS.md` names `build.yml` as their place, and that stays true.
- Each job keeps a `timeout-minutes` sized to it.
- The pack step no longer waits on the tests passing, because it is in another job.  Nothing publishes from a failed run, because `publish.yml` needs the whole `build.yml` call.

## Left to the builder

- Which format job CSharpier joins, from a timed run.
- Whether the package job packs the solution or the two `src` projects.  The result must be the same two `.nupkg` files.
- How the background pull is awaited before the tests.  `tools/pull-test-images.sh` still prints the seconds of each pull.
- Docs to update in the same PR: `.github/workflows/README.md`, `.github/AGENTS.md` (the rule that puts SDK checks in "the `build` job", and the versioning rule), `CONTRIBUTING.md` (the new `format.sh` argument, and the CI paragraph if its wording goes stale) and the usage comment of `format.sh`.
- Verification: actionlint, `pre-commit-validation.sh`, a review of the branch with the branch-review skill whose findings are addressed before the PR opens, and this PR's own CI run, whose timings are compared with the table above.
