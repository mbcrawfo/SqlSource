#!/usr/bin/env bash
# Builds the PostGIS image of the tests, from tests/DatabaseTests/TestHarness/Postgis/Dockerfile on a postgres image,
# under the name that PostgisContainer of the harness looks for, which PostgisImageScriptTests of
# tests/DatabaseTests/Postgres keeps equal.  It is no check: each CI job that runs a PostGIS test calls it through
# .github/actions/postgis-image before the tests, which loads the image from the Actions cache, or builds it with this
# script so that a failed build fails a step of its own with Docker's whole output, and the tests then find the image
# and build nothing.  tools/describe-end-to-end.sh builds the image through it too.
# It builds the image of the postgres image given, or of the default image, which it reads from
# tests/DatabaseTests/TestHarness/DefaultImages.cs by the line that declares it.  The PostGIS package is read from the
# line of PostgisContainer.cs that declares Package.  It always builds, through Docker's cache.
# --name prints the image's name and builds nothing.  --id prints the ID of the image, which a CI job keeps after its
# build step, and --unchanged <id> fails when the image's ID is no longer that one, as it is not when the tests built
# the image again, which would hide a failed build in their errors once more; each job runs it after its tests.
# It needs Docker, but for --name.
# Usage: build-postgis-image.sh [--name | --id | --unchanged <id>] [postgres-image]
set -euo pipefail

IMAGES='tests/DatabaseTests/TestHarness/DefaultImages.cs'
POSTGIS_CONTAINER='tests/DatabaseTests/TestHarness/PostgisContainer.cs'
POSTGIS_DOCKERFILE='tests/DatabaseTests/TestHarness/Postgis'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

usage() {
    echo 'Usage: build-postgis-image.sh [--name | --id | --unchanged <id>] [postgres-image]' >&2
    exit 2
}

mode=build
built_id=''
case "${1:-}" in
--name | --id)
    mode="${1#--}"
    shift
    ;;
--unchanged)
    (($# >= 2)) || usage
    mode=unchanged
    built_id="$2"
    shift 2
    ;;
esac
if (($# > 1)); then
    usage
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

case "$mode" in
name)
    echo "$image"
    exit 0
    ;;
id | unchanged)
    if ! id="$(docker image inspect --format '{{.Id}}' "$image" 2>/dev/null)"; then
        echo "Docker has no image $image." >&2
        exit 1
    fi
    if [[ $mode == id ]]; then
        echo "$id"
    elif [[ $id != "$built_id" ]]; then
        echo "The tests built the PostGIS image again: $image is $id, where the build step left $built_id." >&2
        echo "PostgisContainer must build only a missing image: check WithImageBuildPolicy(PullPolicy.Missing)." >&2
        exit 1
    fi
    exit 0
    ;;
esac

echo "Building $image"
docker build --progress=plain --build-arg "PG=$tag" --build-arg "POSTGIS=$package" -t "$image" "$POSTGIS_DOCKERFILE"
