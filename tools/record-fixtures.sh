#!/usr/bin/env bash
# Records the fixture cases of the PostgreSQL describer: describes the query.sql of each case under
# tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres against a PostgreSQL in a container, and writes the case's
# capture.json and expected.json.  It is no check: a person runs it, after a change to what the describer asks or
# answers or to schema.sql, and reads the diff of what it wrote before committing it.
# It needs Docker.  The image is the one of the tests, which SQLSOURCE_TESTS_POSTGRES_IMAGE overrides.
# Usage: record-fixtures.sh
set -euo pipefail

PROJECT='tests/SqlSource.Tests.Postgres'
CASES='tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres'
TESTS='SqlSource.Tests.Postgres.Describer.FixtureCasesTests'

if [[ $# -ne 0 ]]; then
    echo 'Usage: record-fixtures.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The default image is named in one place, which is read here and not repeated.
default_image="$(sed -n 's/.*DefaultImage = "\(.*\)";.*/\1/p' "$PROJECT/PostgresContainer.cs")"
image="${SQLSOURCE_TESTS_POSTGRES_IMAGE:-$default_image}"
# A case is every folder of the engine's, as the test finds them: one with no query.sql fails its test, and the
# recording.
count="$(find "$CASES" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')"

printf 'Recording %s cases of postgres against %s ... ' "$count" "$image"
if ! output="$(SQLSOURCE_RECORD_FIXTURES=1 dotnet test --project "$PROJECT" -- --filter-class "$TESTS" 2>&1)"; then
    echo 'failed'
    echo "$output" >&2
    exit 1
fi
echo 'done'
