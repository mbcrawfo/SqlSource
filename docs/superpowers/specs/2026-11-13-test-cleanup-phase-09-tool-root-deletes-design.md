# Test cleanup phase 09: tests/SqlSource.Tool.Tests (root files), deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests (root files)`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 16
- Guard: mutation (tool-root), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/TestProjectTests.cs`
- `tests/SqlSource.Tool.Tests/UsageValueTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-1-01

- test: `tests/SqlSource.Tool.Tests/UsageValueTests.cs` | `UsageValueTests.NamedAsPath_PrintableToken_IsTheFullPathInQuotes` | rows: all
- action: delete
- evidence: measured (kills 3, alone 0, timeouts under it 0; cover: `DescribeTests.Run_ProjectOptionOutsideTheSolution_IsSqlsrc207AndExitsWithOne`; `NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes` and `NamedAsPath_FullPathWithALineBreak_IsOnOneLine` each kill 3 with the same cover) and read (UsageValueTests.cs:19-20 gives the token "App" and the path "/work/App" and expects "'/work/App'"; UsageValueTests.cs:31-33 gives the same token "App" and the path "/work/a=b/App" and expects "'/work/a=b/App'", which proves the same thing: a token without = or ; prints its full path in quotes. src/SqlSource.Tool/UsageValue.cs:32-33 has one branch for this, with nothing that depends on the path besides the quoting. No comment above the test. A search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name and `UsageValueTests` finds nothing); blind: no
- cover: `tests/SqlSource.Tool.Tests/UsageValueTests.cs` | `UsageValueTests.NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes`
- proposal: Delete the test. The test of a printable token in a folder with an equals sign makes the same assertion on the same branch, with a path that is also allowed to hold "=".
- verdict: confirmed
- verifier's reason: Read tests/SqlSource.Tool.Tests/UsageValueTests.cs:18-20 and the cover at :30-33: both build `new UsageValue(null, "App", 2)` and assert `NamedAsPath` returns the full path in quotes; the cover's path "/work/a=b/App" also holds "=", so it proves everything the test does plus that the full path's "=" does not matter (src/SqlSource.Tool/UsageValue.cs:25 reads only `Text`, :32-33 has one branch for a printable token, :39 quotes). `kills` in mutation/tool-root gives the test 3 kills, UsageValue.cs:25:39 Equality (`> 0` makes "App" unprintable), :33:9 Conditional (false) and :39:53 String (`$""` drops the quotes), each explicable from its input, 0 timeouts, 0 survivors; the cover kills the same 3. No comment above the test (UsageValueTests.cs:18); the comment at :30 belongs to the cover. A grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt, docs/deferred and site/ for `UsageValueTests` and the test's name finds nothing; src/SqlSource.Tool/AGENTS.md names `UsageValue.NamedAsPath` as the rule, not this test. Nothing mutation cannot see rests on it alone: the quoted text is the same static form the cover asserts.

### F-SqlSource.Tool.Tests-1-02

- test: `tests/SqlSource.Tool.Tests/TestProjectTests.cs` | `TestProjectTests.Manifest_ProjectWithSettingsAndASettingsFileOfAFolder_ListsTheProjectsFileFirst` | rows: all
- action: delete
- evidence: read (this is a test of the test library's `TestProject`, which mutation cannot guard, and is not measured: it was in no scope's run. TestProjectTests.cs:72-80 gives the project a Settings key and a settings file at Queries/sqlsource.json, and expects `SettingsFiles` to be `[project.SettingsPath, nearer]`. TestProjectTests.cs:16-34 gives the project a Settings key and a settings file at the same Queries/sqlsource.json, and line 33 expects `manifest.SettingsFiles.ShouldBe([project.SettingsPath, settings])`: the same order check. tests/SqlSource.Tool.Testing/TestProject.cs:107-134 builds the order when `ManifestText` runs, writing the project's own file before the listed ones, so the order in which the test adds the file and the key does not matter. The comment above the test, "As the build lists the files of the farther folders first", explains why and records nothing. A search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name and `TestProjectTests` finds nothing); blind: no
- cover: `tests/SqlSource.Tool.Tests/TestProjectTests.cs` | `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader`
- proposal: Delete the test, or, if the owner wants the order rule to have a test of its own, keep this one and drop the order assertion from line 33 of the other test. Either way, the same check stops being made twice.
- verdict: confirmed
- verifier's reason: A test of the test library's `TestProject`, in no scope's run, so held to the named-cover rule. Read tests/SqlSource.Tool.Tests/TestProjectTests.cs:70-80 (a settings file at Queries/sqlsource.json, then a Settings key; expects `SettingsFiles` `[project.SettingsPath, nearer]`) and the cover at :15-35, whose :21 sets a Settings key, :24 adds the same Queries/sqlsource.json and :33 asserts `SettingsFiles.ShouldBe([project.SettingsPath, settings])`, the same order on the same kind of inputs. The one difference, adding the file before or after the key, cannot matter: tests/SqlSource.Tool.Testing/TestProject.cs:107-143 builds the list only when `ManifestText` runs, writing the project's own `Settings=` line (:121-130) before every listed file (:132-135), whatever order the calls came in. The comment at :70 explains the order as the build's and records no limit, regression or decision; a grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred for the test's name and `TestProjectTests` finds nothing (only docs/superpowers/plans, not a record document, names the cover). git log -S shows the test came with 6af57a01 and the cover's order assertion later with fb141e29, so the check is now made twice; either way of the proposal removes the duplicate.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-09/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-09/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-09/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-09/tool-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-09/tool-root/before/reports/mutation-report.json UsageValueTests.NamedAsPath_PrintableToken_IsTheFullPathInQuotes
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-09/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-09/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-09/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-09/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-09/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-09/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests-1-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests-1-02 | delete | `TestProjectTests.Manifest_ProjectWithSettingsAndASettingsFileOfAFolder_ListsTheProjectsFileFirst` | `tests/SqlSource.Tool.Tests/TestProjectTests.cs`: `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
