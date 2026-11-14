# Test cleanup phase 78: tests/SqlSource.Tests/Package, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Package`
- Findings: 3 (2 delete, 1 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 35
- Guard: covers and run time

## Files

- `tests/SqlSource.Tests/Package/BuildFileTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Package-1-02

- test: `tests/SqlSource.Tests/Package/BuildFileTests.cs` | `BuildFileTests.Targets_ManifestTarget_WritesAPropertyOfTheSdkOnOneLine` | rows: all
- action: delete
- evidence: read (each row, `"TargetFramework"`, `"LangVersion"` and `"DefineConstants"` at BuildFileTests.cs:578-580, asserts that the manifest's lines contain `$"{name}=$([MSBuild]::Escape({OnOneLine(name)}))"`; `Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder`, BuildFileTests.cs:559-573, asserts the whole ordered list of the same lines, which holds those three strings, built by the same `OnOneLine`, at :566-568, so no change to the targets fails the theory and not that test.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name finds nothing; `BuildFileTests` is named in docs/architecture/msbuild-and-manifest.md:152 for comparing the lines and their order, which the cover does, and src/SqlSource/build/AGENTS.md names the one-line rule and points to ManifestTargetTests for the run.  The comment above the theory, :575-576, gives the reason for the one-line form and records no regression); blind: no
- cover: `tests/SqlSource.Tests/Package/BuildFileTests.cs` | `BuildFileTests.Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder`
- proposal: Delete the theory, and carry its comment's reason (a line break in a property would end its line of the manifest) to the comment of `Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder`.
- verdict: confirmed
- verifier's reason: The theory, BuildFileTests.cs:575-585, asserts with `ShouldContain` that the manifest lines include `$"{name}=$([MSBuild]::Escape({OnOneLine(name)}))"` for TargetFramework, LangVersion and DefineConstants.  `Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder`, :558-573, asserts with `ShouldBe` the exact ordered list of the same `SqlSourceManifestLine` Include values.  That list holds the same three strings, built by the same `OnOneLine` helper (:566-568).  Any change to the targets that fails a row of the theory changes one of those list elements and fails the cover, on the same input (the static SqlSource.targets).  Neither test is under mutation (the evidence says None), so this holds by reading.  Searched AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name and found nothing.  It appears only in docs/superpowers/plans, which are not record documents.  src/SqlSource/build/AGENTS.md states the one-line rule and points to ManifestTargetTests for the real run, and to `BuildFileTests` for "the lines and their order", which is the cover.  docs/architecture/msbuild-and-manifest.md gives the same reason and the same credit to the ordered comparison.  The comment above the theory, :575-576, explains why the form exists and records no regression or limit.

### F-SqlSource.Tests.Package-1-03

- test: `tests/SqlSource.Tests/Package/BuildFileTests.cs` | `BuildFileTests.Targets_ManifestTarget_MakesOnlyItemTypesThatStartWithSqlSource` | rows: all
- action: delete
- evidence: read (the test, BuildFileTests.cs:636-646, asserts that the item types of the manifest target, less FileWrites, are exactly `SqlSourceManifestLine`, `SqlSourceManifestSqlFile` and `SqlSourceManifestSettingsFile`; `Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks`, :650-657, asserts the prefix for the item types of every target of SqlSource.targets, the manifest target's among them, so a type of the manifest target without the prefix fails it too.  What the exact set adds is not the rule its name states: a new item type of the manifest with the prefix fails it, a change that keeps the behaviour, and the three names are already pinned by `Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder` (:570-572), `Targets_ManifestTarget_ListsTheSettingsFilesBetweenTheSqlFilesAndTheCompileFiles` (:524) and `Targets_ManifestTarget_ListsTheSqlFilesAndTheCompileFiles` (:591).  The comment above the every-target test, :648, says it is "the same for the item types of every target".  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name finds nothing; only the plan docs/superpowers/plans/2026-10-26-settings-files-layer-2-build.md:15 names it, from before the every-target test was added); blind: no
- cover: `tests/SqlSource.Tests/Package/BuildFileTests.cs` | `BuildFileTests.Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks`
- proposal: Delete the manifest's test, and move the sentence of its comment on the prefix to the comment of the every-target test, which then stands alone.
- verdict: confirmed
- verifier's reason: The test, BuildFileTests.cs:633-646, takes the item types of the manifest target's direct `ItemGroup`s, less FileWrites, and asserts the exact set {SqlSourceManifestLine, SqlSourceManifestSqlFile, SqlSourceManifestSettingsFile}.  `Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks`, :648-657, takes every `ItemGroup` descendant of every `Target` of the same SqlSource.targets, the manifest target among them, so it is a superset of the elements.  It asserts the `SqlSource` prefix on all but AdditionalFiles, CustomAdditionalCompileInputs and FileWrites.  So an item type of the package without the prefix in the manifest target fails the cover too.  The one difference is that the manifest test would also fail on the manifest target adding to AdditionalFiles or CustomAdditionalCompileInputs.  Its own comment (:633-634) gives its purpose as the prefix and treats an SDK type the target adds to as allowed, so that difference is not the behaviour it states.  Each of the three names is pinned elsewhere: SqlSourceManifestSettingsFile by `ShouldHaveSingleItem` at :524, SqlSourceManifestSqlFile at :591, and SqlSourceManifestLine by the non-empty ordered list at :561-573.  The search of AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ found no mention of the test's name.  It is named only in docs/superpowers/plans/2026-10-26-settings-files-layer-2-build.md:15 and 2026-10-10-query-generation-phase-2-3-project-manifest.md:465, plans and not records.  The comment above the cover (:648) leans on this test's comment ("The same for..."), which the proposal moves.

### F-SqlSource.Tests.Package-1-01

- test: `tests/SqlSource.Tests/Package/BuildFileTests.cs` | `BuildFileTests.Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction` | rows: all
- action: strengthen
- evidence: read (the test, BuildFileTests.cs:734-735, matches each value against `MetadataInAPropertyFunction`, `\$\([^)]*%\(` at BuildFileTests.cs:840, whose `[^)]*` stops at the first `)`, so metadata that follows a nested `$(...)` argument is never seen.  src/SqlSource/build/SqlSource.targets:47 has such a case today, `$([MSBuild]::NormalizeDirectory($(MSBuildProjectDirectory), %(Identity)).Replace(...))`, the metadata `%(Identity)` as the second argument of a property function, and the test passes; the regex found no match in the file when run on it.  The rule is stated in docs/architecture/msbuild-and-manifest.md:60 ("`BuildFileTests` forbids it") and :86, and in the comments of SqlSource.targets:85, :157 and :653; no document records the build folders' item as an exception); blind: no
- cover: none
- proposal: Make the check see metadata anywhere inside the parentheses of a property function, nested `$(...)` included, for example by matching the parentheses' depth instead of `[^)]*`.  The stronger test fails on SqlSource.targets:47, so the cleanup either moves that path through a property, as the search does, or names it as the rule's one exception in the test and the architecture page.
- verdict: confirmed
- verifier's reason: The test, tests/SqlSource.Tests/Package/BuildFileTests.cs:733-735, matches every value of SqlSource.targets and the outer targets against `\$\([^)]*%\(` (BuildFileTests.cs:840-841).  src/SqlSource/build/SqlSource.targets:47 holds `$([MSBuild]::NormalizeDirectory($(MSBuildProjectDirectory), %(Identity)).Replace(...))`.  Traced by hand, the match from the outer `$(` runs `[^)]*` up to the `)` of `$(MSBuildProjectDirectory` and finds no `%(` first, and the match from `$(MSBuildProjectDirectory` stops at its own `)`.  No other `$(` is on the line, so the regex misses metadata that is plainly an argument of a property function, and the test cannot fail on it.  The rule is stated without exception in docs/architecture/msbuild-and-manifest.md (the search section: "`BuildFileTests` forbids it"; the package-brought assemblies: "An item's metadata is never the argument of a property function") and in src/SqlSource/build/SqlSource.targets around :85 and :653.  docs/tech-debt/ and docs/deferred/ record no exception for the build folders' item, and neither does the comment at SqlSource.targets:14-42 or the architecture page's "The build's folders" section.  `Targets_BuildFolders_AreTheEscapedFullPathsOfEveryFolderTheBuildWritesInto` pins the case at BuildFileTests.cs:351 (`"SqlSourceFolder=" + Escaped("%(Identity)")`), so a cleanup that strengthens the regex must change that row too, or name the case as the exception, as the proposal says.  This is a missing assertion that the test's name promises, so strengthen is right.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Package-1-02 | delete | `BuildFileTests.Targets_ManifestTarget_WritesAPropertyOfTheSdkOnOneLine` | `tests/SqlSource.Tests/Package/BuildFileTests.cs`: `BuildFileTests.Targets_ManifestTarget_WritesTheLinesOfVersion2InTheirOrder` |
| F-SqlSource.Tests.Package-1-03 | delete | `BuildFileTests.Targets_ManifestTarget_MakesOnlyItemTypesThatStartWithSqlSource` | `tests/SqlSource.Tests/Package/BuildFileTests.cs`: `BuildFileTests.Targets_EveryTarget_MakesOnlyItemTypesThatStartWithSqlSourceOrAreTheSdks` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/msbuild-and-manifest.md`: 60, 94, 108, 116, 152
- `src/SqlSource/build/AGENTS.md`: 4, 8, 11, 19, 22, 30, 33

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
