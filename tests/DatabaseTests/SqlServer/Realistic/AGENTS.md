# AGENTS.md - tests/DatabaseTests/SqlServer/Realistic

- **`tests/DatabaseTests/SqlServer/Realistic/` is SQL Server's realistic corpus** ([layout and procedures](../README.md#the-realistic-corpus)).  Its rules:
  - What both engines' corpora share is in `tests/SqlSource.Tool.Testing/Realistic/`, with its tests in `tests/SqlSource.Tool.Tests/Realistic/`; what reads a database through a driver stays in this project.  `Realistic/RealisticCases.cs` holds only the binding of the corpus, its models and its describer, and every test reads the corpus through it.
  - A `case.json` is never written from what the describer answers, and an `exempt` needs a reason, which `RealisticCaseReader` enforces, as for PostgreSQL's.
  - The checks of the generated models go in `Realistic/Generation/`, beside `Describer/`, never into the corpus's root.
  - Every folder of `Cases/` holds `query.sql`, `case.json` and, when the case has models, its sidecar `query.sql.json`, and no other file, which `RealisticCasesTests` checks.
  - That sidecar is written by `tools/describe-end-to-end.sh` alone, never by hand, and its diff is read before it is committed, since `RealisticDescriptionTests` compares its entry whole, provenance included, on each recorded major that `RealisticCase.IsCheckedOn` names.  A `checkedFrom` is for a difference of a parameter's or a column's type alone, never of nullability, and its `why` says which.
  - `Realistic/Generation/RealisticQueries.cs` holds one class for each case with models, claimed at `Models`, and `Realistic/Generation/RealisticMethods.cs` one for the same case, claimed a second time at `CodeGen`, which `RealisticClaimsTests` checks.
  - A case whose models the generator refuses says so by `models` in its `case.json`, whose id `Realistic/Generation/RealisticModelRefusalTests.cs` checks.
  - `Realistic/Generation/RealisticDapperTests.cs` passes Dapper the input model's values under the SQL's snake_case names in a `DynamicParameters`, never the model itself ([D-0004](../../../../docs/deferred/D-0004-input-models-bound-by-the-names-of-the-sql.md)), and it and `Realistic/Generation/RealisticMethodTests.cs` give every call the transaction they roll back.  A case with a parameter that Dapper cannot send says why by `dapper` in its `case.json`, which leaves it out of the Dapper run alone; `RealisticMethodTests` runs it.
  - A sample is bound through `sp_executesql` as a variable of the parameter's described type, by `RealisticSamples`, never put into the query's text.
  - Every batch of `Realistic/schema.sql` runs on SQL Server 2017; what a later major brings is made by a batch guarded by `SERVERPROPERTY('ProductMajorVersion')` that runs its text through `EXEC`.  `Realistic/seed.sql` only grows, since a truth or a sample may stand on any of its rows.
  - `.gitattributes` keeps every file of `Cases/` at LF.
  - `Realistic/Describer/RealisticNullabilityNoiseTests.cs` has one budget in `Budgets` for each of `EngineImage.SqlServer.RecordedMajors` and no other, changed as the readme says.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
