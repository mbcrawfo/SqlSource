# Query generation, phase 2.4: the work list - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the plan of a `sqlsource describe` run: from the manifests of sub-phase 2.3, which queries exist, which need an entry in a sidecar, which database each belongs to, and which the run was asked to describe.  `describe` builds the plan, reports what is wrong with it, and describes nothing yet.

**Architecture:** A new folder, `src/SqlSource.Tool/Planning/`.  `AttributeReader` reads `[SqlSourceGenerate]` from a project's C# files as syntax and gives `TypeClaim`s.  `RunPlanner` puts each project's claims through the generator's own `PathResolver`, reads the MSBuild values through the generator's own setting readers behind a small `AnalyzerConfigOptions`, parses each claimed file once through the generator's own `SqlFileReader` behind a small `AdditionalText`, and gives a `RunPlan` of `PlannedFile`s and `PlannedQuery`s with the errors of planning as data.  `DescribeCommand`, moved out of `Cli.cs`, reads the command line into filters and reports the errors.  The generator gains three small things to share and changes no behaviour.

**Tech Stack:** C# on `net8.0` in `src/SqlSource.Tool` with Roslyn 5.9.0 for the syntax reader and System.CommandLine 2.0.12; C# on `netstandard2.0` with Roslyn 4.8.0 in `src/SqlSource`; xunit v3 on Microsoft.Testing.Platform and Shouldly in the test projects on `net10.0`.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-4-work-list-design.md`](../specs/2026-10-09-query-generation-phase-2-4-work-list-design.md).  Read it first; this plan argues from it.  The [epic outline](../specs/2026-10-07-query-generation-epic-design.md) holds what sub-phase 2.5 builds on this one: its sections "Workflow", "What is generated" and "Phase 2".

## Global Constraints

- Branch: `claude/query-generation-phase-2-4-work-list`, which exists and holds the reviewed spec.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running.  **The closing steps of every task** are: run `./format.sh`; run `./pre-commit-validation.sh` as its own command and fix what it reports; then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it, and keep those words out of any other command.
- **The code of this plan was written against the repository as it stood and was not compiled.**  Where the compiler, the formatter or an analyzer objects, change the code to satisfy it and keep the names and the behaviour that the **Interfaces** blocks and the tests give.  Never suppress a rule, and never edit `.editorconfig`.
- `src/SqlSource` stays on `netstandard2.0` and Roslyn 4.8.0 and takes no new package reference.  No behaviour of the generator changes: its tests pass as they are, and `PathResolverAllocationTests` and `SqlFileParserAllocationTests` keep their budgets.
- The tool targets `net8.0`.  Do not raise it, and do not use an API that .NET 8 lacks.
- **The tool reuses the generator's parser, path resolver, setting readers and hash, and copies none of them.**  A rule that both need goes into `src/SqlSource`.
- Nothing in the tool reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`.  The tool reads files of a project and writes nothing into one.
- Every error goes through `Reporter` with a descriptor.  A wrong command line is the exception: its lines have no id, do not go through the reporter, and **never repeat a token's text**; a line names an option the tool has, or a position counted from one.
- The new diagnostics are `SQLSRC208` to `SQLSRC212`, with the titles and messages of the spec's table.  Each touches four places: the descriptor and `All` in `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md` (the table row and the section), and a test in `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`.  `All`, the release file and the document are in the order of the ids, so the five go between `SQLSRC207` and `SQLSRC220`.
- Paths are compared with `SqlPath.Comparer` after `SqlPath.Normalize`.  Database names are compared with `StringComparer.OrdinalIgnoreCase`.
- Exit codes: `0` success; `1` an error was reported, a wrong command line, or a cancelled run.  `Cli.RunAsync` takes `1` from `Reporter.Count`: a command reports and goes on, and never returns `1` itself.
- A file is at most 120 characters wide, apart from Markdown: `tools/editorconfig-checker.sh` rejects a longer line.  `./format.sh` breaks the code of this plan where it is wider, but never a string: split a string literal that is too wide with `+`, at a word.
- A file this plan gives in full ends with one line break, which the block that shows it does not.
- Types under `src/` carry XML documentation as their neighbours do; test files use `//` comments.
- Prose uses two spaces after a full stop, as the repository does.  Files under `docs/superpowers/` are not rewritten, apart from the epic outline in tasks 1 and 10.
- `README.md` at the root and `docs/publishing.md` do not change.
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
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunPlannerTests
```

## Where this plan departs from the spec

1. **`describe` moves to `src/SqlSource.Tool/DescribeCommand.cs`.**  The spec gives the command a planning method of its own and rules for its command line; `Cli.cs` keeps the root, the parse and the exit code.
2. **`AttributeReader.Read` gives a `ProjectClaims` and `RunPlanner.Plan` a `RunPlanResult`**: the value and its errors together, as the spec's "and the errors" asks.  Nothing in `Planning/` reports; `DescribeCommand` does.
3. **`ListedFiles` is a unit of its own**: a project's `.sql` files as the generator sorts them, and the files a claim resolves to.  The planner and the parity test both use it, so the test compares what the planner plans.
4. **The whole shape of the planner arrives in task 5**, with a file that two projects claim already planned once.  Task 7 adds the databases and `SQLSRC211`, and its tests of a shared file pin what task 5 built.
5. **With no project to plan, there is no plan and no `SQLSRC212`.**  The line `no project of '...' uses SqlSource`, or the errors of the projects, already say why.
6. **`SQLSRC210` is reported in a file that is `NotDescribable` too**: both are mended in the `.sql` file, and one run should say both.
7. **Errors are in a fixed order**: for each project in order its `SQLSRC208`s, then `SQLSRC011`, then `SQLSRC014`; then for each planned file in the plan's order its parse errors, or `SQLSRC209` and `SQLSRC210`; then `SQLSRC211` in the plan's order; then `SQLSRC212` in the order of the command line.
8. **Three documents change that the spec does not name.**  `CONTRIBUTING.md`, since the tool's tests now compile three files of `tests/SqlSource.Tests/Generator/`; `src/SqlSource.Tool/README.md`, which says what `describe` does; and `docs/tech-debt/TD-0030`, which names this sub-phase as the place where the `.sql` filter meets its comparison.
9. **The tech-debt ids are `TD-0031`, the syntax-only reader (task 3), `TD-0032`, a file that is not UTF-8 (task 5), and `TD-0033`, a file two projects list (task 7)**: each in the task that brings it.

## Review Focus

What the spec implies and its own test table does not name.  Each has a test in the task that owns the code.

1. **A C# file that does not parse**, as one is in the middle of an edit, with the attribute in it: the reader does not throw, and gives the claims of what it can read.  Task 3.
2. **An attribute with an argument the reader does not expect**: a positional one, an unknown name, `Path` twice.  The first `Path` is read and nothing throws.  Task 3.
3. **A `Path` that matches nothing or leaves the root**, `"../../../.."`: nothing is planned for the type, nothing throws, and nothing is reported, since the build reports it.  Task 5.
4. **A `.sql` filter spelled another way than the project lists the file**: another case, `./`, `..`, a backslash.  It selects the file.  Task 8.
5. **One `.sql` filter given twice that names no claimed file**: one `SQLSRC212`.  Task 8.

---

### Task 1: The outline says In progress

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
- Create: `docs/superpowers/plans/2026-10-10-query-generation-phase-2-4-work-list.md` (this file, already written)

- [ ] **Step 1: Set the row of 2.4**

In the table under `## Phases`, in the row that starts `| 2.4 The work list |`, replace `| Not started |` with `| In progress |`.

- [ ] **Step 2: Put the sentence on later ids right**

Under `### Diagnostics`, in the paragraph that starts `- The diagnostic ids are a new range`, replace

```
Phase 2 uses `SQLSRC200` to `SQLSRC222`, assigned to its sub-phases in its section below, and the later phases take ids from `SQLSRC223`.
```

with

```
Phase 2 uses `SQLSRC200` to `SQLSRC223`, assigned to its sub-phases in its section below, and the later phases take ids from `SQLSRC224`.
```

- [ ] **Step 3: Closing steps, and commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-10-query-generation-phase-2-4-work-list.md
git commit -m "Start phase 2.4: the work list

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: What the generator shares

**Files:**
- Modify: `src/SqlSource/Generation/SqlPath.cs`
- Modify: `src/SqlSource/Generation/PathResolver.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`
- Create: `src/SqlSource/Parsing/SqlDescribableDialects.cs`
- Test: `tests/SqlSource.Tests/Generation/SqlPathTests.cs`, `tests/SqlSource.Tests/Generation/PathResolverTests.cs`
- Create: `tests/SqlSource.Tests/Parsing/SqlDescribableDialectsTests.cs`

**Interfaces:**
- Produces: `SqlPath.ToSortedSet(IEnumerable<string> paths) : EquatableArray<string>`
- Produces: `PathResolver.FindFiles(string sourceFilePath, string? path, EquatableArray<string> sqlPaths) : ImmutableArray<string>`
- Produces: `SqlDescribableDialects.All : ImmutableArray<SqlDialect>` and `SqlDescribableDialects.Contains(SqlDialect) : bool`

- [ ] **Step 1: Measure `PathResolver.Resolve` before the change**

`src/SqlSource/AGENTS.md` asks a change to a hot path for its time and allocation, before and after.  Add this method to `PathResolverAllocationTests`, with `using System.Diagnostics;` at the top.  It is never committed.

```csharp
    [Fact]
    public void Measure()
    {
        const int Calls = 200_000;
        var type = TestModels.Type();
        var paths = CreatePaths();
        for (var call = 0; call < 5_000; call++)
        {
            _ = PathResolver.Resolve(type, paths, isSupportedFramework: true, unsupportedLanguageVersion: null);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var call = 0; call < Calls; call++)
        {
            _ = PathResolver.Resolve(type, paths, isSupportedFramework: true, unsupportedLanguageVersion: null);
        }

        watch.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / Calls;
        Assert.Fail($"{watch.Elapsed.TotalMilliseconds * 1_000_000 / Calls:F0} ns and {bytes} bytes for a call");
    }
```

Run it three times and write the three lines down under "before":

```bash
dotnet test --project tests/SqlSource.Tests -c Release --filter-method "*PathResolverAllocationTests.Measure"
```

Expected: FAIL, with a message such as `412 ns and 728 bytes for a call`.

- [ ] **Step 2: Write the failing tests**

Add to `tests/SqlSource.Tests/Generation/SqlPathTests.cs`:

```csharp
    [Fact]
    public void ToSortedSet_Paths_AreEachOnceIgnoringCaseAndInOrder() =>
        SqlPath
            .ToSortedSet(["b/Users.sql", "a/Z.sql", "B/users.sql", "a/a.sql", "a/Z.sql"])
            .ShouldBe(["a/a.sql", "a/Z.sql", "b/Users.sql"]);

    [Fact]
    public void ToSortedSet_NoPaths_IsEmpty() => SqlPath.ToSortedSet([]).Count.ShouldBe(0);
```

Add to `tests/SqlSource.Tests/Generation/PathResolverTests.cs`:

```csharp
    [Theory]
    [InlineData(null, "app/Repo/Count.sql|app/Repo/Users.sql")]
    [InlineData("../Queries", "app/Queries/Orders.sql|app/Queries/Users.sql")]
    [InlineData("users.SQL", "app/Repo/Users.sql")]
    [InlineData("Missing.sql", "")]
    [InlineData("../../..", "")]
    public void FindFiles_PathOfAnAttribute_GivesTheFilesItNames(string? path, string expected) =>
        string.Join('|', PathResolver.FindFiles("/app/Repo/UserRepository.cs", path, SqlPaths)).ShouldBe(expected);

    [Fact]
    public void FindFiles_SourceFileWhosePathLeavesTheRoot_GivesNone() =>
        PathResolver.FindFiles("../UserRepository.cs", null, SqlPaths).ShouldBeEmpty();
```

Create `tests/SqlSource.Tests/Parsing/SqlDescribableDialectsTests.cs`:

```csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDescribableDialectsTests
{
    // SQLSRC209 names these two in its message.
    [Fact]
    public void All_Dialects_ArePostgresAndSqlServer() =>
        SqlDescribableDialects.All.Select(SqlDialectName.Canonical).ShouldBe(["postgres", "mssql"]);

    [Fact]
    public void Contains_EveryDialect_IsTrueForTheTwoAlone()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            SqlDescribableDialects
                .Contains(dialect)
                .ShouldBe(dialect is SqlDialect.PostgreSql or SqlDialect.SqlServer, dialect.ToString());
        }
    }
}
```

- [ ] **Step 3: Run them to see them fail**

```bash
dotnet build tests/SqlSource.Tests
```

Expected: the build fails with CS0117 for `SqlPath.ToSortedSet`, CS0122 or CS1501 for `PathResolver.FindFiles`, and CS0103 for `SqlDescribableDialects`.

- [ ] **Step 4: Implement**

In `src/SqlSource/Generation/SqlPath.cs`, add `using System.Linq;` and this member after `IsSqlFile`:

```csharp
    /// <summary>
    /// The paths, each once ignoring case and in the order of <see cref="Comparer" />, which is also the order of a
    /// type's members.  Of two paths that differ only by case, the first is kept.  The generator and the
    /// <c>sqlsource</c> tool both make a project's list of <c>.sql</c> files with this.
    /// </summary>
    public static EquatableArray<string> ToSortedSet(IEnumerable<string> paths) =>
        new(paths.Distinct(Comparer).OrderBy(static path => path, Comparer).ToImmutableArray());
```

In `src/SqlSource/SqlSourceGenerator.cs`, delete the private `ToSortedSet` with its comment, and write `SqlPath.ToSortedSet` at its three calls (`sqlPaths`, `claimedPaths`, `commentPaths`).

In `src/SqlSource/Generation/PathResolver.cs`, call `FindFiles(type.FilePath, type.Path, sqlPaths)` in `Resolve`, and replace the private method with:

```csharp
    /// <summary>
    /// The files that a <c>Path</c> names, for an attribute in the file <paramref name="sourceFilePath" />: the
    /// <c>.sql</c> files of the folder of that file when <paramref name="path" /> is null, of the folder it names, or
    /// the one file it names.  Empty when it names nothing.  The <c>sqlsource</c> tool resolves a type's files with
    /// this too, so a change here changes what the tool describes.
    /// </summary>
    /// <param name="sourceFilePath">The path of the C# file that carries the attribute.</param>
    /// <param name="path">The attribute's <c>Path</c>, or null when it is not set.</param>
    /// <param name="sqlPaths">The project's <c>.sql</c> files, as <see cref="SqlPath.ToSortedSet" /> gives them.</param>
    public static ImmutableArray<string> FindFiles(string sourceFilePath, string? path, EquatableArray<string> sqlPaths)
    {
        if (SqlPath.Normalize(sourceFilePath) is not { } sourceFile)
        {
            return ImmutableArray<string>.Empty;
        }

        var folder = SqlPath.GetFolder(sourceFile);

        var target = path is null ? folder : SqlPath.Combine(folder, path);
        if (target is null)
        {
            return ImmutableArray<string>.Empty;
        }

        if (path is null || !SqlPath.IsSqlFile(path))
        {
            return SqlPath.FindInFolder(sqlPaths, target);
        }

        // The path as the project lists it, which may differ from the target in case.
        var index = SqlPath.IndexOf(sqlPaths, target, static listed => listed);
        return index < 0 ? ImmutableArray<string>.Empty : ImmutableArray.Create(sqlPaths[index]);
    }
```

Create `src/SqlSource/Parsing/SqlDescribableDialects.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Parsing;

/// <summary>
/// The dialects whose database can be asked to describe a query.  An output of <c>Models</c> or <c>CodeGen</c> needs
/// one of them.  The <c>sqlsource</c> tool reads this list, and the generator will.
/// </summary>
internal static class SqlDescribableDialects
{
    /// <summary>The dialects, in the order a message names them.</summary>
    public static ImmutableArray<SqlDialect> All { get; } =
        ImmutableArray.Create(SqlDialect.PostgreSql, SqlDialect.SqlServer);

    public static bool Contains(SqlDialect dialect) => All.Contains(dialect);
}
```

- [ ] **Step 5: Run the generator's tests**

```bash
dotnet test --project tests/SqlSource.Tests
```

```bash
dotnet test --project tests/SqlSource.Tests.RoslynFloor
```

Expected: every test passes but `Measure`, which fails with its line.

- [ ] **Step 6: Measure after the change, and remove the measurement**

Run the command of step 1 three times and write the lines down under "after", in the scratchpad file `perf-path-resolver.txt` beside the three of "before".  Task 11 puts both in the pull request.  The bytes must be the same and the time within the noise of the three runs.  Then delete `Measure` and its `using`.

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "Share the sorted list, the path search and the dialects that can be described

The sqlsource tool plans a run with the generator's own rules.  No
behaviour of the generator changes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The attribute reader, and `SQLSRC208`

**Files:**
- Create: `src/SqlSource.Tool/Planning/TypeClaim.cs`, `src/SqlSource.Tool/Planning/ProjectClaims.cs`, `src/SqlSource.Tool/Planning/AttributeReader.cs`
- Modify: `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md`; modify `docs/tech-debt/README.md`
- Test: create `tests/SqlSource.Tool.Tests/AttributeReaderTests.cs`; modify `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Produces: `sealed record TypeClaim(string SourcePath, string? Path, OutputKind? Output, LinePosition Position)`, where `OutputKind` is `SqlSource.Settings.OutputKind`
- Produces: `sealed record ProjectClaims(ImmutableArray<TypeClaim> Claims, ImmutableArray<ToolDiagnostic> Errors)`
- Produces: `AttributeReader.Read(ProjectManifest manifest) : ProjectClaims`
- Produces: `ToolDiagnostic.At(DiagnosticDescriptor descriptor, string path, LinePosition position, params string[] arguments)`, `ToolDiagnostic.At(DiagnosticDescriptor descriptor, LocationInfo location, params string[] arguments)` and `ToolDiagnostic.From(DiagnosticInfo diagnostic)`
- Produces: `ToolDiagnostics.AttributeArgumentNotLiteral` (`SQLSRC208`)

- [ ] **Step 1: The descriptor, in its four places**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `ProjectNotInRun`:

```csharp
    public static readonly DiagnosticDescriptor AttributeArgumentNotLiteral = new(
        id: "SQLSRC208",
        title: "Attribute argument is not a literal",
        messageFormat: "'{0}' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal "
            + "here",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc208",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

Add `AttributeArgumentNotLiteral` to `All` after `ProjectNotInRun`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC207`:

```
SQLSRC208 | SqlSource | Error | Attribute argument is not a literal
```

In `docs/diagnostics.md`, after the table row of `SQLSRC207`:

```
| [SQLSRC208](#sqlsrc208) | Attribute argument is not a literal |
```

and after the section of `SQLSRC207`, before `## SQLSRC220`:

````markdown
## SQLSRC208

**Attribute argument is not a literal**

The `sqlsource` tool finds `[SqlSourceGenerate]` by reading the C# files of a project as text.  It does not compile them, so it cannot follow a name to its value.  `Path` and `Output` decide which queries are described, and the tool reads those two alone: `Path` must be a string literal, and `Output` a member of `GeneratorOutput` written out.

```csharp
private const string Folder = "Queries";

[SqlSourceGenerate(Path = Folder)]
internal static partial class Queries;
```

```console
/work/App/Queries.cs(3,27): error SQLSRC208: 'Path' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here
```

Write the value where the attribute is: `Path = "Queries"`, `Output = GeneratorOutput.Models`.  A constant, `nameof`, a concatenation, an interpolated string and a cast of a number are all this error, though the compiler accepts them.  The tool describes nothing for the type until it is mended; the build is not affected.
````

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`:

```csharp
    [Fact]
    public void AttributeArgumentNotLiteral_Message_HoldsTheArgument() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.AttributeArgumentNotLiteral.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "Path"
            )
            .ShouldBe(
                "'Path' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here"
            );
```

Run `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`.  Expected: PASS.

- [ ] **Step 2: Write the failing tests of the reader**

Create `tests/SqlSource.Tool.Tests/AttributeReaderTests.cs`:

```csharp
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tool's reader of [SqlSourceGenerate] works on syntax: it has no compilation and follows no name.
public sealed class AttributeReaderTests : IDisposable
{
    private const string PathPrefix = "[SqlSourceGenerate(Path = ";
    private const string OutputPrefix = "[SqlSourceGenerate(Output = ";

    private readonly TempFolder _folder = new();

    private string Source => _folder.PathOf("App/Repo/Queries.cs");

    public void Dispose() => _folder.Dispose();

    private ProjectManifest Manifest(string langVersion, string[] constants, params string[] compile) =>
        new()
        {
            ProjectPath = _folder.PathOf("App/App.csproj"),
            TargetFramework = "net10.0",
            LangVersion = langVersion,
            DefineConstants = [.. constants],
            Properties = ImmutableDictionary<string, string>.Empty,
            Files = [],
            CompileFiles = [.. compile],
        };

    private ProjectClaims Read(string source, string langVersion = "", params string[] constants)
    {
        _ = _folder.WriteFile("App/Repo/Queries.cs", source);
        return AttributeReader.Read(Manifest(langVersion, constants, Source));
    }

    // The attribute lists start on the third line, in its first column.
    private static string OnAClass(string attributes) =>
        $"using SqlSource;\n\n{attributes}\ninternal static partial class Queries;\n";

    [Theory]
    [InlineData("[SqlSourceGenerate]")]
    [InlineData("[SqlSourceGenerateAttribute]")]
    [InlineData("[SqlSourceGenerate()]")]
    [InlineData("[SqlSource.SqlSourceGenerate]")]
    [InlineData("[global::SqlSource.SqlSourceGenerateAttribute]")]
    [InlineData("[type: SqlSourceGenerate]")]
    [InlineData("[System.Serializable, SqlSourceGenerate]")]
    [InlineData("[System.Serializable]\n[SqlSourceGenerate]")]
    public void Read_AttributeHoweverItIsNamed_IsAClaimThatSetsNothing(string attributes)
    {
        var claims = Read(OnAClass(attributes));

        claims.Errors.ShouldBeEmpty();
        var claim = claims.Claims.ShouldHaveSingleItem();
        claim.SourcePath.ShouldBe(Source);
        claim.Path.ShouldBeNull();
        claim.Output.ShouldBeNull();
    }

    [Fact]
    public void Read_Attribute_GivesWhereItStarts() =>
        Read(OnAClass("[System.Serializable, SqlSourceGenerate(Path = \"Q\")]"))
            .Claims.ShouldHaveSingleItem()
            .Position.ShouldBe(new LinePosition(2, "[System.Serializable, ".Length));

    [Theory]
    [InlineData("internal partial class Queries { }")]
    [InlineData("internal partial struct Queries { }")]
    [InlineData("internal partial record Queries;")]
    [InlineData("internal partial record struct Queries;")]
    [InlineData("internal readonly partial record struct Queries(int Id);")]
    public void Read_ClassStructOrRecord_IsAClaim(string declaration) =>
        Read("using SqlSource;\n[SqlSourceGenerate]\n" + declaration + "\n").Claims.Length.ShouldBe(1);

    [Fact]
    public void Read_NestedType_IsAClaim() =>
        Read(
                """
                namespace App
                {
                    internal partial class Outer
                    {
                        [SqlSource.SqlSourceGenerate(Path = "Inner")]
                        private partial class Inner;
                    }
                }
                """
            )
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Inner");

    [Theory]
    [InlineData("[assembly: SqlSource.SqlSourceGenerate]\ninternal partial class Queries;")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal partial interface IQueries;")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal enum Kind { One }")]
    [InlineData("[SqlSource.SqlSourceGenerate]\ninternal delegate void Run();")]
    [InlineData("internal partial class Queries { [SqlSource.SqlSourceGenerate] private void Run() { } }")]
    [InlineData("[method: SqlSource.SqlSourceGenerate]\ninternal partial class Queries;")]
    [InlineData("[SqlSource.Generate]\ninternal partial class Queries; // SqlSourceGenerate")]
    public void Read_AttributeElsewhereOrOfAnotherName_IsNoClaim(string source)
    {
        var claims = Read(source);

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_TwoAttributesOnOneDeclaration_TakesTheFirst() =>
        Read(OnAClass("[SqlSourceGenerate(Path = \"A\")]\n[SqlSourceGenerate(Path = \"B\")]"))
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("A");

    [Theory]
    [InlineData("\"Queries\"", "Queries")]
    [InlineData("@\"Queries\\Users.sql\"", "Queries\\Users.sql")]
    [InlineData("\"\"\"Queries/Users.sql\"\"\"", "Queries/Users.sql")]
    [InlineData("\"Qu\\u0065ries\"", "Queries")]
    [InlineData("\" \"", " ")]
    [InlineData("\"\"", null)]
    [InlineData("null", null)]
    [InlineData("default", null)]
    public void Read_PathAsALiteral_IsItsValue(string expression, string? expected)
    {
        var claims = Read(OnAClass(PathPrefix + expression + ")]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Path.ShouldBe(expected);
    }

    [Theory]
    [InlineData("GeneratorOutput.Sql", "Sql")]
    [InlineData("GeneratorOutput.Models", "Models")]
    [InlineData("GeneratorOutput.CodeGen", "CodeGen")]
    [InlineData("SqlSource.GeneratorOutput.Models", "Models")]
    [InlineData("global::SqlSource.GeneratorOutput.CodeGen", "CodeGen")]
    // The compiler rejects a member that does not exist.
    [InlineData("GeneratorOutput.Other", null)]
    public void Read_OutputAsAMember_IsThatMember(string expression, string? expected)
    {
        var claims = Read(OnAClass(OutputPrefix + expression + ")]"));

        claims.Errors.ShouldBeEmpty();
        var output = claims.Claims.ShouldHaveSingleItem().Output;
        // Not "output?.ToString().ShouldBe": that would assert nothing for an output that is not set.
        (output is null ? null : output.ToString()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Folder")]
    [InlineData("nameof(Queries)")]
    [InlineData("\"a\" + \"b\"")]
    [InlineData("$\"Queries\"")]
    [InlineData("(\"Queries\")")]
    [InlineData("default(string)")]
    public void Read_PathThatIsNoLiteral_IsSqlsrc208AtTheExpressionAndNoClaim(string expression)
    {
        var claims = Read(OnAClass(PathPrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.AttributeArgumentNotLiteral,
                Source,
                new LinePosition(2, PathPrefix.Length),
                "Path"
            ),
        ]);
    }

    [Theory]
    [InlineData("(GeneratorOutput)1")]
    [InlineData("Level")]
    [InlineData("default")]
    [InlineData("GeneratorOutput.Models | GeneratorOutput.CodeGen")]
    [InlineData("Other.Models")]
    public void Read_OutputThatIsNoMember_IsSqlsrc208AtTheExpressionAndNoClaim(string expression)
    {
        var claims = Read(OnAClass(OutputPrefix + expression + ")]"));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.AttributeArgumentNotLiteral,
                Source,
                new LinePosition(2, OutputPrefix.Length),
                "Output"
            ),
        ]);
    }

    [Fact]
    public void Read_BothArgumentsNoLiterals_IsSqlsrc208ForEach() =>
        Read(OnAClass("[SqlSourceGenerate(Path = Folder, Output = Level)]")).Errors.Length.ShouldBe(2);

    [Fact]
    public void Read_OtherArgumentsOfTheAttribute_AreNotRead() =>
        Read(OnAClass("[SqlSourceGenerate(Parameters = Words, ModelNamespace = nameof(App), Path = \"Q\")]"))
            .Errors.ShouldBeEmpty();

    // Review focus 2.
    [Fact]
    public void Read_ArgumentsTheReaderDoesNotExpect_AreSteppedOverAndTheFirstPathIsRead()
    {
        var claims = Read(OnAClass("[SqlSourceGenerate(\"positional\", Path = \"A\", Path = \"B\", Unknown = 3)]"));

        claims.Errors.ShouldBeEmpty();
        claims.Claims.ShouldHaveSingleItem().Path.ShouldBe("A");
    }

    // Review focus 1.
    [Fact]
    public void Read_FileThatDoesNotParse_GivesTheClaimsItCanRead() =>
        Read("[SqlSource.SqlSourceGenerate(Path = \"Q\")]\ninternal partial class Queries {\n    void Broken( {\n")
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Q");

    // The text is searched for the word before anything is parsed, so a spelling that hides it is not found.
    [Fact]
    public void Read_FileWithoutTheWord_IsNotParsed() =>
        Read(OnAClass("[SqlSource\\u0047enerate]")).Claims.ShouldBeEmpty();

    [Fact]
    public void Read_FileThatIsNotOnTheDisk_IsSteppedOver()
    {
        var claims = AttributeReader.Read(Manifest("", [], _folder.PathOf("App/Gone.cs")));

        claims.Claims.ShouldBeEmpty();
        claims.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(new[] { "FEATURE" }, 1)]
    [InlineData(new[] { "OTHER" }, 0)]
    public void Read_AttributeInsideIf_IsReadWhenTheManifestHasTheConstant(string[] constants, int expected) =>
        Read("#if FEATURE\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class Queries;\n", "", constants)
            .Claims.Length.ShouldBe(expected);

    // The constants of a framework are in a manifest because its target depends on the SDK's target that adds them.
    [Fact]
    public void Read_AttributeInsideIfOfAFramework_IsReadWithTheConstantsOfTheManifest() =>
        Read(
                "#if NET8_0_OR_GREATER\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class Queries;\n",
                "",
                "TRACE",
                "NET",
                "NET10_0",
                "NET8_0_OR_GREATER"
            )
            .Claims.Length.ShouldBe(1);

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("preview")]
    [InlineData("default")]
    [InlineData("12.0")]
    [InlineData("no version")]
    public void Read_AnyLangVersion_IsRead(string langVersion) =>
        Read(OnAClass("[SqlSourceGenerate]"), langVersion).Claims.Length.ShouldBe(1);

    [Fact]
    public void Read_FileOfTheNewestCSharp_IsRead() =>
        Read(
                """
                [SqlSource.SqlSourceGenerate(Path = "Queries")]
                internal partial class Repository(int size)
                {
                    private readonly int[] _sizes = [1, 2, .. new[] { 3 }];

                    public int Size
                    {
                        get => field;
                        set => field = value < 0 ? size : value;
                    }
                }
                """
            )
            .Claims.ShouldHaveSingleItem()
            .Path.ShouldBe("Queries");
}
```

- [ ] **Step 3: Run them to see them fail**

```bash
dotnet build tests/SqlSource.Tool.Tests
```

Expected: the build fails with CS0234 for the namespace `SqlSource.Tool.Planning`.

- [ ] **Step 4: Implement**

In `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`, add `using SqlSource.Diagnostics;` and, after `ForFile`:

```csharp
    /// <summary>
    /// An error at a position in a file.
    /// </summary>
    /// <param name="descriptor">What is wrong.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="position">Where in the file, counted from zero.</param>
    /// <param name="arguments">The text the descriptor's message quotes.</param>
    public static ToolDiagnostic At(
        DiagnosticDescriptor descriptor,
        string path,
        LinePosition position,
        params string[] arguments
    ) => Create(descriptor, arguments) with { Path = path, Position = position };

    /// <summary>
    /// An error at a position that the generator's code gave.
    /// </summary>
    public static ToolDiagnostic At(DiagnosticDescriptor descriptor, LocationInfo location, params string[] arguments) =>
        At(descriptor, location.Path, location.LineSpan.Start, arguments);

    /// <summary>
    /// An error that the generator's code found, in the tool's form: at the start of where the generator has it.
    /// </summary>
    public static ToolDiagnostic From(DiagnosticInfo diagnostic) =>
        new(
            diagnostic.Descriptor,
            diagnostic.Location.Path,
            diagnostic.Location.LineSpan.Start,
            diagnostic.Arguments,
            EquatableArray<ContinuationLine>.Empty
        );
```

Create `src/SqlSource.Tool/Planning/TypeClaim.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What one <c>[SqlSourceGenerate]</c> says that changes what is described: where its type's <c>.sql</c> files are,
/// and what is generated for them.
/// </summary>
/// <param name="SourcePath">The full path of the C# file that carries the attribute.</param>
/// <param name="Path">The attribute's <c>Path</c> as written, or null when it is not set or is empty.</param>
/// <param name="Output">The attribute's <c>Output</c>, or null when it is not set.</param>
/// <param name="Position">Where the attribute starts in the file, counted from zero.</param>
internal sealed record TypeClaim(string SourcePath, string? Path, OutputKind? Output, LinePosition Position);
```

Create `src/SqlSource.Tool/Planning/ProjectClaims.cs`:

```csharp
using System.Collections.Immutable;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The claims of one project, and the errors of reading them.
/// </summary>
/// <param name="Claims">The claims, in the order of the project's files and then of each file's text.</param>
/// <param name="Errors">An <c>SQLSRC208</c> for each argument that could not be read.</param>
internal sealed record ProjectClaims(ImmutableArray<TypeClaim> Claims, ImmutableArray<ToolDiagnostic> Errors);
```

Create `src/SqlSource.Tool/Planning/AttributeReader.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads <c>[SqlSourceGenerate]</c> from the C# files of a project, as syntax.  The generator's reader works on
/// symbols, and the tool has no compilation, so there is nothing of it to share.
/// </summary>
/// <remarks>
/// It matches the attribute by its name and reads <c>Path</c> and <c>Output</c> alone, each as it is written:
/// nothing else on the attribute changes what is described.  What it cannot see is in <c>docs/tech-debt/TD-0031</c>:
/// an alias, a type of the same name in another namespace, and an attribute inside <c>#if</c> under a configuration
/// other than the manifest's.
/// </remarks>
internal static class AttributeReader
{
    private const string ShortName = "SqlSourceGenerate";

    private const string LongName = "SqlSourceGenerateAttribute";

    private const string OutputType = "GeneratorOutput";

    public static ProjectClaims Read(ProjectManifest manifest)
    {
        var options = ParseOptionsOf(manifest);
        var claims = ImmutableArray.CreateBuilder<TypeClaim>();
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();
        foreach (var path in manifest.CompileFiles)
        {
            // A file that cannot be read is the build's to report.  Most files do not hold the word, and are not
            // parsed.
            if (ReadText(path) is not { } text || !text.ToString().Contains(ShortName, StringComparison.Ordinal))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text, options, path).GetRoot();
            // Into a namespace and a type, for a nested type, and into nothing else: no body of a member is walked.
            foreach (
                var node in root.DescendantNodes(static node =>
                    node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax
                )
            )
            {
                if (TargetTypeReader.IsCandidate(node) && FindAttribute((TypeDeclarationSyntax)node) is { } attribute)
                {
                    ReadAttribute(attribute, path, claims, errors);
                }
            }
        }

        return new ProjectClaims(claims.ToImmutable(), errors.ToImmutable());
    }

    // The language version of the project, so that a word is a keyword where the compiler takes it for one, and the
    // project's constants, so that #if is read as the compiler reads it.
    private static CSharpParseOptions ParseOptionsOf(ProjectManifest manifest) =>
        new(
            manifest.LangVersion.Length > 0 && LanguageVersionFacts.TryParse(manifest.LangVersion, out var version)
                ? version
                : LanguageVersion.Latest,
            DocumentationMode.None,
            SourceCodeKind.Regular,
            manifest.DefineConstants
        );

    private static SourceText? ReadText(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SourceText.From(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The first attribute of the name in a list with no target or the target "type".
    private static AttributeSyntax? FindAttribute(TypeDeclarationSyntax declaration)
    {
        foreach (var list in declaration.AttributeLists)
        {
            if (list.Target is { } target && !target.Identifier.IsKind(SyntaxKind.TypeKeyword))
            {
                continue;
            }

            foreach (var attribute in list.Attributes)
            {
                if (LastIdentifier(attribute.Name) is ShortName or LongName)
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static void ReadAttribute(
        AttributeSyntax attribute,
        string path,
        ImmutableArray<TypeClaim>.Builder claims,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        string? typePath = null;
        OutputKind? output = null;
        var readPath = false;
        var readOutput = false;
        var isRead = true;
        if (attribute.ArgumentList is { } list)
        {
            foreach (var argument in list.Arguments)
            {
                // The first of a name, as the generator takes it; the compiler rejects a second.
                var name = argument.NameEquals?.Name.Identifier.ValueText;
                if (name == AttributeSource.PathProperty && !readPath)
                {
                    readPath = true;
                    if (!TryReadPath(argument.Expression, out typePath))
                    {
                        isRead = false;
                        errors.Add(NotALiteral(path, argument.Expression, AttributeSource.PathProperty));
                    }
                }
                else if (name == AttributeSource.OutputProperty && !readOutput)
                {
                    readOutput = true;
                    if (!TryReadOutput(argument.Expression, out output))
                    {
                        isRead = false;
                        errors.Add(NotALiteral(path, argument.Expression, AttributeSource.OutputProperty));
                    }
                }
            }
        }

        // A claim that could not be read is not planned: its files may be described for another type.
        if (isRead)
        {
            claims.Add(new TypeClaim(path, typePath, output, StartOf(attribute)));
        }
    }

    // A string literal of any kind.  null, default and the empty string are a Path that is not set, as in the
    // generator, where a string of white space is a path that matches nothing.
    private static bool TryReadPath(ExpressionSyntax expression, out string? path)
    {
        path = null;
        if (expression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var text = ((LiteralExpressionSyntax)expression).Token.ValueText;
            path = text.Length > 0 ? text : null;
            return true;
        }

        return expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression);
    }

    // GeneratorOutput.Models, however GeneratorOutput is qualified.  Another member is not set: the compiler
    // rejects it.
    private static bool TryReadOutput(ExpressionSyntax expression, out OutputKind? output)
    {
        output = null;
        if (
            expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || LastIdentifier(access.Expression) != OutputType
        )
        {
            return false;
        }

        output = access.Name.Identifier.ValueText switch
        {
            nameof(OutputKind.Sql) => OutputKind.Sql,
            nameof(OutputKind.Models) => OutputKind.Models,
            nameof(OutputKind.CodeGen) => OutputKind.CodeGen,
            _ => null,
        };
        return true;
    }

    // The last identifier of a name, however it is qualified: as the name of an attribute, or as an expression.
    private static string? LastIdentifier(ExpressionSyntax name) =>
        name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };

    private static ToolDiagnostic NotALiteral(string path, ExpressionSyntax expression, string argument) =>
        ToolDiagnostic.At(ToolDiagnostics.AttributeArgumentNotLiteral, path, StartOf(expression), argument);

    private static LinePosition StartOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition;
}
```

- [ ] **Step 5: Run the tests**

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.AttributeReaderTests
```

Expected: PASS.  If `Read_FileOfTheNewestCSharp_IsRead` fails on `field`, the Roslyn of the tool is older than this plan assumes: check `VersionOverride` in `src/SqlSource.Tool/SqlSource.Tool.csproj`, and tell the owner rather than weaken the test.

- [ ] **Step 6: The tech-debt item**

Create `docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md`:

```markdown
# TD-0031 - The tool reads `[SqlSourceGenerate]` as syntax, and misreads what only a compilation can tell

## Problem

[`AttributeReader`](../../src/SqlSource.Tool/Planning/AttributeReader.cs) finds the attribute by its name in the text of a project's C# files.  The generator finds it by its symbol.  Three things are therefore read differently by the two:

- **An alias.**  `using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]` is a claim for the generator and nothing for the tool, so the type's queries are never described and, from phase 5, its build says that their entries are missing.
- **A type of the same name.**  A project's own `SqlSourceGenerateAttribute` in another namespace is a claim for the tool and nothing for the generator.  The tool describes queries that nothing uses.
- **`#if` under another configuration.**  The tool reads the files with the constants of the manifest, which are those of the configuration that `dotnet msbuild` evaluates, `Debug` unless the environment says otherwise, and of the first target framework.  An attribute inside `#if RELEASE` is not seen.

A value that is not a literal is not among them: the tool reports it, `SQLSRC208`.

## Why it exists

The tool has no compilation.  Making one means resolving every reference of the project, which is a design-time build: slow, and the kind of thing the tool avoids by reading a manifest.  The epic chose the syntax reader with this cost in view.

## Impact

Low.  Each case takes a project that does something unusual with the attribute's name, and the first is found at once from phase 5, when the build reports the missing entries.

## Proposed fix

For the alias: read the `using` aliases of a file, and of the project's global usings, and match an attribute through them.  For the configuration: let `describe` take the configuration to evaluate, as `dotnet build -c` does.  The type of the same name needs symbols, and is left.

## Trigger

A user reports a type whose queries the tool does not describe, or describes and should not.
```

In `docs/tech-debt/README.md`, set `Next id: \`TD-0032\``, and add after the row of `TD-0030`:

```
| [TD-0031](TD-0031-attribute-reader-of-the-tool-is-syntax-only.md) | Open | 2026-10-10 | Low | The `sqlsource` tool reads `[SqlSourceGenerate]` as syntax: an alias for the attribute is not seen, a type of the same name is taken for it, and an attribute inside `#if` is read under the configuration of the manifest alone |
```

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src tests docs
git commit -m "Read the attribute from the source, as syntax

The tool has no compilation.  It finds [SqlSourceGenerate] by name and
reads Path and Output as they are written; anything but a literal is
SQLSRC208.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: A project of a test

**Files:**
- Create: `tests/SqlSource.Tool.Tests/TestProject.cs`, `tests/SqlSource.Tool.Tests/TestProjectTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/FakeProject.cs`

**Interfaces:**
- Produces: `TestProject(TempFolder folder, string name = "App", string? directory = null)`: the project file is `<folder>/<directory>/<name>.csproj`, and `directory` is `name` when null and the folder itself when empty
- Produces: `string ProjectPath`, `string LangVersion { get; set; }`, `List<string> Constants`, `Dictionary<string, string> Properties`
- Produces: `string PathOf(string relativePath)`, `string AddSource(string relativePath, string text)`, `string AddSql(string relativePath, string text, params (string Name, string Value)[] metadata)`, `string AddSqlBytes(string relativePath, byte[] bytes)`, `void ListSql(string fullPath, params (string Name, string Value)[] metadata)`
- Produces: `string ManifestText()`, `ProjectManifest Manifest()`, `TestProject AnsweredBy(FakeProcessRunner runner)`
- Produces: `FakeProject.ManifestWriter : Func<string>?`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/TestProjectTests.cs`:

```csharp
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestProject stands in for a project and for MSBuild's answer about it.  These hold it to the format of a manifest.
public class TestProjectTests
{
    [Fact]
    public void Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder) { LangVersion = "12.0" };
        project.Constants.AddRange(["DEBUG", "NET10_0"]);
        project.Properties["SqlSourceDialect"] = "postgres";
        var source = project.AddSource("Queries.cs", "internal static partial class Queries;");
        var sql = project.AddSql("Queries/Users.sql", "SELECT 1;", ("SqlSourceDatabase", "billing"));

        var manifest = project.Manifest();

        manifest.ProjectPath.ShouldBe(folder.PathOf("App/App.csproj"));
        manifest.LangVersion.ShouldBe("12.0");
        manifest.DefineConstants.ShouldBe(["DEBUG", "NET10_0"]);
        manifest.Properties["SqlSourceDialect"].ShouldBe("postgres");
        manifest.CompileFiles.ShouldBe([source]);
        var file = manifest.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(sql);
        file.Metadata["SqlSourceDatabase"].ShouldBe("billing");
    }

    [Fact]
    public void ProjectPath_EmptyDirectory_IsInTheFolderItself()
    {
        using var folder = new TempFolder();

        new TestProject(folder, directory: "").ProjectPath.ShouldBe(folder.PathOf("App.csproj"));
    }

    [Fact]
    public async Task AnsweredBy_Runner_GivesTheRunTheManifestAsItIsWhenAsked()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        // Added after the runner was told of the project.
        _ = project.AddSql("Queries/Users.sql", "SELECT 1;");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

```bash
dotnet build tests/SqlSource.Tool.Tests
```

Expected: CS0246 for `TestProject`.

- [ ] **Step 3: Implement**

In `tests/SqlSource.Tool.Tests/FakeProject.cs`, after `Manifest`:

```csharp
    // Writes the text of the manifest when the target runs, for a project whose files a test adds to as it goes.
    public Func<string>? ManifestWriter { get; set; }
```

with `using System;` at the top, and in `WriteManifest` write the file from

```csharp
                ManifestWriter?.Invoke()
                    ?? Manifest
                    ?? $"SqlSourceManifest=1\nProject={request.Arguments[1]}\nTargetFramework={framework}\n"
```

Create `tests/SqlSource.Tool.Tests/TestProject.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Tests;

// A project in a temporary folder: C# and .sql files on the disk, and the manifest that the package's target would
// write for them, built here.  A test of the plan gives RunPlanner the manifest; a test of the command gives the
// runner the project, and the two runs of "dotnet msbuild" are answered from it.
internal sealed class TestProject
{
    private readonly TempFolder _folder;
    private readonly string _directory;
    private readonly List<string> _compile = [];
    private readonly List<(string Path, (string Name, string Value)[] Metadata)> _sql = [];

    public TestProject(TempFolder folder, string name = "App", string? directory = null)
    {
        _folder = folder;
        _directory = directory ?? name;
        ProjectPath = folder.WriteFile(Relative(name + ".csproj"), "<Project />");
    }

    public string ProjectPath { get; }

    public string LangVersion { get; set; } = "";

    public List<string> Constants { get; } = [];

    // The properties of the package, by the name MSBuild knows them by.
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

    // The full path of a file of the project, from a path with forward slashes.
    public string PathOf(string relativePath) => _folder.PathOf(Relative(relativePath));

    // A C# file on the disk that the project compiles.
    public string AddSource(string relativePath, string text)
    {
        var path = _folder.WriteFile(Relative(relativePath), text);
        _compile.Add(path);
        return path;
    }

    // A .sql file on the disk that the project lists, with the metadata of its item.
    public string AddSql(string relativePath, string text, params (string Name, string Value)[] metadata)
    {
        var path = _folder.WriteFile(Relative(relativePath), text);
        _sql.Add((path, metadata));
        return path;
    }

    // The same with the bytes as they are, for a byte order mark.
    public string AddSqlBytes(string relativePath, byte[] bytes)
    {
        var path = _folder.WriteFile(Relative(relativePath));
        File.WriteAllBytes(path, bytes);
        _sql.Add((path, []));
        return path;
    }

    // A .sql file that the project lists and this helper does not write: another project's, or one that is gone.
    public void ListSql(string fullPath, params (string Name, string Value)[] metadata) =>
        _sql.Add((fullPath, metadata));

    public string ManifestText()
    {
        var text = new StringBuilder();
        _ = text.Append("SqlSourceManifest=1\n");
        _ = text.Append("Project=").Append(ProjectPath).Append('\n');
        _ = text.Append("TargetFramework=net10.0\n");
        _ = text.Append("LangVersion=").Append(LangVersion).Append('\n');
        _ = text.Append("DefineConstants=").AppendJoin(';', Constants).Append('\n');
        foreach (var (name, value) in Properties)
        {
            _ = text.Append("Property.").Append(name).Append('=').Append(value).Append('\n');
        }

        foreach (var (path, metadata) in _sql)
        {
            _ = text.Append("File=").Append(path).Append('\n');
            foreach (var (name, value) in metadata)
            {
                _ = text.Append("File.").Append(name).Append('=').Append(value).Append('\n');
            }
        }

        foreach (var path in _compile)
        {
            _ = text.Append("Compile=").Append(path).Append('\n');
        }

        return text.ToString();
    }

    public ProjectManifest Manifest() =>
        ManifestReader.Read(ManifestText(), out var reason) ?? throw new InvalidOperationException(reason);

    // Has the runner answer for this project with its manifest as it is when the target runs.
    public TestProject AnsweredBy(FakeProcessRunner runner)
    {
        runner.Projects[ProjectPath] = new FakeProject { ManifestWriter = ManifestText };
        return this;
    }

    private string Relative(string relativePath) =>
        _directory.Length == 0 ? relativePath : _directory + "/" + relativePath;
}
```

- [ ] **Step 4: Run the tests**

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.TestProjectTests
```

Expected: PASS.

- [ ] **Step 5: Closing steps, and commit**

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Give the tool's tests a project that answers for MSBuild

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The plan of a project

**Files:**
- Create in `src/SqlSource.Tool/Planning/`: `ManifestOptions.cs`, `SqlFileText.cs`, `ListedFiles.cs`, `QueryProblems.cs`, `PlannedQuery.cs`, `PlannedFileState.cs`, `PlannedFile.cs`, `PlannedDatabase.cs`, `RunPlan.cs`, `RunPlanResult.cs`, `RunPlanner.cs`
- Create: `src/SqlSource.Tool/DescribeCommand.cs`; modify `src/SqlSource.Tool/Cli.cs`
- Create: `docs/tech-debt/TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md`; modify `docs/tech-debt/README.md`
- Test: create `tests/SqlSource.Tool.Tests/RunPlannerTests.cs`, `tests/SqlSource.Tool.Tests/RunPlannerErrorTests.cs`; modify `tests/SqlSource.Tool.Tests/CliRun.cs`, `tests/SqlSource.Tool.Tests/DescribeTests.cs`

**Interfaces:**
- Consumes: `AttributeReader.Read`, `TypeClaim`, `ToolDiagnostic.At`, `ToolDiagnostic.From` (task 3); `SqlPath.ToSortedSet`, `PathResolver.FindFiles` (task 2); `TestProject` (task 4)
- Produces: `[Flags] enum QueryProblems { None = 0, TokenWithoutDefault = 1, DatabaseDialectConflict = 2 }`
- Produces: `enum PlannedFileState { Ready, HasParseErrors, NotDescribable }`
- Produces: `sealed record PlannedQuery(SqlQuery Query, bool NeedsEntry, string? Database, string? Hash, QueryProblems Problems = QueryProblems.None, bool IsSelected = true)`
- Produces: `sealed record PlannedFile(string Path, string NormalizedPath, string ProjectPath, SqlDialect Dialect, PlannedFileState State, EquatableArray<PlannedQuery> Queries)`
- Produces: `sealed record PlannedDatabase(string Name, SqlDialect Dialect)`, `sealed record RunPlan(EquatableArray<PlannedFile> Files, EquatableArray<PlannedDatabase> Databases)`, `sealed record RunPlanResult(RunPlan Plan, EquatableArray<ToolDiagnostic> Errors)`
- Produces: `ListedFiles.Read(ProjectManifest) : ListedFiles`, with `EquatableArray<string> Paths`, `ManifestFile this[string normalizedPath]` and `ImmutableArray<string> ClaimedBy(TypeClaim claim)`
- Produces: `RunPlanner.Plan(ImmutableArray<ProjectManifest> manifests, CancellationToken cancellationToken) : RunPlanResult`.  Task 8 adds a `RunFilters` parameter before the token.
- Produces: `DescribeCommand.Name`, `DescribeCommand.Create(ToolHost host, Reporter reporter) : Command`, `DescribeCommand.PlanAsync(ParseResult parsed, ToolHost host, Reporter reporter, CancellationToken cancellationToken) : Task<RunPlan?>`
- Produces: `CliRun.PlanAsync(params string[] args) : Task<(RunPlan? Plan, CliResult Result)>`

After this task `State` is `Ready` or `HasParseErrors`, `Problems` is `None`, `IsSelected` is true and `RunPlan.Databases` is empty: tasks 6, 7 and 8 set them.

- [ ] **Step 1: Write the failing tests of the plan**

Create `tests/SqlSource.Tool.Tests/RunPlannerTests.cs`:

```csharp
using System;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan of a run, from manifests that a test builds: which files are claimed, and what each query of them needs.
public sealed class RunPlannerTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static RunPlanResult Plan(params TestProject[] projects) =>
        RunPlanner.Plan([.. projects.Select(project => project.Manifest())], TestContext.Current.CancellationToken);

    // A type with the attribute, in a file of its own.
    private static string Type(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({arguments})]\ninternal static partial class {name};\n";

    private static (string Name, string Value)[] Metadata(string name, string? value) =>
        value is null ? [] : [(name, value)];

    private TestProject Postgres(string name = "App")
    {
        var project = new TestProject(_folder, name);
        project.Properties["SqlSourceDialect"] = "postgres";
        return project;
    }

    [Fact]
    public void Plan_ExampleOfTheSpec_HasThreeQueriesWithTheirValues()
    {
        var project = Postgres();
        _ = project.AddSource(
            "Queries.cs",
            Type("Queries", "Path = \"Queries\", Output = SqlSource.GeneratorOutput.Models")
        );
        var sql = project.AddSql(
            "Queries/Users.sql",
            """
            -- name: GetUser
            SELECT id, name FROM users WHERE id = @id;

            -- name: CountUsers
            -- output: sql
            SELECT count(*) FROM users;

            -- name: GetInvoice
            -- database: billing
            SELECT id FROM invoices WHERE id = @id;
            """
        );

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(sql);
        file.ProjectPath.ShouldBe(project.ProjectPath);
        file.Dialect.ShouldBe(SqlDialect.PostgreSql);
        file.State.ShouldBe(PlannedFileState.Ready);
        file.Queries.Select(query => (query.Query.Name, query.NeedsEntry, query.Database))
            .ShouldBe([
                ("GetUser", true, "postgres"),
                ("CountUsers", false, (string?)null),
                ("GetInvoice", true, "billing"),
            ]);

        var getUser = file.Queries[0];
        getUser.Hash.ShouldBe(
            SqlQueryHash.Compute(
                SqlDialect.PostgreSql,
                getUser.Query.Segments,
                getUser.Query.Tokens,
                getUser.Query.Parameters
            )
        );
        getUser.Hash!.Length.ShouldBe(64);
        file.Queries[1].Hash.ShouldBeNull();
    }

    [Fact]
    public void Plan_NoPath_ClaimsTheFilesBesideTheSourceFileAndNotThoseBelow()
    {
        var project = Postgres();
        _ = project.AddSource("Repo/Repository.cs", Type("Repository"));
        var beside = project.AddSql("Repo/A.sql", "SELECT 1;");
        _ = project.AddSql("Repo/Sub/B.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([beside]);
    }

    [Fact]
    public void Plan_PathOfAFile_ClaimsThatFile()
    {
        var project = Postgres();
        _ = project.AddSource("Repository.cs", Type("Repository", "Path = \"Queries/b.SQL\""));
        _ = project.AddSql("Queries/A.sql", "SELECT 1;");
        var named = project.AddSql("Queries/B.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([named]);
    }

    [Fact]
    public void Plan_Files_AreInTheOrderOfTheirPaths()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        var b = project.AddSql("b.sql", "SELECT 1;");
        var a = project.AddSql("A.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([a, b]);
    }

    [Theory]
    [InlineData("Sql", "Models", true)]
    [InlineData("Sql", "Sql", false)]
    public void Plan_TwoTypesThatClaimOneFile_NeedWhatEitherNeeds(string first, string second, bool needsEntry)
    {
        var project = Postgres();
        _ = project.AddSource("A.cs", Type("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}"));
        _ = project.AddSource("B.cs", Type("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}"));
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plan(project).Plan.Files.ShouldHaveSingleItem();

        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBe(needsEntry);
    }

    [Fact]
    public void Plan_FileThatNoTypeClaims_IsNotReadThoughItDoesNotParse()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries", "Path = \"Queries\""));
        _ = project.AddSql("Queries/One.sql", "SELECT 1;");
        _ = project.AddSql("Migrations/Broken.sql", "-- name: Broken\nSELECT 'open\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(1);
    }

    // Review focus 3.  The build reports a Path that matches nothing.
    [Theory]
    [InlineData("Nowhere")]
    [InlineData("Nowhere.sql")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../../../../../..")]
    public void Plan_PathThatNamesNothing_PlansNothingAndReportsNothing(string path)
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries", $"Path = \"{path}\""));
        _ = project.AddSql("Q.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(null, null, null, null, null, true)]
    [InlineData("sql", null, null, null, null, false)]
    [InlineData("sql", "models", null, null, null, true)]
    [InlineData("models", "models", "Sql", null, null, false)]
    [InlineData("sql", "sql", "Sql", "models", null, true)]
    [InlineData("models", "models", "Models", "models", "sql", false)]
    public void Plan_OutputFromEachLevel_IsTakenFromTheMostSpecific(
        string? property,
        string? metadata,
        string? attribute,
        string? preamble,
        string? query,
        bool needsEntry
    )
    {
        var project = Postgres();
        if (property is not null)
        {
            project.Properties["SqlSourceOutput"] = property;
        }

        _ = project.AddSource(
            "Queries.cs",
            Type("Queries", attribute is null ? "" : $"Output = SqlSource.GeneratorOutput.{attribute}")
        );
        _ = project.AddSql(
            "Q.sql",
            (preamble is null ? "" : $"-- output: {preamble}\n")
                + "-- name: One\n"
                + (query is null ? "" : $"-- output: {query}\n")
                + "SELECT 1;\n",
            Metadata("SqlSourceOutput", metadata)
        );

        var planned = Plan(project).Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();

        planned.NeedsEntry.ShouldBe(needsEntry);
        (planned.Hash is not null).ShouldBe(needsEntry);
        (planned.Database is not null).ShouldBe(needsEntry);
    }

    [Theory]
    [InlineData(null, null, null, null, "postgres")]
    [InlineData("main", null, null, null, "main")]
    [InlineData("main", "files", null, null, "files")]
    [InlineData("main", "files", "pre", null, "pre")]
    [InlineData("main", "files", "pre", "own", "own")]
    public void Plan_DatabaseFromEachLevel_IsTakenFromTheMostSpecificAndElseIsTheDialectsName(
        string? property,
        string? metadata,
        string? preamble,
        string? query,
        string expected
    )
    {
        var project = new TestProject(_folder);
        // An alias: the name of a database is the dialect's first name.
        project.Properties["SqlSourceDialect"] = "postgresql";
        if (property is not null)
        {
            project.Properties["SqlSourceDatabase"] = property;
        }

        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql(
            "Q.sql",
            (preamble is null ? "" : $"-- database: {preamble}\n")
                + "-- name: One\n"
                + (query is null ? "" : $"-- database: {query}\n")
                + "SELECT 1;\n",
            Metadata("SqlSourceDatabase", metadata)
        );

        Plan(project).Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem().Database.ShouldBe(expected);
    }

    [Fact]
    public void Plan_DialectOfAFile_IsItsMarkerThenItsMetadataThenTheProperty()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", "SELECT 1;");
        _ = project.AddSql("B.sql", "SELECT 1;", ("SqlSourceDialect", "mssql"));
        _ = project.AddSql("C.sql", "-- dialect: postgres\nSELECT 1;", ("SqlSourceDialect", "mssql"));

        Plan(project)
            .Plan.Files.Select(file => file.Dialect)
            .ShouldBe([SqlDialect.PostgreSql, SqlDialect.SqlServer, SqlDialect.PostgreSql]);
    }

    [Fact]
    public void Plan_FileWithAByteOrderMarkOrCarriageReturns_HashesAsTheFileWithout()
    {
        const string Sql = "-- name: One\nSELECT id\nFROM users\nWHERE id = @id;\n";
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", Sql);
        _ = project.AddSqlBytes("B.sql", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Sql)]);
        _ = project.AddSql("C.sql", Sql.Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        var hashes = result.Plan.Files.Select(file => file.Queries.ShouldHaveSingleItem().Hash).ToArray();
        hashes.Length.ShouldBe(3);
        hashes.Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
    }

    [Fact]
    public void Plan_TwoListedPathsThatDifferOnlyByCase_PlanTheFirst()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        var first = project.AddSql("Users.sql", "SELECT 1;");
        project.ListSql(project.PathOf("USERS.SQL"));

        Plan(project).Plan.Files.ShouldHaveSingleItem().Path.ShouldBe(first);
    }
}
```

Create `tests/SqlSource.Tool.Tests/RunPlannerErrorTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// What is wrong with a plan: each row of the spec's table, with its id, its place and what it does to the plan.
public sealed class RunPlannerErrorTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static RunPlanResult Plan(params TestProject[] projects) =>
        RunPlanner.Plan([.. projects.Select(project => project.Manifest())], TestContext.Current.CancellationToken);

    private static string Type(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({arguments})]\ninternal static partial class {name};\n";

    // A project whose dialect can be described, with one type that claims the folder "Q".
    private TestProject Project(string name = "App", string dialect = "postgres")
    {
        var project = new TestProject(_folder, name);
        project.Properties["SqlSourceDialect"] = dialect;
        _ = project.AddSource("Queries.cs", Type("Queries", "Path = \"Q\""));
        return project;
    }

    [Fact]
    public void Plan_FileWithAParseError_ReportsItWhereTheGeneratorDoesAndPlansNoQuery()
    {
        var project = Project();
        var sql = project.AddSql("Q/Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = Plan(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.Id.ShouldBe("SQLSRC101");
        error.Path.ShouldBe(sql);
        error.Position.ShouldBe(new LinePosition(1, 7));
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.HasParseErrors);
        file.Queries.Count.ShouldBe(0);
    }

    // The generator parses a file it cannot read as empty.
    [Fact]
    public void Plan_ClaimedFileThatIsNotOnTheDisk_IsReportedAsAFileWithoutSql()
    {
        var project = Project();
        var gone = project.PathOf("Q/Gone.sql");
        project.ListSql(gone);

        var result = Plan(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.Id.ShouldStartWith("SQLSRC1");
        error.Path.ShouldBe(gone);
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.HasParseErrors);
    }

    [Fact]
    public void Plan_PropertyThatIsNoDialect_IsSqlsrc011AtTheProjectAndTheFileIsReadAsAnsi()
    {
        var project = Project(dialect: "nosuch");
        // Nothing needs an entry, so that the dialect is all that is wrong.
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "nosuch")]);
        result.Plan.Files.ShouldHaveSingleItem().Dialect.ShouldBe(SqlDialect.Ansi);
    }

    [Fact]
    public void Plan_MetadataThatIsNoDialect_IsSqlsrc011OnceForEachValueOfAClaimedFile()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSql("Q/A.sql", "SELECT 1;", ("SqlSourceDialect", "bad-one"));
        _ = project.AddSql("Q/B.sql", "SELECT 1;", ("SqlSourceDialect", "bad-one"));
        _ = project.AddSql("Q/C.sql", "SELECT 1;", ("SqlSourceDialect", "another"));
        _ = project.AddSql("Unclaimed/D.sql", "SELECT 1;", ("SqlSourceDialect", "never-read"));

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "another"),
            ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "bad-one"),
        ]);
        result.Plan.Files.Select(file => file.Dialect).ShouldAllBe(dialect => dialect == SqlDialect.Ansi);
    }

    [Fact]
    public void Plan_OutputAndDatabaseThatAreNotValid_AreSqlsrc014AtTheProjectAndAreNotSet()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "everything";
        project.Properties["SqlSourceDatabase"] = "not a name";
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.ForFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "not a name",
                "SqlSourceDatabase"
            ),
            ToolDiagnostic.ForFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "everything",
                "SqlSourceOutput"
            ),
        ]);
        var query = result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();
        query.NeedsEntry.ShouldBeTrue();
        query.Database.ShouldBe("postgres");
    }

    [Fact]
    public void Plan_MetadataThatIsNotValid_IsSqlsrc014ForAClaimedFileAlone()
    {
        var project = Project();
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceOutput", "bad"), ("SqlSourceDatabase", "a b"));
        _ = project.AddSql("Unclaimed/Two.sql", "SELECT 1;", ("SqlSourceOutput", "never-read"));

        Plan(project)
            .Errors.Select(error => (error.Descriptor.Id, error.Arguments[0], error.Arguments[1]))
            .ShouldBe([("SQLSRC014", "a b", "SqlSourceDatabase"), ("SQLSRC014", "bad", "SqlSourceOutput")]);
    }

    // The build reports the settings that the tool does not read.
    [Fact]
    public void Plan_SettingTheToolDoesNotRead_IsNotReportedThoughItIsNotValid()
    {
        var project = Project();
        project.Properties["SqlSourceGeneratorParameters"] = "keep-coments";
        project.Properties["SqlSourceInputModelSuffix"] = "a b";
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceCollectionType", "bag"));

        Plan(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_AttributeArgumentThatIsNoLiteral_IsSqlsrc208AndItsTypeClaimsNothing()
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("A.cs", Type("A", "Path = Folder"));
        _ = project.AddSource("B.cs", Type("B", "Path = \"Other\""));
        _ = project.AddSql("One.sql", "SELECT 1;");
        var other = project.AddSql("Other/Two.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(ToolDiagnostics.AttributeArgumentNotLiteral);
        result.Plan.Files.Select(file => file.Path).ShouldBe([other]);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

```bash
dotnet build tests/SqlSource.Tool.Tests
```

Expected: CS0103 for `RunPlanner`, and CS0246 for `RunPlanResult` and `PlannedFileState`.

- [ ] **Step 3: The values of a plan**

Create each of these in `src/SqlSource.Tool/Planning/`.

`QueryProblems.cs`:

```csharp
using System;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Why a query that needs an entry cannot be described.  A query can have both.
/// </summary>
[Flags]
internal enum QueryProblems
{
    /// <summary>Nothing: almost every query.</summary>
    None = 0,

    /// <summary>A token of the query has no default to stand in its place (<c>SQLSRC210</c>).</summary>
    TokenWithoutDefault = 1,

    /// <summary>The query's database has another dialect than the query's file (<c>SQLSRC211</c>).</summary>
    DatabaseDialectConflict = 2,
}
```

`PlannedQuery.cs`:

```csharp
using SqlSource.Generation;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One query of a claimed file, with what a run needs to know of it.
/// </summary>
/// <param name="Query">The query as the generator's parser read it.</param>
/// <param name="NeedsEntry">
/// Whether a sidecar must hold an entry for it: its output is <c>Models</c> or <c>CodeGen</c> for a type that claims
/// its file.
/// </param>
/// <param name="Database">
/// The database it belongs to, in the spelling the run first met.  Null for a query that needs no entry, which
/// belongs to none.
/// </param>
/// <param name="Hash">The hash of <c>SqlQueryHash</c>.  Null for a query that needs no entry.</param>
/// <param name="Problems">Why it cannot be described, when it cannot.</param>
/// <param name="IsSelected">Whether the filters of the run take it.  True in a run with no filter.</param>
internal sealed record PlannedQuery(
    SqlQuery Query,
    bool NeedsEntry,
    string? Database,
    string? Hash,
    QueryProblems Problems = QueryProblems.None,
    bool IsSelected = true
);
```

`PlannedFileState.cs`:

```csharp
namespace SqlSource.Tool.Planning;

/// <summary>
/// Whether the queries of a planned file can be described.
/// </summary>
internal enum PlannedFileState
{
    /// <summary>The file was parsed, and its dialect can be described or no query of it needs an entry.</summary>
    Ready,

    /// <summary>The file has parse errors, which were reported, and so has no queries.</summary>
    HasParseErrors,

    /// <summary>A query of the file needs an entry and its dialect cannot be described (<c>SQLSRC209</c>).</summary>
    NotDescribable,
}
```

`PlannedFile.cs`:

```csharp
using SqlSource.Parsing;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>.sql</c> file that a type of the run claims.
/// </summary>
/// <param name="Path">The full path, as the project lists it.</param>
/// <param name="NormalizedPath">The path in the form <c>SqlPath.Normalize</c> gives, which is what is compared.</param>
/// <param name="ProjectPath">The full path of the project it is planned under: the first that claims it.</param>
/// <param name="Dialect">The dialect it was read by.</param>
/// <param name="State">Whether its queries can be described.</param>
/// <param name="Queries">Its queries, in the file's order.  Every one, whatever the filters of the run.</param>
internal sealed record PlannedFile(
    string Path,
    string NormalizedPath,
    string ProjectPath,
    SqlDialect Dialect,
    PlannedFileState State,
    EquatableArray<PlannedQuery> Queries
);
```

`PlannedDatabase.cs`:

```csharp
using SqlSource.Parsing;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One logical database of a run.
/// </summary>
/// <param name="Name">The name, in the spelling the plan first holds.  Names are compared ignoring case.</param>
/// <param name="Dialect">The dialect of its queries, which picks the driver of its connection.</param>
internal sealed record PlannedDatabase(string Name, SqlDialect Dialect);
```

`RunPlan.cs`:

```csharp
namespace SqlSource.Tool.Planning;

/// <summary>
/// What a run is on: every file that a type claims, and the databases of the queries that need an entry.
/// </summary>
/// <param name="Files">The files, in the order of their projects and then of their paths.</param>
/// <param name="Databases">The databases, in the order the plan first holds a query of each.</param>
internal sealed record RunPlan(EquatableArray<PlannedFile> Files, EquatableArray<PlannedDatabase> Databases);
```

`RunPlanResult.cs`:

```csharp
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A plan, and what is wrong with it.  The planner reports nothing itself.
/// </summary>
/// <param name="Plan">The plan.  A file or a query with an error is in it, marked.</param>
/// <param name="Errors">The errors, in the order they are to be reported.</param>
internal sealed record RunPlanResult(RunPlan Plan, EquatableArray<ToolDiagnostic> Errors);
```

- [ ] **Step 4: The two adapters**

`ManifestOptions.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The values of a manifest under the keys the compiler would give them, so that the generator's own readers of
/// settings read them: <c>build_property.&lt;Name&gt;</c> for a property of the project, and
/// <c>build_metadata.SqlSourceSettingsFile.&lt;Name&gt;</c> for the metadata of a file.
/// </summary>
internal sealed class ManifestOptions : AnalyzerConfigOptions
{
    private const string PropertyPrefix = "build_property.";

    private const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    private readonly ImmutableDictionary<string, string> _values;

    private readonly string _prefix;

    private ManifestOptions(ImmutableDictionary<string, string> values, string prefix)
    {
        _values = values;
        _prefix = prefix;
    }

    public static ManifestOptions ForProject(ProjectManifest manifest) => new(manifest.Properties, PropertyPrefix);

    public static ManifestOptions ForFile(ManifestFile file) => new(file.Metadata, MetadataPrefix);

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        if (key.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return _values.TryGetValue(key[_prefix.Length..], out value);
        }

        value = null;
        return false;
    }
}
```

`SqlFileText.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A <c>.sql</c> file on the disk in the form the generator's reader takes one.
/// </summary>
/// <remarks>
/// A file that cannot be read has no text, and the generator parses that as an empty file.  A file that is not UTF-8
/// is not read as the compiler reads it: <c>docs/tech-debt/TD-0032</c>.
/// </remarks>
internal sealed class SqlFileText(string path) : AdditionalText
{
    public override string Path => path;

    public override SourceText? GetText(CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SourceText.From(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
```

`ListedFiles.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The <c>.sql</c> files of one project as the generator lists them: normalised, each once ignoring case, the first
/// spelling kept, in the order of <c>SqlPath.Comparer</c>.
/// </summary>
internal sealed class ListedFiles
{
    private readonly Dictionary<string, ManifestFile> _byPath;

    private ListedFiles(Dictionary<string, ManifestFile> byPath)
    {
        _byPath = byPath;
        Paths = SqlPath.ToSortedSet(byPath.Keys);
    }

    /// <summary>The normalised paths, as <c>PathResolver.FindFiles</c> takes them.</summary>
    public EquatableArray<string> Paths { get; }

    /// <summary>The file of a path of <see cref="Paths" />.</summary>
    public ManifestFile this[string normalizedPath] => _byPath[normalizedPath];

    public static ListedFiles Read(ProjectManifest manifest)
    {
        var byPath = new Dictionary<string, ManifestFile>(SqlPath.Comparer);
        foreach (var file in manifest.Files)
        {
            if (SqlPath.IsSqlFile(file.Path) && SqlPath.Normalize(file.Path) is { } path)
            {
                _ = byPath.TryAdd(path, file);
            }
        }

        return new ListedFiles(byPath);
    }

    /// <summary>The files that a claim's <c>Path</c> resolves to, by the generator's rule, in member order.</summary>
    public ImmutableArray<string> ClaimedBy(TypeClaim claim) =>
        PathResolver.FindFiles(claim.SourcePath, claim.Path, Paths);
}
```

- [ ] **Step 5: The planner**

`RunPlanner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Makes the plan of a run from the manifests of its projects: which files are claimed, and what each query of them
/// needs.  It reports nothing: what is wrong is in the result, in the order it is to be reported.
/// </summary>
/// <remarks>
/// The plan must agree with the generator on which type claims which file with which output, so every rule here is
/// the generator's own code: <c>PathResolver</c>, <c>MSBuildSettings</c>, <c>DialectSetting</c>,
/// <c>FileParseInput</c>, <c>SqlFileReader</c>, <c>QuerySettings</c> and <c>SqlQueryHash</c>.
/// <c>GeneratorParityTests</c> holds the two together.
/// </remarks>
internal static class RunPlanner
{
    /// <summary>The setting that the tool reads and the generator does not.</summary>
    public const string DatabaseSetting = "SqlSourceDatabase";

    public static RunPlanResult Plan(ImmutableArray<ProjectManifest> manifests, CancellationToken cancellationToken)
    {
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();

        // A file that two projects claim is planned once, under the first.
        var works = new List<FileWork>();
        var byPath = new Dictionary<string, FileWork>(SqlPath.Comparer);
        foreach (var manifest in manifests)
        {
            foreach (var claimed in ReadProject(manifest, errors))
            {
                if (byPath.TryGetValue(claimed.NormalizedPath, out var work))
                {
                    work.Claimants.Add(claimed);
                }
                else
                {
                    work = new FileWork(claimed);
                    byPath.Add(claimed.NormalizedPath, work);
                    works.Add(work);
                }
            }
        }

        var files = new List<PlannedFile>(works.Count);
        foreach (var work in works)
        {
            files.Add(PlanFile(work, errors, cancellationToken));
        }

        return new RunPlanResult(
            new RunPlan(new EquatableArray<PlannedFile>([.. files]), EquatableArray<PlannedDatabase>.Empty),
            new EquatableArray<ToolDiagnostic>(errors.ToImmutable())
        );
    }

    // The claimed files of one project, in the order of their paths, each with the settings of this project.  The
    // errors of the project are added: SQLSRC208, then SQLSRC011, then SQLSRC014.
    private static List<ClaimedFile> ReadProject(
        ProjectManifest manifest,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var claims = AttributeReader.Read(manifest);
        errors.AddRange(claims.Errors);

        var options = ManifestOptions.ForProject(manifest);
        var dialect = DialectSetting.ReadProperty(options);
        var settings = ProjectSettings.Read(options);

        // Each value once, in ordinal order, as the generator reports them.
        var invalidDialects = new SortedSet<string>(StringComparer.Ordinal);
        var invalidSettings = new HashSet<InvalidSetting>(settings.Invalid.Where(IsRead));
        if (dialect.InvalidValue is { } invalidProperty)
        {
            _ = invalidDialects.Add(invalidProperty);
        }

        var database = ReadDatabase(manifest.Properties, invalidSettings);

        // What each claim of a file says: its Output, and nothing else.
        var listed = ListedFiles.Read(manifest);
        var claimsOf = new Dictionary<string, List<SettingsLevel>>(SqlPath.Comparer);
        foreach (var claim in claims.Claims)
        {
            var level = claim.Output is { } output ? new SettingsLevel { Output = output } : SettingsLevel.None;
            foreach (var path in listed.ClaimedBy(claim))
            {
                if (!claimsOf.TryGetValue(path, out var levels))
                {
                    claimsOf.Add(path, levels = []);
                }

                levels.Add(level);
            }
        }

        // The metadata of a file that no type claims is not read, as nothing else of such a file is.
        var claimed = new List<ClaimedFile>(claimsOf.Count);
        foreach (var path in listed.Paths)
        {
            if (!claimsOf.TryGetValue(path, out var levels))
            {
                continue;
            }

            var file = listed[path];
            var metadata = FileMetadata.Read(new SqlFileText(file.Path), ManifestOptions.ForFile(file));
            if (metadata.Dialect.InvalidValue is { } invalidMetadata)
            {
                _ = invalidDialects.Add(invalidMetadata);
            }

            if (metadata.Settings is { } fileSettings)
            {
                invalidSettings.UnionWith(fileSettings.Invalid.Where(IsRead));
            }

            claimed.Add(
                new ClaimedFile(
                    manifest.ProjectPath,
                    path,
                    metadata,
                    dialect,
                    settings.Level,
                    ReadDatabase(file.Metadata, invalidSettings) ?? database,
                    levels
                )
            );
        }

        foreach (var value in invalidDialects)
        {
            errors.Add(ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, manifest.ProjectPath, value));
        }

        foreach (
            var setting in invalidSettings
                .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
                .ThenBy(static setting => setting.Value, StringComparer.Ordinal)
        )
        {
            errors.Add(
                ToolDiagnostic.ForFile(
                    SqlDiagnostics.InvalidSettingValue,
                    manifest.ProjectPath,
                    setting.Value,
                    setting.Name
                )
            );
        }

        return claimed;
    }

    // The generator's reader gives every setting that is not valid.  The tool reports the ones it reads: the build
    // reports the rest.
    private static bool IsRead(InvalidSetting setting) => setting.Name == MSBuildSettings.OutputName;

    private static string? ReadDatabase(ImmutableDictionary<string, string> values, HashSet<InvalidSetting> invalid)
    {
        if (!values.TryGetValue(DatabaseSetting, out var written) || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (SettingValue.IsDatabaseName(value.AsSpan()))
        {
            return value;
        }

        _ = invalid.Add(new InvalidSetting(DatabaseSetting, value));
        return null;
    }

    // Parses a claimed file once, with the dialect of the project it is planned under, and gives each query its
    // values.  Comments are never wanted: the hash comes from the SQL without them.
    private static PlannedFile PlanFile(
        FileWork work,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        var owner = work.Owner;
        var input = FileParseInput.Resolve(
            owner.Metadata,
            owner.ProjectDialect,
            projectKeepsComments: false,
            EquatableArray<string>.Empty
        );
        var parsed = SqlFileReader.Read(
            owner.Metadata.File,
            owner.NormalizedPath,
            input.Dialect,
            input.InvalidDialect,
            commentsWanted: false,
            cancellationToken
        );

        if (parsed.Errors.Count > 0)
        {
            errors.AddRange(parsed.Errors.Select(ToolDiagnostic.From));
            return new PlannedFile(
                owner.Metadata.File.Path,
                owner.NormalizedPath,
                owner.ProjectPath,
                parsed.Dialect,
                PlannedFileState.HasParseErrors,
                EquatableArray<PlannedQuery>.Empty
            );
        }

        var queries = ImmutableArray.CreateBuilder<PlannedQuery>(parsed.Queries.Count);
        foreach (var query in parsed.Queries)
        {
            var output = GreatestOutput(query, work);
            if (output == OutputKind.Sql)
            {
                // It needs no entry, and so belongs to no database and has no hash.
                queries.Add(new PlannedQuery(query, NeedsEntry: false, Database: null, Hash: null));
                continue;
            }

            queries.Add(
                new PlannedQuery(
                    query,
                    NeedsEntry: true,
                    query.Markers.Database ?? owner.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                    SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters)
                )
            );
        }

        return new PlannedFile(
            owner.Metadata.File.Path,
            owner.NormalizedPath,
            owner.ProjectPath,
            parsed.Dialect,
            PlannedFileState.Ready,
            new EquatableArray<PlannedQuery>(queries.MoveToImmutable())
        );
    }

    // The output resolves for each type and query, and a file needs what any type that claims it needs: the
    // greatest over every claim of every project, each with the metadata and the property of its own project.
    private static OutputKind GreatestOutput(SqlQuery query, FileWork work)
    {
        var greatest = OutputKind.Sql;
        foreach (var claimant in work.Claimants)
        {
            var metadata = claimant.Metadata.Settings?.Level ?? SettingsLevel.None;
            foreach (var claim in claimant.Claims)
            {
                var output = QuerySettings.Resolve(query.Markers, claim, metadata, claimant.Property).Output;
                if (output > greatest)
                {
                    greatest = output;
                }
            }
        }

        return greatest;
    }

    // One claimed file as one project sees it.
    private sealed record ClaimedFile(
        string ProjectPath,
        string NormalizedPath,
        FileMetadata Metadata,
        DialectSetting ProjectDialect,
        SettingsLevel Property,
        string? Database,
        List<SettingsLevel> Claims
    );

    // One claimed file of the run, with every project that claims it.  The first is the one it is planned under.
    private sealed class FileWork(ClaimedFile owner)
    {
        public ClaimedFile Owner => owner;

        public List<ClaimedFile> Claimants { get; } = [owner];
    }
}
```

`ClaimedFile.Database` is the file's `SqlSourceDatabase` metadata, or else the project's property: the two levels below a marker.

- [ ] **Step 6: Run the tests of the plan**

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunPlannerTests
```

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunPlannerErrorTests
```

Expected: PASS.  If the position in `Plan_FileWithAParseError_...` differs, read what the generator reports for the same text with `GeneratorHarness` before changing the expectation: the test says "where the generator does".

- [ ] **Step 7: Write the failing tests of the command**

In `tests/SqlSource.Tool.Tests/CliRun.cs`, replace `InvokeAsync` with these, and add `using SqlSource.Tool.Planning;` and `using SqlSource.Tool.Reporting;`:

```csharp
    // The plan that "describe" builds for a command line, with what it wrote.  Nothing a run prints says what it
    // selected, so a test of that reads the plan.
    public async Task<(RunPlan? Plan, CliResult Result)> PlanAsync(params string[] args)
    {
        using var output = NewWriter();
        using var error = NewWriter();
        var host = CreateHost(output, error);
        var reporter = new Reporter(host.Error);
        var parsed = Cli.BuildCommands(host, reporter).Parse(args, Cli.Parser);

        var plan = await DescribeCommand.PlanAsync(parsed, host, reporter, TestContext.Current.CancellationToken);

        return (plan, new CliResult(reporter.Count > 0 ? 1 : 0, output.ToString(), error.ToString()));
    }

    private async Task<CliResult> InvokeAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = NewWriter();
        using var error = NewWriter();

        var exitCode = await Cli.RunAsync(args, CreateHost(output, error), cancellationToken);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    private static StringWriter NewWriter() => new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    private ToolHost CreateHost(TextWriter output, TextWriter error) =>
        new(
            Out ?? output,
            error,
            Folder.Path,
            name => Environment.GetValueOrDefault(name),
            Processes,
            Folder.CreateFolder("tmp"),
            ProcessorCount: 4
        );
```

Add to `tests/SqlSource.Tool.Tests/DescribeTests.cs`:

```csharp
    private const string Queries = "[SqlSource.SqlSourceGenerate]\ninternal static partial class Queries;\n";

    [Fact]
    public async Task Run_ProjectWhosePlanHasNoError_PrintsNothing()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        _ = project.AddSql("One.sql", "SELECT 1;");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Fact]
    public async Task Run_ProjectWithAFileThatDoesNotParse_ReportsItAsABuildWouldAndExitsOne()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{sql}(2,8): error SQLSRC101: ");
        result.Error.ShouldEndWith("/docs/diagnostics.md#sqlsrc101\n");
    }

    [Fact]
    public async Task Plan_Project_GivesThePlanOfTheCommandLine()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("One.sql", "SELECT 1;");

        var (plan, result) = await run.PlanAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
        plan.ShouldNotBeNull().Files.ShouldHaveSingleItem().Path.ShouldBe(sql);
    }
```

Run `dotnet build tests/SqlSource.Tool.Tests`.  Expected: CS0103 for `DescribeCommand`.

- [ ] **Step 8: Move `describe` to a file of its own, with its plan**

Create `src/SqlSource.Tool/DescribeCommand.cs`:

```csharp
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>describe</c> command.  After sub-phase 2.4 it finds the unit and the projects, builds the plan of the run
/// and reports what is wrong with it.  It describes nothing yet.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    private const string ProjectOption = "--project";

    public static Command Create(ToolHost host, Reporter reporter)
    {
        var path = new Argument<string?>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one.  The current directory "
                + "when left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        // One value each time it is given, and it may be given several times: UsageCheck reads the token after it as
        // its value and no further.
        var project = new Option<string[]>(ProjectOption)
        {
            Description = "A project to run on, of the solution.  May be given several times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        var describe = new Command(Name, "Finds the queries of the projects that a database must describe")
        {
            path,
            project,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                _ = await PlanAsync(parsed, host, reporter, cancellationToken);
                return 0;
            }
        );
        return describe;
    }

    /// <summary>
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project that could be read.
    /// </summary>
    /// <remarks>
    /// Nothing that a run prints says what it selected, so a test reads the plan from here, and sub-phase 2.5 goes
    /// on from it.
    /// </remarks>
    internal static async Task<RunPlan?> PlanAsync(
        ParseResult parsed,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        if (
            RunUnitFinder.Find(parsed.GetValue<string?>(PathArgument), host.WorkingDirectory, reporter)
            is not { } unit
        )
        {
            return null;
        }

        var manifests = await RunProjects.FindAsync(
            unit,
            parsed.GetValue<string[]>(ProjectOption) ?? [],
            host,
            reporter,
            cancellationToken
        );
        if (manifests.IsEmpty)
        {
            if (reporter.Count == 0)
            {
                // A run that found nothing to do must not look like one that did it.
                await host.Out.WriteLineAsync($"sqlsource: no project of '{OneLine.Of(unit.Path)}' uses SqlSource");
            }

            return null;
        }

        var result = RunPlanner.Plan(manifests, cancellationToken);
        foreach (var error in result.Errors)
        {
            reporter.Report(error);
        }

        return result.Plan;
    }
}
```

In `src/SqlSource.Tool/Cli.cs`: delete the private method `Describe`, write `DescribeCommand.Create(host, reporter)` where `BuildCommands` called it, and remove the `using` lines that nothing needs any more (`SqlSource.Tool.Projects`).

- [ ] **Step 9: Run every test of the tool**

```bash
dotnet test --project tests/SqlSource.Tool.Tests
```

Expected: PASS, the tests of sub-phases 2.2 and 2.3 among them, unchanged.

- [ ] **Step 10: The tech-debt item**

Create `docs/tech-debt/TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md`:

```markdown
# TD-0032 - A `.sql` file that is not UTF-8 is hashed differently by the tool and by the compiler

## Problem

[`SqlFileText`](../../src/SqlSource.Tool/Planning/SqlFileText.cs) reads a `.sql` file with `SourceText.From` over its bytes: a byte order mark decides the encoding, and without one the bytes are read as UTF-8, with a replacement character for a sequence that is not UTF-8.  The compiler reads an additional file another way: UTF-8 first, and when the bytes are not valid UTF-8 it reads the whole file again in a fallback encoding, the code page 1252 where the runtime has it, and in the one the project's `CodePage` names when it names one.

So a query that holds a character outside ASCII, in a file saved as Windows-1252, has one text in the tool and another in the generator.  The hash of `SqlQueryHash` is of that text.  From phase 5 the generator will call such a query's entry stale, whatever the tool writes.

## Why it exists

The compiler's reader, `EncodedStringText`, is not public, and its fallback differs by runtime.  Copying it into the tool means copying code that can change under it.  The manifest does not carry `CodePage` either.

## Impact

Low.  It takes a `.sql` file that is not UTF-8 with a character outside ASCII in the SQL itself, not in a comment: the hash is of the SQL without comments.  Files written by any current editor are UTF-8.

## Proposed fix

Read the bytes as strict UTF-8, and on failure with the encoding the compiler falls back to; add `CodePage` to the manifest as a new key, which is an addition to its format.  Or report a `.sql` file that is not valid UTF-8 as an error in the tool, which is simpler and tells the user to save it as UTF-8.

## Trigger

Phase 5, when the generator first compares a hash; or a user whose entries are always stale for one file.
```

In `docs/tech-debt/README.md`, set `Next id: \`TD-0033\``, and add after the row of `TD-0031`:

```
| [TD-0032](TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md) | Open | 2026-10-10 | Low | A `.sql` file that is not UTF-8 is read with replacement characters by the `sqlsource` tool and in a fallback code page by the compiler, so the hash of a query that holds such a character differs between the two |
```

- [ ] **Step 11: Closing steps, and commit**

```bash
git add src tests docs
git commit -m "Plan a run from the manifests of its projects

The claims of each project go through the generator's path resolver,
its readers of settings and its parser.  A query gets whether it needs
an entry, its database and its hash.  describe builds the plan and
reports a file that does not parse, SQLSRC011 and SQLSRC014.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `SQLSRC209` and `SQLSRC210`

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tool.Tests/RunPlannerErrorTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `SqlDescribableDialects.Contains` (task 2), `ToolDiagnostic.At(descriptor, LocationInfo, ...)` (task 3), `RunPlanner.PlanFile` (task 5)
- Produces: `ToolDiagnostics.OutputNeedsDescribableDialect` (`SQLSRC209`) and `ToolDiagnostics.TokenHasNoDefault` (`SQLSRC210`)
- Produces: `PlannedFile.State` may be `NotDescribable`; `PlannedQuery.Problems` may hold `TokenWithoutDefault`

- [ ] **Step 1: The descriptors, in their four places**

In `ToolDiagnostics.cs`, after `AttributeArgumentNotLiteral`, and in `All` in the same place:

```csharp
    public static readonly DiagnosticDescriptor OutputNeedsDescribableDialect = new(
        id: "SQLSRC209",
        title: "Output needs a dialect that can be described",
        messageFormat: "The output '{0}' needs a dialect that can be described, and the dialect of this file is "
            + "'{1}'.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc209",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TokenHasNoDefault = new(
        id: "SQLSRC210",
        title: "Token has no default",
        messageFormat: "The token '{0}' has no default.  A query whose output is '{1}' is described with a sample in "
            + "its place.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc210",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC208`:

```
SQLSRC209 | SqlSource | Error | Output needs a dialect that can be described
SQLSRC210 | SqlSource | Error | Token has no default
```

In `docs/diagnostics.md`, after the table row of `SQLSRC208`:

```
| [SQLSRC209](#sqlsrc209) | Output needs a dialect that can be described |
| [SQLSRC210](#sqlsrc210) | Token has no default |
```

and after the section of `SQLSRC208`:

````markdown
## SQLSRC209

**Output needs a dialect that can be described**

A query whose output is `models` or `codegen` gets its types from a database, and SqlSource can ask two: PostgreSQL, the dialect `postgres`, and SQL Server, the dialect `mssql`.  This file has a query with such an output and another dialect.  The default output is `codegen` and the default dialect is `ansi`, so a project that sets neither gets this error for every file.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC209: The output 'codegen' needs a dialect that can be described, and the dialect of this file is 'ansi'.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.
```

Set the dialect: `<SqlSourceDialect>postgres</SqlSourceDialect>` for the project, the metadata of the same name for a file, or `-- dialect: postgres` at the top of the file.  Or, for queries that only want their SQL, set the output to `sql`: `<SqlSourceOutput>sql</SqlSourceOutput>`, `Output = GeneratorOutput.Sql` on the attribute, or `-- output: sql` in the file.  The error is reported once for a file, at the first query that needs types, and no query of the file is described until it is mended.

## SQLSRC210

**Token has no default**

A query with a `{{token}}` cannot be sent to a database as it is written.  To describe it, the tool puts a sample in the token's place: the token's default.  This query's output is `models` or `codegen`, and the token the message names has none.

```sql
-- name: FindUsers
SELECT id, name FROM users {{where}};
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC210: The token 'where' has no default.  A query whose output is 'codegen' is described with a sample in its place.
    help: write the token with a default, {{where:default}}, or give one in a marker of the query: -- token: {{where:default}}
```

Give the token a default where it stands, `{{where:WHERE deleted_at IS NULL}}`, or once for the query with a marker, `-- token: {{where:WHERE deleted_at IS NULL}}`.  An empty default, `{{where:}}`, describes the query with nothing there.  The default is a sample for describing and nothing else: the generated method still takes the token as an argument.  The error is reported once for each token without a default, at the name of its query.
````

In `ToolDiagnosticsTests.cs`:

```csharp
    [Fact]
    public void OutputNeedsDescribableDialect_Message_HoldsTheOutputAndTheDialect() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.OutputNeedsDescribableDialect.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "codegen",
                "ansi"
            )
            .ShouldBe(
                "The output 'codegen' needs a dialect that can be described, and the dialect of this file is 'ansi'.  "
                    + "Set the dialect to 'postgres' or 'mssql', or the output to 'sql'."
            );

    [Fact]
    public void TokenHasNoDefault_Message_HoldsTheTokenAndTheOutput() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.TokenHasNoDefault.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "where",
                "models"
            )
            .ShouldBe(
                "The token 'where' has no default.  A query whose output is 'models' is described with a sample in "
                    + "its place."
            );
```

Run `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`.  Expected: PASS.

- [ ] **Step 2: Write the failing tests**

Add to `RunPlannerErrorTests.cs`:

```csharp
    private const string Name = "-- name: ";

    [Theory]
    [InlineData("ansi", "ansi")]
    [InlineData("sqlite", "sqlite")]
    [InlineData("cockroach", "cockroachdb")]
    public void Plan_QueryThatNeedsAnEntryUnderADialectThatCannotBeDescribed_IsSqlsrc209OnceForItsFile(
        string dialect,
        string canonical
    )
    {
        var project = Project(dialect: dialect);
        var sql = project.AddSql(
            "Q/Three.sql",
            "-- name: Plain\n-- output: sql\nSELECT 0;\n\n-- name: One\nSELECT 1;\n\n-- name: Two\nSELECT 2;\n"
        );

        var result = Plan(project);

        // At the name of the first query that needs an entry, which is the second of the file.
        result.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.OutputNeedsDescribableDialect,
                sql,
                new LinePosition(4, Name.Length),
                "codegen",
                canonical
            ),
        ]);
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.NotDescribable);
        file.Queries.Select(query => query.NeedsEntry).ShouldBe([false, true, true]);
    }

    [Fact]
    public void Plan_FileUnderADialectThatCannotBeDescribedWhoseQueriesAreAllSql_IsReady()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/One.sql", "-- output: sql\n-- name: One\nSELECT 1;\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.Ready);
    }

    [Fact]
    public void Plan_TokensWithoutADefault_AreSqlsrc210ForEachAtTheNameOfTheirQuery()
    {
        var project = Project();
        var sql = project.AddSql(
            "Q/Find.sql",
            "-- name: Find\nSELECT id FROM users {{where}} {{order}} {{limit:LIMIT 10}} {{tail:}};\n"
        );

        var result = Plan(project);

        result.Errors.Select(error => (error.Descriptor.Id, error.Path, error.Position, error.Arguments[0]))
            .ShouldBe([
                ("SQLSRC210", sql, new LinePosition(0, Name.Length), "where"),
                ("SQLSRC210", sql, new LinePosition(0, Name.Length), "order"),
            ]);
        result.Errors[0].Arguments[1].ShouldBe("codegen");
        var help = result.Errors[0].Lines.ShouldHaveSingleItem();
        help.Label.ShouldBe("help");
        help.Text.ShouldContain("{{where:default}}");
        help.Text.ShouldContain("-- token:");
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.Ready);
        file.Queries.ShouldHaveSingleItem().Problems.ShouldBe(QueryProblems.TokenWithoutDefault);
    }

    [Fact]
    public void Plan_TokenWithoutADefaultInAQueryWhoseOutputIsSql_IsNoError()
    {
        var project = Project();
        _ = project.AddSql("Q/Find.sql", "-- name: Find\n-- output: sql\nSELECT id FROM users {{where}};\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem().Problems.ShouldBe(QueryProblems.None);
    }

    [Fact]
    public void Plan_TokenWithoutADefaultInAFileThatCannotBeDescribed_IsReportedBesideSqlsrc209()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        Plan(project).Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209", "SQLSRC210"]);
    }

    // Two types claim the file, one for its models and one for everything: a message names the greater.
    [Theory]
    [InlineData("Models", "CodeGen", "codegen")]
    [InlineData("Models", "Sql", "models")]
    public void Plan_TwoClaimsWithTwoOutputs_NameTheGreaterInAMessage(string first, string second, string named)
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "ansi";
        _ = project.AddSource("A.cs", Type("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}"));
        _ = project.AddSource("B.cs", Type("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}"));
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        Plan(project).Errors.Select(error => error.Arguments).ShouldAllBe(arguments => arguments.Contains(named));
    }
```

Run `dotnet build tests/SqlSource.Tool.Tests`, then the class.  Expected: the new tests FAIL, the `SQLSRC209` ones with no error where one is expected.

- [ ] **Step 3: Implement**

In `RunPlanner.PlanFile`, replace everything from `var queries = ...` to the end of the method with:

```csharp
        var state = PlannedFileState.Ready;
        var queries = ImmutableArray.CreateBuilder<PlannedQuery>(parsed.Queries.Count);
        foreach (var query in parsed.Queries)
        {
            var output = GreatestOutput(query, work);
            if (output == OutputKind.Sql)
            {
                // It needs no entry, and so belongs to no database and has no hash.
                queries.Add(new PlannedQuery(query, NeedsEntry: false, Database: null, Hash: null));
                continue;
            }

            // Once for the file, at its first query that needs an entry: one setting mends it.  The parser keeps no
            // position for the marker that set the output.
            if (state == PlannedFileState.Ready && !SqlDescribableDialects.Contains(parsed.Dialect))
            {
                state = PlannedFileState.NotDescribable;
                errors.Add(
                    ToolDiagnostic.At(
                        ToolDiagnostics.OutputNeedsDescribableDialect,
                        query.NameLocation,
                        NameOf(output),
                        SqlDialectName.Canonical(parsed.Dialect)
                    )
                );
            }

            var problems = QueryProblems.None;
            foreach (var token in query.Tokens)
            {
                // An empty default is one: the query is described with nothing in the token's place.
                if (token.Default is null)
                {
                    problems |= QueryProblems.TokenWithoutDefault;
                    errors.Add(
                        ToolDiagnostic
                            .At(ToolDiagnostics.TokenHasNoDefault, query.NameLocation, token.Name, NameOf(output))
                            .WithLines(HelpForToken(token.Name))
                    );
                }
            }

            queries.Add(
                new PlannedQuery(
                    query,
                    NeedsEntry: true,
                    query.Markers.Database ?? owner.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                    SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters),
                    problems
                )
            );
        }

        return new PlannedFile(
            owner.Metadata.File.Path,
            owner.NormalizedPath,
            owner.ProjectPath,
            parsed.Dialect,
            state,
            new EquatableArray<PlannedQuery>(queries.MoveToImmutable())
        );
    }

    // An output as a marker spells it.  Only the two that need an entry are named in a message.
    private static string NameOf(OutputKind output) => output == OutputKind.CodeGen ? "codegen" : "models";

    private static ContinuationLine HelpForToken(string name) =>
        new(
            "help",
            $"write the token with a default, {{{{{name}:default}}}}, or give one in a marker of the query: "
                + $"-- token: {{{{{name}:default}}}}"
        );
```

- [ ] **Step 4: Run the tests**

```bash
dotnet test --project tests/SqlSource.Tool.Tests
```

Expected: PASS.  Every earlier test of the plan uses a dialect that can be described or an output of `sql`, so none gains an error.

- [ ] **Step 5: Closing steps, and commit**

```bash
git add src tests docs
git commit -m "Report an output that cannot be described and a token with no default

SQLSRC209 once for a file, at its first query that needs an entry, and
SQLSRC210 for each token without a default.  Neither query is described.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Several projects, the databases of a run, and `SQLSRC211`

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `docs/tech-debt/TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md`; modify `docs/tech-debt/README.md`
- Test: `tests/SqlSource.Tool.Tests/RunPlannerTests.cs`, `tests/SqlSource.Tool.Tests/RunPlannerErrorTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `RunPlanner.Plan` and the plan's records (task 5), `PlannedFileState.NotDescribable` (task 6)
- Produces: `ToolDiagnostics.DatabaseHasTwoDialects` (`SQLSRC211`)
- Produces: `RunPlan.Databases` is filled; `PlannedQuery.Database` is the first spelling of its name; `PlannedQuery.Problems` may hold `DatabaseDialectConflict`

- [ ] **Step 1: The descriptor, in its four places**

In `ToolDiagnostics.cs`, after `TokenHasNoDefault`, and in `All` in the same place:

```csharp
    public static readonly DiagnosticDescriptor DatabaseHasTwoDialects = new(
        id: "SQLSRC211",
        title: "Database has two dialects",
        messageFormat: "The database '{0}' has the dialect '{1}' here and '{2}' in '{3}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc211",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC210`:

```
SQLSRC211 | SqlSource | Error | Database has two dialects
```

`docs/diagnostics.md`, the table row after that of `SQLSRC210`:

```
| [SQLSRC211](#sqlsrc211) | Database has two dialects |
```

and the section after that of `SQLSRC210`:

````markdown
## SQLSRC211

**Database has two dialects**

Each query that is described belongs to one logical database: the one its `-- database:` marker names, or else the `SqlSourceDatabase` metadata of its file, or else the property of that name, or else the name of its file's dialect.  A name is one database across the whole run, with one connection, and the dialect picks the driver of that connection.  Here two files give one name two dialects.  Names are compared ignoring case.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC211: The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Queries/Users.sql'
```

Give the queries of one of the two files another database, or the dialect of the other file.  The database keeps the dialect of the first file that names it, in the order of the projects and then of the files' paths.  The error is reported once for each other file, at its first query of that database, and no query of that database in that file is described until it is mended.  A query whose output is `sql` belongs to no database and is not counted, and neither is a file that already has [SQLSRC209](#sqlsrc209).
````

In `ToolDiagnosticsTests.cs`, give `All_Messages_FormatWithTheirArguments` four arguments, since this message has four places, and change its comment to say that a format ignores an argument it has no place for:

```csharp
            string.Format(
                    CultureInfo.InvariantCulture,
                    descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
                    "/work/app",
                    "the reason",
                    "a third",
                    "a fourth"
                )
                .ShouldContain("'/work/app'", Case.Sensitive, descriptor.Id);
```

and add:

```csharp
    [Fact]
    public void DatabaseHasTwoDialects_Message_HoldsTheDatabaseTheTwoDialectsAndTheOtherFile() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabaseHasTwoDialects.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "main",
                "mssql",
                "postgres",
                "/work/App/Users.sql"
            )
            .ShouldBe("The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Users.sql'");
```

Run `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`.  Expected: PASS.

- [ ] **Step 2: Write the tests of several projects, which pin what task 5 built**

Add to `RunPlannerTests.cs`.  These three must pass without a change to the planner; if one fails, the planner of task 5 is wrong, and is mended there.

```csharp
    [Fact]
    public void Plan_SeveralProjects_HasTheFilesOfEachInTheOrderOfTheProjects()
    {
        var first = Postgres("Zeta");
        _ = first.AddSource("Queries.cs", Type("Queries"));
        var z = first.AddSql("Z.sql", "SELECT 1;");
        var second = Postgres("Alpha");
        _ = second.AddSource("Queries.cs", Type("Queries"));
        var a = second.AddSql("A.sql", "SELECT 1;");

        var files = Plan(first, second).Plan.Files;

        files.Select(file => (file.Path, file.ProjectPath)).ShouldBe([(z, first.ProjectPath), (a, second.ProjectPath)]);
    }

    [Fact]
    public void Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds()
    {
        var shared = _folder.WriteFile("Shared/One.sql", "SELECT 1;");
        var first = Postgres("A");
        first.Properties["SqlSourceOutput"] = "sql";
        _ = first.AddSource("Queries.cs", Type("Queries", "Path = \"../Shared\""));
        first.ListSql(shared);
        var second = Postgres("B");
        _ = second.AddSource("Queries.cs", Type("Queries", "Path = \"../Shared\""));
        second.ListSql(shared);

        var file = Plan(first, second).Plan.Files.ShouldHaveSingleItem();

        file.ProjectPath.ShouldBe(first.ProjectPath);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    [Fact]
    public void Plan_FileThatTheFirstProjectListsAndTheSecondClaims_IsPlannedUnderTheSecond()
    {
        var shared = _folder.WriteFile("Shared/One.sql", "SELECT 1;");
        var first = Postgres("A");
        _ = first.AddSource("Queries.cs", Type("Queries"));
        _ = first.AddSql("Own.sql", "SELECT 1;");
        first.ListSql(shared);
        var second = Postgres("B");
        second.Properties["SqlSourceDialect"] = "mssql";
        _ = second.AddSource("Queries.cs", Type("Queries", "Path = \"../Shared\""));
        second.ListSql(shared);

        var file = Plan(first, second).Plan.Files.Single(file => file.Path == shared);

        file.ProjectPath.ShouldBe(second.ProjectPath);
        file.Dialect.ShouldBe(SqlDialect.SqlServer);
    }
```

Run the class.  Expected: PASS.

- [ ] **Step 3: Write the failing tests of the databases**

Add to `RunPlannerTests.cs`:

```csharp
    [Fact]
    public void Plan_Databases_AreThoseOfTheQueriesThatNeedAnEntryInTheOrderOfThePlan()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", "-- name: One\nSELECT 1;\n\n-- name: Plain\n-- output: sql\nSELECT 0;\n");
        _ = project.AddSql("B.sql", "SELECT 2;", ("SqlSourceDialect", "mssql"));
        _ = project.AddSql("C.sql", "-- output: sql\n-- database: unused\n-- name: Three\nSELECT 3;\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Databases.ShouldBe([
            new PlannedDatabase("postgres", SqlDialect.PostgreSql),
            new PlannedDatabase("mssql", SqlDialect.SqlServer),
        ]);
    }

    [Fact]
    public void Plan_DatabaseNamesThatDifferInCase_AreOneDatabaseInItsFirstSpelling()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", "-- database: Billing\n-- name: One\nSELECT 1;\n");
        _ = project.AddSql("B.sql", "-- database: BILLING\n-- name: Two\nSELECT 2;\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Databases.ShouldBe([new PlannedDatabase("Billing", SqlDialect.PostgreSql)]);
        result.Plan.Files.SelectMany(file => file.Queries).Select(query => query.Database).ShouldBe(["Billing", "Billing"]);
    }
```

Add to `RunPlannerErrorTests.cs`:

```csharp
    private const string InMain = "-- database: main\n";

    [Fact]
    public void Plan_DatabaseWithTwoDialectsInTwoFiles_IsSqlsrc211AtTheFirstQueryOfTheSecondFile()
    {
        var project = Project();
        var first = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        var second = project.AddSql(
            "Q/B.sql",
            InMain + "-- name: Plain\n-- output: sql\nSELECT 0;\n\n-- name: Two\nSELECT 2;\n\n-- name: Three\nSELECT 3;\n",
            ("SqlSourceDialect", "mssql")
        );

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.DatabaseHasTwoDialects,
                second,
                new LinePosition(5, Name.Length),
                "main",
                "mssql",
                "postgres",
                first
            ),
        ]);
        result.Plan.Databases.ShouldBe([new PlannedDatabase("main", SqlDialect.PostgreSql)]);
        result.Plan.Files[0].Queries.Select(query => query.Problems).ShouldBe([QueryProblems.None]);
        result.Plan.Files[1].State.ShouldBe(PlannedFileState.Ready);
        result.Plan.Files[1]
            .Queries.Select(query => query.Problems)
            .ShouldBe([
                QueryProblems.None,
                QueryProblems.DatabaseDialectConflict,
                QueryProblems.DatabaseDialectConflict,
            ]);
    }

    [Fact]
    public void Plan_DatabaseWithTwoDialectsInTwoProjects_IsSqlsrc211InTheSecond()
    {
        var first = Project("A");
        var firstFile = first.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        var second = Project("B", dialect: "mssql");
        var secondFile = second.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2;\n");

        var error = Plan(first, second).Errors.ShouldHaveSingleItem();

        error.Descriptor.ShouldBe(ToolDiagnostics.DatabaseHasTwoDialects);
        error.Path.ShouldBe(secondFile);
        error.Arguments.ShouldBe(["main", "mssql", "postgres", firstFile]);
    }

    [Fact]
    public void Plan_FileOfAnotherDialectWhoseQueriesAreAllSql_GivesItsDatabaseNoSecondDialect()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        _ = project.AddSql("Q/B.sql", InMain + "-- output: sql\n-- name: Two\nSELECT 2;\n", ("SqlSourceDialect", "mssql"));

        Plan(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_FileThatCannotBeDescribed_GivesItsDatabaseNoDialectAndGetsSqlsrc209Alone()
    {
        var project = Project();
        // The first file of the database in the plan's order is the one that cannot be described.
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n", ("SqlSourceDialect", "ansi"));
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2;\n");

        var result = Plan(project);

        result.Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209"]);
        result.Plan.Databases.ShouldBe([new PlannedDatabase("main", SqlDialect.PostgreSql)]);
        result.Plan.Files.SelectMany(file => file.Queries).ShouldAllBe(query => query.Problems == QueryProblems.None);
    }

    [Fact]
    public void Plan_DatabaseWhoseFilesCanNoneBeDescribed_HasTheDialectOfItsFirstQuery()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/A.sql", "-- name: One\nSELECT 1;\n");

        Plan(project).Plan.Databases.ShouldBe([new PlannedDatabase("ansi", SqlDialect.Ansi)]);
    }

    [Fact]
    public void Plan_QueryWithATokenWithoutADefaultInADatabaseOfAnotherDialect_HasBothProblems()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2 {{tail}};\n", ("SqlSourceDialect", "mssql"));

        var result = Plan(project);

        result.Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC210", "SQLSRC211"]);
        result.Plan.Files[1]
            .Queries.ShouldHaveSingleItem()
            .Problems.ShouldBe(QueryProblems.TokenWithoutDefault | QueryProblems.DatabaseDialectConflict);
    }

    [Fact]
    public void Plan_FileWithQueriesOfTwoDatabasesOfAnotherDialect_IsSqlsrc211ForEachDatabase()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", "-- name: One\n-- database: one\nSELECT 1;\n\n-- name: Two\n-- database: two\nSELECT 2;\n");
        _ = project.AddSql(
            "Q/B.sql",
            "-- name: Three\n-- database: ONE\nSELECT 3;\n\n-- name: Four\n-- database: two\nSELECT 4;\n\n"
                + "-- name: Five\n-- database: one\nSELECT 5;\n",
            ("SqlSourceDialect", "mssql")
        );

        Plan(project).Errors.Select(error => error.Arguments[0]).ShouldBe(["one", "two"]);
    }
```

Run the two classes.  Expected: the new tests FAIL, with an empty `Databases` or no error.

- [ ] **Step 4: Implement**

In `RunPlanner.Plan`, replace the `return` with:

```csharp
        var databases = AssignDatabases(files, errors);

        return new RunPlanResult(
            new RunPlan(new EquatableArray<PlannedFile>([.. files]), databases),
            new EquatableArray<ToolDiagnostic>(errors.ToImmutable())
        );
```

and add:

```csharp
    // The databases of the run, in the order the plan first holds a query of each.  Names are compared ignoring
    // case, and every query gets the first spelling.  A database has the dialect of its first query in a file that
    // can be described: a file that cannot gives it none, since SQLSRC209 is that file's error.  A query of the
    // database in a file of another dialect has a problem, and its file's first such query is SQLSRC211.
    private static EquatableArray<PlannedDatabase> AssignDatabases(
        List<PlannedFile> files,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var names = new List<string>();
        var known = new Dictionary<string, DatabaseWork>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            foreach (var query in file.Queries)
            {
                if (query.Database is not { } name)
                {
                    continue;
                }

                if (!known.TryGetValue(name, out var database))
                {
                    database = new DatabaseWork(name, file.Dialect);
                    known.Add(name, database);
                    names.Add(name);
                }

                if (database.File is null && file.State == PlannedFileState.Ready)
                {
                    database.Dialect = file.Dialect;
                    database.File = file.Path;
                }
            }
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            HashSet<string>? reported = null;
            var queries = ImmutableArray.CreateBuilder<PlannedQuery>(file.Queries.Count);
            foreach (var query in file.Queries)
            {
                if (query.Database is not { } name)
                {
                    queries.Add(query);
                    continue;
                }

                var database = known[name];
                var problems = query.Problems;
                if (
                    file.State == PlannedFileState.Ready
                    && database.File is { } firstFile
                    && database.Dialect != file.Dialect
                )
                {
                    problems |= QueryProblems.DatabaseDialectConflict;
                    if ((reported ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(name))
                    {
                        errors.Add(
                            ToolDiagnostic.At(
                                ToolDiagnostics.DatabaseHasTwoDialects,
                                query.Query.NameLocation,
                                database.Name,
                                SqlDialectName.Canonical(file.Dialect),
                                SqlDialectName.Canonical(database.Dialect),
                                firstFile
                            )
                        );
                    }
                }

                queries.Add(query with { Database = database.Name, Problems = problems });
            }

            files[index] = file with { Queries = new EquatableArray<PlannedQuery>(queries.MoveToImmutable()) };
        }

        return new EquatableArray<PlannedDatabase>([
            .. names.Select(name => new PlannedDatabase(known[name].Name, known[name].Dialect)),
        ]);
    }

    // A database while the plan is made.  File is the first file that can be described and holds a query of it.
    private sealed class DatabaseWork(string name, SqlDialect dialect)
    {
        public string Name => name;

        public SqlDialect Dialect { get; set; } = dialect;

        public string? File { get; set; }
    }
```

- [ ] **Step 5: Run the tests**

```bash
dotnet test --project tests/SqlSource.Tool.Tests
```

Expected: PASS.

- [ ] **Step 6: The tech-debt item**

Create `docs/tech-debt/TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md`:

```markdown
# TD-0033 - A `.sql` file that two projects list has one sidecar and no owner

## Problem

Two projects can list one `.sql` file, each with a type that claims it: a shared folder that both include.  The file has one sidecar beside it, and each project's generator will read that one file.  [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs) plans such a file once, under the first project in the run's order that claims it, and two things follow:

- **The second project's dialect and database are not used.**  The file is parsed, hashed and given its database with the settings of the first project.  A second project that gives the file another dialect gets a sidecar whose hashes, from phase 5, its build calls stale.
- **A run that does not hold both projects sees the needs of one.**  A query needs an entry when any claim of any project of the run says so.  A run on one project, as the unit or by `--project`, does not read the other's claims, and may write a sidecar without the entries the other needs, or, in a run with no filter, delete a sidecar the other needs.

## Why it exists

The sidecar is beside the `.sql` file by the epic's decision, so that the generator pairs the two by path with no configuration.  A file with two owners was not designed for: the spec of sub-phase 2.4 plans it and records this.

## Impact

Low.  It takes a `.sql` file in two projects of one solution.  When both give it the same settings and the run is on the solution, the result is right.

## Proposed fix

Report a shared file whose projects resolve it to different dialects or databases as an error of its own, since no sidecar can serve both.  For the run on one project: have a sidecar record which projects' needs it was written for, or refuse to write or delete the sidecar of a file that a project outside the run also lists, which needs the solution's other manifests.

## Trigger

A user shares a `.sql` folder between two projects and reports a stale or missing entry.  Or phase 5, when the generator reports both.
```

In `docs/tech-debt/README.md`, set `Next id: \`TD-0034\``, and add after the row of `TD-0032`:

```
| [TD-0033](TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md) | Open | 2026-10-10 | Low | A `.sql` file that two projects list is planned under the first that claims it, so the second's dialect and database are not used, and a run on one of the two sees the needs of that one alone |
```

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src tests docs
git commit -m "Give a run its databases, and report one with two dialects

A name is one database across the run, compared ignoring case and
shown as first spelled.  A file of another dialect than its database
is SQLSRC211; a file that cannot be described gives a database none.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The filters

**Files:**
- Create: `src/SqlSource.Tool/Planning/FileFilter.cs`, `src/SqlSource.Tool/Planning/RunFilters.cs`, `src/SqlSource.Tool/UsageValue.cs`
- Modify: `src/SqlSource.Tool/Usage.cs`, `src/SqlSource.Tool/UsageCheck.cs`, `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/Planning/RunPlanner.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0030-project-option-through-a-symbolic-link-is-not-found.md`, `docs/tech-debt/README.md`
- Test: create `tests/SqlSource.Tool.Tests/FilterTests.cs`; modify `UsageCheckTests.cs`, `DescribeTests.cs`, `RunPlannerTests.cs`, `RunPlannerErrorTests.cs`, and `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Produces: `sealed record FileFilter(string Path, string NormalizedPath)`
- Produces: `sealed record RunFilters(ImmutableArray<FileFilter> Files, ImmutableArray<string> Databases)` with `RunFilters.None`, `bool IsEmpty` and `RunFilters.Create(IEnumerable<string> sqlPaths, IEnumerable<string> databases, string workingDirectory)`
- Produces: `RunPlanner.Plan(ImmutableArray<ProjectManifest> manifests, RunFilters filters, CancellationToken cancellationToken)`; `PlannedQuery.IsSelected` is set
- Produces: `sealed record UsageValue(Option? Option, string Text, int Position)`; `Usage(Command Command, IReadOnlyList<string> Arguments, IReadOnlyList<UsageValue> Values, IReadOnlyList<string> Messages)`
- Produces: `DescribeCommand.CheckUsage(Usage usage) : IReadOnlyList<string>`
- Produces: `ToolDiagnostics.FileNotInRun` (`SQLSRC212`)

- [ ] **Step 1: The descriptor, in its four places**

In `ToolDiagnostics.cs`, after `DatabaseHasTwoDialects`, and in `All` in the same place:

```csharp
    public static readonly DiagnosticDescriptor FileNotInRun = new(
        id: "SQLSRC212",
        title: "File is not in the run",
        messageFormat: "'{0}' is not a .sql file that a type of the run claims",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc212",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC211`:

```
SQLSRC212 | SqlSource | Error | File is not in the run
```

`docs/diagnostics.md`, the table row after that of `SQLSRC211`:

```
| [SQLSRC212](#sqlsrc212) | File is not in the run |
```

and the section after that of `SQLSRC211`, before `## SQLSRC220`:

````markdown
## SQLSRC212

**File is not in the run**

A path given to `sqlsource describe` that ends in `.sql` restricts the run to that file.  The run holds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it is on, and this path is none of them.  The message holds the full path the tool looked at: a relative path is resolved against the current directory.

```console
$ dotnet sqlsource describe Queries/User.sql
sqlsource : error SQLSRC212: '/work/App/Queries/User.sql' is not a .sql file that a type of the run claims
```

Check the spelling of the path.  If the file exists, it is either not a file of the project, as when `SqlSourceIncludeFiles` is off and the project does not list it, or no type claims it: a type claims the `.sql` files in the folder of its own source file, or the ones its `Path` names.  With `--project`, the file must be claimed by a type of one of the projects named.  The rest of the run goes on.
````

In `ToolDiagnosticsTests.cs`:

```csharp
    [Fact]
    public void FileNotInRun_Message_HoldsThePath() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.FileNotInRun.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/App/User.sql"
            )
            .ShouldBe("'/work/App/User.sql' is not a .sql file that a type of the run claims");
```

Run `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`.  Expected: PASS.

- [ ] **Step 2: Write the failing tests of the command line's reader**

Add to `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, with `using System.IO;` and `using SqlSource.Tool.Reporting;`:

```csharp
    [Fact]
    public void Check_ValuesOfOptionsAndArguments_AreGivenWithTheirPositions()
    {
        var usage = UsageCheck.Check(
            Root(),
            ["describe", "--project", "A.csproj", "App.slnx", "--project=B.csproj", "-c:x"]
        );

        usage.Messages.ShouldBeEmpty();
        usage.Values.Select(value => (value.Option?.Name, value.Text, value.Position))
            .ShouldBe([
                ("--project", "A.csproj", 3),
                ((string?)null, "App.slnx", 4),
                ("--project", "B.csproj", 5),
                ("--connection", "x", 6),
            ]);
    }

    // The same as the test above it, for the commands of the tool itself, whose "describe" takes several paths and
    // has rules of its own.
    [Fact]
    public void Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine()
    {
        string[] vocabulary =
        [
            "describe",
            "--project",
            "--project=P",
            "--database",
            "--database=D",
            "--database=",
            "--help",
            "--",
            "V",
            "W",
            "Q.sql",
            "R.SQL",
        ];
        using var folder = new TempFolder();
        var root = Cli.BuildCommands(
            Hosts.Create(folder.Path, new FakeProcessRunner(), folder.Path),
            new Reporter(TextWriter.Null)
        );
        var disagreements = new List<string>();

        foreach (var args in Sequences(vocabulary, 4))
        {
            var usage = UsageCheck.Check(root, args);
            var parsed = root.Parse(args, Cli.Parser);
            if (
                usage.Messages.Count == 0
                && DescribeCommand.CheckUsage(usage).Count == 0
                && parsed.Errors.Count == 0
                && !usage.IsReadTheSameBy(parsed)
            )
            {
                disagreements.Add(string.Join(' ', args));
            }
        }

        disagreements.Count.ShouldBe(0, string.Join(" | ", disagreements.Take(10)));
    }

    [Theory]
    [InlineData("describe", "App.csproj", "Q.sql", "R.sql")]
    [InlineData("describe", "Q.sql", "App.csproj", "R.SQL")]
    [InlineData("describe", "Q.sql")]
    [InlineData("describe", "--database", "billing", "--database=app-v2.main")]
    public void CheckUsage_PathsAndDatabasesOfDescribe_GiveNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), args)).ShouldBeEmpty();

    [Fact]
    public void CheckUsage_SecondPathThatIsNoSqlFile_IsReportedByItsPosition() =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(ToolRoot(), ["describe", "App.csproj", "Q.sql", Secret, "--project", "P"]))
            .ShouldBe(["sqlsource: unexpected argument at position 4"]);

    [Theory]
    [InlineData(3, "describe", "--database", "not a name")]
    [InlineData(2, "describe", "--database=" + Secret + "!")]
    [InlineData(3, "describe", "--database", "")]
    public void CheckUsage_DatabaseThatIsNoName_IsReportedByItsPositionAndNotItsText(int position, params string[] args) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(ToolRoot(), args))
            .ShouldBe([$"sqlsource: the value of option '--database' at position {position} is not a database name"]);

    [Fact]
    public void CheckUsage_AnotherCommand_GivesNoLine() =>
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), ["--version"])).ShouldBeEmpty();

    private static Command ToolRoot() =>
        Cli.BuildCommands(Hosts.Create("/work", new FakeProcessRunner(), "/tmp"), new Reporter(TextWriter.Null));
```

Run `dotnet build tests/SqlSource.Tool.Tests`.  Expected: CS1061 for `Usage.Values` and CS0117 for `DescribeCommand.CheckUsage`.

- [ ] **Step 3: `Usage` gives each value with its position**

Create `src/SqlSource.Tool/UsageValue.cs`:

```csharp
using System.CommandLine;

namespace SqlSource.Tool;

/// <summary>
/// One value of a command line as <see cref="UsageCheck" /> read it: an argument of the command, or the value of an
/// option.  A command checks its values by rules of its own and reports one by its position, never by its text.
/// </summary>
/// <param name="Option">The option the value belongs to, or null for an argument.</param>
/// <param name="Text">The value.</param>
/// <param name="Position">The place of the token that holds it on the command line, counted from one.</param>
internal sealed record UsageValue(Option? Option, string Text, int Position);
```

In `src/SqlSource.Tool/Usage.cs`, add the parameter and its documentation:

```csharp
/// <param name="Values">The arguments and the values of options, in order, each with its position.</param>
internal sealed record Usage(
    Command Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<UsageValue> Values,
    IReadOnlyList<string> Messages
)
```

In `src/SqlSource.Tool/UsageCheck.cs`, `Check` becomes:

```csharp
    public static Usage Check(Command root, IReadOnlyList<string> args)
    {
        var messages = new List<string>();
        var arguments = new List<string>();
        var values = new List<UsageValue>();
        var command = root;
        var options = root.Options.ToList();

        var index = 0;
        while (index < args.Count)
        {
            // After this, index is the position of the token counted from one.
            var token = args[index++];
            if (token.StartsWith('-'))
            {
                var name = NameOf(token);
                var option = Find(options, name);
                if (option is null)
                {
                    // The token after a misspelt option may be its value, so nothing after it is read, and nothing
                    // found before it is reported beside it.
                    return new Usage(command, arguments, [], [$"sqlsource: unknown option '{OneLine.Of(name)}'"]);
                }

                var hasSeparator = token.Length > name.Length;
                if (option.Arity.MaximumNumberOfValues == 0)
                {
                    if (hasSeparator)
                    {
                        messages.Add($"sqlsource: option '{name}' takes no value");
                    }
                }
                else if (hasSeparator)
                {
                    // "--name=" is what --name="$UNSET" gives.  System.CommandLine reads it as an option without a
                    // value and takes the next token for one, so every token after it would be read one place off.
                    if (token.Length == name.Length + 1)
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }
                    else
                    {
                        values.Add(new UsageValue(option, token[(name.Length + 1)..], index));
                    }
                }
                else if (option.Arity.MinimumNumberOfValues > 0)
                {
                    // The next token is the value, whatever it starts with, unless it names an option of this
                    // command or is "--": System.CommandLine takes neither for a value.
                    if (index == args.Count || IsOption(args[index], options))
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }
                    else
                    {
                        values.Add(new UsageValue(option, args[index], index + 1));
                        index++;
                    }
                }
            }
            else if (arguments.Count == 0 && command.Subcommands.FirstOrDefault(sub => sub.Name == token) is { } sub)
            {
                command = sub;
                options = [.. options.Where(static option => option.Recursive), .. sub.Options];
            }
            else if (arguments.Count < command.Arguments.Sum(static argument => argument.Arity.MaximumNumberOfValues))
            {
                arguments.Add(token);
                values.Add(new UsageValue(null, token, index));
            }
            else
            {
                messages.Add($"sqlsource: unexpected argument at position {index}");
            }
        }

        return new Usage(command, arguments, values, messages);
    }
```

- [ ] **Step 4: Write the failing tests of the filters**

In `RunPlannerTests.cs` and `RunPlannerErrorTests.cs`, the helper `Plan` now passes `RunFilters.None` before the token.

Create `tests/SqlSource.Tool.Tests/FilterTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// What "--database" and a .sql path select, from the command line to the plan.  The plan keeps every query of
// every claimed file; a filter says which of them the run was asked to describe.
public sealed class FilterTests : IDisposable
{
    private const string Secret = "s3cret";

    private const string See =
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc212\n";

    private readonly CliRun _run = new();
    private readonly string _users;

    public FilterTests()
    {
        // The project file is in the working directory, so a run that names no unit finds it.
        var project = new TestProject(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource(
            "Queries.cs",
            "[SqlSource.SqlSourceGenerate(Path = \"Q\")]\ninternal static partial class Queries;\n"
        );
        _ = project.AddSql("Q/Invoices.sql", "-- database: billing\n-- name: ListInvoices\nSELECT 4;\n");
        _users = project.AddSql(
            "Q/Users.sql",
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
                + "-- name: Plain\n-- output: sql\nSELECT 3;\n"
        );
        _ = project.AddSql("Unclaimed/Other.sql", "SELECT 5;");
    }

    public void Dispose() => _run.Dispose();

    // The names of the queries a command line selects, in the plan's order, for a run that reports nothing.
    private async Task<string[]> SelectedAsync(params string[] args)
    {
        var (plan, result) = await _run.PlanAsync(["describe", .. args]);

        result.ShouldBe(new CliResult(0, "", ""));
        var queries = plan.ShouldNotBeNull().Files.SelectMany(file => file.Queries).ToArray();
        // A filter takes nothing out of the plan.
        queries.Length.ShouldBe(4);
        return [.. queries.Where(query => query.IsSelected).Select(query => query.Query.Name)];
    }

    [Fact]
    public async Task Plan_NoFilter_SelectsEveryQuery() =>
        (await SelectedAsync()).ShouldBe(["ListInvoices", "GetUser", "GetInvoice", "Plain"]);

    [Theory]
    [InlineData("--database", "billing")]
    [InlineData("--database=BILLING")]
    [InlineData("--database", "billing", "--database", "Billing")]
    public async Task Plan_Database_SelectsItsQueriesIgnoringCase(params string[] args) =>
        (await SelectedAsync(args)).ShouldBe(["ListInvoices", "GetInvoice"]);

    [Fact]
    public async Task Plan_TwoDatabases_SelectTheQueriesOfBothAndNoneThatNeedsNoEntry() =>
        (await SelectedAsync("--database", "billing", "--database", "postgres"))
            .ShouldBe(["ListInvoices", "GetUser", "GetInvoice"]);

    [Fact]
    public async Task Plan_SqlPath_SelectsEveryQueryOfThatFile() =>
        (await SelectedAsync("Q/Users.sql")).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Plan_SqlPathAndDatabase_SelectWhatBothTake() =>
        (await SelectedAsync("Q/Users.sql", "--database", "billing")).ShouldBe(["GetInvoice"]);

    [Fact]
    public async Task Plan_DatabaseThatNoQueryHas_SelectsNothingAndIsNoError() =>
        (await SelectedAsync("--database", "warehouse")).ShouldBeEmpty();

    // Review focus 4.
    [Theory]
    [InlineData("q/users.SQL")]
    [InlineData("./Q/Users.sql")]
    [InlineData("Q/../Q/Users.sql")]
    [InlineData("Q\\Users.sql")]
    public async Task Plan_SqlPathSpelledAnotherWay_SelectsTheFile(string path) =>
        (await SelectedAsync(path)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Plan_SqlPathInFull_SelectsTheFile() =>
        (await SelectedAsync(_users)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Theory]
    [InlineData("App.csproj", "Q/Users.sql")]
    [InlineData("Q/Users.sql", "App.csproj")]
    [InlineData(".", "Q/Users.sql")]
    public async Task Plan_SqlPathBesideAUnit_IsAFilterAndTheOtherPathIsTheUnit(params string[] args) =>
        (await SelectedAsync(args)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Run_TwoUnits_IsAWrongCommandLineThatDoesNotRepeatTheSecond()
    {
        var result = await _run.RunAsync("describe", "App.csproj", "Q/Users.sql", Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 4\n"));
    }

    [Theory]
    [InlineData("Q/Missing.sql")]
    [InlineData("Unclaimed/Other.sql")]
    public async Task Run_SqlPathThatNoTypeClaims_IsSqlsrc212(string path)
    {
        var result = await _run.RunAsync("describe", path);

        var full = _run.Folder.PathOf(path);
        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"sqlsource : error SQLSRC212: '{full}' is not a .sql file that a type of the run claims\n" + See
            )
        );
    }

    [Fact]
    public async Task Plan_SqlPathThatNoTypeClaimsBesideOneThatIsClaimed_ReportsTheOneAndSelectsTheOther()
    {
        var (plan, result) = await _run.PlanAsync("describe", "Q/Missing.sql", "Q/Users.sql");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("SQLSRC212");
        plan.ShouldNotBeNull()
            .Files.SelectMany(file => file.Queries)
            .Where(query => query.IsSelected)
            .Select(query => query.Query.Name)
            .ShouldBe(["GetUser", "GetInvoice", "Plain"]);
    }

    // Review focus 5.
    [Fact]
    public async Task Run_OneSqlPathGivenThreeWaysThatNoTypeClaims_IsSqlsrc212Once()
    {
        var result = await _run.RunAsync("describe", "Q/Missing.sql", "./Q/Missing.sql", "q/MISSING.SQL");

        result.ExitCode.ShouldBe(1);
        result.Error.Split("error SQLSRC212").Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(3, "describe", "--database", "not a name")]
    [InlineData(2, "describe", "--database=" + Secret + "!")]
    public async Task Run_DatabaseThatIsNoName_IsAWrongCommandLineThatDoesNotRepeatIt(int position, params string[] args)
    {
        var result = await _run.RunAsync(args);

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"sqlsource: the value of option '--database' at position {position} is not a database name\n"
            )
        );
        _run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Help_ShowsThePathsAndNotTheDatabaseOption()
    {
        var result = await _run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>...]");
        result.Out.ShouldContain(".sql");
        result.Out.ShouldNotContain("--database");
    }
}
```

In `DescribeTests.cs`, `Run_Help_ShowsThePath` now expects `"sqlsource describe [<path>...]"`.

Run `dotnet build tests/SqlSource.Tool.Tests`.  Expected: CS0103 for `RunFilters`.

- [ ] **Step 5: The filters, and what they select**

Create `src/SqlSource.Tool/Planning/FileFilter.cs`:

```csharp
namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>.sql</c> path of the command line, which restricts a run to that file.
/// </summary>
/// <param name="Path">The full path, which is what an error shows.</param>
/// <param name="NormalizedPath">The path in the form <c>SqlPath.Normalize</c> gives, which is what is compared.</param>
internal sealed record FileFilter(string Path, string NormalizedPath);
```

Create `src/SqlSource.Tool/Planning/RunFilters.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using SqlSource.Generation;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What the command line restricts a run to.  A filter selects queries and takes nothing out of the plan: a sidecar
/// is written only when every query of its file that needs an entry has one, so the plan keeps them all.
/// </summary>
/// <param name="Files">The <c>.sql</c> files named, each once.  Empty for every file.</param>
/// <param name="Databases">The databases named, each once ignoring case.  Empty for every database.</param>
internal sealed record RunFilters(ImmutableArray<FileFilter> Files, ImmutableArray<string> Databases)
{
    /// <summary>A run with no filter.</summary>
    public static RunFilters None { get; } = new([], []);

    public bool IsEmpty => Files.IsEmpty && Databases.IsEmpty;

    /// <summary>
    /// The filters of a command line.  A path is resolved against the working directory and compared as the
    /// generator compares paths, so another case, <c>./</c>, <c>..</c> and either separator name the same file.
    /// </summary>
    public static RunFilters Create(IEnumerable<string> sqlPaths, IEnumerable<string> databases, string workingDirectory)
    {
        var files = ImmutableArray.CreateBuilder<FileFilter>();
        var seen = new HashSet<string>(SqlPath.Comparer);
        foreach (var given in sqlPaths)
        {
            var path = Path.GetFullPath(given, workingDirectory);
            if (SqlPath.Normalize(path) is { } normalized && seen.Add(normalized))
            {
                files.Add(new FileFilter(path, normalized));
            }
        }

        return new RunFilters(files.ToImmutable(), [.. databases.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }
}
```

In `RunPlanner.cs`, `Plan` takes `RunFilters filters` before the token, and calls `Select(files, filters, errors);` after `AssignDatabases`.  Add:

```csharp
    // Says which queries the filters take.  A file filter that names no planned file is SQLSRC212, and the run goes
    // on.  Under --database a query that needs no entry is not selected: it belongs to no database.
    private static void Select(
        List<PlannedFile> files,
        RunFilters filters,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        if (filters.IsEmpty)
        {
            return;
        }

        var found = new HashSet<string>(SqlPath.Comparer);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var isNamed = filters.Files.IsEmpty;
            foreach (var filter in filters.Files)
            {
                if (SqlPath.Comparer.Equals(filter.NormalizedPath, file.NormalizedPath))
                {
                    isNamed = true;
                    _ = found.Add(filter.NormalizedPath);
                }
            }

            files[index] = file with
            {
                Queries = new EquatableArray<PlannedQuery>([
                    .. file.Queries.Select(query => query with { IsSelected = isNamed && IsNamed(query, filters) }),
                ]),
            };
        }

        foreach (var filter in filters.Files)
        {
            if (!found.Contains(filter.NormalizedPath))
            {
                errors.Add(ToolDiagnostic.Create(ToolDiagnostics.FileNotInRun, filter.Path));
            }
        }
    }

    private static bool IsNamed(PlannedQuery query, RunFilters filters) =>
        filters.Databases.IsEmpty
        || (query.Database is { } database && filters.Databases.Contains(database, StringComparer.OrdinalIgnoreCase));
```

- [ ] **Step 6: The command reads them**

`src/SqlSource.Tool/DescribeCommand.cs` becomes, in full:

```csharp
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// The <c>describe</c> command.  After sub-phase 2.4 it finds the unit and the projects, builds the plan of the run
/// and reports what is wrong with it.  It describes nothing yet.
/// </summary>
internal static class DescribeCommand
{
    public const string Name = "describe";

    private const string PathArgument = "path";

    private const string ProjectOption = "--project";

    private const string DatabaseOption = "--database";

    public static Command Create(ToolHost host, Reporter reporter)
    {
        // Several, so that System.CommandLine takes a .sql path beside the unit.  CheckUsage holds them to one unit.
        var path = new Argument<string[]>(PathArgument)
        {
            Description =
                "A .sln, .slnx or .csproj file, or a directory that holds exactly one; the current directory when "
                + "left out.  A path that ends in .sql restricts the run to that file, and may be given several times.",
            Arity = ArgumentArity.ZeroOrMore,
        };

        // One value each time it is given, and it may be given several times: UsageCheck reads the token after it as
        // its value and no further.
        var project = new Option<string[]>(ProjectOption)
        {
            Description = "A project to run on, of the solution.  May be given several times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };

        // Accepted and checked, and not shown: it selects queries, and nothing this version prints depends on which
        // are selected.  Sub-phase 2.5 gives it an effect and shows it.
        var database = new Option<string[]>(DatabaseOption)
        {
            Description = "A database to describe the queries of.  May be given several times.",
            HelpName = "name",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
            Hidden = true,
        };

        var describe = new Command(Name, "Finds the queries of the projects that a database must describe")
        {
            path,
            project,
            database,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                _ = await PlanAsync(parsed, host, reporter, cancellationToken);
                return 0;
            }
        );
        return describe;
    }

    /// <summary>
    /// What is wrong with a command line of <c>describe</c> by the command's own rules, each a whole line to write.
    /// A line names a position and never repeats a token.  Empty for another command.
    /// </summary>
    /// <remarks>
    /// A path that ends in <c>.sql</c> is a filter and any other is the unit, of which there is one.  A value of
    /// <c>--database</c> is a database name, by the rule of the <c>-- database:</c> marker.
    /// </remarks>
    public static IReadOnlyList<string> CheckUsage(Usage usage)
    {
        if (usage.Command.Name != Name)
        {
            return [];
        }

        var messages = new List<string>();
        var units = 0;
        foreach (var value in usage.Values)
        {
            if (value.Option is null)
            {
                if (!SqlPath.IsSqlFile(value.Text) && ++units > 1)
                {
                    messages.Add($"sqlsource: unexpected argument at position {value.Position}");
                }
            }
            else if (value.Option.Name == DatabaseOption && !SettingValue.IsDatabaseName(value.Text))
            {
                messages.Add(
                    $"sqlsource: the value of option '{DatabaseOption}' at position {value.Position} is not a "
                        + "database name"
                );
            }
        }

        return messages;
    }

    /// <summary>
    /// Builds the plan of the run that a command line asks for, and reports what is wrong with it.  Null when there
    /// is nothing to plan: no unit, or no project that could be read.
    /// </summary>
    /// <remarks>
    /// Nothing that a run prints says what it selected, so a test reads the plan from here, and sub-phase 2.5 goes
    /// on from it.
    /// </remarks>
    internal static async Task<RunPlan?> PlanAsync(
        ParseResult parsed,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        // With file filters and no unit, the unit is found from the working directory, not from the files.
        var paths = parsed.GetValue<string[]>(PathArgument) ?? [];
        var unitPath = paths.FirstOrDefault(static path => !SqlPath.IsSqlFile(path));
        if (RunUnitFinder.Find(unitPath, host.WorkingDirectory, reporter) is not { } unit)
        {
            return null;
        }

        var manifests = await RunProjects.FindAsync(
            unit,
            parsed.GetValue<string[]>(ProjectOption) ?? [],
            host,
            reporter,
            cancellationToken
        );
        if (manifests.IsEmpty)
        {
            if (reporter.Count == 0)
            {
                // A run that found nothing to do must not look like one that did it.
                await host.Out.WriteLineAsync($"sqlsource: no project of '{OneLine.Of(unit.Path)}' uses SqlSource");
            }

            return null;
        }

        var filters = RunFilters.Create(
            paths.Where(SqlPath.IsSqlFile),
            parsed.GetValue<string[]>(DatabaseOption) ?? [],
            host.WorkingDirectory
        );
        var result = RunPlanner.Plan(manifests, filters, cancellationToken);
        foreach (var error in result.Errors)
        {
            reporter.Report(error);
        }

        return result.Plan;
    }
}
```

In `src/SqlSource.Tool/Cli.cs`, `RunAsync` writes the command's own lines when the reader found nothing:

```csharp
            var usage = UsageCheck.Check(root, args);
            var messages = usage.Messages.Count > 0 ? usage.Messages : DescribeCommand.CheckUsage(usage);
            if (messages.Count > 0)
            {
                // These lines have no id and do not go through the reporter.
                foreach (var line in messages)
                {
                    await host.Error.WriteLineAsync(line);
                }

                return 1;
            }
```

- [ ] **Step 7: Run every test of the tool**

```bash
dotnet test --project tests/SqlSource.Tool.Tests
```

Expected: PASS.  If `Check_EveryAcceptedCommandLineOfTheTool_...` finds a disagreement, mend `UsageCheck` or `CheckUsage`, by reading as System.CommandLine does or by rejecting more, as `src/SqlSource.Tool/AGENTS.md` says; never the test.  If the help spells the paths another way than `[<path>...]`, the two tests of the help take System.CommandLine's spelling.

- [ ] **Step 8: `TD-0030` covers the `.sql` path**

`docs/tech-debt/TD-0030-project-option-through-a-symbolic-link-is-not-found.md` names this sub-phase as the place where the `.sql` filter meets the same comparison, and it has.  In that file:

- the title becomes `# TD-0030 - A path of \`--project\` or a \`.sql\` path that goes through a symbolic link is not found`;
- after the paragraph that ends "A relative path is found, and so is a full path when the unit is given by the same spelling.", add the paragraph:

```
A `.sql` path of `describe` is compared the same way, by [`RunFilters`](../../src/SqlSource.Tool/Planning/RunFilters.cs), with the paths of the manifest, which MSBuild gives through the real folder.  A full path through the link is `SQLSRC212`.
```

- in "Proposed fix", the last sentence, "Do the same in sub-phase 2.4, ...", becomes `Do the same for the \`.sql\` path in \`RunFilters.Create\`.`;
- in "Trigger", the second sentence becomes `Or \`SQLSRC212\` for a file that a type claims.`

In `docs/tech-debt/README.md`, the row of `TD-0030` ends `... is \`SQLSRC207\`, and a \`.sql\` path given the same way is \`SQLSRC212\``.

- [ ] **Step 9: Closing steps, and commit**

```bash
git add src tests docs
git commit -m "Restrict a run with --database and a .sql path

Both select queries and take nothing out of the plan.  A path that
ends in .sql is a filter and any other is the unit; one that no type
claims is SQLSRC212.  --database is checked and stays out of --help
until it has an effect.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The plan agrees with the generator

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
- Create: `tests/SqlSource.Tool.Tests/GeneratorParityTests.cs`
- Modify: `CONTRIBUTING.md`, `src/SqlSource/AGENTS.md`

**Interfaces:**
- Consumes: `AttributeReader.Read`, `ListedFiles.Read`, `ListedFiles.ClaimedBy` (tasks 3 and 5); `TestProject` (task 4); `GeneratorHarness`, `SourceFile`, `InMemoryAdditionalText` of `tests/SqlSource.Tests/Generator/`; `TrackingNames.TypeFiles` and `TypeFiles` of the generator

- [ ] **Step 1: Compile the generator's harness into the tool's tests**

In `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, in the item group that holds the fixtures:

```xml
        <!--
            The harness of the generator's driver tests, for GeneratorParityTests, which runs the generator and the
            tool's planner over the same sources.  tests/SqlSource.Tests.RoslynFloor compiles the same files.
        -->
        <Compile Include="../SqlSource.Tests/Generator/GeneratorHarness.cs" Link="Generator/GeneratorHarness.cs" />
        <Compile
            Include="../SqlSource.Tests/Generator/InMemoryAdditionalText.cs"
            Link="Generator/InMemoryAdditionalText.cs"
        />
        <Compile
            Include="../SqlSource.Tests/Generator/TestOptionsProvider.cs"
            Link="Generator/TestOptionsProvider.cs"
        />
```

Run `dotnet build tests/SqlSource.Tool.Tests`.  Expected: it builds.  If a rule objects to something in a linked file that it accepts in its own project, the two projects differ in a setting: find the setting, and do not change the linked file for this project alone.

- [ ] **Step 2: Write the test**

Create `tests/SqlSource.Tool.Tests/GeneratorParityTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Tests.Generator;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan must agree with the generator on which type claims which file with which output: a query the tool does
// not describe is a build error from phase 5, and one it describes for nothing costs a description.  The generator
// reads symbols and the tool reads syntax, so each case runs both over the same sources and file list and compares
// what they find.
public sealed class GeneratorParityTests : IDisposable
{
    private const string Using = "using SqlSource;\n";

    private static readonly Dictionary<string, Case> Cases = new()
    {
        ["no path"] = new(
            1,
            [("Repo/Repository.cs", Using + "[SqlSourceGenerate]\ninternal partial class Repository;\n")],
            ["Repo/A.sql", "Repo/b.sql", "Repo/Sub/C.sql", "Other/D.sql", "E.sql"]
        ),
        ["a folder, a file and a path that leaves its folder"] = new(
            4,
            [
                (
                    "Repo/Types.cs",
                    Using
                        + "[SqlSourceGenerate(Path = \"Queries\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = @\"Queries\\Users.sql\")]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Path = \"../Shared/\")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Path = \"Nowhere\")]\ninternal partial class D;\n"
                ),
            ],
            ["Repo/Queries/Orders.sql", "Repo/Queries/Users.sql", "Repo/Queries/Deep/X.sql", "Shared/S.sql"]
        ),
        ["a path written each way"] = new(
            6,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Path = \"\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = null)]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Path = \" \")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Path = \"\"\"Q\"\"\")]\ninternal partial class D;\n"
                        + "[SqlSourceGenerate(Path = \"\\u0051\")]\ninternal partial class E;\n"
                        + "[SqlSourceGenerate(Path = default)]\ninternal partial class F;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["each output"] = new(
            5,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Output = GeneratorOutput.Sql)]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Output = GeneratorOutput.Models)]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Output = SqlSource.GeneratorOutput.CodeGen)]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Output = global::SqlSource.GeneratorOutput.Models, Path = \"Q\")]\n"
                        + "internal partial class D;\n"
                        + "[SqlSourceGenerate(Parameters = \"keep-comments\", ModelNamespace = \"App.Models\")]\n"
                        + "internal partial class E;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["each name of the attribute"] = new(
            4,
            [
                (
                    "Types.cs",
                    "[SqlSource.SqlSourceGenerateAttribute]\ninternal partial class A;\n"
                        + "[global::SqlSource.SqlSourceGenerate]\ninternal partial class B;\n"
                        + "[type: SqlSource.SqlSourceGenerate]\ninternal partial class C;\n"
                        + "[System.Serializable, SqlSource.SqlSourceGenerate(Path = \"Q\")]\ninternal partial class D;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["each kind of type"] = new(
            5,
            [
                (
                    "Types.cs",
                    Using
                        + "namespace App\n{\n"
                        + "    [SqlSourceGenerate]\n    internal partial struct A { }\n"
                        + "    [SqlSourceGenerate]\n    internal partial record B;\n"
                        + "    [SqlSourceGenerate]\n    internal partial record struct C;\n"
                        + "    internal partial class Outer\n    {\n"
                        + "        [SqlSourceGenerate(Path = \"Q\")]\n        private partial class Inner;\n    }\n"
                        + "    [SqlSourceGenerate]\n    internal static partial class E;\n"
                        + "    [SqlSourceGenerate]\n    internal partial interface INotOne;\n"
                        + "}\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["a type the generator rejects"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate]\ninternal class NotPartial;\n"
                        + "[SqlSourceGenerate(Path = \"Q\")]\nfile partial class FileLocal;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["an attribute inside #if"] = new(
            1,
            [
                (
                    "Types.cs",
                    "#if FEATURE\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class A;\n"
                        + "#if OTHER\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class B;\n"
                ),
            ],
            ["Root.sql"],
            ["FEATURE"]
        ),
        ["paths that differ only by case"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Path = \"q\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = \"Q/USERS.sql\")]\ninternal partial class B;\n"
                ),
            ],
            ["Q/Users.sql", "Q/users.SQL", "Q/Orders.sql"]
        ),
        ["two source files in two folders"] = new(
            2,
            [
                ("A/One.cs", Using + "[SqlSourceGenerate]\ninternal partial class One;\n"),
                ("B/Two.cs", Using + "[SqlSourceGenerate(Path = \"../A\")]\ninternal partial class Two;\n"),
                ("B/None.cs", "internal partial class None;\n"),
            ],
            ["A/A.sql", "B/B.sql"]
        ),
    };

    private readonly TempFolder _folder = new();

    public static TheoryData<string> CaseNames => new(Cases.Keys);

    public void Dispose() => _folder.Dispose();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Claims_OfTheSameSourcesAndFiles_AreTheTypeFilesOfTheGenerator(string name)
    {
        var (types, sources, sqlFiles, constants) = Cases[name];
        // The harness of the generator parses C# 12, the newest that its oldest Roslyn reads.
        var project = new TestProject(_folder) { LangVersion = "12.0" };
        project.Constants.AddRange(constants ?? []);
        var written = sources.Select(source => new SourceFile(project.AddSource(source.Path, source.Text), source.Text));
        var listed = sqlFiles.Select(path => project.AddSql(path, "SELECT 1;"));

        var expected = FromGenerator([.. written], [.. listed], constants ?? []);
        var actual = FromTool(project.Manifest());

        actual.ShouldBe(expected);
        // A case in which neither finds a type would prove nothing.
        expected.Length.ShouldBe(types);
    }

    // What the generator's own step gives for each attributed type.
    private static Claimed[] FromGenerator(SourceFile[] sources, string[] sqlFiles, string[] constants)
    {
        var options = GeneratorHarness.ParseOptions.WithPreprocessorSymbols(constants);
        var compilation = GeneratorHarness.CreateCompilation(sources, parseOptions: options);
        var driver = GeneratorHarness
            .CreateDriver(sqlFiles.Select(AdditionalText (path) => new InMemoryAdditionalText(path, "SELECT 1;")), options)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        if (!steps.TryGetValue(TrackingNames.TypeFiles, out var runs))
        {
            return [];
        }

        return Ordered(
            runs.SelectMany(run => run.Outputs)
                .Select(output => (TypeFiles)output.Value)
                .Select(type => new Claimed(
                    type.Type.FilePath,
                    type.Type.AttributeLocation.LineSpan.Start.Line,
                    type.Type.Path,
                    type.Type.Settings.Output?.ToString(),
                    string.Join('|', type.Files)
                ))
        );
    }

    // What the tool's reader and the planner's own list of files give for the same project.
    private static Claimed[] FromTool(ProjectManifest manifest)
    {
        var claims = AttributeReader.Read(manifest);
        claims.Errors.ShouldBeEmpty();
        var listed = ListedFiles.Read(manifest);
        return Ordered(
            claims.Claims.Select(claim => new Claimed(
                claim.SourcePath,
                claim.Position.Line,
                claim.Path,
                claim.Output?.ToString(),
                string.Join('|', listed.ClaimedBy(claim))
            ))
        );
    }

    private static Claimed[] Ordered(IEnumerable<Claimed> claims) =>
        [.. claims.OrderBy(claim => claim.SourcePath, StringComparer.Ordinal).ThenBy(claim => claim.Line)];

    // One type as each side sees it: the file and the line of its attribute, what the attribute sets, and the .sql
    // files it claims, in member order.
    private sealed record Claimed(string SourcePath, int Line, string? Path, string? Output, string Files);

    private sealed record Case(
        int Types,
        (string Path, string Text)[] Sources,
        string[] SqlFiles,
        string[]? Constants = null
    );
}
```

- [ ] **Step 3: Run it**

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.GeneratorParityTests
```

Expected: PASS.  This test is written after the code it holds, so it is not seen to fail first; prove that it can.  Change `ShortName` in `AttributeReader` to `"SqlSourceGenerat"`, run it, and see every case fail; put the name back.  A case that fails with the code as it should be is a finding: the tool and the generator disagree.  Mend the tool, in `AttributeReader` or `ListedFiles`, unless the generator is the one that is wrong, in which case stop and tell the owner.

- [ ] **Step 4: The two documents that the linked files make wrong**

In `CONTRIBUTING.md`, in the table of test projects, the row of `tests/SqlSource.Tool.Tests` gains a sentence at the end of its last cell:

```
  It also compiles the harness of `tests/SqlSource.Tests/Generator/`, three files, for `GeneratorParityTests`, which runs the generator and the tool over the same sources
```

and the paragraph under the table that starts "A test in `tests/SqlSource.Tests/Generator/` must compile and pass in both" gains:

```
  `GeneratorHarness.cs`, `InMemoryAdditionalText.cs` and `TestOptionsProvider.cs` of that folder are compiled into `tests/SqlSource.Tool.Tests` as well, so a change to one of them must build there too.
```

In `src/SqlSource/AGENTS.md`, the bullet "**The generator tests run on two Roslyn versions.**" gains at its end:

```
  The harness of that folder, `GeneratorHarness.cs`, `InMemoryAdditionalText.cs` and `TestOptionsProvider.cs`, is compiled a third time, into `tests/SqlSource.Tool.Tests`, where `GeneratorParityTests` compares the generator's `TypeFiles` with the claims of the `sqlsource` tool.
```

- [ ] **Step 5: Closing steps, and commit**

```bash
git add tests CONTRIBUTING.md src/SqlSource/AGENTS.md
git commit -m "Hold the tool's claims to the generator's

One test runs the generator and the tool's reader over the same
sources and file lists, and compares the types, their Path and Output,
and the files each claims.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: The documents, and the outline says Done

**Files:**
- Modify: `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`, `docs/diagnostics.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: `src/SqlSource/AGENTS.md`**

Under `## The pipeline`, after the bullet "**Paths are compared with `SqlPath.Comparer`, after `SqlPath.Normalize`.**", add:

```
- **The `sqlsource` tool plans a run with this code.**  `SqlPath.ToSortedSet`, `PathResolver.FindFiles`, `DialectSetting`, `ProjectSettings.Read`, `FileMetadata.Read`, `FileParseInput.Resolve`, `SqlFileReader.Read`, `QuerySettings.Resolve` and `SqlQueryHash.Compute` are called from `src/SqlSource.Tool/Planning/`, so a change to one changes what the tool describes.  The tool gives them an `AdditionalText` and an `AnalyzerConfigOptions` of its own, with the keys the compiler would give.  `tests/SqlSource.Tool.Tests/GeneratorParityTests.cs` compares the two on which type claims which file.
```

Under `## \`Parsing/\``, after the bullet "**A dialect's names, and the names of its options, are in `SqlDialectName` only.**", add:

```
- **The dialects that can be described are in `SqlDescribableDialects` only**: `postgres` and `mssql`.  The tool reads the list for `SQLSRC209`, and the generator will from phase 5.  The message of `SQLSRC209` and its section of `docs/diagnostics.md` repeat the two names; a test holds the list to them.
```

- [ ] **Step 2: `src/SqlSource.Tool/AGENTS.md`**

After the bullet "**An evaluation reports nothing.**", add:

```
- **`Planning/` makes the plan of a run and reports nothing.**  `Planning/RunPlanner.cs` gives a `RunPlan` and its errors as data, in the order they are to be reported, and `DescribeCommand.cs` reports them.  The plan keeps every query of every file that a type claims: `--database` and a `.sql` path set `PlannedQuery.IsSelected` and take nothing out, because sub-phase 2.5 writes a sidecar only when every query of its file that needs an entry has one.  A file that no type claims is never read.
- **The plan must agree with the generator**, on which type claims which file with which output, and on each query's hash.  So `RunPlanner` calls the generator's own path resolver, readers of settings, parser and hash, through `Planning/ManifestOptions.cs` and `Planning/SqlFileText.cs`, and copies no rule.  `GeneratorParityTests` runs both over the same sources.  A rule that both need goes into the generator's project.
- **`Planning/AttributeReader.cs` reads syntax, and two arguments.**  It has no compilation: it finds `[SqlSourceGenerate]` by name and reads `Path` as a string literal and `Output` as a member of `GeneratorOutput`.  Anything else in one of those two is `SQLSRC208`, and nothing else on the attribute is read, since nothing else changes what is described.  What it misreads is `docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md`.
- **A database belongs to a query that needs an entry, and to no other.**  Names are compared ignoring case, and the plan holds the first spelling.  A file that is `NotDescribable` gives its database no dialect.
- **Of the settings that are not valid, the tool reports the ones it reads**: the dialect, `SqlSourceOutput` and `SqlSourceDatabase`, under the generator's `SQLSRC011` and `SQLSRC014`, at the project file.  The build reports the rest.
```

Replace the bullet "**An option is declared so that `UsageCheck` reads it right.**" 's last sentence, "An option is added to `--help` in the sub-phase that gives it an effect, not before.", with:

```
An option is added to `--help` in the sub-phase that gives it an effect, not before: `--database` is read and checked from sub-phase 2.4 and is `Hidden` until sub-phase 2.5.
```

After the bullet "**`UsageCheck` and System.CommandLine must read a command line the same way.**", add:

```
- **A command's own rules for its values are in the command**, as `DescribeCommand.CheckUsage`, which reads `Usage.Values`: each argument and each value of an option with its position.  `Cli.RunAsync` calls it when `UsageCheck` found nothing.  Its lines name a position and never a token, as `UsageCheck`'s do.  `describe` takes several paths so that System.CommandLine accepts a `.sql` path beside the unit; `CheckUsage` holds them to one unit.
```

- [ ] **Step 3: `src/SqlSource.Tool/README.md`**

Replace the paragraph "This version finds the projects to run on and reads what the compiler is given for each.  It describes nothing yet." and the console block and the paragraph after it with:

````markdown
This version finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It reports what would keep one from being described, and describes nothing yet.

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
$ dotnet sqlsource describe src/App/Queries/Users.sql
```

`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on.  A path that ends in `.sql` restricts the run to that file, and may be given several times.

The tool reads `[SqlSourceGenerate]` from the source without compiling it, so `Path` and `Output` must be written as literals: `Path = "Queries"`, `Output = GeneratorOutput.Models`.
````

- [ ] **Step 4: `docs/diagnostics.md`**

The paragraph under the table that starts "Ids below 100 are about the type" gains at its end:

```
  The tool also reports `SQLSRC011`, `SQLSRC014` and the errors of a `.sql` file, the ids from 101, under the generator's ids: what is wrong is the same, and so is the fix.  It gives `SQLSRC011` and `SQLSRC014` the project file as their place, and reports them for the dialect, `SqlSourceOutput` and `SqlSourceDatabase` alone.
```

In the section of `SQLSRC014`, replace the sentence "`SqlSourceDatabase` is not checked by the generator." with:

```
`SqlSourceDatabase` takes one word of letters, digits, `-`, `_` and `.`.  The generator does not read it, so a build does not report it; the `sqlsource` tool does.
```

Run `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Diagnostics`.  Expected: PASS.

- [ ] **Step 5: The outline**

In the table under `## Phases`, in the row of 2.4, replace `| In progress |` with `| Done |`.

- [ ] **Step 6: Closing steps, and commit**

```bash
git add src docs
git commit -m "Document the plan of a run and close phase 2.4

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Before the pull request

- [ ] **Step 1: The whole solution, once more**

```bash
./pre-commit-validation.sh
```

Expected: every check passes.

- [ ] **Step 2: The version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, and nothing to compare.  If there is a tag and `VersionPrefix` in `Directory.Build.props` is not greater, ask the owner for the new version and set it in a commit of its own.

- [ ] **Step 3: The pull request, when the owner says so**

Pushing the branch and opening a pull request are the owner's to ask for.  Its description holds:

- what the sub-phase delivers, in the words of this plan's goal;
- the nine departures from the spec, from the section above;
- **the time and allocation of `PathResolver.Resolve` before and after**, the six lines of `perf-path-resolver.txt` from task 2, as `src/SqlSource/AGENTS.md` requires of a change to a hot path;
- the three tech-debt items added and the one extended;
- that no behaviour of the generator changed, and that its allocation budgets are as they were.

It ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.  After it is open, reply to every CodeRabbit finding in its thread, as `AGENTS.md` requires.

