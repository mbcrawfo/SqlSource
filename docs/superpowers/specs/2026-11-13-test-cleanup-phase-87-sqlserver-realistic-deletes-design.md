# Test cleanup phase 87: tests/DatabaseTests/SqlServer/Realistic, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer/Realistic`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 29
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs`
- `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticClaimsTests.cs`
- `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer-2-01

- test: `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticClaimsTests.cs` | `RealisticClaimsTests.Of_CaseWithModels_IsItsClass` | rows: all
- action: delete
- evidence: read (RealisticClaimsTests.cs:19-25 asserts that `RealisticClaims.Of` gives the class `CategoryTreeRecursiveMethod` for the case `category-tree-recursive`.  RealisticModelsTests.cs:40-51 runs `RealisticModels.MethodOf` on the same case, and that method (tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs:104-116) calls `RealisticClaims.Of`, appends `Extensions` to the class name it returns and looks up the compiled type.  It then asserts `DeclaringType` is `CategoryTreeRecursiveMethodExtensions`, so a wrong class name from `Of` fails it.  Neither test needs a server.  The reading of `TypeName` from source is also tested by `Parse_ClassWithPathAttribute_IsItsClaim` in tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs:14-22.  Search: no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test.  tests/DatabaseTests/SqlServer/README.md:78 describes `RealisticClaimsTests` by the theory and `All_Claims_NameOnlyCasesThatExist`, which stay.  The class comment (RealisticClaimsTests.cs:6-11) itself points to `MethodOf_Entry_IsTheMethodOfItsMethodsClaim`.  The test is named only in the plan docs/superpowers/plans/2026-11-03-realistic-corpus-single-claim.md:154, which is no record of a limit or regression); blind: no
- cover: `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelsTests.cs` | `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`
- proposal: Delete the fact and drop `ShouldBeItsClass` from `RealisticClaimChecks` once PostgreSQL's twin is judged the same way in its own slice.  Until then, keep the check for that project.
- verdict: confirmed
- verifier's reason: The evidence names no mutation scope, so there are no kills to read and the named-cover rule decides.  The test (tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticClaimsTests.cs:19-25) runs `RealisticClaimChecks.ShouldBeItsClass` (tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs:19-20), which asserts only that `RealisticClaims.Of(corpus, "category-tree-recursive").TypeName` is `"CategoryTreeRecursiveMethod"`.  The cover `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim` (tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelsTests.cs:40-51) calls `RealisticModels.MethodOf` on the same corpus and case.  That method (tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs:105-117) takes `RealisticClaims.Of(...).TypeName`, appends `Extensions`, looks the type up and finds the entry's method on it.  The test then asserts `DeclaringType` is `typeof(CategoryTreeRecursiveMethodExtensions)`, so a wrong `TypeName` throws there or fails that assertion.  Both tests need no server, and both read the real source of RealisticMethods.cs:26.  Search of the AGENTS.md files, the readmes, CONTRIBUTING.md and docs/ (tech-debt, deferred and architecture included) finds the test only in the plan docs/superpowers/plans/2026-11-03-realistic-corpus-single-claim.md:154, which records no limit or regression.  tests/DatabaseTests/SqlServer/README.md:78 lists four failures of `RealisticClaimsTests`: a case not claimed exactly once, a claim of a case without models, a claim of a folder that is not a case, and a claim that sets more than `Source`.  This fact checks none of them, so the README stays true once it goes.  The class comment (RealisticClaimsTests.cs:6-11) names the cover, not this test, as what pins the output.  Realistic/AGENTS.md:10's "which `RealisticClaimsTests` checks" refers to the one claim with `Source` alone, which the theory and the parsing check.  `ShouldBeItsClass` is shared with PostgreSQL's twin (tests/DatabaseTests/Postgres/Realistic/Generation/RealisticClaimsTests.cs:20-21), and the proposal rightly keeps it until that slice decides.
- condition: Remove `RealisticClaimChecks.ShouldBeItsClass` from `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs` once no caller is left: phase 84 deletes PostgreSQL's twin before this one.  The review corrected `.claude/skills/reviewing-test-quality/broad-tests.txt`, which named the whole namespace `SqlSource.DatabaseTests.SqlServer.Realistic.` as broad, to the classes that run the corpus; the cover's class is not one of them, so the cover is a focused test.

### F-DatabaseTests.SqlServer-2-02

- test: `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs` | `RealisticNullabilityOracleTests.RunAsync_Parameter_IsBoundToItsSample` | rows: all
- action: delete
- evidence: read (RealisticNullabilityOracleTests.cs:63-82 binds a plain `varchar(10)` parameter to `'active'` and asserts the names and that the row count equals an independent count, which is below the total.  `RunAsync_SampleThatIsAnExpressionWithAComment_IsBound` (lines 109-130) binds a plain `datetime2(0)` parameter.  It asserts that the row count equals an independent count that is pinned at 4 (orders 12 to 15), so it proves binding with a filter that bites on the same path.  `SqlServerTypes.Declared` (src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs:54-64) returns `type.Name` for both plain types, so the two tests take the same branch of `RealisticSamples.Batch` and of the oracle.  `RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound` (lines 84-107) proves binding again, on the alias branch.  The isolated text of a plain declaration is in `RealisticSamplesTests.Batch_TwoParameters_DeclaresEachSampleAndBindsItByName`.  Search: tests/DatabaseTests/SqlServer/README.md:68 says the oracle tests check "that a sample is bound", which the two remaining tests still make true.  No AGENTS.md, tech-debt or deferred item names the test.  Its class comment (lines 11-13) lists binding as one of several behaviours, with no record of a regression); blind: no
- cover: `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs` | `RealisticNullabilityOracleTests.RunAsync_SampleThatIsAnExpressionWithAComment_IsBound`; `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs` | `RealisticNullabilityOracleTests.RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound`
- proposal: Delete the test and its constant `CountActive`.  `CountCustomers` stays, since the alias test uses it at line 105.  The README line 68 stays true as written.
- verdict: confirmed
- verifier's reason: The evidence names no mutation scope.  This is a database test, so its cover must be on SQL Server, and it is: both named covers are in the same class and collection.  The test (tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs:63-82) binds a `varchar(10)` sample to `@status`.  It asserts the names and that the row count equals an independent count that is less than all customers.  `RunAsync_SampleThatIsAnExpressionWithAComment_IsBound` (lines 109-130) binds a `datetime2(0)` sample with a filter pinned at 4 rows of the seed's 15 orders (tests/DatabaseTests/SqlServer/Realistic/seed.sql:85-105).  Unbound or NULL binding would give 0 rows, so that filter bites, and more strictly than this test, which never asserts that `active` is above 0.  Both plain types go down the same path.  `SqlServerTypes.Declared` (src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs:54-58) returns `type.Name` for a type with no user type.  `RealisticSamples.Batch` (tests/DatabaseTests/SqlServer/Realistic/RealisticSamples.cs:30-54) builds the same `DECLARE ... = (\n<value>\n);` and `sp_executesql` call for each.  `RealisticNullabilityOracle.RunAsync` (RealisticNullabilityOracle.cs:30-70) has no branch by type.  `RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound` (lines 84-107) proves binding again, through the alias branch.  A quoted string literal as a sample adds no path of its own: the value is put on lines of its own whatever it is.  Search of the AGENTS.md files, the readmes, CONTRIBUTING.md and docs/ finds the test name nowhere.  `CountActive` appears only in site/generator.md:721, an unrelated generated-interface example.  tests/DatabaseTests/SqlServer/README.md:68 says the oracle tests check "that a sample is bound, an expression that ends in a comment too", and the expression test keeps that true.  The class comment (lines 11-13) lists binding among its behaviours and records no regression.  The proposal correctly keeps `CountCustomers`, which line 105 uses, and drops `CountActive` (line 21), which only this test uses.
- condition: The review corrected `.claude/skills/reviewing-test-quality/broad-tests.txt`, which named the whole namespace `SqlSource.DatabaseTests.SqlServer.Realistic.` as broad, to the classes that run the corpus; `RealisticNullabilityOracleTests` is not one of them, so the covers are focused tests.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer-2-01 | delete | `RealisticClaimsTests.Of_CaseWithModels_IsItsClass` | `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelsTests.cs`: `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim` |
| F-DatabaseTests.SqlServer-2-02 | delete | `RealisticNullabilityOracleTests.RunAsync_Parameter_IsBoundToItsSample` | `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs`: `RealisticNullabilityOracleTests.RunAsync_SampleThatIsAnExpressionWithAComment_IsBound`; `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs`: `RealisticNullabilityOracleTests.RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer`, `dotnet test --project tests/SqlSource.Tool.Testing`.
This phase changes describer tests: run `tests/DatabaseTests/Postgres` on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `tests/DatabaseTests/SqlServer` on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` too, for the projects it touches, as `AGENTS.md` asks before a push.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/SqlServer/README.md`: 7, 68, 78, 85
- `tests/DatabaseTests/SqlServer/Realistic/AGENTS.md`: 5, 10

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
