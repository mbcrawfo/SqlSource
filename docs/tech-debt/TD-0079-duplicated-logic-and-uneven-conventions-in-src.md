# TD-0079 - Duplicated logic and uneven conventions in src

## Problem

The maintainability review of 2026-10-27 found these in `src/`.  Each is small alone; together they are rules written in several places, which a change must find every copy of.  Paths are under `src/`.

1. **A type's usable files and their queries' settings are worked out in three places, and the rule lives in the emitter.**  [`TypeEmitter.FindUsableFiles`](../../src/SqlSource/Generation/Emitting/TypeEmitter.cs) is called by [`MethodPlanner`](../../src/SqlSource/Generation/Methods/MethodPlanner.cs) and [`TypeModels`](../../src/SqlSource/Generation/Models/TypeModels.cs), so two planners depend on an emitter, and the loop "each usable file, each query, `QuerySettings.Resolve`" is written in `TypeEmitter.SelectMembers`, `MethodPlanner.Resolve` and `TypeModels.Of`.  The check of models matched by index and name is in `TypeModels.Of` and `MethodPlanner.FindModels` both.
2. ~~**Binary searches are written out by hand ten times.**  Beside [`SortedSearch`](../../src/SqlSource/SortedSearch.cs): `SortedKeyedList`, `TypeOverrideMap.LowerBound` (TD-0063), [`ColumnTypeMap`](../../src/SqlSource/Settings/ColumnTypes/ColumnTypeMap.cs), `SqlBlockText.FindRun`, `MethodPlanner.FindModels` and `FindAssignment`, `SqlPath.Seek`, [`ConverterSet`](../../src/SqlSource/Generation/Converters/ConverterSet.cs) and [`ModelAssignment`](../../src/SqlSource/Generation/Models/ModelAssignment.cs).  `ColumnTypeMap` also copies `TypeOverrideMap`'s `Order` and `ComparePart`, though its key is an ordinary struct that `SortedSearch` could take.~~  Fixed: `SortedSearch` offers a search by a probe, `IndexOf`, and a lower bound, `LowerBound`, and every search of a sorted list in `src/` goes through it but `TypeOverrideMap.LowerBound`, whose key is a `ref struct` (TD-0063).  The two maps share their order, `SortedKeyedList.Order`, and the comparison of a part, `TypeKeyText.ComparePart`.
3. **The two describers mark their own errors differently.**  PostgreSQL throws `new NpgsqlException(text)` in [`PostgresConnection`](../../src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs) and knows its own by matching the message against `PostgresConnection.OwnTexts`, a list kept by hand with no test that every thrown text is in it; SQL Server throws `SqlServerConnection.UnusableException`, which needs no list.
4. **Four engine-neutral connection texts are written in both describers**: `SessionRefused`, `ConnectTimedOut`, `NoAnswerInTime` and `LostConnection`, in `Describing/Postgres/ConnectionErrors.cs` (`LostConnection` in `PostgresConnection.cs`) and `Describing/SqlServer/ConnectionErrors.cs`.  Each builds the "unreachable" text with the socket's error code, and only SQL Server's drops a meaningless `: Success`.
5. **Parameter names are compared, ordinal ignoring case, inline at about 25 sites** with no named comparer, where database names have `DatabaseName.Comparer` and paths `SqlPath.Comparer`: `SqlParameterList`, `SqlMarkerScope`, `QueryTyper`, the tool's `EntryBuilder`, `NpgsqlCheck`, `ParameterPlaceholders`, `SqlServerErrors`, `SqlServerSession`, `SqlServerSession.Builders` and ten in `ParameterCopies`.
6. **In nine key switches of the sidecar reader, `default:` stands for the last key of the array.**  In [`SidecarReader`](../../src/SqlSource/Snapshot/Reading/SidecarReader.cs) (six) and [`SidecarTypeReader`](../../src/SqlSource/Snapshot/Reading/SidecarTypeReader.cs) (three), a key added after it without a `case` of its own is read as that key, and nothing catches it.  `SidecarTypeReader.ReadOther` writes every key as a `case`.  `SidecarTypeReader` also relies on `BindName` being the last key so that its bits line up for `Require`, which only a comment holds.
7. **SQL Server's naming rules are copied between the generator and the tool**, against `src/SqlSource.Tool/AGENTS.md`'s rule that the tool shares the generator's code: `SqlServerBinding.Part` brackets a name only where needed and the tool's `SqlServerLive.Bracketed` always does, and the CLR system types `hierarchyid`, `geography` and `geometry` are listed in `SqlServerBinding`, `SqlServerTypeMap` and the tool's `SqlServerTypes`.
8. **The parameter binders have two shapes.**  PostgreSQL's is a subclass in a file of its own; SQL Server's, `SqlServerParameterBinder`, sits at the end of [`ParameterBinder.cs`](../../src/SqlSource/Generation/Emitting/ParameterBinder.cs) and forwards to the static `SqlServerBinding.Append`.  `PostgresParameterBinder.AppendValue` copies `ParameterBinder.AppendConverted` but for a cast and the text for null, and the same seven-parameter `Append` signature is written five times.
9. **`PostgresTypeMap.AsWritten` is a helper of C# type text for both engines that lives in PostgreSQL's map**, called from `ConversionPlanner` (11 times), `ConverterSet`, `ModelCollector` and `QueryTyper`, and named in three messages' comments of `SqlDiagnostics`.
10. **A file's engine is found three ways**: `MethodPlanner.EngineOf` gives a `CodeGenEngines`, `SqlDescribableDialects.EngineOf` a `DatabaseEngine`, and `QueryTyper` decides by the type's class.  `MethodPlanner.Plan` is given the one and works out the other for the same file, and the driver check after it is a cascade of booleans.
11. **The method planner passes the same per-query values in long lists**: `MethodPlanner.Plan` takes nine, `TryReadInput` and `TryReadOutput` eight each, and `ConversionPlanner.Plan` eight, from which it builds `Decision` three times, while the caller's `Reading` already holds the engine, the place and the problems.
12. **[`MappedTypeResolver`](../../src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs) is about 880 lines with two jobs**: resolving a name, in a `ResolveType` of about 160 lines, and checking generic constraints (`Construct`, `MeetsConstraints`, `MeetsFlags`, `Converts`, `Substitute` and the rest, about 250 lines).
13. **[`QueryMethodWriter`](../../src/SqlSource/Generation/Emitting/QueryMethodWriter.cs) is about 960 lines** that write the signature and its documentation, the SQL, the command, the run, the reading of rows (`AppendVectorChecks` to `AppendGetFieldValue`), the naming of locals (`Locals`), and the split of a streaming method into its checks and a static local iterator (`AppendBody`, `AppendArguments` and `AppendCallArguments`).
14. **A dead member**: `SelectStatement.CteNames` (`src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectStatement.cs`) is read only by `SelectStatementTests`; the same list goes to `SelectBody.Read` on its own.
15. **Mutable statics**: five `internal static readonly Dictionary` fields of `Settings/TypeOverrides/TypeKeyNames.cs`, which only `TypeKeys` reads and nothing writes.
16. **The two describers lay out their folders differently**: PostgreSQL's table matcher, nullability, declared types and placeholders are in `Postgres/Columns/`, `Postgres/Statements/` and `Postgres/Types/`, where SQL Server's equivalents sit at `SqlServer/`'s root.

## Why it exists

Each grew where the feature that needed it was written, one phase at a time, and a second engine was added beside the first rather than over a shared base.  The review records them and does not refactor, so that its pull request stays a review.

## Impact

Low each, Medium together: a change to a rule that is written twice is made once and passes its tests, and the copies drift, as items 4 and 5 already have (the `: Success` text, `OrdinalIgnoreCase` written out one way here and another there).

## Proposed fix

One item at a time, each in its own pull request, with no change to what is generated:

1. Move `FindUsableFiles` and a `UsableQueries.Of(TypeQueries)` that yields each query with its file, indices and settings into `Generation/`, and call it from the three places.
2. ~~Make `SortedSearch` offer a probe-based search and a lower bound, route every search but `TypeOverrideMap`'s through it, and share `Order` and `ComparePart` between the two maps.~~  Done.
3. Give PostgreSQL an `UnusableException` caught in `PostgresLive.AnswerAsync`, delete `OwnTexts` and `IsOwn`, and keep its texts in `Postgres/ConnectionErrors`.
4. A `Describers/ConnectionTexts` with the four texts and one `Unreachable(prefix, SocketException)`.
5. `SqlParameterName.Comparer` and `Comparison`, and one `IndexOf` over a query's parameters, used at every site.
6. A `case` for every key and a `default` that does nothing, as `ReadOther` has, or a test that each key array has a `case` for each key.
7. One `SqlServerNames` in the generator, with the bracketing rule and the CLR system types, which the tool uses.
8. Move `SqlServerBinding`'s body into `SqlServerParameterBinder.cs`, let `AppendConverted` take the cast and the text for null, and bundle the builder, indent, command, parameters and names into one record.
9. Move `AsWritten` to `Settings/ClrTypes/ClrTypeName` or a helper of its own.
10. One `EngineOf`, `CodeGenEngines.Of(DatabaseEngine)` and `MethodDrivers.Allows(engine)`.
11. A record of a query's context, and `Decision` or `Reading` passed to `ConversionPlanner` whole.
12. Extract `GenericConstraints.cs` from `MappedTypeResolver`, and split `ResolveType`.
13. Move the reading of rows to a `RowReaderWriter.cs`, and `Locals` to a file of its own.
14. Drop `SelectStatement.CteNames` and test through `Body`.
15. Expose the dictionaries as `IReadOnlyDictionary`.
16. Decide whether the two describers' folders mirror each other, and move the other's files to match.

## Trigger

A change to any of the rules above: make it once, in the shared place, instead of in each copy.  Otherwise the next maintainability review.
