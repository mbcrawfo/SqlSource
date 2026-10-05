#!/usr/bin/env bash
# Lints the GitHub Actions workflows with actionlint, run from a pinned Docker image.
# The image bundles ShellCheck, so `run:` blocks are checked too.
set -euo pipefail

IMAGE='rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

files=()
while IFS= read -r -d '' file; do
    if [[ -f "$file" ]]; then
        files+=("$file")
    fi
done < <(git ls-files -z --cached --others --exclude-standard -- '.github/workflows/*.yml' '.github/workflows/*.yaml')

if [[ ${#files[@]} -eq 0 ]]; then
    echo 'actionlint: no workflows found'
    exit 0
fi

docker run --rm --volume "$repo_root:/mnt:ro" --workdir /mnt "$IMAGE" -color "${files[@]}"
