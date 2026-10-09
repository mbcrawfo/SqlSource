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
DefineConstants=TRACE;DEBUG;NET;NET10_0
Property.SqlSourceDialect=postgres
Property.SqlSourceDatabase=
File=/work/src/App/Queries/Users.sql
File.SqlSourceDialect=
File.SqlSourceDatabase=billing
Compile=/work/src/App/UserRepository.cs
```

The example is shortened: the file has a `Property.` line for each of the package's ten settings, and ten `File.` lines after each `File`.

Success is:

- For a project, or for each project of a solution that uses SqlSource, the tool holds a `ProjectManifest`: the project's `.sql` files with their trimmed metadata, its trimmed `SqlSource` properties, and its `Compile` files.
- What the manifest lists is what the compiler is given: a file that a target adds, a file removed with `Remove`, `SqlSourceIncludeFiles` set to `false` and a value written over several lines all show as they do in a build.
- A project that targets several frameworks gives one manifest.
- Nothing a build does changes: the new target runs only when it is asked for by name.

## Decisions

From the epic, unchanged: the manifest is written by a target of `SqlSource.targets` to a file under `obj`, after the package's trims, and carries a format version; a project uses SqlSource when `SqlSourceImported` is set, which the props do; in solution mode the projects that do not use SqlSource are left out, and in project mode such a project is an error; `--project` restricts a run; the tool has no configuration file of its own.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| A solution | One MSBuild process for each project, several at once | `-t:` on a solution fails for a project without the target, and MSBuild refuses `-getTargetResult` for a solution.  A generated traversal project would need both worked around. |
| Restore | The tool never restores | A restore is slow and writes to `obj`.  A package's props reach a project only after a restore, so a project that was never restored looks like one that does not use SqlSource, and the message of `SQLSRC204` says both. |

Settled in this spec, with what the spike behind it found:

| Decision | Choice | Why |
|----|----|----|
| The manifest's format | Lines of `key=value` | MSBuild cannot write a Windows path into JSON without escaping it in a target for each item.  A line needs no escaping but for a line break, which no path of a project holds. |
| Where the tool finds the file | `-getTargetResult:SqlSourceWriteManifest`, in the run that writes it | The target returns the file as an item, and MSBuild prints its full path as JSON.  The tool never works out where `obj` is. |
| Several target frameworks | The first of `TargetFrameworks`, passed as `-p:TargetFramework=` | Run by name on such a project, the target runs in the outer build with no framework and writes a manifest without the framework's constants.  `AdditionalFiles` almost never differ by framework. |
| How a solution is read | `Microsoft.VisualStudio.SolutionPersistence` | It reads `.sln` and `.slnx` and is what the `dotnet` command uses.  The output of `dotnet sln list` is translated. |
| The configuration | The project's default.  No `--configuration` | A file or a constant that depends on the configuration is rare, and an option can be added without breaking anything. |

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

- It hooks nothing: no `BeforeTargets`, no `AfterTargets`.  It has `DependsOnTargets="SqlSourceTrimProperties;SqlSourceTrimMetadataOfFiles"`, so that it writes trimmed values, and so that a target of a project that hooks `SqlSourceTrimMetadataOfFiles` to add a `.sql` file, as the comment in the file tells it to, has run.
- It writes `$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest` with `WriteLinesToFile`, `Overwrite` and `WriteOnlyWhenDifferent`, and adds the file to `FileWrites`.
- It returns the file as an item, with `Returns`.
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

- A reader ignores a key it does not know.  A new key, and a new name under `Property.` or `File.`, are additions and do not change the version.
- A change to what an existing key means, or to how a line is read, raises `SqlSourceManifest`.  A reader that finds another version reads nothing more.
- A path is as MSBuild's `FullPath` gives it, which is the path the compiler is given.
- A value that holds a line break cannot be written.  No trimmed value holds one; a path that does is not supported.

The spike found two things the target must handle, and the plan proves both with the fixture projects below.  A property whose value holds `;`, as `DefineConstants` does, is split into several lines unless it is escaped with `$([MSBuild]::Escape(...))`.  And several lines for one item can be written from one item transform with `%0a` between them, which keeps a path that holds `;` whole.  If a character of a path or of a value does not survive, the pull request says which and records it in `docs/tech-debt`.

## The tool's side

In `src/SqlSource.Tool/Projects/`.

### Running MSBuild

`ToolHost` gains `Processes`, an `IProcessRunner`: one method that runs a program with a list of arguments in a working directory and returns its exit code and its two outputs.  The real one uses `System.Diagnostics.Process` with `ArgumentList`.  The tests give a fake.

The program is `dotnet`: the value of `DOTNET_HOST_PATH` when the host has it, and `dotnet` on the path otherwise.  The working directory is the project's folder, so that the project's own `global.json` picks the SDK.

For each project, two runs:

1. **Evaluate.**  `dotnet msbuild <project> -nologo -getProperty:SqlSourceImported -getProperty:TargetFramework -getProperty:TargetFrameworks`.  MSBuild prints the three as JSON.
2. **Write.**  Only when `SqlSourceImported` is `true`: `dotnet msbuild <project> -nologo -t:SqlSourceWriteManifest -getTargetResult:SqlSourceWriteManifest`, with `-p:TargetFramework=<first>` when `TargetFramework` is empty and `TargetFrameworks` is not.  The manifest is the `FullPath` of the one item of the result.

A run that exits with anything but `0`, or whose output is not the JSON expected, is `SQLSRC205` at the project file, with the first twenty lines of MSBuild's output as continuation lines labelled `msbuild`.

Projects are evaluated at most eight at a time, and never more than the machine has processors.  Their errors are reported in the order of the projects, not of completion.

### The projects of a run

| Unit | Projects |
|----|----|
| A project | That project.  It must use SqlSource: `SQLSRC204` otherwise |
| A solution | Its `.csproj` projects, in ordinal order of their full paths.  One that does not use SqlSource is left out, and nothing is said |

`--project <path>`, which may be given several times, restricts a run:

- In a solution, the run is the named projects.  Each must be a project of the solution, `SQLSRC207` otherwise, and must use SqlSource, `SQLSRC204` otherwise.
- With a project as the unit, each named path must be the unit: `SQLSRC207` otherwise.
- Paths are resolved against the working directory and compared as full paths, ignoring case.

A project with an error is left out and the run goes on with the rest; the exit code is `1`.  A run with no project left and no error prints `sqlsource: no project of '<unit>' uses SqlSource` on standard output and exits `0`.

### Reading the manifest

`ManifestReader.Read(text)` gives a `ProjectManifest` or the reason it could not: the first line is not `SqlSourceManifest=1`, a line has no `=`, a `File.` line comes before any `File` line, or `Project` is missing.  Each is `SQLSRC206` at the manifest file.  A version other than `1` says so, and that the package and the tool are out of step.  A line may end with `\n` or `\r\n`, and a byte order mark is ignored.

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
| `SQLSRC204` | Project does not use SqlSource | `'{0}' does not use SqlSource, or its packages have not been restored` | The project file |
| `SQLSRC205` | Project could not be evaluated | `MSBuild could not evaluate '{0}'` | The project file |
| `SQLSRC206` | Project manifest cannot be read | `The project manifest '{0}' cannot be read: {1}` | The manifest file; what is wrong |
| `SQLSRC207` | Project is not in the run | `'{0}' is not a project of '{1}'` | The path given; the unit |

`SQLSRC204` has a `help:` line: `run 'dotnet restore', or add the SqlSource package to the project`.

### `describe` after this sub-phase

It finds the unit, finds the projects, has each manifest written and reads it, reports what failed, and exits.  It prints nothing for a project that worked.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.3 says In progress.
2. `SqlSourceImported` in the props, with its test in `BuildFileTests`.
3. `SqlSourceWriteManifest` in the targets, with `BuildFileTests` and the fixture projects that prove what it writes.
4. `ManifestReader` and `ProjectManifest`, with `SQLSRC206`.
5. `IProcessRunner`, the two runs and `SQLSRC205`.
6. The projects of a run: a project, a solution, `--project`, `SQLSRC204` and `SQLSRC207`.
7. `tools/check-package-install.sh` runs the target on the installed project.
8. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `tests/SqlSource.Tests/Package/BuildFileTests.cs` | The props set `SqlSourceImported` with no condition.  The test that every target hooks `GenerateMSBuildEditorConfigFileCore` holds for every target but `SqlSourceWriteManifest`, which hooks nothing and depends on the two trims.  The target writes a `Property.` line and a `File.` line for each name of `Settings` and `TrimmedOnly`.  `SdkProperties` gains `MSBuildProjectFullPath`, `TargetFramework`, `LangVersion` and `DefineConstants`, and every other property still starts with `SqlSource` |
| `ManifestReaderTests` | The example above, written out in full, member by member.  A value that holds `=`; an empty value; an unknown key; `\r\n`; a byte order mark.  Each reason of `SQLSRC206`, with its text.  A version of `2` |
| `ProjectEvaluatorTests`, with a fake runner | The arguments of each run, for one framework and for several.  `SqlSourceImported` empty.  A run that exits `1`; output that is not JSON; a result with no item: each is `SQLSRC205` with MSBuild's lines.  `DOTNET_HOST_PATH` set and not set.  The working directory |
| `SolutionReaderTests` | A `.sln` and a `.slnx` with the same projects give the same list.  A solution folder, a project in another language, a project in a folder above the solution |
| `RunProjectsTests`, with a fake runner | Each row of the table of projects.  `--project` once and twice, in a solution and with a project as the unit; a path outside the solution; a project that does not use SqlSource, named and not named.  One project fails and the rest are read.  No project uses SqlSource |
| `ManifestTargetTests`, with the real `dotnet msbuild` | The fixture projects below |
| `tools/check-package-install.sh` | After the build, the script runs the target on the installed project and checks the first line, a `File` line for the `.sql` file that the project's `Directory.Build.targets` adds, and a `File.` line whose value the project writes over several lines |

The fixture projects are under `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, left out of the test project's own `Compile` items and copied to its output.  A test copies one to a temporary folder outside the repository, with the props and targets from the test's output and the repository's `global.json`, so that nothing of the repository's own build applies, and runs the tool's two runs on it for real.

| Fixture | Shows |
|----|----|
| `Single` | One framework: the example of this spec, with a property and a metadata written over several lines |
| `Multi` | Two frameworks: one manifest, for the first, with that framework's constants |
| `Plain` | No SqlSource: `SqlSourceImported` is empty |
| `LateFile` | A `Directory.Build.targets` whose target hooks `SqlSourceTrimMetadataOfFiles` and adds a `.sql` file with metadata: the file is listed, trimmed |
| `OwnFiles` | `SqlSourceIncludeFiles` is `false` and the project lists two of its three `.sql` files: two are listed |
| `OddPaths` | `.sql` and `.cs` files whose names hold `;`, `=`, `%`, `'`, a space and a letter outside ASCII: each is listed whole |

One more test runs the two runs on `tests/SqlSource.Tests/SqlSource.Tests.csproj` where it is, and finds the files of `EndToEnd/` with the dialects that project gives them, as the epic asks.

These tests start MSBuild, so each takes a second or two.  They run with the rest.

## Documentation

- `CONTRIBUTING.md`: the fixture projects and that their tests run `dotnet msbuild`; what `tools/check-package-install.sh` now checks.
- `src/SqlSource/AGENTS.md`: the manifest target hooks nothing on purpose; its format is a contract with the tool, by the rules above; a new setting of the package is a new name in the target.
- `src/SqlSource.Tool/AGENTS.md`: every process goes through `IProcessRunner`; the tool never restores and never builds; MSBuild's output is parsed only as the JSON of `-getProperty` and `-getTargetResult`.
- `docs/diagnostics.md`: `SQLSRC204` to `SQLSRC207`.
- `README.md`: nothing in it changes.  `SqlSourceImported` and the target are for the tool, and the tool is documented with phase 5.
- `docs/tech-debt`: an item for any character the manifest cannot carry, if the fixtures find one.
- The epic outline: in steps 1 and 8.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
