# TD-0038 - Duplicated and uneven patterns in the generator and the tool

## Problem

A maintainability review of `src/` on 2026-10-10 found these.  None changes what a user sees; each makes a change cost more than it should, or lets two places drift apart with nothing to notice.

Items 1 to 6 and 8 of the review are resolved, and so is the tool's part of 10 and 11; the numbers of the others are kept, written out so that a renderer does not count them again, since the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) names them.

- **7. Duplicated text helpers in `Parsing/`.**  `SqlMarkerReader.Trim` and `TokenScanner.Trim` are the same method; `SqlLexeme.GetContentSpan` inlines it a third time.  `SqlLexer` and `SqlTextBuilder` each hold `LineTerminators` and `FindLineEnd`; `SqlLexer.CharAt` and `TokenScanner.CharAt` are the same.
- **9. Methods that do several things.**  `SqlFileParser.Parser.ReadBlock` (about 115 lines: markers, summary, dialect marker, end of block, stripped SQL, token scan, declared variables, parameters, input model, settings, kept SQL), `Cli.RunAsync` (about 120 lines in a nested `try`), `RunPlanner.ReadProject` and `RunPlanner.PlanFile` (about 100 lines each), `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`.  `SqlParameterList.Create` and `DatabaseRuns.DescribeAllAsync` take eight parameters each, several of which always travel together.
- **10. A boolean cascade.**  `SqlMarkerScope.Read(marker, inQuery, inPreamble)`, where both true is a third state.
- **11. Small leftovers.**  `SqlSourceGenerator.ToSortedFiles` and `ToSortedSettings` are one method in two element types.  `SidecarComparer.Json` re-implements the scalar formatting of `SidecarJsonBuilder`.  `SettingValue.IsTypeName` and `SqlDialectMarker.Describe` forward to one other method.

## Why it exists

The code was written in sub-phases, each adding the next reader, record or helper beside the last, and reviewed for correctness in each pull request rather than for the shape of the whole.  Item 7 came from readers that were written to allocate nothing and copied the one way to do that.

## Impact

Low.  Nothing here is wrong today, and the tests hold each pair together where they could drift.  Each item makes a change touch more places than a reader would expect.

## Proposed fix

As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull requests 3 and 4, each held by the existing tests:

- **7.** One allocation-free `SqlText` helper in `Parsing/` for the trim, the line end and the character lookup.
- **9.** Split by the responsibilities named, with a record for the values that travel together.
- **10.** A scope kind of `Preamble`, `Query`, `WholeFile`.
- **11.** Delete, inline or merge each.

## Trigger

Before the next phase that changes `Parsing/`, which adds to 7 and 10, or one of the methods of 9.
