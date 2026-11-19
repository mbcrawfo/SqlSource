# TD-0120: The tests against a real database do not run on Windows

## Problem

The tool runs on Windows, where SqlClient connects through its native network layer and Npgsql through Windows' sockets and TLS, but no workflow runs the container test projects there: [`nightly.yml`](../../.github/workflows/nightly.yml) leaves `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61` and the allocation and sync-async projects out of its Windows job, and [`build.yml`](../../.github/workflows/build.yml) runs on Linux alone.  Untested on Windows are the describers against a real server, the errors of opening a session that only a server gives (the certificate case of `SQLSRC224` among them), and the generated methods through the drivers.  The packages' install and the tool's error for a refused port are run on Windows every night by the `packages` job, and nothing else of a connection is.

## Why it exists

GitHub's Windows runners start Windows containers alone, and the tests start the pinned Linux images of PostgreSQL and SQL Server.  A server installed on the runner instead would not be the pinned image that `AGENTS.md` requires and that the describers' fixtures and the sidecars are recorded on, and installing one makes a slow and fragile job.  The native layer's errors at open were found by running `tests/DatabaseTests/SqlServer` by hand on Windows with Docker Desktop.

## Impact

Low.  The describers' logic is the same on every system and runs on Linux in every pull request; what differs on Windows is the drivers' network layer, whose errors at open `ConnectionErrors` of the SQL Server describer maps by number.  A difference of a driver on Windows shows only when someone runs the container tests there.

## Proposed fix

Run the container projects on Windows by hand before a release, on a Windows machine with Docker Desktop and Linux containers: `dotnet test --solution SqlSource.slnx` in Git Bash.  Or a self-hosted Windows runner with Docker Desktop, added to the nightly run with the same pinned images, if the hand runs keep finding differences.

## Trigger

A user report of a connection error on Windows that differs from Linux's, or a new driver version whose network layer changes on Windows.
