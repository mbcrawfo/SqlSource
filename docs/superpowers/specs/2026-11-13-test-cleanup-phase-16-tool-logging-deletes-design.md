# Test cleanup phase 16: tests/SqlSource.Tool.Tests/Logging, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Logging`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 22
- Guard: mutation (tool-logging)

## Files

- `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs`
- `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-2-02

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs` | `RunLogFileTests.Write_ShortString_IsNotMarked` | rows: all
- action: delete
- evidence: measured (1 row, kills 7, alone 0, timeouts under it 0; cover: `RunLogFileTests.Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder`).  Read: it writes `new LogMember("text", "a")` and asserts the line lacks `truncated`; `Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder` (RunLogFileTests.cs:32-55) writes the same member `("text", "a")` among others and asserts the whole line, which has no `truncated`.  No comment above it, and no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it or ties a decision to it.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs` | `RunLogFileTests.Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder`
- proposal: Delete the test; the exact line of `Write_Event_…` already proves a short string is written without the mark.
- verdict: confirmed
- verifier's reason: Read RunLogFileTests.cs:166-174 and :31-55: the cover writes the same member `("text", "a")` and asserts the whole line with `ShouldBe`, so a `truncated` key anywhere fails it.  `kills` (tool-logging) gives the test 7 kills (RunLogFile.cs:65:5, 152:31 `cut = true`, 153:21, 161:25, 162:25, 165:25 `!(cut)`, 233:17), each one a change to the line the cover asserts in full, and all 7 also name `Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder` as a killer; 152:31 and 165:25 are the two that add `truncated`, which is what the name promises.  No comment above the test; the grep over the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds no mention (only a plan in docs/superpowers/plans, which is not a record the brief names).  Nothing here is wording, a budget, a wait or blind code.

### F-SqlSource.Tool.Tests-2-04

- test: `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs` | `SessionRunLogTests.Write_ValueSplitOverTheNameAndAMember_IsTheMarkInBoth` | rows: all
- action: delete
- evidence: measured (1 row, kills 4, alone 0, timeouts under it 0; cover: `RunLogRedactionTests.Run_DescriberThatGivesItsConnectionAsItsServer_HasTheMarkInDatabaseOpen`).  Read: it writes `"open " + FirstHalf` with a member named `SecondHalf + " key"` and no prefix, and asserts `open ***` and `*** key` (SessionRunLogTests.cs:61-71); `Write_WithAPrefixAndAValueSplitInTheName_PutsThePrefixBeforeTheMark` (lines 146-156) writes the same event and asserts the same name and member with `postgres.` before the name, and `Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach` (lines 19-48) asserts that no prefix adds nothing to the name (`open ***`).  `prefix + rest.Dequeue()` (src/SqlSource.Tool/Logging/SessionRunLog.cs:51) is the one place either path differs.  No comment above it, and no document of those the brief names mentions it.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs` | `SessionRunLogTests.Write_WithAPrefixAndAValueSplitInTheName_PutsThePrefixBeforeTheMark`; `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs` | `SessionRunLogTests.Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach`
- proposal: Delete the test; the prefixed twin proves the split over the name, and the first test of the class proves the unprefixed name.
- verdict: confirmed
- verifier's reason: Read SessionRunLogTests.cs:61-71 and :146-156: the two tests write the identical event, `"open " + FirstHalf` with the member `SecondHalf + " key"`, and differ only in the prefix `"postgres."`.  SessionRunLog.cs:49-51 searches the strings without the prefix and adds it after (`prefix + rest.Dequeue()`), so with a null prefix the name is the cleaned string itself, which `Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach` (lines 19-48) asserts as `open ***`.  `kills` (tool-logging) gives both tests exactly the same 4 kills (SessionRunLog.cs:43:13, 44:13, 52:9, 63:17) and no survivors or timeouts; line 51 is blind, read as above.  The join itself (`JoinedRedaction`) is outside the scope but receives identical strings from both tests.  No comment above the test (the class comment at :9-10 is general); no document of those the brief names mentions it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-logging`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-16/tool-logging/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-16/tool-logging/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Logging.' 'Logging/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-16/tool-logging/before/reports/mutation-report.json artifacts/test-cleanup/phase-16/tool-logging/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-16/tool-logging/before/reports/mutation-report.json RunLogFileTests.Write_ShortString_IsNotMarked
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-16/tool-logging/before/reports/mutation-report.json SessionRunLogTests.Write_ValueSplitOverTheNameAndAMember_IsTheMarkInBoth
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-logging`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-16/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-16/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-16/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-16/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-16/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-16/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests-2-09 (keep, `RunLogFileTests.Open_PathOfAFileOfTheRun_GivesNoLogAndTheReasonAndLeavesTheFileAsItWas`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
