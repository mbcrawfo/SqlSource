# TD-0038 - Duplicated and uneven patterns in the generator and the tool

## Problem

A maintainability review of `src/` on 2026-10-10 found these.  None changes what a user sees; each makes a change cost more than it should, or lets two places drift apart with nothing to notice.

1. **Two readers of a settings level with the same shape.**  [`TargetTypeReader`](../../src/SqlSource/Generation/TargetTypeReader.cs) (`ReadSettings`, `ReadParameters`, `ReadText`, `ReadChoice`) and [`MSBuildSettings`](../../src/SqlSource/Generation/MSBuild/MSBuildSettings.cs) (`Read`, `ReadParameters`, `ReadText`, `ReadChoice`) set the same eight members of `SettingsLevel` with the same validators and the same null-if-blank, trim, validate, else-record rule.  They differ in the source, an attribute's named argument or an `AnalyzerConfigOptions` key, and in the sink, a `DiagnosticInfo` or an `InvalidSetting`.  `TargetTypeReader.ReadPlacement` hand-rolls what its own `ReadChoice<MemberPlacement>` does.
2. **`SqlSourceDatabase` is read by a path of its own.**  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) declares the name and reads the property and the metadata itself, with the same steps as `MSBuildSettings.ReadText`, while the other settings of the manifest go through `ManifestOptions` and `MSBuildSettings.Read`.  `SettingsLevel.Database` exists, and `SqlMarkerScope` fills it from the marker, but `MSBuildSettings` never sets it: the marker level and the property level of one setting travel by different roads.
3. **The compiler's key prefixes are spelled in three files.**  `build_property.` and `build_metadata.SqlSourceSettingsFile.` are literals in [`DialectSetting`](../../src/SqlSource/Generation/MSBuild/DialectSetting.cs), [`MSBuildSettings`](../../src/SqlSource/Generation/MSBuild/MSBuildSettings.cs) and the tool's [`ManifestOptions`](../../src/SqlSource.Tool/Planning/ManifestOptions.cs).  The tool's fake `AnalyzerConfigOptions` agrees with the generator's readers by string coincidence.
4. **The names of `OutputKind` are maintained by hand in three places.**  [`AttributeReader`](../../src/SqlSource.Tool/Planning/AttributeReader.cs) switches on `nameof` of each member, `RunPlanner.NameOf` maps each to its lower-case text, and `SettingValue.TryReadChoice` already derives the accepted spellings from the enum.
5. **Twin one-of records and five shapes of "a value or why not".**  [`DescribeResult`](../../src/SqlSource.Tool/Describing/Describers/DescribeResult.cs) and [`OpenResult`](../../src/SqlSource.Tool/Describing/Describers/OpenResult.cs) are the same record under two names.  [`FileCheck`](../../src/SqlSource.Tool/Describing/FileCheck.cs) repeats the head of `FileOutcome`, from which it is always built.  Across the tool a result that may fail is a `(Projects, Failure?)` pair, a state enum beside two nullables, a `TryX(out reason)` that returns null, a method that reports and returns null, or a `(Value?, Error?)` record, depending on the file.
6. **`ToolDiagnostic.ForFile` takes the path twice at ten of thirteen call sites.**  [`ToolDiagnostic.ForFile`](../../src/SqlSource.Tool/Reporting/ToolDiagnostic.cs) sets the error's path from its second argument, and most file-scoped descriptors also quote the path as `{0}`, so callers write the same expression as the path and as the first argument.  A caller that passes a different value is right in three places and wrong in the rest, and nothing tells them apart.
7. **Duplicated text helpers in `Parsing/`.**  `SqlMarkerReader.Trim` and `TokenScanner.Trim` are the same method; `SqlLexeme.GetContentSpan` inlines it a third time.  `SqlLexer` and `SqlTextBuilder` each hold `LineTerminators` and `FindLineEnd`; `SqlLexer.CharAt` and `TokenScanner.CharAt` are the same.
8. **Two parsers of the generator-parameter list, with rules that differ.**  [`GeneratorParameterList.Parse`](../../src/SqlSource/Settings/GeneratorParameterList.cs), for a property, metadata or the attribute, and [`SqlGeneratorParameterScope`](../../src/SqlSource/Parsing/SqlGeneratorParameterScope.cs), for `-- generator:`, both split on white space, look words up and resolve `default` against the other words.  `Parse` drops an unknown word and makes `default` beside anything the whole value's fault; the scope reports a conflict for `default` beside a parameter, reports `name=value` as a value that is not valid, and accumulates across several markers.  Nothing says whether the difference is wanted.
9. **Methods that do several things.**  `SqlFileParser.Parser.ReadBlock` (about 115 lines: markers, summary, dialect marker, end of block, stripped SQL, token scan, declared variables, parameters, input model, settings, kept SQL), `Cli.RunAsync` (about 120 lines in a nested `try`), `RunPlanner.ReadProject` and `RunPlanner.PlanFile` (about 100 lines each), `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`.  `SqlParameterList.Create` and `DatabaseRuns.DescribeAllAsync` take eight parameters each, several of which always travel together.
10. **Boolean cascades.**  `RunDecisions.Decide(plan, force, errors, check)` passes `options.Force || options.Check` and then `options.Check` again; `FileOutcomes.Decide(files, runHasFilter, anythingWasReported, read)`; `SqlMarkerScope.Read(marker, inQuery, inPreamble)`, where both true is a third state.
11. **Small leftovers.**  `SqlSourceGenerator.ToSortedFiles` and `ToSortedSettings` are one method in two element types.  `SidecarComparer.Json` re-implements the scalar formatting of `SidecarJsonBuilder`.  `PostgresType.TryGetKind` and `PostgresTypeKind`, and `RunFilters.None`, have no caller outside the tests.  `SettingValue.IsTypeName` and `SqlDialectMarker.Describe` forward to one other method.  `Reporter.Trace` and `RunTrace.Databases` are settable properties that `Cli` must assign in order, and nothing enforces the order.  `AttributeReader.ReadText` and `SqlFileText.GetText` read a file the same way.

## Why it exists

The code was written in sub-phases, each adding the next reader, record or helper beside the last, and reviewed for correctness in each pull request rather than for the shape of the whole.  Items 1, 3 and 4 came from the tool growing a second reader of the same settings; 5 and 6 from each step defining the record it needed; 7 from readers that were written to allocate nothing and copied the one way to do that.

## Impact

Low.  Nothing here is wrong today, and the tests hold each pair together where they could drift (`GeneratorParityTests`, the settings tests of `RunPlanner`).  Each item makes a change touch more places than a reader would expect, and 2, 3, 4 and 6 are the kind that a later phase breaks by adding one case in one of the copies.

## Proposed fix

One pull request per numbered item, or per pair, each held by the existing tests:

1. One reader of a `SettingsLevel`, given a function that finds a raw value by name and an action that records an invalid one; `ReadPlacement` becomes `ReadChoice<MemberPlacement>`.
2. A `Database` line in `MSBuildSettings.Read` and its key in `SettingKeys`; `RunPlanner` reads the level and drops its own reader.  Weigh first that the generator would then see the property, cache it and report an invalid value itself.
3. The two prefixes as constants of `MSBuildSettings`, composed everywhere else.
4. `Enum.TryParse` in `AttributeReader` and a shared lower-case name in `SettingValue`.
5. One `Outcome<T>` record, and one convention for a result that may fail, at least within the tool; `FileCheck` holds its `FileOutcome`.
6. `ForFile` prepends the path to the arguments, and a second method serves the three sites that pass something else.
7. One allocation-free `SqlText` helper in `Parsing/` for the trim, the line end and the character lookup.
8. One word walker, shared, and a written decision on the `default` rule; if the two rules are meant to differ, say so in `Settings/`.
9. Split by the responsibilities named, with a record for the values that travel together.
10. A `RunMode` enum of `Describe`, `Force`, `Check` on `DescribeOptions`; a scope kind of `Preamble`, `Query`, `WholeFile`.
11. Delete, inline or merge each; construct `Reporter` with its trace, and have `DescribeRun` return its counts.

## Trigger

Before phase 3 adds the first describer, which adds cases to 2, 4, 5 and 6; or when any of the pairs named in 1 to 4 drifts.
