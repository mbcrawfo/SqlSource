# Test cleanup phase 88: tests/DatabaseTests/Postgres.Npgsql8, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres.Npgsql8`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 5
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.Npgsql8-1-01

- test: `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs` | `AllTypesQueryTests.Int4Array_EachKindOfSequence_RoundTrips` | rows: `[InlineData("Lazy", true)]` (line 160)
- action: fold
- evidence: read (the row sends `Enumerable.Range(1, 3).Select(static x => x)` (line 151) through `InsertEveryType` and asserts `[1, 2, 3]` in order (lines 177-181); `Int4Array_LazySequence_IsEnumeratedOnce` (lines 184-198) sends a `CountingSequence` (`tests/DatabaseTests/TestHarness/EndToEnd/CountingSequence.cs:7`, an `IEnumerable<int>` and no collection) through the same `InsertWith` helper and asserts `[4, 5, 6]` in order and one enumeration, so it makes the row's check on the same path and more; were LINQ's iterator a list, the `List` and `ImmutableArray` rows would cover it instead.  The mutation evidence says `None`, so judged by reading.  No comment above the test or its group records the row, and `grep -rn` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names neither the test nor `AllTypesQueryTests` as a record; only a plan, `docs/superpowers/plans/2026-11-05-list-parameters.md:139`, lists the row as built); blind: no
- cover: `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs` | `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce`
- proposal: Drop the `"Lazy"` row and its arm of `SequenceOf`, and the lazy sequence from the comment at lines 142-143; the theory keeps the array, `List`, `ImmutableArray` and `HashSet` rows.
- verdict: confirmed
- verifier's reason: The evidence says `None` for mutation scopes, so no kills to read; judged by reading under the named-cover rule.  The `"Lazy"` row (`tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs:160`) sends `Enumerable.Range(1, 3).Select(static x => x)` (line 151) through `InsertWith` (lines 236-252) and asserts `[1, 2, 3]` in order (lines 177-181).  The cover, `Int4Array_LazySequence_IsEnumeratedOnce` (lines 184-198), on the same engine, project and Npgsql 8, sends `CountingSequence` (`tests/DatabaseTests/TestHarness/EndToEnd/CountingSequence.cs:7`, an `IEnumerable<int>` and no `IList<int>`) through the same helper and asserts `[4, 5, 6]` in order and one enumeration.  The generated binding is `value as IList<int> ?? Enumerable.ToArray(value)` (`src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs:181-193`), so the row takes either the `ToArray` arm, which the cover takes, or, were LINQ's iterator an `IList<int>`, the `as IList` arm, which the `List` and `ImmutableArray` rows (lines 157-158) take; it reaches no arm alone.  No comment above the test or its group (lines 142-143 describe the kinds only) records the row; my grep of every `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names neither the test nor the row: `tests/DatabaseTests/Postgres/README.md:167` and `docs/architecture/generator.md:712` describe a lazy sequence sent and enumerated once, which stays true through the cover.  The driver-floor exemption covers repetition against the end-to-end project, not two tests inside this project.  A fold that drops a row of a theory cannot be measured by a filter; confirmed by reading, and the cleanup phase's own guard proves it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.Npgsql8-1-01 | fold | `AllTypesQueryTests.Int4Array_EachKindOfSequence_RoundTrips` (rows: `[InlineData("Lazy", true)]` (line 160)) | `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs`: `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres.Npgsql8`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 10

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
