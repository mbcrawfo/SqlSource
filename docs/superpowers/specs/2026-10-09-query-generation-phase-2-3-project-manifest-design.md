# Query generation, phase 2.3: the project manifest and discovery - design

Date: 2026-10-09

Sub-phase 2.3 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers how the tool learns what the compiler sees: a file that the package's targets write for each project, and the tool's side that finds the projects of a run, has MSBuild write that file, and reads it.

It builds on sub-phase 2.2.  Sub-phase 2.4 builds on it.

## Goal

```console
$ dotnet msbuild App.csproj -t:SqlSourceWriteManifest
$ cat obj/Debug/net10.0/App.csproj.SqlSource.manifest
SqlSourceManifest=1
Project=/work/src/App/App.csproj
TargetFramework=net10.0
LangVersion=14.0
DefineConstants=TRACE;DEBUG;NET;NET10_0;NET8_0_OR_GREATER
Property.SqlSourceDialect=postgres
Property.SqlSourceDatabase=
File=/work/src/App/Queries/Users.sql
File.SqlSourceDialect=
File.SqlSourceDatabase=billing
Compile=/work/src/App/UserRepository.cs
```

The example is shortened: the file has a `Property.` line for each of the package's ten settings, ten `File.` lines after each `File`, and every constant the framework defines.

Success is:

- For a project, or for each project of a solution that uses SqlSource, the tool holds a `ProjectManifest`: the project's `.sql` files with their trimmed metadata, its trimmed `SqlSource` properties, its `Compile` files, and the constants its code is compiled with.
- What the manifest lists is what the project file says: a file removed with `Remove`, `SqlSourceIncludeFiles` set to `false` and a value written over several lines all show as they do in a build.  A file that a target adds is listed when the target hooks `SqlSourceTrimMetadataOfFiles`, and not otherwise: see What the manifest does not see.
- A project that targets several frameworks gives one manifest, for the first of them.
- A project that was never restored, or not since SqlSource was added to it, is an error, in a solution as well as alone, so that a run on a fresh checkout does not pass by finding nothing.
- Nothing a build does changes: the new target runs only when it is asked for by name.

## Decisions

From the epic, unchanged: the manifest is written by a target of `SqlSource.targets`, after the package's trims, and carries a format version; a project uses SqlSource when `SqlSourceImported` is set, which the props do; in solution mode the projects that do not use SqlSource are left out, and in project mode such a project is an error; `--project` restricts a run; the tool has no configuration file of its own.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| A solution | One MSBuild process for each project, several at once | `-t:` on a solution fails for a project without the target, and MSBuild refuses its `-get` switches for a solution.  A generated traversal project would need both worked around. |
| Restore | The tool never restores | A restore is slow and writes to `obj`.  A package's props reach a project only after a restore, so the tool tells a project that was never restored apart and reports it, `SQLSRC220`. |
| A file that a target adds | Listed only when the target hooks `SqlSourceTrimMetadataOfFiles` | A target that hooks `BeforeBuild` does not run when the manifest target is asked for by name.  Depending on `BeforeBuild` would run whatever a project hangs on it, a code generator or a package install, on every `describe`. |

Settled in this spec, with what the spikes behind it found:

| Decision | Choice | Why |
|----|----|----|
| The manifest's format | Lines of `key=value` | MSBuild cannot write a Windows path into JSON without escaping it in a target for each item.  A line needs no escaping but for a line break, which no path of a project holds. |
| Where the file is written | Where `SqlSourceManifestFile` says; under `obj` when it is not set.  The tool sets it, to a file in a temporary folder of its own | The tool then reads a path it chose, parses nothing MSBuild prints for it, and writes nothing into the project. |
| The framework's constants | The target depends on the SDK's `AddImplicitDefineConstants` when the project has a `TargetFramework` | Run by name, a project has `TRACE;DEBUG` alone: the SDK adds `NET10_0`, `NET8_0_OR_GREATER` and the rest in that target, which hooks the compile.  Without them an attribute under `#if NET8_0_OR_GREATER` is not found.  The name is the SDK's own, which `docs/tech-debt` records. |
| Several target frameworks | The first of `TargetFrameworks`, passed as `-p:TargetFramework=`, and that one alone | Run by name on such a project, the target runs in the outer build with no framework.  `AdditionalFiles` almost never differ by framework, and a manifest for each framework would multiply every run for the project that has none that do.  What the others would add is not seen, which `docs/tech-debt` records. |
| How a solution is read | `Microsoft.VisualStudio.SolutionPersistence` | It reads `.sln` and `.slnx` and is what the `dotnet` command uses.  The output of `dotnet sln list` is translated. |
| The configuration | The project's default.  No `--configuration` | A file or a constant that depends on the configuration is rare, and an option can be added without breaking anything. |
| A value the tool gives MSBuild as a property | Escaped as MSBuild escapes: `%` as `%25`, `;` as `%3B`, `,` as `%2C` | MSBuild splits the value of `-p:` at `;` and at `,`: `-p:SolutionDir=/work/Acme, Inc/` is `MSB1006`.  The review of this spec ran it, and the escaped form arrives whole. |
| A solution that cannot be read | `SQLSRC223`, an error of its own | The user wrote the file, so that it cannot be read is theirs to mend and no failure of the tool, as `SQLSRC222` is for a directory. |
| A project that NuGet writes no assets file for | Does not use SqlSource | A project whose `ProjectAssetsFile` is empty, one with `packages.config`, has no restore to tell by.  Read as not restored, it would be `SQLSRC220` in every run. |

## Out of scope

- Reading the `Compile` files and the `.sql` files.  Sub-phase 2.4.
- `--manifest`, with which a build hands the tool the file it already wrote, and a hook of the target into the build.  Phase 9.
- `.slnf` files, and projects in other languages: the generator is a C# generator.
- A project manifest for each target framework.

## The package's side

### `SqlSource.props`

An unconditional `PropertyGroup` sets `SqlSourceImported` to `true`.  It is the one thing that says a project uses SqlSource, whether the package came from NuGet or, as in this repository's tests, its files are imported by path.

### `SqlSource.targets`

A target, `SqlSourceWriteManifest`:

- It hooks nothing: no `BeforeTargets`, no `AfterTargets`.
- Its `DependsOnTargets` is a property, `SqlSourceManifestDependsOn`, set beside it: `SqlSourceTrimProperties` and `SqlSourceTrimMetadataOfFiles`, and `AddImplicitDefineConstants` as well when `TargetFramework` is not empty.  The trims make it write trimmed values, and make a target of a project that hooks `SqlSourceTrimMetadataOfFiles` to add a `.sql` file run first.
- That property is set in a `PropertyGroup` at the root of the file, since a target's own `DependsOnTargets` is read before the target runs.  It is the one element of the file that is not a target, and its second line holds the file's one condition.  `BuildFileTests` says so, and holds every other element to what it asserts today.
- It writes the file `SqlSourceManifestFile` names, which it sets to `$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest` when nothing set it, with `WriteLinesToFile`, `Overwrite` and `WriteOnlyWhenDifferent`.  It adds the file to `FileWrites` only when it is that default.
- Every property it sets starts with `SqlSource`, and so does every item type it makes.

### The format, version 1

A text file in UTF-8, one record on a line.  A line is a key, `=`, and a value: the key ends at the first `=`, and the value is the rest of the line as it is, which may hold `=` and may be empty.

| Key | Value | Written |
|----|----|----|
| `SqlSourceManifest` | The format's version, `1` | First, once |
| `Project` | The project file's full path | Once |
| `TargetFramework` | The framework the manifest was written for | Once |
| `LangVersion` | The project's `LangVersion` as MSBuild has it, which may be empty or a word such as `latest` | Once |
| `DefineConstants` | The project's constants, separated by `;` | Once |
| `Property.<Name>` | A property of the package, trimmed.  Empty when not set | Once for each of the ten |
| `File` | The full path of an `AdditionalFiles` item whose extension is `.sql` | Once for each, in item order |
| `File.<Name>` | Metadata of the file of the nearest `File` line above, trimmed.  Empty when not set | Once for each of the ten, after its `File` |
| `Compile` | The full path of a `Compile` item | Once for each |

The ten names are the settings the props list and `SqlSourceDatabase`: `SqlSourceDialect`, `SqlSourceDatabase`, `SqlSourceOutput`, `SqlSourceGeneratorParameters`, `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix`, `SqlSourceModelNamespace`, `SqlSourceInputModelType`, `SqlSourceOutputModelType` and `SqlSourceCollectionType`.  The tool reads three of them today; the rest are there so that the manifest is what the compiler sees, whole, and the tool can use the generator's readers as they are.

The rules of the contract:

- A reader ignores a key it does not know, and an empty line.  A new key, and a new name under `Property.` or `File.`, are additions and do not change the version.
- A change to what an existing key means, or to how a line is read, raises `SqlSourceManifest`.  A reader that finds another version reads nothing more.
- A path is as MSBuild's `FullPath` gives it, which is the path the compiler is given.
- A value that holds a line break cannot be written.  No trimmed value holds one; a path that does is not supported.

Two things the spikes found, which the target must do and the fixture projects below prove.  A property whose value holds `;`, as `DefineConstants` does, is split into several lines unless it is escaped with `$([MSBuild]::Escape(...))`.  And the lines of one item are written from one item transform with `%0a` between them, which kept a path that holds `;`, `=`, `%`, `'`, a space or a letter outside ASCII whole on macOS.  If a character does not survive on another system, the pull request says which and records it in `docs/tech-debt`.

### What the manifest does not see

Asked for by name, the target runs after the two trims and nothing else of a build.

- A `.sql` file that a target adds is in the manifest only when that target hooks `SqlSourceTrimMetadataOfFiles`, the hook the package's own comment names.  One that hooks `BeforeBuild`, or anything else, has not run.  From phase 5 the generator needs an entry for the queries of such a file, and the tool cannot write one until the file is in the manifest.
- `docs/tech-debt/TD-0016` says today that a target hooking `BeforeBuild` "is not affected".  That holds for the build and not for the tool, and this sub-phase amends the item to say so.
- A new tech-debt item records the limit and its fix: a target that adds a `.sql` file hooks `SqlSourceTrimMetadataOfFiles`.  Phase 9's run inside a build sees every file, since the build has run the hooks by then.
- For a project that targets several frameworks the manifest is the first one's, on purpose.  A `.sql` file or a `Compile` file that the project lists only under a condition on another framework, and an attribute under `#if` for a constant only another framework defines, are not in it, so the queries they bring are never described.  A project that references SqlSource for another framework alone does not use it at all, as the tool sees it.  The generator compiles for every framework, so from phase 5 the build of that other framework reports such a query as having no entry: the gap is loud, not silent.  A second tech-debt item records it, with its fix: a manifest for each framework, and the union of their needs.

## The tool's side

In `src/SqlSource.Tool/Projects/`.

### Running MSBuild

`ToolHost` gains `Processes`, an `IProcessRunner`: one method that runs a program with a list of arguments, a working directory, environment variables to set and to remove, and a cancellation token, and returns its exit code and its two outputs, decoded as UTF-8.  The real one uses `System.Diagnostics.Process` with `ArgumentList`, gives the process no input, reads both outputs while it runs, and kills the process and every process it started when the token is cancelled.  The tests give a fake.

The program is `dotnet`: the value of `DOTNET_HOST_PATH` when the host has it, and `dotnet` on the path otherwise.  The working directory is the project's folder, so that the project's own `global.json` picks the SDK.  Each run sets `DOTNET_CLI_FORCE_UTF8_ENCODING` to `true`, so that a path outside ASCII arrives whole on Windows; that it does is not verified, and the plan checks it or records the gap in `docs/tech-debt`.  Each run also sets `DOTNET_NOLOGO` to `true`, so that the first `dotnet` command on a machine prints no welcome before the JSON.

Each run removes `MSBuildSDKsPath` and `MSBuildExtensionsPath`.  A process that MSBuild starts has both, which the review of this spec saw.  The `dotnet` command is understood to use them in place of its own when they are set, so that a tool started from a build, in a project whose `global.json` picks another SDK, would load the targets of the wrong one: the plan checks that, and whether a test process under `dotnet test` has them.

Every value of a `-p:` switch is escaped as the table of decisions says.  The project file is an argument of its own and needs none.

For each project, two runs, and three for a project with several target frameworks:

1. **Evaluate.**  `dotnet msbuild <project> -nologo -getProperty:SqlSourceImported -getProperty:TargetFramework -getProperty:TargetFrameworks -getProperty:ProjectAssetsFile -getItem:PackageReference`.  MSBuild prints the four properties and the items as JSON.  When `TargetFramework` is empty and `TargetFrameworks` is not, this answer gives the first framework and nothing else, and the evaluation is run again with `-p:TargetFramework=<first>`: the table under Whether a project uses SqlSource reads the second answer.
2. **Write.**  Only when `SqlSourceImported` is `true`: `dotnet msbuild <project> -nologo -t:SqlSourceWriteManifest -p:SqlSourceManifestFile=<file>`, with `-p:TargetFramework=<first>` when `TargetFramework` is empty and `TargetFrameworks` is not.  The file is one the tool names, in a temporary folder it makes for the run and deletes after it.

In a solution, both runs also pass what `dotnet build` of that solution gives a project: `SolutionDir`, with its closing separator, `SolutionPath`, `SolutionName`, `SolutionFileName` and `SolutionExt`.  A project that imports a file through `$(SolutionDir)` then evaluates as it does in a build.

A run that exits with anything but `0`, an evaluation whose output is not the JSON expected, and a second run that leaves no file are `SQLSRC205` at the project file, with the first twenty lines that MSBuild wrote as continuation lines labelled `msbuild`: its error output first, where it writes the errors of an evaluation, then its standard output.  Empty lines are not counted.

The second evaluation is not a refinement.  For a project with several target frameworks, NuGet imports a package's props only under a condition on `TargetFramework`, which the review of this spec read in a restored project's `obj/*.nuget.g.props`.  Evaluated with no framework, such a project never has `SqlSourceImported`, and a `PackageReference` that the project lists under a condition on a framework is not among its items either.

Projects are evaluated at most eight at a time, and never more than the machine has processors.  Their errors are reported in the order of the projects, not of completion.  A run that is cancelled starts no further process, reports nothing for the ones it killed, and deletes its temporary folder.

### Whether a project uses SqlSource

The first row that holds:

| `SqlSourceImported` | A `PackageReference` named `SqlSource` | The file `ProjectAssetsFile` names | The project |
|----|----|----|----|
| `true` | | | Uses SqlSource |
| Anything else | Present | | Was not restored since the package was added: `SQLSRC220` |
| Anything else | Absent | `ProjectAssetsFile` is empty | Does not use SqlSource |
| Anything else | Absent | Does not exist | Was never restored: `SQLSRC220` |
| Anything else | Absent | Exists | Does not use SqlSource |

The second row is the project that gained its reference to SqlSource after its last restore: the assets file is there, and the package's props are not.  A `PackageReference` is an item of the project's evaluation, so it shows without a restore, and its name is compared ignoring case.  SqlSource is a development dependency and reaches no project through another, so a project with no such item and an assets file does not use it.

A project that takes SqlSource by path, as this repository's tests do, has `SqlSourceImported` whether it was restored or not.

The third row is a project that keeps its packages in `packages.config`: NuGet writes it no assets file, so nothing says whether it was restored.  Such a project imports a package's props by a line of its project file, so one that uses SqlSource has the marker.  The row is reasoned and was not run: this repository has no such project.

### The projects of a run

| Unit | Projects |
|----|----|
| A project | That project.  It must use SqlSource: `SQLSRC204` otherwise |
| A solution | Its `.csproj` projects, in ordinal order of their full paths.  One that does not use SqlSource is left out, and nothing is said |

`--project <path>`, which may be given several times, restricts a run:

- In a solution, the run is the named projects, and only they are evaluated.  Each must be a project of the solution, `SQLSRC207` otherwise, and must use SqlSource, `SQLSRC204` otherwise.
- With a project as the unit, each named path must be the unit: `SQLSRC207` otherwise.
- Paths are resolved against the working directory and compared as full paths, ignoring case.
- A project that is named twice is named once.

`SolutionReader` gives the projects of a solution.  A `.sln` writes a project's path with `\` on every system, so a path is resolved against the solution's folder with either separator.  A project that the solution lists twice is one project.  A solution that the library cannot read is `SQLSRC223` at the solution file, with the library's reason, and the run ends there.

A project with an error is left out and the run goes on with the rest; the exit code is `1`.  A run with no project left and no error prints `sqlsource: no project of '<unit>' uses SqlSource` on standard output and exits `0`.

### The command line

`describe` gains `--project <path>`.  It takes exactly one value each time it is given and may be given several times: an array with `ArgumentArity.OneOrMore`, as `src/SqlSource.Tool/AGENTS.md` asks.  It has an effect in this sub-phase, so `--help` lists it.

`UsageCheck` already reads an option that takes a value.  The tokens of `--project`, alone, with `=` and with a value after it, join the vocabulary of `Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine`.

A path that `--project` names is printed as a full path, in `SQLSRC204` and `SQLSRC207`, as the path of `describe` is.  So a secret typed after `--project` reaches the output, and `docs/tech-debt/TD-0023` is amended to name the option.  The third gap of `TD-0024` stays open: its fix waits for an option that System.CommandLine rejects when it is given twice, and `--project` may be.  The item is amended to say an option that is given once.

### Reading the manifest

`ManifestReader.Read(text)` gives a `ProjectManifest` or the reason it could not: the first line is not `SqlSourceManifest=1`, a line that is not empty has no `=`, a `File.` line comes before any `File` line, or `Project` is missing.  Each is `SQLSRC206` at the project file.  A version other than `1` says so, and that the package and the tool are out of step.  A line may end with `\n` or `\r\n`, and a byte order mark is ignored.

| `ProjectManifest` | Holds |
|----|----|
| `ProjectPath` | The value of `Project` |
| `TargetFramework`, `LangVersion` | As written |
| `DefineConstants` | The names, split at `;` and `,`, with empty ones dropped |
| `Properties` | The ten, by name; a value that is empty is absent |
| `Files` | For each `File`: its path and its metadata by name, empty values absent |
| `CompileFiles` | The paths |

### Diagnostics

In `ToolDiagnostics`, with the four places each needs.

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC204` | Project does not use SqlSource | `'{0}' does not use SqlSource` | The project file |
| `SQLSRC205` | Project could not be evaluated | `MSBuild could not evaluate '{0}'` | The project file |
| `SQLSRC206` | Project manifest cannot be read | `The project manifest of '{0}' cannot be read: {1}` | The project file; what is wrong |
| `SQLSRC207` | Project is not in the run | `'{0}' is not a project of '{1}'` | The full path of the path given; the unit |
| `SQLSRC220` | Project was not restored | `'{0}' has not been restored, or not since the SqlSource package was added to it` | The project file |
| `SQLSRC223` | Solution cannot be read | `'{0}' cannot be read: {1}` | The solution file; the library's reason |

`SQLSRC204` has a `help:` line, `add the SqlSource package to the project`, and `SQLSRC220` has one, `run 'dotnet restore'`.  `SQLSRC220` is out of the order of the others because it was added after `SQLSRC208` to `SQLSRC219` were given to the later sub-phases, and `SQLSRC223` because the review of this spec added it after `SQLSRC222`.  The epic outline's table of ids, and the range that `src/SqlSource/AGENTS.md` names, gain it.

### `describe` after this sub-phase

It finds the unit, finds the projects, has each manifest written and reads it, reports what failed, and exits.  It prints nothing for a project that worked.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.3 says In progress.
2. `SqlSourceImported` in the props, with its test in `BuildFileTests`.
3. `SqlSourceWriteManifest` in the targets, with `BuildFileTests` and the fixture projects that prove what it writes.  The three tech-debt items, and `TD-0016` amended.
4. `ManifestReader` and `ProjectManifest`, with `SQLSRC206`.
5. `IProcessRunner`, the two runs and `SQLSRC205`.
6. The projects of a run: a project, a solution, `--project` and its command line, `SQLSRC204`, `SQLSRC207`, `SQLSRC220` and `SQLSRC223`.  `TD-0023` and `TD-0024` amended.
7. `tools/check-package-install.sh` runs the target on the installed project.
8. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `tests/SqlSource.Tests/Package/BuildFileTests.cs` | The props set `SqlSourceImported` with no condition.  The test that every target hooks `GenerateMSBuildEditorConfigFileCore` holds for every target but `SqlSourceWriteManifest`, which hooks nothing and depends on the two trims, and on `AddImplicitDefineConstants` under the one condition.  The target writes a `Property.` line and a `File.` line for each name of `Settings` and `TrimmedOnly`.  `SdkProperties` gains `MSBuildProjectFullPath`, `TargetFramework`, `LangVersion` and `DefineConstants`, and every other property still starts with `SqlSource`.  The root of the targets holds its targets and one `PropertyGroup`, which sets `SqlSourceManifestDependsOn` alone, twice, with the one condition on its second line |
| `ManifestReaderTests` | The example above, written out in full, member by member.  A value that holds `=`; an empty value; an unknown key; an empty line; `\r\n`; a byte order mark.  Each reason of `SQLSRC206`, with its text.  A version of `2` |
| `ProjectEvaluatorTests`, with a fake runner | The arguments of each run, for one framework and for several, alone and in a solution.  For several, the table is applied to the second evaluation and not the first.  Each row of the table of whether a project uses SqlSource.  A run that exits `1`; output that is not JSON; no file after the second run: each is `SQLSRC205` with MSBuild's lines.  `DOTNET_HOST_PATH` set and not set.  The working directory, the variables set and the variables removed.  A solution's folder and a temporary folder whose names hold `;`, `,` and `%` are escaped in each `-p:`.  MSBuild's lines: the error output before the standard output, twenty at most, empty ones left out.  A cancelled run starts no further process.  The temporary folder is gone after the run, after a run that failed and after one that was cancelled |
| `ProcessRunnerTests`, with the real `dotnet` | `dotnet --version` gives exit code `0` and a line of output.  A variable set reaches the process and a variable removed does not.  A process that is cancelled is killed |
| `SolutionReaderTests` | A `.sln` and a `.slnx` with the same projects give the same list.  A solution folder, a project in another language, a project in a folder above the solution.  A `.sln` whose paths have `\`.  A project listed twice.  A file that is no solution: `SQLSRC223` with the reason |
| `RunProjectsTests`, with a fake runner | Each row of the table of projects.  `--project` once and twice, in a solution and with a project as the unit; a path outside the solution; a project that does not use SqlSource, named and not named; a project that was not restored, in a solution and alone.  A project that `--project` does not name is not evaluated.  One project fails and the rest are read.  No project uses SqlSource.  One project named twice.  A solution that cannot be read ends the run |
| `UsageCheckTests` and `DescribeTests` | `--help` of `describe` lists `--project`.  `--project` with no value, and `--project=`, say that it needs a value and print nothing else of the command line.  The vocabulary of `Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine` holds its tokens |
| `ManifestTargetTests`, with the real `dotnet msbuild` | The fixture projects below |
| `tools/check-package-install.sh` | After the build, the script runs the target on the installed project and checks the first line, a `File` line for a `.sql` file of the project, a `File.` line whose value the project writes over several lines, and that the file its `Directory.Build.targets` adds from a `BeforeBuild` hook is not listed |

The fixture projects are under `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, left out of the test project's own `Compile` items and copied to its output.  A test copies one to a temporary folder outside the repository, with the props and targets from the test's output and the repository's `global.json`, so that nothing of the repository's own build applies, and runs the tool's two runs on it for real.

| Fixture | Shows |
|----|----|
| `Single` | One framework: the example of this spec, with a property and a metadata written over several lines, and `NET8_0_OR_GREATER` among its constants |
| `Multi` | Two frameworks, with the package's files imported under a condition on `TargetFramework`, as NuGet imports them: evaluated with no framework the project has no `SqlSourceImported`, and the tool still finds that it uses SqlSource.  One manifest, for the first, with that framework's constants.  A `.sql` file and a `Compile` file that the project lists only for the second are not in it: the test pins the limit, and names the tech-debt item |
| `SecondOnly` | Two frameworks, and SqlSource for the second alone: the project does not use SqlSource as the tool sees it.  The test pins the limit, and names the tech-debt item |
| `Plain` | No SqlSource: `SqlSourceImported` is empty.  Restored, it does not use SqlSource; with no `obj`, it was not restored |
| `StaleRestore` | A `PackageReference` to SqlSource, no props of the package, and an assets file the test puts there: `SQLSRC220` |
| `LateFile` | A `Directory.Build.targets` whose target hooks `SqlSourceTrimMetadataOfFiles` and adds a `.sql` file with metadata: the file is listed, trimmed |
| `BuildHookFile` | A target that hooks `BeforeBuild` and adds a `.sql` file: the file is not listed.  The test pins the limit, and names the tech-debt item |
| `OwnFiles` | `SqlSourceIncludeFiles` is `false` and the project lists two of its three `.sql` files: two are listed |
| `OddPaths` | `.sql` and `.cs` files whose names hold `;`, `=`, `%`, `'`, a space and a letter outside ASCII, in a folder whose name does too: each is listed whole |
| `InSolution` | A project that imports a file through `$(SolutionDir)`, in a solution whose folder's name holds `,` and `;`: it evaluates in a solution |

No fixture leaves an `obj` folder behind in a project the tool ran on.

One more test runs the two runs on `tests/SqlSource.Tests/SqlSource.Tests.csproj` where it is, and finds the files of `EndToEnd/` with the dialects that project gives them, as the epic asks.

These tests start MSBuild, so each takes a second or two.  They run with the rest.

## Documentation

- `CONTRIBUTING.md`: the fixture projects and that their tests run `dotnet msbuild`; what `tools/check-package-install.sh` now checks.
- `src/SqlSource/AGENTS.md`: the manifest target hooks nothing on purpose; its format is a contract with the tool, by the rules above; a new setting of the package is a new name in the target; it depends on a target of the SDK by name.
- `src/SqlSource.Tool/AGENTS.md`: every process goes through `IProcessRunner`; the tool never restores and never builds; MSBuild's output is parsed only as the JSON of `-getProperty`; the tool writes nothing into a project.
- `src/SqlSource/AGENTS.md`, under Diagnostics: the ids that the outline assigns end at `SQLSRC223`.
- `docs/diagnostics.md`: `SQLSRC204` to `SQLSRC207`, `SQLSRC220` and `SQLSRC223`.
- `Directory.Packages.props` gains `Microsoft.VisualStudio.SolutionPersistence`, the lock files of the tool and of its tests change with it, and `tools/check-package.sh` requires its DLL in the tool's package.
- `src/SqlSource.Tool/README.md`: what this version does, and that a project is restored before the tool is run.
- `docs/tech-debt`: three items, each with the next free id.  The manifest does not hold a `.sql` file that a target adds, unless the target hooks `SqlSourceTrimMetadataOfFiles`.  The manifest of a project with several target frameworks is the first one's alone, and so is the answer to whether it uses SqlSource.  And the manifest target depends on `AddImplicitDefineConstants`, a target of the SDK whose name is not a contract; the fix is for the tool to work the constants out from `TargetFramework`.  `TD-0016` is amended as above, and `TD-0023` and `TD-0024` as under The command line.  One more item for any character the manifest cannot carry, if the fixtures find one.
- `README.md`: nothing in it changes.  `SqlSourceImported` and the target are for the tool, and the tool is documented with phase 5, which also says which hook a target that adds a `.sql` file uses.
- The epic outline: in steps 1 and 8.  The commit that amended this spec after its review gave the outline `SQLSRC223` and the row of a project with no assets file.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
