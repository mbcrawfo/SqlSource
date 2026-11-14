# Test cleanup phase 40: tests/SqlSource.Tests/Generation/Paths, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Paths`
- Findings: 2 (2 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 16
- Guard: mutation (generator-generation-paths)

## Files

- `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-10

- test: `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs` | `SqlPathTests.IsRooted_PathFromARoot_IsTrue` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Generation/Paths/SqlPath.cs:33:56 Equality mutation `> 'a'`, src/SqlSource/Generation/Paths/SqlPath.cs:33:67 Equality mutation `< 'z'` and src/SqlSource/Generation/Paths/SqlPath.cs:33:79 Equality mutation `> 'A'`, each covered by this test and `IsRooted_RelativePath_IsFalse` alone); read (`IsAsciiLetter`, SqlPath.cs:33, takes `a` to `z` and `A` to `Z` as a drive letter; the rows' drive letters are `C`, `c` and `Z`, so a path on drive `a:`, `z:` or `A:` read as relative would pass, though the name promises every path from a root); blind: no (line 33 is also listed blind for other mutants, which do not compile; these compiled and survived)
- cover: none
- proposal: Add the rows `"a:/app/Users.sql"`, `"z:/app/Users.sql"` and `"A:\\app\\Users.sql"`, the three bounds of the drive letter that no row reaches.
- verdict: confirmed
- verifier's reason: `IsAsciiLetter` (SqlPath.cs:33) takes `>= 'a' and <= 'z'` or `>= 'A' and <= 'Z'`, at columns 56, 67, 79 and 90.  The drive letters of the theory are `C`, `c` and `Z` (SqlPathTests.cs:45-47).  Only `<= 'Z'` is pinned, by `Z:`, the alone kill 33:90.  The survivors `> 'a'`, `< 'z'` and `> 'A'` would read a root path on drive `a:`, `z:` or `A:` as relative, and the name promises every path from a root.  The three rows proposed kill them.
- survivors to kill: `src/SqlSource/Generation/Paths/SqlPath.cs:33:56`, `src/SqlSource/Generation/Paths/SqlPath.cs:33:67`, `src/SqlSource/Generation/Paths/SqlPath.cs:33:79`

### F-SqlSource.Tests.Generation-2-11

- test: `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs` | `SqlPathTests.FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Paths/SqlPath.cs:246:13 Statement mutation, `repeated.Sort(Comparer)` removed, covered by this test and `FindRepeated_PathListedTwiceOrUnderTwoSpellings_IsThatPathOnce` alone); read (FindRepeated adds a path when its second file is met, SqlPath.cs:229-232, and sorts afterwards, line 246; the test's files, `c/C.sql, b/B.sql, b/b.sql, a/A.sql, c/C.sql`, meet b's second file before c's, so the paths are found already in sorted order and the order the name promises holds without the sort); blind: no
- cover: none
- proposal: List the files so that the later path in sorted order repeats first, as `c/C.sql, b/B.sql, c/C.sql, b/b.sql, a/A.sql`, keeping the expected `b/B.sql, c/C.sql`.
- verdict: confirmed
- verifier's reason: FindRepeated (SqlPath.cs:209-252) adds a path when its second file is met (:229-232) and sorts it at :246.  The test's files (SqlPathTests.cs:204) repeat b before c, so the paths are found as `[b, c]` already, and `kills` lists `SURVIVED SqlPath.cs:246:13 Statement mutation`.  The proposed order `c/C.sql, b/B.sql, c/C.sql, b/b.sql, a/A.sql` finds `[c, b]`, which only the sort makes `[b, c]`.  The test's alone kills still fail under that order: 224:21 `index > 0` leaves nothing repeated, and 232:22 keeps only the last path.
- survivors to kill: `src/SqlSource/Generation/Paths/SqlPath.cs:246:13`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-paths`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-40/generator-generation-paths/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Paths.' 'Generation/Paths/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-40/generator-generation-paths/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Paths.' 'Generation/Paths/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-40/generator-generation-paths/before/reports/mutation-report.json artifacts/test-cleanup/phase-40/generator-generation-paths/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-40/generator-generation-paths/before/reports/mutation-report.json SqlPathTests.IsRooted_PathFromARoot_IsTrue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-40/generator-generation-paths/before/reports/mutation-report.json SqlPathTests.FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-2-10: `src/SqlSource/Generation/Paths/SqlPath.cs:33:56`, `src/SqlSource/Generation/Paths/SqlPath.cs:33:67`, `src/SqlSource/Generation/Paths/SqlPath.cs:33:79`
- F-SqlSource.Tests.Generation-2-11: `src/SqlSource/Generation/Paths/SqlPath.cs:246:13`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-paths`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-40/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-40/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-40/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-40/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-40/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-40/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
