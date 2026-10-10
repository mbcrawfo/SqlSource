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
