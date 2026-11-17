#!/usr/bin/env bash
# Formats the C# code and the project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier, and the
# Python of the sqlfluff templater with ruff, through uv.
# Builds the generator first, because `dotnet format` needs its assembly.
# Usage: format.sh [--check] [style|analyzers|csharpier|ruff ...] [-- file ...]   Without --check, files are rewritten
# in place.  The parts name which of the four run, always in that order; without any, all four run.  CI runs the parts
# in separate jobs.  Files after -- limit the three C# parts to those files, which is how pre-commit-validation.sh
# checks only the C# files that changed; ruff ignores them and takes the whole templater project.
set -euo pipefail

SOLUTION='SqlSource.slnx'
GENERATOR='src/SqlSource/SqlSource.csproj'

usage() {
    echo 'Usage: format.sh [--check] [style|analyzers|csharpier|ruff ...] [-- file ...]' >&2
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
ruff=false
parts=0
while [[ $# -gt 0 && "$1" != '--' ]]; do
    case "$1" in
    style) style=true ;;
    analyzers) analyzers=true ;;
    csharpier) csharpier=true ;;
    ruff) ruff=true ;;
    *) usage ;;
    esac
    parts=$((parts + 1))
    shift
done
if [[ "$parts" -eq 0 ]]; then
    style=true
    analyzers=true
    csharpier=true
    ruff=true
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

TEMPLATER='src/sqlfluff-templater-sqlsource'

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
    # uv runs in the project's folder, so that it reads .python-version and uv.lock, and creates a missing .venv.
    if [[ "$ruff" == true ]]; then
        uv run --directory "$TEMPLATER" ruff check --fix
        uv run --directory "$TEMPLATER" ruff format
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
if [[ "$ruff" == true ]]; then
    uv run --directory "$TEMPLATER" ruff check || status=1
    uv run --directory "$TEMPLATER" ruff format --check || status=1
fi
exit "$status"
