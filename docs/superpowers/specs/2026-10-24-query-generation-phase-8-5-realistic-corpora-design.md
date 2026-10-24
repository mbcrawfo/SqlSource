# Query generation, phase 8.5: the realistic corpora through the methods - design

Date: 2026-10-24

Sub-phase 8.5 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It runs each case of both realistic corpora that has models through its generated method, over the seed, with each of its samples, on both majors of both engines, beside the Dapper run of sub-phases [5.6](2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md) and [6.6](2026-10-20-query-generation-phase-6-6-realistic-corpus-as-models-design.md), which stays.

It builds on 8.3 and 8.4.  Sub-phase 8.6 builds on it.

## Intent

The end-to-end projects prove each type and each setting on queries written to cover them; the corpora are what users write.  Running them through the methods proves the methods on about a hundred real queries across both engines, the four PostgreSQL cases and the one SQL Server case that Dapper cannot run included.  And it gives the corpora a second oracle: a method reads a column the model calls not null with no `IsDBNull`, so a `NULL` in such a column throws, where Dapper would leave the member at its default.

## Goals

1. Each case with models claimed a second time, by a `CodeGen` class, beside its `Models` class: 55 PostgreSQL cases and 53 SQL Server cases as of 8.4.
2. Each such case run through its method over the seed with each of its samples, in a transaction rolled back, reading every row without an error, the shape and collection the method returns read whole; a case whose samples all read no row fails, as the Dapper run's does.
3. The cases with `dapper` run through their methods too.
4. The Dapper run unchanged, over the `Models` claims.
5. The run on each major the case runs on: PostgreSQL 14 and 18, SQL Server 2017 and 2025, by `minimumMajor` as the other runs skip.

## Non-goals

- A change to any case's SQL, truth, samples or committed sidecar.  The second claim needs no new entry: the sidecar is per file.
- A new case.
- Removing the `dapper` members.  The outline settled that they stay, against 5.6's spec, which planned to remove them.
- A new count of the noise budget.  The methods do not change what the describer says.

## Anti-goals

- **A case that passes because no sample read a row.**
- **A case left out of the method run.**  Every case with models runs, or the test says which and why, and a case is left out only for a reason the outline records.
- **A database left changed.**

## What exists today

As of the merge of 8.3 and 8.4, which this spec assumes:

- `Realistic/Generation/RealisticQueries.cs` in each engine's project has one `internal static partial class` per case with models, `Output = GeneratorOutput.Models`, `SqlLocation.Direct`.
- `RealisticDapperTests` runs a theory over the cases, skipped by `RealisticModels.SkipUnlessReadThroughDapper` for a case with no models, a later major, or `dapper`; each sample is converted from its SQL literal through the server and built into the input model by `RealisticParameters.InputModelAsync`, and sent to Dapper under the SQL's names by `RealisticInputs.ArgumentsOf`; each run is in a transaction rolled back with `CancellationToken.None`.
- `RealisticClaimsTests` checks each class's `Path`, not its `Output`; `RealisticClaims` reads only the classes of `RealisticQueries.cs`, and its `Of()` expects one claim per case.
- Models are emitted once per full name, so two classes in one namespace that claim one file share its models.

## Design

### The second claim

`Realistic/Generation/RealisticMethods.cs`, or a name the builder chooses, holds one class per case with models, claiming the case's `query.sql` with `Output = GeneratorOutput.CodeGen`, in the namespace of `RealisticQueries`, so that the two claims share the case's models and the method takes the same input model the Dapper run builds.  Its `MethodLocation` is the default, `ExtensionClass`, so each case's method is an extension method of its class's extensions class.  `RealisticClaims` learns the second file and two claims per case, and `RealisticClaimsTests` checks that each case with models has both, of the two outputs.

### The run

`RealisticMethodTests`, beside `RealisticDapperTests`, runs a theory over the same cases with a skip of its own that leaves out a case with no models or a later major, and not a case with `dapper`.  For each sample: the input model as the Dapper run builds it, by `RealisticParameters.InputModelAsync`; the method, found by reflection once per case on its extensions class by the query's name, called with the model, the transaction and the test's cancellation token; the result read whole, its count of rows taken for the "at least one sample reads a row" rule by its shape.  The reflection is the test's, to reach a hundred methods from one theory; no generated code uses it.

### The oracle

A method that throws because a column the model calls not null holds a `NULL` fails the case, and the failure names the column.  The corpus's own oracle checks the describer's answer against the seed; this one checks the whole chain, from the committed sidecar through the model to the generated read, so that a not-null property the generator gave a column the sidecar left nullable, or a `!` suffix the seed contradicts, is found too.  8.0's spike says the throw holds for every kind of type on both drivers; for a kind it does not, the case's truth check stays the oracle, and the test says so.

## Testing

The new tests on both engines and both majors, in the default run and in CI's floor runs, which run the whole project on PostgreSQL 14 and SQL Server 2017.

## Documentation

- `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `tests/AGENTS.md`: the two claims per case, the method run beside the Dapper run.
- The `why` of each `dapper` member, which says the methods do not run the case, rewritten to say only that Dapper does not.
- The epic outline: the row of 8.5.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The second claims, and the claims test.
2. The method run on PostgreSQL.
3. The method run on SQL Server.
4. The documents, and the row set to Done.

## Left to the builder

- The classes' file and names, and the test's name.
- How the result of each shape is counted.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The second claim is a class per case in the same namespace, so that the models are shared and no case's file changes.
- The method run finds each method by reflection, in the test alone.
- A case whose not-null column the seed holds a `NULL` in fails the method run, though its truth check passes, since a method that throws is what a user would see.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
