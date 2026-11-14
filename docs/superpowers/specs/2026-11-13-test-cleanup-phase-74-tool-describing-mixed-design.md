# Test cleanup phase 74: tests/SqlSource.Tool.Tests/Describing, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing`
- Findings: 5 (1 delete, 2 fold, 2 loosen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 62
- Guard: mutation (tool-describing), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing-1-08

- test: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs` | `SidecarStoreTests.Apply_RunOnTheWholeThatWasCancelled_RemovesNothing` | rows: all
- action: delete
- evidence: measured (1 row, 0.003 s; covers 1 mutants and kills none).  Read: it cancels the token before `Apply`, on the whole, with a three-day-old leftover, and asserts the leftover is there (SidecarStoreTests.cs:463-481).  `Apply_RunThatIsCancelledAsItBegins_RemovesNothing` (SidecarStoreTests.cs:483-504; kills 1) cancels the token later, when the store reads its clock, which it reads before it removes anything (SidecarStore.cs:109), and its row `true` (line 485) asserts on the whole the same leftover kept and the sidecar not written; a token cancelled before is still cancelled there, and the sweep cannot come before the clock it is aged by.  The comment above the cover says only when the clock is read; the search of the documents finds neither name; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs` | `SidecarStoreTests.Apply_RunThatIsCancelledAsItBegins_RemovesNothing`
- proposal: Delete the test; the row `true` of the cover proves that a cancelled run on the whole removes nothing, from a later point of cancellation.
- verdict: confirmed
- verifier's reason: `kills` gives 0 kills and one survivor, `SidecarStore.cs:108:9` Statement.  That survivor is equivalent: without line 108, the `ThrowIfCancellationRequested` at SidecarStore.cs:192, and the one at 204, still throw before any delete.  The cover's row `true` (SidecarStoreTests.cs:484-504) cancels when the clock is read at SidecarStore.cs:109, which is after line 108 and before the sweep.  It then asserts on the whole that the three-day-old leftover is kept and the sidecar not written, which is the same claim from a later point of cancellation.  The sweep is aged by that clock (`before`, SidecarStore.cs:109 and 200), so no change could remove a file between a cancel made earlier and that point.  No comment is above the test (line 463), and the comment above the cover (line 483) only says when the clock is read.  The search of the documents finds neither name, nor `SidecarStoreTests`.

### F-SqlSource.Tool.Tests.Describing-1-04

- test: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs` | `ConnectionTests.For_VariableThatIsEmpty_IsNotSet`; `ConnectionTests.For_VariableThatHoldsOnlyWhiteSpace_IsNotSet` | rows: all
- action: fold
- evidence: measured (`For_VariableThatIsEmpty_IsNotSet`: 1 row, kills 10, alone 0, cover `ConnectionTests.For_VariableThatIsBlankInTheDotEnvFile_IsNotSet`; `For_VariableThatHoldsOnlyWhiteSpace_IsNotSet`: 2 rows, kills 9, alone 0, rows needed 1).  Read: the two bodies are the same, both variables set to one value and the same two assertions (ConnectionTests.cs:153-177), and the value is the only difference; both go through the one `NullIfBlank`, `string.IsNullOrWhiteSpace` (ConnectionEnvironment.cs:90).  The two theories of the same rule for the .env file, `For_VariableThatIsBlankInTheEnvironment_IsTheDotEnvFiles` and `For_VariableThatIsBlankInTheDotEnvFile_IsNotSet`, already hold `""` as a row beside white space.  The comment above the white-space theory gives the reason for blanks; the search of the documents finds the name only in a plan of docs/superpowers/plans/, which is no record; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs` | `ConnectionTests.For_VariableThatHoldsOnlyWhiteSpace_IsNotSet`
- proposal: Make `""` a row of `For_VariableThatHoldsOnlyWhiteSpace_IsNotSet`, under a name such as `For_VariableThatIsEmptyOrHoldsOnlyWhiteSpace_IsNotSet`, and remove `For_VariableThatIsEmpty_IsNotSet`.
- verdict: confirmed
- verifier's reason: The two bodies (ConnectionTests.cs:153-160 and 163-177) are the same except for the value.  `kills` shows that the empty test's one extra kill, `ConnectionEnvironment.cs:90:58 String mutation -> (value!="")`, comes from the value `""`.  The proposal keeps `""` as a row of the folded theory, so nothing it kills is lost.  The comment at ConnectionTests.cs:162 gives the reason white space is not set and records no case.  The search of the documents finds neither test name.

### F-SqlSource.Tool.Tests.Describing-1-07

- test: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs` | `SidecarStoreTests.Apply_RunOnTheWhole_LeavesAFileOfAnotherName` | rows: `"Users.sql.json.tmp"` (line 564), `"Users.sql.json.backup.tmp"` (line 565), `"Users.sql.json.0123456789abcdef0123456789abcde.tmp"` (line 566), `"Users.sql.json.0123456789abcdef0123456789abcdeg.tmp"` (line 567), `"Users.sql.json.0123456789abcdef0123456789abcdef.tmp.txt"` (line 569)
- action: fold
- evidence: measured (10 rows, 0.078 s; kills 9, alone 0, timeouts under it 2; rows needed 2; cover `SidecarStoreTests.Apply_FileOfAnotherName_IsLeft`).  Read: the five rows named are each a row of `Apply_FileOfAnotherName_IsLeft` too (SidecarStoreTests.cs:643, 644, 645, 647, 648), and each is a name that `SidecarOfTemporary` refuses before the run on the whole or the filtered run asks whether the sidecar is wanted (SidecarStore.cs:198-199, 217-227), so the same rule of the name is proved twice.  The rows that the run on the whole alone reaches stay: the capital digits (line 568) and the names whose sidecar `SqlPath.IsSidecarFile` refuses (lines 570-573).  The comment above the theory states the rule and records no case; the search of the documents finds nothing for either name; timeouts under it are not the ground of this fold, which drops no row's kind of name; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs` | `SidecarStoreTests.Apply_FileOfAnotherName_IsLeft`
- proposal: Drop the five rows named from `Apply_RunOnTheWhole_LeavesAFileOfAnotherName`; `Apply_FileOfAnotherName_IsLeft` keeps the same names.
- verdict: confirmed
- verifier's reason: Each dropped row is in `Apply_FileOfAnotherName_IsLeft` too: lines 564, 565, 566, 567 and 569 match lines 643, 644, 645, 647 and 648.  `SidecarOfTemporary` (SidecarStore.cs:217-227) refuses each of them before `isWanted`, the choice between a run on the whole and a filtered run (SidecarStore.cs:174-176, 198-199): a negative length, a non-`.` at the cut, the digit `g`, or a name that does not end in `.tmp`.  So both tests prove the same rule of the name.  The two timeouts under the test, `SidecarStore.cs:274:13` Statement and `tries--`, are under the cover too, by `kills`.  Small inaccuracies in the finding do not change the fold.  Row 568 (capitals) is also refused by `SidecarOfTemporary`, since `IdDigits` is lowercase, and stays because the cover lacks it, not because only the run on the whole reaches it.  Row 573 is refused by `SidecarOfTemporary`, not by `IsSidecarFile`.  The comments above both theories (lines 562 and 641) state the rule and record no case, and the search of the documents finds neither name.  The rows share one name, so this fold cannot be measured with a filter; it is confirmed by reading, and the guard of the cleanup phase proves it.

### F-SqlSource.Tool.Tests.Describing-1-05

- test: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs` | `ConnectionTests.ToString_ConnectionEnvironment_LeavesTheValuesOut` | rows: all
- action: loosen
- evidence: measured (1 row; kills 3, alone 2: src/SqlSource.Tool/Describing/ConnectionEnvironment.cs:87:9 String mutation; src/SqlSource.Tool/Describing/ConnectionEnvironment.cs:88:11 String mutation, both of the layout of the text itself).  Read: the name and docs/architecture/tool.md:191 say the rule is that the text leaves the values out, and the test asserts the whole text, `ConnectionEnvironment { UnitFolder = /work, LookedFor = ..., FilePath = ..., Errors = 0 }` (ConnectionTests.cs:282-299).  Adding a member to the debug text, renaming one or changing its punctuation breaks the test while the values stay out, as `ToString_ConnectionArgument_LeavesTheValueOut` (ConnectionTests.cs:106-108) already asserts with `ShouldNotContain`; blind: no
- cover: none
- proposal: Assert that the text holds neither `Secrets.Word`, the environment's value, nor `Secrets.Marker`, the file's, in place of the whole text.
- verdict: confirmed
- verifier's reason: The test (ConnectionTests.cs:282-299) asserts the whole text of `ConnectionEnvironment.ToString` (ConnectionEnvironment.cs:86-88).  Its 2 kills that no other test makes (`ConnectionEnvironment.cs:87:9` and `88:11`, each `$""`) only empty the layout, and no value would leak under either.  The class's summary at ConnectionEnvironment.cs:85 and docs/architecture/tool.md ("Where a connection's value goes") make the rule that the values are left out.  Adding or renaming a member breaks the test while that rule still holds.  Asserting `ShouldNotContain` for `Secrets.Word` and `Secrets.Marker` tests the behaviour the name promises.

### F-SqlSource.Tool.Tests.Describing-1-06

- test: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs` | `ConnectionTests.ToString_UsageValueOfAConnection_LeavesTheTextOut` | rows: all
- action: loosen
- evidence: read (reaches no tested mutant: `UsageValue` is in src/SqlSource.Tool/, outside the scope).  The name promises that the text is left out, and the test asserts the whole text, `UsageValue { Option = --connection, Position = 3 }` (ConnectionTests.cs:110-114), of the override at src/SqlSource.Tool/UsageValue.cs:42.  A change to the debug layout, such as another member or another name for the option, breaks it while the connection's text stays out; `UsageValue` is a record, so a `ShouldNotContain` fails if the override is lost and the record prints `Text`; blind: no
- cover: none
- proposal: Assert `ShouldNotContain(Secrets.Word)` in place of the whole text, as `ToString_ConnectionArgument_LeavesTheValueOut` does.
- verdict: confirmed
- verifier's reason: `kills` in tool-describing gives 0 kills: `UsageValue` (src/SqlSource.Tool/UsageValue.cs) is outside the scope, so the test is judged by reading.  It asserts the whole text `UsageValue { Option = --connection, Position = 3 }` (ConnectionTests.cs:110-114).  `UsageValue` is a `sealed record` (UsageValue.cs:18), so without the override at UsageValue.cs:42 it would print `Text`, and `ShouldNotContain(Secrets.Word)` would fail.  The loosened assertion still guards the rule and no longer breaks on a change to the layout.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-74/tool-describing/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Capture.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Describers.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Postgres.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-74/tool-describing/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Capture.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Describers.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Postgres.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json artifacts/test-cleanup/phase-74/tool-describing/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json ConnectionTests.For_VariableThatIsEmpty_IsNotSet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json ConnectionTests.For_VariableThatHoldsOnlyWhiteSpace_IsNotSet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json ConnectionTests.ToString_ConnectionEnvironment_LeavesTheValuesOut
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json ConnectionTests.ToString_UsageValueOfAConnection_LeavesTheTextOut
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json SidecarStoreTests.Apply_RunOnTheWhole_LeavesAFileOfAnotherName
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-74/tool-describing/before/reports/mutation-report.json SidecarStoreTests.Apply_RunOnTheWholeThatWasCancelled_RemovesNothing
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-74/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-74/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-74/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-74/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-74/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-74/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing-1-06.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
