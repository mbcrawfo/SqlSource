# The PostgreSQL of the example: PostgreSQL 18.6 with PostGIS installed at an exact package version, which the queries
# of CodeGen/Deliveries.sql need for their geography column.  docker-compose.yml builds it on its first start.  Only in
# this repository: both versions are those of its tests, which a test keeps in step, as rule 5 of the repository's
# AGENTS.md says.
FROM postgres:18.6

ARG POSTGIS=3.6.4+dfsg-2.pgdg13+1

# PostGIS comes from the image's own pgdg source, or from pgdg's archive, apt-archive.postgresql.org, which keeps every
# version pgdg has published, once the version has left the live source.  The archive is reached over https, so
# ca-certificates comes first.
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
