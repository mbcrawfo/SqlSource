# The image of tools/sqlfluff.sh: sqlfluff, with the sqlsource templater installed from its source folder, which is the
# build's context.  tools/sqlfluff.sh passes the pinned sqlfluff image as SQLFLUFF_IMAGE.
ARG SQLFLUFF_IMAGE
FROM ${SQLFLUFF_IMAGE}
COPY . /tmp/templater
RUN pip install --no-cache-dir /tmp/templater
