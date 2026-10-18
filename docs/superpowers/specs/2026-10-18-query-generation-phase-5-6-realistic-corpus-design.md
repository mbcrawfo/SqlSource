# Query generation, phase 5.6: the realistic corpus as models - design

Date: 2026-10-18

Sub-phase 5.6 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It makes the corpus of realistic queries under `tests/SqlSource.Tests.Postgres/Realistic/` the end-to-end project's realistic queries, as the epic's Testing section decides: each case the describer does not refuse becomes a `Models` query with a committed sidecar, and two checks of the generated models are added beside the describer's checks, against the same hand-written truth.

It builds on [sub-phase 5.4](2026-10-18-query-generation-phase-5-4-end-to-end-design.md) and can be built beside sub-phase 5.5.  Sub-phase 5.7 builds on it.

## Intent

The corpus is fifty-nine queries an application would write, over a schema with domains, an enum, arrays, a partitioned table, a view and `numeric` facets, each with a truth of which columns can be `NULL` written by hand from the SQL and the schema.  Today it checks the describer alone.  Turned into models, it checks what a user gets: that no generated property is less nullable than the truth says, and that every query reads its rows into its model through Dapper without an error.  It proves the type map on the corpus's domains, enums, arrays, ranges, `json`, `jsonb` and `numeric` facets, which the end-to-end schema of 5.4 does not have.

The epic's decisions on the corpus are kept whole: the truth, the oracle and the noise budget stay; no end-to-end test stands in for them, since a wrong `false` in a committed sidecar is what `--check` expects and only a truth the tool never wrote can catch it.

## Goals

1. Each case that is not refused is a `Models` query of the project, claimed by a type under `Realistic/Generation/`, in the logical database `realistic`, with a committed sidecar beside its `query.sql`.
2. A refused case stays the describer's alone, with no model and no sidecar.
3. `--check` over the corpus's sidecars on PostgreSQL 14 and 18.
4. Each property of each generated output model is as nullable as the case's truth says, or wider.
5. Each query that the run does not leave out runs through Dapper into its generated model over the seed, with each of the case's samples, and reads every row without an error.  The run leaves out a case on a server older than its `minimumMajor`, and a case whose `case.json` has a `dapper` member, below: a parameter Dapper cannot bind.
6. The rule for a case that needs a later major, and for one that an earlier planner describes otherwise.

## Non-goals

- Retiring `expected-pg14.txt` and `expected-pg18.txt`.  Sub-phase 5.7: deleting more than a hundred files would put this pull request over the limit of 150.
- Changing a case's query, truth or samples.  The cases are as they are.
- Lowering the noise budget.  A change to the describer's answers is not this sub-phase's.

## Anti-goals

- **A second set of queries.**  The epic decides that phase 5 writes no second set: each case stays one `query.sql` in one place, its folder.
- **A check of the models that reads the sidecar's `nullable` to decide.**  The nullability check reads the compiled model and the truth, never the sidecar, so that it can fail where `--check` passes.

## What exists today

As of the merge of 5.4:

- `Realistic/Cases/` has fifty-nine folders, each with `query.sql`, `case.json` and the expectations `expected-pg14.txt` and `expected-pg18.txt`, but `merge-returning`, which has `minimumMajor` 17 and `expected-pg18.txt` alone.  Each `query.sql` holds one query with a unique name, and none has a token.  Three are refused: `array-agg-of-row`, `SQLSRC229`, and `parameter-of-two-types` and `interval-without-cast`, `SQLSRC225`.  So fifty-six cases become models.
- `RealisticCasesTests` requires each folder to hold exactly `RealisticCase.Files`, and `RealisticCases` refuses a member of `case.json` it does not know.
- A `Path` names one file or one folder's own `.sql` files and never a folder inside it, and the attribute is not `AllowMultiple`, so no one type can claim every case's `query.sql`.
- The oracle runs a query with its samples written into the SQL by `RealisticSamples.Substitute`, so no check of the corpus binds a parameter today.
- `case.json` holds the truth, `columns` with `nullable` and a `why` or `exempt`, and `samples`, a SQL literal for each parameter.
- `Realistic/Describer/` holds the describer's checks: the descriptions against the expectations, the safety of the nullability against the truth and the oracle, and the noise budget, `{14: 53, 18: 54}`.
- The project runs the generator, and `tools/describe-end-to-end.sh` writes its sidecars against a container that has the database `realistic` with its schema and seed.

## Design

### The claim

A `Path` cannot name the files of a folder's subfolders, and a type has one attribute, so the corpus is claimed by one type per case: one file in `Realistic/Generation/` holds a small class for each case that is not refused, `[SqlSourceGenerate(Path = "../Cases/<case>/query.sql", Output = GeneratorOutput.Models)]`.  A recursive `Path` would be a feature a user sees, and is not this sub-phase's.  A refused case is claimed by no type, so it has no model, no sidecar and no build error, and the describer's tests read it as they do today.

The cases' files are given the database `realistic` by `AdditionalFiles` metadata in the project file, not by a marker in each `query.sql`: the epic recommended a `-- database:` marker per query, and metadata says the same in one line, leaves the cases unedited and keeps the describer's tests reading the text they read today.  The database has no attribute level, so the metadata is what the tool reads.  The epic outline's recommendation is updated to say so.

A test fails when the cases that some type claims are not exactly those whose `case.json` has no `refusal`, each claimed once, so that a case added without its class, a case refused later, or one no longer refused, is caught.

`RealisticCase.Files`, which `RealisticCasesTests` holds each folder to, gains `query.sql.json` for a case that is not refused.

### The sidecars

Written by `tools/describe-end-to-end.sh`, which describes the corpus as part of the project, against the default image.  A sidecar is beside its case's `query.sql`, `query.sql.json`.

### The run on PostgreSQL 14

One set of sidecars is checked on both majors, as the epic recommends.  On 2026-10-15 every case that runs on both was described the same on both.  The epic left to this phase how a case that needs a later major, or that an earlier planner describes otherwise, keeps its sidecar out of the run on the earlier major:

- **A case with `minimumMajor`** is left out of `--check` and of the Dapper run on a server older than its `minimumMajor`.  `merge-returning` is the one today.
- **A case that an earlier major describes otherwise** gets a new member of its `case.json`, `checkedFrom`, an object of `major`, the first major whose description its sidecar holds, and `why`, and is left out of `--check` on an earlier major in the same way.  Its expectation for the earlier major keeps proving the describer there until sub-phase 5.7 retires the expectations.  No case needs it today: every case's `expected-pg14.txt` is its `expected-pg18.txt` byte for byte.  `RealisticCases` reads it and validates it: a major of `PostgresContainer.RecordedMajors`, above the case's `minimumMajor` if it has one, and a `why` that is not empty.

The cases are left out by the positional `.sql` filters of `--check`, which restrict what it compares to the files they name.  A filtered run compares nothing it does not name, so on a server where a case is left out the check is two runs: `--check --database realistic` with every other case's `query.sql` named, and `--check` of the other databases with no filter.  On the default image it is one run with no filter, as sub-phase 5.4 has it.

The sidecar is never recorded on the earlier major, so that the build, which reads one sidecar, is the same on both.

### The nullability of the models

For each case, the output model's type is found from its sidecar entry as the generator names it: the table's model when `matchesTable` is set, the query's otherwise.  For each column of the truth, the property of the same position:

- A truth of `nullable: true` needs a nullable property: `Nullable<T>`, or a reference type annotated nullable, read by `NullabilityInfoContext`.
- A truth of `nullable: false` accepts either.

A property that is not nullable where the truth says the column can be `NULL` fails, naming the case, the column and the property.

### The run through Dapper

For each case and each of its samples, the query is run over the seed through Dapper into its generated output model, with its generated input model as the parameters, and every row is read.  A sample is a SQL literal, so it becomes a C# value the way the database reads it: `SELECT <literal>::<the parameter's type>` with each of the query's parameters, read by `GetValue`, gives the boxed value of each, and the input model is built from them by its constructor, or its properties for a class.  So the input model's types are proven too, since a value the driver boxes as another type than the property's fails to construct.

The corpus writes its parameters in snake_case, `@account_id`, which Dapper does not bind to the input model's `AccountId`: the owner kept the PascalCase rule, and the manual's recipe tells a user to write camelCase parameters or to build the parameters by hand ([D-0004](../../deferred/D-0004-input-models-bound-by-the-names-of-the-sql.md)).  The run does the second: it passes Dapper the input model's values under the names the sidecar's parameters have, in a `DynamicParameters`, so that the model's types are what is sent.  The end-to-end project of 5.4 writes its parameters in camelCase and passes the input model itself, so the two together prove both of the recipe's ways.

The run is in a transaction that is rolled back, as the oracle's is, since some cases write.  It asserts no value of the rows: what the rows hold is the oracle's business.  It asserts that every row is read, and that the query returns at least one row for at least one sample of the case, where the case's query returns rows, so that a run that reads nothing cannot pass; a case whose seed gives no row for any sample, if any, says so with the reason, as an `exempt` does.

Dapper's settings are the end-to-end project's, and the manual's recipe.

**A parameter Dapper cannot bind.**  This is the first check of the corpus that binds parameters; the oracle writes its samples into the SQL.  The base map gives an enum and `jsonb` the C# type `string`, and Dapper hands Npgsql a `string` with no type name, which Npgsql sends as `text`.  The server is expected to refuse `status = @status` against an enum column and `@filter` beside a `jsonb` operator, `operator does not exist`, for the cases that bind such a parameter: `any-ids-and-enum-array`, `delete-using-returning`, `two-columns-named-id`, `search-ilike-optional-filter-paged` and `jsonb-containment-and-key`.  The builder verifies it first.  Where it holds:

- The case is not changed, and the generator is not: the input model's type is the right C# type for the value, and phase 8's methods bind by the sidecar's `bindName`, which names the type.
- The case's `case.json` gains `dapper`, an object with a `why`, which leaves the case out of the Dapper run alone; the nullability check and the describer's checks still cover it.  `RealisticCases` validates it as it does `exempt`.  Phase 8 removes every `dapper` member, since its methods run every case.
- The manual's Dapper recipe says what a user does for such a parameter: a cast in the SQL, `@status::account_status`, or the enum mapped to a C# enum with `MapEnum<T>()` and the override.
- A case that fails the Dapper run for any other reason is a bug, of the map or of the generator, fixed in this sub-phase's pull request, or, if the fix is too large for it, recorded in `docs/tech-debt` with the owner's agreement, as `AGENTS.md` requires.

### `--check`

The corpus's sidecars are compared by the `--check` test of sub-phase 5.4, which already describes the whole project.  Nothing is added but the filters above.

## Testing

What this sub-phase adds is tests, listed above, in `Realistic/Generation/`.  The describer's tests in `Realistic/Describer/` are unchanged.  `RealisticCases` and `RealisticCasesTests` change for `checkedFrom`, `dapper`, the sidecar among a folder's files, and the claim's test.  CI runs them on 18.6 and on 14.24.  The Npgsql 8 project of 5.4 reads the type map's query alone, not the corpus.

## Documentation

- `tests/SqlSource.Tests.Postgres/README.md`: the corpus's part says that its cases are models with sidecars, what the two new checks prove, `checkedFrom`, and that a new case needs a run of `tools/describe-end-to-end.sh` beside `tools/record-realistic.sh`.
- `docs/superpowers/specs/2026-10-15-postgresql-realistic-query-suite-design.md` is not changed: a spec is not rewritten to match what was built.
- The epic outline: the realistic corpus's recommendations, settled as above, and the 5.6 row's status.

## Order of work

One pull request, one commit for each step:

1. The epic's 5.6 row says In progress.  The classes of the claim, the metadata and the claim's test.
2. The sidecars, through the script, and `checkedFrom` with the filters of `--check`.
3. The nullability check.
4. The Dapper run.
5. The documents, and the epic's row set to Done.

About seventy files: fifty-six sidecars and a dozen others.  The plan counts them.

## Left to the builder

- How the tests find a case's model type, within the rule above.
- What a case whose samples give no rows writes in its `case.json`.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- One class per case claims it, and a refused case is claimed by none.
- The database is given by metadata in the project file, not by a marker in each case.
- A case that Dapper cannot bind because it sends a `string` as `text` is left out of the Dapper run by a `dapper` member of its `case.json`, until phase 8.
- `checkedFrom`, an object of `major` and `why`, and filters on `--check`, for a case an earlier major describes otherwise.
- A sample becomes a value by asking the database to read its literal.
- The Dapper run asserts that rows are read, not their values.
- The Dapper run passes the input model's values under the SQL's names, since the corpus writes snake_case parameters.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
