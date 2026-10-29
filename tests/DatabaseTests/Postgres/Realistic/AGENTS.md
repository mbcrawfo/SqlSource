# AGENTS.md - tests/DatabaseTests/Postgres/Realistic

- **`tests/DatabaseTests/Postgres/Realistic/` is the realistic corpus** ([layout and procedures](../README.md#the-realistic-corpus)).  Its rules:
  - What both engines' corpora share is in `tests/SqlSource.Tool.Testing/Realistic/`, with its tests in `tests/SqlSource.Tool.Tests/Realistic/`; what reads a database through a driver stays in this project.  `Realistic/RealisticCases.cs` holds only the binding of the corpus, its models and its describer, and every test reads the corpus through it.
  - The bodies of `RealisticCasesTests`, `RealisticClaimsTests`, `RealisticDescriptionTests`, `RealisticNullabilitySafetyTests` and `RealisticModelRefusalTests` are the `Realistic*Checks` of `tests/SqlSource.Tool.Testing/Realistic/`, each test one line that calls its check with the corpus, `RealisticCases.Describe`, the fixture's major and connection string, and the engine's oracle or generator references; a check that differs by engine takes the difference as an argument, never a copy.
  - The checks of a later layer over the same cases, the generated models', go in a folder of their own beside `Describer/`, never into the corpus's root.
  - A `case.json` is never written from what the describer answers, and an `exempt` needs a reason, which `RealisticCaseReader` of `tests/SqlSource.Tool.Testing/Realistic/` enforces ([why a column says so](../README.md#the-realistic-corpus)).
  - Every folder of `Cases/` holds `query.sql`, `case.json` and, when the case has models, its sidecar `query.sql.json`, and no other file, which `RealisticCasesTests` checks.
  - That sidecar is written by `tools/describe-end-to-end.sh` alone, never by hand, and its diff is read before it is committed, since `RealisticDescriptionTests` compares its entry whole, provenance included, on each recorded major that `RealisticCase.IsCheckedOn` names.
  - `Realistic/Generation/RealisticQueries.cs` holds one class for each case with models, claimed at `Models`, and `Realistic/Generation/RealisticMethods.cs` one for the same case, claimed a second time at `CodeGen`, which `RealisticClaimsTests` checks.
  - A case whose models the generator refuses says so by `models` in its `case.json`, whose id `Realistic/Generation/RealisticModelRefusalTests.cs` checks.
  - `Realistic/Generation/RealisticDapperTests.cs` passes Dapper the input model's values under the SQL's snake_case names in a `DynamicParameters`, never the model itself ([D-0004](../../../../docs/deferred/D-0004-input-models-bound-by-the-names-of-the-sql.md)), and it and `Realistic/Generation/RealisticMethodTests.cs` give every call the transaction they roll back.
  - A case with a parameter that Dapper cannot send says why by `dapper` in its `case.json`, which leaves it out of the Dapper run alone; `RealisticMethodTests` runs it.
  - `.gitattributes` keeps every file of `Cases/` at LF, as the sidecars were described.
  - `Realistic/Describer/RealisticNullabilityNoiseTests.cs` has one budget in `Budgets` for each of `EngineImage.Postgres.RecordedMajors` and no other, changed as the readme says.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
