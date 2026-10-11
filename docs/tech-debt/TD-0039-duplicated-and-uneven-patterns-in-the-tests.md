# TD-0039 - Duplicated and uneven patterns in the tests

## Problem

A maintainability review of `tests/` on 2026-10-10 found these.  No test is wrong; each copy is one more place a change has to reach.

1. **The help-link prefix is written in ten places.**  `https://github.com/.../docs/diagnostics.md#sqlsrc` is a constant `See` in [`DescribeScene`](../../tests/SqlSource.Tool.Tests/DescribeScene.cs) and again in `ReporterTests`, `FilterTests`, `DatabaseRunsTests`, `SidecarStoreTests` and `RunProjectsTests`, inline in `DescribeTests` and `CliTests`, and once more each in `SqlDiagnosticsTests` and `ToolDiagnosticsTests` of `tests/SqlSource.Tests`.
2. **`Plans` is re-implemented twice.**  [`RunPlannerTests`](../../tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs) and [`RunPlannerErrorTests`](../../tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs) each define `Plan`, `Type` and a project factory that differ from [`Plans`](../../tests/SqlSource.Tool.Tests/Plans.cs) only in whether a `Queries.cs` is added.  `ConnectionTests` and `DatabaseRunsTests` share a copied `Parse`; `ConnectionTests` and `UsageCheckTests` a byte-identical `ToolRoot`.
3. **The same fixture text in ten classes.**  The `Users.sql` text with `GetUser` and `GetInvoice`, or a `GetUser`/`ListUsers` variant of it, is a `const` in ten test classes, and the edit `Replace("SELECT 2;", "SELECT 22;")` recurs five times.  An empty or newer sidecar is built by hand in six places; `static readonly DateTime Old` in four; `TestClock`'s seed instant is a literal string in seven assertions of `RunLogTests` and `RunLogFileTests`; the secret marker and connection value are declared a dozen times with four values.  Fifty-five `+ ".json"` concatenations stand where `SidecarFormat.PathFor` would.
4. **`FakeDescriber` lacks what the tests build around it.**  Identical `Rows` descriptions in `CheckTests` and `CheckFilterTests`; `new ServerInfo(..., "FakeDriver", null, Empty)` inline seven times; a five-argument `DescribeFailure` six times where `FakeDescriber.Failure` lacks the `step` and `help` parameters.
5. **Test classes that cover several subjects.**  [`SqlFileParserTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs), about 1,570 lines, covers blocks and the preamble, tokens, generator-parameter errors, dialect markers, parameters and declared variables, `token-ignore`, the shape of a name, and the output, database and model markers, with its helpers scattered between tests.  [`GeneratedSourceTests`](../../tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs), about 1,090 lines, mixes layout snapshots, ordering, methods, escaping, the rule that generated code has no warning, and hint-name collisions; the generated file's three-line header is repeated five times there, five in `TypeEmitterTests` and once in `FileDiagnosticsTests`, and the triple `Diagnostics`, `CompilationErrors`, `GeneratedCodeWarnings` `.ShouldBeEmpty()` twenty-seven times.  [`DatabaseRunsTests`](../../tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs) mixes connection errors, describer failures, redaction and cancellation, and holds a fact about `Redaction.Of` that belongs in `RedactionTests`.
6. **Theories that dispatch on a string.**  `DescribeRunTests`, `CheckTests`, `ExitCodeTests` and `RunLogTests` each have a `[Theory]` whose rows are kinds such as `"a merge conflict"` or `"a directory"` that a `switch` inside the test turns into a different arrangement.  A failure names a magic string, not a path.
7. **Two ways to build a `ToolHost`**: `Hosts.Create` and `CliRun.CreateHost`.  `DescribeTests` and `CliTests` make a `CliRun` in each test while the other classes hold one in a field; four tests of `DescribeTests` rebuild by hand the project that `Plans.Postgres` returns.
8. **Small duplicates in `tests/SqlSource.Tests`.**  `SpanOf` in `SqlFileParserTests` uses `IndexOf` and the one in `SqlGeneratorParameterScopeTests` uses `LastIndexOf`; `Literal` and `Text` both wrap `new SqlSegment(Literal, ...)`; the eleven-argument `SqlQuery` is built by hand in five places though `TypeEmitterTests.Query` exists.  `TestSidecar` of the tool's tests hand-builds the entry that `TestSidecars` of the generator's tests builds with defaults, under a name one letter apart.

## Why it exists

Each sub-phase added its tests beside the last's and copied the nearest helper.  The three test projects share only the three harness files that `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests` link by path, so a helper of one project is not reachable from another without the same linking, and none was linked.

## Impact

Low.  A change to a message, a format or a fixture reaches every copy or leaves one stale, and the tests then say the same thing in two voices.  5 and 6 make a failure harder to place.

## Proposed fix

1. One `Expected.See(id)` in `tests/SqlSource.Tool.Tests`, and `SqlDiagnostics.HelpLinkBase` in `tests/SqlSource.Tests`.
2. `Plans.PlanResult(params TestProject[])` and `Plans.Type(name, arguments)`; `ToolRoot` on `Hosts`; one `Parse` helper.
3. A `Sql` constants class, `TestSidecar.WriteEmpty`, `TestSidecar.Old`, `TestClock.StartIso`, a `Secrets` class, and `SidecarFormat.PathFor` in place of the concatenations.
4. `FakeDescriber.Rows`, `FakeDescriber.ServerWith(...)` and a `Failure` overload with `step` and `help`.
5. Split `SqlFileParserTests` into one class per subject sharing a `ParseOf` helper; split `GeneratedSourceTests` the same way, with `GeneratorRun.ShouldBeClean()` and a `GeneratedFile.Header` constant in the harness, which stays within Roslyn 4.8.0; move the `Redaction.Of` fact.
6. One `[Fact]` per kind, sharing a private assertion.
7. `Hosts.Create` everywhere; a `CliRun` field with `Dispose`; `Plans.Postgres`.
8. `TestModels.Literal`, `TestModels.Query(...)`, one `TextSpans.Of(text, value, last)`; link `Snapshot/TestSidecars.cs` into the tool's tests as the harness is linked, or rename `TestSidecar` to say it is about files on disk.

## Trigger

The first pull request that changes a message, a fixture text or the generated file's header and has to touch more than two test classes to do it; or before phase 3, whose describer tests will copy the same helpers again.
