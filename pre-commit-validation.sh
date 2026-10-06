#!/usr/bin/env bash
# Runs every validation on the whole repository: formatting, linting, build, tests and the two package checks.
# Rewrites nothing.  Every step runs even when an earlier one fails, except that the tests and the package checks are
# skipped when the build fails.
# Usage: pre-commit-validation.sh
set -euo pipefail

SOLUTION='SqlSource.slnx'

if [[ $# -ne 0 ]]; then
    echo 'Usage: pre-commit-validation.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

# Every restore below runs in locked mode, as it does in CI, so that a stale packages.lock.json fails the validation
# instead of being rewritten by it.
export RestoreLockedMode=true

results=()
failed=0

run_step() {
    local name="$1"
    shift
    printf '\n==> %s\n' "$name"
    if "$@"; then
        results+=("passed   $name")
        return 0
    fi
    results+=("FAILED   $name")
    failed=1
    return 1
}

run_step 'format' ./format.sh --check || true
run_step 'editorconfig-checker' tools/editorconfig-checker.sh || true
run_step 'shellcheck' tools/shellcheck.sh || true
run_step 'shfmt' tools/shfmt.sh --check || true
run_step 'actionlint' tools/actionlint.sh || true
if run_step 'build' dotnet build "$SOLUTION"; then
    run_step 'test' dotnet test --solution "$SOLUTION" --no-build || true
    run_step 'package' tools/check-package.sh || true
    run_step 'package-install' tools/check-package-install.sh || true
else
    results+=('skipped  test' 'skipped  package' 'skipped  package-install')
fi

printf '\nSummary:\n'
printf '  %s\n' "${results[@]}"
exit "$failed"
