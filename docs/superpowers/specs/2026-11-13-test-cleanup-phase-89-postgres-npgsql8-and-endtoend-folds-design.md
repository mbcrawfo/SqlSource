# Test cleanup phase 89: tests/DatabaseTests/Postgres.Npgsql8 + tests/DatabaseTests/Postgres/EndToEnd, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folders: `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/Postgres/EndToEnd`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 9
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs`
- `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.EndToEnd-1-11

- test: `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` | rows: `("lazy")` line 31
- action: fold
- evidence: read (the generated count of a list switches on `ICollection<T>`, `IReadOnlyCollection<T>` and a default arm that copies the values once with `Enumerable.ToArray`, src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs:93-118.  The "lazy" row, `values.Select(...)` of TestHarness/EndToEnd/ListArguments.cs:23, is neither collection and takes the default arm, and asserts alice and carol; `ListAccountsByIds_LazySequence_IsEnumeratedOnce`, ListQueryTests.cs:46-66, sends an iterator method's sequence, which takes the same arm of the same method, and asserts bob and carol and one enumeration.  README.md:154 says a lazy sequence is sent and enumerated once, which the other test keeps true; no AGENTS.md, `docs/tech-debt` or `docs/deferred` names the test, and tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md:22 couples the file to its copy in `tests/DatabaseTests/Postgres.Npgsql8`); blind: no
- cover: `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`
- proposal: Drop the "lazy" row here and in the copy of `tests/DatabaseTests/Postgres.Npgsql8`, as that AGENTS.md rule requires; the `"lazy"` arm of `ListArguments.CollectionOf` goes if no other caller uses it.
- verdict: confirmed
- verifier's reason: The count helper switches on `ICollection<T>`, `IReadOnlyCollection<T>` and a default arm that copies once with `Enumerable.ToArray` (src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs:93-118).  The "lazy" row (ListQueryTests.cs:31) sends `values.Select(...)` (TestHarness/EndToEnd/ListArguments.cs:23), a LINQ select iterator that is neither collection, so it takes the default arm; `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (ListQueryTests.cs:46-66) sends an iterator method's sequence through the same arm of the same `ListAccountsByIds` on a closed connection, as the row does, and asserts the rows and one enumeration.  The Npgsql 8 copy (tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs:28-66) has the same row and the same cover, so the AGENTS rule (EndToEnd/AGENTS.md:22) can be kept without loss there.  README.md:154 names a lazy sequence enumerated once, which the cover keeps true; no AGENTS.md, `docs/architecture`, `docs/tech-debt` or `docs/deferred` names the test.  `CollectionOf`'s "lazy" arm is still used by the two SQL Server copies (SqlServer/EndToEnd/ListQueryTests.cs:41, SqlServer.SqlClient61/ListQueryTests.cs:40), so under the proposal's own condition it stays.  A dropped theory row cannot be measured by a filter: confirmed by reading; the phase's own guard proves it.
- condition: The `"lazy"` arm of `ListArguments.CollectionOf` (`tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs`) stays, since SQL Server's copies still pass it; phase 92 removes it with their rows.

### F-DatabaseTests.Postgres.Npgsql8-1-02

- test: `tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` | rows: `[InlineData("lazy")]` (line 32)
- action: fold
- evidence: read (the row sends `ListArguments.CollectionOf("lazy", [1, 3])`, `values.Select(static id => id)` (`tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs`), and asserts `[Alice, Carol]` (line 44); `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (lines 48-67) sends an iterator method's sequence, certainly no collection, through the same method and asserts `[Bob, Carol]` and one enumeration, so it makes the row's check on the same path and more.  The mutation evidence says `None`, so judged by reading.  No comment above the test or its group records the row, and `grep -rn` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names neither as a record; `tests/DatabaseTests/Postgres.Npgsql8/AGENTS.md` says the class is a copy of `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs`, which has the same row (line 31) and the same cover (line 48), so the fold is made in both, and `ListArguments.CollectionOf` keeps its `"lazy"` arm only if the SQL Server classes still use it); blind: no
- cover: `tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`
- proposal: Drop the `"lazy"` row here and in the end-to-end copy, keeping the array, `List` and `HashSet` rows, as the copy rule asks.
- verdict: confirmed
- verifier's reason: The evidence says `None`; judged by reading.  The `"lazy"` row (`tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs:32`) sends `values.Select(static id => id)` (`tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs:23`) through `ListAccountsByIds` and asserts `[Alice, Carol]` (line 44).  The generated counter switches `ICollection<E>` → `Count`, `IReadOnlyCollection<E>` → `Count`, default → `Enumerable.ToArray` and `values = array` (`src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs:93-120`); the array, `List` and `HashSet` rows all take the first arm, and the cover, `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (lines 48-67), same engine and project, sends an iterator method's sequence of two ids through the same method, count 2 as the row's, and asserts the rows and one enumeration, so it takes the default arm the row takes and checks more.  Were LINQ's `Select` iterator a collection it would take the collection arm the other rows take, since no LINQ iterator is a read-only collection without also being an `ICollection<E>`.  No comment above the test records the row; my grep of the documents finds only `tests/DatabaseTests/Postgres/README.md:154` and `tests/DatabaseTests/SqlServer/README.md:176`, which describe a lazy sequence sent and enumerated once, true through the cover.  The end-to-end copy has the same row (`tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs:30`) and the same cover (lines 47-67), so the fold in both, as the copy rule of `tests/DatabaseTests/Postgres.Npgsql8/AGENTS.md` asks, holds; the `"lazy"` arm of `ListArguments.CollectionOf` stays while the SQL Server classes use it.  A row drop cannot be measured by a filter; confirmed by reading, and the phase's own guard proves it.
- condition: The `"lazy"` arm of `ListArguments.CollectionOf` (`tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs`) stays, since SQL Server's copies still pass it; phase 92 removes it with their rows.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.EndToEnd-1-11 | fold | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` (rows: `("lazy")` line 31) | `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs`: `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce` |
| F-DatabaseTests.Postgres.Npgsql8-1-02 | fold | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` (rows: `[InlineData("lazy")]` (line 32)) | `tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs`: `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`, `dotnet test --project tests/DatabaseTests/Postgres.Npgsql8`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/Postgres.Npgsql8/AGENTS.md`: 8
- `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`: 22
- `tests/DatabaseTests/Postgres/README.md`: 150, 154
- `tests/DatabaseTests/README.md`: 45, 49

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
