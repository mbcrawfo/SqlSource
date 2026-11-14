# Test cleanup phase 18: tests/SqlSource.Tool.Tests/Projects, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Projects`
- Findings: 5 (5 delete)
- Kind: one kind, delete
- Changed lines: about 54
- Guard: mutation (tool-projects)

## Files

- `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs`
- `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`
- `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs`
- `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Projects-1-04

- test: `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs` | `TransitivePackageAssembliesTargetTests.Run_PackageListedInAnotherCase_IsNotInTheValue` | rows: all
- action: delete
- evidence: read (TransitivePackageAssembliesTargetTests.cs:30-35 runs the target on the fixture `PackageAssemblies`, as :21-26 does, one MSBuild start each, 0.592 s; :25 asserts the whole value is `["Brought.Client", "Brought.Client.Extra"]`, which already excludes `Mixed`, the assembly of `mixed.case.package` listed in another case in `Fixtures/Projects/PackageAssemblies/App.csproj`; no mutant of the scope is in the targets, so its kill of 1 says nothing).  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: no record names the test; only the plan `docs/superpowers/plans/2026-10-29-td-0086-short-name-binds-to-package-type.md` lists it.  The comment above it explains NuGet's case rule and records no decision.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs` | `TransitivePackageAssembliesTargetTests.Run_ReferencesOfAPackageNotListed_AreTheValue`
- proposal: Delete the test and add its sentence on NuGet's case to the comment of `Run_ReferencesOfAPackageNotListed_AreTheValue`, whose exact value already leaves `Mixed` out.
- verdict: confirmed
- verifier's reason: TransitivePackageAssembliesTargetTests.cs:21-26 and :30-35 both run the target on a copy of `PackageAssemblies`; the fixture's App.csproj lists `mixed.case.package` and gives `lib/Mixed.dll` the `NuGetPackageId` `Mixed.Case.Package`, so if the `MatchOnMetadata` removal at SqlSource.targets:97-100 compared case-sensitively, `Mixed` would be in the value and :25's exact `ShouldBe(["Brought.Client", "Brought.Client.Extra"])` would fail as surely as :34.  The one kill, ProjectEvaluator.cs:305:85 (`"dotnet"` to `""`), is `Programs.Dotnet` of tests/SqlSource.Tool.Testing/Programs.cs:53 starting MSBuild, shared by every target test, and says nothing about the target.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: the method name appears nowhere; docs/architecture/msbuild-and-manifest.md:84 states the case rule and :88 names the class as the one that runs the target, not this test as a record.  The comment at :28 explains NuGet's rule and records no limit or regression; the proposal keeps it beside the cover.

### F-SqlSource.Tool.Tests.Projects-1-05

- test: `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` | `ManifestTargetTests.WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders` | rows: all
- action: delete
- evidence: read (ManifestTargetTests.cs:259-268 asserts that the decoded folders hold the project's `obj/` and `bin/` for `Single`; :321-330 asserts the same two for `OddPaths` copied into a folder named `q;=%41 'é`, both projects of one framework, net10.0; `src/SqlSource/build/SqlSource.targets:52-59` escapes every folder with the same `Replace` chain whatever it holds, so a plain path is no separate branch; `FixtureProjectTests.Evaluate_ProjectWithOneFramework_GivesItsManifest` also checks the plain `obj/` in the evaluator's manifest).  measured (kills 23, alone 0, timeouts under it 1, the one at `src/SqlSource.Tool/Projects/ManifestReader.cs:171`, which the cover has under it as well and which the ManifestReader tests reach; the delete rests on reading, not on the numbers).  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: no record names the test; the comment at ManifestTargetTests.cs:68 points to it.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` | `ManifestTargetTests.WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders`
- proposal: Delete the test, saving one MSBuild start, and point the comment at ManifestTargetTests.cs:68 to `WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders`.
- verdict: confirmed
- verifier's reason: ManifestTargetTests.cs:259-268 (`Single`) and :321-330 (`OddPaths` copied into `q;=%41 'é`) make the same two assertions through `BuildFoldersOfAsync` (:32-37); both fixtures are plain net10.0 projects with no output paths of their own (Fixtures/Projects/Single/App.csproj, OddPaths/App.csproj), and SqlSource.targets:51-63 applies the same `NormalizeDirectory(...).Replace(...)` chain to every folder, a no-op on a plain path.  `kills` on tool-projects: the test has 23 kills, 3 survivors and the TIMEOUT at ManifestReader.cs:171:9; the cover has the same 23 kills, the same 3 survivors and the same TIMEOUT, so no timeout is under the test alone.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred for the method: no hit; the class is named by msbuild-and-manifest.md:152 and TD-0025/26/27 for other fixtures (`BuildHookFile`, `Multi`, `SecondOnly`, an SDK without a target), not for `Single`'s build folders.  The comment at :68 points to the test only as where the line's value is read, which the proposal repoints; it names no limit, regression or decision.

### F-SqlSource.Tool.Tests.Projects-1-06

- test: `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs` | `FixtureProjectTests.Evaluate_RestoredProjectWithoutThePackage_DoesNotUseSqlSource` | rows: all
- action: delete
- evidence: read (FixtureProjectTests.cs:104-112 copies `Plain`, writes its assets file and asserts `DoesNotUseSqlSource`; :117-125 does the same with the copy in the folder `Größe 日本語`, so it proves the plain case and the reading of a path outside ASCII as well).  measured (kills 42, alone 0; the cover kills 42 too).  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: no record names the test, which has no comment; only the plan `docs/superpowers/plans/2026-10-10-query-generation-phase-2-3-project-manifest.md` lists it.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs` | `FixtureProjectTests.Evaluate_RestoredProjectInAFolderWithLettersOutsideAscii_IsSeenAsRestored`
- proposal: Delete the test, which saves one real MSBuild evaluation; the test of the folder outside ASCII states the same outcome on the same fixture.
- verdict: confirmed
- verifier's reason: FixtureProjectTests.cs:104-112 and :117-125 copy the same fixture `Plain` (a net10.0 project with nothing of SqlSource), restore it, evaluate it through the same `EvaluateAsync` (:23-31) and assert the same `ProjectState.DoesNotUseSqlSource`; the second only puts the copy in `Größe 日本語`.  `kills` on tool-projects: both have 42 kills and the same two survivors (95:110, 404:22), no timeouts, and every kill is a line of the evaluation and of the state decision that a restored project without the package reaches.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: the method appears nowhere; TD-0026 names the class for `Multi` and `SecondOnly` only; the test has no comment.

### F-SqlSource.Tool.Tests.Projects-1-07

- test: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` | `OnlineBuildTests.Build_AutoWithADotEnvInAFolderAbove_RunsNothing` | rows: all
- action: delete
- evidence: read (OnlineBuildTests.cs:143-151 builds under auto with a `.env` above the project and asserts no run and no file of the online build; the row `("a .env above", null)` at :159 of `Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile` writes the same layout through `Lay` at :732-734, builds under auto, and asserts through :181-182 that the build did not go online; that auto without a connection source writes no file is asserted by `Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt` at :117-123, and the `.env` above is no connection source to the build, `SqlSourceHasEnvFile` being of the project's folder).  The scope mutates no target, so the measured kill of 1 says nothing.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: no record names it; `docs/superpowers/plans/2026-11-06-env-file-one-folder.md` added it beside the theory.  The leave-alone rule for "the builds of OnlineBuildTests" is of `EndToEnd/OnlineBuildTests.cs`, which starts the built tool, not of this file's stand-in.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` | `OnlineBuildTests.Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile` (row `"a .env above", null`, line 159); `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` | `OnlineBuildTests.Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt`
- proposal: Delete the test and move its comment, that the build looks for a `.env` in the project's folder alone, beside the theory's row.
- verdict: confirmed
- verifier's reason: OnlineBuildTests.cs:141-151 writes `.env` above the project and builds under auto with `NoVariables`; the row `("a .env above", null)` at :159 lays the same file through `Lay` (:732-734) and makes the same first build, and :172-180 require `wentOnline == (read is not null)` with `read` null, so a build that looked above would fail the row.  That auto with no connection source writes no online file is asserted by :115-122, and SqlSource.targets:439-443 builds `SqlSourceEnvFilePath` from `MSBuildProjectDirectory` alone, so a `.env` above changes no property the build reads.  The one kill (ProjectEvaluator.cs:305:85) is `Programs.Dotnet` starting the build and says nothing.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: the method appears nowhere; msbuild-and-manifest.md:102 states the rule ("No folder above the project's is looked in") without naming a test, and the plan docs/superpowers/plans/2026-11-06-env-file-one-folder.md:126, no record document, added the test beside the theory.  The comment at :140 states the rule and the proposal moves it beside the row.

### F-SqlSource.Tool.Tests.Projects-1-08

- test: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` | `OnlineBuildTests.Build_TrueWithoutAConnectionSource_RunsTheTool` | rows: all
- action: delete
- evidence: read (OnlineBuildTests.cs:233-239 builds with `-p:SqlSourceOnline=true` and no variable and asserts one run ending `--online true`; :277-283 builds with `SqlSourceOnline` set to ` TRUE ` and no connection source and asserts the same single run and the same last two arguments; `src/SqlSource/build/SqlSource.targets:422-426` trims and lowercases every value before comparing it, so `true` is no separate branch; `Build_OneFramework_CallsNoConfigurationOfItsOwnForTheRun` at :578-586 also runs the tool under `true` with no variable).  The scope mutates no target, so the measured kill of 1 says nothing.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: no record names it.  blind: no
- cover: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs` | `OnlineBuildTests.Build_OnlineTrueInCapitalsWithSpaces_RunsTheTool`
- proposal: Delete the test, saving one restore and build of the fixture.
- verdict: confirmed
- verifier's reason: OnlineBuildTests.cs:233-239 builds with `-p:SqlSourceOnline=true` and no connection source and asserts one run ending `--online true`; :277-283 sets `SqlSourceOnline` to ` TRUE ` with no connection source and asserts the same run and the same last two arguments.  SqlSource.targets:422-428 trims and lowercases every value, from the command line or a variable alike, so `true` is no separate branch.  Other tests run `-p:SqlSourceOnline=true` with `NoVariables` too: the second build of every row of `Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile` (:170-174, a run counted under true where auto ran none), `Build_TrueWithEnvFileNone_PassesEnvFileNone` (:183-196) and `Build_OneFramework_CallsNoConfigurationOfItsOwnForTheRun` (:578-586).  The one kill (ProjectEvaluator.cs:305:85) is `Programs.Dotnet` and says nothing.  Searched the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred: the method appears nowhere, and it has no comment.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-projects`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-18/tool-projects/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Projects.' 'Projects/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-18/tool-projects/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Projects.' 'Projects/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json artifacts/test-cleanup/phase-18/tool-projects/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json TransitivePackageAssembliesTargetTests.Run_PackageListedInAnotherCase_IsNotInTheValue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json ManifestTargetTests.WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json FixtureProjectTests.Evaluate_RestoredProjectWithoutThePackage_DoesNotUseSqlSource
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json OnlineBuildTests.Build_AutoWithADotEnvInAFolderAbove_RunsNothing
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-18/tool-projects/before/reports/mutation-report.json OnlineBuildTests.Build_TrueWithoutAConnectionSource_RunsTheTool
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-projects`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-18/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-18/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-18/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-18/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-18/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-18/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/msbuild-and-manifest.md`: 88, 114, 152
- `tests/README.md`: 18, 211
- `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`: 13
- `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`: 15
- `docs/tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md`: 23
- `src/SqlSource/build/AGENTS.md`: 35
- `src/SqlSource.Tool/AGENTS.md`: 23
- `tests/AGENTS.md`: 32

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
