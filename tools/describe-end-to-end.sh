#!/usr/bin/env bash
# Writes every sidecar of the end-to-end queries, the .sql.json files under
# tests/SqlSource.Tests.Postgres/EndToEnd/Queries, from nothing: it deletes them, then runs `sqlsource describe --force`
# on the project against the databases "app" and "billing" of a PostgreSQL in a container.  It is no check: a person
# runs it after a change to a query, to a schema or to a seed of tests/SqlSource.Tests.Postgres/EndToEnd, and in the
# pull request that raises VersionPrefix, since every sidecar holds the tool's version, and reads the diff of what it
# wrote before committing it.
# It starts its own container and runs the tool as a project, where the other recording scripts run a test: the test
# project does not build until its sidecars are current.  The image is the default image of the tests, which the
# sidecars are recorded on, so SQLSOURCE_TESTS_POSTGRES_IMAGE is ignored.  It needs Docker.
# It writes the sidecars of the realistic cases, under tests/SqlSource.Tests.Postgres/Realistic/Cases, in the same run,
# against a database "realistic" made from the schema.sql and seed.sql of Realistic/, and runs after a change to a
# case, the schema or the seed of the corpus too.
# Usage: describe-end-to-end.sh
set -euo pipefail

PROJECT='tests/SqlSource.Tests.Postgres'
END_TO_END="$PROJECT/EndToEnd"
REALISTIC="$PROJECT/Realistic"
DATABASES=(realistic app billing)
PASSWORD='postgres'

if [[ $# -ne 0 ]]; then
    echo 'Usage: describe-end-to-end.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The default image is named in one place, which is read here and not repeated.
image="$(sed -n 's/.*DefaultImage = "\(.*\)";.*/\1/p' "$PROJECT/PostgresContainer.cs")"

printf 'Starting %s ... ' "$image"
container="$(docker run -d -e "POSTGRES_PASSWORD=$PASSWORD" -p 127.0.0.1::5432 "$image")"
trap 'docker rm -f "$container" >/dev/null' EXIT
port="$(docker port "$container" 5432/tcp | head -n 1 | sed 's/.*://')"

# The entrypoint's first server listens on its socket alone, so the check asks over TCP, as the tool will.
ready=false
for _ in $(seq 60); do
    if docker exec "$container" pg_isready -h 127.0.0.1 -U postgres >/dev/null 2>&1; then
        ready=true
        break
    fi
    sleep 1
done
if [[ $ready != true ]]; then
    echo 'failed: the server was not ready in 60 seconds' >&2
    exit 1
fi
echo 'done'

# The schemas and seeds are the files that PostgresContainer applies, in the same order.
connections=()
for database in "${DATABASES[@]}"; do
    printf 'Creating the database %s ... ' "$database"
    docker exec "$container" psql -v ON_ERROR_STOP=1 -q -U postgres -c "CREATE DATABASE $database" >/dev/null
    folder="$END_TO_END/$database"
    if [[ $database == realistic ]]; then
        folder="$REALISTIC"
    fi
    for file in schema.sql seed.sql; do
        docker exec -i "$container" psql -v ON_ERROR_STOP=1 -q -U postgres -d "$database" \
            <"$folder/$file" >/dev/null
    done
    echo 'done'
    connection="Host=127.0.0.1;Port=$port;Username=postgres;Password=$PASSWORD;Database=$database"
    connections+=(--connection "$database=$connection")
done

find "$END_TO_END/Queries" "$REALISTIC/Cases" -name '*.sql.json' -type f -delete

dotnet run --project src/SqlSource.Tool -- \
    describe --force "$PROJECT/SqlSource.Tests.Postgres.csproj" "${connections[@]}"
