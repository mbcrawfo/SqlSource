# TD-0028 - The tool's runs of MSBuild are not verified on Windows

## Problem

The `sqlsource` tool starts `dotnet msbuild` for each project, from [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs) through [`ProcessRunner`](../../src/SqlSource.Tool/Processes/ProcessRunner.cs), and reads a file that a target of the package wrote.  All of it was written and tested on macOS, and CI runs on Linux.  Five things can differ on Windows and were not run there:

1. **A path outside ASCII.**  Each run sets `DOTNET_CLI_FORCE_UTF8_ENCODING`, and the tool reads MSBuild's output as UTF-8.  Whether the JSON of an evaluation then holds `ProjectAssetsFile` whole under a console code page that is not UTF-8 is not known.  A wrong path there makes a restored project look like one that was not: `SQLSRC220`.
2. **A path that MSBuild reads.**  The fixture `OddPaths` and the tests of [`MSBuildProperty`](../../src/SqlSource.Tool/Projects/MSBuildProperty.cs) show that a name with `;`, `=`, `%`, `'`, a space and a letter outside ASCII arrives whole in the manifest and in a `-p:` switch.  They ran on macOS and Linux.
3. **`SolutionDir`, which ends with `\`.**  The runner gives each argument through `ProcessStartInfo.ArgumentList`, which quotes as Windows needs, and what `dotnet` then hands MSBuild was not looked at.
4. **The exit code `-1`.**  [`ProcessResult.NotStarted`](../../src/SqlSource.Tool/Processes/ProcessResult.cs) is `-1`, which no program gives on Unix.  On Windows a program can end with it, and `SQLSRC205` would then say that `dotnet` could not be started.
5. **The kill of a process with what it started.**  `Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning` in `tests/SqlSource.Tool.Tests/ProcessRunnerTests.cs` is a shell script and is skipped on Windows.

## Why it exists

The repository has no Windows machine: every workflow in `.github/workflows` runs on `ubuntu-latest`.

## Impact

Low until a Windows user runs the tool.  Then each of the three is a run that fails or, for the first, that reports a project wrongly.

## Proposed fix

Run `tests/SqlSource.Tool.Tests` on `windows-latest` in `build.yml`.  `ProcessRunnerTests`, `ManifestTargetTests` and `FixtureProjectTests` cover the first three, and already choose their programs by operating system.  Give the fifth a program for Windows, and make the fourth a flag of `ProcessResult` in place of a code.  Mend what fails; a character that the manifest cannot carry on Windows becomes an item of its own.

## Trigger

The first report from Windows, or phase 5, the first release that a user can use.
