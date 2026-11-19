#!/usr/bin/env bash
# Checks every file in the repository against .editorconfig with editorconfig-checker, run from a pinned Docker image.
# The version in the image tag must match "Version" in .editorconfig-checker.json.
set -euo pipefail

IMAGE='mstruebing/editorconfig-checker:4.0.2@sha256:2ba6232bfa0058f72f5f7d7816711590aa764afab1542af30b3ecb4553587918'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
# shellcheck source=tools/platform.sh
source tools/platform.sh

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

# The files go to the checker in batches whose paths total at most BATCH_CHARACTERS, since Windows refuses a command
# line of more than 32,767 characters and the repository's paths exceed that.  Every batch runs, and any that fails
# fails the check.
BATCH_CHARACTERS=24000
volume="$(native_path "$repo_root"):/mnt:ro"
status=0
batch=()
length=0
check_batch() {
    if [[ ${#batch[@]} -gt 0 ]]; then
        run_docker run --rm --volume "$volume" --workdir /mnt "$IMAGE" editorconfig-checker "${batch[@]}" || status=1
    fi
    batch=()
    length=0
}
for file in "${files[@]}"; do
    if [[ $((length + ${#file} + 1)) -gt $BATCH_CHARACTERS ]]; then
        check_batch
    fi
    batch+=("$file")
    length=$((length + ${#file} + 1))
done
check_batch
exit "$status"
