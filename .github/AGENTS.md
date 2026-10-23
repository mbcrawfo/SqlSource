# AGENTS.md - .github

GitHub Actions workflows.  [`workflows/README.md`](workflows/README.md) says what each runs, when, and what it uploads; this file is the rules of changing them.

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

- **`workflows/publish.yml` keeps its file name, and its `publish` job keeps `environment: nuget`.**  The nuget.org trusted publishing policy names both; renaming either stops publishing.  The `NuGet/login` and push steps stay in that file, not in a reusable workflow.
- **The check steps in `workflows/build.yml` mirror `pre-commit-validation.sh`.**  A check added to or removed from one is added to or removed from the other in the same commit.  A check that needs only Docker goes in the `lint` job.  One that needs the .NET SDK goes in the job whose build it shares, or in a job of its own when it would make that job the slowest.  No job `needs` another, so the run takes as long as its slowest job, and the `packages` artifact can exist in a run whose lint or tests failed.  Anything that consumes it must `needs` the whole `build.yml` call, as `publish.yml` does, never a single job inside it.  The background pull of the `build` job and the jobs `Test on PostgreSQL 14` and `Test on SQL Server 2017` of `build.yml` are the ones the script has no counterpart for; the floor images they use are pinned as rule 5 of the root `AGENTS.md` says.
- **Each `setup-dotnet` step in `workflows/build.yml` keys its cache on the lock files of exactly what its job restores**, plus `dotnet-tools.json` when the job restores the tools.  The first job to finish saves under its key, so jobs that restore different packages never share one.
- **`workflows/nightly.yml` names each test project in its Test step.**  A new test project that needs no container is added there, or it never runs on Windows or macOS.
- **The `ci` job in `workflows/ci.yml` is the required check, so it keeps its name.**  Add every new job in `ci.yml` to its `needs`.  It must keep `if: ${{ always() }}`: a skipped required check counts as passing.
- **`workflows/coverage-comment.yml` holds a write token and handles data a fork controls.**  It is triggered by `workflow_run`, so it runs as it is on `main`, for every pull request, forks included.
  - Never add a checkout of the pull request's head to it, and never build, restore or run anything the pull request supplies.
  - It reads one file of the `coverage` artifact as text.  Keep it that way: do not extract the archive to disk, and do not drop the size and heading checks.
  - Pass every value from the event to the script through `env:`.  The head branch name is chosen by the fork; an expression inside `run:` would execute it.
  - Find the pull request from the event, never from the artifact.
  - It is bound to three names: `name: CI` in `workflows/ci.yml`, the `coverage` artifact that `workflows/build.yml` uploads, and `SummaryGithub.md` inside it.  Change one and change the workflow in the same commit.
- **Version values reach MSBuild as the environment variables `BuildNumber` and `VersionSuffix`, set in the workflow-level `env` of `workflows/build.yml`**, not as `-p:` flags, so that the format check, build, test and pack jobs cannot disagree.
- **An expression cannot yield an empty string from the middle of `&&`/`||`.**  `cond && '' || x` always yields `x`.  Put the empty string last, as `publish.yml` does.
- The workflow files are limited to 120 characters per line; `tools/editorconfig-checker.sh` skips the Markdown files here.  Fold long commands with `>-` or `\`.
- Run `tools/actionlint.sh` after any change.  A change to `publish.yml` or `coverage-comment.yml` is never exercised before merge: a manual run of `publish.yml` on a branch skips every job, and a pull request runs the `coverage-comment.yml` on `main`, so actionlint and a careful read are the only checks; say in the pull request that the change is untried.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
