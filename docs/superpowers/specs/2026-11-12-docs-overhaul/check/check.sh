#!/usr/bin/env bash
# The checker of the docs overhaul epic, which the epic's phase 0 brief specifies.  Run it from the repository:
#   docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh <command> [<nn> | <page>]
# It builds check.cs, a .NET file-based app beside this script, then runs it on the root of the repository, and exits
# with its code: 0 when the command passes, 1 when it finds something, 2 when it cannot run.
set -euo pipefail

for tool in git dotnet; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "check.sh: $tool is not on the PATH" >&2
        exit 2
    fi
done

if ! root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
    echo 'check.sh: run it inside the repository' >&2
    exit 2
fi
program="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/check.cs"

# A build that fails is a checker that cannot run: its code is 2, where dotnet run would give 1.
if ! build="$(dotnet build "$program" --nologo --verbosity quiet 2>&1)"; then
    printf '%s\n' "$build" >&2
    exit 2
fi
exec dotnet run --file "$program" --no-build -- "$root" "$@"
