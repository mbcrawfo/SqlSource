# Test cleanup phase 76: tests/SqlSource.Tests/TestHarness/SyncAsync, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/TestHarness/SyncAsync`
- Findings: 1 (1 delete)
- Kind: one kind, delete
- Changed lines: about 10
- Guard: covers and run time

## Files

- `tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.TestHarness-1-03

- test: `tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs` | `VerdictsTests.Of_DifferentMessage_IsDifferent` | rows: all
- action: delete
- evidence: read (`VerdictsTests.cs:36-43` compares two outcomes that differ in their failure's message alone and asserts `Different` and that the report holds both messages.  The theory row `"Message", "message-a", "message-b"` at `VerdictsTests.cs:114` changes only `Failure.Message` of an otherwise equal outcome and asserts the same three things; both go through the same path of `Verdicts.Of`, the record equality of `Failure` at `tests/DatabaseTests/TestHarness/SyncAsync/KnownDifference.cs:63` and the `Failure` line of the report through `Describe` at lines 72 and 113.  No comment above the test or its class records it, and `grep -rn` of the test's and its class's names over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds neither.  No mutation scope ran the slice); blind: no
- cover: `tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs` | `VerdictsTests.Of_OutcomesDifferingInOneField_IsDifferent` (row `"Message", "message-a", "message-b"`, line 114)
- proposal: Delete the test; the theory's `Message` row proves the same verdict and report for a difference in the message alone.
- verdict: confirmed
- verifier's reason: `Of_DifferentMessage_IsDifferent` (`tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs:35-43`) and the theory row `"Message", "message-a", "message-b"` (line 114, outcome from line 100) each give `Verdicts.Of` two outcomes that differ only in `Failure.Message`, with no known differences.  Each assert `Different` and that the report holds both messages.  Both take the same path in `tests/DatabaseTests/TestHarness/SyncAsync/KnownDifference.cs`: `Equal` fails on the record equality of `Failure` (line 63, `Failure` a sealed record at `Outcome.cs:6`), the loop is empty, and only the `Failure` line (line 72) is written, through `Describe` (line 113).  The fact's null `InnerType`, `ItemIndex` and `Result` (`VerdictsTests.cs:19-26`) are the same on both sides, so they reach only the `?? "none"` fallbacks of lines 110-112 and the skipped `Result` line, which is not what the test is named for.  Line 35 has no comment and the class has none.  `grep -rn` of `Of_DifferentMessage_IsDifferent` and `VerdictsTests` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` finds neither.  The only mention is the plan that listed it, `docs/superpowers/plans/2026-11-12-sync-methods-part-4.md:111`, which is a task list, not a record.  The masking of messages that the SyncAsync readmes describe (`tests/DatabaseTests/Postgres.SyncAsync/README.md:60`) is `Rendering.Masked`'s and is not exercised here.  No mutation scope ran the slice; the cover was read and proves the same.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.TestHarness-1-03 | delete | `VerdictsTests.Of_DifferentMessage_IsDifferent` | `tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs`: `VerdictsTests.Of_OutcomesDifferingInOneField_IsDifferent` (row `"Message", "message-a", "message-b"`, line 114) |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
