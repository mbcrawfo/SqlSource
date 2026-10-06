#!/usr/bin/env bash
# Formats the C# code and the project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier.
# Builds the generator first, because `dotnet format` needs its assembly.
# Usage: format.sh [--check]   Without --check, files are rewritten in place.
set -euo pipefail

SOLUTION='SqlSource.slnx'
GENERATOR='src/SqlSource/SqlSource.csproj'

if [[ $# -gt 1 || ($# -eq 1 && "$1" != '--check') ]]; then
    echo 'Usage: format.sh [--check]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

# dotnet format compiles the solution in memory and loads the generator from its built assembly, which it does not
# build.  tests/SqlSource.Tests uses generated types, so without that assembly dotnet format reports them as missing
# (CS0246) and fails.  That is the state of a fresh clone, and of CI, where this script runs before the build.
build_generator() {
    dotnet build "$GENERATOR" --nologo --verbosity quiet
}

if [[ $# -eq 0 ]]; then
    build_generator
    # CSharpier runs last so that it has the final say on layout after the dotnet format fixers have changed code.
    dotnet format style "$SOLUTION"
    dotnet format analyzers "$SOLUTION"
    dotnet csharpier format .
    exit 0
fi

# Every check runs, so that one run reports every problem.
# Locked mode makes the restore inside dotnet format fail on a stale packages.lock.json.  Without it that restore
# would rewrite the lock file and the check would pass.
export RestoreLockedMode=true
status=0
build_generator || status=1
dotnet format style "$SOLUTION" --verify-no-changes || status=1
dotnet format analyzers "$SOLUTION" --verify-no-changes || status=1
dotnet csharpier check . || status=1
exit "$status"
