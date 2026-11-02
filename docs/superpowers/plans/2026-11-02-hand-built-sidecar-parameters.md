# Hand-built sidecar parameter lists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every sidecar entry that a test builds by hand lists exactly the parameters its query's SQL has, in the order the SQL has them, as the tool would write them, and the harness fails a test whose entry does not.

**Architecture:** Test-only.  The generator's tests hand a sidecar to a run through `Sidecars.Of` of `tests/SqlSource.Testing/Generator/Sidecars.cs`, which has both the `.sql` file and the entries, so it checks there: an entry that is current for its query (its hash is the query's under some dialect) must list the query's parameters by name, spelling, order and ordinal.  A test of a sidecar a person edited goes through a new `Sidecars.HandEdited`, unchecked.  The two harness helpers that write "current" entries, `Sidecars.Current` and the tool's `SidecarFiles.EntryFor`, list the query's parameters by construction, and `QueryTyperTests` builds its SQL from its entry.

**Tech Stack:** C# (net10.0), xUnit v3 on Microsoft Testing Platform, Shouldly.

**Spec:** none.  The brief is the owner's request relayed by the implementation manager (unit `7-sidecars`): fix every mismatch, keep a deliberately wrong entry with a comment saying so, add a cheap guard, change no product code, generated output or golden file.

## Global Constraints

- No change under `src/`, to any golden file under `tests/SqlSource.Tests/Generator/Golden/`, or to `examples/`.  `QueryMethodCorpusTests` passes unchanged.
- The check compares names (ordinal, the SQL's spelling), order and ordinals (`Ordinal == index`).  It does not compare nullability or types: the corpus and about 20 classes set parameter nullability with no marker, and matching it would change golden files.
- The query's parameter list is `SqlBlock.Parameters`: the SQL's parameters in order of first appearance, then those that only a `-- param:` marker declares (see `src/SqlSource/Parsing/SqlParameterList.cs`).  That is what the tool writes (`src/SqlSource.Tool/Describing/EntryBuilder.cs`).
- Every file under `tests/SqlSource.Tests/Generator/` and `tests/SqlSource.Testing/Generator/` stays within Roslyn 4.14.0 and C# 12, and `tests/SqlSource.Tests.RoslynFloor` compiles it too.
- Keep changes to the hand-built entries and the lines they need: the query-method refactor units edit the same test files in parallel.
- A test's assertions keep guaranteeing what its name says.  Never weaken one; an expected text changes only where the SQL change forces it, and the hand-back names each one.
- Repository rules: `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`.  Test names are `Method_Scenario_Outcome`, assertions Shouldly's, classes `sealed`.

## Review Focus

1. An entry the test means to be stale (its SQL changed after the entry was written): the check must skip it, since its hash is not the query's.  Pinned by `Of_StaleEntry_IsNotChecked`.
2. An entry of a query the file lacks (an orphan entry, `SidecarTests`): skipped.  Pinned by `Of_EntryOfAQueryTheFileLacks_IsNotChecked`.
3. A `.sql` file that does not parse, handed to `Sidecars.Of` by a test of the parser's diagnostics: no throw from the check.  Pinned by `Of_FileThatDoesNotParse_WritesTheSidecar`.
4. A file whose `-- dialect:` marker differs from the run's, or an entry whose hash was computed under `Ansi` while its engine says `postgres`: the check finds the dialect by the hash.  Pinned by `Of_EntryHashedUnderAnotherDialect_IsChecked`.
5. A parameter that only a `-- param:` marker declares: listed after the SQL's.  Pinned by `Current_QueryWithAMarkerOnlyParameter_ListsItLast`.

---

### Task 1: The check in `Sidecars.Of`, and every generator test it fails

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/Sidecars.cs`
- Test: `tests/SqlSource.Tests/Generator/SidecarHarnessTests.cs`
- Modify (fixes): `tests/SqlSource.Tests/Generator/ModelTests.cs`, `ModelNameTests.cs`, `ModelWarningTests.cs`, `SharedModelTests.cs`, `TableModelTests.cs`, `QueryTypesTests.cs`, `TypeOverrideLevelTests.cs`, `ModelRunsTests.cs`, `CachingModelTests.cs`, `ParameterTypeTests.cs`, and any other the check fails
- Modify (docs): `tests/SqlSource.Tests/Generator/AGENTS.md`, `tests/README.md` (the `Sidecars` clause of the `tests/SqlSource.Testing` row)

**Interfaces:**
- Produces: `Sidecars.Of(SqlFile, params SidecarEntry[])` and `Sidecars.Of(SqlFile, int, string, params SidecarEntry[])` throw `InvalidOperationException` for a mismatched current entry; `public static SqlFile HandEdited(SqlFile sqlFile, params SidecarEntry[] entries)` writes the sidecar as `Of` does, unchecked; `Sidecars.Current` lists each query's parameters as `TestSidecars.Parameter(ordinal, name, null, nullable)` with the block's name and `Nullable`.

- [ ] **Step 1: Write the failing harness tests** in `SidecarHarnessTests`, each building a file with `new SqlFile(GeneratorHarness.UsersPath, ...)` and entries through `Sidecars.Entry(name, Sidecars.HashOf(file, name, dialect), engine) with { Parameters = TestSidecars.Of(...) }` or `ModelRuns.Entry`:
  - `Of_CurrentEntryWithoutTheSqlsParameter_ThrowsNamingBothLists`: SQL `-- name: Q\nSELECT @id;\n`, entry with no parameters; `Should.Throw<InvalidOperationException>`, message contains `"Q"`, `"[]"` and `"[id]"`.
  - `Of_CurrentEntryWithAParameterTheSqlLacks_Throws`: SQL `SELECT 1;`, entry lists `id`.
  - `Of_CurrentEntryOutOfTheSqlsOrder_Throws`: SQL `SELECT @a, @b;`, entry lists `b, a`.
  - `Of_CurrentEntryInAnotherSpelling_Throws`: SQL `SELECT @author;`, entry lists `Author`.
  - `Of_CurrentEntryWhoseOrdinalIsNotItsPlace_Throws`: SQL `SELECT @a;`, entry lists `a` at ordinal 1.
  - `Of_StaleEntry_IsNotChecked`: entry hash `"h"` listing `id` for `SELECT 1;`; `Should.NotThrow`.
  - `Of_EntryOfAQueryTheFileLacks_IsNotChecked`: entry `Gone` with a parameter.
  - `Of_FileThatDoesNotParse_WritesTheSidecar`: the duplicate-name file of `HashOf_FileThatDoesNotParse_ThrowsThatItDoesNotParse`, entry `Q` with hash `"h"`; returns the sidecar.
  - `Of_EntryHashedUnderAnotherDialect_IsChecked`: hash from `Sidecars.HashOf(file, "Q")` (Ansi) with engine `postgres`, missing `id`; throws.
  - `Of_EntryListingTheSqlsParameters_WritesTheSidecar`: SQL `SELECT @b, @a, @b;`, entry `b, a`; reads back with those names.
  - `HandEdited_EntryInAnotherSpelling_WritesItAsGiven`: as the spelling case through `HandEdited`; reads back `Author`.
  - `Current_QueryWithParameters_ListsThemAsTheSqlHasThem`: SQL `SELECT @b, @a, @b;`; read-back entry's parameter names `["b", "a"]`, ordinals `[0, 1]`, types null.
  - `Current_QueryWithAMarkerOnlyParameter_ListsItLast`: SQL `-- name: Q\n-- param: @z int4\nSELECT @a;\n` (check the marker grammar in `docs/manual/` for the exact `-- param:` form under postgres); names `["a", "z"]`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.SidecarHarnessTests"`
Expected: the `Of_*_Throws` and `Current_*` cases fail; the `NotChecked`/`Writes` cases pass.

- [ ] **Step 3: Implement the check** as a private `CheckParameters(SqlFile, SidecarEntry[])` called by the four-argument `Of`, `HandEdited` writing through a shared private writer that skips it, and `Current` building the parameters from the block it already parses.
  For each entry: for each `SqlDialect` value, parse with `SqlFileParser.Parse(text, SqlPath.GetFileName(path), dialect)`; skip a parse with errors; find the block by `entry.Name` (ordinal); when `SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters) == entry.Hash`, compare and stop.  The message: `$"The entry {entry.Name} of {sqlFile.Path}.json lists the parameters [{actual}], but its query has [{expected}], each as the SQL spells it, in the order of the SQL, numbered from 0.  Fix the entry, or write a sidecar a person edited with Sidecars.HandEdited."`, with each list `string.Join(", ", names)`, and the entry's names suffixed `@<ordinal>` only where an ordinal is off its place.
  Update the class comment and `Current`'s comment to say what the check guarantees and that `Current`'s parameters have no type.

- [ ] **Step 4: Run the harness tests to see them pass** (same command).  Expected: PASS.

- [ ] **Step 5: Run the whole generator folder and fix every test the check fails**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generator"`
The known failures (from a prototype of the check; it stopped at each test's first bad entry, so more may appear) are in the appendix.  Two kinds:
  - **The SQL has a parameter the entry lacks** (most of `ModelTests`, `ModelWarningTests`, `SharedModelTests`, `TableModelTests`, `CachingModelTests`): when the test is not about parameters (it asserts an output model alone, or no input model, or its name says "without parameters"), take the parameter out of that test's SQL; when it is about them, add the parameter to the entry.  A SQL constant shared by tests that do list `id` stays; give the test that does not its own text, or add the parameter, whichever keeps its assertions unchanged.
  - **The entry lists parameters the SQL lacks** (`ModelNameTests`, `QueryTypesTests`, `TypeOverrideLevelTests`, `ModelRunsTests`, and `ModelTests`' `tags`, `GetEqualityContract`, `name, count, note`, `ba, b_x, a` cases): write them into the SQL in the entry's order, as `SELECT @2fa;` or `... WHERE id = @p AND ...`; every name the entries use lexes as a parameter (`@2fa` included, `SqlLexer.IsParameterNameCharacter`).  Keep `-- name: GetUser` on the first line so that diagnostics at the name keep their spans.
  - **Deliberate:** `ParameterTypeTests.Run_ParamMarkerWhoseEntryNamesTheParameterInAnotherCase_GivesItsType` tests an entry spelling the marker's `@author` as `Author`.  Switch it to `Sidecars.HandEdited` and add a comment saying the spelling differs on purpose: a person edited the sidecar, and the generator matches names ignoring case.
  Do not touch `QueryMethodCorpusTests` or its scenarios: it already passes the check.

- [ ] **Step 6: Run the same folder on RoslynFloor**

Run: `dotnet test --project tests/SqlSource.Tests.RoslynFloor`
Expected: PASS.

- [ ] **Step 7: Document the rule**
  - `tests/SqlSource.Tests/Generator/AGENTS.md`: one bullet, "**A hand-built entry lists its query's parameters as the tool writes them**: each as the SQL spells it, in the order of the SQL, numbered from 0.  `Sidecars.Of` fails a current entry that does not; a test of a sidecar a person edited writes it with `Sidecars.HandEdited` and says why in a comment."
  - `tests/README.md`: the `Sidecars` clause becomes "`Sidecars`, which hands the generator a sidecar and fails a test whose current entry does not list its query's parameters as the tool writes them".

- [ ] **Step 8: Format, run the covering tests, commit**

Run: `./format.sh -- <each changed .cs file>` (see `CONTRIBUTING.md`), then the Step 5 and Step 6 commands, then `./pre-commit-validation.sh`.
```bash
git add tests docs/superpowers/plans
git commit -m "test: check that a hand-built entry lists its query's parameters, and fix those that did not"
```

### Task 2: The helpers outside `Sidecars.Of`

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/SidecarFiles.cs` (`EntryFor`)
- Modify: `tests/SqlSource.Tests/Generation/Types/QueryTyperTests.cs` (`InputOf`, `Entry`, `Type_SqlServerNullTypeWithNoMapping_GivesTheHelp`)

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `SidecarFiles.EntryFor(PlannedQuery)` lists `query.Query.Parameters` as `TestSidecars.Parameter(ordinal, parameter.Name, null, parameter.Nullable)`.

- [ ] **Step 1:** `EntryFor` lists the query's parameters as above, and its comment says so.  Run `dotnet test --project tests/SqlSource.Tool.Tests`; expected PASS.  A test that now fails because it relied on a current entry without parameters is reported in the hand-back, not loosened.
- [ ] **Step 2:** `QueryTyperTests.InputOf` reads a `.sql` text whose `GetUser` writes the entry's parameters in order (`-- name: GetUser\nSELECT @p0, @p1;\n`), and `SqlTexts.GetUser` when the entry has none, so that every entry of the class lists exactly its query's parameters.  Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.Types.QueryTyperTests"`; expected PASS with every assertion unchanged.
- [ ] **Step 3:** format with `./format.sh -- <each changed .cs file>`, run both commands again and `./pre-commit-validation.sh`, commit:
```bash
git add tests
git commit -m "test: list the query's parameters in the tool's current entries and in QueryTyperTests"
```

## Appendix: the prototype's mismatches

Each line is a test, the entry the check stopped at, the query's parameters and the entry's.

```text
CachingModelTests.Run_QueryGainsParameters_EmitsOnlyItsInputModel: GetOrder: SQL [id] entry []
ModelNameTests.Run_NoRows_HasNoOutputModel: GetUser: SQL [] entry [id]
ModelNameTests.Run_ParameterName_IsItsBareName: GetUser: SQL [] entry [accountId, user_id]
ModelNameTests.Run_ParameterNotAnIdentifier_IsSqlsrc254WithRename: GetUser: SQL [] entry [2fa]
ModelNameTests.Run_ParameterNullability_IsTrueOnly: GetUser: SQL [] entry [p, q, r]
ModelNameTests.Run_PropertyDocumentation_NamesTypeOriginAndFlags: GetUser: SQL [] entry [id]
ModelNameTests.Run_Property_HasItsTypeAndSubject: GetUser: SQL [] entry [user_id]
ModelNameTests.Run_QueryWithSeveralErrors_ReportsEach: GetUser: SQL [] entry [2fa]
ModelNameTests.Run_ReservedParameterName_IsSqlsrc256WithRename: GetUser: SQL [] entry [to_string]
ModelNameTests.Run_TwoParametersOneName_IsSqlsrc255: GetUser: SQL [] entry [a_b, aB]
ModelRunsTests.ModelFiles_Run_AreTheModelsHintNames: GetUser: SQL [] entry [id]
ModelTests.Run_ClassOrInputModelWithAnArray_IsUnchanged: GetUser: SQL [id] entry [tags]
ModelTests.Run_DeclaredNamesNotReserved_CompileInEachModel: GetUser: SQL [id] entry [GetEqualityContract, OpEquality, OpInequality]
ModelTests.Run_DocumentationWithALineTerminator_StaysInTheComment: GetUser: SQL [id] entry []
ModelTests.Run_EachInputModelType_WritesItsShape: GetUser: SQL [id] entry [name, count, note]
ModelTests.Run_EachModelType_WritesItsShape: GetUser: SQL [id] entry []
ModelTests.Run_EmptyOutputModel_IsAnEmptyClass: GetUser: SQL [id] entry []
ModelTests.Run_EmptyOutputModel_IsAnEmptyRecord: GetUser: SQL [id] entry []
ModelTests.Run_FileExcludedBySqlsrc008_GetsNoModelsForThatType: GetUser: SQL [id] entry []
ModelTests.Run_NoRowsWithoutParameters_EmitsNoModel: GetUser: SQL [id] entry []
ModelTests.Run_NonSealedRecordWithAnArrayColumn_HasAPrivateConstructorWithoutParameters: GetUser: SQL [id] entry []
ModelTests.Run_RecordWithADomainOverAnArray_HasTheConstructor: GetUser: SQL [id] entry []
ModelTests.Run_RecordWithAnArrayColumn_HasAPrivateConstructorWithoutParameters: GetUser: SQL [id] entry []
ModelTests.Run_RecordWithAnArrayColumn_KeepsItsPositionalConstructorEqualityAndWith: GetUser: SQL [id] entry []
ModelTests.Run_RecordWithOnlyAnArrayColumn_Compiles: GetUser: SQL [id] entry []
ModelTests.Run_RecordWithoutAnArrayColumn_HasNoConstructor: GetUser: SQL [id] entry []
ModelTests.Run_RowsWithoutParameters_EmitsTheOutputModelAlone: GetUser: SQL [id] entry []
ModelTests.Run_SortInput_SortsTheInputModelAlone: GetUser: SQL [id] entry [ba, b_x, a]
ModelTests.Run_SortOutput_SortsTheOutputModelAlone: GetUser: SQL [id] entry [ba, b_x, a]
ModelTests.Run_TypeInANamespaceWithAKeyword_WritesItAsTheTypeDoes: GetUser: SQL [id] entry []
ModelTests.Run_TypeInTheGlobalNamespace_GivesModelsInIt: GetUser: SQL [id] entry []
ModelTests.Run_TypeWithADiagnostic_GetsNoModels: GetUser: SQL [id] entry []
ModelTests.Run_TypesAccessibility_IsTheModels: GetUser: SQL [id] entry []
ModelWarningTests.Run_ExperimentalTypeBesideAnObsoleteOne_DisablesEachIdOnceInOrder: GetUser: SQL [id] entry []
ModelWarningTests.Run_GenericTypeWithABrokenConstraint_IsSqlsrc252AndHasNoModel: GetUser: SQL [id] entry []
ModelWarningTests.Run_GlobalClassSortedWithAnObsoleteType_CompilesWithoutAWarning: GetUser: SQL [id] entry []
ModelWarningTests.Run_ObsoleteTypeWithADiagnosticId_DisablesThatId: GetUser: SQL [id] entry []
ModelWarningTests.Run_TypeObsoleteAsAnError_IsSqlsrc252AndHasNoModel: GetUser: SQL [id] entry []
ModelWarningTests.Run_TypeWithAMalformedAttribute_Compiles: GetUser: SQL [id] entry []
ModelWarningTests.Run_TypesWhoseIdsAreKeywords_DisablesThoseIds: GetUser: SQL [id] entry []
ParameterTypeTests.Run_ParamMarkerWhoseEntryNamesTheParameterInAnotherCase_GivesItsType: ListPosts: SQL [author] entry [Author]
QueryTypesTests.Run_Int4_IsIntUnderEachDriver: GetUser: SQL [] entry [id]
QueryTypesTests.Run_ParameterWithoutAType_IsSqlsrc246: GetUser: SQL [] entry [id]
QueryTypesTests.Run_QueryWithTwoFailures_ReportsTheFirstAndLeavesTheOtherQueryTyped: GetUser: SQL [] entry [relation]
QueryTypesTests.Run_TwoQueries_KeepTheOrderOfTheFile: DeleteUser: SQL [] entry [id]
SharedModelTests.Run_GenericAndPlainTypeOfOneName_AreOrderedByArity: GetUser: SQL [id] entry []
SharedModelTests.Run_InternalMappedTypeInAPublicModel_IsSqlsrc251AtTheQuery: GetUser: SQL [id] entry []
SharedModelTests.Run_InternalMappedTypeInAnInternalModel_Compiles: GetUser: SQL [id] entry []
SharedModelTests.Run_ModelNamedAsATypeOfTheProject_IsSqlsrc258: GetUser: SQL [id] entry []
SharedModelTests.Run_ModelNamespaceThatIsATypeOfTheProject_IsSqlsrc258: GetUser: SQL [id] entry []
SharedModelTests.Run_PublicAndInternalTypeSharing_GiveAPublicModel: GetUser: SQL [id] entry []
SharedModelTests.Run_TwoGenericTypesOfOneName_NameTheEarlierWithItsTypeParameters: GetUser: SQL [id] entry []
SharedModelTests.Run_TwoTypesOfOneNamespace_ShareOneModel: GetUser: SQL [id] entry []
SharedModelTests.Run_TwoTypesOfTwoNamespaces_HaveAModelEach: GetUser: SQL [id] entry []
SidecarHarnessTests.Current_FileWithADialectMarker_UsesTheMarkersDialect: Q: SQL [id] entry []
SidecarHarnessTests.Current_File_ReadsBackWithAnEntryOfEachQuery: Q: SQL [id] entry []
TableModelTests.Run_NoTableModelsAtEachLevel_NamesTheModelAfterTheQuery: GetUser: SQL [id] entry []
TableModelTests.Run_OtherParameters_LeaveTableModelsOn: GetUser: SQL [id] entry []
TableModelTests.Run_OutputSuffix_FollowsTheTablesName: GetUser: SQL [id] entry []
TableModelTests.Run_TableOfAnotherSchema_HasTheSchemaBeforeItsName: GetUser: SQL [id] entry []
TableModelTests.Run_TableThatGivesNoIdentifier_NamesTheModelAfterTheQuery: GetUser: SQL [id] entry []
TableModelTests.Run_TwoNamespacesThatMatchOneTable_HaveAModelEachUnlessTheyShareANamespace: GetUser: SQL [id] entry []
TypeOverrideLevelTests.Run_ArraysOwnKeyOnAParameter_NamesTheParameter: GetUser: SQL [] entry [statuses]
TypeOverrideLevelTests.Run_MappedTypeNotFoundForAParameter_NamesTheParameter: GetUser: SQL [] entry [id]
```
