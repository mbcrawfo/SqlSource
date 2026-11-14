# Test cleanup phase 62: tests/SqlSource.Tool.Tests/Reporting, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Reporting`
- Findings: 3 (3 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 24
- Guard: mutation (tool-reporting)

## Files

- `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-4-01

- test: `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs` | `NearNameTests.IsNear_NameWithinTheDistance_IsTrue` | rows: all
- action: strengthen
- evidence: measured (6 rows, kills 35, alone 3, rows needed 4; survivors under it: src/SqlSource.Tool/Reporting/NearName.cs:23:13 Equality mutation `>= MaxDistance`, src/SqlSource.Tool/Reporting/NearName.cs:46:21 Equality mutation `i < 1`, src/SqlSource.Tool/Reporting/NearName.cs:47:24 Equality mutation `j < 1`); read (no row has lengths that differ by exactly 2, so a gate that turns away a difference of 2 passes; the one transposition rows, `("--conncetion", "--connection")` at :14 and `("ba", "ab")` at :15, stay within 2 edits when a swap costs two, so the comment at :13, "One transposition of neighbours counts as one edit", is not proved: the 46:21 and 47:24 mutants switch the swap off and every row still passes); blind: no
- cover: none
- proposal: Add a row whose lengths differ by 2, as `("--for", "--force")`, and a row of two transpositions, as `("--cnonectoin", "--connection")`, which is near only when each swap counts one edit.
- verdict: confirmed
- verifier's reason: The rows at tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs:10-17 have lengths that differ by 0 or 1 ("--conection" 11 against "--connection" 12; the rest are equal), so the gate at src/SqlSource.Tool/Reporting/NearName.cs:23 mutated to `>= MaxDistance` turns none of them away.  `kills` on tool-reporting shows it `SURVIVED`, with 46:21 `i < 1` and 47:24 `j < 1`.  Those two switch off the swap at :45-53, and both swap rows still pass on plain edit distance: "conncetion"/"connection" and "ba"/"ab" are each 2 edits without the swap, so the comment at :13 is not proved.  The proposed rows hold up.  ("--for", "--force") differs in length by 2 and is 2 edits away, so it fails under `>=`.  ("--cnonectoin", "--connection") is 2 swaps apart but 4 plain edits apart, so it fails when a swap does not count as one edit.  The kills I read can be explained: 61:16 `<` fails the distance-2 row at :17; 52:66 `j + 2` indexes past the row; 52:32 Max raises the cost of the repeated "--" and "nn".  No row has a timeout.
- survivors to kill: `src/SqlSource.Tool/Reporting/NearName.cs:23:13`, `src/SqlSource.Tool/Reporting/NearName.cs:46:21`, `src/SqlSource.Tool/Reporting/NearName.cs:47:24`

### F-SqlSource.Tool.Tests-4-02

- test: `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs` | `NearNameTests.IsNear_NameBeyondTheDistance_IsFalse` | rows: all
- action: strengthen
- evidence: measured (5 rows, kills 29, alone 1, rows needed 2; survivors under it: src/SqlSource.Tool/Reporting/NearName.cs:33:25 Equality mutation `j > known.Length`, src/SqlSource.Tool/Reporting/NearName.cs:33:25 Equality mutation `j < known.Length`, src/SqlSource.Tool/Reporting/NearName.cs:34:9 Block removal mutation, src/SqlSource.Tool/Reporting/NearName.cs:38:25 Equality mutation `i < given.Length`); read (each mutant makes some far name near: the first three leave the first row of distances at 0, which caps the distance at the given name's length, and the fourth drops the given name's last character; the rows at :22-27 are all long names or names the length gate turns away, so none reaches either case); blind: no
- cover: none
- proposal: Add a short given name within the length gate that is more than 2 edits away, as `("-x", "-abc")`, and one that is 3 edits away only by its last character, as `("--forxxx", "--force")`.
- verdict: confirmed
- verifier's reason: `kills` on tool-reporting shows four mutants `SURVIVED` under the test: 33:25 `j > known.Length`, 33:25 `j < known.Length`, 34:9 block removal and 38:25 `i < given.Length`.  Under the first three, `previous[known.Length]` of the first row stays 0 (src/SqlSource.Tool/Reporting/NearName.cs:30-36).  That makes a prefix of the known name free, so the distance is at most the length of the given name.  The fourth reads the row before the given name's last character.  Two of the rows at tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs:22-27 are far by edits ("-pS3cret"/"--project" and "--xxxce"/"--force").  The free prefix does not bring either within 2 edits, and neither is far only by its last character.  The other three rows return at the length gate, :23.  ("-x", "-abc") passes the gate and is 3 edits away, but under the zero first row it is within 2.  ("--forxxx", "--force") is 3 edits away, but 2 without its last character.  Both rows would fail the mutants they target.
- survivors to kill: `src/SqlSource.Tool/Reporting/NearName.cs:33:25`, `src/SqlSource.Tool/Reporting/NearName.cs:34:9`, `src/SqlSource.Tool/Reporting/NearName.cs:38:25`

### F-SqlSource.Tool.Tests-4-03

- test: `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs` | `NearNameTests.IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored` | rows: all
- action: strengthen
- evidence: measured (kills 1, alone 0; cover: `NearNameTests.IsNear_NameBeyondTheDistance_IsFalse`); read (`"Host"` and `"postgres"` at :40 differ in length by 4, so `NearName.IsNear` returns at the length gate, src/SqlSource.Tool/Reporting/NearName.cs:23, before any character is compared with case ignored; the test passes whatever the ignore-case comparison does, which `Describing/Connections.cs:149` and `Describing/RunSummary.cs:135` rely on); blind: no
- cover: none
- proposal: Use two names whose lengths differ by at most 2 and that stay more than 2 edits apart with case ignored, as `("Host", "user")`, so the comparison runs.
- verdict: confirmed
- verifier's reason: "Host" (4) and "postgres" (8) at tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs:40 differ by 4 in length, so `IsNear` returns false at src/SqlSource.Tool/Reporting/NearName.cs:23 before `Same` runs.  `kills` on tool-reporting gives the test one kill, 25:20 `true` (the gate's own return), and 23:13 `>=` `SURVIVED`, so the case-ignoring comparison at :65 is never reached.  The callers that ignore case are src/SqlSource.Tool/Describing/RunSummary.cs:135 and src/SqlSource.Tool/Describing/Connections.cs:149, as the finding says.  ("Host", "user") is 4 edits apart with case ignored, and under a `Same` that is always true with ignoreCase it is 0 edits apart, so it would fail.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-reporting`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-62/tool-reporting/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Reporting.' 'Reporting/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-62/tool-reporting/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Reporting.' 'Reporting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-62/tool-reporting/before/reports/mutation-report.json artifacts/test-cleanup/phase-62/tool-reporting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-62/tool-reporting/before/reports/mutation-report.json NearNameTests.IsNear_NameWithinTheDistance_IsTrue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-62/tool-reporting/before/reports/mutation-report.json NearNameTests.IsNear_NameBeyondTheDistance_IsFalse
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-62/tool-reporting/before/reports/mutation-report.json NearNameTests.IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests-4-01: `src/SqlSource.Tool/Reporting/NearName.cs:23:13`, `src/SqlSource.Tool/Reporting/NearName.cs:46:21`, `src/SqlSource.Tool/Reporting/NearName.cs:47:24`
- F-SqlSource.Tool.Tests-4-02: `src/SqlSource.Tool/Reporting/NearName.cs:33:25`, `src/SqlSource.Tool/Reporting/NearName.cs:34:9`, `src/SqlSource.Tool/Reporting/NearName.cs:38:25`
- F-SqlSource.Tool.Tests-4-03: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-reporting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-62/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-62/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-62/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-62/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-62/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-62/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
