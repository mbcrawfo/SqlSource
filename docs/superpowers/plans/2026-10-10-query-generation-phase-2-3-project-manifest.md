# Query generation, phase 2.3: the project manifest and discovery - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver how the `sqlsource` tool learns what the compiler sees: a file that a target of the package writes for a project, the project manifest, and the tool's side that finds the projects of a run, has MSBuild write that file for each, and reads it.  `describe` then finds its projects and reads their manifests, and describes nothing yet.

**Architecture:** `build/SqlSource.props` sets `SqlSourceImported`, and `build/SqlSource.targets` gains `SqlSourceWriteManifest`, a target that hooks nothing and writes lines of `key=value`.  In the tool, `ProjectEvaluator` runs `dotnet msbuild` two or three times for a project through `ToolHost.Processes`, an `IProcessRunner`: an evaluation that prints JSON, then the target, which writes to a file in a temporary folder of the run.  `ManifestReader` reads that file into a `ProjectManifest`.  `SolutionReader` lists a solution's projects, and `RunProjects` says which projects a run is on, from the unit and `--project`, and reports what cannot be read.  An evaluation reports nothing itself: several run at once, and their errors are reported in the order of the projects.

**Tech Stack:** MSBuild in `src/SqlSource/build/`; C# on `net8.0` in `src/SqlSource.Tool`, with `System.Text.Json` for the JSON of an evaluation, `System.Diagnostics.Process`, and `Microsoft.VisualStudio.SolutionPersistence` 1.0.52 for `.sln` and `.slnx`; xunit v3 on Microsoft.Testing.Platform and Shouldly in the test projects on `net10.0`; the real `dotnet msbuild` of the SDK that `global.json` pins, 10.0.401, in the fixture tests; bash for `tools/check-package-install.sh`.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md`](../specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md).  Read it first; this plan argues from it.  The [epic outline](../specs/2026-10-07-query-generation-epic-design.md) holds what the later sub-phases build on this one: its sections "Packages and repository", "Workflow" and "Phase 2".

## Global Constraints

- Branch: `claude/query-generation-phase-2-3-project-manifest`, which exists and holds the reviewed spec and this plan.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running.  Run `./format.sh`, then the validation as its own command, fix what it reports, then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it, and keep those words out of any other command.
- **All the code of this plan was compiled, formatted and tested in a scratch copy of the repository**, and `./pre-commit-validation.sh` passed on the state that each of tasks 2 to 8 leaves.  Type it as it stands.  If a rule still objects, change the code to satisfy it; never suppress a rule, and never edit `.editorconfig`.
- `src/SqlSource` stays on `netstandard2.0` and Roslyn 4.8.0 and takes no new package reference.  Its package holds the same files as before; only the two files under `build/` change.
- The tool targets `net8.0`.  Do not raise it, and do not use an API that .NET 8 lacks.
- Nothing in the tool reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`.  Every process is started through `ToolHost.Processes`, and a temporary folder is made in `ToolHost.TempDirectory`.
- **The tool never restores and never builds a project, and writes nothing into one.**  No `-restore`, no target of a build, no file under a project's folder.
- **MSBuild's output is parsed only as the JSON of `-getProperty` and `-getItem`.**  No test compares a text that MSBuild or the solution library wrote, beyond an error's code and a file's name: both write in the language of the machine.
- Every MSBuild property of the package starts with `SqlSource`, and so does every item type the new target makes.  A property of the SDK that the files read is listed in `SdkProperties` in `tests/SqlSource.Tests/Package/BuildFileTests.cs`.
- The manifest is version 1 of its format, as the spec's table has it: `SqlSourceManifest`, `Project`, `TargetFramework`, `LangVersion`, `DefineConstants`, `Property.<Name>`, `File`, `File.<Name>`, `Compile`.  The ten names are `SqlSourceDialect`, `SqlSourceDatabase`, `SqlSourceOutput`, `SqlSourceGeneratorParameters`, `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix`, `SqlSourceModelNamespace`, `SqlSourceInputModelType`, `SqlSourceOutputModelType` and `SqlSourceCollectionType`.
- The new diagnostics are `SQLSRC204`, `SQLSRC205`, `SQLSRC206`, `SQLSRC207`, `SQLSRC220` and `SQLSRC223`, with the titles and messages of the spec's table.  Each touches four places: the descriptor and `All` in `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, and a test.  `All`, the release file and the document are in the order of the ids.
- Exit codes: `0` success; `1` an error was reported, a wrong command line, or a cancelled run.  `Cli.RunAsync` takes `1` from `Reporter.Count`: a command reports and goes on, and never returns `1` itself.
- A file is at most 120 characters wide, apart from Markdown: `tools/editorconfig-checker.sh` rejects a longer line.  Shell scripts are formatted by `tools/shfmt.sh`.
- A file this plan gives in full ends with one line break, which the block that shows it does not.
- A diff in this plan is against the file as the task before left it.  Apply it by hand or save it and use `git apply`; either way read `git diff` afterwards.  A diff of a Markdown file has long lines: each `-` line is one whole paragraph or table row of the file.
- Lock files are written by `dotnet restore SqlSource.slnx`, never by hand, and are committed.
- Prose uses two spaces after a full stop, as the repository does.  Files under `docs/superpowers/` are not rewritten, apart from the epic outline in tasks 1 and 8.
- `README.md` at the root does not change.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Before the pull request: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  When this plan was written there was no `v*` tag and nothing to compare.  If there is one now and `VersionPrefix` in `Directory.Build.props` is not greater, ask the owner for the new version.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class of the tool's tests:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ManifestReaderTests
```

`ManifestTargetTests` and `FixtureProjectTests` start the real `dotnet msbuild`, a second or two for each test.  They need nothing but the SDK.

## Where this plan departs from the spec

The spec is the intent; these are the places the plan reads it more exactly or differently.

1. **`ToolHost` gains three members, not one**: `Processes`, and with it `TempDirectory` and `ProcessorCount`.  The spec's tests of the temporary folder and of "eight at a time" need both to be a test's to give, and the rule of `src/SqlSource.Tool/AGENTS.md` is that what comes from outside comes through the host.
2. **An evaluation reports nothing.**  `ProjectEvaluator` gives a `ProjectEvaluation` with a `ProjectState`: `UsesSqlSource`, `DoesNotUseSqlSource`, `NotRestored` or `Failed`.  `RunProjects`, in task 6, turns a state into `SQLSRC204` or `SQLSRC220`, since only it knows whether a project was asked for by name.  `SQLSRC205` and `SQLSRC206` travel in the evaluation as a `ToolDiagnostic`.
3. **`SQLSRC205` has one line before MSBuild's**, labelled `reason`: the exit code, that the output was no JSON, or that the target left no file.  Without it an error whose run printed nothing says nothing.
4. **A `dotnet` that cannot be started is `SQLSRC205`**, with the reason `dotnet could not be started` and the system's message.  `ProcessRunner` gives it as the exit code `ProcessResult.NotStarted`, so it does not reach the general catch as `SQLSRC200`.
5. **A solution that lists one project twice is `SQLSRC223`**, where the spec says it is one project.  `Microsoft.VisualStudio.SolutionPersistence` refuses such a file, under one spelling, under two that differ by case, and through `..`: the probe of task 6 ran all three.  So `SolutionReader` has nothing to make distinct.  A project that `--project` names twice is still named once.
6. **`ManifestReader.Read(text, out reason)`** gives the manifest or null.  A text with no line has the reason `it is empty`, which the spec's list of four does not have.  A name under `Property.` or `File.` is kept whatever it is, so that sub-phase 2.4 reads the ones it knows.
7. **The fixture tests are two classes.**  `ManifestTargetTests`, in task 3, runs the target with a `dotnet msbuild` of its own and looks at the lines of the file: the tool's side does not exist yet.  `FixtureProjectTests`, in task 5, runs the tool's two runs.  The fixtures `SecondOnly`, `Plain`, `StaleRestore` and `InSolution` show what the evaluation does, so they come with task 5.
8. **`TD-0026` is written in task 3 and gains its links in task 5**, when the files it links exist.  A fourth item, `TD-0028`, records that nothing of this was run on Windows: the spec asks for the check of `DOTNET_CLI_FORCE_UTF8_ENCODING` or for the gap in `docs/tech-debt`, and every workflow of the repository runs on `ubuntu-latest`.
9. **With `--project`, the run is the named projects that are of the unit**, for a project as the unit too: `describe App.csproj --project Other.csproj` reports `SQLSRC207` and asks MSBuild nothing.  An empty value, `--project "$UNSET"`, is `SQLSRC207` with `''`.
10. **`SQLSRC204`, `SQLSRC205`, `SQLSRC206` and `SQLSRC220` start with the project file, and `SQLSRC223` with the solution file**, as an error about a file does.  `SQLSRC207` starts with `sqlsource`: its path may be no file.
11. **`tools/check-package-install.sh` checks two things beyond the spec's four**: that the installed project has `SqlSourceImported`, which is the one place in the repository that shows NuGet importing the props, and the property that `Directory.Build.targets` writes over four lines.
12. **`ProcessRunnerTests` runs `env`, `sleep` and `sort`**, or their Windows forms, beside `dotnet`: nothing else shows a variable removed, a process killed, or an input closed.
13. **`describe` says so when no project uses SqlSource only when it reported no error.**  A run whose one project failed has said enough.

## What was run while this plan was written

Facts the code of the plan rests on, each run on macOS with SDK 10.0.401.

- `dotnet msbuild` with `MSBuildSDKsPath` set to a folder that does not exist fails to resolve `Microsoft.NET.Sdk`: the variable is used in place of the SDK's own.  A process that MSBuild starts has it and `MSBuildExtensionsPath`.  A test process under `dotnet test` has neither, and no `DOTNET_HOST_PATH`.
- `-p:SolutionDir=/x/sln, inc;x/` is `MSB1006`.  With `%2C`, `%3B` and `%25` the value arrives whole, and a project that imports `$(SolutionDir)Shared.props` evaluates in a folder named `Acme, Inc;100%`.
- An evaluation of a project that was never restored works, and so does the target with `AddImplicitDefineConstants`.  Neither creates `obj`.
- MSBuild writes a warning or an error of an evaluation to its error output, and the JSON alone to its standard output, also when a `Directory.Build.rsp` asks for `-v:diag`.
- The solution library gives `SolutionException` for a `.sln` that is none, `System.Xml.XmlException` for a `.slnx` that is no XML, and a `.sln`'s `\` paths with the separator of the system.  It finds a serializer for `APP.SLNX` and none for another extension.
- Every test of the tool passed three times in a row, the ones that bound what runs at once among them.

## Review Focus

Inputs the spec implies and does not name.  Each has a test in the task that owns the code.

1. **A project whose evaluation warns**, a file imported twice for one.  MSBuild's warning is on its error output and the project is read all the same.  `Evaluate_ProjectWhoseEvaluationWarns_IsReadAllTheSame` in task 5.
2. **A project file that a solution lists and the disk does not have**, as after a branch was switched.  `SQLSRC205` with MSBuild's line for that project, and the rest of the solution is read.  `Evaluate_ProjectFileThatDoesNotExist_IsSqlsrc205WithMSBuildsError` in task 5; `Find_SolutionWithProjectsThatFail_ReportsThemInTheOrderOfTheProjects` in task 6.
3. **A `Directory.Build.rsp` beside the project**, which MSBuild reads by itself.  What it sets applies to the tool's runs as to a build, and a verbosity it asks for puts nothing before the JSON.  `Evaluate_ProjectWithAResponseFile_IsReadWithWhatTheFileSets` in task 5.
4. **Two runs of the tool at one time on one project**, from two terminals or an editor and a terminal.  Each has a folder of its own for its manifests.  `Evaluate_TwoRunsAtOnceOnOneProject_DoNotShareAManifestFile` in task 5.
5. **`--project` given a folder, the solution itself, a project of another language, or nothing**, `--project "$UNSET"`.  Each is `SQLSRC207` with the full path the tool looked at, and never a run on everything.  `Find_NamedPathThatIsNoCSharpProjectOfTheSolution_IsSqlsrc207` and `Find_EmptyNamedPath_IsSqlsrc207AndNotTheWorkingDirectory` in task 6.

## File Structure

| File | Is |
|----|----|
| `src/SqlSource/build/SqlSource.props` | Gains `SqlSourceImported` |
| `src/SqlSource/build/SqlSource.targets` | Gains `SqlSourceManifestDependsOn` and the target `SqlSourceWriteManifest` |
| `src/SqlSource/Diagnostics/ToolDiagnostics.cs` | Gains six descriptors |
| `src/SqlSource.Tool/ToolHost.cs` | Gains `Processes`, `TempDirectory` and `ProcessorCount` |
| `src/SqlSource.Tool/Processes/` | `IProcessRunner`, `ProcessRequest`, `ProcessResult`, and `ProcessRunner`, the only code that starts a process |
| `src/SqlSource.Tool/Projects/ProjectManifest.cs`, `ManifestFile.cs` | What a manifest holds |
| `src/SqlSource.Tool/Projects/ManifestReader.cs` | Reads the text of a manifest |
| `src/SqlSource.Tool/Projects/MSBuildProperty.cs` | A `-p:` switch with its value escaped |
| `src/SqlSource.Tool/Projects/ProjectEvaluator.cs` | The runs of MSBuild for a project, and the table of whether it uses SqlSource |
| `src/SqlSource.Tool/Projects/ProjectState.cs`, `ProjectEvaluation.cs` | What an evaluation found |
| `src/SqlSource.Tool/Projects/SolutionReader.cs`, `SolutionProjects.cs` | The C# projects of a solution |
| `src/SqlSource.Tool/Projects/RunProjects.cs` | The projects of a run, and what is reported of each |
| `src/SqlSource.Tool/Cli.cs` | `describe` gains `--project` and runs the above |
| `tests/SqlSource.Tool.Tests/Fixtures/Projects/` | Ten small projects that the tests run `dotnet msbuild` on |
| `tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs` | Copies a fixture out of the repository |
| `tests/SqlSource.Tool.Tests/FakeProcessRunner.cs`, `FakeProject.cs` | Answer in MSBuild's place.  Sub-phase 2.4 builds its `TestProject` on them |
| `tests/SqlSource.Tool.Tests/Hosts.cs` | A `ToolHost` for a test of a part of the tool |
| `tools/check-package-install.sh` | Gains the run of the target on the installed project |
| `docs/tech-debt/TD-0025` to `TD-0028` | What the manifest does not see, the SDK's target, and Windows |

---

### Task 1: Start the sub-phase

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
- Add to the commit: `docs/superpowers/plans/2026-10-10-query-generation-phase-2-3-project-manifest.md` (this plan)

- [ ] **Step 1: See that the branch holds the spec**

```bash
git branch --show-current
```

Expected: `claude/query-generation-phase-2-3-project-manifest`.

```bash
git log --oneline -3
```

Expected: the commit `Amend the spec of phase 2.3 after its review` is among them.  If the plan is not committed yet, it is an untracked file, and step 4 adds it.

- [ ] **Step 2: Set the row of 2.3 to In progress**

Change `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

```diff
--- a/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
+++ b/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
@@ -64,7 +64,7 @@ Decided:
 | 1. Parameters and settings | Done | [query-generation-phase-1-parameters-and-settings-design](2026-10-08-query-generation-phase-1-parameters-and-settings-design.md) | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL; every new marker, setting, enum and generator parameter of the epic is parsed and validated with no effect yet.  A user sees four changes: `SqlSourceTokenValidation` gives way to `SqlSourceGeneratorParameters`; `token-validation` and `token-ignore=` are no longer generator parameters, and `-- token-ignore:` is a marker; a query's generator parameters replace the preamble's; and `{{a:b}}` is a token with a default, where it was text. |
 | 2.1 The snapshot format | Done | [query-generation-phase-2-1-snapshot-format-design](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | In the generator assembly: the model of a sidecar, the hand-written reader and writer, the two comparisons the format design defines, and the JSON Schema under `schemas/`.  No tool, and no step of the generator reads a sidecar yet. |
 | 2.2 The tool's shell and its package | Done | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
-| 2.3 The project manifest and discovery | Not started | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
+| 2.3 The project manifest and discovery | In progress | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
 | 2.4 The work list | Not started | [query-generation-phase-2-4-work-list-design](2026-10-09-query-generation-phase-2-4-work-list-design.md) | The syntax-level attribute reader; the plan of a run from the shared path resolver, parser, settings and hash: which queries need an entry, their databases, and which are selected by `--database` and a `.sql` path. |
 | 2.5 Describe | Not started | [query-generation-phase-2-5-describe-design](2026-10-09-query-generation-phase-2-5-describe-design.md) | The describer's interfaces and registry with no engine registered; the exchange's interface with its live shape; connections; the skip rule, the sidecar-written rule, `--force`, and the summary. |
 | 2.6 `--check` and logging | Not started | [query-generation-phase-2-6-check-and-logging-design](2026-10-09-query-generation-phase-2-6-check-and-logging-design.md) | `describe --check` with its comparison and its exit code; `--verbose`; `--log` and `SQLSOURCE_LOG` with the run's events.  Closes phase 2. |
```

- [ ] **Step 3: Validate**

Run: `./pre-commit-validation.sh`
Expected: every line of the summary says `passed`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-10-query-generation-phase-2-3-project-manifest.md
```

```bash
git commit -q -F - <<'EOF'
Start phase 2.3 of query generation: the project manifest

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 2: `SqlSourceImported`

**Files:**
- Modify: `src/SqlSource/build/SqlSource.props`
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the MSBuild property `SqlSourceImported`, `true` in every project that the package's props reach.  Task 5 asks a project for it with `-getProperty:SqlSourceImported`.

- [ ] **Step 1: Write the failing test**

Change `tests/SqlSource.Tests/Package/BuildFileTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Package/BuildFileTests.cs
+++ b/tests/SqlSource.Tests/Package/BuildFileTests.cs
@@ -46,6 +46,18 @@ public partial class BuildFileTests
 
     private static readonly XDocument Targets = Load("SqlSource.targets");
 
+    // The sqlsource tool evaluates a project and asks for this property to find out whether the project uses
+    // SqlSource.  A condition would make a project that uses it look like one that does not.
+    [Fact]
+    public void Props_Marker_SaysThatTheProjectUsesSqlSourceInEveryProject()
+    {
+        var marker = Props.Descendants("SqlSourceImported").ShouldHaveSingleItem();
+
+        marker.Value.ShouldBe("true");
+        marker.Parent.ShouldNotBeNull().Name.LocalName.ShouldBe("PropertyGroup");
+        ConditionsAround(marker).ShouldBeEmpty();
+    }
+
     [Fact]
     public void Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject()
     {
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Package.BuildFileTests`
Expected: `Props_Marker_SaysThatTheProjectUsesSqlSourceInEveryProject` fails: `ShouldHaveSingleItem` found no element.  Every other test of the class passes.

- [ ] **Step 3: Set the property**

Change `src/SqlSource/build/SqlSource.props`:

```diff
--- a/src/SqlSource/build/SqlSource.props
+++ b/src/SqlSource/build/SqlSource.props
@@ -1,4 +1,13 @@
 <Project>
+    <!--
+        Says that the project uses SqlSource.  The sqlsource tool asks a project for this property and for nothing
+        else to find that out, so it is set with no condition, and here: these props reach a project whether the
+        package came from NuGet or its files are imported by path.  A project that was never restored has no props
+        of a package, and so no marker.
+    -->
+    <PropertyGroup>
+        <SqlSourceImported>true</SqlSourceImported>
+    </PropertyGroup>
     <!--
         Hands every .sql file of the project to the compiler, which is the only way a source generator can see one.
         NuGet imports this file into each project that references the package, before the project's own content, so a
```

- [ ] **Step 4: Run the test to see it pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Package.BuildFileTests`
Expected: every test passes.  The test project copies the file to its output when it builds, and `dotnet test` builds first.

`BuildFile_EveryPropertyOfThePackage_StartsWithSqlSource` reads the new property too, and passes because of its name.

- [ ] **Step 5: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Mark a project that uses SqlSource

build/SqlSource.props sets SqlSourceImported, with no condition.  The
sqlsource tool asks a project for it to find out whether the project uses
the package.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 3: The target `SqlSourceWriteManifest`

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets`
- Modify: `tests/SqlSource.Tests/Package/BuildFileTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Projects/` with the fixtures `Single`, `Multi`, `LateFile`, `BuildHookFile`, `OwnFiles` and `OddPaths`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs`
- Create: `tests/SqlSource.Tool.Tests/ManifestTargetTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
- Create: `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`, `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`, `docs/tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md`
- Modify: `docs/tech-debt/README.md`, `docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md`, `src/SqlSource/AGENTS.md`, `CONTRIBUTING.md`

**Interfaces:**
- Consumes: `SqlSourceImported` is not needed here; the target needs the two trims that the file has.
- Produces: the target `SqlSourceWriteManifest`, run as `dotnet msbuild <project> -t:SqlSourceWriteManifest -p:SqlSourceManifestFile=<file>`, which writes version 1 of the manifest.
- Produces, for the tests of tasks 5 and 6: `FixtureProjects` in the namespace `SqlSource.Tool.Tests.Fixtures`, with `string Root`, `string Copy(string fixture, string file = "App.csproj")`, `string PathOf(string relativePath)`, `string WriteFile(string relativePath, string content = "")`, `string[] WriteManifest(string project, params string[] properties)` and `static (int ExitCode, string Output) RunMSBuild(string project, IEnumerable<string> arguments)`.  Task 5 adds a third parameter to `Copy`.

- [ ] **Step 1: Write the failing tests of the build file**

Change `tests/SqlSource.Tests/Package/BuildFileTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Package/BuildFileTests.cs
+++ b/tests/SqlSource.Tests/Package/BuildFileTests.cs
@@ -23,8 +23,15 @@ public partial class BuildFileTests
         "DefaultExcludesInProjectFolder",
         "IntermediateOutputPath",
         "MSBuildProjectFile",
+        "MSBuildProjectFullPath",
+        "TargetFramework",
+        "LangVersion",
+        "DefineConstants",
     ];
 
+    // The target that writes the project manifest for the sqlsource tool.  It is the one target no build runs.
+    private const string ManifestTarget = "SqlSourceWriteManifest";
+
     // What the generator reads: each as a property of the project and as metadata of a file's item.
     private static readonly string[] Settings =
     [
@@ -92,19 +99,146 @@ public partial class BuildFileTests
     // file.  A trim that runs before it sees a value wherever it was set, in Directory.Build.targets or by another
     // target, and runs in every build that writes the file, a design-time build too.  A trim outside a target would
     // see only what is set before NuGet imports the file.  The target that collects the files with metadata runs
-    // there for the same reasons.
+    // there for the same reasons.  The target that writes the manifest is the one that is not of a build: see
+    // Targets_ManifestTarget_HooksNothing.
     [Fact]
-    public void Targets_EveryTarget_RunsBeforeTheBuildWritesTheFileTheCompilerReads()
+    public void Targets_EveryTargetOfABuild_RunsBeforeTheBuildWritesTheFileTheCompilerReads()
+    {
+        var targets = Targets
+            .Root.ShouldNotBeNull()
+            .Elements("Target")
+            .Where(target => target.Attribute("Name")!.Value != ManifestTarget)
+            .ToList();
+
+        targets.Count.ShouldBe(4);
+        targets
+            .Select(target => target.Attribute("BeforeTargets")?.Value)
+            .ShouldAllBe(before => before == "GenerateMSBuildEditorConfigFileCore");
+        targets.SelectMany(ConditionsAround).ShouldBeEmpty();
+    }
+
+    // A target's DependsOnTargets is read before the target runs, so the list that the manifest target depends on
+    // is a property set where the file is imported.  Nothing else is outside a target, and the file has no other
+    // condition outside one: a second such element would be read by every project, in every build.
+    [Fact]
+    public void Targets_Root_HoldsTargetsAndTheListThatTheManifestTargetDependsOn()
     {
         var root = Targets.Root.ShouldNotBeNull();
 
-        root.Elements().ShouldAllBe(element => element.Name.LocalName == "Target");
-        root.Elements()
-            .Select(target => target.Attribute("BeforeTargets")?.Value)
-            .ShouldAllBe(before => before == "GenerateMSBuildEditorConfigFileCore");
-        root.Elements().SelectMany(ConditionsAround).ShouldBeEmpty();
+        var group = root.Elements().Where(element => element.Name.LocalName != "Target").ShouldHaveSingleItem();
+        group.Name.LocalName.ShouldBe("PropertyGroup");
+        group.Attributes().ShouldBeEmpty();
+
+        var lines = group.Elements().ToList();
+        lines
+            .Select(line => line.Name.LocalName)
+            .ShouldBe(["SqlSourceManifestDependsOn", "SqlSourceManifestDependsOn"]);
+        lines[0].Attributes().ShouldBeEmpty();
+        lines[0].Value.ShouldBe("SqlSourceTrimProperties;SqlSourceTrimMetadataOfFiles");
+        // A project that is not of the SDK has no TargetFramework, and no such target to depend on.
+        lines[1].Attributes().ShouldHaveSingleItem().Name.LocalName.ShouldBe("Condition");
+        lines[1].Attribute("Condition")!.Value.ShouldBe("'$(TargetFramework)' != ''");
+        lines[1].Value.ShouldBe("$(SqlSourceManifestDependsOn);AddImplicitDefineConstants");
     }
 
+    // The tool runs the target by name.  A hook would make every build write the manifest, and a condition would
+    // make a run of the tool find no file.
+    [Fact]
+    public void Targets_ManifestTarget_HooksNothing()
+    {
+        var target = Manifest();
+
+        target
+            .Attributes()
+            .Select(attribute => attribute.Name.LocalName)
+            .ShouldBe(["Name", "DependsOnTargets"], ignoreOrder: true);
+        target.Attribute("DependsOnTargets")!.Value.ShouldBe("$(SqlSourceManifestDependsOn)");
+    }
+
+    // The manifest is what the compiler is given, whole: a setting of the package that is added and has no line
+    // here would be read by the generator and not by the tool.  A property goes through Escape, or a value that
+    // holds a semicolon would be written as several lines.  The lines of a file are one item, so that the metadata
+    // stays under its file.
+    [Fact]
+    public void Targets_ManifestTarget_WritesEverySettingOfTheProjectAndOfEachFile()
+    {
+        var names = Settings.Concat(TrimmedOnly).ToList();
+        var lines = Manifest()
+            .Descendants("SqlSourceManifestLine")
+            .Select(line => line.Attribute("Include").ShouldNotBeNull().Value)
+            .ToList();
+
+        lines[0].ShouldBe("SqlSourceManifest=1");
+        lines
+            .Where(line => line.StartsWith("Property.", StringComparison.Ordinal))
+            .ShouldBe(names.Select(name => $"Property.{name}=$([MSBuild]::Escape($({name})))"), ignoreOrder: true);
+
+        const string Start = "@(SqlSourceManifestSqlFile->'";
+        const string End = "')";
+        var file = lines.Where(line => line.StartsWith(Start, StringComparison.Ordinal)).ShouldHaveSingleItem();
+        file.ShouldEndWith(End);
+        var parts = file[Start.Length..^End.Length].Split("%0a");
+        parts[0].ShouldBe("File=%(FullPath)");
+        parts.Skip(1).ShouldBe(names.Select(name => $"File.{name}=%({name})"), ignoreOrder: true);
+    }
+
+    [Fact]
+    public void Targets_ManifestTarget_ListsTheSqlFilesAndTheCompileFiles()
+    {
+        var target = Manifest();
+        var files = target.Descendants("SqlSourceManifestSqlFile").ShouldHaveSingleItem();
+
+        files.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
+        // MSBuild compares ignoring case, as the generator does for the extension.
+        files.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'%(Extension)' == '.sql'");
+        target
+            .Descendants("SqlSourceManifestLine")
+            .Select(line => line.Attribute("Include")!.Value)
+            .ShouldContain("@(Compile->'Compile=%(FullPath)')");
+    }
+
+    // The file is the one SqlSourceManifestFile names, which the tool sets to a file of its own.  A file under obj
+    // belongs to the project, so "dotnet clean" is told of it; a file the tool named is the tool's to delete.
+    [Fact]
+    public void Targets_ManifestTarget_WritesTheFileThatIsNamedOrOneUnderObj()
+    {
+        var target = Manifest();
+        var write = target.Elements("WriteLinesToFile").ShouldHaveSingleItem();
+        var file = target.Descendants("SqlSourceManifestFile").ShouldHaveSingleItem();
+        var isDefault = target.Descendants("SqlSourceManifestIsDefault").ShouldHaveSingleItem();
+        var written = target.Descendants("FileWrites").ShouldHaveSingleItem();
+
+        write.Attribute("File").ShouldNotBeNull().Value.ShouldBe("$(SqlSourceManifestFile)");
+        write.Attribute("Lines").ShouldNotBeNull().Value.ShouldBe("@(SqlSourceManifestLine)");
+        write.Attribute("Overwrite").ShouldNotBeNull().Value.ShouldBe("true");
+        write.Attribute("WriteOnlyWhenDifferent").ShouldNotBeNull().Value.ShouldBe("true");
+
+        const string NotSet = "'$(SqlSourceManifestFile)' == ''";
+        file.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe(NotSet);
+        file.Value.ShouldBe("$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest");
+        isDefault.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe(NotSet);
+        // The flag is read before the name is given its default, or it would never be set.
+        isDefault.IsBefore(file).ShouldBeTrue();
+        written.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("$(SqlSourceManifestFile)");
+        written
+            .Parent.ShouldNotBeNull()
+            .Attribute("Condition")
+            .ShouldNotBeNull()
+            .Value.ShouldBe("'$(SqlSourceManifestIsDefault)' == 'true'");
+    }
+
+    // The prefix keeps an item type of the package apart from those of the SDK and of other packages, as it does a
+    // property.  FileWrites is the SDK's own, and the target adds to it.
+    [Fact]
+    public void Targets_ManifestTarget_MakesOnlyItemTypesThatStartWithSqlSource() =>
+        Manifest()
+            .Elements("ItemGroup")
+            .Elements()
+            .Select(item => item.Name.LocalName)
+            .Where(name => name != "FileWrites")
+            .Distinct()
+            .ShouldBe(["SqlSourceManifestLine", "SqlSourceManifestSqlFile"], ignoreOrder: true);
+
     // SqlSource.Tests.csproj writes its dialect on a line of its own and its generator parameters one on each line,
     // and tools/package-install sets the parameters that way in Directory.Build.targets, so the end-to-end tests and
     // the check of the installed package show the trimming at work; this pins that each property has it.
@@ -232,6 +366,9 @@ public partial class BuildFileTests
         names.ShouldAllBe(name => name.StartsWith(Prefix, StringComparison.Ordinal));
     }
 
+    private static XElement Manifest() =>
+        Targets.Descendants("Target").Single(target => target.Attribute("Name")!.Value == ManifestTarget);
+
     private static IEnumerable<XAttribute> ConditionsAround(XElement element) =>
         element.AncestorsAndSelf().SelectMany(ancestor => ancestor.Attributes("Condition"));
 
```

The test that every target hooks `GenerateMSBuildEditorConfigFileCore` now leaves the manifest target out and counts the four that are left, so that a fifth target of a build cannot hide behind the exception.

- [ ] **Step 2: Add the fixture projects**

Each is a folder with an `App.csproj` that imports the package's two files from `../build/`, where the helper of the next step copies them.  None is a project of the solution, and none is ever built or restored.

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        One target framework.  The dialect and the database are each on a line of their own, which the manifest must
        hold trimmed.  The package's files are imported by path, as tests/SqlSource.Tests imports them: a test copies
        them beside this folder.
    -->
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <SqlSourceDialect>
            postgres
        </SqlSourceDialect>
    </PropertyGroup>
    <ItemGroup>
        <AdditionalFiles Update="Queries/Users.sql">
            <SqlSourceDatabase>
                billing
            </SqlSourceDatabase>
        </AdditionalFiles>
    </ItemGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/UserRepository.cs`:

```csharp
namespace Fixture;

internal static class UserRepository;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Multi/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        Two target frameworks.  NuGet imports the props and the targets of a package into such a project under a
        condition on TargetFramework, and so does this: evaluated with no framework, the project has none of the
        package.  Later/ is listed for the second framework alone.
    -->
    <ImportGroup Condition="'$(TargetFramework)' == 'net8.0' or '$(TargetFramework)' == 'net10.0'">
        <Import Project="../build/SqlSource.props" />
    </ImportGroup>
    <PropertyGroup>
        <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
        <SqlSourceDialect>postgres</SqlSourceDialect>
    </PropertyGroup>
    <ItemGroup Condition="'$(TargetFramework)' != 'net10.0'">
        <AdditionalFiles Remove="Later/**" />
        <Compile Remove="Later/**" />
    </ItemGroup>
    <ImportGroup Condition="'$(TargetFramework)' == 'net8.0' or '$(TargetFramework)' == 'net10.0'">
        <Import Project="../build/SqlSource.targets" />
    </ImportGroup>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Multi/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Multi/Queries.cs`:

```csharp
namespace Fixture;

internal static class Queries;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Multi/Later/OnlyLater.sql`:

```sql
-- name: OnlyLater
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Multi/Later/OnlyLater.cs`:

```csharp
namespace Fixture;

internal static class OnlyLater;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/LateFile/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- Directory.Build.targets adds a .sql file from the hook the package names. -->
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <SqlSourceDialect>postgres</SqlSourceDialect>
    </PropertyGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/LateFile/Directory.Build.targets`:

```xml
<Project>
    <!--
        A target that adds a .sql file while it runs, as one that writes its .sql files would.  It hooks the target
        the package names, so it runs before the manifest is written, and its metadata is trimmed.
    -->
    <ItemGroup>
        <AdditionalFiles Remove="Generated/Late.sql" />
    </ItemGroup>
    <Target Name="AddSqlFile" BeforeTargets="SqlSourceTrimMetadataOfFiles">
        <ItemGroup>
            <AdditionalFiles Include="Generated/Late.sql">
                <SqlSourceDialect>
                    mssql
                </SqlSourceDialect>
            </AdditionalFiles>
        </ItemGroup>
    </Target>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/LateFile/Generated/Late.sql`:

```sql
-- name: Late
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/LateFile/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/BuildHookFile/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- Directory.Build.targets adds a .sql file from a hook of the build. -->
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <SqlSourceDialect>postgres</SqlSourceDialect>
    </PropertyGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/BuildHookFile/Directory.Build.targets`:

```xml
<Project>
    <!--
        A target that adds a .sql file while a build runs.  The manifest target is run by name and runs no hook of
        a build, so the file is not in the manifest.  TD-0025.
    -->
    <ItemGroup>
        <AdditionalFiles Remove="Generated/Late.sql" />
    </ItemGroup>
    <Target Name="AddSqlFile" BeforeTargets="BeforeBuild">
        <ItemGroup>
            <AdditionalFiles Include="Generated/Late.sql">
                <SqlSourceDialect>
                    mssql
                </SqlSourceDialect>
            </AdditionalFiles>
        </ItemGroup>
    </Target>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/BuildHookFile/Generated/Late.sql`:

```sql
-- name: Late
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/BuildHookFile/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/OwnFiles/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- The project lists its own .sql files, and leaves one of the three out. -->
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
    </PropertyGroup>
    <ItemGroup>
        <AdditionalFiles Include="Queries/One.sql" />
        <AdditionalFiles Include="Queries/Two.sql" />
    </ItemGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/OwnFiles/Queries/One.sql`:

```sql
-- name: One
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/OwnFiles/Queries/Two.sql`:

```sql
-- name: Two
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/OwnFiles/Queries/Three.sql`:

```sql
-- name: Three
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/OddPaths/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- The files of this project have names that MSBuild could split or unescape. -->
    <Import Project="../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <SqlSourceDialect>postgres</SqlSourceDialect>
    </PropertyGroup>
    <Import Project="../build/SqlSource.targets" />
</Project>
```

The two files of `OddPaths` have names that a shell would split, in a folder that has one too.  Make them with these commands, from the root of the repository:

```bash
odd="tests/SqlSource.Tool.Tests/Fixtures/Projects/OddPaths/q;=%41 'é"
mkdir -p "$odd"
printf -- '-- name: Odd\nSELECT 1;\n' >"$odd/a;b=c%41 'é.sql"
printf 'namespace Fixture;\n\ninternal static class Odd;\n' >"$odd/a;b=c%41 'é.cs"
```

Every character of those names is one that Windows allows in a file name too.

- [ ] **Step 3: Copy the fixtures to the output, and write the helper and the tests of the target**

Change `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`:

```diff
--- a/tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj
+++ b/tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj
@@ -12,6 +12,27 @@
         <PackageReference Include="Shouldly" />
         <PackageReference Include="xunit.v3" />
     </ItemGroup>
+    <ItemGroup>
+        <!--
+            Fixtures/Projects holds projects that the tests run "dotnet msbuild" on.  They are no code of this
+            project.  Fixtures/FixtureProjects.cs copies one out of the repository before it is used, with the
+            package's MSBuild files and the repository's global.json beside it, so all of them go to the output.
+        -->
+        <Compile Remove="Fixtures/Projects/**" />
+        <None Remove="Fixtures/Projects/**" />
+        <None Include="Fixtures/Projects/**" CopyToOutputDirectory="PreserveNewest" />
+        <None
+            Include="../../src/SqlSource/build/SqlSource.props"
+            Link="Fixtures/build/SqlSource.props"
+            CopyToOutputDirectory="PreserveNewest"
+        />
+        <None
+            Include="../../src/SqlSource/build/SqlSource.targets"
+            Link="Fixtures/build/SqlSource.targets"
+            CopyToOutputDirectory="PreserveNewest"
+        />
+        <None Include="../../global.json" Link="Fixtures/global.json" CopyToOutputDirectory="PreserveNewest" />
+    </ItemGroup>
     <ItemGroup>
         <ProjectReference Include="../../src/SqlSource.Tool/SqlSource.Tool.csproj" />
         <ProjectReference Include="../../src/SqlSource/SqlSource.csproj" />
```

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SqlSource.Tool.Tests.Fixtures;

// The projects of Fixtures/Projects, copied to a temporary folder outside the repository, so that nothing of the
// repository's own build applies to them.  The package's props and targets are in build/ beside the copies, where
// each project imports them from, and the repository's global.json picks the SDK.
internal sealed class FixtureProjects : IDisposable
{
    private static readonly string Source = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private readonly TempFolder _folder = new();

    public FixtureProjects()
    {
        File.Copy(Path.Combine(Source, "global.json"), _folder.PathOf("global.json"));
        CopyFolder(Path.Combine(Source, "build"), _folder.PathOf("build"));
    }

    // The folder that holds the copies.
    public string Root => _folder.Path;

    // Copies a fixture and gives the full path of a file of the copy, its project file when none is named.
    public string Copy(string fixture, string file = "App.csproj")
    {
        var target = _folder.PathOf(fixture);
        if (!Directory.Exists(target))
        {
            CopyFolder(Path.Combine(Source, "Projects", fixture), target);
        }

        return Path.Combine(target, file.Replace('/', Path.DirectorySeparatorChar));
    }

    public string PathOf(string relativePath) => _folder.PathOf(relativePath);

    public string WriteFile(string relativePath, string content = "") => _folder.WriteFile(relativePath, content);

    // Runs the manifest target of a project with the real "dotnet msbuild", as the tool does, and gives the lines of
    // the file it wrote.  A property is given as "Name=value".
    public string[] WriteManifest(string project, params string[] properties)
    {
        var file = _folder.PathOf(Guid.NewGuid().ToString("N") + ".manifest");
        var (exitCode, output) = RunMSBuild(
            project,
            ["-t:SqlSourceWriteManifest", $"-p:SqlSourceManifestFile={file}", .. PropertySwitches(properties)]
        );
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"msbuild exited with {exitCode}:\n{output}");
        }

        return File.ReadAllLines(file);
    }

    public static (int ExitCode, string Output) RunMSBuild(string project, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-nologo");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A test process that MSBuild started has these, and "dotnet" would then load the SDK they name and not the
        // one global.json picks.
        _ = start.Environment.Remove("MSBuildSDKsPath");
        _ = start.Environment.Remove("MSBuildExtensionsPath");
        start.Environment["DOTNET_NOLOGO"] = "true";

        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.GetAwaiter().GetResult());
    }

    public void Dispose() => _folder.Dispose();

    private static IEnumerable<string> PropertySwitches(string[] properties)
    {
        foreach (var property in properties)
        {
            yield return "-p:" + property;
        }
    }

    private static void CopyFolder(string source, string target)
    {
        _ = Directory.CreateDirectory(target);
        foreach (var folder in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            _ = Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, folder)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
```

Create `tests/SqlSource.Tool.Tests/ManifestTargetTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// The target SqlSourceWriteManifest of build/SqlSource.targets, run by name with the real "dotnet msbuild" on the
// projects of Fixtures/Projects.  Each test starts MSBuild once or twice, which takes a second or two.
public sealed class ManifestTargetTests : IDisposable
{
    private static readonly string[] Names =
    [
        "SqlSourceDialect",
        "SqlSourceDatabase",
        "SqlSourceOutput",
        "SqlSourceGeneratorParameters",
        "SqlSourceInputModelSuffix",
        "SqlSourceOutputModelSuffix",
        "SqlSourceModelNamespace",
        "SqlSourceInputModelType",
        "SqlSourceOutputModelType",
        "SqlSourceCollectionType",
    ];

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The lines of one file: its File line and the ten under it, with the values that are not empty.
    private static string[] FileLines(string path, params (string Name, string Value)[] metadata) =>
        [
            "File=" + path,
            .. Names.Select(name => $"File.{name}=" + metadata.FirstOrDefault(pair => pair.Name == name).Value),
        ];

    private static string[] PropertyLines(params (string Name, string Value)[] properties) =>
        [.. Names.Select(name => $"Property.{name}=" + properties.FirstOrDefault(pair => pair.Name == name).Value)];

    private static string[] Constants(string[] lines) =>
        lines
            .Single(line => line.StartsWith("DefineConstants=", StringComparison.Ordinal))["DefineConstants=".Length..]
            .Split(';');

    [Fact]
    public void WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        lines
            .Where(line => !line.StartsWith("DefineConstants=", StringComparison.Ordinal))
            .ShouldBe([
                "SqlSourceManifest=1",
                "Project=" + project,
                "TargetFramework=net10.0",
                "LangVersion=14.0",
                // Each was written on a line of its own, and is trimmed.
                .. PropertyLines(("SqlSourceDialect", "postgres")),
                .. FileLines(Path.Combine(folder, "Queries", "Users.sql"), ("SqlSourceDatabase", "billing")),
                "Compile=" + Path.Combine(folder, "UserRepository.cs"),
            ]);
        lines[4].ShouldStartWith("DefineConstants=");
    }

    // Run by name, a project has TRACE and DEBUG alone: the SDK adds the constants of the framework on the way to a
    // compile.  Without them the tool would not find an attribute under "#if NET8_0_OR_GREATER".
    [Fact]
    public void WriteManifest_ProjectWithOneFramework_HasTheConstantsOfItsFramework()
    {
        var constants = Constants(_fixtures.WriteManifest(_fixtures.Copy("Single")));

        constants.ShouldContain("DEBUG");
        constants.ShouldContain("NET10_0");
        constants.ShouldContain("NET8_0_OR_GREATER");
        constants.ShouldAllBe(constant => constant.Length > 0);
    }

    [Fact]
    public void WriteManifest_NoFileNamed_WritesOneUnderObj()
    {
        var project = _fixtures.Copy("Single");
        var file = Path.Combine(
            Path.GetDirectoryName(project)!,
            "obj",
            "Debug",
            "net10.0",
            "App.csproj.SqlSource.manifest"
        );

        var (exitCode, output) = FixtureProjects.RunMSBuild(project, ["-t:SqlSourceWriteManifest"]);

        exitCode.ShouldBe(0, output);
        File.ReadLines(file).First().ShouldBe("SqlSourceManifest=1");
    }

    [Fact]
    public void WriteManifest_FileNamed_WritesNothingIntoTheProject()
    {
        var project = _fixtures.Copy("Single");

        _ = _fixtures.WriteManifest(project);

        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "obj")).ShouldBeFalse();
        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "bin")).ShouldBeFalse();
    }

    // A project with several frameworks has no target of the package until it is given one of them.
    [Fact]
    public void WriteManifest_SeveralFrameworksAndNoneGiven_Fails()
    {
        var (exitCode, output) = FixtureProjects.RunMSBuild(_fixtures.Copy("Multi"), ["-t:SqlSourceWriteManifest"]);

        exitCode.ShouldNotBe(0);
        output.ShouldContain("MSB4057");
    }

    // TD-0026: the manifest is the first framework's.  What the project lists for another alone is not in it.
    [Fact]
    public void WriteManifest_SeveralFrameworksAndTheFirstGiven_IsTheManifestOfThatFramework()
    {
        var project = _fixtures.Copy("Multi");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project, "TargetFramework=net8.0");

        lines.ShouldContain("TargetFramework=net8.0");
        Constants(lines).ShouldContain("NET8_0");
        Constants(lines).ShouldNotContain("NET10_0");
        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe(["File=" + Path.Combine(folder, "Queries", "Users.sql")]);
        lines
            .Where(line => line.StartsWith("Compile=", StringComparison.Ordinal))
            .ShouldBe(["Compile=" + Path.Combine(folder, "Queries.cs")]);
    }

    [Fact]
    public void WriteManifest_FileAddedByATargetThatHooksTheTrim_IsListedWithTrimmedMetadata()
    {
        var project = _fixtures.Copy("LateFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        var late = FileLines(Path.Combine(folder, "Generated", "Late.sql"), ("SqlSourceDialect", "mssql"));
        var at = Array.IndexOf(lines, late[0]);
        at.ShouldBeGreaterThan(0);
        lines.Skip(at).Take(late.Length).ShouldBe(late);
        lines.ShouldContain("File=" + Path.Combine(folder, "Queries", "Users.sql"));
    }

    // TD-0025: the target runs no hook of a build.
    [Fact]
    public void WriteManifest_FileAddedByATargetThatHooksTheBuild_IsNotListed()
    {
        var project = _fixtures.Copy("BuildHookFile");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe(["File=" + Path.Combine(folder, "Queries", "Users.sql")]);
    }

    [Fact]
    public void WriteManifest_ProjectThatListsItsOwnFiles_HoldsTheFilesItLists()
    {
        var project = _fixtures.Copy("OwnFiles");
        var folder = Path.GetDirectoryName(project)!;

        var lines = _fixtures.WriteManifest(project);

        lines
            .Where(line => line.StartsWith("File=", StringComparison.Ordinal))
            .ShouldBe([
                "File=" + Path.Combine(folder, "Queries", "One.sql"),
                "File=" + Path.Combine(folder, "Queries", "Two.sql"),
            ]);
    }

    // MSBuild splits a list at a semicolon and reads "%41" as "A".  A path is none of its lists.
    [Fact]
    public void WriteManifest_PathsWithCharactersThatMSBuildReads_AreListedWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var lines = _fixtures.WriteManifest(project);

        lines.ShouldContain("File=" + Path.Combine(folder, "a;b=c%41 'é.sql"));
        lines.ShouldContain("Compile=" + Path.Combine(folder, "a;b=c%41 'é.cs"));
    }

    [Fact]
    public void WriteManifest_ValuesThatHoldASemicolon_AreEachOneLine()
    {
        var project = _fixtures.Copy("Single");

        var lines = _fixtures.WriteManifest(
            project,
            "SqlSourceGeneratorParameters=a%3Bb",
            "SqlSourceModelNamespace=A=B"
        );

        lines.ShouldContain("Property.SqlSourceGeneratorParameters=a;b");
        lines.ShouldContain("Property.SqlSourceModelNamespace=A=B");
    }
}
```

- [ ] **Step 4: Run both to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Package.BuildFileTests`
Expected: the six tests whose names start with `Targets_ManifestTarget_` or `Targets_Root_` fail: `Single` finds no target of that name, and the root holds no `PropertyGroup`.  The others pass.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ManifestTargetTests`
Expected: every test but `WriteManifest_SeveralFrameworksAndNoneGiven_Fails` fails, with MSBuild's `MSB4057` in the exception's text: the target does not exist.

- [ ] **Step 5: Write the target**

Change `src/SqlSource/build/SqlSource.targets`:

```diff
--- a/src/SqlSource/build/SqlSource.targets
+++ b/src/SqlSource/build/SqlSource.targets
@@ -141,4 +141,72 @@
             <FileWrites Include="$(SqlSourceFilesCache)" />
         </ItemGroup>
     </Target>
+    <!--
+        What SqlSourceWriteManifest depends on.  A target's DependsOnTargets is read before the target runs, so the
+        list is a property and is set here, where the file is imported: this is the one element of the file that is
+        not a target, and its second line holds the file's one condition.  The trims make the manifest hold trimmed
+        values, and make a target of a project that hooks SqlSourceTrimMetadataOfFiles to add a .sql file run first.
+
+        AddImplicitDefineConstants is a target of the SDK and not a contract: TD-0027.  It adds the constants of the
+        framework, NET10_0 and NET8_0_OR_GREATER among them, which the SDK otherwise adds only on the way to a
+        compile.  A project that is not of the SDK has no TargetFramework and no such target.
+    -->
+    <PropertyGroup>
+        <SqlSourceManifestDependsOn>SqlSourceTrimProperties;SqlSourceTrimMetadataOfFiles</SqlSourceManifestDependsOn>
+        <SqlSourceManifestDependsOn Condition="'$(TargetFramework)' != ''"
+            >$(SqlSourceManifestDependsOn);AddImplicitDefineConstants</SqlSourceManifestDependsOn>
+    </PropertyGroup>
+    <!--
+        Writes the project manifest: what the compiler is given, for the sqlsource tool, which runs this target by
+        name.  It hooks nothing on purpose, so no build runs it, and nothing of a build runs before it but the two
+        trims: a .sql file that a target adds is in the manifest only when that target hooks
+        SqlSourceTrimMetadataOfFiles.  TD-0025.
+
+        The file is lines of key=value, and its format is a contract with the tool: see src/SqlSource/AGENTS.md.  The
+        tool names the file with SqlSourceManifestFile; without it the file is under obj.
+
+        A property goes through Escape, or MSBuild would make an item of each part of a value that holds a
+        semicolon, as DefineConstants does.  The lines of one .sql file are one item, with %0a between them, so that
+        the metadata of a file stays under its File line whatever the order of the items is; the path and the
+        metadata come from the item and are not split.  A setting of the package that is added is a line here, and
+        one in each of the two lists of the trims.
+    -->
+    <!-- editorconfig-checker-disable -->
+    <Target Name="SqlSourceWriteManifest" DependsOnTargets="$(SqlSourceManifestDependsOn)">
+        <PropertyGroup>
+            <SqlSourceManifestIsDefault Condition="'$(SqlSourceManifestFile)' == ''">true</SqlSourceManifestIsDefault>
+            <SqlSourceManifestFile Condition="'$(SqlSourceManifestFile)' == ''"
+                >$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest</SqlSourceManifestFile>
+        </PropertyGroup>
+        <ItemGroup>
+            <SqlSourceManifestLine Include="SqlSourceManifest=1" />
+            <SqlSourceManifestLine Include="Project=$([MSBuild]::Escape($(MSBuildProjectFullPath)))" />
+            <SqlSourceManifestLine Include="TargetFramework=$([MSBuild]::Escape($(TargetFramework)))" />
+            <SqlSourceManifestLine Include="LangVersion=$([MSBuild]::Escape($(LangVersion)))" />
+            <SqlSourceManifestLine Include="DefineConstants=$([MSBuild]::Escape($(DefineConstants)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceDialect=$([MSBuild]::Escape($(SqlSourceDialect)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceDatabase=$([MSBuild]::Escape($(SqlSourceDatabase)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceOutput=$([MSBuild]::Escape($(SqlSourceOutput)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceGeneratorParameters=$([MSBuild]::Escape($(SqlSourceGeneratorParameters)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceInputModelSuffix=$([MSBuild]::Escape($(SqlSourceInputModelSuffix)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceOutputModelSuffix=$([MSBuild]::Escape($(SqlSourceOutputModelSuffix)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceModelNamespace=$([MSBuild]::Escape($(SqlSourceModelNamespace)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceInputModelType=$([MSBuild]::Escape($(SqlSourceInputModelType)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceOutputModelType=$([MSBuild]::Escape($(SqlSourceOutputModelType)))" />
+            <SqlSourceManifestLine Include="Property.SqlSourceCollectionType=$([MSBuild]::Escape($(SqlSourceCollectionType)))" />
+            <SqlSourceManifestSqlFile Include="@(AdditionalFiles)" Condition="'%(Extension)' == '.sql'" />
+            <SqlSourceManifestLine Include="@(SqlSourceManifestSqlFile->'File=%(FullPath)%0aFile.SqlSourceDialect=%(SqlSourceDialect)%0aFile.SqlSourceDatabase=%(SqlSourceDatabase)%0aFile.SqlSourceOutput=%(SqlSourceOutput)%0aFile.SqlSourceGeneratorParameters=%(SqlSourceGeneratorParameters)%0aFile.SqlSourceInputModelSuffix=%(SqlSourceInputModelSuffix)%0aFile.SqlSourceOutputModelSuffix=%(SqlSourceOutputModelSuffix)%0aFile.SqlSourceModelNamespace=%(SqlSourceModelNamespace)%0aFile.SqlSourceInputModelType=%(SqlSourceInputModelType)%0aFile.SqlSourceOutputModelType=%(SqlSourceOutputModelType)%0aFile.SqlSourceCollectionType=%(SqlSourceCollectionType)')" />
+            <SqlSourceManifestLine Include="@(Compile->'Compile=%(FullPath)')" />
+        </ItemGroup>
+        <WriteLinesToFile
+            File="$(SqlSourceManifestFile)"
+            Lines="@(SqlSourceManifestLine)"
+            Overwrite="true"
+            WriteOnlyWhenDifferent="true"
+        />
+        <ItemGroup Condition="'$(SqlSourceManifestIsDefault)' == 'true'">
+            <FileWrites Include="$(SqlSourceManifestFile)" />
+        </ItemGroup>
+    </Target>
+    <!-- editorconfig-checker-enable -->
 </Project>
```

- [ ] **Step 6: Run both to see them pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Package.BuildFileTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ManifestTargetTests`
Expected: eleven tests pass, in about four seconds.

To see a manifest:

```bash
dotnet msbuild tests/SqlSource.Tests/SqlSource.Tests.csproj -nologo -t:SqlSourceWriteManifest -p:SqlSourceManifestFile=/tmp/SqlSource.Tests.manifest
```

Expected: the file starts with `SqlSourceManifest=1`, and holds `Property.SqlSourceDialect=postgres` and a `File=` line for each `.sql` file under `tests/SqlSource.Tests/EndToEnd/`.  `git status` shows nothing new: the target wrote nowhere else.

- [ ] **Step 7: Record what the manifest does not see**

Create `docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`:

```markdown
# TD-0025 - The project manifest misses a `.sql` file that a target adds from a hook of the build

## Problem

The `sqlsource` tool learns what the compiler is given from the project manifest, which the target `SqlSourceWriteManifest` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) writes.  The tool runs that target by name, so nothing of a build runs before it but what it depends on: the package's two trims.

A `.sql` file that a target of the project adds as an `AdditionalFiles` item is therefore in the manifest only when that target hooks `SqlSourceTrimMetadataOfFiles`.  One that hooks `BeforeBuild`, or any other target of a build, has not run.  The build compiles the file, and the tool does not know it: it describes none of its queries.  From phase 5 of query generation the generator needs a sidecar entry for each of them, and reports each as missing.

The same holds for a `Compile` item that a target adds: a `[SqlSourceGenerate]` in such a file is not read.

`tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` pins it with the fixture `BuildHookFile`, and `tools/check-package-install.sh` with the file that `tools/package-install/Directory.Build.targets` adds.

## Why it exists

Depending on `BeforeBuild` would run whatever a project hangs on it, a code generator or a package install, on every `sqlsource describe`.  The owner weighed that and rejected it.

## Impact

Low.  It takes a target that adds `.sql` files while the build runs.  The failure is loud from phase 5: the build says which queries have no entry.

## Proposed fix

A target that adds a `.sql` file hooks `SqlSourceTrimMetadataOfFiles`, which runs it before the manifest is written as well as before the trims; the comment at the top of `build/SqlSource.targets` and [TD-0016](TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md) say the same for a build.  Phase 5 says so in `README.md`.  Phase 9's run inside a build does not have the limit: the build has run its hooks by then.

## Trigger

A user reports that `sqlsource describe` leaves out the queries of a file that their build adds.  Or phase 9, which may close it for a run inside a build.
```

Create `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`:

```markdown
# TD-0026 - The manifest of a project with several target frameworks is the first one's alone

## Problem

For a project with `TargetFrameworks`, the `sqlsource` tool passes the first of them as `TargetFramework`, to the evaluation that asks whether the project uses SqlSource and to the target that writes the project manifest.  So there is one manifest for the project, and it is that framework's.

- A `.sql` file or a `Compile` file that the project lists only under a condition on another framework is not in it.
- An attribute under `#if` for a constant that only another framework defines is not read.
- A project that references SqlSource for another framework alone does not use SqlSource at all, as the tool sees it: in a solution it is left out and nothing is said.

The queries those bring are never described.  The generator compiles for every framework, so from phase 5 the build of the other framework reports each as having no entry.

`tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` pins the first two with the fixture `Multi`.

## Why it exists

`AdditionalFiles` almost never differ by framework, and a manifest for each framework would multiply the MSBuild runs of every project that has several, for the few that differ.

## Impact

Low.  The gap is loud from phase 5 for a file or an attribute, and silent only for the project that uses SqlSource in a later framework alone.

## Proposed fix

One evaluation and one manifest for each framework, and the plan of a run as the union of what they need: a query needs an entry when any framework's types claim it with an output that needs one.

## Trigger

A user reports a query that the tool does not describe and the build asks for, in a project with several target frameworks.
```

Create `docs/tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md`:

```markdown
# TD-0027 - The manifest target depends on a target of the SDK by name

## Problem

`SqlSourceWriteManifest` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) depends on `AddImplicitDefineConstants` when the project has a `TargetFramework`.  That is the target of the .NET SDK that adds the constants of the framework, `NET10_0` and `NET8_0_OR_GREATER` among them, to `DefineConstants`.  The SDK runs it on the way to a compile; run by name, a project has `TRACE;DEBUG` alone, and the tool would not find an attribute under `#if NET8_0_OR_GREATER`.

The name is the SDK's own and no contract.  An SDK that renames the target fails every run of the tool with `MSB4057`, which the tool reports as `SQLSRC205`.  A project that sets `DisableImplicitFrameworkDefines` gets no such constants, in the manifest as in its build.

## Why it exists

It is the one way to get the constants as the compiler gets them, with what a project adds or removes.  The target has had this name since the .NET 5 SDK.

## Impact

Low today.  A rename breaks the tool for every project until the package is updated, and the failure names the missing target.

## Proposed fix

The tool works the constants out itself from the manifest's `TargetFramework`, as Roslyn's own workspace does, and the target stops depending on the SDK's.  That loses a constant a project's own target adds, which the manifest does not hold today either.

## Trigger

An SDK in which `dotnet msbuild -t:SqlSourceWriteManifest` fails with `MSB4057` for `AddImplicitDefineConstants`.  `tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` fails on that SDK.
```

Change `docs/tech-debt/README.md`:

```diff
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -2,7 +2,7 @@
 
 Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.
 
-Next id: `TD-0025`
+Next id: `TD-0028`
 
 ## Active items
 
@@ -20,6 +20,9 @@ Next id: `TD-0025`
 | [TD-0022](TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md) | Open | 2026-10-08 | Low | A query that keeps its comments has its SQL built and scanned for tokens twice, so its parse allocates and takes about twice as much |
 | [TD-0023](TD-0023-unknown-option-and-path-are-printed-as-given.md) | Open | 2026-10-09 | Low | The `sqlsource` tool prints the name of an unknown option and the path of `describe` as they were typed, so a secret typed as either reaches its output |
 | [TD-0024](TD-0024-gaps-of-the-tools-shell.md) | Open | 2026-10-09 | Low | Three gaps of the `sqlsource` tool's shell that wait for sub-phase 2.5: `SQLSRC200` prints the message of any exception, a driver's included; Ctrl+C is always swallowed; and no test reaches the line the tool writes for a command line that System.CommandLine rejects |
+| [TD-0025](TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md) | Open | 2026-10-09 | Low | The `sqlsource` tool does not see a `.sql` file, or a C# file, that a target adds from a hook of the build: only a target that hooks `SqlSourceTrimMetadataOfFiles` runs before the project manifest is written |
+| [TD-0026](TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md) | Open | 2026-10-09 | Low | For a project with several target frameworks the `sqlsource` tool reads the first one alone: a file, an attribute or a reference to SqlSource that only another framework has is not seen |
+| [TD-0027](TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md) | Open | 2026-10-09 | Low | The target that writes the project manifest depends on `AddImplicitDefineConstants`, a target of the SDK whose name is no contract |
 
 ## Columns
 
```

Change `docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md`:

```diff
--- a/docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md
+++ b/docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md
@@ -14,7 +14,7 @@ Listing the metadata for `AdditionalFiles` itself gave every `.sql` file of a pr
 
 ## Impact
 
-Low.  It takes a target that adds `.sql` files during the build, gives them a dialect as metadata, hooks that one target of the SDK, and is declared after the package's.  A target that hooks anything earlier, `BeforeBuild` for example, is not affected.
+Low.  It takes a target that adds `.sql` files during the build, gives them a dialect as metadata, hooks that one target of the SDK, and is declared after the package's.  A target that hooks anything earlier, `BeforeBuild` for example, is not affected in a build.  The `sqlsource` tool does not see the file such a target adds at all: [TD-0025](TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md).
 
 ## Proposed fix
 
```

- [ ] **Step 8: Say how to work with the target and the fixtures**

Change `src/SqlSource/AGENTS.md`:

```diff
--- a/src/SqlSource/AGENTS.md
+++ b/src/SqlSource/AGENTS.md
@@ -32,6 +32,16 @@ The source generator.  It is loaded into the C# compiler of whoever consumes it,
 - **The settings have two stages, and only the first reaches the parse.**  Stage one is `Generation/FileParseInput.cs`: a file's dialect, and whether comments may be wanted, which is true when the list of the file's metadata, or else of the property, has `keep-comments`, or when the file is in `CommentPaths`, the files of the types whose attribute asks for it.  Stage two is everything else: `Generation/ProjectSettings.cs` reads the properties, `Generation/FileSettings.cs` the metadata of each file's item, both through `Generation/MSBuildSettings.cs`, `TargetTypeReader` the attribute, and `TypeEmitter` resolves the four levels with a query's markers through `QuerySettings.Resolve`.  A setting of stage two must never be an input of `SqlFileReader.Read`: a change to a property would then parse every file again.  `tests/SqlSource.Tests/Generator/CachingTests.cs` pins what each kind of edit parses.
 - **A value MSBuild gives is trimmed by a target.**  The compiler reads it from a file the build writes with one line for each value, so a value on a line of its own would arrive empty and a list with one word on each line as its first word, and no code in the generator can see that.  A trim removes the white space around a value and makes one space of each run of white space inside it, so a new setting must not give white space any meaning beyond separating words.  The trims run before `GenerateMSBuildEditorConfigFileCore`, the target of the SDK that writes the file, because a trim outside a target misses `Directory.Build.targets` and an item that a target adds.  They still miss an item added by a target that hooks the same SDK target and is declared later; the comment in the file says what such a target does instead.
 
+## The project manifest
+
+`SqlSourceWriteManifest` in `build/SqlSource.targets` writes what the compiler is given, for the `sqlsource` tool: the project's `.sql` files with their metadata, the package's properties, the `Compile` files, `LangVersion` and `DefineConstants`.  `build/SqlSource.props` sets `SqlSourceImported`, which is how the tool tells that a project uses SqlSource.
+
+- **The target hooks nothing, on purpose.**  The tool runs it by name, and no build runs it.  Do not give it `BeforeTargets`, `AfterTargets` or a condition: `tests/SqlSource.Tests/Package/BuildFileTests.cs` holds that.  Its `DependsOnTargets` is the property `SqlSourceManifestDependsOn`, set in the one `PropertyGroup` at the root of the file.
+- **The format is a contract with the tool**, as the sidecar's is with the generator.  Lines of `key=value`, the key ending at the first `=`; the first line is `SqlSourceManifest=1`.  A new key, and a new name under `Property.` or `File.`, are additions: a reader ignores what it does not know.  A change to what an existing key means, or to how a line is read, raises the version, and a reader of another version reads nothing.  The spec of sub-phase 2.3, `docs/superpowers/specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md`, has the table of keys, and `src/SqlSource.Tool/Projects/ManifestReader.cs` is the reader.
+- **A new setting of the package is a new line in the target**, twice: `Property.<Name>` and `File.<Name>` in the transform.  `BuildFileTests` fails until both are there.
+- **A property is written through `$([MSBuild]::Escape(...))`, and the lines of a file are one item.**  Without the first a value that holds `;` becomes several lines; without the second the metadata of a file could be parted from its `File` line.  `tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` runs the target on the projects of `tests/SqlSource.Tool.Tests/Fixtures/Projects/` with the real `dotnet msbuild`.
+- **It depends on `AddImplicitDefineConstants`, a target of the SDK, by name** (`docs/tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md`), and it does not see a file that a hook of the build adds (`docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md`).
+
 ## `Settings/`
 
 Plain data and the readers of values: no file access, no symbols, no pipeline types.  `Parsing/` and `Generation/` both use it, and the command-line tool will.  It uses nothing from `Parsing/`, `Generation/` or `Diagnostics/`, and must stay so: a rule that it shares with another folder goes in the root namespace, as `SqlIdentifier` does.
```

Change `CONTRIBUTING.md`:

````diff
--- a/CONTRIBUTING.md
+++ b/CONTRIBUTING.md
@@ -44,6 +44,8 @@ A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both, so i
 
 `tests/SqlSource.Tests` also uses the generator the way a consumer does: the types in `EndToEnd/` are compiled with the generator loaded, and the project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`, the MSBuild files the package ships.  It gets them by path and the generator through a project reference; `tools/check-package-install.sh`, under Package below, is what installs the packed package.
 
+`tests/SqlSource.Tool.Tests/Fixtures/Projects/` holds small projects that are no part of the solution.  `ManifestTargetTests` copies one to a temporary folder outside the repository, with the package's two MSBuild files and `global.json` beside it, and runs `dotnet msbuild` on it for real, so these tests need the SDK that `global.json` pins and take a second or two each.  A fixture is never built or restored, and a test leaves nothing in the repository.  A file under that folder is copied to the test project's output as it is; add a project there as a folder with an `App.csproj` that imports `../build/SqlSource.props` and `../build/SqlSource.targets`.
+
 To run the tool from its source:
 
 ```bash
````

The section of `src/SqlSource/AGENTS.md` names `src/SqlSource.Tool/Projects/ManifestReader.cs`, which task 4 creates.

- [ ] **Step 9: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Write the project manifest from a target of the package

SqlSourceWriteManifest hooks nothing: the sqlsource tool runs it by name.
It writes what the compiler is given as lines of key=value, after the
package's trims and the SDK's target that adds the constants of the
framework.  Fixture projects show what it writes with the real MSBuild.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 4: `ManifestReader`, `ProjectManifest` and `SQLSRC206`

**Files:**
- Create: `src/SqlSource.Tool/Projects/ProjectManifest.cs`, `src/SqlSource.Tool/Projects/ManifestFile.cs`, `src/SqlSource.Tool/Projects/ManifestReader.cs`
- Test: `tests/SqlSource.Tool.Tests/ManifestReaderTests.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: the format that task 3's target writes.
- Produces: `ProjectManifest`, a sealed class with the required members `string ProjectPath`, `string TargetFramework`, `string LangVersion`, `ImmutableArray<string> DefineConstants`, `ImmutableDictionary<string, string> Properties`, `ImmutableArray<ManifestFile> Files`, `ImmutableArray<string> CompileFiles`.
- Produces: `sealed record ManifestFile(string Path, ImmutableDictionary<string, string> Metadata)`.
- Produces: `static ProjectManifest? ManifestReader.Read(string text, out string reason)`.  The reason is the end of the sentence of `SQLSRC206`, and empty when the manifest was read.
- Produces: `ToolDiagnostics.ManifestCannotBeRead`, `SQLSRC206`, with the arguments the project file and the reason.  Task 5 reports it.

- [ ] **Step 1: Write the failing tests of the reader**

Create `tests/SqlSource.Tool.Tests/ManifestReaderTests.cs`:

```csharp
using System.Linq;
using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public class ManifestReaderTests
{
    // The example of the spec of sub-phase 2.3, with every line the target writes.
    private const string Example = """
        SqlSourceManifest=1
        Project=/work/src/App/App.csproj
        TargetFramework=net10.0
        LangVersion=14.0
        DefineConstants=TRACE;DEBUG;NET;NET10_0;NET8_0_OR_GREATER
        Property.SqlSourceDialect=postgres
        Property.SqlSourceDatabase=
        Property.SqlSourceOutput=
        Property.SqlSourceGeneratorParameters=sort-input no-token-validation
        Property.SqlSourceInputModelSuffix=
        Property.SqlSourceOutputModelSuffix=
        Property.SqlSourceModelNamespace=
        Property.SqlSourceInputModelType=
        Property.SqlSourceOutputModelType=
        Property.SqlSourceCollectionType=
        File=/work/src/App/Queries/Users.sql
        File.SqlSourceDialect=
        File.SqlSourceDatabase=billing
        File.SqlSourceOutput=
        File.SqlSourceGeneratorParameters=
        File.SqlSourceInputModelSuffix=
        File.SqlSourceOutputModelSuffix=
        File.SqlSourceModelNamespace=
        File.SqlSourceInputModelType=
        File.SqlSourceOutputModelType=
        File.SqlSourceCollectionType=
        File=/work/src/App/Queries/Orders.sql
        File.SqlSourceDialect=mysql, no-backslash-escapes
        File.SqlSourceDatabase=
        Compile=/work/src/App/UserRepository.cs
        Compile=/work/src/App/Program.cs

        """;

    private static ProjectManifest Read(string text)
    {
        var manifest = ManifestReader.Read(text, out var reason).ShouldNotBeNull(reason);
        reason.ShouldBeEmpty();
        return manifest;
    }

    private static string ReasonOf(string text)
    {
        ManifestReader.Read(text, out var reason).ShouldBeNull();
        return reason;
    }

    [Fact]
    public void Read_ExampleOfTheSpec_GivesEveryMember()
    {
        var manifest = Read(Example);

        manifest.ProjectPath.ShouldBe("/work/src/App/App.csproj");
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.LangVersion.ShouldBe("14.0");
        manifest.DefineConstants.ShouldBe(["TRACE", "DEBUG", "NET", "NET10_0", "NET8_0_OR_GREATER"]);
        manifest
            .Properties.OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value))
            .ShouldBe([
                ("SqlSourceDialect", "postgres"),
                ("SqlSourceGeneratorParameters", "sort-input no-token-validation"),
            ]);
        manifest.Files.Length.ShouldBe(2);
        manifest.Files[0].Path.ShouldBe("/work/src/App/Queries/Users.sql");
        manifest.Files[0].Metadata.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDatabase", "billing"));
        manifest.Files[1].Path.ShouldBe("/work/src/App/Queries/Orders.sql");
        manifest
            .Files[1]
            .Metadata.ShouldHaveSingleItem()
            .ShouldBe(new("SqlSourceDialect", "mysql, no-backslash-escapes"));
        manifest.CompileFiles.ShouldBe(["/work/src/App/UserRepository.cs", "/work/src/App/Program.cs"]);
    }

    [Fact]
    public void Read_OnlyTheVersionAndTheProject_GivesEmptyMembers()
    {
        var manifest = Read("SqlSourceManifest=1\nProject=/work/App.csproj");

        manifest.TargetFramework.ShouldBeEmpty();
        manifest.LangVersion.ShouldBeEmpty();
        manifest.DefineConstants.ShouldBeEmpty();
        manifest.Properties.ShouldBeEmpty();
        manifest.Files.ShouldBeEmpty();
        manifest.CompileFiles.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ValueThatHoldsAnEqualsSign_IsTheRestOfTheLine()
    {
        var manifest = Read("SqlSourceManifest=1\nProject=/work/a=b/App.csproj\nFile=/work/a=b/x=y.sql\nCompile==.cs");

        manifest.ProjectPath.ShouldBe("/work/a=b/App.csproj");
        manifest.Files.ShouldHaveSingleItem().Path.ShouldBe("/work/a=b/x=y.sql");
        manifest.CompileFiles.ShouldBe(["=.cs"]);
    }

    [Fact]
    public void Read_ValueWithSpacesAroundIt_IsKeptAsItIs() =>
        Read("SqlSourceManifest=1\nProject= /work/App.csproj \nFile=/work/a .sql ")
            .Files.ShouldHaveSingleItem()
            .Path.ShouldBe("/work/a .sql ");

    [Theory]
    [InlineData("TRACE;DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData("TRACE,DEBUG", new[] { "TRACE", "DEBUG" })]
    [InlineData(";TRACE;; DEBUG ,;", new[] { "TRACE", "DEBUG" })]
    [InlineData("", new string[0])]
    public void Read_Constants_AreSplitAtSemicolonsAndCommas(string value, string[] expected) =>
        Read($"SqlSourceManifest=1\nProject=p\nDefineConstants={value}").DefineConstants.ShouldBe(expected);

    [Fact]
    public void Read_KeyThatIsNotKnown_IsPassedOver()
    {
        var manifest = Read(
            "SqlSourceManifest=1\nProject=p\nAssemblyName=App\nFile=a.sql\nFile.SqlSourceLater=x\n"
                + "Property.SqlSourceLater=y"
        );

        // A name under a prefix is kept: a later sub-phase reads the ones it knows.
        manifest.Files.ShouldHaveSingleItem().Metadata["SqlSourceLater"].ShouldBe("x");
        manifest.Properties["SqlSourceLater"].ShouldBe("y");
    }

    [Fact]
    public void Read_EmptyLines_ArePassedOver() =>
        Read("SqlSourceManifest=1\n\nProject=p\n\n\nCompile=a.cs\n").CompileFiles.ShouldBe(["a.cs"]);

    [Fact]
    public void Read_LinesThatEndWithACarriageReturn_AreReadWithoutIt()
    {
        var manifest = Read("SqlSourceManifest=1\r\nProject=p\r\nFile=a.sql\r\nFile.SqlSourceDialect=mssql\r\n");

        manifest.ProjectPath.ShouldBe("p");
        manifest.Files.ShouldHaveSingleItem().Metadata["SqlSourceDialect"].ShouldBe("mssql");
    }

    [Fact]
    public void Read_ByteOrderMark_IsPassedOver() => Read("﻿SqlSourceManifest=1\nProject=p").ProjectPath.ShouldBe("p");

    [Fact]
    public void Read_KeyGivenTwice_TakesTheLast() =>
        Read("SqlSourceManifest=1\nProject=p\nProject=q\nProperty.SqlSourceDialect=a\nProperty.SqlSourceDialect=b")
            .ShouldSatisfyAllConditions(
                manifest => manifest.ProjectPath.ShouldBe("q"),
                manifest => manifest.Properties["SqlSourceDialect"].ShouldBe("b")
            );

    [Theory]
    [InlineData("")]
    [InlineData("﻿")]
    public void Read_NoText_IsEmpty(string text) => ReasonOf(text).ShouldBe("it is empty");

    [Theory]
    [InlineData("Project=p\nSqlSourceManifest=1")]
    [InlineData("\nSqlSourceManifest=1\nProject=p")]
    [InlineData("sqlsourcemanifest=1\nProject=p")]
    [InlineData( /*lang=json,strict*/
        "{ \"SqlSourceManifest\": 1 }"
    )]
    public void Read_FirstLineThatIsNotTheVersion_SaysSo(string text) =>
        ReasonOf(text).ShouldBe("its first line is not 'SqlSourceManifest=1'");

    [Theory]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    public void Read_AnotherVersion_SaysThatThePackageAndTheToolAreOutOfStep(string version) =>
        ReasonOf($"SqlSourceManifest={version}\nProject=p\nthis line has no equals sign")
            .ShouldBe(
                $"it has version '{version}' of the format and this tool reads version 1.  "
                    + "The SqlSource package and the sqlsource tool are out of step: update the older one"
            );

    [Fact]
    public void Read_LineWithoutAnEqualsSign_NamesTheLine() =>
        ReasonOf("SqlSourceManifest=1\nProject=p\n\nCompile").ShouldBe("line 4 has no '='");

    [Fact]
    public void Read_MetadataBeforeAnyFile_NamesTheLine() =>
        ReasonOf("SqlSourceManifest=1\nProject=p\nFile.SqlSourceDialect=mssql\nFile=a.sql")
            .ShouldBe("line 3 gives metadata of a file before any file");

    [Theory]
    [InlineData("SqlSourceManifest=1")]
    [InlineData("SqlSourceManifest=1\nProject=")]
    [InlineData("SqlSourceManifest=1\nFile=a.sql")]
    public void Read_NoProject_SaysSo(string text) => ReasonOf(text).ShouldBe("it names no project");
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ManifestReaderTests`
Expected: the build fails: `ManifestReader` and `ProjectManifest` do not exist (CS0103, CS0246).

- [ ] **Step 3: Write the model and the reader**

Create `src/SqlSource.Tool/Projects/ProjectManifest.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// What the compiler is given for one project, as the target <c>SqlSourceWriteManifest</c> of the package wrote it.
/// </summary>
/// <remarks>
/// A class and not a record: its collections would compare by reference, and nothing compares two manifests.
/// </remarks>
internal sealed class ProjectManifest
{
    /// <summary>The full path of the project file.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>The framework the manifest was written for.  Empty for a project that is not of the SDK.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>The project's <c>LangVersion</c> as MSBuild has it: empty, a number, or a word as latest is.</summary>
    public required string LangVersion { get; init; }

    /// <summary>The constants the project's code is compiled with.</summary>
    public required ImmutableArray<string> DefineConstants { get; init; }

    /// <summary>The properties of the package that have a value, by name, trimmed.</summary>
    public required ImmutableDictionary<string, string> Properties { get; init; }

    /// <summary>The <c>.sql</c> files of the project, in the order of its items.</summary>
    public required ImmutableArray<ManifestFile> Files { get; init; }

    /// <summary>The full paths of the project's <c>Compile</c> items.</summary>
    public required ImmutableArray<string> CompileFiles { get; init; }
}
```

Create `src/SqlSource.Tool/Projects/ManifestFile.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// One <c>.sql</c> file of a project manifest.
/// </summary>
/// <param name="Path">The full path, as MSBuild gives it.</param>
/// <param name="Metadata">The metadata of the file's item that has a value, by name, trimmed.</param>
internal sealed record ManifestFile(string Path, ImmutableDictionary<string, string> Metadata);
```

Create `src/SqlSource.Tool/Projects/ManifestReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Reads a project manifest: lines of <c>key=value</c>, where the key ends at the first <c>=</c> and the value is
/// the rest of the line as it is.
/// </summary>
/// <remarks>
/// The format is a contract with <c>build/SqlSource.targets</c> of the package, which may be of another version than
/// the tool.  So a key that is not known is passed over, and so is an empty line: both are how a later package adds
/// to the format.  A manifest of another version is not read at all.
/// </remarks>
internal static class ManifestReader
{
    /// <summary>The version of the format that this reader reads.</summary>
    public const string Version = "1";

    private const string VersionKey = "SqlSourceManifest";
    private const string PropertyPrefix = "Property.";
    private const string FilePrefix = "File.";

    private static readonly char[] ConstantSeparators = [';', ','];

    /// <summary>
    /// The manifest, or null and why the text is not one.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="reason">What is wrong, as the end of a sentence; empty when nothing is.</param>
    public static ProjectManifest? Read(string text, out string reason)
    {
        // WriteLinesToFile writes none, but an editor or a copy may add one.
        var rest = text.AsSpan().TrimStart('﻿');

        string? project = null;
        var targetFramework = "";
        var langVersion = "";
        var constants = "";
        var properties = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = new List<(string Path, ImmutableDictionary<string, string>.Builder Metadata)>();
        var compileFiles = ImmutableArray.CreateBuilder<string>();

        var number = 0;
        while (TryReadLine(ref rest, out var line))
        {
            number++;
            if (number == 1)
            {
                if (!IsVersionLine(line, out reason))
                {
                    return null;
                }

                continue;
            }

            if (line.IsEmpty)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                reason = string.Create(CultureInfo.InvariantCulture, $"line {number} has no '='");
                return null;
            }

            var key = line[..separator];
            var value = line[(separator + 1)..].ToString();
            if (key.StartsWith(FilePrefix, StringComparison.Ordinal))
            {
                if (files.Count == 0)
                {
                    reason = string.Create(
                        CultureInfo.InvariantCulture,
                        $"line {number} gives metadata of a file before any file"
                    );
                    return null;
                }

                Set(files[^1].Metadata, key[FilePrefix.Length..], value);
            }
            else if (key.StartsWith(PropertyPrefix, StringComparison.Ordinal))
            {
                Set(properties, key[PropertyPrefix.Length..], value);
            }
            else
            {
                switch (key)
                {
                    case "Project":
                        project = value;
                        break;
                    case "TargetFramework":
                        targetFramework = value;
                        break;
                    case "LangVersion":
                        langVersion = value;
                        break;
                    case "DefineConstants":
                        constants = value;
                        break;
                    case "File":
                        files.Add((value, ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal)));
                        break;
                    case "Compile":
                        compileFiles.Add(value);
                        break;
                    default:
                        // A key of a later package.
                        break;
                }
            }
        }

        if (number == 0)
        {
            reason = "it is empty";
            return null;
        }

        if (string.IsNullOrEmpty(project))
        {
            reason = "it names no project";
            return null;
        }

        reason = "";
        return new ProjectManifest
        {
            ProjectPath = project,
            TargetFramework = targetFramework,
            LangVersion = langVersion,
            DefineConstants =
            [
                .. constants.Split(
                    ConstantSeparators,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ],
            Properties = properties.ToImmutable(),
            Files = [.. files.ConvertAll(static file => new ManifestFile(file.Path, file.Metadata.ToImmutable()))],
            CompileFiles = compileFiles.ToImmutable(),
        };
    }

    private static bool IsVersionLine(ReadOnlySpan<char> line, out string reason)
    {
        const string Prefix = VersionKey + "=";
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            reason = $"its first line is not '{Prefix}{Version}'";
            return false;
        }

        var version = line[Prefix.Length..];
        if (!version.SequenceEqual(Version))
        {
            reason =
                $"it has version '{version}' of the format and this tool reads version {Version}.  "
                + "The SqlSource package and the sqlsource tool are out of step: update the older one";
            return false;
        }

        reason = "";
        return true;
    }

    // A value that is empty is one that is not set, and is left out.
    private static void Set(ImmutableDictionary<string, string>.Builder values, ReadOnlySpan<char> name, string value)
    {
        if (value.Length > 0)
        {
            values[name.ToString()] = value;
        }
    }

    // The next line, without its line break: "\n", or "\r\n" as MSBuild writes it on Windows.
    private static bool TryReadLine(ref ReadOnlySpan<char> rest, out ReadOnlySpan<char> line)
    {
        if (rest.IsEmpty)
        {
            line = default;
            return false;
        }

        var end = rest.IndexOf('\n');
        if (end < 0)
        {
            line = rest;
            rest = default;
        }
        else
        {
            line = rest[..end];
            rest = rest[(end + 1)..];
        }

        if (!line.IsEmpty && line[^1] == '\r')
        {
            line = line[..^1];
        }

        return true;
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ManifestReaderTests`
Expected: every test passes.

- [ ] **Step 5: Write the failing test of `SQLSRC206`**

Change `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
+++ b/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
@@ -78,6 +78,16 @@ public class ToolDiagnosticsTests
         }
     }
 
+    [Fact]
+    public void ManifestCannotBeRead_Message_HoldsTheProjectAndTheReason() =>
+        string.Format(
+                CultureInfo.InvariantCulture,
+                ToolDiagnostics.ManifestCannotBeRead.MessageFormat.ToString(CultureInfo.InvariantCulture),
+                "/work/App.csproj",
+                "it names no project"
+            )
+            .ShouldBe("The project manifest of '/work/App.csproj' cannot be read: it names no project");
+
     [Fact]
     public void DirectoryCannotBeRead_Message_HoldsTheDirectoryAndTheReason() =>
         string.Format(
```

- [ ] **Step 6: Add the descriptor in its three other places**

Change `src/SqlSource/Diagnostics/ToolDiagnostics.cs`:

```diff
--- a/src/SqlSource/Diagnostics/ToolDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/ToolDiagnostics.cs
@@ -56,6 +56,17 @@ internal static class ToolDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor ManifestCannotBeRead = new(
+        id: "SQLSRC206",
+        title: "Project manifest cannot be read",
+        messageFormat: "The project manifest of '{0}' cannot be read: {1}",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc206",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     public static readonly DiagnosticDescriptor DirectoryCannotBeRead = new(
         id: "SQLSRC222",
         title: "Directory cannot be read",
@@ -71,5 +82,12 @@ internal static class ToolDiagnostics
     /// Every descriptor, in the order of its id.
     /// </summary>
     public static ImmutableArray<DiagnosticDescriptor> All { get; } =
-        ImmutableArray.Create(UnexpectedFailure, NoRunUnit, SeveralRunUnits, NotARunUnit, DirectoryCannotBeRead);
+        ImmutableArray.Create(
+            UnexpectedFailure,
+            NoRunUnit,
+            SeveralRunUnits,
+            NotARunUnit,
+            ManifestCannotBeRead,
+            DirectoryCannotBeRead
+        );
 }
```

Change `src/SqlSource/AnalyzerReleases.Unshipped.md`:

```diff
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -41,4 +41,5 @@ SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
 SQLSRC201 | SqlSource | Error | No project or solution found
 SQLSRC202 | SqlSource | Error | More than one project or solution found
 SQLSRC203 | SqlSource | Error | Path is not a project or a solution
+SQLSRC206 | SqlSource | Error | Project manifest cannot be read
 SQLSRC222 | SqlSource | Error | Directory cannot be read
```

Change `docs/diagnostics.md`:

````diff
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -40,6 +40,7 @@ Every problem SqlSource finds is an error, and none can be turned off or made a
 | [SQLSRC201](#sqlsrc201) | No project or solution found |
 | [SQLSRC202](#sqlsrc202) | More than one project or solution found |
 | [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
+| [SQLSRC206](#sqlsrc206) | Project manifest cannot be read |
 | [SQLSRC222](#sqlsrc222) | Directory cannot be read |
 
 Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.  Ids from 200 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1.
@@ -575,6 +576,19 @@ The path given to `sqlsource describe` does not exist, or is a file that is not
 
 Give the path of a solution, of a C# project, or of a directory that holds exactly one of them.
 
+## SQLSRC206
+
+**Project manifest cannot be read**
+
+For each project, `sqlsource` has MSBuild write a file that lists what the compiler is given: the project manifest, which the target `SqlSourceWriteManifest` of the SqlSource package writes.  The tool could not read what the target wrote.  The message ends with what is wrong.
+
+```console
+$ dotnet sqlsource describe
+/work/App/App.csproj : error SQLSRC206: The project manifest of '/work/App/App.csproj' cannot be read: it has version '2' of the format and this tool reads version 1.  The SqlSource package and the sqlsource tool are out of step: update the older one
+```
+
+The SqlSource package of the project and the `sqlsource` tool are of different versions.  Update the older of the two: the package in the project file, or the tool with `dotnet tool update SqlSource.Tool`.  The two are released together under one version number.
+
 ## SQLSRC222
 
 **Directory cannot be read**
````

- [ ] **Step 7: Run the tests of the diagnostics**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.ToolDiagnosticsTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.DiagnosticsDocumentTests`
Expected: every test passes: the document has a row and a section for `SQLSRC206`, between `SQLSRC203` and `SQLSRC222`.

- [ ] **Step 8: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Read the project manifest

ManifestReader reads the lines that the package's target writes into a
ProjectManifest, and says why when it cannot: SQLSRC206.  A key it does not
know is passed over, and a manifest of another version is not read.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 5: `IProcessRunner`, the runs of MSBuild and `SQLSRC205`

**Files:**
- Create: `src/SqlSource.Tool/Processes/IProcessRunner.cs`, `ProcessRequest.cs`, `ProcessResult.cs`, `ProcessRunner.cs`
- Create: `src/SqlSource.Tool/Projects/MSBuildProperty.cs`, `ProjectState.cs`, `ProjectEvaluation.cs`, `ProjectEvaluator.cs`
- Modify: `src/SqlSource.Tool/ToolHost.cs`, `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tool.Tests/ProcessRunnerTests.cs`, `MSBuildPropertyTests.cs`, `ProjectEvaluatorTests.cs`, `FixtureProjectTests.cs`
- Create: `tests/SqlSource.Tool.Tests/FakeProcessRunner.cs`, `FakeProject.cs`, `Hosts.cs`
- Modify: `tests/SqlSource.Tool.Tests/CliRun.cs`, `CliTests.cs`, `ToolHostTests.cs`, `Fixtures/FixtureProjects.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`
- Create: the fixtures `SecondOnly`, `Plain`, `StaleRestore` and `InSolution` under `tests/SqlSource.Tool.Tests/Fixtures/Projects/`
- Create: `docs/tech-debt/TD-0028-msbuild-runs-of-the-tool-are-not-verified-on-windows.md`
- Modify: `docs/tech-debt/README.md`, `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`, `src/SqlSource.Tool/AGENTS.md`, `CONTRIBUTING.md`

**Interfaces:**
- Consumes: `ManifestReader.Read` and `ToolDiagnostics.ManifestCannotBeRead` of task 4; the target of task 3; `SqlSourceImported` of task 2.
- Produces: `interface IProcessRunner { Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken); }`, `sealed record ProcessRequest(string Program, ImmutableArray<string> Arguments, string WorkingDirectory, ImmutableDictionary<string, string> SetVariables, ImmutableArray<string> RemovedVariables)`, `sealed record ProcessResult(int ExitCode, string Output, string Error)` with `const int NotStarted = -1`, all in `SqlSource.Tool.Processes`.
- Produces: `ToolHost(TextWriter Out, TextWriter Error, string WorkingDirectory, Func<string, string?> GetEnvironmentVariable, IProcessRunner Processes, string TempDirectory, int ProcessorCount)`.
- Produces: `static ToolDiagnostic ToolDiagnostic.ForFile(DiagnosticDescriptor descriptor, string path, params string[] arguments)`.
- Produces: `static Task<ImmutableArray<ProjectEvaluation>> ProjectEvaluator.EvaluateAsync(ToolHost host, IReadOnlyList<string> projects, string? solution, CancellationToken cancellationToken)`, with the constants `MaxAtOnce` (8), `MaxOutputLines` (20) and `ManifestTarget`.
- Produces: `sealed record ProjectEvaluation(string ProjectPath, ProjectState State, ProjectManifest? Manifest = null, ToolDiagnostic? Failure = null)` and `enum ProjectState { UsesSqlSource, DoesNotUseSqlSource, NotRestored, Failed }`.
- Produces: `static string MSBuildProperty.Switch(string name, string value)` and `Escape(string value)`.
- Produces, for tests: `FakeProcessRunner` with `Projects`, `Default`, `BeforeAnswer`, `Requests`, `MostAtOnce`, `static string? PropertyOf(ProcessRequest request, string name)` and `static bool IsManifestRun(ProcessRequest request)`; `FakeProject`; `Hosts.Create(...)` and `Hosts.Real(...)`; `CliRun.Processes`.

- [ ] **Step 1: Write the failing tests of the real runner**

Create `tests/SqlSource.Tool.Tests/ProcessRunnerTests.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests;

// The runner of a real run, with real programs: "dotnet", and three that every operating system has.
public class ProcessRunnerTests
{
    private static readonly ProcessRunner Runner = new();

    private static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], []);

    // Prints the environment, one variable on a line.
    private static ProcessRequest PrintEnvironment() =>
        OperatingSystem.IsWindows() ? Request("cmd", "/c", "set") : Request("env");

    // Waits for half a minute.
    private static ProcessRequest Wait() =>
        OperatingSystem.IsWindows() ? Request("ping", "-n", "30", "127.0.0.1") : Request("sleep", "30");

    [Fact]
    public async Task Run_Dotnet_GivesItsExitCodeAndItsOutput()
    {
        var result = await Runner.RunAsync(Request("dotnet", "--version"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldNotBeEmpty();
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput()
    {
        var result = await Runner.RunAsync(
            Request("dotnet", "sqlsource-no-such-command"),
            TestContext.Current.CancellationToken
        );

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Run_VariableSet_ReachesTheProgramAndVariableRemovedDoesNot()
    {
        Environment.SetEnvironmentVariable("SQLSOURCE_TEST_REMOVED", "removed");
        Environment.SetEnvironmentVariable("SQLSOURCE_TEST_KEPT", "kept");
        var request = PrintEnvironment() with
        {
            SetVariables = ImmutableDictionary<string, string>.Empty.Add("SQLSOURCE_TEST_SET", "set"),
            RemovedVariables = ["SQLSOURCE_TEST_REMOVED", "SQLSOURCE_TEST_NEVER_SET"],
        };

        var result = await Runner.RunAsync(request, TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("SQLSOURCE_TEST_SET=set");
        result.Output.ShouldContain("SQLSOURCE_TEST_KEPT=kept");
        result.Output.ShouldNotContain("SQLSOURCE_TEST_REMOVED");
    }

    [Fact]
    public async Task Run_ProgramThatReadsItsInput_FindsTheEndOfIt()
    {
        // "sort" with no file reads its input to the end.  With an input left open it would never end.
        var result = await Runner.RunAsync(Request("sort"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatDoesNotExist_IsNotStartedAndSaysWhy()
    {
        var result = await Runner.RunAsync(Request("sqlsource-no-such-program"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(ProcessResult.NotStarted);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Run_Cancelled_KillsTheProgramAndThrows()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var time = Stopwatch.StartNew();

        var run = Runner.RunAsync(Wait(), cancel.Token);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(200));

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
        time.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(Runner.RunAsync(Wait(), cancelled.Token));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ProcessRunnerTests`
Expected: the build fails: the namespace `SqlSource.Tool.Processes` does not exist (CS0234).

- [ ] **Step 3: Write the runner**

Create `src/SqlSource.Tool/Processes/IProcessRunner.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Processes;

/// <summary>
/// Runs a program and waits for it.  Every process the tool starts goes through the runner of its
/// <see cref="ToolHost" />, so that a test can answer in the program's place.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs the program to its end.  A program that cannot be started gives the exit code
    /// <see cref="ProcessResult.NotStarted" /> and the reason as its error output.
    /// </summary>
    /// <exception cref="System.OperationCanceledException">
    /// The token was cancelled.  The process, and every process it started, was killed first.
    /// </exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
```

Create `src/SqlSource.Tool/Processes/ProcessRequest.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Tool.Processes;

/// <summary>
/// A program to run.
/// </summary>
/// <param name="Program">The program: a path, or a name that is looked for on the path.</param>
/// <param name="Arguments">Its arguments, each as the program is to see it: nothing is quoted or split.</param>
/// <param name="WorkingDirectory">The full path of the directory it runs in.</param>
/// <param name="SetVariables">Environment variables it gets beside the ones of this process.</param>
/// <param name="RemovedVariables">Environment variables of this process that it does not get.</param>
internal sealed record ProcessRequest(
    string Program,
    ImmutableArray<string> Arguments,
    string WorkingDirectory,
    ImmutableDictionary<string, string> SetVariables,
    ImmutableArray<string> RemovedVariables
);
```

Create `src/SqlSource.Tool/Processes/ProcessResult.cs`:

```csharp
namespace SqlSource.Tool.Processes;

/// <summary>
/// What a program left behind.
/// </summary>
/// <param name="ExitCode">Its exit code, or <see cref="NotStarted" />.</param>
/// <param name="Output">Everything it wrote to its standard output, read as UTF-8.</param>
/// <param name="Error">Everything it wrote to its error output, read as UTF-8.</param>
internal sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    /// <summary>
    /// The exit code of a program that could not be started.  No program gives it: an exit code is not negative
    /// on Unix, and on Windows a negative one is a crash.
    /// </summary>
    public const int NotStarted = -1;
}
```

Create `src/SqlSource.Tool/Processes/ProcessRunner.cs`:

```csharp
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Processes;

/// <summary>
/// The runner of a real run, over <see cref="Process" />.
/// </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    // Without a byte order mark: the encoding is also what the process is told its input has.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(request.Program)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in request.Arguments)
        {
            // ArgumentList quotes each argument as the operating system needs, so a path with a space is one.
            start.ArgumentList.Add(argument);
        }

        foreach (var name in request.RemovedVariables)
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var (name, value) in request.SetVariables)
        {
            start.Environment[name] = value;
        }

        using var process = new Process { StartInfo = start };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception exception)
        {
            // No such program, or no such working directory.
            return new ProcessResult(ProcessResult.NotStarted, "", exception.Message);
        }

        try
        {
            // A program that asks a question would wait for an answer for ever.  It finds the end of its input.
            process.StandardInput.Close();

            // Both are read while the program runs: one that fills an output nobody reads stops and waits.
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            // MSBuild starts processes of its own, and they would go on without this one.
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It ended between the cancellation and here.
        }
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ProcessRunnerTests`
Expected: seven tests pass.  The one that cancels takes a fifth of a second, not the half minute of its program.

- [ ] **Step 5: Write the failing test of `SQLSRC205`**

Change `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
+++ b/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
@@ -78,6 +78,15 @@ public class ToolDiagnosticsTests
         }
     }
 
+    [Fact]
+    public void ProjectCannotBeEvaluated_Message_HoldsTheProject() =>
+        string.Format(
+                CultureInfo.InvariantCulture,
+                ToolDiagnostics.ProjectCannotBeEvaluated.MessageFormat.ToString(CultureInfo.InvariantCulture),
+                "/work/App.csproj"
+            )
+            .ShouldBe("MSBuild could not evaluate '/work/App.csproj'");
+
     [Fact]
     public void ManifestCannotBeRead_Message_HoldsTheProjectAndTheReason() =>
         string.Format(
```

- [ ] **Step 6: Add the descriptor in its three other places**

Change `src/SqlSource/Diagnostics/ToolDiagnostics.cs`:

```diff
--- a/src/SqlSource/Diagnostics/ToolDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/ToolDiagnostics.cs
@@ -56,6 +56,17 @@ internal static class ToolDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor ProjectCannotBeEvaluated = new(
+        id: "SQLSRC205",
+        title: "Project could not be evaluated",
+        messageFormat: "MSBuild could not evaluate '{0}'",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc205",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     public static readonly DiagnosticDescriptor ManifestCannotBeRead = new(
         id: "SQLSRC206",
         title: "Project manifest cannot be read",
@@ -87,6 +98,7 @@ internal static class ToolDiagnostics
             NoRunUnit,
             SeveralRunUnits,
             NotARunUnit,
+            ProjectCannotBeEvaluated,
             ManifestCannotBeRead,
             DirectoryCannotBeRead
         );
```

Change `src/SqlSource/AnalyzerReleases.Unshipped.md`:

```diff
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -41,5 +41,6 @@ SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
 SQLSRC201 | SqlSource | Error | No project or solution found
 SQLSRC202 | SqlSource | Error | More than one project or solution found
 SQLSRC203 | SqlSource | Error | Path is not a project or a solution
+SQLSRC205 | SqlSource | Error | Project could not be evaluated
 SQLSRC206 | SqlSource | Error | Project manifest cannot be read
 SQLSRC222 | SqlSource | Error | Directory cannot be read
```

Change `docs/diagnostics.md`:

````diff
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -40,6 +40,7 @@ Every problem SqlSource finds is an error, and none can be turned off or made a
 | [SQLSRC201](#sqlsrc201) | No project or solution found |
 | [SQLSRC202](#sqlsrc202) | More than one project or solution found |
 | [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
+| [SQLSRC205](#sqlsrc205) | Project could not be evaluated |
 | [SQLSRC206](#sqlsrc206) | Project manifest cannot be read |
 | [SQLSRC222](#sqlsrc222) | Directory cannot be read |
 
@@ -576,6 +577,21 @@ The path given to `sqlsource describe` does not exist, or is a file that is not
 
 Give the path of a solution, of a C# project, or of a directory that holds exactly one of them.
 
+## SQLSRC205
+
+**Project could not be evaluated**
+
+`sqlsource` asks MSBuild about each project with `dotnet msbuild`: first what the project is, then for the list of what the compiler is given, which a target of the SqlSource package writes.  One of those runs failed.  The lines under the error give the reason, and then the first twenty lines that MSBuild wrote.
+
+```console
+$ dotnet sqlsource describe
+/work/App/App.csproj : error SQLSRC205: MSBuild could not evaluate '/work/App/App.csproj'
+    reason: dotnet msbuild ended with the exit code 1
+    msbuild: /work/App/App.csproj(4,5): error MSB4019: The imported project "/work/Shared.props" was not found.
+```
+
+Mend what MSBuild reports; `dotnet build` of the project reports the same.  When MSBuild says that the target `SqlSourceWriteManifest` does not exist, the SqlSource package of the project is older than the tool: update the package.  When the reason is that `dotnet` could not be started, the .NET SDK is not installed or not on the path: the tool needs it, and not only the runtime it runs on.
+
 ## SQLSRC206
 
 **Project manifest cannot be read**
````

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.ToolDiagnosticsTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.DiagnosticsDocumentTests`
Expected: every test passes.

- [ ] **Step 7: Write the fake MSBuild and the failing tests of the evaluator**

`FakeProcessRunner` reads a command line as MSBuild would, and throws at a `;` or a `,` in the value of a `-p:` that was not escaped.  `CliRun` and the one test of `CliTests` that makes a host give the three new members; `ToolHostTests` expects them of the real host.

Create `tests/SqlSource.Tool.Tests/FakeProject.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool.Tests;

// What FakeProcessRunner answers for one project, in MSBuild's place.  As it is made, it is a restored project
// with one target framework that uses SqlSource and whose manifest holds nothing but the project.
internal sealed class FakeProject
{
    // The value of SqlSourceImported.  With several frameworks it is what an evaluation with a framework gives:
    // one without gives none, as NuGet imports the package's props under a condition on the framework.
    public string Imported { get; set; } = "true";

    // The value of SqlSourceImported for a framework that differs from Imported.
    public Dictionary<string, string> ImportedByFramework { get; } = [];

    public string TargetFramework { get; set; } = "net10.0";

    public string TargetFrameworks { get; set; } = "";

    public string ProjectAssetsFile { get; set; } = "";

    public List<string> PackageReferences { get; } = [];

    // What the evaluation prints in place of its JSON.
    public string? EvaluationOutput { get; set; }

    public int EvaluationExitCode { get; set; }

    public int TargetExitCode { get; set; }

    // What a run that fails prints.
    public string Output { get; set; } = "";

    public string Error { get; set; } = "";

    // Whether the target writes the file it was asked for.
    public bool WritesManifest { get; set; } = true;

    // The text of the manifest; when null, the version, the project and the framework.
    public string? Manifest { get; set; }

    public ProcessResult Evaluate(ProcessRequest request)
    {
        if (EvaluationExitCode != 0)
        {
            return new ProcessResult(EvaluationExitCode, Output, Error);
        }

        if (EvaluationOutput is not null)
        {
            return new ProcessResult(0, EvaluationOutput, Error);
        }

        var framework = FakeProcessRunner.PropertyOf(request, "TargetFramework");
        var several = TargetFrameworks.Length > 0;
        var imported =
            several && framework is null ? "" : ImportedByFramework.GetValueOrDefault(framework ?? "", Imported);
        var json = JsonSerializer.Serialize(
            new
            {
                Properties = new
                {
                    SqlSourceImported = imported,
                    TargetFramework = several ? framework ?? "" : TargetFramework,
                    TargetFrameworks,
                    ProjectAssetsFile,
                },
                Items = new
                {
                    PackageReference = PackageReferences.Select(name => new { Identity = name, Version = "1.0.0" }),
                },
            }
        );
        return new ProcessResult(0, json, "");
    }

    public ProcessResult WriteManifest(ProcessRequest request)
    {
        if (TargetExitCode != 0)
        {
            return new ProcessResult(TargetExitCode, Output, Error);
        }

        if (WritesManifest)
        {
            var file = FakeProcessRunner.PropertyOf(request, "SqlSourceManifestFile")!;
            var framework = FakeProcessRunner.PropertyOf(request, "TargetFramework") ?? TargetFramework;
            File.WriteAllText(
                file,
                Manifest ?? $"SqlSourceManifest=1\nProject={request.Arguments[1]}\nTargetFramework={framework}\n"
            );
        }

        return new ProcessResult(0, Output, Error);
    }
}
```

Create `tests/SqlSource.Tool.Tests/FakeProcessRunner.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Tests;

// Stands in for "dotnet msbuild": answers the evaluation of a project with JSON, and the manifest target by writing
// the file it was asked for.  It reads the command line as MSBuild would, so a test of the tool's arguments looks at
// Requests.
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ConcurrentQueue<ProcessRequest> _requests = new();

    private int _running;

    private int _mostAtOnce;

    // The projects by the full path of their file, as the tool gives it.  A project that is not here is answered by
    // Default.
    public Dictionary<string, FakeProject> Projects { get; } = [];

    public FakeProject Default { get; set; } = new();

    // Runs before each answer, while the request counts as running.
    public Func<ProcessRequest, CancellationToken, Task>? BeforeAnswer { get; set; }

    // Every request, in the order they came.
    public IReadOnlyList<ProcessRequest> Requests => [.. _requests];

    // The largest number of requests that were running at one time.
    public int MostAtOnce => _mostAtOnce;

    // The value of a "-p:Name=value" switch of a request, as MSBuild reads it, or null when it has none.
    public static string? PropertyOf(ProcessRequest request, string name)
    {
        var prefix = "-p:" + name + "=";
        var argument = request.Arguments.LastOrDefault(argument =>
            argument.StartsWith(prefix, StringComparison.Ordinal)
        );
        if (argument is null)
        {
            return null;
        }

        var value = argument[prefix.Length..];
        if (value.AsSpan().IndexOfAny(';', ',') >= 0)
        {
            throw new InvalidOperationException($"MSB1006: MSBuild would split the value of -p:{name} at a ; or a ,");
        }

        return Uri.UnescapeDataString(value);
    }

    public static bool IsManifestRun(ProcessRequest request) =>
        request.Arguments.Contains("-t:" + ProjectEvaluator.ManifestTarget);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        var running = Interlocked.Increment(ref _running);
        int most;
        while (running > (most = Volatile.Read(ref _mostAtOnce)))
        {
            _ = Interlocked.CompareExchange(ref _mostAtOnce, running, most);
        }

        try
        {
            if (BeforeAnswer is not null)
            {
                await BeforeAnswer(request, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var project = Projects.GetValueOrDefault(request.Arguments[1], Default);
            return IsManifestRun(request) ? project.WriteManifest(request) : project.Evaluate(request);
        }
        finally
        {
            _ = Interlocked.Decrement(ref _running);
        }
    }
}
```

Create `tests/SqlSource.Tool.Tests/Hosts.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool.Tests;

// A host for a test that calls a part of the tool and not the whole command: nothing is written anywhere.
internal static class Hosts
{
    public static ToolHost Create(
        string workingDirectory,
        IProcessRunner processes,
        string tempDirectory,
        int processorCount = 4,
        IReadOnlyDictionary<string, string>? environment = null
    ) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            name => environment?.GetValueOrDefault(name),
            processes,
            tempDirectory,
            processorCount
        );

    // The environment of the test process, for a run of the real "dotnet".
    public static ToolHost Real(string workingDirectory, string tempDirectory) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            tempDirectory,
            Environment.ProcessorCount
        );
}
```

Change `tests/SqlSource.Tool.Tests/CliRun.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/CliRun.cs
+++ b/tests/SqlSource.Tool.Tests/CliRun.cs
@@ -16,6 +16,9 @@ internal sealed class CliRun : IDisposable
 
     public Dictionary<string, string> Environment { get; } = [];
 
+    // Answers every "dotnet msbuild" of the run.  A project it was told nothing about uses SqlSource.
+    public FakeProcessRunner Processes { get; } = new();
+
     // Set to stand in for standard output, for a test of a writer that fails.
     public TextWriter? Out { get; init; }
 
@@ -34,7 +37,15 @@ internal sealed class CliRun : IDisposable
     {
         using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
         using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
-        var host = new ToolHost(Out ?? output, error, Folder.Path, name => Environment.GetValueOrDefault(name));
+        var host = new ToolHost(
+            Out ?? output,
+            error,
+            Folder.Path,
+            name => Environment.GetValueOrDefault(name),
+            Processes,
+            Folder.CreateFolder("tmp"),
+            ProcessorCount: 4
+        );
 
         var exitCode = await Cli.RunAsync(args, host, cancellationToken);
 
```

Change `tests/SqlSource.Tool.Tests/CliTests.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/CliTests.cs
+++ b/tests/SqlSource.Tool.Tests/CliTests.cs
@@ -140,7 +140,15 @@ public class CliTests
     {
         // System.CommandLine's default for a flag takes an optional value, which UsageCheck cannot tell from the
         // argument after the flag.
-        var host = new ToolHost(TextWriter.Null, TextWriter.Null, "/", static _ => null);
+        var host = new ToolHost(
+            TextWriter.Null,
+            TextWriter.Null,
+            "/",
+            static _ => null,
+            new FakeProcessRunner(),
+            "/",
+            ProcessorCount: 1
+        );
         var commands = new List<Command> { Cli.BuildCommands(host, new Reporter(TextWriter.Null)) };
 
         for (var index = 0; index < commands.Count; index++)
```

Change `tests/SqlSource.Tool.Tests/ToolHostTests.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/ToolHostTests.cs
+++ b/tests/SqlSource.Tool.Tests/ToolHostTests.cs
@@ -1,6 +1,7 @@
 using System;
 using System.IO;
 using Shouldly;
+using SqlSource.Tool.Processes;
 using Xunit;
 
 namespace SqlSource.Tool.Tests;
@@ -17,5 +18,8 @@ public class ToolHostTests
         host.WorkingDirectory.ShouldBe(Directory.GetCurrentDirectory());
         host.GetEnvironmentVariable("PATH").ShouldBe(Environment.GetEnvironmentVariable("PATH"));
         host.GetEnvironmentVariable("SQLSOURCE_NOT_A_VARIABLE").ShouldBeNull();
+        _ = host.Processes.ShouldBeOfType<ProcessRunner>();
+        host.TempDirectory.ShouldBe(Path.GetTempPath());
+        host.ProcessorCount.ShouldBe(Environment.ProcessorCount);
     }
 }
```

Create `tests/SqlSource.Tool.Tests/MSBuildPropertyTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public class MSBuildPropertyTests
{
    [Theory]
    [InlineData("/work/App", "/work/App")]
    [InlineData("C:\\work\\App\\", "C:\\work\\App\\")]
    [InlineData("/work/Acme, Inc/", "/work/Acme%2C Inc/")]
    [InlineData("/work/a;b/", "/work/a%3Bb/")]
    [InlineData("/work/100%/", "/work/100%25/")]
    // The percent sign first: "%3B" as a user typed it is three characters, and must come back as three.
    [InlineData("/work/%3B;,/", "/work/%253B%3B%2C/")]
    [InlineData("", "")]
    public void Escape_Value_IsWhatMSBuildReadsBackWhole(string value, string expected) =>
        MSBuildProperty.Escape(value).ShouldBe(expected);

    [Fact]
    public void Switch_NameAndValue_IsOneArgument() =>
        MSBuildProperty.Switch("SolutionDir", "/work/Acme, Inc/").ShouldBe("-p:SolutionDir=/work/Acme%2C Inc/");
}
```

Create `tests/SqlSource.Tool.Tests/ProjectEvaluatorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// How the tool asks MSBuild about a project, with a runner that answers in MSBuild's place.  FixtureProjectTests
// asks the real one.
public sealed class ProjectEvaluatorTests : IDisposable
{
    private static readonly string[] Evaluation =
    [
        "-nologo",
        "-getProperty:SqlSourceImported",
        "-getProperty:TargetFramework",
        "-getProperty:TargetFrameworks",
        "-getProperty:ProjectAssetsFile",
        "-getItem:PackageReference",
    ];

    private readonly TempFolder _folder = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly Dictionary<string, string> _environment = [];
    private readonly string _project;
    private string _temp;
    private int _processors = 4;

    public ProjectEvaluatorTests()
    {
        _project = _folder.WriteFile("App/App.csproj");
        _temp = _folder.CreateFolder("tmp");
    }

    public void Dispose() => _folder.Dispose();

    private Task<System.Collections.Immutable.ImmutableArray<ProjectEvaluation>> EvaluateAsync(
        IReadOnlyList<string> projects,
        string? solution = null,
        CancellationToken? cancellationToken = null
    ) =>
        ProjectEvaluator.EvaluateAsync(
            Hosts.Create(_folder.Path, _runner, _temp, _processors, _environment),
            projects,
            solution,
            cancellationToken ?? TestContext.Current.CancellationToken
        );

    private async Task<ProjectEvaluation> EvaluateAsync(string? solution = null) =>
        (await EvaluateAsync([_project], solution)).ShouldHaveSingleItem();

    private string[] Projects(int count) =>
        [.. Enumerable.Range(0, count).Select(index => _folder.WriteFile($"P{index}/P{index}.csproj"))];

    [Fact]
    public async Task Evaluate_ProjectWithOneFramework_EvaluatesItAndRunsTheManifestTarget()
    {
        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        evaluation.ProjectPath.ShouldBe(_project);
        evaluation.Manifest.ShouldNotBeNull().ProjectPath.ShouldBe(_project);
        evaluation.Failure.ShouldBeNull();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests[0].Arguments.ShouldBe(["msbuild", _project, .. Evaluation]);
        var target = _runner.Requests[1].Arguments;
        target.Length.ShouldBe(5);
        target.Take(4).ShouldBe(["msbuild", _project, "-nologo", "-t:SqlSourceWriteManifest"]);
        target[4].ShouldStartWith("-p:SqlSourceManifestFile=" + Path.Combine(_temp, "sqlsource-"));
        target[4].ShouldEndWith(Path.DirectorySeparatorChar + "0.manifest");
    }

    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_AsksAgainWithTheFirstAndWritesItsManifest()
    {
        _runner.Default = new FakeProject { TargetFrameworks = " net8.0 ; net10.0" };

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        evaluation.Manifest.ShouldNotBeNull().TargetFramework.ShouldBe("net8.0");
        _runner.Requests.Count.ShouldBe(3);
        _runner.Requests[0].Arguments.ShouldBe(["msbuild", _project, .. Evaluation]);
        _runner.Requests[1].Arguments.ShouldBe(["msbuild", _project, .. Evaluation, "-p:TargetFramework=net8.0"]);
        _runner.Requests[2].Arguments[3].ShouldBe("-t:SqlSourceWriteManifest");
        _runner.Requests[2].Arguments[5].ShouldBe("-p:TargetFramework=net8.0");
        _runner.Requests[2].Arguments.Length.ShouldBe(6);
    }

    // With no framework, NuGet's condition on TargetFramework keeps the package's props out, so the first
    // evaluation never has the marker.  The second is the one the table reads.
    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_ReadsTheTableFromTheSecondEvaluation()
    {
        var assets = _folder.WriteFile("App/obj/project.assets.json");
        var project = new FakeProject { TargetFrameworks = "net8.0;net10.0", ProjectAssetsFile = assets };
        project.ImportedByFramework["net8.0"] = "";
        _runner.Default = project;

        var evaluation = await EvaluateAsync();

        // TD-0026: SqlSource for the second framework alone is no SqlSource.
        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource);
        _runner.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Evaluate_ProjectWithBothKindsOfFramework_KeepsTheOneItHas()
    {
        _runner.Default = new FakeProject { TargetFramework = "net9.0" };

        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "TargetFramework") == null);
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolution_GivesBothRunsWhatABuildOfTheSolutionGives()
    {
        var solution = _folder.WriteFile("Acme, Inc;100%/App.slnx");
        var directory = Path.GetDirectoryName(solution) + Path.DirectorySeparatorChar;
        _runner.Default = new FakeProject { TargetFrameworks = "net8.0;net10.0" };

        var evaluation = await EvaluateAsync(solution);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        _runner.Requests.Count.ShouldBe(3);
        foreach (var request in _runner.Requests)
        {
            // The fake reads a switch as MSBuild does, and throws at a ; or a , that was not escaped.
            FakeProcessRunner.PropertyOf(request, "SolutionDir").ShouldBe(directory);
            FakeProcessRunner.PropertyOf(request, "SolutionPath").ShouldBe(solution);
            FakeProcessRunner.PropertyOf(request, "SolutionName").ShouldBe("App");
            FakeProcessRunner.PropertyOf(request, "SolutionFileName").ShouldBe("App.slnx");
            FakeProcessRunner.PropertyOf(request, "SolutionExt").ShouldBe(".slnx");
            request
                .Arguments.Single(argument => argument.StartsWith("-p:SolutionDir=", StringComparison.Ordinal))
                .ShouldEndWith("Acme%2C Inc%3B100%25" + Path.DirectorySeparatorChar);
        }
    }

    [Fact]
    public async Task Evaluate_ProjectAlone_GivesNoPropertyOfASolution()
    {
        _ = await EvaluateAsync();

        _runner.Requests.ShouldAllBe(request =>
            !request.Arguments.Any(argument => argument.StartsWith("-p:Solution", StringComparison.Ordinal))
        );
    }

    [Fact]
    public async Task Evaluate_TemporaryDirectoryWithCharactersThatMSBuildReads_NamesTheManifestEscaped()
    {
        _temp = _folder.CreateFolder("tmp;a,b%41");

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        _runner.Requests[1].Arguments[4].ShouldContain("tmp%3Ba%2Cb%2541");
        FakeProcessRunner.PropertyOf(_runner.Requests[1], "SqlSourceManifestFile").ShouldStartWith(_temp);
    }

    // The table of the spec: the first row that holds.
    [Theory]
    [InlineData("true", "", "", nameof(ProjectState.UsesSqlSource))]
    [InlineData("True", "SqlSource", "missing", nameof(ProjectState.UsesSqlSource))]
    [InlineData("", "SqlSource", "exists", nameof(ProjectState.NotRestored))]
    [InlineData("", "Dapper;sqlsource", "", nameof(ProjectState.NotRestored))]
    [InlineData("false", "Dapper", "", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "", "missing", nameof(ProjectState.NotRestored))]
    [InlineData("", "SqlSource.Tool", "missing", nameof(ProjectState.NotRestored))]
    [InlineData("", "Dapper", "exists", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "SqlSource.Tool", "exists", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "", "relative", nameof(ProjectState.DoesNotUseSqlSource))]
    public async Task Evaluate_Project_IsReadByTheFirstRowOfTheTableThatHolds(
        string imported,
        string references,
        string assets,
        string state
    )
    {
        var expected = Enum.Parse<ProjectState>(state);
        var existing = _folder.WriteFile("App/obj/project.assets.json");
        var project = new FakeProject
        {
            Imported = imported,
            ProjectAssetsFile = assets switch
            {
                "exists" => existing,
                "relative" => "obj/project.assets.json",
                "missing" => _folder.PathOf("App/obj/none/project.assets.json"),
                _ => "",
            },
        };
        project.PackageReferences.AddRange(references.Split(';', StringSplitOptions.RemoveEmptyEntries));
        _runner.Default = project;

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(expected);
        evaluation.Failure.ShouldBeNull();
        // The manifest target is run for a project that uses SqlSource, and for no other.
        _runner.Requests.Count.ShouldBe(expected == ProjectState.UsesSqlSource ? 2 : 1);
        (evaluation.Manifest is not null).ShouldBe(expected == ProjectState.UsesSqlSource);
    }

    private static void ShouldBeSqlsrc205(ProjectEvaluation evaluation, params (string Label, string Text)[] lines)
    {
        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Manifest.ShouldBeNull();
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure.Path.ShouldBe(evaluation.ProjectPath);
        failure.Position.ShouldBeNull();
        failure.Arguments.ShouldBe([evaluation.ProjectPath]);
        failure.Lines.ShouldBe(lines.Select(line => new ContinuationLine(line.Label, line.Text)));
    }

    [Fact]
    public async Task Evaluate_EvaluationThatFails_IsSqlsrc205WithMSBuildsErrorOutputFirst()
    {
        _runner.Default = new FakeProject
        {
            EvaluationExitCode = 1,
            Error = "App.csproj(3,5): error MSB4019: no such import\r\n\n   \n",
            Output = "\nBuild FAILED.  \n",
        };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet msbuild ended with the exit code 1"),
            ("msbuild", "App.csproj(3,5): error MSB4019: no such import"),
            ("msbuild", "Build FAILED.")
        );
        _runner.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Evaluate_RunThatPrintsManyLines_ShowsTheFirstTwenty()
    {
        _runner.Default = new FakeProject
        {
            EvaluationExitCode = 1,
            Error = string.Join('\n', Enumerable.Range(1, 15).Select(number => $"error {number}")),
            Output = string.Join('\n', Enumerable.Range(1, 15).Select(number => $"line {number}")),
        };

        var lines = (await EvaluateAsync()).Failure.ShouldNotBeNull().Lines;

        lines.Count.ShouldBe(1 + ProjectEvaluator.MaxOutputLines);
        lines[1].ShouldBe(new ContinuationLine("msbuild", "error 1"));
        lines[15].ShouldBe(new ContinuationLine("msbuild", "error 15"));
        lines[^1].ShouldBe(new ContinuationLine("msbuild", "line 5"));
    }

    [Theory]
    [InlineData("MSBuild version 18.0.0\n")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData( /*lang=json,strict*/
        "{\"Properties\":{\"SqlSourceImported\":\"true\"}}"
    )]
    [InlineData(
        "{\"Properties\":{\"SqlSourceImported\":true,\"TargetFramework\":\"\",\"TargetFrameworks\":\"\","
            + "\"ProjectAssetsFile\":\"\"},\"Items\":{\"PackageReference\":[]}}"
    )]
    [InlineData(
        "{\"Properties\":{\"SqlSourceImported\":\"\",\"TargetFramework\":\"\",\"TargetFrameworks\":\"\","
            + "\"ProjectAssetsFile\":\"\"},\"Items\":{\"PackageReference\":[{\"Version\":\"1.0.0\"}]}}"
    )]
    public async Task Evaluate_OutputThatIsNotTheJsonExpected_IsSqlsrc205(string output)
    {
        _runner.Default = new FakeProject { EvaluationOutput = output };

        var failure = (await EvaluateAsync()).Failure.ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure
            .Lines[0]
            .ShouldBe(new ContinuationLine("reason", "dotnet msbuild did not print the JSON of an evaluation"));
        _runner.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Evaluate_ManifestTargetThatFails_IsSqlsrc205WithItsOutput()
    {
        _runner.Default = new FakeProject
        {
            TargetExitCode = 1,
            Output = "App.csproj : error MSB4057: The target \"SqlSourceWriteManifest\" does not exist in the project.",
        };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet msbuild ended with the exit code 1"),
            (
                "msbuild",
                "App.csproj : error MSB4057: The target \"SqlSourceWriteManifest\" does not exist in the project."
            )
        );
    }

    [Fact]
    public async Task Evaluate_ManifestTargetThatWritesNoFile_IsSqlsrc205()
    {
        _runner.Default = new FakeProject { WritesManifest = false, Output = "Build succeeded." };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "the target SqlSourceWriteManifest left no file that can be read"),
            ("msbuild", "Build succeeded.")
        );
    }

    [Fact]
    public async Task Evaluate_DotnetThatCannotBeStarted_IsSqlsrc205WithTheReason()
    {
        _runner.Default = new FakeProject { EvaluationExitCode = -1, Error = "No such file or directory" };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet could not be started"),
            ("msbuild", "No such file or directory")
        );
    }

    [Fact]
    public async Task Evaluate_ManifestThatCannotBeRead_IsSqlsrc206WithTheReason()
    {
        _runner.Default = new FakeProject { Manifest = "SqlSourceManifest=1\nCompile=/work/A.cs\n" };

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Manifest.ShouldBeNull();
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC206");
        failure.Path.ShouldBe(_project);
        failure.Arguments.ShouldBe([_project, "it names no project"]);
        failure.Lines.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/opt/dotnet/dotnet", "/opt/dotnet/dotnet")]
    [InlineData("", "dotnet")]
    [InlineData(null, "dotnet")]
    public async Task Evaluate_Project_RunsTheDotnetThatStartedTheToolOrTheOneOnThePath(
        string? hostPath,
        string expected
    )
    {
        if (hostPath is not null)
        {
            _environment["DOTNET_HOST_PATH"] = hostPath;
        }

        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests.ShouldAllBe(request => request.Program == expected);
    }

    [Fact]
    public async Task Evaluate_Project_RunsInTheProjectsFolderWithItsVariables()
    {
        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        foreach (var request in _runner.Requests)
        {
            // The project's own global.json picks the SDK.
            request.WorkingDirectory.ShouldBe(Path.GetDirectoryName(_project));
            request
                .SetVariables.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value)
                .ShouldBe(["DOTNET_CLI_FORCE_UTF8_ENCODING=true", "DOTNET_NOLOGO=true"]);
            request.RemovedVariables.ShouldBe(["MSBuildSDKsPath", "MSBuildExtensionsPath"]);
        }
    }

    [Fact]
    public async Task Evaluate_NoProjects_StartsNothingAndMakesNoFolder()
    {
        (await EvaluateAsync([])).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_SeveralProjects_GivesThemInTheirOrderWhateverEndsFirst()
    {
        var projects = Projects(6);
        // The first project is the last to answer.
        _runner.BeforeAnswer = (request, token) =>
            Task.Delay(60 - (10 * Array.IndexOf(projects, request.Arguments[1])), token);

        var evaluations = await EvaluateAsync(projects);

        evaluations.Select(evaluation => evaluation.ProjectPath).ShouldBe(projects);
        evaluations.Select(evaluation => evaluation.Manifest.ShouldNotBeNull().ProjectPath).ShouldBe(projects);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(64, ProjectEvaluator.MaxAtOnce)]
    [InlineData(0, 1)]
    public async Task Evaluate_SeveralProjects_AsksAboutAsManyAtOnceAsTheMachineHasProcessorsAndEightAtMost(
        int processors,
        int expected
    )
    {
        _processors = processors;
        _runner.BeforeAnswer = (_, token) => Task.Delay(30, token);

        var evaluations = await EvaluateAsync(Projects(20));

        evaluations.ShouldAllBe(evaluation => evaluation.State == ProjectState.UsesSqlSource);
        _runner.MostAtOnce.ShouldBe(expected);
    }

    // Two runs at one time, as from two terminals, or from an editor and a terminal: each has a folder of its own.
    [Fact]
    public async Task Evaluate_TwoRunsAtOnceOnOneProject_DoNotShareAManifestFile()
    {
        _runner.BeforeAnswer = (_, token) => Task.Delay(20, token);

        var both = await Task.WhenAll(EvaluateAsync([_project]), EvaluateAsync([_project]));

        both.ShouldAllBe(evaluations => evaluations[0].State == ProjectState.UsesSqlSource);
        _runner
            .Requests.Where(FakeProcessRunner.IsManifestRun)
            .Select(request => FakeProcessRunner.PropertyOf(request, "SqlSourceManifestFile"))
            .Distinct()
            .Count()
            .ShouldBe(2);
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_AfterTheRun_TheTemporaryFolderIsGone()
    {
        _ = await EvaluateAsync();

        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_AfterARunThatFailed_TheTemporaryFolderIsGone()
    {
        _runner.Default = new FakeProject { Manifest = "not a manifest" };

        (await EvaluateAsync()).State.ShouldBe(ProjectState.Failed);

        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_Cancelled_StartsNoFurtherProcessAndLeavesNoFolder()
    {
        _processors = 1;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _runner.BeforeAnswer = async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(EvaluateAsync(Projects(3), null, cancel.Token));

        _runner.Requests.Count.ShouldBe(1);
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("net8.0;net10.0", "net8.0")]
    [InlineData(" ; net8.0 ;net10.0", "net8.0")]
    [InlineData("net10.0", "net10.0")]
    [InlineData("", null)]
    [InlineData(" ; ", null)]
    public void FirstFramework_List_IsItsFirstEntryThatIsNotEmpty(string targetFrameworks, string? expected) =>
        ProjectEvaluator.FirstFramework(targetFrameworks).ShouldBe(expected);
}
```

The comment `/*lang=json,strict*/` in one `InlineData` is what `./format.sh` writes there; leave it.

- [ ] **Step 8: Run them to see them fail**

Run: `dotnet build tests/SqlSource.Tool.Tests`
Expected: the build fails: `ProjectEvaluator`, `MSBuildProperty`, `ProjectState` and the new members of `ToolHost` do not exist.

- [ ] **Step 9: Widen the host, and write the evaluator**

Change `src/SqlSource.Tool/ToolHost.cs`:

```diff
--- a/src/SqlSource.Tool/ToolHost.cs
+++ b/src/SqlSource.Tool/ToolHost.cs
@@ -1,5 +1,6 @@
 using System;
 using System.IO;
+using SqlSource.Tool.Processes;
 
 namespace SqlSource.Tool;
 
@@ -11,16 +12,31 @@ namespace SqlSource.Tool;
 /// <param name="Error">Where an error goes.</param>
 /// <param name="WorkingDirectory">The full path that a relative path is resolved against.</param>
 /// <param name="GetEnvironmentVariable">The value of an environment variable, or null when it is not set.</param>
+/// <param name="Processes">Runs every program the tool starts.</param>
+/// <param name="TempDirectory">The full path of the directory the tool makes its temporary folders in.</param>
+/// <param name="ProcessorCount">How many processors the machine has, which bounds what the tool runs at once.</param>
 internal sealed record ToolHost(
     TextWriter Out,
     TextWriter Error,
     string WorkingDirectory,
-    Func<string, string?> GetEnvironmentVariable
+    Func<string, string?> GetEnvironmentVariable,
+    IProcessRunner Processes,
+    string TempDirectory,
+    int ProcessorCount
 )
 {
     /// <summary>
-    /// The host of a real run: the console, the current directory and the environment of the process.
+    /// The host of a real run: the console, the current directory, the environment and the processes of the
+    /// machine.
     /// </summary>
     public static ToolHost Create() =>
-        new(Console.Out, Console.Error, Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable);
+        new(
+            Console.Out,
+            Console.Error,
+            Directory.GetCurrentDirectory(),
+            Environment.GetEnvironmentVariable,
+            new ProcessRunner(),
+            Path.GetTempPath(),
+            Environment.ProcessorCount
+        );
 }
```

Change `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`:

```diff
--- a/src/SqlSource.Tool/Reporting/ToolDiagnostic.cs
+++ b/src/SqlSource.Tool/Reporting/ToolDiagnostic.cs
@@ -32,6 +32,18 @@ internal sealed record ToolDiagnostic(
             EquatableArray<ContinuationLine>.Empty
         );
 
+    /// <summary>
+    /// An error about a file, at no position in it.
+    /// </summary>
+    /// <param name="descriptor">What is wrong.</param>
+    /// <param name="path">The full path of the file.</param>
+    /// <param name="arguments">The text the descriptor's message quotes.</param>
+    public static ToolDiagnostic ForFile(DiagnosticDescriptor descriptor, string path, params string[] arguments) =>
+        Create(descriptor, arguments) with
+        {
+            Path = path,
+        };
+
     /// <summary>
     /// The same error with these lines under its first, in this order.
     /// </summary>
```

Create `src/SqlSource.Tool/Projects/MSBuildProperty.cs`:

```csharp
using System;

namespace SqlSource.Tool.Projects;

/// <summary>
/// A property given to MSBuild on its command line.
/// </summary>
internal static class MSBuildProperty
{
    /// <summary>
    /// The switch that sets a property: <c>-p:Name=value</c>, with the value escaped.
    /// </summary>
    /// <remarks>
    /// MSBuild splits what follows <c>-p:</c> at each <c>;</c> and <c>,</c> into several properties, and reads
    /// <c>%3B</c> as a semicolon.  A path holds any of the three: <c>-p:SolutionDir=/work/Acme, Inc/</c> is
    /// <c>MSB1006</c> as it stands.
    /// </remarks>
    public static string Switch(string name, string value) => "-p:" + name + "=" + Escape(value);

    /// <summary>
    /// The value as MSBuild must be given it to read it back whole.  The percent sign goes first, or the two
    /// after it would be escaped twice.
    /// </summary>
    public static string Escape(string value) =>
        value.AsSpan().IndexOfAny('%', ';', ',') < 0
            ? value
            : value
                .Replace("%", "%25", StringComparison.Ordinal)
                .Replace(";", "%3B", StringComparison.Ordinal)
                .Replace(",", "%2C", StringComparison.Ordinal);
}
```

Create `src/SqlSource.Tool/Projects/ProjectState.cs`:

```csharp
namespace SqlSource.Tool.Projects;

/// <summary>
/// What the tool found out about a project.
/// </summary>
internal enum ProjectState
{
    /// <summary>The package's props reached the project, and its manifest was read.</summary>
    UsesSqlSource,

    /// <summary>The project was restored and has nothing of the package.</summary>
    DoesNotUseSqlSource,

    /// <summary>
    /// The project was never restored, or not since SqlSource was added to it, so nothing says whether it uses it.
    /// </summary>
    NotRestored,

    /// <summary>MSBuild failed, or wrote a manifest that cannot be read.</summary>
    Failed,
}
```

Create `src/SqlSource.Tool/Projects/ProjectEvaluation.cs`:

```csharp
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// One project of a run, after MSBuild was asked about it.
/// </summary>
/// <param name="ProjectPath">The full path of the project file.</param>
/// <param name="State">What was found.</param>
/// <param name="Manifest">The manifest, when the project uses SqlSource.</param>
/// <param name="Failure">The error to report, when the state is <see cref="ProjectState.Failed" />.</param>
internal sealed record ProjectEvaluation(
    string ProjectPath,
    ProjectState State,
    ProjectManifest? Manifest = null,
    ToolDiagnostic? Failure = null
);
```

Create `src/SqlSource.Tool/Projects/ProjectEvaluator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Asks MSBuild about the projects of a run: whether each uses SqlSource, and for its project manifest.
/// </summary>
/// <remarks>
/// <para>
/// Two runs of <c>dotnet msbuild</c> for a project, and three for one with several target frameworks.  The first
/// evaluates the project and runs no target; the last runs the target <c>SqlSourceWriteManifest</c> of the package,
/// which writes a file the tool named.  So the tool reads nothing that MSBuild prints but the JSON of
/// <c>-getProperty</c>, writes nothing into a project, and never restores or builds one.
/// </para>
/// <para>
/// Nothing is reported here.  An error travels in the <see cref="ProjectEvaluation" />, so that the caller reports
/// the errors of several projects in the order of the projects, and not of the processes.
/// </para>
/// </remarks>
internal static class ProjectEvaluator
{
    /// <summary>How many projects are asked about at once, at most.</summary>
    public const int MaxAtOnce = 8;

    /// <summary>How many lines of MSBuild's output an error shows.</summary>
    public const int MaxOutputLines = 20;

    /// <summary>The name of the target of the package that writes the manifest.</summary>
    public const string ManifestTarget = "SqlSourceWriteManifest";

    private const string PackageId = "SqlSource";

    private static readonly ImmutableDictionary<string, string> SetVariables = ImmutableDictionary
        .Create<string, string>(StringComparer.Ordinal)
        // A path outside ASCII arrives whole on Windows, where the console's code page is not UTF-8.  TD-0028.
        .Add("DOTNET_CLI_FORCE_UTF8_ENCODING", "true")
        // The first "dotnet" command on a machine prints a welcome, which would stand before the JSON.
        .Add("DOTNET_NOLOGO", "true");

    // A tool that a build started has these, and "dotnet" loads the SDK they name in place of the one that the
    // project's global.json picks.
    private static readonly ImmutableArray<string> RemovedVariables = ["MSBuildSDKsPath", "MSBuildExtensionsPath"];

    /// <summary>
    /// Asks about each project, several at once.
    /// </summary>
    /// <param name="host">The processes, the environment and the temporary directory.</param>
    /// <param name="projects">The full paths of the project files.</param>
    /// <param name="solution">The full path of the solution the projects are of, or null for a project alone.</param>
    /// <param name="cancellationToken">Ends the run: no further process is started.</param>
    /// <returns>One evaluation for each project, in the order of <paramref name="projects" />.</returns>
    public static async Task<ImmutableArray<ProjectEvaluation>> EvaluateAsync(
        ToolHost host,
        IReadOnlyList<string> projects,
        string? solution,
        CancellationToken cancellationToken
    )
    {
        if (projects.Count == 0)
        {
            return [];
        }

        // A folder of this run's own, for the manifests: nothing is written into a project.
        var folder = Directory
            .CreateDirectory(Path.Combine(host.TempDirectory, "sqlsource-" + Guid.NewGuid().ToString("N")))
            .FullName;
        try
        {
            var solutionSwitches = SolutionSwitches(solution);
            var evaluations = new ProjectEvaluation[projects.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(host.ProcessorCount, 1, MaxAtOnce),
                CancellationToken = cancellationToken,
            };
            await Parallel.ForEachAsync(
                Enumerable.Range(0, projects.Count),
                options,
                async (index, token) =>
                {
                    var manifestFile = Path.Combine(folder, index.ToString(CultureInfo.InvariantCulture) + ".manifest");
                    evaluations[index] = await EvaluateOneAsync(
                        host,
                        projects[index],
                        solutionSwitches,
                        manifestFile,
                        token
                    );
                }
            );
            return [.. evaluations];
        }
        finally
        {
            Delete(folder);
        }
    }

    /// <summary>
    /// The first of a project's <c>TargetFrameworks</c>, or null when it has none.
    /// </summary>
    internal static string? FirstFramework(string targetFrameworks) =>
        targetFrameworks
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    private static async Task<ProjectEvaluation> EvaluateOneAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> solutionSwitches,
        string manifestFile,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<string> evaluate =
        [
            "msbuild",
            project,
            "-nologo",
            "-getProperty:SqlSourceImported",
            "-getProperty:TargetFramework",
            "-getProperty:TargetFrameworks",
            "-getProperty:ProjectAssetsFile",
            "-getItem:PackageReference",
            .. solutionSwitches,
        ];

        var result = await RunAsync(host, project, evaluate, cancellationToken);
        if (!TryReadAnswer(result, out var answer, out var reason))
        {
            return Failed(project, reason, result);
        }

        // NuGet imports a package's props into a project with several frameworks only under a condition on
        // TargetFramework, so this answer says nothing but which framework is the first.  TD-0026.
        ImmutableArray<string> frameworkSwitch = [];
        if (answer.TargetFramework.Length == 0 && FirstFramework(answer.TargetFrameworks) is { } first)
        {
            frameworkSwitch = [MSBuildProperty.Switch("TargetFramework", first)];
            result = await RunAsync(host, project, [.. evaluate, .. frameworkSwitch], cancellationToken);
            if (!TryReadAnswer(result, out answer, out reason))
            {
                return Failed(project, reason, result);
            }
        }

        var state = StateOf(answer, project);
        if (state != ProjectState.UsesSqlSource)
        {
            return new ProjectEvaluation(project, state);
        }

        result = await RunAsync(
            host,
            project,
            [
                "msbuild",
                project,
                "-nologo",
                "-t:" + ManifestTarget,
                MSBuildProperty.Switch("SqlSourceManifestFile", manifestFile),
                .. frameworkSwitch,
                .. solutionSwitches,
            ],
            cancellationToken
        );
        if (result.ExitCode != 0)
        {
            return Failed(project, ExitCodeReason(result), result);
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(manifestFile, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(project, $"the target {ManifestTarget} left no file that can be read", result);
        }

        return ManifestReader.Read(text, out reason) is { } manifest
            ? new ProjectEvaluation(project, ProjectState.UsesSqlSource, manifest)
            : new ProjectEvaluation(
                project,
                ProjectState.Failed,
                Failure: ToolDiagnostic.ForFile(ToolDiagnostics.ManifestCannotBeRead, project, project, reason)
            );
    }

    private static Task<ProcessResult> RunAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> arguments,
        CancellationToken cancellationToken
    )
    {
        // The dotnet that started the tool, when it said which; else the one on the path.
        var dotnet = host.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

        // The project's folder, so that the project's own global.json picks the SDK.
        var workingDirectory = Path.GetDirectoryName(project) ?? host.WorkingDirectory;
        return host.Processes.RunAsync(
            new ProcessRequest(dotnet, arguments, workingDirectory, SetVariables, RemovedVariables),
            cancellationToken
        );
    }

    // What "dotnet build" of the solution gives each project.  A project that imports a file through
    // $(SolutionDir) evaluates with these as it does in a build.
    private static ImmutableArray<string> SolutionSwitches(string? solution)
    {
        if (solution is null)
        {
            return [];
        }

        var directory = Path.GetDirectoryName(solution) ?? "";
        if (!Path.EndsInDirectorySeparator(directory))
        {
            directory += Path.DirectorySeparatorChar;
        }

        return
        [
            MSBuildProperty.Switch("SolutionDir", directory),
            MSBuildProperty.Switch("SolutionPath", solution),
            MSBuildProperty.Switch("SolutionName", Path.GetFileNameWithoutExtension(solution)),
            MSBuildProperty.Switch("SolutionFileName", Path.GetFileName(solution)),
            MSBuildProperty.Switch("SolutionExt", Path.GetExtension(solution)),
        ];
    }

    // The first row that holds, of the table in the spec of sub-phase 2.3.
    private static ProjectState StateOf(Answer answer, string project)
    {
        if (string.Equals(answer.Imported, "true", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectState.UsesSqlSource;
        }

        // The reference is there and the package's props are not: no restore since it was added.
        if (answer.PackageReferences.Contains(PackageId, StringComparer.OrdinalIgnoreCase))
        {
            return ProjectState.NotRestored;
        }

        // A project with packages.config: NuGet writes it no assets file, so nothing says whether it was restored.
        // Such a project imports a package's props by a line of its own, so one that uses SqlSource has the marker.
        if (answer.ProjectAssetsFile.Length == 0)
        {
            return ProjectState.DoesNotUseSqlSource;
        }

        var assetsFile = Path.GetFullPath(answer.ProjectAssetsFile, Path.GetDirectoryName(project) ?? "");
        return File.Exists(assetsFile) ? ProjectState.DoesNotUseSqlSource : ProjectState.NotRestored;
    }

    private static bool TryReadAnswer(ProcessResult result, out Answer answer, out string reason)
    {
        answer = default;
        if (result.ExitCode != 0)
        {
            reason = ExitCodeReason(result);
            return false;
        }

        reason = "dotnet msbuild did not print the JSON of an evaluation";
        try
        {
            using var document = JsonDocument.Parse(result.Output);
            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("Properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object
                || !TryGetString(properties, "SqlSourceImported", out var imported)
                || !TryGetString(properties, "TargetFramework", out var targetFramework)
                || !TryGetString(properties, "TargetFrameworks", out var targetFrameworks)
                || !TryGetString(properties, "ProjectAssetsFile", out var assetsFile)
                || !document.RootElement.TryGetProperty("Items", out var items)
                || items.ValueKind != JsonValueKind.Object
                || !items.TryGetProperty("PackageReference", out var references)
                || references.ValueKind != JsonValueKind.Array
            )
            {
                return false;
            }

            var names = ImmutableArray.CreateBuilder<string>();
            foreach (var reference in references.EnumerateArray())
            {
                if (reference.ValueKind != JsonValueKind.Object || !TryGetString(reference, "Identity", out var name))
                {
                    return false;
                }

                names.Add(name.Trim());
            }

            answer = new Answer(
                imported.Trim(),
                targetFramework.Trim(),
                targetFrameworks,
                assetsFile.Trim(),
                names.ToImmutable()
            );
            reason = "";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? "";
            return true;
        }

        value = "";
        return false;
    }

    private static string ExitCodeReason(ProcessResult result) =>
        result.ExitCode == ProcessResult.NotStarted
            ? "dotnet could not be started"
            : string.Create(CultureInfo.InvariantCulture, $"dotnet msbuild ended with the exit code {result.ExitCode}");

    private static ProjectEvaluation Failed(string project, string reason, ProcessResult result)
    {
        // MSBuild writes the errors of an evaluation to its error output, and those of a target to the other.
        var lines = (result.Error + "\n" + result.Output)
            .Split('\n')
            .Select(static line => line.TrimEnd())
            .Where(static line => line.Length > 0)
            .Take(MaxOutputLines)
            .Select(static line => new ContinuationLine("msbuild", line));
        return new ProjectEvaluation(
            project,
            ProjectState.Failed,
            Failure: ToolDiagnostic
                .ForFile(ToolDiagnostics.ProjectCannotBeEvaluated, project, project)
                .WithLines([new ContinuationLine("reason", reason), .. lines])
        );
    }

    private static void Delete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder left in the temporary directory is no failure of the run.
        }
    }

    private readonly record struct Answer(
        string Imported,
        string TargetFramework,
        string TargetFrameworks,
        string ProjectAssetsFile,
        ImmutableArray<string> PackageReferences
    );
}
```

- [ ] **Step 10: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.MSBuildPropertyTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ProjectEvaluatorTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: every test of the project passes: the tests of `describe` still find their unit and report nothing, since nothing calls the evaluator yet.

- [ ] **Step 11: Add the fixtures of the evaluation, and ask the real MSBuild**

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/SecondOnly/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        Two target frameworks, and the package for the second alone, as a PackageReference under a condition on the
        framework gives.  The tool reads the first framework, so this project does not use SqlSource as the tool
        sees it.  TD-0026.
    -->
    <ImportGroup Condition="'$(TargetFramework)' == 'net10.0'">
        <Import Project="../build/SqlSource.props" />
    </ImportGroup>
    <PropertyGroup>
        <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    </PropertyGroup>
    <ImportGroup Condition="'$(TargetFramework)' == 'net10.0'">
        <Import Project="../build/SqlSource.targets" />
    </ImportGroup>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/SecondOnly/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Plain/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- A project that has nothing of SqlSource. -->
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/Plain/Program.cs`:

```csharp
namespace Fixture;

internal static class Program;
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/StaleRestore/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        A reference to the package and nothing of its props: the project as it is between "dotnet add package" and
        the next restore.  A test puts an assets file under obj, as an earlier restore left one.
    -->
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="sqlsource" Version="1.0.0" />
    </ItemGroup>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/InSolution/App.slnx`:

```xml
<Solution>
  <Project Path="App/App.csproj" />
</Solution>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/InSolution/Shared.props`:

```xml
<Project>
    <!-- What a solution shares between its projects from its own folder. -->
    <PropertyGroup>
        <SqlSourceDialect>mssql</SqlSourceDialect>
    </PropertyGroup>
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/InSolution/App/App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!--
        Imports a file through SolutionDir, which a build of the solution gives each project and a build of the
        project alone does not: evaluated without it, the import finds no file.
    -->
    <Import Project="$(SolutionDir)Shared.props" />
    <Import Project="../../build/SqlSource.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <Import Project="../../build/SqlSource.targets" />
</Project>
```

Create `tests/SqlSource.Tool.Tests/Fixtures/Projects/InSolution/App/Queries/Users.sql`:

```sql
-- name: GetUser
SELECT 1;
```

Change `tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs
+++ b/tests/SqlSource.Tool.Tests/Fixtures/FixtureProjects.cs
@@ -23,10 +23,11 @@ internal sealed class FixtureProjects : IDisposable
     // The folder that holds the copies.
     public string Root => _folder.Path;
 
-    // Copies a fixture and gives the full path of a file of the copy, its project file when none is named.
-    public string Copy(string fixture, string file = "App.csproj")
+    // Copies a fixture and gives the full path of a file of the copy, its project file when none is named.  The
+    // copy is in a folder of the fixture's name, or of the name given.
+    public string Copy(string fixture, string file = "App.csproj", string? folder = null)
     {
-        var target = _folder.PathOf(fixture);
+        var target = _folder.PathOf(folder ?? fixture);
         if (!Directory.Exists(target))
         {
             CopyFolder(Path.Combine(Source, "Projects", fixture), target);
```

Create `tests/SqlSource.Tool.Tests/FixtureProjectTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tool's runs of MSBuild on the projects of Fixtures/Projects, with the real "dotnet msbuild": what
// ProjectEvaluatorTests shows with a runner of its own, shown true of MSBuild.  Each test starts MSBuild once to
// three times.
public sealed class FixtureProjectTests : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The temporary directory has a name that MSBuild would split, as a user's may.
    private async Task<ProjectEvaluation> EvaluateAsync(string project, string? solution = null)
    {
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp ;,%41")).FullName;
        var evaluations = await ProjectEvaluator.EvaluateAsync(
            Hosts.Real(_fixtures.Root, temp),
            [project],
            solution,
            TestContext.Current.CancellationToken
        );
        Directory.EnumerateFileSystemEntries(temp).ShouldBeEmpty();
        return evaluations.ShouldHaveSingleItem();
    }

    private static string Failure(ProjectEvaluation evaluation) =>
        evaluation.Failure is { } failure ? string.Join('\n', failure.Lines.Select(line => line.Text)) : "";

    // An assets file, as a restore leaves one.  The tool looks for the file and does not read it.
    private static void Restore(string project) =>
        File.WriteAllText(
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(project)!, "obj")).FullName
                + Path.DirectorySeparatorChar
                + "project.assets.json",
            "{}"
        );

    [Fact]
    public async Task Evaluate_ProjectWithOneFramework_GivesItsManifest()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.ProjectPath.ShouldBe(project);
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.LangVersion.ShouldBe("14.0");
        manifest.DefineConstants.ShouldContain("NET8_0_OR_GREATER");
        manifest.Properties.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDialect", "postgres"));
        var file = manifest.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(Path.Combine(folder, "Queries", "Users.sql"));
        file.Metadata.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDatabase", "billing"));
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "UserRepository.cs")]);
        // The project was never restored or built, and the tool made it neither.
        Directory.Exists(Path.Combine(folder, "obj")).ShouldBeFalse();
    }

    // Evaluated with no framework, this project has nothing of the package, as one that NuGet restored.
    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_GivesTheManifestOfTheFirst()
    {
        var project = _fixtures.Copy("Multi");
        var folder = Path.GetDirectoryName(project)!;

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.TargetFramework.ShouldBe("net8.0");
        manifest.DefineConstants.ShouldContain("NET8_0");
        manifest.DefineConstants.ShouldNotContain("NET10_0");
        // TD-0026: Later/ is the second framework's alone.
        manifest.Files.Select(file => file.Path).ShouldBe([Path.Combine(folder, "Queries", "Users.sql")]);
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "Queries.cs")]);
        Directory.Exists(Path.Combine(folder, "obj")).ShouldBeFalse();
    }

    // TD-0026: the project uses SqlSource for its second framework, and the tool reads the first.
    [Fact]
    public async Task Evaluate_ProjectWithSqlSourceForItsSecondFrameworkAlone_DoesNotUseSqlSource()
    {
        var project = _fixtures.Copy("SecondOnly");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_RestoredProjectWithoutThePackage_DoesNotUseSqlSource()
    {
        var project = _fixtures.Copy("Plain");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_ProjectWithoutThePackageThatWasNeverRestored_IsNotRestored()
    {
        var project = _fixtures.Copy("Plain");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.NotRestored, Failure(evaluation));
        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "obj")).ShouldBeFalse();
    }

    // The reference is an item of the evaluation, so it shows without a restore, whatever its case.
    [Fact]
    public async Task Evaluate_ProjectThatGainedThePackageAfterItsLastRestore_IsNotRestored()
    {
        var project = _fixtures.Copy("StaleRestore");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.NotRestored, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolution_EvaluatesAsInABuildOfTheSolution()
    {
        // The folder of the solution has a name that MSBuild would split.
        var solution = _fixtures.Copy("InSolution", "App.slnx", folder: "Acme, Inc;100%");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "App", "App.csproj");

        var evaluation = await EvaluateAsync(project, solution);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        // Shared.props, which the project imports through SolutionDir, sets it.
        evaluation.Manifest.ShouldNotBeNull().Properties["SqlSourceDialect"].ShouldBe("mssql");
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolutionAskedAboutAlone_IsSqlsrc205WithMSBuildsError()
    {
        var project = _fixtures.Copy("InSolution", "App/App.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.Failed);
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure.Lines[0].Text.ShouldBe("dotnet msbuild ended with the exit code 1");
        Failure(evaluation).ShouldContain("Shared.props");
    }

    [Fact]
    public async Task Evaluate_PathsWithCharactersThatMSBuildReads_AreInTheManifestWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Combine(folder, "a;b=c%41 'é.sql"));
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "a;b=c%41 'é.cs")]);
    }

    // MSBuild writes a warning of an evaluation to its error output, and the JSON alone to the other.
    [Fact]
    public async Task Evaluate_ProjectWhoseEvaluationWarns_IsReadAllTheSame()
    {
        // The second import of one file is the warning MSB4011.
        var project = _fixtures.WriteFile(
            "Warns/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <Import Project="../build/SqlSource.props" />
                <Import Project="../build/SqlSource.props" />
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                <Import Project="../build/SqlSource.targets" />
            </Project>
            """
        );

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
    }

    // MSBuild reads Directory.Build.rsp beside a project by itself.  What it holds applies to the tool's runs as to
    // a build, and a verbosity it asks for puts nothing before the JSON.
    [Fact]
    public async Task Evaluate_ProjectWithAResponseFile_IsReadWithWhatTheFileSets()
    {
        var project = _fixtures.Copy("Single");
        _ = _fixtures.WriteFile("Single/Directory.Build.rsp", "-v:diag\n-p:SqlSourceOutput=sql\n");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        evaluation.Manifest.ShouldNotBeNull().Properties["SqlSourceOutput"].ShouldBe("sql");
    }

    // A solution may list a project that is not on the disk, as after a branch was switched.
    [Fact]
    public async Task Evaluate_ProjectFileThatDoesNotExist_IsSqlsrc205WithMSBuildsError()
    {
        var project = Path.Combine(Path.GetDirectoryName(_fixtures.Copy("Single"))!, "Gone.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC205");
        Failure(evaluation).ShouldContain("Gone.csproj");
    }

    // The repository's own test project of the generator, where it is: it takes the package's files by path, and
    // its end-to-end files have a dialect each way a file can be given one.
    [Fact]
    public async Task Evaluate_TestProjectOfTheGenerator_FindsItsFilesWithTheirDialects()
    {
        var folder = Path.Combine(RepositoryRoot(), "tests", "SqlSource.Tests");
        var project = Path.Combine(folder, "SqlSource.Tests.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.ProjectPath.ShouldBe(project);
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.DefineConstants.ShouldContain("NET10_0");
        manifest.Properties["SqlSourceDialect"].ShouldBe("postgres");
        manifest.Properties["SqlSourceGeneratorParameters"].ShouldBe("sort-input no-token-validation");

        string EndToEnd(params string[] parts) => Path.Combine([folder, "EndToEnd", .. parts]);
        var files = manifest.Files.ToDictionary(file => file.Path, file => file.Metadata);
        files[EndToEnd("Users.sql")].ShouldBeEmpty();
        files[EndToEnd("Dialects", "ByMetadata.sql")]["SqlSourceDialect"].ShouldBe("mysql");
        files[EndToEnd("Dialects", "ByOption.sql")]["SqlSourceDialect"].ShouldBe("mysql, no-backslash-escapes");
        files[EndToEnd("Parameters", "Kept.sql")]
            ["SqlSourceGeneratorParameters"]
            .ShouldBe("no-token-validation keep-comments");
        manifest.CompileFiles.ShouldContain(EndToEnd("UserQueries.cs"));
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "SqlSource.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("The tests do not run from a folder of the repository.");
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.FixtureProjectTests`
Expected: thirteen tests pass, in about five seconds.  These show the evaluator true of MSBuild, so they pass as they are written.  One that fails says that an argument of `ProjectEvaluator` is wrong for the real MSBuild: read the `msbuild` lines the assertion prints, and mend the evaluator, not the test.

`Evaluate_TestProjectOfTheGenerator_FindsItsFilesWithTheirDialects` asks about `tests/SqlSource.Tests` where it is, which the build of the solution restored.

- [ ] **Step 12: Record Windows, and give `TD-0026` its links**

Create `docs/tech-debt/TD-0028-msbuild-runs-of-the-tool-are-not-verified-on-windows.md`:

```markdown
# TD-0028 - The tool's runs of MSBuild are not verified on Windows

## Problem

The `sqlsource` tool starts `dotnet msbuild` for each project, from [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) through [`ProcessRunner`](../../src/SqlSource.Tool/Processes/ProcessRunner.cs), and reads a file that a target of the package wrote.  All of it was written and tested on macOS, and CI runs on Linux.  Three things can differ on Windows and were not run there:

1. **A path outside ASCII.**  Each run sets `DOTNET_CLI_FORCE_UTF8_ENCODING`, and the tool reads MSBuild's output as UTF-8.  Whether the JSON of an evaluation then holds `ProjectAssetsFile` whole under a console code page that is not UTF-8 is not known.  A wrong path there makes a restored project look like one that was not: `SQLSRC220`.
2. **A path that MSBuild reads.**  The fixture `OddPaths` and the tests of [`MSBuildProperty`](../../src/SqlSource.Tool/Projects/MSBuildProperty.cs) show that a name with `;`, `=`, `%`, `'`, a space and a letter outside ASCII arrives whole in the manifest and in a `-p:` switch.  They ran on macOS and Linux.
3. **`SolutionDir`, which ends with `\`.**  The runner gives each argument through `ProcessStartInfo.ArgumentList`, which quotes as Windows needs, and what `dotnet` then hands MSBuild was not looked at.

## Why it exists

The repository has no Windows machine: every workflow in `.github/workflows` runs on `ubuntu-latest`.

## Impact

Low until a Windows user runs the tool.  Then each of the three is a run that fails or, for the first, that reports a project wrongly.

## Proposed fix

Run `tests/SqlSource.Tool.Tests` on `windows-latest` in `build.yml`.  `ProcessRunnerTests`, `ManifestTargetTests` and `FixtureProjectTests` cover the three, and already choose their programs by operating system.  Mend what fails; a character that the manifest cannot carry on Windows becomes an item of its own.

## Trigger

The first report from Windows, or phase 5, the first release that a user can use.
```

Change `docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md`:

```diff
--- a/docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md
+++ b/docs/tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md
@@ -2,7 +2,7 @@
 
 ## Problem
 
-For a project with `TargetFrameworks`, the `sqlsource` tool passes the first of them as `TargetFramework`, to the evaluation that asks whether the project uses SqlSource and to the target that writes the project manifest.  So there is one manifest for the project, and it is that framework's.
+For a project with `TargetFrameworks`, the `sqlsource` tool passes the first of them as `TargetFramework`, to the evaluation that asks whether the project uses SqlSource and to the target that writes the project manifest: [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs).  So there is one manifest for the project, and it is that framework's.
 
 - A `.sql` file or a `Compile` file that the project lists only under a condition on another framework is not in it.
 - An attribute under `#if` for a constant that only another framework defines is not read.
@@ -10,7 +10,7 @@ For a project with `TargetFrameworks`, the `sqlsource` tool passes the first of
 
 The queries those bring are never described.  The generator compiles for every framework, so from phase 5 the build of the other framework reports each as having no entry.
 
-`tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` pins the first two with the fixture `Multi`.
+`tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` and `tests/SqlSource.Tool.Tests/FixtureProjectTests.cs` pin the first two with the fixture `Multi`, and the third with `SecondOnly`.
 
 ## Why it exists
 
```

Change `docs/tech-debt/README.md`:

```diff
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -2,7 +2,7 @@
 
 Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.
 
-Next id: `TD-0028`
+Next id: `TD-0029`
 
 ## Active items
 
@@ -23,6 +23,7 @@ Next id: `TD-0028`
 | [TD-0025](TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md) | Open | 2026-10-09 | Low | The `sqlsource` tool does not see a `.sql` file, or a C# file, that a target adds from a hook of the build: only a target that hooks `SqlSourceTrimMetadataOfFiles` runs before the project manifest is written |
 | [TD-0026](TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md) | Open | 2026-10-09 | Low | For a project with several target frameworks the `sqlsource` tool reads the first one alone: a file, an attribute or a reference to SqlSource that only another framework has is not seen |
 | [TD-0027](TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md) | Open | 2026-10-09 | Low | The target that writes the project manifest depends on `AddImplicitDefineConstants`, a target of the SDK whose name is no contract |
+| [TD-0028](TD-0028-msbuild-runs-of-the-tool-are-not-verified-on-windows.md) | Open | 2026-10-10 | Low | The `sqlsource` tool's runs of `dotnet msbuild` were never run on Windows: a path outside ASCII in MSBuild's output, a path with characters that MSBuild reads, and a `SolutionDir` that ends with a backslash |
 
 ## Columns
 
```

- [ ] **Step 13: Say how the tool runs MSBuild**

Change `src/SqlSource.Tool/AGENTS.md`:

```diff
--- a/src/SqlSource.Tool/AGENTS.md
+++ b/src/SqlSource.Tool/AGENTS.md
@@ -5,6 +5,11 @@ The `sqlsource` command, packed as the .NET tool `SqlSource.Tool`.  `tests/SqlSo
 - **The tool shares the generator's code and never copies it.**  It references `src/SqlSource/SqlSource.csproj` and sees its internals.  The parser, the dialect rules, the settings, the sidecar's reader and writer, the hash and the descriptors are used from there.  Code that both need goes in the generator's project, where `netstandard2.0` and Roslyn 4.8.0 bound it.
 - **It targets `net8.0` with `RollForward` `Major`, and references a current Roslyn by `VersionOverride`.**  Do not raise the target: a user with only the .NET 8 SDK must be able to run it.  `tools/check-tool-install.sh` runs the packed tool on the newest runtime the machine has.
 - **Everything from outside comes through `ToolHost`.**  Nothing reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`, which is the entry of a real run and listens for Ctrl+C.  A test gives `Cli.RunAsync` a host of its own.  What a later sub-phase takes from outside, a process, a clock, a database, is a new member of `ToolHost`.
+- **Every process goes through `ToolHost.Processes`**, an `IProcessRunner` of `Processes/`.  `Processes/ProcessRunner.cs` is the only code that starts one, and a test gives `tests/SqlSource.Tool.Tests/FakeProcessRunner.cs`, which answers in MSBuild's place.  A temporary folder is made in `ToolHost.TempDirectory`, and what runs at once is bounded by `ToolHost.ProcessorCount`.
+- **The tool never restores and never builds a project, and writes nothing into one.**  `Projects/ProjectEvaluator.cs` runs `dotnet msbuild` to evaluate a project and to run the package's target `SqlSourceWriteManifest`, which writes to a file the tool named in a temporary folder of the run.  Do not add `-restore`, a target of a build, or a file under a project's folder: `tests/SqlSource.Tool.Tests/FixtureProjectTests.cs` checks that a project has no `obj` afterwards.
+- **MSBuild's output is parsed only as the JSON of `-getProperty` and `-getItem`.**  Everything else it prints is shown, the first twenty lines, under `SQLSRC205`, and never read: MSBuild writes in the language of the machine.  What the tool needs from a target comes in the manifest file, read by `Projects/ManifestReader.cs`; the format is the package's contract, and `src/SqlSource/AGENTS.md` has its rules.
+- **A value given to MSBuild as a property goes through `Projects/MSBuildProperty.cs`.**  MSBuild splits what follows `-p:` at `;` and `,`, and a path holds either.  Each run also sets and removes the environment variables that `ProjectEvaluator` lists, with the reason beside each.
+- **An evaluation reports nothing.**  `ProjectEvaluator` runs several projects at once and gives each error as data, in a `ProjectEvaluation`, so that the caller reports them in the order of the projects.
 - **Every error goes through `Reporting/Reporter.cs` with a descriptor of `ToolDiagnostics`**, or of `SqlDiagnostics` where the generator reports the same condition.  The descriptors are in the generator's project; its `AGENTS.md` lists the four places a new one touches.  A wrong command line is the one exception, below.
 - **The exit code is `0`, `1` or `2`.**  `1` when the reporter wrote anything, when the command line was wrong, and when the run was cancelled.  `2` is for `--check` alone: a difference, and nothing failed.  `Cli.RunAsync` takes `1` from `Reporter.Count`, so a command reports and goes on, and never returns `1` itself.
 - **The format of an error is a contract with MSBuild and with editors.**  `<path>(<line>,<column>): error <id>: <message>`, or `<path> : error`, or `sqlsource : error`; then continuation lines, each four spaces, a label, a colon, a space and a text; then `    see:` with the descriptor's help link, last.  Only the first column tells a first line from a continuation line, so `Reporting/OneLine.cs` writes a line break as a space in every text the tool prints and did not write itself: an argument of a message, a path, a label, the name of an unknown option.  Several threads may report, so `Reporter` writes an error with one call, under a lock.  A path in a message is a full path.
```

Change `CONTRIBUTING.md`:

```diff
--- a/CONTRIBUTING.md
+++ b/CONTRIBUTING.md
@@ -44,7 +44,7 @@ A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both, so i
 
 `tests/SqlSource.Tests` also uses the generator the way a consumer does: the types in `EndToEnd/` are compiled with the generator loaded, and the project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`, the MSBuild files the package ships.  It gets them by path and the generator through a project reference; `tools/check-package-install.sh`, under Package below, is what installs the packed package.
 
-`tests/SqlSource.Tool.Tests/Fixtures/Projects/` holds small projects that are no part of the solution.  `ManifestTargetTests` copies one to a temporary folder outside the repository, with the package's two MSBuild files and `global.json` beside it, and runs `dotnet msbuild` on it for real, so these tests need the SDK that `global.json` pins and take a second or two each.  A fixture is never built or restored, and a test leaves nothing in the repository.  A file under that folder is copied to the test project's output as it is; add a project there as a folder with an `App.csproj` that imports `../build/SqlSource.props` and `../build/SqlSource.targets`.
+`tests/SqlSource.Tool.Tests/Fixtures/Projects/` holds small projects that are no part of the solution.  `ManifestTargetTests` and `FixtureProjectTests` copy one to a temporary folder outside the repository, with the package's two MSBuild files and `global.json` beside it, and run `dotnet msbuild` on it for real, the second through the tool's own code, so these tests need the SDK that `global.json` pins and take a second or two each.  `FixtureProjectTests` also asks MSBuild about `tests/SqlSource.Tests` where it is, which must have been restored, as it is once the solution was built.  A fixture is never built or restored, and a test leaves nothing in the repository.  A file under that folder is copied to the test project's output as it is; add a project there as a folder with an `App.csproj` that imports `../build/SqlSource.props` and `../build/SqlSource.targets`.
 
 To run the tool from its source:
 
```

- [ ] **Step 14: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Ask MSBuild about a project

ProjectEvaluator runs dotnet msbuild through the host's IProcessRunner: an
evaluation that says whether the project uses SqlSource, again with the
first framework for a project that has several, and then the package's
target, which writes the manifest to a file of the run's own.  A run that
fails is SQLSRC205 with MSBuild's lines.  The tool restores nothing and
writes nothing into a project.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 6: The projects of a run: a solution, `--project`, and four diagnostics

**Files:**
- Modify: `Directory.Packages.props`, `src/SqlSource.Tool/SqlSource.Tool.csproj`, `tools/check-package.sh`, and the two lock files by `dotnet restore`
- Create: `src/SqlSource.Tool/Projects/SolutionProjects.cs`, `SolutionReader.cs`, `RunProjects.cs`
- Modify: `src/SqlSource.Tool/Cli.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tool.Tests/SolutionReaderTests.cs`, `RunProjectsTests.cs`, `DescribeTests.cs`, `UsageCheckTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/CliRun.cs`
- Modify: `docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md`, `docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md`, `docs/tech-debt/README.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `CONTRIBUTING.md`

**Interfaces:**
- Consumes: `ProjectEvaluator.EvaluateAsync`, `ProjectEvaluation`, `ProjectState`, `ToolDiagnostic.ForFile`, `FakeProcessRunner`, `FakeProject`, `Hosts.Create` and `CliRun.Processes` of task 5; `RunUnit`, `RunUnitKind` and `RunUnitFinder.Find` of sub-phase 2.2.
- Produces: `sealed record SolutionProjects(ImmutableArray<string> Projects, string? Failure)` and `static Task<SolutionProjects> SolutionReader.ReadAsync(string solutionPath, CancellationToken cancellationToken)`.
- Produces: `static Task<ImmutableArray<ProjectManifest>> RunProjects.FindAsync(RunUnit unit, IReadOnlyList<string> named, ToolHost host, Reporter reporter, CancellationToken cancellationToken)`.  Sub-phase 2.4 plans a run from what it gives.
- Produces: `ToolDiagnostics.ProjectDoesNotUseSqlSource` (`SQLSRC204`), `ProjectNotInRun` (`SQLSRC207`), `ProjectNotRestored` (`SQLSRC220`) and `SolutionCannotBeRead` (`SQLSRC223`).
- Produces: `describe --project <path>`, and `CliRun.RunAsync(CancellationToken cancellationToken, params string[] args)`.

- [ ] **Step 1: Reference the library that reads a solution**

Change `Directory.Packages.props`:

```diff
--- a/Directory.Packages.props
+++ b/Directory.Packages.props
@@ -26,6 +26,8 @@
         -->
         <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
         <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.11.2" />
+        <!-- Reads .sln and .slnx files for the tool, as the dotnet command does with the same library. -->
+        <PackageVersion Include="Microsoft.VisualStudio.SolutionPersistence" Version="1.0.52" />
         <PackageVersion Include="NSubstitute" Version="6.2.0" />
         <PackageVersion Include="NSubstitute.Analyzers.CSharp" Version="1.0.17" />
         <PackageVersion Include="Roslynator.Analyzers" Version="5.0.0" />
```

Change `src/SqlSource.Tool/SqlSource.Tool.csproj`:

```diff
--- a/src/SqlSource.Tool/SqlSource.Tool.csproj
+++ b/src/SqlSource.Tool/SqlSource.Tool.csproj
@@ -38,6 +38,7 @@
             takes a current compiler and not the generator's pin, as tests/SqlSource.Tests does.
         -->
         <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
+        <PackageReference Include="Microsoft.VisualStudio.SolutionPersistence" />
         <PackageReference Include="System.CommandLine" />
     </ItemGroup>
     <ItemGroup>
```

Change `tools/check-package.sh`:

```diff
--- a/tools/check-package.sh
+++ b/tools/check-package.sh
@@ -1,8 +1,8 @@
 #!/usr/bin/env bash
 # Checks the contents of the two packages.  SqlSource: the generator, the two MSBuild files that hand .sql files and
 # the settings to the compiler, the readme, and nothing under lib/.  SqlSource.Tool: the tool with the generator's
-# assembly and Roslyn beside it, the settings that name its command, the readme, and no documentation files.  Both
-# have one version.
+# assembly, Roslyn and the library that reads a solution beside it, the settings that name its command, the readme,
+# and no documentation files.  Both have one version.
 # Usage: check-package.sh [directory]   The directory holds one SqlSource.<version>.nupkg and one
 # SqlSource.Tool.<version>.nupkg.  Without it, both are packed into a temporary directory first.
 set -euo pipefail
@@ -15,6 +15,7 @@ TOOL_REQUIRED=(
     'tools/net8.0/any/SqlSource.Tool.dll'
     'tools/net8.0/any/SqlSource.dll'
     'tools/net8.0/any/Microsoft.CodeAnalysis.CSharp.dll'
+    'tools/net8.0/any/Microsoft.VisualStudio.SolutionPersistence.dll'
     'tools/net8.0/any/DotnetToolSettings.xml'
     'README.md'
 )
```

```bash
dotnet restore SqlSource.slnx
```

Expected: `git status` shows `src/SqlSource.Tool/packages.lock.json` and `tests/SqlSource.Tool.Tests/packages.lock.json` changed, each by the one package.  The library is MIT and has an asset for `net8.0`.

- [ ] **Step 2: Write the failing test of the four diagnostics**

Change `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
+++ b/tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs
@@ -97,6 +97,26 @@ public class ToolDiagnosticsTests
             )
             .ShouldBe("The project manifest of '/work/App.csproj' cannot be read: it names no project");
 
+    [Fact]
+    public void ProjectNotInRun_Message_HoldsThePathAndTheUnit() =>
+        string.Format(
+                CultureInfo.InvariantCulture,
+                ToolDiagnostics.ProjectNotInRun.MessageFormat.ToString(CultureInfo.InvariantCulture),
+                "/work/Other/Other.csproj",
+                "/work/App.slnx"
+            )
+            .ShouldBe("'/work/Other/Other.csproj' is not a project of '/work/App.slnx'");
+
+    [Fact]
+    public void SolutionCannotBeRead_Message_HoldsTheSolutionAndTheReason() =>
+        string.Format(
+                CultureInfo.InvariantCulture,
+                ToolDiagnostics.SolutionCannotBeRead.MessageFormat.ToString(CultureInfo.InvariantCulture),
+                "/work/App.sln",
+                "Not a solution file."
+            )
+            .ShouldBe("'/work/App.sln' cannot be read: Not a solution file.");
+
     [Fact]
     public void DirectoryCannotBeRead_Message_HoldsTheDirectoryAndTheReason() =>
         string.Format(
```

- [ ] **Step 3: Add the four descriptors in their three other places**

Change `src/SqlSource/Diagnostics/ToolDiagnostics.cs`:

```diff
--- a/src/SqlSource/Diagnostics/ToolDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/ToolDiagnostics.cs
@@ -56,6 +56,17 @@ internal static class ToolDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor ProjectDoesNotUseSqlSource = new(
+        id: "SQLSRC204",
+        title: "Project does not use SqlSource",
+        messageFormat: "'{0}' does not use SqlSource",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc204",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     public static readonly DiagnosticDescriptor ProjectCannotBeEvaluated = new(
         id: "SQLSRC205",
         title: "Project could not be evaluated",
@@ -78,6 +89,28 @@ internal static class ToolDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor ProjectNotInRun = new(
+        id: "SQLSRC207",
+        title: "Project is not in the run",
+        messageFormat: "'{0}' is not a project of '{1}'",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc207",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
+    public static readonly DiagnosticDescriptor ProjectNotRestored = new(
+        id: "SQLSRC220",
+        title: "Project was not restored",
+        messageFormat: "'{0}' has not been restored, or not since the SqlSource package was added to it",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc220",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     public static readonly DiagnosticDescriptor DirectoryCannotBeRead = new(
         id: "SQLSRC222",
         title: "Directory cannot be read",
@@ -89,6 +122,17 @@ internal static class ToolDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor SolutionCannotBeRead = new(
+        id: "SQLSRC223",
+        title: "Solution cannot be read",
+        messageFormat: "'{0}' cannot be read: {1}",
+        category: SqlDiagnostics.Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc223",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     /// <summary>
     /// Every descriptor, in the order of its id.
     /// </summary>
@@ -98,8 +142,12 @@ internal static class ToolDiagnostics
             NoRunUnit,
             SeveralRunUnits,
             NotARunUnit,
+            ProjectDoesNotUseSqlSource,
             ProjectCannotBeEvaluated,
             ManifestCannotBeRead,
-            DirectoryCannotBeRead
+            ProjectNotInRun,
+            ProjectNotRestored,
+            DirectoryCannotBeRead,
+            SolutionCannotBeRead
         );
 }
```

Change `src/SqlSource/AnalyzerReleases.Unshipped.md`:

```diff
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -41,6 +41,10 @@ SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
 SQLSRC201 | SqlSource | Error | No project or solution found
 SQLSRC202 | SqlSource | Error | More than one project or solution found
 SQLSRC203 | SqlSource | Error | Path is not a project or a solution
+SQLSRC204 | SqlSource | Error | Project does not use SqlSource
 SQLSRC205 | SqlSource | Error | Project could not be evaluated
 SQLSRC206 | SqlSource | Error | Project manifest cannot be read
+SQLSRC207 | SqlSource | Error | Project is not in the run
+SQLSRC220 | SqlSource | Error | Project was not restored
 SQLSRC222 | SqlSource | Error | Directory cannot be read
+SQLSRC223 | SqlSource | Error | Solution cannot be read
```

Change `docs/diagnostics.md`:

````diff
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -40,9 +40,13 @@ Every problem SqlSource finds is an error, and none can be turned off or made a
 | [SQLSRC201](#sqlsrc201) | No project or solution found |
 | [SQLSRC202](#sqlsrc202) | More than one project or solution found |
 | [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
+| [SQLSRC204](#sqlsrc204) | Project does not use SqlSource |
 | [SQLSRC205](#sqlsrc205) | Project could not be evaluated |
 | [SQLSRC206](#sqlsrc206) | Project manifest cannot be read |
+| [SQLSRC207](#sqlsrc207) | Project is not in the run |
+| [SQLSRC220](#sqlsrc220) | Project was not restored |
 | [SQLSRC222](#sqlsrc222) | Directory cannot be read |
+| [SQLSRC223](#sqlsrc223) | Solution cannot be read |
 
 Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.  Ids from 200 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1.
 
@@ -577,6 +581,20 @@ The path given to `sqlsource describe` does not exist, or is a file that is not
 
 Give the path of a solution, of a C# project, or of a directory that holds exactly one of them.
 
+## SQLSRC204
+
+**Project does not use SqlSource**
+
+The project given to `sqlsource describe`, or named with `--project`, was restored and has nothing of the SqlSource package.  The tool tells by a property that the package's MSBuild files set.  A project of a solution that was not named is not an error: it is left out, and nothing is said.
+
+```console
+$ dotnet sqlsource describe src/Tools/Tools.csproj
+/work/src/Tools/Tools.csproj : error SQLSRC204: '/work/src/Tools/Tools.csproj' does not use SqlSource
+    help: add the SqlSource package to the project
+```
+
+Add the package with `dotnet add package SqlSource` and restore, or run the command on the project that has the queries.  A project with several target frameworks is read by the first of them: one that references SqlSource for a later framework alone is this error.
+
 ## SQLSRC205
 
 **Project could not be evaluated**
@@ -605,6 +623,33 @@ $ dotnet sqlsource describe
 
 The SqlSource package of the project and the `sqlsource` tool are of different versions.  Update the older of the two: the package in the project file, or the tool with `dotnet tool update SqlSource.Tool`.  The two are released together under one version number.
 
+## SQLSRC207
+
+**Project is not in the run**
+
+A path given with `--project` is not a C# project of the solution that the run is on.  When the run is on a project, the option may name that project and no other.  The message holds the full path the tool looked at: a relative path is resolved against the current directory, not against the folder of the solution.
+
+```console
+$ dotnet sqlsource describe App.slnx --project Tools.csproj
+sqlsource : error SQLSRC207: '/work/Tools.csproj' is not a project of '/work/App.slnx'
+```
+
+Give the path of a `.csproj` file that the solution lists.  `dotnet sln list` shows them.
+
+## SQLSRC220
+
+**Project was not restored**
+
+The tool tells that a project uses SqlSource by the package's MSBuild files, and those reach a project only through a restore.  This project either references the SqlSource package and has nothing of it, so it was not restored since the reference was added, or has no file of a restore at all, so nothing says whether it uses SqlSource.  The tool does not restore: a restore is slow and writes into the project.
+
+```console
+$ dotnet sqlsource describe
+/work/src/App/App.csproj : error SQLSRC220: '/work/src/App/App.csproj' has not been restored, or not since the SqlSource package was added to it
+    help: run 'dotnet restore'
+```
+
+Run `dotnet restore` on the solution or the project, then the command again.  In a solution every C# project is checked, one that has no SQL too: a run on a fresh checkout must not succeed by finding nothing.  A reference that keeps the package's MSBuild files out of the project, `ExcludeAssets="build"` for one, gives this error after a restore as well: remove that from the reference.
+
 ## SQLSRC222
 
 **Directory cannot be read**
@@ -617,3 +662,16 @@ sqlsource : error SQLSRC222: '/srv/locked' cannot be read: Access to the path '/
 ```
 
 Give yourself the right to read the directory.
+
+## SQLSRC223
+
+**Solution cannot be read**
+
+The `.sln` or `.slnx` file that `sqlsource describe` was given, or found in the directory, is not a solution that can be read.  The message ends with the reason of the library that reads it, which is the one the `dotnet` command uses.  A solution that lists one project twice is refused too.
+
+```console
+$ dotnet sqlsource describe
+/work/App.sln : error SQLSRC223: '/work/App.sln' cannot be read: Not a solution file.
+```
+
+Mend the file.  `dotnet sln list` reads it the same way.
````

Change `src/SqlSource/AGENTS.md`:

```diff
--- a/src/SqlSource/AGENTS.md
+++ b/src/SqlSource/AGENTS.md
@@ -59,7 +59,7 @@ Every diagnostic is a `DiagnosticDescriptor`: an error, tagged `NotConfigurable`
 - **`SQLSRC011` and `SQLSRC014` have no position.**  The compiler does not say where an MSBuild property or the metadata of an item was set, so `SqlSourceGenerator` reports them with `Location.None`, built in an output step.  They do not travel as a `DiagnosticInfo`, and neither does `SQLSRC013`, which travels as a `PathCollision`.  An invalid dialect of a file travels as `ParsedSqlFile.InvalidDialect`, and an invalid setting as an `InvalidSetting` of `ProjectSettings` or `FileSettings`; each is reported once for each distinct value.
 - **`SQLSRC901` is a suppression, not a diagnostic.**  `Diagnostics/AttributeConflictSuppressor.cs` is the package's one analyzer: it turns off CS0436 at a use of the types that `AttributeSource.GeneratedTypes` lists, for a project that sees the internals of another one that uses SqlSource.  It is not a step of the pipeline, so it may read symbols and syntax.  Its `SuppressionDescriptor` is not in `SqlDiagnostics.All`, in `AnalyzerReleases.Unshipped.md` or in a section of `docs/diagnostics.md`.  It must suppress nothing but those types, as declared in the project being compiled: `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` holds both halves.  It hides the conflict and does not remove it (`docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`).
 - **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, so a helper method that builds descriptors hides them from it.
-- **An id below 100 is about the attributed type or the project, one from 101 is about a `.sql` file, and one from 200 is about the snapshot or the tool.**  `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` holds the tool's to their range.  The ids `SQLSRC200` to `SQLSRC222` are assigned to the sub-phases of phase 2 in the epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: take a new id from there, not the next free one.  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.
+- **An id below 100 is about the attributed type or the project, one from 101 is about a `.sql` file, and one from 200 is about the snapshot or the tool.**  `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` holds the tool's to their range.  The ids `SQLSRC200` to `SQLSRC223` are assigned to the sub-phases of phase 2 in the epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: take a new id from there, not the next free one.  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.
 
 ## `Parsing/`
 
```

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.ToolDiagnosticsTests`
Expected: every test passes.

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Diagnostics.DiagnosticsDocumentTests`
Expected: every test passes: the sections are in the order of the ids, `SQLSRC223` last.

- [ ] **Step 4: Write the failing tests of the solution reader**

Create `tests/SqlSource.Tool.Tests/SolutionReaderTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class SolutionReaderTests : IDisposable
{
    // The type of a C# project and of an F# project, as a .sln names them.
    private const string CSharp = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
    private const string FSharp = "{F2A71F9B-5D33-465A-A702-920D77279786}";
    private const string SolutionFolder = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    // A solution folder, a project under it, one beside the solution, one in a folder above it, and one of another
    // language.  A .sln writes its paths with "\" on every system.
    private const string Sln = $$"""
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{{SolutionFolder}}") = "src", "src", "{00000000-0000-0000-0000-000000000001}"
        EndProject
        Project("{{CSharp}}") = "Web", "src\Web\Web.csproj", "{00000000-0000-0000-0000-000000000002}"
        EndProject
        Project("{{CSharp}}") = "App", "App.csproj", "{00000000-0000-0000-0000-000000000003}"
        EndProject
        Project("{{CSharp}}") = "Shared", "..\Shared\Shared.csproj", "{00000000-0000-0000-0000-000000000004}"
        EndProject
        Project("{{FSharp}}") = "Script", "src\Script\Script.fsproj", "{00000000-0000-0000-0000-000000000005}"
        EndProject

        """;

    private const string Slnx = """
        <Solution>
            <Folder Name="/src/">
                <Project Path="src/Web/Web.csproj" />
                <Project Path="src/Script/Script.fsproj" />
            </Folder>
            <Project Path="App.csproj" />
            <Project Path="../Shared/Shared.csproj" />
        </Solution>

        """;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static Task<SolutionProjects> ReadAsync(string path) =>
        SolutionReader.ReadAsync(path, TestContext.Current.CancellationToken);

    private string[] Expected() =>
        // Ordinal order of the full paths: "Shared" is beside the solution's folder, and "A" is before "s".
        [
            _folder.PathOf("Shared/Shared.csproj"),
            _folder.PathOf("Solution/App.csproj"),
            _folder.PathOf("Solution/src/Web/Web.csproj"),
        ];

    [Theory]
    [InlineData("Solution/App.sln", Sln)]
    [InlineData("Solution/App.slnx", Slnx)]
    [InlineData("Solution/APP.SLNX", Slnx)]
    public async Task Read_Solution_GivesItsCSharpProjectsInOrdinalOrder(string file, string content)
    {
        var read = await ReadAsync(_folder.WriteFile(file, content));

        read.Failure.ShouldBeNull();
        read.Projects.ShouldBe(Expected());
    }

    [Fact]
    public async Task Read_SolutionWithNoProject_GivesNone()
    {
        var read = await ReadAsync(_folder.WriteFile("App.slnx", "<Solution />"));

        read.Failure.ShouldBeNull();
        read.Projects.ShouldBeEmpty();
    }

    // The library refuses a solution that lists one project twice, whatever the two spellings, so the list that
    // is read never holds a project twice.
    [Theory]
    [InlineData("src/App/App.csproj")]
    [InlineData("src/app/APP.csproj")]
    [InlineData("src/App/../App/App.csproj")]
    public async Task Read_ProjectListedTwice_IsNoSolution(string second)
    {
        var read = await ReadAsync(
            _folder.WriteFile(
                "App.slnx",
                $"<Solution><Project Path=\"src/App/App.csproj\" /><Project Path=\"{second}\" /></Solution>"
            )
        );

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    // The reason is the library's own text, in the language of the machine, so it is not compared.
    [Theory]
    [InlineData("App.sln", "")]
    [InlineData("App.sln", "this is not a solution\n")]
    [InlineData("App.slnx", "")]
    [InlineData("App.slnx", "<Solution><Project Path=\"App.csproj\"")]
    [InlineData("App.slnx", "<Project Sdk=\"Microsoft.NET.Sdk\" />")]
    public async Task Read_FileThatIsNoSolution_GivesTheReason(string file, string content)
    {
        var read = await ReadAsync(_folder.WriteFile(file, content));

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_FileThatIsGone_GivesTheReason()
    {
        var read = await ReadAsync(_folder.PathOf("Gone.slnx"));

        read.Failure.ShouldNotBeNullOrWhiteSpace();
        read.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_FileOfAnotherKind_IsNotRead()
    {
        var read = await ReadAsync(_folder.WriteFile("App.slnf", "{}"));

        read.Failure.ShouldBe("it is neither a .sln nor a .slnx file");
    }
}
```

- [ ] **Step 5: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SolutionReaderTests`
Expected: the build fails: `SolutionReader` and `SolutionProjects` do not exist.

- [ ] **Step 6: Write the solution reader**

Create `src/SqlSource.Tool/Projects/SolutionProjects.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// The C# projects of a solution, or why the solution could not be read.
/// </summary>
/// <param name="Projects">The full paths of the <c>.csproj</c> files, in ordinal order.</param>
/// <param name="Failure">What the reader said when it could not read the file; null when it could.</param>
internal sealed record SolutionProjects(ImmutableArray<string> Projects, string? Failure);
```

Create `src/SqlSource.Tool/Projects/SolutionReader.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Lists the C# projects of a <c>.sln</c> or <c>.slnx</c> file, with the library the <c>dotnet</c> command reads
/// them with.
/// </summary>
internal static class SolutionReader
{
    /// <summary>
    /// Reads the solution.  A file that is no solution is not an exception: the user wrote it, and the caller
    /// reports it.
    /// </summary>
    /// <param name="solutionPath">The full path of the solution file.</param>
    /// <param name="cancellationToken">Ends the read.</param>
    public static async Task<SolutionProjects> ReadAsync(string solutionPath, CancellationToken cancellationToken)
    {
        if (SolutionSerializers.GetSerializerByMoniker(solutionPath) is not { } serializer)
        {
            return new SolutionProjects([], "it is neither a .sln nor a .slnx file");
        }

        SolutionModel solution;
        try
        {
            solution = await serializer.OpenAsync(solutionPath, cancellationToken);
        }
        // The library gives a .slnx that is no XML as the exception of the XML reader.
        catch (Exception exception)
            when (exception is SolutionException or XmlException or IOException or UnauthorizedAccessException)
        {
            return new SolutionProjects([], exception.Message);
        }

        var directory = Path.GetDirectoryName(solutionPath) ?? "";
        return new SolutionProjects(
            [
                .. solution
                    .SolutionProjects.Select(project => project.FilePath)
                    // A project of another language is not the generator's.
                    .Where(static path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                    // A .sln writes a path with "\" on every system.  The library gives it with the separator of this
                    // one today, and nothing promises that.
                    .Select(path => Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar), directory))
                    // The library refuses a solution that lists one project twice, so each is here once.
                    .Order(StringComparer.Ordinal),
            ],
            Failure: null
        );
    }
}
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SolutionReaderTests`
Expected: every test passes.

- [ ] **Step 8: Write the failing tests of the projects of a run**

Create `tests/SqlSource.Tool.Tests/RunProjectsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Which projects a run is on, and what it says of the ones it cannot read, with a runner that answers in MSBuild's
// place.
public sealed class RunProjectsTests : IDisposable
{
    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private readonly TempFolder _folder = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly string _solution;
    private readonly string _a;
    private readonly string _b;
    private readonly string _c;

    public RunProjectsTests()
    {
        _a = _folder.WriteFile("A/A.csproj");
        _b = _folder.WriteFile("B/B.csproj");
        _c = _folder.WriteFile("C/C.csproj");
        // Not in the order of their paths, and with a project of another language.
        _solution = _folder.WriteFile(
            "App.slnx",
            """
            <Solution>
                <Project Path="C/C.csproj" />
                <Project Path="A/A.csproj" />
                <Project Path="F/F.fsproj" />
                <Project Path="B/B.csproj" />
            </Solution>
            """
        );
    }

    public void Dispose()
    {
        _folder.Dispose();
        _error.Dispose();
    }

    private Task<ImmutableArray<ProjectManifest>> FindAsync(RunUnit unit, params string[] named) =>
        RunProjects.FindAsync(
            unit,
            named,
            Hosts.Create(_folder.Path, _runner, _folder.CreateFolder("tmp")),
            new Reporter(_error),
            TestContext.Current.CancellationToken
        );

    private Task<ImmutableArray<ProjectManifest>> FindInSolutionAsync(params string[] named) =>
        FindAsync(new RunUnit(RunUnitKind.Solution, _solution), named);

    private Task<ImmutableArray<ProjectManifest>> FindInProjectAsync(params string[] named) =>
        FindAsync(new RunUnit(RunUnitKind.Project, _a), named);

    // The projects MSBuild was asked about, each once, in the order of the first question.
    private IEnumerable<string> Asked() => _runner.Requests.Select(request => request.Arguments[1]).Distinct();

    private FakeProject DoesNotUse(string project)
    {
        var assets = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(assets)!);
        File.WriteAllText(assets, "{}");
        return _runner.Projects[project] = new FakeProject { Imported = "", ProjectAssetsFile = assets };
    }

    private FakeProject NotRestored(string project) =>
        _runner.Projects[project] = new FakeProject
        {
            Imported = "",
            ProjectAssetsFile = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json"),
        };

    private static string Sqlsrc204(string project) =>
        $"{project} : error SQLSRC204: '{project}' does not use SqlSource\n"
        + "    help: add the SqlSource package to the project\n"
        + See
        + "204\n";

    private static string Sqlsrc220(string project) =>
        $"{project} : error SQLSRC220: '{project}' has not been restored, or not since the SqlSource package was "
        + "added to it\n"
        + "    help: run 'dotnet restore'\n"
        + See
        + "220\n";

    private string Sqlsrc207(string path, string? unit = null) =>
        $"sqlsource : error SQLSRC207: '{path}' is not a project of '{unit ?? _solution}'\n" + See + "207\n";

    [Fact]
    public async Task Find_ProjectThatUsesSqlSource_GivesItsManifest()
    {
        var manifests = await FindInProjectAsync();

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_a);
        _error.ToString().ShouldBeEmpty();
        // A project alone is given nothing of a solution.
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "SolutionDir") == null);
    }

    [Fact]
    public async Task Find_ProjectThatDoesNotUseSqlSource_IsSqlsrc204()
    {
        _ = DoesNotUse(_a);

        (await FindInProjectAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc204(_a));
    }

    [Fact]
    public async Task Find_ProjectThatWasNotRestored_IsSqlsrc220()
    {
        _ = NotRestored(_a);

        (await FindInProjectAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc220(_a));
    }

    [Fact]
    public async Task Find_Solution_GivesTheManifestsOfItsCSharpProjectsInOrdinalOrder()
    {
        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _b, _c]);
        _error.ToString().ShouldBeEmpty();
        Asked().ShouldBe([_a, _b, _c], ignoreOrder: true);
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "SolutionPath") == _solution);
    }

    [Fact]
    public async Task Find_SolutionWithAProjectThatDoesNotUseSqlSource_LeavesItOutAndSaysNothing()
    {
        _ = DoesNotUse(_b);

        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_SolutionWhereNoProjectUsesSqlSource_GivesNoneAndSaysNothing()
    {
        _ = DoesNotUse(_a);
        _ = DoesNotUse(_b);
        _ = DoesNotUse(_c);

        (await FindInSolutionAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBeEmpty();
    }

    // A fresh checkout: nothing was restored, so no project has the package's props.  The run must not pass.
    [Fact]
    public async Task Find_SolutionWithAProjectThatWasNotRestored_IsSqlsrc220AndReadsTheRest()
    {
        _ = NotRestored(_b);

        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        _error.ToString().ShouldBe(Sqlsrc220(_b));
    }

    [Fact]
    public async Task Find_SolutionWithProjectsThatFail_ReportsThemInTheOrderOfTheProjects()
    {
        _runner.Projects[_a] = new FakeProject { EvaluationExitCode = 1, Error = "error of A" };
        _runner.Projects[_c] = new FakeProject { Manifest = "SqlSourceManifest=7\n" };
        // A answers last.
        _runner.BeforeAnswer = (request, token) => Task.Delay(request.Arguments[1] == _a ? 80 : 0, token);

        var manifests = await FindInSolutionAsync();

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        var errors = _error.ToString();
        errors.ShouldStartWith(
            $"{_a} : error SQLSRC205: MSBuild could not evaluate '{_a}'\n"
                + "    reason: dotnet msbuild ended with the exit code 1\n"
                + "    msbuild: error of A\n"
                + See
                + "205\n"
                + $"{_c} : error SQLSRC206: The project manifest of '{_c}' cannot be read: it has version '7'"
        );
        errors.ShouldEndWith(See + "206\n");
    }

    [Fact]
    public async Task Find_OneProjectNamed_IsTheOnlyOneAskedAbout()
    {
        var manifests = await FindInSolutionAsync(_b);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        Asked().ShouldBe([_b]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_TwoProjectsNamed_AreGivenInTheOrderOfTheSolution()
    {
        // A relative path is resolved against the working directory, and a path is compared ignoring case.
        var manifests = await FindInSolutionAsync(Path.Combine("C", "C.csproj"), _a.ToUpperInvariant());

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        Asked().ShouldBe([_a, _c], ignoreOrder: true);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_ProjectNamedTwice_IsAskedAboutOnce()
    {
        var manifests = await FindInSolutionAsync(_b, Path.Combine("B", "B.csproj"), _b);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        _runner.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Find_NamedPathOutsideTheSolution_IsSqlsrc207AndTheRestIsRead()
    {
        var outside = _folder.WriteFile("Other/Other.csproj");

        var manifests = await FindInSolutionAsync(Path.Combine("Other", "Other.csproj"), _b, outside);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        Asked().ShouldBe([_b]);
        // Once, though it was named twice.
        _error.ToString().ShouldBe(Sqlsrc207(outside));
    }

    [Theory]
    [InlineData("F/F.fsproj")]
    [InlineData("App.slnx")]
    [InlineData("A")]
    public async Task Find_NamedPathThatIsNoCSharpProjectOfTheSolution_IsSqlsrc207(string path)
    {
        (await FindInSolutionAsync(path)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(_folder.PathOf(path)));
    }

    // What --project "$UNSET" gives.
    [Fact]
    public async Task Find_EmptyNamedPath_IsSqlsrc207AndNotTheWorkingDirectory()
    {
        (await FindInSolutionAsync("")).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(""));
    }

    [Fact]
    public async Task Find_NamedProjectThatDoesNotUseSqlSource_IsSqlsrc204()
    {
        _ = DoesNotUse(_a);
        _ = DoesNotUse(_b);

        var manifests = await FindInSolutionAsync(_b, _c);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_c);
        // B was named.  A was not, and is not asked about.
        _error.ToString().ShouldBe(Sqlsrc204(_b));
        Asked().ShouldBe([_b, _c], ignoreOrder: true);
    }

    [Fact]
    public async Task Find_NamedProjectThatWasNotRestored_IsSqlsrc220()
    {
        _ = NotRestored(_b);

        (await FindInSolutionAsync(_b)).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc220(_b));
    }

    [Fact]
    public async Task Find_ProjectAsTheUnitAndNamed_IsThatProject()
    {
        var manifests = await FindInProjectAsync(Path.Combine("A", "A.csproj"));

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_a);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_ProjectAsTheUnitAndAnotherNamed_IsSqlsrc207AndNothingIsRead()
    {
        (await FindInProjectAsync(_b)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(_b, _a));
    }

    [Fact]
    public async Task Find_SolutionThatCannotBeRead_IsSqlsrc223AndNothingIsRead()
    {
        var solution = _folder.WriteFile("Broken.sln", "this is not a solution\n");

        (await FindAsync(new RunUnit(RunUnitKind.Solution, solution), _a)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        var lines = _error.ToString().Split('\n');
        lines.Length.ShouldBe(3, _error.ToString());
        // The reason is the library's own text.
        lines[0].ShouldStartWith($"{solution} : error SQLSRC223: '{solution}' cannot be read: ");
        lines[0].Length.ShouldBeGreaterThan($"{solution} : error SQLSRC223: '{solution}' cannot be read: ".Length);
        lines[1].ShouldBe(See + "223");
    }

    [Fact]
    public async Task Find_SolutionWithNoCSharpProject_GivesNoneAndStartsNothing()
    {
        var solution = _folder.WriteFile("Empty.slnx", "<Solution><Project Path=\"F/F.fsproj\" /></Solution>");

        (await FindAsync(new RunUnit(RunUnitKind.Solution, solution))).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBeEmpty();
    }
}
```

- [ ] **Step 9: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunProjectsTests`
Expected: the build fails: `RunProjects` does not exist.

- [ ] **Step 10: Write `RunProjects`**

Create `src/SqlSource.Tool/Projects/RunProjects.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Finds the projects of a run and reads the manifest of each: the unit's projects, or the ones that
/// <c>--project</c> names, that use SqlSource.
/// </summary>
internal static class RunProjects
{
    private static readonly ContinuationLine AddThePackage = new("help", "add the SqlSource package to the project");

    private static readonly ContinuationLine Restore = new("help", "run 'dotnet restore'");

    /// <summary>
    /// The manifests of the run's projects, in the order of the projects.  A project with an error is reported and
    /// left out, and the run goes on with the rest.
    /// </summary>
    /// <param name="unit">The solution or the project the run is on.</param>
    /// <param name="named">The paths that <c>--project</c> gave, as they were typed.  Empty when it gave none.</param>
    /// <param name="host">The working directory, and what MSBuild is run with.</param>
    /// <param name="reporter">Where the errors go.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task<ImmutableArray<ProjectManifest>> FindAsync(
        RunUnit unit,
        IReadOnlyList<string> named,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<string> projects = [unit.Path];
        string? solution = null;
        if (unit.Kind == RunUnitKind.Solution)
        {
            var read = await SolutionReader.ReadAsync(unit.Path, cancellationToken);
            if (read.Failure is { } reason)
            {
                reporter.Report(
                    ToolDiagnostic.ForFile(ToolDiagnostics.SolutionCannotBeRead, unit.Path, unit.Path, reason)
                );
                return [];
            }

            projects = read.Projects;
            solution = unit.Path;
        }

        var restricted = named.Count > 0;
        if (restricted)
        {
            projects = Restrict(projects, named, unit, host.WorkingDirectory, reporter);
        }

        var manifests = ImmutableArray.CreateBuilder<ProjectManifest>();
        foreach (var evaluation in await ProjectEvaluator.EvaluateAsync(host, projects, solution, cancellationToken))
        {
            var project = evaluation.ProjectPath;
            switch (evaluation.State)
            {
                case ProjectState.UsesSqlSource:
                    if (evaluation.Manifest is { } manifest)
                    {
                        manifests.Add(manifest);
                    }

                    break;

                case ProjectState.DoesNotUseSqlSource:
                    // A project of a solution that was not asked for by name is left out, and nothing is said:
                    // most solutions hold projects that have no SQL.
                    if (unit.Kind == RunUnitKind.Project || restricted)
                    {
                        reporter.Report(
                            ToolDiagnostic
                                .ForFile(ToolDiagnostics.ProjectDoesNotUseSqlSource, project, project)
                                .WithLines(AddThePackage)
                        );
                    }

                    break;

                case ProjectState.NotRestored:
                    // In a solution too: a run on a fresh checkout must not pass by finding nothing.
                    reporter.Report(
                        ToolDiagnostic.ForFile(ToolDiagnostics.ProjectNotRestored, project, project).WithLines(Restore)
                    );
                    break;

                case ProjectState.Failed:
                    if (evaluation.Failure is { } failure)
                    {
                        reporter.Report(failure);
                    }

                    break;

                default:
                    break;
            }
        }

        return manifests.ToImmutable();
    }

    // The projects of the unit that --project names, in the unit's order.  A path that names none is reported once.
    private static ImmutableArray<string> Restrict(
        ImmutableArray<string> projects,
        IReadOnlyList<string> named,
        RunUnit unit,
        string workingDirectory,
        Reporter reporter
    )
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in named)
        {
            // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
            var path = name.Length == 0 ? name : Path.GetFullPath(name, workingDirectory);
            if (projects.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _ = selected.Add(path);
            }
            else if (reported.Add(path))
            {
                reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.ProjectNotInRun, path, unit.Path));
            }
        }

        return [.. projects.Where(selected.Contains)];
    }
}
```

- [ ] **Step 11: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunProjectsTests`
Expected: every test passes.

- [ ] **Step 12: Write the tests of the command line**

Change `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/UsageCheckTests.cs
+++ b/tests/SqlSource.Tool.Tests/UsageCheckTests.cs
@@ -15,13 +15,14 @@ public class UsageCheckTests
     private static Command Root()
     {
         var connection = new Option<string>("--connection", "-c") { Arity = ArgumentArity.ExactlyOne };
+        var project = new Option<string[]>("--project") { Arity = ArgumentArity.OneOrMore };
         var force = new Option<bool>("--force") { Arity = ArgumentArity.Zero };
         var verbose = new Option<bool>("--verbose") { Arity = ArgumentArity.Zero, Recursive = true };
         var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
         return new Command("sqlsource")
         {
             verbose,
-            new Command("describe") { connection, force, path },
+            new Command("describe") { connection, project, force, path },
         };
     }
 
@@ -35,6 +36,8 @@ public class UsageCheckTests
     [InlineData("describe", "-c", "billing=Host=db", "App.csproj", "--force")]
     [InlineData("--verbose", "describe")]
     [InlineData("describe", "--verbose")]
+    [InlineData("describe", "--project", "A.csproj")]
+    [InlineData("describe", "--project=A.csproj", "App.slnx", "--project", "B.csproj")]
     public void Check_CommandLineWithNothingWrong_GivesNoLine(params string[] args) =>
         UsageCheck.Check(Root(), args).Messages.ShouldBeEmpty();
 
@@ -109,6 +112,23 @@ public class UsageCheckTests
                 "sqlsource: option '--force' takes no value",
             ]);
 
+    // An option that may be given several times takes one value each time, and the token after that is the path.
+    [Fact]
+    public void Check_OptionThatMayBeRepeated_TakesOneValueEachTime()
+    {
+        var usage = UsageCheck.Check(Root(), ["describe", "--project", "A.csproj", "App.slnx", "--project", Secret]);
+
+        usage.Messages.ShouldBeEmpty();
+        usage.Arguments.ShouldBe(["App.slnx"]);
+    }
+
+    [Theory]
+    [InlineData("describe", "--project")]
+    [InlineData("describe", "--project=")]
+    [InlineData("describe", "--project", "--force")]
+    public void Check_RepeatedOptionWithoutAValue_NeedsOne(params string[] args) =>
+        UsageCheck.Check(Root(), args).Messages.ShouldBe(["sqlsource: option '--project' needs a value"]);
+
     [Fact]
     public void Check_CommandLineWithNothingWrong_GivesTheCommandAndItsArguments()
     {
@@ -144,6 +164,9 @@ public class UsageCheckTests
             "--connection=V",
             "--connection:V",
             "-c",
+            "--project",
+            "--project=",
+            "--project=P",
             "--force",
             "--force=V",
             "--verbose",
```

Change `tests/SqlSource.Tool.Tests/CliRun.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/CliRun.cs
+++ b/tests/SqlSource.Tool.Tests/CliRun.cs
@@ -24,6 +24,10 @@ internal sealed class CliRun : IDisposable
 
     public Task<CliResult> RunAsync(params string[] args) => InvokeAsync(args, TestContext.Current.CancellationToken);
 
+    // A run that the test ends itself.
+    public Task<CliResult> RunAsync(CancellationToken cancellationToken, params string[] args) =>
+        InvokeAsync(args, cancellationToken);
+
     public async Task<CliResult> RunCancelledAsync(params string[] args)
     {
         using var cancelled = new CancellationTokenSource();
```

Change `tests/SqlSource.Tool.Tests/DescribeTests.cs`:

```diff
--- a/tests/SqlSource.Tool.Tests/DescribeTests.cs
+++ b/tests/SqlSource.Tool.Tests/DescribeTests.cs
@@ -1,3 +1,6 @@
+using System.IO;
+using System.Linq;
+using System.Threading;
 using System.Threading.Tasks;
 using Shouldly;
 using Xunit;
@@ -85,6 +88,155 @@ public class DescribeTests
         result.ShouldBe(new CliResult(0, "", ""));
     }
 
+    [Fact]
+    public async Task Run_Help_ShowsTheProjectOption()
+    {
+        using var run = new CliRun();
+
+        var result = await run.RunAsync("describe", "--help");
+
+        result.ExitCode.ShouldBe(0);
+        result.Out.ShouldContain("--project <path>");
+        run.Processes.Requests.ShouldBeEmpty();
+    }
+
+    [Theory]
+    [InlineData("describe", "--project")]
+    [InlineData("describe", "--project=")]
+    [InlineData("describe", "App.slnx", "--project", Secret, "--project")]
+    public async Task Run_ProjectOptionWithoutAValue_NamesTheOptionAndRunsNothing(params string[] args)
+    {
+        using var run = new CliRun();
+        _ = run.Folder.WriteFile("App.slnx", "<Solution />");
+
+        var result = await run.RunAsync(args);
+
+        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--project' needs a value\n"));
+        run.Processes.Requests.ShouldBeEmpty();
+    }
+
+    [Fact]
+    public async Task Run_Solution_ReadsItsProjectsAndPrintsNothing()
+    {
+        using var run = new CliRun();
+        _ = run.Folder.WriteFile(
+            "App.slnx",
+            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
+        );
+
+        var result = await run.RunAsync("describe");
+
+        result.ShouldBe(new CliResult(0, "", ""));
+        // Two runs of MSBuild for each of the two projects.
+        run.Processes.Requests.Count.ShouldBe(4);
+        Directory.EnumerateFileSystemEntries(run.Folder.PathOf("tmp")).ShouldBeEmpty();
+    }
+
+    [Fact]
+    public async Task Run_ProjectOptionGivenTwice_RestrictsTheRunToBoth()
+    {
+        using var run = new CliRun();
+        var a = run.Folder.WriteFile("A/A.csproj");
+        var c = run.Folder.WriteFile("C/C.csproj");
+        _ = run.Folder.WriteFile(
+            "App.slnx",
+            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" />"
+                + "<Project Path=\"C/C.csproj\" /></Solution>"
+        );
+
+        var result = await run.RunAsync("describe", "--project", "A/A.csproj", "App.slnx", "--project=" + c);
+
+        result.ShouldBe(new CliResult(0, "", ""));
+        run.Processes.Requests.Select(request => request.Arguments[1]).Distinct().ShouldBe([a, c], ignoreOrder: true);
+    }
+
+    [Fact]
+    public async Task Run_ProjectOptionOutsideTheSolution_IsSqlsrc207AndExitsWithOne()
+    {
+        using var run = new CliRun();
+        var solution = run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");
+
+        var result = await run.RunAsync("describe", "--project", "Other.csproj");
+
+        result.ExitCode.ShouldBe(1);
+        result.Out.ShouldBeEmpty();
+        result.Error.ShouldBe(
+            $"sqlsource : error SQLSRC207: '{run.Folder.PathOf("Other.csproj")}' is not a project of '{solution}'\n"
+                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc207\n"
+        );
+    }
+
+    [Fact]
+    public async Task Run_ProjectThatDoesNotUseSqlSource_IsSqlsrc204AndExitsWithOne()
+    {
+        using var run = new CliRun();
+        var project = run.Folder.WriteFile("App.csproj");
+        // Restored, and nothing of the package.
+        run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = project };
+
+        var result = await run.RunAsync("describe");
+
+        result.ExitCode.ShouldBe(1);
+        result.Out.ShouldBeEmpty();
+        result.Error.ShouldStartWith($"{project} : error SQLSRC204: '{project}' does not use SqlSource\n");
+    }
+
+    // A run that had nothing to do says so, and is no error: a solution may gain its first query later.
+    [Fact]
+    public async Task Run_SolutionWhereNoProjectUsesSqlSource_SaysSoAndExitsWithZero()
+    {
+        using var run = new CliRun();
+        var solution = run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"A/A.csproj\" /></Solution>");
+        run.Processes.Default = new FakeProject { Imported = "", ProjectAssetsFile = solution };
+
+        var result = await run.RunAsync("describe");
+
+        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
+    }
+
+    [Fact]
+    public async Task Run_SolutionWithNoProject_SaysThatNoProjectUsesSqlSource()
+    {
+        using var run = new CliRun();
+        var solution = run.Folder.WriteFile("App.slnx", "<Solution />");
+
+        var result = await run.RunAsync("describe");
+
+        result.ShouldBe(new CliResult(0, $"sqlsource: no project of '{solution}' uses SqlSource\n", ""));
+        run.Processes.Requests.ShouldBeEmpty();
+    }
+
+    [Fact]
+    public async Task Run_SolutionThatCannotBeRead_IsSqlsrc223AndSaysNothingElse()
+    {
+        using var run = new CliRun();
+        var solution = run.Folder.WriteFile("App.sln", "this is not a solution\n");
+
+        var result = await run.RunAsync("describe");
+
+        result.ExitCode.ShouldBe(1);
+        result.Out.ShouldBeEmpty();
+        result.Error.ShouldStartWith($"{solution} : error SQLSRC223: '{solution}' cannot be read: ");
+    }
+
+    [Fact]
+    public async Task Run_CancelledWhileMSBuildRuns_ExitsWithOneAndPrintsNothing()
+    {
+        using var run = new CliRun();
+        _ = run.Folder.WriteFile("App.csproj");
+        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
+        run.Processes.BeforeAnswer = async (_, token) =>
+        {
+            await cancel.CancelAsync();
+            await Task.Delay(Timeout.Infinite, token);
+        };
+
+        var result = await run.RunAsync(cancel.Token, "describe");
+
+        result.ShouldBe(new CliResult(1, "", ""));
+        Directory.EnumerateFileSystemEntries(run.Folder.PathOf("tmp")).ShouldBeEmpty();
+    }
+
     [Fact]
     public async Task Run_NoUnit_ReportsItAndExitsWithOne()
     {
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.UsageCheckTests`
Expected: every test passes as it is written: `UsageCheck` already reads an option that takes a value, and these hold it for one that may be repeated.  `Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine` now walks seventeen tokens.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DescribeTests`
Expected: the new tests fail: `describe` has no `--project`, so `sqlsource: unknown option '--project'` is what it writes, and it asks MSBuild nothing.

- [ ] **Step 13: Give `describe` its option and its run**

Change `src/SqlSource.Tool/Cli.cs`:

```diff
--- a/src/SqlSource.Tool/Cli.cs
+++ b/src/SqlSource.Tool/Cli.cs
@@ -180,13 +180,46 @@ public static class Cli
             Arity = ArgumentArity.ZeroOrOne,
         };
 
-        var describe = new Command("describe", "Finds the project or the solution to describe") { path };
-        describe.SetAction(parsed =>
+        // One value each time it is given, and it may be given several times: UsageCheck reads the token after it as
+        // its value and no further.
+        var project = new Option<string[]>("--project")
         {
-            // The unit is all this sub-phase finds.  An error of it is in the reporter, where the exit code is taken.
-            _ = RunUnitFinder.Find(parsed.GetValue(path), host.WorkingDirectory, reporter);
-            return 0;
-        });
+            Description = "A project to run on, of the solution.  May be given several times.",
+            HelpName = "path",
+            Arity = ArgumentArity.OneOrMore,
+            AllowMultipleArgumentsPerToken = false,
+        };
+
+        var describe = new Command("describe", "Finds the projects to describe and reads what the compiler is given")
+        {
+            path,
+            project,
+        };
+        describe.SetAction(
+            async (parsed, cancellationToken) =>
+            {
+                // An error is in the reporter, where the exit code is taken.
+                if (RunUnitFinder.Find(parsed.GetValue(path), host.WorkingDirectory, reporter) is not { } unit)
+                {
+                    return 0;
+                }
+
+                var manifests = await RunProjects.FindAsync(
+                    unit,
+                    parsed.GetValue(project) ?? [],
+                    host,
+                    reporter,
+                    cancellationToken
+                );
+                if (manifests.IsEmpty && reporter.Count == 0)
+                {
+                    // A run that found nothing to do must not look like one that did it.
+                    await host.Out.WriteLineAsync($"sqlsource: no project of '{OneLine.Of(unit.Path)}' uses SqlSource");
+                }
+
+                return 0;
+            }
+        );
         return describe;
     }
 
```

- [ ] **Step 14: Run the tests, and the tool**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: every test of the project passes.

```bash
dotnet run --project src/SqlSource.Tool -- describe --help
```

Expected: the usage is `sqlsource describe [<path>] [options]`, and the options list `--project <path>`.

```bash
dotnet run --project src/SqlSource.Tool -- describe
```

Expected: nothing is printed and the exit code is `0`: of the solution's five projects, `tests/SqlSource.Tests` uses SqlSource and its manifest was read, and the others were left out.

```bash
dotnet run --project src/SqlSource.Tool -- describe --project src/SqlSource/SqlSource.csproj --project README.md
```

Expected: `SQLSRC207` for the full path of `README.md`, which is no project of the solution, and then `SQLSRC204` for `src/SqlSource/SqlSource.csproj`, with `help: add the SqlSource package to the project`.  The exit code is `1`.

- [ ] **Step 15: Amend the two items of the shell**

Change `docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md`:

```diff
--- a/docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md
+++ b/docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md
@@ -2,10 +2,11 @@
 
 ## Problem
 
-The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  Two things are still printed as the user typed them.
+The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  Three things are still printed as the user typed them.
 
 - **The name of an unknown option**, which is the token cut at its first `=` or `:`.  A token that starts with `-` and holds a secret with neither character is printed whole: `-pS3cret`, the way `mysql` takes a password, gives `sqlsource: unknown option '-pS3cret'`.
 - **The path of `describe`**, in `SQLSRC201` to `SQLSRC203` and `SQLSRC222`, from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs).  A value typed where the path stands is printed as a path: `sqlsource describe "Host=db;Password=S3cret"` gives `SQLSRC203` with the whole string in it.
+- **A path given with `--project`**, in `SQLSRC204`, `SQLSRC207` and `SQLSRC220`, from [`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs), as a full path.  A value typed after `--project` by mistake is printed whole in `SQLSRC207`.
 
 ## Why it exists
 
```

Change `docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md`:

```diff
--- a/docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md
+++ b/docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md
@@ -20,7 +20,7 @@ Low today.  The first is a secret in the tool's output from sub-phase 2.5 on, an
 
 1. Catch the exceptions of a driver where a connection is opened and a query described, and report each with an id of its own and without the connection's text, so that none reaches the general catch.
 2. Let a second Ctrl+C end the process: leave `ConsoleCancelEventArgs.Cancel` false once the token is cancelled.
-3. With the first option that takes a value, add a test that gives it twice and expects the tool's own line and nothing of the command line in the output.
+3. With the first option that may be given only once, add a test that gives it twice and expects the tool's own line and nothing of the command line in the output.  `--project`, the first option that takes a value, may be given several times, so it cannot serve.
 
 ## Trigger
 
```

Change `docs/tech-debt/README.md`:

```diff
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -18,7 +18,7 @@ Next id: `TD-0029`
 | [TD-0019](TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md) | Open | 2026-10-08 | Low | A change to the set of claimed files, such as a file added to a type's folder or a changed `Path`, reads every claimed file again |
 | [TD-0021](TD-0021-comments-and-token-defaults.md) | Open | 2026-10-08 | Low | A `--` comment inside an inline default swallows its closing braces, a `-- token:` default is compared as written while an inline one is compared without comments, and a comment can be scanned for tokens where no type keeps comments |
 | [TD-0022](TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md) | Open | 2026-10-08 | Low | A query that keeps its comments has its SQL built and scanned for tokens twice, so its parse allocates and takes about twice as much |
-| [TD-0023](TD-0023-unknown-option-and-path-are-printed-as-given.md) | Open | 2026-10-09 | Low | The `sqlsource` tool prints the name of an unknown option and the path of `describe` as they were typed, so a secret typed as either reaches its output |
+| [TD-0023](TD-0023-unknown-option-and-path-are-printed-as-given.md) | Open | 2026-10-09 | Low | The `sqlsource` tool prints the name of an unknown option, the path of `describe` and a path of `--project` as they were typed, so a secret typed as any of them reaches its output |
 | [TD-0024](TD-0024-gaps-of-the-tools-shell.md) | Open | 2026-10-09 | Low | Three gaps of the `sqlsource` tool's shell that wait for sub-phase 2.5: `SQLSRC200` prints the message of any exception, a driver's included; Ctrl+C is always swallowed; and no test reaches the line the tool writes for a command line that System.CommandLine rejects |
 | [TD-0025](TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md) | Open | 2026-10-09 | Low | The `sqlsource` tool does not see a `.sql` file, or a C# file, that a target adds from a hook of the build: only a target that hooks `SqlSourceTrimMetadataOfFiles` runs before the project manifest is written |
 | [TD-0026](TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md) | Open | 2026-10-09 | Low | For a project with several target frameworks the `sqlsource` tool reads the first one alone: a file, an attribute or a reference to SqlSource that only another framework has is not seen |
```

- [ ] **Step 16: Say where a run's projects are decided**

Change `src/SqlSource.Tool/AGENTS.md`:

```diff
--- a/src/SqlSource.Tool/AGENTS.md
+++ b/src/SqlSource.Tool/AGENTS.md
@@ -9,6 +9,7 @@ The `sqlsource` command, packed as the .NET tool `SqlSource.Tool`.  `tests/SqlSo
 - **The tool never restores and never builds a project, and writes nothing into one.**  `Projects/ProjectEvaluator.cs` runs `dotnet msbuild` to evaluate a project and to run the package's target `SqlSourceWriteManifest`, which writes to a file the tool named in a temporary folder of the run.  Do not add `-restore`, a target of a build, or a file under a project's folder: `tests/SqlSource.Tool.Tests/FixtureProjectTests.cs` checks that a project has no `obj` afterwards.
 - **MSBuild's output is parsed only as the JSON of `-getProperty` and `-getItem`.**  Everything else it prints is shown, the first twenty lines, under `SQLSRC205`, and never read: MSBuild writes in the language of the machine.  What the tool needs from a target comes in the manifest file, read by `Projects/ManifestReader.cs`; the format is the package's contract, and `src/SqlSource/AGENTS.md` has its rules.
 - **A value given to MSBuild as a property goes through `Projects/MSBuildProperty.cs`.**  MSBuild splits what follows `-p:` at `;` and `,`, and a path holds either.  Each run also sets and removes the environment variables that `ProjectEvaluator` lists, with the reason beside each.
+- **`Projects/RunProjects.cs` is the one place that says which projects a run is on** and what is said of each: the unit's projects, or the ones `--project` names.  A project of a solution that does not use SqlSource is left out silently unless it was named; one that was not restored is an error everywhere.  `Projects/SolutionReader.cs` reads a solution with `Microsoft.VisualStudio.SolutionPersistence`, and the reason of `SQLSRC223` is that library's text: no test compares it.
 - **An evaluation reports nothing.**  `ProjectEvaluator` runs several projects at once and gives each error as data, in a `ProjectEvaluation`, so that the caller reports them in the order of the projects.
 - **Every error goes through `Reporting/Reporter.cs` with a descriptor of `ToolDiagnostics`**, or of `SqlDiagnostics` where the generator reports the same condition.  The descriptors are in the generator's project; its `AGENTS.md` lists the four places a new one touches.  A wrong command line is the one exception, below.
 - **The exit code is `0`, `1` or `2`.**  `1` when the reporter wrote anything, when the command line was wrong, and when the run was cancelled.  `2` is for `--check` alone: a difference, and nothing failed.  `Cli.RunAsync` takes `1` from `Reporter.Count`, so a command reports and goes on, and never returns `1` itself.
```

Change `CONTRIBUTING.md`:

````diff
--- a/CONTRIBUTING.md
+++ b/CONTRIBUTING.md
@@ -76,7 +76,7 @@ This builds in `Release` and writes two packages with one version, `artifacts/pa
 tools/check-package.sh artifacts/packages
 ```
 
-This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly and Roslyn beside it, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.
+This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly, Roslyn and the library that reads a solution beside it, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.
 
 ```bash
 tools/check-package-install.sh artifacts/packages
````

- [ ] **Step 17: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Find the projects of a run

describe runs on the projects of its unit that use SqlSource: a project, or
the C# projects of a solution, read with the library the dotnet command
uses.  --project names the ones to run on.  A project that does not use
SqlSource is SQLSRC204 when it was asked for, one that was not restored is
SQLSRC220 everywhere, a path outside the run is SQLSRC207, and a solution
that cannot be read is SQLSRC223.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 7: The installed package writes its manifest

**Files:**
- Modify: `tools/check-package-install.sh`
- Modify: `CONTRIBUTING.md`

**Interfaces:**
- Consumes: `SqlSourceImported` of task 2 and the target of task 3, from the packed package.
- Produces: nothing a later task uses.

- [ ] **Step 1: Run the target on the installed project**

Change `tools/check-package-install.sh`:

```diff
--- a/tools/check-package-install.sh
+++ b/tools/check-package-install.sh
@@ -1,5 +1,6 @@
 #!/usr/bin/env bash
 # Installs the SqlSource package into a project the way a consumer does, runs the project and checks what it prints.
+# Then asks the project what the sqlsource tool asks one: whether it uses SqlSource, and for its project manifest.
 # Then removes a .sql file the project uses and checks that the next build, an incremental one, fails for it.
 # The project is tools/package-install.  It is copied out of the repository first, so that nothing of the repository's
 # own build applies to it, and it restores from the folder that holds the package and from nowhere else.
@@ -108,6 +109,56 @@ if ! diff "$EXPECTED" "$work/actual-output.txt"; then
     exit 1
 fi
 
+# The sqlsource tool asks a project two things, and the installed package answers both.  SqlSourceImported, which
+# build/SqlSource.props sets, says that the project uses SqlSource: NuGet imported the props, which nothing else in
+# the repository shows.  The target SqlSourceWriteManifest of build/SqlSource.targets writes the project manifest: the
+# tool runs it by name, as here, with a file of its own, and nothing of a build runs before it but the package's trims.
+imported="$(dotnet msbuild "$PROJECT" -nologo -getProperty:SqlSourceImported)"
+if [[ "$imported" != 'true' ]]; then
+    echo "check-package-install: a project that references $package does not have SqlSourceImported" >&2
+    exit 1
+fi
+
+manifest="$work/consumer.manifest"
+dotnet msbuild "$PROJECT" -nologo -t:SqlSourceWriteManifest "-p:SqlSourceManifestFile=$manifest" \
+    >"$work/manifest.log" 2>&1 || {
+    cat "$work/manifest.log" >&2
+    echo "check-package-install: the target SqlSourceWriteManifest of $package fails" >&2
+    exit 1
+}
+
+manifest_fails() {
+    cat "$manifest" >&2
+    echo "check-package-install: the project manifest that $package writes $1" >&2
+    exit 1
+}
+
+if [[ "$(head -n 1 "$manifest")" != 'SqlSourceManifest=1' ]]; then
+    manifest_fails 'does not start with its version'
+fi
+
+# A path is as MSBuild gives it, which on macOS is not the spelling of the temporary directory that mktemp gave.
+if ! grep -q '^File=.*/Queries/Users\.sql$' "$manifest"; then
+    manifest_fails 'does not list Queries/Users.sql'
+fi
+
+# The project writes this dialect over two lines, and Directory.Build.targets the property over four.  The lines
+# under a File line are the metadata of that file.
+if ! grep -A 10 '^File=.*/Queries/ByOption\.sql$' "$manifest" |
+    grep -qx 'File.SqlSourceDialect=mysql, no-backslash-escapes'; then
+    manifest_fails 'does not hold the trimmed dialect of Queries/ByOption.sql'
+fi
+
+if ! grep -qx 'Property.SqlSourceGeneratorParameters=sort-input no-token-validation' "$manifest"; then
+    manifest_fails 'does not hold the trimmed property of Directory.Build.targets'
+fi
+
+# Directory.Build.targets adds this file from a hook of the build, which a target that is run by name does not run.
+# docs/tech-debt/TD-0025.  The build above compiled it: expected-output.txt has its query.
+if grep -q 'AddedByATarget\.sql' "$manifest"; then
+    manifest_fails 'lists a file that a target adds from a hook of the build'
+fi
+
 # A .sql file that is removed has no timestamp left to compare, so the build after it compiles again only if an input
 # of the compiler changed.  The target SqlSourceTrackAdditionalFiles of build/SqlSource.targets writes a hash of the
 # list of AdditionalFiles to a file and names that file as an input: the hash, and so the file, changes when a .sql
@@ -127,4 +178,5 @@ if ! grep -q "error CS0117: .*$REMOVED_MEMBER" "$work/rebuild.log"; then
 fi
 
 echo "check-package-install: $package installs into a project, which builds and prints $FIXTURE/$EXPECTED,"
+echo "check-package-install: which has SqlSourceImported and writes its project manifest,"
 echo "check-package-install: and whose next build compiles again after a .sql file is removed"
```

- [ ] **Step 2: Run the script**

Run: `tools/check-package-install.sh`
Expected: it ends with three lines, the second of them `check-package-install: which has SqlSourceImported and writes its project manifest,`.

To see a check fail, change `'sort-input no-token-validation'` in the script to another text and run it again: it prints the manifest and `does not hold the trimmed property of Directory.Build.targets`.  Change it back.

```bash
tools/shfmt.sh --check
```

Expected: no output.  Without `--check` the script formats the file.

- [ ] **Step 3: Say what the script checks now**

Change `CONTRIBUTING.md`:

````diff
--- a/CONTRIBUTING.md
+++ b/CONTRIBUTING.md
@@ -82,7 +82,7 @@ This checks what the two packages hold.  `SqlSource`: the generator under `analy
 tools/check-package-install.sh artifacts/packages
 ```
 
-This installs the package the way a consumer does.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.  The project uses a constant, a method with tokens, `SqlSourceGeneratorParameters` and `SqlSourceDialect`, each as a property and as metadata of an item, `Parameters` on the attribute, and the two markers `-- generator:` and `-- dialect:`, so it fails when the generator or either MSBuild file does not reach a consumer.  Its `Directory.Build.targets` sets one of the two properties and has a target that adds a `.sql` file, each with its value on a line of its own, so it also fails when the package trims a value only where its targets are imported.  Two of its lists of generator parameters have one word on each line, with the word that shows as the second, and one item names its dialect on one line and an option of the dialect on the next, so it fails when a value reaches the compiler cut at a line break.  It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.  Without an argument the script packs first, as the other does.
+This installs the package the way a consumer does.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.  The project uses a constant, a method with tokens, `SqlSourceGeneratorParameters` and `SqlSourceDialect`, each as a property and as metadata of an item, `Parameters` on the attribute, and the two markers `-- generator:` and `-- dialect:`, so it fails when the generator or either MSBuild file does not reach a consumer.  Its `Directory.Build.targets` sets one of the two properties and has a target that adds a `.sql` file, each with its value on a line of its own, so it also fails when the package trims a value only where its targets are imported.  Two of its lists of generator parameters have one word on each line, with the word that shows as the second, and one item names its dialect on one line and an option of the dialect on the next, so it fails when a value reaches the compiler cut at a line break.  It then asks the project what the `sqlsource` tool asks one: `SqlSourceImported` must be `true`, which shows that NuGet imported the package's props, and the target `SqlSourceWriteManifest`, run by name, must write a manifest that starts with its version, lists a `.sql` file, holds a dialect and a property that the project writes over several lines, trimmed, and does not list the file that `Directory.Build.targets` adds from a hook of the build.  It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.  Without an argument the script packs first, as the other does.
 
 A change to what the package gives a consumer through its MSBuild files adds a line to `tools/package-install/Program.cs` and to the expected output.
 
````

- [ ] **Step 4: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Run the manifest target on the installed package

tools/check-package-install.sh asks the installed project what the tool
asks one: SqlSourceImported, which shows NuGet importing the props, and the
manifest, which must hold trimmed values and must not list the file that a
hook of the build adds.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 8: The readme of the tool, and the sub-phase is done

**Files:**
- Modify: `src/SqlSource.Tool/README.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: Say what this version of the tool does**

Change `src/SqlSource.Tool/README.md`:

````diff
--- a/src/SqlSource.Tool/README.md
+++ b/src/SqlSource.Tool/README.md
@@ -2,4 +2,13 @@
 
 The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.
 
-This version finds the project or the solution to run on and describes nothing yet.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
+This version finds the projects to run on and reads what the compiler is given for each.  It describes nothing yet.
+
+```console
+$ dotnet sqlsource describe
+$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
+```
+
+`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on.
+
+The tool asks MSBuild about each project, so it needs the .NET SDK, and a project must have been restored: the tool does not restore, and reports a project that was not.  The SqlSource package of a project and the tool should be of one version.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
````

This file is the readme of the package on nuget.org, so its links stay absolute URLs.  `README.md` at the root does not change: the tool is documented there with phase 5.

- [ ] **Step 2: Set the row of 2.3 to Done**

Change `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

```diff
--- a/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
+++ b/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
@@ -64,7 +64,7 @@ Decided:
 | 1. Parameters and settings | Done | [query-generation-phase-1-parameters-and-settings-design](2026-10-08-query-generation-phase-1-parameters-and-settings-design.md) | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL; every new marker, setting, enum and generator parameter of the epic is parsed and validated with no effect yet.  A user sees four changes: `SqlSourceTokenValidation` gives way to `SqlSourceGeneratorParameters`; `token-validation` and `token-ignore=` are no longer generator parameters, and `-- token-ignore:` is a marker; a query's generator parameters replace the preamble's; and `{{a:b}}` is a token with a default, where it was text. |
 | 2.1 The snapshot format | Done | [query-generation-phase-2-1-snapshot-format-design](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | In the generator assembly: the model of a sidecar, the hand-written reader and writer, the two comparisons the format design defines, and the JSON Schema under `schemas/`.  No tool, and no step of the generator reads a sidecar yet. |
 | 2.2 The tool's shell and its package | Done | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
-| 2.3 The project manifest and discovery | In progress | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
+| 2.3 The project manifest and discovery | Done | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
 | 2.4 The work list | Not started | [query-generation-phase-2-4-work-list-design](2026-10-09-query-generation-phase-2-4-work-list-design.md) | The syntax-level attribute reader; the plan of a run from the shared path resolver, parser, settings and hash: which queries need an entry, their databases, and which are selected by `--database` and a `.sql` path. |
 | 2.5 Describe | Not started | [query-generation-phase-2-5-describe-design](2026-10-09-query-generation-phase-2-5-describe-design.md) | The describer's interfaces and registry with no engine registered; the exchange's interface with its live shape; connections; the skip rule, the sidecar-written rule, `--force`, and the summary. |
 | 2.6 `--check` and logging | Not started | [query-generation-phase-2-6-check-and-logging-design](2026-10-09-query-generation-phase-2-6-check-and-logging-design.md) | `describe --check` with its comparison and its exit code; `--verbose`; `--log` and `SQLSOURCE_LOG` with the run's events.  Closes phase 2. |
```

- [ ] **Step 3: Read the documents against the code**

Each task brought the documents it made wrong.  Check that none was missed:

```bash
grep -rn "SQLSRC222\` are assigned\|finds the project or the solution to run on" --include='*.md' . | grep -v docs/superpowers
```

Expected: no output.

```bash
grep -c "^| \[TD-00" docs/tech-debt/README.md
```

Expected: `16`, and `Next id` in that file is `TD-0029`.

- [ ] **Step 4: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as when this plan was written.  If a tag is printed and `VersionPrefix` in `Directory.Build.props` is not greater than it, stop and ask the owner for the new version.

- [ ] **Step 5: Format, validate and commit**

```bash
./format.sh
```

Read `git diff` for what the formatter changed: it should be nothing, since the code of this plan was formatted.

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A
```

```bash
git commit -q -F - <<'EOF'
Document the tool's projects and close phase 2.3

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 6: Hand over**

The branch holds eight commits over the spec's.  Use superpowers:finishing-a-development-branch.  The pull request says:

- What a user sees: nothing in the generator's package but one property and one target that no build runs; `sqlsource describe` reads its projects, takes `--project`, and reports `SQLSRC204` to `SQLSRC207`, `SQLSRC220` and `SQLSRC223`.
- No hot path of the generator changed, so there is no time or allocation to state.
- The four tech-debt items it adds, `TD-0025` to `TD-0028`, and the two it amends.
- That nothing ran on Windows (`TD-0028`).

Reply to every CodeRabbit finding in its thread, as `AGENTS.md` requires.
