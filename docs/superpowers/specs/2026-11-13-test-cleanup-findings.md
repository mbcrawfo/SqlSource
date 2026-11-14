# Test cleanup findings of 2026-11-13

The appendix of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), and the memory between runs: the next review is given this file, and what it refuted or kept is not raised again unless the test or its evidence has changed.  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`; every line number here is of that commit.

## Counts

By slice, action and verdict.  `measured` is a finding measured again after its verdict.  A slice of no findings had no verifier.

| Slice | Delete | Fold | Strengthen | Loosen | Demote | Keep | Confirmed | Refuted | Needs measurement |
|----|----|----|----|----|----|----|----|----|----|
| SqlSource.Tests-1 | 2 |  | 2 | 1 |  | 5 | 10 |  |  |
| SqlSource.Tests.Generation-1 | 7 | 1 | 4 | 2 |  | 11 | 24 |  | 1 (measured: confirmed) |
| SqlSource.Tests.Generation.Emitting-1 | 4 | 2 | 5 |  |  | 4 | 15 |  |  |
| SqlSource.Tests.Generation.Emitting.QueryMethods-1 | 3 | 2 | 2 | 3 |  | 2 | 12 |  |  |
| SqlSource.Tests.Generation-2 | 1 | 4 | 6 |  |  | 2 | 13 |  |  |
| SqlSource.Tests.Generation-3 |  | 2 | 6 |  |  | 9 | 17 |  |  |
| SqlSource.Tests.Generation.Types-1 | 7 | 1 | 1 |  |  | 1 | 10 |  |  |
| SqlSource.Tests.Generator-1 | 1 | 1 |  | 2 |  | 6 | 10 |  |  |
| SqlSource.Tests.Generator-2 | 1 | 3 |  | 2 |  |  | 6 |  |  |
| SqlSource.Tests.Generator-3 |  |  |  | 3 |  | 1 | 4 |  |  |
| SqlSource.Tests.Generator-4 | 1 |  | 1 | 1 |  |  | 3 |  |  |
| SqlSource.Tests.Generator-5 | 1 | 5 |  | 1 |  |  | 7 |  |  |
| SqlSource.Tests.Generator-6 | 8 | 2 |  | 2 |  |  | 12 |  |  |
| SqlSource.Tests.Generator-7 | 3 |  |  |  |  |  | 3 |  |  |
| SqlSource.Tests.Generator-8 |  | 2 |  | 1 |  |  | 3 |  |  |
| SqlSource.Tests.Package-1 | 2 |  | 1 |  |  |  | 3 |  |  |
| SqlSource.Tests.Parsing-1 | 2 | 2 | 6 | 1 |  | 3 | 14 |  |  |
| SqlSource.Tests.Parsing-2 | 10 | 1 | 5 |  |  | 4 | 20 |  |  |
| SqlSource.Tests.Settings-1 | 4 | 2 | 13 |  |  | 5 | 24 |  |  |
| SqlSource.Tests.SettingsFiles-1 |  |  |  |  |  | 6 | 6 |  |  |
| SqlSource.Tests.Snapshot-1 | 5 |  | 4 |  |  | 1 | 9 | 1 |  |
| SqlSource.Tests.TestHarness-1 | 1 |  | 1 | 1 |  |  | 3 |  |  |
| SqlSource.Tests.RoslynFloor-1 |  |  |  |  |  |  |  |  |  |
| SqlSource.Repository.Tests-1 | 1 |  | 1 |  |  |  | 2 |  |  |
| SqlSource.Repository.Tests-2 | 1 |  |  |  |  |  | 1 |  |  |
| SqlSource.Tool.Tests-1 | 2 |  |  |  |  |  | 2 |  |  |
| SqlSource.Tool.Tests.Bundles-1 |  | 2 |  | 1 |  | 4 | 7 |  |  |
| SqlSource.Tool.Tests.Bundles.Renaming-1 | 2 |  | 3 |  |  | 5 | 8 | 2 |  |
| SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1 | 4 |  |  |  |  | 2 | 4 | 2 |  |
| SqlSource.Tool.Tests.Commands-1 | 6 | 3 | 1 |  |  | 1 | 11 |  |  |
| SqlSource.Tool.Tests.Commands-2 | 4 | 3 | 1 | 6 |  | 5 | 18 | 1 |  |
| SqlSource.Tool.Tests.Describing-1 | 2 | 2 | 1 | 2 |  | 1 | 8 |  |  |
| SqlSource.Tool.Tests.Describing.Describers-1 | 4 |  | 1 |  |  | 6 | 10 | 1 |  |
| SqlSource.Tool.Tests.Describing.Postgres-1 | 7 |  |  |  |  | 6 | 13 |  |  |
| SqlSource.Tool.Tests.Describing.Postgres-2 | 4 | 1 | 1 |  |  |  | 6 |  |  |
| SqlSource.Tool.Tests.Describing.Postgres.Walk-1 | 3 | 2 | 4 |  |  | 3 | 12 |  |  |
| SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1 | 1 |  | 2 |  |  | 2 | 5 |  |  |
| SqlSource.Tool.Tests.Describing.SqlServer-1 | 4 |  | 1 |  |  |  | 5 |  |  |
| SqlSource.Tool.Tests.Describing.SqlServer-2 | 3 |  | 4 |  |  | 1 | 8 |  |  |
| SqlSource.Tool.Tests-2 | 3 | 2 | 1 |  |  | 5 | 11 |  |  |
| SqlSource.Tool.Tests-3 | 3 |  | 2 |  |  | 4 | 9 |  |  |
| SqlSource.Tool.Tests.Projects-1 | 5 |  | 3 |  |  |  | 8 |  |  |
| SqlSource.Tool.Tests-4 | 1 |  | 5 |  |  | 1 | 7 |  |  |
| DatabaseTests.Postgres-1 | 2 |  |  |  |  |  | 2 |  |  |
| DatabaseTests.Postgres.Describer-1 | 3 | 1 |  |  | 4 |  | 8 |  |  |
| DatabaseTests.Postgres.Describer-2 |  | 1 |  | 1 | 1 |  | 3 |  |  |
| DatabaseTests.Postgres.EndToEnd-1 | 13 | 2 |  |  |  |  | 15 |  |  |
| DatabaseTests.Postgres.EndToEnd-2 | 2 |  |  |  |  |  | 2 |  |  |
| DatabaseTests.Postgres-2 | 1 |  |  |  |  |  | 1 |  |  |
| DatabaseTests.Postgres.Npgsql8-1 |  | 2 | 1 |  |  |  | 3 |  |  |
| DatabaseTests.Postgres.SyncAsync-1 |  |  |  |  |  |  |  |  |  |
| DatabaseTests.SqlServer-1 | 1 |  |  | 1 | 2 |  | 4 |  |  |
| DatabaseTests.SqlServer.EndToEnd-1 | 1 | 1 |  |  |  |  | 2 |  |  |
| DatabaseTests.SqlServer.EndToEnd-2 | 3 | 3 |  |  |  |  | 6 |  |  |
| DatabaseTests.SqlServer.EndToEnd-3 |  |  |  |  |  |  |  |  |  |
| DatabaseTests.SqlServer-2 | 2 |  |  |  |  |  | 2 |  |  |
| DatabaseTests.SqlServer.SqlClient61-1 |  |  | 1 |  |  |  | 1 |  |  |
| DatabaseTests.SqlServer.SyncAsync-1 |  |  |  |  | 1 |  | 1 |  |  |
| DatabaseTests.QueryMethodAllocation-1 |  |  |  |  |  |  |  |  |  |
| DatabaseTests.QueryMethodAllocation.SqlServer-1 |  |  |  |  |  |  |  |  |  |
| mirror: engines | 5 | 3 | 1 |  | 2 |  | 11 |  |  |
| mirror: generator |  |  | 1 |  |  |  | 1 |  |  |
| mirror: describers | 2 |  |  |  |  |  | 2 |  |  |
| **Total** | **158** | **58** | **92** | **31** | **10** | **106** | **447** | **7** | **1** |

Of these, 342 are in a phase (152 delete, 57 fold, 92 strengthen, 31 loosen, 10 demote), and the rest are below: 105 keeps, 7 refuted and 1 withdrawn.  F-SqlSource.Tests.Generation-1-09, the one finding left at **needs measurement**, was measured again: the scope `generator-generation-chains` without its test lost nothing (131 detected before, 131 still detected, 0 lost, 0 not comparable), so it is confirmed and in phase 36.

## Refuted

Each with the verifier's reason, which stops the next run raising it again.

### F-SqlSource.Tests.Snapshot-1-03

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_FileWithACommentOrATrailingComma_IsInvalidJson` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` | `SidecarReaderTests.Read_TextThatIsNotOneJsonObject_IsInvalidJson` (rows at lines 789, 794 and 799)
- proposal: Delete the theory and move its one-line comment above the three rows of `Read_TextThatIsNotOneJsonObject_IsInvalidJson` that hold a comment or a trailing comma.
- verdict: refuted
- reason: The measurement holds: `kills` of `Read_FileWithACommentOrATrailingComma_IsInvalidJson` (9) and its 4 timeouts are all also under `Read_TextThatIsNotOneJsonObject_IsInvalidJson`, whose rows at tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs:789-803 assert more.  But the comment above the test, "The settings files read comments and trailing commas; a sidecar is strict JSON" (:826), records a decision, the one src/SqlSource/Snapshot/AGENTS.md makes a rule ("The sidecar is strict JSON") and docs/architecture/generator.md:433 states ("Only this reader sets that mode, so a sidecar stays strict JSON"); the test was added with the lenient mode (commit fdaf186d) as its guard, while the cover's rows predate that mode (commit 40bc5988) and no comment or document names them as that record.  By the record rule a delete is refuted while the cover is not named as the record; the proposal's move of the comment would be a different finding (a fold that labels the three rows), not a delete on the kills.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-04

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.Keep_SpellingsTheEngineTakesForOne_MapToTheFirstKept` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.Keep_ThenName_StaysVerbatim`
- proposal: Delete it; or, to keep the named pair with `Keep_SpellingsTheEngineTellsApart_KeepEachAsWritten`, drop line 141 from `Keep_ThenName_StaysVerbatim` instead, so the check is made once.
- verdict: refuted
- reason: The named cover does not prove the same behaviour.  `Keep_ThenName_StaysVerbatim` asks `Name("pg_catalog")` (NameMappingTests.cs:139) before `Name("PG_CATALOG")` (:141), and under `Mapping()` both have the exact spelling `PG_CATALOG`.  If `Store` stopped recording the kept spelling, `_ = kept.TryAdd(exact, spelling)` at NameMapping.cs:174, line 139 would itself write `kept["PG_CATALOG"] = "pg_catalog"` through NameMapping.cs:72-76, and line 141 would still pass; `Keep_SpellingsTheEngineTakesForOne_MapToTheFirstKept` asks `PG_CATALOG` first (NameMappingTests.cs:286-288) and would get `"PG_CATALOG"` and fail.  So this test alone proves that `Keep` maps another spelling to the first kept one.  Mutation cannot see this: the report holds no mutant on NameMapping.cs:174 (its mutants are on lines 163, 165, 168, 169 and 175), and `Keep_SpellingsTheEngineTellsApart_KeepEachAsWritten` (the cover by the numbers) passes too without line 174.  The other proposal, dropping line 141 from `Keep_ThenName_StaysVerbatim`, loses nothing, but it is not the delete this finding proposes.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-07

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.IsMapped_AnotherCaseOfAMappedName_IsTrue` | rows: all
- action proposed: keep
- cover proposed: none
- proposal: Keep it.
- verdict: refuted
- reason: The finding's stated guard is wrong.  `Name("email")` on `CaseKeeping()` stores the name under its fold `EMAIL` (NameMapping.cs:68, 86), and the test asks `IsMapped("EMAIL")` (NameMappingTests.cs:305-307).  `EMAIL` is the fold, so an `IsMapped` that looks up `spelled(spelling, quoted)` instead of the fold at NameMapping.cs:128 still finds `_byKey["EMAIL"]` and the test passes; it does not "alone fail if IsMapped compares by the spelling as written".  What it does fail on is an `IsMapped` that requires the exact spelling among a name's spellings, since only `email` was named; the cover `Keep_AfterName_KeepsTheReplacement` asks in the case it named (NameMappingTests.cs:178-183) and would not fail.  To guard the lookup by fold it needs a spelling that is neither the fold nor the one named, such as `IsMapped("Email")`.  The test is still not removable: it is the one that asks a name in another case than the one named, and the cover of F-05 (NameMappingTests.cs:308).

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-02

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs` | `SqlServerFixtureRenamingTests.RenameAsync_RelationsOfSystemDatabases_KeepOnlyTheNamesOfSys` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs` | `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersUnderItsOwnName_KeepsTheServersNames` (row `OwnName`); `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs` | `SqlServerRenamingTests.IsEngineWord_TheServersOrTheToolsWord_IsTrue` (row `"msdb", false`)
- proposal: Delete the test; the same renaming of `select-other-database` is asserted by the `OwnName` row of `KeepsTheServersNames`, whose leak search finds `sysjobs` and `job_id` and whose assertions keep the names of sys.
- verdict: refuted
- reason: The named cover does not prove that `sysjobs` and `job_id` are renamed.  The row `OwnName` of `KeepsTheServersNames` (`SqlServerErrorRenamingTests.cs:281-300`) checks them only through `SqlServerBundleLeaks.Find`.  That search skips every word for which the part under test's `engine.IsEngineWord` is true (`tests/SqlSource.Tool.Testing/Bundles/SqlServerBundleLeaks.cs:53-61`), so it cannot see a user's word that the vocabulary keeps.  This test asserts `ShouldNotContain("sysjobs"/"job_id")` on the query and the capture directly (`SqlServerFixtureRenamingTests.cs:80-84`), whatever the vocabulary says.  `kills` on tool-bundles-renaming shows the gap: this test kills `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs:77:13 Logical mutation` and `:192:16 Boolean mutation`, both of which make every word an engine word.  `KeepsTheServersNames` does not kill them, because its leak search goes blind with the renamer.  The evidence's set covers them only through `RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`'s direct `ShouldNotContain(Server)` (`:272-273`), on another word, and the finding does not name that test.  A wrong entry in `SqlServerVocabulary` is a narrower regression that mutation cannot make, since the lists are static and unmutated.  If `sysjobs` were added there, against `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` ("Never put a word of the user's in the vocabulary"), it would pass every cover and fail only here.  This test is the one direct guard of the two words that `docs/architecture/bundles.md:369` names in the decision of 2026-11-05.  `RenameAsync_AnAliasOfAColumnOfTheServers_IsRenamed`'s row `job_id` (`:185`) holds `job_id` directly but not `sysjobs`.  The row `("msdb", false)` of `IsEngineWord_TheServersOrTheToolsWord_IsTrue` is at `SqlServerRenamingTests.cs:58`, not `:57`.

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-03

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_EveryStringOfTheFixtures_HasARule` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs` | `SqlServerFixtureRenamingTests.RenameAsync_Fixture_GivesABundleThatReplaysToItsRenamedEntry`; `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_EveryStringMemberOfEveryCall_HasARule`
- proposal: Delete the test, and in the same change say in `docs/architecture/bundles.md:335` and `docs/architecture/sqlserver-describer.md:98` that a fixture's string with no rule fails the renaming of that fixture in `SqlServerFixtureRenamingTests`.
- verdict: refuted
- reason: Two documents name `SqlServerFieldsTests` as the check that fails for a string of a committed fixture's capture with no rule: `docs/architecture/bundles.md:335` and `docs/architecture/sqlserver-describer.md:98` ("so recording the fixtures again after such a change finds a missed member").  They name it as the record of that guard, and neither names the proposed cover, `SqlServerFixtureRenamingTests`.  The finding's own proposal has to rewrite both documents.  The class comment (`SqlServerFieldsTests.cs:16-18`) lists "a rule for every string that the SQL Server fixtures' captures hold" as one of its checks.  `docs/superpowers/plans/2026-11-08-td-0108-renaming-tables.md:96` records the decision that "the fixture test ... stay[s]" beside the walk of the records.  By reading, the cover would fail on such a string: `CaptureRenamer.Rename` visits every string of request and answer and refuses one with no rule (`src/SqlSource.Tool/Bundles/Renaming/CaptureRenamer.cs:97-120`), and SQL Server's table has no `Drop`.  `RawBundles.OfFixture` (`tests/SqlSource.Tool.Testing/Bundles/RawBundles.cs:30-40`) keeps the capture as recorded.  But under the rule for a named record, a delete is refused unless the cover is named as that record too.  `kills`: 3 kills (`JsonStrings.cs:59:37`, `:81:36`, `SqlServerFields.cs:91:36`), each killed by 25 or more other methods; this changes nothing.

### F-SqlSource.Tool.Tests.Commands-2-08

- test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` | `UsageCheckTests.Check_TokenThatStartsWithAHyphenAndIsFarFromEveryOption_IsAnOptionNamedByItsPosition` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` | `UsageCheckTests.Check_UnknownOptionFarFromEveryOption_IsNamedByItsPosition`
- proposal: Delete the one-row theory.
- verdict: refuted
- reason: UsageCheckTests.cs:96-99 gives `describe -s3cret`, where the path argument (arity ZeroOrOne, :21) is still open; the cover, `Check_UnknownOptionFarFromEveryOption_IsNamedByItsPosition` (:101-105), gives `describe App.csproj -pS3cret`, where it is filled.  What the name promises, that a hyphen token is read as an option even where the path could stand, so that a secret typed as `-p<value>` is never taken for a path and printed, is the input that matters, and the cover cannot show it: a change that took such a token for the argument while a slot is open (src/SqlSource.Tool/UsageCheck.cs:71 and 131-135 decide it) passes the cover and fails this test.  The finding's reading that the cover "shows the same reading where an argument could not stand either" is backwards.  The lines are blind (UsageCheck.cs:72-127), so no number settles it.  Not raised again unless a cover is named that gives a single-hyphen token far from every option where an argument may stand on this class's own root; `SecretTests.Run_SecretTypedAsAShortOption_IsNamedByItsPosition` does so only through the tool's commands, which this class keeps apart on purpose (UsageCheckTests.cs:10-12).

### F-SqlSource.Tool.Tests.Describing.Describers-1-05

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberEventsTests.cs` | `DescriberEventsTests.TableOf_SchemaAndTable_IsAMapOfThemApart` | rows: all
- action proposed: delete
- cover proposed: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberEventsTests.cs` | `DescriberEventsTests.TableMembers_SchemaAndTable_AreTheTwoMembersInOrder`; `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberEventsTests.cs` | `DescriberEventsTests.TableOf_NoSchema_HasANullSchema`
- proposal: Delete the test; the two covers stay, and each is the other's cover in the evidence, so neither of them goes.
- verdict: refuted
- reason: The named covers do not prove what this test proves on the input that matters.  `TableOf` (DescriberEvents.cs:28) must pass a given schema through to its map.  `TableMembers_SchemaAndTable_AreTheTwoMembersInOrder` (DescriberEventsTests.cs:26-33) never calls `TableOf`.  `TableOf_NoSchema_HasANullSchema` (:40-41) passes a null schema.  So a `TableOf` that drops or nulls the schema (for example `MapOf(TableMembers(null, table))`) passes both covers and fails only this test (:36-37).  `TableMatchTail_ATable_HasTheVerdictAndTheTable` (:72) cannot catch it either, since it compares against `TableOf` itself.  Stryker makes no such mutant, so the equal kills (DescriberEvents.cs:23:14 and 23:37, both string mutations of `TableMembers`) cannot show the gap.  Tests outside the slice assert a table map that has a schema, such as SqlServerEventsTableTests.cs:71 and PostgresEventsRelationTests.cs:64-65.  A later run would have to read them and name them as the cover before this test could go.

## Keeps

Each **keep** is a test that a measurement made look removable and that reading showed is not; the finding's evidence says why, and its verifier confirmed it.  The first entry is withdrawn from the cleanup by the writer's rule that a cover stays.

### F-engines-09 (withdrawn fold)

- test: `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs` | `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` | rows: `[InlineData("read uncommitted")]` (line 340), `[InlineData("read committed")]` (341), `[InlineData("repeatable read")]` (342), `[InlineData("serializable")]` (343), `[InlineData("snapshot")]` (344)
- why it stays: F-DatabaseTests.SqlServer.EndToEnd-2-06 names `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing` as its cover (for the ItemIndex and empty table of its dropped rows), and this fold, with F-DatabaseTests.SqlServer.EndToEnd-2-04, would drop every row of it.  The two findings lean on each other; F-DatabaseTests.SqlServer.EndToEnd-2-06 only drops rows of its own theory and keeps one row of every level, so its theory, which also asserts the restore of the level, stays a test, and so does this one.
- the finding's evidence: read (twin fault of F-DatabaseTests.Postgres.EndToEnd-2-02, whose evidence says SQL Server's theory "is not affected, because its levels also restore the session's level"; the code does not bear that out, and F-DatabaseTests.SqlServer.EndToEnd-2-04 drops only the `on` row.  On SQL Server the level copies differ from `InsertNoteSync` (Queries/Batches.sql:21-23, `transaction=on` by default) only in the argument of `BeginTransaction` (src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs:142-146, :155-167): the restore of the session's level is given to every mode that begins its own transaction, `On` included (ExecutionCapabilityRules.cs:121-125, `BeginsItsOwn` :141-150), so `InsertNoteSync` has it too, and its switch is over the level read before, not the copy's (F-DatabaseTests.SqlServer.EndToEnd-1-02's verdict).  The theory (SynchronousBatchTests.cs:338-358) makes the four assertions of `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing` (:322-336) on the same input, ItemIndex 1200, number 2627, an empty table and no transaction, and asserts nothing of the level.  That each copy begins at its level is `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` (:740-756); that an item's duplicate key rolls back each copy's transaction, with the level set back, is `InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` (:776-793, ItemIndex 2, count 0).  A duplicate key in one session is a statement error at every level, with no concurrency here to tell serializable or snapshot apart.  The comment above the theory (:338) lists the rows and records nothing; nothing in the searched documents names the test; README.md:185 lists "a duplicate key in the second chunk under `on`, each isolation level and `transaction=off`" as coverage.  6 rows, 0.317 s.  No mutation scope ran the slice.); blind: no
- the finding's proposal, not applied: Drop the five rows; with F-DatabaseTests.SqlServer.EndToEnd-2-04's `on` row the theory goes whole, and README.md:185 drops "each isolation level" from the duplicate-key clause.  F-DatabaseTests.SqlServer.EndToEnd-2-06 names this theory as a second cover, but the same level's other row of its own theory keeps it.  The asynchronous theories of both engines (Postgres `BatchQueryTests.cs:397-402`, SQL Server `BatchQueryTests.cs:492-511`) hold the same rows and have no finding, and F-DatabaseTests.SqlServer.EndToEnd-1-02's verdict cites SQL Server's as extra support, so the cleanup judges them in the same change.
- verdict: confirmed; reason: No mutation scope (evidence/DatabaseTests.SqlServer.EndToEnd-2.md:253: 6 rows, 0.317 s).  The theory (tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs:338-358) runs `DuplicateInTheSecondChunk()` (:1097-1103) through `InsertAtLevel` (:1003-1018) and asserts ItemIndex 1200, number 2627, no notes and `@@TRANCOUNT` 0.  The finding's correction of the PostgreSQL twin is right: `RestoresSessionIsolation` is given to every mode that begins its own transaction, `On` included (src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityRules.cs:121-125, `BeginsItsOwn` :141-151), so `InsertNoteSync` (Queries/Batches.sql:21-28, default `on`) has the same read and restore, and the copies differ from it in the generated method only by the argument of `BeginTransaction` (CollectionMethods/BatchWriter.cs:142-146, :155-167) and their SQL's output column, which plays no part in the throw.  `InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing` (:322-336) makes the same four assertions on the same input; `InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel` (:740-756) proves each copy begins at its level; `InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound` (:776-793) proves each copy's rollback after a duplicate key and the restore, and after F-DatabaseTests.SqlServer.EndToEnd-2-06 drops its equal-level rows `LevelsAndSessionLevels` (:1022-1041) still keeps one row of every level.  A duplicate key in one session is the same statement error at every level, with no concurrency in the test.  The comment above the theory (:338) lists the rows only; my grep of the documents finds the name nowhere, and README.md:185 lists "each isolation level" as coverage, which the proposal edits.  A fold that drops rows of a theory cannot be measured by a filter, since the rows share a name: confirmed by reading, and the phase's own guard proves it.

### F-SqlSource.Tests-1-06

- test: `tests/SqlSource.Tests/EquatableArrayTests.cs` | `EquatableArrayTests.GetHashCode_SameItemsInSeparateInstances_IsTheSame` | rows: all
- evidence: measured (covers 4 mutants and kills none; survivors src/SqlSource/EquatableArray.cs:38:30 Arithmetic mutation and src/SqlSource/EquatableArray.cs:38:31 Arithmetic mutation under it alone).  Read: EquatableArrayTests.cs:31-33 asserts what its name says, equal hashes for equal items in separate instances, and both surviving mutants keep that true, so they are not its to kill.  What it guards is a hash of the instance rather than of the items, as `ImmutableArray<T>.GetHashCode` is, which would break the pipeline's caching and which no mutant produces; blind: no
- verdict: confirmed
- reason: `kills` on generator-root shows 0 kills, with SURVIVED EquatableArray.cs:38:30 (`-`) and 38:31 (`hash / 31`).  Both still hash equal items to equal values, so EquatableArrayTests.cs:31-33 is right to pass on them, and neither is a defect of the test.  It asserts what its name says.  It guards an items-based hash, not a hash of the instance, which the pipeline's caching needs (src/SqlSource/AGENTS.md, "Models need value equality") and which no mutant produces.  `Default_OfTheArray_BehavesAsEmpty` (EquatableArrayTests.cs:36-44) compares the hashes of two empty arrays only.  Keep is right.

### F-SqlSource.Tests-1-07

- test: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` | `SettingsFileDiagnosticsTests.Create_EachKind_GivesItsDescriptor` | rows: all
- evidence: measured (7 rows; covers 9 mutants and kills none).  Read: SettingsFileDiagnosticsTests.cs:20-39 is the one test that holds every `SettingsFileErrorKind` to its descriptor id, SQLSRC015 for two kinds; the switch it tests, SettingsFileDiagnostics.cs:54-97, is on lines the evidence lists as blind, does not compile, so no mutant could change which descriptor a kind gets; blind: yes
- verdict: confirmed
- reason: `kills` on generator-diagnostics shows 7 rows and 0 kills.  The switch that picks a descriptor for each kind, SettingsFileDiagnostics.cs:54-97, is on lines that the evidence lists as blind (does not compile), so no mutant could change the descriptor a kind gets.  SettingsFileDiagnosticsTests.cs:20-39 is the one test that holds all seven kinds to their ids.  `Create_GeneratorParameterInTwoStates_IsARepeatAtTheSecondElement` (:85) checks SQLSRC020 only, and the message tests check text but not ids.  Keep is right.

### F-SqlSource.Tests-1-08

- test: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` | `SettingsFileDiagnosticsTests.Create_ColumnTypeKeyInBrackets_HasNoHelp` | rows: all
- evidence: measured (kills 4, alone 0; cover `Create_ColumnTypeStringThatIsNotOne_HasNoHelp`, which has this test as its own cover).  Read: the two prove different arms of `HelpOf` (SettingsFileDiagnostics.cs:116-126).  This one, SettingsFileDiagnosticsTests.cs:121-124, holds a bracketed key with a type under columnTypes to no help, so the bracket help stays with typeOverrides; `Create_ColumnTypeStringThatIsNotOne_HasNoHelp`, SettingsFileDiagnosticsTests.cs:183-192, goes through the reader and holds a string member to no object-form help, which is the reader's `IsObjectForm`, outside the scope's source; blind: yes
- verdict: confirmed
- reason: `kills` on generator-diagnostics gives both tests the same four kills: 52:24, 53:23, 85:54 and 125:18.  Each is the other's cover, so both cannot go, and they prove different things.  SettingsFileDiagnosticsTests.cs:121-124 gives a columnTypes error a bracketed key and a non-null `Other`.  It fails if the `TypeOverrides when error.Other is not null` arm (SettingsFileDiagnostics.cs:122) is widened to columnTypes, because `TypeOverrideGrammar.BracketHelp` (TypeOverrideGrammar.cs:126-141) answers for `[users].[id]`.  :183-192 runs the reader on a string member without brackets, `orders.metadata`, so BracketHelp would return empty and that test cannot catch the widening.  What it holds is the reader's `IsObjectForm` false for a string member, which lies outside this scope's source.  Keep is right.

### F-SqlSource.Tests-1-09

- test: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs` | `SettingsFileDiagnosticsTests.Create_RepeatedKey_QuotesBothKeysAndTheSetting` | rows: all
- evidence: measured (kills 3, alone 0; cover `Create_GeneratorParameterInTwoStates_IsARepeatAtTheSecondElement`, which has this test as its own cover).  Read: this one, SettingsFileDiagnosticsTests.cs:250-259, reads two type-override keys that differ in spelling, `integer` and `int4`, as one key and quotes both under typeOverrides; the other, SettingsFileDiagnosticsTests.cs:76-91, reads one generator parameter in two states and holds the error to SQLSRC020 at the second element.  They prove two readers' repeats; the shared kills are only the argument and location mutants of SettingsFileDiagnostics.cs:51-53; blind: yes
- verdict: confirmed
- reason: `kills` on generator-diagnostics gives both tests three kills: 52:24, 53:23 and 93:17.  The finding says the shared kills are "the argument and location mutants of :51-53".  In fact they are the argument and setting coalesces (:52-53) and the `Other` coalesce (:93:17), with no location mutant.  The conclusion holds all the same.  SettingsFileDiagnosticsTests.cs:250-259 proves that the type-override reader treats the synonyms `integer` and `int4` as one key and quotes both and the setting.  :76-91 proves that the generator-parameter reader reports one parameter in two states as SQLSRC020, at the span of the second element.  Each is a different reader's repeat, and neither is the other's cover.  Keep is right.

### F-SqlSource.Tests-1-10

- test: `tests/SqlSource.Tests/SortedSearchTests.cs` | `SortedSearchTests.IndexOf_KeyThatIsTheStartOfAText_IsTheIndexOfThatStart` | rows: all
- evidence: measured (7 rows; kills 17, alone 0, timeouts under it 2; rows needed 3; cover `CachingSettingsFileTests.Run_SettingsFileAdded_ParsesOnlyTheFilesItReaches`).  Read: the cover is a caching test of the generator that passes through the search, not a test of it.  The rows, SortedSearchTests.cs:32-38, are each an edge of a prefix search over `["a", "B", "bc"]`: the first, a middle and the last item, ignoring case; an empty text; a key longer than its match; a text past every item; and one between two items.  The timeouts under it, at src/SqlSource/SortedSearch.cs lines 66, 72, 101 and 104, name no killer, so it may be what detects them.  The same holds for the other tests of the class, whose covers are generator tests too; blind: no
- verdict: confirmed
- reason: `kills` on generator-root shows 7 rows, 17 kills and 0 alone.  The covers named are generator tests, such as `CachingSettingsFileTests.Run_SettingsFileAdded_ParsesOnlyTheFilesItReaches` for the prefix comparer at SortedSearch.cs:138.  They pass through the search but promise nothing about it.  The rows at SortedSearchTests.cs:32-38 are each an edge of the prefix search over `["a", "B", "bc"]`.  The test has TIMEOUT SortedSearch.cs:66:17 (`order <= 0`) and 72:24 (`middle + 1`) under it, with no killer named, so the brief forbids deleting it on its numbers.  One correction: the timeouts at lines 101 and 104 that the finding names are under the class's `LowerBound` tests, not under this one.  Keep is right.

### F-SqlSource.Tests.Generation-1-15

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_FolderWithNoFileAbove_IsNone` | rows: all
- evidence: measured (kills 23, alone 0, timeouts under it 1; cover: `SettingsFileSetTests.ChainOf_Dialect_IsTheNearestFilesThatSetsOne`); read (the `SettingsChain.None` of a path with no file in or above its folder is SettingsFileSet.cs:82, a blind line; the cover's paths all find a file, its `other/a.sql` the root's, so nothing else asserts `None` for a folder beside or above every file, `app/a.sql` and `other/Queries/a.sql` here, or for the empty set; it has a timeout under it); blind: yes
- verdict: confirmed
- reason: ChainOf's `SettingsChain.None` for a path that no file reaches is SettingsFileSet.cs:82, a blind line.  The cover's paths all find a file, tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:135-136.  No other test of the class asserts None for an unreached path or for the empty set (149-152).  `ChainOf_RootFileThatSaysNothingElse_EndsTheChainWithNone` reaches None through a found file's chain instead.  The test also carries the 179:57 timeout.

### F-SqlSource.Tests.Generation-1-16

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.ChainOf_RootFalse_GoesOn` | rows: all
- evidence: measured (kills 39, alone 0, timeouts under it 0; cover: `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`); read (the chain ends only when `Root == true`, SettingsFileSet.cs:108 and 157; the cover's passing-through file has an unknown root, null, so a check for any written root, `Root is not null` or `HasValue`, would pass it and every other test of the class, and fail only this one, the one file that writes `"root": false`); blind: no
- verdict: confirmed
- reason: The only `"root": false` in the chain tests is tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:107; the other mention in the tests is a reader test, SettingsFileReaderTests.cs:112.  Every other chain test has a root that is true or unknown, so a test of `Root.HasValue` in place of `== true` at SettingsFileSet.cs:108 and 157 would fail only this test.

### F-SqlSource.Tests.Generation-1-17

- test: `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs` | `SettingsFileSetTests.Of_SameFiles_AreEqual` | rows: all
- evidence: measured (kills 26, alone 0, timeouts under it 0; cover: `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`); read (the set's value equality and hash, which the pipeline caches on, are compiler-made members of the record over its `EquatableArray` fields, which mutation cannot see; the cover compares chains' levels and never two sets; this is the one test that builds a set twice and compares them, with a file whose diagnostics take part); blind: no
- verdict: confirmed
- reason: The set's Equals and GetHashCode are compiler-made record members, which mutation cannot see.  This is the only test that builds a set twice and compares the two, tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs:351-372.  CachingSettingsFileTests checks the `SettingsFiles` step's reason only after its collected inputs are equal, so the set's own equality is never compared there.

### F-SqlSource.Tests.Generation-1-18

- test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_PathThatDoesNotNormalise_IsNull` | rows: all
- evidence: measured (kills 1, alone 0, timeouts under it 0; cover: `ListedSettingsFileTests.Read_FileAtTheRoot_HasDepthZero`); read (the null for a path that `SqlPath.Normalize` refuses is the second half of ListedSettingsFile.cs:60, a blind line; the cover's `/sqlsource.json` normalises, so only `../sqlsource.json` here takes that branch); blind: yes
- verdict: confirmed
- reason: `SqlPath.Normalize` returns null when a `..` has nothing before it, src/SqlSource/Generation/Paths/SqlPath.cs:88-92.  `../sqlsource.json` passes IsSettingsFile, so only this test takes the null branch of ListedSettingsFile.cs:60, a blind line.  Its one kill, 46:22 `+`, throws out of range, which this test notices as an exception.

### F-SqlSource.Tests.Generation-1-19

- test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_PathWithBackslashes_IsReadAtItsNormalisedFolder` | rows: all
- evidence: measured (kills 16, alone 0, timeouts under it 0; cover: `ListedSettingsFileTests.Read_FileNamedExactly_IsRead`); read (the `'\\'` of `IsSettingsFile`, ListedSettingsFile.cs:47, a blind line, is asserted here alone in the class; the cover's path has forward slashes; it is also the cover named by F-SqlSource.Tests.Generation-1-09); blind: yes
- verdict: confirmed
- reason: The `'\\'` arm of IsSettingsFile, ListedSettingsFile.cs:47, is on a blind line, and in ListedSettingsFileTests only this test uses a path with backslashes, tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs:66-76.  The only other test that reads a backslash settings path is F-09's test, which that finding proposes to delete.

### F-SqlSource.Tests.Generation-1-20

- test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs` | `ListedSettingsFileTests.Read_TextTheCompilerCannotGive_IsInvalidJson` | rows: all
- evidence: measured (kills 14, alone 0, timeouts under it 0; cover: `ListedSettingsFileTests.Read_InvalidDatabase_IsReportedByTheToolAndNotByTheBuild`); read (a text the compiler cannot give, null, is read as empty at ListedSettingsFile.cs:67, a blind line; without it `Read` throws; this is the one test that gives a null text; the string mutant that survives under it, `"Stryker was here!"`, is also not JSON, so it is equivalent for this behaviour); blind: yes
- verdict: confirmed
- reason: No other test of the repository passes a null text to a settings file: a grep finds `new InMemoryAdditionalText(QueriesPath, null)` only at ListedSettingsFileTests.cs:195.  ListedSettingsFile.cs:67 reads a null text as empty, and without that `Read` throws on `text.ToString()`.  The surviving `"Stryker was here!"` string is not JSON either, so it is equivalent for SQLSRC015.

### F-SqlSource.Tests.Generation-1-21

- test: `tests/SqlSource.Tests/Generation/Converters/ConverterSetTests.cs` | `ConverterSetTests.Of_ProjectAndMappedTypeOfOneEdge_KeepsTheProjects` | rows: all
- evidence: measured (kills 6, alone 0, timeouts under it 0; cover: `ConverterSetTests.Of_Converters_AreSortedByArgumentThenReturn`); read (a mapped type's converter of an edge the project has is left out at ConverterSet.cs:70; the cover's mapped converter, `int` to `global::Lib.Id`, shares no edge with the project's, so a filter that kept every mapped converter passes it and fails only this test, whose `global::Lib.Email.From` must go and `global::Lib.Email.To` stay); blind: no
- verdict: confirmed
- reason: The mapped converters are filtered against the project's edges at src/SqlSource/Generation/Converters/ConverterSet.cs:70.  The cover's mapped `int`→`global::Lib.Id` (tests/SqlSource.Tests/Generation/Converters/ConverterSetTests.cs:42) shares no edge with the project, and neither does `Touching`'s `byte[]`→Email (67).  Only this test (16-31) drops a mapped converter, `Email.From`, and keeps `Email.To`.

### F-SqlSource.Tests.Generation-1-22

- test: `tests/SqlSource.Tests/Generation/Converters/ConverterSetTests.cs` | `ConverterSetTests.Of_LibraryEdgeAMappedTypeHas_KeepsTheMappedTypes` | rows: all
- evidence: measured (kills 3, alone 0, timeouts under it 0; cover: `ConverterSetTests.Of_Converters_AreSortedByArgumentThenReturn`, `ConverterSetTests.Of_LibraryEdgeTheProjectHas_KeepsTheProjects`); read (a library edge is dropped against `takenEdges`, which is made after the mapped converters are added, ConverterSet.cs:73-76; `Of_LibraryEdgeTheProjectHas_KeepsTheProjects` has no mapped converter, so a `takenEdges` made from the project's alone passes it and every other test of the class, and fails only this one); blind: no
- verdict: confirmed
- reason: `takenEdges` is built after the mapped converters are added, ConverterSet.cs:73-76.  `Of_LibraryEdgeTheProjectHas_KeepsTheProjects` (ConverterSetTests.cs:85-102) has no mapped converter, so a `takenEdges` built from the project's converters alone passes it.  Only this test (104-125) fails on that change.

### F-SqlSource.Tests.Generation-1-23

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_EveryEdge_IsNamedByItsMethodAndItsArgument` | rows: all
- evidence: measured (kills 1, alone 0, timeouts under it 0; cover: `LibraryConvertersTests.Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem`); read (the NodaTime edges' display names are built in `NodaTimeConversions.BuildEdges`, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:69-92, a static initialiser no run can mutate; the cover compares `Of` with `NodaTimeConversions.Edges` itself, so it cannot see a wrong display name, and no other test of the class reads `DisplayName` of a NodaTime edge); blind: yes
- verdict: confirmed
- reason: The NodaTime display names are built in the static `BuildEdges`, src/SqlSource/Generation/Converters/NodaTimeConversions.cs:75.  The cover compares `Of` with `NodaTimeConversions.Edges` itself.  A grep of tests/ for the text of a display name finds only LibraryConvertersTests.cs:124-126.

### F-SqlSource.Tests.Generation-1-24

- test: `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs` | `LibraryConvertersTests.Of_WithJson_IsNamedByItsMethodAndItsArgument` | rows: all
- evidence: measured (kills 2, alone 0, timeouts under it 0; cover: `LibraryConvertersTests.Of_WithJson_HasTheTenEdges`); read (the Json edges' display names are built in `JsonConversions.BuildEdges`, src/SqlSource/Generation/Converters/JsonConversions.cs:68 and 77-100, a static initialiser no run can mutate; the cover compares argument, return, method and targets with its table and `Of` with `JsonConversions.Edges` itself, so it cannot see a wrong display name, and no other test of the class reads `DisplayName` of a Json edge); blind: yes
- verdict: confirmed
- reason: The Json display names are built in the static `BuildEdges`, src/SqlSource/Generation/Converters/JsonConversions.cs:68 and 85.  The cover, `Of_WithJson_HasTheTenEdges` (204-217), compares argument, return, method and targets, and `Of` with `Edges` itself, never `DisplayName`.  The only Json display names in the tests are at LibraryConvertersTests.cs:244-246.

### F-SqlSource.Tests.Generation-1-25

- test: `tests/SqlSource.Tests/Generation/UsableQueriesTests.cs` | `UsableQueriesTests.Of_TwoFiles_GivesEachQueryWithItsIndicesAndSettings` | rows: all
- evidence: measured (kills 15, alone 0, timeouts under it 2; cover: `UsableQueriesTests.Of_FileWithADuplicateName_LeavesItOutAndReportsSqlsrc008`); read (it has two timeouts under it, src/SqlSource/Generation/UsableQuerySequence.cs:50 and 60, which no report names a killer for; and the cover asserts only which queries come out, while this test alone asserts the file and query indices and each query's settings, a query's marker over its file's level, UsableQueriesTests.cs:36-41); blind: no
- verdict: confirmed
- reason: The cover asserts only which queries come out, tests/SqlSource.Tests/Generation/UsableQueriesTests.cs:60 and 72.  Only this test asserts the file and query indices and each query's settings, a marker over its file's level, 36-41.
  - `kills` (generator-generation) puts TIMEOUT UsableQuerySequence.cs:50:13 and 60:28 under it.  The finding's point about the timeouts is weak, because both timeouts are under the cover too.
  - The keep stands on the indices and the settings alone.

### F-SqlSource.Tests.Generation-2-02

- test: `tests/SqlSource.Tests/Generation/Methods/ListNamesTests.cs` | `ListNamesTests.PrefixOf_ANameOfAnotherCase_ComparesIgnoringCase` | rows: all
- evidence: measured (kills 20, alone 0, timeouts under it 5; cover: `ListNamesTests.PrefixOf_EachMatchedInTurn_AddsAnUnderscoreForEach`, `ListNamesTests.PrefixOf_ADeclaredVariableMatchesTheSuffix_AddsAnUnderscore`); read (the case is ignored by `SqlParameterName.Comparison` and `SqlParameterName.Comparer`, src/SqlSource/Parsing/SqlParameterName.cs:20,23, used at ListNames.cs:108 and :137; that source is outside the scope, which mutated only src/SqlSource/Generation/Methods, so no mutant could make the match case-sensitive; `IDS_3` against `ids` is the only parameter of another case in ListNamesTests, and an ordinal comparison would fail this test alone; it has timeouts under it, so it is not deleted on its numbers); blind: no
- verdict: confirmed
- reason: The evidence gives 20 kills, 0 alone and 5 timeouts (ListNames.cs:98, 117, 131, 137, 139, which `kills` confirms).  The case is ignored by `SqlParameterName.Comparer` and `Comparison`, src/SqlSource/Parsing/SqlParameterName.cs:20,23, which ListNames.cs:109 and :137 use.  That source is outside the generator-generation-methods scope, so no mutant could make the match case-sensitive.  `IDS_3` (ListNamesTests.cs:31) is the only parameter of another case in the class.  Reading shows why its cover cannot stand in for it, so a **keep** is right.

### F-SqlSource.Tests.Generation-2-04

- test: `tests/SqlSource.Tests/Generation/Methods/MethodConflictsTests.cs` | `MethodConflictsTests.Find_TypeWithTwoSignaturesOfOneName_IsReportedAndDroppedOnce` | rows: all
- evidence: measured (kills 12, alone 0; cover: `MethodConflictsTests.DroppedOf_TwoMethodsOfOnePlaceOneClashing_DropsOnlyTheClashingName`); read (the cover's two methods of App.First are named Get and List, so it never gives one type two signatures of one name; only this test does, a query method and its collection method, and only it fails when the `reported` set of MethodConflicts.cs:277 and :288 is lost, since `Dropped` would then hold `App.First.Queries.Get` twice; FindClashes, MethodConflicts.cs:273-307, is blind, so the cover is no evidence there); blind: yes
- verdict: confirmed
- reason: The comment at MethodConflictsTests.cs:56-57 makes this test the guard of a type being reported and dropped once.  Only this test gives one type two signatures of one name (:61-62).  The cover, `DroppedOf_TwoMethodsOfOnePlaceOneClashing_DropsOnlyTheClashingName` (:234-244), names its two methods `Get` and `List`.  The `reported` set (MethodConflicts.cs:277, :288) is what drops the name once.  FindClashes, MethodConflicts.cs:273-307, is listed blind, so the cover's kills are no evidence there.  A **keep** is right.

### F-SqlSource.Tests.Generation-3-03

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_FieldNamedConnectionString_IsAConnectionStringCandidate` | rows: all
- evidence: measured (3 rows, kills 28, alone 0, rows needed 1).  Read: the rows `"connectionString"`, `"_connectionString"` and `"ConnectionString"` (ConnectionCandidateReaderTests.cs:36-38) are each one entry of `ConventionNames`, a static array at ConnectionCandidateReader.cs:31, which the evidence lists as blind (static): no mutant can drop an entry, so each row is the one guard of its name; blind: yes
- verdict: confirmed
- reason: `ConventionNames` is the static array at ConnectionCandidateReader.cs:31, which the evidence lists as blind (static), so no mutant drops an entry and "rows needed 1" says nothing about the names.  Each row (ConnectionCandidateReaderTests.cs:36-38) is one entry; within the class `_connectionString` is read unmarked nowhere else (lines 354, 393 and 719 sit beside a marked member, which drops it).  `connectionString` and `ConnectionString` are also read by the parameter theories and by `Read_RecordPositionalString_IsItsPropertyAlone` and `Read_ProtectedBaseMember_IsInherited`, so "the one guard of its name" is overstated for those two, but this theory is the one whose name promises the field convention and the keep is right.

### F-SqlSource.Tests.Generation-3-04

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_ParameterPassedOnOrStored_IsHeld` | rows: all
- evidence: measured (5 rows, kills 41, alone 5, rows needed 3).  Read: each row is one kind of initial value that `InitialValuesOf` and `IsReadBesides` look at (ConnectionCandidateReaderTests.cs:437 a base-type argument, 441 a field initializer, 445 a property initializer, 449 an event field's initializer, 453 an initializer in another part of the type), on lines the evidence lists as blind (ConnectionCandidateReader.cs:224-257, 264-270, 275-287); the event row alone guards that an event field counts, through `BaseFieldDeclarationSyntax`, and the last alone guards the loop over every declaration of the type; blind: yes
- verdict: confirmed
- reason: `kills` lists five mutants this theory alone kills: 104:17 (held.Add), 262:34 and 268:21 (the base-type row, ConnectionCandidateReaderTests.cs:437), 280:21 (the field-initializer rows) and 286:13 (the property row, 445).  The event row (449) and the other-part row (453) have no kill of their own: they guard that `BaseFieldDeclarationSyntax` takes an event field (ConnectionCandidateReader.cs:275) and that every declaring reference is searched (225), changes Stryker does not make, on lines the evidence lists as blind (224-257, 275-287).  The doc comment of `Read` (ConnectionCandidateReader.cs:35-38) names fields, properties and events, and the comment above the theory (433-435) says "in every part of the type".  The reading is right.

### F-SqlSource.Tests.Generation-3-05

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_ParameterNotPassedOnOrStored_IsACandidate` | rows: all
- evidence: measured (2 rows, kills 35, alone 0, rows needed 1; cover `Read_ParameterPassedOnOrStored_IsHeld`, `Read_PrimaryConstructorDataSource_IsAParameterCandidate`).  Read: the row at ConnectionCandidateReaderTests.cs:472 (`Other.connectionString` read in an initializer) is the one case where an identifier of the parameter's name is bound to another symbol, which only the symbol comparison at ConnectionCandidateReader.cs:242-250 tells apart, on blind lines; without it a check by name alone would pass every other test; blind: yes
- verdict: confirmed
- reason: The row at ConnectionCandidateReaderTests.cs:472-475 has an identifier `connectionString` in an initializer that binds to `Other.connectionString`; only the symbol comparison at ConnectionCandidateReader.cs:242-250 (blind, does not compile, per the evidence) keeps the parameter a candidate, so a check by name alone would fail this row and pass every other test.  The row at 468-470 is the one where a method body reads the parameter, which `InitialValuesOf` must not look at.  The comment above the theory (465-466) names both cases.  `kills` gives the theory no alone kill, as the finding says; the keep is right.

### F-SqlSource.Tests.Generation-3-09

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileChecksTests.cs` | `SidecarFileChecksTests.ReasonOf_EveryKind_HasAReasonOfItsOwn` | rows: all
- evidence: measured (covers 7 mutants and kills none).  Read: the test runs `ReasonOf` over `Enum.GetValues<SidecarErrorKind>()` and asserts the reasons are distinct (SidecarFileChecksTests.cs:146-153); its comment says it is for a kind added later, which no mutant can add, while `ReasonOf_Error_SaysWhatIsWrong` names only the seven kinds of today.  The build's exhaustive-switch rule makes a new kind give a reason; this test alone stops it copying another kind's; blind: no
- verdict: confirmed
- reason: `kills` (generator-generation-sidecars) gives 0 kills and 0 survivors; the test asserts only that the reasons of every `SidecarErrorKind` are distinct (SidecarFileChecksTests.cs:146-153), which `ReasonOf_Error_SaysWhatIsWrong` implies for today's seven kinds (134-140) but cannot for a kind added later.  src/AGENTS.md makes a new member fail the build at the switch (IDE0072) of `ReasonOf` (SidecarFileChecks.cs:94-105), so it must get a reason; only this test stops the reason copying another's, a change no mutant models, as the comment at 144 says.  The keep is right.

### F-SqlSource.Tests.Generation-3-13

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileReaderTests.cs` | `SidecarFileReaderTests.Read_CurrentFile_GivesTheSidecarAndWhereEachKeyIs` | rows: all
- evidence: measured (kills 5, alone 0; cover `SidecarFileChecksTests.Read_EntryVersionWithSuffix_DoesNotWarn`).  Read: the cover runs the reader only to build its input and compares nothing it gives with an independent value (SidecarFileChecksTests.cs:20-39, 121-130; the other checks tests take their locations from `file.EntryLocations` itself), while this test is the one that asserts the start, the absence of an error location and each key's span and line positions (SidecarFileReaderTests.cs:45-50), computed on lines the evidence lists as blind (SidecarFileReader.cs:29-39, 46, 58-63); blind: yes
- verdict: confirmed
- reason: `kills` shows the test's five kills (28:13, 35:22 twice, 35:41, 66:13) are mutants that break the read outright, all killed by the cover too; the cover, `Read_EntryVersionWithSuffix_DoesNotWarn` (SidecarFileChecksTests.cs:121-130), asserts no position, and the other checks tests take their locations from `file.EntryLocations` and `file.Start` themselves (72, 92, 112).  This test alone checks the start, a null error location and each key's span and line positions against independent values (SidecarFileReaderTests.cs:45-50), computed on lines the evidence lists as blind (SidecarFileReader.cs:34-39, 46, 58-63).  The keep is right.

### F-SqlSource.Tests.Generation-3-14

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileReaderTests.cs` | `SidecarFileReaderTests.Read_MalformedFile_KeepsWhereTheErrorIs` | rows: all
- evidence: measured (kills 4, alone 0; cover `SidecarFileChecksTests.Read_EntryVersionWithSuffix_DoesNotWarn`).  Read: the cover reads a well-formed file and never has an error; this test is the one that asserts the error location of a malformed file without a byte order mark, its path, span and line (SidecarFileReaderTests.cs:89-92), on lines the evidence lists as blind (SidecarFileReader.cs:58-63); blind: yes
- verdict: confirmed
- reason: The cover reads a well-formed file and shares only the four general kills (`kills`: 28:13, 35:22 twice, 35:41).  This test is the one that asserts a malformed file's error location in absolute terms: its path, span and line (2, 2) (SidecarFileReaderTests.cs:89-92); `Read_MalformedFileWithAByteOrderMark_KeepsWhereTheErrorIsInTheFile` checks only that its location is the plain one's plus one (104-107) and would pass on a wrong path or line.  The location comes from SidecarFileReader.cs:46 and 73-74, which the evidence lists as blind in part.  The keep is right.

### F-SqlSource.Tests.Generation-3-15

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileReaderTests.cs` | `SidecarFileReaderTests.Read_TextThatCannotBeHad_IsMalformedAtTheStart` | rows: all
- evidence: measured (kills 1, alone 0; cover `SidecarFileChecksTests.Read_EntryVersionWithSuffix_DoesNotWarn`).  Read: it is the one test of a file whose text is null (SidecarFileReaderTests.cs:71), the branch at SidecarFileReader.cs:28-32, which the evidence lists as blind (29-32); the cover always has a text; blind: yes
- verdict: confirmed
- reason: It is the one test that hands the reader a null text (SidecarFileReaderTests.cs:71), the branch at SidecarFileReader.cs:28-32 (blind 29-32 per the evidence).  Its one kill, 28:13, is killed by every reader test, since the mutant sends a real text into the null branch; for a null text that mutant would throw, and only this test asserts what the branch gives, `InvalidJson` at the start with no entries (73-77).  The keep is right.

### F-SqlSource.Tests.Generation-3-16

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileReaderTests.cs` | `SidecarFileReaderTests.Read_FileOfAnotherVersion_KeepsTheVersionAlone` | rows: all
- evidence: measured (kills 4, alone 0; cover `SidecarFileChecksTests.Read_EntryVersionWithSuffix_DoesNotWarn`).  Read: the cover's file is of this format; this test is the one that reads a file of another format version and asserts it gives no error location and no entry locations (SidecarFileReaderTests.cs:118-120), from lines the evidence lists as blind (SidecarFileReader.cs:46, 58-63); blind: yes
- verdict: confirmed
- reason: The cover's file is of this format (SidecarFileChecksTests.cs:26-27); no other test of the slice reads a file of another format version, and the evidence lists the checks' format-version lines (SidecarFileChecks.cs:52-62) as covered by no test.  This test asserts `OfAnotherVersion(2)`, no error location and no entry locations (SidecarFileReaderTests.cs:118-120), from lines the evidence lists as blind (SidecarFileReader.cs:46, 58-61); its four kills are the general ones every reader test shares.  The keep is right.

### F-SqlSource.Tests.Generation-3-17

- test: `tests/SqlSource.Tests/Generation/Repositories/RepositoryInterfacesTests.cs` | `RepositoryInterfacesTests.Read_Value_IsOnUnlessFalse` | rows: all
- evidence: measured (6 rows, kills 3, alone 1, rows needed 1).  Read: the rows (RepositoryInterfacesTests.cs:12-17) each state one part of the rule of RepositoryInterfaces.cs:20-24: no property (`null`) and an empty one are on, any value but false is on (`"true"`, `"yes"`), and false is off ignoring case (`"false"`, `"FALSE"`); the `StringComparison.OrdinalIgnoreCase` and the parse of a value other than `true` are changes mutation does not make, so the one row it needs does not say the others are redundant; blind: no
- verdict: confirmed
- reason: `kills` gives three kills (21:9, 22:13 alone, 23:37).  Each row states one part of RepositoryInterfaces.cs:20-24: `null`, no property, on (TryGetValue false); `""` and `"yes"`, on for any value but false; `"true"`, plainly on; `"false"`, off; `"FALSE"`, off ignoring case.  Stryker did not mutate `StringComparison.OrdinalIgnoreCase` and makes no change that parses the value as a bool, so "rows needed 1" says nothing about the `"FALSE"` and `"yes"` rows, which alone guard those parts.  The same six rows are the plan's stated cases (docs/superpowers/plans/2026-11-09-repository-output.md:138).  The keep is right.

### F-SqlSource.Tests.Generation.Emitting-1-12

- test: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs` | `PostgresParameterBinderTests.Append_NoBindName_SetsNoDataTypeName` | rows: all
- evidence: measured (covers 23 mutants and kills none) and read (PostgresParameterBinderTests.cs:134-135 is the one test of a parameter with no bind name, which `DataTypeNameOf` answers with null at PostgresParameterBinder.cs:106-108 so that no `DataTypeName` line is written; every mutant there does not compile, so its kills of none say nothing); blind: yes (PostgresParameterBinder.cs:105-133 does not compile under mutation)
- verdict: confirmed
- reason: The test (PostgresParameterBinderTests.cs:133-135) is the only one in the class that asserts the absence of the `DataTypeName` line, the branch `DataTypeNameOf` takes with no bind name (PostgresParameterBinder.cs:53, :106-108).  The other tests without a described type (:151-354) only `ShouldContain` other lines.  The evidence marks PostgresParameterBinder.cs:46-53 and 105-133 as not compiling under mutation, and `kills` gives the test 0 kills, timeouts and survivors, so its numbers say nothing there.  Its reason says "the one test of a parameter with no bind name", but several have none: it is the one that asserts the line is left out, which is what the keep rests on.

### F-SqlSource.Tests.Generation.Emitting-1-13

- test: `tests/SqlSource.Tests/Generation/Emitting/SqlServerParameterBinderTests.cs` | `SqlServerParameterBinderTests.Append_EachBaseType_SetsItsSqlDbType` | rows: all
- evidence: measured (34 rows; kills 15, alone 0, timeouts under it 0; rows needed 4; cover `SqlServerParameterBinderTests.Append_DecimalOfScale0_RoundsTheValueToAWholeNumber`, `SqlServerParameterBinderTests.Append_BoundedVariableLength_BindsAnOverLongValueAtMax`, `SqlServerParameterBinderTests.Append_DateTime2OfAnyScale_SetsScale7`) and read (each row at SqlServerParameterBinderTests.cs:58-91 checks one entry of the map of base names to `SqlDbType` members, built in a static initialiser that no mutation run can change, and `SqlDbTypeOf_EveryKeyOfTheMap_IsAMemberOfSqlDbType` checks only that each entry names some member, not the right one); blind: yes (SqlServerParameterBinder.cs:339-389, static)
- verdict: confirmed
- reason: The rows at SqlServerParameterBinderTests.cs:58-91 each check one entry of `Rows`, built in a static initialiser (SqlServerParameterBinder.cs:339-389, blind), and `SqlDbTypeOf_EveryKeyOfTheMap_IsAMemberOfSqlDbType` (:98-109) checks only that each entry is some member.  The reason's "each is the only check" overstates it a little: NVarChar, Char, VarChar, NChar, Binary, VarBinary, Decimal, Money and Timestamp are also asserted by focused tests after :94.  Most rows (bigint through sql_variant, time, datetimeoffset, sysname, numeric, rowversion) are checked nowhere else, and the keep of the whole theory stands.

### F-SqlSource.Tests.Generation.Emitting-1-14

- test: `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs` | `MethodWriterTests.Run_TokenWithAnAwkwardName_CompilesOnTheNewestCSharp` | rows: all
- evidence: measured (74 rows, 6.1 s; kills 104, alone 0, timeouts under it 2; rows needed 1; cover `TypeEmitterTests.Emit_QueryWithAToken_IsAMethodInTheSamePlaceAndOrderAsTheConstants`, `MethodWriterTests.Append_SeveralTokensWithOneRepeated_WritesTheDocumentationAndTheMethod`, `MethodWriterTests.Append_OneTokenNamedState_ReadsTheStateByItsOtherName`, `MethodWriterTests.Run_TokenWithAnAwkwardNameInAQueryMethod_CompilesOnTheNewestCSharp`) and read (each row at MethodWriterTests.cs:299-372 is a name that a token could hide, and a mutant never adds an identifier, so the rows guard what mutation cannot see; `src/SqlSource/Generation/Emitting/AGENTS.md` and `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md` require a row here for each such name, and the comment at :294-297 says why the theory is outside `Generator/`); blind: no
- verdict: confirmed
- reason: `src/SqlSource/Generation/Emitting/AGENTS.md` ("Names a token could hide") and `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md:15` require a row in `Run_TokenWithAnAwkwardName_CompilesOnTheNewestCSharp` for each name a token could hide.  The comment at MethodWriterTests.cs:294-297 says why it sits outside `Generator/` (C# 14 contextual keywords).  A mutant never introduces an identifier, so the rows guard what mutation cannot see, and the cover the numbers give does not compile at `LanguageVersion.Latest` with those names.

### F-SqlSource.Tests.Generation.Emitting-1-15

- test: `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs` | `MethodWriterTests.Run_TokenWithAnAwkwardNameInAQueryMethod_CompilesOnTheNewestCSharp` | rows: all
- evidence: measured (76 rows, 32.4 s, most of `MethodWriterTests`' 38.5 s, 8.9% of the project; kills 1,174, alone 208, timeouts under it 4; rows needed 1) and read (each row at MethodWriterTests.cs:415-490 is a local, pattern variable or lambda parameter name a token could hide, which mutation cannot introduce, and `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md` requires a row in this sibling of `GeneratedSourceMethodTests`' theory for each such name; it needs no database or process, only the generator and the compiler); blind: no
- verdict: confirmed
- reason: `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md:15` requires a row in `Run_TokenWithAnAwkwardNameInAQueryMethod_CompilesOnTheNewestCSharp` for each local name a token could hide, and the comment at MethodWriterTests.cs:405-407 makes it the newest-C# sibling of `GeneratedSourceMethodTests`' theory.  Each row compiles the generator's output twice, async and sync (:418-422), with no database or process.  The test also has 208 kills of its own.  One correction: 8.9% is `MethodWriterTests`' share of the project (38.5 s of 431.0 s); this test's 32.4 s is 7.5%.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-06

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs` | `CallSpellingTests.AppendCallStart_EveryCall_HasAnAsynchronousAndASynchronousSpelling` | rows: all
- evidence: measured (kills 25, alone 0; cover `CallSpellingTests.AppendCallStart_EachCall_IsTheCallOfEachSpelling`, which in turn has this test as its cover) and read: it walks `Enum.GetValues<DriverCall>()`, so a member added to `DriverCall` without a spelling throws `UnexpectedEnumValueException` here, while the 12-row theory lists only today's members and pins their exact text; neither can go for the other.  blind: no
- verdict: confirmed
- reason: The two tests are each other's only cover (`kills`: 25 each, 0 alone), so both cannot go.  The finding's mechanism needs a correction.  A member added to `DriverCall` without a spelling fails the build first, through IDE0072 in the switches of `SyncCallSpelling.cs:44-59` and `AsyncCallSpelling` (`Directory.Build.props:7-8`, `src/AGENTS.md`), before any test runs.  What the walk at CallSpellingTests.cs:83-88 adds over the 12-row theory is the rule that every member's asynchronous call is the synchronous one plus `Async` and an await.  That rule holds for every member, future ones too, while the theory pins today's members' exact text.  The keep holds on that reason.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-09

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/LocalNamesTests.cs` | `LocalNamesTests.Take_NameNoTokenHas_IsTheName` | rows: all
- evidence: measured (covers 1 mutants and kills none; timeouts under it 1, at `src/SqlSource/Generation/Emitting/QueryMethods/LocalNames.cs` line 23) and read: the one mutant it reaches is the timeout of line 23, `while (!_taken.Add(candidate))`, whose negation loops for ever on a free name, which this test detects by never returning; it is also the only test that takes a free name while other names are taken.  blind: no
- verdict: confirmed
- reason: The test (LocalNamesTests.cs:10-11) asserts what its name says, a free name returned as is while other names are taken.  Its 0 kills come from its only mutant, `LocalNames.cs:23:16` (`while (_taken.Add(candidate))`), which loops for ever on a free name and is reported as a timeout (`kills`: 0 kills, 1 timeout).  So the measurement does not show a weak assertion, and the keep holds on the value question.  The finding's proposal should not read as if this test alone catches that mutant.  `Take_NameATokenHas_GetsTheFirstFreeNumber` fails on it (it returns `"sql"`, not `"sql2"`, line 15).  `Take_SameNameTwice_GivesTwoNames` hangs on it at its first `Take` on an empty set (line 22).

### F-SqlSource.Tests.Generation.Types-1-10

- test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/TransitivePackageAssembliesTests.cs` | `TransitivePackageAssembliesTests.Of_Empty_IsNone` | rows: all
- evidence: measured (covers 4 mutants and kills none) and read: the mutants are of the early return `if (string.IsNullOrEmpty(value)) return None;` (src/SqlSource/Generation/Types/MappedTypes/TransitivePackageAssemblies.cs:53-56), and for `""` the loop that follows finds no name and also returns `None` (lines 61-84), so they are equivalent on this input and no test could kill them.  The assertion is the behaviour the name promises, for the value the build gives a project whose references were not resolved or bring no package's assembly (the comment at line 20), which `Of_Null_IsNone` and `Read_NoProperty_IsNone` do not exercise; blind: no
- verdict: confirmed
- reason: Read TransitivePackageAssembliesTests.cs:20-28 and src/SqlSource/Generation/Types/MappedTypes/TransitivePackageAssemblies.cs:51-88.  For `""`, a mutant of the early return at lines 53-56 (`value != null`, the negation, the emptied block) falls into the loop: `IndexOf` gives -1, `end` is 0, nothing is added, and lines 81-84 return the same `None`, so each is equivalent on this input.  `kills` gives 0 kills, 0 survivors and 0 timeouts under it, matching "covers 4 mutants and kills none".  The test is the only one that passes the empty value the build gives (comment at line 20); `Of_Null_IsNone` passes null and `Read_NoProperty_IsNone` a missing property.  The keep reads it rightly.

### F-SqlSource.Tests.Generator-1-05

- test: `tests/SqlSource.Tests/Generator/CachingCheckTests.cs` | `CachingCheckTests.Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck` | rows: all
- evidence: measured (kills 54, alone 0, timeouts under it 13; cover `CachingCheckTests.Run_ClaimedSqlFileAdded_ChecksOnlyThatFile`, which in turn has this test as its only cover) and read: the cover adds Payments and pins a `New` check that moves no other file's node (CachingCheckTests.cs:93-114); this one removes Orders, which sorts first, and pins `Removed` for it with no other file checked and no `FileTypesInput` Modified or New (CachingCheckTests.cs:118-143).  The subject is the nodes' identity across runs in the pipeline and the records' equality in src/SqlSource/Generation/, outside what the root scope sees, and docs/architecture/generator.md:372 names both tests as pinning the add and the remove separately.; blind: no
- verdict: confirmed
- reason: The evidence makes each the other's only cover (evidence lines 48-49).  `Run_ClaimedSqlFileAdded_ChecksOnlyThatFile` (tests/SqlSource.Tests/Generator/CachingCheckTests.cs:92-114) adds Payments and pins `New` for it and `Cached` for the others; `Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck` (:117-143) removes Orders, which sorts first, pins `Removed` for it, `Cached` for the others, and no `FileTypesInput` `Modified` or `New` (:138-141), which the other test does not assert.  docs/architecture/generator.md:372 names both tests for the add and the remove separately.  The finding reads them rightly.

### F-SqlSource.Tests.Generator-1-06

- test: `tests/SqlSource.Tests/Generator/CachingMappedTypeTests.cs` | `CachingMappedTypeTests.Run_EditOfACommentOutsideTheMappedType_LeavesMappedTypeConvertersUnchanged` | rows: all
- evidence: measured (kills 43, alone 0, timeouts under it 11; cover `CachingMappedTypeTests.Run_ConverterOfAReferenceChanged_LeavesMappedTypesUnchanged`, whose only cover is this test) and read: this test pins that a C# edit of the project leaves `MappedTypeConverters` `Cached` and `Converters` `Cached` (CachingMappedTypeTests.cs:209-215); the cover changes the library's converter and pins `MappedTypeConverters` and `Converters` `Modified` while `MappedTypes` stays `Unchanged` (CachingMappedTypeTests.cs:234-240).  Opposite outcomes of one step, both named in docs/architecture/generator.md:372.; blind: no
- verdict: confirmed
- reason: Each is the other's only cover (evidence lines 71-72).  `Run_EditOfACommentOutsideTheMappedType_LeavesMappedTypeConvertersUnchanged` (tests/SqlSource.Tests/Generator/CachingMappedTypeTests.cs:194-216) pins `ResolvedTypes` `Unchanged`, `MappedTypes`, `MappedTypeConverters` and `Converters` `Cached`, an equal value, and every output cached; `Run_ConverterOfAReferenceChanged_LeavesMappedTypesUnchanged` (:220-241) pins `ResolvedTypes`, `MappedTypeConverters` and `Converters` `Modified` with `MappedTypes` `Unchanged` and the new method name.  Opposite outcomes of the same steps, each named in docs/architecture/generator.md:372.

### F-SqlSource.Tests.Generator-1-07

- test: `tests/SqlSource.Tests/Generator/CachingMethodTests.cs` | `CachingMethodTests.Run_DriverVersionEdit_ReEmitsNoMethod` | rows: all
- evidence: measured (kills 50, alone 0, timeouts under it 13; cover `CachingMethodTests.Run_CSharpEdit_ReEmitsNoMethod`, whose only cover is this test) and read: the cover edits a method body, so `MethodDrivers` is `Cached` (CachingMethodTests.cs:228); this test swaps Npgsql 9 for Npgsql 8, so `MethodDrivers` runs and must come out `Unchanged` (CachingMethodTests.cs:250), which is the equality of the drivers' value across versions, in src/SqlSource/Generation/, outside the root scope.  docs/architecture/generator.md:372 names "another version of the driver" apart from an edit to C#.; blind: no
- verdict: confirmed
- reason: Each is the other's only cover (evidence lines 79-80).  `Run_CSharpEdit_ReEmitsNoMethod` (tests/SqlSource.Tests/Generator/CachingMethodTests.cs:216-236) edits a method body and pins `MethodDrivers` `Cached` (:228); `Run_DriverVersionEdit_ReEmitsNoMethod` (:240-260) swaps Npgsql 9 for Npgsql 8 and pins `MethodDrivers` `Unchanged` (:250), the step running and giving an equal value, which the C# edit never makes it do.  docs/architecture/generator.md:372 names "another version of the driver" apart from an edit to C#.

### F-SqlSource.Tests.Generator-1-08

- test: `tests/SqlSource.Tests/Generator/CachingMethodTests.cs` | `CachingMethodTests.Run_EditThatKeepsTheLibrariesUsed_LeavesTheConversionOutputCached` | rows: all
- evidence: measured (kills 64, alone 0, timeouts under it 14; cover `CachingMethodTests.Run_FirstUseOfAnEdge_EmitsTheClass`, whose only cover is this test) and read: this test pins `ConversionLibraries` and `LibraryConversionOutput` `Cached` when an edit keeps the same library (CachingMethodTests.cs:642-643); the cover pins `Modified` and `New` when an edit first uses an edge (CachingMethodTests.cs:665-666).  Opposite outcomes of the same two steps.; blind: no
- verdict: confirmed
- reason: Each is the other's only cover (evidence lines 82 and 88).  `Run_EditThatKeepsTheLibrariesUsed_LeavesTheConversionOutputCached` (tests/SqlSource.Tests/Generator/CachingMethodTests.cs:622-646) pins `ConversionLibraries` and `LibraryConversionOutput` `Cached` (:642-643); `Run_FirstUseOfAnEdge_EmitsTheClass` (:650-669) pins them `Modified` and `New` (:665-666) and that the class is written.  Opposite outcomes of the same two steps.

### F-SqlSource.Tests.Generator-1-09

- test: `tests/SqlSource.Tests/Generator/CachingModelTests.cs` | `CachingModelTests.Run_ModelRenamedInPlace_EmitsOnlyThatModel` | rows: all
- evidence: measured (kills 56, alone 0, timeouts under it 13; cover `CachingModelTests.Run_FileThatGivesASharedModelRemoved_EmitsItFromTheNextFile`, whose only cover is this test) and read: this test renames a model by its marker and pins that its slot's node emits the new name alone, though it sorts after another model (CachingModelTests.cs:288-321); the cover removes the file that gives a shared model and pins that the model moves to the next file's node in one run (CachingModelTests.cs:363-400).  Different edits of the fan-out by file, each named in docs/architecture/generator.md:372 and in the proposed fix of docs/tech-debt/TD-0069, which keeps a change only "if every case of `CachingModelTests` holds".; blind: no
- verdict: confirmed
- reason: Each is the other's only cover (evidence lines 115 and 120).  `Run_ModelRenamedInPlace_EmitsOnlyThatModel` (tests/SqlSource.Tests/Generator/CachingModelTests.cs:291-321) renames the model by its marker and pins `OrderRow` `Modified`, the others `Cached` and the old file gone, though the new name sorts after `GetPaymentDto` (:288-290); `Run_FileThatGivesASharedModelRemoved_EmitsItFromTheNextFile` (:365-400) removes the file that gives a shared model and pins it `New` from the next file in one run, with no exception.  Different edits of the fan-out by file, both named in docs/architecture/generator.md:372, and docs/tech-debt/TD-0069-change-to-any-model-runs-every-files-model-node-again.md:17 keeps a change only "if every case of `CachingModelTests` holds".

### F-SqlSource.Tests.Generator-1-10

- test: `tests/SqlSource.Tests/Generator/CachingModelTests.cs` | `CachingModelTests.Run_SharedModelEditedInTheOtherFile_EmitsOnlyThatModelAgain` | rows: all
- evidence: measured (kills 59, alone 0, timeouts under it 13; cover `CachingModelTests.Run_SharedModelEditedInTheFileThatGivesIt_EmitsOnlyThatModelAgain`, whose only cover is this test) and read: the cover edits Orders, which gives the shared model, and its property changes to `Row(string Value)` (CachingModelTests.cs:109-119); this test edits Users, which does not give it, so the model is written again for its summary alone and keeps `Row(int Value)` (CachingModelTests.cs:123-133).  docs/architecture/generator.md:372 pins "an edit to either query of a shared model", and TD-0069 keeps every case of the class.; blind: no
- verdict: confirmed
- reason: Each is the other's only cover (evidence lines 123-124).  `Run_SharedModelEditedInTheFileThatGivesIt_EmitsOnlyThatModelAgain` (tests/SqlSource.Tests/Generator/CachingModelTests.cs:108-119) edits the sidecar of Orders and the model becomes `Row(string Value)`; `Run_SharedModelEditedInTheOtherFile_EmitsOnlyThatModelAgain` (:122-133) edits that of Users, so the model is written again for its summary alone and keeps `Row(int Value)`, as the comment at :104-106 and :121 says.  docs/architecture/generator.md:372 pins "an edit to either query of a shared model", and TD-0069:17 names every case of the class.

### F-SqlSource.Tests.Generator-3-04

- test: `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs` | `GeneratedSourceMethodTests.Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` | rows: all
- evidence: measured (75 rows, 30.7 s, the most costly test of the slice and most of `GeneratedSourceMethodTests`' 7.3% of tests/SqlSource.Tests; kills 68, alone 0, timeouts under it 14; rows needed 1; cover: `ConverterCompileTests.Run_ConvertedQueries_CompileWithNoWarning`) and read (each row is a token named like one local, lambda parameter or helper of the generated query and collection methods, and the helper at GeneratedSourceMethodTests.cs:364-436 checks for each that the generated code compiles clean and that the colliding local took its `1` suffix.  The renaming it guards is in `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs` and `src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchLocals.cs`, outside the generator-root scope, so "rows needed 1" and the cover only describe the root files the scope mutated: every row is the one guard of its own name); blind: no
- verdict: confirmed
- reason: Read tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs:280-436: each of the 75 rows is a name a local, lambda parameter or helper of the generated query and collection methods uses, and the helper asserts per name that the colliding local took its `1` suffix (lines 374, 383, 389-390, 398-427).  `kills` in generator-root gives 68 kills, 14 timeouts and 3 survivors, all in src/SqlSource/SqlSourceGenerator.cs and other root files, explicable as pipeline breaks that any clean-run test fails on; the renaming itself is in src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs:106,113 and src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchLocals.cs:15,30, outside the root scope, and the reports of generator-generation-emitting and generator-generation-methods hold no test of this name, so "rows needed 1" says nothing about the renaming.  The test is also a named record: src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md:15 and docs/architecture/generator.md:761 require a row in it for every local name a token could hide.  Keep is right.

### F-SqlSource.Tests.Parsing-1-12

- test: `tests/SqlSource.Tests/Parsing/SqlDeclaredVariablesTests.cs` | `SqlDeclaredVariablesTests.Find_WordThatStartsAStatement_EndsTheDeclaration` | rows: all
- evidence: measured (48 rows, kills 84, alone 0, timeouts under it 17; rows needed 1; cover `Find_NameAfterTheDeclarationHasEnded_IsNotDeclared`); read (each row is one word of `StatementStarters`, SqlDeclaredVariables.cs:24-75, the list the comment at SqlDeclaredVariablesTests.cs:50 names; the list is a static initialiser, so no mutant removes a word, and each row is the only guard of its word); blind: yes (SqlDeclaredVariables.cs:28-75, static)
- verdict: confirmed
- reason: The 48 rows at tests/SqlSource.Tests/Parsing/SqlDeclaredVariablesTests.cs:52-99 are the 48 words of `StatementStarters`, src/SqlSource/Parsing/SqlDeclaredVariables.cs:26-75, a static array that the evidence lists as blind (28-75).  No mutant removes a word, so "rows needed 1" measures only the code around the list.  The named cover, `Find_NameAfterTheDeclarationHasEnded_IsNotDeclared` (:49), uses only `SELECT` and `SET`.  Each row is the one guard of its word.

### F-SqlSource.Tests.Parsing-1-13

- test: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect` | rows: all
- evidence: measured (18 rows, kills 8, alone 0, timeouts under it 0; rows needed 1; cover `SqlDialectMarkerTests.Apply_CommentFormOfTheNewDialectBelowTheMarker_IsPartOfTheHeader`); read (the rows at SqlDialectNameTests.cs:13-25 are one for each entry of `Names`, SqlDialectName.cs:24-37, a static table that the settings schema repeats, so each row is the only guard of its alias; the rows at :27-31 hold the case and whitespace the doc comment of `TryParse` promises); blind: yes (SqlDialectName.cs:26-37, static)
- verdict: confirmed
- reason: The rows at tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs:13-24 cover each of the 12 entries of the static `Names` table, src/SqlSource/Parsing/SqlDialectName.cs:24-38, which is blind (26-37), so "rows needed 1" says nothing about the table.  The aliases `sqlserver`, `tsql`, `postgresql` and `cockroach` have no other test.  One part of the finding is overstated.  The canonical names are also parsed by `TryParse_EveryDialect_HasAName` (:118-130) and `Canonical_Dialect_IsItsFirstName` (:136-152), so their rows are not each name's only guard.  This does not change the action: the theory is the full mirror of the table, and the measured cover, `SqlDialectMarkerTests.Apply_CommentFormOfTheNewDialectBelowTheMarker_IsPartOfTheHeader`, parses a single name.

### F-SqlSource.Tests.Parsing-1-14

- test: `tests/SqlSource.Tests/Parsing/SqlDeclaredVariablesTests.cs` | `SqlDeclaredVariablesTests.Find_DialectWithoutDeclaredVariables_FindsNone` | rows: all
- evidence: measured (7 rows, kills 8, alone 0, timeouts under it 8; rows needed 1; cover `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone`); read (whether a dialect has declared variables is `DeclaredVariables = true` in the static rules of SQL Server alone, SqlDialectRules.cs:81, so each row is the only unit-level guard that its dialect's rules leave the flag off; the named cover tests PostgreSQL alone); blind: yes (SqlDialectRules.cs:80-82, static)
- verdict: confirmed
- reason: The seven rows at tests/SqlSource.Tests/Parsing/SqlDeclaredVariablesTests.cs:191-206 each call `SqlDeclaredVariables.Find` under one dialect's rules and expect null.  The flag that decides, `DeclaredVariables` at src/SqlSource/Parsing/SqlDialectRules.cs:81 (read at SqlDeclaredVariables.cs:109), is set in static initialisers and is true for SQL Server alone, so mutation cannot see a row's dialect.  The measured cover, `Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone`, runs PostgreSQL alone.  The parser theory `Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` checks each dialect through the whole parse, but only this test does it at the unit level, as the finding says.

### F-SqlSource.Tests.Parsing-2-17

- test: `tests/SqlSource.Tests/Parsing/Tokens/SqlListSegmentsTests.cs` | `SqlListSegmentsTests.Split_NoList_ReturnsTheSameInstance` | rows: all
- evidence: measured (2 rows; covers 2 mutants and kills none; survivor src/SqlSource/Parsing/Tokens/SqlListSegments.cs:140:16 Boolean mutation `true` under it alone); read (the mutant skips the early return of `Split`, SqlListSegments.cs:44-47, and the walk then splits nothing and builds nothing, so it gives the same instance and allocates nothing: an equivalent mutant; what the test holds, that a query with no list gets its segments back unallocated, SqlListSegmentsTests.cs:191-206, is an allocation that mutation cannot see and that no other test checks); blind: yes (SqlListSegments.cs:43-128 does not compile under some mutants)
- verdict: confirmed
- reason: SqlListSegments.cs:140 is `return false;` of `HasList`, and the 4th argument of `SqlQueryParameter` is `IsDeclared`, not `IsList`.  In the `hasUses: true` row, the test's parameter `other` is therefore no list, and the original returns early at :44-47.  Under the mutant the walk runs.  `KindOf` finds no parameter named `v` and gives null, so `split` stays null.  The walk returns the same segments and allocates nothing.  That makes the mutant equivalent.  The test's zero-allocation assertion (SqlListSegmentsTests.cs:191-206) is what mutation cannot see.  No other test calls `Split` directly or measures its allocation.

### F-SqlSource.Tests.Parsing-2-18

- test: `tests/SqlSource.Tests/Parsing/SqlModelTests.cs` | `SqlModelTests.SqlParseError_SameValuesInSeparateInstances_AreEqual` | rows: all
- evidence: measured (1 row; reaches no tested mutant); read (`SqlParseError` is a record whose equality the compiler writes, src/SqlSource/Parsing/SqlParseError.cs:15, which mutation cannot change; every `errors.ShouldBe([SqlParseError.Create(...)])` relies on equality by value, and the assertion that errors differing only in an argument are unequal, SqlModelTests.cs:31, is made by no other test, so an `Arguments` that compared by reference or not at all would let those tests pass wrongly); blind: no
- verdict: confirmed
- reason: `SqlParseError` is a positional record over `EquatableArray<string>` (SqlParseError.cs:15).  SqlModelTests.cs:31 is the only `ShouldNotBe(SqlParseError...)` in tests/, and so the only check that errors which differ only in an argument are unequal.  If equality ignored `Arguments`, every `errors.ShouldBe([SqlParseError.Create(...)])` would still pass.  `kills` shows it reaches no mutant, and what it checks is compiler-written equality.

### F-SqlSource.Tests.Parsing-2-19

- test: `tests/SqlSource.Tests/Parsing/SqlModelTests.cs` | `SqlModelTests.SqlBlock_SameValuesInSeparateInstances_AreEqual` | rows: all
- evidence: measured (1 row; reaches no tested mutant); read (`SqlBlock` is a record, src/SqlSource/Parsing/SqlBlock.cs:49, whose compiler-written equality mutation cannot change; the test holds that each of its collections, the segments, the kept segments, the tokens, the parameters, the column markers and the method errors, takes part in equality by value, SqlModelTests.cs:79-94, which the generator's caching depends on and no other test of the slice checks); blind: no
- verdict: confirmed
- reason: `SqlBlock` is a positional record (SqlBlock.cs:49-63).  The test (SqlModelTests.cs:79-94) varies, one at a time, the segments, a parameter, a token's default, the shape, the kept segments, the markers, a column marker and a kept error, and asserts equality and inequality.  One slip: the finding calls the last field "method errors".  The test's `keptError` is in fact `KeptErrors`, the 13th positional field.  The substance is right: each varied value takes part in equality by value, and the test reaches no tested mutant.

### F-SqlSource.Tests.Parsing-2-20

- test: `tests/SqlSource.Tests/Parsing/SqlParameterNameTests.cs` | `SqlParameterNameTests.Comparer_Names_AreEqualWhenTheyDifferOnlyInCase` | rows: all
- evidence: measured (2 rows; reaches no tested mutant); read (`SqlParameterName.Comparer` and `Comparison`, src/SqlSource/Parsing/SqlParameterName.cs:21-24, are a property and a constant that mutation does not change, and src/SqlSource/Parsing/AGENTS.md names them the one rule for parameters' names that the generator and the tool share; no other test of the slice reads `Comparer`, SqlParameterNameTests.cs:58, and the `IndexOf` tests reach `Comparison` alone); blind: no
- verdict: confirmed
- reason: `Comparer` is an expression-bodied property and `Comparison` a const (SqlParameterName.cs:21-24).  `kills` shows that the test reaches no tested mutant.  `IndexOf` (:48-59) uses `Comparison` alone, so the `IndexOf` tests never read `Comparer`.  SqlParameterNameTests.cs:58 is the slice's only read of `Comparer`.  src/SqlSource/Parsing/AGENTS.md makes the pair the one rule the generator and the tool share, and the tool builds sets and dictionaries with `Comparer` (for example, RenamingCopies.cs:48 and SqlServerSession.cs:164).

### F-SqlSource.Tests.Settings-1-03

- test: `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs` | `ColumnTypeMapTests.Over_OneSideNull_IsTheOther` | rows: all
- evidence: measured (covers 2 mutants and kills none; survivor src/SqlSource/Settings/SortedKeyedList.cs:35:9 Block removal mutation); read (ColumnTypeMapTests.cs:262-268: the surviving mutant merges with an empty list and gives an equal copy, which the name, "is the other", allows; it is the only test of the column types' `Over` with one side null, since every other `Over` of ColumnTypeMapTests and QuerySettingsTests has two lists); blind: no
- verdict: confirmed
- reason: read ColumnTypeMapTests.cs:261-268 and src/SqlSource/Settings/SortedKeyedList.cs:29-37: `kills` shows the one mutant it reaches that lives, 35:9 block removal, which merges with an empty list and gives an equal copy; the name says "is the other", not "itself", so a value comparison reads it rightly.  Every other `ColumnTypeMap.Over` in ColumnTypeMapTests and QuerySettingsTests has two lists, and `SettingsLevel.Over` (src/SqlSource/Settings/SettingsLevel.cs:128) is only reached in QuerySettingsTests with both column types null or both set (QuerySettingsTests.cs:101-108, 141-147, 314-320, 437-452), so it is the one test of `ColumnTypeMap.Over` with one side null.

### F-SqlSource.Tests.Settings-1-10

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_ModelSettings_HaveTheirDefaults` | rows: all
- evidence: measured (covers 32 mutants and kills none); read (QuerySettingsTests.cs:150-160 asserts the defaults `Params`, `Dto`, `SealedRecord` and `Array`, which are constants and enum members that mutation does not change; no other test of the slice states them); blind: no
- verdict: confirmed
- reason: read QuerySettingsTests.cs:149-160: it resolves two `None` levels and states `Params`, `Dto`, a null namespace, `SealedRecord` twice and `Array`; the suffixes are the constants of QuerySettings.cs:44 and 47, which mutation does not change, and `kills` shows only survivors and no kill.  A grep of tests/SqlSource.Tests/Settings finds `"Params"` and the default collection only here, so it is the one guard of those defaults in the slice.

### F-SqlSource.Tests.Settings-1-12

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_PlacementSettings_HaveTheirDefaults` | rows: all
- evidence: measured (covers 32 mutants and kills none); read (QuerySettingsTests.cs:192-200 asserts that no member location or visibility is written by default and that the nested class is `Sql`, `QuerySettings.DefaultNestedClass`, a constant that mutation does not change); blind: no
- verdict: confirmed
- reason: read QuerySettingsTests.cs:191-200: it states that nothing writes the member location or the visibility by default and that the nested class is `Sql`, the constant `QuerySettings.DefaultNestedClass` (QuerySettings.cs:53); `kills` shows no kill and only survivors on other lines.  `MemberLocation_NoLevelWritesOne_IsTheDefaultOfTheOutput` (QuerySettingsTests.cs:202-216) checks a null written location with an output set, but no other test states the default visibility or `Sql`.

### F-SqlSource.Tests.Settings-1-13

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.ResolveColumnTypes_NoLevelSetsOne_IsEmpty` | rows: all
- evidence: measured (covers 2 mutants and kills none); read (QuerySettingsTests.cs:432-434 is the only test of the `?? EquatableArray<ColumnType>.Empty` of QuerySettings.cs:143, whose mutants do not change a value that compiles; `ResolveTypeOverrides_NoLevelSetsOne_IsEmpty` is the type override's twin, not this one's); blind: no
- verdict: confirmed
- reason: read QuerySettingsTests.cs:432-434 and QuerySettings.cs:142-143: `kills` shows no kill and no survivor; of the mutants of `?? EquatableArray<ColumnType>.Empty`, remove left is killed by `ResolveColumnTypes_Levels_MergePerKeyAcrossTheLevels` and the others do not compile, so its numbers say nothing of the value it states, an empty list for a query no level gives column types.  `ResolveTypeOverrides_NoLevelSetsOne_IsEmpty` (QuerySettingsTests.cs:294-298) states it for the type override, a different member.

### F-SqlSource.Tests.Settings-1-15

- test: `tests/SqlSource.Tests/Settings/SettingPlacesTests.cs` | `SettingPlacesTests.Specificity_OfADeepSettingsFile_StaysBelowTheFileHeader` | rows: all
- evidence: measured (covers 1 mutants and kills none); read (SettingPlacesTests.cs:46-49 asserts that a settings file 10,000 folders deep still ranks below the file's header, the bound that `1 + Depth` against `int.MaxValue - 1` at SettingPlaces.cs:13-15 keeps and docs/architecture/generator.md describes; the other two Specificity tests use depths up to 2); blind: no
- verdict: confirmed
- reason: read SettingPlacesTests.cs:45-49 and src/SqlSource/Settings/SettingPlaces.cs:10-17: it is the one test with a depth past 2, holding that `1 + Depth` stays below `int.MaxValue - 1` for a deep tree, a bound no mutant of the scope changes (`kills` shows no kill and no survivor); docs/architecture/generator.md:413 states the ranking `1 + Depth` below `FileHeader` that it holds.

### F-SqlSource.Tests.SettingsFiles-1-01

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderRobustnessTests.cs` | `SettingsFileReaderRobustnessTests.Read_DesignsExampleWithACharacterReplacedAnywhere_Returns` | rows: all
- evidence: measured (1 row, 0.418 s; kills 6, alone 0, timeouts under it 1; cover: `SettingsFileSchemaTests.Schema_FileTheReaderRejects_IsNotValid`).  Read: the cover runs 39 fixed texts and asserts only that their errors are not empty (SettingsFileSchemaTests.cs:537-541); this test feeds about 6,000 corrupted copies of the design's example through the reader (SettingsFileReaderRobustnessTests.cs:32-52) and is the one test of the slice that proves the reader never throws on an arbitrary edit and that `Root` is null exactly when the text is unreadable, the rule the class comment states (lines 12-13).  Throwing on an unforeseen input is not something the cover's fixed rows can show, and it has a timeout under it that no report attributes; blind: no
- verdict: confirmed
- reason: `kills` on generator-settings-files gives 6 kills, 0 alone, 1 timeout (SettingsFileReader.cs:326:39), as the evidence says.  The measured cover `Schema_FileTheReaderRejects_IsNotValid` (tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs:537-541) only asserts that the schema's errors and the reader's errors are not empty on fixed texts.  The test (SettingsFileReaderRobustnessTests.cs:31-52) puts 13 replacement characters at every offset of the design's example.  For each result it checks that `Root` is null exactly when an `InvalidJson` or `NotAnObject` error is present.  The class comment (lines 12-13) says the reader must never throw, and src/SqlSource/AGENTS.md says nothing a user writes may make the generator throw.  No fixed row of the cover proves that, so the finding reads the test rightly.

### F-SqlSource.Tests.SettingsFiles-1-02

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_TwoReadsOfOneText_AreEqual` | rows: all
- evidence: measured (1 row, 0.002 s; kills 5, alone 0, timeouts under it 1; cover: `SettingsFileReaderRobustnessTests.Read_DesignsExampleWithACharacterReplacedAnywhere_Returns`).  Read: the cover never reads a text twice nor compares two results (SettingsFileReaderRobustnessTests.cs:32-52); this test is the only one in the slice that asserts two reads of one text are equal and hash alike, for a text with errors, a type override and an unknown key and for the design's example (SettingsFileReaderTests.cs:762-776), which is what lets the generator cache a `SettingsFile` (docs/architecture/generator.md:440).  The equality is the record's and `EquatableArray`'s, which the scope did not mutate; blind: no
- verdict: confirmed
- reason: `kills` gives 5 kills, 0 alone, 1 timeout (SettingsFileReader.cs:326:39).  One kill is 117:26, which inverts the unknown-key check: it sends the unknown key `"x"` into `SettingsFileKeys.TypeOf`, which throws, so the kill is explained by the throw and not by equality.  The measured cover (SettingsFileReaderRobustnessTests.cs:31-52) never reads a text twice or compares results.  The test (SettingsFileReaderTests.cs:762-776) asserts equality and equal hash codes for two reads of one text, and equality again for the design's example.  docs/architecture/generator.md:440 says `SettingsFile` is a record compared by value so that the generator can cache it, and the scope did not mutate that equality.

### F-SqlSource.Tests.SettingsFiles-1-03

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_TextNestedDeeperThanTheLimit_IsInvalidJsonAtTheBracketThatOpensLevel65` | rows: all
- evidence: measured (1 row, 0.000 s; kills 2, alone 0, timeouts under it 0; cover: `SettingsFileReaderRobustnessTests.Read_DesignsExampleCutShortAnywhere_ReturnsWithAnError`, which in turn names this test as its own cover).  Read: the cover cuts the design's example short, which never nests more than two levels, and asserts only that an `InvalidJson` error is among the errors (SettingsFileReaderRobustnessTests.cs:17-29); this test pins the depth limit itself, an error at exactly the bracket that opens level 65 (SettingsFileReaderTests.cs:143-161).  The limit is `SidecarTokenizer.MaxDepth` in src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs:24, outside the scope's source, so its kills here are only of the reader's error handling; blind: no
- verdict: confirmed
- reason: `kills` gives 2 kills (SettingsFileReader.cs:70:13 and 71:9, the `TrySkipValue` failure branch) and no timeouts.  `SidecarTokenizer.MaxDepth = 64` is at src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs:24, outside the mutated folder.  The cover `Read_DesignsExampleCutShortAnywhere_ReturnsWithAnError` (SettingsFileReaderRobustnessTests.cs:16-29) cuts short the design's example, which nests no deeper than an object inside the root (SettingsFileExamples.cs:9-26).  It asserts only a null `Root` and an `InvalidJson` error among the errors.  The test (SettingsFileReaderTests.cs:143-161) is the slice's one assertion of the exact limit, an error spanning the bracket that opens level 65.  The two tests are each other's measured cover and prove different behaviours.

### F-SqlSource.Tests.SettingsFiles-1-04

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_TypeOverrideMembers_AreReportedByTheirSpansAndCarryThePlace` | rows: all
- evidence: measured (1 row, 0.001 s; kills 27, alone 0, timeouts under it 0; cover: `SettingsFileReaderTests.Read_ColumnTypeMembers_AreReportedByTheirSpansAndCarryThePlace`).  Read: the cover's members are column types, whose repeat is a quoted spelling of one column (SettingsFileReaderTests.cs:429-464); this test's repeat is `integer` beside `int4`, two names the PostgreSQL map takes as one type, and its invalid member is a blank-padded key and type that are reported trimmed (lines 390-427).  Which keys are one and how a member is trimmed are the type override's own rules, in src/SqlSource/Settings/TypeOverrides, outside the scope, so the measured cover sees only the shared reporting path in SettingsFiles; blind: no
- verdict: confirmed
- reason: `kills` gives 27 kills, all on the shared reporting path in SettingsFiles/.  The cover `Read_ColumnTypeMembers_...` (SettingsFileReaderTests.cs:429-464) repeats a key only through its quoted spelling `"users".id`.  This test (lines 390-427) repeats a type through `integer` beside `int4`.  That works only because of the PostgreSQL alias at src/SqlSource/Settings/TypeOverrides/TypeKeyNames.cs:157.  The test also asserts that the blank-padded key and type of the invalid member are trimmed, as TypeOverrideGrammar.cs:75-76 trims them.  Those rules are outside the scope, so the kill set does not show them.

### F-SqlSource.Tests.SettingsFiles-1-05

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_LibraryTypeMembers_AreReportedByTheirSpansAndCarryThePlace` | rows: all
- evidence: measured (1 row, 0.000 s; kills 27, alone 0, timeouts under it 0; cover: `SettingsFileReaderTests.Read_ColumnTypeMembers_AreReportedByTheirSpansAndCarryThePlace`).  Read: this test is the slice's one check that a library type whose state is not a choice (`"maybe"`) is invalid with both its name and value quoted, and that `noda-time` after `nodaTime` is a repeat of the first (SettingsFileReaderTests.cs:694-725); the cover reads column types only (lines 429-464).  The library types' choices and aliases are in src/SqlSource/Settings/Libraries, outside the scope; blind: no
- verdict: confirmed
- reason: `kills` gives 27 kills, the same set as F-04's test, which is the shared reporting path.  The cover reads only column types (SettingsFileReaderTests.cs:429-464).  This test (lines 694-725) is the one reader test that asserts the `InvalidValue` error, quoting name and value, for a library state that is not a choice (`"maybe"`).  The schema's row of the same text (SettingsFileSchemaTests.cs:514) checks only that the errors are not empty.  The test is also the one place that asserts the `RepeatedKey` error for `noda-time` after `nodaTime`.  `Read_LibraryMembers_AreThoseThatGiveEntries` (lines 680-692) uses that text but asserts only the member's span.  The finding is slightly loose on where the rule lives.  The match that ignores hyphens is `SettingValue.TryReadChoice` in src/SqlSource/Settings, called from src/SqlSource/Settings/Libraries/LibrarySettings.cs:82-83, but both are outside the scope as the finding says.

### F-SqlSource.Tests.SettingsFiles-1-06

- test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` | `SettingsFileReaderTests.Read_KeyNotInTheTable_IsUnknown` | rows: all
- evidence: measured (1 row, 0.002 s; kills 15, alone 0, timeouts under it 0; cover: `SettingsFileReaderTests.Read_Errors_AreInTheOrderOfTheirPositions`).  Read: the cover holds one unknown key but asserts only the kinds of the errors, their order and the first one's setting (SettingsFileReaderTests.cs:727-744); this test asserts the unknown key's span and key, that keys match ordinally so `"Dialect"` is unknown and sets no dialect, and that a container value under an unknown key is skipped (lines 180-195); blind: no
- verdict: confirmed
- reason: `kills` gives 15 kills, 0 alone, no timeouts.  The cover `Read_Errors_AreInTheOrderOfTheirPositions` (SettingsFileReaderTests.cs:727-744) asserts only the error kinds, their order by start and the first error's setting.  This test (lines 180-195) asserts the full `UnknownKey` errors with span and key, a null dialect for `"Dialect"`, and a false `Root`.  Its comment at line 180 records that keys match ordinally, a rule also stated at docs/architecture/generator.md:436.  It also skips a container value under an unknown key (`[1, {}]`).  The cover asserts none of these.

### F-SqlSource.Tests.Snapshot-1-10

- test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderRobustnessTests.cs` | `SidecarReaderRobustnessTests.Read_FileDamagedAnywhere_ReturnsOneOfItsThreeStates` | rows: all
- evidence: measured (2 rows, 15.4 s, the slowest test of the slice; kills 37, alone 0, timeouts under it 31; rows needed 1; cover `SidecarReaderRobustnessTests.Read_ReorderedFileCutShortOrDamagedAnywhere_ReturnsOneOfItsThreeStates`, `SidecarReaderAllocationTests.Read_UsersExample_AllocatesWithinItsBudget`) and read (the cover damages `SidecarReaderTests.Reordered`, one PostgreSQL entry with a base and an array type (SidecarReaderTests.cs:1069-1085); this test damages the two examples, whose texts are in the tool's order and hold what that one lacks: the SQL Server types, facets and user types of `Orders.sql.json` and the enums, domains and labels of `Users.sql.json`.  A damage that leaves the JSON well formed reaches those type readers, so the promise of the file's comment (SidecarReaderRobustnessTests.cs:12-13), that nothing makes the reader throw, is proved there only here; the allocation test checks one undamaged read.  It has 31 timeouts under it, the most of any test of the slice with the round trip and the reordered test, and may be what detects a reader that loops on a damaged file; src/SqlSource/Snapshot/AGENTS.md makes "the reader never throws" a rule); blind: no
- verdict: confirmed
- reason: tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderRobustnessTests.cs:56-74 damages the two examples, whose texts hold SQL Server types, facets and user types (Orders.sql.json) and enums with labels and domains (Users.sql.json, as SidecarReaderTests.cs:152-187 reads them); the cover `Read_ReorderedFileCutShortOrDamagedAnywhere_ReturnsOneOfItsThreeStates` (:76-99) damages `SidecarReaderTests.Reordered` (SidecarReaderTests.cs:1069-1085), one PostgreSQL entry with a base and an array type, and the allocation test reads one undamaged file.  The file's comment (:12-13) and src/SqlSource/Snapshot/AGENTS.md ("The reader never throws") make not throwing the rule, and only this test brings damaged SQL Server and enum shapes to the type readers; it has 31 timeouts under it.  The keep reads the test rightly, both rows included.

### F-SqlSource.Tool.Tests-2-07

- test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs` | `DotEnvParserTests.Parse_ErrorAmongGoodLines_LeavesTheGoodLines` | rows: all
- evidence: measured (1 row, kills 31, alone 0, timeouts under it 2; cover: `DotEnvParserTests.Parse_ToolLineThatCannotBeRead_IsOneErrorThatNamesNoPartOfTheValue`, `DotEnvParserTests.Parse_Text_HoldsTheValuesOfTheToolsKeys`).  Read: it is the one test that a good line of a tool key after a bad one is still read; the `Failures` rows assert only that the bad key is absent (DotEnvParserTests.cs:230), and the `Syntax` rows have no error.  It has 2 timeouts under it.  blind: no
- verdict: confirmed
- reason: Read DotEnvParserTests.cs:235-244 against the `Failures` rows (:151-233) and the `Syntax` rows (:19-145): the Failures test asserts only the error and that the bad key is absent (:228-230), even for the row at :199 whose first line is good, and every Syntax row asserts no error; so this is the one test that a good tool line on each side of a bad one is kept.  `kills` (tool-dotenv) shows 2 TIMEOUTs under it (DotEnvParser.cs:186:17, 247:16), another reason it is not removable on its numbers.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests-2-08

- test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs` | `DotEnvParserTests.Parse_VariableOfADatabase_IsAKeyOfTheFile` | rows: all
- evidence: measured (7 rows, kills 27, alone 0, timeouts under it 1; rows needed 1; cover: `DotEnvParserTests.Parse_ErrorAmongGoodLines_LeavesTheGoodLines`).  Read: the subject is that the name `ConnectionVariables.For` writes (src/SqlSource.Tool/Describing/ConnectionVariables.cs:22-31) is a key the parser takes, as the comment at DotEnvParserTests.cs:264 says; `For` is outside the tool-dotenv scope, so no mutant of it was run, and each row is a kind of name it rewrites (letters outside ASCII, a digit outside ASCII, `-` and `.`).  blind: no
- verdict: confirmed
- reason: Read DotEnvParserTests.cs:264-279 and ConnectionVariables.cs:22-31: the theory feeds `ConnectionVariables.For(database)`, which lives in src/SqlSource.Tool/Describing and is outside the tool-dotenv scope, so no mutant of it was run and `rows needed 1` says nothing of it; the rows are the kinds of name For rewrites or keeps (ASCII, letters outside ASCII in either case, `ß` which upper-cases to itself, CJK letters, a digit outside ASCII, `-` and `.`).  The comment at :264 names the test as the record of that agreement, and docs/architecture/tool.md:158 states the same rule.  The 1 TIMEOUT under it (247:16) is confirmed by `kills`.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests-2-09

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs` | `RunLogFileTests.Open_PathOfAFileOfTheRun_GivesNoLogAndTheReasonAndLeavesTheFileAsItWas` | rows: all
- evidence: measured (7 rows, kills 4, alone 0, timeouts under it 0; rows needed 1; cover: `RunLogDestinationTests.Run_LogOptionThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas`).  Read: each row is an entry of `FilesOfTheRun` (src/SqlSource.Tool/Logging/RunLogFile.cs:40-47) or the rule that the case does not matter (`USERS.SQL`, `sub/App.CsProj`); a mutant that blanks an ending is killed by any row, and no mutant removes one, so the count of rows needed says nothing of them.  RunLogFile.cs:44-46 is blind (static).  blind: yes
- verdict: confirmed
- reason: Read RunLogFileTests.cs:251-275 and RunLogFile.cs:40-47, 126-127: each row is one ending of `FilesOfTheRun` or the rule that the case does not matter (`USERS.SQL`, `sub/App.CsProj` against `OrdinalIgnoreCase`); the endings `.csproj`, `.sln` and `.slnx` are string literals of a static initialiser (RunLogFile.cs:44-46, blind, static, in the evidence), so no mutant can remove one and only these rows guard them.  The named cover, `Run_LogOptionThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas`, writes only `Users.sql` (RunLogDestinationTests.cs:126-128).  The comment at :251 says why the rule exists.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests-2-10

- test: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` | `RunLogTests.Run_SidecarThatCannotBeDeleted_HasTheActionFailed` | rows: all
- evidence: measured (1 row, 0.960 s, about half of `RunLogTests`' 1.8 s; kills 36, alone 0; cover: `RunLogTests.Run_SidecarThatCannotBeWritten_HasTheActionFailed`).  Read: it guards the delete's own `failed: !deleted` (src/SqlSource.Tool/Describing/SidecarStore.cs:127), apart from the write's at line 123, which the cover proves; `SidecarStore` is outside the tool-logging scope, so the cover's numbers cannot see the difference.  It needs no database or process.  blind: no
- verdict: confirmed
- reason: Read RunLogTests.cs:621-649 and :373-383 with SidecarStore.cs:117-133: the cover makes a write fail (`failed: !written`, line 123), this test a delete (`failed: !deleted`, line 127), which nothing else checks in the log: SidecarStoreTests.cs:173-181 and :355-383 assert only the SQLSRC218 line of a failed delete, and VerboseTests' line `could not be changed` (VerboseTests.cs:177-190) is of a sidecar that cannot be written.  SidecarStore is outside tool-logging, and the tool-describing report does not hold this test (`kills` says so), so no number sees the difference.  It changes a file mode and runs no database or process.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests-2-11

- test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvSearchTests.cs` | `DotEnvSearchTests.Read_ReaderDeniesAccess_ReportsSqlsrc285AndGivesNothing` | rows: all
- evidence: measured (1 row, kills 2, alone 0; cover: `DotEnvSearchTests.Read_ReaderThrows_ReportsSqlsrc285AndGivesNothing`, which in turn has this test as its cover).  Read: the two tests are the two exception types of the filter at src/SqlSource.Tool/DotEnv/DotEnvSearch.cs:67, `IOException` and `UnauthorizedAccessException`; lines 64-74 are blind (do not compile), so no mutant could tell them apart.  blind: yes
- verdict: confirmed
- reason: Read DotEnvSearchTests.cs:137-167 and DotEnvSearch.cs:62-74: the two tests throw the two types of the filter at line 67, `IOException` and `UnauthorizedAccessException`, and lines 64-74 are blind (do not compile), so no mutant could drop one type and each test's 2 kills (confirmed by `kills`, no timeouts) are shared.  Each is the only guard of its type.  The keep reads the tests rightly.

### F-SqlSource.Tool.Tests-3-06

- test: `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs` | `ProcessRunnerTests.Run_ProgramThatReadsItsInput_FindsTheEndOfIt` | rows: all
- evidence: measured (covers 13 mutants and kills none; timeouts under it 1; timeouts of ProcessRunner.cs at lines 71, 89, 99, 110) and read: the one change it guards is the removal of `process.StandardInput.Close()` (src/SqlSource.Tool/Processes/ProcessRunner.cs:89), after which `sort` waits for its input for ever, as the test's comment says (ProcessRunnerTests.cs:92).  Such a mutant shows as the timeout at line 89, for which a report names no killer, and no other test of the slice runs a program that reads its input; blind: no
- verdict: confirmed
- reason: The test runs `sort` with no file (tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs:87-97), which ends only when its input is closed, as the comment at line 92 says; the one line it guards is `process.StandardInput.Close()` (src/SqlSource.Tool/Processes/ProcessRunner.cs:89), whose removal makes it wait for ever.  `kills` in tool-processes shows that mutant as `TIMEOUT src/SqlSource.Tool/Processes/ProcessRunner.cs:89:13 Statement mutation` under it, for which a report names no killer, and no other test of the slice runs a program that reads its input (`dotnet --version`, `env`, `cmd /c set` and the waiters do not).  A wait is what mutation cannot see, so its kills of none are no evidence against it.

### F-SqlSource.Tool.Tests-3-07

- test: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs` | `AttributeReaderTests.Read_FileWithoutTheWord_IsNotParsed` | rows: all
- evidence: measured (covers 7 mutants and kills none) and read: the search for the word before parsing is at src/SqlSource.Tool/Planning/AttributeReader.cs:47-53, inside the lines 37-77 that the evidence marks blind, so no mutant of it was tested.  The test fails without that search: `[SqlSourceGenerate]` (AttributeReaderTests.cs:386) parses to the identifier whose value text is `SqlSourceGenerate`, which `LastIdentifier` (AttributeReader.cs:177-184) would match into a claim; blind: yes
- verdict: confirmed
- reason: The search for the word is at src/SqlSource.Tool/Planning/AttributeReader.cs:47-53, inside the lines 37-77 that the evidence marks blind (does not compile), so `kills` in tool-planning shows 0 kills and only the two survivors at AttributeReader.cs:83:13 under it.  The input `using SqlSource;\n\n[SqlSourceGenerate]\n...` (tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs:54-55, 386) holds no "SqlSourceGenerate" in its text, but the escape `G` is `G`, so parsed it would be an identifier whose `ValueText` is `SqlSourceGenerate`, which `LastIdentifier` (AttributeReader.cs:177-184) and `FindAttribute` (line 98) would take for a claim: without the search the test fails.  It is the only test of that search.

### F-SqlSource.Tool.Tests-3-08

- test: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs` | `FileTextTests.SqlFileText_FileChangedAfterTheFirstRead_GivesTheFirstText` | rows: all
- evidence: measured (covers 5 mutants and kills none) and read: the mutants it covers are of `FileText.Read`, which it does not test; its subject is that `SqlFileText` reads its file once, through the `Lazy` at src/SqlSource.Tool/Planning/SqlFileText.cs:20, which no mutator changes.  It writes a second text after the first read and asserts the first (FileTextTests.cs:96-106), and no other test of the slice does; blind: no
- verdict: confirmed
- reason: The test (tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs:96-106) reads a file through `SqlFileText`, rewrites it, and asserts the first text again, which holds only because `SqlFileText` reads through a `Lazy` (src/SqlSource.Tool/Planning/SqlFileText.cs:20) that no mutator changes; `kills` in tool-planning: 0 kills, 0 timeouts, 0 survivors under it, the mutants it covers being those of `FileText.Read`.  The read-once rule is stated in SqlFileText.cs:12 and in docs/architecture/tool.md:121 ("`SqlFileText` reads the file once").  A grep of tests/ finds `SqlFileText` built only here, in RunPlannerSettingsFileTests.cs:143 (a single read of a settings file) and in tests/SqlSource.Tool.Testing/Fixtures/FixtureReplay.cs:117, none of which reads twice.

### F-SqlSource.Tool.Tests-3-09

- test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs` | `RunPlannerTests.Plan_RunThatWasCancelled_Throws` | rows: all
- evidence: measured (covers 2 mutants and kills none; survivor src/SqlSource.Tool/Planning/RunPlanner.cs:165:9 Statement mutation, covered by 56 methods) and read: the plan checks the token at RunPlanner.cs:165 and, before that, in `AttributeReader.Read` at src/SqlSource.Tool/Planning/AttributeReader.cs:43, on the blind lines 37-77, so removing the planner's check leaves the reader's to throw, and the reader's check, the one the test reaches first, is blind.  It is the only test of the slice that a cancelled plan throws (RunPlannerTests.cs:523-535); blind: yes
- verdict: confirmed
- reason: With a token cancelled before the call, `RunPlanner.Plan` reaches `ReadProject`, then `AttributeReader.Read`, which throws at `cancellationToken.ThrowIfCancellationRequested()` (src/SqlSource.Tool/Planning/AttributeReader.cs:43) on the first compile file, before the planner's own check at RunPlanner.cs:165, so removing that check leaves the test passing, and line 43 is on the blind lines 37-77.  `kills` in tool-planning confirms it: the 2 mutants it covers are the survivors AttributeReader.cs:83:13 (the parse options, made before the loop), and RunPlanner.cs:165:9 is not among them, since the test never reaches it; the finding's citing of that survivor is context, and its reading, that the kills of none come from a check on blind lines and not a weak assertion, holds.  No other test of tests/SqlSource.Tool.Tests/Planning/ passes a cancelled token (grep of `Cancel`).

### F-SqlSource.Tool.Tests-4-07

- test: `tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs` | `PollingFolderWatcherTests.Poll_NotRecursiveLinkToAFolderCreated_RaisesCreatedFolderAlone` | rows: all
- evidence: measured (kills 9, alone 0, timeouts under it 0; cover: `PollingFolderWatcherTests.Poll_NotRecursiveFolderCreated_RaisesCreatedFolderAlone`); read (a link is a reparse point, and the watch of a folder alone lists it by name only because the non-recursive branch comes before the link check, src/SqlSource.Tool/Watching/PollingFolderWatcher.cs:138-149; before TD-0110 the two were one condition that skipped the link, and the plan docs/superpowers/plans/2026-11-06-td-0110-poller-sub-folders.md:31 records the decision that a link in a folder watched alone is listed by name; moving the check back above the branch breaks this test alone, since the cover makes a plain folder); blind: no
- verdict: confirmed
- reason: The test (tests/SqlSource.Tool.Tests/Watching/PollingFolderWatcherTests.cs:207-222) makes a link to a folder in a folder watched alone and expects one CreatedFolder.  This holds only because the `!_request.Recursive` branch at src/SqlSource.Tool/Watching/PollingFolderWatcher.cs:139-143 comes before the reparse-point skip at :146-149.  With the skip moved first, the link goes unlisted and this test alone fails, since the cover, `Poll_NotRecursiveFolderCreated_RaisesCreatedFolderAlone` (:164-175), makes a plain folder.  `kills` on tool-watching gives the link test the same kills as its cover, minus 142:21 statement, which the reparse skip still covers for a link; there are no timeouts.  docs/architecture/tool.md:269 records the behaviour ("a link included" in the listing of a folder watched alone, and "follows no link to a folder").  The decision is also in docs/superpowers/plans/2026-11-06-td-0110-poller-sub-folders.md:31.  Mutation cannot reorder the two checks, so keep reads the test rightly.

### F-SqlSource.Tool.Tests.Bundles-1-04

- test: `tests/SqlSource.Tool.Tests/Bundles/BundleReplayTests.cs` | `BundleReplayTests.RunAsync_CaptureWithoutItsDeallocate_IsMissedWhileDescribing` | rows: all
- evidence: measured (kills 21, alone 0; cover `RunAsync_CaptureWithoutItsExplain_IsMissedNamingExplain`, which has this test as its own cover) and read: `deallocate` is asked in the `finally` of `PostgresSession`'s describe (`src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:145-158`), after a describe that succeeded, so its miss replaces a result already made; `explain` is asked inside `DescribePreparedAsync`, before any result.  Only this test proves that a miss from the cleanup still ends as `ReplayOutcome.Missed` and not a described entry or an exception.  The describer is outside the scope's `src/SqlSource.Tool/Bundles`, so the numbers do not see the difference.  blind: no
- verdict: confirmed
- reason: Read `tests/SqlSource.Tool.Tests/Bundles/BundleReplayTests.cs:73-95`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:139-160` and `src/SqlSource.Tool/Bundles/BundleReplay.cs:239-268`.  `deallocate` is asked in the `finally` after `DescribePreparedAsync` has made its result, and `explain` inside it, as the finding says.  The two tests are each other's only cover, and their `kills` lists are identical: 21 mutants, all in the bundle code, with the same survivors.  So mutation cannot tell them apart, since the describer is outside `tool-bundles`.  The "only this test" needs one qualification.  `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs:193-232` has a `deallocate` row that proves the describer throws `CaptureMissException` when that call is cut.  That is a test of the `FixtureReplay` harness of `tests/SqlSource.Tool.Testing`, asserting only the exception's type and not a replay's `Missed` naming `deallocate`.  Under the named-cover rule it is no cover for this test.  The keep stands.

### F-SqlSource.Tool.Tests.Bundles-1-05

- test: `tests/SqlSource.Tool.Tests/Bundles/DiagnoseBundlesTests.cs` | `DiagnoseBundlesTests.Raw_AnEntry_IsRenamedAndProven` | rows: all
- evidence: measured (kills 39, alone 0; cover `Raw_AnEntry_ReplaysToReproduced`, `Raw_AnEntry_SaysWhatMadeItAndWhatItGave`) and read: its one visible assertion is `Renamed` (line 60), but `PostgresRenamings.RenamedAsync` fails the test with the refusal when `BundleRenamer` refuses (`tests/SqlSource.Tool.Tests/PostgresRenamings.cs:47-51`), so it proves that the raw bundle `diagnose` writes, from a file with markers `query.sql` drops (lines 22-37), passes the renaming and its proof.  That is the rule of `src/SqlSource.Tool/Bundles/AGENTS.md` that `BundleQueryFile.cs` writes only markers the renamer renames, end to end; neither cover renames anything.  blind: no
- verdict: confirmed
- reason: Read `tests/SqlSource.Tool.Tests/Bundles/DiagnoseBundlesTests.cs:16-61,108-154` and `tests/SqlSource.Tool.Tests/PostgresRenamings.cs:24-51`.  `RenamedAsync` fails the test with the refusal when `BundleRenamings.RenameAsync` refuses (line 50), so the test proves that the raw bundle renames and passes its proof.  `Raw_AnEntry_ReplaysToReproduced` and `Raw_AnEntry_SaysWhatMadeItAndWhatItGave` rename nothing.  `Commands/DiagnoseTests.cs:34-59` (`Diagnose_AQuery_WritesARenamedBundleAndItsMapping`) also renames a bundle of `diagnose`, but it is a plain fixture query with none of the dropped markers.  Only this test renames one made from a file of several queries, with `-- summary:`, `-- param: ... ->` and `-- output:` markers that `query.sql` drops and its database from the settings (lines 22-37).  That is the rule of `src/SqlSource.Tool/Bundles/AGENTS.md` on `BundleQueryFile.cs`.  `kills` gives 39 kills and 0 timeouts.

### F-SqlSource.Tool.Tests.Bundles-1-06

- test: `tests/SqlSource.Tool.Tests/Bundles/EntryDifferencesTests.cs` | `EntryDifferencesTests.Of_EntriesOfTwoToolVersions_IsEmpty` | rows: all
- evidence: measured (kills 7, alone 0; cover `Of_ColumnNullabilityChanged_NamesItsPath`) and read: the cover compares two entries of one tool version, so it would pass if `EntryDifferences` compared `toolVersion`.  Only this test gives two entries that differ in their tool version alone, the case of a bundle replayed by another version of the tool (comment, line 30), which `EntryOf` writes away with `entry with { ToolVersion = ToolVersion }` (`src/SqlSource.Tool/Bundles/EntryDifferences.cs:61`).  blind: no
- verdict: confirmed
- reason: Read `tests/SqlSource.Tool.Tests/Bundles/EntryDifferencesTests.cs:27-52` and `src/SqlSource.Tool/Bundles/EntryDifferences.cs:54-69`.  The named cover, `Of_ColumnNullabilityChanged_NamesItsPath`, compares two entries of one tool version, so it would pass if line 61 no longer wrote the version away.  `kills` for this test lists only mutants that the cover also kills, and none on line 61, which mutation never reaches.  The "one guard" needs a qualification.  The fixtures' `expected.json` hold `"toolVersion": "fixture"`, so `Commands/ReplayTests.cs` `Replay_EveryFixtureFolder_IsReproduced` (lines 98-109, broad) and `Replay_ZipOfAFixture_GivesWhatTheFolderGives` (line 321) fail if `EntryDifferences` compares the tool version.  Neither is in this scope, and their names promise something else, so they guard it only incidentally.  This test is the one whose name and comment (line 30) state the rule.  The keep stands.

### F-SqlSource.Tool.Tests.Bundles-1-07

- test: `tests/SqlSource.Tool.Tests/Bundles/ReplayLinesTests.cs` | `ReplayLinesTests.Write_DecisionOfAnEngine_WritesALine` | rows: all
- evidence: measured (3 rows, rows needed 1, kills 9, alone 0; cover `Write_WalkNode_WritesALine`) and read: each row is one name of the decisions, which are the static array `Decisions` (`src/SqlSource.Tool/Bundles/ReplayLines.cs:27`, blind, static), so no mutant drops `walk.column` or `table-match` and the rows `"postgres.walk.column"` (line 45) and `"postgres.table-match"` (line 46) are their only guard; `Write_WalkNode_WritesALine` covers `walk.node` alone.  The row `"mssql.table-match"` (line 47) is the one that strips the prefix of an engine other than the first of `SqlDescribableDialects.All` (lines 58-65).  blind: yes
- verdict: confirmed
- reason: Read `tests/SqlSource.Tool.Tests/Bundles/ReplayLinesTests.cs:22-53` and `src/SqlSource.Tool/Bundles/ReplayLines.cs:27,55-69`.  `Decisions` is a static array (line 27), which the evidence lists as blind.  So no mutant removes `walk.column` or `table-match`, and the rows at lines 45 and 46 are the only guards of those two names.  `Write_WalkNode_WritesALine` covers `walk.node` alone.  `SqlDescribableDialects.All` is PostgreSQL then SQL Server (`src/SqlSource/Parsing/SqlDescribableDialects.cs:13-14`), so the row `"mssql.table-match"` (line 47) is the one whose prefix is stripped on the loop's second pass.  `kills` gives 9 kills and 1 survivor, `64:17` (the removed `break`), which cannot change the result because a name starts with only one engine's prefix.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-06

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs` | `NameMappingTests.KeepFromEngineRow_AnotherCase_IsKeptFromTheRow` | rows: all
- evidence: measured (kills 8, alone 0; cover `NameMappingTests.IsKeptFromEngineRow_OnlyASpellingKeptFromARow_IsTrue`).  Read: the cover asks the same thing on `Mapping()`, whose `spelled` is its fold, so it cannot tell whether `KeepFromEngineRow` and `IsKeptFromEngineRow` compare by the fold or by the spelling as written (NameMapping.cs:114-121, 151-152); this test's `CaseKeeping()` mapping spells as written, so it alone fails if the comparison moves to `spelled`, a change no mutation operator makes.  blind: no
- verdict: confirmed
- reason: Under `Mapping()` the fold and the spelling are both `ToUpperInvariant` (NameMappingTests.cs:13, 19), so the cover `IsKeptFromEngineRow_OnlyASpellingKeptFromARow_IsTrue` (NameMappingTests.cs:147-160) cannot tell whether `KeepFromEngineRow` records, and `IsKeptFromEngineRow` checks, the fold or the spelling as written (NameMapping.cs:114-121, 151-152).  Under `CaseKeeping()` this test records `name` and asks `NAME` (NameMappingTests.cs:294-298), so it fails if the record moves to `spelled`, alone or with the check.  One caveat that does not change the action: moving only the check at line 152 to `spelled` passes, since the asked spelling `NAME` is also the stored fold.  `kills` gives 8 kills, 0 alone, and no survivors.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-08

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/BundleRenamerTests.cs` | `BundleRenamerTests.RenameAsync_RenamedBundle_Throws` | rows: all
- evidence: measured (covers 5 mutants and kills none; its one survivor, src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs:102:41 String mutation, is the exception's message).  Read: the guard it proves, a bundle already renamed or with no bundle.json refused (BundleRenamer.cs:100-103), is on lines the evidence lists as blind, does not compile, so no mutant could be killed there; no other test of the slice renames a renamed bundle or one without info.  blind: yes
- verdict: confirmed
- reason: `kills` gives 0 kills, and its one survivor is BundleRenamer.cs:102:41, the exception's message.  The guard at BundleRenamer.cs:100-103 is on lines the evidence lists as blind, does not compile (evidence line 547).  The test asserts `ArgumentException` both for `Renamed = true` and for `Info = null` (BundleRenamerTests.cs:244-247).  A grep of tests/SqlSource.Tool.Tests finds no other renaming of a renamed bundle or of one without info; the other `Renamed = true` lines assert a result or are in ReplayTests, which do not rename.  Without the guard, a renamed bundle would be renamed again and a null info would fail with some other exception, so this test alone fails.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-09

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresPlanRenamerTests.cs` | `PostgresPlanRenamerTests.Register_PlanThatIsNoString_DoesNothing` | rows: all
- evidence: measured (covers 1 mutant and kills none).  Read: the guard it reaches, an explain whose `answer.plan` is null, as it is after an error (PostgresPlanRenamer.Register.cs:18-25), is on lines 17-37 that the evidence lists as blind, does not compile; what the test proves is that `Register` returns without throwing on that answer, and no fixture holds an explain with a null plan (a grep of the fixtures finds none), so nothing else reaches it.  blind: yes
- verdict: confirmed
- reason: `answer.plan` set to null (PostgresPlanRenamerTests.cs:645-654) takes the early return at PostgresPlanRenamer.Register.cs:18-25, on lines 17-37 that the evidence lists as blind, does not compile (evidence line 555).  `kills` gives 0 kills and no survivors.  My scan of the 47 `explain` calls of every non-bin `capture.json` under tests/ finds none whose `answer.plan` is not a string, so no fixture reaches this branch.  The test proves that `Register` returns on such an answer without throwing or refusing, which nothing else does.

### F-SqlSource.Tool.Tests.Bundles.Renaming-1-10

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlRenamerTests.cs` | `SqlRenamerTests.ContentOf_OtherOpener_Throws` | rows: all
- evidence: measured (covers 1 mutant and kills none; the mutant, src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs:147:54 String mutation -> `$""`, is the exception's message, which mutation cannot see).  Read: the throw for an opener that is none of the four is the exception `ContentOf` documents (SqlRenamer.cs:137-147), and no other test reaches that arm; a change that returned a slice instead of throwing would fail only here.  blind: no
- verdict: confirmed
- reason: `kills` gives 0 kills and one survivor, SqlRenamer.cs:147:54, the message of the `_ => throw` arm.  The test calls `ContentOf("x", 0, 1)` and expects `InvalidOperationException` (SqlRenamerTests.cs:24-25).  The callers in the source reach it only under a `'` or `$` opener (SqlRenamer.cs:502, 507), so no other test reaches the arm.  A change that returned a slice for another opener would fail only here.

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-05

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_AMemberOfTheRecords_IsItsRule` | rows: all
- evidence: measured (82 rows, kills 1, alone 0, timeouts under it 0; rows needed 1; cover: `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`) and read: the rules are built in a static initialiser and looked up on a line that does not compile under mutation (`SqlServerFields.cs` blind, static 101-183 and line 91), so no mutant can give a member a wrong rule and the one kill and "rows needed 1" say nothing of the rows.  Each row, written out apart from the table (SqlServerFieldsTests.cs:149-211), is the only check that a member has its intended rule: a `Name` member given `Fixed` or `Closed` keeps the user's name and is no longer searched by `SqlServerBundleLeaks`, which skips those rules.  blind: yes
- verdict: confirmed
- reason: `SqlServerFields.Rules` is built in a static initialiser (`SqlServerFields.cs:84`, `Build` from `:100`), which the evidence lists as blind (static 101-183).  The one mutant reached, `:91:36` (the separator emptied), nulls every rule at once, so "kills 1, rows needed 1" says nothing about any row.  Each row of `Build()` (`SqlServerFieldsTests.cs:149-211`) pins one member's rule apart from the table.  `RuleOf_EveryStringMemberOfEveryCall_HasARule` checks only that some rule exists, and a member given `Fixed` or `Closed` instead of `Name` would still rename and replay.  It would also slip out of the leak search, whose `IsRenamed` skips `Fixed` and `Closed` (`SqlServerBundleLeaks.cs:402-403`).  `docs/architecture/sqlserver-describer.md:98` says `SqlServerFieldsTests` "holds the rule of each member".  The finding reads the test rightly.

### F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-06

- test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs` | `SqlServerFieldsTests.RuleOf_AnyOtherMember_IsNull` | rows: all
- evidence: measured (9 rows; covers 1 mutants and kills none) and read: `RuleOf` is the blind line 91 of `SqlServerFields.cs` over the blind static table (101-183), so what the rows check, that a near miss such as `probe answer.rows[].name`, `open answer.errors[].message` (not `answer.error.errors[]`) or `declared answer.rows[].sourceSchema` has no rule, is a fact of the table that no mutant changes.  A rule added there by mistake would let a member the records do not hold pass unrefused; this theory is its only guard.  blind: yes
- verdict: confirmed
- reason: `kills` shows 0 kills.  The one mutant it reaches, `SqlServerFields.cs:91:36` (the separator emptied, every lookup null), cannot fail a test that asserts null, and the table it checks is static and blind.  So the lack of kills says nothing about the test's assertions, which are sound (`SqlServerFieldsTests.cs:135-146`).  One overstatement does not change the action.  For a call member that no record holds, a stray rule would also fail `RuleOf_EveryRuleOfTheTable_IsOfAMemberThatExists` (`:96-102`).  But that test checks only `SqlServerFields.Members`, which leaves out the entry's rules (`SqlServerFields.cs:97-98`), and it does not check an unknown method.  So the rows `(BundleFiles.Expected, "parameters[].type.element.name")` and `("other", "request.sql")` are guarded only here, and the keep stands.

### F-SqlSource.Tool.Tests.Commands-1-11

- test: `tests/SqlSource.Tool.Tests/Commands/CliTests.cs` | `CliTests.Interrupt_SeveralAtOnce_OnlyOneIsTheFirst` | rows: all
- evidence: measured (kills 5, alone 0, timeouts under it 0, cover `CliTests.Interrupt_FirstThenSecondTime_CancelsTheRunThenLetsTheProcessEnd`; 11.9 s, the slowest test of the slice and most of `CliTests`' 12.0 s, 3.7% of the project) and read (CliTests.cs:318-351 releases eight threads at once through a `Barrier`, 2000 times, and asserts that exactly one call of `Cli.Interrupt` is the first; the one-at-a-time test cannot fail on a count that is not atomic, `Interlocked.Increment` at `src/SqlSource.Tool/Cli.cs:139`, and the comment above the test says why two signals that both counted as the first would leave the process unended.  An order of events is what mutation cannot see, so this test is the only guard of it); blind: no
- verdict: confirmed
- reason: CliTests.cs:318-351 releases eight threads at once through a `Barrier`, 2000 rounds, and asserts one first; `Interrupt_FirstThenSecondTime_CancelsTheRunThenLetsTheProcessEnd` (:307-316) calls `Cli.Interrupt` twice on one thread and cannot fail on a count that is not atomic (`Interlocked.Increment` at `src/SqlSource.Tool/Cli.cs:139`).  `kills` in tool-root: the test's 5 kills (`Cli.cs:139:13` three, `:141:20`, `:145:16`) are all also the one-thread test's, 0 timeouts, so it looks removable on numbers; a race between two signals is an order of events, which mutation cannot see, and the comment above it (:318-319) says why two firsts would leave the process unended.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Commands-2-15

- test: `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs` | `ReplayTests.Replay_EveryFixtureFolder_IsReproduced` | rows: all
- evidence: measured (95 rows, 1.2 s; kills 54, alone 0, timeouts under it 54; rows needed 1; cover: `DiagnoseTests.Diagnose_ARefusedQuery_WritesItsFailure`) and read (each row replays one recorded fixture of the describers through the released `replay`, and fails when that fixture stops reproducing, which no mutant of the tool-root scope can show: what it guards is the recorded data and the describers outside the scope.  docs/architecture/bundles.md:79 names it as the test that runs `replay` on every describer fixture's folder); blind: no
- verdict: confirmed
- reason: ReplayTests.cs:98-109 replays each recorded describer fixture (`Cases`, :34-42) through the released `replay`; what it guards is the fixtures and the describers, outside the tool-root scope, so its one-row cover says nothing of it, and docs/architecture/bundles.md:79 names it as the test that runs `replay` on every describer fixture's folder.

### F-SqlSource.Tool.Tests.Commands-2-16

- test: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue` | rows: all
- evidence: measured (1 row, kills 79, alone 0, timeouts under it 62; cover: `SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`) and read (SecretTests.cs:107-132 opens the real `PostgresDescriber` on a refused port, so what is printed, logged and captured is Npgsql's own text; the cover does so with SqlClient, whose text and events differ (its log assertion on `mssql.driver` at :158).  Each driver's text is what could carry the host or the password, and neither test proves the other's); blind: no
- verdict: confirmed
- reason: SecretTests.cs:107-132 opens the real `PostgresDescriber` and asserts Npgsql's text (`ConnectionRefused`) and the absence of the value and host in output, log and capture; the cover, :134-161, does so with `SqlServerDescriber`, whose text and events differ (the `mssql.driver` assertion at :158).  Each driver's own text is what could carry the value, so neither proves the other.

### F-SqlSource.Tool.Tests.Commands-2-17

- test: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_ConnectionStringSplitByTheShellWithAPartThatEndsInSql_IsAWrongCommandLineThatDoesNotRepeatIt` | rows: all
- evidence: measured (1 row, kills 65, alone 0, timeouts under it 51; cover: `SecretTests.Run_ConnectionStringSplitByTheShell_IsAWrongCommandLineThatDoesNotRepeatIt`, which has it as its cover in turn) and read (the comment at SecretTests.cs:389 gives the case: a rest that ends in `.sql` would otherwise be a file filter, which SQLSRC212 prints in full, so the test holds that the split connection string is caught before the token is read as a path; its twin's token has no `.sql` and cannot reach that road); blind: no
- verdict: confirmed
- reason: SecretTests.cs:389-410: the left part ends in `.sql`, and `DescribeCommand.CheckUsage` tests for a split connection string before it reads a token as a `.sql` file (src/SqlSource.Tool/DescribeCommand.cs:233-240), the order the comment at :389 records; the twin at :368-387 has no `.sql` and cannot reach that branch, so the two are each other's cover in numbers only.

### F-SqlSource.Tool.Tests.Commands-2-18

- test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` | `UsageCheckTests.Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine` | rows: all
- evidence: measured (1 row, 4.4 s; kills 30, alone 0, timeouts under it 7; cover: `UsageCheckTests.Check_RepeatedOptionWithoutAValue_NeedsOne`, `DescribeTests.Run_ProjectOptionGivenTwice_RestrictsTheRunToBoth`, `CliTests.Run_TokenThatStartsWithAnAtSign_IsNotReadAsAFile`, `CliTests.Run_ValueForAnOptionThatTakesNone_NamesTheOptionAlone`) and read (UsageCheckTests.cs:251-297 holds, for every command line of up to four tokens that both readers accept, that `UsageCheck` and System.CommandLine read it alike, which the comment gives as the guard against a value read as a path and printed; its command, built apart from the tool's, has a short alias `-c` and an option that takes one value, which no option of the tool has, so `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine` does not hold it, and the covers are single command lines); blind: yes (src/SqlSource.Tool/UsageCheck.cs:72-127)
- verdict: confirmed
- reason: UsageCheckTests.cs:251-297 holds, over every command line of up to four tokens of its vocabulary, that `UsageCheck` and System.CommandLine read the class's own root alike; that root has a short alias `-c` with a value, a ZeroOrOne path and a replay with an ExactlyOne bundle, none of which `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine` (:352-433) reaches, and the measured covers are single command lines.  One slip in the finding: the tool does have options that take one value (`--log`, `--env-file`, `--manifest`, `--online`, `--query`, `--out`, `--scrub`, all ExactlyOne), so what the tool lacks is the short alias with a value, not a one-value option; the keep stands.

### F-SqlSource.Tool.Tests.Commands-2-19

- test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` | `UsageCheckTests.Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine` | rows: all
- evidence: measured (1 row, 44.7 s; kills 38, alone 0, timeouts under it 101; cover: `DiagnoseTests.Diagnose_WrongCommandLine_IsAUsageLineThatNamesNoToken`, `UsageCheckTests.Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine`, `CheckFilterTests.Check_SidecarOfAFileThatNeedsNone_IsADifferenceOnlyWithoutAFilter`, `UsageCheckTests.CheckUsage_DatabaseThatIsNoName_IsReportedByItsPositionAndNotItsText`, `UsageCheckTests.Check_EnvFileGivenTwice_IsNamedAndItsValueIsNot`) and read (UsageCheckTests.cs:352-433 runs every command line of up to four of 40 tokens through both readers and the three commands' `CheckUsage` and `ReadsValuesAs`; it is the one test that finds a command line of the tool the two readers take differently, which `Cli` would refuse with a line that docs/tech-debt/TD-0024 says no test reaches, and the covers are single command lines that cannot.  It has the most timeouts under it of the slice, so it is never removed on its numbers); blind: yes (src/SqlSource.Tool/UsageCheck.cs:72-127)
- verdict: confirmed
- reason: UsageCheckTests.cs:352-433 runs every command line of up to four of 40 tokens through both readers and every command's `CheckUsage` and `ReadsValuesAs`; it is the one guard of the agreement that docs/tech-debt/TD-0024-no-test-reaches-the-line-that-a-command-line-is-not-valid.md:5 relies on, since the line Cli writes on disagreement is reached by no test.  Its 101 timeouts forbid removal on numbers, and its covers are single command lines that cannot find a new disagreement.  Its time, 44.7 s, is the bulk of `UsageCheckTests`' 15.3% of the project.

### F-SqlSource.Tool.Tests.Describing-1-02

- test: `tests/SqlSource.Tool.Tests/Describing/SampleSqlTests.cs` | `SampleSqlTests.Build_NullTestOnSqlServer_IsNull` | rows: all
- evidence: measured (1 row, 0.017 s; covers 6 mutants and kills none).  Read: it is the one test that builds a sample for SQL Server, and so the one guard of the arm `DatabaseEngine.SqlServer => Null` (SampleSql.cs:43), a constant that no mutant changes.  The mutants it reaches are equivalent for SQL Server, whose stand-in is the same `NULL` the segments were written with: `||` to `&&` or `==` to `!=` at SampleSql.cs:73 send the text through `WithStandIns`, which puts `NULL` back in each place, and `??=` to `=` at SampleSql.cs:61 changes nothing for one null test; line 62, which would change the text, is blind.  A change that gave SQL Server the subquery of PostgreSQL, which the comment at SampleSqlTests.cs:180 says SQL Server refuses, fails only this test; blind: yes (SampleSql.cs:62 does not compile under its mutants)
- verdict: confirmed
- reason: `kills` in tool-describing gives 0 kills and one survivor, `SampleSql.cs:61:22 ... nullTests = []`, which is equivalent for a query with one null test.  For SQL Server `nullTest` is `Null` (SampleSql.cs:43), so the `||`/`==` mutants at SampleSql.cs:73 only send the text through `WithStandIns`, which writes `Null` back at each place (SampleSql.cs:86), giving the same text; line 62 is blind.  It is the only focused test that builds a SQL Server sample (`SqlServerSample`, SampleSqlTests.cs:19-20); the others that do, through `FixtureReplay`, `RealisticDescribe` and `DescribeRequests`, are broad or harness code and are no cover.  The comment at SampleSqlTests.cs:180 states the SQL Server rule it guards.  Keep is right.

### F-SqlSource.Tool.Tests.Describing.Describers-1-06

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` | `DescriberSessionTests.IsBelow_Version_ComparesItsLeadingMajor` | rows: all
- evidence: measured (3 rows, kills 2, alone 0, rows needed 2; cover `OpenAsync_ServerAtTheFloor_IsTheSessionAndKeepsTheConnection`, `OpenAsync_ServerBelowTheFloor_IsVersionNotSupportedAndDisposesTheConnection`) and read (the row `("devel", 14, false)` at DescriberSessionTests.cs:89 is the only test of the rule that a version with no leading digit is not below the floor, `Versions.LeadingMajor(version) is { } major` at src/SqlSource.Tool/Describing/Describers/DescriberSession.cs:104, a line whose mutants do not compile; the rows `13.12` and `14.0` repeat the opening tests' versions); blind: yes
- verdict: confirmed
- reason: `kills`: its 2 kills, DescriberSession.cs:104:56 (`major > minimum` and `major <= minimum`), are killed by the opening tests at the floor and below it, whose versions are this theory's rows `13.12` and `14.0` (DescriberSessionTests.cs:34, :45).  The row `("devel", 14, false)` at :89 is the only test of a version with no leading digit, which `Versions.LeadingMajor` reads as null.  That rule is `is { } major &&` at DescriberSession.cs:104, a line the evidence marks blind.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Describers-1-07

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` | `DescriberSessionTests.OpenAsync_AnswerWithAnError_IsTheFailureAndDisposesTheConnection` | rows: all
- evidence: measured (kills 3, alone 0; cover `OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`) and read (DescriberSessionTests.cs:19-27 is the only test that the answer's error is the outcome, and that the connection is disposed on a return, not a throw, before `kept` is set; that branch is src/SqlSource.Tool/Describing/Describers/DescriberSession.cs:57-60, lines whose mutants do not compile); blind: yes
- verdict: confirmed
- reason: `kills`: 3 kills (DescriberSession.cs:48:20, 74:17 and 76:17), all shared with the other opening tests, and no kill of its own.  DescriberSessionTests.cs:19-27 is the only test whose answer holds an error.  It asserts that the outcome is that error's failure and that the connection is disposed when the method returns.  That branch is DescriberSession.cs:57-60, which the evidence marks blind.  The named cover, `AnswerWithNoVersion` (:67-78), throws from `ServerOf` and never reaches the branch.  The keep is right.

### F-SqlSource.Tool.Tests.Describing.Describers-1-08

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` | `DescriberSessionTests.OpenAsync_ExchangeThatThrows_ThrowsAndDisposesTheConnection` | rows: all
- evidence: measured (kills 3, alone 0; cover `OpenAsync_AnswerWithAnError_IsTheFailureAndDisposesTheConnection`) and read (DescriberSessionTests.cs:55-64 is the only test whose exception comes from the exchange's ask itself, src/SqlSource.Tool/Describing/Describers/DescriberSession.cs:51-56, as a refused or lost connection does; a `try` moved below the ask would leak the connection there while every other test passes, a change no mutant makes); blind: no
- verdict: confirmed
- reason: `kills`: 3 kills, all shared, and no timeouts.  DescriberSessionTests.cs:55-64 is the only test whose exception comes from `request.Exchange.AskAsync` itself (DescriberSession.cs:51-56).  `AnswerWithNoVersion` throws later, at :62, and `AnswerWithAnError` returns without throwing.  If the `try` began after the ask, this test alone would fail, and Stryker makes no such mutant.  The keep is right.

### F-SqlSource.Tool.Tests.Describing.Describers-1-09

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/ExceptionChainTests.cs` | `ExceptionChainTests.Find_TypeOfNone_IsNull` | rows: all
- evidence: measured (covers 1 mutants and kills none) and read (ExceptionChainTests.cs:29-33 is the only test of a chain with no exception of the type, the `return null` after the loop at src/SqlSource.Tool/Describing/Describers/ExceptionChain.cs:25, whose lines 16-26 hold mutants that do not compile); blind: yes
- verdict: confirmed
- reason: `kills`: 0 kills, 0 survivors, 0 timeouts.  The one mutant it covers is ExceptionChain.cs:17:39 (`current is null`), which also returns null on this input, so the test cannot kill it.  ExceptionChainTests.cs:29-33 is the only test of a chain that holds no exception of the type, the `return null` at ExceptionChain.cs:25, and the evidence marks lines 16-26 blind.  The assertion `ShouldBeNull` is right.

### F-SqlSource.Tool.Tests.Describing.Describers-1-10

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/ExceptionChainTests.cs` | `ExceptionChainTests.Find_TypeOfTheException_IsTheException` | rows: all
- evidence: measured (kills 1, alone 0; cover `ExceptionChainTests.Find_TypeOfAnInnerException_IsThatException`) and read (ExceptionChainTests.cs:21-27 gives an exception and an inner one of the same type and expects the outer, the only test that the search starts at the exception itself; a loop that began at `InnerException` passes the cover and fails this one; ExceptionChain.cs:16-26 hold mutants that do not compile); blind: yes
- verdict: confirmed
- reason: `kills`: its one kill, ExceptionChain.cs:17:39, is shared with `Find_TypeOfAnInnerException_IsThatException`.  ExceptionChainTests.cs:21-27 gives an outer `TimeoutException` whose inner exception is also a `TimeoutException`, and expects the outer one.  A loop that started at `exception.InnerException` would return the inner exception and fail this test.  It would still pass the cover at :12-19, whose outer exception is not of the type.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Describers-1-11

- test: `tests/SqlSource.Tool.Tests/Describing/Describers/ExceptionChainTests.cs` | `ExceptionChainTests.Find_TypeOfAnInnerException_IsThatException` | rows: all
- evidence: measured (kills 1, alone 0; cover `ExceptionChainTests.Find_TypeOfTheException_IsTheException`) and read (ExceptionChainTests.cs:12-19 is the only test that the search walks the chain, two levels down; a `Find` that looked at the exception alone passes the cover and fails this one; ExceptionChain.cs:16-26 hold mutants that do not compile); blind: yes
- verdict: confirmed
- reason: `kills`: its one kill, ExceptionChain.cs:17:39, is shared with `Find_TypeOfTheException_IsTheException`.  ExceptionChainTests.cs:12-19 puts the `SocketException` two levels down.  A `Find` that tested only the exception itself would return null and fail this test, yet pass the cover at :21-27, where the outer exception is of the type.  This is the only test that the search walks the chain.  The keep is right.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-08

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs` | `PostgresDescriberTests.SecretsOf_PasswordFileThatIsAPipe_IsNoneAndLeavesItUnread` | rows: all
- evidence: measured (covers 12 mutants and kills none; timeouts under it 3, at PostgresConnection.cs lines 127, 128 and 134) and read: a mutant that opens or reads the pipe there blocks on a pipe whose writer stays open, which a run can only record as a timeout; the test detects it through `Eventually.WithinAsync` and then reads the line back to show the tool left it unread (PostgresDescriberTests.cs:120-144).  A wait is what mutation cannot see, so this test is the guard of `PasswordsIn`'s `CanSeek` refusal (PostgresConnection.cs:132-136) and must not be deleted on its numbers.  blind: no.
- verdict: confirmed
- reason: `kills` shows no kill and three timeouts, PostgresConnection.cs:127:17 and 127:67 Equality and 134:13 Block removal; the last, which removes `return []` after `!stream.CanSeek` (PostgresConnection.cs:132-136), lets `ReadToEnd` wait on a pipe whose writer the test keeps open, and the test turns that wait into a failure through `Eventually.WithinAsync` and then reads its line back (PostgresDescriberTests.cs:120-144).  No other test of the scope has the 134:13 timeout under it (the named-pipe and missing-file tests have 128:13 instead), so this is the one guard of the `CanSeek` refusal, a wait that mutation records only as a timeout.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-09

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs` | `PostgresDescriberTests.SecretsOf_PasswordFileThatIsANamedPipe_IsNoneAndIsNotOpened` | rows: all
- evidence: measured (covers 11 mutants and kills none; timeouts under it 3, at PostgresConnection.cs lines 127, 128 and 134) and read: a named pipe with no writer has length 0, and the guard `new FileInfo(path) is not { Exists: true, Length: > 0 }` (PostgresConnection.cs:127) is what keeps the tool from opening it; a mutant there blocks the open, which a run records as a timeout and the test detects through `Eventually.WithinAsync` (PostgresDescriberTests.cs:146-163).  It guards another line than the anonymous pipe's test, which reaches the `CanSeek` refusal.  blind: no.
- verdict: confirmed
- reason: `kills` shows no kill and three timeouts, PostgresConnection.cs:127:17, 127:67 and 128:13 Block removal: each lets the tool open a FIFO with no writer, whose length is 0, and the open blocks, which the test detects through `Eventually.WithinAsync` (PostgresDescriberTests.cs:146-163).  `SecretsOf_PasswordFileMissingOrAFolder_IsNoneAndDoesNotThrow` reaches the same three mutants, but for a missing file or a folder the open throws a path error that line 141 catches, so it cannot be the test that times out; the named-pipe test is the guard of the length guard at line 127.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-10

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs` | `PostgresDescriberTests.SecretsOf_ConnectionWithBothPasswords_IsBoth` | rows: all
- evidence: measured (covers 2 mutants and kills none) and read: it is the one test whose string holds both `Password` and `SSL Password` (PostgresDescriberTests.cs:27-31), so the one that shows the SSL password is kept when the early return for a `Password` is taken (PostgresConnection.cs:83-87).  `SecretsOf_SslPasswordOnlyAndPgPasswordSet_IsBoth` shows the SSL password without a `Password`, and `SecretsOf_PasswordInTheStringAndPgPasswordSet_IsTheStringsAlone` the early return without an SSL password; a change that returned the `Password` alone would pass both and fail this one, though no mutant of the run makes it.  blind: no.
- verdict: confirmed
- reason: The report holds no mutant of line 83, and the mutants the test reaches, 84:13 Equality and 85:9 Block removal, are equivalent on its input (no `PGPASSWORD`, no password file, so falling past the early return still gives `pw` and `kp`), hence kills none.  It is the one test anywhere under `tests/` whose string has both `Password` and `SSL Password` (grep for `SSL Password` finds only PostgresDescriberTests.cs:30 and :170), so it alone fails a change that kept only `builder.Password` when there is one (PostgresConnection.cs:83-87); `SecretsOf_SslPasswordOnlyAndPgPasswordSet_IsBoth` and `SecretsOf_PasswordInTheStringAndPgPasswordSet_IsTheStringsAlone` would both pass it.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-11

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs` | `PostgresDescriberTests.RewriteOf_ConnectionTheDriverCannotRead_IsNull` | rows: all
- evidence: measured (covers 2 mutants and kills none; survivor `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs:114:9 Block removal mutation` under it alone) and read: the survivor empties `catch (ArgumentException) { return null; }` (PostgresConnection.cs:113-116), and a block removed from a method that returns a value returns its default, null, so it is an equivalent mutant.  The test is the one that shows a string the driver cannot read has no rewrite rather than a throw (PostgresDescriberTests.cs:179-183), which the run relies on to remove the rewrite's text.  blind: no.
- verdict: confirmed
- reason: `kills` shows the one survivor, PostgresConnection.cs:114:9 Block removal of `catch (ArgumentException) { return null; }` (lines 113-116).  The mutant compiled, so control falls out of the empty catch to the default return the mutation tool supplies, null: the same value, an equivalent mutant.  The test (PostgresDescriberTests.cs:179-183) asserts that a string the driver cannot read gives null and does not throw, which the removal of the whole `catch` would break; the keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-12

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs` | `PostgresLiveTests.UnusableException_InnerException_IsDropped` | rows: all
- evidence: measured (covers 1 mutant and kills none; survivor `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs:546:9 Block removal mutation` under it alone) and read: the block that survives holds only `_ = innerException;` (PostgresConnection.cs:544-549), so removing it changes nothing and the mutant is equivalent.  The test is the one guard that the constructor does not pass the inner exception, whose text may quote the connection's value or a path, to its base (PostgresLiveTests.cs:174-180).  blind: no.
- verdict: confirmed
- reason: `kills` shows the one survivor, PostgresConnection.cs:546:9 Block removal of `{ _ = innerException; }` (lines 544-549), which removes a discard and changes nothing: an equivalent mutant.  The test (PostgresLiveTests.cs:174-180) asserts `InnerException` is null, the one guard that the constructor does not pass the inner exception, whose text may quote a path, to `base`; no mutator of the run makes that change.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres-1-13

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlCheckTests.cs` | `NpgsqlCheckTests.Read_AnswerWithNothing_Throws` | rows: all
- evidence: measured (covers 1 mutant and kills none; survivor `src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs:103:52 String mutation` under it alone) and read: the survivor empties the exception's message, which the test's name does not promise; it asserts the throw itself (NpgsqlCheckTests.cs:135-137), the one guard that an answer with no columns, no error and no driver error is a bug and not a verdict (NpgsqlCheck.cs:101-103).  blind: yes (NpgsqlCheck.cs:90-110 does not compile under mutation, so the throw is unmeasured).
- verdict: confirmed
- reason: `kills` shows no kill and one survivor, NpgsqlCheck.cs:103:52 String mutation emptying the exception's message, which the test's name, `_Throws`, does not promise; it asserts the throw of `InvalidOperationException` (NpgsqlCheckTests.cs:135-137) for an answer with no columns, no error and no driver error (NpgsqlCheck.cs:101-103), which no other test of the slice reaches.  The evidence lists 90-110 as lines with mutants that do not compile, so a mutant removing the throw was never measured.  The keep reads the test rightly.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-10

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs` | `PlanInstancesTests.BareAlias_NodeWithAnInitPlan_IsNull` | rows: all
- evidence: measured (covers 44 mutants and kills none) and read (the chain `BareAliasOf` follows, `node.Plans is [{ ParentRelationship: Outer } only]` at src/SqlSource.Tool/Describing/Postgres/Walk/PlanInstances.cs:288-295, is on lines 279-299 that do not compile under mutation, so no mutant there could be killed.  The test is the only one that puts an InitPlan beside the one input and asserts the bare alias null, PlanInstancesTests.cs:321-329, which is what its name says); blind: yes
- verdict: confirmed
- reason: The test (PlanInstancesTests.cs:321-329) puts an `InitPlan` beside the one `Outer` input of a `Sort`. It asserts `BareAlias` null, which is what the comment at :321 and the name say.
  - **The rule:** the chain stops at a node unless `node.Plans is [{ ParentRelationship: Outer } only]` (PlanInstances.cs:292), inside `BareAliasOf` at :278-299, which the evidence lists as blind.
  - **Kills:** `kills` gives 0 kills, 0 timeouts, and survivors only at PlanInstances.cs:180:19 and PlanPartitions.cs:52:13, neither of which this rule touches.
  - **No other guard:** no other test of the slice sets an InitPlan beside the input and asserts the bare alias (grep for `InitPlan` across tests/SqlSource.Tool.Tests/Describing/Postgres).

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-11

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs` | `PlanInstancesTests.BareAlias_RootOverAJoin_IsNull` | rows: all
- evidence: measured (covers 45 mutants and kills none; timeouts under it 1) and read (the loop of `BareAliasOf` that stops at a node of two children is on PlanInstances.cs:279-299, which do not compile under mutation.  The test asserts what its name says, PlanInstancesTests.cs:317-319, at the unit that decides it); blind: yes
- verdict: confirmed
- reason: The test (PlanInstancesTests.cs:317-319) asserts `BareAlias` null for a `Sort` over an inner join, which is what the name says.
  - **Kills:** `kills` gives 0 kills and one TIMEOUT, PlanInstances.cs:294:9 Block removal. Removing `node = only;` makes the loop at :289-295 spin forever on `Sort`, so this test is plausibly what detects it.
  - **No other test of the same stop:** the other bare-null assertions stop elsewhere. :155 has no scan, :166 a pushed-down `Foreign Scan`, :182 a `Subquery Scan`, :285 and :307 a root `ModifyTable`. None stops the non-ModifyTable chain at a node of two join children.
  - **Why it kills nothing:** the code is on blind lines 279-299, so the lack of kills says nothing about a weak assertion.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-12

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs` | `PlanNodeTests.Read_PlanNestedPastTheLimit_IsNull` | rows: all
- evidence: measured (covers 1 mutant and kills none) and read (the mutant it covers is the `MaxDepth` initializer at src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs:144, which `Read_PlanOfAHundredNestedNodes_IsRead` kills alone.  Past either limit a 400-deep text is no plan, so this test cannot tell them apart and does not try to.  What it guards, that the text is null and nothing is thrown (comment at PlanNodeTests.cs:264), is the `try`/`catch` of `Read` on PlanNode.cs:141-158, which do not compile under mutation); blind: yes
- verdict: confirmed
- reason: The test (PlanNodeTests.cs:264-266) asserts that `Read(Nested(400))` is null without throwing.
  - **Kills:** `kills` gives 0 kills, 0 timeouts and 0 survivors. The one mutant it covers, the `MaxDepth` initializer at PlanNode.cs:144, falls back to the default limit. A 400-node chain is about 800 levels of JSON, past both the default and `MaxDepth = 512` (PlanNode.cs:68), so this test cannot see that mutant. `Read_PlanOfAHundredNestedNodes_IsRead` kills it alone.
  - **One correction:** the `catch (JsonException)` is not guarded by this test alone. The rows `""` and `"not json"` of `Read_TextThatIsNotAnArrayOfOnePlan_IsNull` (PlanNodeTests.cs:192-193) reach it too. What is this test's own is the upper bound: a limit raised past the chain's depth would let `NodeOf` read 400 nested nodes and return a plan.
  - **Verdict:** that is the depth refusal the finding's proposal names, on blind lines 141-158, so keeping the test stands.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-04

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/TotalFunctionsTests.cs` | `TotalFunctionsTests.OperatorResult_RowOfTheTable_IsItsResult` | rows: all
- evidence: measured (5 rows, covers 78 mutants and kills none; it is one of three tests under every survivor of src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs:242-376).  Read: those lines are `BuildOperators`, `Arithmetic`, `Comparison` and `Prefix`, which run only from the static initialiser of `OperatorRows` (TotalFunctions.cs:90-93), so the table is built once before any mutant is switched on and no test can kill a mutant there; the evidence lists the static lines 98-152 as blind but not these.  The rows assert exactly what those lines build: the wider type of mixed integers (`"*", Int8, Int4 -> Int8`, the choice at line 251), real with double precision (`"/", Float4, Float8 -> Float8`), and the two extra rows `=(xid, integer)` and `||(text, text)` (TotalFunctionsTests.cs:14-18).  blind: yes
- verdict: confirmed
- reason: `kills` gives 5 rows, 0 kills, 0 timeouts, 78 survivors, every one in src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs:242-346 and beyond, which is `BuildOperators`, `Arithmetic`, `Comparison` and `Prefix` (lines 240, 304, 331, 356).  They run only from the static initialiser `OperatorRows = [.. BuildOperators()]` (lines 91-94), through `OperatorsBySignature` (165-166), so the table is built once and no mutant there can be switched on under a test; `OperatorResult` itself (197-198) is on the evidence's blind line 198.  The rows (tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/TotalFunctionsTests.cs:14-18) check the wider of mixed integers (the choice at TotalFunctions.cs:251), float4 with float8, and the two extra rows `=(xid, integer)` and `||(text, text)` (lines 298-299 of the source's yields).  The test is read rightly: its zero kills are the static initialiser's.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-05

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs` | `PlanExpressionTests.NullableOf_CoalesceOverBothSidesWithoutTheFullJoin_IsUnknown` | rows: all
- evidence: measured (kills 26, alone 0, timeouts under it 7; cover `PlanExpressionTests.NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull`).  Read: its two assertions (PlanExpressionTests.cs:742-743) are the two halves of the gate of FullJoinSides, `!_context.CatalogFunctions || _context.ProvedOnFullJoinSide is not { } sideOf` (PlanExpression.Nullability.cs:92): no full-join answer, and pg_catalog not first.  The cover's every row passes `AtAFullJoin()` with both set (PlanExpressionTests.cs:713-714, 734-735), so it cannot fail when either half is dropped; the gate is on the evidence's blind lines 91-115, so the cover's numbers say nothing about it, and timeouts sit under the test.  blind: yes
- verdict: confirmed
- reason: `kills` gives 1 row, 26 kills, 0 alone, 7 timeouts; all 7 timeouts are under the named cover too, so they do not set the test apart, but the keep does not rest on them.  The two assertions (PlanExpressionTests.cs:742-743) are the two halves of the gate `!_context.CatalogFunctions || _context.ProvedOnFullJoinSide is not { } sideOf` (src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Nullability.cs:92).  Dropping either half does not compile, since `sideOf` is then unassigned, which is why lines 91-115 are blind; and every row of the cover goes through `AtAFullJoin()` with both set (PlanExpressionTests.cs:713-714, 734-735), so it cannot fail on either half.  The walk-level tests `NullabilityWalkExpressionTests.Run_CoalesceAtAFullJoin_IsAnswered` (tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkExpressionTests.cs:204-211, catalog not first) and `Run_CoalesceAboveAFullJoin_KeepsNoOrigin` (a Result above the join) reach the same gate.  They test the walk, which builds the context (src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.Expressions.cs:344-345), not the reader's own contract.  So the claim that this test is "the one test of the gate" holds at the reader's level, and the keep stands.

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-08

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs` | `ProofTests.Holds_NotClrOfAnOperandOfAClrType_IsFalse` | rows: all
- evidence: measured (covers 5 mutants and kills none); read (the mutants it reaches are on Proof.cs:30, where every one still yields NotClr false for its answer `(NotNull: true, NotClr: false)`, so none can fail it.  What it guards is that `Proof.NotClr` reads `NotClr` and not `NotNull` (Proof.cs:60): with NotNull true a node reading the wrong field would hold, and the test fails.  It is the false case beside `Holds_NotClrOfAnOperandOfNoClrType_IsTrueWhateverItsNullability`, and its assertion is not weak); blind: yes (Proof.cs:30)
- verdict: confirmed
- reason: `kills` reports 0 kills, 0 survivors and 0 timeouts for `ProofTests.Holds_NotClrOfAnOperandOfAClrType_IsFalse`.  For its input, `NotClr(0)` over `[(NotNull: true, NotClr: false)]` (ProofTests.cs:38-39), every mutant of Proof.cs:30 still gives NotClr false: either `answers[0]` or `default`, both with NotClr false.  The test rightly guards that `Proof.NotClr` reads `NotClr` (Proof.cs:60), which no mutator changes: reading `NotNull`, or `NotNull || NotClr`, would hold here and fail the test.  `Holds_NotClrOfAnOperandOfNoClrType_IsTrueWhateverItsNullability` and `Of_OperandOfAClrType_IsNotProved` (ExpressionProofsTests.cs:358) also catch a plain field swap.  Even so, this is the unit-level false case that tells a node reading `NotNull || NotClr` apart from one reading `NotClr`, so keeping it reads the test rightly.

## Unsettled

None.  The one finding left at **needs measurement** was measured again and confirmed.

## Coverage gaps and failed scopes

- Coverage gaps: none.  Every file of every slice was read in full by its reviewer; for each slice, the files of its list less the files of its read list are none.
- Scopes failed or left out: none.  All 33 scopes of `scopes.txt` ran and passed on the first run.
- Test projects failed: none.  `tests/DatabaseTests/Postgres.SyncAsync` (492 passed, 1 skipped) and `tests/DatabaseTests/SqlServer.SyncAsync` (770 passed, 5 skipped) were run by hand with `-p:RunSyncAsync=true`, since `measure.sh` ran them without the property, and without `--coverage`, under which neither runs a test, since neither references the coverage extension: their tests have times and no coverage.
- `tests/DatabaseTests/QueryMethodAllocation` was not run, as the skill says, and was reviewed by reading.
- Both source projects had every one of their scopes run, so each has an item of tech debt: [TD-0114](../../tech-debt/TD-0114-generator-code-its-own-tests-do-not-check.md) for `src/SqlSource` and [TD-0115](../../tech-debt/TD-0115-tool-code-its-own-tests-do-not-check.md) for `src/SqlSource.Tool`.

## Tests that stay as cover

294 tests, each named as cover by a confirmed **delete**, **fold** or **demote** in a phase, with the findings that lean on it.  None may be removed, nor lose the rows that make it a cover, while those findings stand; a phase that changes one keeps what makes it a cover.

- `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs`
  - `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce`: F-DatabaseTests.Postgres.Npgsql8-1-01 (phase 88)
- `tests/DatabaseTests/Postgres.Npgsql8/ListQueryTests.cs`
  - `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`: F-DatabaseTests.Postgres.Npgsql8-1-02 (phase 89)
- `tests/DatabaseTests/Postgres/Describer/CatalogPinTests.cs`
  - `CatalogPinTests.Catalog_CatalogOverloads_ArePinned`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-03 (phase 14)
- `tests/DatabaseTests/Postgres/Describer/ColumnTests.cs`
  - `ColumnTests.DescribeAsync_CastWithFieldsOrAnArrayOfAModifier_IsSpelledWithTheModifier`: F-DatabaseTests.Postgres.Describer-1-02 (phase 82)
  - `ColumnTests.DescribeAsync_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity`: F-DatabaseTests.Postgres.Describer-1-02 (phase 82)
  - `ColumnTests.DescribeAsync_InsertReturning_IsRowsAndInsertsNothing`: F-DatabaseTests.Postgres.Describer-1-08 (phase 90)
  - `ColumnTests.DescribeAsync_QueryOfATable_AsksTablesBetweenColumnsAndTypes`: F-DatabaseTests.Postgres.Describer-1-08 (phase 90)
  - `ColumnTests.DescribeAsync_UpdateWithoutReturning_IsNone`: F-DatabaseTests.Postgres.Describer-1-08 (phase 90)
- `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs`
  - `ParameterTests.DescribeAsync_OptionalFilterWithADeclaration_IsTypedAsDeclared`: F-DatabaseTests.Postgres.Describer-1-01 (phase 98)
  - `ParameterTests.DescribeAsync_ParameterComparedWithATextualColumn_IsText`: F-DatabaseTests.Postgres.Describer-1-01 (phase 98)
- `tests/DatabaseTests/Postgres/Describer/ScanTests.cs`
  - `ScanTests.DescribeAsync_AggregateOverAPartitionedTable_IsAnswered`: F-DatabaseTests.Postgres.Describer-1-03 (phase 82)
- `tests/DatabaseTests/Postgres/Describer/SetOperationTests.cs`
  - `SetOperationTests.DescribeAsync_UnionAllWhoseBranchNamesCrossThePlaces_StaysUnknown`: F-DatabaseTests.Postgres.Describer-2-03 (phase 90)
- `tests/DatabaseTests/Postgres/EndToEnd/AccountQueryTests.cs`
  - `AccountQueryTests.ListAccounts_EveryAccount_ReadsTheTableModel`: F-DatabaseTests.Postgres-1-01 (phase 81)
- `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs`
  - `AllTypesQueryTests.Int4Array_LazySequence_IsEnumeratedOnce`: F-DatabaseTests.Postgres.EndToEnd-1-09 (phase 91)
  - `AllTypesQueryTests.ListAllTypes_BothRows_ReadsEveryColumn`: F-DatabaseTests.Postgres-1-01 (phase 81), F-DatabaseTests.Postgres-1-02 (phase 81)
- `tests/DatabaseTests/Postgres/EndToEnd/EveryTypeTests.cs`
  - `EveryTypeTests.InsertEveryType_FullRow_ReadsEveryValueBack`: F-DatabaseTests.Postgres.EndToEnd-1-09 (phase 91)
  - `EveryTypeTests.InsertEveryType_NullRow_ReadsEveryNullBack`: F-DatabaseTests.Postgres.EndToEnd-1-10 (phase 91)
- `tests/DatabaseTests/Postgres/EndToEnd/InvoiceQueryTests.cs`
  - `InvoiceQueryTests.ListInvoices_EveryInvoice_ReadsTheTableModel`: F-DatabaseTests.Postgres-1-01 (phase 81)
- `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs`
  - `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`: F-DatabaseTests.Postgres.EndToEnd-1-11 (phase 89)
- `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`
  - `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn`: F-DatabaseTests.Postgres.EndToEnd-1-14 (phase 83), F-DatabaseTests.Postgres.EndToEnd-1-15 (phase 83)
  - `ModelShapeTests.Build_OutputTypeByFolderFile_IsASealedClass`: F-DatabaseTests.Postgres.EndToEnd-1-13 (phase 83)
- `tests/DatabaseTests/Postgres/EndToEnd/NoteQueryTests.cs`
  - `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls`: F-DatabaseTests.Postgres.EndToEnd-1-12 (phase 83)
- `tests/DatabaseTests/Postgres/EndToEnd/RowBufferQueryTests.cs`
  - `RowBufferQueryTests.NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-01 (phase 91)
  - `RowBufferQueryTests.NumbersAsICollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-05 (phase 91)
  - `RowBufferQueryTests.NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-02 (phase 91)
  - `RowBufferQueryTests.NumbersAsIList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-06 (phase 91)
  - `RowBufferQueryTests.NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-03 (phase 91)
  - `RowBufferQueryTests.NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-04 (phase 91)
  - `RowBufferQueryTests.NumbersAsImmutableArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-08 (phase 91)
  - `RowBufferQueryTests.NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes`: F-DatabaseTests.Postgres.EndToEnd-1-07 (phase 91)
- `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs`
  - `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`: F-DatabaseTests.Postgres.EndToEnd-2-02 (phase 91)
  - `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`: F-DatabaseTests.Postgres.EndToEnd-2-02 (phase 91)
- `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs`
  - `SynchronousMethodTests.TouchAccountSync_InATransaction_IsUndoneByItsRollback`: F-DatabaseTests.Postgres.EndToEnd-2-01 (phase 91)
  - `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows`: F-engines-08 (phase 91)
- `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelsTests.cs`
  - `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`: F-DatabaseTests.Postgres-2-01 (phase 84)
- `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs`
  - `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`: F-engines-07 (phase 92)
- `tests/DatabaseTests/SqlServer/EndToEnd/AccountQueryTests.cs`
  - `AccountQueryTests.ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel`: F-engines-01 (phase 85)
- `tests/DatabaseTests/SqlServer/EndToEnd/BatchQueryTests.cs`
  - `BatchQueryTests.InsertNoteAtALevel_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound`: F-DatabaseTests.SqlServer.EndToEnd-1-02 (phase 94)
  - `BatchQueryTests.InsertNote_DuplicateKeyInTheCallersTransaction_LeavesTheOtherItemsInIt`: F-DatabaseTests.SqlServer.EndToEnd-1-01 (phase 94)
  - `BatchQueryTests.InsertNote_DuplicateKeyInTheSecondChunkOfTheCallersTransaction_LeavesItsRollbackToTheCaller`: F-DatabaseTests.SqlServer.EndToEnd-1-01 (phase 94)
- `tests/DatabaseTests/SqlServer/EndToEnd/InvoiceQueryTests.cs`
  - `InvoiceQueryTests.ListInvoices_Seed_ReadsTheTableModel`: F-engines-01 (phase 85)
- `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs`
  - `ListQueryTests.ListAccountsByIds_LazySequence_IsEnumeratedOnce`: F-engines-06 (phase 92)
- `tests/DatabaseTests/SqlServer/EndToEnd/MethodAllTypesTests.cs`
  - `MethodAllTypesTests.ListAllTypes_TheSeed_ReadsBothRows`: F-engines-01 (phase 85), F-engines-02 (phase 85)
- `tests/DatabaseTests/SqlServer/EndToEnd/ModelShapeTests.cs`
  - `ModelShapeTests.Build_AllTypes_IsASealedRecordOfEveryColumn`: F-engines-05 (phase 86)
  - `ModelShapeTests.Build_TableOfAnotherSchema_IsNamedWithItsSchema`: F-engines-04 (phase 86)
- `tests/DatabaseTests/SqlServer/EndToEnd/NoteQueryTests.cs`
  - `NoteQueryTests.ListAccountNotes_AnAccountWithNoNote_HasNulls`: F-DatabaseTests.SqlServer.EndToEnd-2-03 (phase 86), F-engines-01 (phase 85)
- `tests/DatabaseTests/SqlServer/EndToEnd/RunTimeArgumentTests.cs`
  - `RunTimeArgumentTests.OpenConnection_AfterTheMethod_IsLeftOpen`: F-DatabaseTests.SqlServer.EndToEnd-2-01 (phase 94)
- `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousBatchTests.cs`
  - `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound`: F-DatabaseTests.SqlServer.EndToEnd-2-06 (phase 93)
  - `SynchronousBatchTests.InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing`: F-DatabaseTests.SqlServer.EndToEnd-2-06 (phase 93)
  - `SynchronousBatchTests.InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound`: F-DatabaseTests.SqlServer.EndToEnd-2-05 (phase 93)
  - `SynchronousBatchTests.InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel`: F-DatabaseTests.SqlServer.EndToEnd-2-05 (phase 93)
  - `SynchronousBatchTests.InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing`: F-DatabaseTests.SqlServer.EndToEnd-2-04 (phase 93)
- `tests/DatabaseTests/SqlServer/EndToEnd/SynchronousMethodTests.cs`
  - `SynchronousMethodTests.TouchAccountSync_RowLockedPastACommandTimeoutOfOneSecond_ThrowsWhatTheAsyncMethodThrows`: F-DatabaseTests.SqlServer.EndToEnd-2-02 (phase 94)
- `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs`
  - `RealisticNullabilityOracleTests.RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound`: F-DatabaseTests.SqlServer-2-02 (phase 87)
  - `RealisticNullabilityOracleTests.RunAsync_SampleThatIsAnExpressionWithAComment_IsBound`: F-DatabaseTests.SqlServer-2-02 (phase 87)
- `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelsTests.cs`
  - `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`: F-DatabaseTests.SqlServer-2-01 (phase 87)
- `tests/DatabaseTests/SqlServer/SqlServerDatabasesTests.cs`
  - `SqlServerDatabasesTests.ImageBuilds_EveryImage_IsPinnedAndNotLatest`: F-DatabaseTests.SqlServer-1-04 (phase 85)
  - `SqlServerDatabasesTests.Image_OfTheRun_IsInTheMapOfBuilds`: F-DatabaseTests.SqlServer-1-04 (phase 85)
- `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`
  - `DiagnosticsDocumentTests.Document_Sections_AreTheDescriptorsInOrder`: F-SqlSource.Tests-1-01 (phase 02)
  - `DiagnosticsDocumentTests.Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor`: F-SqlSource.Tests-1-01 (phase 02)
- `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`
  - `UserDocsLinksTests.Links_OfTheUserDocs_AreFound`: F-SqlSource.Repository.Tests-1-02 (phase 75)
  - `UserDocsLinksTests.RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist`: F-SqlSource.Repository.Tests-1-02 (phase 75)
- `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs`
  - `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix`: F-SqlSource.Repository.Tests-1-02 (phase 75)
- `tests/SqlSource.Repository.Tests/Validation/TestScopesFileTests.cs`
  - `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects`: F-SqlSource.Repository.Tests-2-01 (phase 75)
- `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs`
  - `TestScopesTests.Parse_ProjectLine_MapsTheProjectToItsEngine`: F-SqlSource.Repository.Tests-2-01 (phase 75)
- `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs`
  - `SettingsFileDiagnosticsTests.Create_EachKind_GivesItsDescriptor`: F-SqlSource.Tests.Generation-1-07 (phase 36)
- `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`
  - `SqlDiagnosticsTests.All_EveryDescriptor_IsAnErrorThatCannotBeConfiguredButTheListedOnes`: F-SqlSource.Tests-1-01 (phase 02)
- `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs`
  - `ListedSettingsFileTests.Read_InvalidValue_IsReportedByTheSidesThatReadItsSetting`: F-SqlSource.Tests.Generation-1-07 (phase 36)
  - `ListedSettingsFileTests.Read_PathWithBackslashes_IsReadAtItsNormalisedFolder`: F-SqlSource.Tests.Generation-1-09 (phase 36)
- `tests/SqlSource.Tests/Generation/Chains/SettingsFileSetTests.cs`
  - `SettingsFileSetTests.ChainOf_FileEditedToTextThatIsNotJson_PassesThrough`: F-SqlSource.Tests.Generation-1-08 (phase 36)
  - `SettingsFileSetTests.ChainOf_FoldersNotInAscii_AreFoundInTheOrderTheyAreSorted`: F-SqlSource.Tests.Generation-1-09 (phase 36)
- `tests/SqlSource.Tests/Generation/Converters/ConverterTargetsTests.cs`
  - `ConverterTargetsTests.Serves_SqlServerEdge_DoesNotServeNpgsql10`: F-SqlSource.Tests.Generation-1-11 (phase 69)
- `tests/SqlSource.Tests/Generation/Converters/LibraryConvertersTests.cs`
  - `LibraryConvertersTests.Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation`: F-SqlSource.Tests.Generation-1-11 (phase 69)
  - `LibraryConvertersTests.Of_WithJson_HasTheTenEdges`: F-SqlSource.Tests.Generation-1-12 (phase 69)
  - `LibraryConvertersTests.Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem`: F-SqlSource.Tests.Generation-1-10 (phase 69)
  - `LibraryConvertersTests.Of_WithNodaTime_HasTheFourteenEdges`: F-SqlSource.Tests.Generation-1-10 (phase 69), F-SqlSource.Tests.Generation-1-12 (phase 69)
- `tests/SqlSource.Tests/Generation/Emitting/GeneratedTextTests.cs`
  - `GeneratedTextTests.Deeper_IndentOfNoWholeLevel_AddsOneLevel`: F-SqlSource.Tests.Generation.Emitting-1-09 (phase 04)
  - `GeneratedTextTests.IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel`: F-SqlSource.Tests.Generation.Emitting-1-08 (phase 04)
- `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs`
  - `ParameterBinderTests.Append_NullableOrReferencePropertyWithAConverter_SendsDbNullForNull`: F-SqlSource.Tests.Generation.Emitting-1-07 (phase 04)
- `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs`
  - `PostgresParameterBinderTests.Append_ArrayConvertedByElement_BindsTheDatabaseArray`: F-SqlSource.Tests.Generation.Emitting-1-07 (phase 04)
- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs`
  - `CallSpellingTests.AppendAwait_OfEachSpelling_IsAwaitOrNothing`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-04 (phase 04)
  - `CallSpellingTests.AppendCallEnd_OfEachSpelling_ClosesTheCall`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-05 (phase 04)
  - `CallSpellingTests.AppendCallMember_Call_IsTheMemberAloneOfEachSpelling`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-04 (phase 04)
  - `CallSpellingTests.AppendCallStart_EachCall_IsTheCallOfEachSpelling`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-04 (phase 04), F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-05 (phase 04)
  - `CallSpellingTests.AppendToken_AsTheOnlyArgument_IsTheTokenOrNothing`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-05 (phase 04)
- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs`
  - `ExecutionCapabilityRulesTests.Of_CollectionMethodAtAMode_RestoresTheSessionsIsolationOnlyWithItsOwnTransactionOnSqlServer`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-01 (phase 70), F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-02 (phase 70)
  - `ExecutionCapabilityRulesTests.Of_CollectionMethod_GivesItsEnginesFlags`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-02 (phase 70)
  - `ExecutionCapabilityRulesTests.Of_EachEngineShapeAndStreaming_GivesItsFlags`: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-07 (phase 70)
- `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`
  - `RowBufferTests.IsUsedBy_EachShapeCollectionAndFlag_IsTrueOnlyForAPooledManyOfAllButTheImmutableLists`: F-SqlSource.Tests.Generation.Emitting-1-06 (phase 04)
- `tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs`
  - `TypeEmitterTests.Emit_NestedTypeOfOnlyMethodsQueries_WritesNoNestedClass`: F-SqlSource.Tests.Generation.Emitting-1-10 (phase 21)
- `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs`
  - `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept`: F-SqlSource.Tests.Generation.Emitting-1-11 (phase 70)
- `tests/SqlSource.Tests/Generation/FileParseInputTests.cs`
  - `FileParseInputTests.Resolve_Dialect_IsTheChainsOrElseAnsi`: F-SqlSource.Tests.Generator-2-05 (phase 19)
- `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`
  - `MethodArgumentsTests.AppendDeclaration_TokenNamedAwait_IsVerbatim`: F-SqlSource.Tests.Generation-2-03 (phase 22)
- `tests/SqlSource.Tests/Generation/Methods/MethodHostTests.cs`
  - `MethodHostTests.IsExtension_EachLocation_IsTheExtensionClassAlone`: F-SqlSource.Tests.Generation-2-03 (phase 22)
- `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`
  - `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments`: F-SqlSource.Tests.Generation-2-05 (phase 23)
  - `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces`: F-SqlSource.Tests.Generation-2-05 (phase 23)
- `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`
  - `PropertyNamesTests.FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone`: F-SqlSource.Tests.Generation-2-07 (phase 24), F-SqlSource.Tests.Generation-2-08 (phase 24)
  - `PropertyNamesTests.FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable`: F-SqlSource.Tests.Generation-2-08 (phase 24)
  - `PropertyNamesTests.IsReserved_EachName_IsTrue`: F-SqlSource.Tests.Generator-5-03 (phase 19)
- `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs`
  - `PathResolverTests.Resolve_FileSource_TakesThatFile`: F-SqlSource.Tests.Generation-2-09 (phase 25)
  - `PathResolverTests.Resolve_FolderSource_TakesTheFilesInThatFolder`: F-SqlSource.Tests.Generation-2-09 (phase 25)
  - `PathResolverTests.Resolve_NoSource_TakesTheFileNamedForTheType`: F-SqlSource.Tests.Generation-2-09 (phase 25)
  - `PathResolverTests.Resolve_SourceDot_TakesTheFilesInTheFolderOfTheSourceFileAndNotItsSubfolders`: F-SqlSource.Tests.Generation-2-09 (phase 25)
  - `PathResolverTests.Resolve_SourceThatMatchesNothing_IsAnErrorAtTheAttribute`: F-SqlSource.Tests.Generation-2-09 (phase 25)
- `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs`
  - `ConnectionCandidateReaderTests.Read_MarkedMemberOfAnotherShape_IsUnsupported`: F-SqlSource.Tests.Generation-3-02 (phase 42)
- `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`
  - `ConnectionResolverTests.Resolve_MarkedCandidate_HasTheKindOfWhatItGives`: F-SqlSource.Tests.Generation-3-08 (phase 42)
  - `ConnectionResolverTests.Resolve_OneConventionCandidate_IsTheSource`: F-SqlSource.Tests.Generation-3-08 (phase 42)
- `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`
  - `SqlFileReaderTests.Read_FileParseInput_ReadsTheFileWithItsDialect`: F-SqlSource.Tests.Generation-1-13 (phase 03)
  - `SqlFileReaderTests.Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries`: F-SqlSource.Tests.Generation-1-13 (phase 03)
- `tests/SqlSource.Tests/Generation/Types/FileTypesInputTests.cs`
  - `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt`: F-SqlSource.Tests.Generation.Types-1-09 (phase 26)
  - `FileTypesInputTests.TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt`: F-SqlSource.Tests.Generation.Types-1-09 (phase 26)
- `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs`
  - `MappedTypeNamesTests.Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder`: F-SqlSource.Tests.Generation.Types-1-08 (phase 05)
- `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`
  - `PostgresTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`: F-SqlSource.Tests.Generation.Types-1-02 (phase 05), F-SqlSource.Tests.Generator-6-04 (phase 67), F-SqlSource.Tests.Generator-6-05 (phase 67)
  - `PostgresTypeMapTests.Map_JsonOn_GivesJsonElement`: F-SqlSource.Tests.Generation.Types-1-06 (phase 05)
  - `PostgresTypeMapTests.Map_TypeReadByACastToText_NamesTheCastOfItsRank`: F-SqlSource.Tests.Generator-6-04 (phase 67)
  - `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance`: F-SqlSource.Tests.Generation.Types-1-04 (phase 05), F-SqlSource.Tests.Generation.Types-1-05 (phase 05)
- `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs`
  - `SqlServerTypeMapTests.Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase`: F-SqlSource.Tests.Generation.Types-1-07 (phase 05)
  - `SqlServerTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`: F-SqlSource.Tests.Generation.Types-1-03 (phase 05)
  - `SqlServerTypeMapTests.Map_OverrideAndLibrary_OverrideWins`: F-SqlSource.Tests.Generation.Types-1-07 (phase 05)
- `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`
  - `ArrayPoolParameterTests.Run_ArrayPoolOff_GivesTheTextOfBeforeThePoolForThePooledTypes`: F-SqlSource.Tests.Generator-6-07 (phase 01)
- `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs`
  - `AttributeConflictTests.Build_UseOfAGeneratedType_HasNoConflict`: F-SqlSource.Tests.Generator-1-01 (phase 01)
- `tests/SqlSource.Tests/Generator/CachingTypeTests.cs`
  - `CachingTypeTests.LibraryTypesMarker_Edit_RetypesOnlyItsFile`: F-SqlSource.Tests.Generator-2-03 (phase 01)
- `tests/SqlSource.Tests/Generator/DialectTests.cs`
  - `DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile`: F-SqlSource.Tests.Generator-6-09 (phase 01), F-SqlSource.Tests.Generator-6-10 (phase 01), F-SqlSource.Tests.Generator-6-11 (phase 01)
- `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`
  - `GeneratorParameterLevelTests.Run_GeneratorParametersWhoseNearestStateIsOn_KeepTheComment`: F-SqlSource.Tests.Generator-1-02 (phase 19)
- `tests/SqlSource.Tests/Generator/LibraryDiagnosticsTests.cs`
  - `LibraryDiagnosticsTests.Run_NodaTimeBelowTheFloorAndAMethodsUse_Reports282Once`: F-SqlSource.Tests-1-01 (phase 02)
  - `LibraryDiagnosticsTests.Run_OnWithoutPlugin_KeepsTheQueriesMethodsWithBaseTypes`: F-SqlSource.Tests.Generator-4-01 (phase 01)
- `tests/SqlSource.Tests/Generator/MethodTests.cs`
  - `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType`: F-SqlSource.Tests.Generator-5-02 (phase 67)
  - `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass`: F-SqlSource.Tests.Generator-6-01 (phase 67)
- `tests/SqlSource.Tests/Generator/ParameterTypeTests.cs`
  - `ParameterTypeTests.Run_ParamMarker_WinsOverTheTypeOverride`: F-SqlSource.Tests.Generator-5-06 (phase 01)
- `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`
  - `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod`: F-SqlSource.Tests.Generator-5-07 (phase 19)
- `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`
  - `QueryTypesTests.Run_Int4_IsIntUnderEachDriver`: F-SqlSource.Tests.Generator-6-03 (phase 67)
  - `QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText`: F-SqlSource.Tests.Generator-6-04 (phase 67)
  - `QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe`: F-SqlSource.Tests.Generator-6-05 (phase 67)
- `tests/SqlSource.Tests/Generator/RowBufferTests.cs`
  - `RowBufferTests.Run_ManyMethodOfEachPooledTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool`: F-SqlSource.Tests.Generator-6-07 (phase 01)
- `tests/SqlSource.Tests/Generator/SettingLevelTests.cs`
  - `SettingLevelTests.Run_MemberLocationInTheHeader_PlacesTheMethod`: F-SqlSource.Tests.Generator-6-01 (phase 67)
  - `SettingLevelTests.Run_SqlOutputAtEachLevel_GivesAConstant`: F-SqlSource.Tests.Generator-6-12 (phase 01)
- `tests/SqlSource.Tests/Generator/SettingsFileBuildFoldersTests.cs`
  - `SettingsFileBuildFoldersTests.Run_CompileItemUnderAFolderOfTheBuildOutsideTheProject_ReachesNoSettingsFile`: F-SqlSource.Tests.Generator-6-08 (phase 01)
  - `SettingsFileBuildFoldersTests.Run_CompileItemUnderTheIntermediateFolder_ReachesNoSettingsFile`: F-SqlSource.Tests.Generator-6-08 (phase 01)
- `tests/SqlSource.Tests/Generator/SharedModelTests.cs`
  - `SharedModelTests.Run_TwoInternalMappedTypesInAPublicModel_AreTwoSqlsrc251AtTheQuery`: F-SqlSource.Tests.Generator-7-02 (phase 01)
- `tests/SqlSource.Tests/Generator/SidecarTests.cs`
  - `SidecarTests.Run_EachDescribableDialect_IsNeverSqlsrc243`: F-SqlSource.Tests.Generator-7-01 (phase 01)
- `tests/SqlSource.Tests/Generator/TableModelTests.cs`
  - `TableModelTests.Run_ThreeTablesOfOneModelName_AreNamedOnceEach`: F-SqlSource.Tests.Generator-7-03 (phase 01)
- `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`
  - `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute`: F-SqlSource.Tests.Generator-8-02 (phase 67)
  - `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute`: F-SqlSource.Tests.Generator-8-01 (phase 67)
- `tests/SqlSource.Tests/Package/BuildFileTests.cs`
  - `BuildFileTests.Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks`: F-SqlSource.Tests.Package-1-03 (phase 78)
  - `BuildFileTests.Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder`: F-SqlSource.Tests.Package-1-02 (phase 78)
- `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`
  - `SqlGeneratorParameterScopeTests.Read_DefaultWithAValue_IsInvalidWithNoValues`: F-SqlSource.Tests.Parsing-2-12 (phase 71)
  - `SqlGeneratorParameterScopeTests.Read_OneOptionInTwoStates_ConflictsAtTheLater`: F-SqlSource.Tests.Parsing-2-13 (phase 06)
  - `SqlGeneratorParameterScopeTests.Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues`: F-SqlSource.Tests.Parsing-2-12 (phase 71)
- `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs`
  - `SqlMarkerReaderTests.Describe_Marker_IsItsWordAndItsValue`: F-SqlSource.Tests.Parsing-2-11 (phase 71)
  - `SqlMarkerReaderTests.Read_MarkerComment_ReturnsItsKindAndValue`: F-SqlSource.Tests.Parsing-2-11 (phase 71)
- `tests/SqlSource.Tests/Parsing/Quoting/DollarQuoteReaderTests.cs`
  - `DollarQuoteReaderTests.FindEnd_OpenerWithItsCloser_EndsAfterTheSameTag`: F-SqlSource.Tests.Parsing-2-06 (phase 06)
- `tests/SqlSource.Tests/Parsing/Quoting/DoubledQuoteReaderTests.cs`
  - `DoubledQuoteReaderTests.FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNotDoubled`: F-SqlSource.Tests.Parsing-2-07 (phase 06)
- `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs`
  - `EscapeStringReaderTests.FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix`: F-SqlSource.Tests.Parsing-2-10 (phase 06)
  - `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix`: F-SqlSource.Tests.Parsing-2-07 (phase 06), F-SqlSource.Tests.Parsing-2-08 (phase 06), F-SqlSource.Tests.Parsing-2-09 (phase 06)
- `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs`
  - `SqlDialectMarkerTests.Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption`: F-SqlSource.Tests.Generator-2-04 (phase 19)
- `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
  - `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect`: F-SqlSource.Tests.Generator-2-05 (phase 19), F-SqlSource.Tests.Generator-2-06 (phase 19)
  - `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions`: F-SqlSource.Tests.Generator-2-04 (phase 19)
- `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`
  - `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter`: F-SqlSource.Tests.Parsing-1-11 (phase 71)
  - `SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregateTwice_IsReportedAtEachUseAsWritten`: F-SqlSource.Tests.Parsing-1-09 (phase 71)
- `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs`
  - `SqlFileParserTokenTests.Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported`: F-SqlSource.Tests.Parsing-1-08 (phase 71)
  - `SqlFileParserTokenTests.Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted`: F-SqlSource.Tests.Parsing-1-10 (phase 71)
- `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`
  - `SqlLexerTests.Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment`: F-SqlSource.Tests.Parsing-2-07 (phase 06)
  - `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`: F-SqlSource.Tests.Parsing-2-06 (phase 06), F-SqlSource.Tests.Parsing-2-08 (phase 06), F-SqlSource.Tests.Parsing-2-09 (phase 06)
  - `SqlLexerTests.Lex_DollarTagWithANonAsciiCharacter_IsAQuote`: F-SqlSource.Tests.Parsing-2-06 (phase 06)
  - `SqlLexerTests.Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix`: F-SqlSource.Tests.Parsing-2-10 (phase 06)
- `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs`
  - `SqlListPlacesTests.Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing`: F-SqlSource.Tests.Parsing-2-16 (phase 71)
- `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`
  - `SqlQueryHashTests.Compute_BareParamMarker_IsTheHashWithoutIt`: F-SqlSource.Tests.Parsing-2-15 (phase 06)
  - `SqlQueryHashTests.Compute_ChangeToWhatIsDescribed_GivesAnotherHash`: F-SqlSource.Tests.Parsing-2-15 (phase 06)
  - `SqlQueryHashTests.Compute_SimpleQuery_IsTheSha256OfItsInput`: F-SqlSource.Tests.Parsing-2-14 (phase 06)
- `tests/SqlSource.Tests/Parsing/Tokens/TokenScannerTests.cs`
  - `TokenScannerTests.Scan_ReservedNameWithADefault_IsAnError`: F-SqlSource.Tests.Parsing-2-13 (phase 06)
- `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs`
  - `ColumnTypeMapTests.Find_HeaderWins_OverASchemaInASettingsFile`: F-SqlSource.Tests.Settings-1-02 (phase 07)
- `tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs`
  - `GeneratorParameterChoicesTests.Reset_Resolves_ToTheDefaults`: F-SqlSource.Tests.Generation-1-14 (phase 20)
- `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`
  - `GeneratorParameterListTests.ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`: F-SqlSource.Tests-1-02 (phase 02), F-SqlSource.Tests.Generation-1-14 (phase 20)
  - `GeneratorParameterListTests.ReadElements_Words_NameTheirStates`: F-SqlSource.Tests.Generation-1-14 (phase 20)
- `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs`
  - `LibrarySettingsTests.Over_OneNull_IsTheOtherItself`: F-SqlSource.Tests.Settings-1-14 (phase 47)
- `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`
  - `QuerySettingsTests.KeepsComments_OfALevel_IsTheLevelsResolvedKeepComments`: F-SqlSource.Tests.Generation-1-14 (phase 20)
  - `QuerySettingsTests.Over_EachMember_ComesFromThisLevelWhenItHasOne`: F-SqlSource.Tests.Settings-1-14 (phase 47)
  - `QuerySettingsTests.Resolve_NoLevelSaysAnything_GivesTheDefaults`: F-SqlSource.Tests.Settings-1-08 (phase 47)
  - `QuerySettingsTests.Resolve_Parameters_MergePerOption`: F-SqlSource.Tests.Settings-1-08 (phase 47)
- `tests/SqlSource.Tests/Settings/SettingValueTests.cs`
  - `SettingValueTests.IsNestedClassName_OneIdentifierThatCanNameANestedClass_IsAName`: F-SqlSource.Tests.Generator-5-05 (phase 19)
  - `SettingValueTests.IsQualifiedName_IdentifiersJoinedByPeriods_IsAName`: F-SqlSource.Tests.Generator-5-04 (phase 19)
- `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs`
  - `TypeKeyTextTests.WritePart_WhiteSpaceThatIsNoControlCharacter_IsKeptInTheQuotes`: F-SqlSource.Tests.Settings-1-17 (phase 27)
- `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs`
  - `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives`: F-SqlSource.Tests.Settings-1-19 (phase 47), F-SqlSource.Tests.Settings-1-20 (phase 47)
- `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs`
  - `SettingsFileReaderTests.Read_GeneratorParameters_FollowTheElementRules`: F-SqlSource.Tests-1-02 (phase 02)
- `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`
  - `SidecarReaderTests.Read_OrdersExample_ReadsTheShapeOfSqlServerTypes`: F-SqlSource.Tests.Snapshot-1-05 (phase 48)
  - `SidecarReaderTests.Read_UsersExample_ReadsAnEntryWithoutRows`: F-SqlSource.Tests.Snapshot-1-05 (phase 48)
- `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs`
  - `SidecarTokenizerTests.Next_TextThatIsNotJson_IsInvalidAtTheCharacter`: F-SqlSource.Tests.Snapshot-1-04 (phase 08)
  - `SidecarTokenizerTests.TrySkipValue_BrokenGrammar_FailsAtTheToken`: F-SqlSource.Tests.Snapshot-1-02 (phase 08), F-SqlSource.Tests.Snapshot-1-04 (phase 08)
- `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`
  - `SidecarComparerTests.IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys`: F-SqlSource.Tests.Snapshot-1-01 (phase 48)
- `tests/SqlSource.Tests/SqlIdentifierTests.cs`
  - `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable`: F-SqlSource.Tests.Generator-5-04 (phase 19)
- `tests/SqlSource.Tests/TestHarness/SyncAsync/VerdictsTests.cs`
  - `VerdictsTests.Of_OutcomesDifferingInOneField_IsDifferent`: F-SqlSource.Tests.TestHarness-1-03 (phase 76)
- `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs`
  - `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`: F-SqlSource.Tool.Tests.Bundles-1-01 (phase 29), F-SqlSource.Tool.Tests.Bundles-1-02 (phase 29), F-SqlSource.Tool.Tests.Commands-2-04 (phase 50), F-SqlSource.Tool.Tests.Commands-2-05 (phase 50)
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`
  - `NameMappingTests.IsMapped_AnotherCaseOfAMappedName_IsTrue`: F-SqlSource.Tool.Tests.Bundles.Renaming-1-05 (phase 52)
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.Fallback.cs`
  - `SqlServerErrorRenamingTests.RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts`: F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-01 (phase 11)
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs`
  - `SqlServerErrorRenamingTests.RenameAsync_AColumnOfTheServersThroughALinkedServer_IsRenamed`: F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-04 (phase 11)
  - `SqlServerErrorRenamingTests.RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace`: F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-04 (phase 11)
- `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs`
  - `SqlServerFieldsTests.RuleOf_AMemberOfTheRecords_IsItsRule`: F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-04 (phase 11)
  - `SqlServerFieldsTests.RuleOf_AnyOtherMember_IsNull`: F-SqlSource.Tool.Tests.Bundles.Renaming.SqlServer-1-04 (phase 11)
- `tests/SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`
  - `CheckFilterTests.Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe`: F-SqlSource.Tool.Tests.Commands-1-03 (phase 28)
- `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs`
  - `CheckTests.Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo`: F-SqlSource.Tool.Tests.Commands-1-08 (phase 10)
- `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`
  - `CliTests.Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne`: F-SqlSource.Tool.Tests.Commands-1-09 (phase 10)
- `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs`
  - `DescribeRunTests.Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten`: F-SqlSource.Tool.Tests.Commands-1-07 (phase 10)
  - `DescribeRunTests.Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten`: F-SqlSource.Tool.Tests.Commands-1-06 (phase 10)
  - `DescribeRunTests.Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten`: F-SqlSource.Tool.Tests.Commands-1-05 (phase 10)
- `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs`
  - `DescribeTests.Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing`: F-SqlSource.Tool.Tests.Commands-1-09 (phase 10)
  - `DescribeTests.Run_NoUnit_ReportsItAndExitsWithOne`: F-SqlSource.Tool.Tests.Commands-1-10 (phase 10)
- `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`
  - `DiagnoseTests.Diagnose_OutIsAFileOfTheRun_IsSQLSRC218`: F-SqlSource.Tool.Tests.Commands-1-04 (phase 28)
- `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs`
  - `FilterTests.Run_Help_ShowsThePathsAndTheOptionsOfDescribe`: F-SqlSource.Tool.Tests.Commands-1-02 (phase 50)
- `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`
  - `ReplayTests.Replay_EngineWithNoDescriber_IsSqlsrc291`: F-SqlSource.Tool.Tests.Commands-2-02 (phase 50)
  - `ReplayTests.Replay_NoBundle_IsAWrongCommandLine`: F-SqlSource.Tool.Tests.Commands-2-03 (phase 50)
  - `ReplayTests.Replay_NothingAtThePath_IsSqlsrc288`: F-SqlSource.Tool.Tests.Commands-2-02 (phase 50)
- `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`
  - `SecretTests.Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace`: F-SqlSource.Tool.Tests.Commands-2-06 (phase 99)
  - `SecretTests.Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace`: F-SqlSource.Tool.Tests.Commands-2-06 (phase 99)
  - `SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`: F-engines-03 (phase 99)
  - `SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`: F-DatabaseTests.SqlServer-1-03 (phase 99)
- `tests/SqlSource.Tool.Tests/Commands/SummaryTests.cs`
  - `SummaryTests.Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition`: F-SqlSource.Tool.Tests.Commands-2-07 (phase 99)
- `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs`
  - `UsageCheckTests.CheckUsage_ReplayLogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition`: F-SqlSource.Tool.Tests.Commands-2-03 (phase 50)
- `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`
  - `ConnectionTests.For_VariableThatHoldsOnlyWhiteSpace_IsNotSet`: F-SqlSource.Tool.Tests.Describing-1-04 (phase 74)
- `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs`
  - `DatabaseRunsFaultTests.Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed`: F-SqlSource.Tool.Tests.Describing-1-03 (phase 12)
- `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`
  - `DatabaseRunsTests.Run_Describer_DescribesEachQueryInOneSessionForItsDatabase`: F-SqlSource.Tool.Tests.Describing-1-03 (phase 12)
- `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs`
  - `DescriberSessionTests.OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection`: F-SqlSource.Tool.Tests.Describing.Describers-1-03 (phase 13)
- `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`
  - `DescriberTests.DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart`: F-SqlSource.Tool.Tests.Describing.Describers-1-02 (phase 54)
- `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs`
  - `TableMatchChecksTests.OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue`: F-SqlSource.Tool.Tests.Describing.Describers-1-04 (phase 13)
  - `TableMatchChecksTests.OfTheTable_EveryCheckFails_IsNamesDiffer`: F-SqlSource.Tool.Tests.Describing.Describers-1-04 (phase 13)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Columns/ColumnNullabilityTests.cs`
  - `ColumnNullabilityTests.Of_NotNullColumnOfAPartitionedTableWithAForeignPartition_IsNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-06 (phase 14)
  - `ColumnNullabilityTests.Of_NotNullColumnOfAPartitionedTableWithoutAForeignPartitionOrUnsaid_IsNotNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres-2-02 (phase 14)
  - `ColumnNullabilityTests.Of_NotNullColumnOfATableWithChildren_IsNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-05 (phase 14)
  - `ColumnNullabilityTests.Of_NotNullColumnOfATableWithoutAnInsteadRuleOrUnsaid_IsNotNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres-2-05 (phase 14)
  - `ColumnNullabilityTests.Of_NotNullColumnOfATableWithoutChildrenOrUnsaid_IsNotNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres-2-03 (phase 14)
  - `ColumnNullabilityTests.Of_NotNullColumnWhoseConstraintIsValidatedOrUnsaid_IsNotNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres-2-03 (phase 14)
  - `ColumnNullabilityTests.Of_NullableColumnWhateverTheConstraintMemberSays_IsNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres-2-04 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/NpgsqlCheckTests.cs`
  - `NpgsqlCheckTests.Read_AnswerWithoutSent_ComparesColumnsAlone`: F-SqlSource.Tool.Tests.Describing.Postgres-1-02 (phase 14)
  - `NpgsqlCheckTests.Read_ErrorOfTheServer_IsADisagreementOnlyWhereAMisreadAtCouldGiveIt`: F-SqlSource.Tool.Tests.Describing.Postgres-1-01 (phase 14)
  - `NpgsqlCheckTests.Read_SameTextsAndColumns_IsSame`: F-SqlSource.Tool.Tests.Describing.Postgres-1-02 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`
  - `PostgresErrorsTests.Check_ServerErrorOfClass42_IsSqlsrc231`: F-SqlSource.Tool.Tests.Describing.Postgres-1-04 (phase 14)
  - `PostgresErrorsTests.Check_VerdictErrorOutsideClass42_IsSqlsrc225AtCheckParameters`: F-SqlSource.Tool.Tests.Describing.Postgres-1-03 (phase 14)
  - `PostgresErrorsTests.Prepare_ErrorWithoutASqlState_IsSqlsrc225WithTheMessageAlone`: F-SqlSource.Tool.Tests.Describing.Postgres-1-03 (phase 14), F-SqlSource.Tool.Tests.Describing.Postgres-1-04 (phase 14)
  - `PostgresErrorsTests.SchemaChanged_TableThatWentAtTheCatalog_IsSqlsrc225WithNoServerLineAndTheHelp`: F-describers-02 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
  - `PostgresEventsTests.DescribeAsync_QueryThroughEveryCall_NamesTheQueryInEachOfItsEvents`: F-SqlSource.Tool.Tests.Describing.Postgres-1-05 (phase 14), F-SqlSource.Tool.Tests.Describing.Postgres-1-06 (phase 14)
  - `PostgresEventsTests.DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`: F-SqlSource.Tool.Tests.Describing.Postgres-1-07 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionColumnTests.cs`
  - `PostgresSessionColumnTests.DescribeAsync_TypeOfAColumnThatTypesLacks_IsSqlsrc225AtResolveTypeAndAsksNoCheck`: F-describers-02 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs`
  - `PostgresSessionPlanTests.DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable`: F-SqlSource.Tool.Tests.Describing.Postgres-2-06 (phase 30)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
  - `PostgresSessionTests.DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing`: F-DatabaseTests.Postgres.Describer-1-05 (phase 98)
  - `PostgresSessionTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing`: F-DatabaseTests.Postgres.Describer-1-06 (phase 98)
  - `PostgresSessionTests.DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing`: F-DatabaseTests.Postgres.Describer-1-07 (phase 98)
  - `PostgresSessionTests.DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing`: F-DatabaseTests.Postgres.Describer-1-04 (phase 98)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/StatementShapeTests.cs`
  - `StatementShapeTests.RefusedKind_RefusedFirstWord_IsThatWord`: F-DatabaseTests.Postgres.Describer-1-05 (phase 98)
  - `StatementShapeTests.RefusedKind_SelectInto_IsSelectInto`: F-DatabaseTests.Postgres.Describer-1-05 (phase 98)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNameTests.cs`
  - `NullabilityWalkNameTests.Run_BareNameUnderAJoin_IsUnmappable`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-08 (phase 56)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNotNullTests.cs`
  - `NullabilityWalkNotNullTests.Run_NotNullThatIsNotValidated_IsNullableByTheCatalog`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-05 (phase 14), F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-06 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/OutputReferenceTests.cs`
  - `OutputReferenceTests.Parse_Expression_IsNoReference`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-08 (phase 56)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs`
  - `PlanInstancesTests.Find_NodeThatIsNoScanOfARelation_NamesNone`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-09 (phase 30)
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs`
  - `PlanNodeTests.Read_TextThatIsNotAnArrayOfOnePlan_IsNull`: F-SqlSource.Tool.Tests.Describing.Postgres-2-06 (phase 30)
  - `PlanNodeTests.SelfAndDescendants_Tree_IsTheNodeThenEachChildAndAllUnderItInOrder`: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-07 (phase 14)
- `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs`
  - `SidecarStoreTests.Apply_FileOfAnotherName_IsLeft`: F-SqlSource.Tool.Tests.Describing-1-07 (phase 74)
  - `SidecarStoreTests.Apply_RunThatIsCancelledAsItBegins_RemovesNothing`: F-SqlSource.Tool.Tests.Describing-1-08 (phase 74)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs`
  - `SqlServerColumnsTests.OriginOf_Expression_IsNone`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-01 (phase 15)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ColumnProofsTests.cs`
  - `ColumnProofsTests.Apply_OperandNotNull_ColumnProved`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-06 (phase 58)
  - `ColumnProofsTests.Read_NothingToAsk_OperandsSqlIsNull`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-05 (phase 58)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs`
  - `ExpressionProofsTests.Of_Form_IsProvedAsExpected`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-05 (phase 58), F-SqlSource.Tool.Tests.Describing.SqlServer-2-06 (phase 58)
  - `ExpressionProofsTests.Operands_TwoItems_OneListInOrder`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-06 (phase 58)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs`
  - `ProofTests.Holds_NotClrWithoutAnswers_DoesNotHold`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-07 (phase 15)
  - `ProofTests.Holds_OperandBeyondTheAnswers_DoesNotHold`: F-SqlSource.Tool.Tests.Describing.SqlServer-2-07 (phase 15)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs`
  - `SqlServerDescriberTests.OpenAsync_ConnectionStringRefusedBeforeOpening_IsSqlsrc224WithTheFixedLine`: F-DatabaseTests.SqlServer-1-02 (phase 99)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`
  - `SqlServerErrorsTests.Columns_OtherType_Is225WithEveryLineInOrder`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-03 (phase 58)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs`
  - `SqlServerEventsSessionTests.DescribeAsync_OneTable_WritesTableBeforeTheMatch`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-04 (phase 15)
  - `SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`: F-describers-01 (phase 15)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsTests.cs`
  - `SqlServerEventsTests.Parameters_ErrorOfTheServer_WritesEveryErrorInThePlaceOfTheRows`: F-describers-01 (phase 15)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs`
  - `SqlServerSessionProofTests.DescribeAsync_BareNullableColumn_AsksTableAndNoOperands`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-04 (phase 15)
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTableTests.cs`
  - `SqlServerSessionTableTests.DescribeAsync_NoOrigin_AsksNoTable`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-05 (phase 15)
  - `SqlServerSessionTableTests.DescribeAsync_TableFails_Is225AtTableMatch`: F-SqlSource.Tool.Tests.Describing.SqlServer-1-03 (phase 58)
- `tests/SqlSource.Tool.Tests/Logging/RunLogFileTests.cs`
  - `RunLogFileTests.Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason`: F-SqlSource.Tool.Tests-2-05 (phase 31)
  - `RunLogFileTests.Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder`: F-SqlSource.Tool.Tests-2-02 (phase 16)
- `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs`
  - `RunLogRedactionTests.Run_ConnectionOnTheCommandLine_IsLoggedWithNeitherItsNameNorItsValue`: F-SqlSource.Tool.Tests-2-03 (phase 32)
  - `RunLogRedactionTests.Run_DescriberThatLogsAParametersEventWithItsConnection_HasTheMarkInEachString`: F-describers-01 (phase 15)
  - `RunLogRedactionTests.Run_PostgresDescriberThatLogsATableMatchEventWithItsConnection_HasTheMarkInResultAndTableMatch`: F-SqlSource.Tool.Tests.Describing.Postgres-1-06 (phase 14)
- `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`
  - `RunLogTests.Sidecar_EachAction_IsItsWord`: F-SqlSource.Tool.Tests-2-06 (phase 32)
- `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs`
  - `SessionRunLogTests.Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach`: F-SqlSource.Tool.Tests-2-04 (phase 16), F-SqlSource.Tool.Tests.Describing.Postgres-1-05 (phase 14), F-SqlSource.Tool.Tests.Describing.Postgres-1-07 (phase 14), F-describers-01 (phase 15)
  - `SessionRunLogTests.Write_WithAPrefixAndAValueSplitInTheName_PutsThePrefixBeforeTheMark`: F-SqlSource.Tool.Tests-2-04 (phase 16)
- `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`
  - `AttributeReaderTests.Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant`: F-SqlSource.Tool.Tests-3-02 (phase 17)
- `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs`
  - `FileTextTests.Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack`: F-SqlSource.Tool.Tests-3-01 (phase 17)
- `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`
  - `RunPlannerTests.Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds`: F-SqlSource.Tool.Tests-3-03 (phase 17)
- `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs`
  - `FixtureProjectTests.Evaluate_RestoredProjectInAFolderWithLettersOutsideAscii_IsSeenAsRestored`: F-SqlSource.Tool.Tests.Projects-1-06 (phase 18)
- `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`
  - `ManifestTargetTests.WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders`: F-SqlSource.Tool.Tests.Projects-1-05 (phase 18)
- `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs`
  - `OnlineBuildTests.Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt`: F-SqlSource.Tool.Tests.Projects-1-07 (phase 18)
  - `OnlineBuildTests.Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile`: F-SqlSource.Tool.Tests.Projects-1-07 (phase 18)
  - `OnlineBuildTests.Build_OnlineTrueInCapitalsWithSpaces_RunsTheTool`: F-SqlSource.Tool.Tests.Projects-1-08 (phase 18)
- `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs`
  - `TransitivePackageAssembliesTargetTests.Run_ReferencesOfAPackageNotListed_AreTheValue`: F-SqlSource.Tool.Tests.Projects-1-04 (phase 18)
- `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`
  - `RealisticClaimsTests.Parse_ClassWithPathAttribute_IsItsClaim`: F-DatabaseTests.Postgres-2-01 (phase 84)
- `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`
  - `ReporterTests.Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt`: F-SqlSource.Tool.Tests-4-05 (phase 63)
  - `ReporterTests.Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference`: F-SqlSource.Tool.Tests-4-05 (phase 63)
- `tests/SqlSource.Tool.Tests/TestProjectTests.cs`
  - `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader`: F-SqlSource.Tool.Tests-1-02 (phase 09)
- `tests/SqlSource.Tool.Tests/UsageValueTests.cs`
  - `UsageValueTests.NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes`: F-SqlSource.Tool.Tests-1-01 (phase 09)

## Gaps that belong to no test

The survivors and the mutants with no coverage of `evidence/gaps.md`, by source file, less the survivors that a **strengthen** names (by file, place and mutator, at most the file's survivors).  A survivor is a mutant that no test of its scope fails on, and a mutant with no coverage is one that no test of its scope runs; tests of other scopes, such as the driver and command tests or the database projects, may still reach either, which a scope does not see.  Files with no gap are left out.

### src/SqlSource

2504 mutants in 142 files: 1003 survivors and 1563 with no coverage, less 62 that a strengthen names.

| File | Survived | No coverage | Named by a strengthen | Gap |
|----|----|----|----|----|
| `src/SqlSource/Generation/Methods/MethodPlanner.cs` | 4 | 422 | 0 | 426 |
| `src/SqlSource/Generation/Models/ModelReader.cs` | 0 | 141 | 0 | 141 |
| `src/SqlSource/Generation/Exceptions/ExceptionProbe.cs` | 0 | 95 | 0 | 95 |
| `src/SqlSource/Generation/Types/QueryTyper.cs` | 56 | 35 | 0 | 91 |
| `src/SqlSource/Generation/Models/ModelCollector.cs` | 40 | 44 | 0 | 84 |
| `src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs` | 61 | 6 | 0 | 67 |
| `src/SqlSource/Generation/Models/TypeModels.cs` | 0 | 65 | 0 | 65 |
| `src/SqlSource/Generation/Converters/ConverterReader.cs` | 22 | 42 | 0 | 64 |
| `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs` | 20 | 40 | 0 | 60 |
| `src/SqlSource/Generation/Models/ModelSummary.cs` | 24 | 33 | 0 | 57 |
| `src/SqlSource/Generation/Types/TypeDiagnostics.cs` | 6 | 51 | 0 | 57 |
| `src/SqlSource/Generation/Methods/MethodsInput.cs` | 1 | 54 | 0 | 55 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ListCountWriter.cs` | 29 | 23 | 0 | 52 |
| `src/SqlSource/Generation/Sidecars/SidecarChecker.cs` | 0 | 51 | 0 | 51 |
| `src/SqlSource/Generation/TargetTypeReader.cs` | 0 | 51 | 0 | 51 |
| `src/SqlSource/Generation/Emitting/ModelEmitter.cs` | 19 | 27 | 0 | 46 |
| `src/SqlSource/Generation/Emitting/MethodEmitter.cs` | 27 | 17 | 0 | 44 |
| `src/SqlSource/Parsing/SqlListUses.Conditions.cs` | 39 | 0 | 0 | 39 |
| `src/SqlSource/Generation/Methods/MethodConflicts.cs` | 6 | 29 | 0 | 35 |
| `src/SqlSource/Parsing/SqlListUses.cs` | 28 | 2 | 0 | 30 |
| `src/SqlSource/Parsing/SqlListUses.Places.cs` | 26 | 5 | 2 | 29 |
| `src/SqlSource/Generation/Types/Drivers/IDriverProbe.cs` | 4 | 24 | 0 | 28 |
| `src/SqlSource/Generation/Types/Drivers/NpgsqlProbe.cs` | 7 | 21 | 0 | 28 |
| `src/SqlSource/Parsing/SqlFileParser.cs` | 28 | 0 | 1 | 27 |
| `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs` | 26 | 0 | 0 | 26 |
| `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs` | 13 | 12 | 2 | 23 |
| `src/SqlSource/Settings/SettingsLevel.cs` | 24 | 0 | 1 | 23 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ListSqlWriter.cs` | 19 | 3 | 0 | 22 |
| `src/SqlSource/Generation/Methods/ConversionPlanner.cs` | 1 | 20 | 0 | 21 |
| `src/SqlSource/Snapshot/SidecarComparer.cs` | 25 | 0 | 4 | 21 |
| `src/SqlSource/Generation/Emitting/QueryMethods/RowReaderWriter.cs` | 2 | 17 | 0 | 19 |
| `src/SqlSource/Generation/Models/ModelNameChecker.cs` | 0 | 19 | 0 | 19 |
| `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` | 19 | 4 | 4 | 19 |
| `src/SqlSource/Generation/Emitting/MethodWriter.cs` | 16 | 2 | 0 | 18 |
| `src/SqlSource/Generation/Emitting/TypeEmitter.cs` | 0 | 17 | 0 | 17 |
| `src/SqlSource/Generation/Repositories/RepositoryShapeReader.cs` | 5 | 11 | 0 | 16 |
| `src/SqlSource/Generation/Types/Drivers/SqlClientProbe.cs` | 6 | 10 | 0 | 16 |
| `src/SqlSource/Settings/ClrTypes/ClrTypeName.cs` | 16 | 0 | 1 | 15 |
| `src/SqlSource/Generation/Converters/ConverterSet.cs` | 7 | 7 | 0 | 14 |
| `src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs` | 13 | 1 | 1 | 13 |
| `src/SqlSource/Generation/Types/MappedTypes/GenericConstraints.cs` | 7 | 6 | 0 | 13 |
| `src/SqlSource/Generation/ColumnMarker.cs` | 0 | 12 | 0 | 12 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs` | 10 | 2 | 0 | 12 |
| `src/SqlSource/Generation/Methods/InterfaceSignature.cs` | 0 | 12 | 0 | 12 |
| `src/SqlSource/Generation/Placement/TypePlacement.cs` | 13 | 0 | 1 | 12 |
| `src/SqlSource/Generation/Types/FileTypesInput.cs` | 0 | 12 | 0 | 12 |
| `src/SqlSource/Settings/TypeOverrides/TypeKeys.cs` | 12 | 0 | 0 | 12 |
| `src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs` | 11 | 2 | 1 | 12 |
| `src/SqlSource/Generation/Methods/MethodArguments.cs` | 4 | 7 | 0 | 11 |
| `src/SqlSource/Settings/Libraries/LibrarySettings.cs` | 11 | 2 | 2 | 11 |
| `src/SqlSource/Generation/Emitting/QueryMethods/HeaderWriter.cs` | 10 | 0 | 0 | 10 |
| `src/SqlSource/Parsing/Tokens/TokenScanner.cs` | 10 | 0 | 0 | 10 |
| `src/SqlSource/Generation/Converters/NetTopologySuiteConversions.cs` | 0 | 9 | 0 | 9 |
| `src/SqlSource/Generation/Paths/SqlPath.cs` | 13 | 0 | 4 | 9 |
| `src/SqlSource/Parsing/SqlLexer.cs` | 9 | 0 | 0 | 9 |
| `src/SqlSource/Parsing/SqlParameterList.cs` | 9 | 0 | 0 | 9 |
| `src/SqlSource/Generation/Sidecars/SidecarCheckInput.cs` | 0 | 8 | 0 | 8 |
| `src/SqlSource/Generation/Sidecars/SidecarFileChecks.cs` | 0 | 8 | 0 | 8 |
| `src/SqlSource/Generation/Sidecars/SidecarNeeds.cs` | 1 | 7 | 0 | 8 |
| `src/SqlSource/Settings/ColumnTypes/ColumnTypeMap.cs` | 7 | 1 | 0 | 8 |
| `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs` | 11 | 0 | 3 | 8 |
| `src/SqlSource/Generation/Chains/SettingsFileSet.cs` | 8 | 0 | 1 | 7 |
| `src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchLocals.cs` | 7 | 0 | 0 | 7 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ListLocals.cs` | 4 | 3 | 0 | 7 |
| `src/SqlSource/Settings/ColumnTypes/ColumnTypeKey.cs` | 4 | 3 | 0 | 7 |
| `src/SqlSource/Settings/SortedKeyedList.cs` | 1 | 6 | 0 | 7 |
| `src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs` | 8 | 0 | 1 | 7 |
| `src/SqlSource/Snapshot/Reading/SidecarReader.cs` | 0 | 7 | 0 | 7 |
| `src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs` | 11 | 4 | 8 | 7 |
| `src/SqlSource/Generation/Methods/QueryMethod.cs` | 2 | 4 | 0 | 6 |
| `src/SqlSource/Generation/Models/ModelsByFile.cs` | 6 | 1 | 1 | 6 |
| `src/SqlSource/Parsing/Tokens/SqlTokenList.cs` | 5 | 3 | 2 | 6 |
| `src/SqlSource/Settings/PerEngine.cs` | 8 | 0 | 2 | 6 |
| `src/SqlSource/SettingsFiles/SettingsFileReader.cs` | 6 | 0 | 0 | 6 |
| `src/SqlSource/Generation/Emitting/ParameterBinder.cs` | 0 | 5 | 0 | 5 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ListBindingWriter.cs` | 5 | 0 | 0 | 5 |
| `src/SqlSource/Generation/Methods/MethodNames.cs` | 0 | 5 | 0 | 5 |
| `src/SqlSource/Generation/SqlFileReader.cs` | 4 | 1 | 0 | 5 |
| `src/SqlSource/Generation/TargetType.cs` | 1 | 4 | 0 | 5 |
| `src/SqlSource/Parsing/SqlTextBuilder.cs` | 5 | 0 | 0 | 5 |
| `src/SqlSource/Parsing/Tokens/OpenDefaultFinder.cs` | 6 | 0 | 1 | 5 |
| `src/SqlSource/Settings/QuerySettings.cs` | 6 | 0 | 1 | 5 |
| `src/SqlSource/SqlSourceGenerator.cs` | 4 | 1 | 0 | 5 |
| `src/SqlSource/Generation/Converters/LibraryConverters.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource/Generation/Methods/Conversion.cs` | 1 | 3 | 0 | 4 |
| `src/SqlSource/Generation/Methods/TypeMethods.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource/Generation/Models/ModelRequest.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource/Generation/Repositories/ConnectionResolver.cs` | 3 | 2 | 1 | 4 |
| `src/SqlSource/Generation/Repositories/RepositoryTypes.cs` | 3 | 1 | 0 | 4 |
| `src/SqlSource/Generation/Types/MappedTypes/MappedTypeNames.cs` | 3 | 1 | 0 | 4 |
| `src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs` | 1 | 3 | 0 | 4 |
| `src/SqlSource/Parsing/SqlDeclaredVariables.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource/Parsing/SqlDialectName.cs` | 5 | 0 | 1 | 4 |
| `src/SqlSource/Settings/TypeOverrides/TypeOverrideMap.cs` | 3 | 1 | 0 | 4 |
| `src/SqlSource/Generation/Emitting/QueryMethods/CommandWriter.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Generation/Methods/MethodDrivers.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Generation/Methods/MethodSignature.cs` | 1 | 2 | 0 | 3 |
| `src/SqlSource/Generation/Models/ModelProperty.cs` | 0 | 3 | 0 | 3 |
| `src/SqlSource/Generation/Models/PropertyNames.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Generation/Sidecars/SidecarFileReader.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Generation/Types/MappedTypes/MappedTypeWarnings.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs` | 4 | 0 | 1 | 3 |
| `src/SqlSource/Generation/Types/MappedTypes/TransitivePackageAssemblies.cs` | 2 | 1 | 0 | 3 |
| `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Parsing/Quoting/QuoteReader.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Settings/ColumnTypes/ColumnTypeGrammar.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Settings/GeneratorParameterValues.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource/EquatableArray.cs` | 3 | 0 | 1 | 2 |
| `src/SqlSource/Generation/Chains/ListedSettingsFile.cs` | 3 | 0 | 1 | 2 |
| `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs` | 4 | 0 | 2 | 2 |
| `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityWriter.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource/Generation/Exceptions/PublicExceptions.cs` | 0 | 2 | 0 | 2 |
| `src/SqlSource/Generation/Models/FileModels.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource/Generation/Models/ParameterSequences.cs` | 0 | 2 | 0 | 2 |
| `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs` | 3 | 0 | 1 | 2 |
| `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs` | 2 | 1 | 1 | 2 |
| `src/SqlSource/Parsing/Quoting/DollarQuoteReader.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource/Settings/SettingValue.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource/Generation/Chains/BuildFolders.cs` | 3 | 0 | 2 | 1 |
| `src/SqlSource/Generation/Emitting/GeneratedText.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Generation/Emitting/HintName.cs` | 2 | 0 | 1 | 1 |
| `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodWriter.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Generation/Emitting/XmlDocWriter.cs` | 4 | 0 | 3 | 1 |
| `src/SqlSource/Generation/IncrementalValuesProviderExtensions.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource/Generation/Methods/DatabaseEngineExtensions.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource/Generation/Methods/SettingsFileLibraryUse.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Generation/Paths/PathCollision.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Parsing/Quoting/BracketReader.cs` | 3 | 0 | 2 | 1 |
| `src/SqlSource/Parsing/Quoting/QuoteOperatorReader.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Parsing/SqlBlockText.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Parsing/SqlQueryHash.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Parsing/Tokens/SqlListSegments.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Settings/GeneratorParameterList.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Settings/OutputKindExtensions.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource/SettingsFiles/SettingsFileSource.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource/Snapshot/Reading/SidecarCursor.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/Snapshot/SidecarJsonBuilder.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/SqlIdentifier.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource/TextLines.cs` | 2 | 0 | 1 | 1 |

### src/SqlSource.Tool

1881 mutants in 149 files: 1028 survivors and 884 with no coverage, less 31 that a strengthen names.

| File | Survived | No coverage | Named by a strengthen | Gap |
|----|----|----|----|----|
| `src/SqlSource.Tool/Watching/WatchSession.cs` | 0 | 173 | 0 | 173 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerLive.cs` | 0 | 102 | 0 | 102 |
| `src/SqlSource.Tool/Bundles/DiagnoseRun.cs` | 0 | 96 | 0 | 96 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/TotalFunctions.cs` | 78 | 1 | 0 | 79 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` | 1 | 77 | 0 | 78 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs` | 63 | 8 | 1 | 70 |
| `src/SqlSource.Tool/Describing/DescribeRun.cs` | 0 | 65 | 0 | 65 |
| `src/SqlSource.Tool/Logging/RunEvents.cs` | 44 | 10 | 0 | 54 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs` | 46 | 1 | 1 | 46 |
| `src/SqlSource.Tool/Describing/SidecarCheck.cs` | 0 | 43 | 0 | 43 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs` | 44 | 4 | 7 | 41 |
| `src/SqlSource.Tool/Describing/RunSummary.cs` | 0 | 39 | 0 | 39 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.cs` | 34 | 2 | 0 | 36 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs` | 7 | 25 | 0 | 32 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Forms.cs` | 32 | 0 | 0 | 32 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.Expressions.cs` | 30 | 2 | 0 | 32 |
| `src/SqlSource.Tool/Describing/SidecarStore.cs` | 22 | 7 | 0 | 29 |
| `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs` | 23 | 5 | 0 | 28 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.SetOperations.cs` | 24 | 2 | 0 | 26 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectStatement.cs` | 18 | 8 | 0 | 26 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Nullability.cs` | 24 | 2 | 1 | 25 |
| `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.Sql.cs` | 22 | 1 | 0 | 23 |
| `src/SqlSource.Tool/Watching/WatchSet.cs` | 22 | 0 | 0 | 22 |
| `src/SqlSource.Tool/Bundles/BundleReplay.cs` | 7 | 12 | 0 | 19 |
| `src/SqlSource.Tool/Describing/Postgres/RowDescriptions.cs` | 0 | 17 | 0 | 17 |
| `src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs` | 13 | 3 | 0 | 16 |
| `src/SqlSource.Tool/Describing/SampleSql.cs` | 14 | 2 | 0 | 16 |
| `src/SqlSource.Tool/Bundles/BundleQueryFile.cs` | 11 | 4 | 0 | 15 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanTokens.cs` | 14 | 1 | 0 | 15 |
| `src/SqlSource.Tool/Bundles/Renaming/RenamingRun.cs` | 10 | 4 | 0 | 14 |
| `src/SqlSource.Tool/Describing/DatabaseRuns.cs` | 13 | 1 | 0 | 14 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlClientRunListener.cs` | 10 | 4 | 0 | 14 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs` | 11 | 3 | 0 | 14 |
| `src/SqlSource.Tool/Bundles/BundleErrors.cs` | 0 | 13 | 0 | 13 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Renaming.cs` | 13 | 0 | 0 | 13 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Words.cs` | 4 | 10 | 1 | 13 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Types.cs` | 11 | 2 | 0 | 13 |
| `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.cs` | 12 | 0 | 0 | 12 |
| `src/SqlSource.Tool/Describing/Postgres/BatchRewrites.cs` | 0 | 12 | 0 | 12 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | 9 | 3 | 0 | 12 |
| `src/SqlSource.Tool/Bundles/Renaming/NameMapping.cs` | 9 | 2 | 0 | 11 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerConnection.cs` | 4 | 7 | 0 | 11 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Proofs.cs` | 8 | 2 | 0 | 10 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/CastTargets.cs` | 10 | 0 | 0 | 10 |
| `src/SqlSource.Tool/Projects/RunProjects.cs` | 2 | 8 | 0 | 10 |
| `src/SqlSource.Tool/Reporting/Reporter.cs` | 10 | 0 | 0 | 10 |
| `src/SqlSource.Tool/Watching/SystemFolderWatcher.cs` | 9 | 1 | 0 | 10 |
| `src/SqlSource.Tool/Bundles/BundleJson.cs` | 8 | 1 | 0 | 9 |
| `src/SqlSource.Tool/Bundles/Renaming/EntryRenamer.cs` | 6 | 3 | 0 | 9 |
| `src/SqlSource.Tool/Bundles/Renaming/NameClass.cs` | 10 | 0 | 1 | 9 |
| `src/SqlSource.Tool/Bundles/Renaming/RenamingProof.cs` | 0 | 9 | 0 | 9 |
| `src/SqlSource.Tool/Describing/DescribeErrors.cs` | 3 | 6 | 0 | 9 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` | 5 | 4 | 0 | 9 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs` | 9 | 0 | 0 | 9 |
| `src/SqlSource.Tool/Describing/Postgres/Types/PostgresTypes.cs` | 7 | 2 | 0 | 9 |
| `src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs` | 0 | 9 | 0 | 9 |
| `src/SqlSource.Tool/Logging/RunLogFile.cs` | 9 | 0 | 0 | 9 |
| `src/SqlSource.Tool/Planning/AttributeReader.cs` | 9 | 0 | 0 | 9 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresDerivations.cs` | 8 | 1 | 1 | 8 |
| `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.QueryFile.cs` | 7 | 1 | 0 | 8 |
| `src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs` | 8 | 0 | 0 | 8 |
| `src/SqlSource.Tool/DotEnv/DotEnvParser.cs` | 11 | 0 | 3 | 8 |
| `src/SqlSource.Tool/Bundles/EntryDifferences.cs` | 5 | 2 | 0 | 7 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/TsqlTokens.cs` | 7 | 0 | 0 | 7 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.Builders.cs` | 4 | 3 | 0 | 7 |
| `src/SqlSource.Tool/Projects/ProjectEvaluator.cs` | 4 | 4 | 1 | 7 |
| `src/SqlSource.Tool/Reporting/UnexpectedFailure.cs` | 0 | 7 | 0 | 7 |
| `src/SqlSource.Tool/Watching/PollingFolderWatcher.cs` | 5 | 2 | 0 | 7 |
| `src/SqlSource.Tool/Bundles/DiagnoseBundles.cs` | 1 | 5 | 0 | 6 |
| `src/SqlSource.Tool/Bundles/Renaming/RenamedTextBuilder.cs` | 6 | 0 | 0 | 6 |
| `src/SqlSource.Tool/Describing/Postgres/Statements/UncastParameters.cs` | 6 | 0 | 0 | 6 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/PlanInstances.cs` | 6 | 0 | 0 | 6 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/PlanPartitions.cs` | 7 | 0 | 1 | 6 |
| `src/SqlSource.Tool/Planning/RunPlanner.cs` | 5 | 1 | 0 | 6 |
| `src/SqlSource.Tool/Bundles/Renaming/CaptureRenamer.cs` | 4 | 1 | 0 | 5 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs` | 5 | 0 | 0 | 5 |
| `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs` | 4 | 1 | 0 | 5 |
| `src/SqlSource.Tool/Describing/Describers/DescriberFaultException.cs` | 0 | 5 | 0 | 5 |
| `src/SqlSource.Tool/Describing/FileOutcomes.cs` | 2 | 3 | 0 | 5 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSearchPath.cs` | 5 | 0 | 0 | 5 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs` | 4 | 1 | 0 | 5 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectItem.cs` | 5 | 0 | 0 | 5 |
| `src/SqlSource.Tool/Bundles/BundleReader.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs` | 2 | 2 | 0 | 4 |
| `src/SqlSource.Tool/Describing/JoinedRedaction.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource.Tool/Describing/Redaction.cs` | 3 | 1 | 0 | 4 |
| `src/SqlSource.Tool/Planning/RunSettingsFiles.cs` | 4 | 0 | 0 | 4 |
| `src/SqlSource.Tool/Bundles/ExpectedFile.cs` | 1 | 2 | 0 | 3 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresOwnStrings.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresRenaming.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Bundles/Renaming/RenamedText.cs` | 2 | 1 | 0 | 3 |
| `src/SqlSource.Tool/Describing/Capture/AnswerNulls.cs` | 3 | 1 | 1 | 3 |
| `src/SqlSource.Tool/Describing/ConnectionSecret.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Describing/DatabaseConnection.cs` | 0 | 3 | 0 | 3 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs` | 3 | 2 | 2 | 3 |
| `src/SqlSource.Tool/Describing/Postgres/Statements/StatementShape.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Describing/Postgres/Types/PostgresTypeNames.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs` | 3 | 1 | 1 | 3 |
| `src/SqlSource.Tool/Describing/SqlServer/Statements/ParameterCopies.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Projects/ManifestReader.cs` | 3 | 0 | 0 | 3 |
| `src/SqlSource.Tool/Bundles/BundleWriter.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.Register.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource.Tool/Bundles/ReplayLines.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource.Tool/Describing/ConnectionArgument.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Describing/Describers/DescriberSession.cs` | 0 | 2 | 0 | 2 |
| `src/SqlSource.Tool/Describing/EntrySecretCheck.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Describing/SqlServer/Columns/SqlServerTableMatcher.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs` | 1 | 1 | 0 | 2 |
| `src/SqlSource.Tool/DotEnv/DotEnvSearch.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Planning/RunFilters.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Processes/ProcessResult.cs` | 2 | 0 | 0 | 2 |
| `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresWords.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Bundles/Renaming/RenamingKey.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerFields.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerMessages.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Bundles/ReplayOutcome.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Capture/JsonFile.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/ConnectionSources.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/EntryBuilder.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Columns/ColumnOrigin.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Columns/PostgresColumns.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Columns/PostgresTableMatcher.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/NpgsqlRunLogger.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/PasswordFile.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/PlannerSettings.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresIdentifiers.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Types/TypeModifiers.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Types/TypeSpellings.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/CatalogOverloads.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/OutputReference.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SidecarOnDisk.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/ColumnProofs.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/Proof.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs/TsqlToken.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SqlServer/SqlServerEvents.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/DotEnv/DotEnvContents.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/DotEnv/DotEnvFile.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Logging/SessionRunLog.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Planning/FileText.cs` | 1 | 0 | 0 | 1 |
| `src/SqlSource.Tool/Processes/ProcessRunner.cs` | 2 | 0 | 1 | 1 |
| `src/SqlSource.Tool/Projects/MSBuildProperty.cs` | 2 | 0 | 1 | 1 |
| `src/SqlSource.Tool/Projects/SolutionReader.cs` | 0 | 1 | 0 | 1 |
| `src/SqlSource.Tool/Reporting/NearName.cs` | 7 | 0 | 6 | 1 |
| `src/SqlSource.Tool/Reporting/OneLine.cs` | 2 | 0 | 1 | 1 |

