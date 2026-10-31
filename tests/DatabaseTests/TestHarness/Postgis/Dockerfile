# The PostGIS image of the NetTopologySuite tests: the run's pinned postgres image with PostGIS installed at an exact
# package version.  PostgisContainer of the harness and tools/describe-end-to-end.sh build it, passing both arguments,
# so no build reads the defaults; PostgisContainerTests keeps POSTGIS's equal to PostgisContainer.Package.  See
# tests/DatabaseTests/Postgres/README.md.
ARG PG=18.6
FROM postgres:${PG}

ARG POSTGIS=3.6.4+dfsg-2.pgdg13+1

# The archive line is the image's own pgdg line, with its key, pointed at apt-archive.postgresql.org, which keeps every
# version pgdg has published; it needs https, so ca-certificates comes first.  The spike's one sed expression is split
# into its two halves, live and archive, to keep each line within 120 characters.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends ca-certificates; \
    live='^deb \(\[[^]]*\]\) http://apt.postgresql.org/pub/repos/apt \([a-z]*\)-pgdg .*'; \
    archive='deb \1 https://apt-archive.postgresql.org/pub/repos/apt \2-pgdg-archive main'; \
    sed -n "s|$live|$archive|p" /etc/apt/sources.list.d/pgdg.list > /etc/apt/sources.list.d/pgdg-archive.list; \
    test -s /etc/apt/sources.list.d/pgdg-archive.list; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        "postgresql-${PG_MAJOR}-postgis-3=${POSTGIS}" \
        "postgresql-${PG_MAJOR}-postgis-3-scripts=${POSTGIS}"; \
    rm -rf /var/lib/apt/lists/*
