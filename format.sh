#!/usr/bin/env bash
# Formats the C# code and the project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier.
# Usage: format.sh [--check]   Without --check, files are rewritten in place.
set -euo pipefail

SOLUTION='SqlSource.slnx'

if [[ $# -gt 1 || ($# -eq 1 && "$1" != '--check') ]]; then
    echo 'Usage: format.sh [--check]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

if [[ $# -eq 0 ]]; then
    # CSharpier runs last so that it has the final say on layout after the dotnet format fixers have changed code.
    dotnet format style "$SOLUTION"
    dotnet format analyzers "$SOLUTION"
    dotnet csharpier format .
    exit 0
fi

# Every check runs, so that one run reports every problem.
status=0
dotnet format style "$SOLUTION" --verify-no-changes || status=1
dotnet format analyzers "$SOLUTION" --verify-no-changes || status=1
dotnet csharpier check . || status=1
exit "$status"
