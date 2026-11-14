# Test cleanup phase 04: tests/SqlSource.Tests/Generation/Emitting, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Emitting`
- Findings: 6 (6 delete)
- Kind: one kind, delete
- Changed lines: about 74
- Guard: mutation (generator-generation-emitting), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Emitting-1-06

- test: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs` | `RowBufferTests.IsUsedBy_ManyOfIAsyncEnumerable_IsFalse` | rows: all
- action: delete
- evidence: measured (2 rows; kills 2, alone 0, timeouts under it 0; rows needed 1; cover `BatchMethodWriterTests.Append_EachShapeThatReadsRows_ReadsIntoTheBufferOfAManyMethod`) and read (RowBufferTests.cs:55-68 asks `IsUsedBy` of `ListUsers` with `Many`, `IAsyncEnumerable` and each `UseArrayPool`; `IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfAllButTheImmutableLists` at :34-52 asks it of the same `ListUsers` for every shape, collection and flag, these two inputs among them, and expects false since `Pooled` at :22-32 leaves out `IAsyncEnumerable`; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test or `RowBufferTests`, and its comment gives the rule's reason, not a record); blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs` | `RowBufferTests.IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfAllButTheImmutableLists`
- proposal: Delete the theory, and move its one-line reason, that a streaming method has nothing to buffer, onto `Pooled`, which is where the exhaustive test leaves `IAsyncEnumerable` out.
- verdict: confirmed
- verifier's reason: `IsUsedBy_EachShapeCollectionAndFlag_...` (RowBufferTests.cs:34-52) loops over every `ResultShape`, every `CollectionKind` (IAsyncEnumerable among them) and both flags on the same `ListUsers`.  It expects false for `Many`, `IAsyncEnumerable`, true or false, since `Pooled` (:22-32) leaves out `IAsyncEnumerable`.  Those are the deleted theory's two inputs (:58-68) and its assertion.  `kills` gives the test 2 kills, RowBuffer.cs:136:9 and :141:12, both also killed by the exhaustive test.  The comment at :54 gives the rule's reason and is no record.  `grep -rn` over the `AGENTS.md` files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds neither `IsUsedBy_ManyOfIAsyncEnumerable` nor `RowBufferTests`.

### F-SqlSource.Tests.Generation.Emitting-1-07

- test: `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs` | `ParameterBinderTests.Append_ArrayConvertedByElement_SendsTheConvertedArray` | rows: all
- action: delete
- evidence: measured (3 rows; kills 36, alone 0, timeouts under it 0; rows needed 2; cover `PostgresParameterBinderTests.Append_ArrayConvertedByElement_BindsTheDatabaseArray`, `ParameterBinderTests.Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull`) and read (ParameterBinderTests.cs:64-86 holds the same three `Array.ConvertAll` texts, row for row, as PostgresParameterBinderTests.cs:188-213; both binders write them through the shared `ParameterBinder.AppendConverted` and `AppendByElement`, PostgreSQL's from `PostgresParameterBinder.cs:179`, and what SQL Server adds, the `(object)` cast and the `DBNull.Value` arm, is the same `AppendConverted` call that ParameterBinderTests.cs:52-59 asserts; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test or `ParameterBinderTests`); blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs` | `PostgresParameterBinderTests.Append_ArrayConvertedByElement_BindsTheDatabaseArray`; `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs` | `ParameterBinderTests.Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull`
- proposal: Delete the theory; the PostgreSQL theory keeps the element conversion of each row, and the converter theory keeps SQL Server's cast and null.
- verdict: confirmed
- verifier's reason: SQL Server binds an undescribed parameter through `AppendByValue`, then `AppendValueOrNull`, then `AppendConverted` with `(object)` and `DBNull.Value` (ParameterBinder.cs:59-106).  PostgreSQL calls the same `AppendConverted` with `""` and `null` (PostgresParameterBinder.cs:179).  Within `AppendConverted` the cast and the null arm (:137, :148) are written alike for a by-element and a whole conversion, and only :139-146 chooses `AppendByElement`.  The PostgreSQL theory (PostgresParameterBinderTests.cs:187-236) asserts the same three `ConvertAll` texts row for row, and `Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull` (ParameterBinderTests.cs:48-59) asserts SQL Server's `(object)` and `DBNull.Value` arms.  All 36 of the test's kills are explainable (ParameterBinder.cs:76-239, SqlServerParameterBinder.cs:40:13, BindingContext.cs:44:86), each also killed elsewhere, 30 of them by the PostgreSQL theory, with no timeout or survivor under it.  The doc search finds neither name, and the comment at :61-62 states the rule, not a record.

### F-SqlSource.Tests.Generation.Emitting-1-08

- test: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs` | `GeneratedTextTests.IndentOf_ThreeLevels_IsTwelveSpaces` | rows: all
- action: delete
- evidence: measured (covers 4 mutants and kills none) and read (GeneratedTextTests.cs:10 checks the cached indent of level 3 by value; row `15` of `IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel` at :14 checks the cached indent of level 15 by the same rule, four spaces a level, and the cache is built one level from the one before, GeneratedText.cs:43-46, so a wrong level 3 makes a wrong level 15; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test or `GeneratedTextTests`, and it has no comment); blind: yes (the cache is built in a static initialiser, GeneratedText.cs:40-49)
- cover: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs` | `GeneratedTextTests.IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel`
- proposal: Delete the test; the theory's row 15 proves the cached levels' text.
- verdict: confirmed
- verifier's reason: `kills` gives the test 0 kills, 0 timeouts and 0 survivors, and the cache it reads is built in the static initialiser (GeneratedText.cs:39-49, blind).  Each cached level is the one before plus `Indent` (:45), and `IndentOf` reads `Indents[depth]` (:26) for 3 as for 15.  So any change of the table's text, or of the lookup, that breaks level 3 also breaks row `15` of `IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel` (GeneratedTextTests.cs:13-17), unless the table were written by hand with a single wrong entry.  The test has no comment, and the doc search finds neither `IndentOf_ThreeLevels` nor `GeneratedTextTests`.

### F-SqlSource.Tests.Generation.Emitting-1-09

- test: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs` | `GeneratedTextTests.Deeper_IndentNotFromTheCache_AddsOneLevel` | rows: all
- action: delete
- evidence: measured (covers 8 mutants and kills none) and read (GeneratedTextTests.cs:33 passes eight spaces that are not the cached string, so `Deeper` takes the branch `indent + Indent` of GeneratedText.cs:36, whose value equals the cached next level, and no change to the branch's condition can change the result; `Deeper_IndentOfNoWholeLevel_AddsOneLevel` at :38-39 passes an indent that is not the cache's too, takes the same branch, and fails when the cached string is given instead; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test or `GeneratedTextTests`); blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs` | `GeneratedTextTests.Deeper_IndentOfNoWholeLevel_AddsOneLevel`
- proposal: Delete the test; the six-space test proves that an indent not from the cache gets one level after it.
- verdict: confirmed
- verifier's reason: `new string(' ', 8)` is never the cached instance, so `Deeper` (GeneratedText.cs:36) takes `indent + Indent`, and that value equals `Indents[3]`.  The test therefore fails no change to the condition, and `kills` gives it 0 kills, 0 timeouts and 0 survivors.  `Deeper_IndentOfNoWholeLevel_AddsOneLevel` (GeneratedTextTests.cs:35-39) also passes a string the cache did not give.  It kills 35:20, 36:16 Conditional (true) and 36:16 Logical, since there the cached level near its length (8 spaces) differs from the right answer (10).  I could find no change that fails the eight-space test and passes the six-space one, short of special-casing whole levels.  The test has no comment, and the doc search finds neither name.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-04

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallStart_EveryCall_IsTheAwaitTheTargetAndTheMember` | rows: all
- action: delete
- evidence: measured (covers 25 mutants and kills none) and read: it compares `AppendCallStart` with `AppendCallMember(AppendAwait(builder).Append(target))`, which is the method's body word for word (`src/SqlSource/Generation/Emitting/QueryMethods/CallSpelling.cs`, `AppendCallStart`), so no change to a spelling can fail it; what its comment (lines 64-65) claims, that the member alone follows a target already in the builder, is what `AppendCallMember_Call_IsTheMemberAloneOfEachSpelling` asserts with the builder `"t"`, and the start's text of every call is pinned by `AppendCallStart_EachCall_IsTheCallOfEachSpelling`.  No document names the test or its class.  blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallMember_Call_IsTheMemberAloneOfEachSpelling`; `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallStart_EachCall_IsTheCallOfEachSpelling`; `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendAwait_OfEachSpelling_IsAwaitOrNothing`
- proposal: Delete it; it restates the implementation and the three tests named pin each part's text and the member after a target.
- verdict: confirmed
- verifier's reason: `CallSpelling.AppendCallStart` is `AppendCallMember(AppendAwait(builder).Append(target), call)` (`src/SqlSource/Generation/Emitting/QueryMethods/CallSpelling.cs:29-30`).  The test's expected text, CallSpellingTests.cs:73, builds the same expression, so no change to a spelling can fail it.  `kills` gives 0 kills over 25 mutants covered.  The named covers pin each part: the member after a target already in the builder (`"t" + member`, lines 60-61), each call's whole start for both spellings (lines 44-45) and the await (lines 141-142).  No document names the test or its class, and its comment (lines 64-65) states a design, not a record.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-05

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.WholeCall_WithItsToken_IsTheAwaitedCallOrTheCall` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0, cover outside the file) and read: it appends the start of `DriverCall.Read`, the token as the only argument and the end, and each of the three is asserted by itself for both spellings: `"await c.ReadAsync("` / `"c.Read("` at CallSpellingTests.cs:34, `"token"` / empty at lines 105-106, `").ConfigureAwait(false)"` / `")"` at lines 119-120; the three appends only add to one builder, so their concatenation proves nothing more.  No document names the test or its class, and no comment marks it as a record.  blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallStart_EachCall_IsTheCallOfEachSpelling`; `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendToken_AsTheOnlyArgument_IsTheTokenOrNothing`; `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallEnd_OfEachSpelling_ClosesTheCall`
- proposal: Delete it.
- verdict: confirmed
- verifier's reason: The test (CallSpellingTests.cs:124-136) concatenates `AppendCallStart(..., DriverCall.Read)`, `AppendToken(..., afterArgument: false)` and `AppendCallEnd`, three appends to one builder that do not interact.  `kills` gives 5 mutants, and each has a named cover in this file.  `AsyncCallSpelling.cs:37:10` is killed by `AppendToken_AsTheOnlyArgument_IsTheTokenOrNothing`, which is listed.  `AsyncCallSpelling.cs:79:32` and `SyncCallSpelling.cs:54:32` are killed by `AppendCallStart_EachCall_IsTheCallOfEachSpelling`, which is listed.  `AsyncCallSpelling.cs:43:90` (`").ConfigureAwait(false)"`) is the exact text asserted at line 119, and `46:88` (`"await "`) the exact text at lines 141 and 44.  No document names the test or its class, and no comment marks it as a record.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-emitting`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-04/generator-generation-emitting/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-04/generator-generation-emitting/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json artifacts/test-cleanup/phase-04/generator-generation-emitting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json RowBufferTests.IsUsedBy_ManyOfIAsyncEnumerable_IsFalse
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json ParameterBinderTests.Append_ArrayConvertedByElement_SendsTheConvertedArray
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json GeneratedTextTests.IndentOf_ThreeLevels_IsTwelveSpaces
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json GeneratedTextTests.Deeper_IndentNotFromTheCache_AddsOneLevel
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json CallSpellingTests.AppendCallStart_EveryCall_IsTheAwaitTheTargetAndTheMember
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-04/generator-generation-emitting/before/reports/mutation-report.json CallSpellingTests.WholeCall_WithItsToken_IsTheAwaitedCallOrTheCall
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-emitting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-04/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-04/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-04/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-04/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-04/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-04/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation.Emitting-1-08.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generation.Emitting-1-08 | delete | `GeneratedTextTests.IndentOf_ThreeLevels_IsTwelveSpaces` | `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs`: `GeneratedTextTests.IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-06 (keep, `CallSpellingTests.AppendCallStart_EveryCall_HasAnAsynchronousAndASynchronousSpelling`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
