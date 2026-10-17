# AGENTS.md - src/SqlSource

How the generator works: [`docs/architecture/generator.md`](../../docs/architecture/generator.md).

The source generator.  It is loaded into the C# compiler of whoever consumes it, which constrains what the project may target and reference.

- **`netstandard2.0` only.**  The compiler loads Roslyn components built for `netstandard2.0`.  Do not add or change target frameworks in `SqlSource.csproj`.
- **The Roslyn pin is the support floor.**  `Microsoft.CodeAnalysis.CSharp` is pinned to 4.8.0 in `Directory.Packages.props`, the compiler in the .NET 8 SDK and Visual Studio 2022 17.8.  An API added after 4.8.0 means raising the pin, which drops consumers: ask the user first, and update the support target in `README.md` in the same commit.
- **The generator tests run on two Roslyn versions.**  A test in `tests/SqlSource.Tests/Generator/` also runs on 4.8.0, so it may use only Roslyn API that 4.8.0 has and hand the compiler C# 12 at most.  [`tests/README.md`](../../tests/README.md) has the projects and the harness.
- **Language features need runtime support.**  `LangVersion` is `preview`, but `netstandard2.0` lacks the types behind some features.  One other than `init` and records (`required` members, say) needs a polyfill in `Polyfills/`, beside `IsExternalInit.cs`, in the namespace of the type it stands in for; `.editorconfig` turns IDE0130 off there.
- **No collection expression may target `ImmutableArray<T>`.**  The `System.Collections.Immutable` that Roslyn 4.8.0 brings predates support for it (CS9210).  Use `ImmutableArray.Create`, a builder or `ToImmutableArray()`.
- **Models need value equality.**  A type the pipeline caches is a record, and holds its collections in `EquatableArray<T>`, never `ImmutableArray<T>`.
- **Performance is a requirement.**  The generator runs in the compiler and the IDE, on every edit.  Allocate nothing per character or line, copy text in runs, rent buffers from `ArrayPool<T>`, and make a string only where a result needs one.  Never raise an allocation budget of `tests/SqlSource.Tests` to make a change pass; lower one when the code improves.  A change to a hot path states its time and allocation, before and after, in its pull request, from a `Stopwatch` loop over a `Release` build, since there is no benchmark project.
- **Do not reference `System.Memory`, `System.Buffers` or `System.Collections.Immutable` directly.**  They arrive with Roslyn 4.8.0 at the versions the compiler host loads; a newer one can fail to load there.
- **Keep Roslyn references private.**  Package references here use `PrivateAssets="all"` so that nothing flows to the projects that reference the generator.

## The pipeline

The steps are in `Generation/`, wired by `SqlSourceGenerator`.  See [the pipeline](../../docs/architecture/generator.md#the-pipeline).

- **No Roslyn object flows between steps.**  Only `Generation/TargetTypeReader.cs` and the `SupportedFramework` step touch symbols and syntax.  A value between steps never holds an `ISymbol`, a `SyntaxNode`, a `Compilation`, a `Location` or a `Diagnostic`; carry a `LocationInfo` or `DiagnosticInfo`, and make the Roslyn object in an output step.
- **Every `Collect()` has a `Select` after it that gives an `EquatableArray<T>`**, or the step after runs on every edit.  The listed paths that `CaseCollisions` takes are the one exception.
- **Resolving a type's `Path` must not depend on the text of any file**, only on the list of paths.
- **A `.sql` file that no type claims is never read**, and its errors are never reported.
- **Compare paths with `SqlPath.Comparer`, after `SqlPath.Normalize`; find one with `SqlPath.IndexOf`, `SqlPath.Contains` or `SqlPath.FindInFolder`.**  Never compare a raw path, call `System.IO.Path` or touch the file system (RS1035), and never search a list with a pass over it.  See [paths](../../docs/architecture/generator.md#paths).
- **The lists of paths and of parsed files stay distinct ignoring case.**  A later file whose path differs only by case is left out, and `Generation/Paths/PathCollision.cs` reports it as `SQLSRC013`.
- **The `sqlsource` tool plans a run with this code.**  A change to a member that `src/SqlSource.Tool/Planning/` calls changes what the tool describes ([the list](../../docs/architecture/generator.md#the-tool-runs-the-same-code)); `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs` compares the claims of the two.
- **A step whose re-runs matter has a name in `Generation/TrackingNames.cs` and a test in `tests/SqlSource.Tests/Generator/CachingTests.cs`.**  A small `Select` between two named steps may go unnamed.  A change to the pipeline keeps those tests true, or changes them on purpose.
- **The attribute file compiles as C# 7.3; a type's file may use C# 12.**  `AttributeSource.Text` has no `#nullable` directive and no nullable annotation.  Generated code uses nothing newer than C# 12: raising the floor in `Generation/Emitting/LanguageSupport.cs` drops consumers and changes the README.  See [floors](../../docs/architecture/generator.md#language-and-framework-floors).
- **A type's file starts with `// <auto-generated/>`, `#nullable enable` and `#pragma warning disable CS0108, CS0114`**, writes strings with `SymbolDisplay.FormatLiteral`, never as raw string literals, and uses `\n` line endings.
- **A compiler warning inside generated code is a bug.**  A new kind of generated code gets a driver test that asserts there are none.
- **Nothing a user can write may make the generator throw.**  An exception drops all of the generator's output (CS8785).  See [what costs every type its code](../../docs/architecture/generator.md#what-costs-every-type-its-code).
- **A generated file's name comes from `Generation/Emitting/HintName.cs`**, built from identifier values, never the text as written.
- **Nothing a token method writes may be hidden by a parameter.**  `Generation/Emitting/MethodWriter.cs` reads the state tuple by position, names the lambda's parameters with `GetUnusedName`, writes framework types from `global::` and `string` as the keyword.  A new identifier there needs the same care, and a row in `Run_TokenWithAnAwkwardName_Compiles` of `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`.
- **The dialect is resolved for each file before the parse**, by `Generation/MSBuild/DialectSetting.cs` and `Generation/FileParseInput.cs`, as an input of the parse.  See [the dialect](../../docs/architecture/generator.md#how-the-dialect-reaches-the-parse).
- **The `SqlSourceDialect` property and metadata reach the generator only because `build/SqlSource.props` lists them.**  The metadata is listed for the item type `SqlSourceSettingsFile`, which a target in `build/SqlSource.targets` fills with the `AdditionalFiles` items that have any metadata the package reads: the SDK writes a section for every item of a listed type into the file the compiler reads, and listing `AdditionalFiles` would give every `.sql` file of a project one.  `MSBuildSettings.MetadataPrefix` holds the name of that item type, and `MSBuildSettings.PropertyPrefix` where a property is; every key is composed from one of the two, in the generator and in the tool's `ManifestOptions`.
- **Only stage one of the settings reaches the parse.**  A setting of stage two must never be an input of `SqlFileReader.Read`, or a change to a property would parse every file again.  `CachingTests` pins what each edit parses.  See [the two stages](../../docs/architecture/generator.md#the-settings-have-two-stages).
- **A value MSBuild gives is trimmed by a target.**  The compiler reads it from a file the build writes with one line for each value, so a value on a line of its own would arrive empty and a list with one word on each line as its first word, and no code in the generator can see that.  A trim removes the white space around a value and makes one space of each run of white space inside it, so a new setting must not give white space any meaning beyond separating words.  The trims run before `GenerateMSBuildEditorConfigFileCore`, the target of the SDK that writes the file, because a trim outside a target misses `Directory.Build.targets` and an item that a target adds.  They still miss an item added by a target that hooks the same SDK target and is declared later; the comment in the file says what such a target does instead.

## The project manifest

`SqlSourceWriteManifest` in `build/SqlSource.targets` writes what the compiler is given, for the `sqlsource` tool: the project's path and `TargetFramework`, its `.sql` files with their metadata, the package's properties, the `Compile` files, `LangVersion` and `DefineConstants`.  `build/SqlSource.props` sets `SqlSourceImported`, which is how the tool tells that a project uses SqlSource.

- **The target hooks nothing, on purpose.**  The tool runs it by name, and no build runs it.  Do not give it `BeforeTargets`, `AfterTargets` or a condition: `tests/SqlSource.Tests/Package/BuildFileTests.cs` holds that.  Its `DependsOnTargets` is the property `SqlSourceManifestDependsOn`, set in the one `PropertyGroup` at the root of the file.
- **The format is a contract with the tool**, as the sidecar's is with the generator.  Lines of `key=value`, the key ending at the first `=`; the first line is `SqlSourceManifest=1`.  A new key, and a new name under `Property.` or `File.`, are additions: a reader ignores what it does not know.  A change to what an existing key means, or to how a line is read, raises the version, and a reader of another version reads nothing.  The spec of sub-phase 2.3, `docs/superpowers/specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md`, has the table of keys, and `src/SqlSource.Tool/Projects/ManifestReader.cs` is the reader.
- **A new setting of the package is a new line in the target**, twice: `Property.<Name>` and `File.<Name>` in the transform.  `BuildFileTests` fails until both are there.
- **A property is written through `$([MSBuild]::Escape(...))`, and the lines of a file are one item.**  Without the first a value that holds `;` becomes several lines; without the second the metadata of a file could be parted from its `File` line.  A property of the SDK, `DefineConstants` for one, is put on one line first with the expression of a trim: the trims do not reach it, and a line break inside a value ends its line of the file.  `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` runs the target on the projects of `tests/SqlSource.Tool.Tests/Fixtures/Projects/` with the real `dotnet msbuild`.
- **It depends on `AddImplicitDefineConstants`, a target of the SDK, by name** (`docs/tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md`), and it does not see a file that a hook of the build adds (`docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`).

## `Settings/`

Plain data and the readers of values.  See [settings](../../docs/architecture/generator.md#settings).

- **No file access, no symbols, no pipeline types, and nothing from `Parsing/`, `Generation/` or `Diagnostics/`.**  A rule shared with another folder goes in the root namespace, as `SqlIdentifier` does.
- **`QuerySettings.Resolve` is the one rule that puts the levels together.**  A new setting is a nullable member of `SettingsLevel`, a line in `Over`, a member of `QuerySettings` with its default, and a branch in `SqlMarkerScope.Read` when a marker gives it.  When another source gives it, it is also a member of `SettingId`, a line in `SettingsLevelReader.Read`, a name in `MSBuildSettings` in the order of `SettingId`, and a property in the `AttributeSettings` of `TargetTypeReader`.  An `ISettingSource` only finds a raw value and records one that is not valid.  A reader that finds nothing returns `SettingsLevel.None`, never a new empty level.
- **A setting a user can write is documented in `docs/manual/settings.md` alone**: a row of its table with every place it can be written and its status, and a section anchored `setting-<name>`.  A new marker also gets a row in the marker index of `docs/manual/sql-files.md`.  A new generator parameter gets its name in the Values cell of the Generator parameters row and in the table under `#setting-generator-parameters` of `settings.md`, and in the section of `SQLSRC109` in `docs/manual/diagnostics.md`, which a test checks.  A setting accepted before the change that gives it an effect says so in its status, and that change flips it.
- **`OutputKind`, `MemberPlacement`, `ModelKind`, `CollectionKind` and `MethodPlacement` keep the numbers of the attribute's enums.**  `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs` compares each pair.
- **A value from a fixed list is read by `SettingValue.TryReadChoice`**, from the names of an enum's members, so renaming a member changes what a user may write.

## Diagnostics

The generator's are in `Diagnostics/SqlDiagnostics.cs`, and those only the `sqlsource` tool reports in `Diagnostics/ToolDiagnostics.cs`.  See [diagnostics](../../docs/architecture/generator.md#diagnostics).

- **Every diagnostic is an error tagged `NotConfigurable`**, and takes its category and the start of its help link from `SqlDiagnostics.Category` and `SqlDiagnostics.HelpLinkBase`.
- **Adding, removing or changing one touches four places:** the descriptor and the `All` of its class; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/manual/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
- **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
- **`SQLSRC011` and `SQLSRC014` have no position**, so they are reported with `Location.None` in an output step and never travel as a `DiagnosticInfo`.
- **`SQLSRC901` is a suppression, not a diagnostic.**  Its `SuppressionDescriptor` is not in `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md` or a section of `docs/manual/diagnostics.md`.  `Diagnostics/AttributeConflictSuppressor.cs` suppresses nothing but the types of `AttributeSource.GeneratedTypes` declared in the project being compiled; `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` holds both halves.
- **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, and a helper that builds descriptors hides them from it.
- **An id below 100 is about the type or the project, one from 101 about a `.sql` file, and one from 200 about the snapshot or the tool** (`tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` holds the tool's).  `SQLSRC200` to `SQLSRC236` are taken; a later phase takes the ids from `SQLSRC237` that its section of `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` assigns, not the next free one.  Add a new `SqlParseErrorKind` at the end: the parser ids follow its order.

## `Parsing/`

See [parsing](../../docs/architecture/generator.md#parsing).

- **Never build the kept form of a query's SQL for every query**: it doubles what a parse allocates.  The tokens, the parameters and the hash come from `Segments`, the SQL without comments.  See [the two forms](../../docs/architecture/generator.md#a-block-has-two-forms-of-its-sql).
- **Pure.**  No file access and no pipeline types.  Comparisons are ordinal; marker keywords and the names of generator parameters are ordinal ignoring case.
- **Every span is an offset into the file's text**; `SqlBlockText.ToSourceSpan` maps one of a block's built SQL back.
- **Quoted regions and hints are copied as written**, apart from line endings.  Trimming and blank-line removal must never reach inside one.
- **Lexemes cover the text without gaps**, and a quoted region or block comment ends with its closing delimiter: `SqlMarkerReader` and `SqlTextBuilder` depend on both.
- **The small readings of a text are in `SqlText`**, called and never copied.  The two line terminators of SQL are there and nowhere else in `Parsing/`; `Generation/Emitting/XmlDocWriter.cs` has the five of a documentation comment, another rule.
- **Dialect-sensitive choices live in `SqlDialectRules` and the readers of `Parsing/Quoting/`.**  `SqlLexer` names no dialect and no quoting form.  `Lex_Construct_IsReadByTheRulesOfTheDialect` in `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` pins every cell; changing one changes the SQL users get.
- **A reader holds no state and allocates nothing.**  It reads one part of a string; whether a string goes on is the lexer's call.  See [lexemes](../../docs/architecture/generator.md#lexemes-and-the-rules-of-a-dialect).
- **A quoted region must never be able to hold a marker.**  Keep each part of a continued string a quoted region of its own, and carry nothing but the pending continuation from one lexeme to the next in `SqlLexer`.
- **A parameter is a lexeme, and `SqlParameterList` alone builds a query's list.**  A name the query declares as a local variable is left out everywhere, defaults too; `SqlDeclaredVariables` reads the file's lexemes and lexes nothing again.  A parameter inside a token must be in the SQL or declared (`SQLSRC118`).
- **`SqlMarkerScope` alone knows a marker's scope and value.**  A new marker is a `SqlMarkerKind`, a keyword in `SqlMarkerReader`, a row in `IsAllowedInQuery`, `IsAllowedInPreamble` or both, and a branch in `SqlMarkerScope.Read`.
- **`SqlFileParser.Parser.ReadBlock` is three steps in a fixed order**: the block's markers, the query, the settings.  Moving one changes which of two errors at one place comes first.  See [markers](../../docs/architecture/generator.md#markers).
- **The parser and `SqlDialectMarker.Apply` judge a `-- dialect:` marker with `SqlDialectMarker.TryRead` alone**, so that they agree.
- **A dialect's names, and those of its options, are in `SqlDialectName` only.**  `SQLSRC011` and `docs/manual/diagnostics.md` repeat them; `SqlDialectName.Accepted` and a test keep the message in step.
- **The describable dialects are in `SqlDescribableDialects` only**: `postgres` and `mssql`.  `SQLSRC209` and its section of `docs/manual/diagnostics.md` repeat them; a test holds the list to them.
- **The dialect is not a generator parameter.**  `SqlGeneratorParameterScope` holds what `-- generator:` lines give.  A setting with a value of its own, and an MSBuild property beside it, is a marker.
- **The hash has one definition, `SqlQueryHash`, and is not computed during a parse.**  A change to it, or to what `SqlTextBuilder` gives for stripped SQL, makes every stored hash stale.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.

## `Snapshot/`

See [snapshot](../../docs/architecture/generator.md#snapshot).

- **The format is a contract** with every committed sidecar and `schemas/sidecar-v1.schema.json`, recorded in `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, which says which changes need a new version.  A change to what `SidecarWriter` gives changes the format design, the schema, and the examples there and in `tests/SqlSource.Tests/Snapshot/Examples/`, in one pull request.
- **The reader never throws.**  Never hand a `SidecarCursor` a text the grammar pass has not passed, and keep a file the tool wrote tokenized twice and no more.  `Read_MistakeInAnArrayBeforeTheEngine_IsFound` and the reordered file of `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderRobustnessTests.cs` hold the path no file of the tool takes.  See [reading a sidecar](../../docs/architecture/generator.md#reading-a-sidecar).
- **The reader is lenient and the schema is strict.**  A new key is added to the model, to its object's list of keys and to the `switch` beside it; a key of a parameter's type to each engine's list in `SidecarTypeReader`, and a key of an entry to `SidecarReader.EntryMembers`, which the `switch` of `ReadMembers` sets.
- **Every name of a key is in `SidecarKeys`, and every value the tool writes in `SidecarValues`.**  A test compares each list of `SidecarValues` with the schema's.
- **The writer goes by the condition, not by the value**, as the remarks of `SidecarWriter` say.  It checks nothing and never throws.
- **`Snapshot/` uses `EquatableArray<T>` and `TextSpan` and nothing of `Parsing/`, `Generation/`, `Settings/` or `Diagnostics/`.**  A test holds `SidecarValues.Engine` and `Parsing/SqlDialectName.cs` together.
- **`SidecarComparer` compares what `describe --check` compares, and no more**, as section 3 of the format design lists.  A new key is not compared, unless it is a key of a type object.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
