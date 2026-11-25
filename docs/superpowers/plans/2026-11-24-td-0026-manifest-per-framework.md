# TD-0026: a manifest for each target framework - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A project with several target frameworks is evaluated and has a manifest for each framework, the plan of a run is the union of what they need, and the online build's one run reads every framework's manifest.

**Architecture:** `ProjectEvaluator` evaluates each framework of `TargetFrameworks` and runs `SqlSourceWriteManifest` under each that imports the package, so a `ProjectEvaluation` carries several manifests and `FoundProjects.Manifests` lists them flat, a project's frameworks next to each other.  `RunPlanner` already plans a file claimed by several manifests once, with the greatest output over its claimants, so it changes only where it counted per manifest what is per project.  In the build, the run's configuration asks each other framework's configuration of its own (`SqlSourceOnlineRun=true`) for its manifest and inputs, and passes every manifest to the tool as one more `--manifest`.

**Tech Stack:** C#, .NET 10, xUnit v3, Shouldly, MSBuild.

**Spec:** `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`.

## Where the code differs from the spec

- The spec says a project that references SqlSource for another framework alone is left out by the tool.  The online build of such a project fails today with `MSB4057: The target "SqlSourceRunOnline" does not exist` (checked on `main` with the fixture `SecondOnly`'s imports), since it calls the run in the first framework's configuration, which has no package.  This plan runs the online build in the first framework that imports the package, found by a probe target that only such a framework has.
- The spec does not say what a run does when one framework of a project fails.  A partial union would plan without that framework's claims and could delete a sidecar only it needs, so the whole project fails, as a project whose one evaluation fails does now.

## Global Constraints

- A project with one framework, or with `TargetFramework` set, costs what it costs now: the same two `dotnet msbuild` processes with the same arguments, and the online build's single-framework path unchanged.
- A query needs an entry when any framework's types claim it with an output that needs one.
- Two runs never undo each other's sidecars: the online build still runs the tool once.
- The manifest's format stays version 2; nothing is added to it.
- `--manifest` stays a build option; a tool older than the package refuses a second `--manifest` as a usage error, which the build already reports as `SQLSRC027` with the advice to update the tool.
- Resolving the item deletes its file, its row, every reference to `TD-0026`, the limit `limit-several-target-frameworks` in `site/reference/describing.md` with its row of `site/guides/troubleshooting.md`, and the sentences in `site/` that say the first framework alone is read.
- TD-0025 stays open; its text changes where it says the online build reads the first framework's manifest.

## Review Focus

- One framework's evaluation or target fails while another succeeds: the project is `SQLSRC205` and has no manifest, so nothing is planned or deleted for it.  Test in Task 1.
- A sidecar needed only by the second framework's query survives a run, by hand and online.  Tests in Tasks 2 and 4.
- A project folder whose path holds a space, `$` and `%`: the second `--manifest` reaches the tool whole.  Test in Task 4, through the fixture copy's folder name.
- A project that uses SqlSource for its second framework alone builds online, runs the tool once and gives that framework the created sidecar.  Test in Task 4.
- The same `[SqlSourceGenerate]` error in a C# file both frameworks compile is reported once.  Test in Task 2.

---

### Task 1: An evaluation and a manifest for each framework

**Files:**
- Modify: `src/SqlSource.Tool/Projects/ProjectEvaluator.cs`, `src/SqlSource.Tool/Projects/ProjectEvaluation.cs`, `src/SqlSource.Tool/Projects/RunProjects.cs`, `src/SqlSource.Tool/Projects/FoundProjects.cs`, `src/SqlSource.Tool/Logging/RunEvents.cs`, and every reader of `ProjectEvaluation.Manifest`
- Modify fixtures' comments: `tests/SqlSource.Tool.Tests/Fixtures/Projects/SecondOnly/App.csproj`, `.../Multi/App.csproj`
- Test: `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`, `FixtureProjectTests.cs`, `ManifestTargetTests.cs`, `RunProjectsTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`

**Interfaces:**
- Produces: `ProjectEvaluation.Manifests : ImmutableArray<ProjectManifest>` in place of `Manifest`, empty unless `UsesSqlSource`, in the order of `TargetFrameworks`; `FoundProjects.Manifests` holds every project's manifests in project order, a project's frameworks consecutive.  `ProjectEvaluator.Frameworks(string targetFrameworks) : ImmutableArray<string>` replaces `FirstFramework`: split at `;`, trimmed, empty entries dropped, distinct ignoring case, first kept.

- [ ] **Step 1:** Replace the TD-0026 tests of `ProjectEvaluatorTests` with: `Evaluate_ProjectWithSeveralFrameworks_EvaluatesEachAndWritesEachManifest` (`" net8.0 ; net10.0"`: five requests, the outer evaluation, then for each framework its evaluation with `-p:TargetFramework=<f>` and its target with the same switch, manifests `net8.0` then `net10.0`); `Evaluate_SqlSourceForTheSecondFrameworkAlone_WritesThatManifestAlone` (state `UsesSqlSource`, one manifest, `net10.0`, no target run for `net8.0`); `Evaluate_OneFrameworksTargetFails_FailsTheProjectWithNoManifest` (`Failed`, `Manifests` empty, `SQLSRC205`); `Evaluate_NoFrameworkImportsTheReference_IsNotRestored`.  Keep the one-framework test's two requests and arguments as they are.
- [ ] **Step 2:** Flip the fixture tests that pin TD-0026: `Multi` gives two manifests and only the `net10.0` one lists `Later/OnlyLater.sql` and `Later/OnlyLater.cs`; `SecondOnly` uses SqlSource with one `net10.0` manifest, in a solution run too.  Run them and see them fail.
- [ ] **Step 3:** Implement.  After the outer evaluation, a project with `TargetFramework` or with no framework in `TargetFrameworks` goes on as now.  Otherwise each framework is evaluated in turn and its state taken by `StateOf`; the project is `UsesSqlSource` when any framework is, else `NotRestored` when any is, else `DoesNotUseSqlSource`; the target runs for each framework that uses SqlSource, in order, into `<index>-<n>.manifest`; the first failure fails the project.  The assets file is the outer answer's.  Frameworks of one project run one after another.
- [ ] **Step 4:** `RunEvents.Project` writes one `project` event whose `targetFramework` is the manifests' frameworks joined by `;`, and whose `sqlFiles` and `compileFiles` count the distinct paths, ignoring case, over its manifests; `--verbose` says the same.  Update `RunLogTests` for a project of two frameworks.
- [ ] **Step 5:** Run `SqlSource.Tool.Tests` filtered to `Projects` and `Logging`; they pass.  Commit.

### Task 2: The plan is the union of the frameworks

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/Planning/RunPlanResult.cs`, `src/SqlSource.Tool/PlanSteps.cs`
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs` (or the planner test class that exists), `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs` if it builds manifests by hand

**Interfaces:**
- Consumes: manifests of one project are consecutive in the array (Task 1).
- Produces: `RunPlanResult.Unclaimed` holds one count for each distinct project, in the order of first appearance, as `(string ProjectPath, int Files)`; `PlanSteps` logs `RunEvents.Unclaimed` once for each.

- [ ] **Step 1:** Write tests with two manifests of one project, `net8.0` and `net10.0`: `Plan_FileClaimedForTheSecondFrameworkAlone_IsPlanned`; `Plan_QueryNeedsAnEntryForOneFrameworkAlone_NeedsAnEntry` (the `net10.0` chain's settings file gives `output` `model`, the `net8.0` one `sql`); `Plan_FileListedByBothFrameworks_IsPlannedOnceWithEachClaimOnce` (`Claims` has one entry for a claim both compile); `Plan_SourceNotALiteralInAFileOfBothFrameworks_ReportsSQLSRC208Once`; `Plan_UnclaimedFiles_AreCountedOncePerProject` (a file listed by both and claimed by neither counts 1, one claimed by either counts 0).
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement: count unclaimed over the union of a project's listed paths less the union of its claimed ones; add a project's claim errors only once each (`ToolDiagnostic` equality); make `ClaimsOf` distinct.  A run with one manifest for each project plans as before.
- [ ] **Step 4:** Run `SqlSource.Tool.Tests`; it passes.  Commit.

### Task 3: `--manifest` given once for each framework

**Files:**
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/PlanSteps.cs`, `src/SqlSource.Tool/Projects/RunProjects.cs`, `src/SqlSource.Tool/UsageCheck.cs` if it counts the option
- Test: `tests/SqlSource.Tool.Tests/Commands/` (the `--manifest` tests), `tests/SqlSource.Tool.Tests/Projects/RunProjectsTests.cs`, `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`

**Interfaces:**
- Produces: `--manifest` is `Option<string[]>`, one path per occurrence, any number of occurrences, read with `SharedOptions.ReadsAs`.  `PlanSteps.PlanUnitAsync` reads each with `RunProjects.FromManifestAsync`, in order; the unit is the first manifest's project.

- [ ] **Step 1:** Tests: `Describe_TwoManifestsOfOneProject_PlansBoth` (a file listed in the second alone gets its sidecar); `Describe_ManifestsOfTwoProjects_IsSQLSRC206` (reason `it is of '<second project>', and the first manifest is of '<first project>'`, the run plans nothing); `Describe_OneOfTwoManifestsCannotBeRead_PlansNothing`; and the usage tests: `--manifest a --manifest b` passes, each value checked by `ProblemOfManifestPath`.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement.  Any manifest that cannot be read, or names another project than the first, is reported and the run plans nothing.  Update the option's description: the manifest of each of the project's frameworks.
- [ ] **Step 4:** Run `SqlSource.Tool.Tests`; it passes.  Commit.

### Task 4: The online build reads every framework's manifest

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets`, `src/SqlSource/buildMultiTargeting/SqlSource.targets`, `tests/SqlSource.Tool.StandIn/Program.cs`
- Modify fixture: `tests/SqlSource.Tool.Tests/Fixtures/Projects/OnlineMulti/` (a `.sql` file and a claim listed for `net10.0` alone under a property, and imports for `net10.0` alone under `OnlineMultiSecondOnly=true`)
- Test: `tests/SqlSource.Tool.Tests/Projects/OnlineMultiTargetTests.cs`, `tests/SqlSource.Tests/Package/BuildFileTests.Online.cs`, `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Consumes: the tool takes several `--manifest` (Task 3).
- Produces, in `build/SqlSource.targets`:
  - `SqlSourceOnlineFramework`, no body, `Returns="$(TargetFramework)"`: a framework that imports the package has it.
  - `SqlSourceFrameworkOnlineInputs`, `DependsOnTargets="SqlSourceWriteManifest"`, returns this framework's manifest with `SqlSourceOnlineKind=Manifest` and `SqlSourceToolArgument` = ` --manifest "<path escaped for the shell>"`, its `.sql` items by full path with `SqlSourceOnlineKind=Sql`, and its `sqlsource.json` and `Compile` items with `SqlSourceOnlineKind=Input`.
  - `SqlSourceWriteFrameworkManifests`, in `SqlSourceRunOnline`'s `DependsOnTargets` between the manifest and `SqlSourceListSidecars`: when `TargetFrameworks` is set, calls `SqlSourceFrameworkOnlineInputs` in each other framework's configuration `TargetFramework=<f>;SqlSourceOnlineRun=true`, with `SkipNonexistentTargets="true"`.
  - `SqlSourceShellSpecial` and `SqlSourceShellEscape` move from `SqlSourceRunOnline` to the online `PropertyGroup`.
  - `SqlSourceFirstFramework` is removed from both files; the configuration of the run is the first framework, in `TargetFrameworks` order, whose `SqlSourceOnlineFramework` answers.

- [ ] **Step 1:** The stand-in records every `--manifest`'s lines, in order (`StandInRun.Manifests`).  Tests: `Build_EveryFramework_GivesTheToolEachFrameworksManifest` (one run, two manifests, `net8.0` first); `Build_AFileOfTheSecondFrameworkAlone_GivesItsCreatedSidecarToThatCompiler` (and not to `net8.0`'s); `Build_SqlSourceForTheSecondFrameworkAlone_RunsTheToolOnceForIt` (exit 0, one run, one `net10.0` manifest, under `-f net8.0` too); `Build_ChangedFileOfTheSecondFrameworkAlone_RunsTheToolAgain` (a second build after editing a `Compile` file of `net10.0` alone runs again); `Build_ProjectFolderWithShellCharacters_GivesTheToolEveryManifest` (the copy under a folder named `a $b %c`); `Clean_DeletesEveryFrameworksManifest`.  Keep the existing tests' expectations of one run.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** Implement the targets above.  `SqlSourceDescribeOnline` and `SqlSourceRunOnlineBeforeDispatch` each probe `SqlSourceOnlineFramework` over the frameworks with `SkipNonexistentTargets="true"` and call the run, and `SqlSourceCreatedSidecars`, in the first answer's configuration, and call nothing when none answers.  `SqlSourceListSidecars` adds the other frameworks' sidecars of `.sql` files not already listed, their manifests and inputs to `SqlSourceOnlineInput`.  The `Exec` appends every `SqlSourceToolArgument`.  `SqlSourceOnlineFile` adds the other manifests, for `FileWrites`.  A project with no `TargetFrameworks` runs exactly as now.
- [ ] **Step 4:** Update `BuildFileTests` for the moved and removed properties, the new targets and the outer build's text.  Run `SqlSource.Tests` filtered to `Package` and `SqlSource.Tool.Tests` filtered to `Online`; they pass.  Commit.

### Task 5: Documents and the item

**Files:**
- Delete: `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`
- Modify: `docs/tech-debt/README.md`, `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md` and its row, `docs/architecture/msbuild-and-manifest.md`, `docs/architecture/tool.md`, `src/SqlSource/build/AGENTS.md`, `src/SqlSource.Tool/Projects/AGENTS.md` if a rule changes, `site/reference/describing.md`, `site/reference/online-build.md`, `site/reference/msbuild.md`, `site/reference/cli.md`, `site/diagnostics.md` (`SQLSRC204`, `SQLSRC206`), `site/guides/troubleshooting.md`, `tools/README.md` and `tools/check-package-install.sh` where they say the first framework, and every comment that names TD-0026

- [ ] **Step 1:** Remove the limit and its troubleshooting row; say in `online-build.md` that the run reads every framework's manifest and runs in the first framework that uses SqlSource; drop the `SQLSRC204` bullet about the first framework; add the `SQLSRC206` cause of manifests of two projects.
- [ ] **Step 2:** Rewrite the architecture pages' evaluation, online build, mermaid and known limits; the build `AGENTS.md` rule "once, for the first framework"; TD-0025's problem and fix paragraphs.  `git grep -n "TD-0026\|first framework"` shows nothing stale.
- [ ] **Step 3:** Run `./format.sh`, then `./pre-commit-validation.sh --full`; it passes.  Commit.
