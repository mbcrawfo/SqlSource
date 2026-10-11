# TD-0038, duplicated and uneven patterns in the generator and the tool - design

Date: 2026-10-10

Resolves [TD-0038](../../tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md).  The item lists eleven findings of a maintainability review of `src/`.  This document says what each becomes, in which pull request, and records the four decisions the item left open.  The numbers 1 to 11 below are the item's own.

## Goal

Each pair the item names has one definition, each result that may fail has one shape where files meet, and no method named in the item does several things.  A later phase that adds a setting, an output, a describer or a file-scoped error then touches one place.

Success is:

- Nothing a user sees changes: no message, id, exit code, generated file, sidecar, log member or line of `--verbose`.  `README.md`, `docs/diagnostics.md` and `docs/publishing.md` are not touched.
- The existing tests pass unchanged, apart from tests of a member that is renamed, moved or removed, which follow it.
- The allocation budgets of `SqlFileParserAllocationTests`, `PathResolverAllocationTests` and `SidecarReaderAllocationTests` do not rise.
- When the last pull request merges, `docs/tech-debt/TD-0038-*.md` and its row are gone, and no file names the id.

Not in scope: the tests' own duplication, which is [TD-0039](../../tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md); a test changes here only where the code under it does.

## What the review found

All eleven findings hold on `main` at `fc3a34f`.  Three points go beyond the item's text:

- **The caveat of item 2 does not apply.**  The item says to weigh that the generator would see `SqlSourceDatabase`, cache it and report an invalid value.  `build/SqlSource.props` does not list the property or the metadata for the compiler, so the key is never among the generator's options: a `Database` line in the shared reader finds nothing there.
- **The count of item 6 is right.**  Of thirteen calls of `ToolDiagnostic.ForFile`, ten pass the path again as the first argument.  The three that do not are the two of `RunPlanner.ReadProject`, with the generator's `SQLSRC011` and `SQLSRC014`, whose messages do not quote a path, and `SidecarCheck`, which is at the sidecar and quotes the `.sql` file.
- **Item 9 carries the risk.**  `SqlFileParser.Parser.ReadBlock` and `SidecarReader.ReadEntry` are held by allocation budgets, so their splits are measured.

## Decisions

| Question | Choice |
|----|----|
| Shape of the work | One spec and four pull requests by theme, merged in order.  Each has its own plan, written when its work starts.  Each rewrites TD-0038 to what remains, keeping the item's numbers; the last deletes it. |
| Item 8, the two rules for `default` | Both stay.  A marker has a position for each word and adds up over several lines, so it reports the word that conflicts; a property, a metadata value or an attribute argument has no position, so the whole value is what is quoted.  The walker of words is shared, and the remarks of `GeneratorParameterList` say that the rules differ on purpose. |
| Item 5, how far one shape reaches | A method whose result another file consumes, and which gives one value or one reason, returns the one record.  A private `TryX(out ...)` helper stays, as the C# idiom.  `ProjectEvaluation` keeps its state enum: it has more than two states. |
| Item 11, members with no caller outside the tests | `PostgresType.TryGetKind`, `PostgresTypeKind` and `RunFilters.None` stay.  The first two are the sidecar model's reading of `kind`, which the type map of phase 5 switches on; the third is the plain name of "no filter".  They leave the item as not being leftovers. |

## Pull request 1: the readers of settings (items 1, 2, 3, 4, 8)

### One reader of a level (1)

`Settings/SettingsLevelReader` is the one place that sets the members of a `SettingsLevel` from a source that is not a marker.  It holds what the two readers share today: the list of members, the validator of each, the rule for a text (blank is not set; trimmed; valid, or else recorded and not set), the parse of the generator-parameter list with one invalid value for each word, and the shared `SettingsLevel.None` when nothing is set.

A source says how a raw value is found and where an invalid one goes:

```csharp
internal enum SettingId { Parameters, Output, Database, InputModelSuffix, OutputModelSuffix, ModelNamespace, InputModelType, OutputModelType, CollectionType }

internal interface ISettingSource
{
    // The text as written, or null when the source does not have the setting.
    string? Text(SettingId setting);

    // The choice, or null when it is not set.  A value that is no member is recorded by the source, which alone
    // knows how it was written: a number in an attribute, a word in a property.
    T? Choice<T>(SettingId setting) where T : struct, Enum;

    void Invalid(SettingId setting, string value);
}

public static SettingsLevel Read<TSource>(ref TSource source) where TSource : struct, ISettingSource;
```

A source is a struct, and `Read` is generic over it, so nothing is boxed and the generator allocates what it does today.  A source maps a `SettingId` to its own name: `TargetTypeReader` keeps a source over an `AttributeData`, which answers with the property names of `AttributeSource`, has no `Database`, and adds a `DiagnosticInfo` of `SQLSRC006`; `MSBuildSettings` keeps one over `AnalyzerConfigOptions` and a `SettingKeys`, which looks up by key and adds an `InvalidSetting` under the name without its prefix.  Both lose their `ReadSettings`/`Read` bodies, `ReadParameters` and `ReadText`.

`TargetTypeReader.ReadPlacement` becomes the attribute source's own read of a choice, `MemberPlacement`, with `Nested` when it is not set.

`Settings/` still uses nothing of `Parsing/`, `Generation/` or `Diagnostics/`: the interface is its own, and the sources live with their readers.

### `SqlSourceDatabase` travels with the others (2)

`MSBuildSettings` gains `DatabaseName` and a `Database` key in `SettingKeys`; the shared reader sets `SettingsLevel.Database` under `SettingValue.IsDatabaseName`.  `RunPlanner` drops `DatabaseSetting` and `ReadDatabase`: a file's database is `query.Markers.Database`, then the metadata level's, then the property level's, then the dialect's name, and `IsRead` names `DatabaseName` beside `OutputName`, so the same `SQLSRC014` is reported at the project file in the same order.

The generator's behaviour does not change, because the compiler is not shown the key.  `BuildFileTests` already holds `build/SqlSource.props` to that, with its list `TrimmedOnly`.

### The prefixes once (3)

`MSBuildSettings.PropertyPrefix` and `MSBuildSettings.MetadataPrefix` are the two constants.  `DialectSetting.PropertyName` and `MetadataName` are composed from them, and so are the `SettingKeys` and the tool's `ManifestOptions`, which loses its own two.

### The names of `OutputKind` once (4)

`AttributeReader.TryReadOutput` reads the member with `Enum.TryParse<OutputKind>`, case-sensitive, as the compiler reads it.  `SettingValue.NameOf<T>(T)` gives a member's name in lower case from the names `TryReadChoice` already holds, built once for each enum, and `RunPlanner.NameOf` goes.  A new member of `OutputKind` then needs no line in the tool.

### One walker of words (8)

`GeneratorParameterList.Words(ReadOnlySpan<char>)` enumerates the words of a list with their offsets and allocates nothing.  `GeneratorParameterList.Parse` and `SqlGeneratorParameterScope.Read` both walk with it and keep their own rules, and `FindWordEnd` and `SkipWhiteSpace` go.  The remarks of `GeneratorParameterList` state the decision above.

## Pull request 2: results and errors of the tool (items 5 and 6, and the tool's part of 10 and 11)

### One record for a value or why not (5)

```csharp
internal sealed record Outcome<TValue, TFailure>
{
    public TValue? Value { get; }
    public TFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool Succeeded { get; }

    public static Outcome<TValue, TFailure> Of(TValue value);
    public static Outcome<TValue, TFailure> Failed(TFailure failure);
}
```

It replaces `DescribeResult` and `OpenResult`.  The describer contract keeps its two names as aliases, `global using DescribeResult = Outcome<QueryDescription, DescribeFailure>` and the same for `OpenResult`, so that a describer still writes `OpenResult.Failed(failure)`; `Described` and `Opened` become `Of`.

It is also what these return, each reporting nothing itself:

| Method | Today | Becomes |
|----|----|----|
| `ManifestReader.Read` | `ProjectManifest?` and `out string reason` | `Outcome<ProjectManifest, string>` |
| `SolutionReader.ReadAsync` | `SolutionProjects(Projects, Failure?)` | `Outcome<ImmutableArray<string>, string>`; `SolutionProjects` goes |
| `RunLogFile.TryOpen` | `RunLogFile?` with `out path` and `out reason` | `Outcome<RunLogFile, ToolDiagnostic>`, the failure being the `SQLSRC218` that `Cli` reports |
| `RunUnitFinder.Find` | reports, and returns `RunUnit?` | `Outcome<RunUnit, ToolDiagnostic>`, with no reporter; `DescribeCommand.PlanAsync` reports |

`DescribeCommand.PlanAsync` still returns `RunPlan?`.  It is the command's own sequence: it reports several errors as it goes, and null means there is nothing to plan, which has no one reason.

The rule goes into `src/SqlSource.Tool/AGENTS.md`: a method that another file calls and that gives one value or one reason returns an `Outcome`, and does not report; a private helper may be a `TryX`; a result with more than two states is a record with a state.

`FileCheck` holds its `FileOutcome` in place of the `File` and `SidecarPath` it repeats.

### `ForFile` takes the path once (6)

`ToolDiagnostic.ForFile(descriptor, path, params rest)` sets the error's path and puts the path first among the arguments, for a descriptor whose message quotes its file as `{0}`.  `ToolDiagnostic.AtFile(descriptor, path, params arguments)` sets the path and takes the arguments as given, for the three sites above.  The ten sites lose their repeated argument.

### A mode in place of two booleans (10)

`DescribeOptions` holds `RunMode Mode`, one of `Describe`, `Force` and `Check`, in place of `Force` and `Check`; `--force` beside `--check` is `Check`, as `options.Force || options.Check` with `options.Check` reads today.  `RunDecisions.Decide(plan, mode, errors)`, `RunSummary` and `DescribeRun` take the mode.

`FileOutcomes.Decide(files, mayDelete, read)` takes one answer in place of `runHasFilter` and `anythingWasReported`.  `DescribeRun` computes it, and the reasons for both halves move to the comment there and to the parameter's documentation.

### No order to remember (11)

`Reporter` takes its `RunTrace` in its constructor, and `Trace` has no setter.  `DescribeRun.RunAsync` returns the counts that `run.end` ends with, and `RunTrace.Databases` goes.  System.CommandLine gives a command's action nothing but an exit code to return, so `Cli.RunAsync` changes its order: it builds the commands without a reporter, reads the command line, opens the log, makes the reporter with its trace, binds the action of `describe` to a function that keeps the returned counts in a local, and invokes.  An error before the trace exists, the log that cannot be opened, is reported by a reporter with no trace, as today.

`AttributeReader.ReadText` and `SqlFileText.GetText` read through one helper of `Planning/`.

## Pull request 3: `Parsing/` and the generator (item 7, and the generator's part of 9, 10 and 11)

### One helper for text (7)

`Parsing/SqlText` holds `Trim(text, start, end)`, `FindLineEnd(text, start, end)`, `CharAt(text, index)` and the two line terminators, each allocating nothing.  `SqlMarkerReader`, `TokenScanner`, `SqlLexeme.GetContentSpan`, `SqlLexer` and `SqlTextBuilder` call it and lose their copies.

### A scope knows its kind (10)

`SqlMarkerScope` takes a `SqlScopeKind`, one of `Preamble`, `Query` and `WholeFile`, in its constructor, and `Read(marker)` loses `inQuery` and `inPreamble`: a scope never changes what it is.

### `ReadBlock` in three steps (9)

`SqlFileParser.Parser.ReadBlock` becomes a sequence of three:

1. Read the block's markers: the scope, the summary, the dialect marker and the marker after the last SQL.
2. Build the query: the stripped SQL, the token scan and its errors, the tokens, the declared variables and the parameters.
3. Resolve what the parser can of the settings: the markers over the preamble's, whether comments are kept, and the kept form.

`SqlParameterList.Create` takes one record for the values that always travel together, the built SQL with what was scanned from it, in place of a parameter for each.  The record is a `readonly record struct`, so the parse allocates nothing more.

The pull request states the time and the allocation of a parse before and after, measured as `src/SqlSource/AGENTS.md` says.

### Leftovers (11)

- `SqlSourceGenerator.ToSortedFiles` and `ToSortedSettings` become one generic method that takes how to find an element's path.
- `SidecarComparer.Json` formats a number and a boolean through `SidecarJsonBuilder`, as it already does a string.
- `SettingValue.IsNamespace` and `IsTypeName` become one method, `IsQualifiedName`, whose documentation says it is the rule of both.
- `SqlFileParser` calls `SqlMarkerReader.Describe`, and `SqlDialectMarker.Describe` goes.

## Pull request 4: long methods (the rest of item 9)

Each is split by the responsibilities the item names, with no change to what it does or in what order:

- `Cli.RunAsync`: read the command line; open the log; invoke; end the run.  The nested `try` becomes one.
- `RunPlanner.ReadProject`: read the project's own settings; map each claim to its files; read each claimed file; report what is not valid.
- `RunPlanner.PlanFile`: parse; plan one query; build the planned file.
- `ProjectEvaluator.EvaluateOneAsync` and `SidecarReader.ReadEntry`: by their steps, the second within its allocation budget.
- `DatabaseRuns.DescribeAllAsync`: one record for what belongs to a session, the session, its guard, the clock, the reporter and the trace, in place of five of its eight parameters.

## Testing

The existing suites hold each change: `GeneratorParityTests` and the settings tests of `RunPlanner` for pull request 1; the tests of `Commands/`, with `SecretTests`, `FilterRunTests`, `CheckFilterTests` and `RunLogTests`, for pull request 2; the parser's tests and the allocation tests for pull request 3; all of them for pull request 4.

Each new unit gets tests of its own, written before it: `SettingsLevelReader` over a fake source, `SettingValue.NameOf`, `GeneratorParameterList.Words`, `Outcome`, `ToolDiagnostic.ForFile` and `AtFile`, `SqlText`.  A test of a member that goes moves to the member that replaces it.

## Documents

- `src/SqlSource/AGENTS.md` and `src/SqlSource.Tool/AGENTS.md` change in the commit that renames or removes a member they name, and gain the rules above: the shared reader as the place a new setting's line goes, and the `Outcome` rule.
- `CONTRIBUTING.md`, `README.md`, `docs/diagnostics.md` and `docs/publishing.md` do not change.
- No new tech debt is expected.  What a pull request finds and does not fix gets its own item, as the core rules say.
