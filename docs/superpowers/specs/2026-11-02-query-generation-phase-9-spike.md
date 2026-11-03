# The spike of phase 9, before the build - appendix

Date: 2026-11-02

Appendix to [the phase 9 design](2026-11-02-query-generation-phase-9-build-integration-design.md) of the [query generation epic](2026-10-07-query-generation-epic-design.md), written by sub-phase 9.0.  Before any code of the online build or the watcher, throwaway projects ran stand-ins of the design's targets with the packed package and the packed tool, MSBuild's `Exec` read the error forms, `FileSystemWatcher` recorded what saves raise, and the CI services' documentation was read for the variables they set.  The owner ran, from the checklist below, what only an IDE shows, in Rider on macOS; the other IDEs and systems are [TD-0097](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md).  The projects and scripts were not kept; what they ran and what they answered are here.  Each answer is what was run and what it did; a question the spike could not reach names the sub-phase that must answer it.

## The owner's checklist

Items 8 to 11 of the design's 9.0, and the editors' half of item 6, need an IDE, which an agent cannot drive.  Each is run in the sample below, on the machine and IDE it names, and its answers are written into the tables here.

### Setting up

1. On each machine, from a checkout of this branch, pack the generator into a folder of its own: `dotnet pack src/SqlSource/SqlSource.csproj --output ~/sqlsource-spike-feed`.  It is `SqlSource.0.1.0-dev.nupkg`.
2. Save the script in [TD-0097's procedure](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md#the-script) as `make-sample.sh` and run `bash make-sample.sh ~/sqlsource-spike-sample ~/sqlsource-spike-feed`.  On Windows, run it in Git Bash; or run it on the Mac and copy the folder and the feed across, then change the two paths in the sample's `nuget.config`.  The sample is outside the repository, so nothing of the repository's build applies to it.
3. In the sample's folder, run `dotnet build`.  It fails with one error, on purpose: `Queries/Orders.sql(1,10): error SQLSRC241: The query 'ListOrders' has no entry in the sidecar of its file, ...`, since `Orders.sql` has no sidecar yet.
4. Record the versions:

| | Version |
|----|----|
| Machine and OS | Mac |
| .NET SDK (`dotnet --version` in the sample) | 11.0.100-rc.1.26425.128 |
| Visual Studio | |
| Rider | 2026.2.4 |
| VS Code, C# extension, C# Dev Kit | |

The sample holds three queries and a tool that writes their sidecars from a process of its own, as the watcher will.  Every command below is run in a terminal in the sample's folder; a terminal inside the IDE is fine.

- `dotnet run --file tools/swap.cs -- b` rewrites `Queries/Users.sql.json` with a sidecar that says `id` is `bigint`: the model's `Id` becomes a `long`, and line 5 of `Program.cs`, `int id = new UsersDto(1, "name", null).Id;`, gets `CS0266`.  `-- a` writes the first one back, and the error goes.
- `dotnet run --file tools/swap.cs -- new` writes `Queries/Orders.sql.json`, which did not exist: `SQLSRC241` at `Orders.sql` goes.
- `dotnet run --file tools/swap.cs -- edited` writes the sidecar of the edited text of `Queries/Count.sql`, case E below.
- `dotnet run --file tools/swap.cs -- reset` puts everything back: sidecar A, `Count.sql`'s first text and its sidecar, no sidecar for `Orders.sql`.

### Item 8: IntelliSense after another process writes a sidecar

For each IDE, Visual Studio, Rider and VS Code with C# Dev Kit, and in each generator mode, the default and "Automatic": Visual Studio has the setting in Tools > Options (search "source generator"); VS Code in its settings (search "source generator"; record the setting's id); Rider has no such mode, so it has one row for each case.

Before each mode: run `swap.cs -- reset`, build once from the IDE, and wait until the IDE shows exactly one error, `SQLSRC241` at `Orders.sql`.  Open `Program.cs`, `Orders.cs` and `Queries/Count.sql` in the editor.  Then, for each case, run the command, touch nothing, and watch for 30 seconds.  If nothing changed, type a space at the end of a comment in `Program.cs` and save; if nothing changed, build.  Record what changed and after which step, and about how long after.

- **R, rewritten.**  `swap.cs -- b`.  Does `CS0266` appear on line 5 of `Program.cs`?  Then `swap.cs -- a`: does it go?
- **N, new.**  `swap.cs -- new`.  Does `SQLSRC241` go from the error list, and does `OrdersDto` in `Orders.cs` show as a known type on hover?
- **E, edited.**  In the editor, change line 2 of `Queries/Count.sql` to `SELECT count(*) AS total, max(id) AS top_id FROM users;`, exactly, and save.  Does `SQLSRC244`, "The query 'CountUsers' has changed since its entry was written", appear, and when?  Then `swap.cs -- edited`: does `SQLSRC244` go?  This is the race the research foresaw: the editor's save runs the generator before the watcher writes the sidecar.

| IDE | Mode | Case | Changed? | After what (nothing, a save, a build) | About how long |
|----|----|----|----|----|----|
| Visual Studio | default ( ) | R | | | |
| Visual Studio | default | N | | | |
| Visual Studio | default | E | | | |
| Visual Studio | Automatic | R | | | |
| Visual Studio | Automatic | N | | | |
| Visual Studio | Automatic | E | | | |
| Rider | | R | yes | nothing | seconds |
| Rider | | N | yes | nothing | seconds |
| Rider | | E | yes | build; this error didn't show in the editor| after build |
| VS Code | default ( ) | R | | | |
| VS Code | default | N | | | |
| VS Code | default | E | | | |
| VS Code | Automatic | R | | | |
| VS Code | Automatic | N | | | |
| VS Code | Automatic | E | | | |

Write the default mode's name in the brackets of its first row.

### Item 9: the design-time guard in the IDEs

The sample's `Directory.Build.targets` has two targets in the place of the online target, just before the compiler.  `SpikeProbe` has no condition, and appends a line to `obj/probe.log` each time any build of the project reaches it, with the time, `DesignTimeBuild`, `BuildingProject`, `BuildingInsideVisualStudio`, `SkipCompilerExecution`, `BuildingByReSharper`, `MSBuildRuntimeType` and `MSBuildVersion`.  `SpikeGuarded` has the design's condition, `'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'`, and appends `guarded target ran` to `obj/guarded.log`.

In each IDE:

1. Close the IDE.  Delete `obj/probe.log` and `obj/guarded.log`, if they exist, and nothing else of `obj`.
2. Open the sample in the IDE and wait until it has loaded and is idle, about a minute.  Copy both files.
3. Add a space to the end of line 2 of `Queries/Users.sql` and save; wait 15 seconds.  Copy the lines added since.
4. Add an empty line to `Sample.csproj` and save; wait 30 seconds.  Copy the lines added since.
5. Build from the IDE's own build command.  Copy the lines added since.

| IDE | Step | `obj/probe.log`, the lines added | `obj/guarded.log`, the lines added |
|----|----|----|----|
| Visual Studio | 2. open | | |
| Visual Studio | 3. save a `.sql` file | | |
| Visual Studio | 4. save the project file | | |
| Visual Studio | 5. build | | |
| Rider | 2. open | 00:09:41.670 DesignTimeBuild=True BuildingProject=False BuildingInsideVisualStudio= SkipCompilerExecution=True BuildingByReSharper=True MSBuildRuntimeType=Core MSBuildVersion=18.11.0\n00:09:46.173 DesignTimeBuild=True BuildingProject=False BuildingInsideVisualStudio= SkipCompilerExecution=True BuildingByReSharper=True MSBuildRuntimeType=Core MSBuildVersion=18.11.0 | file was not created |
| Rider | 3. save a `.sql` file | no change | file does not exist |
| Rider | 4. save the project file | 00:13:34.877 DesignTimeBuild=True BuildingProject=False BuildingInsideVisualStudio= SkipCompilerExecution=True BuildingByReSharper=True MSBuildRuntimeType=Core MSBuildVersion=18.11.0 | file does not exist |
| Rider | 5. build | 00:14:29.511 DesignTimeBuild= BuildingProject=true BuildingInsideVisualStudio= SkipCompilerExecution= BuildingByReSharper= MSBuildRuntimeType=Core MSBuildVersion=18.11.0 | 00:14:29.511 guarded target ran |
| VS Code | 2. open | | |
| VS Code | 3. save a `.sql` file | | |
| VS Code | 4. save the project file | | |
| VS Code | 5. build | | |

The guard is right if `guarded.log` gains a line at step 5 and at no other step.

### Item 10: the VS Code task

`.vscode/tasks.json` holds the design's task: it runs `tools/watcher.cs`, a stand-in for `describe --watch` that describes nothing.  It checks `Queries/*.sql` every 300 ms, and after each change prints `sqlsource: change detected; describing`, then, for each file with a line that holds `-- BROKEN`, one error on one line, `<path>(<line>,1): error SQLSRC999: stand-in error for the spike; detail: the file says BROKEN; see: https://example.invalid/sqlsrc999`, then `sqlsource: watching for changes; press Ctrl+C to stop`.  The problem matcher is `$msCompile`'s form, with the two markers as the background's begin and end.

1. Open the sample's folder in VS Code.  If it asks whether to trust the folder, record the question and say yes.  If it asks whether to allow automatic tasks, record the question's exact text and allow.
2. Does a terminal named `sqlsource watch (stand-in)` start by itself?  (Its first start compiles `watcher.cs`, a few seconds.)
3. Add a line `-- BROKEN` as line 2 of `Queries/Count.sql` and save.  Does a problem `SQLSRC999` appear in the Problems panel at `Count.sql` line 2, and is its text whole, `; detail:` and `; see:` included?
4. Remove the line and save.  Does the problem go?
5. Close and reopen the folder.  Does the task start again without asking?
6. In a copy of the folder that VS Code does not trust (open it and choose "Don't Trust"): does the task start?

| Step | What happened |
|----|----|
| 1. trust and automatic tasks | |
| 2. starts on open | |
| 3. problem appears | |
| 4. problem cleared | |
| 5. reopen | |
| 6. untrusted | |

### Item 11: Rider's startup task

1. Open the sample in Rider.  In Run > Edit Configurations, add a configuration that runs `dotnet run --file tools/watcher.cs` in the sample's folder: a Shell Script one with the script text, or whichever kind the current Rider offers for a command.  Record its kind.
2. In Settings > Tools > Startup Tasks, add it.  Record the setting's exact name and place, if it is not that.
3. Close and reopen the solution.  Does the watcher start, and where does its output show?
4. Add `-- BROKEN` as line 2 of `Queries/Count.sql` and save.  Does the error line show in the output, and is its path a link that opens the file at the line?

| Step | What happened |
|----|----|
| 1. kind of configuration | shell script (text) |
| 2. the setting's name and place | as described |
| 3. starts on open | yes, terminal pane |
| 4. the error, and whether it is a link | yes |

### Item 6, the editors' half: the events of a save

`tools/events.cs` prints every event `FileSystemWatcher` raises under the sample's folder, but `bin` and `obj`, with the time in milliseconds.

1. In a terminal in the sample's folder, run `dotnet run --file tools/events.cs` and wait for `watching ...`.
2. In each editor at hand (VS Code, Rider, Visual Studio), open `Queries/Users.sql`, add a space at the end of line 2, and save.  Copy what the terminal printed for that save.
3. For Rider, record whether Settings > Appearance & Behavior > System Settings > "Use safe write" (or "Back up files before saving") is on.

| OS | Editor | Events printed for one save |
|----|----|----|
| macOS | VS Code | |
| macOS | Rider | (backup before saving enabled) 24156 ms Changed  /Users/michael/Projects/sqlsource-spike-sample/Queries/Count.sql~\n24157 ms Created  /Users/michael/Projects/sqlsource-spike-sample/Queries/Count.sql~\n24158 ms Deleted  /Users/michael/Projects/sqlsource-spike-sample/Queries/Count.sql~\n24158 ms Changed  /Users/michael/Projects/sqlsource-spike-sample/Queries/Count.sql |
| Windows | Visual Studio | |
| Windows | VS Code | |
| Windows | Rider | |

### The sample

The script that writes the sample, `make-sample.sh`, is in [TD-0097](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md#the-script), with the procedure for the IDEs and systems not yet run.

## What ran

| | Version |
|----|----|
| .NET SDK | 10.0.401, the repository's `global.json`; MSBuild `18.9.11+e34a38d2a` (`dotnet msbuild -version`); the C# compiler `5.9.0-1.26423.113` |
| MSBuild, cross-checks | 17.11.31, 17.14.51 and 18.11.0, for the `Exec` cases of [item 4](#4-exec-and-the-error-format) |
| SqlSource and SqlSource.Tool | `0.1.0-dev`, packed from this branch at `adf45187` into a local feed; the tool installed through a tool manifest, as a user installs it |
| PostgreSQL | `postgres:18.6`, for the sidecars of the sample and the real tool's runs of [item 5](#5-the-cost-of-compile-as-an-input) |
| Linux | `mcr.microsoft.com/dotnet/sdk:10.0` in Docker 29.8, for [item 6](#6-file-events) |
| Roslyn's workspace | `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.9.0 with `Microsoft.Build.Locator` 1.11.2 |
| Host | macOS on Apple silicon |

Every project was outside the repository, so that none of its `Directory.Build.props`, `Directory.Packages.props` or `.git` applied.  Where the design names a target that 9.2 will write, a stand-in in the probe's `Directory.Build.targets` had the design's name, condition, `Inputs` and `Outputs`, and `Exec`ed a shell script in place of the tool, which logged its arguments and the manifest it was given and created or deleted a sidecar as the probe asked.  `SqlSourceTrackAdditionalFiles` was redefined there with `SqlSourceDescribeOnline` after `SqlSourceFindSettingsFiles` in its `DependsOnTargets`, as the design places it.  Every build cleared `SQLSOURCE_CONNECTION*`, `SQLSOURCE_LOG` and the CI variables from its environment, or set them on purpose.

## 1. The target's place

- **It runs before the compiler, after the manifest, in both kinds of project.**  In a project of the SDK the order was `SqlSourceWriteManifest`, the target that lists the sidecars, `SqlSourceRunOnline`, `SqlSourceDescribeOnline`, `SqlSourceTrackAdditionalFiles`, then `CoreCompile`, through `SqlSource.AfterSdk.targets`' hook before `CoreCompile`.  In a project not of the SDK (`Microsoft.Common.props`, the package's props, `Microsoft.CSharp.targets`, the package's targets imported where NuGet would), the same order ran from `GenerateMSBuildEditorConfigFileCore`.  The stand-in was given the manifest the dependency had written a few milliseconds before, in the same build.
- **The manifest a build writes is not the one the tool gets by name.**  Diffed against `dotnet msbuild -t:SqlSourceWriteManifest`, the build's holds two more `Compile` lines, the SDK's `obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` and `Base.AssemblyInfo.cs`, which targets add on the way to a compile; both are under `obj`, so in `SqlSourceBuildFolders`, and reach no settings file.  With a `.sql` file that a target hooked `BeforeTargets="CoreCompile"` adds (TD-0025's case), the build's manifest also has its `File=` line, which the one by name lacks: inside a build the online run does not have TD-0025's limit, as the design says.
- **A false `Condition` skips the dependencies.**  With `SqlSourceOnline=false`, and under `auto` with no `.env` and no variable, `obj/Debug/net10.0` held no manifest, stamp, list or value file, the stand-in never ran, and the performance summary listed none of the four targets.  This MSBuild writes no "skipped, due to false condition" line at `-v:d` or `-v:diag`, so the evidence is the files and the summary.
- **A sidecar the run creates reaches the generator in the same build.**  A project whose `Orders.sql` had no sidecar fails with `Queries/Orders.sql(1,10): error SQLSRC241: The query 'ListOrders' has no entry in the sidecar of its file, and its output 'models' needs one.`  With the stand-in creating the sidecar and the include after it, the same build compiled code that uses `OrdersDto`, and `Csc`'s `AdditionalFiles` ended with `Queries/Orders.sql.json`.  Without the include, the file on disk was not enough: `SQLSRC241` again, since the props' glob had expanded at evaluation.
- **A sidecar the run deletes must be removed from the items.**  Left in, the compiler fails with `CSC : error CS2001: Source file '.../Queries/Users.sql.json' could not be found.` and the generator adds `Queries/Users.sql.json(1,1): error SQLSRC237: The sidecar cannot be read, because it is not one JSON object.`  With the removal, the build reports only `SQLSRC241` at `Users.sql`, the consequence a deleted sidecar should have.
- **Up to date, and each input.**  A build with nothing changed printed `Skipping target "SqlSourceRunOnline" because all output files are up-to-date with respect to the input files.` and gave the compiler the same items.  Each input touched alone ran the tool once and the next build skipped it again: a `.sql` file, `sqlsource.json`, a sidecar, a deleted sidecar (through the list), a `.cs` file, `.env`, `dotnet-tools.json`, and `-p:SqlSourceOnline=true` after `auto` and back, through the value file (`Input file "obj/Debug/net10.0/Base.csproj.SqlSource.online" is newer than output file ...`).  A `.env` or tool manifest touched alone ran the tool and skipped `CoreCompile`.
- **The include and the removal are safe when the run is skipped.**  They are in `SqlSourceDescribeOnline`, which has no `Inputs`, so they run in every build.  A separate probe showed they would be safe inside the skipped target too: MSBuild runs a skipped target's `ItemGroup` and `PropertyGroup` for output inference.
- **Two costs of the design as written.**  The list of sidecars, written before the run, changes when the run creates or deletes one, so the next build launches the tool once more for nothing (`Input file "obj/Debug/net10.0/Base.csproj.SqlSource.sidecars" is newer than output file ...`).  Writing the list again after the `Exec` and before the stamp's `Touch` removed it, which a variant showed.  And the include appends a created sidecar after the settings files the search added, while the next evaluation's glob puts it among the other sidecars, so `SqlSourceTrackAdditionalFiles`' hash changes and the next build compiles once more; the run is still skipped there.
- **Multi-targeting.**  A project of `net8.0;net10.0` and a `net10.0` project that references it, each on a tree with no `obj`: `dotnet build`, `dotnet build -f net10.0` and the referencing project's build each ran the tool once, for `net8.0`, with a `net8.0` manifest written in that build, and every framework that compiled saw the sidecar the run created.  Under `dotnet build` the second framework's call reached the configuration the dispatch built (`Target "SqlSourceRunOnline" skipped. Previously built successfully.`); under `-f` and through the reference it built that configuration by name.
- **The second framework waited for the first framework's whole build.**  Under `dotnet build`, `net10.0`'s call into the `net8.0` configuration was answered only when `net8.0`'s `Build` request ended, after its `CopyFilesToOutputDirectory` and `IncrementalClean`, not when its `SqlSourceRunOnline` did: MSBuild runs one request of a configuration at a time.  So the inner builds ran in series from `SqlSourceDescribeOnline` on.  A variant fixed it: a target of the outer build, `BeforeTargets="DispatchToInnerBuilds"` under `'$(TargetFramework)' == '' and '$(TargetFrameworks)' != ''`, calls `SqlSourceRunOnline` in the first framework's configuration before the dispatch.  The dispatch passes only `TargetFramework` as a global property, as that call does, so its `net8.0` build is the same configuration and printed `Target "SqlSourceRunOnline" skipped. Previously built successfully.`; `net10.0`'s call was answered at once from the cached result and `net10.0` finished before `net8.0`.  The tool still ran once, with a manifest of that build.  `-f` and the reference have no outer build and behave as before.  The `net10.0` configuration, evaluated after the run, found the new sidecar through its own glob.
- **The first framework.**  `$([System.Text.RegularExpressions.Regex]::Match($(TargetFrameworks), '[^;\s]+'))` gave `net8.0`, `net10.0` and `net10.0` for `' ; net8.0 ; net10.0 '`, `'net10.0'` and `'net10.0;'`, as `ProjectEvaluator.FirstFramework` does.  `$(TargetFrameworks.Split(';')[0])` parses, but gives a blank for the first.
- **A solution built with `-m`.**  Two projects under one `.env` ran their tools at once, on two nodes, overlapping for about two seconds, each with its own manifest and working folder.

## 2. The connection in the log

The probe's value was `Host=leak-probe-7f3a;Password=p'a$(x)ss;Database=d`.  Each form ran in a bare `<Project>` and in a project of the SDK with the package, with `-v:diag` to a file and `-bl`; the binary log was searched replayed to text and decompressed.

| Form | `-v:diag` | Binary log, replayed | Binary log, raw |
|----|----|----|----|
| a. `Condition="'$(SQLSOURCE_CONNECTION)' != ''"` | the value | the value | the value |
| b. a property set to `$([System.String]::IsNullOrWhiteSpace($([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))`, and a condition on it | nothing | nothing | nothing |
| c. the same expression in the condition itself | nothing | nothing | nothing |
| d. nothing that names it | nothing | nothing | nothing |

- Form a leaks through MSBuild's own message, `Property 'SQLSOURCE_CONNECTION' with value '...' expanded from the environment.`, at the condition, with or without any setting.
- Forms b and c show only the property's `true` or `false`.  The variable's name, never its value, is in the copy of the project files that a binary log embeds as an archive.
- **With `MSBUILDLOGALLENVIRONMENTVARIABLES=1` every form leaks, d included**: MSBuild then logs the whole environment.  That is the caller's setting, and it exposes every secret of the environment, not only this one.  The manual can say so.

## 3. Design-time builds

| Build | `DesignTimeBuild` | `BuildingProject` | `SkipCompilerExecution` | Guarded target |
|----|----|----|----|----|
| `dotnet build`, `-t:Build`, `-t:Rebuild`, `publish`, `run` | empty | `true` | empty | ran |
| `dotnet msbuild -p:DesignTimeBuild=true -p:BuildingProject=false -p:SkipCompilerExecution=true -t:Compile` | `true` | `false` | `true` | did not run |
| `MSBuildWorkspace.OpenProjectAsync`, through its BuildHost | `True` | `False` | `True` | did not run; an unguarded target at the same place ran once |
| `dotnet msbuild -t:Compile` or `-t:CoreCompile`, no design-time property | empty | `false` | empty | did not run |

- The SDK 10.0.401 has no `CompileDesignTime` target (`MSB4057`).
- The BuildHost also set `ProvideCommandLineArgs` to `True`, and left `BuildingInsideVisualStudio` empty.
- The last row: a compile that does not go through `Build` leaves `BuildingProject` `false`, so the guard skips the online run there.  No IDE's build does that, which item 9 checks; a user who runs `-t:Compile` gets the committed sidecars.

## 4. `Exec` and the error format

- **One line is read whole.**  `/abs/Queries/Users.sql(3,8): error SQLSRC213: <text>; help: <text>; see: <link>`, on standard error or standard output, became an error with the file, line 3, column 8, code `SQLSRC213` and the whole text after the code, `; help:` and `; see:` included.  So did a path with a space, a Windows path, a range, a line alone, no position (`sqlsource : error SQLSRC213: ...`), a text of 6,000 characters, and `$(`, `%(` and `%25` in the text.
- **The old form.**  A continuation line `    help: ...`, `    see: ...` or `    query: ...` stayed a plain message.  One whose text holds `error:` or `warning:` after a label became a second error: `    server: ERROR: relation "users" does not exist` was read as an error in a file named `server`, with no code and no position, the epic's finding.  The one-line form never meets it.
- **`Exec` adds its own error beside the tool's.**  With its defaults, a tool that printed an error and exited `1` gave the tool's error and `MSB3073: The command "..." exited with code 1.`  An error line with exit `0` gave `MSB3077: The command "..." exited with return value 0, but errors were detected during execution. ExitCode was set to -1.` on MSBuild 18, and `MSB3073 ... exited with code -1.` on 17.  With `IgnoreExitCode="true"` and `ConsoleToMSBuild="true"`, neither: the tool's errors alone failed the build of a project of the SDK, `ExitCode` reached the target as `1` (or `-1`), and `ConsoleOutput` held every line of both streams.  The design's "the default reading of canonical errors" alone cannot keep `MSB3073` out; `IgnoreExitCode` does.
- **What `dotnet` does when it cannot start the tool:**

| Case | Exit | Output |
|----|----|----|
| No tool manifest names `sqlsource` | `1` | standard error `Could not execute because the specified command or file was not found.`; standard output `Possible reasons for this include:` and three lines starting `* ` |
| A manifest names it and was never restored, or names `0.0.9`, which no feed has | `1` | standard error `Run "dotnet tool restore" to make the "sqlsource" command available.` |
| The tool `0.1.0-dev` beside the package `0.1.0-other` | `0` | nothing of `dotnet`'s; the tool runs |

  `Exec`'s default reading takes none of `dotnet`'s lines as an error.  Two surprises: "restored" is a matter of the user's tool cache (`~/.dotnet/toolResolverCache`), not of the folder, so a manifest never restored in this folder still runs a version this user restored anywhere; and in a user profile `dotnet` had not run in, the not-restored case exited `0` or `143` in several of the first ten runs, and always `1` with `DOTNET_CLI_TELEMETRY_OPTOUT=1`, cause not found.
- **Telling the two apart.**  Not by the exit code: the tool's errors, its usage errors and `dotnet`'s failure all exit `1`, and the last can exit `0`.  Not by `dotnet`'s words, which are localised.  What worked in nine cases and in real builds: with `IgnoreExitCode` and `ConsoleToMSBuild`, count the captured lines that only the tool writes, those starting `sqlsource:`, the `SQLSRCnnn` errors and the summary line `<database> (<engine>): ...`.  None means that the tool did not run, but for one case this branch's review found (R3-F1): a run with nothing to describe, every query `sql` or no file claimed, exits `0` and prints nothing at all, so the design has the tool write a line of its own under `--online`.
- **`StandardOutputImportance` `low`.**  With the console logger (`-tl:off`), the summary and the offline line show at `-v:d` and `-v:diag` only.  With the terminal logger, which `dotnet build` uses in an interactive terminal, they show at no verbosity, nor at `normal` importance.
- **The terminal logger shows no message at `-v:m` or `-v:n`**, whatever its importance: a `<Message Importance="high">` and an `Exec` with `StandardOutputImportance="high"` showed only at `-v:d`.  A `<Warning>` showed at every verbosity.  And a target's `<Warning>` stays a warning in a project that sets `TreatWarningsAsErrors` `true`, which is the compiler's setting: the build succeeded with `warning SQLSRC099: ...`.  Only `-warnaserror` or `MSBuildTreatWarningsAsErrors=true` made it an error, and `MSBuildWarningsNotAsErrors` naming its code kept it a warning.  So the owner chose a warning: [owner decision 1](#what-the-later-sub-phases-take-from-this).

## 5. The cost of `@(Compile)` as an input

A copy of the base with 202 C# files, one claiming class, its sidecar current, and PostgreSQL up.  Before each build one C# file's body changed, so the C# input was newer than the stamp; five builds after two warm-up builds.

| | Median | Five builds |
|----|----|----|
| No online target, the build's `Time Elapsed` | 0.36 s | 0.36, 0.35, 0.36, 0.36, 0.37 |
| The stand-in running the real `dotnet sqlsource describe` | 1.52 s | 1.52, 1.45, 1.57, 1.59, 1.50 |
| The target alone, from the performance summary | 1,159 ms | 1,159, 1,105, 1,214, 1,200, 1,135 |
| `dotnet sqlsource --version`, the launch | 0.287 s | 0.280, 0.315, 0.297, 0.287, 0.284 |
| The tool's own `dotnet msbuild` for the manifest, from its log | 779 ms | 744, 850, 768, 784, 779 |
| Reading the claims of the 202 files, from its log | 75 ms | |

- The tool as it is today adds 1.16 s, over the design's "about a second".  Its own `dotnet msbuild` is 779 ms of it, which `--manifest` removes, so a run from the build's manifest should add about 0.38 s: the launch, the claims and about 15 ms of the rest.  That is an estimate from the log, since `--manifest` does not exist yet; the cost of `SqlSourceWriteManifest` inside the build is not in it.  The first cold build took the target to 2.2 s.
- So `@(Compile)` stays an input, and 9.2 measures the real run from a manifest by the design's rule.

## 6. File events

A console program watched a folder with `FileSystemWatcher` (`IncludeSubdirectories`, every `NotifyFilter`, a 64 KiB buffer) and printed each event with its kind, path, old path and time.  It ran on macOS, and on Linux in `mcr.microsoft.com/dotnet/sdk:10.0`, on the container's own file system and on a bind mount of a macOS folder written from the host.  Docker here is OrbStack 2.2.5, not Docker Desktop.  The saves were emulated; the owner's save from Rider is in [item 6's table](#item-6-the-editors-half-the-events-of-a-save).

| Save | Linux | macOS |
|----|----|----|
| In place (`File.WriteAllText`); VS Code saves a file of the workspace this way, by its source (`doWriteFile` in `src/vs/platform/files/node/diskFileSystemProvider.ts` opens it with the flag `w`; the temporary file and rename are for its own settings and state) | one `Changed` | `Changed`, sometimes with a `Created` in the same millisecond |
| Rider's "safe write", emulated: `Users.sql___jb_tmp___` written, `Users.sql` renamed to `Users.sql___jb_old___`, the temporary file renamed over it, the old one deleted | `Created` and `Changed` of the temporary file, the two `Renamed`, `Deleted` of the old one; no `Created` and no `Changed` of `Users.sql` | `Renamed Users.sql (from ...___jb_tmp___)`, with a `Created` of `Users.sql` in every run and a `Changed` in two of three; the first rename showed only as the old file's `Deleted` |
| Rider 2026.2.4 itself, by the owner, "back up files before saving" on | | `Changed`, `Created` and `Deleted` of `Count.sql~`, then `Changed` of `Count.sql`: in place |
| `vim -es -u NONE`, which is Vi-compatible | | in place, after a backup `Users.sql~` created and deleted around it |
| `vim -es -u NONE -N` | | `Renamed Users.sql~ (from Users.sql)`, then `Changed` and `Created` of `Users.sql`, then `Deleted Users.sql~` |
| `git checkout` of one changed file | `Deleted`, `Created`, `Changed` within 1 ms | the same |
| `git checkout` that rewrote 300 files | 900 events in 17 ms | 946 events in 53 ms |
| `git checkout` that added 300 files in a new folder | the folder's `Created`, and events for 244 of the 300 files; 230, 232, 232, 82 and 232 in five more runs | 690 events in 47 ms, all 300 files, 44 twice |

- **The design's rule for a save by rename holds in part.**  The listed path gets a `Renamed` to it on both systems, but a `Created` on macOS alone, and on macOS a `Changed` in three of five such saves.  And on macOS an in-place write sometimes raises a `Created` beside its `Changed`, with no pattern in the file's age.  So a `Changed`, a `Created` or a `Renamed` to a listed path has to be read as a write of it, and `Created` never as "the file is new".
- **Linux loses the files of a new directory.**  .NET adds the directory's watch after its `Created` event, and the files written into it before that are never reported.  A created directory has to make the watcher look at the tree again.
- **The bursts.**  No burst was longer than 141 ms, on the bind mount, and none raised an `Error` or an overflow, so a debounce of 300 ms holds each in one pass.
- **The bind mount.**  Saves made on the host raised events in the container: in place 54 ms later, the others within 6 ms.  A save by rename showed as two `Renamed X (from X)`, the old path the new one.  A folder created and filled on the host at once raised no event at all, for the folder or its files, nor did a later write into it; a folder created 0.3 s before its file was fine.
- **inotify.**  This worktree has 1,185 directories, 455 without `bin` and `obj`, and a clone adds 317 under `.git`.  A recursive `FileSystemWatcher` holds one inotify instance and one watch for each directory, whatever its handler ignores, and started in 15 ms.  The container's limits are 1,048,576 watches and instances; on Linux machines the default `fs.inotify.max_user_watches` is often 8,192 or 65,536, which this spike did not run.  With the limit lowered below the count, in a privileged container: `EnableRaisingEvents` did not throw, the watcher raised `Error` with `IOException: The configured user limit (N) on the number of inotify watches has been reached, or the operating system failed to allocate a required resource.` and went on watching part of the tree, 11 of 40 test folders at a limit of 300.  Too few instances makes the start throw.
- **Polling.**  `FileSystemWatcher` does not read `DOTNET_USE_POLLING_FILE_WATCHER`; `PhysicalFileProvider` (`"1"` or `"true"`, ignoring case) and `dotnet watch` do.  The provider polls every 4 s and fires one change token for a batch of changes, with no path and no kind, 4 to 8 s after the change; it saw the cases inotify lost on the bind mount.  The image `mcr.microsoft.com/dotnet/sdk:10.0` sets `DOTNET_USE_POLLING_FILE_WATCHER=true`, so a watcher in a container of it, or in a dev container built on it, polls unless the user sets the variable to `false`.
- Not run: Windows, Docker Desktop's bind mounts, and the saves of Visual Studio and VS Code ([TD-0097](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md)).

## 7. The CI variables

| Service | What it sets, from its documentation |
|----|----|
| GitHub Actions | `CI`: "Always set to `true`."; `GITHUB_ACTIONS` `true` ([variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables)) |
| GitLab | `CI` and `GITLAB_CI`: "`true` when available", for every job ([predefined variables](https://docs.gitlab.com/ci/variables/predefined_variables/)) |
| Azure Pipelines | `TF_BUILD`: "Set to `True` if a build task runs the script."  No `CI` is documented ([variables](https://learn.microsoft.com/en-us/azure/devops/pipelines/build/variables)) |
| Buildkite | `CI` and `BUILDKITE`: "Always `true`" ([environment variables](https://buildkite.com/docs/pipelines/configure/environment-variables)) |
| TeamCity | `env.TEAMCITY_VERSION`, "The version of the TeamCity server", which "can be used to determine if the build runs within TeamCity".  No `CI` is documented ([predefined build parameters](https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html)) |
| Jenkins | `CI`, "Statically set to the string "true"", since March 2021 ([the change](https://github.com/jenkinsci/jenkins/commit/4f1139764c1f099c326a3fd21a07544633ba76f7), JENKINS-36707); `JENKINS_URL` "only available if Jenkins URL set in system configuration" |

- The design's rule matched every service in an MSBuild condition: `'$(CI)' == 'true'` is true for `true`, `True` and `TRUE`, since MSBuild compares strings ignoring case, and false for `1` and `false`; `TF_BUILD` `True`; `TEAMCITY_VERSION` and `JENKINS_URL` not empty.  `GITHUB_ACTIONS` alone does not match, but GitHub sets `CI` beside it.
- Jenkins is recognised by `CI` since 2021; `JENKINS_URL` adds an older Jenkins whose URL is configured, and misses one whose URL is not.  The .NET SDK's own test for CI reads the same names, with `BUILD_ID` and `BUILD_URL` for Jenkins (`CIEnvironmentDetectorForTelemetry` in dotnet/sdk).
- This repository's workflows set and clear none of these names; GitHub sets `CI` on every runner, and the root `Directory.Build.props` already reads `'$(CI)' == 'true'`.  So every workflow step that builds the solution is a CI build under the design's rule, besides the root `SqlSourceOnline=false` that 9.2 adds.

## 8 to 11. What the owner ran

The owner ran the checklist in Rider 2026.2.4 on macOS, with the .NET SDK 11.0.100-rc.1 that the sample, which has no `global.json`, picked up; its answers are in the tables above.  On 2026-11-03 the owner chose to go on from those answers and to record the rest, Visual Studio, VS Code, Rider on other systems and Windows, as [TD-0097](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md), which holds the sample's script and the procedure.

- **Item 8, Rider.**  A sidecar rewritten (R) and one created (N) by another process changed the models within seconds, with nothing else done.  The research had expected Rider to wait for a save of the file, as its issue RIDER-75959 describes for additional files.  For a query edited in the editor and then described by another process (E), the owner recorded the change "after build", and that "this error didn't show in the editor".
- **Item 9, Rider.**  Its loading builds and the design-time build after a save of the project file reached the online target's place with `DesignTimeBuild=True`, `BuildingProject=False`, `SkipCompilerExecution=True` and `BuildingByReSharper=True`; a save of a `.sql` file ran none.  Its build from the IDE had `DesignTimeBuild` empty and `BuildingProject=true`.  The guarded target ran in the build alone: the guard is right in Rider.  `MSBuildRuntimeType` was `Core` throughout.
- **Item 10, the VS Code task**, was not run.
- **Item 11, Rider.**  A Shell Script configuration whose text is the command, added under Settings > Tools > Startup Tasks, started with the solution in a terminal pane, and the stand-in's error line was a link to the file.
- **Item 6, Rider on macOS.**  With "back up files before saving" on, Rider wrote `Count.sql~` and then the file in place: one `Changed` of the `.sql` file.  The backup's events are of a name that is not a `.sql` file, which the watcher passes over.

## What the later sub-phases take from this

**Owner decisions.**

1. **The tool that cannot start under `auto`** ([item 4](#4-exec-and-the-error-format)).  The owner chose on 2026-11-03, "R3-F2: 2": a targets warning with its own `SQLSRC0xx` code, which the package adds to `MSBuildWarningsNotAsErrors`, and the build goes on with the committed sidecars and the stamp untouched.  The design's "When the tool cannot run" and its diagnostics table say so.
2. **The IDE checks not run** ([items 8 to 11](#8-to-11-what-the-owner-ran)).  The owner chose on 2026-11-03 to go on from Rider's answers on macOS and to record the rest as [TD-0097](../../tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md), with the sample's script and the procedure.
3. **Where the outer build's target lives** (found by this branch's review, R1-F2).  The spike's variant of [item 1](#1-the-targets-place) ran from a `Directory.Build.targets`, which the outer build of a multi-targeted project imports; NuGet imports a package's `build/` files into the inner builds alone.  The owner chose on 2026-11-03, "recommended option": the target goes in `buildMultiTargeting/SqlSource.targets`, with the properties its condition reads, tested through the packed package.  The design's multi-targeting paragraph and 9.2's deliverables and tests say so.
4. **Settings through a symbolic link** (found by this branch's review, R1-F6, outside phase 9).  A build started in a folder reached through a link applies no `sqlsource.json`, since the compiler is given the settings file through the link and the `.sql` files without it.  The owner chose on 2026-11-03 to record it: [TD-0098](../../tech-debt/TD-0098-settings-file-through-a-symbolic-link-is-not-applied.md), with a known issue in the manual's page on settings.

**Amended in the design**, each a designer's call or a "Verify first" the spike answered, and marked there with the date:

- **9.2, the target**: the order and the false condition confirmed; the list of sidecars written again after the run; `Exec` with `IgnoreExitCode` and `ConsoleToMSBuild`, and a tool that did not start told apart by the absence of the tool's own lines; the cannot-start warning of owner decision 1; `@(Compile)` kept as an input, with the cost measured; the summary and the offline line visible with `-tl:off -v:d` only; the include and the removal confirmed, and the build after a created sidecar compiling once more.
- **9.2, multi-targeting**: the outer build calls `SqlSourceRunOnline` in the first framework's configuration before `DispatchToInnerBuilds`, from `buildMultiTargeting/SqlSource.targets` (owner decision 3), so that the inner builds keep running in parallel; the first framework by `Regex::Match`.
- **9.2, a tool that reports nothing**: a tool that started, exited other than `0` and wrote no `SQLSRCnnn` error, such as one older than the package refusing `--manifest` with `sqlsource: unknown option at position 2`, fails the build with a targets error, since `IgnoreExitCode` would otherwise let it pass (found by this branch's review, R1-F1).
- **9.2, the connection source and CI**: the boolean form confirmed; Jenkins' `CI`.
- **9.3, the watcher**: a `Changed`, `Created` or `Renamed` to a listed path is a write; a created directory runs the manifest targets again; the inotify limit's `Error` reported once; the tool polls the listed files itself under `DOTNET_USE_POLLING_FILE_WATCHER`, which the SDK's container image sets.
- **9.4, the recipes**: Rider's startup task confirmed; what Rider shows; Visual Studio and VS Code left to TD-0097.
- **The epic outline**: its bullet on the error format says that the target sets `IgnoreExitCode`.

**Exact values** the later sub-phases use:

- The connection source: `$([System.String]::IsNullOrWhiteSpace($([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))`, never `$(SQLSOURCE_CONNECTION)`.
- The first framework: `$([System.Text.RegularExpressions.Regex]::Match($(TargetFrameworks), '[^;\s]+'))`.
- The CI rule as the design states it, `'$(CI)' == 'true' or '$(TF_BUILD)' == 'True' or '$(TEAMCITY_VERSION)' != '' or '$(JENKINS_URL)' != ''`, matched every service's documented values.
- `dotnet`'s lines: `Could not execute because the specified command or file was not found.` and `Run "dotnet tool restore" to make the "sqlsource" command available.`, both with exit `1`, and both localised, so no target matches them.
- The cost on 202 C# files after a C# change: 1.16 s today, about 0.38 s from a manifest, of which the launch is 0.29 s.

**Where merged code differs from the design**, as of `adf45187`: nowhere the spike reached.  `dotnet new tool-manifest` of the SDK 10.0.401 writes `dotnet-tools.json` at the folder's root, which the design's inputs already name.

