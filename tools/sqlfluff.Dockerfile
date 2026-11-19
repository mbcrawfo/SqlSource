# The image of tools/sqlfluff.sh: sqlfluff, with the sqlsource templater installed from its source folder, which is the
# build's context.  tools/sqlfluff.sh passes the pinned sqlfluff image as SQLFLUFF_IMAGE.
ARG SQLFLUFF_IMAGE
FROM ${SQLFLUFF_IMAGE}
COPY . /tmp/templater
# --no-deps keeps the image's sqlfluff, and pip check fails the build when it is outside the templater's range, which
# pip would otherwise meet by installing another sqlfluff.
RUN pip install --no-cache-dir --no-deps /tmp/templater && pip check
