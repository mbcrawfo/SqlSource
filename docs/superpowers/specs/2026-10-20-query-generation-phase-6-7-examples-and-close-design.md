# Query generation, phase 6.7: the SQL Server examples and the close - design

Date: 2026-10-20

Sub-phase 6.7 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It adds `examples/SqlServerExamples`, by the pattern of `examples/PostgresExamples` and the rules of `examples/AGENTS.md`, reads the user documentation of SQL Server whole, and closes phase 6.

It builds on sub-phases 6.5 and 6.6.  It follows [sub-phase 5.5](2026-10-18-query-generation-phase-5-5-examples-design.md).

## Intent

A user who reads the manual's SQL Server page should be able to open one small project and see what they write and what they get, built with the solution so it never goes stale.  And the phase ends with the documents saying, everywhere, that SQL Server is described and modelled, with nothing left that says PostgreSQL alone.

## Goals

1. `examples/SqlServerExamples`, in `SqlSource.slnx` under `/examples/`: a csproj importing the props and targets by path, `SqlSourceDialect` `mssql`, Dapper and Microsoft.Data.SqlClient; a `docker-compose.yml` whose image is `SqlServerContainer.DefaultImage`, with a service that applies `schema.sql` through the image's `sqlcmd` with `-I` once the server answers; `schema.sql` naming no owner, role or database; `Sql/`, `Models/` and `CodeGen/` folders, each with one class and its queries, using what was generated through Dapper as the manual's recipe says; a README of a user's workflow, the Compose file and the tool, with the Dapper and SqlClient versions it uses.
2. `tools/describe-end-to-end.sh` writes the example's sidecars, with `EXAMPLES` and its connection, against a database `examples` under the default logical name `mssql`.
3. `examples/AGENTS.md`'s rules that name PostgreSQL alone, the image rule and the script rule, say each engine's; root `AGENTS.md` rule 5 names the Compose file's image.
4. The user documentation of SQL Server read whole, in the pages the phase map of `docs/manual/AGENTS.md` names for phase 6, the phase 6 finder run and every hit that is PostgreSQL alone where it should be both fixed; the manual's "What works today" and `settings.md`'s Status cells for SQL Server; the README's status line, supported environments, step 5 and "Use a database other than PostgreSQL" row.
5. The epic outline: phase 6 Done, its Settled section final, and the phase map's row of 6 in `docs/manual/AGENTS.md` replaced by what phase 7 will need, as each closed phase has done.

## Non-goals

Anything new in the describer, the map or the tests.  A finding of the reading is fixed here if it is a document's; if it is the code's, it is recorded in `docs/tech-debt` or, if small, fixed with the owner's agreement.

## Anti-goals

From 5.5, kept: an example with tests; an example that shows what the manual does not recommend; a sidecar written by hand.

## Design

As 5.5 for PostgreSQL, with SQL Server's Compose service: the SQL Server image has no init folder, so a second service on the same image runs `sqlcmd -I -b -i /schema.sql` against the first once it answers and exits, and the README says to wait for it.  The connection in the README holds `TrustServerCertificate=True` and says why.  Nothing checks that the example's sidecars are current with its schema, as for PostgreSQL's.

## Documentation

Goals 3 to 5.  `CONTRIBUTING.md` for the second example project.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The example project, its Compose file, schema and README.
2. The classes and queries, with sidecars written by the script.
3. `examples/AGENTS.md` and root `AGENTS.md`.
4. The reading of the user documentation, one commit for the fixes it finds.
5. The outline's Settled, the phase map, and phase 6 set to Done.

## Left to the builder

The example's queries and schema, small and representative; the Compose service's wait.

## Calls made in this spec

- A second Compose service applies the schema, since the image has no init folder.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
