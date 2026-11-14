# Cutting the wall-clock time of CI: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the median CI run of a pull request from about 14 minutes to 8 or less, by giving each test project a
runner of its own, moving RoslynFloor's tests to nightly and caching the PostGIS image.

**Architecture:** The `build` job of `.github/workflows/build.yml` gives way to four test jobs that each build only
their projects and upload their Cobertura files, and a `coverage` job that merges them into today's `coverage`
artifact.  The `package` job also builds the whole solution, so every project still compiles.  A composite action
loads the PostGIS image from the Actions cache or builds and saves it.  A repository test fails when a test project of
the solution is run by no CI job.

**Tech Stack:** GitHub Actions (reusable workflow, composite action, `actions/cache`, `actions/upload-artifact`,
`actions/download-artifact`), .NET 10 with Microsoft.Testing.Platform, xunit.v3 and Shouldly, ReportGenerator, Docker.

**Spec:** `docs/superpowers/specs/2026-11-13-ci-wall-clock-design.md`

## Global Constraints

- Every action is pinned to a full commit SHA with its version in a trailing comment, looked up with the two `gh api`
  commands of `.github/AGENTS.md`, never copied from memory.
- Every `--report-github` comes with `--report-github-summary-include-passed false`.
- No workflow names a default image; floor and nightly images stay where rule 5 of the root `AGENTS.md` lists them.
- Version values reach MSBuild only through the `BuildNumber` and `VersionSuffix` environment variables, never `-p:`.
- Each job's NuGet cache key covers the lock files of what it restores, with `dotnet-tools.json` when it restores the
  tools; two jobs that restore different packages never share a key.
- The `coverage` artifact keeps its name and files (`SummaryGithub.md`, the HTML and the Cobertura reports), and
  `reportgenerator` keeps today's arguments, `-assemblyfilters:+SqlSource;+SqlSource.Tool` among them.
- Workflow lines stay within 120 characters, folded with `>-` or `\`.
- Root `AGENTS.md` stays within 16 KB and every other AGENTS.md within 8 KB, one rule per bullet of at most 80 words.
- Every commit passes `./pre-commit-validation.sh`; every push passes `./pre-commit-validation.sh --full` and a clean
  `branch-review:review` on the branch as pushed.  Pushing and opening the pull request wait for the owner's word.

## Review Focus

- A grouped test job whose first project fails still runs the second, and fails.  Task 1, Step 6 checks the loop.
- A test job that fails or times out still leaves the `coverage` job a report of the other jobs, as today's report runs
  whenever any Cobertura file exists.  Task 1 gives the upload and the coverage job `!cancelled()` conditions.
- A project path that is a prefix of another (`tests/DatabaseTests/Postgres` and `…/Postgres.Npgsql8`), or one named
  only in a comment, must not count as run.  Task 1's unit facts pin both.
- A cached PostGIS tarball that fails to load must fall back to a build, not fail the job.  Task 2, Step 2.
- A run from a fork's pull request reads the cache but its writes are scoped to its own ref; nothing about the cache
  may need a secret.  Task 2 uses only `actions/cache`, which needs none.

---

### Task 1: One test job per project, the coverage job, and the guard

**Files:**
- Create: `tests/SqlSource.Repository.Tests/CiTestProjectsTests.cs`
- Modify: `.github/workflows/build.yml` (replace the `build` job; change `package`; header comment)
- Modify: `.github/workflows/nightly.yml` (add the `roslyn-floor` job)
- Modify: `.github/workflows/README.md`, `.github/AGENTS.md`, `tools/AGENTS.md`, `tools/README.md`, `tools/pull-test-images.sh`
  (header comment: its callers), `tests/README.md`, `tests/DatabaseTests/AGENTS.md`,
  `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`, `AGENTS.md` (Commits rule 2)

**Interfaces:**
- Produces: job IDs `test-generator`, `test-tool`, `test-postgres`, `test-sqlserver`, `coverage` in `build.yml`, and
  `roslyn-floor` in `nightly.yml`; intermediate artifacts named `cobertura-<job id>`.  Task 2 changes the PostGIS step
  of `test-postgres`; Task 3 measures these jobs.

- [ ] **Step 1: Write the guard tests** in `CiTestProjectsTests.cs`, namespace `SqlSource.Repository.Tests`, in the
  style of `TestLayoutTests.cs`, with two private static helpers the facts call:
  - `TestProjects()`: the folder, as `tests/...` with `/`, of each project of `SqlSource.slnx` under `tests/` whose
    `.csproj` has `<PackageReference Include="xunit.v3" />`.
  - `Unrun(IEnumerable<string> projects, string workflow)`: the projects not named in the workflow outside comment
    lines (a line whose trimmed text starts with `#`).  A project is named when its path occurs with no `[\w./-]`
    before or after it, so `tests/X/packages.lock.json`, a cache path, does not name `tests/X`.
  - `NightlyOnly`: `tests/SqlSource.Tests.RoslynFloor`, `tests/DatabaseTests/QueryMethodAllocation`,
    `tests/DatabaseTests/Postgres.SyncAsync`, `tests/DatabaseTests/SqlServer.SyncAsync`.

  ```csharp
  [Fact] public void TestProjects_Solution_AreTheElevenTestProjects()   // count 11; contains tests/SqlSource.Tests;
                                                                        // excludes tests/DatabaseTests/TestHarness
  [Fact] public void BuildWorkflow_RunsEveryTestProjectButTheNightlyOnes()
      => Unrun(TestProjects().Except(NightlyOnly), BuildYml).ShouldBeEmpty();
  [Fact] public void NightlyOnlyProjects_AreRunByNightlyAlone()
      // Unrun(NightlyOnly, BuildYml) == NightlyOnly and Unrun(NightlyOnly, NightlyYml) is empty
  [Fact] public void Unrun_ProjectNamedOnlyInAComment_IsUnrun()
      => Unrun(["tests/A"], "  # runs tests/A\nrun: true").ShouldBe(["tests/A"]);
  [Fact] public void Unrun_ProjectWhosePathStartsAnother_IsUnrunWhenOnlyTheOtherIsNamed()
      => Unrun(["tests/DatabaseTests/Postgres"], "run: dotnet test tests/DatabaseTests/Postgres.Npgsql8")
          .ShouldBe(["tests/DatabaseTests/Postgres"]);
  [Fact] public void Unrun_ProjectNamedOnlyInACachePath_IsUnrun()
      => Unrun(["tests/A"], "cache-dependency-path: tests/A/packages.lock.json").ShouldBe(["tests/A"]);
  [Fact] public void Unrun_ProjectsNamedInALoop_AreRun()
      => Unrun(["tests/A", "tests/B"], "for project in tests/A \\\n    tests/B; do").ShouldBeEmpty();
  ```

- [ ] **Step 2: Run them; expect the workflow facts to fail.**
  Run: `dotnet test --project tests/SqlSource.Repository.Tests --filter-class SqlSource.Repository.Tests.CiTestProjectsTests`
  Expected: the five unit facts, `TestProjects_Solution_AreTheElevenTestProjects` and
  `NightlyOnlyProjects_AreRunByNightlyAlone` pass (nightly already names all four, and today's `build.yml` names none);
  `BuildWorkflow_RunsEveryTestProjectButTheNightlyOnes` fails naming the seven others, since `build.yml` runs
  `--solution`.

- [ ] **Step 3: Replace the `build` job of `build.yml`** with four jobs, each: checkout; `setup-dotnet` cached on the
  lock files of its projects (no tool restore); `dotnet build <project> --configuration "$CONFIGURATION"` for each of
  its projects; a test step; an upload of `artifacts/test-results/*.cobertura.xml` as `cobertura-<job id>`, with
  `if: ${{ !cancelled() }}` and `if-no-files-found: warn`.  `timeout-minutes: 20`.
  - `test-generator`: `tests/SqlSource.Tests`.
  - `test-tool`: `tests/SqlSource.Tool.Tests`.
  - `test-postgres`: `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`; then
    `tools/pull-test-images.sh`, the "Build PostGIS image" step and the "Check the tests built no PostGIS image" step
    exactly as the `build` job has them today, the Test step requiring the PostGIS step's success.
  - `test-sqlserver`: `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61`,
    `tests/SqlSource.Repository.Tests`; pulls with `tools/pull-test-images.sh`.
  - Test step of every job: a loop over its projects like nightly's `Test` step, each
    `dotnet test --project "$project" --configuration "$CONFIGURATION" --no-build --results-directory artifacts/test-results --coverage --coverage-output-format cobertura --report-github --report-github-summary-include-passed false || status=1`,
    then `exit "$status"`.  A one-project job uses the same loop, so every job reads alike.
  - Keep the comments of the steps it carries over; the database jobs name their projects in a comment above the loop.

- [ ] **Step 4: Add the `coverage` job**: `needs` the four test jobs, `if: ${{ !cancelled() }}`, `timeout-minutes: 10`;
  checkout (reportgenerator maps the Cobertura paths to sources); `setup-dotnet` cached on `dotnet-tools.json`;
  `dotnet tool restore`; `actions/download-artifact` with `pattern: cobertura-*`, `merge-multiple: true`,
  `path: artifacts/test-results`; then today's "Report coverage" and "Upload coverage" steps unchanged.  A comment
  says it is the one job that needs another and why.

- [ ] **Step 5: Change the other jobs and comments.**
  - `package`: a "Build the solution" step before "Pack", `dotnet build "$SOLUTION" --configuration "$CONFIGURATION"`,
    and its cache key becomes `'**/packages.lock.json'`, the same set `format-analyzers` restores.  Its comment says
    why it builds the solution.  `timeout-minutes: 20`.
  - The header of `build.yml`: every job but `coverage` starts at once; `coverage` waits for the test jobs.
  - `nightly.yml`: a `roslyn-floor` job on `ubuntu-latest`, `timeout-minutes: 20`, with `PROJECT:
    tests/SqlSource.Tests.RoslynFloor`, cached on its lock file, building and testing it alone without coverage, like
    `test-postgres` without the images.  Its comment says CI compiles it but only nightly runs its tests.

- [ ] **Step 6: Lint and run the guard.**
  Run: `tools/actionlint.sh && dotnet test --project tests/SqlSource.Repository.Tests --filter-class SqlSource.Repository.Tests.CiTestProjectsTests`
  Expected: no actionlint finding; all eight facts pass.  Then read each test step and confirm `|| status=1` follows each
  `dotnet test`, so a failed first project does not stop the second.

- [ ] **Step 7: Update the documents.**  Run
  `git grep -n -e 'build job' -e 'Build, test and report coverage' -e RoslynFloor -e '--solution' -- .github tools tests/README.md tests/DatabaseTests AGENTS.md CONTRIBUTING.md docs/architecture`
  (and read each hit naming the job by its ID, `` `build` ``)
  and correct every statement the new jobs make wrong:
  - `.github/workflows/README.md`: the `build.yml` row (the jobs, the coverage job, the package job's solution build),
    the test images section (which jobs pull and build PostGIS), the artifacts section (the `cobertura-*`
    intermediates, the coverage job), and the nightly section (`roslyn-floor`).
  - `.github/AGENTS.md`: "no job needs another" names `coverage` as the one exception, which consumes only the
    `cobertura-*` artifacts; the steps without a counterpart in the script now sit in `test-postgres` and
    `test-sqlserver`; a bullet that a test project runs in a job of `build.yml` or is named nightly-only in
    `CiTestProjectsTests`.
  - Root `AGENTS.md`, Commits rule 2: CI runs the same checks but RoslynFloor's tests, which only nightly runs.
    Keep the file under 16 KB.
  - The rest wherever the grep shows a statement that no longer holds.

- [ ] **Step 8: Validate and commit.**
  Run: `./pre-commit-validation.sh`
  Expected: passes, `tests/SqlSource.Repository.Tests` among what ran (its AGENTS.md size and link tests included).
  ```bash
  git add .github tools tests AGENTS.md
  git commit -m "ci: give each test project a job of its own and merge coverage after"
  ```

### Task 2: Cache the PostGIS image

**Files:**
- Create: `.github/actions/postgis-image/action.yml`
- Modify: `.github/workflows/build.yml` (`test-postgres`, `test-postgres-floor`), `.github/workflows/nightly.yml`
  (`test-postgres`, `sync-async-postgres`), `tools/build-postgis-image.sh` (header), `tools/README.md` (Building the
  PostGIS image), `.github/workflows/README.md` (test images section), `.github/AGENTS.md`

**Interfaces:**
- Consumes: `tools/build-postgis-image.sh` `--name`, `--id` and the build mode, unchanged.
- Produces: composite action `./.github/actions/postgis-image`, input `postgres-image` (optional; empty means the
  default image, as the script reads it), writing `POSTGIS_IMAGE_ID` to `$GITHUB_ENV` as the step does today, and
  output `cache-hit`.

- [ ] **Step 1: Look up the pin of `actions/cache`** with the two commands of `.github/AGENTS.md`.

- [ ] **Step 2: Write the action.**  Steps, all `shell: bash`, values passed through `env:`:
  1. `name="$(tools/build-postgis-image.sh --name $IMAGE)"` into a step output.
  2. `actions/cache` with `path: ${{ runner.temp }}/postgis-image.tar` and `key: postgis-image-<name>`.  The name holds
     the postgres tag, the PostGIS package and the Dockerfile's hash, so no other key part is needed.
  3. On a hit: `docker load --input` the tarball; when the load fails, print a warning and fall through to the build.
  4. When the image is still missing (`docker image inspect` fails): `tools/build-postgis-image.sh $IMAGE`, then
     `docker save --output` the tarball.  A failed build fails the step with Docker's whole output, as today.
  5. `echo "POSTGIS_IMAGE_ID=$(tools/build-postgis-image.sh --id $IMAGE)" >> "$GITHUB_ENV"`.
  The action's header comment says why it does not build after a load: BuildKit's cache does not hold a loaded image,
  so a build would run apt again.

- [ ] **Step 3: Use it** as the "Build PostGIS image" step of the four jobs, `id: postgis` kept, with
  `postgres-image: ${{ env.POSTGRES_FLOOR_IMAGE }}` in `test-postgres-floor` and `${{ matrix.image }}` in nightly's
  `test-postgres`.  The "Check the tests built no PostGIS image" steps are unchanged.

- [ ] **Step 4: Update the documents**: the header of `build-postgis-image.sh` (CI's action may load the image in
  place of calling it), `tools/README.md`, the test images section of `.github/workflows/README.md`, and a bullet of
  `.github/AGENTS.md` on the action (local actions live in `.github/actions/`; a job checks out before using one).

- [ ] **Step 5: Lint, validate and commit.**
  Run: `tools/actionlint.sh && ./pre-commit-validation.sh`
  Expected: both pass.
  ```bash
  git add .github tools
  git commit -m "ci: cache the PostGIS image between runs"
  ```

### Task 3: Measure on the pull request and settle the engine-image cache

**Files:**
- Possibly create: `.github/actions/cached-image/action.yml`; modify the pull steps it would replace
- Modify: the pull request's description

- [ ] **Step 1: Get ready to push.**  `git fetch --tags origin`; compare `VersionPrefix` in `Directory.Build.props`
  with `git tag --list 'v*' --sort=-v:refname | head -n 1` and ask the owner if it is not greater.  Merge `origin/main`
  if it moved.  Run `branch-review:review` until clean, then `./pre-commit-validation.sh --full`.

- [ ] **Step 2: With the owner's word, push and open a draft pull request** with Auto-fix on.  The pull request's own
  CI run is the first test of the new jobs; nightly runs too, since the pull request changes `nightly.yml`.
  Expected: CI green, a `coverage` artifact with `SummaryGithub.md`, the coverage summary in the run's summary, the
  coverage comment posted; nightly's `roslyn-floor` green.

- [ ] **Step 3: Record three runs.**  Re-run CI twice (`gh run rerun <id>`; the second and third hit the PostGIS cache)
  and, for each, read job and step times as the spec's numbers were read:
  `gh run view <id> --json jobs`.  Record the wall time, each test job's build and test steps, and the PostGIS step
  with and without a cache hit.  Before: median 14.2 minutes (10.3 to 17.4) over runs 43264212598, 43267467851,
  43332227404, 43333819086, 43342328347, 43346782351, 43347600116, 43349233347, 43442424854, 43445084895.

- [ ] **Step 4: Try the engine-image cache** in `test-postgres` and `test-sqlserver`: a composite action
  `./.github/actions/cached-image` that keys `actions/cache` on the image reference, loads on a hit, and pulls and
  saves on a miss.  Push (after Step 1's checks again) and record three runs.  Keep it only where the median pull
  step is faster than `tools/pull-test-images.sh`'s; otherwise revert it in a commit of its own.  Keep the PostGIS
  cache only if its hit is faster than its build; otherwise revert Task 2 and say so.

- [ ] **Step 5: Settle the result.**  Write the before and after numbers, the cache entry sizes (`gh cache list`) and
  each keep-or-drop decision into the pull request's description.  If the median is over 8 minutes, say by how much
  and what would close it; do not drop coverage or a job to meet it.  If queueing behind another run shows in a
  job's start time, fold the shortest jobs together as the spec says and measure again.  Mark the pull request ready.
