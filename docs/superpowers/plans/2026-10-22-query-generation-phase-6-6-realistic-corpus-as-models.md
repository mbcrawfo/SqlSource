# Query generation phase 6.6: the realistic T-SQL corpus as models - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each case of SQL Server's realistic corpus that the describer does not refuse becomes a `Models` query of `tests/SqlSource.Tests.SqlServer` with a committed sidecar, compared whole on each recorded major, its output model held to the truth's nullability, and read through Dapper over the seed.

**Architecture:** A port of PostgreSQL's `Realistic/Generation/` (phases 5.6 and 5.7) to `tests/SqlSource.Tests.SqlServer/Realistic/Generation/`, with SQL Server's majors (14 and 17), its describer, SqlClient and `sp_executesql`-free sample values read by `SELECT CAST(...)`.  The shared case code in `tests/SqlSource.Tool.Testing/Realistic/` already knows `models`, `checkedFrom` and `dapper`; nothing there changes.

**Tech Stack:** C# / .NET 10, xunit v3, Shouldly, Dapper, Microsoft.Data.SqlClient 7.1.1, Testcontainers.MsSql, bash.

**Spec:** `docs/superpowers/specs/2026-10-20-query-generation-phase-6-6-realistic-corpus-as-models-design.md`, following `2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md` and `2026-10-18-query-generation-phase-5-7-retire-expectations-and-close-design.md`.

## Global Constraints

- Every commit and push passes `./pre-commit-validation.sh` on the tree as committed (root `AGENTS.md`, Commits).  A change under `tests/SqlSource.Tests.SqlServer` is also run on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` before it is pushed.
- Sidecars are written by `tools/describe-end-to-end.sh` alone, never by hand, and their diff is read before committing.  A `case.json` is never changed to suit the generator or written from the describer's answer (anti-goal of 5.6, kept).
- `checkedFrom` is for a difference of a parameter's or a column's type alone, never of nullability; its `why` says which.  Its `major` is one of `SqlServerContainer.RecordedMajors` (`[14, 17]`), so 17.
- Every Dapper call in a transaction is given it as `transaction`.  Dapper gets the input model's values under the SQL's names in a `DynamicParameters`, never the model (D-0004).
- The corpus's `AdditionalFiles` set `SqlSourceDatabase="realistic"` and `SqlSourceTypeOverrides="date=default"` (the project overrides `date=System.DateOnly`).
- One PR, at most 150 files: about 53 sidecars, about 12 code files, a handful of `case.json` and documents.
- Test names `Method_Scenario_Outcome`, every test class `sealed`, Shouldly assertions, types `internal`.

## Review Focus

1. A case whose output model is a table's model (`matchesTable`) shared by two cases, or named like a model of another case: the model lookup must find the generator's one type, and `RealisticModelsTests` pins one table-model case and one query-model case.
2. A sample that ends in a `--` comment or is an expression (`DATEADD(...)`): the select that turns it into a value puts it on lines of its own, as `RealisticSamples.Batch` does; `RealisticParametersTests.SelectOf_SampleEndingInAComment_KeepsItsParenthesis` pins it.
3. A `NULL` sample for a value-type parameter: `ModelOf` throws naming the parameter instead of sending 0 (ported test).
4. A run of `CheckTests` on 2017, where `minimumMajor` and `checkedFrom` cases are left out by naming files: the counts it expects must be the counts of the named sidecars, so a case wrongly left out or in fails.
5. A case that writes (`INSERT`/`UPDATE`/`DELETE`/`MERGE ... OUTPUT`): the Dapper run rolls back with `CancellationToken.None`, so the seed stays as every other test reads it.

---

### Task 1: The claims and the metadata

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.6: In progress)
- Modify: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj` (an `AdditionalFiles Update="Realistic/Cases/**/*.sql"` item with the two metadata, commented as PostgreSQL's is)
- Modify: `tests/SqlSource.Tests.SqlServer/RepositoryPaths.cs` (`RealisticQueries` = `Realistic/Generation/RealisticQueries.cs`)
- Create: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticQueries.cs` (header comment as PostgreSQL's; one `[SqlSourceGenerate(Path = "../Cases/<case>/query.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)] internal static partial class <PascalCase(case)>Query;` per case with models, in folder order)
- Create: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticClaims.cs`, `RealisticClaimsTests.cs` (PostgreSQL's, namespace `SqlSource.Tests.SqlServer.Realistic.Generation`)
- Modify: `tests/SqlSource.Tests.SqlServer/Realistic/RealisticCasesTests.cs` (`Folder_Case_HoldsItsFilesAlone` against `found.Files`)

**Interfaces:**
- Produces: `RealisticClaim(string TypeName, string Case)`, `RealisticClaims.All`, `RealisticClaims.Parse(string)`, `RealisticClaims.Of(RealisticCase)`, `RealisticClaims.ClaimedBy(RealisticCase, int)`; `RepositoryPaths.RealisticQueries`.

The 53 cases without `refusal` are claimed; the three refused (`report-dynamic-exec`, `staging-temp-table`, `report-label-parameter`) are not.  The project does not build until Task 2 writes the sidecars (SQLSRC2xx for a claimed file without one), so Tasks 1 and 2 are one commit.

- [ ] Write `RealisticClaimsTests` (the five `Parse` tests and the two `All` tests of PostgreSQL's) and the file-list test change.
- [ ] Write `RealisticClaims`, `RealisticQueries.cs`, the metadata and the path.
- [ ] Go on to Task 2 before building.

### Task 2: The sidecars and `--check` with `realistic`

**Files:**
- Modify: `tools/describe-end-to-end.sh`: `SQLSERVER_REALISTIC="$SQLSERVER_PROJECT/Realistic"`, `SQLSERVER_DATABASES=(realistic app billing)`, the loop reads `realistic`'s `schema.sql` and `seed.sql` from `$SQLSERVER_REALISTIC` (as PostgreSQL's loop does), `find` also deletes `$SQLSERVER_REALISTIC/Cases/**/*.sql.json`; header comment names the realistic cases.
- Create: 53 × `tests/SqlSource.Tests.SqlServer/Realistic/Cases/<case>/query.sql.json` (written by the script)
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/CheckTests.cs`
- Modify: `tools/AGENTS.md` (the SQL Server rule names `Realistic/` first and `SQLSERVER_DATABASES`), `tools/README.md` if it lists the script's databases.

- [ ] Run `tools/describe-end-to-end.sh` (background; it runs both engines).  Expected: PostgreSQL's sidecars unchanged (`git status` shows none of them), 53 new SQL Server sidecars.  A case the generator then refuses (a build error naming its file) gets `models` with the id and why in its `case.json` and loses its claim; none is expected, since no case has two columns of one name.
- [ ] Read the diff of the sidecars: engine `mssql`, database `realistic`, server `17.0.5015.4`.
- [ ] `CheckTests.Check_Project_EveryEntryIsTheSame`: count entries over `EndToEnd/` and `Realistic/Cases/` per database (as PostgreSQL's `ProjectEntries`).  On a major where every realistic case with models `IsCheckedOn` and the major is 17 or later: one run with the three connections, output `app`, `billing`, `realistic` lines in that order (the order the tool prints; read it from a run).  Otherwise (14): one run naming every `EndToEnd/Queries/*.sql` but `NewTypes.sql`, plus `Path.Combine(found.Folder, RealisticCase.QueryFile)` of each case that `IsCheckedOn(major)`; expected counts are those of the named sidecars; `checkedRealistic.ShouldBeLessThan(realistic)`.
- [ ] Build and run `CheckTests`, `RealisticClaimsTests`, `RealisticCasesTests` on 2025.  Expected: PASS.
- [ ] `./pre-commit-validation.sh`, commit "Claim the realistic T-SQL cases as Models queries with their sidecars".

### Task 3: The whole comparison and any `checkedFrom`

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticDescribe.cs` (add `EntryOf(RealisticCase, ServerInfo, QueryDescription)` → `EntryBuilder.Build(planned, dialect, SqlServerContainer.RealisticDatabase, server, description)`)
- Modify: `tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticDescriptionTests.cs` (add `DescribeAsync_Case_IsItsCommittedEntry`, PostgreSQL's, with `sqlServer.SkipUnlessRecordedMajor`, "SQL Server" in the skip text)
- Create (needed here): `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticModels.cs` with `EntryOf` only for now (the rest in Task 4).
- Modify: `case.json` of each case 2017 describes otherwise by a type: `"checkedFrom": { "major": 17, "why": "<which parameter or column, and the two types>" }`.

**Interfaces:**
- Produces: `RealisticDescribe.EntryOf(RealisticCase found, ServerInfo server, QueryDescription description) : SidecarEntry`; `RealisticModels.EntryOf(RealisticCase found) : SidecarEntry`.

- [ ] Write the test; run it on 2025.  Expected: PASS for every case with models (the sidecars were just written).
- [ ] Run it on 2017.  Every failure is read: a difference of a type alone gets `checkedFrom` with why; a difference of nullability is not hidden, and is brought to the owner as a describer finding.
- [ ] Run on 2017 again, and `CheckTests` on 2017.  Expected: PASS.
- [ ] Validate, commit "Compare each realistic T-SQL case's entry whole on each major".

### Task 4: The models' nullability

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticModels.cs` (PostgreSQL's whole, `PropertyNames.FromTable(table, DatabaseEngine.SqlServer)`)
- Create: `RealisticModelsTests.cs` (PostgreSQL's, with a table-model case and a query-model case of this corpus, and a case with and one without parameters, chosen from the sidecars)
- Create: `RealisticModelNullabilityTests.cs` (PostgreSQL's)
- Create: `RealisticModelRefusalTests.cs` (PostgreSQL's, `CorpusMetadata` `realistic` / `date=default`, reference `typeof(SqlConnection).Assembly`, dialect `mssql`, `Sessions.OpenAsync(sqlServer.RealisticConnectionString, database: SqlServerContainer.RealisticDatabase)`)

**Interfaces:**
- Produces: `RealisticModels.InputSuffix = "Args"`, `OutputSuffix`, `SkipUnlessHasModels`, `OutputModelOf(SidecarEntry) : Type`, `InputModelOf(SidecarEntry) : Type?`, `PropertiesOf(Type)`, `IsNullable(PropertyInfo)`.

- [ ] Write the tests, run.  Expected: PASS; a nullability failure is a generator or type-map bug, brought to the owner, never fixed in `case.json`.
- [ ] Validate, commit "Hold each realistic T-SQL case's model to its truth".

### Task 5: The Dapper run and any `dapper`

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticParameters.cs`, `RealisticParametersTests.cs`, `RealisticDapperTests.cs`
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/EndToEndSources.cs` (`Realistic(SqlServerContainer)` → `SqlConnection` of `RealisticConnectionString`)
- Modify: `case.json` of each case Dapper cannot bind: `"dapper": "<why>"`.

**Interfaces:**
- Produces: `RealisticParameters.SelectOf(IReadOnlyDictionary<string,string> sample, IReadOnlyList<SidecarParameter> parameters) : string`, `ValueOf(object) : object?`, `InputModelAsync(SqlConnection, SqlTransaction?, Type, IReadOnlyList<SidecarParameter>, IReadOnlyDictionary<string,string>) : Task<object>`, `ModelOf(Type, IReadOnlyList<SidecarParameter>, IReadOnlyList<object?>) : object`, `ArgumentsOf(object, IReadOnlyList<SidecarParameter>) : DynamicParameters`.

`SelectOf` is `SELECT CAST((\n<literal>\n) AS <type>), ...` with `<type>` the sidecar parameter's `Type.Name` (as the server writes it, `nvarchar(100)`); the sample is found by `RealisticSampleChecks.Find` (ignoring case).  The values are read with `GetValue` on the realistic database.

- [ ] Port PostgreSQL's parameter tests with SQL Server's expected text (`SELECT CAST((\n10\n) AS bigint), CAST((\nN'a'\n) AS nvarchar(10))`) and add `SelectOf_SampleEndingInAComment_KeepsItsParenthesis`.
- [ ] Write `RealisticParameters`, `RealisticDapperTests` (PostgreSQL's, with the transaction given to every call and the select of the sample run on the same connection and transaction).
- [ ] Run on 2025 and 2017.  A case that fails because Dapper cannot send a parameter gets `dapper` with why.
- [ ] Validate, commit "Read each realistic T-SQL case through Dapper into its model".

### Task 6: The documents

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/README.md` (the realistic corpus: sidecar, claims, `Generation/` checks, `models`, `checkedFrom`, `dapper`; writing the sidecars names `realistic`)
- Modify: `tests/AGENTS.md` (SQL Server corpus rules as PostgreSQL's: `Cases/` holds the sidecar, claims, `models`, `dapper`, D-0004; the no-server tests list gains the `Generation/` tests that need none)
- Modify: root `AGENTS.md` rule 5 (SQL Server's recorded sidecars include the realistic cases')
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 6.6 Done)

- [ ] Write them; check every name they cite exists.
- [ ] Validate, commit "Document the realistic T-SQL corpus as models and close 6.6".

## Decisions made here

- 5.6's and 5.7's work is one unit (the spec's call; no expectation file exists).
- `checkedFrom` for types alone, as the spec recommends.
- Sample values come from `SELECT CAST(...)` on the server, not `sp_executesql`: only the value is wanted, and the cast at the described type gives what SqlClient reads for that type.
