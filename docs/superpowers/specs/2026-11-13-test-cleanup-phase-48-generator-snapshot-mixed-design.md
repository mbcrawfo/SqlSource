# Test cleanup phase 48: tests/SqlSource.Tests/Snapshot, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Snapshot`
- Findings: 6 (2 delete, 4 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 57
- Guard: mutation (generator-snapshot)

## Files

- `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`
- `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Snapshot-1-01

- test: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs` | `SidecarComparerTests.IsCurrentFor_SameHashDatabaseAndVersion_IsTrue` | rows: all
- action: delete
- evidence: measured (1 row; covers 2 mutants and kills none) and read (SidecarComparerTests.cs:463-465 calls `Entry(hash: "h", database: "app", toolVersion: "1.2.3").IsCurrentFor("h", "app", "1.2.3")` and expects true, the exact call and outcome of the row `[InlineData("h", "app", true)]` at SidecarComparerTests.cs:447 of `IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys`, whose entry is built the same way at :455-457; no comment above the test, and no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it or its class; only a plan, `docs/superpowers/plans/2026-11-06-td-0100-entry-tool-version.md`, lists it); blind: no
- cover: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs` | `SidecarComparerTests.IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys` (row `"h", "app", true`, line 447)
- proposal: Delete the fact; the theory's first row already asserts that an entry is current for its own hash, database and tool version.
- verdict: confirmed
- verifier's reason: The fact at tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs:463-465 makes the call `Entry(hash: "h", database: "app", toolVersion: "1.2.3").IsCurrentFor("h", "app", "1.2.3")` and expects true; the row `[InlineData("h", "app", true)]` at :447 of `IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys` builds the same entry (:455) and makes the same call with the same version (:456) and the same expectation, so it proves the same behaviour on the same inputs.  `kills` on generator-snapshot: the fact has 0 kills, 0 timeouts, 0 survivors under it; the theory kills the two mutants of src/SqlSource/Snapshot/SidecarEntry.cs:50:9 through its false rows, which a true-expecting call cannot notice, as expected.  No comment above the fact or its group (the comment at :448 is on the row `"h", "APP"`); the documents name `IsCurrentFor` the method (docs/architecture/tool.md, docs/architecture/sidecar-format.md), never this test; only the plan docs/superpowers/plans/2026-11-06-td-0100-entry-tool-version.md:60 lists it, which is not a record.

### F-SqlSource.Tests.Snapshot-1-05

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_EntryWithoutRows_HasNoColumns` | rows: all
- action: delete
- evidence: measured (1 row; kills 154, alone 0, timeouts under it 11; cover `SidecarRoundTripTests.Write_AnySidecar_ReadsBackEqualAndWritesTheSameTextAgain`) and read (SidecarReaderTests.cs:1107-1126 reads a hand-written SQL Server entry with `resultKind: none` and no `columns` key and asserts the whole sidecar, so that `Columns` is null.  `Read_UsersExample_ReadsAnEntryWithoutRows` (:122-146) asserts the whole of such an entry, `DeleteUser`, with `SidecarResultKind.None` and `Columns` null, and `Read_OrdersExample_ReadsTheShapeOfSqlServerTypes` asserts the same of the SQL Server entry `ArchiveOrder` (:237); the round trip writes and reads entries without rows on both engines.  The timeouts under it are on the reader's shared paths, which the cover reaches with 21 timeouts under it.  No comment above the test; no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it); blind: no
- cover: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_UsersExample_ReadsAnEntryWithoutRows`; `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_OrdersExample_ReadsTheShapeOfSqlServerTypes`
- proposal: Delete the fact; reading an entry without rows as one with no columns stays proved on each engine by the two example tests.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs:1107-1126 reads a SQL Server entry with `resultKind: none`, `parameters: []` and no `columns` key and asserts the whole sidecar, `Columns` null by tests/SqlSource.Testing/Snapshot/TestSidecars.cs:105.  `Read_UsersExample_ReadsAnEntryWithoutRows` (:122-146) asserts the whole `DeleteUser` entry, which in tests/SqlSource.Tests/Snapshot/Examples/Users.sql.json has no `columns`, `matchesTable`, `plan` or `tableMatch` key, with `None` and `Columns` null; `Read_OrdersExample_ReadsTheShapeOfSqlServerTypes` asserts `Columns` null for `ArchiveOrder` (:237), the SQL Server entry of Orders.sql.json with the same keys.  `kills`: of its 154 kills only SidecarReader.cs:169:71 (the file's `toolVersion`) and SidecarTokenizer.cs:303:20 (an empty array) are not killed by those two, both incidental to entries without rows and killed by 6 and 17 other tests; its 11 timeouts are all under `Read_UsersExample_ReadsAnEntryWithoutRows`.  No comment above it; only the plan docs/superpowers/plans/2026-10-09-query-generation-phase-2-1-snapshot-format.md:2577 holds it.

### F-SqlSource.Tests.Snapshot-1-06

- test: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs` | `SidecarComparerTests.FindDifference_TwoDifferences_IsTheFirstInTheOrder` | rows: all
- action: strengthen
- evidence: measured (survivors under it: src/SqlSource/Snapshot/SidecarComparer.cs:72:17 Null coalescing mutation (left to right); src/SqlSource/Snapshot/SidecarComparer.cs:73:20 Null coalescing mutation (left to right); src/SqlSource/Snapshot/SidecarComparer.cs:74:20 Null coalescing mutation (left to right); src/SqlSource/Snapshot/SidecarComparer.cs:144:16 Null coalescing mutation (left to right)) and read (the comparer's summary, SidecarComparer.cs:6-12, documents the order as each parameter's and column's `name`, `ordinal`, `type` and `nullable`, and its comment at :139-141 compares a type's name and bind name "before the shape's own keys".  The test, SidecarComparerTests.cs:363-444, holds that order for the entry's keys and for a column's five keys (:397-432), but no row changes two keys of one parameter, or a type's bind name with one of its shape's keys, so a reordered parameter or type comparison passes it); blind: no
- cover: none
- proposal: Add rows that change two keys of one parameter (ordinal and type, type and nullable) and one that changes a parameter type's bind name and its kind, each expecting the earlier key's path.
- verdict: confirmed
- verifier's reason: src/SqlSource/Snapshot/SidecarComparer.cs:6-12 and docs/architecture/sidecar-format.md:108 promise each parameter's `name`, `ordinal`, `type` and `nullable` in that order, and :139-141 the type's name and bind name before the shape's keys.  The test, tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs:363-444, changes two keys of one column (:397-432) but never two keys of one parameter (:386-396 changes a parameter's nullable and a column), nor a bind name with a shape key, so the right-associative `??` chains at :72-75 and :144 can be reordered unseen; the survivors 72:17, 73:20, 74:20 and 144:16 are each a swap of that order.  One precision for the cleanup: 72:17 swaps `name` with the rest, so it needs a row changing a parameter's `name` and another key; the proposal's rows (ordinal and type, type and nullable, bind name and kind) kill 73:20, 74:20 and 144:16 but not it.
- survivors to kill: `src/SqlSource/Snapshot/SidecarComparer.cs:144:16`, `src/SqlSource/Snapshot/SidecarComparer.cs:72:17`, `src/SqlSource/Snapshot/SidecarComparer.cs:73:20`, `src/SqlSource/Snapshot/SidecarComparer.cs:74:20`

### F-SqlSource.Tests.Snapshot-1-07

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_NullUnderEveryKeyAReaderCanDoWithout_ReadsNull` | rows: all
- action: strengthen
- evidence: measured (survivors of the keys it promises: src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:271:63 Boolean mutation, under 7 methods; src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:295:76 Boolean mutation, under `SidecarReaderTests.Read_SqlServerType_ReadsItsFacetsAndItsUserType` and `SidecarRoundTripTests.Write_AnySidecar_ReadsBackEqualAndWritesTheSameTextAgain`) and read (the name promises every key a reader can do without, but its text, SidecarReaderTests.cs:410-456, holds only PostgreSQL types; a SQL Server type's `userType`, which the reader takes as null (SidecarTypeReader.cs:271, `orNull: true`), and a user type's `assemblyQualifiedName` (:295) are never read as null by any test, since the writer leaves both out when they have no value); blind: no
- cover: none
- proposal: Add a SQL Server column, or a second case, whose type has `'userType': null`, and one whose user type has `'assemblyQualifiedName': null`, and assert both read as null.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs:410-456 holds only a PostgreSQL entry.  A SQL Server type's `userType` is read with `orNull: true` (src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:271) and a user type's `assemblyQualifiedName` with `orNull: true` (:295), both keys a reader can do without by docs/architecture/sidecar-format.md:39, yet src/SqlSource/Snapshot/SidecarWriter.cs:236-243 leaves both out when they have no value, so no test, the round trip included, reads either as null; the survivors 271:63 and 295:76 in the evidence are exactly those.  The test's name promises every such key, so the finding reads it rightly.
- survivors to kill: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:271:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:295:76`

### F-SqlSource.Tests.Snapshot-1-08

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_TypeWithAValueOfAnotherJsonType_IsWrongTypeAtTheValue` | rows: all
- action: strengthen
- evidence: measured (survivors under it: src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:228:59 Boolean mutation; src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:237:63 Boolean mutation; src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:240:59 Boolean mutation, each under 11 methods) and read (the theory treats null under a SQL Server facet as a value of another type, with the row `'maxLength': null` at SidecarReaderTests.cs:600-606, which kills that key's mutant; a null `name`, `precision` or `scale` of a SQL Server type is in no row, and `Read_NullUnderAKeyAReaderCannotDoWithout_IsWrongTypeAtTheNull` reads only PostgreSQL types, so a reader that took null for any of the three passes); blind: no
- cover: none
- proposal: Add `mssql` rows like the one at line 600 with `'name': null`, `'precision': null` and `'scale': null`, each expecting `WrongType` at the null.
- verdict: confirmed
- verifier's reason: The theory at tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs:557-629 holds null under a SQL Server facet as a value of another type only for `maxLength` (:600-606), which kills SidecarTypeReader.cs:234:63.  `name`, `precision` and `scale` of a SQL Server type are read with `orNull: false` at src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:228, :237 and :240, and no row nulls them; `Read_NullUnderAKeyAReaderCannotDoWithout_IsWrongTypeAtTheNull` (:459-484) changes only the PostgreSQL `Valid`.  Under the survivor 228:59 a null name gives `name is null`, and :255 returns a null type with no error, so the file reads with a column of no type; docs/architecture/sidecar-format.md:39-47 makes these keys a reader cannot do without.
- survivors to kill: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:228:59`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:237:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:240:59`

### F-SqlSource.Tests.Snapshot-1-09

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_BindNameThatIsNotAString_IsWrongTypeAtTheValue` | rows: all
- action: strengthen
- evidence: measured (survivors: src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:98:63 Boolean mutation, under 14 methods with this one; src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:231:63 Boolean mutation, under 3 methods; src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:320:63 Boolean mutation, under 4 methods) and read (the fact, SidecarReaderTests.cs:1215-1226, gives a PostgreSQL bind name the number 3; null is not a string either, the reader refuses it on each engine (`orNull: false` at SidecarTypeReader.cs:98, :231 and :320) and the schema test row `"bindName": null` (SidecarSchemaTests.cs:79) holds it invalid, yet no test reads a null bind name, and the SQL Server and other-engine readers' bind names are not tested for a wrong type at all); blind: no
- cover: none
- proposal: Make it a theory over the engine and the value, adding `null` on `postgres`, `mssql` and `sqlite` parameter types, each expecting `WrongType` at the value with the argument `bindName`.
- verdict: confirmed
- verifier's reason: The fact at tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs:1215-1226 gives only a PostgreSQL bind name the number 3, which `ReadString` refuses whatever `orNull` is, so the `orNull: false` of src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:98, :231 and :320 survives.  docs/architecture/sidecar-format.md:39 names `bindName` as one of the two keys a reader refuses as `null`, and :75 makes writing it as `null` a breaking change, which tests/SqlSource.Tests/Snapshot/SidecarSchemaTests.cs:79 holds for the schema; no reader test reads a null bind name, and grep of the file finds no wrong-type row for a SQL Server or other-engine bind name.
- survivors to kill: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:231:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:320:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:98:63`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-snapshot`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-48/generator-snapshot/before --tests 'FullyQualifiedName~SqlSource.Tests.Snapshot.' 'Snapshot/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-48/generator-snapshot/after --tests 'FullyQualifiedName~SqlSource.Tests.Snapshot.' 'Snapshot/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json artifacts/test-cleanup/phase-48/generator-snapshot/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarComparerTests.IsCurrentFor_SameHashDatabaseAndVersion_IsTrue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarReaderTests.Read_EntryWithoutRows_HasNoColumns
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarComparerTests.FindDifference_TwoDifferences_IsTheFirstInTheOrder
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarReaderTests.Read_NullUnderEveryKeyAReaderCanDoWithout_ReadsNull
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarReaderTests.Read_TypeWithAValueOfAnotherJsonType_IsWrongTypeAtTheValue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-48/generator-snapshot/before/reports/mutation-report.json SidecarReaderTests.Read_BindNameThatIsNotAString_IsWrongTypeAtTheValue
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Snapshot-1-06: `src/SqlSource/Snapshot/SidecarComparer.cs:144:16`, `src/SqlSource/Snapshot/SidecarComparer.cs:72:17`, `src/SqlSource/Snapshot/SidecarComparer.cs:73:20`, `src/SqlSource/Snapshot/SidecarComparer.cs:74:20`
- F-SqlSource.Tests.Snapshot-1-07: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:271:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:295:76`
- F-SqlSource.Tests.Snapshot-1-08: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:228:59`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:237:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:240:59`
- F-SqlSource.Tests.Snapshot-1-09: `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:231:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:320:63`, `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs:98:63`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-snapshot`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-48/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-48/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-48/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-48/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-48/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-48/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 14
- `src/SqlSource/Snapshot/AGENTS.md`: 6

## Left out

- F-SqlSource.Tests.Snapshot-1-03 (delete, `SidecarReaderTests.Read_FileWithACommentOrATrailingComma_IsInvalidJson`): refuted; the appendix keeps the reason.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
