# TD-0114: the mutants of eleven files that need no test

The eleven files of TD-0114 were measured on 2026-11-23 with the tests of `SqlSource.Tests.Generation` and then the driver tests of `SqlSource.Tests.Generator`.  Tests were added for the mutants that change what a user or a later step sees.  The 98 below are what remains, each read and given a class, so that a later review need not read them again.  Their lines are those of the pull request that closed the eleven files, and drift as the code changes.

- **equivalent**: no input can tell it from the original.
- **unreachable**: it sits where no input a user can write arrives, or changes a result only for such an input.
- **cost**: it changes how much work is done, never the result.

## `Generation/Converters/ConverterReader.cs`

- 76:9, **cost**, Statement mutation: a cancellation check
- 129:16, **equivalent**, Conditional (true) mutation: WithNullableAnnotation(NotAnnotated) on a type that is not Annotated changes nothing this method reads: TypeFormat writes ? only for an Annotated type, and IsValueType is the same
- 157:13, **equivalent**, OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation: only a top-level type can be file-local (CS9054), so at most one type of a chain is, and `^=` of one true is `|=`
- 159:13, **equivalent**, OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation: an extension block is declared directly in a non-generic top-level static class and holds no type, so at most one type of a chain is TypeKind.Extension, and `^=` of one true is `|=`

## `Generation/Exceptions/ExceptionProbe.cs`

- 43:9, **cost**, Statement mutation: a cancellation check
- 119:9, **cost**, Block removal mutation: for an empty array RemoveAll returns the same empty array, so the early return only skips the call

## `Generation/Methods/MethodPlanner.cs`

- 66:56, **cost**, Equality mutation: Collect over a type with no file plans nothing: UsableQueries.Of has no query, and InterfaceNameOf needs input.Repository, which MethodsInput.Gather gives only a type that claims a repository query, so the mutant only runs an empty pass
- 211:9, **cost**, Block removal mutation: an empty `used` falls through to Any over it, false, and to new EquatableArray(used) of an empty array, which EquatableArray.Equals (items compared) makes equal to EquatableArray.Empty; only an allocation is skipped
- 331:13, **unreachable**, Logical mutation: resolution is never null here: MethodsInput.Gather gives a type that claims a repository query its RepositoryShape (RepositoryTypes.ClaimsRepositoryQuery is also the rule of the RepositoryShapes step) and Collect then resolves; for a non-null resolution a failed database is never in Sources (ConnectionResolver.Resolving.Take adds a source only after IsUsable, else Fail), so FirstOrDefault gives null as the check does
- 334:9, **unreachable**, Block removal mutation: same as 331:13: resolution is never null, and a failed database is not in Sources, so falling through to FirstOrDefault gives the same null
- 338:16, **unreachable**, Linq method mutation (FirstOrDefault() to First()): every needed database ends in ConnectionResolver as a source or as failed (Resolve takes or fails each, and the failed ones return null at 331 first), and ConnectionOf asks only for a database of NeededDatabases, by the same DatabaseOf, so First never finds none
- 397:20, **cost**, Conditional (false) mutation: a plan with no problem and no method adds nothing in AddPlanned (no diagnostic, no method, IsDriverUsable false skips the rest); the null only saves allocating it
- 399:92, **equivalent**, Boolean mutation: IsDriverUsable is read only by AddPlanned, to add the plan's methods and warning ids, and this plan has no method
- 485:78, **equivalent**, Boolean mutation: IsDriverUsable is read only by AddPlanned, to add the plan's methods and warning ids, and this plan has no method
- 496:13, **equivalent**, Conditional (true) mutation: read, not shown by a test: KeptSegments is built by SqlFileParser.ResolveSettings only when the query's markers over its header, else the chain (FileParseInput.CommentsWanted = chain.Level.KeepsComments), turn keep-comments on, which is the resolution QuerySettings.Resolve(markers, file level).KeepComments makes from the same levels, so it is null whenever KeepComments is false
- 601:9, **unreachable**, Statement mutation: Plan returns before FirstMethodForm when forms.Count == 0, and a form without a query method is only the synchronous form of a stream, which FormsOf adds beside the asynchronous form that has one (under sync it returns no form at all)
- 601:45, **unreachable**, String mutation: same as 601:9: the throw is never reached, so its text is never seen
- 620:21, **equivalent**, Conditional (false) mutation: the first form with a query method is FirstMethodForm's, and method was built from it (name, synchrony, source), so the copy `method with {...}` equals method
- 872:13, **cost**, Statement mutation: with no properties the loop runs over a default EquatableArray, whose Count is 0 (Items maps default to Empty), so it adds nothing
- 1206:9, **cost**, Block removal mutation: a method on an extension class has the connection as its receiver (form.Source is null), so MethodArguments always has a first argument and the Finalize check adds nothing; the early return only skips GetTokens
- 1217:65, **equivalent**, Boolean mutation: MethodArguments reads streams only to pick the receiver's documentation text (MethodArguments.cs, the `_streams` use), never whether an argument exists, and the check asks only MoveNext
- 1331:25, **equivalent**, Conditional (true) mutation: for last == 0 DisplayNameOf(type, -1) runs no iteration and returns type.NamespaceValue, which the other arm gives too; for last > 0 it is the original arm
- 1331:25, **equivalent**, Equality mutation: for last == 0 DisplayNameOf(type, -1) runs no iteration and returns type.NamespaceValue, which the other arm gives too; last is never negative
- 1350:9, **equivalent**, Equality mutation: the text is "" or "<" names ">", never of length 2, and for "<>" Split gives one empty name, which no identifier equals
- 1425:17, **unreachable**, Equality mutation: a plan with IsDriverUsable true is the last return of Plan, whose Methods hold at least the first form's query method (FirstMethodForm); every plan without a method has IsDriverUsable false and is skipped before this
- 1484:33, **unreachable**, String mutation: ShapeName is called only for One, OneOptional and Many (the guard at line 416)
- 1485:37, **unreachable**, String mutation: ShapeName is called only for One, OneOptional and Many (the guard at line 416)
- 1542:17, **cost**, LogicalNotExpression to un-LogicalNotExpression mutation: ConversionPlanner checks Serves(target) on every converter it reads (lines 393, 413, 485), so an unfiltered slice gives the same result; the filter only shortens its scans
- 1543:13, **cost**, Block removal mutation: without the early return a slice with no library edge goes through Serving, where every converter serves every target and the slice itself is returned
- 1547:17, **equivalent**, Equality mutation: the two slots stay one per target: a project's Postgres target is one value (MethodDrivers.PostgresAtLeast10), so with != the Postgres slot is _sqlServer and SQL Server's is _npgsql10, still apart
- 1552:20, **equivalent**, Conditional (false) mutation: a project has one Postgres target (MethodDrivers.PostgresAtLeast10), so only one of the two Npgsql slots is ever used, whichever it is
- 1552:20, **equivalent**, Conditional (true) mutation: a project has one Postgres target (MethodDrivers.PostgresAtLeast10), so only one of the two Npgsql slots is ever used, whichever it is
- 1552:20, **equivalent**, Equality mutation: a project has one Postgres target (MethodDrivers.PostgresAtLeast10), so only one of the two Npgsql slots is ever used, whichever it is
- 1562:21, **cost**, Equality mutation: HasLibraryEdge only decides whether to filter, and ConversionPlanner filters by Serves(target) again, so either answer gives the same conversions
- 1564:28, **cost**, Boolean mutation: HasLibraryEdge only decides whether to filter, and ConversionPlanner filters by Serves(target) again, so either answer gives the same conversions
- 1568:20, **cost**, Boolean mutation: HasLibraryEdge only decides whether to filter; with no library edge Serving returns the slice itself as every converter serves every target
- 1577:20, **cost**, Conditional (false) mutation: Serving returns an equal array by value (EquatableArray compares items) when it makes a new one of the whole slice; only an allocation is spent
- 1577:20, **cost**, Conditional (true) mutation: Serving returns the whole slice, which ConversionPlanner filters by Serves(target) again, so the result is the same

## `Generation/Models/ModelCollector.cs`

- 316:17, **cost**, Statement mutation: early exit of FindProblems' scan: property names are distinct ignoring case (ModelReader.IsUsable's Seen), so at most one equals the model's name and the rest of the loop adds nothing
- 322:64, **equivalent**, UnaryMinusExpression to UnaryPlusExpression mutation: Problem.Property of a NameUnusable problem is never read: Tell (line 331) returns before request.Properties[problem.Property]
- 338:35, **unreachable**, String mutation: the only NameUnusable problem is built at line 320 from `FindUnusable(...) is { } reason`, so Reason is never null
- 351:40, **unreachable**, String mutation: a TypeNotPublic problem needs PropertyType.IsPublic false, which only a type override, column type or marker mapping gives (QueryTyper.MappingOf/Chosen), each with its Place set; TypeMapping.Mapped (base map, library rows) is always public with a null Place
- 412:60, **unreachable**, Equality mutation: period is 0 only for a namespace that starts with a period; a model namespace is a Roslyn namespace value or passes SettingValue.IsQualifiedName, where every part is a usable identifier
- 417:21, **equivalent**, Equality mutation: _namespaces is only read by Contains(fullName) (line 445) and a full name ends in a model name, never empty, so the added "" is never found
- 454:17, **equivalent**, Equality mutation: the condition does not change in the loop; for an empty namespace the mutant runs once with part "" and _fullNames never holds "" (a full name ends in a non-empty model name), then breaks
- 458:28, **unreachable**, Equality mutation: `period <= 0` differs only at period 0, a namespace that starts with a period, which SettingValue.IsQualifiedName and Roslyn namespaces rule out; the focused model tests pass with it and do not hang
- 464:21, **unreachable**, Equality mutation: `period <= 0` differs only at period 0, a namespace that starts with a period, which SettingValue.IsQualifiedName and Roslyn namespaces rule out; the focused model tests pass with it and do not hang
- 477:13, **unreachable**, Equality mutation: an empty model name is no identifier: a marker passes SettingValue.IsQualifiedName, and a derived name is a query or table name plus a suffix that SettingValue.IsSuffix makes non-empty

## `Generation/Models/ModelReader.cs`

- 65:17, **equivalent**, Null coalescing mutation (remove left): FileTypesInput.Gather sets File to null exactly when no entry is usable, so File?.FileName is null on this path and both sides give the empty text
- 65:46, **equivalent**, String mutation: the FileName of a file with no usable entry reaches no step, generated code or diagnostic: ModelSource takes the ParsedSqlFile name at TypeModels.cs:212
- 83:17, **unreachable**, Statement mutation: each usable entry is made by SidecarChecker.CheckQueries from a query of the same parse, in the file's order, so QueryNames.IndexOf from the last match always finds it
- 124:13, **unreachable**, Logical mutation: QueryTyper.TypeQuery gives one PropertyType per parameter and per column and none for a query with a failure, and Columns is null exactly when the entry's is, so the counts always agree
- 125:9, **unreachable**, Block removal mutation: the guard's counts always agree (QueryTyper.TypeQuery types every parameter and column or gives no QueryTypes), so the return is never reached
- 146:30, **equivalent**, Conditional (false) mutation: QueryModels.ParameterTypes is read only at MethodPlanner.cs:1082, inside the loop over a non-null Input, so a query without parameters, whose Input is null, never has it read
- 258:17, **cost**, Statement mutation: a property of an unusable parameter name is built and then dropped: IsUsable added an error, so ReadQuery returns null for the query and its properties are discarded
- 309:17, **cost**, Statement mutation: a property of an unusable column name is built and then dropped: IsUsable added an error, so ReadQuery returns null for the query and its properties are discarded
- 341:12, **unreachable**, Equality mutation: `IndexOf("[,") == 0` needs a type text that starts with `[`, and ClrTypeName's grammar and every map's text start with a keyword, `global::` or an identifier
- 369:20, **cost**, Boolean mutation: the property of a name that is no identifier is kept instead of skipped, but its error makes ReadQuery discard every property of the query
- 384:20, **cost**, Boolean mutation: the property of a repeated name is kept instead of skipped, but its error makes ReadQuery discard every property of the query
- 400:20, **cost**, Boolean mutation: the property of a reserved name is kept instead of skipped, but its error makes ReadQuery discard every property of the query
- 471:65, **unreachable**, String mutation: a parameter without a type gets TypeHasNoCSharpType from QueryTyper.TypeOf and its query no types, so ReadParameters never sees one

## `Generation/Models/ModelSummary.cs`

- 68:73, **unreachable**, String mutation: a request has a Table only from TypeModels.Request, which sets it only when PropertyNames.FromTable returned a name, and that needs non-null Schema and Table
- 70:72, **unreachable**, String mutation: a request has a Table only from TypeModels.Request, which sets it only when PropertyNames.FromTable returned a name, and that needs non-null Schema and Table

## `Generation/Models/TypeModels.cs`

- 129:22, **unreachable**, Conditional (false) mutation: every file of TypeQueries.Files comes from parsedFiles and has one FileModels in allFileModels, from EachFile or OfRepeatedPaths (SqlSourceGenerator.CollectModels, SelectFiles, FilesOfRepeatedPaths), so the search always finds it
- 151:55, **equivalent**, UnaryMinusExpression to UnaryPlusExpression mutation: period is read only under `marker is not null && period >= 0` and in the branch that guard enters; for a null marker its value is never read
- 152:35, **unreachable**, Equality mutation: `period >= 0` differs from `> 0` only for a marker name that starts with a period, which SettingValue.IsQualifiedName refuses (every part a usable identifier)

## `Generation/Types/Drivers/LibraryProbe.cs`

- 240:40, **equivalent**, Equality mutation: `>=` differs only when the two identities have the same version, and the assigned Version is then equal in value

## `Generation/Types/MappedTypes/MappedTypeResolver.cs`

- 88:13, **cost**, Statement mutation: a cancellation check: only a cancelled token changes, never a record; the plan files it as cost
- 181:17, **equivalent**, Conditional (false) mutation: the Symbol of a keyword used Whole is read by no one: Resolve reads only Named, Written and the flags of a Whole name (lines 90-117)
- 476:28, **equivalent**, Equality mutation: a metadata name has a dot only after a namespace name, never first: ShortNameIndex.MetadataNameOf builds Ns.Type or the bare name of a global type
- 545:20, **unreachable**, Boolean mutation: the type is one GetTypesByMetadataName gives (FindNamed, LookUp), always a definition, and IsUnboundGenericType is true only of a typeof(C<>) symbol from syntax
- 596:29, **equivalent**, Equality mutation: at index == longest the body sets longest = index and breaks: the value is unchanged
- 596:29, **unreachable**, Equality mutation: with index > longest the loop never narrows, so FindNamed also tries a namespace holding a "`n" part; only a reference with a namespace literally named with a backtick could match, which C# cannot declare
- 601:17, **equivalent**, Statement mutation: after longest = index, the loop increments index past longest and its condition ends it, so the break changes nothing
- 703:50, **equivalent**, Equality mutation: for current == next both arms of the conditional give the same status

## `Generation/Types/QueryTyper.cs`

- 74:17, **unreachable**, Statement mutation: `at < 0` needs a usable entry whose name is no query of the file; SidecarChecker.CheckQueries builds UsableEntries from the file's own queries, in file order, each found by name (SidecarChecker.cs:171-215), and QueryNames.IndexOf compares the same names ordinally
- 94:21, **cost**, Conditional (false) mutation: `types with { UnavailableLibraries = <empty> }` equals `types` (EquatableArray equality and every read go through Items, where default is Empty); only a record copy is avoided
- 237:9, **cost**, Equality mutation: ColumnTypeMap.Find over an empty map returns null (LowerBound(0) and no loop), so `>= 0` only skips the call
- 352:53, **equivalent**, Boolean mutation: IsValueType of a TypeMapping that is not Mapped is read nowhere: QueryTyper 304, 360, 548 and 592 sit behind Kind == Mapped, and TypeDiagnostics reads Key, Kind, Text and Detail alone
- 352:60, **equivalent**, Boolean mutation: IsPublic of a TypeMapping that is not Mapped is read nowhere: QueryTyper 305, 361 and 549 sit behind Kind == Mapped
- 355:23, **equivalent**, Conditional (false) mutation: Written is read only in WarnOfPackage (QueryTyper.cs:385) after `mapping.Package is not { }`, and Written is null exactly when Package is, so naming it when Package is null reads nowhere
- 385:58, **unreachable**, String mutation: `Written ?? ""` in WarnOfPackage: Written is `name` whenever Package is non-null (MappingOf, QueryTyper.cs:355), and WarnOfPackage returns when Package is null
- 386:35, **unreachable**, String mutation: `Detail ?? ""` in WarnOfPackage: a mapping with a Package comes from MappingOf, whose Detail is `place`, a non-null string
- 452:25, **unreachable**, Null coalescing mutation (remove left): the default branch of TypeOf gets a null type, where `type?.Name` is null and both forms give "null", or an OtherEngineType, which SidecarTypeReader reads only under an engine that is neither postgres nor mssql, and an entry of such an engine is never usable (SQLSRC242, SidecarChecker.cs:187)
- 606:16, **unreachable**, Logical mutation: only a null `type` differs, and HoldsJson gets one only from an array without its element or a domain without its base, which SidecarTypeReader.MissingNestedKey refuses
- 654:13, **cost**, Block removal mutation: returning the cached LevelTypes or making an equal one again gives the same value; only the made lookup object is not shared
- 666:13, **cost**, Conditional (true) mutation: an empty map gives an OverrideLookup whose Find returns null for every key, as a null lookup does
- 666:13, **cost**, Equality mutation: an empty map gives an OverrideLookup whose Find returns null for every key, as a null lookup does
- 671:9, **cost**, Statement mutation: without the cache each query makes its level again, an equal value
- 816:21, **equivalent**, Boolean mutation: IsValueType of an ArrayNamedByItsOwnKey mapping is read nowhere (only Mapped mappings are read, as for line 352)
- 817:21, **equivalent**, Boolean mutation: IsPublic of an ArrayNamedByItsOwnKey mapping is read nowhere (only Mapped mappings are read, as for line 352)
- 836:21, **equivalent**, Equality mutation: key.Length == bare.Length only when the key is the bare name, so the second Find repeats the first, which returned null

## `Generation/Types/TypeDiagnostics.cs`

- 105:35, **unreachable**, String mutation: NeedsNewerSqlClient is made only at SqlServerTypeMap.cs:262 with Detail = VersionOf(...), a non-null string
- 170:35, **unreachable**, String mutation: ArrayNamedByItsOwnKey is made only at QueryTyper.cs:813 with Detail = PlaceOf(...), non-null
- 171:33, **unreachable**, String mutation: ArrayNamedByItsOwnKey is made only at QueryTyper.cs:813 with Text = element key + "[]", non-null
- 211:42, **unreachable**, Conditional (false) mutation: a bare key the map reads as itself is a row of the map, and PostgresTypeMap.Map answers a type of pg_catalog from its row before any path that fails, so this arm is never given such a key
- 269:74, **unreachable**, String mutation: MappedTypeFailure's failure.Text comes from MappingOf (QueryTyper.cs:352), where it is `name`, a non-null string
- 269:97, **unreachable**, String mutation: MappedTypeFailure's failure.Detail comes from MappingOf (QueryTyper.cs:352), where it is `place`, a non-null string
