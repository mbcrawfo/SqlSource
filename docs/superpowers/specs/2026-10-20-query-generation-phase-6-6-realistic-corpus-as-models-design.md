# Query generation, phase 6.6: the realistic T-SQL corpus as models - design

Date: 2026-10-20

Sub-phase 6.6 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It makes each case of sub-phase 6.3's corpus that is not refused a `Models` query of the SQL Server end-to-end project, with a committed sidecar, and checks what PostgreSQL's 5.6 and 5.7 check together: each committed entry compared whole with the one described, on each major the case runs on; each output model as nullable as the case's truth or wider; and each query read through Dapper into its model over the seed.

It builds on sub-phase 6.5.  Sub-phase 6.7 builds on it.  It follows [sub-phase 5.6](2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md) and [5.7](2026-10-18-query-generation-phase-5-7-retire-expectations-and-close-design.md).

## Intent

6.3 checked the describer against the truth with no recorded expectation.  Here the description of each case is first recorded, as the sidecar a user would commit, so a change of the describer shows as a diff of a file and the model the generator builds from it is held to the truth.  PostgreSQL reached this in two steps, through expectation files it later retired; SQL Server takes one.

## Goals

1. Each case of the corpus that the describer does not refuse is claimed by a class of its own with output `Models`, under `Realistic/Generation/`, and has a committed `query.sql.json` written by `tools/describe-end-to-end.sh`, whose `DATABASES` for SQL Server gains `realistic`.
2. A case whose models the generator refuses, two columns of one name if the corpus has one, is not claimed and says why in a `models` member of its `case.json`, as `two-columns-named-id` does.
3. Each committed entry is compared whole with the one described, provenance and origins included, which `--check` ignores, on each of 14 and 17 that runs the case and that a `checkedFrom` does not leave out.  A case that 2017 describes otherwise, by a parameter's type as the spike found for `DATEADD` or by a column's type, gets `checkedFrom` with why, and is left out of the comparison on 2017 whole, as PostgreSQL's `checkedFrom` leaves a case out; the sidecar is always recorded on the default image.
4. `--check` covers `realistic` on both majors, with the positional filters leaving out what `minimumMajor` and `checkedFrom` leave out.
5. Each property of each generated output model is as nullable as the case's truth says or wider, read from the compiled model.
6. Each query runs through Dapper into its model over the seed with each of its samples, in a transaction that is rolled back and that every Dapper call is given as its `transaction`, since SqlClient refuses a command on a connection with a pending transaction it was not given, and reads every row without an error; a case Dapper cannot bind is left out of this run alone by a `dapper` member with why.

## Non-goals

- Lowering the noise, and execution through generated methods, phase 8, which runs every case, those with `dapper` too.

## Anti-goals

From 5.6, kept: a case changed to suit the generator; a sidecar written by hand.  And: **a `checkedFrom` that hides a difference of nullability**: it is for a difference of a type alone, and its `why` says which.  A case with one is still checked on 2017 by 6.3's safety, oracle and coverage checks, which run on every major and read nothing committed, so its nullability on 2017 is held to the truth all the same.

## What exists today

As of 6.5: the project builds with the generator and has its end-to-end sidecars; 6.3's cases have no sidecar and no claim; `case.json` has no `models`, `dapper` or `checkedFrom` member in use; the shared case rules know all three from PostgreSQL's.

## Design

As 5.6 and 5.7 have it, with SQL Server's majors and with these differences:

- The database is 6.3's `realistic`, which `SqlServerContainer` creates from `Realistic/schema.sql` and `Realistic/seed.sql`; `tools/describe-end-to-end.sh`'s SQL Server part creates it from the same files in the same order and gives the tool `--connection realistic=...`, as PostgreSQL's part does for its corpus.
- The corpus's `AdditionalFiles` metadata sets `SqlSourceDatabase` `realistic` and, if the project overrides a type at its own level, returns that key to the base map, as PostgreSQL's corpus returns `timestamptz`.
- The Dapper run binds the samples by the SQL's parameter names, as PostgreSQL's does, `D-0004` being the reason an input model cannot be passed; a sample's literal is turned into a value by asking the server for it.
- The comparison whole sets `NameSpan`, `ServerVersion` and `Database` equal on both sides, as `DescribeCommandTests` does.

## Testing

`RealisticClaimsTests`, `RealisticDescriptionTests` with the whole comparison, `RealisticModelNullabilityTests`, `RealisticDapperTests` and `RealisticModelRefusalTests`, by PostgreSQL's pattern; `CheckTests` with `realistic`.

## Documentation

`tests/SqlSource.Tests.SqlServer/README.md`: a case's sidecar, `checkedFrom`, `dapper` and `models`; `tools/AGENTS.md` for `DATABASES`; the epic outline's row of 6.6.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The claims and the metadata.
2. The sidecars written by the script, and `--check` with `realistic`.
3. The whole comparison and any `checkedFrom`.
4. The models' nullability.
5. The Dapper run and any `dapper`.
6. The documents, and the row set to Done.

About sixty sidecars and a handful of code files; well within the limit of 150.

## Left to the builder

- Which cases need `checkedFrom`, `dapper` or `models`, each with why.
- How a sample's literal becomes a value for Dapper.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- 5.6's and 5.7's work is one sub-phase here, since no expectation file exists to retire.
- `checkedFrom` is allowed for a difference of a parameter's or a column's type, never of nullability, and leaves the case out of the whole comparison on the earlier major, as on PostgreSQL; 6.3's checks still cover its nullability there.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
