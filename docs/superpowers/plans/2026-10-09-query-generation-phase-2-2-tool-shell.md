# Query generation, phase 2.2: the tool's shell and its package - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the `sqlsource` command as a program that starts, reports errors in the compiler's format, finds the solution or project to run on, and is packed, checked, installed by a script and published beside the generator.  It describes nothing yet.

**Architecture:** A console application, `src/SqlSource.Tool`, references the generator's project and shares its internals.  `Cli.RunAsync` runs the whole command over a `ToolHost`, a record of everything the tool takes from outside, so a test runs it in process.  A wrong command line is found by `UsageCheck` before System.CommandLine reads it, so that no token of it is ever printed; every other error is a `ToolDiagnostic` written by one `Reporter`, whose count gives the exit code.  The tool's descriptors, `SQLSRC200` to `SQLSRC203`, live in the generator assembly.

**Tech Stack:** C# on `net8.0` with `RollForward` `Major`; System.CommandLine 2.0.12; `Microsoft.CodeAnalysis.CSharp` 5.9.0 by `VersionOverride` (its `netstandard2.0` asset: it has no `net8.0` one); xunit v3 on Microsoft.Testing.Platform and Shouldly in `tests/SqlSource.Tool.Tests` on `net10.0`; bash for the package scripts.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-2-tool-shell-design.md`](../specs/2026-10-09-query-generation-phase-2-2-tool-shell-design.md).  Read it first; this plan argues from it.  The [epic outline](../specs/2026-10-07-query-generation-epic-design.md) holds what the later sub-phases build on this one: its sections "Packages and repository", "Output of the tool" and "Phase 2".

## Global Constraints

- Branch: `claude/query-generation-phase-2-2-tool-shell`, which exists and holds the reviewed spec.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running.  Run `./format.sh`, then the validation as its own command, fix what it reports, then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it, and keep those words out of any other command.
- **All the code of this plan was compiled, formatted and tested in a scratch copy of the repository**, at the state each of tasks 2 to 5 leaves and as a whole, with `./pre-commit-validation.sh` passing at the end.  Type it as it stands.  If a rule still objects, change the code to satisfy it; never suppress a rule, and never edit `.editorconfig` beyond what tasks 2 and 4 give.
- The three analyzer rules the spec names are the only ones the tool trips: CA2007 for `src/SqlSource.Tool/`, and CA1515 and CA1031 for `Cli.cs` alone.  `format.sh` needs no change.
- `src/SqlSource` stays on `netstandard2.0` and Roslyn 4.8.0, and takes no new package reference.  Its package holds the same files as before.
- The tool targets `net8.0`.  Do not raise it, and do not use an API that .NET 8 lacks.
- Nothing in the tool reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`.
- No text of System.CommandLine's is ever written for a wrong command line, and no test compares a text that System.CommandLine wrote: it writes in the language of the machine.
- Exit codes: `0` success; `1` an error was reported, a wrong command line, or a cancelled run; `2` is `--check`'s, in sub-phase 2.6.
- A file is at most 120 characters wide, apart from Markdown: `tools/editorconfig-checker.sh` rejects a longer line.  Shell scripts are formatted by `tools/shfmt.sh` and must be executable.
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
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ReporterTests
```

## Where this plan departs from the spec

The spec is the intent; these are the places the plan reads it more exactly or differently.

1. **`UsageCheck.cs` is a file of its own**, beside `Cli.cs`.  It walks the command line against the commands System.CommandLine holds, before System.CommandLine parses.  If System.CommandLine still rejects what `UsageCheck` let through, which no known command line does, `Cli` writes `sqlsource: the command line is not valid` and exits `1`.
2. **The root is a `Command`, not a `RootCommand`.**  A `RootCommand` takes its name from the process, reads `[suggest]` as a directive and has `/h` as a name of its help.  `--version` is an option whose action is the tool's own, so `sqlsource --version describe` prints the version and runs nothing.
3. **The stack trace is continuation lines** labelled `trace`, one for each line of the exception's text, before `see:`.  So it goes through the reporter, and no line of it starts in the first column.
4. **The reporter writes a line break inside a text as a space.**  An exception's message often holds one, and the first column is all that tells a first line from a continuation line.
5. **An empty path, `describe ""`, is `SQLSRC203`** with `''` as its path.  It is what an unset variable of a shell gives, and it must not run on the working directory.
6. **A directory that cannot be read is `SQLSRC200`**, with the exception's own message.  `docs/diagnostics.md` says so.
7. **A run that is cancelled before it starts exits `1` whatever it was asked**, `--help` included.
8. **`ToolDiagnostic.Position` is Roslyn's `LinePosition`**, counted from zero, and the reporter writes it from one.  It is what sub-phase 2.4 gets from a `DiagnosticInfo`.
9. **The tool writes English only**: `SatelliteResourceLanguages` is `en`.  The package is 4.5 MB and not 6.5 MB, and the help is not in two languages at once.
10. **`tools/check-package.sh` also fails when the two packages have different versions.**
11. **`docs/tech-debt` gains `TD-0023`**, where the spec expects nothing: a secret typed as an unknown option without `=` or `:`, or where the path stands, is printed.  The review of the spec found it and the owner accepted it; rule 2 of the root `AGENTS.md` records it.
12. **Tests beyond the spec's table:** `UsageCheckTests`, which gives `UsageCheck` an option that takes a value, since no option of this sub-phase does; `ToolHostTests`; and `DescribeTests`, which holds the `describe` cases of the spec's `CliTests` row, so that `CliTests.cs` is whole in task 4.
13. **The spec's example `0.1.0-dev+3f2a9c1`** shows a short commit id.  The SDK writes the whole one.

## Review Focus

Inputs the spec implies and does not name.  Each has a test in the task that owns the code.

1. **A text of an error that holds a line break**, an exception's message for one.  The error stays one first line and its continuation lines.  `Report_TextWithLineBreaks_StaysOnItsLine` in task 3; `Run_ExceptionWhoseMessageHasLineBreaks_KeepsTheErrorOnOneLine` in task 4.
2. **A token that System.CommandLine gives a meaning of its own**: `@file`, `[suggest]`, `/h`, `--`, the tool's own name.  Each is an argument or an unknown option like any other.  `Run_TokenThatStartsWithAnAtSign_IsNotReadAsAFile`, `Run_DoubleHyphen_IsAnUnknownOption` and `Run_TokenThatSystemCommandLineGivesAMeaning_IsAnArgumentLikeAnyOther` in task 4.
3. **The value of an option that starts with `-`**, a password for one.  It is the value, not an unknown option.  `Check_ValueThatStartsWithAHyphen_IsTheValueOfTheOptionBeforeIt` in task 4.
4. **An empty path**, `describe "$UNSET"`.  `SQLSRC203`, and not a run on the working directory.  `Find_EmptyArgument_IsSqlsrc203AndNotTheWorkingDirectory` in task 5.
5. **A directory written with a trailing separator, as `.`, or through `..`; a folder named `Nested.csproj`; a project in a folder below.**  The first three are found and reported by one full path, and the last two are no unit.  `Find_RelativeDirectory_IsTheOneUnitInIt`, `Find_Dot_IsTheWorkingDirectory`, `Find_FolderNamedLikeAProject_IsNotAUnit` and `Find_UnitInAFolderBelow_IsNotFound` in task 5.

## File Structure

| File | Is |
|----|----|
| `src/SqlSource.Tool/SqlSource.Tool.csproj` | The tool's project and its package |
| `src/SqlSource.Tool/Program.cs` | `Main`, which calls `Cli.Run` |
| `src/SqlSource.Tool/Cli.cs` | The public entry, the commands, and the catch of an exception |
| `src/SqlSource.Tool/UsageCheck.cs` | What is wrong with a command line, without a token's text |
| `src/SqlSource.Tool/ToolHost.cs` | What the tool takes from outside |
| `src/SqlSource.Tool/Reporting/` | `ContinuationLine`, `ToolDiagnostic`, `Reporter` |
| `src/SqlSource.Tool/Projects/` | `RunUnitKind`, `RunUnit`, `RunUnitFinder` |
| `src/SqlSource.Tool/README.md` | The package's readme |
| `src/SqlSource.Tool/AGENTS.md` | Guidance for the folder |
| `src/SqlSource/Diagnostics/ToolDiagnostics.cs` | The tool's descriptors, in the generator assembly |
| `tests/SqlSource.Tool.Tests/` | `TempFolder`, `CliResult`, `CliRun`, and the test classes |
| `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` | The rules every descriptor of the tool keeps |
| `tools/check-tool-install.sh` | Installs the packed tool and runs it |

---

### Task 1: Start the sub-phase

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
- Add to the commit: `docs/superpowers/plans/2026-10-09-query-generation-phase-2-2-tool-shell.md` (this plan)

- [ ] **Step 1: Set the row of 2.2 to In progress**

```diff
--- a/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
+++ b/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
@@ -64,5 +64,5 @@
 | 1. Parameters and settings | Done | [query-generation-phase-1-parameters-and-settings-design](2026-10-08-query-generation-phase-1-parameters-and-settings-design.md) | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL; every new marker, setting, enum and generator parameter of the epic is parsed and validated with no effect yet.  A user sees four changes: `SqlSourceTokenValidation` gives way to `SqlSourceGeneratorParameters`; `token-validation` and `token-ignore=` are no longer generator parameters, and `-- token-ignore:` is a marker; a query's generator parameters replace the preamble's; and `{{a:b}}` is a token with a default, where it was text. |
 | 2.1 The snapshot format | Done | [query-generation-phase-2-1-snapshot-format-design](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | In the generator assembly: the model of a sidecar, the hand-written reader and writer, the two comparisons the format design defines, and the JSON Schema under `schemas/`.  No tool, and no step of the generator reads a sidecar yet. |
-| 2.2 The tool's shell and its package | Not started | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
+| 2.2 The tool's shell and its package | In progress | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
 | 2.3 The project manifest and discovery | Not started | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
 | 2.4 The work list | Not started | [query-generation-phase-2-4-work-list-design](2026-10-09-query-generation-phase-2-4-work-list-design.md) | The syntax-level attribute reader; the plan of a run from the shared path resolver, parser, settings and hash: which queries need an entry, their databases, and which are selected by `--database` and a `.sql` path. |
```

- [ ] **Step 2: Validate**

Run: `./pre-commit-validation.sh`
Expected: every line of the summary says `passed`.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-09-query-generation-phase-2-2-tool-shell.md
```

```bash
git commit -F - <<'EOF'
Start phase 2.2 of query generation: the tool's shell

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 2: The two projects

**Files:**
- Create: `src/SqlSource.Tool/SqlSource.Tool.csproj`, `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/Program.cs`, `src/SqlSource.Tool/Cli.cs`
- Create: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, `tests/SqlSource.Tool.Tests/CliTests.cs`
- Create by restore: `src/SqlSource.Tool/packages.lock.json`, `tests/SqlSource.Tool.Tests/packages.lock.json`
- Modify: `Directory.Packages.props`, `SqlSource.slnx`, `src/SqlSource/SqlSource.csproj`, `.editorconfig`

**Interfaces:**
- Produces: `public static class Cli` in namespace `SqlSource.Tool` with `public static int Run(string[] args)`, which returns `0`.  The assembly `SqlSource.Tool` shows its internals to `SqlSource.Tool.Tests`, and `SqlSource` shows its to both.

- [ ] **Step 1: Add System.CommandLine to the central versions**

```diff
--- a/Directory.Packages.props
+++ b/Directory.Packages.props
@@ -32,4 +32,5 @@
         <PackageVersion Include="Shouldly" Version="4.3.0" />
         <PackageVersion Include="SonarAnalyzer.CSharp" Version="10.35.0.4138" />
+        <PackageVersion Include="System.CommandLine" Version="2.0.12" />
         <PackageVersion Include="xunit.analyzers" Version="2.1.0" />
         <PackageVersion Include="xunit.v3" Version="4.0.1" />
```

- [ ] **Step 2: Let the tool's tests see the generator's internals**

`SqlSource.csproj` already has the line for `SqlSource.Tool`.

```diff
--- a/src/SqlSource/SqlSource.csproj
+++ b/src/SqlSource/SqlSource.csproj
@@ -23,4 +23,5 @@
         <InternalsVisibleTo Include="SqlSource.Tests.RoslynFloor" />
         <InternalsVisibleTo Include="SqlSource.Tool" />
+        <InternalsVisibleTo Include="SqlSource.Tool.Tests" />
     </ItemGroup>
     <ItemGroup>
```

- [ ] **Step 3: Create the tool's project**

`src/SqlSource.Tool/SqlSource.Tool.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <!-- A machine that has only a later runtime than .NET 8 runs the tool on that one. -->
        <RollForward>Major</RollForward>
        <!--
            The tool writes English.  Without this, the package carries the resources of Roslyn and of
            System.CommandLine in thirteen languages, and the help is in two languages at once.
        -->
        <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    </PropertyGroup>
    <PropertyGroup>
        <PackAsTool>true</PackAsTool>
        <ToolCommandName>sqlsource</ToolCommandName>
        <PackageId>SqlSource.Tool</PackageId>
        <Description>The sqlsource command of the SqlSource C# SQL query source generator.</Description>
        <Authors>Michael Crawford</Authors>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <PackageProjectUrl>https://github.com/mbcrawfo/SqlSource</PackageProjectUrl>
        <RepositoryUrl>https://github.com/mbcrawfo/SqlSource</RepositoryUrl>
        <PackageTags>sql;source-generator;roslyn</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
    </PropertyGroup>
    <ItemGroup>
        <InternalsVisibleTo Include="SqlSource.Tool.Tests" />
    </ItemGroup>
    <ItemGroup>
        <!--
            The generator's reference to Roslyn is private and does not flow.  The tool reads a consumer's C#, so it
            takes a current compiler and not the generator's pin, as tests/SqlSource.Tests does.
        -->
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
        <PackageReference Include="System.CommandLine" />
    </ItemGroup>
    <ItemGroup>
        <None Include="README.md" Pack="true" PackagePath="/" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="../SqlSource/SqlSource.csproj" />
    </ItemGroup>
</Project>
```

`src/SqlSource.Tool/README.md`, the readme of the package.  Its links are absolute, because nuget.org shows it:

```markdown
# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.

This version finds the project or the solution to run on and describes nothing yet.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
```

`src/SqlSource.Tool/Program.cs`:

```csharp
using SqlSource.Tool;

return Cli.Run(args);
```

- [ ] **Step 4: Create the test project with one failing test**

`tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="GitHubActionsTestLogger" />
        <!-- The version the tool itself references: central pinning would otherwise hold it at the generator's. -->
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
        <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="xunit.v3" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="../../src/SqlSource.Tool/SqlSource.Tool.csproj" />
        <ProjectReference Include="../../src/SqlSource/SqlSource.csproj" />
    </ItemGroup>
</Project>
```

`tests/SqlSource.Tool.Tests/CliTests.cs`:

```csharp
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public class CliTests
{
    [Fact]
    public void Run_NoArguments_ReturnsZero() => Cli.Run([]).ShouldBe(0);
}
```

- [ ] **Step 5: Add both projects to the solution and write the lock files**

```diff
--- a/SqlSource.slnx
+++ b/SqlSource.slnx
@@ -1,4 +1,5 @@
 <Solution>
   <Folder Name="/src/">
+    <Project Path="src/SqlSource.Tool/SqlSource.Tool.csproj" />
     <Project Path="src/SqlSource/SqlSource.csproj" />
   </Folder>
@@ -6,4 +7,5 @@
     <Project Path="tests/SqlSource.Tests.RoslynFloor/SqlSource.Tests.RoslynFloor.csproj" />
     <Project Path="tests/SqlSource.Tests/SqlSource.Tests.csproj" />
+    <Project Path="tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj" />
   </Folder>
 </Solution>
```

```bash
dotnet restore SqlSource.slnx
```

Expected: it writes `src/SqlSource.Tool/packages.lock.json` and `tests/SqlSource.Tool.Tests/packages.lock.json`, and changes no other lock file.

- [ ] **Step 6: Run the build to see it fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0103 or CS0246, `Cli` does not exist.

- [ ] **Step 7: Write `Cli` and allow it to be public**

`src/SqlSource.Tool/Cli.cs`:

```csharp
using System;

namespace SqlSource.Tool;

/// <summary>
/// The <c>sqlsource</c> command.
/// </summary>
public static class Cli
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="args">The command line, without the name of the program.</param>
    /// <returns>The exit code of the process.</returns>
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return 0;
    }
}
```

`Cli` is public because the end-to-end projects of phase 5 call it, and CA1515 objects to a public type in a program.  Add to the end of `.editorconfig`:

```diff
--- a/.editorconfig
+++ b/.editorconfig
@@ -224,2 +224,7 @@
 # IDE0130 (namespace matches folder): a polyfill must be in the namespace of the type it stands in for.
 dotnet_diagnostic.IDE0130.severity = none
+
+[src/SqlSource.Tool/Cli.cs]
+# CA1515 (make types internal): Cli is the tool's public entry, which the end-to-end test projects call.  Any other
+# public type of the tool is a mistake, so the rule stays on for the rest of the project.
+dotnet_diagnostic.CA1515.severity = none
```

- [ ] **Step 8: Run the test**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, 1 test.

- [ ] **Step 9: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every line of the summary says `passed`.  `package` and `package-install` still pack the generator alone; task 6 changes them.

```bash
git add Directory.Packages.props SqlSource.slnx .editorconfig src/SqlSource/SqlSource.csproj src/SqlSource.Tool tests/SqlSource.Tool.Tests
```

```bash
git commit -F - <<'EOF'
Add the projects of the sqlsource tool and of its tests

The tool is a console application for .NET 8 that rolls forward, packed
as the .NET tool SqlSource.Tool.  It references the generator's project
and a current Roslyn.  Cli.Run returns 0.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 3: The host, the reporter and `SQLSRC200`

**Files:**
- Create: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `src/SqlSource.Tool/ToolHost.cs`, `src/SqlSource.Tool/Reporting/ContinuationLine.cs`, `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`, `src/SqlSource.Tool/Reporting/Reporter.cs`
- Test: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` (create), `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (modify), `tests/SqlSource.Tool.Tests/ReporterTests.cs` (create), `tests/SqlSource.Tool.Tests/ToolHostTests.cs` (create)

**Interfaces:**
- Produces, in `SqlSource.Diagnostics`: `internal static class ToolDiagnostics` with `DiagnosticDescriptor UnexpectedFailure` (`SQLSRC200`, two arguments: the exception's type and its message) and `ImmutableArray<DiagnosticDescriptor> All`.  `SqlDiagnostics.Category` and `SqlDiagnostics.HelpLinkBase` become `internal const string`.
- Produces, in `SqlSource.Tool`: `internal sealed record ToolHost(TextWriter Out, TextWriter Error, string WorkingDirectory, Func<string, string?> GetEnvironmentVariable)` with `static ToolHost Create()`.
- Produces, in `SqlSource.Tool.Reporting`: `internal sealed record ContinuationLine(string Label, string Text)`; `internal sealed record ToolDiagnostic(DiagnosticDescriptor Descriptor, string? Path, LinePosition? Position, EquatableArray<string> Arguments, EquatableArray<ContinuationLine> Lines)` with `static ToolDiagnostic Create(DiagnosticDescriptor descriptor, params string[] arguments)` and `ToolDiagnostic WithLines(params ContinuationLine[] lines)`; `internal sealed class Reporter(TextWriter error)` with `int Count` and `void Report(ToolDiagnostic diagnostic)`.

- [ ] **Step 1: Write the tests of the descriptors**

`tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`.  It is written once for every descriptor the tool will have:

```csharp
using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

// The errors of the sqlsource tool are descriptors of the generator assembly, held to the rules of the generator's.
public class ToolDiagnosticsTests
{
    [Fact]
    public void All_EveryDescriptor_IsAnErrorThatCannotBeConfigured()
    {
        foreach (var descriptor in ToolDiagnostics.All)
        {
            descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error, descriptor.Id);
            descriptor.IsEnabledByDefault.ShouldBeTrue(descriptor.Id);
            descriptor.Category.ShouldBe("SqlSource", descriptor.Id);
            descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.NotConfigurable, descriptor.Id);
        }
    }

    [Fact]
    public void All_Ids_AreUniqueInOrderAndFrom200()
    {
        var ids = ToolDiagnostics.All.Select(descriptor => descriptor.Id).ToArray();

        ids.ShouldBe(ids.Distinct().OrderBy(id => id, StringComparer.Ordinal));
        ids.ShouldAllBe(id => id.StartsWith("SQLSRC", StringComparison.Ordinal) && id.Length == 9);
        foreach (var id in ids)
        {
            int.Parse(id.AsSpan(6), CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(200, id);
        }
    }

    [Fact]
    public void All_Ids_ComeAfterEveryIdOfTheGenerator() =>
        string.CompareOrdinal(SqlDiagnostics.All[^1].Id, ToolDiagnostics.All[0].Id).ShouldBeLessThan(0);

    [Fact]
    public void All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument()
    {
        foreach (var descriptor in ToolDiagnostics.All)
        {
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            descriptor.HelpLinkUri.ShouldBe(
                "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc" + descriptor.Id[6..]
            );
        }
    }

    // RS1032, which the build checks, allows a period only at the end of a message of several sentences.  The tool
    // formats a message itself, so nothing else would notice a placeholder that the arguments do not fill.
    [Fact]
    public void All_Messages_FormatWithTheirArguments()
    {
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.UnexpectedFailure.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "System.Exception",
                "boom"
            )
            .ShouldBe("sqlsource failed unexpectedly: System.Exception: boom");

        foreach (var descriptor in ToolDiagnostics.All.Remove(ToolDiagnostics.UnexpectedFailure))
        {
            string.Format(
                    CultureInfo.InvariantCulture,
                    descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
                    "/work/app"
                )
                .ShouldContain("'/work/app'", Case.Sensitive, descriptor.Id);
        }
    }
}
```

The document test now reads both lists.  `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`:

```diff
--- a/tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs
+++ b/tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs
@@ -1,6 +1,8 @@
 using System;
 using System.Collections.Generic;
+using System.Collections.Immutable;
 using System.IO;
 using System.Linq;
+using Microsoft.CodeAnalysis;
 using Shouldly;
 using SqlSource.Diagnostics;
@@ -16,4 +18,9 @@
     private const string HeadingPrefix = "## ";
 
+    // The generator's descriptors and then the tool's, which is the order of their ids.
+    private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors = SqlDiagnostics.All.AddRange(
+        ToolDiagnostics.All
+    );
+
     private static readonly string[] Lines = File.ReadAllLines(
         Path.Combine(AppContext.BaseDirectory, "docs", "diagnostics.md")
@@ -22,5 +29,5 @@
     [Fact]
     public void Document_Sections_AreTheDescriptorsInOrder() =>
-        Sections().Select(section => section.Id).ShouldBe(SqlDiagnostics.All.Select(descriptor => descriptor.Id));
+        Sections().Select(section => section.Id).ShouldBe(Descriptors.Select(descriptor => descriptor.Id));
 
     [Fact]
@@ -29,5 +36,5 @@
         var sections = Sections().ToDictionary(section => section.Id, section => section.Body);
 
-        foreach (var descriptor in SqlDiagnostics.All)
+        foreach (var descriptor in Descriptors)
         {
             sections[descriptor.Id].FirstOrDefault(line => line.Length > 0).ShouldBe($"**{descriptor.Title}**");
@@ -42,7 +49,5 @@
         rows.ShouldBe(
             // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
-            SqlDiagnostics.All.Select(descriptor =>
-                $"| [{descriptor.Id}](#sqlsrc{descriptor.Id[6..]}) | {descriptor.Title} |"
-            )
+            Descriptors.Select(descriptor => $"| [{descriptor.Id}](#sqlsrc{descriptor.Id[6..]}) | {descriptor.Title} |")
         );
     }
```

- [ ] **Step 2: Run the build to see it fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0103, `ToolDiagnostics` does not exist.

- [ ] **Step 3: Add the descriptor, in its four places**

`SqlDiagnostics` shares its category and the start of its help link.  `src/SqlSource/Diagnostics/SqlDiagnostics.cs`:

```diff
--- a/src/SqlSource/Diagnostics/SqlDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/SqlDiagnostics.cs
@@ -14,7 +14,7 @@
 internal static class SqlDiagnostics
 {
-    private const string Category = "SqlSource";
-
-    private const string HelpLinkBase = "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#";
+    internal const string Category = "SqlSource";
+
+    internal const string HelpLinkBase = "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#";
 
     public static readonly DiagnosticDescriptor TypeNotPartial = new(
```

`src/SqlSource/Diagnostics/ToolDiagnostics.cs`.  Each descriptor is written out in full with a literal id, because the release-tracking analyzer reads the arguments:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SqlSource.Diagnostics;

/// <summary>
/// Every error that only the <c>sqlsource</c> tool reports.  The generator reports none of them: they are here so
/// that the release tracking and the tests of <c>docs/diagnostics.md</c> cover them with the generator's own.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and <c>docs/diagnostics.md</c>.
/// </remarks>
internal static class ToolDiagnostics
{
    public static readonly DiagnosticDescriptor UnexpectedFailure = new(
        id: "SQLSRC200",
        title: "The tool failed unexpectedly",
        messageFormat: "sqlsource failed unexpectedly: {0}: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc200",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>
    /// Every descriptor, in the order of its id.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(UnexpectedFailure);
}
```

`src/SqlSource/AnalyzerReleases.Unshipped.md`, which the build checks:

```diff
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -38,2 +38,3 @@
 SQLSRC118 | SqlSource | Error | Parameter is not declared
 SQLSRC119 | SqlSource | Error | Query has no parameters
+SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
```

`docs/diagnostics.md`.  The document test reads every `## ` heading as an id and every table row as a descriptor, in the order of the two lists, so the tool's errors go in the one table and the one run of sections.  Three edits near the top and a new section at the end:

````diff
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -1,5 +1,5 @@
 # Diagnostics
 
-Every problem SqlSource finds is a build error, and none can be turned off or made a warning.  An error in a `.sql` file is reported at its line and column in that file.  A `.sql` file with an error produces no members until the error is fixed.
+Every problem SqlSource finds is an error, and none can be turned off or made a warning.  The generator reports its errors in the build.  An error in a `.sql` file is reported at its line and column in that file, and a `.sql` file with an error produces no members until the error is fixed.  The `sqlsource` command-line tool prints its errors itself, in the format of a build error.
 
 | Id | Title |
@@ -37,6 +37,7 @@
 | [SQLSRC118](#sqlsrc118) | Parameter is not declared |
 | [SQLSRC119](#sqlsrc119) | Query has no parameters |
-
-Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.
+| [SQLSRC200](#sqlsrc200) | The tool failed unexpectedly |
+
+Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.  Ids from 200 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1.
 
 `SQLSRC901` is not in this list because it is not a problem.  It is the id under which SqlSource turns off the compiler's warning CS0436 for the types it adds to every project, in a project that sees the internals of another one that uses SqlSource; see [Projects that share internals](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#projects-that-share-internals).
@@ -525,2 +526,15 @@
 
 Remove the marker.
+
+## SQLSRC200
+
+**The tool failed unexpectedly**
+
+`sqlsource` stopped on an exception that it has no error of its own for.  The message holds the type of the exception and its message.
+
+```console
+$ dotnet sqlsource describe /srv/locked
+sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.UnauthorizedAccessException: Access to the path '/srv/locked' is denied.
+```
+
+When the message names something of your machine, as this one does, fix that.  Otherwise it is a bug in the tool: set the environment variable `SQLSOURCE_DEBUG` to any value, run the command again, and [report it](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.
````

- [ ] **Step 4: Run the tests of the descriptors**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`
Expected: PASS.

- [ ] **Step 5: Write the tests of the reporter and of the host**

`tests/SqlSource.Tool.Tests/ReporterTests.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class ReporterTests : IDisposable
{
    private const string Message = "error SQLSRC200: sqlsource failed unexpectedly: System.Exception: boom\n";

    private const string See =
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200\n";

    private static readonly ToolDiagnostic Failure = ToolDiagnostic.Create(
        ToolDiagnostics.UnexpectedFailure,
        "System.Exception",
        "boom"
    );

    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose() => _error.Dispose();

    [Fact]
    public void Report_ErrorAboutNoFile_StartsWithTheNameOfTheTool()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure);

        _error.ToString().ShouldBe("sqlsource : " + Message + See);
    }

    [Fact]
    public void Report_ErrorAboutAFile_StartsWithItsPath()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/App.csproj" });

        _error.ToString().ShouldBe("/work/App/App.csproj : " + Message + See);
    }

    [Fact]
    public void Report_ErrorAtAPosition_CountsTheLineAndTheColumnFromOne()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/Users.sql", Position = new LinePosition(2, 9) });

        _error.ToString().ShouldBe("/work/App/Users.sql(3,10): " + Message + See);
    }

    [Fact]
    public void Report_ContinuationLines_AreWrittenInOrderWithTheLinkLast()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            Failure.WithLines(
                new ContinuationLine("server", "ERROR: relation \"users\" does not exist"),
                new ContinuationLine("help", "create the table")
            )
        );

        _error
            .ToString()
            .ShouldBe(
                "sqlsource : "
                    + Message
                    + "    server: ERROR: relation \"users\" does not exist\n"
                    + "    help: create the table\n"
                    + See
            );
    }

    [Fact]
    public void Report_ArgumentWithBracesAndQuotes_IsWrittenAsItIs()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            ToolDiagnostic.Create(ToolDiagnostics.UnexpectedFailure, "System.Exception", "{0}'s \"x\" {{y}} {")
        );

        _error.ToString().ShouldStartWith("sqlsource : error SQLSRC200: ");
        _error.ToString().ShouldContain(": System.Exception: {0}'s \"x\" {{y}} {\n");
    }

    [Fact]
    public void Report_TextWithLineBreaks_StaysOnItsLine()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            ToolDiagnostic
                .Create(ToolDiagnostics.UnexpectedFailure, "System.Exception", "one\r\ntwo\nthree\rfour")
                .WithLines(new ContinuationLine("server", "first\nsecond"))
        );

        _error
            .ToString()
            .ShouldBe(
                "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.Exception: one two three four\n"
                    + "    server: first second\n"
                    + See
            );
    }

    [Fact]
    public void Count_AfterTwoErrors_IsTwo()
    {
        var reporter = new Reporter(_error);
        reporter.Count.ShouldBe(0);

        reporter.Report(Failure);
        reporter.Report(Failure with { Path = "/work/App/App.csproj" });

        reporter.Count.ShouldBe(2);
    }
}
```

`tests/SqlSource.Tool.Tests/ToolHostTests.cs`:

```csharp
using System;
using System.IO;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public class ToolHostTests
{
    [Fact]
    public void Create_RealHost_IsTheConsoleTheCurrentDirectoryAndTheEnvironment()
    {
        var host = ToolHost.Create();

        host.Out.ShouldBeSameAs(Console.Out);
        host.Error.ShouldBeSameAs(Console.Error);
        host.WorkingDirectory.ShouldBe(Directory.GetCurrentDirectory());
        host.GetEnvironmentVariable("PATH").ShouldBe(Environment.GetEnvironmentVariable("PATH"));
        host.GetEnvironmentVariable("SQLSOURCE_NOT_A_VARIABLE").ShouldBeNull();
    }
}
```

- [ ] **Step 6: Run the build to see it fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0246, `Reporter`, `ToolDiagnostic`, `ContinuationLine` and `ToolHost` do not exist.

- [ ] **Step 7: Write the host**

`src/SqlSource.Tool/ToolHost.cs`:

```csharp
using System;
using System.IO;

namespace SqlSource.Tool;

/// <summary>
/// What the tool takes from outside itself.  Nothing in the tool reads <see cref="Console" />,
/// <see cref="Environment" /> or the current directory except through a host, so that a test can give a run its own.
/// </summary>
/// <param name="Out">Where everything but an error goes.</param>
/// <param name="Error">Where an error goes.</param>
/// <param name="WorkingDirectory">The full path that a relative path is resolved against.</param>
/// <param name="GetEnvironmentVariable">The value of an environment variable, or null when it is not set.</param>
internal sealed record ToolHost(
    TextWriter Out,
    TextWriter Error,
    string WorkingDirectory,
    Func<string, string?> GetEnvironmentVariable
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory and the environment of the process.
    /// </summary>
    public static ToolHost Create() =>
        new(Console.Out, Console.Error, Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable);
}
```

- [ ] **Step 8: Write the error as data**

`src/SqlSource.Tool/Reporting/ContinuationLine.cs`:

```csharp
using System;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// A line under the first line of an error: <c>    label: text</c>.
/// </summary>
/// <param name="Label">What the line holds, <c>help</c> for one.</param>
/// <param name="Text">The line's text.</param>
internal sealed record ContinuationLine(string Label, string Text) : IEquatable<ContinuationLine>;
```

`src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`.  `EquatableArray<T>` is the generator's, in the namespace `SqlSource`, which the tool's namespaces are inside:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// One error of the tool, as data.  <see cref="Reporter" /> writes it.
/// </summary>
/// <param name="Descriptor">What is wrong.  Its id has a section in <c>docs/diagnostics.md</c>.</param>
/// <param name="Path">The full path of the file the error is about, or null when it is about no file.</param>
/// <param name="Position">Where in the file, counted from zero, or null.  Never set without a path.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
/// <param name="Lines">The lines under the first, in the order they are written.</param>
internal sealed record ToolDiagnostic(
    DiagnosticDescriptor Descriptor,
    string? Path,
    LinePosition? Position,
    EquatableArray<string> Arguments,
    EquatableArray<ContinuationLine> Lines
)
{
    /// <summary>
    /// An error about no file.
    /// </summary>
    public static ToolDiagnostic Create(DiagnosticDescriptor descriptor, params string[] arguments) =>
        new(
            descriptor,
            Path: null,
            Position: null,
            new EquatableArray<string>(ImmutableArray.Create(arguments)),
            EquatableArray<ContinuationLine>.Empty
        );

    /// <summary>
    /// The same error with these lines under its first, in this order.
    /// </summary>
    public ToolDiagnostic WithLines(params ContinuationLine[] lines) =>
        this with
        {
            Lines = new EquatableArray<ContinuationLine>(ImmutableArray.Create(lines)),
        };
}
```

- [ ] **Step 9: Write the reporter**

`src/SqlSource.Tool/Reporting/Reporter.cs`.  The arguments go to `string.Format` as an `object?[]` in a local, which is what Sonar's S3220 asks for:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Writes every error of a run, in the compiler's format, and counts them.  The exit code comes from the count.
/// </summary>
/// <remarks>
/// The first line of an error starts in the first column and every other line of it with four spaces.  That is all
/// a reader may rely on to tell them apart, so no text of an error may hold a line break: each is written as a space.
/// </remarks>
internal sealed class Reporter(TextWriter error)
{
    private const string Indent = "    ";

    /// <summary>
    /// How many errors were written.
    /// </summary>
    public int Count { get; private set; }

    public void Report(ToolDiagnostic diagnostic)
    {
        var descriptor = diagnostic.Descriptor;
        object?[] arguments = [.. diagnostic.Arguments.Select(OnOneLine)];
        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );

        error.WriteLine($"{Origin(diagnostic)}: error {descriptor.Id}: {message}");
        foreach (var line in diagnostic.Lines)
        {
            error.WriteLine($"{Indent}{line.Label}: {OnOneLine(line.Text)}");
        }

        error.WriteLine($"{Indent}see: {descriptor.HelpLinkUri}");
        Count++;
    }

    // MSBuild's own form: a file with a position has no space before the colon, and anything else has one.
    private static string Origin(ToolDiagnostic diagnostic)
    {
        if (diagnostic.Path is null)
        {
            return "sqlsource ";
        }

        return diagnostic.Position is { } position
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostic.Path}({position.Line + 1},{position.Character + 1})"
            )
            : diagnostic.Path + " ";
    }

    private static string OnOneLine(string text) =>
        text.AsSpan().IndexOfAny('\r', '\n') < 0
            ? text
            : text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
```

- [ ] **Step 10: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, the test of task 2 with those of `ReporterTests` and `ToolHostTests`.

- [ ] **Step 11: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every line of the summary says `passed`.

```bash
git add src/SqlSource docs/diagnostics.md src/SqlSource.Tool tests/SqlSource.Tests/Diagnostics tests/SqlSource.Tool.Tests
```

```bash
git commit -F - <<'EOF'
Add the tool's host, its reporter and SQLSRC200

An error of the tool is data, a descriptor of the generator assembly
with a path, a position and continuation lines, and one reporter writes
it in the compiler's format with the link to its section last.  A line
break inside a text is written as a space, since only the first column
tells a first line from a continuation line.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 4: The command line, `--version`, `--help` and the catch of an exception

**Files:**
- Create: `src/SqlSource.Tool/UsageCheck.cs`
- Modify: `src/SqlSource.Tool/Cli.cs` (replace), `.editorconfig`
- Test: `tests/SqlSource.Tool.Tests/TempFolder.cs`, `CliResult.cs`, `CliRun.cs`, `UsageCheckTests.cs` (create), `CliTests.cs` (replace)

**Interfaces:**
- Consumes: `ToolHost`, `Reporter`, `ToolDiagnostic`, `ContinuationLine`, `ToolDiagnostics.UnexpectedFailure` from task 3.
- Produces: `internal static IReadOnlyList<string> UsageCheck.Check(Command root, IReadOnlyList<string> args)`, each item a whole line to write.
- Produces on `Cli`: `internal static Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)`; `internal const string DebugVariable = "SQLSOURCE_DEBUG"`; `internal static string Version`; and `private static Command BuildCommands()`, which task 5 gives two parameters.
- Produces for the tests: `TempFolder` (`Path`, `PathOf(relative)`, `WriteFile(relative, content = "")`, `CreateFolder(relative)`), `CliResult(int ExitCode, string Out, string Error)` with `\n` line ends, and `CliRun` (`Folder`, `Environment`, `Out`, `RunAsync(params string[] args)`, `RunCancelledAsync(params string[] args)`).

How System.CommandLine 2.0.12 is used, since it differs from the betas most examples show:

- `command.Parse(args, parserConfiguration)` gives a `ParseResult`; `parseResult.InvokeAsync(invocationConfiguration, cancellationToken)` runs the action and gives the exit code.  Output goes to `InvocationConfiguration.Output` and `Error`.
- An option's `Action` runs in place of the command's.  `HelpOption` has one; `--version` gets one here.
- `Option.Arity`, `Option.Recursive`, `Option.Name`, `Option.Aliases`, `Command.Options`, `Command.Subcommands` and `Command.Arguments` are what `UsageCheck` reads.

- [ ] **Step 1: Write the helpers of the tests**

`tests/SqlSource.Tool.Tests/TempFolder.cs`:

```csharp
using System;
using System.IO;

namespace SqlSource.Tool.Tests;

// A folder of a test's own, deleted with everything in it when the test ends.
internal sealed class TempFolder : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("sqlsource-tests-");

    public string Path => _directory.FullName;

    // The full path of a file or a folder under this one, from a path with forward slashes.
    public string PathOf(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string WriteFile(string relativePath, string content = "")
    {
        var path = PathOf(relativePath);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string CreateFolder(string relativePath) => Directory.CreateDirectory(PathOf(relativePath)).FullName;

    public void Dispose() => _directory.Delete(recursive: true);
}
```

`tests/SqlSource.Tool.Tests/CliResult.cs`:

```csharp
namespace SqlSource.Tool.Tests;

// What a run left behind.  Lines end with "\n" on every operating system.
internal sealed record CliResult(int ExitCode, string Out, string Error);
```

`tests/SqlSource.Tool.Tests/CliRun.cs`.  The private method has another name than `RunAsync` on purpose: xUnit1051 objects to a call of a method that has an overload with a `CancellationToken`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.Tool.Tests;

// Runs the whole command in process, as Cli.Run does, with a host of the test's own: writers that are strings, a
// working directory that is a temporary folder, and an environment that is a dictionary.
internal sealed class CliRun : IDisposable
{
    public TempFolder Folder { get; } = new();

    public Dictionary<string, string> Environment { get; } = [];

    // Set to stand in for standard output, for a test of a writer that fails.
    public TextWriter? Out { get; init; }

    public Task<CliResult> RunAsync(params string[] args) => InvokeAsync(args, TestContext.Current.CancellationToken);

    public async Task<CliResult> RunCancelledAsync(params string[] args)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        return await InvokeAsync(args, cancelled.Token);
    }

    public void Dispose() => Folder.Dispose();

    private async Task<CliResult> InvokeAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        var host = new ToolHost(Out ?? output, error, Folder.Path, name => Environment.GetValueOrDefault(name));

        var exitCode = await Cli.RunAsync(args, host, cancellationToken);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
```

- [ ] **Step 2: Write the tests of `UsageCheck`**

`tests/SqlSource.Tool.Tests/UsageCheckTests.cs`:

```csharp
using System.CommandLine;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The command line of this sub-phase has no option that takes a value.  These tests give UsageCheck a command that
// has one, as later sub-phases will, so that the rule for a value is held before the first secret arrives.
public class UsageCheckTests
{
    private const string Secret = "s3cret";

    private static Command Root()
    {
        var connection = new Option<string>("--connection", "-c") { Arity = ArgumentArity.ExactlyOne };
        var force = new Option<bool>("--force") { Arity = ArgumentArity.Zero };
        var verbose = new Option<bool>("--verbose") { Arity = ArgumentArity.Zero, Recursive = true };
        var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        return new Command("sqlsource")
        {
            verbose,
            new Command("describe") { connection, force, path },
        };
    }

    [Theory]
    [InlineData]
    [InlineData("describe")]
    [InlineData("describe", "App.csproj")]
    [InlineData("describe", "--connection", "billing=Host=db")]
    [InlineData("describe", "--connection=billing=Host=db")]
    [InlineData("describe", "--connection:billing=Host=db")]
    [InlineData("describe", "-c", "billing=Host=db", "App.csproj", "--force")]
    [InlineData("--verbose", "describe")]
    [InlineData("describe", "--verbose")]
    public void Check_CommandLineWithNothingWrong_GivesNoLine(params string[] args) =>
        UsageCheck.Check(Root(), args).ShouldBeEmpty();

    [Fact]
    public void Check_ValueThatStartsWithAHyphen_IsTheValueOfTheOptionBeforeIt() =>
        UsageCheck.Check(Root(), ["describe", "--connection", "-" + Secret]).ShouldBeEmpty();

    [Fact]
    public void Check_OptionAtTheEndWithoutItsValue_NamesTheOption() =>
        UsageCheck
            .Check(Root(), ["describe", "--connection"])
            .ShouldBe(["sqlsource: option '--connection' needs a value"]);

    [Fact]
    public void Check_ValueForAFlag_NamesTheOptionAndNotTheValue() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret])
            .ShouldBe(["sqlsource: option '--force' takes no value"]);

    [Fact]
    public void Check_OptionOfACommandGivenBeforeTheCommand_IsUnknownThere() =>
        UsageCheck.Check(Root(), ["--force", "describe"]).ShouldBe(["sqlsource: unknown option '--force'"]);

    [Fact]
    public void Check_OptionThatIsNotRecursive_IsUnknownUnderACommand()
    {
        var root = new Command("sqlsource") { new Option<bool>("--version"), new Command("describe") };

        UsageCheck.Check(root, ["describe", "--version"]).ShouldBe(["sqlsource: unknown option '--version'"]);
    }

    [Fact]
    public void Check_SeveralMistakes_GivesALineForEach() =>
        UsageCheck
            .Check(Root(), ["describe", "--force=" + Secret, "one", Secret, "--connection"])
            .ShouldBe([
                "sqlsource: option '--force' takes no value",
                "sqlsource: unexpected argument at position 4",
                "sqlsource: option '--connection' needs a value",
            ]);

    [Fact]
    public void Check_CommandNameAfterAnArgument_IsAnArgument() =>
        UsageCheck
            .Check(Root(), ["describe", "App.csproj", "describe"])
            .ShouldBe(["sqlsource: unexpected argument at position 3"]);

    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("-" + Secret)]
    public void Check_AnyTokenThatStartsWithAHyphen_IsAnOption(string token) =>
        UsageCheck.Check(Root(), ["describe", token]).ShouldBe([$"sqlsource: unknown option '{token}'"]);
}
```

- [ ] **Step 3: Write the tests of the command**

Replace `tests/SqlSource.Tool.Tests/CliTests.cs` with:

```csharp
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public class CliTests
{
    private const string Secret = "s3cret";

    [Fact]
    public async Task Run_NoArguments_PrintsTheHelp()
    {
        using var run = new CliRun();

        var result = await run.RunAsync();

        result.ExitCode.ShouldBe(0);
        // Nothing here is a text of System.CommandLine, which it writes in the language of the machine.
        result.Out.ShouldContain("Describes the SQL queries of a project");
        result.Out.ShouldContain("--version");
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public async Task Run_Help_ListsTheOptionsThatDoSomething(string option)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(option);

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("Describes the SQL queries of a project");
        result.Out.ShouldContain("--version");
        result.Out.ShouldContain("--help");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Version_PrintsTheInformationalVersionOfTheTool()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldBe(Cli.Version + "\n");
        // The test host has a version of its own, which is what System.CommandLine would print.
        result.Out.ShouldMatch(@"^\d+\.\d+\.\d+");
        result.Out.ShouldStartWith(PackageVersion.Prefix);
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--conection=" + Secret)]
    [InlineData("--conection:" + Secret)]
    [InlineData("--conection", Secret)]
    [InlineData("describe", "--conection", Secret)]
    [InlineData("describe", "--conection=billing=Host=db;Password=" + Secret)]
    public async Task Run_UnknownOption_IsNamedWithoutItsValue(params string[] args)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--conection'\n"));
    }

    [Fact]
    public async Task Run_TwoUnknownOptions_ReportsTheFirstAlone()
    {
        using var run = new CliRun();

        // The second may be the value of the first.
        var result = await run.RunAsync("--pasword", "-" + Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--pasword'\n"));
    }

    [Fact]
    public async Task Run_UnknownOptionAfterAnotherMistake_ReportsTheOptionAlone()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "one", "two", "--oops");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--oops'\n"));
    }

    [Fact]
    public async Task Run_UnknownCommand_IsReportedByItsPosition()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("descrbe");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_ValueForAnOptionThatTakesNone_NamesTheOptionAlone()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--help=" + Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--help' takes no value\n"));
    }

    [Fact]
    public async Task Run_TokenThatStartsWithAnAtSign_IsNotReadAsAFile()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("args.rsp", "--version");

        var result = await run.RunAsync("@args.rsp");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_DoubleHyphen_IsAnUnknownOption()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--", "--version");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--'\n"));
    }

    [Theory]
    [InlineData("[suggest]")]
    [InlineData("/h")]
    [InlineData("sqlsource")]
    public async Task Run_TokenThatSystemCommandLineGivesAMeaning_IsAnArgumentLikeAnyOther(string token)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(token);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_Exception_IsReportedAsSqlsrc200WithoutAStackTrace()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: "
                + "System.InvalidOperationException: the pipe is closed\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200\n"
        );
    }

    [Fact]
    public async Task Run_ExceptionWithTheDebugVariableSet_AddsTheStackTraceBeforeTheLink()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Environment["SQLSOURCE_DEBUG"] = "1";

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(1);
        var lines = result.Error.TrimEnd('\n').Split('\n');
        lines[0].ShouldStartWith("sqlsource : error SQLSRC200: ");
        lines[1].ShouldBe("    trace: System.InvalidOperationException: the pipe is closed");
        lines[2].ShouldStartWith("    trace:    at ");
        lines[^1].ShouldStartWith("    see: ");
        // Every line but the first is a continuation line: nothing of a trace starts in the first column.
        lines[1..].ShouldAllBe(line => line.StartsWith("    ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_ExceptionWithTheDebugVariableEmpty_HasNoStackTrace()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Environment["SQLSOURCE_DEBUG"] = "";

        var result = await run.RunAsync("--version");

        result.Error.ShouldNotContain("trace:");
    }

    [Fact]
    public async Task Run_ExceptionWhoseMessageHasLineBreaks_KeepsTheErrorOnOneLine()
    {
        using var run = new CliRun { Out = new FailingWriter("first\r\nsecond\nthird") };

        var result = await run.RunAsync("--version");

        result.Error.Split('\n')[0].ShouldEndWith("System.InvalidOperationException: first second third");
        result.Error.Split('\n').Length.ShouldBe(3);
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne()
    {
        using var run = new CliRun();

        var result = await run.RunCancelledAsync("--version");

        result.ShouldBe(new CliResult(1, "", ""));
    }

    // Stands in for a standard output that fails: every write throws.
    private sealed class FailingWriter(string message) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new InvalidOperationException(message);
    }
}
```

- [ ] **Step 4: Run the build to see it fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0103 and CS0117: `UsageCheck` does not exist, and `Cli` has no `RunAsync` and no `Version`.

- [ ] **Step 5: Write `UsageCheck`**

`src/SqlSource.Tool/UsageCheck.cs`.  The loop is a `while` because Sonar's S127 objects to a `for` whose body moves its counter:

```csharp
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace SqlSource.Tool;

/// <summary>
/// Finds what is wrong with a command line, before System.CommandLine reads it.
/// </summary>
/// <remarks>
/// System.CommandLine's own messages repeat the token they reject, and a token may be a secret: a misspelt
/// <c>--conection</c> is followed by a connection string.  So the tool writes these lines itself.  A line names an
/// option the tool has, or the name of an unknown option cut at its first <c>=</c> or <c>:</c>, or a position, and
/// never any other text of the command line.
/// </remarks>
internal static class UsageCheck
{
    /// <summary>
    /// The lines to write, each a whole message.  Empty for a command line with nothing wrong.
    /// </summary>
    public static IReadOnlyList<string> Check(Command root, IReadOnlyList<string> args)
    {
        var messages = new List<string>();
        var command = root;
        var options = root.Options.ToList();
        var arguments = 0;

        var index = 0;
        while (index < args.Count)
        {
            var token = args[index++];
            if (token.StartsWith('-'))
            {
                var name = NameOf(token);
                var option = options.Find(option => option.Name == name || option.Aliases.Contains(name));
                if (option is null)
                {
                    // The token after a misspelt option may be its value, so nothing after it is read, and nothing
                    // found before it is reported beside it.
                    return [$"sqlsource: unknown option '{name}'"];
                }

                var hasValue = token.Length > name.Length;
                if (option.Arity.MaximumNumberOfValues == 0)
                {
                    if (hasValue)
                    {
                        messages.Add($"sqlsource: option '{name}' takes no value");
                    }
                }
                else if (!hasValue && option.Arity.MinimumNumberOfValues > 0)
                {
                    // The next token is the value, whatever it starts with.
                    if (index == args.Count)
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }

                    index++;
                }
            }
            else if (arguments == 0 && command.Subcommands.FirstOrDefault(sub => sub.Name == token) is { } sub)
            {
                command = sub;
                options = [.. options.Where(static option => option.Recursive), .. sub.Options];
            }
            else if (arguments < command.Arguments.Sum(static argument => argument.Arity.MaximumNumberOfValues))
            {
                arguments++;
            }
            else
            {
                messages.Add($"sqlsource: unexpected argument at position {index}");
            }
        }

        return messages;
    }

    private static string NameOf(string token)
    {
        var end = token.AsSpan().IndexOfAny('=', ':');
        return end < 0 ? token : token[..end];
    }
}
```

- [ ] **Step 6: Write the command**

Replace `src/SqlSource.Tool/Cli.cs` with:

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>sqlsource</c> command.
/// </summary>
public static class Cli
{
    /// <summary>
    /// The name of the environment variable that adds the stack trace to <c>SQLSRC200</c>.
    /// </summary>
    internal const string DebugVariable = "SQLSOURCE_DEBUG";

    private static readonly ParserConfiguration Parser = new()
    {
        // A token is read as it stands: "@name" is no file to read, and "-ab" is not "-a -b".
        ResponseFileTokenReplacer = null,
        EnablePosixBundling = false,
    };

    /// <summary>
    /// The informational version of the tool's assembly: the package's version and the commit.
    /// </summary>
    internal static string Version { get; } =
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? PackageVersion.Prefix;

    /// <summary>
    /// Runs the command with the console, the current directory and the environment of the process.
    /// </summary>
    /// <param name="args">The command line, without the name of the program.</param>
    /// <returns>
    /// <c>0</c> on success, <c>1</c> when an error was reported, a wrong command line included, and <c>2</c> when
    /// <c>--check</c> found a difference and nothing failed.
    /// </returns>
    public static int Run(string[] args)
    {
        using var interrupt = new CancellationTokenSource();
        void Cancel(object? sender, ConsoleCancelEventArgs e)
        {
            // The run ends by its own road, with its exit code, and not by the process being killed.
            e.Cancel = true;
            interrupt.Cancel();
        }

        Console.CancelKeyPress += Cancel;
        try
        {
            return RunAsync(args, ToolHost.Create(), interrupt.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= Cancel;
        }
    }

    /// <summary>
    /// Runs the command with a host that the caller gives.  The tests call this.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        var reporter = new Reporter(host.Error);
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            var root = BuildCommands();

            var wrong = UsageCheck.Check(root, args);
            if (wrong.Count > 0)
            {
                // These lines have no id and do not go through the reporter.
                foreach (var line in wrong)
                {
                    await host.Error.WriteLineAsync(line);
                }

                return 1;
            }

            var parsed = root.Parse(args, Parser);
            if (parsed.Errors.Count > 0)
            {
                // UsageCheck lets nothing through that System.CommandLine rejects, as far as the tests know.  If
                // something is, System.CommandLine's message is not written: it may repeat a token.
                await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
                return 1;
            }

            var invocation = new InvocationConfiguration
            {
                Output = host.Out,
                Error = host.Error,
                // An exception comes here, to the catch below.
                EnableDefaultExceptionHandler = false,
                // Cli.Run listens for Ctrl+C itself.
                ProcessTerminationTimeout = null,
            };
            var exitCode = await parsed.InvokeAsync(invocation, cancellationToken);
            return reporter.Count > 0 ? 1 : exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception exception)
        {
            reporter.Report(Failure(exception, host));
            return 1;
        }
    }

    private static Command BuildCommands()
    {
        var version = new Option<bool>("--version")
        {
            Description = "Show the version of sqlsource",
            Arity = ArgumentArity.Zero,
            Action = new VersionAction(),
        };

        // Not a RootCommand: that one takes its name from the process, has options of its own under names the tool
        // does not choose, and reads "[suggest]" as a directive.
        var root = new Command("sqlsource", "Describes the SQL queries of a project for the SqlSource generator")
        {
            new HelpOption("--help", "-h", "-?"),
            version,
        };

        // System.CommandLine's own answer to no command is an error.
        root.SetAction(static parsed => new HelpAction().Invoke(parsed));
        return root;
    }

    private static ToolDiagnostic Failure(Exception exception, ToolHost host)
    {
        var failure = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message
        );
        if (string.IsNullOrEmpty(host.GetEnvironmentVariable(DebugVariable)))
        {
            return failure;
        }

        var trace = exception
            .ToString()
            .Split('\n')
            .Select(static line => new ContinuationLine("trace", line.TrimEnd('\r')));
        return failure.WithLines([.. trace]);
    }

    // System.CommandLine's own action for a version reads the assembly the process started with, which under a test
    // is the test host.  An option's action runs in place of the command's.
    private sealed class VersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(Version);
            return 0;
        }
    }
}
```

- [ ] **Step 7: Turn off the two rules that are wrong here**

`.editorconfig`.  CA2007 is off for the folder, and CA1031 for the one file with the one catch:

```diff
--- a/.editorconfig
+++ b/.editorconfig
@@ -225,6 +225,12 @@
 dotnet_diagnostic.IDE0130.severity = none
 
+[src/SqlSource.Tool/**.cs]
+# CA2007 (ConfigureAwait): a console program has no synchronization context.
+dotnet_diagnostic.CA2007.severity = none
+
 [src/SqlSource.Tool/Cli.cs]
 # CA1515 (make types internal): Cli is the tool's public entry, which the end-to-end test projects call.  Any other
 # public type of the tool is a mistake, so the rule stays on for the rest of the project.
 dotnet_diagnostic.CA1515.severity = none
+# CA1031 (catch a specific exception): the one catch that turns any exception into SQLSRC200.
+dotnet_diagnostic.CA1031.severity = none
```

- [ ] **Step 8: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 9: Try it by hand**

```bash
dotnet run --project src/SqlSource.Tool -- --version
```

Expected: `0.1.0-dev+` and the commit id.

```bash
dotnet run --project src/SqlSource.Tool -- --conection=secret
```

Expected: `sqlsource: unknown option '--conection'`, and the exit code 1.

- [ ] **Step 10: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every line of the summary says `passed`.

```bash
git add .editorconfig src/SqlSource.Tool tests/SqlSource.Tool.Tests
```

```bash
git commit -F - <<'EOF'
Add the command line of sqlsource, --version and --help

The tool finds what is wrong with a command line itself, before
System.CommandLine reads it, and never repeats a token: an unknown
option is named up to its first = or :, and anything else by its
position.  Only the first unknown option is reported, since the token
after it may be its value.  Response files and bundling are off.

An exception is caught in Cli and reported as SQLSRC200, with the stack
trace as continuation lines when SQLSOURCE_DEBUG is set.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 5: `describe`, the unit of a run, and `SQLSRC201` to `SQLSRC203`

**Files:**
- Create: `src/SqlSource.Tool/Projects/RunUnitKind.cs`, `RunUnit.cs`, `RunUnitFinder.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (replace), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, `src/SqlSource.Tool/Cli.cs` (replace)
- Test: `tests/SqlSource.Tool.Tests/RunUnitTests.cs`, `tests/SqlSource.Tool.Tests/DescribeTests.cs` (create)

**Interfaces:**
- Consumes: `Reporter`, `ToolDiagnostic.Create`, `ToolHost`, `CliRun`, `TempFolder` and `CliResult` from tasks 3 and 4.
- Produces, in `ToolDiagnostics`: `NoRunUnit` (`SQLSRC201`), `SeveralRunUnits` (`SQLSRC202`), `NotARunUnit` (`SQLSRC203`), each with one argument, a full path.
- Produces, in `SqlSource.Tool.Projects`: `internal enum RunUnitKind { Solution, Project }`; `internal sealed record RunUnit(RunUnitKind Kind, string Path)`; `internal static RunUnit? RunUnitFinder.Find(string? argument, string workingDirectory, Reporter reporter)`, which returns null after reporting.

- [ ] **Step 1: Write the tests of the finder**

`tests/SqlSource.Tool.Tests/RunUnitTests.cs`.  A kind is passed to a test as its name, because a public test method cannot take the internal enum:

```csharp
using System;
using System.Globalization;
using System.IO;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class RunUnitTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose()
    {
        _folder.Dispose();
        _error.Dispose();
    }

    private RunUnit? Find(string? argument) => RunUnitFinder.Find(argument, _folder.Path, new Reporter(_error));

    private void ShouldHaveReported(string id, string path)
    {
        var lines = _error.ToString().Split('\n');
        lines.Length.ShouldBe(3, _error.ToString());
        lines[0].ShouldStartWith($"sqlsource : error {id}: '{path}' ");
    }

    [Theory]
    [InlineData("App.csproj", nameof(RunUnitKind.Project))]
    [InlineData("App.sln", nameof(RunUnitKind.Solution))]
    [InlineData("App.slnx", nameof(RunUnitKind.Solution))]
    [InlineData("App.CSPROJ", nameof(RunUnitKind.Project))]
    [InlineData("App.Sln", nameof(RunUnitKind.Solution))]
    [InlineData("APP.SLNX", nameof(RunUnitKind.Solution))]
    public void Find_NoArgument_IsTheOneUnitOfTheWorkingDirectory(string file, string kind)
    {
        var path = _folder.WriteFile(file);
        _ = _folder.WriteFile("notes.txt");
        _ = _folder.WriteFile("App.slnf");

        Find(null).ShouldBe(new RunUnit(Enum.Parse<RunUnitKind>(kind), path));
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Find_NoArgumentAndNoUnit_IsSqlsrc201()
    {
        _ = _folder.WriteFile("App.fsproj");
        _ = _folder.WriteFile("App.slnf");

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC201", _folder.Path);
    }

    [Theory]
    [InlineData("One.csproj", "Two.csproj")]
    [InlineData("One.sln", "Two.sln")]
    [InlineData("App.sln", "App.csproj")]
    [InlineData("App.sln", "App.slnx")]
    public void Find_NoArgumentAndSeveralUnits_IsSqlsrc202(string first, string second)
    {
        _ = _folder.WriteFile(first);
        _ = _folder.WriteFile(second);

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC202", _folder.Path);
    }

    [Fact]
    public void Find_FolderNamedLikeAProject_IsNotAUnit()
    {
        _ = _folder.CreateFolder("Nested.csproj");
        var path = _folder.WriteFile("App.csproj");

        Find(null).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_UnitInAFolderBelow_IsNotFound()
    {
        _ = _folder.WriteFile("src/App/App.csproj");

        Find(null).ShouldBeNull();
        ShouldHaveReported("SQLSRC201", _folder.Path);
    }

    [Theory]
    [InlineData("src/App")]
    [InlineData("src/App/")]
    [InlineData("./src/App")]
    [InlineData("src/Other/../App")]
    public void Find_RelativeDirectory_IsTheOneUnitInIt(string argument)
    {
        var path = _folder.WriteFile("src/App/App.csproj");
        _ = _folder.WriteFile("Whole.sln");

        Find(argument).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_Dot_IsTheWorkingDirectory()
    {
        var path = _folder.WriteFile("Whole.slnx");

        Find(".").ShouldBe(new RunUnit(RunUnitKind.Solution, path));
    }

    [Fact]
    public void Find_AbsoluteDirectory_IsTheOneUnitInIt()
    {
        var path = _folder.WriteFile("src/App/App.csproj");

        Find(_folder.PathOf("src/App")).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Fact]
    public void Find_DirectoryWithoutAUnit_IsSqlsrc201WithItsFullPath()
    {
        var directory = _folder.CreateFolder("src/Empty");

        Find("src/Empty/").ShouldBeNull();
        ShouldHaveReported("SQLSRC201", directory);
    }

    [Fact]
    public void Find_DirectoryWithSeveralUnits_IsSqlsrc202WithItsFullPath()
    {
        _ = _folder.WriteFile("src/Two/One.csproj");
        _ = _folder.WriteFile("src/Two/Two.csproj");

        Find("src/Two").ShouldBeNull();
        ShouldHaveReported("SQLSRC202", _folder.PathOf("src/Two"));
    }

    [Theory]
    [InlineData("src/App/App.csproj", nameof(RunUnitKind.Project))]
    [InlineData("Whole.sln", nameof(RunUnitKind.Solution))]
    [InlineData("Whole.SLNX", nameof(RunUnitKind.Solution))]
    public void Find_RelativeFile_IsThatFile(string file, string kind)
    {
        var path = _folder.WriteFile(file);
        // A file that is named is the unit, whatever lies beside it.
        _ = _folder.WriteFile(Path.Combine(Path.GetDirectoryName(file)!, "Other.csproj"));

        Find(file).ShouldBe(new RunUnit(Enum.Parse<RunUnitKind>(kind), path));
    }

    [Fact]
    public void Find_AbsoluteFile_IsThatFile()
    {
        var path = _folder.WriteFile("src/App/App.csproj");

        Find(path).ShouldBe(new RunUnit(RunUnitKind.Project, path));
    }

    [Theory]
    [InlineData("Missing.csproj")]
    [InlineData("missing/folder")]
    public void Find_PathThatDoesNotExist_IsSqlsrc203WithItsFullPath(string argument)
    {
        Find(argument).ShouldBeNull();
        ShouldHaveReported("SQLSRC203", _folder.PathOf(argument));
    }

    [Theory]
    [InlineData("App.slnf")]
    [InlineData("App.fsproj")]
    [InlineData("App.csproj.user")]
    [InlineData("csproj")]
    public void Find_FileOfAnotherKind_IsSqlsrc203(string file)
    {
        var path = _folder.WriteFile(file);

        Find(file).ShouldBeNull();
        ShouldHaveReported("SQLSRC203", path);
    }

    [Fact]
    public void Find_EmptyArgument_IsSqlsrc203AndNotTheWorkingDirectory()
    {
        // What "$UNSET" gives in a shell.  It must not run on whatever the working directory holds.
        _ = _folder.WriteFile("App.csproj");

        Find("").ShouldBeNull();
        ShouldHaveReported("SQLSRC203", "");
    }
}
```

- [ ] **Step 2: Write the tests of the command**

`tests/SqlSource.Tool.Tests/DescribeTests.cs`:

```csharp
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The describe command from the command line to the exit code.  RunUnitTests holds every case of finding the unit.
public class DescribeTests
{
    private const string Secret = "s3cret";

    [Fact]
    public async Task Run_HelpOfTheTool_ListsTheCommand()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("describe");
    }

    [Fact]
    public async Task Run_Help_ShowsThePath()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>]");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_VersionBeforeACommand_PrintsTheVersionAndRunsNothing()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--version", "describe");

        result.ShouldBe(new CliResult(0, Cli.Version + "\n", ""));
    }

    [Fact]
    public async Task Run_UnknownOptionWhereThePathCouldStand_IsNotTakenForAPath()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("-x/App.csproj");

        var result = await run.RunAsync("describe", "-x");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '-x'\n"));
    }

    [Fact]
    public async Task Run_PathThatStartsWithAHyphen_IsGivenWithAFolderBeforeIt()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("-x/App.csproj");

        var result = await run.RunAsync("describe", "./-x");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_SecondPath_IsReportedByItsPositionAndNotItsText()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "App.csproj", Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 3\n"));
    }

    [Fact]
    public async Task Run_UnitFound_PrintsNothing()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("App.csproj");

        var result = await run.RunAsync("describe");

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_NoUnit_ReportsItAndExitsWithOne()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldBe(
            $"sqlsource : error SQLSRC201: '{run.Folder.Path}' holds no .sln, .slnx or .csproj file\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc201\n"
        );
    }
}
```

- [ ] **Step 3: Run the build to see it fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0246, `RunUnit`, `RunUnitKind` and `RunUnitFinder` do not exist.

- [ ] **Step 4: Add the three descriptors, in their four places**

Replace `src/SqlSource/Diagnostics/ToolDiagnostics.cs` with:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SqlSource.Diagnostics;

/// <summary>
/// Every error that only the <c>sqlsource</c> tool reports.  The generator reports none of them: they are here so
/// that the release tracking and the tests of <c>docs/diagnostics.md</c> cover them with the generator's own.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and <c>docs/diagnostics.md</c>.
/// </remarks>
internal static class ToolDiagnostics
{
    public static readonly DiagnosticDescriptor UnexpectedFailure = new(
        id: "SQLSRC200",
        title: "The tool failed unexpectedly",
        messageFormat: "sqlsource failed unexpectedly: {0}: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc200",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NoRunUnit = new(
        id: "SQLSRC201",
        title: "No project or solution found",
        messageFormat: "'{0}' holds no .sln, .slnx or .csproj file",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc201",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SeveralRunUnits = new(
        id: "SQLSRC202",
        title: "More than one project or solution found",
        messageFormat: "'{0}' holds more than one .sln, .slnx or .csproj file.  Name the one to run on.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc202",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NotARunUnit = new(
        id: "SQLSRC203",
        title: "Path is not a project or a solution",
        messageFormat: "'{0}' is not a .sln, .slnx or .csproj file, or a directory that holds one",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc203",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>
    /// Every descriptor, in the order of its id.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(UnexpectedFailure, NoRunUnit, SeveralRunUnits, NotARunUnit);
}
```

`src/SqlSource/AnalyzerReleases.Unshipped.md`:

```diff
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -39,2 +39,5 @@
 SQLSRC119 | SqlSource | Error | Query has no parameters
 SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
+SQLSRC201 | SqlSource | Error | No project or solution found
+SQLSRC202 | SqlSource | Error | More than one project or solution found
+SQLSRC203 | SqlSource | Error | Path is not a project or a solution
```

`docs/diagnostics.md`, three rows of the table and three sections at the end:

````diff
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -38,4 +38,7 @@
 | [SQLSRC119](#sqlsrc119) | Query has no parameters |
 | [SQLSRC200](#sqlsrc200) | The tool failed unexpectedly |
+| [SQLSRC201](#sqlsrc201) | No project or solution found |
+| [SQLSRC202](#sqlsrc202) | More than one project or solution found |
+| [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
 
 Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.  Ids from 200 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1.
@@ -539,2 +542,34 @@
 
 When the message names something of your machine, as this one does, fix that.  Otherwise it is a bug in the tool: set the environment variable `SQLSOURCE_DEBUG` to any value, run the command again, and [report it](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.
+
+## SQLSRC201
+
+**No project or solution found**
+
+`sqlsource describe` was given a directory, or none and so the current one, that holds no `.sln`, `.slnx` or `.csproj` file.  The tool looks in that directory alone, not in the folders below it.
+
+Run the command from the folder of the project or the solution, or name the one to run on:
+
+```console
+$ dotnet sqlsource describe src/App/App.csproj
+```
+
+## SQLSRC202
+
+**More than one project or solution found**
+
+`sqlsource describe` was given a directory, or none and so the current one, that holds more than one `.sln`, `.slnx` or `.csproj` file.  The tool does not choose between them, as `dotnet build` does not.
+
+Name the one to run on:
+
+```console
+$ dotnet sqlsource describe App.slnx
+```
+
+## SQLSRC203
+
+**Path is not a project or a solution**
+
+The path given to `sqlsource describe` does not exist, or is a file that is not a `.sln`, `.slnx` or `.csproj` file.  A solution filter, `.slnf`, and a project of another language are not read.  The message holds the full path the tool looked at: a relative path is resolved against the current directory.  An empty path is this error too, which is what a variable that is not set gives in a shell.
+
+Give the path of a solution, of a C# project, or of a directory that holds exactly one of them.
````

- [ ] **Step 5: Write the unit and its finder**

`src/SqlSource.Tool/Projects/RunUnitKind.cs`:

```csharp
namespace SqlSource.Tool.Projects;

/// <summary>
/// What a run is on.
/// </summary>
internal enum RunUnitKind
{
    /// <summary>A <c>.sln</c> or <c>.slnx</c> file.</summary>
    Solution,

    /// <summary>A <c>.csproj</c> file.</summary>
    Project,
}
```

`src/SqlSource.Tool/Projects/RunUnit.cs`:

```csharp
namespace SqlSource.Tool.Projects;

/// <summary>
/// The one solution or project that a run is on.
/// </summary>
/// <param name="Kind">Which of the two.</param>
/// <param name="Path">The full path of the file.</param>
internal sealed record RunUnit(RunUnitKind Kind, string Path);
```

`src/SqlSource.Tool/Projects/RunUnitFinder.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Finds the unit of a run from the path on the command line, the way <c>dotnet build</c> finds what to build.
/// </summary>
internal static class RunUnitFinder
{
    /// <summary>
    /// The unit, or null after reporting why there is none.
    /// </summary>
    /// <param name="argument">The path as given, or null when none was.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    /// <param name="reporter">Where the error goes.</param>
    public static RunUnit? Find(string? argument, string workingDirectory, Reporter reporter)
    {
        // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
        if (argument is { Length: 0 })
        {
            reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NotARunUnit, argument));
            return null;
        }

        var path = Path.TrimEndingDirectorySeparator(
            argument is null ? workingDirectory : Path.GetFullPath(argument, workingDirectory)
        );

        if (Directory.Exists(path))
        {
            return FindIn(path, reporter);
        }

        if (File.Exists(path) && KindOf(path) is { } kind)
        {
            return new RunUnit(kind, path);
        }

        reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NotARunUnit, path));
        return null;
    }

    private static RunUnit? FindIn(string directory, Reporter reporter)
    {
        // Only files: a folder named App.csproj is not a project.
        var units = Directory
            .EnumerateFiles(directory)
            .Select(static file => (File: file, Kind: KindOf(file)))
            .Where(static unit => unit.Kind is not null)
            .Take(2)
            .ToArray();

        switch (units)
        {
            case [{ Kind: { } kind } unit]:
                return new RunUnit(kind, unit.File);
            case []:
                reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NoRunUnit, directory));
                return null;
            default:
                reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.SeveralRunUnits, directory));
                return null;
        }
    }

    private static RunUnitKind? KindOf(string path)
    {
        var extension = Path.GetExtension(path.AsSpan());
        if (
            extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
        )
        {
            return RunUnitKind.Solution;
        }

        return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ? RunUnitKind.Project : null;
    }
}
```

- [ ] **Step 6: Add the command**

Replace `src/SqlSource.Tool/Cli.cs` with the file below.  Against task 4 it has one more `using`, `BuildCommands` takes the host and the reporter and adds `Describe(host, reporter)` to the root, and the method `Describe` is new:

```csharp
using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>sqlsource</c> command.
/// </summary>
public static class Cli
{
    /// <summary>
    /// The name of the environment variable that adds the stack trace to <c>SQLSRC200</c>.
    /// </summary>
    internal const string DebugVariable = "SQLSOURCE_DEBUG";

    private static readonly ParserConfiguration Parser = new()
    {
        // A token is read as it stands: "@name" is no file to read, and "-ab" is not "-a -b".
        ResponseFileTokenReplacer = null,
        EnablePosixBundling = false,
    };

    /// <summary>
    /// The informational version of the tool's assembly: the package's version and the commit.
    /// </summary>
    internal static string Version { get; } =
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? PackageVersion.Prefix;

    /// <summary>
    /// Runs the command with the console, the current directory and the environment of the process.
    /// </summary>
    /// <param name="args">The command line, without the name of the program.</param>
    /// <returns>
    /// <c>0</c> on success, <c>1</c> when an error was reported, a wrong command line included, and <c>2</c> when
    /// <c>--check</c> found a difference and nothing failed.
    /// </returns>
    public static int Run(string[] args)
    {
        using var interrupt = new CancellationTokenSource();
        void Cancel(object? sender, ConsoleCancelEventArgs e)
        {
            // The run ends by its own road, with its exit code, and not by the process being killed.
            e.Cancel = true;
            interrupt.Cancel();
        }

        Console.CancelKeyPress += Cancel;
        try
        {
            return RunAsync(args, ToolHost.Create(), interrupt.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= Cancel;
        }
    }

    /// <summary>
    /// Runs the command with a host that the caller gives.  The tests call this.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        var reporter = new Reporter(host.Error);
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            var root = BuildCommands(host, reporter);

            var wrong = UsageCheck.Check(root, args);
            if (wrong.Count > 0)
            {
                // These lines have no id and do not go through the reporter.
                foreach (var line in wrong)
                {
                    await host.Error.WriteLineAsync(line);
                }

                return 1;
            }

            var parsed = root.Parse(args, Parser);
            if (parsed.Errors.Count > 0)
            {
                // UsageCheck lets nothing through that System.CommandLine rejects, as far as the tests know.  If
                // something is, System.CommandLine's message is not written: it may repeat a token.
                await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
                return 1;
            }

            var invocation = new InvocationConfiguration
            {
                Output = host.Out,
                Error = host.Error,
                // An exception comes here, to the catch below.
                EnableDefaultExceptionHandler = false,
                // Cli.Run listens for Ctrl+C itself.
                ProcessTerminationTimeout = null,
            };
            var exitCode = await parsed.InvokeAsync(invocation, cancellationToken);
            return reporter.Count > 0 ? 1 : exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception exception)
        {
            reporter.Report(Failure(exception, host));
            return 1;
        }
    }

    private static Command BuildCommands(ToolHost host, Reporter reporter)
    {
        var version = new Option<bool>("--version")
        {
            Description = "Show the version of sqlsource",
            Arity = ArgumentArity.Zero,
            Action = new VersionAction(),
        };

        // Not a RootCommand: that one takes its name from the process, has options of its own under names the tool
        // does not choose, and reads "[suggest]" as a directive.
        var root = new Command("sqlsource", "Describes the SQL queries of a project for the SqlSource generator")
        {
            new HelpOption("--help", "-h", "-?"),
            version,
            Describe(host, reporter),
        };

        // System.CommandLine's own answer to no command is an error.
        root.SetAction(static parsed => new HelpAction().Invoke(parsed));
        return root;
    }

    private static Command Describe(ToolHost host, Reporter reporter)
    {
        var path = new Argument<string?>("path")
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one.  The current directory "
                + "when left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var describe = new Command("describe", "Finds the project or the solution to describe") { path };
        describe.SetAction(parsed =>
        {
            // The unit is all this sub-phase finds.  An error of it is in the reporter, where the exit code is taken.
            _ = RunUnitFinder.Find(parsed.GetValue(path), host.WorkingDirectory, reporter);
            return 0;
        });
        return describe;
    }

    private static ToolDiagnostic Failure(Exception exception, ToolHost host)
    {
        var failure = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message
        );
        if (string.IsNullOrEmpty(host.GetEnvironmentVariable(DebugVariable)))
        {
            return failure;
        }

        var trace = exception
            .ToString()
            .Split('\n')
            .Select(static line => new ContinuationLine("trace", line.TrimEnd('\r')));
        return failure.WithLines([.. trace]);
    }

    // System.CommandLine's own action for a version reads the assembly the process started with, which under a test
    // is the test host.  An option's action runs in place of the command's.
    private sealed class VersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(Version);
            return 0;
        }
    }
}
```

- [ ] **Step 7: Run every test**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  The document test of `tests/SqlSource.Tests` passes with the three new sections.

- [ ] **Step 8: Try it by hand**

```bash
dotnet run --project src/SqlSource.Tool -- describe
```

Expected: nothing, and the exit code 0: the root of the repository holds `SqlSource.slnx` alone.

```bash
dotnet run --project src/SqlSource.Tool -- describe src
```

Expected: `sqlsource : error SQLSRC201: '<the repository>/src' holds no .sln, .slnx or .csproj file`, the `see:` line under it, and the exit code 1.

- [ ] **Step 9: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every line of the summary says `passed`.

```bash
git add src/SqlSource docs/diagnostics.md src/SqlSource.Tool tests/SqlSource.Tool.Tests
```

```bash
git commit -F - <<'EOF'
Add describe, which finds the unit of a run

The unit is the one .sln, .slnx or .csproj file of the working
directory or of a directory given, or the file given.  None is
SQLSRC201, more than one is SQLSRC202, and a path that is neither is
SQLSRC203, each with the full path.  describe stops there and exits 0.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 6: The package checks

**Files:**
- Create: `tools/check-tool-install.sh`
- Modify: `tools/check-package.sh` (replace), `tools/check-package-install.sh`, `pre-commit-validation.sh`, `.github/workflows/build.yml`

**Interfaces:**
- Consumes: the packed tool's behaviour from tasks 4 and 5: `--version` prints the package's version, alone or followed by `+` and the commit; `--help` lists `describe`; `describe` in an empty folder exits `1` with a line that starts `sqlsource : error SQLSRC201: ` on standard error and nothing on standard output.
- Produces: a step `tool-install` in `pre-commit-validation.sh`, and `Check tool install` in `build.yml`.

What the scratch build showed of the package: `dotnet pack` puts the tool under `tools/net8.0/any/`, with `SqlSource.dll`, `Microsoft.CodeAnalysis.CSharp.dll`, `System.CommandLine.dll` and `DotnetToolSettings.xml` beside `SqlSource.Tool.dll`, and its `runtimeconfig.json` says `"rollForward": "Major"`.  `dotnet pack SqlSource.slnx --no-build`, which CI runs, writes both packages.

- [ ] **Step 1: See the old check fail on two packages**

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

```bash
tools/check-package.sh artifacts/packages
```

Expected: FAIL, `expected one SqlSource.*.nupkg in ..., found 2`.  This is what CI would say.

- [ ] **Step 2: Check both packages**

Replace `tools/check-package.sh` with:

```bash
#!/usr/bin/env bash
# Checks the contents of the two packages.  SqlSource: the generator, the two MSBuild files that hand .sql files and
# the settings to the compiler, the readme, and nothing under lib/.  SqlSource.Tool: the tool with the generator's
# assembly and Roslyn beside it, the settings that name its command, and the readme.  Both have one version.
# Usage: check-package.sh [directory]   The directory holds one SqlSource.<version>.nupkg and one
# SqlSource.Tool.<version>.nupkg.  Without it, both are packed into a temporary directory first.
set -euo pipefail

GENERATOR_ID='SqlSource'
GENERATOR_REQUIRED=('build/SqlSource.props' 'build/SqlSource.targets' 'analyzers/dotnet/cs/SqlSource.dll' 'README.md')
TOOL_ID='SqlSource.Tool'
# A tool carries everything it loads: tools/check-tool-install.sh is what shows that the whole of it runs.
TOOL_REQUIRED=(
    'tools/net8.0/any/SqlSource.Tool.dll'
    'tools/net8.0/any/SqlSource.dll'
    'tools/net8.0/any/Microsoft.CodeAnalysis.CSharp.dll'
    'tools/net8.0/any/DotnetToolSettings.xml'
    'README.md'
)

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-package.sh [directory]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -eq 1 ]]; then
    # Resolved before the working directory changes, so that a relative path means what the caller meant.
    directory="$(cd "$1" && pwd)"
    cd "$repo_root"
else
    cd "$repo_root"
    directory="$(mktemp -d)"
    trap 'rm -rf "$directory"' EXIT
    dotnet pack src/SqlSource/SqlSource.csproj --output "$directory" --nologo --verbosity quiet
    dotnet pack src/SqlSource.Tool/SqlSource.Tool.csproj --output "$directory" --nologo --verbosity quiet
fi

status=0

# Prints the path of the one package of an id.  A version starts with a digit, and that is what keeps the pattern of
# SqlSource from matching SqlSource.Tool.<version>.nupkg as well.
find_package() {
    local id="$1"
    local found=()
    local package
    for package in "$directory/$id".[0-9]*.nupkg; do
        if [[ -f "$package" ]]; then
            found+=("$package")
        fi
    done

    if [[ ${#found[@]} -ne 1 ]]; then
        echo "check-package: expected one $id.<version>.nupkg in $directory, found ${#found[@]}" >&2
        return 1
    fi
    echo "${found[0]}"
}

# Fails for each required entry that a package lacks.
check_entries() {
    local package="$1"
    shift
    local entries
    entries="$(unzip -Z1 "$package")"
    local required
    for required in "$@"; do
        if ! grep --quiet --line-regexp --fixed-strings "$required" <<<"$entries"; then
            echo "check-package: $package lacks $required" >&2
            status=1
        fi
    done
}

generator="$(find_package "$GENERATOR_ID")" || exit 1
tool="$(find_package "$TOOL_ID")" || exit 1

check_entries "$generator" "${GENERATOR_REQUIRED[@]}"
check_entries "$tool" "${TOOL_REQUIRED[@]}"

# The generator is loaded by the compiler from analyzers/.  A file under lib/ would become a reference of the consumer.
if unzip -Z1 "$generator" | grep --quiet '^lib/'; then
    echo "check-package: $generator holds files under lib/" >&2
    status=1
fi

# The two are released together, and a sidecar records the version of the tool for the generator to compare.
generator_version="$(basename "$generator" .nupkg)"
generator_version="${generator_version#"$GENERATOR_ID".}"
tool_version="$(basename "$tool" .nupkg)"
tool_version="${tool_version#"$TOOL_ID".}"
if [[ "$generator_version" != "$tool_version" ]]; then
    echo "check-package: $GENERATOR_ID is $generator_version and $TOOL_ID is $tool_version" >&2
    status=1
fi

if [[ "$status" -eq 0 ]]; then
    echo "check-package: $(basename "$generator") holds ${GENERATOR_REQUIRED[*]}"
    echo "check-package: $(basename "$tool") holds ${TOOL_REQUIRED[*]}"
fi
exit "$status"
```

Run: `tools/check-package.sh artifacts/packages`
Expected: two lines, one for each package.

- [ ] **Step 3: Find the generator's package beside the tool's**

`tools/check-package-install.sh`:

```diff
--- a/tools/check-package-install.sh
+++ b/tools/check-package-install.sh
@@ -4,6 +4,6 @@
 # The project is tools/package-install.  It is copied out of the repository first, so that nothing of the repository's
 # own build applies to it, and it restores from the folder that holds the package and from nowhere else.
-# Usage: check-package-install.sh [directory]   The directory holds one SqlSource.*.nupkg.  Without it, the package is
-# packed into a temporary directory first.
+# Usage: check-package-install.sh [directory]   The directory holds one SqlSource.<version>.nupkg, and may hold the
+# package of the tool beside it.  Without it, the package is packed into a temporary directory first.
 set -euo pipefail
 
@@ -35,6 +35,7 @@
 fi
 
+# A version starts with a digit, and that is what keeps the pattern from matching SqlSource.Tool.<version>.nupkg.
 packages=()
-for package in "$directory/$PACKAGE_ID".*.nupkg; do
+for package in "$directory/$PACKAGE_ID".[0-9]*.nupkg; do
     if [[ -f "$package" ]]; then
         packages+=("$package")
@@ -43,5 +44,5 @@
 
 if [[ ${#packages[@]} -ne 1 ]]; then
-    echo "check-package-install: expected one $PACKAGE_ID.*.nupkg in $directory, found ${#packages[@]}" >&2
+    echo "check-package-install: expected one $PACKAGE_ID.<version>.nupkg in $directory, found ${#packages[@]}" >&2
     exit 1
 fi
```

Run: `tools/check-package-install.sh artifacts/packages`
Expected: its two closing lines, as before.

- [ ] **Step 4: Install the tool and run it**

`tools/check-tool-install.sh`:

```bash
#!/usr/bin/env bash
# Installs the packed sqlsource tool the way a user does, into an empty folder outside the repository, and runs it.
# A tool carries everything it loads, so only a run of the packed tool shows that all of it is there and loads, and
# that a tool built for .NET 8 starts on a machine whose only runtime is a later one.
# It restores from the folder that holds the package and from nowhere else.
# Usage: check-tool-install.sh [directory]   The directory holds one SqlSource.Tool.<version>.nupkg.  Without it, the
# package is packed into a temporary directory first.
set -euo pipefail

PACKAGE_ID='SqlSource.Tool'
COMMAND='sqlsource'

if [[ $# -gt 1 ]]; then
    echo 'Usage: check-tool-install.sh [directory]' >&2
    exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ $# -eq 1 ]]; then
    # Resolved before the working directory changes, so that a relative path means what the caller meant.
    directory="$(cd "$1" && pwd)"
    cd "$repo_root"
else
    cd "$repo_root"
    directory="$work/feed"
    dotnet pack src/SqlSource.Tool/SqlSource.Tool.csproj --output "$directory" --nologo --verbosity quiet
fi

packages=()
for package in "$directory/$PACKAGE_ID".[0-9]*.nupkg; do
    if [[ -f "$package" ]]; then
        packages+=("$package")
    fi
done

if [[ ${#packages[@]} -ne 1 ]]; then
    echo "check-tool-install: expected one $PACKAGE_ID.<version>.nupkg in $directory, found ${#packages[@]}" >&2
    exit 1
fi

package="$(basename "${packages[0]}")"
version="${package#"$PACKAGE_ID".}"
version="${version%.nupkg}"

install="$work/install"
mkdir "$install"
# The same SDK as the repository, whatever else is installed.
cp global.json "$install/"

# The package folder is the only source, so the package under test is the one that is installed.  The packages folder
# is inside the temporary directory because a version is unpacked into it once: in the folder of the user, a package
# packed again with the same version would not replace the one from the last run.
cat >"$install/nuget.config" <<CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <config>
        <add key="globalPackagesFolder" value="$work/packages" />
    </config>
    <packageSources>
        <clear />
        <add key="package-under-test" value="$directory" />
    </packageSources>
    <packageSourceMapping>
        <clear />
    </packageSourceMapping>
    <fallbackPackageFolders>
        <clear />
    </fallbackPackageFolders>
</configuration>
CONFIG

cd "$install"
# pre-commit-validation.sh sets locked mode for the repository's projects.  The install has no lock file.
unset RestoreLockedMode

dotnet tool install "$PACKAGE_ID" --version "$version" --tool-path "$work/tool" --configfile nuget.config \
    >"$work/install.log" 2>&1 || {
    cat "$work/install.log" >&2
    echo "check-tool-install: $package could not be installed" >&2
    exit 1
}

tool="$work/tool/$COMMAND"

# Runs the tool and fails unless it prints the package's version, alone or followed by "+" and the commit.
check_version() {
    local what="$1"
    local actual
    actual="$("$tool" --version)" || {
        echo "check-tool-install: $COMMAND --version failed $what" >&2
        exit 1
    }
    if [[ "$actual" != "$version" && "$actual" != "$version+"* ]]; then
        echo "check-tool-install: $COMMAND --version printed '$actual' $what, and the package is $version" >&2
        exit 1
    fi
}

check_version 'on the runtime it chose'

"$tool" --help >"$work/help.txt" || {
    cat "$work/help.txt" >&2
    echo "check-tool-install: $COMMAND --help failed" >&2
    exit 1
}
if ! grep --quiet --fixed-strings 'describe' "$work/help.txt"; then
    cat "$work/help.txt" >&2
    echo "check-tool-install: $COMMAND --help does not list describe" >&2
    exit 1
fi

# A folder with no project in it: the tool must find nothing, say so in the compiler's format, and exit with 1.
mkdir "$work/empty"
cd "$work/empty"
code=0
"$tool" describe >"$work/describe.out" 2>"$work/describe.err" || code=$?
if [[ "$code" -ne 1 ]] || [[ -s "$work/describe.out" ]] ||
    ! grep --quiet '^sqlsource : error SQLSRC201: ' "$work/describe.err"; then
    cat "$work/describe.out" "$work/describe.err" >&2
    echo "check-tool-install: $COMMAND describe in an empty folder exited with $code, not with 1 and SQLSRC201" >&2
    exit 1
fi

# A machine that has a .NET 8 runtime ran the tool on it above.  This run takes the newest runtime the machine has,
# which is what a machine without .NET 8 does by the RollForward of the project.
export DOTNET_ROLL_FORWARD=LatestMajor
check_version 'on the newest runtime'

echo "check-tool-install: $package installs as a tool, and $COMMAND runs: --version, --help, and describe, which"
echo "check-tool-install: reports SQLSRC201 in an empty folder.  It runs on the newest runtime as well."
```

```bash
chmod +x tools/check-tool-install.sh
```

Run: `tools/check-tool-install.sh artifacts/packages`, then `tools/check-tool-install.sh` with no argument.
Expected: both end with `It runs on the newest runtime as well.`

- [ ] **Step 5: See the check catch a broken tool**

Prove that the script fails when the tool does.  In `src/SqlSource.Tool/Projects/RunUnitFinder.cs`, change `ToolDiagnostics.NoRunUnit` to `ToolDiagnostics.SeveralRunUnits` in `FindIn`, and run `tools/check-tool-install.sh`.
Expected: FAIL, `sqlsource describe in an empty folder exited with 1, not with 1 and SQLSRC201`.
Undo the change: `git checkout src/SqlSource.Tool/Projects/RunUnitFinder.cs`.

- [ ] **Step 6: Add the step to the validation and to CI**

`pre-commit-validation.sh`:

```diff
--- a/pre-commit-validation.sh
+++ b/pre-commit-validation.sh
@@ -1,4 +1,4 @@
 #!/usr/bin/env bash
-# Runs every validation on the whole repository: formatting, linting, build, tests and the two package checks.
+# Runs every validation on the whole repository: formatting, linting, build, tests and the three package checks.
 # Rewrites nothing.  Every step runs even when an earlier one fails, except that the tests and the package checks are
 # skipped when the build fails.
@@ -45,6 +45,7 @@
     run_step 'package' tools/check-package.sh || true
     run_step 'package-install' tools/check-package-install.sh || true
+    run_step 'tool-install' tools/check-tool-install.sh || true
 else
-    results+=('skipped  test' 'skipped  package' 'skipped  package-install')
+    results+=('skipped  test' 'skipped  package' 'skipped  package-install' 'skipped  tool-install')
 fi
 
```

`.github/workflows/build.yml`.  The coverage report takes the tool's assembly as well:

```diff
--- a/.github/workflows/build.yml
+++ b/.github/workflows/build.yml
@@ -103,5 +103,5 @@
               -targetdir:artifacts/coverage \
               '-reporttypes:Html;Cobertura;MarkdownSummaryGithub' \
-              '-assemblyfilters:+SqlSource'
+              '-assemblyfilters:+SqlSource;+SqlSource.Tool'
           cat artifacts/coverage/SummaryGithub.md >> "$GITHUB_STEP_SUMMARY"
 
@@ -123,4 +123,7 @@
         run: tools/check-package-install.sh artifacts/packages
 
+      - name: Check tool install
+        run: tools/check-tool-install.sh artifacts/packages
+
       - name: Upload packages
         uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
```

Run: `tools/actionlint.sh`
Expected: no output.

- [ ] **Step 7: Format the scripts, validate, commit**

Run: `tools/shfmt.sh`, then `rm -rf artifacts`, then `./pre-commit-validation.sh`.
Expected: `tools/shfmt.sh` changes nothing, and the summary has a tenth line, `passed   tool-install`.

```bash
git add tools pre-commit-validation.sh .github/workflows/build.yml
```

```bash
git commit -F - <<'EOF'
Check the tool's package and install the packed tool

check-package.sh checks both packages and that they have one version.
check-tool-install.sh installs the tool into a temporary folder from a
feed that holds nothing else, runs --version, --help and describe, and
runs it once more on the newest runtime the machine has.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 7: Publishing

**Files:**
- Modify: `.github/workflows/publish.yml`, `docs/publishing.md`

`publish.yml` cannot run from a branch: it is first exercised on `main`.  The push and the release already take every package of the folder, so only the check of a tag changes.

- [ ] **Step 1: Expect the two packages of the tag's version**

`.github/workflows/publish.yml`.  The file keeps its name and the job keeps `environment: nuget`:

```diff
--- a/.github/workflows/publish.yml
+++ b/.github/workflows/publish.yml
@@ -30,5 +30,5 @@
       id-token: write
     steps:
-      - name: Download the package
+      - name: Download the packages
         uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
         with:
@@ -36,5 +36,6 @@
           path: packages
 
-      - name: Check that the package matches the tag
+      # The generator and the tool are released together, with one version.
+      - name: Check that the packages match the tag
         if: ${{ github.ref_type == 'tag' }}
         env:
@@ -42,8 +43,11 @@
         run: |
           shopt -s nullglob
-          expected="packages/SqlSource.${TAG#v}.nupkg"
+          version="${TAG#v}"
+          generator="packages/SqlSource.$version.nupkg"
+          tool="packages/SqlSource.Tool.$version.nupkg"
           found=(packages/*.nupkg)
-          if [[ ${#found[@]} -ne 1 || "${found[0]}" != "$expected" ]]; then
-              echo "::error::Tag $TAG needs exactly $expected, but the build produced: ${found[*]:-nothing}."
+          if [[ ${#found[@]} -ne 2 || ! -f "$generator" || ! -f "$tool" ]]; then
+              echo "::error::Tag $TAG needs exactly $generator and $tool, but the build produced:" \
+                  "${found[*]:-nothing}."
               echo 'Set VersionPrefix in Directory.Build.props to the version in the tag.'
               exit 1
@@ -75,5 +79,5 @@
       contents: write
     steps:
-      - name: Download the package
+      - name: Download the packages
         uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
         with:
```

Run: `tools/actionlint.sh`
Expected: no output.

- [ ] **Step 2: Try the check's logic by hand**

Save the body of the step as a script in a temporary folder and run it against empty files:

```bash
work="$(mktemp -d)" && mkdir "$work/packages" && touch "$work/packages/SqlSource.0.2.0.nupkg" "$work/packages/SqlSource.Tool.0.2.0.nupkg" && cat >"$work/check.sh" <<'EOF'
shopt -s nullglob
version="${TAG#v}"
generator="packages/SqlSource.$version.nupkg"
tool="packages/SqlSource.Tool.$version.nupkg"
found=(packages/*.nupkg)
if [[ ${#found[@]} -ne 2 || ! -f "$generator" || ! -f "$tool" ]]; then echo mismatch; else echo match; fi
EOF
(cd "$work" && TAG=v0.2.0 bash check.sh && TAG=v0.3.0 bash check.sh && touch packages/Other.0.2.0.nupkg && TAG=v0.2.0 bash check.sh); rm -rf "$work"
```

Expected: `match`, `mismatch`, `mismatch`: the tag's version, another version, and a third package.

- [ ] **Step 3: Say it in the publishing document**

`docs/publishing.md`:

````diff
--- a/docs/publishing.md
+++ b/docs/publishing.md
@@ -2,4 +2,6 @@
 
 How SqlSource is versioned and how it is released to nuget.org.  Building and testing locally are covered in [CONTRIBUTING.md](../CONTRIBUTING.md).
+
+A release is two packages: `SqlSource`, the generator, and `SqlSource.Tool`, the `sqlsource` command.  They are built by one run, carry one version and are published together.
 
 ## Versioning
@@ -14,5 +16,5 @@
 | Local | `0.1.0-dev` | `0.1.0.0` |
 
-The informational version is the package version followed by `+<commit sha>`.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.
+The table holds for both packages.  The informational version is the package version followed by `+<commit sha>`, and it is what `sqlsource --version` prints.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.
 
 ## Releasing
@@ -35,5 +37,5 @@
    ```
 
-5. `publish.yml` builds the tag, fails if the tag is not `v<VersionPrefix>`, pushes `SqlSource.0.2.0.nupkg` to nuget.org and creates the GitHub release.
+5. `publish.yml` builds the tag, fails unless the build gave exactly `SqlSource.0.2.0.nupkg` and `SqlSource.Tool.0.2.0.nupkg`, which it does when the tag is `v<VersionPrefix>`, pushes both to nuget.org and creates the GitHub release with both attached.  If the push of the second fails, run the workflow again: `--skip-duplicate` passes over the first.
 6. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.
 
@@ -46,5 +48,5 @@
 ```
 
-It publishes `<VersionPrefix>-beta.<run>`.  A run started from any other branch builds and publishes nothing.
+It publishes `<VersionPrefix>-beta.<run>` of both packages.  A run started from any other branch builds and publishes nothing.
 
 ### External configuration
@@ -60,2 +62,4 @@
 | | Variable `NUGET_USER` | The nuget.org profile name, not the email address |
 | GitHub ruleset `main` | Required status check | `ci` |
+
+The policy was made when `SqlSource` was the only package.  Before the first release that holds `SqlSource.Tool`, confirm on nuget.org that the policy covers a package id that does not exist yet.  If it does not, the push of the tool fails after the push of the generator has succeeded.
````

- [ ] **Step 4: Validate, commit**

Run: `./pre-commit-validation.sh`
Expected: every line of the summary says `passed`.

```bash
git add .github/workflows/publish.yml docs/publishing.md
```

```bash
git commit -F - <<'EOF'
Publish the tool's package beside the generator's

A tag needs exactly the two packages of its version.  The push and the
release already take every package of the folder.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 8: The documents, and the sub-phase is done

**Files:**
- Create: `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md`
- Modify: `CONTRIBUTING.md`, `src/SqlSource/AGENTS.md`, `docs/tech-debt/README.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: `CONTRIBUTING.md`**

````diff
--- a/CONTRIBUTING.md
+++ b/CONTRIBUTING.md
@@ -10,5 +10,5 @@
 | [Docker](https://docs.docker.com/get-started/get-docker/) | Any current release | Runs the pinned lint images used by the scripts in `tools/` |
 | [jq](https://jqlang.org/download/) | Any current release | Only for Claude Code: the commit hook in `.claude/hooks/` uses it to read the command being run |
-| `unzip` | Any | `tools/check-package.sh` lists the package's contents with it.  Present on macOS and most Linux distributions. |
+| `unzip` | Any | `tools/check-package.sh` lists the contents of the packages with it.  Present on macOS and most Linux distributions. |
 
 CSharpier and ReportGenerator are local .NET tools pinned in `dotnet-tools.json`.  Restore them once after cloning:
@@ -28,14 +28,26 @@
 ```
 
-The solution has two test projects:
+The solution has two projects that ship and three test projects:
+
+| Project | Is |
+|----|----|
+| `src/SqlSource` | The source generator, and the package `SqlSource` |
+| `src/SqlSource.Tool` | The `sqlsource` command, a console application that references the generator's project and shares its code, and the package `SqlSource.Tool` |
 
 | Project | Roslyn | Runs |
 |----|----|----|
-| `tests/SqlSource.Tests` | A current version | Every test |
+| `tests/SqlSource.Tests` | A current version | Every test of the generator |
 | `tests/SqlSource.Tests.RoslynFloor` | 4.8.0, the oldest supported | The generator driver tests: the files of `tests/SqlSource.Tests/Generator/`, compiled a second time |
+| `tests/SqlSource.Tool.Tests` | A current version | The tests of the tool, which run the whole command in process with their own output, working directory and environment |
 
 A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both, so it may use only Roslyn API that 4.8.0 has, and the C# it hands to the compiler is C# 12 at most.
 
 `tests/SqlSource.Tests` also uses the generator the way a consumer does: the types in `EndToEnd/` are compiled with the generator loaded, and the project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`, the MSBuild files the package ships.  It gets them by path and the generator through a project reference; `tools/check-package-install.sh`, under Package below, is what installs the packed package.
+
+To run the tool from its source:
+
+```bash
+dotnet run --project src/SqlSource.Tool -- describe
+```
 
 ### Coverage
@@ -46,5 +58,5 @@
 
 ```bash
-dotnet reportgenerator '-reports:artifacts/test-results/*.cobertura.xml' -targetdir:artifacts/coverage -reporttypes:Html -assemblyfilters:+SqlSource
+dotnet reportgenerator '-reports:artifacts/test-results/*.cobertura.xml' -targetdir:artifacts/coverage -reporttypes:Html '-assemblyfilters:+SqlSource;+SqlSource.Tool'
 ```
 
@@ -57,5 +69,5 @@
 ```
 
-This builds in `Release` and writes `artifacts/packages/SqlSource.<version>-dev.nupkg`.
+This builds in `Release` and writes two packages with one version, `artifacts/packages/SqlSource.<version>-dev.nupkg` and `artifacts/packages/SqlSource.Tool.<version>-dev.nupkg`.
 
 ```bash
@@ -63,5 +75,5 @@
 ```
 
-This checks what the package holds: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  Without an argument it packs into a temporary folder first.
+This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props` and `build/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly and Roslyn beside it, `DotnetToolSettings.xml`, and the readme, which is `src/SqlSource.Tool/README.md`.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.
 
 ```bash
@@ -73,4 +85,10 @@
 A change to what the package gives a consumer through its MSBuild files adds a line to `tools/package-install/Program.cs` and to the expected output.
 
+```bash
+tools/check-tool-install.sh artifacts/packages
+```
+
+This installs the tool the way a user does: `dotnet tool install` into a temporary folder outside the repository, from a feed that holds nothing else.  It then runs `sqlsource --version` and compares it with the package's version, runs `sqlsource --help`, and runs `sqlsource describe` in an empty folder, which must exit with 1 and report `SQLSRC201`.  A tool's package carries everything the tool loads, so this is what shows that all of it is there.  The last run sets `DOTNET_ROLL_FORWARD` to `LatestMajor`, which starts the tool on the newest runtime the machine has: the tool targets .NET 8 and rolls forward on a machine that has only a later one, and this proves it wherever the script runs.  Without an argument the script packs first.
+
 ## Checks
 
@@ -81,5 +99,5 @@
 ```
 
-It verifies formatting, runs the linters, builds the solution, runs the tests, checks the package and installs it into a project.  It never rewrites files, runs every step even when one fails (the tests and the two package checks are skipped if the build fails), and ends with a summary of what passed and failed.
+It verifies formatting, runs the linters, builds the solution, runs the tests, checks the two packages, installs the generator's into a project and installs the tool.  It never rewrites files, runs every step even when one fails (the tests and the three package checks are skipped if the build fails), and ends with a summary of what passed and failed.
 
 Both `pre-commit-validation.sh` and `format.sh --check` restore packages in locked mode, as CI does, so a `packages.lock.json` that no longer matches its project fails the check.  After changing a package reference or version, update the lock files and commit them:
@@ -93,5 +111,5 @@
 | `format.sh` | Builds the generator, which `dotnet format` needs in order to compile the test project, then rewrites C# and project files: `dotnet format style`, `dotnet format analyzers`, then CSharpier |
 | `format.sh --check` | Reports what `format.sh` would change, and rewrites nothing |
-| `pre-commit-validation.sh` | `format.sh --check`, the four linters below, the build, the tests, `tools/check-package.sh` and `tools/check-package-install.sh` |
+| `pre-commit-validation.sh` | `format.sh --check`, the four linters below, the build, the tests, `tools/check-package.sh`, `tools/check-package-install.sh` and `tools/check-tool-install.sh` |
 
 The scripts in `tools/` each run a linter from a pinned Docker image:
@@ -109,9 +127,9 @@
 |----|----|----|
 | `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml` and ends with the `ci` job |
-| `build.yml` | Called by `ci.yml` and `publish.yml` | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, `dotnet pack`, and the two package checks |
-| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the package to nuget.org, and creates a GitHub release for a tag |
+| `build.yml` | Called by `ci.yml` and `publish.yml` | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, `dotnet pack`, and the three package checks |
+| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the two packages to nuget.org, and creates a GitHub release for a tag |
 | `coverage-comment.yml` | A successful `ci.yml` run for a pull request | Posts the coverage report as a comment on the pull request, or updates the comment it posted before |
 
-Each run uploads two artifacts: `packages`, the `.nupkg`, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.
+Each run uploads two artifacts: `packages`, the two `.nupkg` files, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.
 
 `coverage-comment.yml` always runs as it is on `main`, never as a pull request changes it.  A change to it takes effect, and can first be tried, after it is merged.
````

- [ ] **Step 2: The generator's `AGENTS.md`**

`src/SqlSource/AGENTS.md`, three edits in its section on diagnostics:

```diff
--- a/src/SqlSource/AGENTS.md
+++ b/src/SqlSource/AGENTS.md
@@ -43,12 +43,12 @@
 ## Diagnostics
 
-Every diagnostic is a `DiagnosticDescriptor` in `Diagnostics/SqlDiagnostics.cs`: an error, tagged `NotConfigurable`.
+Every diagnostic is a `DiagnosticDescriptor`: an error, tagged `NotConfigurable`.  The generator's are in `Diagnostics/SqlDiagnostics.cs`.  The errors that only the `sqlsource` tool reports are in `Diagnostics/ToolDiagnostics.cs`, with an `All` of their own: the generator reports none of them, and they are in this assembly so that the release tracking and the tests of the document cover them.  They take their category and the start of their help link from `SqlDiagnostics.Category` and `SqlDiagnostics.HelpLinkBase`.
 
-- **Adding, removing or changing one touches four places:** the descriptor and `SqlDiagnostics.All`; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
+- **Adding, removing or changing one touches four places:** the descriptor and the `All` of its class; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
 - **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
 - **`SQLSRC011` and `SQLSRC014` have no position.**  The compiler does not say where an MSBuild property or the metadata of an item was set, so `SqlSourceGenerator` reports them with `Location.None`, built in an output step.  They do not travel as a `DiagnosticInfo`, and neither does `SQLSRC013`, which travels as a `PathCollision`.  An invalid dialect of a file travels as `ParsedSqlFile.InvalidDialect`, and an invalid setting as an `InvalidSetting` of `ProjectSettings` or `FileSettings`; each is reported once for each distinct value.
 - **`SQLSRC901` is a suppression, not a diagnostic.**  `Diagnostics/AttributeConflictSuppressor.cs` is the package's one analyzer: it turns off CS0436 at a use of the types that `AttributeSource.GeneratedTypes` lists, for a project that sees the internals of another one that uses SqlSource.  It is not a step of the pipeline, so it may read symbols and syntax.  Its `SuppressionDescriptor` is not in `SqlDiagnostics.All`, in `AnalyzerReleases.Unshipped.md` or in a section of `docs/diagnostics.md`.  It must suppress nothing but those types, as declared in the project being compiled: `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` holds both halves.  It hides the conflict and does not remove it (`docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`).
 - **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, so a helper method that builds descriptors hides them from it.
-- **An id below 100 is about the attributed type or the project, and one from 101 is about a `.sql` file.**  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.
+- **An id below 100 is about the attributed type or the project, one from 101 is about a `.sql` file, and one from 200 is about the snapshot or the tool.**  `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` holds the tool's to their range.  The ids `SQLSRC200` to `SQLSRC221` are assigned to the sub-phases of phase 2 in the epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: take a new id from there, not the next free one.  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.
 
 ## `Parsing/`
```

- [ ] **Step 3: The tool's `AGENTS.md`**

`src/SqlSource.Tool/AGENTS.md`:

```markdown
# AGENTS.md - src/SqlSource.Tool

The `sqlsource` command, packed as the .NET tool `SqlSource.Tool`.  `tests/SqlSource.Tool.Tests` holds its tests.

- **The tool shares the generator's code and never copies it.**  It references `src/SqlSource/SqlSource.csproj` and sees its internals.  The parser, the dialect rules, the settings, the sidecar's reader and writer, the hash and the descriptors are used from there.  Code that both need goes in the generator's project, where `netstandard2.0` and Roslyn 4.8.0 bound it.
- **It targets `net8.0` with `RollForward` `Major`, and references a current Roslyn by `VersionOverride`.**  Do not raise the target: a user with only the .NET 8 SDK must be able to run it.  `tools/check-tool-install.sh` runs the packed tool on the newest runtime the machine has.
- **Everything from outside comes through `ToolHost`.**  Nothing reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`, which is the entry of a real run and listens for Ctrl+C.  A test gives `Cli.RunAsync` a host of its own.  What a later sub-phase takes from outside, a process, a clock, a database, is a new member of `ToolHost`.
- **Every error goes through `Reporting/Reporter.cs` with a descriptor of `ToolDiagnostics`**, or of `SqlDiagnostics` where the generator reports the same condition.  The descriptors are in the generator's project; its `AGENTS.md` lists the four places a new one touches.  A wrong command line is the one exception, below.
- **The exit code is `0`, `1` or `2`.**  `1` when the reporter wrote anything, when the command line was wrong, and when the run was cancelled.  `2` is for `--check` alone: a difference, and nothing failed.  `Cli.RunAsync` takes `1` from `Reporter.Count`, so a command reports and goes on, and never returns `1` itself.
- **The format of an error is a contract with MSBuild and with editors.**  `<path>(<line>,<column>): error <id>: <message>`, or `<path> : error`, or `sqlsource : error`; then continuation lines, each four spaces, a label, a colon, a space and a text; then `    see:` with the descriptor's help link, last.  Only the first column tells a first line from a continuation line, so `Reporter` writes any line break inside a text as a space.  A path in a message is a full path.
- **An exception is `SQLSRC200`**, caught in `Cli.RunAsync` and nowhere else.  The stack trace is continuation lines labelled `trace`, written only when `SQLSOURCE_DEBUG` is set and not empty.
- **A wrong command line is reported by `UsageCheck.cs`, before System.CommandLine reads it, and never repeats a token's text.**  System.CommandLine's own messages repeat the token they reject, and a token can be a secret.  A line names an option the tool has, or the name of an unknown option cut at its first `=` or `:`, or a position counted from one.  Only the first unknown option is reported, and nothing after it is read.  These lines have no id and do not go through the reporter.  `tests/SqlSource.Tool.Tests/UsageCheckTests.cs` holds the rule for an option that takes a value.  What is left of the risk is `docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md`.
- **An option is declared so that `UsageCheck` reads it right.**  A flag has `Arity = ArgumentArity.Zero`: System.CommandLine's default for a `bool` takes an optional value, and the token after the flag would be read as that value.  An option that takes a value has `ArgumentArity.ExactlyOne`.  An option is added to `--help` in the sub-phase that gives it an effect, not before.
- **The root is a `Command`, not a `RootCommand`**, with a `HelpOption` under three names and a `--version` whose action is the tool's own.  A `RootCommand` takes its name from the process, prints the version of the assembly the process started with, which under a test is the test host, and reads `[suggest]` as a directive.  Response files and the bundling of short options are off in `Cli`.
- **The tool writes English**, and `SatelliteResourceLanguages` keeps other languages out of the package.  A test compares no text that System.CommandLine wrote.
- **`README.md` here is the package's readme on nuget.org**, so its links are absolute URLs.  It says what a user of the tool needs, and the repository's `README.md` is not packed into this package.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
```

- [ ] **Step 4: Record what is still printed**

Check `Next id` in `docs/tech-debt/README.md` first.  If it is no longer `TD-0023`, take the id it gives, and use it in the file's name, its heading, the row, and the reference in `src/SqlSource.Tool/AGENTS.md`.

`docs/tech-debt/TD-0023-unknown-option-and-path-are-printed-as-given.md`:

```markdown
# TD-0023 - An unknown option and a path are printed as they were given

## Problem

The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  Two things are still printed as the user typed them.

- **The name of an unknown option**, which is the token cut at its first `=` or `:`.  A token that starts with `-` and holds a secret with neither character is printed whole: `-pS3cret`, the way `mysql` takes a password, gives `sqlsource: unknown option '-pS3cret'`.
- **The path of `describe`**, in `SQLSRC201` to `SQLSRC203`, from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs).  A value typed where the path stands is printed as a path: `sqlsource describe "Host=db;Password=S3cret"` gives `SQLSRC203` with the whole string in it.

## Why it exists

A message that names nothing is of no use: a user who misspells `--force` must be told which word was not known, and a path that was not found must be shown.  Cutting the option at `=` or `:` covers the forms a value is given in beside an option of this tool, and reading nothing after the first unknown option covers the value that follows one.

## Impact

Low.  Both need a command line that the tool does not offer: it has no short option that takes a value, and a connection is always given with `--connection`.  What is printed goes to the terminal of the person who typed it, and to the log of a build that runs the tool.

## Proposed fix

Print an unknown option only when it is close to one the tool has, and by its position otherwise.  For the path, print it only when it has no `=` and no `;` in it, and its position otherwise.

## Trigger

The tool gains a short option that takes a value, or a report of a secret in the log of a build.
```

`docs/tech-debt/README.md`:

```diff
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -3,5 +3,5 @@
 Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.
 
-Next id: `TD-0023`
+Next id: `TD-0024`
 
 ## Active items
@@ -19,4 +19,5 @@
 | [TD-0021](TD-0021-comments-and-token-defaults.md) | Open | 2026-10-08 | Low | A `--` comment inside an inline default swallows its closing braces, a `-- token:` default is compared as written while an inline one is compared without comments, and a comment can be scanned for tokens where no type keeps comments |
 | [TD-0022](TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md) | Open | 2026-10-08 | Low | A query that keeps its comments has its SQL built and scanned for tokens twice, so its parse allocates and takes about twice as much |
+| [TD-0023](TD-0023-unknown-option-and-path-are-printed-as-given.md) | Open | 2026-10-09 | Low | The `sqlsource` tool prints the name of an unknown option and the path of `describe` as they were typed, so a secret typed as either reaches its output |
 
 ## Columns
```

- [ ] **Step 5: Close the sub-phase in the epic outline**

`docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

```diff
--- a/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
+++ b/docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
@@ -64,5 +64,5 @@
 | 1. Parameters and settings | Done | [query-generation-phase-1-parameters-and-settings-design](2026-10-08-query-generation-phase-1-parameters-and-settings-design.md) | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL; every new marker, setting, enum and generator parameter of the epic is parsed and validated with no effect yet.  A user sees four changes: `SqlSourceTokenValidation` gives way to `SqlSourceGeneratorParameters`; `token-validation` and `token-ignore=` are no longer generator parameters, and `-- token-ignore:` is a marker; a query's generator parameters replace the preamble's; and `{{a:b}}` is a token with a default, where it was text. |
 | 2.1 The snapshot format | Done | [query-generation-phase-2-1-snapshot-format-design](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | In the generator assembly: the model of a sidecar, the hand-written reader and writer, the two comparisons the format design defines, and the JSON Schema under `schemas/`.  No tool, and no step of the generator reads a sidecar yet. |
-| 2.2 The tool's shell and its package | In progress | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
+| 2.2 The tool's shell and its package | Done | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
 | 2.3 The project manifest and discovery | Not started | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
 | 2.4 The work list | Not started | [query-generation-phase-2-4-work-list-design](2026-10-09-query-generation-phase-2-4-work-list-design.md) | The syntax-level attribute reader; the plan of a run from the shared path resolver, parser, settings and hash: which queries need an entry, their databases, and which are selected by `--database` and a `.sql` path. |
@@ -539,5 +539,5 @@
   `SQLSRC209` and `SQLSRC210` are also the generator's, from phase 5.
 - **What a filter lets a run change.**  A sidecar is written only for a file with a selected query, and deleted only in a run with no filter.  `--check` compares within the same bounds, so it reports what `describe` with the same filters would change.
-- **A wrong command line** is reported by the tool, and never repeats the text of a token it did not expect: a misspelt `--connection` must not print the connection string after it.
+- **A wrong command line** is reported by the tool, and never repeats the text of a token it did not expect: a misspelt `--connection` must not print the connection string after it.  Its lines have no id and do not go through the reporter.  Only the first unknown option is reported, and nothing after it is read.  `--` and a response file are given no meaning.  A flag is declared to take no value, so that the token after it is never read as one.
 - **The summary under `--check`** counts `same`, `differ` and `failed`.
 - **A sidecar of a higher format version** is an error for `describe`, which leaves it alone; a lower one is written again.
```

- [ ] **Step 6: Read the documents against the code**

Check each by reading, and fix the document where it is wrong:

- Every file, type and member that `src/SqlSource.Tool/AGENTS.md` names exists under that name: `grep -n 'UsageCheck\|RunUnitFinder\|ToolHost.Create\|Reporter.Count' -r src/SqlSource.Tool --include=*.cs | head`.
- `README.md` at the root is unchanged: `git diff main -- README.md` prints nothing.
- `docs/deferred` needs nothing: every item of the spec's scope is delivered.

- [ ] **Step 7: Validate, commit**

Run: `./pre-commit-validation.sh`
Expected: every line of the summary says `passed`.

```bash
git add CONTRIBUTING.md src/SqlSource/AGENTS.md src/SqlSource.Tool/AGENTS.md docs/tech-debt docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -F - <<'EOF'
Document the tool and close phase 2.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 8: Check the version before the pull request**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: nothing, and so nothing to compare.  If a tag is printed and `VersionPrefix` in `Directory.Build.props` is not greater than it, ask the owner for the new version and set it in this pull request.

The pull request's description says, for the owner: confirm on nuget.org that the trusted publishing policy covers `SqlSource.Tool`, a package id that does not exist yet, before the first release.
