# Test cleanup phase 73: tests/SqlSource.Tool.Tests/Bundles, loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Bundles`
- Findings: 1 (1 loosen)
- Kind: one kind, loosen
- Changed lines: about 10
- Guard: mutation (tool-bundles)

## Files

- `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Bundles-1-03

- test: `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs` | `BundleJsonTests.Write_Outcome_IsAnObjectWithItsKind` | rows: all
- action: loosen
- evidence: read: the name promises that `outcome` is an object with its `kind` (and, by the second assertion, an `id` and a `step` for a failure), as `BundleJson`'s remarks say (`src/SqlSource.Tool/Bundles/BundleJson.cs:15-16`).  The test asserts it as substrings of the written text with their exact indentation and line breaks, `"\"outcome\": {\n    \"kind\": \"entry\"\n  }"` (line 38) and the failure's at lines 42-43, which also pin the order of `id` and `step`.  The indentation is `JsonFile`'s `Indented = true` (`src/SqlSource.Tool/Describing/Capture/JsonFile.cs:17`), shared with the capture; a change of its indent, or writing `step` before `id`, fails the test while `outcome` is still an object with the same members.  Measured: kills 6, alone 0.  blind: no
- cover: none
- proposal: Parse the written text with `JsonDocument` and assert that `outcome` is an object whose `kind` is `entry`, and for the failure `failure` with its `id` and `step`, without the layout.
- verdict: confirmed
- verifier's reason: Read `tests/SqlSource.Tool.Tests/Bundles/BundleJsonTests.cs:35-45`, `src/SqlSource.Tool/Bundles/BundleJson.cs:13-16,42-72` and `src/SqlSource.Tool/Describing/Capture/JsonFile.cs:16-17`.  The name promises that `outcome` is an object with its `kind`, plus `id` and `step` for a failure.  The assertions are substrings that also fix the four-space and two-space indents of `JsonFile.Options` (`Indented = true`), which `BundleJson` shares with every JSON file of the tool, and the order of `id` before `step`, which a JSON reader ignores.  `kills` gives 6 kills, all statement removals of the writes at `BundleJson.cs:45,55,59,62,63,64`.  A parse with `JsonDocument` that asserts the members would still fail on each of them.  `Write_EndsWithOneLineBreak_AndHasNoCarriageReturn` (lines 155-164) holds the `\n` line ends that the remarks promise.  No document names this test as a record.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-bundles`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-73/tool-bundles/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.&FullyQualifiedName!~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-73/tool-bundles/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.&FullyQualifiedName!~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-73/tool-bundles/before/reports/mutation-report.json artifacts/test-cleanup/phase-73/tool-bundles/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-73/tool-bundles/before/reports/mutation-report.json BundleJsonTests.Write_Outcome_IsAnObjectWithItsKind
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-bundles`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-73/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-73/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-73/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-73/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-73/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-73/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
