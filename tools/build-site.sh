#!/usr/bin/env bash
# Builds the manual's site from site/ into artifacts/site with docfx, the local tool of dotnet-tools.json, which
# `dotnet tool restore` installs.  Any docfx warning fails the build: a link to a page or an anchor that is not there.
# Then fails when the output holds a page built from an AGENTS.md or a CLAUDE*.md, which site/docfx.json excludes, so a
# broken exclude is caught by its effect.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

output='artifacts/site'
rm -rf "$output"
dotnet docfx site/docfx.json --warningsAsErrors

guidance=()
while IFS= read -r -d '' file; do
    guidance+=("$file")
done < <(find "$output" -type f \( -iname 'agents.html' -o -iname 'claude*.html' \) -print0)

if [[ ${#guidance[@]} -gt 0 ]]; then
    echo "build-site.sh: the site holds agent guidance, which site/docfx.json must exclude:" >&2
    printf '  %s\n' "${guidance[@]}" >&2
    exit 1
fi
