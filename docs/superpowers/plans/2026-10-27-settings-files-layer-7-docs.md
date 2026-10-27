# Settings files, layer 7: the documentation - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every document, and every comment of the code, says what is true after layers 1 to 6: settings live in `sqlsource.json` files, MSBuild carries none of them, and the manifest is version 2.

**Architecture:** Six commits, each passing `./pre-commit-validation.sh`.  The manual's settings page is rewritten first, since every other page links to its anchors.  The other manual pages and the two packed readmes follow, then the architecture pages, then the code comments, `AGENTS.md` files, readmes and the deferred item, and a last search for the old names.

**Tech Stack:** Markdown (GitHub flavour, Mermaid), C# comments, the documentation tests of `tests/SqlSource.Tests/Documentation/` and `tests/SqlSource.Tests/Diagnostics/`.

**Spec:** `docs/superpowers/specs/2026-10-25-settings-files-design.md` ("The layers", item 7; "The settings file"; "Which settings apply"; "What changes in the build files").  Hand-over notes of layers 1 to 6 are in the session group's shared notes folder; `6-remove-msbuild-notes.md` lists the documents left for this layer.

## Where the merged code differs from the spec

- **TD-0025 already names its new hook.**  Layer 6 rewrote it for `SqlSourceListFiles`, and the build files' comments are current.  This layer only checks them.
- **TD-0016 is not resolved but rewritten** (owner, 6-remove-msbuild R1-F1): a `.sql` file that a late target adds misses its settings file only in a project that is not of the SDK, or one that overwrites `AfterMicrosoftNETSdkTargets`.  The known issue of `settings.md` keeps one sentence for it.
- **Which settings files are checked** (owner's decision 2 of 2026-10-26): only files that the chain of some `.sql` file or C# file reaches.  A file beyond the first `"root": true` is never read, so it cannot fail a build.  The manual says so.
- **The case of the file's name** (owner, 3-generator R1-F3, "document and move on"): a project that lists a `SqlSource.json` itself has it ignored; the build's search finds one in another case on macOS and Windows, and reads it, and not on Linux.
- **The search for the old names** found three tests that feed the old names to show they are ignored (`SettingsFileTests.Run_ProjectThatStillSetsMSBuildSettings_ReadsNothingOfThem`, `TestOptionsProvider`'s metadata prefix, `ManifestReaderTests`' passed-over lines).  Owner, 2026-10-27: "Remove or rewrite the three tests so no old MSBuild setting name remains outside docs/superpowers."  Task 7 does it: a test whose point survives without the names is rewritten with neutral names, any other is deleted, and the PR names what covers its ground.
- **Designer's call changed:** the section "Values written in MSBuild" is replaced by a section on the settings file itself, anchored `settings-files`, which the `See:` lines of `SQLSRC015` to `SQLSRC020` link to in place of the bare page.  Its old anchor `values-written-in-msbuild` goes; the one link to it outside `docs/superpowers/` is updated.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` before it (root `AGENTS.md`, Commits).
- User documentation describes effects, never internals (core rule 4, `docs/manual/AGENTS.md`, "Write effects, never internals"); run that file's grep over every page changed.
- The packed readmes, `README.md` and `src/SqlSource.Tool/README.md`: absolute `https://github.com/mbcrawfo/SqlSource/blob/main/...` links, no raw HTML, `sql` samples carry their dialect.
- A manual heading that is linked keeps its `<a id>` anchor when reworded; never link into `docs/tech-debt`, `docs/deferred`, `docs/superpowers` or `src/` from the manual.
- `docs/architecture/` describes the code as it is; check every claim against the code before writing it, and update `docs/architecture/AGENTS.md`'s table when a section it names is renamed or removed.
- A settings file's example is fenced `jsonc` (it has comments), as `diagnostics.md` already does.
- At the end, this finds nothing outside `docs/superpowers/`:

  ```bash
  git grep -n -P 'SqlSource(Dialect|Database|Output|GeneratorParameters|InputModelSuffix|OutputModelSuffix|ModelNamespace|InputModelType|OutputModelType|CollectionType|TypeOverrides|ColumnTypes|NestedClass|TrimProperties|TrimMetadataOfFiles|CollectSettingsFiles|SettingsFile)(?![A-Za-z])' -- ':!docs/superpowers'
  ```

## Facts every task writes from

Checked against the merged code; a task that finds the code says otherwise follows the code and reports it.

- **Name and place.**  `sqlsource.json`, any folder.  It applies to its folder and every folder below.  A `.sql` file uses the chain of its own folder, a linked one included (its own folder, not the project's).  A type's nested class uses the chain of the folder of the C# file that carries `[SqlSourceGenerate]`.  A settings file in a folder that has no `.sql` or C# file in it or below it is not found and applies to nothing.
- **The chain.**  Nearest first, ending after the first file that says `"root": true`, or at the top of the drive.  A file whose text is not JSON has an unknown `root`, so the chain goes past it, and the error fails the build.
- **Format.**  One JSON object; `//` and `/* */` comments and trailing commas allowed.  Keys: `$schema` (ignored, for editors: `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`), `root`, `dialect`, `database`, `output`, `generatorParameters` (array of strings; `[]` is every default), `inputModelSuffix`, `outputModelSuffix`, `modelNamespace`, `inputModelType`, `outputModelType`, `collectionType`, `typeOverrides` and `columnTypes` (objects of strings, `"key": "type"` or `"key": "default"`), `nestedClass`.  Keys match exactly, case included.  An empty or white-space string sets nothing and is no error.  Values hold any character; no cut at `;` or `#`, no trimming of lines, no `&amp;`, no escape beyond JSON's own.
- **Which place wins.**  A marker inside the query, a marker in the file header, the attribute of the claiming type, the nearest settings file of the `.sql` file's chain that sets it, each farther one, the default.  Generator parameters: the nearest list replaces farther ones whole.  Dialect: the header marker, else the nearest file of the chain that sets it, else `ansi`; a value replaces whole, options included.  Type overrides and column types: merged per key across every place, nearer wins for its key, `default` takes a key out, and a plain key at a nearer place beats an exact key at a farther one.  A settings file's type override or column type reaches only the `.sql` files under it.  Database: markers, then the chain, then the dialect's name; no attribute.  Nested class: the attribute, then the chain of the type's C# file, then `Sql`.
- **What is reported**, at file, line and column: `SQLSRC015` to `SQLSRC020` (see `diagnostics.md`).  A value that is not valid is not set by that file, so a farther file or the default applies.  The build reports every setting but `database`; `sqlsource describe` reports a file's own problems and the values of `dialect`, `output` and `database`, once per run however many projects reach the file; the build does not report `database`, and the tool does not report `typeOverrides`, `columnTypes` or the other settings' values.
- **Which files are checked.**  Only a file that the chain of some `.sql` or C# file of the project reaches, whether or not a type claims that `.sql` file.  A file beyond the first `"root": true` of every such chain is never read, so it cannot fail a build (owner's decision of 2026-10-26).  The files the build writes into its output and intermediate folders reach nothing, wherever those folders are.
- **Case of the name.**  Write it in lower case.  A `SqlSource.json` that the project lists itself is ignored.  One that only the build finds is read on macOS and Windows, which compare names ignoring case, and is not found on Linux.
- **The build.**  A settings file is never copied to the output or publish folder, the Web SDK's included.  Adding, deleting, moving or editing one makes the next build compile again.  An IDE that builds the project sees the same files.  The project file holds no setting: an old property such as `<SqlSourceDialect>` is not read and not reported.  The MSBuild properties a user still writes: `SqlSourceIncludeFiles`.  The hook for a target that adds `.sql` files: `BeforeTargets="SqlSourceListFiles"`, spelled exactly, since a misspelt name is passed over silently.
- **The manifest** (architecture only): version 2, lines in order `SqlSourceManifest=2`, `Project=`, `TargetFramework=`, `LangVersion=`, `DefineConstants=`, `SqlSourceBuildFolders=`, `File=` per `.sql` item, `Settings=` per item named `sqlsource.json` (ignoring case, once each), `Compile=` per `Compile` item.  No `Property.` or `File.<Name>` line; a reader passes over an unknown key, and refuses another version as out of step (`SQLSRC206`).  `ProjectManifest` has `BuildFolders` and `Files`, no `Properties`.
- **Code names gone** (architecture): `Generation/MSBuild/` (`DialectSetting`, `FileMetadata`, `FileSettings`, `InvalidSetting`, `InvalidValues`, `MSBuildSettings`, `ProjectSettings`), the pipeline steps `ProjectSettings`, `ProjectDialect`, `FileSettings`, `FilesSettings`, `ManifestOptions`, `SettingPlace.Property`/`Metadata`, `TypeOverrideGrammar.ReadList`, `ColumnTypeGrammar.ReadList`, the targets `SqlSourceTrimProperties`, `SqlSourceTrimMetadataOfFiles`, `SqlSourceCollectSettingsFiles`, the item type `SqlSourceSettingsFile`, `SQLSRC011`, `SQLSRC014`.  `SettingPlaceKind` is `Attribute, FileHeader, Query, SettingsFile`; `SettingPlaces.Specificity` ranks a settings file `1 + Depth`.  Type keys in messages escape only control characters (6-remove-msbuild R1-F3).

## Review Focus

1. A reader who copies the README's getting-started steps gets a project whose dialect is set: the `sqlsource.json` sample is valid, has the key the reader expects, and sits where the steps say (Task 3; check against `examples/PostgresExamples/sqlsource.json`).
2. A reader with a broken `sqlsource.json` above a root file, such as in a parent checkout: the manual says it is never read and fails nothing (Task 1).
3. A reader who names the file `SqlSource.json`: the manual says what happens on each operating system and when listed by hand (Task 1).
4. A reader of a linked `.sql` file from another folder: the manual says it takes its own folder's settings, not the project's (Task 1).
5. A link anywhere to an anchor that this layer removes or renames: `UserDocsLinksTests` catches the manual and readmes; the architecture pages and `AGENTS.md` files are searched by hand (Tasks 4 to 6).

---

### Task 1: `docs/manual/settings.md` around settings files

**Files:**
- Modify: `docs/manual/settings.md`
- Modify: `docs/manual/concepts.md` (glossary: a *settings file* entry, anchored `settings-file`; line 85's "MSBuild property")
- Modify: `docs/manual/README.md` ("What works today": rows 71-72 name `typeOverrides`/`columnTypes` of a settings file; a row "Settings files: `sqlsource.json` in a folder, for it and every folder below" Available, linking `settings.md#settings-files`)

**Interfaces:**
- Produces: anchors `settings-files` (the new section on the file), `configure-a-folder`, `precedence`, `a-value-that-is-wrong`, `all-settings`, `known-issues`, every `setting-*`, `converters`, `included-files`, all kept; `values-written-in-msbuild` removed.  Later tasks link to `settings.md#settings-files`.

- [ ] **Step 1: Rewrite the opening** example: the markers block, a `jsonc` `sqlsource.json` block (`root`, `dialect`, and `generatorParameters` for a folder), and the attribute block; the sentence after says the second is a settings file that applies to its folder and every folder below.
- [ ] **Step 2: The tables under `#all-settings`.**  The "Written as" sentence lists the places: a settings file, the attribute, a marker in the file header, a marker inside a query.  Each row's "property and metadata" becomes the key: `` `output` in a settings file ``, and so on; the nested class gains `nestedClass` in a settings file.  The two "Values" cells of type overrides and column types read as JSON members (`"key": "type"`).  Statuses unchanged.
- [ ] **Step 3: "Which place wins".**  The `Output` table's rows 4 and 5 become "The nearest settings file that sets it" (`"output": "models"` in `Reports/sqlsource.json`) and "Each farther settings file, nearest first" (`"output": "sql"` at the project's folder).  Rules: "A place that gives no value is skipped" covers a settings file's empty string; "A setting has only the places its row lists" says a key not in the table is `SQLSRC016`.  Exceptions: dialect (header marker, else the nearest settings file that sets it, else `ansi`); nested class (the attribute over the settings files); type overrides and column types merge per key across every place, the settings files nearest first.
- [ ] **Step 4: "A value that is wrong".**  The MSBuild row becomes "A settings file": `SQLSRC015` to `SQLSRC020` at its file, line and column; the setting is not set by that file, so a farther file or the default applies.
- [ ] **Step 5: Every setting's section.**  Replace each MSBuild example and sentence with the settings-file form: Database ("Only the tool reads this setting" now says the build does not report a wrong `database` in a settings file, the tool does), Dialect (example `jsonc`, "Three places" becomes the marker, then the nearest settings file of the file's chain, then `ansi`; drop the "two item lines" sentence; a wrong value is `SQLSRC019` and the file's farther dialect or `ansi` applies), Generator parameters (list of five becomes marker, header, attribute, nearest settings file, farther ones; the property paragraph becomes the array form, `[]`, `"default"` alone, a wrong word is `SQLSRC019` and the others apply), model suffix, namespace, shape, collection type (one clause each), Type overrides (example `jsonc` with `typeOverrides` at the project and a `Legacy/sqlsource.json`; "Four places, merged by type" names the settings files; the "In a property or metadata" paragraph and its two XML samples become "In a settings file": a JSON object, one member per entry, any spacing, a quote in a key written `\"`, `U&"..."` written plainly, two members of one key `SQLSRC020`, a wrong member `SQLSRC019` and the others apply; drop `&amp;`, `&lt;`, `&gt;` and `Directory.Build.props` sentences; the database-specific sentence points at a settings file of that database's folder), Column types (same treatment; "A value in MSBuild" becomes "A settings file's entry"), Nested class (`jsonc` example; the type's C# file's chain; `SQLSRC019`), Where the methods go (`SqlSourceMethodLocation` sentence: "There is no settings file key or marker for it"), the tool sentences ("The `sqlsource` tool does not read the property or the metadata" becomes "does not read `typeOverrides` or `columnTypes`").
- [ ] **Step 6: `#configure-a-folder`.**  Same story (`Legacy/` MySQL, no token validation, keeps comments) told with a project-folder `sqlsource.json` and a `Legacy/sqlsource.json`; keep the three "things to notice"; replace the `Update` paragraphs with: the folder's file applies to its subfolders too, and to a type, through the folder of its C# file, only for the nested class.
- [ ] **Step 7: `#settings-files`**, replacing "Values written in MSBuild", placed after "Which `.sql` files are included": example first, then the rules from "Facts every task writes from": name and case, format and keys (link `diagnostics.md#sqlsrc016` for the list rather than repeating it twice), the chain and `root`, which files are checked and the root decision stated plainly ("A `sqlsource.json` beyond the first `"root": true` is never read, so it cannot fail a build"), linked files, a folder with nothing under it, never copied to output, edits rebuild, the project file holds no setting, `$schema` for editors.  End with `See:`-style links to the diagnostics.
- [ ] **Step 8: Known issues.**  The type-key bullet: `SQLSRC019` in a settings file, `SQLSRC111` in a marker.  The late-target bullet stays (already current).  No new known issue: every behaviour above is documented as a rule.
- [ ] **Step 9: Glossary and "What works today"** in `concepts.md` and `README.md` as listed under Files.
- [ ] **Step 10: Check.**  The internals grep of `docs/manual/AGENTS.md` over the three pages; `git grep -n -i -E 'msbuild|metadata|property' docs/manual/settings.md` and read each hit (only `SqlSourceIncludeFiles`, `SqlSourceListFiles`, the attribute's properties and model properties may remain).
- [ ] **Step 11: Validate and commit.**  `./format.sh`, `./pre-commit-validation.sh` passes, then `git commit -m "Rewrite the settings page around settings files"`.

### Task 2: The other manual pages

**Files:**
- Modify: `docs/manual/diagnostics.md`, `docs/manual/recipes.md`, `docs/manual/dialects.md`, `docs/manual/postgresql.md`, `docs/manual/sqlserver.md`; read and fix where needed `docs/manual/tool.md`, `generator.md`, `sql-files.md`, `parameters-and-tokens.md`.

**Interfaces:**
- Consumes: `settings.md#settings-files`, `#configure-a-folder` from Task 1.

- [ ] **Step 1: `diagnostics.md`.**  `SQLSRC007` (nested class "with `NestedClass` on the attribute or `nestedClass` in a settings file"); `SQLSRC013`'s sample item drops its metadata (`<AdditionalFiles Include="queries/users.sql" />`); the `See:` line of `SQLSRC015` to `SQLSRC020` to `settings.md#settings-files`; the dialect (~1090), database (~1118, "or else the nearest settings file of its folder that sets `database`"), output (~1840) and every MSBuild sample (~1823, ~1950, ~2436): settings-file form.  The messages of `SQLSRC250` to `SQLSRC252` (~1980-2017, ~2258): take the wording from the code's message (`the typeOverrides of the settings file <path>`, `the columnTypes of the settings file <path>`, full path), never "property" or "metadata".  `DiagnosticsDocumentTests` must stay green.
- [ ] **Step 2: `recipes.md`.**  Every recipe that sets a setting in the project file (~21-49 a folder's dialect, ~209, ~335 and the rest the grep finds) shows a `sqlsource.json`; "Add `SqlSourceOutput="sql"` to the metadata" becomes `"output": "sql"` in that folder's file.  Keep each recipe a sample and a link.
- [ ] **Step 3: `dialects.md`, `postgresql.md`, `sqlserver.md`.**  The opening `<SqlSourceDialect>` samples become `sqlsource.json` with `dialect`; `dialects.md`'s `Legacy/**` metadata becomes a folder file.
- [ ] **Step 4: Read the remaining pages** (`tool.md`, `generator.md`, `sql-files.md`, `parameters-and-tokens.md`) for MSBuild settings wording and for what the tool reports; `tool.md` says, where it lists what `describe` checks, that it reports a settings file's problems with the build's file and line, only for files that apply to the project's files, once per run.
- [ ] **Step 5: Check** with the internals grep and `git grep -n -i -E 'msbuild|metadata|SqlSource[A-Z]' docs/manual`, reading each hit.
- [ ] **Step 6: Validate and commit**: `git commit -m "Show settings files on every manual page"`.

### Task 3: The packed readmes

**Files:**
- Modify: `README.md`; read `src/SqlSource.Tool/README.md` (fix only if it names a setting's MSBuild form).

- [ ] **Step 1: Getting started.**  Step "In the project file" (~75) keeps only what MSBuild still needs; the dialect (and anything else set there today) moves to a `sqlsource.json` beside the project file, as `jsonc` with `"root": true` and `"dialect": "postgres"`, with one sentence saying it applies to the folder and every folder below.  The samples at ~133, ~165, ~224, ~234 and the FAQ answers at ~180 (`async-method-suffix`) and ~193 (model namespace) say the key of a settings file.
- [ ] **Step 2: "Where to next"** row ~250 keeps its link to `#configure-a-folder`; add nothing that repeats the manual's tables.
- [ ] **Step 3: Check** links are absolute, no raw HTML; validate and commit: `git commit -m "Set the README's settings in a settings file"`.

### Task 4: The build and the tool in `docs/architecture/`

**Files:**
- Modify: `docs/architecture/msbuild-and-manifest.md`, `docs/architecture/tool.md`, `docs/architecture/README.md`, `docs/architecture/AGENTS.md` (row of `src/SqlSource/build`: `## Trimming` is gone)

- [ ] **Step 1: `msbuild-and-manifest.md`.**  Opening links: the manual's `#included-files` and `#settings-files`.  "What the props and targets do": the props do four things (marker, `AfterMicrosoftNETSdkTargets`, the `.sql` and sidecar items, `CompilerVisibleProperty` `SqlSourceBuildFolders` alone); the targets list and the Mermaid diagram from today's `SqlSource.targets` (`SqlSourceFindBuildFolders`, `SqlSourceListFiles`, `SqlSourceFindSettingsFiles`, `SqlSourceTrackAdditionalFiles`, `SqlSourceSettingsFilesBeforeCompile`, `SqlSourceWriteManifest`).  "Finding the settings files": the search depends on `SqlSourceListFiles`.  Remove the `SqlSourceSettingsFile` section and "Trimming"; say in one place that no setting is a property, so nothing is trimmed and no value is cut.  "The manifest": its dependencies (`SqlSourceListFiles`, the search, the folders, `AddImplicitDefineConstants`); "The format": version 2 lines as in the Facts, with a sentence on why the version rose; the escape rules of `SqlSourceBuildFolders` and of the SDK's properties; drop the `%0a` transform paragraph unless the target still uses it.  "Reading it": `ManifestReader.Read` gives `ProjectManifest` (`Files`, `SettingsFiles`, `BuildFolders`, `CompileFiles` and what else it has, checked against the code); first line `SqlSourceManifest=2`; unknown keys, `Property.` and `File.` included, passed over; what `ListedFiles` does now with a file listed twice (check the code).  Known limits: TD-0025's line names `SqlSourceListFiles`.
- [ ] **Step 2: `tool.md`** (~108-121, ~201): the plan reads the settings files through `RunSettingsFiles` and the build folders through `BuildFolders.Of(manifest.BuildFolders)`; the invalid values bullet goes; the settings files' problems follow the project's `SQLSRC208`s; check the error order against `RunPlanner`.
- [ ] **Step 3: `README.md`** (architecture) lines ~32, ~42, ~74, ~90: the compiler gives the generator settings files and one property; the manifest holds no metadata; a level is what one source says: markers, the attribute, a settings file.
- [ ] **Step 4: Check** every heading this task renames or removes against `git grep -n 'msbuild-and-manifest.md#'` and `docs/architecture/AGENTS.md`; validate and commit: `git commit -m "Describe the build files and the manifest of version 2"`.

### Task 5: The generator in `docs/architecture/generator.md`

**Files:**
- Modify: `docs/architecture/generator.md`

- [ ] **Step 1: The pipeline.**  Redraw the Mermaid diagram and the step list from `SqlSourceGenerator.cs` as it is: no `ProjectSettings`, `ProjectDialect`, `FileSettings`, `FilesSettings`, metadata node or `SQLSRC011`/`SQLSRC014` outputs; add what is there (`SettingsFile`, `SettingsFiles`, `CompilePaths`, `BuildFolders`, `ReachedSettingsFiles`, `FileLevel`, `FileLevels`, and whatever else the code wires).  Recount the source outputs (~211).  `NestedClassCheck` (~176): attribute, chain, `Sql`.
- [ ] **Step 2: "The tool runs the same code"** (~349-351): no `MSBuildSettings`; the tool reads the settings files with the generator's reader and reports what `SettingsFileSides` says.
- [ ] **Step 3: "Settings".**  The places (~355), "Reading a level" (~362-369: no `MSBuildSettings`; the sources are the markers, the attribute and the settings file), the type override (~377-381: no property or metadata, no MSBuild list grammar; the settings file's object form; type keys escape only control characters) and the column types (~388), `SettingPlace`/`Specificity` as in the Facts.
- [ ] **Step 4: The two stages and the dialect** (~433-445, ~497, ~517): `FileParseInput.Resolve` and `FileLevel.Resolve` without metadata or property, as the code has them; the dialect from the header marker or the chain.
- [ ] **Step 5: "How a problem travels"** (~669): drop the unlocated `SQLSRC011`/`SQLSRC014` paragraph; a settings file's diagnostics are located.
- [ ] **Step 6: Check** `git grep -n -E 'MSBuildSettings|ManifestOptions|FileMetadata|DialectSetting|FilesSettings|FileSettings|ProjectSettings|ProjectDialect|InvalidValues|SQLSRC011|SQLSRC014|Generation/MSBuild|ReadList|metadata' docs/architecture` finds nothing but unrelated uses; every link to a `src/` file resolves; validate and commit: `git commit -m "Describe the generator's settings without MSBuild"`.

### Task 6: Comments, AGENTS.md files, readmes, the deferred item and the final search

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs:301` (the `NestedClass` doc: "else the `nestedClass` of the settings files of the folder of the type's C# file, else `Sql`"), `tests/SqlSource.Tests/Generator/NestedClassTests.cs:13`, `docs/deferred/D-0008-converter-per-file-or-query.md` (no `SQLSRC014` or MSBuild grammar; the settings file's object form, as the spec's "What phase 8 brought" says)
- Read and fix where wrong: every `AGENTS.md`, `tests/README.md`, `tests/*/README.md`, `tools/README.md`, `examples/*/README.md`, `CONTRIBUTING.md`, `.github/workflows/README.md`, the comments of `src/` and `tests/` that the search below finds, `docs/tech-debt/TD-0025-*.md` and `TD-0016-*.md` and `docs/tech-debt/README.md`'s rows.

- [ ] **Step 1: Fix the named files.**  If `AttributeSource.Text` is pinned by a snapshot or a test, update the expected text.
- [ ] **Step 2: Search comments and guidance** with `git grep -n -i -E 'msbuild (propert|setting|value)|item metadata|metadata of (a|the|each) file|SqlSourceSettingsFile|trim(s|med)? (the )?(propert|metadata)' -- src tests tools examples '*.md' ':!docs/superpowers'` and the removed-names grep of Task 5 over `src tests tools`; fix every hit that describes a removed setting place.  Leave code names that are current (`GeneratorHarness.ProjectSettings`, `LevelSettings`, `TestProject.Settings`).
- [ ] **Step 3: Validate and commit**: `git commit -m "Correct the comments and guidance that still named MSBuild settings"`.

### Task 7: The tests that still name old settings

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/SettingsFileTests.cs` (`Run_ProjectThatStillSetsMSBuildSettings_ReadsNothingOfThem`), `tests/SqlSource.Testing/Generator/TestOptionsProvider.cs` (its `build_metadata.SqlSourceSettingsFile.` prefix and whatever uses it), `tests/SqlSource.Tool.Tests/Projects/ManifestReaderTests.cs` (the version-2 manifest with `Property.SqlSourceDialect` and `File.SqlSourceDialect` lines), and anything that only served them.

- [ ] **Step 1: `ManifestReaderTests`.**  Its point, that the reader passes over a key it does not know, `Property.` and `File.` lines included, survives: rewrite the lines with neutral names, such as `Property.SqlSourceSomethingNew=x` and `File.SqlSourceSomethingNew=y`, and keep the assertions.
- [ ] **Step 2: `SettingsFileTests.Run_ProjectThatStillSetsMSBuildSettings_ReadsNothingOfThem`.**  Its point is that the old names are ignored, which needs the names: delete it.  If the generator test that shows a property other than `SqlSourceBuildFolders` changes nothing can be kept with a neutral name (`build_property.SqlSourceSomethingNew`), keep that half; otherwise record in the task report which tests cover the ground (the generator reads only `SqlSourceBuildFolders`; `BuildFileTests` pins that the props show the compiler that one property alone).
- [ ] **Step 3: `TestOptionsProvider`.**  Remove the metadata prefix and the metadata support it gives if nothing else uses it after Step 2; otherwise rename what remains so no old name is left.  Update `tests/AGENTS.md` and `tests/README.md` if they name what changed.
- [ ] **Step 4: The final search** of Global Constraints finds nothing outside `docs/superpowers/`.
- [ ] **Step 5: Validate and commit**: `git commit -m "Keep no old setting name in the tests"`.
