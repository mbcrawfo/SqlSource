# TD-0039 - Duplicated and uneven patterns in the tests

## Problem

A maintainability review of `tests/` on 2026-10-10 found eight of these.  Items 1, 2, 3, 4 and 7, and the sidecar of item 8, are resolved; the numbers of the rest are the review's, except those of item 5, which were counted again on 2026-10-20.  No test is wrong; each copy is one more place a change has to reach.

5. **Test classes that cover several subjects.**  [`SqlFileParserTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs), about 1,700 lines, covers blocks and the preamble, tokens, generator-parameter errors, dialect markers, parameters and declared variables, `token-ignore`, the shape of a name, and the output, database and model markers, with its helpers scattered between tests.  [`GeneratedSourceTests`](../../tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs), about 1,140 lines, mixes layout snapshots, ordering, methods, escaping, the rule that generated code has no warning, and hint-name collisions; the generated file's three-line header is repeated five times there, five in `TypeEmitterTests`, eight in `ModelTests`, four in `ModelWarningTests` and once each in `FileDiagnosticsTests`, `ColumnMarkerTests`, `SharedModelTests` and `TableModelTests`, and the triple `Diagnostics`, `CompilationErrors`, `GeneratedCodeWarnings` `.ShouldBeEmpty()` is written out about thirty-three times in nine classes, besides a private `AssertClean` in each of `ModelTests`, `ModelWarningTests`, `SharedModelTests` and `TableModelTests` (the figures of this item were counted again on 2026-10-20).  [`DatabaseRunsTests`](../../tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs) mixes connection errors, describer failures, redaction and cancellation, and holds a fact about `Redaction.Of` that belongs in `RedactionTests`.
6. **Theories that dispatch on a string.**  `DescribeRunTests`, `CheckTests`, `ExitCodeTests` and `RunLogTests` each have a `[Theory]` whose rows are kinds such as `"a merge conflict"` or `"a directory"` that a `switch` inside the test turns into a different arrangement.  A failure names a magic string, not a path.  The review of 2026-10-19 found that the level theories of `ModelTests`, `TableModelTests` and `TypeOverrideLevelTests` in `Generator/` place a setting at a level named by a string the same way, and two theories of `ModelTests` build their expected text with a `switch` and `Replace`.
8. **Small duplicates in `tests/SqlSource.Tests`.**  `SpanOf` in `SqlFileParserTests` uses `IndexOf` and the one in `SqlGeneratorParameterScopeTests` uses `LastIndexOf`; `Literal` and `Text` both wrap `new SqlSegment(Literal, ...)`; the eleven-argument `SqlQuery` is built by hand in five places though `TypeEmitterTests.Query` exists.

## Why it exists

Each sub-phase added its tests beside the last's and copied the nearest helper.

## Impact

Low.  A change to the generated file's header reaches twenty-six snapshots.  5 and 6 make a failure harder to place.

## Proposed fix

The design is `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`, whose pull requests 2 and 3 are what remains.

5. Split `SqlFileParserTests` into one class per subject sharing a `ParseOf` helper; split `GeneratedSourceTests` the same way, with `GeneratorRun.ShouldBeClean()` and a `GeneratedFile.Header` constant in the harness, which stays within Roslyn 4.8.0; split `DatabaseRunsTests` over a `DatabaseRunsScene`, and move the `Redaction.Of` fact.
6. One `[Fact]` per kind, sharing a private assertion; for the level theories, one helper in `tests/SqlSource.Testing/Generator` that places a setting at a level given as an enum, with the expected shapes in the rows.
8. `TestModels.Literal`, `TestModels.Query(...)`, one `TextSpans.Of(text, value, last)`.

## Trigger

The first pull request that changes the generated file's header and has to touch more than two test classes to do it; or before more describer tests are added to `DatabaseRunsTests`.
