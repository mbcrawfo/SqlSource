# TD-0039, pull request 1: the helpers of the tool's tests - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each helper, constant and fixture text that items 1, 2, 3, 4 and 7 of TD-0039 name, and the sidecar of item 8, has one definition at the root of `tests/SqlSource.Tool.Tests`, and TD-0039 is rewritten to what remains.

**Architecture:** Six small static helpers at the root of the tool's test project replace the copies in the test classes: `Expected`, `ConnectionArguments`, `SqlTexts`, `Secrets`, `SidecarFiles` (the renamed `TestSidecar`) and new members of `Plans`, `FakeDescriber` and `TestClock`.  No test moves and none is renamed; a test changes only in how it names a value.  Nothing under `src/` changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net8.0`; xunit v3 on Microsoft.Testing.Platform with Shouldly; System.CommandLine.

**Spec:** `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`, the section "Pull request 1".

**Branch:** `claude/td-0039-tool-test-helpers`, already made from `main`, with the spec committed as `34751ea`.  This file is untracked until Task 7 commits it.

## Where the plan settles what the spec leaves open

- **`Expected.See` takes the digits from the id and does not lower its case.**  `"...#sqlsrc" + id[6..]`, as `SqlDiagnosticsTests` builds the anchor.  `ToLowerInvariant` is `CA1308` under `AnalysisLevel` `latest-all`.
- **The test of `Expected.See` compares it with the descriptors**, `"    see: " + descriptor.HelpLinkUri + "\n"` for each of `SqlDiagnostics.All` and `ToolDiagnostics.All`, so the address is written once in the tool's tests.
- **The edit of a fixture text is `SqlTexts.WithSecondQueryEdited`, not `WithInvoiceEdited`.**  Two of its five callers apply it to the text whose second query is `ListUsers`.
- **`SqlTexts` has four texts**: `GetUser`, `TwoUsers`, `Users` and `UsersAndPlain`.  The text of `DatabaseRunsTests`, whose `GetUser` has a parameter, stays in its class.
- **`Secrets` has five constants and merges none.**  `Word`, `Marker`, `Connection`, `ConnectionWithWord` and `ConnectionWithMarker`.  Tests search output for each, so no two become one.
- **A class loses its constant; it gets no alias.**  `private const string Users = SqlTexts.Users;` would leave a declaration for each class.  Each use names the helper.
- **`TestClock.Now` is parsed from `TestClock.StartIso`**, so the seed is written once.  A test pins the instant.
- **`CliRun.Out` becomes settable.**  Six tests of `CliTests` give a run a writer that fails; with the run in a field they set it.
- **The spec's "leaves the list of tests the same" holds apart from two classes this plan adds**, `ExpectedTests` and `TestClockTests`, of one test each.

## Global Constraints

- Nothing under `src/` changes.  `README.md`, `CONTRIBUTING.md`, `docs/diagnostics.md` and `docs/publishing.md` are not touched.
- No test is lost, moved or renamed.  The list of tests of `tests/SqlSource.Tool.Tests` after the work is the list before it plus `ExpectedTests.See_EveryDiagnostic_IsTheLineThatNamesItsSection` and `TestClockTests.Now_NewClock_IsTheInstantOfStartIso`.
- A test's expectation does not change in value.  Replace a literal with a name for the same text; if the texts differ by one character, they are not the same and the literal stays.
- Every helper is `internal`, at the root of `tests/SqlSource.Tool.Tests`, in the namespace `SqlSource.Tool.Tests`, which every sub-folder of the project sees without a `using`.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports in the code, a `using` that nothing needs any more included; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A fixture text that looks the same and is not.**  The `Users` text of `DatabaseRunsTests` has `WHERE @id = 1`; those of `FileOutcomesTests`, `RunDecisionsTests` and `FilterTests` end with a third query.  A wrong merge can leave every test passing while a test pins less.  Task 4 lists each class with the constant it takes, and its first step compares the texts.
- **`SidecarFiles.EntryFor` through `TestSidecars.Entry` must build the record it builds today.**  An entry that differs makes a "current" sidecar look out of date.  The tests of `RunDecisionsTests` that write a current sidecar and expect every query skipped hold this; Task 3 runs them by name.
- **`TestClock.Now` parsed from text.**  A parse that takes the local zone would move every time in a log.  Task 4 adds `TestClockTests`.
- **`Expected.See` for an id that is not `SQLSRC` and digits, or for `SQLSRC999`.**  The descriptor of the tests, `FakeDescriber.Rejected`, has another address.  No call site passes it; Task 1's test covers every real descriptor.
- **A test dropped in a mass edit.**  Task 7 compares the list of tests with the one Task 1 saves.

---

### Task 1: The help link once (item 1)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Expected.cs`
- Create: `tests/SqlSource.Tool.Tests/ExpectedTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/DescribeScene.cs:13`
- Modify: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`, `Projects/RunProjectsTests.cs`, `Commands/FilterTests.cs`, `Commands/CheckTests.cs`, `Commands/ExitCodeTests.cs`, `Commands/DescribeTests.cs`, `Commands/CliTests.cs`, `Commands/FilterRunTests.cs`, `Commands/DescribeRunTests.cs`, `Describing/SidecarStoreTests.cs`, `Describing/DatabaseRunsTests.cs`

**Interfaces:**
- Produces: `Expected.See(string id)`, which returns the line with its line end, for example `Expected.See("SQLSRC207")` is `"    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc207\n"`.

- [ ] **Step 1: Save the list of tests as it is**

```bash
dotnet build SqlSource.slnx
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0039-pr1-before.txt"
wc -l < "${TMPDIR:-/tmp}/td-0039-pr1-before.txt"
```

Expected: `844`.

- [ ] **Step 2: Write the failing test**

Create `tests/SqlSource.Tool.Tests/ExpectedTests.cs`:

```csharp
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;

namespace SqlSource.Tool.Tests;

// Expected stands in for what the tool writes.  This holds it to the descriptors.
public class ExpectedTests
{
    [Fact]
    public void See_EveryDiagnostic_IsTheLineThatNamesItsSection()
    {
        foreach (var descriptor in SqlDiagnostics.All.Concat(ToolDiagnostics.All))
        {
            Expected.See(descriptor.Id).ShouldBe($"    see: {descriptor.HelpLinkUri}\n");
        }
    }
}
```

- [ ] **Step 3: Run it to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL with `CS0103: The name 'Expected' does not exist in the current context`.

- [ ] **Step 4: Write the helper**

Create `tests/SqlSource.Tool.Tests/Expected.cs`:

```csharp
namespace SqlSource.Tool.Tests;

// What the tool writes, where many tests expect the same text.
internal static class Expected
{
    // The line that ends a diagnostic and says where its id is explained.  The anchor of a heading is its text in
    // lower case, and an id is "SQLSRC" followed by digits.
    public static string See(string id) =>
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc" + id[6..] + "\n";
}
```

- [ ] **Step 5: Run the test to see it pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.ExpectedTests"`
Expected: PASS, 1 test.

- [ ] **Step 6: Replace every copy**

Delete these declarations:

- `DescribeScene.cs:13`, `public const string See = ...;` and the blank line after it.
- `Reporting/ReporterTests.cs:20-21`, `Projects/RunProjectsTests.cs:20`, `Commands/FilterTests.cs:15-16`, `Commands/DescribeRunTests.cs:17`, `Describing/SidecarStoreTests.cs:17`, `Describing/DatabaseRunsTests.cs:24`: each `private const string See`.

Then rewrite each use by this rule: a prefix followed by digits and a line end becomes one call with the whole id.

| Before | After |
|----|----|
| `See + "207\n"` | `Expected.See("SQLSRC207")` |
| `$"{See}217\n"` | `Expected.See("SQLSRC217")` |
| `$"...'postgres'\n{See}216\n"` | `$"...'postgres'\n{Expected.See("SQLSRC216")}"` |
| `$"{help}{DescribeScene.See}219\n"` | `$"{help}{Expected.See("SQLSRC219")}"` |
| `$"{DescribeScene.See}200\n{DescribeScene.RunAgain}"` | `Expected.See("SQLSRC200") + DescribeScene.RunAgain` |
| `"    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc207\n"` | `Expected.See("SQLSRC207")` |

The cases the rule does not cover:

- `Reporting/ReporterTests.cs` and `Commands/FilterTests.cs` held the whole line for one id.  Each use `See` becomes `Expected.See("SQLSRC200")` in `ReporterTests` and `Expected.See("SQLSRC212")` in `FilterTests`.
- `Projects/RunProjectsTests.cs:91` and `:98` are `+ See` followed by a line `+ "NNN\n"`: join the two into `+ Expected.See("SQLSRCNNN")` with the digits of that next line.  The same holds at `:213` and `:233`.
- `Projects/RunProjectsTests.cs:359`, `lines[1].ShouldBe(See + "223");`, compares a line without its end.  It becomes:

```csharp
        (lines[1] + "\n").ShouldBe(Expected.See("SQLSRC223"));
```

- `Commands/DescribeTests.cs:332` becomes:

```csharp
        result.Error.ShouldEndWith(Expected.See("SQLSRC101") + DescribeScene.RunAgain);
```

- [ ] **Step 7: Check that no copy is left**

Run: `grep -rn --include='*.cs' 'diagnostics.md#' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: one line, in `tests/SqlSource.Tool.Tests/Expected.cs`.

Run: `grep -rnw --include='*.cs' 'See' tests/SqlSource.Tool.Tests | grep -v '/obj/' | grep -v 'Expected.See' | grep -v 'Expected.cs' | grep -v 'ExpectedTests.cs'`
Expected: no output.

- [ ] **Step 8: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 845 tests.

- [ ] **Step 9: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Write the help link of a diagnostic once in the tool's tests

Expected.See gives the line for an id, and a test holds it to the descriptors.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: One `Plans`, one `Parse`, no `ToolRoot` (item 2)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Plans.cs`
- Create: `tests/SqlSource.Tool.Tests/ConnectionArguments.cs`
- Modify: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs:19-28`, `Planning/RunPlannerErrorTests.cs:20-28` and every call of `Plan(` and `Type(` in both
- Modify: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs:15-19,31`, `Describing/DatabaseRunsTests.cs:50-54,69`, `Commands/UsageCheckTests.cs:377`
- Modify: `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs:26-29`, `TestProjectTests.cs:47`

**Interfaces:**
- Produces: `Plans.Result(params TestProject[] projects)` and `Plans.Result(RunFilters filters, params TestProject[] projects)`, both returning `RunPlanResult`; `Plans.TypeNamed(string name, string arguments = "")` returning `string`; `ConnectionArguments.Parse(string text)` returning `ConnectionArgument`.

- [ ] **Step 1: Add the members to `Plans`**

In `tests/SqlSource.Tool.Tests/Plans.cs`, add after the constant `Type`:

```csharp
    // A type with the attribute and these arguments, in a file of its own.
    public static string TypeNamed(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({arguments})]\ninternal static partial class {name};\n";
```

and replace the two `Of` methods with:

```csharp
    public static RunPlan Of(params TestProject[] projects) => Of(RunFilters.None, projects);

    // The plan, whatever is wrong with it: a test of a query with a problem wants the plan all the same.
    public static RunPlan Of(RunFilters filters, params TestProject[] projects) => Result(filters, projects).Plan;

    public static RunPlanResult Result(params TestProject[] projects) => Result(RunFilters.None, projects);

    // The plan with what is wrong with it, for a test of the planner itself.
    public static RunPlanResult Result(RunFilters filters, params TestProject[] projects) =>
        RunPlanner.Plan([.. projects.Select(project => project.Manifest())], filters, TestContext.Current.CancellationToken);
```

- [ ] **Step 2: Use them in the two planner test classes**

In `Planning/RunPlannerTests.cs` delete the private methods `Plan` (lines 19-24) and `Type` with its comment (lines 26-28).  In `Planning/RunPlannerErrorTests.cs` delete `Plan` (lines 20-25) and `Type` (lines 27-28).  Keep `Metadata`, `Postgres` and `Project`: they are not copies.

In both files replace every call `Plan(` with `Plans.Result(` and every call `Type(` with `Plans.TypeNamed(`.  For example, `RunPlannerTests.cs:92` becomes:

```csharp
        _ = project.AddSource("Repo/Repository.cs", Plans.TypeNamed("Repository"));
```

and `RunPlannerErrorTests.Project` ends with:

```csharp
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"Q\""));
```

Do not touch `RunPlanner.Plan(`, a call of the product that a test may make itself.

- [ ] **Step 3: Use them in the two other copies of the type's text**

`Commands/FilterTests.cs:26-29` becomes:

```csharp
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"Q\""));
```

`TestProjectTests.cs:47` becomes:

```csharp
        _ = project.AddSource("Queries.cs", Plans.Type);
```

- [ ] **Step 4: Write `ConnectionArguments`**

Create `tests/SqlSource.Tool.Tests/ConnectionArguments.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// A value of "--connection" as the tool reads it, for a test that has the text.
internal static class ConnectionArguments
{
    public static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }
}
```

`ConnectionArgument` is the record of `src/SqlSource.Tool/Describing/ConnectionArgument.cs`.

- [ ] **Step 5: Use it, and drop `ToolRoot`**

- `Describing/ConnectionTests.cs`: delete `Parse` (lines 15-19) and `ToolRoot` (line 31).  `Parse(` at lines 44, 60 and 64 and `given.Select(Parse)` become `ConnectionArguments.Parse(` and `given.Select(ConnectionArguments.Parse)`.  Each `ToolRoot()` becomes `Cli.BuildCommands()`.
- `Describing/DatabaseRunsTests.cs`: delete `Parse` (lines 50-54).  `.Select(Parse)` at line 69 becomes `.Select(ConnectionArguments.Parse)`.
- `Commands/UsageCheckTests.cs`: delete `ToolRoot` (line 377).  Each `ToolRoot()` becomes `Cli.BuildCommands()`.

- [ ] **Step 6: Check that no copy is left**

Run: `grep -rn --include='*.cs' -E 'ToolRoot|static ConnectionArgument Parse|static RunPlanResult Plan|static string Type\(' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: one line, `ConnectionArguments.cs: public static ConnectionArgument Parse(string text)`.

- [ ] **Step 7: Build and run the tests**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: no error.  A `using System.CommandLine;` or `using System.Linq;` that nothing needs any more is reported; delete it.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 845 tests.

- [ ] **Step 8: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Give the planner's tests the Plans of the other tests

Plans.Result is the plan with its errors and Plans.TypeNamed a type with the
attribute.  ConnectionArguments.Parse is the one Parse, and ToolRoot goes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `SidecarFiles` (the sidecar of items 3 and 8)

**Files:**
- Rename: `tests/SqlSource.Tool.Tests/TestSidecar.cs` to `tests/SqlSource.Tool.Tests/SidecarFiles.cs`
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj:40-52`
- Modify: `tests/AGENTS.md`
- Modify: every file of `tests/SqlSource.Tool.Tests` that names `TestSidecar.`, that holds `+ ".json"`, that builds an empty `Sidecar` or that declares `Old`: `Commands/CheckFilterTests.cs`, `Commands/CheckTests.cs`, `Commands/DescribeRunTests.cs`, `Commands/FilterRunTests.cs`, `Commands/SecretTests.cs`, `Commands/VerboseTests.cs`, `Logging/RunLogTests.cs`, `Describing/DatabaseRunsTests.cs`, `Describing/FileOutcomesTests.cs`, `Describing/RunDecisionsTests.cs`, `Describing/SidecarStoreTests.cs`

**Interfaces:**
- Consumes: `SqlSource.Tests.Snapshot.TestSidecars.Entry(string name = "Q", string hash = "h", string engine = "postgres", string? database = "app", string? serverVersion = "16.4", SidecarResultKind resultKind = SidecarResultKind.Rows, ...)`, in `tests/SqlSource.Tests/Snapshot/TestSidecars.cs`; `SidecarFormat.PathFor(string sqlPath)` of the product.
- Produces: `SidecarFiles.EntryFor(PlannedQuery)`, `SidecarFiles.Write(PlannedFile, IEnumerable<SidecarEntry>, string? toolVersion = null, int formatVersion = SidecarFormat.Version)`, `SidecarFiles.WriteCurrent(PlannedFile)`, `SidecarFiles.Read(string sqlPath)`, all as `TestSidecar` has them today; and new: `SidecarFiles.Empty(int formatVersion = SidecarFormat.Version, string? toolVersion = null)` returning the text of a sidecar, `SidecarFiles.Newer` (a `string`), `SidecarFiles.Old` (a `DateTime`).

- [ ] **Step 1: Rename the file and the class**

```bash
git mv tests/SqlSource.Tool.Tests/TestSidecar.cs tests/SqlSource.Tool.Tests/SidecarFiles.cs
```

In `SidecarFiles.cs` rename the class to `SidecarFiles`.  In every other file of `tests/SqlSource.Tool.Tests` replace `TestSidecar.` with `SidecarFiles.`.

Run: `grep -rnw --include='*.cs' 'TestSidecar' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: no output.

- [ ] **Step 2: Link the builder of the generator's tests**

In `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, after the `Compile` item of `TestOptionsProvider.cs` and before `</ItemGroup>`, add:

```xml
        <!--
            The builder of a sidecar's records, with the values a test does not care about filled in, for
            SidecarFiles.  tests/SqlSource.Tests compiles the same file.
        -->
        <Compile Include="../SqlSource.Tests/Snapshot/TestSidecars.cs" Link="Snapshot/TestSidecars.cs" />
```

- [ ] **Step 3: Rewrite `SidecarFiles`**

Replace the whole of `tests/SqlSource.Tool.Tests/SidecarFiles.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Tests;

// A sidecar as a file: one that a test puts beside a .sql file, or reads from there.  TestSidecars, of the
// generator's tests, builds the records.
internal static class SidecarFiles
{
    // A time long before any run, which a test gives a sidecar so that a write to it would show.
    public static DateTime Old { get; } = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    // The text of a sidecar that a newer tool wrote: a format above the one this tool writes.
    public static string Newer { get; } = Empty(2, "9.9.9");

    // An entry that is current for the query: its hash and its database, and a statement without rows.
    public static SidecarEntry EntryFor(PlannedQuery query) =>
        TestSidecars.Entry(
            query.Query.Name,
            query.Hash.ShouldNotBeNull(),
            database: query.Database,
            resultKind: SidecarResultKind.None
        );

    // The text of a sidecar with no entry.  The versions are the tool's unless given.
    public static string Empty(int formatVersion = SidecarFormat.Version, string? toolVersion = null) =>
        SidecarWriter.Write(
            new Sidecar(formatVersion, toolVersion ?? PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty)
        );

    // Writes a sidecar beside the file and gives its path.  The versions are the tool's unless given.
    public static string Write(
        PlannedFile file,
        IEnumerable<SidecarEntry> entries,
        string? toolVersion = null,
        int formatVersion = SidecarFormat.Version
    )
    {
        var path = SidecarFormat.PathFor(file.Path);
        var sidecar = new Sidecar(
            formatVersion,
            toolVersion ?? PackageVersion.Prefix,
            new EquatableArray<SidecarEntry>([.. entries])
        );
        File.WriteAllText(path, SidecarWriter.Write(sidecar));
        return path;
    }

    // A sidecar with a current entry for every query of the file that needs one.
    public static string WriteCurrent(PlannedFile file) =>
        Write(file, file.Queries.Where(query => query.NeedsEntry).Select(EntryFor));

    public static Sidecar Read(string sqlPath) =>
        SidecarReader.Read(File.ReadAllText(SidecarFormat.PathFor(sqlPath))).Sidecar.ShouldNotBeNull();
}
```

`TestSidecars.Entry` gives a statement without rows no table, no plan, no table match and no columns, an engine of `postgres` and a server version of `16.4`: the record `EntryFor` built by hand.

- [ ] **Step 4: Run the tests that depend on a current entry**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.RunDecisionsTests"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.FileOutcomesTests"`
Expected: PASS.

If either fails, the entry is not the one it was: compare `TestSidecars.Entry` with the body `EntryFor` had (`git show HEAD:tests/SqlSource.Tool.Tests/TestSidecar.cs`) and pass the member that differs by name.  Do not change `TestSidecars.cs`.

- [ ] **Step 5: Use `Empty` and `Newer` where a sidecar is built by hand**

| Place | Before | After |
|----|----|----|
| `Commands/CheckFilterTests.cs:47-49` | `SidecarWriter.Write(new Sidecar(SidecarFormat.Version, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty))` | `SidecarFiles.Empty()` |
| `Commands/CheckTests.cs:321` | `SidecarWriter.Write(new Sidecar(formatVersion, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty))` | `SidecarFiles.Empty(formatVersion)` |
| `Commands/SecretTests.cs:54` | `SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty))` | `SidecarFiles.Newer` |
| `Describing/FileOutcomesTests.cs:216` | the same | `SidecarFiles.Newer` |
| `Commands/FilterRunTests.cs:90` and `:165` | `var newer = SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty));` | delete the line, and write `SidecarFiles.Newer` where `newer` was used |

- [ ] **Step 6: Use `Old`**

In `Commands/CheckTests.cs:18`, `Commands/FilterRunTests.cs:18`, `Commands/DescribeRunTests.cs:19` and `Describing/SidecarStoreTests.cs:19` delete `private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);` and the blank line after it.  Each use `Old` in those four files becomes `SidecarFiles.Old`.

- [ ] **Step 7: Use `SidecarFormat.PathFor` for every path of a sidecar**

Every `X + ".json"` in `tests/SqlSource.Tool.Tests`, where `X` is the path of a `.sql` file, becomes `SidecarFormat.PathFor(X)`.  `X` is the whole expression before the `+`:

| Before | After |
|----|----|
| `private string Sidecar => _users + ".json";` | `private string Sidecar => SidecarFormat.PathFor(_users);` |
| `File.Exists(_users + ".json")` | `File.Exists(SidecarFormat.PathFor(_users))` |
| `ActionOf(_users + ".json", ...)` | `ActionOf(SidecarFormat.PathFor(_users), ...)` |
| `plan.Files[0].Path + ".json"` | `SidecarFormat.PathFor(plan.Files[0].Path)` |
| `Directory.CreateDirectory(locked + ".json")` | `Directory.CreateDirectory(SidecarFormat.PathFor(locked))` |

A file that gains the call and does not name the namespace gets `using SqlSource.Snapshot;`, in the alphabetical place among its usings.

Run: `grep -rn --include='*.cs' '+ ".json"' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: no output.

- [ ] **Step 8: Name the linked file in `tests/AGENTS.md`**

In `tests/AGENTS.md`, after the first bullet (the one that starts `**\`tests/SqlSource.Tests/Generator/\` is compiled twice`), add:

```markdown
- **`tests/SqlSource.Tests/Snapshot/TestSidecars.cs` is compiled into `tests/SqlSource.Tool.Tests` too**, by exact path in its `.csproj`, for `tests/SqlSource.Tool.Tests/SidecarFiles.cs`: a move or rename of it changes that file.  It uses the sidecar model and nothing else of its own project, and a helper it needs goes into the file itself.
```

- [ ] **Step 9: Build and run the tests**

Run: `dotnet build SqlSource.slnx`
Expected: no error.  Delete a `using` the build reports as not needed, such as `using SqlSource.Snapshot;` in a file that built a `Sidecar` and no longer does.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 845 tests.

- [ ] **Step 10: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests tests/AGENTS.md
git commit -m "Name the tool tests' sidecars on disk SidecarFiles, built by TestSidecars

TestSidecars.cs of the generator's tests is linked into the tool's tests, so an
entry is built in one place.  SidecarFiles gains Empty, Newer and Old, and a
sidecar's path is SidecarFormat.PathFor everywhere.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Fixture texts, the clock and secrets (the rest of item 3)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/SqlTexts.cs`, `tests/SqlSource.Tool.Tests/Secrets.cs`, `tests/SqlSource.Tool.Tests/TestClockTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/TestClock.cs`, `tests/SqlSource.Tool.Tests/DescribeScene.cs`
- Modify: the test classes named in the steps below

**Interfaces:**
- Produces: `SqlTexts.GetUser`, `SqlTexts.TwoUsers`, `SqlTexts.Users`, `SqlTexts.UsersAndPlain` (constants) and `SqlTexts.WithSecondQueryEdited(string text)`; `Secrets.Word`, `Secrets.Marker`, `Secrets.Connection`, `Secrets.ConnectionWithWord`, `Secrets.ConnectionWithMarker` (constants); `TestClock.StartIso` (a constant).

- [ ] **Step 1: Write `SqlTexts`**

Create `tests/SqlSource.Tool.Tests/SqlTexts.cs`:

```csharp
using System;

namespace SqlSource.Tool.Tests;

// The .sql texts that many tests put in a project.  A test whose text differs for its own sake keeps it.
internal static class SqlTexts
{
    // One query, of the project's own database.
    public const string GetUser = "-- name: GetUser\nSELECT 1;\n";

    // Two queries of the project's own database.
    public const string TwoUsers = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    // A query of the project's own database and one of the database "billing".
    public const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    // Users, and a query that needs no entry.
    public const string UsersAndPlain = Users + "\n-- name: Plain\n-- output: sql\nSELECT 3;\n";

    // The text with the SQL of its second query changed, so that the query has another hash.
    public static string WithSecondQueryEdited(string text) =>
        text.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Compare each class's text with the constant it takes**

For each row, read the constant or the literal at the place and compare it, character for character, with the constant of `SqlTexts`.  If one differs, leave that class as it is and say so in the commit message.

| Place | Today | Takes |
|----|----|----|
| `Commands/CheckFilterTests.cs:14-15` | `const string Users` | `SqlTexts.Users` |
| `Commands/SummaryTests.cs:12-13` | `const string Users` | `SqlTexts.Users` |
| `Commands/FilterRunTests.cs:15-16` | `const string Users` | `SqlTexts.Users` |
| `Commands/VerboseTests.cs:28` | a literal | `SqlTexts.Users` |
| `Describing/FileOutcomesTests.cs:17-19` | `const string Users` | `SqlTexts.UsersAndPlain` |
| `Describing/RunDecisionsTests.cs:18-20` | `const string Users` | `SqlTexts.UsersAndPlain` |
| `Commands/FilterTests.cs:33-34` | a literal of two lines | `SqlTexts.UsersAndPlain` |
| `Commands/CheckTests.cs:16` | `const string Users` | `SqlTexts.TwoUsers` |
| `Commands/DescribeRunTests.cs:15` | `const string Users` | `SqlTexts.TwoUsers` |
| `Logging/RunLogTests.cs:18` | `const string Users` | `SqlTexts.TwoUsers` |
| `Commands/ExitCodeTests.cs:18`, `Commands/CliTests.cs:256` and `:277`, `Commands/DescribeRunTests.cs:353`, `Describing/RedactionTests.cs:24`, `Describing/SidecarStoreTests.cs:29` | the literal `"-- name: GetUser\nSELECT 1;\n"` | `SqlTexts.GetUser` |
| `Commands/DescribeRunTests.cs:66` and `:72` | the same literal, as the first argument of an `InlineData` | `SqlTexts.GetUser` |

`Describing/DatabaseRunsTests.cs:26-27` is not in the table: its `GetUser` has a parameter.  Leave it.

- [ ] **Step 3: Replace them**

Where the row says `const string Users`, delete the constant and the blank line after it, and write the name from the table at each use of `Users` in that file.  Where it says a literal, write the name in its place.

Then the five edits of the second query:

| Place | Before | After |
|----|----|----|
| `Commands/SummaryTests.cs:48` | `Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal)` | `SqlTexts.WithSecondQueryEdited(SqlTexts.Users)` |
| `Commands/FilterRunTests.cs:45` | the same | `SqlTexts.WithSecondQueryEdited(SqlTexts.Users)` |
| `Commands/DescribeRunTests.cs:223` | the same | `SqlTexts.WithSecondQueryEdited(SqlTexts.TwoUsers)` |
| `Logging/RunLogTests.cs:96` | the same | `SqlTexts.WithSecondQueryEdited(SqlTexts.TwoUsers)` |
| `Commands/FilterRunTests.cs:63-65` | `Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal).Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal)` | `SqlTexts.WithSecondQueryEdited(SqlTexts.Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal))` |

Run: `grep -rn --include='*.cs' -E 'SELECT 22;|const string Users' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: two lines: `SqlTexts.cs` for `SELECT 22;`, and `Describing/DatabaseRunsTests.cs` for its own `const string Users`.

- [ ] **Step 4: Write the failing test of the clock**

Create `tests/SqlSource.Tool.Tests/TestClockTests.cs`:

```csharp
using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestClock stands in for the machine's clock.  This holds its start to the text the tests expect in a log.
public class TestClockTests
{
    [Fact]
    public void Now_NewClock_IsTheInstantOfStartIso()
    {
        var clock = new TestClock();

        TestClock.StartIso.ShouldBe("2026-10-09T14:02:11.412Z");
        clock.Now.ShouldBe(new DateTimeOffset(2026, 10, 9, 14, 2, 11, 412, TimeSpan.Zero));
        clock.GetUtcNow().Offset.ShouldBe(TimeSpan.Zero);
    }
}
```

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL with `CS0117: 'TestClock' does not contain a definition for 'StartIso'`.

- [ ] **Step 5: Give the clock its start as text**

In `tests/SqlSource.Tool.Tests/TestClock.cs` add `using System.Globalization;` and replace the property `Now` with:

```csharp
    // Where a new clock stands, as a log writes a time.
    public const string StartIso = "2026-10-09T14:02:11.412Z";

    public DateTimeOffset Now { get; private set; } =
        DateTimeOffset.ParseExact(
            StartIso,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal
        );
```

A constant goes before the field `_elapsed`, if the analyzers ask for that order.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.TestClockTests"`
Expected: PASS, 1 test.

- [ ] **Step 6: Use `StartIso` in the four assertions that hold the seed**

- `Logging/RunLogFileTests.cs:16` becomes:

```csharp
    private const string Head = "{\"event\":\"e\",\"time\":\"" + TestClock.StartIso + "\"";
```

- `Logging/RunLogFileTests.cs:68`, `Logging/RunLogTests.cs:50` and `Logging/RunLogTests.cs:337`: the literal `"2026-10-09T14:02:11.412Z"` becomes `TestClock.StartIso`.

Leave `RunLogFileTests.cs:103` (`...12.912Z`), `RunLogTests.cs:58` (`...11.462Z`) and `RunLogTests.cs:127` (`...11.487Z`): each is a time the test worked out from what it advanced the clock by.

Run: `grep -rn --include='*.cs' '2026-10-09T14:02:11.412Z' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: two lines, in `TestClock.cs` and `TestClockTests.cs`.

- [ ] **Step 7: Write `Secrets`**

Create `tests/SqlSource.Tool.Tests/Secrets.cs`:

```csharp
namespace SqlSource.Tool.Tests;

// The values that stand for a secret.  A test searches what the tool wrote for one of them, so none is a word the
// tool writes itself, and each differs from the others where a test tells them apart.
internal static class Secrets
{
    // A secret of one word.
    public const string Word = "s3cret";

    // A word that no other value of a test holds, for a search of everything a run wrote.
    public const string Marker = "s3cret-marker";

    // The connection of the databases of DescribeScene.
    public const string Connection = "Host=db;Password=hunter2";

    public const string ConnectionWithWord = "Host=db;Password=" + Word;

    public const string ConnectionWithMarker = "Host=db;Password=" + Marker;
}
```

- [ ] **Step 8: Use it**

Delete each declaration and write the name at each use.  Replace whole identifiers only: `Secret` is not `SecretTests`, and the constant `Value` is not a member access such as `variable.Value` or `pair.Value`.

| File | Delete | Each use becomes |
|----|----|----|
| `Commands/FilterTests.cs:13`, `Commands/DescribeTests.cs:13`, `Commands/UsageCheckTests.cs:13`, `Commands/CliTests.cs:16`, `Describing/ConnectionTests.cs:13` | `private const string Secret = "s3cret";` | `Secret` → `Secrets.Word` |
| `Commands/SecretTests.cs:15-17`, `Describing/RedactionTests.cs:15-17` | `Marker` and `Value`, with the comment between them if it only says what they are | `Marker` → `Secrets.Marker`; `Value` → `Secrets.ConnectionWithMarker` |
| `Logging/SessionRunLogTests.cs:10` | `private const string Secret = "Host=db;Password=hunter2";` | `Secret` → `Secrets.Connection` |
| `Describing/DatabaseRunsTests.cs:22` | `private const string Secret = "Host=db;Password=s3cret";` | `Secret` → `Secrets.ConnectionWithWord` |
| `DescribeScene.cs:11` | `public const string Connection = "Host=db;Password=hunter2";` | `Connection` in the constructor, and `DescribeScene.Connection` in `Commands/FilterRunTests.cs`, `Commands/DescribeRunTests.cs` and `Commands/CliTests.cs` → `Secrets.Connection` |

If the comment between `Marker` and `Value` says something a reader of the test needs, move it to the member of `Secrets` it is about.

Then the literals:

- `Describing/DatabaseRunsTests.cs:249`, `:313`, `:346`, `:350`, `:369`, `:478`, `:486` and `Describing/RedactionTests.cs:107`: the literal `"s3cret"` becomes `Secrets.Word`.
- `Describing/Describers/DescriberTests.cs:105`: the literal `"Host=db;Password=s3cret"` becomes `Secrets.ConnectionWithWord`.

Leave `Describing/DatabaseRunsTests.cs:524` (`Awkward`, a value with a quote and a line end) and `:604` (a fact about `Redaction.Of` and a text, which pull request 2 moves).

Run: `grep -rn --include='*.cs' -E 's3cret|hunter2' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: five lines: three in `Secrets.cs` (`Word`, `Marker`, `Connection`) and two in `DatabaseRunsTests.cs` (`Awkward`, and the fact about `Redaction.Of`).  Any other line is a copy that was missed.

- [ ] **Step 9: Build and run the tests**

Run: `dotnet build SqlSource.slnx`
Expected: no error.  Delete a `using System;` that a file needed only for `StringComparison` and that the build now reports.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 846 tests.

- [ ] **Step 10: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Hold the tool tests' fixture texts, secrets and the clock's start once

SqlTexts has the four .sql texts and the edit of a second query, Secrets the
five values that stand for a secret, and TestClock.StartIso the seed as a log
writes it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: What `FakeDescriber` lacks (item 4)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/FakeDescriber.cs:29-34,73-80`
- Modify: `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs`, `Commands/CheckFilterTests.cs`, `Commands/SecretTests.cs`, `Describing/RedactionTests.cs`, `Describing/DatabaseRunsTests.cs`

**Interfaces:**
- Consumes: `Secrets.ConnectionWithMarker`, `Secrets.ConnectionWithWord` from Task 4.
- Produces: `FakeDescriber.Rows` (a `QueryDescription`); `FakeDescriber.ServerWith(string version = "16.4", string? driverVersion = null)` returning `ServerInfo`; `FakeDescriber.Failure(string argument, DescribeStep? step, string? help, params string[] serverLines)` returning `DescribeFailure`, beside the existing `Failure(string query, params string[] serverLines)`.

- [ ] **Step 1: Add the members**

In `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, after `NoRows` add:

```csharp
    // A statement that produces rows, where the describer says none unless told.
    public static QueryDescription Rows { get; } =
        new(
            SidecarResultKind.Rows,
            EquatableArray<DescribedParameter>.Empty,
            EquatableArray<SidecarColumn>.Empty,
            null,
            "not-needed",
            "no-origin"
        );
```

Replace the property `Server` with:

```csharp
    public ServerInfo Server { get; set; } = ServerWith(driverVersion: "1.2.3");
```

Replace the method `Failure` with:

```csharp
    // The server of the fake driver.  A driver without a version is named alone.
    public static ServerInfo ServerWith(string version = "16.4", string? driverVersion = null) =>
        new(version, "FakeDriver", driverVersion, EquatableArray<ServerSetting>.Empty);

    public static DescribeFailure Failure(string query, params string[] serverLines) =>
        Failure(query, DescribeStep.DescribeColumns, "check the SQL", serverLines);

    // A failure at a step, or at none, with the help it gives, or none.  Name "step" or "help" where it is null.
    public static DescribeFailure Failure(
        string argument,
        DescribeStep? step,
        string? help,
        params string[] serverLines
    ) =>
        new(
            Rejected,
            new EquatableArray<string>([argument]),
            step,
            new EquatableArray<string>([.. serverLines]),
            help
        );
```

- [ ] **Step 2: Use `Rows`**

Delete `private static readonly QueryDescription Rows = new(...);` at `Commands/CheckTests.cs:111-119`, with its comment, and at `Commands/CheckFilterTests.cs:17-24`.  Each use `Rows` in the two files becomes `FakeDescriber.Rows`.

- [ ] **Step 3: Use `ServerWith`**

| Place | After |
|----|----|
| `Commands/SecretTests.cs:259-264` and `:302-307` | `_scene.Describer.Server = FakeDescriber.ServerWith($"16.4 ({Secrets.ConnectionWithMarker})");` |
| `Describing/DatabaseRunsTests.cs:496-501` | `_describer.Server = FakeDescriber.ServerWith($"16.4 at {Secrets.ConnectionWithWord}");` |
| `Describing/DatabaseRunsTests.cs:525` | `_describer.Server = FakeDescriber.ServerWith($"16.4 {Awkward}");` |
| `Describing/DatabaseRunsTests.cs:533` | `_describer.Server = FakeDescriber.ServerWith();` |

Leave `Describing/DatabaseRunsTests.cs:299` and `Describing/RedactionTests.cs:120`: their driver is the secret, not the fake driver.

- [ ] **Step 4: Use the overload of `Failure`**

| Place | After |
|----|----|
| `Commands/SecretTests.cs:36-42` | `_scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad", DescribeStep.Explain, "check the SQL", "could not explain");` |
| `Commands/SecretTests.cs:265-271` | `_scene.Describer.Failures["Bad"] = FakeDescriber.Failure($"Bad at {Secrets.ConnectionWithMarker}", DescribeStep.DescribeColumns, $"check {Secrets.ConnectionWithMarker}", $"connection to {Secrets.ConnectionWithMarker} was lost");` |
| `Describing/RedactionTests.cs:157-163` | `_scene.Describer.Failures["GetUser"] = FakeDescriber.Failure(Secrets.ConnectionWithMarker, DescribeStep.Explain, "try " + Secrets.ConnectionWithMarker, "server said " + Secrets.ConnectionWithMarker);` |
| `Describing/DatabaseRunsTests.cs:211-217` | `_describer.OpenFailure = FakeDescriber.Failure("(open)", step: null, help: null, "28P01: password authentication failed");` |
| `Describing/DatabaseRunsTests.cs:239-245` | `_describer.OpenFailure = FakeDescriber.Failure($"open on {Secrets.ConnectionWithWord}", step: null, $"check {Secrets.ConnectionWithWord}", $"invalid connection string: {Secrets.ConnectionWithWord}");` |
| `Describing/DatabaseRunsTests.cs:300-306` | `_describer.Failures["GetUser"] = FakeDescriber.Failure($"GetUser on {Secrets.ConnectionWithWord}", DescribeStep.Catalog, $"check {Secrets.ConnectionWithWord}", $"could not connect to {Secrets.ConnectionWithWord}");` |

`./format.sh` breaks the long lines.

Run: `grep -rn --include='*.cs' -E 'new DescribeFailure\(|"FakeDriver", *$|"FakeDriver", null' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: no output.

- [ ] **Step 5: Build and run the tests**

Run: `dotnet build SqlSource.slnx`
Expected: no error.  Delete a `using` the build reports as not needed.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 846 tests.

- [ ] **Step 6: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Give FakeDescriber what the tests built around it

Rows, the server of the fake driver, and a failure with its step and its help.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: One way to build a host (item 7)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/CliRun.cs:36,82-94`
- Modify: `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs`, `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`

**Interfaces:**
- Consumes: `Hosts.Create(string workingDirectory, IProcessRunner processes, string tempDirectory, int processorCount = 4, IReadOnlyDictionary<string, string>? environment = null)`; `Plans.Postgres(TempFolder folder, string name = "App", string? directory = null)`; `Expected.See`.
- Produces: `CliRun.Out` with a setter.

- [ ] **Step 1: Build the host of a `CliRun` through `Hosts.Create`**

In `tests/SqlSource.Tool.Tests/CliRun.cs` change the property to:

```csharp
    // Set to stand in for standard output, for a test of a writer that fails.
    public TextWriter? Out { get; set; }
```

and replace `CreateHost` with:

```csharp
    private ToolHost CreateHost(TextWriter output, TextWriter error) =>
        Hosts.Create(Folder.Path, Processes, Folder.CreateFolder("tmp"), environment: Environment) with
        {
            Out = Out ?? output,
            Error = error,
            Describers = new DescriberRegistry(Describers),
            Exchange = _ => Exchange,
            Time = Time,
        };
```

`Hosts.Create` reads the dictionary when a variable is asked for, so a variable a test sets after the host was made is still seen, as it is today.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Commands"`
Expected: PASS.

Run: `grep -rn --include='*.cs' 'new ToolHost(' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: lines in `Hosts.cs` only, and in `ToolHostTests.cs` if it builds one to test the record itself.

- [ ] **Step 2: Hold the run of `DescribeTests` in a field**

In `tests/SqlSource.Tool.Tests/Commands/DescribeTests.cs`:

1. Add `using System;` and change the declaration to:

```csharp
public sealed class DescribeTests : IDisposable
{
    private readonly CliRun _run = new();

    public void Dispose() => _run.Dispose();
```

2. In each of the twenty-three tests delete the line `using var run = new CliRun();` and rename `run` to `_run` in the rest of the test.
3. Delete the constant `Queries` at line 278.
4. In the four tests that build a project by hand (`Run_ProjectWithAQueryToDescribe_IsSqlsrc216AndASummaryLine`, `Run_ProjectWhoseQueriesNeedNoEntry_PrintsNothing`, `Run_ProjectWithAFileThatDoesNotParse_ReportsItAsABuildWouldAndExitsOne`, `Plan_Project_GivesThePlanOfTheCommandLine`) replace:

```csharp
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
```

with:

```csharp
        var project = Plans.Postgres(_run.Folder).AnsweredBy(_run.Processes);
```

`Run_ProjectWhoseQueriesNeedNoEntry_PrintsNothing` keeps its line `project.Properties["SqlSourceOutput"] = "sql";` after it.

`Plans.Postgres` makes the project `App` in the folder `App`, sets the dialect `postgres` and adds `Queries.cs` with `Plans.Type`, which is the text of the constant `Queries`: the same project.

- [ ] **Step 3: Hold the run of `CliTests` in a field**

In `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`:

1. Change the declaration to `public sealed class CliTests : IDisposable`, with the field and `Dispose` as in Step 2, and `using System;` if the file lacks it.
2. In each test delete `using var run = new CliRun();` and rename `run` to `_run`.
3. In the six tests that start with `using var run = new CliRun { Out = new FailingWriter("...") };` (lines 196, 211, 229, 240, 252, 273) the line becomes:

```csharp
        _run.Out = new FailingWriter("the pipe is closed");
```

with the message that test had: line 240 has `"first\r\nsecond\nthird"`.

If the build reports `CA2000` for a `FailingWriter`, hold it in a local the test disposes:

```csharp
        using var failing = new FailingWriter("the pipe is closed");
        _run.Out = failing;
```

- [ ] **Step 4: Check and run**

Run: `grep -rn --include='*.cs' 'new CliRun' tests/SqlSource.Tool.Tests | grep -v '/obj/'`
Expected: the fields of the classes that hold a run, the property of `DescribeScene`, and two locals: `TestProjectTests.cs:43` and `Commands/FilterRunTests.cs:223`, each a test that needs a run of its own beside what its class holds.  No line in a test method of `DescribeTests` or `CliTests`.

Run: `dotnet build SqlSource.slnx`
Expected: no error.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, 846 tests.

- [ ] **Step 5: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests
git commit -m "Build every host of the tool's tests through Hosts.Create

CliRun sets what differs with a with expression.  DescribeTests and CliTests
hold their run in a field, and four tests take their project from Plans.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The tech-debt item, the list of tests and the pull request

**Files:**
- Modify: `docs/tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0039)
- Add: `docs/superpowers/plans/2026-10-11-td-0039-pr1-tool-test-helpers.md` (this file)

- [ ] **Step 1: Compare the list of tests with the one Task 1 saved**

```bash
dotnet build SqlSource.slnx
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0039-pr1-after.txt"
diff "${TMPDIR:-/tmp}/td-0039-pr1-before.txt" "${TMPDIR:-/tmp}/td-0039-pr1-after.txt"
```

Expected: two added lines and none removed:

```
>   SqlSource.Tool.Tests.ExpectedTests.See_EveryDiagnostic_IsTheLineThatNamesItsSection
>   SqlSource.Tool.Tests.TestClockTests.Now_NewClock_IsTheInstantOfStartIso
```

A removed line is a lost test: find it and put it back before going on.

- [ ] **Step 2: Check that nothing under `src/` changed**

Run: `git diff --stat main -- src README.md CONTRIBUTING.md docs/diagnostics.md docs/publishing.md`
Expected: no output.

- [ ] **Step 3: Rewrite TD-0039 to what remains**

Replace the whole of `docs/tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md` with the text below.  The numbers are the item's own, so 5, 6 and 8 keep theirs.

```markdown
# TD-0039 - Duplicated and uneven patterns in the tests

## Problem

A maintainability review of `tests/` on 2026-10-10 found eight of these.  Items 1, 2, 3, 4 and 7, and the sidecar of item 8, are resolved; the numbers of the rest are the review's.  No test is wrong; each copy is one more place a change has to reach.

5. **Test classes that cover several subjects.**  [`SqlFileParserTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs), about 1,590 lines, covers blocks and the preamble, tokens, generator-parameter errors, dialect markers, parameters and declared variables, `token-ignore`, the shape of a name, and the output, database and model markers, with its helpers scattered between tests.  [`GeneratedSourceTests`](../../tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs), about 1,090 lines, mixes layout snapshots, ordering, methods, escaping, the rule that generated code has no warning, and hint-name collisions; the generated file's three-line header is repeated five times there, five in `TypeEmitterTests` and once in `FileDiagnosticsTests`, and the triple `Diagnostics`, `CompilationErrors`, `GeneratedCodeWarnings` `.ShouldBeEmpty()` twenty-seven times there and six in four other classes.  [`DatabaseRunsTests`](../../tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs) mixes connection errors, describer failures, redaction and cancellation, and holds a fact about `Redaction.Of` that belongs in `RedactionTests`.
6. **Theories that dispatch on a string.**  `DescribeRunTests`, `CheckTests`, `ExitCodeTests` and `RunLogTests` each have a `[Theory]` whose rows are kinds such as `"a merge conflict"` or `"a directory"` that a `switch` inside the test turns into a different arrangement.  A failure names a magic string, not a path.
8. **Small duplicates in `tests/SqlSource.Tests`.**  `SpanOf` in `SqlFileParserTests` uses `IndexOf` and the one in `SqlGeneratorParameterScopeTests` uses `LastIndexOf`; `Literal` and `Text` both wrap `new SqlSegment(Literal, ...)`; the eleven-argument `SqlQuery` is built by hand in five places though `TypeEmitterTests.Query` exists.

## Why it exists

Each sub-phase added its tests beside the last's and copied the nearest helper.

## Impact

Low.  A change to the generated file's header reaches eleven snapshots.  5 and 6 make a failure harder to place.

## Proposed fix

The design is `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`, whose pull requests 2 and 3 are what remains.

5. Split `SqlFileParserTests` into one class per subject sharing a `ParseOf` helper; split `GeneratedSourceTests` the same way, with `GeneratorRun.ShouldBeClean()` and a `GeneratedFile.Header` constant in the harness, which stays within Roslyn 4.8.0; split `DatabaseRunsTests` over a `DatabaseRunsScene`, and move the `Redaction.Of` fact.
6. One `[Fact]` per kind, sharing a private assertion.
8. `TestModels.Literal`, `TestModels.Query(...)`, one `TextSpans.Of(text, value, last)`.

## Trigger

The first pull request that changes the generated file's header and has to touch more than two test classes to do it; or before phase 3, whose describer tests will be added to `DatabaseRunsTests`.
```

- [ ] **Step 4: Shorten the row of the table**

In `docs/tech-debt/README.md` replace the description of the row of `TD-0039`, the last cell, with:

```
Duplicated and uneven patterns in `tests/`: test classes that cover several subjects, theories that dispatch on a string, and small duplicates among the helpers of `tests/SqlSource.Tests` |
```

The id, the status `Open`, the date `2026-10-10` and the impact `Low` stay.

- [ ] **Step 5: Commit the item and this plan**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add docs/tech-debt docs/superpowers/plans/2026-10-11-td-0039-pr1-tool-test-helpers.md
git commit -m "Rewrite TD-0039 to what remains, and add the plan

The helpers of the tool's tests are done: items 1, 2, 3, 4 and 7, and the
sidecar of item 8.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Check the version, as `AGENTS.md` asks before a pull request**

```bash
git fetch --tags origin
git tag --list 'v*' --sort=-v:refname | head -n 1
grep -n '<VersionPrefix>' Directory.Build.props
```

Expected: `VersionPrefix` is `0.1.0`.  With no `v*` tag there is nothing to compare.  If a tag is not lower than `VersionPrefix`, stop and ask the user for the new version.

- [ ] **Step 7: Open the pull request**

Ask the user before pushing.  Then:

```bash
git push -u origin claude/td-0039-tool-test-helpers
gh pr create --base main --title "TD-0039, 1 of 3: the helpers of the tool's tests" --body "$(cat <<'EOF'
The first of three pull requests for TD-0039, the duplicated and uneven patterns in the tests.  The design is `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`.

Each helper, constant and fixture text of items 1, 2, 3, 4 and 7, and the sidecar of item 8, now has one definition at the root of `tests/SqlSource.Tool.Tests`:

- `Expected.See(id)` is the help link of a diagnostic, held to the descriptors by a test.
- `Plans.Result` and `Plans.TypeNamed` replace the planner tests' own `Plan` and `Type`; `ConnectionArguments.Parse` is the one `Parse`; `ToolRoot` goes.
- `SidecarFiles` is `TestSidecar` renamed, with `Empty`, `Newer` and `Old`.  Its entry is built by `TestSidecars` of the generator's tests, linked by path.  A sidecar's path is `SidecarFormat.PathFor` everywhere.
- `SqlTexts`, `Secrets` and `TestClock.StartIso` hold the fixture texts, the secrets and the clock's start.
- `FakeDescriber` gains `Rows`, `ServerWith` and a `Failure` with its step and help.
- `CliRun` builds its host through `Hosts.Create`; `DescribeTests` and `CliTests` hold a run in a field.

Nothing under `src/` changes.  No test moves or is renamed; the list of tests is the one of `main` plus `ExpectedTests` and `TestClockTests`.  TD-0039 is rewritten to items 5, 6 and the rest of 8.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Reply to every CodeRabbit finding in its thread, as `AGENTS.md` says.
