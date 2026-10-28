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

# tools/test-scopes.txt, read once.  Its patterns hold no glob character, escape or extended glob operator but `*`,
# which the C# reader refuses, and `*` matches `/` in bash as it does there, so matching a path against the pattern
# unquoted gives the same answer.
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
    # A container project's own .csproj can override the compile settings and driver versions, with a VersionOverride.
    for project in "${project_paths[@]}"; do
        if [[ "$1" == "$project" ]]; then
            allocation=true
        fi
    done
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

# Every temporary file goes here, the plan's included, and none is left behind.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# The change set is what differs from HEAD, untracked files included.  It is read with -z, so that git does not quote
# a path that holds a space or a quote, and through a file, since bash 3.2 cannot see the status of a process
# substitution.  A git command that fails makes the run full, since its change set is unknown.
changes=()
git_failure=''
collect() {
    if ! "$@" >"$work/paths"; then
        echo "pre-commit-validation.sh: '$*' failed, so everything runs" >&2
        if [[ -z "$git_failure" ]]; then
            git_failure="'$*' failed"
        fi
        return
    fi
    while IFS= read -r -d '' path; do
        changes+=("$path")
    done <"$work/paths"
}
if [[ ${#planned[@]} -gt 0 ]]; then
    for path in "${planned[@]}"; do
        while [[ "$path" == ./* ]]; do
            path="${path#./}"
        done
        changes+=("$path")
    done
else
    collect git diff -z --name-only --no-renames HEAD
    collect git ls-files -z --others --exclude-standard
fi

# A clean tree is almost always a check before a push, so it runs everything.
full_reason=''
if [[ "$full" == true ]]; then
    full_reason='--full'
elif [[ -n "$git_failure" ]]; then
    full_reason="$git_failure"
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
    # shfmt takes its layout from .editorconfig.
    if [[ "$path" == *.sh || "${path##*/}" == .editorconfig ]]; then first shell_trigger; fi
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

# Each verdict is decided once, from its trigger and the reason to skip without one, into <name>_go and <name>_why.  A
# full run sets off every trigger.  The plan, the run and the summary all read these.
decide() {
    local go=true why="${full_reason:-$2}"
    if [[ -z "$why" ]]; then
        go=false
        why="$3"
    fi
    printf -v "${1}_go" '%s' "$go"
    printf -v "${1}_why" '%s' "$why"
}
no_scope='no change in its scope'
decide always always ''
decide format "$format_trigger" "$no_format"
decide shell "$shell_trigger" 'no .sh file or .editorconfig changed'
decide workflow "$workflow_trigger" 'no file under .github/workflows/ changed'
decide postgres "$postgres_trigger" "$no_scope"
decide sqlserver "$sqlserver_trigger" "$no_scope"
decide allocation "$allocation_trigger" 'no change can alter what they measure'
decide packages "$packages_trigger" 'no change reaches the packages'

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
    local name="$1" go_name="${2}_go" why_name="${2}_why"
    local go="${!go_name}" why="${!why_name}" action=run
    shift 2
    if [[ "$go" == false ]]; then
        action=skip
    fi
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

# Records each named step as skipped for one reason, as when the build fails.
skip_all() {
    local reason="$1" name
    shift
    for name in "$@"; do
        results+=("skipped  $name ($reason)")
    done
}

# A string of the solution filter, escaped for JSON.
json_string() {
    printf '"%s"' "$(printf '%s' "$1" | sed 's/[\\"]/\\&/g')"
}

# The test step runs a solution filter of every project but the container projects whose pair is skipped.  The
# projects in the filter still run in parallel, as they do for the solution.
filter_projects=''
project_plan=()
while IFS= read -r project; do
    decision=always
    for index in "${!project_paths[@]}"; do
        if [[ "${project_paths[index]}" == "$project" ]]; then
            decision="${project_engines[index]}"
        fi
    done
    go_name="${decision}_go"
    why_name="${decision}_why"
    action=skip
    if [[ "${!go_name}" == true ]]; then
        action=run
        filter_projects+="${filter_projects:+,}$(json_string "$project")"
    fi
    project_plan+=("$(printf '        %-4s  %s  (%s)' "$action" "$project" "${!why_name}")")
done < <(sed -n 's/.*<Project Path="\([^"]*\)".*/\1/p' "$SOLUTION")
test_args=()
# shellcheck disable=SC2154 # decide sets it, through printf -v.
if [[ "$allocation_go" == false ]]; then
    test_args+=(--filter-not-class '*.EndToEnd.AllocationTests')
fi

if [[ "$plan" == true ]]; then
    if [[ -n "$full_reason" ]]; then
        printf 'Full run: %s.\n' "$full_reason"
    else
        printf 'Partial run of %s changed path(s).\n' "${#changes[@]}"
    fi
else
    printf '{"solution":{"path":%s,"projects":[%s]}}\n' "$(json_string "$repo_root/$SOLUTION")" "$filter_projects" \
        >"$work/tests.slnf"
fi

# Each package check is tools/check-<name>.sh, and takes the folder that the pack writes.
PACKAGE_CHECKS=(package package-install tool-install)
package_folder="$work/packages"
step 'format' format ./format.sh "${format_args[@]}" || true
step 'editorconfig-checker' always tools/editorconfig-checker.sh || true
step 'shellcheck' shell tools/shellcheck.sh || true
step 'shfmt' shell tools/shfmt.sh --check || true
step 'actionlint' workflow tools/actionlint.sh || true
if step 'build' always dotnet build "$SOLUTION"; then
    step 'test' always dotnet test --solution "$work/tests.slnf" --no-build ${test_args[@]+"${test_args[@]}"} || true
    if [[ "$plan" == true ]]; then
        printf '%s\n' "${project_plan[@]}"
    fi
    step 'postgres tests' postgres
    step 'sqlserver tests' sqlserver
    step 'allocation tests' allocation
    if step 'pack' packages dotnet pack "$SOLUTION" --output "$package_folder"; then
        for check in "${PACKAGE_CHECKS[@]}"; do
            step "$check" packages "tools/check-$check.sh" "$package_folder" || true
        done
    else
        skip_all 'the pack failed' "${PACKAGE_CHECKS[@]}"
    fi
else
    skip_all 'the build failed' test pack "${PACKAGE_CHECKS[@]}"
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
