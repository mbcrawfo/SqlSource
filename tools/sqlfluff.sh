#!/usr/bin/env bash
# Formats and lints the repository's .sql files with sqlfluff and the sqlsource templater, run from an image that
# tools/sqlfluff.Dockerfile builds on a pinned sqlfluff image, with the templater installed from
# src/sqlfluff-templater-sqlsource, so every run uses the templater of this checkout.
# sqlfluff runs on the repository's root, whose .sqlfluff and .sqlfluffignore, with the .sqlfluff of each folder below,
# choose the files, their dialects and the rules.
# Usage: sqlfluff.sh [--check] [-- argument ...]
#   Without arguments, `sqlfluff fix .` rewrites every file it can fix.  --check runs `sqlfluff lint .`, which fails on
#   any finding.  The arguments after -- replace both, as in `sqlfluff.sh -- lint examples`, and run with the rights to
#   rewrite files.
set -euo pipefail

IMAGE='sqlfluff/sqlfluff:4.5.0@sha256:8a408e8f236e922fc128e06d634c74f4c683394970c8ffc5d44100a341e3adf0'
TAG='sqlsource-sqlfluff:local'
TEMPLATER='src/sqlfluff-templater-sqlsource'

usage() {
    echo 'Usage: sqlfluff.sh [--check] [-- argument ...]' >&2
    exit 2
}

check=false
arguments=(fix .)
if [[ $# -gt 0 && "$1" == '--check' ]]; then
    check=true
    arguments=(lint .)
    shift
fi
if [[ $# -gt 0 ]]; then
    if [[ "$1" != '--' || "$check" == true ]]; then
        usage
    fi
    shift
    if [[ $# -eq 0 ]]; then
        usage
    fi
    arguments=("$@")
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
# shellcheck source=tools/platform.sh
source tools/platform.sh

# The templater's folder is the build's context, and tools/sqlfluff.Dockerfile.dockerignore keeps out everything the
# install does not read, so the cached image is rebuilt only when the templater's package changes.
run_docker build --quiet --build-arg "SQLFLUFF_IMAGE=$IMAGE" --file tools/sqlfluff.Dockerfile --tag "$TAG" \
    "$TEMPLATER" >/dev/null

if [[ "$check" == true ]]; then
    run_docker run --rm --volume "$(native_path "$repo_root"):/sql:ro" --workdir /sql "$TAG" "${arguments[@]}"
else
    # The user and group of the caller, so that a rewritten file keeps its owner.
    run_docker run --rm --user "$(id -u):$(id -g)" --volume "$(native_path "$repo_root"):/sql" --workdir /sql "$TAG" \
        "${arguments[@]}"
fi
