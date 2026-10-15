#!/usr/bin/env bash
# Records the expectations of the realistic cases of the PostgreSQL describer: describes the query.sql of each case
# under tests/SqlSource.Tests.Postgres/Realistic/Cases against a PostgreSQL in a container, and writes the case's
# expected-pg{major}.txt for the major version of that server.  It never writes a case.json, whose truth is written
# by hand.  It is no check: a person runs it, after a change to what the describer asks or answers or to the
# schema, the seed or a case, and reads the diff of what it wrote before committing it.
# It needs Docker.  The image is the one of the tests, which SQLSOURCE_TESTS_POSTGRES_IMAGE overrides.  The
# expectations are those of PostgreSQL 14 and 18, so recording both is two runs, the second with
# SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.
# Usage: record-realistic.sh
set -euo pipefail

PROJECT='tests/SqlSource.Tests.Postgres'
CASES='tests/SqlSource.Tests.Postgres/Realistic/Cases'
TEST='*Describe_Case_IsItsExpectation'

if [[ $# -ne 0 ]]; then
    echo 'Usage: record-realistic.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The default image is named in one place, which is read here and not repeated.
default_image="$(sed -n 's/.*DefaultImage = "\(.*\)";.*/\1/p' "$PROJECT/PostgresContainer.cs")"
image="${SQLSOURCE_TESTS_POSTGRES_IMAGE:-$default_image}"
# A case is a folder that holds a query.sql, which is what the test finds.
count="$(find "$CASES" -mindepth 2 -maxdepth 2 -type f -name query.sql | wc -l | tr -d ' ')"

printf 'Recording %s realistic cases against %s ... ' "$count" "$image"
if ! output="$(SQLSOURCE_RECORD_REALISTIC=1 dotnet test --project "$PROJECT" -- --filter-method "$TEST" 2>&1)"; then
    echo 'failed'
    echo "$output" >&2
    exit 1
fi
echo 'done'
