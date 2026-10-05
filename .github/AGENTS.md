# AGENTS.md - .github

GitHub Actions workflows.  `workflows/ci.yml` and `workflows/publish.yml` both call the reusable `workflows/build.yml`.

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
- **The check steps in `workflows/build.yml` mirror `pre-commit-validation.sh`.**  A check added to or removed from one is added to or removed from the other in the same commit.  A check that needs the .NET SDK goes in the `build` job; one that needs only Docker goes in the `lint` job.  The two jobs run side by side and neither `needs` the other, so the `packages` artifact can exist in a run whose lint failed.  Anything that consumes it must `needs` the whole `build.yml` call, as `publish.yml` does, never a single job inside it.
- **The `ci` job in `workflows/ci.yml` is the required status check.**  Keep its name.  Add every new job in `ci.yml` to its `needs`.  It must keep `if: ${{ always() }}`: a skipped required check counts as passing.
- **Version values reach MSBuild as the job-level environment variables `BuildNumber` and `VersionSuffix`**, not as `-p:` flags, so that the format check, build, test and pack steps cannot disagree.
- **An expression cannot yield an empty string from the middle of `&&`/`||`.**  `cond && '' || x` always yields `x`.  Put the empty string last, as `publish.yml` does.
- Files here are limited to 120 characters per line.  Fold long commands with `>-` or `\`.
- Run `tools/actionlint.sh` after any change.  `publish.yml` cannot run from a branch: it is first exercised on `main`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
