# TD-0038 - Duplicated and uneven patterns in the generator and the tool

## Problem

A maintainability review of `src/` on 2026-10-10 found these.  None changes what a user sees; each makes a change cost more than it should, or lets two places drift apart with nothing to notice.

Items 1 to 4 and 8 of the review are resolved; the numbers of the others are kept, written out so that a renderer does not count them again, since the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) names them.

- **5. Twin one-of records and five shapes of "a value or why not".**  [`DescribeResult`](../../src/SqlSource.Tool/Describing/Describers/DescribeResult.cs) and [`OpenResult`](../../src/SqlSource.Tool/Describing/Describers/OpenResult.cs) are the same record under two names.  [`FileCheck`](../../src/SqlSource.Tool/Describing/FileCheck.cs) repeats the head of `FileOutcome`, from which it is always built.  Across the tool a result that may fail is a `(Projects, Failure?)` pair, a state enum beside two nullables, a `TryX(out reason)` that returns null, a method that reports and returns null, or a `(Value?, Error?)` record, depending on the file.
- **6. `ToolDiagnostic.ForFile` takes the path twice at ten of thirteen call sites.**  [`ToolDiagnostic.ForFile`](../../src/SqlSource.Tool/Reporting/ToolDiagnostic.cs) sets the error's path from its second argument, and most file-scoped descriptors also quote the path as `{0}`, so callers write the same expression as the path and as the first argument.  A caller that passes a different value is right in three places and wrong in the rest, and nothing tells them apart.
- **7. Duplicated text helpers in `Parsing/`.**  `SqlMarkerReader.Trim` and `TokenScanner.Trim` are the same method; `SqlLexeme.GetContentSpan` inlines it a third time.  `SqlLexer` and `SqlTextBuilder` each hold `LineTerminators` and `FindLineEnd`; `SqlLexer.CharAt` and `TokenScanner.CharAt` are the same.
- **9. Methods that do several things.**  `SqlFileParser.Parser.ReadBlock` (about 115 lines: markers, summary, dialect marker, end of block, stripped SQL, token scan, declared variables, parameters, input model, settings, kept SQL), `Cli.RunAsync` (about 120 lines in a nested `try`), `RunPlanner.ReadProject` and `RunPlanner.PlanFile` (about 100 lines each), `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`.  `SqlParameterList.Create` and `DatabaseRuns.DescribeAllAsync` take eight parameters each, several of which always travel together.
- **10. Boolean cascades.**  `RunDecisions.Decide(plan, force, errors, check)` passes `options.Force || options.Check` and then `options.Check` again; `FileOutcomes.Decide(files, runHasFilter, anythingWasReported, read)`; `SqlMarkerScope.Read(marker, inQuery, inPreamble)`, where both true is a third state.
- **11. Small leftovers.**  `SqlSourceGenerator.ToSortedFiles` and `ToSortedSettings` are one method in two element types.  `SidecarComparer.Json` re-implements the scalar formatting of `SidecarJsonBuilder`.  `SettingValue.IsTypeName` and `SqlDialectMarker.Describe` forward to one other method.  `Reporter.Trace` and `RunTrace.Databases` are settable properties that `Cli` must assign in order, and nothing enforces the order.  `AttributeReader.ReadText` and `SqlFileText.GetText` read a file the same way.

## Why it exists

The code was written in sub-phases, each adding the next reader, record or helper beside the last, and reviewed for correctness in each pull request rather than for the shape of the whole.  Items 5 and 6 came from each step defining the record it needed; 7 from readers that were written to allocate nothing and copied the one way to do that.

## Impact

Low.  Nothing here is wrong today, and the tests hold each pair together where they could drift.  Each item makes a change touch more places than a reader would expect, and 6 is the kind that a later phase breaks by adding one case in one of the copies.

## Proposed fix

As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull requests 2 to 4, each held by the existing tests:

- **5.** One `Outcome<T>` record, and one convention for a result that may fail, at least within the tool; `FileCheck` holds its `FileOutcome`.
- **6.** `ForFile` prepends the path to the arguments, and a second method serves the three sites that pass something else.
- **7.** One allocation-free `SqlText` helper in `Parsing/` for the trim, the line end and the character lookup.
- **9.** Split by the responsibilities named, with a record for the values that travel together.
- **10.** A `RunMode` enum of `Describe`, `Force`, `Check` on `DescribeOptions`; a scope kind of `Preamble`, `Query`, `WholeFile`.
- **11.** Delete, inline or merge each; construct `Reporter` with its trace, and have `DescribeRun` return its counts.

## Trigger

Before phase 3 adds the first describer, which adds cases to 5 and 6.
