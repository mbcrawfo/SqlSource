# Query generation 8.5: the realistic corpora through the methods - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run every realistic case with models, on both engines and every major it runs on, through its generated method over the seed with each of its samples, beside the Dapper run, which stays.

**Architecture:** Each engine's `Realistic/Generation/RealisticMethods.cs` claims each case's `query.sql` a second time, at `CodeGen`, in the namespace of `RealisticQueries.cs`, so the two claims share the case's models.  `RealisticClaims` reads both files and the `Output` of each claim.  A shared helper, `RealisticCalls` of `tests/SqlSource.Tool.Testing/Realistic/`, finds a case's method on its claim's extension class by reflection, calls it and reads the returned collection whole.  Each engine's `RealisticMethodTests` runs the theory, and on a read of a `NULL` into a not-null property reruns the sample through the existing `RealisticNullabilityOracle` to name the column.

**Tech Stack:** C# on .NET 10, xunit v3, Shouldly, Roslyn syntax (claims), Npgsql 10 on PostgreSQL 14.24 and 18.6, Microsoft.Data.SqlClient 7 on SQL Server 2017 (`2017-CU31-GDR19-ubuntu-18.04`) and 2025.

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-5-realistic-corpora-design.md`](../specs/2026-10-24-query-generation-phase-8-5-realistic-corpora-design.md), with 8.1's spec (signature, shapes, reading a row) and [8.0's spike](../specs/2026-10-24-query-generation-phase-8-spike.md) section 1 (what a `NULL` read as `T` throws).

## Global Constraints

- No change to any case's SQL, truth, samples or committed sidecar.  No new case.  The `dapper` members stay; only their `why` is rewritten (Task 4).
- The Dapper run, `RealisticDapperTests`, is unchanged and runs over the `Models` claims.
- Every case with models runs through its method; a case is left out only by `minimumMajor` (a later major) or for having no models.  `dapper` never leaves a case out of the method run.
- A case whose samples all read no row fails.  Each sample runs in a transaction rolled back with `CancellationToken.None`; the method gets the test's token.
- The reflection is the test's; no generated or shipped code uses it.
- A not-null column that holds a `NULL` fails the case, and the failure names the column and its property.  Such a failure is a finding: never work around it in the test; report it to the session's lead (repository rule: fix or record as tech debt, owner's choice).
- Every commit passes `./pre-commit-validation.sh` after its last edit.  A task that changes `tests/SqlSource.Tests.Postgres` also runs that project with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; one that changes `tests/SqlSource.Tests.SqlServer` also runs it with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- Tests: `Method_Scenario_Outcome`, Shouldly, `sealed` classes, a comment above each class saying what it proves.  Shared helpers stay `internal` in `tests/SqlSource.Tool.Testing/Realistic/`, their tests in `tests/SqlSource.Tool.Tests/Realistic/`.  An `AGENTS.md` or readme that names a file is updated in the commit that adds or renames it.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Where merged code differs from the spec

- **Counts.**  As the spec says: 55 PostgreSQL and 53 SQL Server cases with models; 4 PostgreSQL cases and 1 SQL Server case carry `dapper`.  `merge-returning` needs PostgreSQL 17; four SQL Server cases need 16 or 17, so they skip on 2017.
- **Every case's shape is `many`.**  No case's `-- name:` marker gives a shape and every entry's `resultKind` is `rows`, so every method returns `Task<IReadOnlyList<TDto>>` (both projects set `SqlSourceCollectionType` to `IReadOnlyList`).  `RealisticCalls` still counts by the returned collection, and fails on any other return type, so a case that comes to have another shape fails naming itself rather than passing unread.
- **SqlClient's `NULL` read does not name the column.**  Npgsql throws `InvalidCastException("Column '<name>' is null.")`; SqlClient throws `SqlNullValueException` with no name (spike section 1).  So on either exception the test runs the sample through the engine's `RealisticNullabilityOracle`, as `RealisticNullabilitySafetyTests` does, and names every column whose property is not nullable and which held a `NULL`.
- **`vector`.**  Neither corpus has a `vector` column, and the generated read checks `IsNull` on a not-null `SqlVector<float>` (8.0's owner decision), which throws `InvalidCastException` too; no kind of type is left to the truth check alone.

## Names

| Thing | Name |
|----|----|
| Second claim file, each engine | `tests/SqlSource.Tests.<Engine>/Realistic/Generation/RealisticMethods.cs` |
| Class of a case in it | `<PascalCaseOfFolder>Method`, e.g. `AggregateCteJoinedInnerMethod` (the `Models` class is `...Query`) |
| Its extension class (generated) | `<PascalCaseOfFolder>MethodExtensions` |
| Path to the claim file | `RealisticCorpus.Methods` |
| Shared call helper | `tests/SqlSource.Tool.Testing/Realistic/RealisticCalls.cs`, `internal static class RealisticCalls` |
| Its tests | `tests/SqlSource.Tool.Tests/Realistic/RealisticCallsTests.cs` |
| Engine run | `tests/SqlSource.Tests.<Engine>/Realistic/Generation/RealisticMethodTests.cs`, theory `Method_Case_ReadsEveryRowIntoItsModel` |

## Review Focus

1. **A method that throws for a reason other than a `NULL`** (a type the driver reads otherwise than the model says): the failure must show the driver's exception as is, never swallowed or relabelled.  Task 2's `RealisticCallsTests` pins that `InvokeAsync` lets the exception through unwrapped (not as `TargetInvocationException`).
2. **A `NULL` read with no column found by the oracle** (the method threw, the rerun saw no `NULL` in a not-null column): the failure must still fail with the exception, saying the oracle found none.  Task 2's `RealisticCallsTests.NullColumnsOf_NoNotNullColumnHeldNull_SaysNone` pins the message.
3. **A case that comes to return another shape** (a `-- name: X :one` added later): it must fail naming the case and the return type, not pass unread.  Task 2 pins `MethodOf` failing on a method that does not return `Task<IReadOnlyList<output>>`.
4. **A rolled-back write after a failure** (`data-modifying-cte-archive` throwing mid-read): the transaction must still roll back, so a failure cannot change what later cases see.  Tasks 2 and 3 put the rollback in a `finally`.
5. **The second claim drifting from the first** (a case added to `RealisticQueries.cs` but not `RealisticMethods.cs`): `RealisticClaimsTests` fails naming the case.  Task 1 pins it.

---

### Task 1: The outline's row, the second claims, and the claims test

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.5: `Not started` -> `In progress`)
- Add: this plan
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticClaims.cs`, `RealisticCorpus.cs`, `RealisticModels.cs` (`SqlOf`)
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticMethods.cs`, `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticMethods.cs`
- Modify: both engines' `Realistic/Generation/RealisticClaimsTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`, `RealisticCorpusTests.cs`
- Docs in the same commit: `tests/AGENTS.md` (the two bullets "`Realistic/Generation/RealisticQueries.cs` holds one class for each case with models" now name both files and both outputs), `tests/README.md` line 43 (`RealisticClaims` reads the claims of a corpus's `RealisticQueries.cs` and `RealisticMethods.cs`) and line 109 (the attributed types are also in `RealisticMethods.cs`).

**Interfaces:**
- Produces: `internal sealed record RealisticClaim(string TypeName, string Case, string? Output)`, `Output` the member name of `GeneratorOutput` the attribute names (`"Models"`, `"CodeGen"`), null when the attribute sets none.
- Produces: `RealisticClaims.ModelsOutput = "Models"`, `RealisticClaims.CodeGenOutput = "CodeGen"`; `RealisticClaims.AllOf(RealisticCorpus)` reads `corpus.Queries` then `corpus.Methods`; `RealisticClaims.Of(RealisticCorpus corpus, RealisticCase found, string output)` returns the one claim of the case at that output, failing with `ClaimedBy` otherwise.  `ClaimedBy(RealisticCorpus, RealisticCase, string output, int count)` says "`{found}` is claimed at `{output}` by `{count}` classes of `{Queries}` and `{Methods}`".
- Produces: `RealisticCorpus.Methods => Path.Combine(Folder, "Generation", "RealisticMethods.cs")`.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`: `Parse_ClassWithPathAttribute_IsItsClaim` now expects `new RealisticClaim("ABQuery", "a-b", "Models")`; `Parse_QualifiedAttributeName_IsItsClaim` expects `Output` null (its source sets none); new `Parse_ClaimAtCodeGen_HasThatOutput` (`Output = GeneratorOutput.CodeGen` gives `"CodeGen"`) and `Parse_QualifiedOutput_IsItsMemberName` (`Output = SqlSource.GeneratorOutput.CodeGen` gives `"CodeGen"`).  In `RealisticCorpusTests`, the loop that checks `File.Exists(corpus.Queries)` also checks `corpus.Methods`.

In each engine's `RealisticClaimsTests`, replace the theory with `All_Claims_AreTheCasesWithModelsOnceAtEachOutput(string name)`: for a case with models, exactly one claim at `ModelsOutput` and one at `CodeGenOutput` and no other; for a case without, none (each count asserted with `ClaimedBy`'s message).  Add `Queries_EveryClaim_IsAtModels` and `Methods_EveryClaim_IsAtCodeGen`, which `Parse` each file alone and assert every claim's `Output`.  Keep `All_Claims_NameOnlyCasesThatExist`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*RealisticClaimsTests" --filter-class "*RealisticCorpusTests"`
Expected: FAIL: the record has no `Output`, and `Methods` does not exist.

- [ ] **Step 3: Implement**

`RealisticClaims.Parse` reads the `Output` argument the way it reads `Path`: a `MemberAccessExpressionSyntax` whose name is the member (`GeneratorOutput.X` or qualified), its `Name.Identifier.ValueText`; any other expression throws naming the class.  The argument's name is `AttributeSource.OutputProperty`, as `Path`'s is `AttributeSource.PathProperty`.  `RealisticModels.SqlOf` calls `RealisticClaims.Of(corpus, found, RealisticClaims.ModelsOutput)`.

Write each engine's `RealisticMethods.cs` from its `RealisticQueries.cs`: the same cases in the same order, each class `<Pascal>Method` with `[SqlSourceGenerate(Path = "../Cases/<case>/query.sql", Output = GeneratorOutput.CodeGen)]` (no `SqlLocation`: a `CodeGen` query has no constant), `internal static partial class`, the same namespace.  Its header comment says: each case with models is claimed a second time, at `CodeGen`, so its method runs it; same namespace so the two claims share the models; `RealisticClaimsTests` fails on a case missing from either file.  Mention in `RealisticQueries.cs`'s header that `RealisticMethods.cs` is the second claim.

- [ ] **Step 4: Build and run**

Run: `dotnet build` then the Step 2 command and `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticClaimsTests"` and the same for `tests/SqlSource.Tests.SqlServer`.
Expected: the build has no error or warning (a `SQLSRC27x` on a `CodeGen` claim is a finding: stop and report it with the case); all claims tests pass.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh` (PASS).  Commit: "Claim each realistic case a second time at CodeGen".

### Task 2: The method run on PostgreSQL

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticCase.cs` (`IsReadThroughMethodOn`), `RealisticModels.cs` (`SkipUnlessReadThroughMethod`, `MethodOf`)
- Create: `tests/SqlSource.Tool.Testing/Realistic/RealisticCalls.cs`, `tests/SqlSource.Tool.Tests/Realistic/RealisticCallsTests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticMethodTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Realistic/RealisticCaseTests.cs`, `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticModelsTests.cs`
- Docs in the same commit: `tests/AGENTS.md` (the shared list of `tests/SqlSource.Tool.Testing/Realistic/` gains `RealisticCalls`), `tests/README.md` line 43 (`RealisticCalls`, which finds and calls a case's method).

**Interfaces:**
- Produces: `RealisticCase.IsReadThroughMethodOn(int major) => HasModels && RunsOn(major)`.
- Produces: `static void RealisticModels.SkipUnlessReadThroughMethod(RealisticCase found, int major)`: returns when `IsReadThroughMethodOn`; else `SkipUnlessHasModels`, then `SkipUnlessRunsOn`.
- Produces: `MethodInfo RealisticModels.MethodOf(RealisticCorpus corpus, RealisticCase found, SidecarEntry entry)`: the `CodeGen` claim's `{namespace}.{TypeName}Extensions` in the assembly, its static method named `entry.Name` (public or not), checked by `RealisticCalls.CheckSignature(method, OutputModelOf(entry), InputModelOf(entry))`; throws `InvalidOperationException` naming the case and the class or method when missing.
- Produces, in `RealisticCalls`:
  - `static void CheckSignature(MethodInfo method, Type output, Type? input)`: parameters exactly `connection` (`DbConnection`), then `parameters` (`input`) when `input` is not null, then `transaction` (`DbTransaction`), `commandTimeout` (`int?`), `cancellationToken` (`CancellationToken`); return type exactly `Task<IReadOnlyList<output>>`.  Throws naming the method and what differs.
  - `static async Task<IReadOnlyList<object>> InvokeAsync(MethodInfo method, DbConnection connection, object? input, DbTransaction transaction, CancellationToken token)`: invokes with `commandTimeout` null, awaits the returned `Task`, reads its `Result` by reflection, and returns its elements enumerated whole.  A synchronous throw is unwrapped from `TargetInvocationException` (`ExceptionDispatchInfo.Capture(inner).Throw()`); an asynchronous one surfaces from the `await` as is.
  - `static string NullColumnsOf(RealisticCase found, Type output, IReadOnlyList<string> names, IReadOnlyList<bool> sawNull)`: one line per column `i` whose property (`ModelReflection.PropertiesOf(output)[i]`) is not nullable and `sawNull[i]`: "`{found}` column `{i}` `{names[i]}` is the property `{output.Name}.{property.Name}` of `{type}`, which is not nullable, and held a NULL over the seed"; when there is none, "`{found}`: the oracle saw no NULL in a column whose property is not nullable".

- [ ] **Step 1: Write the failing tests**

`RealisticCaseTests`: `IsReadThroughMethodOn_CaseAndServer_IsWhetherTheRunReadsIt` with rows `(null, null, 14, true)`, `(null, "w", 14, true)` (a `dapper` case runs), `(17, null, 14, false)`, `(17, "w", 18, true)`; `IsReadThroughMethodOn_RefusedOrModelRefusedCase_IsNever` as its Dapper twin.

`RealisticCallsTests`, over a private static class of fakes in the test file with methods of the generated signature (one with `parameters`, one without, one returning `Task<List<Row>>`, one that throws synchronously before its first `await`, one that throws after an `await`), and a private `sealed record Row(int Id, string? Name)`:
- `CheckSignature_GeneratedSignature_Passes` (with and without input).
- `CheckSignature_OtherReturnType_ThrowsNamingIt` (message contains the method's name and `List`).
- `CheckSignature_MissingParameters_Throws` (input given, method has none).
- `InvokeAsync_Method_ReturnsEveryRow` (the fake returns two rows; the result is those two, in order) and `InvokeAsync_Method_PassesConnectionInputTransactionAndToken` (the fake records what it got; `commandTimeout` is null).  The fakes never touch the connection or the transaction, so the test passes stubs: a `private sealed class StubConnection : DbConnection` and a `StubTransaction : DbTransaction` whose members throw `NotSupportedException`.
- `InvokeAsync_MethodThatThrowsAtOnce_ThrowsItsException` and `InvokeAsync_MethodThatThrowsLater_ThrowsItsException`: `Should.ThrowAsync<InvalidCastException>`, never `TargetInvocationException`.
- `NullColumnsOf_NotNullColumnThatHeldNull_NamesColumnAndProperty`, `NullColumnsOf_NullableColumnThatHeldNull_IsNotNamed`, `NullColumnsOf_NoNotNullColumnHeldNull_SaysNone`.

`tests/SqlSource.Tests.Postgres/.../RealisticModelsTests`: `MethodOf_Entry_IsTheMethodOfItsCodeGenClaim` (`cte-chain`: the method `ListCustomerLifetimeValue` of `CteChainMethodExtensions`) and `MethodOf_EntryWithoutAMethod_ThrowsNamingIt` (entry renamed `Missing`).

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*RealisticCallsTests" --filter-class "*RealisticCaseTests"`.  Expected: FAIL to compile (no `RealisticCalls`, no `IsReadThroughMethodOn`).

- [ ] **Step 3: Implement** the interfaces above.

- [ ] **Step 4: Write `RealisticMethodTests` (PostgreSQL)**

`[Collection(UsesPostgres.Name)] public sealed class RealisticMethodTests(PostgresContainer postgres)`, theory `Method_Case_ReadsEveryRowIntoItsModel(string name)` over `RealisticCasesTests.Cases`, the shape of `RealisticDapperTests`:
1. `RealisticModels.SkipUnlessReadThroughMethod(found, postgres.Major)`; entry, output, input, `method = RealisticCases.Models.MethodOf(...)`; `await using var source = EndToEndSources.Realistic(postgres)`.
2. For each sample (index `k`): the input model by `RealisticParameters.InputModelAsync(source, input, entry.Parameters, sample)` when `input` is not null; open a connection, begin a transaction; in `try`, `read = await RealisticCalls.InvokeAsync(method, connection, model, transaction, TestContext.Current.CancellationToken)`; `read.ShouldAllBe(row => row.GetType() == output, ...)`; `rows += read.Count`; `catch (InvalidCastException thrown)`: `Assert.Fail($"{found} sample {k} threw {thrown.Message}\n{await NullColumnsAsync(found, output, sample)}")`; `finally` roll back with `CancellationToken.None`.
3. After the loop, `Assert.Fail($"{found} read no row for any sample")` when `rows == 0`.
4. `private async Task<string> NullColumnsAsync(RealisticCase found, Type output, IReadOnlyDictionary<string, string> sample)`: as `RealisticNullabilitySafetyTests.Execute_Case_...` builds its run (describe through `RealisticCases.Describe`, `RealisticSamples.Substitute(request.Sql, sample, types)`, `RealisticNullabilityOracle.RunAsync`), then `RealisticCalls.NullColumnsOf(found, output, run.Names, run.SawNull)`.  The case runs on this major and has models, so `RealisticCases.Describe.DescribedAsync(found, postgres.RealisticConnectionString)` needs no `Start`.

The class comment: what it runs and why beside the Dapper run; the second oracle (a not-null property's `NULL` throws, the failure names the column); a `dapper` case runs here; the truth check remains the oracle of nullability as such.

- [ ] **Step 5: Run it on both images**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "*RealisticMethodTests" --filter-class "*RealisticModelsTests"`, then the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
Expected: 55 cases pass on 18 (the four `dapper` cases included); on 14, `merge-returning` skips naming 17 and 54 pass; refused cases skip naming their reason.  A failure is a finding: report it (case, exception, named column) and stop, never change the case or the test to pass.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh` (PASS).  Commit: "Run the PostgreSQL realistic cases through their methods".

### Task 3: The method run on SQL Server

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticMethodTests.cs`
- Modify: `tests/SqlSource.Tests.SqlServer/Realistic/Generation/RealisticModelsTests.cs`

**Interfaces:**
- Consumes: Task 2's `RealisticModels.SkipUnlessReadThroughMethod`, `RealisticModels.MethodOf`, `RealisticCalls.InvokeAsync`, `RealisticCalls.NullColumnsOf`.

- [ ] **Step 1: Write the tests**

`RealisticModelsTests`: `MethodOf_Entry_IsTheMethodOfItsCodeGenClaim` on a case without parameters of this corpus (e.g. `category-tree-recursive`, `ListCategoryTree`, class `CategoryTreeRecursiveMethodExtensions`).

`RealisticMethodTests` as Task 2's, with the differences of `RealisticDapperTests` of this project: one `SqlConnection` from `EndToEndSources.Realistic(sqlServer)`, opened once; each sample's input model read on it before the sample's transaction begins; the transaction given to the method.  It catches `InvalidCastException` and `System.Data.SqlTypes.SqlNullValueException`, which SqlClient throws without the column's name; `NullColumnsAsync` builds the run as this project's `RealisticNullabilitySafetyTests` does (`RealisticSamples.Batch(request.Sql, sample, parameters)`).

- [ ] **Step 2: Run on both images**

Run: `dotnet test --project tests/SqlSource.Tests.SqlServer -- --filter-class "*RealisticMethodTests" --filter-class "*RealisticModelsTests"`, then with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
Expected: 53 pass on 2025, `insert-invoice-next-value` included; on 2017 the four cases of `minimumMajor` 16 or 17 skip and 49 pass.  A failure is a finding, as in Task 2.

- [ ] **Step 3: Validate and commit**

Run: `./pre-commit-validation.sh` (PASS).  Commit: "Run the SQL Server realistic cases through their methods".

### Task 4: The documents, and the row set to Done

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md` (the realistic corpus sections)
- Modify: `tests/AGENTS.md` (the `dapper` bullet of PostgreSQL's corpus; SQL Server's `dapper` sentence)
- Modify: the `why` of `dapper` in `tests/SqlSource.Tests.Postgres/Realistic/Cases/{any-ids-and-enum-array,delete-using-returning,jsonb-containment-and-key,search-ilike-optional-filter-paged}/case.json` and `tests/SqlSource.Tests.SqlServer/Realistic/Cases/insert-invoice-next-value/case.json`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.5 `Done`)

- [ ] **Step 1: Readmes**

In each readme's corpus section: each case with models is claimed twice, at `Models` by `RealisticQueries.cs` and at `CodeGen` by `RealisticMethods.cs`, in one namespace so they share the models; `RealisticClaimsTests` checks both files.  A new bullet for `RealisticMethodTests`: each case runs through its generated method over the seed with each sample, in a transaction rolled back, `dapper` cases included; it fails on an error, a row of another type, a case that reads no row, and a `NULL` in a column whose property is not nullable, naming the column through the oracle's rerun.  The `dapper` bullet: the member leaves the case out of the Dapper run alone, and `RealisticMethodTests` runs it (drop "are to run ... in sub-phase 8.5" and "phase 8's generated methods ... are to run").  "To add a case": add its class to both files.  `minimumMajor` leaves a case out of the method run too.  `RealisticModelsTests` also tests `MethodOf`; `RealisticCalls` is among the shared helpers.

- [ ] **Step 2: `tests/AGENTS.md`**

The PostgreSQL `dapper` rule ends at "which leaves it out of the Dapper run alone; `RealisticMethodTests` runs it."  Add `RealisticMethodTests` beside `RealisticDapperTests` in both corpora's rules: it gives every call the transaction it rolls back.

- [ ] **Step 3: The five `why`s**

Each keeps the reason Dapper cannot send the value and drops its last clause ("; phase 8 binds it", "; phase 8 binds by type", "; phase 8 binds it by its type").  E.g. `@status is a string, sent as text: the server has no operator account_status = text`.  Nothing else of the `case.json` changes.

- [ ] **Step 4: Row 8.5 Done**, and run `./pre-commit-validation.sh` (PASS), then both container projects on their floor images once more.

- [ ] **Step 5: Commit**: "Document the realistic method run and close 8.5".
