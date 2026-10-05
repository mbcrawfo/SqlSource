# TD-0002: No coverage comment on fork and Dependabot pull requests

## Problem

The `coverage-comment` job in [`ci.yml`](../../.github/workflows/ci.yml) is skipped when a pull request comes from a fork or from Dependabot.  Those pull requests get no coverage comment.

## Why it exists

GitHub gives the `pull_request` runs of forks and of Dependabot a read-only `GITHUB_TOKEN`, so the job could not create the comment.  Skipping it keeps those runs green.  The alternative, a privileged workflow that comments on their behalf, was judged not worth its risk while the project has no outside contributors.

## Impact

Low.  The coverage report is still in the run's job summary and in its `coverage` artifact.  Only the comment on the pull request is missing, so a reviewer has to open the run to see coverage.

## Proposed fix

Add a second workflow triggered by `workflow_run` on completion of `CI`.  It runs with `pull-requests: write`, downloads the `coverage` artifact of the triggering run and creates or updates the comment.  It must treat the artifact as untrusted input: never check out or execute the pull request's code, and post the markdown only after checking its size and that it is the expected file.  Then remove the fork and Dependabot conditions from `coverage-comment`, or remove that job entirely.

## Trigger

The first pull request from an outside contributor, or enabling Dependabot.
