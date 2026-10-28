#!/usr/bin/env bash
# Records the fixture cases of a describer: describes the query.sql of each case under
# tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine> against that engine's server in a container, and writes the
# case's capture.json and expected.json.  It is no check: a person runs it, after a change to what the describer asks
# or answers or to the engine's schema.sql, and reads the diff of what it wrote before committing it.
# It needs Docker.  The image is the one of the tests, which SQLSOURCE_TESTS_POSTGRES_IMAGE or
# SQLSOURCE_TESTS_SQLSERVER_IMAGE overrides.
# Usage: record-fixtures.sh [postgres|mssql]   (both engines when none is given)
set -euo pipefail

IMAGES='tests/SqlSource.Testing/DefaultImages.cs'

usage() {
    echo 'Usage: record-fixtures.sh [postgres|mssql]' >&2
    exit 2
}

# Sets the row of an engine: its test project, the folder of its cases, its test class, the variable that names
# another image and the constant of DefaultImages.cs that names the default one.
engine_row() {
    case "$1" in
    postgres)
        project='tests/DatabaseTests/Postgres'
        cases='tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres'
        tests='SqlSource.DatabaseTests.Postgres.Describer.FixtureCasesTests'
        image_variable='SQLSOURCE_TESTS_POSTGRES_IMAGE'
        image_constant='Postgres'
        ;;
    mssql)
        project='tests/DatabaseTests/SqlServer'
        cases='tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql'
        tests='SqlSource.DatabaseTests.SqlServer.Describer.FixtureCasesTests'
        image_variable='SQLSOURCE_TESTS_SQLSERVER_IMAGE'
        image_constant='SqlServer'
        ;;
    *)
        usage
        ;;
    esac
}

record() {
    local engine="$1" project cases tests image_variable image_constant
    engine_row "$engine"

    # The default image is named in one place, which is read here and not repeated.
    local default_image
    default_image="$(sed -n "s/.*const string $image_constant = \"\\(.*\\)\";.*/\\1/p" "$IMAGES")"
    if [[ -z "$default_image" ]]; then
        echo "$IMAGES has no line that declares $image_constant as a string." >&2
        exit 1
    fi
    local image="${!image_variable:-$default_image}"
    # A case is every folder of the engine's, as the test finds them: one with no query.sql fails its test, and the
    # recording.
    local count
    count="$(find "$cases" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')"

    printf 'Recording %s cases of %s against %s ... ' "$count" "$engine" "$image"
    local output
    if ! output="$(SQLSOURCE_RECORD_FIXTURES=1 dotnet test --project "$project" -- --filter-class "$tests" 2>&1)"; then
        echo 'failed'
        echo "$output" >&2
        exit 1
    fi
    echo 'done'
}

if [[ $# -gt 1 ]]; then
    usage
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ $# -eq 1 ]]; then
    # Only checks the argument, before anything runs: an engine with no row prints the usage.  record sets the row
    # again for itself.
    engine_row "$1"
    record "$1"
else
    record postgres
    record mssql
fi
