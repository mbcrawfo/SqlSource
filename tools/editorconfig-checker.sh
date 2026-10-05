#!/usr/bin/env bash
# Checks every file in the repository against .editorconfig with editorconfig-checker, run from a pinned Docker image.
# The version in the image tag must match "Version" in .editorconfig-checker.json.
set -euo pipefail

IMAGE='mstruebing/editorconfig-checker:4.0.2@sha256:2ba6232bfa0058f72f5f7d7816711590aa764afab1542af30b3ecb4553587918'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

files=()
while IFS= read -r -d '' file; do
    if [[ -f "$file" ]]; then
        files+=("$file")
    fi
done < <(git ls-files -z --cached --others --exclude-standard)

if [[ ${#files[@]} -eq 0 ]]; then
    echo 'editorconfig-checker: no files found'
    exit 0
fi

docker run --rm --volume "$repo_root:/mnt:ro" --workdir /mnt "$IMAGE" editorconfig-checker "${files[@]}"
