# Building the PostGIS image before the tests

## Intent

The nightly run of 2026-11-12 failed on PostgreSQL 15 because one `apt-get` of the PostGIS image's build failed.
Testcontainers built the image inside the test run, so the failure showed only as ten fixture errors that say
"returned a non-zero code: 100", with apt's own message lost.  A failure to build the image should fail its job on a
step of its own, with Docker's whole output, and the tests should then not run.

Looking into it showed a second problem behind the first: the harness's `PostgisContainer` builds the image again on
every run, never reusing the one it kept.  Rebuilding `sqlsource-postgis:14.24-3.6.4` locally and then running
`PostgisParameterTests` twice gave a new image ID each time and spent 40 to 70 seconds in apt.  Testcontainers' default
build policy builds always, and its builds through Docker's API do not share the CLI's cache.  So every test run that
touches PostGIS goes back to `apt.postgresql.org`, and an image built before the tests would be built again inside them.
`tests/DatabaseTests/Postgres/README.md` says the opposite, that a later run reuses the image.

## Goals

- A step of each CI job that runs PostGIS tests builds the image, prints Docker's whole output, and fails the job when
  the build fails; the tests do not run after it.
- The tests use an image of the right name that already exists, and build it only when it is missing, so a test run
  that follows the step never builds, and a local run builds once per PostgreSQL image and Dockerfile.
- The image's name is computed in one script for CI and `tools/describe-end-to-end.sh`, and a test fails if the script
  and the harness disagree, since a disagreement would bring the hidden build back.

## Non-goals

- Retrying apt, or any other change to what the Dockerfile does.
- The example's own image, `examples/PostgresExamples/Postgis/Dockerfile`.
- The nightly jobs on Windows and macOS, which start no container.

## Approaches

- **A script, `tools/build-postgis-image.sh` (chosen).**  It builds the image under the harness's name and every CI job
  that needs it, and `describe-end-to-end.sh`, calls it.  The name lives in one script beside the harness, and CI cannot
  drift from the tests' name without a test failing.
- **The build folded into `tools/pull-test-images.sh`.**  No new file, but the floor and nightly jobs pull with a plain
  `docker pull`, not that script, and a build failure would read as a pull failure.
- **An inline `docker build` in each workflow.**  It repeats the name and the package in four places, breaks the rule
  that no workflow names the default image, and a drift from the harness's name silently brings the hidden build back.

## Decided

- **The harness reuses an image that exists.**  `PostgisContainer.PrepareImageAsync` builds with the policy
  `PullPolicy.Missing`, so it builds only when no image of the name exists.
- **The name carries a hash of the Dockerfile.**  `sqlsource-postgis:<postgres tag>-<PostGIS version>-<hash>`, the hash
  a short prefix of the SHA-256 of `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` with its line endings made LF,
  so a Windows checkout gives the same name.  An edit to the Dockerfile, a comment included, names a new image, so a
  stale one is never reused.  The postgres tag and PostGIS package are pinned, so the base cannot change under a name.
- **`tools/build-postgis-image.sh [postgres-image]`** builds the image of the postgres image given, or of
  `DefaultImages.Postgres` when none is, reading it as `pull-test-images.sh` does and the package from
  `PostgisContainer.Package` as `describe-end-to-end.sh` does today.  It runs `docker build --progress=plain`, so apt's
  output is in the log, and exits non-zero when the build fails.  It always builds, through Docker's cache, so a CI
  step is the one place the build happens and is seen.
- **The four jobs that run PostGIS tests get a "Build PostGIS image" step after their pull and before the tests**:
  `build` (default image) and `test-postgres-floor` (`$POSTGRES_FLOOR_IMAGE`) of `build.yml`, and `test-postgres`
  (`matrix.image`) and `sync-async-postgres` (default) of `nightly.yml`.  In `build`, the Test step's condition also
  requires this step's success, since today it runs whenever the build succeeded.
- **`describe-end-to-end.sh` calls the script** in place of its own `docker build`.
- **When the build fails the job fails** and no test runs.

## Left to the builder

- The hash's length, and how the script and the harness each compute it.
- The test that keeps the script's name equal to `PostgisContainer.ImageOf`, and where it lives.
- The documents this makes wrong, in the same pull request: the PostGIS image section of
  `tests/DatabaseTests/Postgres/README.md`, `tools/README.md`, `tools/AGENTS.md` (the rule on `describe-end-to-end.sh`
  building the image, and a rule for the new script), `.github/workflows/README.md`, the workflows' comments, and the
  comments of `PostgisContainer`.
