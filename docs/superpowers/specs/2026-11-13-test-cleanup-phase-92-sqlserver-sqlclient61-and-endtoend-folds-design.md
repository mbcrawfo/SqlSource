# Test cleanup phase 92: tests/DatabaseTests/SqlServer.SqlClient61 + tests/DatabaseTests/SqlServer/EndToEnd, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folders: `tests/DatabaseTests/SqlServer.SqlClient61`, `tests/DatabaseTests/SqlServer/EndToEnd`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 10
- Guard: covers and run time

## Files

- `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs`
- `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs`
- `tests/DatabaseTests/TestHarness/EndToEnd/ListArguments.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-engines-06

- test: `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` | rows: `[InlineData("lazy")]` (line 34)
- action: fold
- evidence: read (twin fault of F-DatabaseTests.Postgres.EndToEnd-1-11; the SqlServer.EndToEnd-1 slice has no finding on it.  The count helper of a list is one code for both engines, switching on `ICollection<T>`, `IReadOnlyCollection<T>` and a default arm that copies once with `Enumerable.ToArray` (src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs:80-123; its only engine branch is the parameter limit, :175-190).  The row sends `values.Select(static id => id)` (TestHarness/EndToEnd/ListArguments.cs:23), no collection, so it takes the default arm, and asserts alice and carol; `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (ListQueryTests.cs:48-66), same engine and method, sends an iterator method's sequence through the same arm and asserts bob and carol and one enumeration.  README.md:176 says a lazy sequence is sent and enumerated once, which the cover keeps true; no comment records the row, and no AGENTS.md, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test.  tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md couples the file to its copy in `tests/DatabaseTests/SqlServer.SqlClient61` (F-engines-07).  No mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`
- proposal: Drop the `"lazy"` row here and in the SqlClient 6.1 copy, as the copy rule asks.  With F-DatabaseTests.Postgres.EndToEnd-1-11 and F-DatabaseTests.Postgres.Npgsql8-1-02 applied too, no caller of `ListArguments.CollectionOf` passes `"lazy"`, and its arm goes, which those verdicts kept only for the SQL Server copies.
- verdict: confirmed
- verifier's reason: The `"lazy"` row (tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs:34) sends `values.Select(static id => id)` (TestHarness/EndToEnd/ListArguments.cs:23) and asserts alice and carol.  The generated count switches `ICollection<T>`, `IReadOnlyCollection<T>`, default `Enumerable.ToArray` (src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs:93-120), with no engine branch but the limit (:175-187).  The cover `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (:48-66), same class, engine and method, sends an iterator method's sequence through the default arm and asserts the rows and one enumeration.  Whichever arm LINQ's select iterator takes, the row is covered: the default arm by the cover, or a collection arm by the array and List rows of the same theory.  README.md:176 says a lazy sequence is sent and enumerated once, which the cover keeps true; no comment records the row and no AGENTS.md, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it.  EndToEnd/AGENTS.md:26 couples the file to its SqlClient 6.1 copy, which F-engines-07 drops in the same change.  `grep -rn CollectionOf tests` finds only the four list theories, so with the PostgreSQL drops (F-DatabaseTests.Postgres.EndToEnd-1-11 and Npgsql8-1-02, both confirmed) the `"lazy"` arm has no caller.  A row drop cannot be measured by a filter, since the rows share a name: confirmed by reading, and the phase's own guard proves it.
- condition: Remove the `"lazy"` arm of `ListArguments.CollectionOf`, whose PostgreSQL callers phase 89 has already dropped, once no caller passes `"lazy"`.

### F-engines-07

- test: `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` | rows: `[InlineData("lazy")]` (line 33)
- action: fold
- evidence: read (twin fault of F-DatabaseTests.Postgres.Npgsql8-1-02; the SqlServer.SqlClient61-1 slice has no finding on it.  The row sends `values.Select(static id => id)` through `ListAccountsByIds` and asserts alice and carol; `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (ListQueryTests.cs:47-65), same project, driver and engine, sends an iterator method's sequence through the same default arm of the same generated count (ListWriter.cs:80-123) and asserts the rows and one enumeration.  The driver-floor exemption covers repetition against the end-to-end project, not two tests inside this one.  tests/DatabaseTests/SqlServer.SqlClient61/AGENTS.md says the class is a copy of the end-to-end `ListQueryTests`, which has the same row and cover (F-engines-06).  No comment records the row; nothing in the searched documents names it.  No mutation scope ran the slice.); blind: no
- cover: `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs` | `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`
- proposal: Drop the `"lazy"` row in the same change as F-engines-06.
- verdict: confirmed
- verifier's reason: tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs:33 is the same `"lazy"` row; the cover `ListAccountsByIds_LazySequence_IsEnumeratedOnce` (:47-65) is in the same class, on the same driver (SqlClient 6.1) and the same `AppContainer`, through the same generated default arm, and asserts more.  The driver-floor exemption is for repeating the end-to-end tests on the oldest driver, which the cover still does; it does not protect two tests inside the copy that prove the same.  tests/DatabaseTests/SqlServer.SqlClient61/AGENTS.md:5 says the class is a copy of the end-to-end `ListQueryTests`, a change to one changing the other, so dropping it with F-engines-06 keeps the rule.  No comment records the row; the documents name it nowhere.  Confirmed by reading; a row drop cannot be measured by a filter, and the phase's own guard proves it.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-engines-06 | fold | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` (rows: `[InlineData("lazy")]` (line 34)) | `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs`: `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce` |
| F-engines-07 | fold | `ListQueryTests.ListAccountsByIds_EachKindOfCollection_FindsTheRows` (rows: `[InlineData("lazy")]` (line 33)) | `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs`: `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer`, `dotnet test --project tests/DatabaseTests/SqlServer.SqlClient61`, `dotnet test --project tests/DatabaseTests/TestHarness`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/README.md`: 45, 49
- `tests/DatabaseTests/SqlServer.SqlClient61/AGENTS.md`: 5
- `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`: 26
- `tests/DatabaseTests/SqlServer/README.md`: 173, 176

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
