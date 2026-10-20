# TD-0063 - Duplicated and uneven patterns in `src/`

## Problem

A maintainability review of `src/` on 2026-10-19 found these.  No behaviour is wrong today; each is a rule written in two places, or a list kept in step by hand, that a change has to reach everywhere.

1. **The tool copies the generator's order of `SQLSRC011` and `SQLSRC014`.**  The private `InvalidValues` of [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) de-duplicates and sorts the invalid dialects and settings "as the generator reports them", which `FindInvalidDialects` and `FindInvalidSettings` of [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs) also do.  `src/SqlSource.Tool/AGENTS.md` says `RunPlanner` copies no rule, and `GeneratorParityTests` compares only claims and `SQLSRC209`/`SQLSRC210`, so nothing pins that the two orders agree.
2. **The tool copies the sidecar's file name.**  `SidecarSuffix` and `IsSidecarName` of [`SidecarStore`](../../src/SqlSource.Tool/Describing/SidecarStore.cs) are `SqlPath.IsSidecarFile` of [`SqlPath`](../../src/SqlSource/Generation/Paths/SqlPath.cs) again; [`SidecarFormat`](../../src/SqlSource/Snapshot/SidecarFormat.cs) spells the `.json` that `SqlPath.JsonExtension` holds, and [`RunLogFile`](../../src/SqlSource.Tool/Logging/RunLogFile.cs) spells both extensions once more.
3. **The database setting is resolved by hand.**  `RunPlanner` resolves a query's database as markers, then metadata, then property, then the dialect's name, outside `QuerySettings.Resolve`, which `src/SqlSource/AGENTS.md` calls the one rule that puts the levels together.
4. **A database name has no comparer of its own.**  `StringComparer.OrdinalIgnoreCase` is written out for database names at sixteen sites: `RunPlanner`, `RunFilters`, `DescribeCommand`, `Connections`, `RunDecisions`, `SidecarCheck`, `DatabaseRuns`, `RunSummary`, [`SidecarEntry`](../../src/SqlSource/Snapshot/SidecarEntry.cs) and [`SidecarComparer`](../../src/SqlSource/Snapshot/SidecarComparer.cs).  Paths have `SqlPath.Comparer`.  Project paths are compared ignoring case in [`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs) and ordinally in `DescribeCommand`.
5. **The `--database` list and the filter flag are built twice.**  [`DescribeCommand`](../../src/SqlSource.Tool/DescribeCommand.cs) builds `DescribeOptions.Databases` and `HasFilter` from the options, and [`RunFilters`](../../src/SqlSource.Tool/Planning/RunFilters.cs) builds the same list and most of the flag.  `DescribeCommand` also reads each project's listed files again after `RunPlanner` did, to count the unclaimed ones.
6. **The marker lists are kept in step by hand.**  `SqlMarkerKind`, `SqlMarkerReader.Keywords`, `IsAllowedInQuery` and `IsAllowedInPreamble`, and the `if` chain of `SqlMarkerScope.Read`, all in [`Parsing/Markers/`](../../src/SqlSource/Parsing/Markers), list the markers.  The chain's last `else` hands any other kind to `ReadModelSetting`, which has no `else` of its own, so a kind allowed and given no branch is accepted and ignored.  No test enumerates `SqlMarkerKind`.
7. **`SqlSourceGenerator.Initialize` is about 450 lines.**  It wires every step and output in one method; four `RegisterSourceOutput` blocks that report an array of diagnostics are the same, and `.Where(x is not null).Select(x!)` is written five times.
8. **Small duplicates in the generator.**
   - `ModelProperty.IsSameShape` ([`ModelProperty`](../../src/SqlSource/Generation/Models/ModelProperty.cs)) has no caller; `ModelCollector.FindDifference` writes its rule out.
   - The switch from `ModelKind` to its C# keyword is in [`ModelEmitter`](../../src/SqlSource/Generation/Emitting/ModelEmitter.cs) and in `ModelCollector.KindOf` ([`ModelCollector`](../../src/SqlSource/Generation/Models/ModelCollector.cs)).
   - The subject of a message, `column 'id'` or `parameter 'id'`, has its constants and a `Subject` helper in both [`QueryTyper`](../../src/SqlSource/Generation/Types/QueryTyper.cs) and [`ModelReader`](../../src/SqlSource/Generation/Models/ModelReader.cs).
   - `TypeModels.ClaimingTypeOf` ([`TypeModels`](../../src/SqlSource/Generation/Models/TypeModels.cs)) builds the name that `HintName.Create` ([`HintName`](../../src/SqlSource/Generation/Emitting/HintName.cs)) builds, without `.g.cs`; only a comment ties them.
   - `SqlPath.IndexOf`, `TypeOverrideMap.Search` ([`TypeOverrideMap`](../../src/SqlSource/Settings/TypeOverrideMap.cs)) and `MappedTypeResolver.Find` ([`MappedTypeResolver`](../../src/SqlSource/Generation/Types/MappedTypeResolver.cs)) are one binary search written three times.
   - The two SQL line terminators are named in `SqlText`, `SqlLexer`, `SqlTextBuilder`, `SqlMarkerReader` and `QuoteOperatorReader`.
9. **Uneven events of the log.**  `RunEvents.Sidecar` ([`RunEvents`](../../src/SqlSource.Tool/Logging/RunEvents.cs)) takes its action as a free string, given as literals in `SidecarStore` and `DescribeRun`, where its siblings map an enum inside `RunEvents`.  The `postgres.types`, `postgres.spellings`, `postgres.tables` and `postgres.deallocate` events of [`PostgresEvents`](../../src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs) carry no `query`, which every other `postgres.*` event of a query does.
10. **Repeated blocks in the tool.**  [`DatabaseRuns`](../../src/SqlSource.Tool/Describing/DatabaseRuns.cs) reports a failure, redacted, with its event, in two blocks that are the same; `RowsOfAsync`, `TablesOfAsync` and `SpellingsOfAsync` of [`PostgresSession`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs) repeat one shape of asking and answering.

## Why it exists

Each sub-phase added its code beside the last's and copied the nearest rule; the copies in the tool predate the rule that a rule both need goes into the generator's project.

## Impact

Low.  1, 2 and 6 can let the tool and the generator, or the marker lists, disagree silently after a change; the rest are more places for one change to reach.

## Proposed fix

1. One generator helper, on `InvalidSetting` or beside it, that de-duplicates and orders the invalid values, called by both.
2. `SidecarStore` calls `SqlPath.IsSidecarFile`; `SidecarFormat.PathFor` and `SqlPath` share one constant, and `RunLogFile` uses them.
3. A resolver of the database beside `QuerySettings.Resolve`, with the attribute's level empty.
4. A `DatabaseName.Comparer`, beside `SettingValue.IsDatabaseName`, and one comparer of project paths, used at every site.
5. Keep `RunFilters` on the plan and derive `DescribeOptions.Databases` and `HasFilter` from it and `--project`; have `RunPlanner` return the unclaimed count.
6. Make `SqlMarkerScope.Read` a `switch` whose default throws, and add a theory over every `SqlMarkerKind` that checks it has a keyword and a scope.
7. Split `Initialize` into one method per stage, with a `ReportAll` helper and a `WhereNotNull` extension; no step name changes.
8. Delete or use `IsSameShape`; one `ModelKinds.Keyword`; one class for the subject of a message; one builder of the claiming type's name; one allocation-free sorted search; one `SqlText.IsLineTerminator`.
9. Pass `FileAction` to `RunEvents.Sidecar`; give the four events their `query`.
10. A `ReportFailure` of `DatabaseRuns` and one generic answer helper of `PostgresSession`.

## Trigger

Fix 1, 2 and 6 the next time either side of them changes; fix 7 before the next step is added to the pipeline; the rest when the code around them is next changed.
