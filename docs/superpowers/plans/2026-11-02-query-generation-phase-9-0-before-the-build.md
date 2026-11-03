# Query generation 9.0: before the build - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Answer, by running MSBuild, the packed tool and package, `FileSystemWatcher` and the CI services' documentation, spike items 1 to 7 of sub-phase 9.0 and every "Verify first" item of the phase 9 design an agent can reach; write the owner's checklist for items 8 to 11; record all of it in `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md`; and amend the design and the epic outline where a finding changes a designer's call.

**Architecture:** One pull request of documents.  Throwaway projects, scripts and console programs live in the session's scratchpad, outside the repository, so that none of its `Directory.Build.props`, `Directory.Packages.props` or `.git` applies, and are never committed, as 7.0's and 8.0's spikes were.  Each task writes one results file per probe; the document is written from the results files.  Where a probe stands in for something 9.2 or 9.3 will build (the online target, `--manifest`, `--online`, `--watch`), the stand-in is a target in the probe's own `Directory.Build.targets` or a script, and the document says so.

**Tech Stack:** .NET SDK 10.0.401 (the repository's `global.json`), its MSBuild 18.9; the generator and tool packed from this branch, version `0.1.0-dev`; `postgres:18.6` in Docker 29.8 on Apple silicon; `mcr.microsoft.com/dotnet/sdk:10.0` for the Linux probes; `Microsoft.CodeAnalysis.Workspaces.MSBuild` for the BuildHost probe.

**Spec:** [`docs/superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md`](../specs/2026-11-02-query-generation-phase-9-build-integration-design.md), section "9.0 Before the build" and every "Verify first" item; its [research appendix](../specs/2026-11-02-query-generation-phase-9-research.md); the epic's [phase 9 section](../specs/2026-10-07-query-generation-epic-design.md#phase-9---build-integration).

## Global Constraints

- `$SCRATCH` is `/private/tmp/claude-501/-Users-michael-Projects-SqlSource--claude-worktrees-qm-refactor-5-identity-2d2a27/9eae11ce-b73b-4f01-b985-088fcb6ab956/scratchpad`.  Every probe lives under `$SCRATCH/spike/`; nothing of it is committed.
- Results: `$SCRATCH/spike/results/<task>-<probe>.txt`.  The first line names the command and the versions (`dotnet --version`, `dotnet msbuild -version`, the package's version); then one line per observation, `Q<item>.<case> | <what ran> | <what it did>`, quoting the tool's, MSBuild's or `dotnet`'s output exactly.
- The packed feed is `$SCRATCH/spike/feed`, holding `SqlSource.0.1.0-dev.nupkg` and `SqlSource.Tool.0.1.0-dev.nupkg` from `dotnet pack SqlSource.slnx --output $SCRATCH/spike/feed`.  A consumer restores from it and from nuget.org only, through its own `nuget.config`, and installs the tool through its own `.config/dotnet-tools.json`.
- PostgreSQL is `postgres:18.6` (root `AGENTS.md` rule 5) on host port 55418, `docker run -d --rm`, stopped when the task that needs it hands back.
- Every probe build clears `SQLSOURCE_CONNECTION*`, `SQLSOURCE_LOG`, `CI`, `TF_BUILD`, `TEAMCITY_VERSION`, `JENKINS_URL` and `GITHUB_ACTIONS` from its environment, or sets them deliberately and says so.  A test connection string is a throwaway value of the probe, never a real one.
- Where the design names a stand-in's names (`SqlSourceDescribeOnline`, `SqlSourceRunOnline`, the stamp, the list of sidecars, the value file), the probe uses them, so that the document's answers read against the design.
- Item 1's builds that ask about "a tree with no `obj`" delete `bin` and `obj` first.
- A finding that contradicts a Decided item is written as found and reported to me; nothing is worked around.

## Where merged code differs from the spec

- None found while planning.  The design was written against `6d67d35e`; `main` is `adf45187` and changed only the design itself and the epic outline since.  The spike's document records any the tasks find.
- The task's brief numbers the owner's items 8 to 10; the design has four, 8 to 11 (IntelliSense, the design-time guard in the IDEs, the VS Code task, Rider's startup task).  The checklist covers all four.

## Review Focus

- A `TargetFrameworks` with spaces, a leading `;` or one framework: task 2's first-framework expression is run on `' ; net8.0 ; net10.0 '`, `'net10.0'` and `'net10.0;'`.
- A connection string holding `;`, `'` and `$(`: task 3's leak probe uses `Host=leak-probe-7f3a;Password=p'a$(x)ss;Database=d`, so a partial echo or an MSBuild expansion shows up.
- A path with a space: task 2's base consumer sits in `$SCRATCH/spike/with space/` for the `Exec` probes of task 4, so the command's quoting is exercised.
- An editor's save by rename on macOS, where FSEvents coalesces: task 5 emulates Rider's "safe write" sequence exactly and records every event with its timestamp.
- `dotnet build` of a solution where two projects share a `.env` and run at once: task 2's `-m` solution has two projects under one `.env`.

---

### Task 1: The base, the owner's checklist and its sample

**Files:** Create `$SCRATCH/spike/feed/`, `$SCRATCH/spike/base/` (a consumer: `Base.csproj`, `nuget.config`, `.config/dotnet-tools.json`, `Queries/Users.sql`, `sqlsource.json`, `Program.cs`), `$SCRATCH/spike/sample/` (the owner's sample, below).  Create `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md` with its header and the owner's checklist only.

**Interfaces:** Produces the feed, the base consumer that every later task copies, and its sidecar described by the real tool, `Queries/Users.sql.json`.

- [ ] **Step 1:** Pack to the feed; create the base consumer, `net10.0`, `PackageReference` to `SqlSource` `0.1.0-dev` with `PrivateAssets="all"`, the tool installed with `dotnet tool install SqlSource.Tool --version 0.1.0-dev --add-source $SCRATCH/spike/feed` into its tool manifest.  `Users.sql` is a query on a table the probe creates (`create table users (id int4 primary key, name text not null, email text)`).
- [ ] **Step 2:** Start the container, create the table, run `dotnet sqlsource describe` with `SQLSOURCE_CONNECTION` set; `dotnet build` passes and `Program.cs` calls the generated method.  Record versions to `results/t1-base.txt`.
- [ ] **Step 3: The sample.**  A consumer like the base, with: the sidecar of `Users.sql` (`A`), a copy of it with `id` as `int8` (`B`, same hash, so current, and the model's `Id` becomes `long`); a second query `Orders.sql` with no sidecar and its sidecar kept aside (`New`); `swap.cs`, a .NET 10 file-based app (`dotnet run swap.cs -- a|b|new|reset`) that writes those files from another process; `probe.targets`, a target in the place of `SqlSourceDescribeOnline` that appends `DesignTimeBuild`, `BuildingProject`, the time and the process id to `obj/probe.log` without the guard, and a second, guarded by the design's condition, that appends to `obj/guarded.log`; `watcher.cs`, a stand-in watcher (file-based app) that polls `**/*.sql` every 300 ms, prints the begin marker `sqlsource: change detected; describing`, then for each `.sql` file holding `-- BROKEN` the one-line error `<path>(1,1): error SQLSRC999: stand-in error; detail: the file says BROKEN; see: https://example.invalid`, then the end marker `sqlsource: watching for changes; press Ctrl+C to stop`; `.vscode/tasks.json` with the design's task (`isBackground`, `runOn` `folderOpen`, `$msCompile`'s pattern, background begin and end on the markers) running `dotnet run watcher.cs`; and `events.cs`, a file-based app that prints every `FileSystemWatcher` event under the folder.  Build it once from the feed.
- [ ] **Step 4:** Decide, by the sample's size, whether it is committed under `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike/` (with a `README.md` that says how to pack the feed it restores from) or given inline as one setup script.  Prefer inline; commit only if the sidecars make the script unreadable.
- [ ] **Step 5: The checklist.**  In the document, items 8 to 11 and the IDE half of item 6, each with exact steps (versions of each IDE to record, where each generator-mode setting is, what to click, how long to wait), and a table to fill: IDE, mode, case (rewritten, new), did the model change, after what (nothing, a save of another file, a build), how long.  Item 9 records what `obj/probe.log` and `obj/guarded.log` hold after load, after an edit, after a build from the IDE.  Item 6 records `events.cs`'s output for a save of a `.sql` file from VS Code and from Rider.
- [ ] **Step 6:** Commit the plan and the document's skeleton; send the checklist to the manager as one owner question, "blocks merge: yes, because the spike document must record the answers".

### Task 2: The target's place (item 1)

**Files:** `$SCRATCH/spike/place/` only: copies of the base; a project not of the SDK (`Microsoft.Common.props`, `Microsoft.CSharp.targets`, the package's `build/` files imported by path from the extracted `.nupkg`); a solution of two SDK projects under one `.env`; a project with `TargetFrameworks` `net8.0;net10.0`, and a `net10.0` project that references it.  Results: `results/t2-*.txt`.

**Interfaces:** Consumes the feed and base.  The stand-in, in each probe's `Directory.Build.targets`: `SqlSourceRunOnline` (`Inputs`/`Outputs` per the design, a stamp under `obj`, condition without `BuildingProject`, `DependsOnTargets` `SqlSourceWriteManifest;SqlSourceListSidecars`) whose body `Exec`s `stand-in.sh`, which logs its arguments, the time and the manifest's lines to `$SCRATCH/spike/place/calls.log` and copies a sidecar into place or deletes one as the probe asks; `SqlSourceDescribeOnline` (the full guard, depends on or calls `SqlSourceRunOnline`, then the include and the removal); and a redefinition of `SqlSourceTrackAdditionalFiles` that adds `SqlSourceDescribeOnline` after `SqlSourceFindSettingsFiles`.

- [ ] **Step 1:** Each project kind with `dotnet build`, then `-v:d`: the order of `SqlSourceRunOnline`, `SqlSourceWriteManifest` and `CoreCompile` (target start times from `-v:d`), with `SqlSource.AfterSdk.targets` imported and in the non-SDK project.
- [ ] **Step 2:** The build's manifest against `dotnet msbuild -t:SqlSourceWriteManifest` on the same project, diffed, for the base and for a copy with a `.sql` file added by a target hooked to `BeforeTargets="CoreCompile"` (TD-0025's case).
- [ ] **Step 3:** With the `Condition` false (`SqlSourceOnline=false`), is the manifest written (`obj` listing before and after, `-v:d` "Skipping target" lines)?
- [ ] **Step 4:** The stand-in creates `Orders.sql.json` for a `.sql` file that had none: the same build compiles the method: `Program.cs` calls it under `#if USE_ORDERS`, and the build with `-p:DefineConstants=USE_ORDERS` fails if the generator did not see the sidecar.  Then the stand-in deletes a sidecar: with and without the removal, the compiler's message.
- [ ] **Step 5:** A second build with nothing changed skips `SqlSourceRunOnline` ("Skipping target ... because all output files are up-to-date"), and the include and removal give the same items (`-v:diag` item listing of `AdditionalFiles` diffed between the two builds); the generator still compiles `Orders`.  Each input touched alone runs it again: a `.sql`, `sqlsource.json`, a sidecar, a deleted sidecar, a `.cs`, `.env`, the tool manifest, `-p:SqlSourceOnline=true` after `auto`.
- [ ] **Step 6: Multi-targeting**, each on a tree with no `obj`: `dotnet build` (default parallelism), `dotnet build -f net10.0`, and `dotnet build` of the referencing `net10.0` project.  For each: how many times the stand-in ran, for which `TargetFramework`, with which manifest (its `TargetFramework=` line and its timestamp against the build's start), and whether each framework's compile saw the sidecar.  The first framework by `$([System.Text.RegularExpressions.Regex]::Match($(TargetFrameworks), '[^;\s]+'))` against `ProjectEvaluator.FirstFramework` on the three Review Focus values; and whether `$(TargetFrameworks.Split(';')[0])` parses at all.
- [ ] **Step 7:** `dotnet build -m` of the two-project solution under one `.env`: both run, the time each starts and ends.

### Task 3: The connection in the log, and design-time builds (items 2 and 3)

**Files:** `$SCRATCH/spike/leak/`, `$SCRATCH/spike/dtb/` (a console program on `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.Build.Locator` that opens a probe project with `MSBuildWorkspace`).  Results: `results/t3-leak-*.txt`, `results/t3-dtb-*.txt`.

- [ ] **Step 1:** With `SQLSOURCE_CONNECTION` set to the Review Focus value, build a project whose targets file holds, each in its own build: (a) a condition on `'$(SQLSOURCE_CONNECTION)' != ''`; (b) a property `SqlSourceHasConnection` set from `$([System.String]::IsNullOrWhiteSpace($([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))` and a condition on that property; (c) the same as one expression in the condition with no property; (d) nothing that names it.  For each: `-v:diag` to a file and `-bl`; search the text log and the binary log replayed to text (`dotnet msbuild x.binlog -noconlog -flp:v=diag;logfile=replay.txt`), and the raw binlog after `gunzip`, for `leak-probe-7f3a`, `p'a` and `SQLSOURCE_CONNECTION`.  Again with `MSBUILDLOGALLENVIRONMENTVARIABLES=1`.
- [ ] **Step 2:** The values of `DesignTimeBuild`, `BuildingProject`, `SkipCompilerExecution` and `BuildingInsideVisualStudio` at the stand-in's place, written by a target to a file, in `dotnet build`, in `dotnet msbuild -p:DesignTimeBuild=true -p:BuildingProject=false -p:SkipCompilerExecution=true -t:Compile` (and `-t:CompileDesignTime` if the SDK has it), and when `MSBuildWorkspace.OpenProjectAsync` loads the project; whether the guarded target ran in each.

### Task 4: `Exec`, the error format, `dotnet`'s messages, and the cost of `@(Compile)` (items 4 and 5)

**Files:** `$SCRATCH/spike/with space/exec/` and `$SCRATCH/spike/cost/`.  Results: `results/t4-*.txt`, `results/t5-cost.txt`.

- [ ] **Step 1:** A target `Exec`s a script that prints, to standard error and in another build to standard output: one one-line error in the design's form (`/abs/Users.sql(3,8): error SQLSRC213: <text>; help: <text>; see: <link>`), then the same error in today's form with four-space continuation lines, then exits `1`.  Record what MSBuild reports for each line (file, line, column, code, text), whether `MSB3073` is added, and with `IgnoreExitCode` and `ConsoleToMSBuild` how the exit code reaches the target.  The same with exit `0`.
- [ ] **Step 2:** What `dotnet sqlsource describe` prints and exits with: no tool manifest; a manifest that names the tool and no restore; a manifest that names `0.0.9` not in the feed; the tool at `0.1.0-dev` beside a package of another version (repack the package with `-p:VersionSuffix=other`), and whether `Exec`'s defaults read any of `dotnet`'s lines as an error.
- [ ] **Step 3:** `StandardOutputImportance` `low` and `StandardErrorImportance` default: the summary line and the offline line at `-v:q`, `-v:m`, `-v:n`, `-v:d`, and in the terminal logger (`-tl:on`) and without it (`-tl:off`).
- [ ] **Step 4: Cost.**  A copy of the base with 200 generated C# files (one class of about 30 lines each), one claiming `[SqlSourceGenerate]` class, the sidecar current, the container up.  After a warm build, change one `.cs` file's body and build with `-v:d` and `-clp:PerformanceSummary`, five times each: (a) with no online target; (b) with the stand-in target running the real `dotnet sqlsource describe` on the project (which today runs its own `dotnet msbuild` for the manifest); (c) the real tool's `--version` alone, as the launch cost.  From the tool's log (`--log`), the time its manifest step took, so that (b) minus that step estimates a run from `--manifest`.  Record the median of each.

### Task 5: File events (item 6)

**Files:** `$SCRATCH/spike/events/`: a console program that watches a folder recursively with `FileSystemWatcher` (`IncludeSubdirectories`, `NotifyFilter` all, `InternalBufferSize` 64 KiB) and prints each event, its kind, path, old path and a monotonic time; and one using `PhysicalFileProvider` with `DOTNET_USE_POLLING_FILE_WATCHER=true`.  Results: `results/t6-*.txt`.

- [ ] **Step 1:** On macOS, in a `git init` folder with `Queries/Users.sql`: an in-place write (`File.WriteAllText`), VS Code's save (its source's write path, cited; emulated), Rider's "safe write" (`Users.sql___jb_tmp___` written, original renamed to `Users.sql___jb_old___`, temp renamed over, old deleted), `vim`'s default (`:w` with `backupcopy=auto`, run headless with `vim -es`), `git checkout` of a branch that changes the file, and one that changes 300 files.
- [ ] **Step 2:** The same on Linux in `mcr.microsoft.com/dotnet/sdk:10.0`, in the container's own file system and on a bind mount of a macOS folder written from the host.
- [ ] **Step 3:** In the container: `fs.inotify.max_user_watches` and `max_user_instances`; directories of this repository with and without `bin`, `obj`, `.git` and `node_modules`; whether a recursive `FileSystemWatcher` on a copy of the repository starts, and the error when the limit is lowered below the count (`sysctl -w` in a privileged container).
- [ ] **Step 4:** Polling: the provider's events on the bind mount, and their delay.

### Task 6: The CI variables (item 7)

**Files:** `results/t7-ci.txt`.

- [ ] **Step 1:** From each service's documentation, with the URL and the exact sentence: whether it sets `CI`, with what value and case; `TF_BUILD` (Azure Pipelines); `TEAMCITY_VERSION`; `JENKINS_URL`; and what GitHub Actions, GitLab, Buildkite, Azure Pipelines, TeamCity and Jenkins each set that the design's rule reads.  Note any that sets `CI` to a value other than `true` (`1`, `True`).
- [ ] **Step 2:** This repository's workflows: what each sets or clears among those names (`grep -rn` over `.github/workflows`), and whether a step of theirs builds a project that would go online under `auto`.  MSBuild's reading of `$(CI)` when the variable is `True` and `1`.

### Task 7: The document, the amendments and the questions

**Files:** Modify `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md`; modify the design and `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` where a designer's call changes.

- [ ] **Step 1:** Write each item's section from the results files: what ran (a table of versions first, as 7.0's document has), then what it did, quoting output exactly; a question not reached names the sub-phase that must answer it.
- [ ] **Step 2:** "What the later sub-phases take from this": owner decisions, amendments made to the design, exact values (expressions, ids of `dotnet`'s messages, the measured cost), and where merged code differs.
- [ ] **Step 3:** For each finding that changes a designer's call, amend the design's statement in place, saying "Amended by the 9.0 spike on <date>: ..." and the epic outline where it states the same; for each that contradicts a Decided item, leave the design as it is and send the manager an owner question, "blocks merge: yes".
- [ ] **Step 4:** Leave the epic outline's phase 9 rows as they are: only 9.4 marks them Done (the run's shared notes).  Run `./format.sh`, then `./pre-commit-validation.sh`, and commit.
