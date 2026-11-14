# Test cleanup phase 38: tests/SqlSource.Tests/Generation/Methods, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Methods`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (generator-generation-methods)

## Files

- `tests/SqlSource.Tests/Generation/Methods/ListNamesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-01

- test: `tests/SqlSource.Tests/Generation/Methods/ListNamesTests.cs` | `ListNamesTests.PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Methods/ListNames.cs:144:41 Equality mutation `> '9'` to `>= '9'`, covered by 7 methods of ListNamesTests, none killing it); read (ListNames.cs:144 tests each character after the prefix with `is < '0' or > '9'`; every suffix the tests write ends in a digit below 9, `ids_0`, `IDS_3`, `a_5`, `ids__12`, `list1_0`, so a parameter `ids_9` taking the list's names is never seen; the test's name promises that a parameter that is the prefix and digits pushes the prefix on); blind: no (line 144 is also listed blind for other mutants, which do not compile; this one compiled and survived)
- cover: none
- proposal: Write the other parameter as `ids_9` in place of `ids_0` (ListNamesTests.cs:23), keeping `ids__`, so that the upper bound of the digit check is pinned; the lower bound stays pinned by the `_0` suffixes of the other tests.
- verdict: confirmed
- verifier's reason: `kills` in generator-generation-methods lists `SURVIVED src/SqlSource/Generation/Methods/ListNames.cs:144:41 Equality mutation -> >= '9'` under the test.  ListNames.cs:144 `name[index] is < '0' or > '9'`; with `>= '9'` only the digit 9 is read as no digit.  Every suffix that ListNamesTests.cs writes is a digit below 9 (`ids_0` :23, `IDS_3` :31, `ids__12` :48, `a_5` :57, `ids_0` :69, `list1_0` :78), so no test sees it.  The name promises that a parameter named as the prefix and digits pushes the prefix on.  Writing `ids_9` at :23 kills the survivor.  It drops this test's kills of `144:32 <= '0'` and `144:41 < '9'`, but the `_0` suffixes at :48, :69 and :78 still read those bounds.
- survivors to kill: `src/SqlSource/Generation/Methods/ListNames.cs:144:41`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-methods`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-38/generator-generation-methods/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Methods.' 'Generation/Methods/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-38/generator-generation-methods/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Methods.' 'Generation/Methods/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-38/generator-generation-methods/before/reports/mutation-report.json artifacts/test-cleanup/phase-38/generator-generation-methods/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-38/generator-generation-methods/before/reports/mutation-report.json ListNamesTests.PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-2-01: `src/SqlSource/Generation/Methods/ListNames.cs:144:41`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-methods`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-38/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-38/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-38/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-38/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-38/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-38/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Generation-2-02 (keep, `ListNamesTests.PrefixOf_ANameOfAnotherCase_ComparesIgnoringCase`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
