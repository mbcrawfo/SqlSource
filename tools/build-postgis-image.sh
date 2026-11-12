#!/usr/bin/env bash
# Builds the PostGIS image of the tests, from tests/DatabaseTests/TestHarness/Postgis/Dockerfile on a postgres image,
# under the name that PostgisContainer of the harness looks for, which PostgisImageScriptTests of
# tests/DatabaseTests/Postgres keeps equal.  It is a step and no check: each CI job that runs a PostGIS test runs it
# before the tests, so that a failed build fails a step of its own with Docker's whole output, and the tests then find
# the image and build nothing.  tools/describe-end-to-end.sh builds the image through it too.
# It builds the image of the postgres image given, or of the default image, which it reads from
# tests/DatabaseTests/TestHarness/DefaultImages.cs by the line that declares it.  The PostGIS package is read from the
# line of PostgisContainer.cs that declares Package.  It always builds, through Docker's cache.
# --name prints the image's name and builds nothing.
# It needs Docker, but for --name.
# Usage: build-postgis-image.sh [--name] [postgres-image]
set -euo pipefail

IMAGES='tests/DatabaseTests/TestHarness/DefaultImages.cs'
POSTGIS_CONTAINER='tests/DatabaseTests/TestHarness/PostgisContainer.cs'
POSTGIS_DOCKERFILE='tests/DatabaseTests/TestHarness/Postgis'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

name_only=false
if [[ ${1:-} == --name ]]; then
    name_only=true
    shift
fi
if (($# > 1)); then
    echo 'Usage: build-postgis-image.sh [--name] [postgres-image]' >&2
    exit 2
fi

postgres_image="${1:-}"
if [[ -z "$postgres_image" ]]; then
    postgres_image="$(sed -n 's/.*const string Postgres = "\(.*\)";.*/\1/p' "$IMAGES")"
    if [[ -z "$postgres_image" ]]; then
        echo "$IMAGES has no line that declares Postgres as a string." >&2
        exit 1
    fi
fi

package="$(sed -n 's/.*const string Package = "\(.*\)";.*/\1/p' "$POSTGIS_CONTAINER")"
if [[ -z "$package" ]]; then
    echo "$POSTGIS_CONTAINER has no line that declares Package as a string." >&2
    exit 1
fi

# The name is PostgisContainer.ImageOf's: the postgres tag, the PostGIS version, and the first twelve hex digits of the
# SHA-256 of the Dockerfile without its carriage returns.
tag="${postgres_image##*:}"
hash="$(tr -d '\r' <"$POSTGIS_DOCKERFILE/Dockerfile" | shasum -a 256 | cut -c1-12)"
image="sqlsource-postgis:$tag-${package%%+*}-$hash"

if [[ $name_only == true ]]; then
    echo "$image"
    exit 0
fi

echo "Building $image"
docker build --progress=plain --build-arg "PG=$tag" --build-arg "POSTGIS=$package" -t "$image" "$POSTGIS_DOCKERFILE"
