# Settings files - design

Date: 2026-10-25

Written against `main` after phase 8 of the [query generation epic](2026-10-07-query-generation-epic-design.md) closed, sub-phase 8.7's column types included.

SqlSource's settings move out of MSBuild and into `sqlsource.json` files that apply to the folder they sit in and every folder below it, as `.editorconfig` files do.  MSBuild keeps only what interacts with the build itself.  The work is delivered as one pull request per layer into an integration branch, which reaches `main` whole, so no intermediate state ever does.

## How to read this document

Each decision is one of three kinds:

- **Decided.**  Agreed with the project owner.  An implementation must not contradict it without asking.
- **Designer's call.**  A proposal made while writing this design, which the owner has seen and not changed.  An implementation may change one when the code shows a better way, and says so in its pull request.
- **Verify first.**  A fact about MSBuild, Roslyn or a tool that the design depends on and that was not confirmed while writing it.  The phase that depends on it confirms it before building on it.

## Why

Thirteen settings are written today as MSBuild properties of the project and, all but one, as metadata of `AdditionalFiles` items; sub-phase 8.7 added the thirteenth, `SqlSourceColumnTypes`.  Only three `SqlSource*` properties need MSBuild at all: `SqlSourceIncludeFiles`, which controls the item globs, `SqlSourceImported`, the marker the tool asks for, and `SqlSourceManifestFile`, which the tool passes.  The others are values for the generator and the tool that MSBuild carries, and carrying them is where most of the friction is:

- The compiler hands a generator a value only up to its first `;` or `#`, and nothing reports the rest ([settings.md, values written in MSBuild](../../manual/settings.md#values-written-in-msbuild)).
- A value written over several lines arrives empty or as its first word, so the package trims every value in three targets of [`SqlSource.targets`](../../../src/SqlSource/build/SqlSource.targets), and a type override needs escapes such as `\003B` for characters a JSON string holds plainly.
- Metadata reaches the compiler only through the `SqlSourceSettingsFile` collection, and a file added by a late target loses it: [TD-0016](../../tech-debt/TD-0016-dialect-of-a-file-that-a-late-target-adds-is-lost.md).
- A value inside a project file is XML, so a quoted key's `U&"..."` is written `U&amp;"..."`, and the column types use `=` rather than the markers' `->` because a `>` would be `&gt;` ([8.7's design](2026-10-25-query-generation-phase-8-7-column-types-design.md#column-types)).
- A value that is not valid, `SQLSRC011` or `SQLSRC014`, is reported with no file and no line.
- Adding a setting means editing the `CompilerVisibleProperty` list, the `CompilerVisibleItemMetadata` list, two trims, the collect condition and two manifest lines, which `BuildFileTests` checks against a list of its own.

The owner's goal is large projects: a folder tree in which a subtree has its own settings is common there, and a file per folder expresses it directly.

## Goals

1. Every setting that can be written as an MSBuild property or item metadata today can be written in a `sqlsource.json` file instead, and no longer in MSBuild.
2. A `sqlsource.json` applies to its folder and every folder below it.  A nearer file overrides a farther one, setting by setting, and `"root": true` stops the search.
3. A value that is not valid is reported at its file, line and column.
4. The generator and the tool read the same files and resolve the same values for every query.
5. A value holds any character it needs, with no trimming, no cut at `;` or `#`, and no escapes beyond JSON's own.

## Non-goals

- **Sections for file patterns inside a settings file**, as `[*.report.sql]` is in `.editorconfig`.  Decided: a settings file sets values for its folder only, and a file's header markers handle exceptions.
- **Migration from the MSBuild properties.**  Decided: the package is not published (`VersionPrefix` 0.1.0, no `v*` tags), so an old property that is still set needs no warning and is simply not read.
- **Values computed by MSBuild**, such as `$(RootNamespace).Models`, and values that differ by configuration or target framework through a `Condition`.  A JSON value is literal.  This capability is given up knowingly.
- **Changing the attribute or the markers.**  They keep their places and their rules; only the two MSBuild places are replaced.
- **Changing what a setting means**, its values or its defaults.

## Anti-goals

- A settings file that does nothing and says nothing.  A typo in a key, a value of the wrong JSON type, or a `sqlsource.json` the generator cannot use must be reported.
- A generator and a tool that disagree on which files apply to a query.  The tool must read exactly the settings files the compiler was given.
- A settings file that a build or a publish copies to the output folder.

## Constraints

- **A generator cannot read the disk.**  It sees only the `AdditionalFiles` the build gives it, so every settings file that can apply must be an `AdditionalFiles` item, including those in folders above the project.
- **The build finds the files, not the generator.**  MSBuild's item function `GetPathsOfAllDirectoriesAbove()` gives every folder above an item, to the top of the drive, which is how the SDK finds `.editorconfig` files: see [finding the files](#finding-the-files).
- **The analyzer targets `netstandard2.0` and ships no dependencies.**  `System.Text.Json` is not available; the generator already has its own JSON tokenizer for sidecars, in [`Snapshot/Reading/`](../../../src/SqlSource/Snapshot/Reading).
- **Every push passes `./pre-commit-validation.sh`** (AGENTS.md, Commits).  That holds for every layer, not only the last: each layer builds, passes every test, and passes the documentation tests in `tests/SqlSource.Tests/Documentation/` and `tests/SqlSource.Tests/Diagnostics/`.
- **AGENTS.md files are updated in the commit that renames, moves or removes what they name** (AGENTS.md, rule 6 of the guidance for agents).  That stays per layer.
- **MSBuild property names keep the `SqlSource` prefix** (AGENTS.md, core rule 6), and an SDK property the build files read is listed in `SdkProperties` in [`BuildFileTests`](../../../tests/SqlSource.Tests/Package/BuildFileTests.cs).

## Approaches

Three ways of structuring the files were considered.  Each had to cover the two jobs MSBuild does today: sharing settings across projects (`Directory.Build.props`) and giving a folder its own settings (`AdditionalFiles Update` metadata).

**A. One file per project, with glob sections.**  A project uses the nearest `sqlsource.json` found walking up from its folder, like `global.json`, and a project-level file replaces a shared one whole.  Folders get their own settings through glob sections.  Simplest to build and to explain, and one file to look at, but a project that wants most of a shared file's settings repeats them.

**B. A settings file per folder, inheriting like `.editorconfig`.**  Any folder can hold one; nearer files override farther ones; `"root": true` stops the walk.  Fits a large project's folder tree directly and shares across projects naturally.  Costs: the build files must find every settings file above each `.sql` and C# file, "which place wins" becomes a stack of unknown depth, and the type override's ranking must account for depth.

**C. A plus one inherited file.**  A project's file overrides the nearest file above the project.  Covers "a shared base plus a tweak", but needs two-file merge rules for every setting while still not handling folders inside a project except by globs.

`.editorconfig` and `.globalconfig` were dropped: Roslyn reads them natively, but they have the same `;` and `#` cut and white space problems that motivate the move.

**Decided: B**, without glob sections.  The owner chose it for large projects, accepting its complexity.

## The settings file

### Name and place

Designer's call: the file is named `sqlsource.json`, in lower case, matched exactly.  Any folder may hold one.  A file whose name differs only by case is not read; whether it is reported is the builder's call.

### Format

Designer's call: JSON with comments and trailing commas, as `tsconfig.json` allows: `//` and `/* */` comments, and a comma after the last member of an object or array.  The top level is one object.  Keys are camelCase, matching the sidecar's style:

| Key | JSON type | Setting | Today's MSBuild name |
|----|----|----|----|
| `$schema` | string | none: ignored, for editors | |
| `root` | boolean | stops the search: see [the chain](#the-chain-of-a-folder) | |
| `dialect` | string | Dialect, options included, as `"mysql,ansi-quotes"` | `SqlSourceDialect` |
| `database` | string | Database | `SqlSourceDatabase` |
| `output` | string | Output | `SqlSourceOutput` |
| `generatorParameters` | array of strings | Generator parameters | `SqlSourceGeneratorParameters` |
| `inputModelSuffix` | string | Input model suffix | `SqlSourceInputModelSuffix` |
| `outputModelSuffix` | string | Output model suffix | `SqlSourceOutputModelSuffix` |
| `modelNamespace` | string | Model namespace | `SqlSourceModelNamespace` |
| `inputModelType` | string | Input model type | `SqlSourceInputModelType` |
| `outputModelType` | string | Output model type | `SqlSourceOutputModelType` |
| `collectionType` | string | Collection type | `SqlSourceCollectionType` |
| `typeOverrides` | object of strings | Type overrides | `SqlSourceTypeOverrides` |
| `columnTypes` | object of strings | Column types | `SqlSourceColumnTypes` |
| `nestedClass` | string | Nested class | `SqlSourceNestedClass` |

```jsonc
{
    "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
    "root": true,
    "dialect": "postgres",
    "generatorParameters": ["keep-comments", "async-method-suffix"],
    // Dapper reads these through UtcDateTimeOffsetHandler.
    "typeOverrides": {
        "timestamptz": "System.DateTimeOffset",
        "public.\"Email\"": "MyApp.Email",
    },
    // Every query that reads one of these columns gets the C# type, by its short name.
    "columnTypes": {
        "users.id": "UserId",
        "posts.created_by_user_id": "UserId",
    },
}
```

The values and their rules are those of today's MSBuild values, read by the same code in [`Settings/`](../../../src/SqlSource/Settings):

- A choice, `output` and the model and collection types, is matched ignoring case, hyphens and spaces, as `SettingValue.TryReadChoice` does now.
- `generatorParameters` is an array of words, each read by `GeneratorParameterList`.  Designer's call: `[]` is the empty list, which replaces the farther list with every default, as `-- generator: default` does; `"default"` is accepted as an element too, with today's rule that it stands alone.
- `typeOverrides` maps a key to a C# type or to `"default"`.  Each key is read by `TypeKeys`' rules and each type by `ClrTypeName`, as an MSBuild entry's are; the list splitting of `TypeOverrideGrammar.ReadList` is not needed, since JSON separates the entries.  A key's quoted parts are still how a case or a dot is matched exactly; only the escapes for white space, `;` and `#` stop being needed.  Two members with one key, by JSON's text or by `TypeOverrideMap`'s identity, are an error.
- `columnTypes` maps a source key, `table.column` or `schema.table.column`, to a C# type or to `"default"`, by `ColumnTypeKey` and `ColumnTypeGrammar`'s rules for a key and a type, with the same object form and the same rule for two members of one key.  Its keys need no `&amp;`, and no reason remains for MSBuild's `=` against the markers' `->`, since the object separates key and type.
- A C# type in `typeOverrides` and `columnTypes` may be a short name, resolved against the project as 8.7 resolves it; the reader keeps the text, and resolution stays where it is.
- A string that is empty or only white space sets nothing, as today.  Designer's call: it is not an error, so a file can blank a value out while editing.

### What is reported

Designer's call, each with the file, line and column of the member or value:

- Text that is not JSON, or whose top level is not an object: an error, and the file gives nothing.  Its `root` is then unknown, so the search goes on past it; the error fails the build anyway.
- A key that is not in the table: an error.  `$schema` is the only key not read.  A key given twice at the top level: an error.
- A word of `generatorParameters` that is not a parameter: an error for that word, and the other words apply, as for an MSBuild list today.
- A value of the wrong JSON type: an error, and the setting is not set by this file.
- A value that is not valid for its setting: an error, and the setting is not set by this file, so a farther file or the default applies, as for a bad MSBuild value today.
- `nestedClass` in a file that no type's chain reaches: the builder's call whether it is reported.

`SQLSRC011` and `SQLSRC014` exist only for MSBuild values, and are retired when the MSBuild path is removed.  The new diagnostics take ids of their own in the `SQLSRC0xx` range, chosen by the builder, and are added to `AnalyzerReleases.Unshipped.md` and to [`diagnostics.md`](../../manual/diagnostics.md).  A Roslyn `Location` can point into an additional file, so the build shows the file and line; the tool reports the ones it reports with the same position.

**Who reports what.**  Decided:

- **Both the build and the tool** report what is wrong with a file as a file: text that is not JSON or a top level that is not an object, a key that is not in the table or is given twice, and a value of the wrong JSON type.
- **Each side** reports a value that is not valid only for the settings it reads, as `RunPlanner.AddSettings` and `diagnostics.md` say today: the tool for `dialect`, `output` and `database`; the build for every setting but `database`, which it still does not read.  So a bad `typeOverrides` entry fails the build and not `sqlsource describe`, and a bad `database` fails `sqlsource describe` and not the build.
- **A listed settings file is checked only when a chain reaches it**: the owner's decision of 2026-10-26, which narrows "every settings file the build lists is checked".  A file is checked when the chain of the folder of some `.sql` item or C# `Compile` item reaches it, whether or not that `.sql` file is claimed or that C# file declares a type.  A file beyond the first `"root": true` of every such chain is listed but never checked, so a broken `sqlsource.json` above a root file never fails a build.  A file whose text is not JSON has an unknown `root`, so chains pass through it.  The tool checks the files that the chains of the manifest's `.sql` files and `Compile=` files reach, by the same rule.  `GeneratorParityTests` holds the two sides equal on what both report.

### Schema

`schemas/settings-v1.schema.json`, beside [`sidecar-v1.schema.json`](../../../schemas/sidecar-v1.schema.json), describes the file for editors: the keys, their JSON types and their enumerated values.  It accepts what the reader accepts and no more, except where a rule cannot be written in a schema, such as a type override's key grammar.  A test checks that the keys and choices of the schema and of the reader agree, as the sidecar's schema is tested.

## Which settings apply

### The chain of a folder

The chain of a folder is the list of settings files in that folder and in each folder above it, nearest first, ending after the first one that says `"root": true`, or at the top of the drive.

- **A `.sql` file** uses the chain of its own folder.
- **A type** uses the chain of the folder of the C# file whose declaration carries `[SqlSourceGenerate]`.  This is where the nested class is read.  Designer's call: it replaces "the project's property", since a type, like a `.sql` file, sits in a folder; the attribute's `Path` is already relative to that folder.
- **A `.sql` or C# file outside the project's folder**, linked in, uses the chain of its own folder like any other: the build files search above every file, not above the project.

### Which place wins

Each setting is decided on its own, as today.  The places, most specific first:

| Order | Place |
|----|----|
| 1 | A marker inside the query |
| 2 | A marker in the file header |
| 3 | The attribute of the type that claims the file |
| 4 | The nearest settings file of the `.sql` file's chain that sets it |
| ... | each farther file of the chain |
| last | The default |

The settings files take the places that the item metadata and the project property hold today.  The exceptions keep their rules:

- **Generator parameters**: the nearest list replaces the farther ones whole.
- **Dialect**: the file header's marker, else the nearest file of the chain that sets it, else `ansi`.  A value replaces a farther one whole, options included.
- **Type overrides and column types**: merged per key across every place; the nearer entry wins for its key, and `default` takes a key out.  Both rank the entries that match one type or column by `SettingPlaces.Specificity` (`Query`, `FileHeader`, `Metadata`, `Property`), so that a plain key at a more specific place beats an exact key below it, and for column types a key with a schema beats one without only within one place.  The folder files need a rank of their own by depth, nearer above farther, in that one function, so both maps keep the rule across the chain.  How the rank is carried, a depth on the entry or a place per depth, is the builder's call.  A query's own `-- column: name -> type`, a source key at any level over the type override at any level, and `-- param: ... -> type` keep 8.7's precedence unchanged.
- **The place a diagnostic names**: `SQLSRC250` to `SQLSRC252` word the place that chose a type, "the property `SqlSourceColumnTypes`" for one.  A folder file's place is worded with the settings file's path; whether it is shortened relative to the project's folder is the builder's call.
- **Database**: the markers, then the chain, then the dialect's name.  It has no attribute level.
- **Nested class**: the attribute, then the chain of the type's C# file, then `Sql`.

### Finding the files

The build finds the settings files the way the SDK finds `.editorconfig` files, in `Roslyn/Microsoft.Managed.Core.targets`:

```xml
<_AllDirectoriesAbove Include="@(Compile->GetPathsOfAllDirectoriesAbove())" />
<PotentialEditorConfigFiles Include="@(_AllDirectoriesAbove->'%(FullPath)'->Distinct()->Combine('.editorconfig'))" />
<EditorConfigFiles Include="@(PotentialEditorConfigFiles->Exists())" />
```

The package does the same over the `.sql` items of `AdditionalFiles` and the `Compile` items, with `sqlsource.json`: every folder above any of them, each combined with the name, kept where the file exists.  There is no limit on depth and nothing to warn about.  As for `.editorconfig`, the build finds every file and the reader honours `root`: the generator and the tool cut each chain at its first `"root": true`.  The SDK's comment on the `->'%(FullPath)'->Distinct()` step, that `GetPathsOfAllDirectoriesAbove()` can give one folder twice for a linked file with a relative path (msbuild issue 4392), applies here too.

Designer's call: the search runs in a target of the package before `GenerateMSBuildEditorConfigFileCore`, and before the manifest, rather than where the props are imported.  A `.sql` file that a target adds while the build runs is then searched above too, as long as that target runs first.  Verify first: that a design-time build of an IDE runs the target and passes the items it adds to the compiler, as the comment of `SqlSourceTrimProperties` says such a build does for the trims.

## What changes in the build files

After the last layer, [`SqlSource.props`](../../../src/SqlSource/build/SqlSource.props) and [`SqlSource.targets`](../../../src/SqlSource/build/SqlSource.targets) keep:

- `SqlSourceImported`, `SqlSourceIncludeFiles` and its `**/*.sql` and `**/*.sql.json` globs, `SqlSourceTrackAdditionalFiles`, the nesting of sidecars, and `SqlSourceWriteManifest`.

They add:

- **The settings files the [search](#finding-the-files) finds** as `AdditionalFiles` items, with `Visible="false"` on those outside the project's folder so an IDE does not list them.  Designer's call: the search is not under `SqlSourceIncludeFiles`; it runs over whatever `.sql` items the project has, so a project that lists its own `.sql` files still gets its settings.  A settings file in a folder that holds no `.sql` or C# file, and has none below it, is not found, and applies to nothing.
- **`CopyToOutputDirectory="Never"` and `CopyToPublishDirectory="Never"`** on `Content` items named `sqlsource.json`, as the sidecars have, since the Web SDK lists `.json` files as `Content`.

They remove:

- Every `CompilerVisibleProperty` and `CompilerVisibleItemMetadata` of a setting, `SqlSourceSettingsFile`, `SqlSourceTrimProperties`, `SqlSourceTrimMetadataOfFiles` and `SqlSourceCollectSettingsFiles`.
- The `Property.` and `File.<Name>` lines of the manifest.

`SqlSourceTrackAdditionalFiles` hashes every `AdditionalFiles` item, so adding, deleting or moving a settings file already makes the next build compile again.

**A hook for targets that add `.sql` files.**  Today a user's target that adds a `.sql` file hooks `SqlSourceTrimMetadataOfFiles` so that the manifest sees the file ([settings.md, known issues](../../manual/settings.md#known-issues), [TD-0025](../../tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md)).  That target goes away, so the targets keep a named target that `SqlSourceWriteManifest` depends on and that such a target can hook.  Its name is the builder's call.  TD-0016 is resolved by the removal: no metadata is collected, so nothing can be lost.

### The manifest

The manifest lists the settings files the compiler is given, one `Settings=` line each with its full path, after the `File=` lines and before the `Compile=` lines.  The tool reads each listed file with the generator's reader, so both see the same files.  Designer's call: the version becomes `SqlSourceManifest=2` in the layer that removes the `Property.` and `File.` setting lines, since a tool of version 1 would read such a manifest as setting nothing, without a word.  The `File=` lines stay, without their metadata.

## What changes in the generator and the tool

The design leaves the structure to the builder, within these requirements:

- **One reader** of a settings file, in [`Settings/`](../../../src/SqlSource/Settings) or a folder beside it, used by the generator and the tool.  It never throws, whatever the text, for the reason [`SidecarReader`](../../../src/SqlSource/Snapshot/Reading/SidecarReader.cs) never does, and it bounds nesting as `SidecarTokenizer.MaxDepth` does.  Whether it extends `SidecarTokenizer` with a mode for comments and trailing commas or has a tokenizer of its own is the builder's call; the sidecar stays strict JSON.
- **The values go through `SettingsLevelReader`** by way of an `ISettingSource` over the parsed file, so the rules of validity stay in one place.  The dialect is read beside it, as `DialectSetting` reads it now, since it is not a `SettingId`.
- **Stage one stays cheap.**  The parse of a `.sql` file depends on its dialect and on whether a level it cannot see may ask for `keep-comments` ([generator.md, the settings have two stages](../../architecture/generator.md#the-settings-have-two-stages)).  Those two values are resolved from the file's chain into its `FileParseInput`, which compares equal when they do not change, so an edit of a settings file parses again only the files whose two values changed.  The caching tests in [`CachingScene`](../../../tests/SqlSource.Testing/Generator/CachingScene.cs) cover an edit that changes them and one that does not.
- **Stage two** joins each file's resolved level where `FileSettings` and the project's level join today: `TypeQueries`, `TypeOutput`, `TypeNeeds`, `TypeModels`, `NestedClassCheck` and `FileTypesInput`.
- **`SettingPlace`** loses `Property` and `Metadata` and gains what the folder files need: see [which place wins](#which-place-wins).
- **The tool** builds the same chains from the manifest's `Settings=` lines, the `.sql` files' paths and the project's path, in [`RunPlanner`](../../../src/SqlSource.Tool/Planning/RunPlanner.cs), where it reads `ManifestOptions` today.  `ManifestOptions`, `MSBuildSettings`, `ProjectSettings`, `FileSettings`, `FileMetadata`'s MSBuild reading and `DialectSetting`'s property and metadata reading go once nothing uses them.

## Delivery: an integration branch

Decided: the layers are merged, one pull request each and in order, into an integration branch cut from `main`, and the integration branch reaches `main` in one last pull request once every layer is in.  So `main` never holds a layer without the ones after it.  The integration branch's name is the builder's call.

So a layer need not leave the product in a state a user could use: it may read settings files alongside MSBuild, or leave the user documentation describing MSBuild, until a later layer finishes the job.  What a layer must do is pass `./pre-commit-validation.sh` (see [constraints](#constraints)), and update the documentation that a test of that layer checks and the AGENTS.md files that name what it changes.  AGENTS.md core rule 4, which asks each pull request to keep the documentation current, is met by the integration branch as a whole: the owner's decision for this work.

The pull request of each layer targets the integration branch and is merged with a merge commit, as the repository merges.  The last pull request, from the integration branch to `main`, runs the version check of AGENTS.md (pull requests, rule 1); today no `v*` tag exists, so there is nothing to compare.

### The layers

In order.  Each is one pull request into the integration branch, and needs the ones before it merged there.  Designer's call: the cut between layers, which may be changed while planning a layer when the code shows a better one, provided the order of dependencies holds.

1. **The reader.**  The settings file's tokenizer and reader, its `ISettingSource`, the object form of the type override and the column types, the new diagnostics' descriptors with their `diagnostics.md` sections and `AnalyzerReleases.Unshipped.md` rows, and `schemas/settings-v1.schema.json` with its agreement test.  Unit tests of every rule of [the settings file](#the-settings-file).  Nothing calls it yet.
2. **The build files deliver the files.**  The targets find the settings files above every `.sql` and C# file and add them as `AdditionalFiles`, and the props add the `Content` exclusion; the manifest gains its `Settings=` lines and the tool's `ManifestReader` reads them.  Purely additive: every MSBuild setting still flows.  [`BuildFileTests`](../../../tests/SqlSource.Tests/Package/BuildFileTests.cs) and [`ManifestTargetTests`](../../../tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs), with a fixture project that has settings files inside and above it, a linked `.sql` file from outside its folder, and a `.sql` file a target adds.
3. **The generator reads the chains.**  Chains and `root`, stage one and stage two, the nested class from the type's folder, the type override's rank by depth, and the diagnostics reported at their positions.  The MSBuild levels stay, below the folder files, for this layer and the next two only, so that every project in the repository still builds.  Generator tests through [`GeneratorHarness`](../../../tests/SqlSource.Testing/Generator/GeneratorHarness.cs) with settings files as additional texts, and caching tests.
4. **The tool reads the chains.**  `RunPlanner` builds the chains from the manifest, the database is resolved from them, and the settings files' diagnostics are reported by the tool.  Tool tests.
5. **Every project of the repository moves to settings files.**  The examples, with their type override and their column type, the end-to-end projects of `tests/SqlSource.Tests.Postgres/` and `tests/SqlSource.Tests.SqlServer/` including their `ErrorPaths` projects, the test projects' own `.csproj` files that set a setting, the tool's fixture projects in `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, `tools/package-install/`, the realistic models of `tests/SqlSource.Tool.Testing/Realistic/`, and the generator tests that set a property or metadata through `TestOptionsProvider` or `LevelSettings`.  Metadata on a glob of a folder, such as `Realistic/Cases/**/*.sql` or the billing queries, becomes that folder's `sqlsource.json`.  Metadata on one file, such as `SqlSourceColumnTypes` on `EndToEnd/Queries/Notes.sql`, becomes a marker in that file's header, `-- column: accounts.id -> AccountId`; a header marker is not part of a query's hash (`SqlQueryHash`), so no sidecar goes stale.  A glob that is neither a folder nor one file is the builder's to map to markers or folders, and is named in the pull request.  Designer's call: the repository's root gets a `sqlsource.json` holding `"root": true` and nothing else, so no file above the checkout reaches its projects; this matters here because the worktrees under `.claude/worktrees/` sit inside the main checkout, whose own files would otherwise be ancestors of every worktree's projects.  A test of an MSBuild-only behaviour, such as the trims, the cut at `;` or a value over several lines, is deleted here or in the next layer, with what replaces its coverage named in the pull request.  Sidecars do not change, since what is described does not.  If this layer is too large to review, it splits into the examples and projects, then the tests.
6. **The MSBuild path is removed.**  The build files' removals listed [above](#what-changes-in-the-build-files), the hook target that replaces `SqlSourceTrimMetadataOfFiles`, the manifest's version 2, the MSBuild readers of the generator and the tool, `SettingPlace.Property` and `Metadata`, the retirement of `SQLSRC011` and `SQLSRC014` from the descriptors, `AnalyzerReleases.Unshipped.md` and `diagnostics.md`, and TD-0016 resolved as `docs/tech-debt/AGENTS.md` says a resolved item is.  [`BuildFileTests`](../../../tests/SqlSource.Tests/Package/BuildFileTests.cs) loses its lists of settings, and AGENTS.md core rule 6 is checked for anything it says that is no longer true.
7. **The documentation.**  Everything AGENTS.md core rule 4 asks for that the layers below did not already do.  At least: [`docs/manual/settings.md`](../../manual/settings.md), rewritten around settings files, with its tables, [which place wins](../../manual/settings.md#which-place-wins), [configure one folder differently](../../manual/settings.md#configure-a-folder) and [values written in MSBuild](../../manual/settings.md#values-written-in-msbuild) replaced, and its known issues updated; every other manual page and `README.md` where it shows a `SqlSource*` property or metadata; [`docs/architecture/generator.md`](../../architecture/generator.md) (settings), [`docs/architecture/msbuild-and-manifest.md`](../../architecture/msbuild-and-manifest.md) and [`docs/architecture/tool.md`](../../architecture/tool.md); the AGENTS.md files and the tests' and tools' READMEs not already updated; TD-0025 for its new hook; and the comments of the build files and examples.  A search for each of today's MSBuild names finds nothing outside `docs/superpowers/` when this layer is done.

## What phase 8 brought

- **Column types** (8.7): a thirteenth setting, `SqlSourceColumnTypes`, at four levels like the type override, merged by the shared keyed-map rules of `SortedKeyedList` and ranked by `SettingPlaces`.  It becomes `columnTypes`; see [the format](#format) and [which place wins](#which-place-wins).  Its header-marker form, `-- column: table.column -> Type`, is what a single file's metadata moves to in layer 5.
- **Short C# names** (8.7) need nothing of this design: the reader keeps a type's text, and the resolver is unchanged.
- **`-- param: ... -> Type`** (8.7) is a marker alone, unaffected.
- **The realistic corpora claimed at `CodeGen`** (8.5) and **the examples' `CodeGen` folders** (8.6) set their settings in the projects layer 5 already lists.
- **`D-0008`, a converter per file or per query**, is still deferred.  Should it be built later, a `typeOverrides` or `columnTypes` value can grow from a string to an object, `{"type": "MyApp.Email", "converter": "MyApp.EmailConverter"}`, which needs none of the grammar of a suffix that `D-0008` lists for MSBuild values.  Nothing here builds it; the design only keeps that form free.

## Left to the builder

- The diagnostic ids, their titles and messages, and whether a misnamed `sqlsource.json` or an unreachable `nestedClass` is reported.
- Whether the tokenizer extends `SidecarTokenizer` or is separate, and the folder the reader lives in.
- How a type override entry carries its depth.
- The name of the hook target.
- The cut between layers, within the order above.
- The URL of the schema in `$schema`, and whether the examples' files carry it.
