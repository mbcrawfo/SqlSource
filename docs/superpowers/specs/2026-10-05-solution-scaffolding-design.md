# Solution scaffolding - design

Date: 2026-10-05

## Goal

Give the repository a buildable, testable, checkable skeleton: a solution, an empty source generator project, a test project wired to it, and root scripts that format the code and run every validation before a commit.  No generator behaviour is designed here.

## Decisions

| Decision | Choice | Reason |
|----|----|----|
| Supported consumers | .NET 8 SDK and later, Visual Studio 2022 17.8 and later | Explicit support target set by the project owner |
| Roslyn version the generator compiles against | `Microsoft.CodeAnalysis.CSharp` 4.8.0 | The compiler shipped with the .NET 8 SDK; it is the support floor |
| Roslyn version the tests run on | 5.9.0, the compiler in the pinned SDK 10.0.401 | The driver must parse current C# in test inputs, and most consumers host the generator on a newer compiler |
| How tests consume the generator | As an analyzer and as an assembly reference | Tests can call generated code and can drive the generator in memory to assert on output and diagnostics |
| Generator contents | Empty `IIncrementalGenerator` | No product behaviour is invented in a scaffolding change |
| Formatting entry point | One `format.sh` | A single command for CSharpier and both `dotnet format` passes |
| Validation failure mode | Run every step, then summarise | One run shows every problem |

## Out of scope

- NuGet packaging of the generator.
- Code coverage collection and reporting.
- GitHub Actions workflows.
- Installing `pre-commit-validation.sh` as a git hook.  It is run by hand.
- Polyfills for newer language features on `netstandard2.0`.  Add them when code first needs them.

## Solution and projects

### `SqlSource.slnx`

At the repository root, with two solution folders:

- `/src/` containing `src/SqlSource/SqlSource.csproj`
- `/tests/` containing `tests/SqlSource.Tests/SqlSource.Tests.csproj`

### `src/SqlSource`

- `TargetFramework` is `netstandard2.0`, the only framework a Roslyn component may target.
- `IsRoslynComponent` and `EnforceExtendedAnalyzerRules` are `true`.
- Package references, both with `PrivateAssets="all"`:
  - `Microsoft.CodeAnalysis.CSharp`, pinned to 4.8.0 in `Directory.Packages.props`.
  - `Microsoft.CodeAnalysis.Analyzers`, at a version compatible with that pin.
- One source file: a `[Generator]` class implementing `IIncrementalGenerator` whose `Initialize` registers nothing.
- The project inherits `Directory.Build.props` unchanged: warnings are errors, `AnalysisLevel` is `latest-all`, and the Roslynator and Sonar analyzers apply.  A rule that cannot be satisfied on `netstandard2.0` is turned off in `.editorconfig` with a comment, never with `NoWarn`.

### `tests/SqlSource.Tests`

- `TargetFramework` is `net10.0`.  The project is an xunit v3 executable that runs on Microsoft.Testing.Platform, the runner already selected in `global.json`.
- Package references: `xunit.v3`, `Shouldly`, and `Microsoft.CodeAnalysis.CSharp` overridden to 5.9.0 with `VersionOverride`.  The other packages already pinned in `Directory.Packages.props` are referenced when a test first needs them.
- One `ProjectReference` to `src/SqlSource` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="true"`, so the generator both runs on the test project and is callable from it.
- One smoke test: build a `CSharpCompilation` from a trivial source text, run the generator through `CSharpGeneratorDriver`, and assert that it produced no generated trees and no diagnostics.  Microsoft.Testing.Platform fails a run that discovers no tests, so this test is also what lets the validation script pass.

### Lock files

`RestorePackagesWithLockFile` is already on, so each project gets a `packages.lock.json`.  Both are committed.

## Scripts

Both scripts live at the repository root, are bash, resolve the repository root from their own location like the scripts in `tools/`, and must pass `tools/shellcheck.sh` and `tools/shfmt.sh --check`.

### `format.sh [--check]`

| Mode | Steps, in order |
|----|----|
| Default (rewrites files) | `dotnet format style SqlSource.slnx`, `dotnet format analyzers SqlSource.slnx`, `dotnet csharpier format .` |
| `--check` (rewrites nothing) | `dotnet format style SqlSource.slnx --verify-no-changes`, `dotnet format analyzers SqlSource.slnx --verify-no-changes`, `dotnet csharpier check .` |

- CSharpier runs last in the default mode so that it has the final say on layout after the `dotnet format` fixers have changed code.
- In `--check` mode all three steps run even when an earlier one fails, and the script exits non-zero if any failed.
- In the default mode the script stops at the first step that fails.
- Any argument other than `--check` is an error.

### `pre-commit-validation.sh`

Takes no arguments and never rewrites files.  It runs, in order:

1. `format.sh --check`
2. `tools/editorconfig-checker.sh`
3. `tools/shellcheck.sh`
4. `tools/shfmt.sh --check`
5. `tools/actionlint.sh`
6. `dotnet build SqlSource.slnx`
7. `dotnet test` on the solution without rebuilding

Every step runs regardless of earlier failures, with one exception: step 7 is skipped, and reported as skipped, when step 6 fails.  The script ends with a summary listing each step as passed, failed or skipped, and exits non-zero if any step failed.

`dotnet tool restore` is not a step.  It is one-time setup, documented in `README.md`.

## Documentation

- `README.md`:
  - States the support target: .NET 8 SDK and later, Visual Studio 2022 17.8 and later.
  - Documents how to build and test the solution.
  - Replaces the bare `dotnet csharpier check .` instruction with `format.sh` and `pre-commit-validation.sh`.
- `Directory.Packages.props`: a comment on the `Microsoft.CodeAnalysis.CSharp` pin saying that it is the support floor and must not be raised without changing the support target.
- `src/SqlSource/AGENTS.md`: the constraints an agent cannot infer from the code - `netstandard2.0` only, the Roslyn pin is the support floor, and the test project deliberately runs on a newer Roslyn.
- `docs/tech-debt/TD-0001`: nothing runs the generator on a Roslyn 4.8 host.  Compiling against 4.8.0 guards the API surface only, so a behavioural difference between 4.8 and the 5.9 used by the tests would go unnoticed.  The proposed fix is a second test run with the test project's Roslyn reference set to 4.8.0.

## Verification

- `./pre-commit-validation.sh` exits zero on the finished tree, with every step reported as passed.
- `./format.sh --check` exits non-zero when a `.cs` file is deliberately mis-formatted, and `./format.sh` repairs it.
- `./pre-commit-validation.sh` exits non-zero and reports the test step as skipped when the build is deliberately broken.
