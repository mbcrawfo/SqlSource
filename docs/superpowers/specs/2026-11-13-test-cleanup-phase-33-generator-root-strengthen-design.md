# Test cleanup phase 33: tests/SqlSource.Tests (root files), strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests (root files)`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (generator-root)

## Files

- `tests/SqlSource.Tests/EquatableArrayTests.cs`
- `tests/SqlSource.Tests/TextLinesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests-1-03

- test: `tests/SqlSource.Tests/EquatableArrayTests.cs` | `EquatableArrayTests.Equals_DifferentItems_ReturnsFalse` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/EquatableArray.cs:31:49 Logical mutation -> `obj is EquatableArray<T> other || Equals(other)`, covered only by `Equals_SameItemsInSeparateInstances_ReturnsTrue`).  Read: EquatableArrayTests.cs:24-29 compares different items only through `Equals(EquatableArray<T>)` and `!=`, never through `Equals(object)`, so the mutant, which makes every `EquatableArray<T>` equal as an object, passes; blind: no
- cover: none
- proposal: Add `Of("a", "b").Equals((object)Of("a", "c")).ShouldBeFalse();` so that the object overload is held to false for different items.
- verdict: confirmed
- verifier's reason: `kills` on generator-root shows SURVIVED src/SqlSource/EquatableArray.cs:31:49 Logical mutation -> `obj is EquatableArray<T> other || Equals(other)` under `Equals_SameItemsInSeparateInstances_ReturnsTrue` only.  The mutant compiles because `other` is a struct whose one field is an imported `ImmutableArray<T>`, which the compiler treats as definitely assigned.  It makes `Equals(object)` true for every `EquatableArray<T>`, and stays false for `"a"` (EquatableArrayTests.cs:20), since `Equals(default)` of two items is false.  EquatableArrayTests.cs:24-29 uses only `Equals(EquatableArray<T>)` and `!=`.  The proposed `Of("a", "b").Equals((object)Of("a", "c")).ShouldBeFalse()` fails on the mutant and holds the name's promise for the object overload.
- survivors to kill: `src/SqlSource/EquatableArray.cs:31:49`

### F-SqlSource.Tests-1-04

- test: `tests/SqlSource.Tests/TextLinesTests.cs` | `TextLinesTests.LineSpanOf_ASpanOfText_IsTheSourceTextsLineSpan` | rows: all
- action: strengthen
- evidence: measured (21 rows; kills 23, alone 6, timeouts under it 3; survivor src/SqlSource/TextLines.cs:43:40 Equality mutation -> `end <= text.Length`).  Read: TextLines.cs:42-43 looks past a `\r` for a `\n` only when one more character exists; no row's text ends in a bare `\r` (TextLinesTests.cs:12-32), so the mutant's read of `text[text.Length]` is never reached.  The name promises SourceText's line span for any span.  The other survivor, TextLines.cs:32:21 `Math.Max`, differs only for a span past the text's end and is not a finding; blind: no
- cover: none
- proposal: Add the rows `[InlineData("a\r", 0, 2)]` and `[InlineData("a\r", 2, 0)]`, a text that ends in a bare `\r`, which throw under the mutant and give (0,0)-(1,0) and (1,0)-(1,0) as SourceText does.
- verdict: confirmed
- verifier's reason: `kills` on generator-root shows SURVIVED src/SqlSource/TextLines.cs:43:40 `end <= text.Length` under this theory alone.  That mutant differs only where `end == text.Length` after a `\r`, so only for a text whose last character is a bare `\r`.  No row of TextLinesTests.cs:12-32 has one: `"a\rb"` and `"a\n\r\n\rb\n"` both follow the `\r` with a character.  For `"a\r"`, span (0,2) reaches `text[2]` under the mutant and throws.  SourceText gives (0,0)-(1,0) there, and (1,0)-(1,0) for span (2,0), as the proposal says.  The name promises SourceText's span for any span.  The finding reads TextLines.cs:42-45 rightly.
- survivors to kill: `src/SqlSource/TextLines.cs:43:40`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-33/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-33/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-33/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-33/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-33/generator-root/before/reports/mutation-report.json EquatableArrayTests.Equals_DifferentItems_ReturnsFalse
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-33/generator-root/before/reports/mutation-report.json TextLinesTests.LineSpanOf_ASpanOfText_IsTheSourceTextsLineSpan
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests-1-03: `src/SqlSource/EquatableArray.cs:31:49`
- F-SqlSource.Tests-1-04: `src/SqlSource/TextLines.cs:43:40`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-33/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-33/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-33/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-33/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-33/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-33/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests-1-06 (keep, `EquatableArrayTests.GetHashCode_SameItemsInSeparateInstances_IsTheSame`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
