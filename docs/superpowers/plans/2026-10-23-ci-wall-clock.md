# CI Wall-Clock Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the `CI` workflow's wall-clock time from 6.5–9.5 minutes to about 4 by running its checks in parallel jobs, with every check and the coverage report kept.

**Architecture:** `.github/workflows/build.yml` is split from two jobs into seven that do not `needs` one another: lint, two format jobs, build-and-test with coverage, one job per floor image, and package.  `format.sh` learns to run named parts so each format job runs half of it.  `ci.yml`, `publish.yml` and `coverage-comment.yml` are untouched.

**Tech Stack:** GitHub Actions, bash, .NET 10 SDK (`dotnet format`, `dotnet test` on Microsoft.Testing.Platform), CSharpier, ReportGenerator, Docker.

**Spec:** `docs/superpowers/specs/2026-10-23-ci-wall-clock-design.md`

## Global Constraints

- Every action stays pinned to the SHA already used in the file; add no new action.
- Workflow files are limited to 120 characters per line.
- `pre-commit-validation.sh` keeps calling `./format.sh --check` with no part, and its list of checks does not change.
- `name: CI` in `ci.yml`, the `ci` job, the `coverage` artifact with `SummaryGithub.md` inside it, and the `packages` artifact with exactly the two `.nupkg` files keep their names and contents.
- `BuildNumber` and `VersionSuffix` reach MSBuild as environment variables, never as `-p:` flags.
- Floor images stay `postgres:14.24` and `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, named once each in `build.yml` as `POSTGRES_FLOOR_IMAGE` and `SQLSERVER_FLOOR_IMAGE`.
- Every check still runs when another fails, so one run reports every problem.

## Review Focus

- A failure in any one job (a format part, a floor test, a package check) must fail the `ci` check: `ci` sees only the result of the `build.yml` call, which fails when any of its jobs fails.
- The background image pull fails, for example on a registry error: the wait step must fail with the pull's log printed, not hang until the job times out, and not pass.
- The package job builds its own packages: their file names must carry the run's `VersionSuffix` (`pr-<number>.<run>` on a pull request), the same version the build-and-test job compiles with.
- `format.sh` given an unknown part, or `--check` after a part, must exit 2 with the usage line, never run a partial check that passes.
- A stale `packages.lock.json` must still fail CI: every job restores in locked mode because `CI=true` sets `RestoreLockedMode` in `Directory.Build.props`.

---

### Task 1: `format.sh` runs named parts

**Files:**
- Modify: `format.sh`
- Modify: `CONTRIBUTING.md:97` (the sentence that describes `format.sh`)

**Interfaces:**
- Produces: `format.sh [--check] [part ...]`, where each part is `style`, `analyzers` or `csharpier`.  No part means all three, in that order, exactly as today.  Parts run in the fixed order style, analyzers, csharpier, whatever order they are given in.  The generator is built only when `style` or `analyzers` is among the parts.  An unknown part, or `--check` anywhere but first, prints `Usage: format.sh [--check] [style|analyzers|csharpier ...]` to stderr and exits 2.  With `--check` every chosen part runs even when an earlier one fails, and the exit status is 1 if any failed.

- [ ] **Step 1: Check the current behaviour as the baseline**

Run: `./format.sh --check; echo "rc=$?"`
Expected: `rc=0`, and the log shows the generator build, then the style, analyzers and CSharpier checks.

- [ ] **Step 2: Implement the part arguments in `format.sh`**

Parse `--check` first, then the parts into three booleans.  Keep the existing comments about why the generator is built and why locked mode is set.  Update the usage comment at the top of the file.

- [ ] **Step 3: Verify each case**

Run each and compare:

| Command | Expected |
|----|----|
| `./format.sh --check csharpier; echo $?` | no `dotnet build` output, CSharpier check only, `0` |
| `./format.sh --check style; echo $?` | generator build, then `dotnet format style` only, `0` |
| `./format.sh --check analyzers csharpier; echo $?` | generator build, analyzers, then CSharpier, `0` |
| `./format.sh --check bogus; echo $?` | usage line on stderr, `2` |
| `./format.sh style --check; echo $?` | usage line on stderr, `2` |
| `./format.sh --check; echo $?` | all three, as in Step 1, `0` |

Then make a deliberate style violation that CSharpier does not fix (an unused `using` directive in a file under `src/SqlSource`), and check that `./format.sh --check style csharpier` reports it, still runs CSharpier, and exits `1`.  Revert the violation.

- [ ] **Step 4: Run the shell linters**

Run: `tools/shellcheck.sh && tools/shfmt.sh --check`
Expected: both exit 0.

- [ ] **Step 5: Update `CONTRIBUTING.md`**

In the sentence at line 97 that describes `format.sh`, add that it takes optional part names (`style`, `analyzers`, `csharpier`) to run only those, which is how CI splits the check across jobs.

- [ ] **Step 6: Commit**

```bash
git add format.sh CONTRIBUTING.md
git commit -m "Let format.sh run named parts of the format check"
```

### Task 2: Split `build.yml` into parallel jobs

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `.github/workflows/README.md`
- Modify: `.github/AGENTS.md`
- Modify: `CONTRIBUTING.md:107` (the CI paragraph), only if its wording goes stale

**Interfaces:**
- Consumes: `format.sh --check <part ...>` from Task 1.
- Produces: the jobs below, all without `needs`.  `ci.yml` calls the workflow unchanged.

Workflow-level `env` (moved from the old `build` job, plus the version values):

```yaml
env:
  SOLUTION: SqlSource.slnx
  CONFIGURATION: Release
  DOTNET_NOLOGO: 'true'
  BuildNumber: ${{ github.run_number }}
  VersionSuffix: ${{ inputs.version-suffix }}
```

Each .NET job starts with the same two steps as today's `build` job: `Check out` (with `persist-credentials: false`) and `Set up .NET` (the same `setup-dotnet` inputs).  A job adds `Restore tools` only when it runs CSharpier or ReportGenerator.

| Job id | `name:` | Timeout | Steps after setup |
|----|----|----|----|
| `lint` | `Lint` | 10 | Unchanged |
| `format-style` | `Format check: style and CSharpier` | 15 | `Restore tools`, `./format.sh --check style csharpier` |
| `format-analyzers` | `Format check: analyzers` | 15 | `./format.sh --check analyzers` |
| `build` | `Build, test and report coverage` | 15 | `Restore tools`, `Restore packages`, `Start pulling test images`, `Build`, `Wait for test images`, `Test`, `Report coverage`, `Upload coverage` |
| `test-postgres-floor` | `Test on PostgreSQL 14` | 15 | `Build` of `tests/SqlSource.Tests.Postgres`, `Pull test image`, `Test` |
| `test-sqlserver-floor` | `Test on SQL Server 2017` | 15 | `Build` of `tests/SqlSource.Tests.SqlServer`, `Pull test image`, `Test` |
| `package` | `Pack and check packages` | 15 | `Pack` (two `dotnet pack` calls: `src/SqlSource/SqlSource.csproj`, then `src/SqlSource.Tool/SqlSource.Tool.csproj`, each with `--configuration "$CONFIGURATION" --output artifacts/packages`), `Check package`, `Check package install`, `Check tool install`, `Upload packages` |

CSharpier goes with `style` because a local timing run measured style 21s, analyzers 45s, CSharpier 2s.

- [ ] **Step 1: Rewrite `build.yml`**

Keep every step's existing `if:` condition and comment where the step survives.  Specifics:

- `build` job: `Start pulling test images` runs `tools/pull-test-images.sh` with no arguments (the default images only) in the background, writing its output to `$RUNNER_TEMP/pull-test-images.log` and, when it ends, its exit status to `$RUNNER_TEMP/pull-test-images.status` (written to a temporary name and moved, so a reader never sees a half-written file).  The background command must record a failure as a status, not die from `bash -e` before writing it.  `Wait for test images` has the old pull step's `if:`, waits until the status file exists, prints the log, and exits with the recorded status.  `Test` keeps its coverage options and `--results-directory artifacts/test-results`.
- Floor jobs: the job's `env` names its floor image (`POSTGRES_FLOOR_IMAGE` or `SQLSERVER_FLOOR_IMAGE`) and sets `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` to it.  `Build` runs `dotnet build tests/SqlSource.Tests.Postgres --configuration "$CONFIGURATION"` (or the SQL Server project).  `Pull test image` runs `docker pull --quiet` on the floor image, guarded by the build's success.  `Test` runs `dotnet test --project <project> --configuration "$CONFIGURATION" --no-build --report-github`, guarded by the build's success, with no coverage, as today.
- Move the existing comments: the header comment's job-placement sentence, the floor-image comment and the two floor-test comments go with the jobs they describe.

- [ ] **Step 2: Check the background-pull snippet locally**

Copy the two `run:` blocks into a scratch script, replace `tools/pull-test-images.sh` with `false`, and run them under `bash -e` with `RUNNER_TEMP` set to a scratch directory.
Expected: the wait part prints the log and exits non-zero within a second or two.  With `true` in its place it exits 0.

- [ ] **Step 3: Lint the workflows**

Run: `tools/actionlint.sh && tools/editorconfig-checker.sh`
Expected: both exit 0, so no line is over 120 characters.

- [ ] **Step 4: Update the docs**

- `.github/workflows/README.md`: the `build.yml` row of the table lists the jobs; "The test images" section says the `build` job pulls the default images in the background while it builds, and each floor job pulls its own image; the paragraph about where `POSTGRES_FLOOR_IMAGE` and `SQLSERVER_FLOOR_IMAGE` are named points at the floor jobs.
- `.github/AGENTS.md`: the rule that a check needing the .NET SDK "goes in the `build` job" becomes: it goes in a job of its own, or the job whose build it shares, and no job `needs` another.  The rule that names `Pull test images`, `Test on PostgreSQL 14` and `Test on SQL Server 2017` as steps without a counterpart names the floor jobs and the pull steps instead.  The versioning rule says the variables are set in the workflow-level `env` of `build.yml`.
- `CONTRIBUTING.md:107`: update only if a sentence is now wrong.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/build.yml .github/workflows/README.md .github/AGENTS.md CONTRIBUTING.md
git commit -m "Run the CI checks in parallel jobs"
```

### Task 3: Validate, review and measure

**Files:** none, unless the review finds something.

- [ ] **Step 1: Run the full validation**

Run: `./pre-commit-validation.sh`
Expected: every line of the summary `passed`.

- [ ] **Step 2: Review the branch**

Invoke the `branch-review:review` skill on the branch.  Fix every valid finding, minor ones included (root `AGENTS.md`, core rule 3), re-run Step 1 after the fixes, and repeat the review until it is clean.

- [ ] **Step 3: Ask the user before pushing**

Pushing opens CI on a pull request, which is the only way to measure the result.  With the user's go-ahead: run `git fetch --tags origin` and check `VersionPrefix` against the highest `v*` tag (root `AGENTS.md`, pull requests rule 1), bring the branch up to date with `main`, re-run Step 1 if that changed the tree, push, open the PR, and turn on Auto-fix.  The PR body says `coverage-comment.yml` and `publish.yml` are unchanged, and that `publish.yml`'s use of `build.yml` is untried until the next release or manual run on `main`.

- [ ] **Step 4: Compare the timings**

From the PR's CI run, read the job timings with `gh api repos/{owner}/{repo}/actions/runs/<id>/jobs`.
Expected: the run takes about 4 minutes, every job succeeds, the `packages` artifact holds `SqlSource.0.1.0-pr-<n>.<run>.nupkg`-style names for both packages (version matching `VersionPrefix`), and `coverage` holds `SummaryGithub.md`.  Report the before and after to the user.
