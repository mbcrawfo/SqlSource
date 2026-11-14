# Test cleanup phase 08: tests/SqlSource.Tests/Snapshot, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Snapshot`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 16
- Guard: mutation (generator-snapshot)

## Files

- `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Snapshot-1-02

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` | `SidecarTokenizerTests.TrySkipValue_TrailingCommaInStrictMode_IsTheClosingBracket` | rows: all
- action: delete
- evidence: measured (1 row; kills 39, alone 0, timeouts under it 2; cover `SidecarTokenizerTests.TrySkipValue_BrokenGrammar_FailsAtTheToken`) and read (SidecarTokenizerTests.cs:305-313 runs a strict tokenizer over `"[1,]"` and expects `TrySkipValue` false with the error `new TextSpan(3, 1)`; the row `[InlineData("[1,]", 3, 1)]` at SidecarTokenizerTests.cs:267 of `TrySkipValue_BrokenGrammar_FailsAtTheToken` makes the identical call on the identical text with the identical expectation, and the row `"{\"a\":1,}", 7, 1` at :252-256 does the same for an object.  The timeouts under it are reached by that identical input, so the row detects whatever it does.  No comment above the test; no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; only the plan `docs/superpowers/plans/2026-10-26-settings-files-1-reader.md` lists it); blind: no
- cover: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` | `SidecarTokenizerTests.TrySkipValue_BrokenGrammar_FailsAtTheToken` (rows `"[1,]", 3, 1` line 267 and `"{\"a\":1,}", 7, 1` line 252)
- proposal: Delete the fact; the strict-mode contrast with `TrySkipValue_TrailingCommaInLenientMode_IsAccepted` stays in the two rows of the grammar theory.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs:305-313 runs a strict `SidecarTokenizer("[1,]")`, expects `TrySkipValue(..., 0, ...)` false and the error `TextSpan(3, 1)`; the row `[InlineData("[1,]", 3, 1)]` at :267 of `TrySkipValue_BrokenGrammar_FailsAtTheToken` (:276-283) is the identical construction, call and expectation.  `kills`: 39 kills, 0 alone; its two timeouts, SidecarTokenizer.cs:103:17 and :480:13, are also under the cover (its `kills` lists both as TIMEOUT), as the identical input must reach them.  Every kill is on the paths `[1,]` runs in strict mode (white space, number scan, array skip, the strict comma at :325), which the test would notice.  No comment above the test; no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; only docs/superpowers/plans/2026-10-26-settings-files-1-reader.md:65 lists it.

### F-SqlSource.Tests.Snapshot-1-04

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` | `SidecarTokenizerTests.Next_CommentInStrictMode_IsInvalidAtTheSlash` | rows: all
- action: delete
- evidence: measured (2 rows; kills 7, alone 0, timeouts under it 1; rows needed 1; cover `SidecarReaderTests.Read_TextThatIsNotOneJsonObject_IsInvalidJson`) and read (SidecarTokenizerTests.cs:82-86 expects a strict tokenizer's first token over `"/ 1"` and `"// a\n1"` to be `Invalid` at `(0, 1)`.  In strict mode `Next()` never reaches `TrySkipComment` (src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs:94, `lenient && character == '/'`), so every slash takes one path, which the row `[InlineData("/* c */", 0, 1)]` at SidecarTokenizerTests.cs:47 of `Next_TextThatIsNotJson_IsInvalidAtTheCharacter`, listed there as "A comment", already asserts on a strict tokenizer; the row `"[1, // c\n2]", 4, 1` at :271 of `TrySkipValue_BrokenGrammar_FailsAtTheToken` asserts a strict line comment fails at its slash.  No comment above the test; no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; only the plan `docs/superpowers/plans/2026-10-26-settings-files-1-reader.md` lists it); blind: no
- cover: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` | `SidecarTokenizerTests.Next_TextThatIsNotJson_IsInvalidAtTheCharacter` (row `"/* c */", 0, 1`, line 47); `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs` | `SidecarTokenizerTests.TrySkipValue_BrokenGrammar_FailsAtTheToken` (row `"[1, // c\n2]", 4, 1`, line 271)
- proposal: Delete the theory.  If the line-comment form is wanted beside the block comment at the tokenizer level, add `"// a\n1", 0, 1` as a row of `Next_TextThatIsNotJson_IsInvalidAtTheCharacter`, whose following `End` holds for it too.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs:82-86 expects a strict tokenizer's first token over `"/ 1"` and `"// a\n1"` to be `Invalid` at `(0, 1)`.  In strict mode src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs:94 (`lenient && character == '/'`) is false for every slash, so any slash takes the same path to the invalid token; the row `"/* c */", 0, 1` at :47, listed under "A comment" (:46), asserts that on a strict tokenizer and the `End` after it, and the row `"[1, // c\n2]", 4, 1` at :271 asserts a strict line comment fails at its slash.  `kills`: 7 kills, 0 alone, all explicable (:94:22 `lenient ||` would skip the comment, :90, :107, :388:79 the invalid token's span), and :94:22 and :388:79 are killed by `Next_TextThatIsNotJson_IsInvalidAtTheCharacter` too; its one timeout, :103:17, is also under that cover.  No comment above the test; only the plan docs/superpowers/plans/2026-10-26-settings-files-1-reader.md:61 lists it.
- condition: The proposal's extra row is optional, as it says.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-snapshot`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-08/generator-snapshot/before --tests 'FullyQualifiedName~SqlSource.Tests.Snapshot.' 'Snapshot/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-08/generator-snapshot/after --tests 'FullyQualifiedName~SqlSource.Tests.Snapshot.' 'Snapshot/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-08/generator-snapshot/before/reports/mutation-report.json artifacts/test-cleanup/phase-08/generator-snapshot/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-08/generator-snapshot/before/reports/mutation-report.json SidecarTokenizerTests.TrySkipValue_TrailingCommaInStrictMode_IsTheClosingBracket
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-08/generator-snapshot/before/reports/mutation-report.json SidecarTokenizerTests.Next_CommentInStrictMode_IsInvalidAtTheSlash
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-snapshot`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-08/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-08/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-08/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-08/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-08/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-08/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 15

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
