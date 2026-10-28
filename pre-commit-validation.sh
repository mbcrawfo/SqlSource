#!/usr/bin/env bash
# Runs the validations that the uncommitted change could break: formatting, linting, build, tests and the three
# package checks.  A step, a pair of container test projects or the allocation tests are skipped when nothing in the
# change reaches them.  --full runs everything, as a clean tree and a changed path of no known kind do.
# Rewrites nothing.  Every step runs even when an earlier one fails, except that the tests and the package checks are
# skipped when the build fails, and the package checks when the pack fails.
# Usage: pre-commit-validation.sh [--full] [--plan [path ...]]   --plan prints what a run would do and why, and runs
# nothing.  The paths after it replace the change set, so the rules can be tried without editing the tree.
set -euo pipefail

SOLUTION='SqlSource.slnx'
SCOPES='tools/test-scopes.txt'

usage() {
    echo 'Usage: pre-commit-validation.sh [--full] [--plan [path ...]]' >&2
    exit 2
}

full=false
plan=false
planned=()
while [[ $# -gt 0 ]]; do
    case "$1" in
    --full) full=true ;;
    --plan)
        plan=true
        shift
        planned=("$@")
        break
        ;;
    *) usage ;;
    esac
    shift
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

# The decisions, one function each, each taking a path from the repository root and setting its result.  The step
# rules are positive triggers, so a kind of file that none of them knows makes the run full instead of skipping its
# checks.
is_known_kind() {
    known=true
    case "$1" in
    *.cs | *.csproj | *.props | *.targets | *.slnx | *.md | *.sql | *.json | *.sh | *.txt) return ;;
    .github/workflows/*.yml | .github/workflows/*.yaml | examples/*/docker-compose.yml) return ;;
    esac
    case "${1##*/}" in
    .editorconfig | .gitignore | .worktreeinclude) return ;;
    esac
    known=false
}

# The files that the formatters read as configuration, so a change to one can change the verdict on any file:
# Directory.Packages.props pins the analyzers and dotnet-tools.json pins CSharpier.  CSharpier formats the .slnx files
# too, and they reach it only this way, since the changed files that format is limited to are the .cs files.
formats_everything() {
    everything=false
    case "${1##*/}" in
    .editorconfig | *.globalconfig | *.props | *.targets | *.csproj | *.slnx | global.json | dotnet-tools.json)
        everything=true
        ;;
    .csharpierrc* | .csharpierignore) everything=true ;;
    esac
}

# tools/test-scopes.txt, read once.  Its patterns hold no glob character but `*`, which matches `/` in bash as it does
# in the C# reader, so matching a path against the pattern unquoted gives the same answer.
scope_names=()
scope_patterns=()
project_engines=()
project_paths=()
while read -r kind second third || [[ -n "$kind" ]]; do
    case "$kind" in
    '' | '#'*) ;;
    project)
        project_engines+=("$second")
        project_paths+=("$third")
        ;;
    *)
        scope_names+=("$kind")
        scope_patterns+=("$second")
        ;;
    esac
    kind=''
done <"$SCOPES"

scope_of() {
    local index
    for index in "${!scope_patterns[@]}"; do
        # shellcheck disable=SC2053 # Unquoted, so that the pattern's `*` matches.
        if [[ "$1" == ${scope_patterns[index]} ]]; then
            scope="${scope_names[index]}"
            return
        fi
    done
    scope=both
}

# A path that can change what the allocation tests measure: the generated code, the shared test code, the queries and
# the classes that the tests call, the compile settings and driver versions, or the tests themselves.
needs_allocation() {
    allocation=false
    case "$1" in
    src/SqlSource/*.md) ;;
    src/SqlSource/* | tests/SqlSource.Testing/* | tests/SqlSource.Tests.*/EndToEnd/*) allocation=true ;;
    */*) ;;
    Directory.*.props) allocation=true ;;
    esac
    case "${1##*/}" in
    *Allocation*) allocation=true ;;
    esac
}

# A path that can change what is packed or how the packages are checked.  README.md is the generator's readme.
needs_packages() {
    packages=false
    case "$1" in
    src/* | tools/package-install/* | tools/check-*.sh) packages=true ;;
    */*) ;;
    Directory.*.props | global.json | *.slnx | dotnet-tools.json | README.md) packages=true ;;
    esac
}

# The change set is what differs from HEAD, untracked files included.  It is read with -z, so that git does not quote
# a path that holds a space or a quote.
changes=()
if [[ ${#planned[@]} -gt 0 ]]; then
    changes=("${planned[@]}")
else
    while IFS= read -r -d '' path; do
        changes+=("$path")
    done < <(git diff -z --name-only --no-renames HEAD && git ls-files -z --others --exclude-standard)
fi

# A clean tree is almost always a check before a push, so it runs everything.
full_reason=''
if [[ "$full" == true ]]; then
    full_reason='--full'
elif [[ ${#changes[@]} -eq 0 ]]; then
    full_reason='the tree is clean'
fi

# Each trigger holds the first path that sets it off, which is the reason that the plan and the summary give.
first() {
    if [[ -z "${!1}" ]]; then
        printf -v "$1" '%s' "$path"
    fi
}
unknown=''
format_all=''
format_files=()
no_format='no .cs file changed'
shell_trigger=''
workflow_trigger=''
postgres_trigger=''
sqlserver_trigger=''
allocation_trigger=''
packages_trigger=''
for path in ${changes[@]+"${changes[@]}"}; do
    is_known_kind "$path"
    formats_everything "$path"
    # The scopes file places every path, so a change to it can move any path into either pair.
    scope=both
    if [[ "$path" != "$SCOPES" ]]; then scope_of "$path"; fi
    needs_allocation "$path"
    needs_packages "$path"
    if [[ "$known" == false ]]; then first unknown; fi
    if [[ "$everything" == true ]]; then first format_all; fi
    if [[ "$path" == *.sh ]]; then first shell_trigger; fi
    if [[ "$path" == .github/workflows/* ]]; then first workflow_trigger; fi
    if [[ "$scope" == postgres || "$scope" == both ]]; then first postgres_trigger; fi
    if [[ "$scope" == sqlserver || "$scope" == both ]]; then first sqlserver_trigger; fi
    if [[ "$allocation" == true ]]; then first allocation_trigger; fi
    if [[ "$packages" == true ]]; then first packages_trigger; fi
    # A deleted file has nothing to format, but its scopes still apply.
    if [[ "$path" == *.cs && -f "$path" ]]; then
        format_files+=("$path")
    elif [[ "$path" == *.cs ]]; then
        no_format='no changed .cs file exists'
    fi
done
if [[ -n "$unknown" && -z "$full_reason" ]]; then
    full_reason="$unknown is of no known kind"
fi

format_trigger=''
format_args=(--check)
if [[ -n "$format_all" ]]; then
    format_trigger="everything, for $format_all"
elif [[ ${#format_files[@]} -gt 0 ]]; then
    format_trigger="${#format_files[@]} changed .cs file(s)"
    if [[ -z "$full_reason" ]]; then
        format_args+=(-- "${format_files[@]}")
    fi
fi

# Sets go, action and why from a trigger and the reason to skip without one.  A full run sets off every trigger.
verdict() {
    go=true action=run why="${full_reason:-$1}"
    if [[ -z "$why" ]]; then
        go=false action=skip why="$2"
    fi
}

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

# Runs a step when its verdict says so, and records the skip otherwise.  Under --plan it prints the verdict instead.
# Without a command it only records the decision, as for a pair of test projects that the test step leaves out.
step() {
    local name="$1"
    verdict "$2" "$3"
    shift 3
    if [[ "$plan" == true ]]; then
        printf '  %-4s  %-20s  %s\n' "$action" "$name" "$why"
        return 0
    fi
    if [[ "$go" == false ]]; then
        results+=("skipped  $name ($why)")
        return 0
    fi
    if [[ $# -gt 0 ]]; then
        run_step "$name" "$@"
    fi
}

# The test step runs a solution filter of every project but the container projects whose pair is skipped.  The
# projects in the filter still run in parallel, as they do for the solution.
filter_projects=''
project_plan=()
while IFS= read -r project; do
    verdict always ''
    for index in "${!project_paths[@]}"; do
        if [[ "${project_paths[index]}" == "$project" ]]; then
            case "${project_engines[index]}" in
            postgres) verdict "$postgres_trigger" 'no change in its scope' ;;
            sqlserver) verdict "$sqlserver_trigger" 'no change in its scope' ;;
            esac
        fi
    done
    project_plan+=("$(printf '        %-4s  %s  (%s)' "$action" "$project" "$why")")
    if [[ "$go" == true ]]; then
        filter_projects+="${filter_projects:+,}\"$project\""
    fi
done < <(sed -n 's/.*<Project Path="\([^"]*\)".*/\1/p' "$SOLUTION")
test_args=()
verdict "$allocation_trigger" ''
if [[ "$go" == false ]]; then
    test_args+=(--filter-not-class '*.EndToEnd.AllocationTests')
fi

if [[ "$plan" == true ]]; then
    if [[ -n "$full_reason" ]]; then
        printf 'Full run: %s.\n' "$full_reason"
    else
        printf 'Partial run of %s changed path(s).\n' "${#changes[@]}"
    fi
else
    work="$(mktemp -d)"
    trap 'rm -rf "$work"' EXIT
    printf '{"solution":{"path":"%s","projects":[%s]}}\n' "$repo_root/$SOLUTION" "$filter_projects" \
        >"$work/tests.slnf"
fi

step 'format' "$format_trigger" "$no_format" ./format.sh "${format_args[@]}" || true
step 'editorconfig-checker' always '' tools/editorconfig-checker.sh || true
step 'shellcheck' "$shell_trigger" 'no .sh file changed' tools/shellcheck.sh || true
step 'shfmt' "$shell_trigger" 'no .sh file changed' tools/shfmt.sh --check || true
step 'actionlint' "$workflow_trigger" 'no file under .github/workflows/ changed' tools/actionlint.sh || true
if step 'build' always '' dotnet build "$SOLUTION"; then
    step 'test' always '' dotnet test --solution "${work:-}/tests.slnf" --no-build ${test_args[@]+"${test_args[@]}"} ||
        true
    if [[ "$plan" == true ]]; then
        printf '%s\n' "${project_plan[@]}"
    fi
    step 'postgres tests' "$postgres_trigger" 'no change in its scope'
    step 'sqlserver tests' "$sqlserver_trigger" 'no change in its scope'
    step 'allocation tests' "$allocation_trigger" 'no change can alter what they measure'
    no_packages='no change reaches the packages'
    if step 'pack' "$packages_trigger" "$no_packages" dotnet pack "$SOLUTION" --output "${work:-}/packages"; then
        step 'package' "$packages_trigger" "$no_packages" tools/check-package.sh "${work:-}/packages" || true
        step 'package-install' "$packages_trigger" "$no_packages" \
            tools/check-package-install.sh "${work:-}/packages" || true
        step 'tool-install' "$packages_trigger" "$no_packages" tools/check-tool-install.sh "${work:-}/packages" ||
            true
    else
        results+=('skipped  package (the pack failed)' 'skipped  package-install (the pack failed)'
            'skipped  tool-install (the pack failed)')
    fi
else
    results+=('skipped  test (the build failed)' 'skipped  pack (the build failed)'
        'skipped  package (the build failed)' 'skipped  package-install (the build failed)'
        'skipped  tool-install (the build failed)')
fi

if [[ "$plan" == true ]]; then
    exit 0
fi
printf '\nSummary:\n'
printf '  %s\n' "${results[@]}"
if [[ -n "$full_reason" ]]; then
    printf '\nFull run: %s.\n' "$full_reason"
else
    printf '\nPartial run: run ./pre-commit-validation.sh --full before pushing.\n'
fi
exit "$failed"
