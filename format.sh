#!/usr/bin/env bash
# Formats the C# code and the project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier.
# Builds the generator first, because `dotnet format` needs its assembly.
# Usage: format.sh [--check] [style|analyzers|csharpier ...] [-- file ...]   Without --check, files are rewritten in
# place.  The parts name which of the three run, always in that order; without any, all three run.  CI runs the parts
# in separate jobs.  Files after -- limit all three to those files, which is how pre-commit-validation.sh checks only
# the C# files that changed.
set -euo pipefail

SOLUTION='SqlSource.slnx'
GENERATOR='src/SqlSource/SqlSource.csproj'

usage() {
    echo 'Usage: format.sh [--check] [style|analyzers|csharpier ...] [-- file ...]' >&2
    exit 2
}

check=false
if [[ $# -gt 0 && "$1" == '--check' ]]; then
    check=true
    shift
fi

style=false
analyzers=false
csharpier=false
parts=0
while [[ $# -gt 0 && "$1" != '--' ]]; do
    case "$1" in
    style) style=true ;;
    analyzers) analyzers=true ;;
    csharpier) csharpier=true ;;
    *) usage ;;
    esac
    parts=$((parts + 1))
    shift
done
if [[ "$parts" -eq 0 ]]; then
    style=true
    analyzers=true
    csharpier=true
fi

# The files are paths from the repository root, where the script runs.  Without any, dotnet format takes the whole
# solution and CSharpier the whole tree.  The ${include[@]+...} form below is there because bash 3.2, the one macOS
# ships, refuses to expand an empty array under set -u.
include=()
targets=(.)
if [[ $# -gt 0 ]]; then
    shift
    if [[ $# -eq 0 ]]; then
        usage
    fi
    include=(--include "$@")
    targets=("$@")
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

# dotnet format compiles the solution in memory and loads the generator from its built assembly, which it does not
# build.  tests/SqlSource.Tests uses generated types, so without that assembly dotnet format reports them as missing
# (CS0246) and fails.  That is the state of a fresh clone, and of CI, where this script runs before the build.
# CSharpier needs no build.
build_generator() {
    dotnet build "$GENERATOR" --nologo --verbosity quiet
}

if [[ "$check" == false ]]; then
    if [[ "$style" == true || "$analyzers" == true ]]; then
        build_generator
    fi
    if [[ "$style" == true ]]; then
        dotnet format style "$SOLUTION" ${include[@]+"${include[@]}"}
    fi
    if [[ "$analyzers" == true ]]; then
        dotnet format analyzers "$SOLUTION" ${include[@]+"${include[@]}"}
    fi
    # CSharpier runs last so that it has the final say on layout after the dotnet format fixers have changed code.
    if [[ "$csharpier" == true ]]; then
        dotnet csharpier format "${targets[@]}"
    fi
    exit 0
fi

# Every check runs, so that one run reports every problem.
# Locked mode makes the restore inside dotnet format fail on a stale packages.lock.json.  Without it that restore
# would rewrite the lock file and the check would pass.
export RestoreLockedMode=true
status=0
if [[ "$style" == true || "$analyzers" == true ]]; then
    build_generator || status=1
fi
if [[ "$style" == true ]]; then
    dotnet format style "$SOLUTION" --verify-no-changes ${include[@]+"${include[@]}"} || status=1
fi
if [[ "$analyzers" == true ]]; then
    dotnet format analyzers "$SOLUTION" --verify-no-changes ${include[@]+"${include[@]}"} || status=1
fi
if [[ "$csharpier" == true ]]; then
    dotnet csharpier check "${targets[@]}" || status=1
fi
exit "$status"
