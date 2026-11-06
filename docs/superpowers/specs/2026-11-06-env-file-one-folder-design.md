# The `.env` file: one folder, or the one you name

The owner's decision on the `.env` lookup was "Implement c and d": the tool and the build read a `.env` from one folder only, never searching upward, and otherwise rely on the environment the user sets up (c); an explicit override, `--env-file <path>` on the tool and a `SqlSourceEnvFile` build property, with `none` turning `.env` off (d).

## Why

Today the build checks for a `.env` in the project's folder and every folder above it, up to the root of the file system, and under `SqlSourceOnline` `auto` any hit runs the tool.  The tool reads the nearest `.env` from the unit's folder up to the nearest `.git`, or the unit's folder alone outside a repository.  The two disagree: a `~/.env` makes every build start the tool for nothing; before `git init`, a `.env` beside the `.sln` turns the build on and the tool never reads it; and outside a repository, a planted `.git` higher up makes the tool read a planted `.env`.  Tools that load a `.env` on their own (Vite, Next.js, Docker Compose, Rails, Bun) read one known folder and offer a path override.

## The folder

The folder is the unit's: the folder of the solution or project the run is on (owner's answer to Q1, recommended; pending).  The build's unit is each project, so the build checks the project's folder, and a run from a manifest, which is the build's, has the project as its unit: the two agree by construction.

| Layout | Hand-run `describe` | Build under `auto` |
|----|----|----|
| `.env` beside the project | Read | Online |
| `.env` beside the `.sln`, run on the solution | Read | Not online, unless `SqlSourceEnvFile` names it |
| `.env` at a monorepo's root, or in the home folder | Not read | Not online |

Nothing looks above the unit's folder for a `.env` or for a `.git` to bound a search.  The watch still finds the root of a repository for settings files and a restore's assets folder, which read no secret, so that helper moves out of the `.env` code.

## The override

- **Tool:** `--env-file <path>` on `describe` (with `--watch` and `--online` too) and `diagnose`, given at most once.  A relative path is from the working folder.  `none`, ignoring case, reads no `.env`; `./none` names a file.  A blank value is a usage error.
- **Build:** the property `SqlSourceEnvFile`, trimmed.  A relative path is from the project's folder, so `$(MSBuildThisFileDirectory).env` in a `Directory.Build.props` names one file for every project below it.  `none`, ignoring case, turns `.env` off.  Empty or not set: the project's folder.
- **The build passes it on**: `--env-file "<full path>"`, or `--env-file none`, only when the property is set, so a default build's command line is unchanged.
- **A named file that does not exist is no file**, as a missing `.env` is today: under `auto` the build does not go online, and the tool reads nothing.  SQLSRC213's `help:` line names the path, so a misspelt one is seen.  So a committed `SqlSourceEnvFile` does not break the build of a teammate who has no `.env` (Q3, recommended; pending).
- **A folder at the path is no file**, in both, as a folder named `.env` is passed over today; the build tests `System.IO.File.Exists`, not `Exists()`, which is true of a folder.
- The environment still wins over the file, variable by variable.

## What a user with a `.env` above the unit sees

Silence in the build: `auto` does not go online, as with no `.env`.  In the tool, a database with no connection is SQLSRC213, whose `help:` line names the file the run looked for and says it reads only that one, and that `--env-file` or `SqlSourceEnvFile` names another (Q2, recommended; pending).  No upward look of any kind, so no warning on every build of a machine with `~/.env`.

## Changes

- `src/SqlSource.Tool/DotEnv/DotEnvSearch.cs`: the file of a run is the unit folder's `.env`, the named file, or none; `FoldersOf` and `IsInRepository` move to the watch, which alone still uses them.  `NotRead` keeps naming the project `.env` files below the unit that a solution run did not read.
- `DescribeCommand`, `DiagnoseCommand`, `UsageCheck`'s option tables, `DescribeOptions`, `DescribeRun.ConnectionsOf`, `ConnectionEnvironment`, `DescribeErrors.NoConnection`, `WatchSession` and `WatchSet` (which watches the folder of the run's file alone).
- Both `SqlSource.targets` files: `SqlSourceEnvFolder` gives way to `SqlSourceEnvFileValue`, `SqlSourceEnvFilePath` and `SqlSourceHasEnvFile`, kept equal by `BuildFileTests`; the run's input and the `Exec` command follow.
- Tests that pin the agreement: for each case (default, a file beside the project, one above it, a named file, a missing named file, a folder, `none`) the build goes online exactly when the tool reads a file, in `OnlineBuildTests` with the stand-in, and `DotEnvSearchTests`, `ManifestRunTests` and the command tests for the tool.
- td-0113's skip of `Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt` under a `.env` above the temporary folder guards nothing once the build reads one folder, and goes.  Its `.git` skips stay where the watch still finds a repository's root.
- Docs: `README.md` (the recommended layout becomes a `.env` beside the project, or one named by `SqlSourceEnvFile`), `src/SqlSource.Tool/README.md`, `docs/manual/tool.md`, `docs/manual/settings.md` (the `auto` row, its bullet and a section for `SqlSourceEnvFile`), `docs/manual/diagnostics.md` (SQLSRC026, SQLSRC213), `docs/architecture/tool.md` and `msbuild-and-manifest.md`, the examples' readmes.  Nothing is released, so the breaking change is said plainly and needs no migration.
