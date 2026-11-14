# Test cleanup phase 29: tests/SqlSource.Tool.Tests/Bundles, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Bundles`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 30
- Guard: mutation (tool-bundles)

## Files

- `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs`
- `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Bundles-1-01

- test: `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs` | `BundleErrorsTests.CannotBeRead_ReasonWithAControlCharacter_EscapesItAndKeepsTheTab`; `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs` | `BundleErrorsTests.CallMissing_MethodWithAControlCharacter_EscapesItAndKeepsTheTab`; `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs` | `BundleErrorsTests.CallMissing_ToolVersionWithAControlCharacter_EscapesItAndKeepsTheTab`; `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs` | `BundleErrorsTests.NoDescriber_EngineWithAControlCharacter_EscapesItAndKeepsTheTab` | rows: the rows of their shared `Controls` `{ "\u0007", "\\u0007" }` (line 16), `{ "\u009b", "\\u009B" }` (line 17), `{ "\u007f", "\\u007F" }` (line 18) and `{ "\n", "\\u000A" }` (line 19)
- action: fold
- evidence: measured (each of the four: 5 rows, rows needed 1, alone 0; kills 14, 20, 19 and 14) and read: each `BundleErrors` method passes its argument through `BundleText.Of` (`src/SqlSource.Tool/Bundles/BundleErrors.cs:23`, `:50-51`, `:148`), so what these tests own is that the argument goes through it; the row `{ "\u001b", "\\u001B" }` (line 15) proves that alone, since neither no escaping nor `OneLine.Of` would write ESC as `\u001B`.  How each kind of control character is escaped is `BundleText`'s, and `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` has every one of the four rows dropped here among its eight.  The comment over `Controls` (lines 10-11) says only what the rows are; no `AGENTS.md`, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names `BundleErrorsTests` or these tests (only plans in `docs/superpowers/plans/` name the class).  blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs` | `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`
- proposal: Cut `BundleErrorsTests.Controls` to its ESC row, or make the four theories facts with ESC, so each still proves its argument is escaped and keeps the tab, and leave the kinds of control character to `BundleTextTests`.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs:10-78` and `BundleTextTests.cs:11-27`.  Each of the four theories calls one `BundleErrors` method whose argument goes through `BundleText.Of` (`src/SqlSource.Tool/Bundles/BundleErrors.cs:23`, `:50-51`, `:148`).  ESC (line 15) alone tells `BundleText.Of` from no escaping and from `OneLine.Of` (`src/SqlSource.Tool/Reporting/OneLine.cs:15-18`, which only turns a line break into a space).  BEL, CSI `\u009b`, DEL and `\n` (lines 16-19) are each a row of `BundleTextTests.Controls` (`BundleTextTests.cs:15,18,17,20`), and `Of_ControlCharacter_IsWrittenAsItsEscape` asserts their escapes.  The `kills` lists in `mutation/tool-bundles` give 14, 20, 19 and 14 kills, all in `BundleText.cs` (24, 25, 29, 30, 33, 34, 43, 46, 54), plus `BundleErrors.cs:63` for the two `CallMissing` theories, which is `MadeBy` and stays under the ESC row.  Every kill is one a single ESC row would also make: the texts are short enough for the `text.Length - 10` capacity mutant, and the tab is beside the control character.  There are no timeouts and no survivors.  The comment at lines 10-11 only describes the rows.  `grep` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds no mention of `BundleErrorsTests` or of these methods; only `docs/superpowers/plans/2026-11-08-td-0109-bundle-tests.md:132` names the file, as one to edit.  `docs/architecture/bundles.md:29,73` describes `BundleText`'s rule and names no test as its record.

### F-SqlSource.Tool.Tests.Bundles-1-02

- test: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs` | `BundleTextTests.Of_ControlCharacterBesideATab_KeepsTheTab` | rows: every row of `Controls` but `{ "\u001b", "\\u001B" }`: lines 15-21
- action: fold
- evidence: measured (8 rows, rows needed 1, kills 15, alone 0) and read: whether a character is escaped is decided for each character alone, `character != '\t' && char.IsControl(character)` (`src/SqlSource.Tool/Bundles/BundleText.cs:54`), so a tab beside one kind of control character is the same check as beside any other.  What the theory owns is that the tab is kept; that each kind is escaped is proved, row for row on the same `Controls`, by `Of_ControlCharacter_IsWrittenAsItsEscape`.  No comment over the test marks it a record, and no `AGENTS.md`, readme or document of `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names `BundleTextTests`.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs` | `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`
- proposal: Make `Of_ControlCharacterBesideATab_KeepsTheTab` a fact on one control character, ESC, between two tabs.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs:24-32` and `src/SqlSource.Tool/Bundles/BundleText.cs:21-54`.  Each character is escaped or not on its own (`IsEscaped`, line 54), so the kind of control character beside the tab makes no difference to the tab.  A single ESC row between two tabs still runs both paths that copy a tab: the prefix `Append(text, 0, first)` (line 29) for the first tab and the loop (line 35) for the second.  `kills` gives this theory and `Of_ControlCharacter_IsWrittenAsItsEscape` the same 15 mutants of `BundleText.cs`, with rows needed 1, no timeouts and no survivors.  The tab mutants `54:54 Equality` and `54:54 Logical` fail under any row.  The comment at lines 9-10 only describes the rows.  No `AGENTS.md`, readme, `CONTRIBUTING.md` or document of `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names `BundleTextTests`.  The cover asserts the escape of every row that is dropped.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-bundles`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-29/tool-bundles/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.&FullyQualifiedName!~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-29/tool-bundles/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.&FullyQualifiedName!~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json artifacts/test-cleanup/phase-29/tool-bundles/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json BundleErrorsTests.CannotBeRead_ReasonWithAControlCharacter_EscapesItAndKeepsTheTab
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json BundleErrorsTests.CallMissing_MethodWithAControlCharacter_EscapesItAndKeepsTheTab
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json BundleErrorsTests.CallMissing_ToolVersionWithAControlCharacter_EscapesItAndKeepsTheTab
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json BundleErrorsTests.NoDescriber_EngineWithAControlCharacter_EscapesItAndKeepsTheTab
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-29/tool-bundles/before/reports/mutation-report.json BundleTextTests.Of_ControlCharacterBesideATab_KeepsTheTab
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-bundles`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-29/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-29/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-29/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-29/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-29/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-29/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
