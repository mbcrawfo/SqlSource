# TD-0039 - Duplicated and uneven patterns in the tests

## Problem

A maintainability review of `tests/` on 2026-10-10 found eight of these.  Items 1, 2, 3, 4 and 7, and the sidecar of item 8, are resolved; the numbers of the rest are the review's.  No test is wrong; each copy is one more place a change has to reach.

5. **Test classes that cover several subjects.**  [`SqlFileParserTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs), about 1,590 lines, covers blocks and the preamble, tokens, generator-parameter errors, dialect markers, parameters and declared variables, `token-ignore`, the shape of a name, and the output, database and model markers, with its helpers scattered between tests.  [`GeneratedSourceTests`](../../tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs), about 1,090 lines, mixes layout snapshots, ordering, methods, escaping, the rule that generated code has no warning, and hint-name collisions; the generated file's three-line header is repeated five times there, five in `TypeEmitterTests` and once in `FileDiagnosticsTests`, and the triple `Diagnostics`, `CompilationErrors`, `GeneratedCodeWarnings` `.ShouldBeEmpty()` twenty-seven times there and six in four other classes.  [`DatabaseRunsTests`](../../tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs) mixes connection errors, describer failures, redaction and cancellation, and holds a fact about `Redaction.Of` that belongs in `RedactionTests`.
6. **Theories that dispatch on a string.**  `DescribeRunTests`, `CheckTests`, `ExitCodeTests` and `RunLogTests` each have a `[Theory]` whose rows are kinds such as `"a merge conflict"` or `"a directory"` that a `switch` inside the test turns into a different arrangement.  A failure names a magic string, not a path.
8. **Small duplicates in `tests/SqlSource.Tests`.**  `SpanOf` in `SqlFileParserTests` uses `IndexOf` and the one in `SqlGeneratorParameterScopeTests` uses `LastIndexOf`; `Literal` and `Text` both wrap `new SqlSegment(Literal, ...)`; the eleven-argument `SqlQuery` is built by hand in five places though `TypeEmitterTests.Query` exists.

## Why it exists

Each sub-phase added its tests beside the last's and copied the nearest helper.

## Impact

Low.  A change to the generated file's header reaches eleven snapshots.  5 and 6 make a failure harder to place.

## Proposed fix

The design is `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`, whose pull requests 2 and 3 are what remains.

5. Split `SqlFileParserTests` into one class per subject sharing a `ParseOf` helper; split `GeneratedSourceTests` the same way, with `GeneratorRun.ShouldBeClean()` and a `GeneratedFile.Header` constant in the harness, which stays within Roslyn 4.8.0; split `DatabaseRunsTests` over a `DatabaseRunsScene`, and move the `Redaction.Of` fact.
6. One `[Fact]` per kind, sharing a private assertion.
8. `TestModels.Literal`, `TestModels.Query(...)`, one `TextSpans.Of(text, value, last)`.

## Trigger

The first pull request that changes the generated file's header and has to touch more than two test classes to do it; or before phase 3, whose describer tests will be added to `DatabaseRunsTests`.
