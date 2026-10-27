# Settings files, layer 6: the MSBuild path is removed - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Settings reach the generator and the tool only through `sqlsource.json` files: every MSBuild property and item metadata of a setting, their readers, their diagnostics and their manifest lines are gone.

**Architecture:** Four commits, each passing `./pre-commit-validation.sh`.  The tool stops reading MSBuild settings first, so the generator's MSBuild readers can then go with the shared resolution code they feed.  The settings places `Property` and `Metadata` and the list grammars only MSBuild used go next.  Last, the build files lose what fed the compiler and the manifest, the manifest becomes version 2, and a named hook target replaces `SqlSourceTrimMetadataOfFiles`.

**Tech Stack:** C# (analyzer on `netstandard2.0`, tool on .NET 10), MSBuild props/targets, xUnit v3 + Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-25-settings-files-design.md` ("The layers", item 6; "What changes in the build files"; "The manifest").  Hand-over notes of layers 1-5 are in the session group's shared notes folder; `5-projects-notes.md` lists every MSBuild-path test left, with the settings-file test that covers its ground.

## Where the merged code differs from the spec

- `SqlSourceBuildFolders` (layer 3, owner's answer to 3-generator R1-F1) is a `CompilerVisibleProperty` and a manifest value that is about the build, not a setting.  It stays, so `GenerateMSBuildEditorConfigFileCore` still has a property of the package to write, and the manifest keeps one property line, renamed (Task 4).
- The spec says `ManifestOptions` goes.  Layer 4's notes say keep `ManifestOptions.ForProject` for `BuildFolders.Read`.  It goes: the tool calls `BuildFolders.Of(manifest.BuildFolders)` directly (Task 1, then Task 4 renames the manifest member).
- The text forms of `typeOverrides` and `columnTypes` (`TypeOverrideGrammar.ReadList`, `ColumnTypeGrammar.ReadList`) are read only from MSBuild: the attribute has neither setting and the markers have their own grammar.  They go in Task 3 with their tests.  The text form of the generator parameters stays: the attribute writes it.
- The search for settings files is hooked in three ways (layer 2); only its `DependsOnTargets` changes.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` before it (root `AGENTS.md`, Commits).
- An `AGENTS.md` that names a removed or renamed thing is updated in the same commit (root `AGENTS.md`, guidance rule 6).
- Every MSBuild property the build files set, read or show to the compiler starts with `SqlSource` (core rule 6); the hook target's name starts with `SqlSource`.
- User documentation (`docs/manual/settings.md`, `README.md`, other manual pages) and `docs/architecture/` are layer 7's, except what a test of this layer checks (`docs/manual/diagnostics.md`) and what resolving TD-0016 requires (`docs/tech-debt/AGENTS.md`, "Resolving an item").
- A deleted MSBuild-path test is named, with the test that covers its ground, in the PR description (from `5-projects-notes.md`).
- `SqlSourceBuildFolders` and its tests stay (`CachingSettingsFileTests.Run_BuildFolderChanged_ReachesTheSameSettingsFiles`, `ManifestTargetTests.WriteManifest_*BuildFolders*`).

## Review Focus

1. A user's target that adds a `.sql` file and hooks the new hook target with `BeforeTargets`: its file is in the manifest and in the compiler's settings search, in a build and through `sqlsource describe` (Task 4, `ManifestTargetTests`, `SettingsFileSearchTests`).
2. A project that still sets an old property such as `<SqlSourceDialect>postgres</SqlSourceDialect>`: nothing reads it, nothing is reported, and the build and the tool agree (Task 2 generator test, Task 1 tool test).
3. A manifest of version 1 given to the tool: refused with the "out of step" reason, not read as setting nothing (Task 4, `ManifestReaderTests`).
4. A project with no `sqlsource.json` anywhere: every setting is its default, with no diagnostic (Task 2, existing default tests stay green).
5. `SettingPlaces.Specificity` with the MSBuild places gone: a nearer settings file still beats a farther one, and a header marker beats both (Task 3, `SettingPlacesTests`).

---

### Task 1: The tool reads no MSBuild setting

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`
- Delete: `src/SqlSource.Tool/Planning/ManifestOptions.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md` (rules "The plan must agree with the generator" and "Of the settings that are not valid")
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, `RunPlannerErrorTests.cs`, `RunPlannerSettingsFileTests.cs`, `GeneratorParityTests.cs`, `tests/SqlSource.Tool.Tests/TestProjectTests.cs`

**Interfaces:**
- Consumes: `BuildFolders.Of(string? value)` (exists), `ProjectManifest.Properties` (until Task 4).
- Produces: `RunPlanner` reports no `SQLSRC011`/`SQLSRC014`; `ClaimedFile` has no `Metadata`, `ProjectDialect` or `Property` members (it holds `SqlFileText Text`).  Until Task 2 changes the shared signatures, call them with empty MSBuild values: `new FileMetadata(text, DialectSetting.Parse(null), null)`, `DialectSetting.Parse(null)`, `SettingsLevel.None`.

- [ ] **Step 1: Write the failing test**

In `RunPlannerTests.cs`:

```csharp
[Fact]
public void Plan_ProjectThatStillSetsAnMSBuildSetting_ReadsNothingOfIt()
{
    var project = Plans.Postgres(); // settings file says postgres
    project.Properties["SqlSourceDialect"] = "nope";
    project.Properties["SqlSourceDatabase"] = "a b";
    project.Properties["SqlSourceOutput"] = "Sql";
    // add one claimed query file, with metadata SqlSourceDatabase=other
    var result = RunPlanner.Plan([project.Manifest()], RunFilters.None, CancellationToken.None);
    result.Errors.ShouldBeEmpty();
    // the query needs an entry (CodeGen default), dialect postgres, database "postgres"
}
```

Use the file's existing helpers for the project and the claim; assertions: no error, the query's `NeedsEntry` is true, its `Database` is `"postgres"`.

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-method "*Plan_ProjectThatStillSetsAnMSBuildSetting_ReadsNothingOfIt"`
Expected: FAIL (SQLSRC011/SQLSRC014 reported, or output Sql).

- [ ] **Step 3: Remove the MSBuild reading from `RunPlanner`**

`ReadProject` no longer builds options, a dialect, project settings or `InvalidValues`; drop `ReadMetadata`, `AddSettings`, `AddInvalid`, and `ListedFiles`' rule of the last item's metadata for a path listed twice if nothing else needs metadata.  The reaching compile paths are `BuildFolders.Of(manifest.Properties.GetValueOrDefault(BuildFolders.PropertyName)).ReachingPaths(manifest.CompileFiles)`.  Update the class remarks and the comments that list the error order (now `SQLSRC208`, then the settings files' problems).  Delete `ManifestOptions.cs`.

- [ ] **Step 4: Delete the MSBuild-path tool tests**

Delete (each is covered by its settings-file counterpart, `5-projects-notes.md`): in `RunPlannerTests` `Plan_OutputFromEachLevel_IsTakenFromTheMostSpecific`, `Plan_DatabaseFromEachLevel_IsTakenFromTheMostSpecificAndElseIsTheDialectsName`, `Plan_DialectOfAFile_IsItsMarkerThenItsMetadataThenTheProperty`, `Plan_FileListedTwiceByOnePath_HasTheMetadataOfTheLastItemThatHasAny`, `Plan_ChainsDialect_WinsOverMetadataAndProperty`, `Plan_InvalidDialectOfTheMetadata_IsStillSqlsrc011BesideAChain`, helper `PostgresOnMSBuild`; in `RunPlannerErrorTests` `Plan_PropertyThatIsNoDialect_*`, `Plan_MetadataThatIsNoDialect_*`, `Plan_OutputAndDatabaseThatAreNotValid_AreSqlsrc014AtTheProjectAndAreNotSet`, `Plan_MetadataThatIsNotValid_IsSqlsrc014ForAClaimedFileAlone`, `Plan_SettingTheToolDoesNotRead_IsNotReportedThoughItIsNotValid`, helper `ProjectOnMSBuild`; rewrite `Plan_ProjectWithEveryKindOfError_ReportsThemInOrderAndEachValueOnce` without `SQLSRC011`/`SQLSRC014` (expected `["SQLSRC208", "SQLSRC209"]` plus whatever settings-file error it gains, if any); `RunPlannerSettingsFileTests.Plan_ErrorsOfAProject_AreInvalidValuesThenSettingsFiles` becomes `Plan_ErrorsOfAProject_AreClaimsThenSettingsFiles` (SQLSRC208 then the settings file's); `GeneratorParityTests.Errors_OfInvalidValues_AreInTheGeneratorsOrder` is deleted (the generator still reports them until Task 2, so a parity test cannot hold here).  `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader` stays until Task 4 (it tests the writer).

- [ ] **Step 5: Run the tool tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, the new test included.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh && ./pre-commit-validation.sh
git add -A && git commit -m "Have the tool read no MSBuild setting"   # the plan file goes in this commit
```

### Task 2: The generator reads no MSBuild setting

**Files:**
- Delete: `src/SqlSource/Generation/MSBuild/` (all seven files)
- Modify: `src/SqlSource/SqlSourceGenerator.cs`, `Generation/FileParseInput.cs`, `Generation/FileLevel.cs`, `Generation/SqlFileReader.cs`, `Generation/ParsedSqlFile.cs`, `Generation/NestedClassCheck.cs`, `Generation/TrackingNames.cs`, `Settings/QuerySettings.cs`, and every caller of a changed signature (`TypeQueries`, `Sidecars/SidecarNeeds.cs`, `Types/FileTypesInput.cs`, `Types/MappedTypeNames.cs`, `Types/QueryTyper.cs`, `Models/TypeModels.cs`, `Methods/MethodPlanner.cs`, `Emitting/TypeEmitter.cs`, ...), `Parsing/SqlDialectName.cs` (doc naming `SQLSRC011`)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (remove `InvalidDialect`, `InvalidSettingValue`), `src/SqlSource/AnalyzerReleases.Unshipped.md` (rows SQLSRC011, SQLSRC014), `docs/manual/diagnostics.md` (table rows and sections SQLSRC011, SQLSRC014, and every link to them in that page)
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (new signatures), `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `tests/README.md` where they name what goes
- Test: `tests/SqlSource.Tests/Generation/MSBuild/` (delete), `tests/SqlSource.Tests/Generator/*`, `tests/SqlSource.Testing/Generator/{GeneratorHarness,CachingScene,TestOptionsProvider,StepReasons,LevelSettings}.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`, `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`

**Interfaces:**
- Produces (shared with the tool):
  - `FileParseInput.Resolve(AdditionalText file, string? normalizedPath, SettingsChain chain, EquatableArray<string> commentPaths) -> FileParseInput`; record `FileParseInput(AdditionalText File, string? NormalizedPath, SqlDialectChoice Dialect, bool CommentsWanted)`: dialect `chain.Dialect ?? default`; comments wanted when `chain.Level.Parameters` has `KeepComments` or the path is in `commentPaths`.  Keep the overload without `normalizedPath` only if a caller uses it.
  - `FileLevel.Resolve(string? normalizedPath, SettingsChain chain) -> FileLevel?`: null when `chain.Level` is `SettingsLevel.None` or the path is null.
  - `SqlFileReader.Read(AdditionalText file, string? normalizedPath, SqlDialectChoice dialect, bool commentsWanted, CancellationToken)`; `ParsedSqlFile` without `InvalidDialect`.
  - `QuerySettings.Resolve(markers, attribute, file)`, `ResolveTypeOverrides(markers, file, engine)`, `ResolveColumnTypes(markers, file)`, `ResolveDatabase(markers, file, dialectName)`, `ResolveNestedClass(attribute, chain)`: the `property` parameter removed from each.
  - `NestedClassCheck.Apply(declared, settingsFiles)`.
- Removed: `DialectSetting`, `FileMetadata`, `FileSettings`, `InvalidSetting`, `InvalidValues`, `MSBuildSettings`, `ProjectSettings`, `SqlDiagnostics.InvalidDialect`, `SqlDiagnostics.InvalidSettingValue`, tracking names `ProjectSettings`, `ProjectDialect`, `FileSettings`, `FilesSettings` (and any other step that existed only for the project level).

- [ ] **Step 1: Write the failing test**

In `tests/SqlSource.Tests/Generator/SettingsFileTests.cs`:

```csharp
[Fact]
public void Run_ProjectThatStillSetsMSBuildSettings_ReadsNothingOfThem()
{
    // A Postgres query (a ::cast) in a file the type claims, no settings file,
    // global options SqlSourceDialect=nope, SqlSourceOutput=Sql, SqlSourceGeneratorParameters=bad,
    // and file metadata SqlSourceDialect=postgres given through TestOptionsProvider.
    // Expected: the file is read as ansi (its parse error, not SQLSRC011), and nothing else is reported:
    // no SQLSRC011, no SQLSRC014; the output is the default CodeGen (its SQLSRC2xx sidecar error as for no settings).
}
```

Write it with the harness's MSBuild-path parameters before they are removed in step 4 (so the test runs today and fails), then rewrite its setup through `TestOptionsProvider` directly once the harness loses them.  Assertions: `run.Diagnostics` contains no entry starting with `SQLSRC011` or `SQLSRC014`, and equals what the same run gives with no options at all.

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-method "*Run_ProjectThatStillSetsMSBuildSettings_ReadsNothingOfThem"`
Expected: FAIL (SQLSRC011 and SQLSRC014 reported).

- [ ] **Step 3: Remove the MSBuild path from the generator**

- `Initialize`: no `ReadProjectSettings`, no project level, no `ReportInvalidSettings`; `ReadSettings` keeps only `FileLevel`/`FileLevels` from `sqlFiles` and the settings files (no `AnalyzerConfigOptionsProvider` combine per file); `ParseFiles` takes `sqlFiles`, the settings files, `commentPaths` and `claimedPaths`, and reports only parse errors.  Every step that took `projectLevel` loses it (`ClaimFiles`/`NestedClassCheck`, `CheckSidecars`/`SidecarNeeds.OfType`, `TypeFiles`/`MappedTypeNames.Collect`/`FileTypesInput`, `CollectModels`, `RegisterTypeSources`, `RegisterMethodSources`); a step whose only other input it was goes.  Keep each comment true; remove `FindInvalidSettings`, `FindInvalidDialects`, `ProjectProperties`, and the record members for the removed values.
- `BuildFolders.Read(AnalyzerConfigOptions)` keeps its key `build_property.SqlSourceBuildFolders` as a literal of its own, since `MSBuildSettings.PropertyPrefix` goes.
- `SettingPlace.Property`/`Metadata` stay until Task 3, so code that only names them compiles.
- Tool: `RunPlanner` uses the new signatures (`FileParseInput.Resolve(text, path, chain, Empty)`, `FileLevel.Resolve(path, chain)`, no property level).

- [ ] **Step 4: Remove the MSBuild-path tests and helpers**

Delete `tests/SqlSource.Tests/Generation/MSBuild/` and `TestOptionsProviderTests` unless `TestOptionsProvider` keeps a part that test covers.  Delete the tests listed under "Part 2 ... For layer 6" in `5-projects-notes.md` (generator: `DialectTests`, `GeneratorParameterLevelTests`, `ArrayPoolParameterTests`, `TokenValidationTests`, `NestedClassTests`, `SettingLevelTests`, `TypeOverrideLevelTests`, `SettingsFileTests` entries, the 14 caching tests that first-run with `FirstRunOnMSBuild`, with their `RunOnMSBuild`/`OnMSBuild`/`SqlOutput` helpers).  Restructure `SettingsFileTests.Run_RootFileThatSaysNothingElse_HidesTheFilesAbove` as the notes say: farther file `{"output":"Sql","dialect":"postgres"}`, nearer root file `{"root":true}`, assert Q's `SQLSRC209` under ansi at the default output.  `GeneratorHarness` loses `SqlFile.Dialect`/`Metadata` and every `properties:` use but what `SqlSourceBuildFolders` tests need; `CachingScene` loses `FirstRunOnMSBuild`; `StepReasons.AllReasons` loses the `FileSettings` exemption and the SettingsFile/FileLevel exemptions once no run lacks a settings file (keep whatever a run still needs); `QuerySettingsTests` drop the property argument and its cases.  `GeneratorParityTests` drops its MSBuild cases.

- [ ] **Step 5: Retire SQLSRC011 and SQLSRC014**

Remove both descriptors, their rows in `AnalyzerReleases.Unshipped.md`, and their rows and sections in `docs/manual/diagnostics.md` (fix every link to `#sqlsrc011`/`#sqlsrc014` in that page; links from `settings.md` are layer 7's, but if the documentation link test fails on them, point them at `diagnostics.md`'s list instead and record it for layer 7).  `src/SqlSource/AGENTS.md` loses them from the "have no position" rule.

- [ ] **Step 6: Run the generator and tool tests**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, the new test included.

- [ ] **Step 7: Format, validate, commit**

```bash
./format.sh && ./pre-commit-validation.sh
git add -A && git commit -m "Have the generator read no MSBuild setting, and retire SQLSRC011 and SQLSRC014"
```

### Task 3: The places of MSBuild and their list grammars go

**Files:**
- Modify: `src/SqlSource/Settings/SettingPlace.cs`, `SettingPlaces.cs`, `SettingsLevelReader.cs`, `Settings/TypeOverrides/TypeOverrideGrammar.cs`, `Settings/ColumnTypes/ColumnTypeGrammar.cs`, `Generation/Types/QueryTyper.cs` (the place wording of `SQLSRC250`-`252`), `ISettingSource.cs` docs
- Test: `tests/SqlSource.Tests/Settings/{SettingPlacesTests,SettingsLevelReaderTests,QuerySettingsTests}.cs`, `Settings/TypeOverrides/{TypeOverrideGrammarTests,TypeOverrideMapTests}.cs`, `Settings/ColumnTypes/{ColumnTypeGrammarTests,ColumnTypeMapTests}.cs`, `Generation/Types/{MappedTypeNamesTests,QueryTyperTests}.cs`, `Diagnostics/SettingsFileDiagnosticsTests.cs`, `SettingsFiles/*Tests.cs`

**Interfaces:**
- Produces: `SettingPlaceKind { Attribute, FileHeader, Query, SettingsFile }` (members renumber; nothing persists them); no `SettingPlace.Property`/`Metadata`.  `SettingPlaces.Specificity`: `Query` `int.MaxValue`, `FileHeader` `int.MaxValue - 1`, `SettingsFile` `1 + Depth`, `Attribute` 0.  No `TypeOverrideGrammar.ReadList`, `ColumnTypeGrammar.ReadList`, nor any helper only they used; `SettingsLevelReader` reads `typeOverrides` and `columnTypes` from `Members` alone.

- [ ] **Step 1: Write the failing test**

In `SettingPlacesTests.cs`:

```csharp
[Fact]
public void Specificity_SettingsFiles_RankNearerAboveFartherAndBelowTheMarkers()
{
    var root = SettingPlace.OfSettingsFile("/sqlsource.json", 0);
    var nearer = SettingPlace.OfSettingsFile("/a/b/sqlsource.json", 2);
    SettingPlaces.Specificity(root).ShouldBe(1);
    SettingPlaces.Specificity(SettingPlace.Attribute).ShouldBe(0);
    SettingPlaces.Specificity(nearer).ShouldBeGreaterThan(SettingPlaces.Specificity(root));
    SettingPlaces.Specificity(SettingPlace.FileHeader).ShouldBeGreaterThan(SettingPlaces.Specificity(nearer));
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-method "*Specificity_SettingsFiles_RankNearerAboveFartherAndBelowTheMarkers"`
Expected: FAIL (a root settings file ranks 2 today).

- [ ] **Step 3: Remove the places and the list forms**

Remove the two kinds and their static members; rank as above; remove the `ReadList` paths of `SettingsLevelReader` and the two `ReadList` methods with whatever only they call (check each helper with a search before removing it: `TryReadEntry` and `BracketHelp` serve the markers and the settings files).  `QueryTyper`'s place wording loses the property and metadata cases.  Tests that used `SettingPlace.Metadata`/`Property` as an arbitrary place use `SettingPlace.OfSettingsFile(...)`; tests of the list grammars and of MSBuild list ranking are deleted; a ranking test between `Metadata` and `Property` becomes one between a nearer and a farther settings file.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 5: Format, validate, commit**

```bash
./format.sh && ./pre-commit-validation.sh
git add -A && git commit -m "Remove the settings places of MSBuild and the list forms only MSBuild wrote"
```

### Task 4: The build files carry no setting, and the manifest is version 2

**Files:**
- Modify: `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets`, `src/SqlSource/build/SqlSource.AfterSdk.targets` (comments only, if they name a removed target)
- Modify: `src/SqlSource.Tool/Projects/{ManifestReader,ProjectManifest}.cs`, delete `ManifestFile.cs` (or keep it with `Path` alone if callers read better), `src/SqlSource.Tool/Planning/RunPlanner.cs` (and `ListedFiles`), `tests/SqlSource.Tool.Testing/{TestProject,FakeProject}.cs`
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/Projects/LateFile/Directory.Build.targets`, `tests/SqlSource.Tool.Tests/Fixtures/Projects/SettingsFiles/App/Directory.Build.targets` (hook the new target)
- Modify: `tools/check-package-install.sh` if it reads a version-1 line
- Modify: root `AGENTS.md` core rule 6 (its example `SqlSourceGeneratorParameters` is no longer a property: use `SqlSourceIncludeFiles` and `SqlSourceBuildFolders`), `src/SqlSource/AGENTS.md` and `src/SqlSource.Tool/AGENTS.md` (manifest format rules), `tests/AGENTS.md`, `tools/AGENTS.md`/`tools/README.md` where they name a removed target or line
- Resolve TD-0016: delete `docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md`, its row in `docs/tech-debt/README.md`, every `TD-0016` reference (`TD-0025-*.md`, `docs/architecture/msbuild-and-manifest.md`, `SqlSource.targets`), and its known issue in `docs/manual/settings.md` (search the "Known issues" section for the lost metadata of a late target's file).  In TD-0025 replace `SqlSourceTrimMetadataOfFiles` by the hook target.
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`, `tests/SqlSource.Tool.Tests/Projects/{ManifestTargetTests,ManifestReaderTests,ProjectEvaluatorTests,FixtureProjectTests,SettingsFileSearchTests}.cs`, `tests/SqlSource.Tool.Tests/TestProjectTests.cs`, `tests/SqlSource.Tests/TestLayoutTests.cs`

**Interfaces:**
- Produces:
  - Target `SqlSourceListFiles`, empty, in `SqlSource.targets`, no `BeforeTargets`: the target a user's target that adds `.sql` files hooks with `BeforeTargets="SqlSourceListFiles"`.  `SqlSourceFindSettingsFiles` has `DependsOnTargets="SqlSourceListFiles"`; `SqlSourceManifestDependsOn` is `SqlSourceListFiles;SqlSourceFindSettingsFiles;SqlSourceFindBuildFolders` (+ `AddImplicitDefineConstants` for a project of the SDK).
  - Manifest version 2, in this order: `SqlSourceManifest=2`, `Project=`, `TargetFramework=`, `LangVersion=`, `DefineConstants=`, `SqlSourceBuildFolders=<escaped value>`, one `File=<full path>` per `.sql` item, one `Settings=<full path>` per settings file, one `Compile=<full path>` per `Compile` item.  No `Property.` or `File.<Name>` line.
  - `ManifestReader.Version = "2"`; reads `SqlSourceBuildFolders` into `ProjectManifest.BuildFolders` (`string`, empty when absent); `ProjectManifest.Files` is `ImmutableArray<string>` (full paths) unless keeping `ManifestFile(Path)` reads better; no `Properties`.  A key it does not know, `Property.X` and `File.X` included, is passed over.
  - `TestProject` writes version 2 and has no `Properties` and no metadata on `AddSql`/`ListSql`; it keeps a way to set the build folders if a test needs it.

- [ ] **Step 1: Write the failing tests**

`ManifestTargetTests`:

```csharp
[Fact]
public async Task WriteManifest_FileAddedByATargetThatHooksSqlSourceListFiles_IsListed()
// rename of WriteManifest_FileAddedByATargetThatHooksTheTrim_IsListed; the LateFile fixture hooks SqlSourceListFiles.
// Expected: "File=<folder>/Generated/Late.sql" is among the lines.

[Fact]
public async Task WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven()
// updated: lines are exactly SqlSourceManifest=2, Project, TargetFramework, LangVersion, DefineConstants,
// SqlSourceBuildFolders=..., File=<Users.sql>, Settings=..., Compile=...; no line starts with "Property." or "File.".
```

`ManifestReaderTests`:

```csharp
[Fact]
public void Read_ManifestOfVersion1_IsRefusedAsOutOfStep()
// text "SqlSourceManifest=1\nProject=/p.csproj" -> failure whose reason contains "version '1'" and "out of step".

[Fact]
public void Read_LinesOfVersion1Settings_ArePassedOver()
// "SqlSourceManifest=2\nProject=/p.csproj\nProperty.SqlSourceDialect=postgres\nFile=/a.sql\nFile.SqlSourceDialect=x\nSqlSourceBuildFolders=|/p/obj/"
// -> Files == ["/a.sql"], BuildFolders == "|/p/obj/".
```

`BuildFileTests`:

```csharp
[Fact]
public void Props_OnlyPropertyTheCompilerIsShown_IsTheBuildsFolders()
// the CompilerVisibleProperty items of the props are exactly ["SqlSourceBuildFolders"], and there is no CompilerVisibleItemMetadata.

[Fact]
public void Targets_HookForFilesThatTargetsAdd_RunsBeforeTheSearchAndTheManifest()
// SqlSourceListFiles exists, has no BeforeTargets, is in SqlSourceFindSettingsFiles' DependsOnTargets and first in SqlSourceManifestDependsOn.
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*ManifestTargetTests" --filter-class "*ManifestReaderTests"` and `dotnet test --project tests/SqlSource.Tests --filter-class "*BuildFileTests"`
Expected: FAIL.

- [ ] **Step 3: Change the build files**

Props: remove every `CompilerVisibleProperty` but `SqlSourceBuildFolders` and every `CompilerVisibleItemMetadata`, and rewrite the comment above them to say what remains and why.  Targets: remove `SqlSourceTrimProperties`, `SqlSourceTrimMetadataOfFiles`, `SqlSourceCollectSettingsFiles` and the comments that explain them; add `SqlSourceListFiles` with a comment that it is the hook for a target that adds `.sql` files; the manifest's lines as above, with `SqlSourceBuildFolders=$([MSBuild]::Escape($(SqlSourceBuildFolders)))` and `@(SqlSourceManifestSqlFile->'File=%(FullPath)')`; rewrite the manifest target's comments (no metadata, no trims; TD-0025 names the new hook).  Verify that the fallback hook still runs once only `SqlSourceBuildFolders` is compiler-visible: `SettingsFileSearchTests` and the `NotOfTheSdk` case of the search tests stay green.

- [ ] **Step 4: Change the reader and the tool**

As in Interfaces; `RunPlanner` reads `BuildFolders.Of(manifest.BuildFolders)` and the files' paths.

- [ ] **Step 5: Update the tests of the build files**

`BuildFileTests`: delete the `Settings`, `TrimmedOnly`, `PropertyOnly` lists and every test built on them (`Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject`, `Props_MetadataOfASqlFile_ReachesTheCompilerInEveryProject`, `Targets_ManifestTarget_WritesEverySettingOfTheProjectAndOfEachFile`, `Targets_EveryPropertyOfThePackage_IsTrimmed`, `Targets_MetadataOfEverySqlFile_IsTrimmed`, `Targets_FilesWithMetadata_AreTheItemsWhoseMetadataTheCompilerReads`, `Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction` if nothing it guards remains); fix the ones naming removed targets (`Targets_EveryTargetOfABuild_RunsBeforeTheBuildWritesTheFileTheCompilerReads`, `Targets_Root_*`, `Targets_SettingsSearch_*`).  `ManifestTargetTests`: drop `Names`, `PropertyOnlyNames`, `PropertyLines`, `FileLines` metadata and `WriteManifest_ValuesThatHoldASemicolon_AreEachOneLine`.  `ManifestReaderTests`: the format tests use version 2 and the new members.  `TestProjectTests.Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader` asserts files and settings files instead of a property.  `FixtureProjectTests.Evaluate_ProjectWithOneFramework_GivesItsManifest` asserts `BuildFolders`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 7: Check that nothing names what is gone**

Run: `git grep -nE "SqlSourceTrimProperties|SqlSourceTrimMetadataOfFiles|SqlSourceCollectSettingsFiles|SqlSourceSettingsFile\b|CompilerVisibleItemMetadata|SqlSourceManifest=1|TD-0016|MSBuildSettings|ManifestOptions|FileMetadata|DialectSetting|InvalidSettingValue|SettingPlace\.(Property|Metadata)" -- ':!docs/superpowers'`
Expected: hits only in `docs/manual/` (other than diagnostics.md) and `docs/architecture/`, `README.md`, which layer 7 rewrites; list them in the notes for layer 7.

- [ ] **Step 8: Format, validate, commit**

```bash
./format.sh && ./pre-commit-validation.sh
git add -A && git commit -m "Remove the settings from the build files, write manifest version 2, and resolve TD-0016"
```

### Task 5: Hand-over notes for layer 7

**Files:**
- Create: `/Users/michael/Library/Application Support/Claude/side-session-notes/local_4cf760b4-56e0-49bb-9eff-d89dd6351edd/6-remove-msbuild-notes.md` (outside the repository; nothing to commit)

- [ ] **Step 1: Write the notes**: the hook target's name and how to hook it; manifest version 2's lines in order; the removed types, targets and diagnostics; the `git grep` hits of Task 4 step 7 (the docs left for layer 7); designer's calls changed.
