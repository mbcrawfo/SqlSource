#!/usr/bin/env bash
# Lints every shell script in the repository with ShellCheck, run from a pinned Docker image.
set -euo pipefail

IMAGE='koalaman/shellcheck:v0.11.0@sha256:61862eba1fcf09a484ebcc6feea46f1782532571a34ed51fedf90dd25f925a8d'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

files=()
while IFS= read -r -d '' file; do
    if [[ -f "$file" ]]; then
        files+=("$file")
    fi
done < <(git ls-files -z --cached --others --exclude-standard -- '*.sh')

if [[ ${#files[@]} -eq 0 ]]; then
    echo 'shellcheck: no shell scripts found'
    exit 0
fi

docker run --rm --volume "$repo_root:/mnt:ro" --workdir /mnt "$IMAGE" "${files[@]}"
