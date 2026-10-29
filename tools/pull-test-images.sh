#!/usr/bin/env bash
# Pulls the Docker images that the tests start, one `docker pull` each, and prints the seconds each took.  It is a step
# and no check: the build job of build.yml and the two allocation jobs of nightly.yml run it before the tests, so that
# the time a pull takes shows apart from the time a test takes, and a person can run it to have the images before a
# first test run.
# It pulls the default image of each engine, which it reads from tests/DatabaseTests/TestHarness/DefaultImages.cs by
# the line that declares it, and every image given as an argument, such as the floor images.
# It needs Docker.
# Usage: pull-test-images.sh [image ...]
set -euo pipefail

IMAGES='tests/DatabaseTests/TestHarness/DefaultImages.cs'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The default images are named in one place, which is read here and not repeated.
postgres_image="$(sed -n 's/.*const string Postgres = "\(.*\)";.*/\1/p' "$IMAGES")"
if [[ -z "$postgres_image" ]]; then
    echo "$IMAGES has no line that declares Postgres as a string." >&2
    exit 1
fi
sql_server_image="$(sed -n 's/.*const string SqlServer = "\(.*\)";.*/\1/p' "$IMAGES")"
if [[ -z "$sql_server_image" ]]; then
    echo "$IMAGES has no line that declares SqlServer as a string." >&2
    exit 1
fi

# An image that is given twice, or that is a default too, is pulled twice: the second is fast, and shows as such.
for image in "$postgres_image" "$sql_server_image" "$@"; do
    start="$SECONDS"
    docker pull --quiet "$image" >/dev/null
    echo "$image: $((SECONDS - start)) s"
done
