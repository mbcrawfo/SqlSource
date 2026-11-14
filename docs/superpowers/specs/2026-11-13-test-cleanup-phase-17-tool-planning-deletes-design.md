# Test cleanup phase 17: tests/SqlSource.Tool.Tests/Planning, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Planning`
- Findings: 3 (3 delete)
- Kind: one kind, delete
- Changed lines: about 40
- Guard: mutation (tool-planning)

## Files

- `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`
- `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs`
- `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-3-01

- test: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs` | `FileTextTests.Read_FileThatIsThere_GivesItsText` | rows: all
- action: delete
- evidence: measured (covers 5 mutants and kills none) and read: it writes "SELECT 1;\n" through `TempFolder.WriteFile`, which is `File.WriteAllText`, UTF-8 without a mark (tests/SqlSource.Tool.Testing/TempFolder.cs:21), and asserts the text (FileTextTests.cs:19-25); `Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack` (FileTextTests.cs:33-42) reads a UTF-8 file without a mark too, with a letter outside ASCII, and asserts the same text and `CompilerFallsBack` besides.  No comment above it; its name and its class's are in no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` (only in a past plan, `docs/superpowers/plans/2026-10-23-td-0032-sql-file-not-utf8.md`, for the class); blind: no
- cover: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs` | `FileTextTests.Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack`
- proposal: Delete the test; the UTF-8 test without a mark already proves that a file on the disk gives its text, on a harder text.
- verdict: confirmed
- verifier's reason: `Read_FileThatIsThere_GivesItsText` (tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs:19-25) writes "SELECT 1;\n" through `TempFolder.WriteFile`, which is `File.WriteAllText`, UTF-8 with no mark (tests/SqlSource.Tool.Testing/TempFolder.cs:21), and asserts the text.  The cover (FileTextTests.cs:33-42) writes `Encoding.UTF8.GetBytes(Query)`, also UTF-8 with no mark, and asserts the text, which holds a letter outside ASCII, and `CompilerFallsBack` besides; both go through the one path of `FileText.Read` (src/SqlSource.Tool/Planning/FileText.cs:19-32).  `kills` in tool-planning: 0 kills, 0 timeouts, 0 survivors under it; the cover kills FileText.cs:26:62 and 26:90, which its assertion of `CompilerFallsBack` explains.  No comment above the test or its class; a grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither the test nor `FileTextTests`.  Not a test of a test library, and its subject is in the scope's source.

### F-SqlSource.Tool.Tests-3-02

- test: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs` | `AttributeReaderTests.Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest` | rows: all
- action: delete
- evidence: measured (kills 4, alone 0; cover `AttributeReaderTests.Read_AnyLangVersion_IsRead`) and read: the reader hands `manifest.DefineConstants` to the parse options whole and treats no constant apart (src/SqlSource.Tool/Planning/AttributeReader.cs:81-89), so `#if NET8_0_OR_GREATER` with that constant in the manifest (AttributeReaderTests.cs:407-418) is the row `new[] { "FEATURE" }, 1` of `Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant` (AttributeReaderTests.cs:401, 403-405) under another name.  Its comment (line 407) is about how the framework's constants reach the manifest, which is the manifest's behaviour and is proved by `ManifestReaderTests` (tests/SqlSource.Tool.Tests/Projects/ManifestReaderTests.cs:49) and `FixtureProjectTests` (tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs:61), outside this slice.  The name is in no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`, only in the past plan `docs/superpowers/plans/2026-10-10-query-generation-phase-2-4-work-list.md`; it records no limit or regression; blind: no
- cover: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs` | `AttributeReaderTests.Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant`
- proposal: Delete the test; the `#if` theory already proves that the reader reads under the manifest's constants, and the manifest's own tests prove that a framework's constants are among them.
- verdict: confirmed
- verifier's reason: The reader hands `manifest.DefineConstants` whole to `CSharpParseOptions` (src/SqlSource.Tool/Planning/AttributeReader.cs:81-89) and treats no constant apart, so `#if NET8_0_OR_GREATER` with that constant in the manifest (tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs:408-418) proves what the row `new[] { "FEATURE" }, 1` of `Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant` (lines 401, 403-405) proves, and that theory's `"OTHER"` row proves the converse besides.  `kills` in tool-planning: its 4 kills (AttributeReader.cs:98:49, 111:22, 154:13, 156:13) are all killed by the cover too, which has 5, and each is explained by a claim being read or not.  The comment above it (line 407) is about how a framework's constants reach the manifest, which `Evaluate_ProjectWithOneFramework_GivesItsManifest` asserts (`DefineConstants.ShouldContain("NET8_0_OR_GREATER")`, tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs:61) and `Read_ExampleOfTheSpec_GivesEveryMember` reads from a manifest's text (tests/SqlSource.Tool.Tests/Projects/ManifestReaderTests.cs:49); the test itself builds the manifest by hand and never runs that path.  TD-0031 (docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md) records the `#if` under another configuration, which this test does not show; the grep of the documents names neither the test nor `AttributeReaderTests`.

### F-SqlSource.Tool.Tests-3-03

- test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs` | `RunPlannerTests.Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput` | rows: all
- action: delete
- evidence: measured (kills 33, alone 0; cover `RunPlannerTests.Plan_FileClaimedByTwoProjects_TakesTheChainOfAProjectThatIsNotTheOwner`; `Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds` also kills 33 with this test as its cover) and read: both tests give the first project a settings file that says `sql` and that the second does not list, let the second claim the file with the default output, and assert the same two things, `ProjectPath` is the first's and the one query needs an entry (RunPlannerTests.cs:230-252 and 477-491).  The one difference is where the file sits, in the first project's folder or in `Shared/`, which the planner does not tell apart.  The two diverged only in the past: the plan `docs/superpowers/plans/2026-10-26-settings-files-layer-4-tool.md:163` gave the second project a property `models`, and the tool no longer reads properties.  The name is in no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`; the comment above it (lines 474-476) restates the same rule as the other's (lines 234-235); blind: no
- cover: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs` | `RunPlannerTests.Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds`
- proposal: Delete the test; the other proves that a file claimed by two projects is planned under the first and needs what the second's chain needs, and `Plan_FileClaimedByTwoProjects_TakesTheChainOfAProjectThatIsNotTheOwner` stays for a second chain that has a nearer file of its own.
- verdict: confirmed
- verifier's reason: Both tests (tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs:230-252 and 477-491) give the first project a listed settings file that says `output: sql` over the shared file, give the second project no settings file above that file, so its claim has the default output, and assert only `ProjectPath` is the first's and the one query `NeedsEntry`.  The one difference is that the settings file is the first project's own `sqlsource.json` (written by `TestProject.Settings`, tests/SqlSource.Tool.Testing/TestProject.cs:121-129) or a file of `Shared/` listed by `ListSettings`; both reach the planner as `Settings=` lines and go through the same `settingsFileSet.ChainOf(path)` (src/SqlSource.Tool/Planning/RunPlanner.cs:101-114), and neither assertion reads the level.  `kills` in tool-planning: the two tests have the same 33 kills and the same 7 survivors, line for line (diffed), and `Plan_FileClaimedByTwoProjects_TakesTheChainOfAProjectThatIsNotTheOwner` has the same 33 as well; no timeouts under any.  The comment above it (lines 474-476) states the rule that the cover's comment (234-235) states; the grep of the documents names neither test (only docs/tech-debt/TD-0089 names another test of the class, `Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry`).  No other finding of the slice proposes removing the named cover.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-planning`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-17/tool-planning/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Planning.' 'Planning/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-17/tool-planning/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Planning.' 'Planning/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-17/tool-planning/before/reports/mutation-report.json artifacts/test-cleanup/phase-17/tool-planning/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-17/tool-planning/before/reports/mutation-report.json FileTextTests.Read_FileThatIsThere_GivesItsText
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-17/tool-planning/before/reports/mutation-report.json AttributeReaderTests.Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-17/tool-planning/before/reports/mutation-report.json RunPlannerTests.Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-planning`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-17/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-17/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-17/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-17/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-17/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-17/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 22

## Left out

- F-SqlSource.Tool.Tests-3-07 (keep, `AttributeReaderTests.Read_FileWithoutTheWord_IsNotParsed`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests-3-08 (keep, `FileTextTests.SqlFileText_FileChangedAfterTheFirstRead_GivesTheFirstText`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests-3-09 (keep, `RunPlannerTests.Plan_RunThatWasCancelled_Throws`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
