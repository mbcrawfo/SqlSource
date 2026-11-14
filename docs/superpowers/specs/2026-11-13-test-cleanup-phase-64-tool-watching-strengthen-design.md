# Test cleanup phase 64: tests/SqlSource.Tool.Tests/Watching, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Watching`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (tool-watching), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-4-06

- test: `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs` | `WatchSetTests.Classify_UnderBinObjOrGit_IsNone` | rows: `".git/index"` (:303)
- action: strengthen
- evidence: read (the row is the theory's one `.git` row, and `index` is no settings, `.sql`, `.cs` or evaluation file, so `WatchSet.EffectOf` gives `None` for its change, creation and deletion with `.git` taken out of `IgnoredFolders`, src/SqlSource.Tool/Watching/WatchSet.cs:31 and :162-188; the rule matters for content git writes below a project whose folder is the repository's root, as a folder `App/.git/objects/ab` made, which would ask for an evaluation; today only the `beside` check of `Classify_NamedEnvFileUnderAnIgnoredFolder_Describes`, row `"App/.git/sqlsource.env"` at :217 and :232, proves it, as a side assertion of another test); blind: yes (the folder list is a static initialiser)
- cover: none
- proposal: Replace or join the row with a path below `.git` that would be new content without the rule, as `"App/.git/x/New.cs"`.
- verdict: confirmed
- verifier's reason: The row ".git/index" (tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs:303) gives `None` for Changed, Created and Deleted with or without ".git" in `IgnoredFolders` (src/SqlSource.Tool/Watching/WatchSet.cs:31).  It is outside the project folder /w/App, "index" is not listed, and it is not an evaluation file (:190-194) or a .sql, .cs or settings file (:200-219), so `EffectOf` (:162-188) returns `None` either way.  The other .git rows of `Classify_ProjectAssetsBelowABinOrGitFolder_IsNone` (:346-347) read `IgnoredAssetsFolders` (:35), a different array.  So only the `beside` check at :232 of row "App/.git/sqlsource.env" (:217) proves the rule: without it, "App/.git/Other.cs" created below the project is new content and asks for an evaluation.  Line 31 is blind as a static initialiser, as the evidence says.  The proposed "App/.git/x/New.cs" gives `Evaluate` on Created without the rule.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-watching`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-64/tool-watching/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Watching.' 'Watching/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-64/tool-watching/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Watching.' 'Watching/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-64/tool-watching/before/reports/mutation-report.json artifacts/test-cleanup/phase-64/tool-watching/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-64/tool-watching/before/reports/mutation-report.json WatchSetTests.Classify_UnderBinObjOrGit_IsNone
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests-4-06: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-watching`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-64/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-64/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-64/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-64/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-64/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-64/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests-4-06.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
