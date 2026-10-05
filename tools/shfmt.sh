#!/usr/bin/env bash
# Formats every shell script in the repository with shfmt, run from a pinned Docker image.
# Usage: shfmt.sh [--check]   Without --check, files are rewritten in place.
set -euo pipefail

IMAGE='mvdan/shfmt:v3.14.1@sha256:8c06884a35683d8763fba6c0d9484d11a61a96da65f8d497d6f625825a6043f2'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

files=()
while IFS= read -r -d '' file; do
    if [[ -f "$file" ]]; then
        files+=("$file")
    fi
done < <(git ls-files -z --cached --others --exclude-standard -- '*.sh')

if [[ ${#files[@]} -eq 0 ]]; then
    echo 'shfmt: no shell scripts found'
    exit 0
fi

if [[ "${1:-}" == '--check' ]]; then
    docker run --rm --volume "$repo_root:/mnt:ro" --workdir /mnt "$IMAGE" --diff "${files[@]}"
else
    docker run --rm --user "$(id -u):$(id -g)" --volume "$repo_root:/mnt" --workdir /mnt "$IMAGE" \
        --write "${files[@]}"
fi
