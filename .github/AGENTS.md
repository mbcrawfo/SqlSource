# AGENTS.md - .github

[`workflows/README.md`](workflows/README.md) says what each workflow runs, when, and what it uploads.

## Actions

- **Pin every action to a full commit SHA**, with the version in a trailing comment: `uses: actions/checkout@<40-character sha> # v7.0.1`.  Never use a tag or a branch as the ref.
- **When adding an action, pin its latest release.**  Look it up; do not copy a SHA from another repository or from memory:

  ```bash
  gh api repos/<owner>/<repo>/releases/latest --jq .tag_name
  ```

  ```bash
  gh api repos/<owner>/<repo>/commits/<tag> --jq .sha
  ```

- When changing a pin, change the SHA and the version comment together, everywhere the action is used.

## Workflows

- **The check steps in `workflows/build.yml` mirror `pre-commit-validation.sh`.**  A check added to or removed from one is added to or removed from the other in the same commit.  A check that needs only Docker goes in the `lint` job.  One that needs the .NET SDK goes in the job whose build it shares, or in a job of its own when it would make that job the slowest.  No job `needs` another, so the `packages` artifact can exist in a run whose lint or tests failed: anything that consumes it must `needs` the whole `build.yml` call, as `publish.yml` does, never a single job inside it.  The step `Pull test images` of the `build` job and the jobs `Test on PostgreSQL 14` and `Test on SQL Server 2017` are the ones the script has no counterpart for.
- **The `Test` step of the `build` job leaves out the two end-to-end `AllocationTests` classes by name, and its `Test allocations` step runs them without coverage**, since the collector allocates in the process they measure.  Renaming or moving either class, or adding another test that measures the whole process, changes both steps.
- **`workflows/build.yml` and `workflows/nightly.yml` together test every supported major of each engine, each once.**  `build.yml` tests the floor and the default; the matrices of the `test-postgres` and `test-sqlserver` jobs of `nightly.yml` test every supported major between them, each image pinned as rule 5 of the root `AGENTS.md` says.
  - A pull request that moves a floor or a default, or adds or drops a supported major, changes the other workflow in the same pull request: when the default moves to PostgreSQL 19, PostgreSQL 18 joins the nightly matrix.
  - A SQL Server image added to a matrix gets its row of `SqlServerContainer.ImageBuilds`, or its tests fail.
- **`workflows/coverage-comment.yml` holds a write token and handles data a fork controls.**  It is triggered by `workflow_run`, so it runs as it is on `main`, for every pull request, forks included.
  - Never add a checkout of the pull request's head to it, and never build, restore or run anything the pull request supplies.
  - It reads one file of the `coverage` artifact as text.  Keep it that way: do not extract the archive to disk, and do not drop the size and heading checks.
  - Pass every value from the event to the script through `env:`.  The head branch name is chosen by the fork; an expression inside `run:` would execute it.
  - Find the pull request from the event, never from the artifact.
  - It is bound to three names: `name: CI` in `workflows/ci.yml`, the `coverage` artifact that `workflows/build.yml` uploads, and `SummaryGithub.md` inside it.  Change one and change the workflow in the same commit.
- **Every `--report-github` comes with `--report-github-summary-include-passed false`.**  A step's summary holds 1 MiB and the reporter cuts its output there, so a list of every passed test loses the projects after the cut.
- **An expression cannot yield an empty string from the middle of `&&`/`||`.**  `cond && '' || x` always yields `x`.  Put the empty string last, as `publish.yml` does.
- Fold workflow lines over the 120-character `.editorconfig` limit with `>-` or `\`.
- Run `tools/actionlint.sh` after any change.  A change to `publish.yml` or `coverage-comment.yml` is never exercised before merge: a manual run of `publish.yml` on a branch skips every job, and a pull request runs the `coverage-comment.yml` on `main`, so actionlint and a careful read are the only checks; say in the pull request that the change is untried.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
