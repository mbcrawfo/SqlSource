# Query generation, phase 2.2: the tool's shell and its package - design

Date: 2026-10-09

Sub-phase 2.2 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers the `sqlsource` command as a program that starts, reports errors, finds what to run on, and is packed, checked and published beside the generator.  It describes nothing: that arrives in sub-phases 2.3 to 2.6, each of which adds a stage to the command this one creates.

It depends on no other sub-phase.  Sub-phase 2.3 builds on it.

## Goal

```console
$ dotnet sqlsource --version
0.1.0-dev+3f2a9c1

$ dotnet sqlsource describe
sqlsource : error SQLSRC201: '/work/empty' holds no .sln, .slnx or .csproj file
    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc201

$ dotnet sqlsource describe src/App/App.csproj
$ echo $?
0
```

Success is:

- `dotnet pack SqlSource.slnx` writes two packages, `SqlSource` and `SqlSource.Tool`, with one version.
- The packed tool installs into an empty folder and runs on a machine that has only the repository's SDK.
- `Cli.Run(args)` runs the whole command in process, and a test can give it its own output, working directory and environment.
- Every error the tool prints goes through one reporter, in the compiler's format, with an id that `docs/diagnostics.md` explains.
- The generator's package is byte for byte what it was, apart from the new descriptors in its assembly.

## Decisions

From the epic, unchanged: a console application in `src/SqlSource.Tool`, packed with `PackAsTool` under the command name `sqlsource` and the package id `SqlSource.Tool`; `net8.0` with `RollForward` set to `Major`; a project reference to the generator, which shares its internals; System.CommandLine; errors in the compiler's format; the tool's descriptors in the generator assembly; both packages published together with one `VersionPrefix`; `tests/SqlSource.Tool.Tests`.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| The tool's Roslyn | A current `Microsoft.CodeAnalysis.CSharp`, by `VersionOverride`, as `tests/SqlSource.Tests` has it | Sub-phase 2.4 parses a consumer's C# to find attributes.  The generator's pin, 4.8.0, reads C# 12 at most.  The generator's own reference is untouched. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Exit codes | `0` success; `1` an error was reported, a wrong command line included; `2` `--check` found a difference and nothing failed | The epic's recommendation.  A build needs to tell "failed" from "differs", and nothing else. |
| An exception | Caught in `Cli`, reported as `SQLSRC200` with its type and message, exit `1`.  The stack trace is printed when `SQLSOURCE_DEBUG` is set | A stack trace as the tool's last word reads as a crash of the user's build. |
| Where errors go | The standard error stream.  Everything else goes to standard output | A caller can keep the two apart, and MSBuild reads both. |
| Paths in a message | Full paths | An IDE's error list resolves them from anywhere, and a path relative to the working directory is wrong inside a build. |
| The `see:` line | Every error ends with one, the descriptor's help link | The id's section is where the fix is explained. |
| A wrong command line | Reported by the tool, not by System.CommandLine: an unknown option by its name alone, and any other token that is not expected by its position, never by its text | Sub-phase 2.5 adds an option whose value is a secret.  A misspelt `--conection` followed by a connection string must not print the string, and System.CommandLine's own messages repeat the token they reject. |
| Options | Each sub-phase adds the options it gives an effect.  This one has `--help` and `--version` | `--help` never lists an option that does nothing. |
| Where the 2xx descriptors live | `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, with an `All` of its own | `SqlDiagnostics.cs` is the generator's and is long already.  The document test and the release tracking cover both. |
| The packed tool's check | A new script, `tools/check-tool-install.sh`, installs the packed tool into a temporary folder and runs it | A tool carries its whole dependency closure.  Only running the packed tool shows that the closure loads and that the roll-forward works. |

## Out of scope

- Reading a project: the manifest, a solution's projects, `--project`.  Sub-phase 2.3.
- Everything about queries, databases, connections, sidecars and the log.
- The `README.md` of the repository.  It waits for phase 5, when a user can do something with the tool.
- Pinning `sqlsource` in the repository's own `dotnet-tools.json`.  The end-to-end projects of phase 5 use the tool as a project.
- The packed tool inside `tools/check-package-install.sh`.  Phase 9, as the epic says.

## The command line

```
sqlsource [--version] [--help]
sqlsource describe [<path>] [--help]
```

- The root command with no subcommand prints the help and exits `0`.  System.CommandLine's default is an error, "Required command was not provided", so the root has an action of its own.
- `--version` prints the informational version of the tool's assembly.
- `describe` takes at most one `<path>` in this sub-phase.  Sub-phase 2.4 lets it take more.
- **A wrong command line** exits `1` with one line on standard error for each thing wrong, and nothing on standard output: System.CommandLine's default, which also prints the help, is replaced.  The tool writes the lines itself:
  - A token that starts with `-` and is no option of the command is `sqlsource: unknown option '<name>'`, where the name is the token cut at its first `=` or `:`.  This holds for a token where a `<path>` could stand, so that a misspelt option is never taken for a path.  When there is one, nothing else of the command line is looked at.
  - Any other token that is not expected is `sqlsource: unexpected argument at position <n>`.
  - An option with no value, or with one it cannot take, is named by the option alone.

### The unit of a run

`describe` first finds the unit: one solution or one project.

| Argument | The unit |
|----|----|
| None | The one `.sln`, `.slnx` or `.csproj` file in the working directory |
| A directory | The one such file in that directory |
| A `.sln`, `.slnx` or `.csproj` file | That file |

- A relative path is resolved against the working directory of the host.  Extensions are compared ignoring case.
- A directory that holds none is `SQLSRC201`; one that holds more than one, of any of the three kinds, is `SQLSRC202`, as `dotnet build` refuses such a directory.
- A path that does not exist, or a file with any other extension, a `.slnf`, an `.fsproj`, is `SQLSRC203`.

The unit is a `RunUnit`: its kind, `Solution` or `Project`, and its full path.  In this sub-phase `describe` stops there and exits `0`.

## Code

### `src/SqlSource.Tool`

```
Program.cs            Main, which calls Cli.Run
Cli.cs                The public entry, the commands and the catch of an exception
ToolHost.cs           What the tool takes from outside
Reporting/            ToolDiagnostic and Reporter
Projects/             RunUnit and its finder
```

- **`Cli`** is `public static class Cli`, public because the end-to-end projects of phase 5 call it, with `public static int Run(string[] args)`, which runs with the real host, and `internal static Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)`, which the tests call.  `Run` cancels on Ctrl+C; a cancelled run prints nothing more and exits `1`.
- **`ToolHost`** is a record of what the tool does not own: `Out` and `Error`, two `TextWriter`s; `WorkingDirectory`; and `GetEnvironmentVariable`, a function from a name to a value or null.  `ToolHost.Create()` gives the real one.  Later sub-phases add members: the process runner in 2.3, the describers in 2.5.  Nothing in the tool reads `Console`, `Environment` or the current directory except through it.
- **`ToolDiagnostic`** is one error as data: a descriptor, a path or null, a line and column or null, the arguments of the message, and continuation lines, each a label and a text.
- **`Reporter`** writes a `ToolDiagnostic` to the host's `Error` and counts what it wrote.  The exit code comes from it.

### The format of an error

```
<path>(<line>,<column>): error <id>: <message>
<path> : error <id>: <message>
sqlsource : error <id>: <message>
    <label>: <text>
    see: <help link>
```

- The first form has a position, one-based; the second names a file with no position, a project file for one; the third names no file.
- The message is the descriptor's format with the arguments, in the invariant culture.
- A continuation line is four spaces, a label, a colon, a space and the text.  The reporter writes them in the order given, and `see:` last.  The epic's Diagnostics section lists the labels a describer's error has.  The errors of this sub-phase have none but `see:`; `help:` is first used in sub-phase 2.3.
- The first line of an error starts in the first column, and every continuation line starts with four spaces.  That is what lets a reader tell them apart, and it is all the reporter promises.  It does not stop MSBuild's own reading of a program's output: MSBuild allows white space before an error, so `    server: ERROR: relation "users" does not exist` is listed as a second error by an `Exec` task left at its defaults.  Phase 9, which runs the tool from a target, turns that reading off and gives an expression of its own that starts at a character that is not a space; the outline's notes on phase 9 say so.

### Diagnostics

In `Diagnostics/ToolDiagnostics.cs` of the generator assembly: errors, tagged `NotConfigurable`, with the category and the help link of `SqlDiagnostics`, whose two private constants for them become internal.

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC200` | The tool failed unexpectedly | `sqlsource failed unexpectedly: {0}: {1}` | The exception's type; its message |
| `SQLSRC201` | No project or solution found | `'{0}' holds no .sln, .slnx or .csproj file` | The directory |
| `SQLSRC202` | More than one project or solution found | `'{0}' holds more than one .sln, .slnx or .csproj file.  Name the one to run on.` | The directory |
| `SQLSRC203` | Path is not a project or a solution | `'{0}' is not a .sln, .slnx or .csproj file, or a directory that holds one` | The path as given |

A message of two sentences ends with a period, as the analyzer rule RS1032 requires and the messages of `SqlDiagnostics` do.

The ids `SQLSRC200` to `SQLSRC221` are assigned across the six sub-phases, in the outline's section on phase 2, so that two sub-phases built at once never take the same id.  Each sub-phase adds its own, with the four places `src/SqlSource/AGENTS.md` lists: the descriptor and `ToolDiagnostics.All`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`.  `DiagnosticsDocumentTests` compares the document with `SqlDiagnostics.All` and `ToolDiagnostics.All` together.  The rule on ids in `src/SqlSource/AGENTS.md` gains: an id from 200 is about the snapshot or the tool.

### `SqlSource.Tool.csproj`

- `OutputType` `Exe`, `TargetFramework` `net8.0`, `RollForward` `Major`, `PackAsTool`, `ToolCommandName` `sqlsource`, `PackageId` `SqlSource.Tool`, and the package's description, licence, URLs and tags as `SqlSource.csproj` has them.
- A project reference to `src/SqlSource/SqlSource.csproj`, as an assembly.  `Microsoft.CodeAnalysis.CSharp` with a `VersionOverride`, since the generator's reference is private and does not flow.  The plan checks that the version chosen has a `net8.0` or `netstandard2.0` asset.
- `System.CommandLine`, at the current stable version, in `Directory.Packages.props`.
- `InternalsVisibleTo` for `SqlSource.Tool.Tests`.  `SqlSource.csproj` gains the same for `SqlSource.Tool.Tests`, so that the tool's tests can build the parser's records.
- A package readme, `src/SqlSource.Tool/README.md`: what the tool is, in two sentences, and an absolute link to the repository's readme.  The root `README.md` is not packed into this package.
- Both projects join `SqlSource.slnx`, each with a `packages.lock.json`.

### Analyzer rules

The repository builds with `AnalysisLevel` `latest-all` and warnings as errors, and three rules are wrong for a console program.  `.editorconfig` turns each off for `src/SqlSource.Tool/`, with its reason, as that file's own comments ask; none is silenced with `NoWarn`.

| Rule | Why it is wrong here |
|----|----|
| CA1515, make public types internal | `Cli` is public on purpose |
| CA2007, call `ConfigureAwait` | A console program has no synchronization context |
| CA1031, do not catch general exceptions | Only for `Cli.cs`: the one catch that turns an exception into `SQLSRC200` |

The plan lists any other rule of `latest-all` that the tool trips, each with its reason, before it turns one off.

## Packaging and publishing

| File | Change |
|----|----|
| `tools/check-package.sh` | Finds each package by its id and a version, `SqlSource.[0-9]*.nupkg` and `SqlSource.Tool.[0-9]*.nupkg`, since `SqlSource.*.nupkg` now matches both.  Checks the generator's package as today, and the tool's for `tools/net8.0/any/SqlSource.Tool.dll`, `SqlSource.dll`, `Microsoft.CodeAnalysis.CSharp.dll` and `DotnetToolSettings.xml`, and its readme.  Without an argument it packs both projects |
| `tools/check-package-install.sh` | The same change to how it finds the generator's package |
| `tools/check-tool-install.sh`, new | Takes the folder of packages, or packs.  Installs `SqlSource.Tool` with `dotnet tool install --tool-path` into a temporary folder, from that folder as the only source.  Runs `sqlsource --version` and checks the version; `sqlsource --help` and checks that it lists `describe`; `sqlsource describe` in an empty folder and checks exit `1` and `SQLSRC201`.  Runs `--version` once more with `DOTNET_ROLL_FORWARD` set to `LatestMajor` |
| `pre-commit-validation.sh` | A step `tool-install` after `package-install`, skipped when the build fails |
| `.github/workflows/build.yml` | The same step after `Check package install`.  The coverage report's filter becomes `+SqlSource;+SqlSource.Tool` |
| `.github/workflows/publish.yml` | The check of a tag expects exactly the two packages of the tag's version.  The push and the release already take every package of the folder |
| `.editorconfig` | The three rules under Analyzer rules |
| `format.sh` | Nothing, unless `dotnet format` needs more than the generator built first; the plan finds out |

A machine that has a .NET 8 runtime runs the tool on it, and a machine that has only a later one rolls forward.  The last run of the script forces the newest runtime the machine has, so the roll-forward is proven wherever the check runs.

Before the first release that holds the tool, the owner confirms on nuget.org that the trusted publishing policy covers a package id that does not exist yet.  `docs/publishing.md` says so under External configuration.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.2 says In progress.
2. The two projects, empty but for `Program.cs` and one test, in the solution, with lock files.  `Cli.Run` returns `0`.
3. `ToolHost`, `ToolDiagnostic`, `Reporter`, `ToolDiagnostics` with `SQLSRC200`, and the catch of an exception.
4. The root command, `--version` and `--help`.
5. `describe`, the unit of a run, and `SQLSRC201` to `SQLSRC203`.
6. The package: `tools/check-package.sh`, `tools/check-package-install.sh`, `tools/check-tool-install.sh`, `pre-commit-validation.sh` and `build.yml`.
7. `publish.yml` and `docs/publishing.md`.
8. `CONTRIBUTING.md`, the two `AGENTS.md` files, and the outline's row set to Done.

## Testing

In `tests/SqlSource.Tool.Tests`: `net10.0`, xUnit v3, Shouldly, as `tests/SqlSource.Tests` is set up.  A helper, `CliRun`, calls `Cli.RunAsync` with a host whose writers are strings, whose working directory is a temporary folder it deletes, and whose environment is a dictionary.

| Where | Cases |
|----|----|
| `CliTests` | No arguments prints the help, exit `0`.  `--version`.  `--help` of the root and of `describe`.  An unknown option and an unknown command: exit `1`, a message on the error writer, nothing on the other.  An unknown option with `=value`, with `:value` and followed by a value: the message holds the option's name and not the value.  An unknown option where the path could stand is not taken for a path.  A host whose step throws: `SQLSRC200`, exit `1`, and no stack trace unless `SQLSOURCE_DEBUG` is set.  A cancelled token: exit `1` |
| `ReporterTests` | Each of the three first lines.  Continuation lines in order, and `see:` last.  A message with braces and quotes in an argument.  The count |
| `RunUnitTests` | Each row of the table of the unit.  One of each kind alone; two of one kind; a solution beside a project; an extension in capitals; a relative and an absolute path; a directory given; a path that is not there; a `.slnf` and an `.fsproj` |
| `tests/SqlSource.Tests/Diagnostics/` | The document test and the descriptor tests cover `ToolDiagnostics.All`: an error, not configurable, a help link that is the id's section |
| `tools/check-tool-install.sh` | The packed tool, as above |

## Documentation

- `.editorconfig`: as under Analyzer rules.
- `CONTRIBUTING.md`: the two projects in Build and test; the two packages and the three scripts under Package; the new step under Checks and Continuous integration; the coverage command's filter.
- `docs/publishing.md`: the release pushes two packages; the table of versions holds for both; the note under External configuration.
- `docs/diagnostics.md`: a part for the errors of the `sqlsource` tool, with `SQLSRC200` to `SQLSRC203`.
- `src/SqlSource/AGENTS.md`: `ToolDiagnostics`, and the rule on ids.
- `src/SqlSource.Tool/AGENTS.md`, new: the tool shares the generator's code and never copies it; everything from outside comes through `ToolHost`; every error goes through `Reporter` with a descriptor; the exit codes; the format of an error, and that only the first column tells a first line from a continuation; a wrong command line is reported by the tool and never repeats a token's text.  It ends with the maintenance footer.
- `README.md`: nothing in it changes.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 8.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
