# Test cleanup phase 77: tests/SqlSource.Repository.Tests, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Repository.Tests`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: covers and run time

## Files

- `site/AGENTS.md`
- `src/SqlSource/Settings/AGENTS.md`
- `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Repository.Tests-1-01

- test: `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` | `DiagnosticsDocumentTests.Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter` | rows: all
- action: strengthen
- evidence: read (DiagnosticsDocumentTests.cs:64-78 checks a hand-written list of twelve names, and its comment at :57-58 says a new parameter must be added to that list by hand. The definitions are `GeneratorParameterList.Definitions` in src/SqlSource/Settings/GeneratorParameterList.cs:60-87, the "only place the parameters are defined" (:7), plus `GeneratorParameterList.Default` (:29). The test project can see them through InternalsVisibleTo (src/SqlSource/SqlSource.csproj:24), and the test already calls internal parser types. If a parameter is added to `Definitions` but not to the list or the SQLSRC109 section, the test still passes, so it does not prove "every". The by-hand list is not called deliberate anywhere: src/SqlSource/Settings/AGENTS.md:17 and site/AGENTS.md:11 only list it among the places a new name must be added. No tech-debt or deferred item names the test.); blind: no
- cover: none
- proposal: Take the names from `GeneratorParameterList.Definitions` (each definition's `Name`) plus `GeneratorParameterList.Default`, not from the hand-written array. A new parameter then fails the test until the SQLSRC109 section names it. Drop the list from the checklist in src/SqlSource/Settings/AGENTS.md:17 and from the test's comment in the same change.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs:64-78 checks a hand-written array of twelve words, and the comment at :57-60 says a new parameter is added to that array by hand.  The parameters are defined only in `GeneratorParameterList.Definitions` (src/SqlSource/Settings/GeneratorParameterList.cs:60-86, "the only place the parameters are defined" at :7-8), with `Default` at :29, all reachable from the test project through `InternalsVisibleTo` (src/SqlSource/SqlSource.csproj:24); the test already calls internal parser types (:86-90).  A twelfth definition left out of both the array and the SQLSRC109 section passes the test, so the name's "every" is not proved.  Nothing calls the hand-written array deliberate: src/SqlSource/Settings/AGENTS.md:17 and site/AGENTS.md:11 list it, or the comment on it, only as a place a new name goes, and docs/superpowers specs (phase-07 design:20, phase-21 design:53) only describe it; no item of docs/tech-debt or docs/deferred names it.  No scope ran the slice (evidence: `None`), so there are no kills to read.  Strengthening it removes no check, so the named-cover rule does not apply.  The cleanup should also keep site/AGENTS.md:11 true, since it points at the comment on the list.
- survivors to kill: none named by place; the missing assertion is the proposal's
- condition: Drop the list from the checklist of `src/SqlSource/Settings/AGENTS.md:17` and from the test's comment, as the proposal says, and keep `site/AGENTS.md:11`, which points at that comment, true.
- after the merge of `main`: re-verified, confirmed.  The test is unchanged but for its lines (the list at DiagnosticsDocumentTests.cs:140-172, its comment at 136-139), `GeneratorParameterList.Definitions` and `Default` are unchanged, and the row of `site/AGENTS.md` that points at the list's comment is now line 11.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Repository.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Edits the findings and their verdicts ask for:

- F-SqlSource.Repository.Tests-1-01: Drop the list from the checklist of `src/SqlSource/Settings/AGENTS.md:17` and from the test's comment, as the proposal says, and keep `site/AGENTS.md:11`, which points at that comment, true.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
