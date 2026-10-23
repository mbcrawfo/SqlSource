#!/usr/bin/env bash
# Formats the C# code and the project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier.
# Builds the generator first, because `dotnet format` needs its assembly.
# Usage: format.sh [--check] [style|analyzers|csharpier ...]   Without --check, files are rewritten in place.  The
# parts name which of the three run, always in that order; without any, all three run.  CI runs the parts in separate
# jobs.
set -euo pipefail

SOLUTION='SqlSource.slnx'
GENERATOR='src/SqlSource/SqlSource.csproj'

usage() {
    echo 'Usage: format.sh [--check] [style|analyzers|csharpier ...]' >&2
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
if [[ $# -eq 0 ]]; then
    style=true
    analyzers=true
    csharpier=true
fi
for part in "$@"; do
    case "$part" in
    style) style=true ;;
    analyzers) analyzers=true ;;
    csharpier) csharpier=true ;;
    *) usage ;;
    esac
done

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
        dotnet format style "$SOLUTION"
    fi
    if [[ "$analyzers" == true ]]; then
        dotnet format analyzers "$SOLUTION"
    fi
    # CSharpier runs last so that it has the final say on layout after the dotnet format fixers have changed code.
    if [[ "$csharpier" == true ]]; then
        dotnet csharpier format .
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
    dotnet format style "$SOLUTION" --verify-no-changes || status=1
fi
if [[ "$analyzers" == true ]]; then
    dotnet format analyzers "$SOLUTION" --verify-no-changes || status=1
fi
if [[ "$csharpier" == true ]]; then
    dotnet csharpier check . || status=1
fi
exit "$status"
