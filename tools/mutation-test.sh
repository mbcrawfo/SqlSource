#!/usr/bin/env bash
# Runs a mutation test of one of the two source projects with Stryker, or compares the reports of two runs.  It is no
# check: a whole run takes hours, a person runs it, and neither pre-commit-validation.sh nor build.yml does.
# Stryker refuses the repository root, where it finds the solution, so a run starts in the folder of the project it
# mutates, with the stryker-config.json there.  It builds in the working tree, so one run at a time holds the lock,
# the folder artifacts/mutation/.running.
# A run is two runs of Stryker.  The first tests nothing and finds the static mutants, those of a static initialiser
# or constructor; the second leaves them out.  Stryker's runner tests a static mutant in a process it goes on using,
# whose state then fails tests under the mutants that follow, so a run that tested one reports kills that are not
# real, and the script fails it.  tools/README.md has what a report holds and Stryker's other limits.
# Usage: mutation-test.sh <generator|tool> [--output <folder>] [--tests <filter>] [<glob> ...]
#        mutation-test.sh compare <before.json> <after.json>
# A glob names source files from the project's folder, as '**/Parsing/*.cs', and with none every file is tested.  The
# filter is Stryker's --test-case-filter.  The output folder defaults to artifacts/mutation/<generator|tool>; the
# report is reports/mutation-report.json in it, and static/ in it holds the first run.
set -euo pipefail

usage() {
    {
        echo 'Usage: mutation-test.sh <generator|tool> [--output <folder>] [--tests <filter>] [<glob> ...]'
        echo '       mutation-test.sh compare <before.json> <after.json>'
    } >&2
    exit 2
}

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
called_in="$PWD"

# Prints a path as an absolute one, read from the folder the script was called in.
absolute() {
    case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s\n' "$called_in/$1" ;;
    esac
}

# tools/MutationReport reads a report.  It is built once and then run from its output, so that nothing a build
# prints is taken for its answer, and what the build prints is shown only when it fails.
build_report_tool() {
    local built
    if ! built="$(dotnet build "$root/tools/MutationReport" --nologo --verbosity quiet 2>&1)"; then
        printf '%s\n' "$built" >&2
        exit 1
    fi
}
report_tool() {
    dotnet "$root/tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll" "$@"
}

[[ $# -ge 1 ]] || usage
target="$1"
shift

if [[ "$target" == compare ]]; then
    [[ $# -eq 2 ]] || usage
    before="$(absolute "$1")"
    after="$(absolute "$2")"
    build_report_tool
    report_tool compare "$before" "$after"
    exit
fi

case "$target" in
generator) project='src/SqlSource' ;;
tool) project='src/SqlSource.Tool' ;;
*) usage ;;
esac

output="$root/artifacts/mutation/$target"
tests=()
mutate=()
while [[ $# -gt 0 ]]; do
    case "$1" in
    --output)
        [[ $# -ge 2 ]] || usage
        output="$(absolute "$2")"
        shift 2
        ;;
    --tests)
        [[ $# -ge 2 ]] || usage
        tests=(--test-case-filter "$2")
        shift 2
        ;;
    --*) usage ;;
    *)
        mutate+=(--mutate "$1")
        shift
        ;;
    esac
done
if [[ ${#mutate[@]} -eq 0 ]]; then
    mutate=(--mutate '**/*.cs')
fi

# mkdir makes the folder or fails, in one step, so two runs that start together cannot both take the lock.
lock="$root/artifacts/mutation/.running"
mkdir -p "$root/artifacts/mutation"
if ! mkdir "$lock" 2>/dev/null; then
    {
        echo "Another mutation run holds $lock."
        echo 'Stryker builds in the working tree, so two runs cannot share a checkout.'
        echo 'If no run is going, remove that folder and run again.'
    } >&2
    exit 2
fi
trap 'rmdir "$lock"' EXIT
trap 'exit 130' INT TERM

build_report_tool
cd "$root/$project"

# The first run: every file, and of each only an empty span, so that Stryker lists every mutant and tests none.  A
# pattern that excludes a file whole would have it leave that file's mutants out of the report.
dotnet stryker --skip-version-check --output "$output/static" --reporter json --reporter progress \
    --mutate '**/*.cs{0..1}' ${tests[@]+"${tests[@]}"}
exclusions="$output/static/exclusions.txt"
report_tool static "$output/static/reports/mutation-report.json" >"$exclusions"
while IFS= read -r exclusion; do
    mutate+=(--mutate "$exclusion")
done <"$exclusions"

# The second run.  The expansion of tests is this long because bash 3.2 takes an empty array for an unset variable.
status=0
dotnet stryker --skip-version-check --output "$output" "${mutate[@]}" ${tests[@]+"${tests[@]}"} || status=$?

report="$output/reports/mutation-report.json"
if [[ -f "$report" ]]; then
    if ! report_tool static "$report" >/dev/null; then
        echo "The run tested a static mutant that its first run did not find, so $report is not to be used." >&2
        [[ "$status" -ne 0 ]] || status=1
    fi
    echo "Report: $report"
fi
exit "$status"
