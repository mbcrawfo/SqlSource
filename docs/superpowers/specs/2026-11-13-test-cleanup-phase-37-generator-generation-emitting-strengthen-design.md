# Test cleanup phase 37: tests/SqlSource.Tests/Generation/Emitting, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Emitting`
- Findings: 3 (3 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 24
- Guard: mutation (generator-generation-emitting)

## Files

- `tests/SqlSource.Tests/Generation/Emitting/HintNameTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/SqlServerParameterBinderTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Emitting-1-01

- test: `tests/SqlSource.Tests/Generation/Emitting/HintNameTests.cs` | `HintNameTests.MakeUnique_AmbiguousName_GainsAHashOfItsExactSpelling` | rows: all
- action: strengthen
- evidence: measured (kills 6, alone 4, timeouts under it 0; survivor src/SqlSource/Generation/Emitting/HintName.cs:96:17 Linq method mutation (Any() to All()), reached only by this test and `MakeUnique_NameThatIsNotAmbiguous_IsKept`) and read (HintNameTests.cs:63 gives an ambiguous list of the two spellings of the name under test alone, so `ambiguous.All(...)` is true exactly where `Any(...)` is; with the mutant, a project with a second clashing pair leaves an ambiguous name without its hash, which is the clash the name promises to undo); blind: no
- cover: none
- proposal: Add a second clashing pair to the list, `TestSidecars.Of("App.Other.g.cs", "App.other.g.cs", "App.Sample.g.cs", "App.sample.g.cs")`; the expected names do not change, since each hash is of the name alone.
- verdict: confirmed
- verifier's reason: HintNameTests.cs:63 gives `ambiguous` the two spellings of the name under test and nothing else, so for `App.Sample.g.cs` and `App.sample.g.cs` `ambiguous.All(...)` equals `ambiguous.Any(...)` (HintName.cs:96), and for `App.Other.g.cs` in `MakeUnique_NameThatIsNotAmbiguous_IsKept` both are false.  `kills` on the generator-generation-emitting report lists `SURVIVED src/SqlSource/Generation/Emitting/HintName.cs:96:17 Linq method mutation (Any() to All())` under the test.  With a second pair in the list, `All` is false for `App.Sample.g.cs`, the name is returned without its hash and line :66 fails; the expected hashes do not change, since `Hash` (HintName.cs:109-118) reads only the name.
- survivors to kill: `src/SqlSource/Generation/Emitting/HintName.cs:96:17`

### F-SqlSource.Tests.Generation.Emitting-1-02

- test: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs` | `PostgresParameterBinderTests.Append_DotInTheName_BindsItsBindName` | rows: all
- action: strengthen
- evidence: measured (2 rows; kills 7, alone 2, timeouts under it 0; survivors src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:9 Equality mutation (`type.Schema.IndexOf('.') > 0`) and src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:42 Equality mutation (`type.InternalName.IndexOf('.') > 0`)) and read (the comment at PostgresParameterBinderTests.cs:96 promises a dot anywhere in the schema or the name, but the rows `("my.schema", "t")` at :98 and `("public", "a.b")` at :99 put it at index 2 and 1, so a canonical name built from a schema or a name that starts with a dot passes); blind: no
- cover: none
- proposal: Add the rows `[InlineData(".schema", "t")]` and `[InlineData("public", ".b")]`, whose dot is the first character.
- verdict: confirmed
- verifier's reason: `CanonicalName` (PostgresParameterBinder.cs:143-146) tests `IndexOf('.') >= 0`; the rows at PostgresParameterBinderTests.cs:98-99 put the dot at index 2 (`my.schema`) and 1 (`a.b`), so the `> 0` mutants at 144:9 and 144:42 pass both, and `kills` lists both as SURVIVED under the test.  A row whose dot is the first character (`.schema`, `.b`) gives `IndexOf` 0, the mutant then writes `.schema.t` in place of the bind name `x.y`, and the row fails.  The comment at :96 promises any dot in the schema or the name.
- survivors to kill: `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:42`, `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:9`

### F-SqlSource.Tests.Generation.Emitting-1-03

- test: `tests/SqlSource.Tests/Generation/Emitting/SqlServerParameterBinderTests.cs` | `SqlServerParameterBinderTests.Append_TypeWithoutABinding_BindsByValue` | rows: all
- action: strengthen
- evidence: measured (5 rows; kills 32, alone 4, timeouts under it 1; survivors src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:213:16 Equality mutation (`end <= name.Length`) and src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:218:16 Equality mutation (`end <= name.Length`), each reached by this test and the three vector tests) and read (the comment at SqlServerParameterBinderTests.cs:494-495 promises that a vector whose dimension cannot be read is bound by value, but the vector rows at :498-499, `vector` and `vector(x)`, never reach the end of the name inside the bracket; `vector(3`, unclosed, would crash under the first mutant and be bound as a vector of 3 under the second); blind: no
- cover: none
- proposal: Add the row `[InlineData("vector(3", 20, 0, 0)]`, a dimension whose bracket is never closed, which the binding must send by value.
- verdict: confirmed
- verifier's reason: `TryReadDimension` (SqlServerParameterBinder.cs:203-226) on `vector(3`: `BaseName` cuts at the `(` (SqlServerTypeMap.cs:128-132), so the `vector` row is found; the loop at :213 stops at `end == name.Length` and :218 returns false, so the binding is by value.  Under `end <= name.Length` at :213 the loop reads `name[8]` and throws; under the same mutant at :218 it parses `3` and binds a vector.  The rows at SqlServerParameterBinderTests.cs:497-501 never let the loop reach the end of the name (`vector` has no bracket, `vector(x)` closes it), and `kills` lists both 213:16 and 218:16 `end <= name.Length` as SURVIVED under the test.  The comment at :494-495 promises a vector whose dimension cannot be read.
- survivors to kill: `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:213:16`, `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:218:16`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-emitting`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-37/generator-generation-emitting/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-37/generator-generation-emitting/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-37/generator-generation-emitting/before/reports/mutation-report.json artifacts/test-cleanup/phase-37/generator-generation-emitting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-37/generator-generation-emitting/before/reports/mutation-report.json HintNameTests.MakeUnique_AmbiguousName_GainsAHashOfItsExactSpelling
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-37/generator-generation-emitting/before/reports/mutation-report.json PostgresParameterBinderTests.Append_DotInTheName_BindsItsBindName
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-37/generator-generation-emitting/before/reports/mutation-report.json SqlServerParameterBinderTests.Append_TypeWithoutABinding_BindsByValue
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation.Emitting-1-01: `src/SqlSource/Generation/Emitting/HintName.cs:96:17`
- F-SqlSource.Tests.Generation.Emitting-1-02: `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:42`, `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:144:9`
- F-SqlSource.Tests.Generation.Emitting-1-03: `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:213:16`, `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:218:16`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-emitting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-37/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-37/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-37/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-37/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-37/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-37/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Generation.Emitting-1-12 (keep, `PostgresParameterBinderTests.Append_NoBindName_SetsNoDataTypeName`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation.Emitting-1-13 (keep, `SqlServerParameterBinderTests.Append_EachBaseType_SetsItsSqlDbType`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
