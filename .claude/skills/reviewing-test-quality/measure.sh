#!/usr/bin/env bash
# Runs the measurements of a test review, one after another, into a run folder: the mutation scopes of scopes.txt,
# each through tools/mutation-test.sh, or the test projects of projects.txt, each with coverage and each test's time.
# Nothing else that builds runs in the checkout meanwhile, since Stryker builds in the working tree.
# It goes on after a scope or a project that fails, and exits 1 at the end if any did.  A scope that fails has the
# file "failed" in its folder, which the condense command of tools/MutationReport refuses, and a later run of the
# scope that passes removes it.  Every start and end is a line of measure.log in the run folder.
# Usage: measure.sh <run folder> mutation [<scope> ...]
#        measure.sh <run folder> tests [<project folder> ...]
# With no scope every scope is run, and with no project every project that projects.txt does not mark not-run.
# The results are mutation/<scope>/reports/mutation-report.json, and results.xunit.xml and coverage.cobertura.xml in
# tests/<project folder without its tests/>, where condense looks for them.
set -euo pipefail

# A glob of a scope is passed on as it is written, never expanded here.
set -f

usage() {
    {
        echo 'Usage: measure.sh <run folder> mutation [<scope> ...]'
        echo '       measure.sh <run folder> tests [<project folder> ...]'
    } >&2
    exit 2
}

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"

[[ $# -ge 2 ]] || usage
case "$1" in
/*) run="$1" ;;
*) run="$PWD/$1" ;;
esac
kind="$2"
shift 2
mkdir -p "$run"
log="$run/measure.log"

note() {
    printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$*" | tee -a "$log"
}

# The blocks of scopes.txt, in order: a key and its value to a line, and a line with no key ends nothing.
scope_names=()
scope_projects=()
scope_mutates=()
scope_tests=()
read_scopes() {
    local line key value last
    while IFS= read -r line || [[ -n "$line" ]]; do
        line="${line%%#*}"
        [[ "$line" == *' '* ]] || continue
        key="${line%% *}"
        value="${line#* }"
        last=$((${#scope_names[@]} - 1))
        case "$key" in
        scope)
            scope_names+=("$value")
            scope_projects+=('')
            scope_mutates+=('')
            scope_tests+=('')
            ;;
        project) scope_projects[last]="$value" ;;
        mutate) scope_mutates[last]="$value" ;;
        tests) scope_tests[last]="$value" ;;
        folders) ;;
        *)
            echo "measure.sh: scopes.txt has the line '$line', whose key is not known." >&2
            exit 2
            ;;
        esac
    done <"$here/scopes.txt"
}

failed=0

run_scope() {
    local index="$1" name="${scope_names[$1]}" output started status=0
    output="$run/mutation/$name"
    mkdir -p "$output"
    rm -f "$output/failed"
    note "start scope $name"
    started=$SECONDS
    # The globs of a scope are words apart by a space, and none holds a space.
    # shellcheck disable=SC2086
    "$root/tools/mutation-test.sh" "${scope_projects[index]}" --output "$output" \
        --tests "${scope_tests[index]}" ${scope_mutates[index]} || status=$?
    if [[ "$status" -ne 0 || ! -f "$output/reports/mutation-report.json" ]]; then
        echo "$status" >"$output/failed"
        failed=1
    fi
    note "end scope $name exit $status seconds $((SECONDS - started))"
}

run_project() {
    local project="${1%/}" output started status=0
    output="$run/tests/${project#tests/}"
    mkdir -p "$output"
    note "start tests $project"
    started=$SECONDS
    dotnet test --project "$root/$project" --results-directory "$output" \
        --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml \
        --report-xunit-xml --report-xunit-xml-filename results.xunit.xml || status=$?
    if [[ "$status" -ne 0 ]]; then
        failed=1
    fi
    note "end tests $project exit $status seconds $((SECONDS - started))"
}

case "$kind" in
mutation)
    read_scopes
    wanted=("$@")
    if [[ ${#wanted[@]} -eq 0 ]]; then
        wanted=("${scope_names[@]}")
    fi
    # Every name is looked up before the first scope runs, so that a wrong one costs no hours.
    indexes=()
    for name in "${wanted[@]}"; do
        found=''
        for index in "${!scope_names[@]}"; do
            if [[ "${scope_names[index]}" == "$name" ]]; then
                found="$index"
            fi
        done
        if [[ -z "$found" ]]; then
            echo "measure.sh: scopes.txt has no scope '$name'." >&2
            exit 2
        fi
        indexes+=("$found")
    done
    for index in "${indexes[@]}"; do
        run_scope "$index"
    done
    ;;
tests)
    projects=("$@")
    if [[ ${#projects[@]} -eq 0 ]]; then
        while IFS= read -r line || [[ -n "$line" ]]; do
            line="${line%%#*}"
            if [[ -n "${line// /}" && "$line" != *' not-run'* ]]; then
                projects+=("${line%% *}")
            fi
        done <"$here/projects.txt"
    fi
    for project in "${projects[@]}"; do
        if [[ ! -d "$root/${project%/}" ]]; then
            echo "measure.sh: there is no folder '$project'." >&2
            exit 2
        fi
    done
    for project in "${projects[@]}"; do
        run_project "$project"
    done
    ;;
*) usage ;;
esac

exit "$failed"
