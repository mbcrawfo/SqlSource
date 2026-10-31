#!/usr/bin/env bash
# Writes every sidecar of the test projects' queries from nothing: it deletes them, then runs
# `sqlsource describe --force` on each project against databases in a container of its engine.  It is no check: a
# person runs it after a change to a query or a case, to a schema or to a seed of the folders below, and in the pull
# request that raises VersionPrefix, since every sidecar holds the tool's version, and reads the diff of what it wrote
# before committing it.  It has a part for each engine, which starts its own container and removes it:
# - PostgreSQL: the .sql.json files of the realistic cases under tests/DatabaseTests/Postgres/Realistic/Cases and of
#   the end-to-end queries under tests/DatabaseTests/Postgres/EndToEnd/Queries, EndToEnd/NodaTime/Queries,
#   EndToEnd/NetTopologySuite/Queries and EndToEnd/Json/Queries, against the databases "realistic", "app" and
#   "billing", each made from the schema.sql and seed.sql of Realistic/, EndToEnd/app/ and EndToEnd/billing/, and
#   "gis", made from those of EndToEnd/gis/ in a second container, of the PostGIS image that the harness's
#   PostgisContainer builds from tests/DatabaseTests/TestHarness/Postgis/Dockerfile on the same postgres image.  That
#   image is kept, as the tests keep it.
#   It then writes the sidecars of examples/PostgresExamples the same way, against a database "examples" made from its
#   schema.sql, and runs after a change to a query or to the schema of the examples too.
# - SQL Server: the .sql.json files of the realistic cases under tests/DatabaseTests/SqlServer/Realistic/Cases and of
#   the end-to-end queries under tests/DatabaseTests/SqlServer/EndToEnd/Queries, EndToEnd/NodaTime/Queries,
#   EndToEnd/NetTopologySuite/Queries and EndToEnd/Json/Queries, against the databases "realistic", "app" and
#   "billing", each made from the schema.sql and seed.sql of Realistic/, EndToEnd/app/ and EndToEnd/billing/, with
#   EndToEnd/app/new-types.sql after the seed of "app".  It then writes the sidecars of examples/SqlServerExamples the
#   same way, against a database "examples" made from its schema.sql, and runs after a change to a query or to the
#   schema of those examples too.
# It runs the tool as a project, where the other recording scripts run a test: a test project does not build until its
# sidecars are current.  Each image is the default image of its engine's tests, which the sidecars are recorded on, so
# SQLSOURCE_TESTS_POSTGRES_IMAGE and SQLSOURCE_TESTS_SQLSERVER_IMAGE are ignored.  It needs Docker.
# Usage: describe-end-to-end.sh
set -euo pipefail

IMAGES='tests/DatabaseTests/TestHarness/DefaultImages.cs'
POSTGIS_CONTAINER='tests/DatabaseTests/TestHarness/PostgisContainer.cs'
POSTGIS_DOCKERFILE='tests/DatabaseTests/TestHarness/Postgis'

POSTGRES_PROJECT='tests/DatabaseTests/Postgres'
POSTGRES_END_TO_END="$POSTGRES_PROJECT/EndToEnd"
POSTGRES_REALISTIC="$POSTGRES_PROJECT/Realistic"
POSTGRES_DATABASES=(realistic app billing)
# The database of PostGIS, on its own container.
POSTGIS_DATABASE='gis'
POSTGRES_PASSWORD='postgres'
POSTGRES_EXAMPLES='examples/PostgresExamples'
# The database of each engine's examples.
EXAMPLES_DATABASE='examples'

SQLSERVER_PROJECT='tests/DatabaseTests/SqlServer'
SQLSERVER_END_TO_END="$SQLSERVER_PROJECT/EndToEnd"
SQLSERVER_REALISTIC="$SQLSERVER_PROJECT/Realistic"
SQLSERVER_DATABASES=(realistic app billing)
SQLSERVER_EXAMPLES='examples/SqlServerExamples'
# The password of sa, which SQL Server refuses unless it has three kinds of character and eight of them.
SQLSERVER_PASSWORD='Describe-End-To-End-1'
# The image's sqlcmd: -C trusts the server's own certificate, -I sets QUOTED_IDENTIFIER on, as SqlClient does, and -b
# fails on the first error.  Keep -I, so that the files run as SqlServerContainer runs them, with QUOTED_IDENTIFIER on;
# a default image whose sqlcmd is at another path changes this path.
SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -P "$SQLSERVER_PASSWORD")

if [[ $# -ne 0 ]]; then
    echo 'Usage: describe-end-to-end.sh' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# The containers that are running, which are removed however the script ends.
postgres_container=''
postgis_container=''
sqlserver_container=''
remove_containers() {
    local container
    for container in "$postgres_container" "$postgis_container" "$sqlserver_container"; do
        if [[ -n $container ]]; then
            docker rm -f "$container" >/dev/null
        fi
    done
}
trap remove_containers EXIT

# The default image of an engine, read from the tests' line that names it, and not repeated here.
default_image() {
    local constant="$1" image
    image="$(sed -n "s/.*const string $constant = \"\\(.*\\)\";.*/\\1/p" "$IMAGES")"
    if [[ -z "$image" ]]; then
        echo "$IMAGES has no line that declares $constant as a string." >&2
        exit 1
    fi
    echo "$image"
}

# The package of PostGIS that the harness's PostgisContainer installs, read from its line, and not repeated here.
postgis_package() {
    local package
    package="$(sed -n 's/.*const string Package = "\(.*\)";.*/\1/p' "$POSTGIS_CONTAINER")"
    if [[ -z "$package" ]]; then
        echo "$POSTGIS_CONTAINER has no line that declares Package as a string." >&2
        exit 1
    fi
    echo "$package"
}

# Starts a container of a PostgreSQL image, sets the variable named by the second argument to it, so that the trap
# removes it, and waits until the server answers.
start_postgres() {
    local image="$1" variable="$2" container ready
    printf 'Starting %s ... ' "$image"
    container="$(docker run -d -e "POSTGRES_PASSWORD=$POSTGRES_PASSWORD" -p 127.0.0.1::5432 "$image")"
    printf -v "$variable" '%s' "$container"

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
}

# The port on the host of a PostgreSQL container.
postgres_port() {
    docker port "$1" 5432/tcp | head -n 1 | sed 's/.*://'
}

# Creates the database on the container and runs the schema.sql and seed.sql of the folder on it.
create_postgres_database() {
    local container="$1" database="$2" folder="$3" file
    printf 'Creating the database %s ... ' "$database"
    docker exec "$container" psql -v ON_ERROR_STOP=1 -q -U postgres -c "CREATE DATABASE $database" >/dev/null
    for file in schema.sql seed.sql; do
        docker exec -i "$container" psql -v ON_ERROR_STOP=1 -q -U postgres -d "$database" <"$folder/$file" >/dev/null
    done
    echo 'done'
}

describe_postgres() {
    local image tag package postgis_image port postgis_port database folder connection connections=()
    image="$(default_image Postgres)"

    start_postgres "$image" postgres_container
    port="$(postgres_port "$postgres_container")"

    # The schemas and seeds are the files that PostgresDatabases of the PostgreSQL project applies, in the same order.
    for database in "${POSTGRES_DATABASES[@]}"; do
        folder="$POSTGRES_END_TO_END/$database"
        if [[ $database == realistic ]]; then
            folder="$POSTGRES_REALISTIC"
        fi
        create_postgres_database "$postgres_container" "$database" "$folder"
        connection="Host=127.0.0.1;Port=$port;Username=postgres;Password=$POSTGRES_PASSWORD;Database=$database"
        connections+=(--connection "$database=$connection")
    done

    # The PostGIS image, built as PostgisContainer builds it, by the same arguments and under the same name, so that a
    # build of either reuses the other's.
    tag="${image##*:}"
    package="$(postgis_package)"
    postgis_image="sqlsource-postgis:$tag-${package%%+*}"
    printf 'Building %s ... ' "$postgis_image"
    docker build --quiet --build-arg "PG=$tag" --build-arg "POSTGIS=$package" -t "$postgis_image" \
        "$POSTGIS_DOCKERFILE" >/dev/null
    echo 'done'

    start_postgres "$postgis_image" postgis_container
    postgis_port="$(postgres_port "$postgis_container")"
    create_postgres_database "$postgis_container" "$POSTGIS_DATABASE" "$POSTGRES_END_TO_END/$POSTGIS_DATABASE"
    connection="Host=127.0.0.1;Port=$postgis_port;Username=postgres;Password=$POSTGRES_PASSWORD"
    connections+=(--connection "$POSTGIS_DATABASE=$connection;Database=$POSTGIS_DATABASE")

    find "$POSTGRES_END_TO_END/Queries" "$POSTGRES_END_TO_END/NodaTime/Queries" \
        "$POSTGRES_END_TO_END/NetTopologySuite/Queries" "$POSTGRES_END_TO_END/Json/Queries" \
        "$POSTGRES_REALISTIC/Cases" -name '*.sql.json' -type f -delete

    dotnet run --project src/SqlSource.Tool -- \
        describe --force "$POSTGRES_PROJECT/Postgres.csproj" "${connections[@]}"

    docker rm -f "$postgis_container" >/dev/null
    postgis_container=''

    # The examples: their schema on a database of their own, and their project described with that connection, under
    # the logical database that its queries belong to, named after their dialect.
    printf 'Creating the database %s ... ' "$EXAMPLES_DATABASE"
    docker exec "$postgres_container" psql -v ON_ERROR_STOP=1 -q -U postgres -c "CREATE DATABASE $EXAMPLES_DATABASE" \
        >/dev/null
    docker exec -i "$postgres_container" psql -v ON_ERROR_STOP=1 -q -U postgres -d "$EXAMPLES_DATABASE" \
        <"$POSTGRES_EXAMPLES/schema.sql" >/dev/null
    echo 'done'

    find "$POSTGRES_EXAMPLES" -name '*.sql.json' -type f -delete

    connection="Host=127.0.0.1;Port=$port;Username=postgres;Password=$POSTGRES_PASSWORD;Database=$EXAMPLES_DATABASE"
    dotnet run --project src/SqlSource.Tool -- \
        describe --force "$POSTGRES_EXAMPLES/PostgresExamples.csproj" --connection "postgres=$connection"

    docker rm -f "$postgres_container" >/dev/null
    postgres_container=''
}

describe_sqlserver() {
    local image port deadline database folder file files connection connections=()
    image="$(default_image SqlServer)"

    # The image is built for x64 alone, which an arm64 host runs under emulation.  Keep --platform linux/amd64.
    printf 'Starting %s ... ' "$image"
    sqlserver_container="$(docker run -d --platform linux/amd64 -e ACCEPT_EULA=Y \
        -e "MSSQL_SA_PASSWORD=$SQLSERVER_PASSWORD" -p 127.0.0.1::1433 "$image")"
    port="$(docker port "$sqlserver_container" 1433/tcp | head -n 1 | sed 's/.*://')"

    # A try can wait for its login as long as -l allows, so the limit is a time, not a count of tries.
    deadline=$((SECONDS + 120))
    until docker exec "$sqlserver_container" "${SQLCMD[@]}" -l 5 -Q 'SELECT 1' >/dev/null 2>&1; do
        if ((SECONDS >= deadline)); then
            echo 'failed: the server was not ready in 120 seconds' >&2
            exit 1
        fi
        sleep 1
    done
    echo 'done'

    # The schemas and seeds are the files that SqlServerDatabases of the SQL Server project applies, in the same order:
    # those of "realistic", then those of "app", with new-types.sql after them, since the default image is SQL Server
    # 2025, then those of "billing".  sqlcmd splits each at its lines of GO, and -f 65001 names the files' code page,
    # UTF-8, rather than leaving it to sqlcmd's default.  Keep -f 65001 on the run of each file, so that each is read as
    # UTF-8, as SqlServerContainer reads it.
    for database in "${SQLSERVER_DATABASES[@]}"; do
        printf 'Creating the database %s ... ' "$database"
        docker exec "$sqlserver_container" "${SQLCMD[@]}" -Q "CREATE DATABASE [$database]" >/dev/null
        folder="$SQLSERVER_END_TO_END/$database"
        if [[ $database == realistic ]]; then
            folder="$SQLSERVER_REALISTIC"
        fi
        files=(schema.sql seed.sql)
        if [[ $database == app ]]; then
            files+=(new-types.sql)
        fi
        for file in "${files[@]}"; do
            docker exec -i "$sqlserver_container" "${SQLCMD[@]}" -d "$database" -f 65001 -i /dev/stdin \
                <"$folder/$file" >/dev/null
        done
        echo 'done'
        connection="Server=127.0.0.1,$port;User ID=sa;Password=$SQLSERVER_PASSWORD;TrustServerCertificate=True"
        connections+=(--connection "$database=$connection;Database=$database")
    done

    find "$SQLSERVER_END_TO_END/Queries" "$SQLSERVER_END_TO_END/NodaTime/Queries" \
        "$SQLSERVER_END_TO_END/NetTopologySuite/Queries" "$SQLSERVER_END_TO_END/Json/Queries" \
        "$SQLSERVER_REALISTIC/Cases" -name '*.sql.json' -type f -delete

    dotnet run --project src/SqlSource.Tool -- \
        describe --force "$SQLSERVER_PROJECT/SqlServer.csproj" "${connections[@]}"

    # The examples: their schema on a database of their own, and their project described with that connection, under
    # the logical database that its queries belong to, named after their dialect.
    printf 'Creating the database %s ... ' "$EXAMPLES_DATABASE"
    docker exec "$sqlserver_container" "${SQLCMD[@]}" -Q "CREATE DATABASE [$EXAMPLES_DATABASE]" >/dev/null
    docker exec -i "$sqlserver_container" "${SQLCMD[@]}" -d "$EXAMPLES_DATABASE" -f 65001 -i /dev/stdin \
        <"$SQLSERVER_EXAMPLES/schema.sql" >/dev/null
    echo 'done'

    find "$SQLSERVER_EXAMPLES" -name '*.sql.json' -type f -delete

    connection="Server=127.0.0.1,$port;User ID=sa;Password=$SQLSERVER_PASSWORD;TrustServerCertificate=True"
    dotnet run --project src/SqlSource.Tool -- \
        describe --force "$SQLSERVER_EXAMPLES/SqlServerExamples.csproj" \
        --connection "mssql=$connection;Database=$EXAMPLES_DATABASE"

    docker rm -f "$sqlserver_container" >/dev/null
    sqlserver_container=''
}

describe_postgres
describe_sqlserver
