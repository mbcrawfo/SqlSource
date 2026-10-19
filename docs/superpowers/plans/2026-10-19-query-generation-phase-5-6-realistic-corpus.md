# Query generation, phase 5.6: the realistic corpus as models - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each realistic case that the describer and the generator accept becomes a `Models` query of `tests/SqlSource.Tests.Postgres` with a committed sidecar, and two checks of the generated models are added beside the describer's: each output property is as nullable as the truth or wider, and each eligible query reads its rows through Dapper over the seed.

**Architecture:** One file, `Realistic/Generation/RealisticQueries.cs`, holds one small class per case, each claiming `../Cases/<case>/query.sql`.  `AdditionalFiles` metadata gives the cases the database `realistic`.  `tools/describe-end-to-end.sh` writes the cases' sidecars with the rest of the project.  The tests in `Realistic/Generation/` read the claim file with Roslyn, find each case's models by the generator's own naming rules, and use reflection, `NullabilityInfoContext` and Dapper.

**Tech Stack:** C# on .NET 10, xunit v3, Shouldly, Dapper 2.1, Npgsql 10, Testcontainers, Roslyn (`Microsoft.CodeAnalysis.CSharp`, already referenced by the project).

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md`](../specs/2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md).  Read it, the root `AGENTS.md`, `tests/AGENTS.md` and `tests/SqlSource.Tests.Postgres/README.md` before starting a task.

## Where this plan follows the merged code, not the spec

The spec was written before 5.3 and 5.4 merged.  A spike on 2026-10-19 (described the corpus, built it, ran every case through Dapper) settled these, two of them with the owner:

1. **`two-columns-named-id` gets no model.**  It selects `a.id, o.id`, and the generator refuses it as `SQLSRC255`, a build error, which 5.3 made deliberate.  The cases are not edited.  The owner chose to treat it as a refusal by the generator: its `case.json` gains a third new member, `models`, `{ "refusal": "SQLSRC255", "why": "..." }`; no class claims it, it has no sidecar, and the describer's checks still cover it.  So **55** cases become models, not 56.
2. **The corpus gets the base type map.**  The project sets `timestamptz=System.DateTimeOffset`.  The owner chose to take that out for the corpus with `SqlSourceTypeOverrides="timestamptz=default"` on the same metadata item that sets the database, so that each input model is built from the driver's `GetValue` boxes, as the spec says, and the corpus proves the base map.
3. **`array-and-string-agg` has a class output model.**  5.4 found that Dapper cannot construct a positional record with an array column ([TD-0062](../../tech-debt/TD-0062-record-models-with-arrays-and-dapper.md)); the spike reproduced it on this case.  Its class sets `OutputModelType = GeneratorModelType.Class`, by the owner's rule, and TD-0062 names it.  It is the only case with an array column.
4. **The `-- column:` marker of 5.3 changes nothing here.**  No case writes a marker or a `!`/`?` mark, so every column is read by its name.  The nullability check reads the compiled model, so a marker added later that narrows a column below its truth fails it.
5. **The `dapper` cases are four, not five.**  The spike confirmed `operator does not exist` for `any-ids-and-enum-array`, `delete-using-returning`, `search-ilike-optional-filter-paged` (enum `account_status = text`) and `jsonb-containment-and-key` (`jsonb @> text`).  The fifth that the spec lists, `two-columns-named-id`, has no model (point 1).  Every other case read its rows, at least one row for some sample, so no case needs a member that says its seed gives no row.
6. **The manual's recipe already gives the remedy.**  5.4's recipe lists "a `string` for `json`, `jsonb`, ..., an enum" among the parameters Dapper cannot send, and says to cast in the SQL or send a `TypedParameter`.  The spec's other remedy, `MapEnum<T>()` with the override, does not work for a parameter: 5.4 found that Dapper sends a C# enum as its number.  So `docs/manual/recipes.md` needs no change.
7. **The claim and the sidecars land in one commit.**  The spec's order puts the classes in step 1 and the sidecars in step 2, but a claimed `Models` query with no sidecar is `SQLSRC241`, and every commit must pass `pre-commit-validation.sh`.  So task 1 claims, writes the sidecars and gives `CheckTests` the `realistic` connection; task 2 is `checkedFrom` and the filters of `--check` on an earlier major.
8. **The input model is a record.**  The project sets `SqlSourceInputModelType` to `record`, so the run builds each input model by its constructor; it has no path for a class input model, which no case has.

## Global Constraints

- The truth (`columns`), the samples, every `query.sql`, the oracle, the noise budget `{14: 53, 18: 54}` and the `expected-pg*.txt` files stay unchanged.  Retiring the expectations is 5.7's.
- New `case.json` members: `checkedFrom` (`{ "major", "why" }`), `dapper` (`{ "why" }`) and `models` (`{ "refusal", "why" }`).  `RealisticCases` keeps refusing any member it does not know.
- The Dapper run passes the input model's values under the sidecar's snake_case parameter names in a `DynamicParameters` ([D-0004](../../deferred/D-0004-input-models-bound-by-the-names-of-the-sql.md)).
- The nullability check never reads the sidecar's `nullable`.
- A sidecar is written only by `tools/describe-end-to-end.sh`, on the default image, never by hand.
- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  Before a push, also `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres`.
- Test names are `Method_Scenario_Outcome`, assertions are Shouldly's, every test class is `sealed`, a test that needs the server is in `[Collection(UsesPostgres.Name)]`.
- `tools/describe-end-to-end.sh` is also changed by sub-phase 5.5 in parallel: keep its diff to a few lines.
- About ninety files in the pull request: 55 sidecars, 5 `case.json` files and about thirty others.

## Review Focus

1. **A case added later without its class, or a case that stops being refused.**  The claim test fails naming the case; covered in task 1.
2. **A sample of `NULL` for a parameter that can be null** (`upsert-product-returning`, `search-ilike-optional-filter-paged`): the value is `null`, never `DBNull`, in the input model and in `DynamicParameters`.  Task 4 pins it with a unit test of `RealisticParameters.ValueOf`.
3. **A query that reads no row for any sample** passes a run that asserts nothing.  Task 4 asserts at least one row over the case's samples.
4. **Property order.**  `Type.GetProperties()` promises no order; the nullability check pairs the truth's columns with properties by position, so it orders them by `MetadataToken`, and fails when the counts differ.  Task 3 pins both with a unit test on a hand-written class.
5. **PostgreSQL 14.**  `merge-returning` has a sidecar recorded on 18: `--check`, the Dapper run and the filters must leave it out on 14 without failing the run of the others.  Task 2 and task 4 cover it; the 14.24 run before the push proves it.

---

### Task 1: The claim, the sidecars and the `models` member

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 5.6: `In progress`)
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/RealisticCase.cs`, `RealisticCases.cs`, `RealisticCasesTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/Cases/two-columns-named-id/case.json`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticQueries.cs`, `RealisticClaims.cs`, `RealisticClaimsTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`, `SqlSource.Tests.Postgres.csproj`, `EndToEnd/CheckTests.cs`
- Modify: `tools/describe-end-to-end.sh`, `tools/AGENTS.md`, `tests/AGENTS.md`
- Create: 55 × `tests/SqlSource.Tests.Postgres/Realistic/Cases/<case>/query.sql.json` (by the script)

**Interfaces:**
- Produces, in `RealisticCase.cs`:
  - `internal sealed record ModelRefusal(string Id, string Why);`
  - `RealisticCase` gains the positional parameter `ModelRefusal? Models = null` after `Samples`.
  - `public const string SidecarFile = "query.sql.json";` and `public string SidecarPath => Path.Combine(Folder, SidecarFile);`
  - `public bool HasModels => Refusal is null && Models is null;`
  - `Files` adds `SidecarFile` when `HasModels`.
- Produces, in `Realistic/Generation/RealisticClaims.cs` (namespace `SqlSource.Tests.Postgres.Realistic.Generation`):
  - `internal sealed record RealisticClaim(string TypeName, string Case);`
  - `internal static class RealisticClaims` with `public static IReadOnlyList<RealisticClaim> All` (reads `RepositoryPaths.RealisticQueries`) and `public static IReadOnlyList<RealisticClaim> Parse(string source)`.
  - `public static RealisticClaim Of(RealisticCase found)`: the one claim of the case, or `Assert.Fail` naming it.
- Produces: `RepositoryPaths.RealisticQueries` = `Realistic/Generation/RealisticQueries.cs`.

- [ ] **Step 1: The epic's row.**  In the phase table of the epic outline, row 5.6's status `Specified` becomes `In progress`.

- [ ] **Step 2: Failing tests of `models` in `RealisticCasesTests`.**
  - `Read_CaseJsonThatIsWrong_ThrowsNamingTheCaseAndTheFault` gains rows:
    - `{ "refusal": "SQLSRC229", "models": { "refusal": "SQLSRC255", "why": "w" } }` → `"both a refusal and a refusal of its models"`
    - `{ "columns": [], "models": { "refusal": " ", "why": "w" } }` → `"models gives no refusal"`
    - `{ "columns": [], "models": { "refusal": "SQLSRC255", "why": "" } }` → `"models gives no why"`
  - `Read_CaseWithModelRefusal_ReadsIt`: `{ "columns": [], "models": { "refusal": "SQLSRC255", "why": "two ids" } }` gives `Models == new ModelRefusal("SQLSRC255", "two ids")` and `HasModels == false`.
  - `Files_MinimumMajor_...` becomes `Files_Case_AreTheQueryTheTruthTheSidecarAndAnExpectationOfEachRecordedMajorItRunsOn`, rows: `(null, no refusal)` → `query.sql, case.json, query.sql.json, expected-pg14.txt, expected-pg18.txt`; `(17)` → `query.sql, case.json, query.sql.json, expected-pg18.txt`.  New `Files_RefusedOrModelRefusedCase_HasNoSidecar` for both kinds of refusal.
  - Rename `Folder_Case_HoldsItsQueryItsTruthAndTheExpectationsOfItsRecordedMajorsAlone` to `Folder_Case_HoldsItsFilesAlone`; its body stays.

- [ ] **Step 3: Run them and see them fail.**  `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticCasesTests"`: the new rows fail.

- [ ] **Step 4: Implement `models` in `RealisticCase` and `RealisticCases`.**  `CaseJson` gains `ModelsJson? Models`, a record of `string? Refusal, string? Why`.  The faults above, in the existing `Fault` style.

- [ ] **Step 5: `two-columns-named-id/case.json` gains** `"models": { "refusal": "SQLSRC255", "why": "a.id and o.id give one property name, Id, which the generator refuses; the case keeps its two columns named id" }`, after `samples`.

- [ ] **Step 6: Failing tests of the claim in `RealisticClaimsTests`** (no server, not in `UsesPostgres`):
  - `Parse_ClassWithPathAttribute_IsItsClaim`: a source with `[SqlSourceGenerate(Path = "../Cases/a-b/query.sql", Output = GeneratorOutput.Models)] internal static partial class ABQuery;` gives `[new("ABQuery", "a-b")]`.
  - `Parse_PathOutsideCases_Throws`: `Path = "Queries/x.sql"` throws `InvalidOperationException` naming the path.
  - `All_Claims_AreTheCasesWithModelsEachOnce` (theory over `RealisticCasesTests.Cases`): a case with `HasModels` has exactly one claim, any other none, naming the case and the count.
  - `All_Claims_NameOnlyCasesThatExist`: every claim's `Case` is in `RealisticCases.Names`.

- [ ] **Step 7: Implement `RealisticClaims`.**  `CSharpSyntaxTree.ParseText`, every `ClassDeclarationSyntax` with an attribute named `SqlSourceGenerate`, its `Path` argument's string literal, matched against `^\.\./Cases/(?<case>[^/]+)/query\.sql$`.

- [ ] **Step 8: `RealisticQueries.cs`.**  One class for each case with `HasModels`, in folder order, namespace `SqlSource.Tests.Postgres.Realistic.Generation`, named the PascalCase of the case's folder with `Query` after it, a comment at the top of the file saying why there is one class per case (a `Path` names no folder below it, and the attribute is not `AllowMultiple`).  Each is:

  ```csharp
  [SqlSourceGenerate(Path = "../Cases/cte-chain/query.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)]
  internal static partial class CteChainQuery;
  ```

  `SqlLocation.Direct` makes the query's constant a member of the class, which the Dapper run reads.  `ArrayAndStringAggQuery` adds `OutputModelType = GeneratorModelType.Class`, with a comment that names TD-0062.  Let `./format.sh` lay them out.

- [ ] **Step 9: The metadata.**  In `SqlSource.Tests.Postgres.csproj`, beside the billing item:

  ```xml
  <!-- The realistic corpus: the database realistic, and the type map's own C# type for timestamptz. -->
  <AdditionalFiles
      Update="Realistic/Cases/**/*.sql"
      SqlSourceDatabase="realistic"
      SqlSourceTypeOverrides="timestamptz=default"
  />
  ```

- [ ] **Step 10: The script.**  In `tools/describe-end-to-end.sh`, `DATABASES=(realistic app billing)`, `REALISTIC="$PROJECT/Realistic"`, the loop reads `schema.sql` and `seed.sql` from `$REALISTIC` for `realistic` and from `$END_TO_END/$database` otherwise, and the `find` deletes the sidecars of `"$END_TO_END/Queries" "$REALISTIC/Cases"`.  Update the header comment, and the rule of `tools/AGENTS.md` that names the files the script applies.

- [ ] **Step 11: Write the sidecars.**  Run `tools/describe-end-to-end.sh`.  Expected: `realistic (postgres): 55 described, 0 skipped, 0 failed`, and `git status` shows 55 new `query.sql.json` and no change to `EndToEnd/Queries/`.

- [ ] **Step 12: `CheckTests` on every database.**  `CommittedEntries` also reads the sidecars under `RepositoryPaths.RealisticCases`.  `Check_Project_EveryEntryIsTheSame` passes the `realistic` connection too, and expects a third line `realistic (postgres): {realistic} same, 0 differ, 0 failed` in the order the tool prints (app, billing, realistic).  Task 2 adds the earlier major.

- [ ] **Step 13: `tests/AGENTS.md`.**  The corpus rule: a folder of `Cases/` holds its sidecar too when it has models; its sidecar is written by `tools/describe-end-to-end.sh` alone; `Realistic/Generation/RealisticQueries.cs` holds one class per case with models, and a case the generator refuses says so by `models` in its `case.json`.  Add `Realistic/Generation/RealisticClaimsTests.cs` to the list of tests that need no server.

- [ ] **Step 14: Validate and commit.**  `./pre-commit-validation.sh` passes.

  ```bash
  git commit -m "Claim each realistic case as a Models query with its sidecar"
  ```

### Task 2: `checkedFrom` and the run of `--check` on an earlier major

**Files:**
- Modify: `Realistic/RealisticCase.cs`, `RealisticCases.cs`, `RealisticCasesTests.cs`, `EndToEnd/CheckTests.cs`

**Interfaces:**
- Produces: `internal sealed record CheckedFrom(int Major, string Why);`, the positional `CheckedFrom? CheckedFrom = null` after `Models`, and `public bool IsCheckedOn(int major) => HasModels && RunsOn(major) && (CheckedFrom is not { } from || from.Major <= major);`

- [ ] **Step 1: Failing tests in `RealisticCasesTests`.**
  - Fault rows: `"checkedFrom": { "major": 15, "why": "w" }` → `"checkedFrom names 15, which is not a recorded major"`; with `"minimumMajor": 18` and `"checkedFrom": { "major": 18, ... }` → `"checkedFrom is not above its minimumMajor"`; `"why": " "` → `"checkedFrom gives no why"`; on a case with `refusal` → `"checkedFrom on a case with no models"`.
  - `IsCheckedOn_CaseAndServer_IsWhetherCheckComparesIt` with rows `(minimum, checkedFrom, major, expected)`: `(null, null, 14, true)`, `(17, null, 14, false)`, `(17, null, 18, true)`, `(null, 18, 14, false)`, `(null, 18, 18, true)`; and a refused case is never checked.

- [ ] **Step 2: See them fail, implement, see them pass.**

- [ ] **Step 3: `CheckTests` on a major that leaves cases out.**  When every case with models `IsCheckedOn(postgres.Major)`, the test is task 1's one run.  Otherwise it makes two runs, each `ExitCode` 0:
  - `describe --check <project> --database realistic --connection realistic=... <query.sql of each case that IsCheckedOn>`, expecting `realistic (postgres): {those cases' entries} same, 0 differ, 0 failed\n`;
  - `describe --check <project> --database app --database billing --connection app=... --connection billing=...`, expecting the two lines of today.
  
  Read what the tool prints with a filter on 14.24 and pin it exactly.  A comment says why the run is split: a filtered check compares only what it names.

- [ ] **Step 4: Run on both images.**  `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*CheckTests"` and the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`: both pass, and on 14 `merge-returning` is not in the first run.

- [ ] **Step 5: Validate and commit.**  `git commit -m "Leave out of --check a realistic case that an earlier major cannot describe as recorded"`

### Task 3: The nullability of the models

**Files:**
- Create: `Realistic/Generation/RealisticModels.cs`, `RealisticModelsTests.cs`, `RealisticModelNullabilityTests.cs`
- Modify: `tests/AGENTS.md` (the two new tests need no server)

**Interfaces:**
- Consumes: `RealisticCase.HasModels`, `SidecarPath`.
- Produces, `internal static class RealisticModels`:
  - `public const string InputSuffix = "Args";` (the project's `SqlSourceInputModelSuffix`) and `OutputSuffix = "Dto"` (the default, `QuerySettings.DefaultOutputModelSuffix`; use that constant).
  - `public static SidecarEntry EntryOf(RealisticCase found)`: the one entry of its sidecar, read by `SidecarReader`.
  - `public static Type OutputModelOf(SidecarEntry entry)`: `PropertyNames.FromTable(entry.MatchesTable)` when `MatchesTable` is set and gives a name, else `entry.Name`, with `OutputSuffix`, in the namespace of `RealisticModels`, from this assembly; fails naming the type when it is missing.
  - `public static Type? InputModelOf(SidecarEntry entry)`: `entry.Name + InputSuffix`, null when the entry has no parameters.
  - `public static IReadOnlyList<PropertyInfo> PropertiesOf(Type model)`: public instance properties ordered by `MetadataToken`.
  - `public static bool IsNullable(PropertyInfo property)`: `Nullable.GetUnderlyingType` for a value type, `NullabilityInfoContext.Create(property).ReadState == NullabilityState.Nullable` for a reference type.

- [ ] **Step 1: Failing unit tests in `RealisticModelsTests`** over a private sealed class `Sample { int A; int? B; string C = ""; string? D; }` declared in that order: `PropertiesOf_Class_IsInDeclarationOrder` (`A, B, C, D`) and `IsNullable_Property_IsWhetherItCanHoldNull` (`false, true, false, true`).  Also `OutputModelOf_EntryThatMatchesATable_IsTheTableModel` on the real `table-statement` case → `BillingInvoicesDto`, and `OutputModelOf_Entry_IsTheQueryModel` on `cte-chain` → `ListCustomerLifetimeValueDto`.

- [ ] **Step 2: See them fail, implement `RealisticModels`, see them pass.**

- [ ] **Step 3: `RealisticModelNullabilityTests.Model_Case_IsAsNullableAsItsTruthOrWider`**, a theory over `RealisticCasesTests.Cases`, no server.  A case without models skips, saying it is refused or which id the generator gave.  It asserts the property count equals the truth's column count, then for each truth column with `Nullable` the property at its position `IsNullable`, failing with `"{found} column {index} {column} can be NULL ({why}), and the property {Model}.{Property} of {type} is not nullable"`.

- [ ] **Step 4: Run it.**  55 pass and 4 skip.  Prove that it can fail: make one class model's property non-nullable by hand in a scratch copy, or temporarily set one truth's `nullable` to `true` on a column the model has not null (e.g. `cte-chain` `id`), see the failure naming it, and undo.

- [ ] **Step 5: Validate and commit.**  `git commit -m "Check that each realistic model is as nullable as its truth"`

### Task 4: The run through Dapper

**Files:**
- Create: `Realistic/Generation/RealisticParameters.cs`, `RealisticParametersTests.cs`, `RealisticDapperTests.cs`
- Modify: `Realistic/RealisticCase.cs`, `RealisticCases.cs`, `RealisticCasesTests.cs`, `EndToEnd/EndToEndSources.cs`
- Modify: the `case.json` of `any-ids-and-enum-array`, `delete-using-returning`, `search-ilike-optional-filter-paged`, `jsonb-containment-and-key`
- Modify: `docs/tech-debt/TD-0062-record-models-with-arrays-and-dapper.md`, `tests/AGENTS.md`

**Interfaces:**
- Consumes: `RealisticModels`, `RealisticClaims.Of`.
- Produces:
  - `RealisticCase` gains the positional `string? Dapper = null` (the why of `dapper`) after `CheckedFrom`, and `public bool IsReadThroughDapperOn(int major) => HasModels && Dapper is null && RunsOn(major);`
  - `EndToEndSources.Realistic(PostgresContainer postgres)`: a plain data source on `realistic`, after the static settings.
  - `internal static class RealisticParameters`:
    - `public static string SelectOf(IReadOnlyDictionary<string, string> sample, IReadOnlyList<SidecarParameter> parameters)`: `SELECT (<literal>)::<type name>, ...` in the parameters' order.
    - `public static object? ValueOf(object value)`: `null` for `DBNull`, the value otherwise.
    - `public static async Task<object> InputModelAsync(NpgsqlDataSource source, Type input, IReadOnlyList<SidecarParameter> parameters, IReadOnlyDictionary<string, string> sample)`: reads the select by `GetValue`, then invokes the record's constructor, each argument by the ctor parameter whose name is `PropertyNames.PascalCase(parameter.Name)`.
    - `public static DynamicParameters ArgumentsOf(object model, IReadOnlyList<SidecarParameter> parameters)`: each parameter's sidecar name, with its property's value.

- [ ] **Step 1: Failing tests of `dapper` in `RealisticCasesTests`.**  Fault rows `"dapper": { "why": "" }` → `"dapper gives no why"`, and `dapper` on a refused case → `"dapper on a case with no models"`.  `IsReadThroughDapperOn_CaseAndServer_IsWhetherTheRunReadsIt`: no member and 14 → true; `dapper` → false; minimum 17 on 14 → false.  Implement, see them pass.

- [ ] **Step 2: The four `case.json` files gain `dapper`**, after `samples`, each `why` naming the parameter, the type it is sent as, and the server's error, e.g. `"@status is a string, which Dapper sends as text, and the server has no operator account_status = text; phase 8's methods bind it by its type"`.  For `jsonb-containment-and-key`: `@filter`, `jsonb @> text`.

- [ ] **Step 3: Failing unit tests in `RealisticParametersTests`** (no server): `SelectOf_Sample_CastsEachLiteralToItsType` (`limit` `10` bigint and `q` `'a'` text → `SELECT (10)::bigint, ('a')::text`), `ValueOf_DbNull_IsNull`, `ArgumentsOf_Model_NamesEachValueAsTheSql` (a private record `R(long Limit, string? Q)` gives `ParameterNames` `limit`, `q` and `Get<object?>("q")` null).

- [ ] **Step 4: See them fail, implement, see them pass.**

- [ ] **Step 5: `RealisticDapperTests.QueryAsync_Case_ReadsEveryRowIntoItsModel`**, a theory over `RealisticCasesTests.Cases` in `UsesPostgres`.  It skips, with the reason, a case with no models, one that needs a later major, and one with `dapper` (giving its why).  For each sample: build the input model (when there are parameters), open a connection on `EndToEndSources.Realistic`, begin a transaction, `connection.QueryAsync(OutputModelOf(entry), sql, ArgumentsOf(...), transaction)` with `sql` the claiming class's constant of `entry.Name` (`BindingFlags.Static | NonPublic | Public`), count the rows, roll back with `CancellationToken.None`.  After the samples, the total is above zero, or it fails with `"{found} read no row for any sample"`.

- [ ] **Step 6: Run on both images.**  On 18.6: 51 pass, 8 skip.  On 14.24: 50 pass, 9 skip, `merge-returning` among them.

- [ ] **Step 7: TD-0062.**  Its Problem names the corpus's `array-and-string-agg`, whose class sets `OutputModelType = GeneratorModelType.Class`; its Proposed fix also takes that setting out.

- [ ] **Step 8: `tests/AGENTS.md`.**  Add `RealisticModelsTests` (task 3) and `RealisticParametersTests` to the tests that need no server if not yet there; the corpus rule says the Dapper run passes the input model's values by the SQL's names and that a `dapper` member is removed by phase 8.

- [ ] **Step 9: Validate and commit.**  `git commit -m "Read each realistic query through Dapper into its model"`

### Task 5: The documents, and 5.6 Done

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/README.md`, `tools/README.md`, `CONTRIBUTING.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: `tests/SqlSource.Tests.Postgres/README.md`.**  The corpus's section: each case with models is a `Models` query claimed by a class of `Realistic/Generation/RealisticQueries.cs`, in the database `realistic`, with its sidecar; `Realistic/Generation/` holds the claim test, the nullability check and the Dapper run, and what each proves; the members `checkedFrom`, `dapper` and `models` in the list of what a `case.json` can say; to add a case, run `tools/describe-end-to-end.sh` beside `tools/record-realistic.sh` and add its class (or `models` when the generator refuses it).  "Writing the sidecars" says the script also describes the corpus, and the opening paragraph's description of the end-to-end project gains the corpus.
- [ ] **Step 2: `tools/README.md`** (the script also applies `Realistic/schema.sql` and `seed.sql` to `realistic`) **and `CONTRIBUTING.md`** (adding a realistic case also runs `tools/describe-end-to-end.sh`).
- [ ] **Step 3: The epic outline.**  Row 5.6 `Done`.  The realistic corpus's decisions under Testing say that a case the generator refuses has a `models` member and no model (`two-columns-named-id`, `SQLSRC255`), that the corpus reads `timestamptz` by the base map, and that the `dapper` cases are four.  Phase 5's Testing paragraph agrees.  The 5.6 spec is not changed.
- [ ] **Step 4: Validate and commit.**  `git commit -m "Document the realistic corpus as models and close phase 5.6"`
