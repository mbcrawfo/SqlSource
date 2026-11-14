# Test cleanup phase 35: tests/SqlSource.Tests/Generation/Chains, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Chains`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (generator-generation-chains)

## Files

- `tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-02

- test: `tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs` | `BuildFoldersTests.Of_OtherPercentSign_IsKept` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Generation/Chains/BuildFolders.cs:135:36 Equality mutation `i + 2 <= entry.Length` and src/SqlSource/Generation/Chains/BuildFolders.cs:135:36 Arithmetic mutation `i - 2`, each covered by this test, `Of_EscapedDelimiter_PartsNoEntry` and `Of_EscapedFolder_IsDecodedToTheRealFolder`, none killing them); read (BuildFolders.cs:135 reads the two characters after a `%` only when both exist; Unescape runs only on an entry that ends with a separator, BuildFolders.cs:114-115, so a `%` just before that last separator is where the bound matters, and either mutant then reads `entry[entry.Length]` and throws; every row of the test, BuildFoldersTests.cs:100-103, has at least two characters after its `%`, `/repo/a%/obj/` among them; the name promises that any other `%` stays as it is); blind: no (line 135 is also listed blind for other mutants, which do not compile; these two compiled and survived)
- cover: none
- proposal: Add the row `[InlineData("/repo/a%/", "repo/a%")]`, a `%` right before the entry's last separator, which both mutants turn into an exception.
- verdict: confirmed
- verifier's reason: src/SqlSource/Generation/Chains/BuildFolders.cs:135 reads `entry[i + 1]` and `entry[i + 2]` only when `i + 2 < entry.Length`.  Every row at tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs:100-103 has at least two characters after its `%`.  `/repo/a%3/obj/` and `/repo/a%/obj/` are among them, and their `%` is at 7 in an entry of 14 or 13.  So `<=` and `i - 2` both read in range and survive.  For `/repo/a%/` (length 9, `%` at 7), FolderOf accepts the entry because it is rooted and ends with `/`, BuildFolders.cs:111-116.  Each mutant then evaluates `entry[9]` and throws, while the original appends `%` and gives `repo/a%`.
- survivors to kill: `src/SqlSource/Generation/Chains/BuildFolders.cs:135:36`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-chains`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-35/generator-generation-chains/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Chains.' 'Generation/Chains/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-35/generator-generation-chains/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Chains.' 'Generation/Chains/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-35/generator-generation-chains/before/reports/mutation-report.json artifacts/test-cleanup/phase-35/generator-generation-chains/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-35/generator-generation-chains/before/reports/mutation-report.json BuildFoldersTests.Of_OtherPercentSign_IsKept
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-1-02: `src/SqlSource/Generation/Chains/BuildFolders.cs:135:36`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-chains`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-35/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-35/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-35/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-35/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-35/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-35/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
