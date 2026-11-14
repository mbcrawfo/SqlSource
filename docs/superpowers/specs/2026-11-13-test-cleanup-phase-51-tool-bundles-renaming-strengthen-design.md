# Test cleanup phase 51: tests/SqlSource.Tool.Tests/Bundles/Renaming, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Bundles/Renaming`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (tool-bundles-renaming)

## Files

- `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresDerivationsTests.cs`
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresPlanRenamerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-02

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresDerivationsTests.cs` | `PostgresDerivationsTests.Derive_PrepareErrorBeforeTheList_IsKept` | rows: all
- action: strengthen
- evidence: measured (kills 19, alone 3; survivors under it alone: src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresDerivations.cs:124:20 Equality mutation -> `position > 1` and 124:20 Conditional (true) mutation; also 122:42 Equality mutation -> `position < opening`, under 5 methods).  Read: the test keeps position 3 alone (PostgresDerivationsTests.cs:149), so a mutant that refuses position 1, the first code point of `PREPARE sqlsource_1 (` and as much before the list as 3, survives a test whose name promises that a position before the list is kept; the `(` at 21, the opening's last code point, is the other edge of "before the list" (PostgresDerivations.cs:122).  blind: no
- cover: none
- proposal: Make it a theory over positions 1, 3 and 21 (the opening's length, through `ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(...))`), each kept as it is with no refusal, which kills the `position > 1` survivor and should kill `position < opening`.
- verdict: confirmed
- verifier's reason: `kills` gives 19 kills, 3 alone, and survivors `PostgresDerivations.cs:124:20 position > 1`, `124:20 (true?position :null)` and `122:42 position < opening`.  The test asks position 3 alone (PostgresDerivationsTests.cs:149-157); `position > 1` keeps 3, so it survives, and position 1 would be refused under it.  `ParameterPlaceholders.Opening` is `$"PREPARE {statement} ("` (ParameterPlaceholders.cs:163), 21 code points for `sqlsource_1`, so 21 is the other edge of `position <= opening` (PostgresDerivations.cs:121-125).  The proposal claims only to kill `position > 1` and hedges `position < opening`; neither claim is wrong.  No comment marks the test as a record.
- survivors to kill: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresDerivations.cs:124:20`

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-03

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresPlanRenamerTests.cs` | `PostgresPlanRenamerTests.WordsOf_KeyNotRead_IsGivenWhole` | rows: all
- action: strengthen
- evidence: measured (kills 18, alone 6; survivor under it alone: src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Words.cs:55:21 Statement mutation -> `;`, which drops the strings of a key beside `Plan` at the plan's root).  Read: the root's unread key in the test is `"Extra": 1` (PostgresPlanRenamerTests.cs:772), a number, so the test cannot see whether a root-level unread key's strings reach the token check; the name and docs/architecture/bundles.md ("gives every string of a key it does not read whole, the key too") promise that they do, and a string left out there is user text the check never sees.  blind: no
- cover: none
- proposal: Give the root's unread key a string, `"Extra": "other"`, and expect `["Future Key", "secret", "Extra", "other"]`.
- verdict: confirmed
- verifier's reason: The root's unread key is `"Extra": 1` (PostgresPlanRenamerTests.cs:772), a number, for which `AllStrings` adds nothing; removing `AllStrings(member.Value, words)` at PostgresPlanRenamer.Words.cs:55 leaves `["Future Key", "secret", "Extra"]` unchanged, and `kills` lists exactly that mutant (55:21 Statement) as the one survivor under the test, covered by no other test (evidence line 443).  docs/architecture/bundles.md:283 promises every string of a key it does not read is given whole.  A string value at the root makes the mutant fail.
- survivors to kill: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Words.cs:55:21`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-bundles-renaming`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-51/tool-bundles-renaming/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-51/tool-bundles-renaming/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-51/tool-bundles-renaming/before/reports/mutation-report.json artifacts/test-cleanup/phase-51/tool-bundles-renaming/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-51/tool-bundles-renaming/before/reports/mutation-report.json PostgresDerivationsTests.Derive_PrepareErrorBeforeTheList_IsKept
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-51/tool-bundles-renaming/before/reports/mutation-report.json PostgresPlanRenamerTests.WordsOf_KeyNotRead_IsGivenWhole
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Bundles.Renaming-1-02: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresDerivations.cs:124:20`
- F-SqlSource.Tool.Tests.Bundles.Renaming-1-03: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Words.cs:55:21`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-bundles-renaming`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-51/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-51/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-51/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-51/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-51/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-51/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests.Bundles.Renaming-1-09 (keep, `PostgresPlanRenamerTests.Register_PlanThatIsNoString_DoesNothing`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
