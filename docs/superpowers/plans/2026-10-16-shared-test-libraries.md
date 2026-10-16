# Shared Test Libraries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the twenty test helper files that test projects compile from each other with two class libraries, `SqlSource.Testing` and `SqlSource.Tool.Testing`, leaving the RoslynFloor compile of the generator's test classes as the one visible exception.

**Architecture:** `tests/SqlSource.Testing` references only `src/SqlSource` and the pinned Roslyn 4.8.0, so `tests/SqlSource.Tests.RoslynFloor` can reference it.  `tests/SqlSource.Tool.Testing` references the tool (Roslyn 5.9.0) and `SqlSource.Testing`.  Types stay `internal`, reached through `InternalsVisibleTo`.  A small layout test in `tests/SqlSource.Tests` keeps the rule from drifting.

**Tech Stack:** .NET 10, MSBuild central package management with transitive pinning, xunit v3 on Microsoft.Testing.Platform, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-16-shared-test-libraries-design.md`

## Global Constraints

- `tests/SqlSource.Tests.RoslynFloor` resolves `Microsoft.CodeAnalysis.CSharp` 4.8.0 and nothing newer, through any reference.
- `SqlSource.Testing` has no `VersionOverride` on `Microsoft.CodeAnalysis.CSharp`; its code uses only API Roslyn 4.8.0 has, and hands the compiler C# 12 at most.
- `SqlSource.Tool.Testing` uses `VersionOverride="5.9.0"` on `Microsoft.CodeAnalysis.CSharp`, as the tool's tests do.
- Both libraries: class libraries (no `OutputType` `Exe`), `IsPackable` false, `IsTestProject` false, never run by `dotnet test --solution SqlSource.slnx`, no tests of their own.
- No type becomes `public` and no product internal is exposed beyond `InternalsVisibleTo`.
- A moved file changes only its namespace, its `using`s and comments that name its old place.  Names, signatures and behaviour stay.
- Namespaces follow folders.  `ImplicitUsings` is disabled: each consumer file adds the `using` it needs.
- A warning is turned off in `.editorconfig`, never with `NoWarn` (`Directory.Build.props`).
- Linked files that are not code stay as they are: the JSON schema, `docs/diagnostics.md`, `SqlSource.props`/`.targets`, `global.json`, `Fixtures/Projects/**`, `Fixtures/Describers/**`, the compiler's `.editorconfig`.
- Lock files are committed: after a project or package change, run `dotnet restore SqlSource.slnx --force-evaluate` and commit every changed `packages.lock.json`.
- Each commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **RoslynFloor silently moving off 4.8.0** through the new reference: `RoslynVersionTests.Tests_RunOnTheOldestSupportedRoslyn` must pass, and `tests/SqlSource.Tests.RoslynFloor/packages.lock.json` must resolve `Microsoft.CodeAnalysis.CSharp` 4.8.0 (Task 1, Step 7).
2. **A test that stops being discovered** after the move: each project's test total equals the baseline, plus only the layout tests this plan adds (Task 1, Step 1; every task's run step).
3. **A linked `.cs` file coming back** in a later change: `TestLayoutTests.TestProjects_CompileNoFileOfAnotherProject` (Task 2).
4. **A helper put back into `Generator/`, or a new test there without the warning**: `TestLayoutTests.GeneratorFolder_HoldsOnlyTestClasses` (Task 1) and `TestLayoutTests.GeneratorTests_SayTheyAreShared` (Task 3).
5. **The tests that start the built tool** no longer finding `SqlSource.Tool.dll` or its runtime config beside the test assembly: `TerminationTests` and `ToolProcessTests` run by name (Task 2, Step 7).

---

### Task 1: `SqlSource.Testing`, with the generator harness and `TestSidecars`

**Files:**
- Create: `tests/SqlSource.Testing/SqlSource.Testing.csproj`
- Create: `tests/SqlSource.Testing/RepositoryRoot.cs`
- Move (`git mv`): `tests/SqlSource.Tests/Generator/GeneratorHarness.cs`, `InMemoryAdditionalText.cs`, `TestOptionsProvider.cs` → `tests/SqlSource.Testing/Generator/`
- Move (`git mv`): `tests/SqlSource.Tests/Snapshot/TestSidecars.cs` → `tests/SqlSource.Testing/Snapshot/TestSidecars.cs`
- Create: `tests/SqlSource.Tests/TestLayoutTests.cs`
- Modify: `Directory.Packages.props`, `SqlSource.slnx`, `src/SqlSource/SqlSource.csproj`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `tests/SqlSource.Tests.RoslynFloor/SqlSource.Tests.RoslynFloor.csproj`, `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
- Modify: `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`
- Modify (`using`s only): every file of `tests/SqlSource.Tests/Generator/`; the files of `tests/SqlSource.Tests/Snapshot/` that use `TestSidecars`; `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, `Generation/MSBuild/DialectSettingTests.cs`, `Generation/MSBuild/MSBuildSettingsTests.cs`; in `tests/SqlSource.Tool.Tests`, each file with `using SqlSource.Tests.Snapshot;` or `using SqlSource.Tests.Generator;`; `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, `ParameterTests.cs`

**Interfaces:**
- Produces: `SqlSource.Testing.RepositoryRoot.Folder` (`internal static string`, the folder that holds `SqlSource.slnx`, found by walking up from `AppContext.BaseDirectory`, throwing `InvalidOperationException` when there is none: the body of today's `RepositoryPaths.FindRoot`).
- Produces: namespaces `SqlSource.Testing.Generator` (`GeneratorHarness`, `SourceFile`, `SqlFile`, `GeneratorRun`, `LoadedAssembly`, `InMemoryAdditionalText`, `TestOptionsProvider`) and `SqlSource.Testing.Snapshot` (`TestSidecars`), unchanged members.
- Produces: project `tests/SqlSource.Testing/SqlSource.Testing.csproj`, which Task 2's library references.

- [ ] **Step 1: Record the baseline test totals**

Run (Docker running): `dotnet test --solution SqlSource.slnx`
Write down the total each of the four projects reports, in a scratch file outside the repository.  Every later total is compared with these.

- [ ] **Step 2: Write the failing layout test**

`tests/SqlSource.Tests/TestLayoutTests.cs`, namespace `SqlSource.Tests`, `public class TestLayoutTests`:

```csharp
private static readonly string Tests = Path.Combine(RepositoryRoot.Folder, "tests");
private static readonly string GeneratorTests = Path.Combine(Tests, "SqlSource.Tests", "Generator");

// tests/SqlSource.Tests.RoslynFloor compiles every file of Generator/; a helper there would be compiled twice.
[Fact]
public void GeneratorFolder_HoldsOnlyTestClasses() =>
    Directory
        .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
        .Select(Path.GetFileName)
        .Where(name => !name!.EndsWith("Tests.cs", StringComparison.Ordinal))
        .ShouldBeEmpty();
```

With a comment above the class saying it holds the rules of `tests/AGENTS.md` that a test can check.

- [ ] **Step 3: Run it to see it fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: FAIL, `RepositoryRoot` does not exist.

- [ ] **Step 4: Create the library**

`tests/SqlSource.Testing/SqlSource.Testing.csproj`: `TargetFramework` `net10.0`, `IsPackable` false, `IsTestProject` false; `PackageReference` `Microsoft.CodeAnalysis.CSharp` (with a comment: no `VersionOverride`, because `tests/SqlSource.Tests.RoslynFloor` references this library and must stay on the pinned Roslyn), `Shouldly`, `xunit.v3.extensibility.core` (for `TestContext`); `ProjectReference` `../../src/SqlSource/SqlSource.csproj`; `InternalsVisibleTo` `SqlSource.Tests`, `SqlSource.Tests.RoslynFloor`, `SqlSource.Tool.Testing`, `SqlSource.Tool.Tests`, `SqlSource.Tests.Postgres`.  Add `PackageVersion` entries in `Directory.Packages.props` for each xunit v3 package the libraries use, at the version `xunit.v3` 4.0.1 already resolves (read it from `tests/SqlSource.Tests/packages.lock.json`).  If `xunit.v3.extensibility.core` is not the package that holds `TestContext` at that version, use the one that does.  It must not be a package that makes the project a test executable.

Add the project to `SqlSource.slnx` under `/tests/`.  Add `InternalsVisibleTo Include="SqlSource.Testing"` to `src/SqlSource/SqlSource.csproj`.

Create `RepositoryRoot.cs` (Interfaces above).  `git mv` the four files.  Change their namespaces, and rewrite the comment at the top of `GeneratorHarness.cs` that says the folder is compiled into RoslynFloor: it is this library that stays within Roslyn 4.8.0 and C# 12, because RoslynFloor references it.

- [ ] **Step 5: Point the consumers at the library**

- `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor`, `tests/SqlSource.Tool.Tests`, `tests/SqlSource.Tests.Postgres`: add `<ProjectReference Include="../SqlSource.Testing/SqlSource.Testing.csproj" />`.  Keep every existing `ProjectReference`.
- `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`: remove the four `Compile` links and their comments.
- `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`: remove the `TestSidecars.cs` link and its comment.
- `tests/SqlSource.Tests.Postgres/RepositoryPaths.cs`: `Root` becomes `RepositoryRoot.Folder`; delete `FindRoot` and `Solution`.
- Add `using SqlSource.Testing.Generator;` / `using SqlSource.Testing.Snapshot;` where the build asks, and replace `using SqlSource.Tests.Snapshot;` / `using SqlSource.Tests.Generator;` in the other test projects.

- [ ] **Step 6: Restore and build**

Run: `dotnet restore SqlSource.slnx --force-evaluate && dotnet build SqlSource.slnx`
Expected: build succeeds with no warning.  If `CA1812` is reported for a library type that only its consumers make, turn it off for the libraries in `.editorconfig`, in a section `[tests/{SqlSource.Testing,SqlSource.Tool.Testing}/**.cs]` beside `[tests/**.cs]`, with a comment saying why.

- [ ] **Step 7: Run the tests and check the floor**

Run: `dotnet test --project tests/SqlSource.Tests`, `dotnet test --project tests/SqlSource.Tests.RoslynFloor`, `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: all pass.  The totals equal the baseline, except `SqlSource.Tests`, which has one more (the layout test).  `RoslynVersionTests` passes.

Run: `grep -A2 '"Microsoft.CodeAnalysis.CSharp"' tests/SqlSource.Tests.RoslynFloor/packages.lock.json`
Expected: `"resolved": "4.8.0"` and no other version.

- [ ] **Step 8: Commit**

```bash
git add -A Directory.Packages.props SqlSource.slnx src tests .editorconfig
git commit -m "Share the generator harness and TestSidecars through SqlSource.Testing"
```

### Task 2: `SqlSource.Tool.Testing`, with the tool's helpers

**Files:**
- Create: `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj`
- Move (`git mv`) from `tests/SqlSource.Tool.Tests/` to `tests/SqlSource.Tool.Testing/`: `CliResult.cs`, `CliRun.cs`, `FakeProcessRunner.cs`, `FakeProject.cs`, `Hosts.cs`, `LogReader.cs`, `Plans.cs`, `Programs.cs`, `Secrets.cs`, `SidecarFiles.cs`, `TempFolder.cs`, `TestProject.cs`
- Move (`git mv`) from `tests/SqlSource.Tool.Tests/Fixtures/` to `tests/SqlSource.Tool.Testing/Fixtures/`: `FixtureCase.cs`, `FixtureCases.cs`, `FixtureReplay.cs`, `FixtureProjects.cs`
- Modify: `tests/SqlSource.Tests/TestLayoutTests.cs`
- Modify: `SqlSource.slnx`, `Directory.Packages.props`, `src/SqlSource/SqlSource.csproj`, `src/SqlSource.Tool/SqlSource.Tool.csproj`
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
- Modify (`using`s only): the files of `tests/SqlSource.Tool.Tests` that use a moved type; the eight files of `tests/SqlSource.Tests.Postgres` with `using SqlSource.Tool.Tests`

**Interfaces:**
- Consumes: `SqlSource.Testing.Snapshot.TestSidecars` (from Task 1), used by `SidecarFiles`.
- Produces: namespaces `SqlSource.Tool.Testing` and `SqlSource.Tool.Testing.Fixtures`, unchanged members.

- [ ] **Step 1: Write the failing layout test**

Add to `TestLayoutTests`:

```csharp
// A test project shares code through SqlSource.Testing or SqlSource.Tool.Testing, never by compiling another
// project's file.  RoslynFloor's compile of the generator's tests is the one exception.
[Fact]
public void TestProjects_CompileNoFileOfAnotherProject() =>
    Directory
        .EnumerateDirectories(Tests)
        .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj"))
        .SelectMany(project =>
            XDocument
                .Load(project)
                .Descendants("Compile")
                .Select(item => (string?)item.Attribute("Include"))
                .Where(include => include?.StartsWith("../", StringComparison.Ordinal) == true)
                .Select(include => $"{Path.GetFileName(project)}: {include}")
        )
        .ShouldBe(["SqlSource.Tests.RoslynFloor.csproj: ../SqlSource.Tests/Generator/**/*.cs"]);
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-method "*TestProjects_CompileNoFileOfAnotherProject"` (or the runner's equivalent filter)
Expected: FAIL, listing the sixteen `Compile` links of `SqlSource.Tests.Postgres.csproj`.

- [ ] **Step 3: Create the library**

`tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj`: `TargetFramework` `net10.0`, `IsPackable` false, `IsTestProject` false; `PackageReference` `Microsoft.CodeAnalysis.CSharp` with `VersionOverride="5.9.0"` (the comment the tool's tests use), `Shouldly`, `xunit.v3.extensibility.core` and the xunit v3 package that holds `Assert.SkipUnless` (`xunit.v3.assert` at 4.0.1 is the expected one); `ProjectReference` `../../src/SqlSource.Tool/SqlSource.Tool.csproj`, `../../src/SqlSource/SqlSource.csproj`, `../SqlSource.Testing/SqlSource.Testing.csproj`; `InternalsVisibleTo` `SqlSource.Tool.Tests`, `SqlSource.Tests.Postgres`.

Add it to `SqlSource.slnx`.  Add `InternalsVisibleTo Include="SqlSource.Tool.Testing"` to `src/SqlSource/SqlSource.csproj` and `src/SqlSource.Tool/SqlSource.Tool.csproj`.  `git mv` the sixteen files and change their namespaces.

- [ ] **Step 4: Point the consumers at the library**

- `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tests.Postgres`: add `<ProjectReference Include="../SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj" />`; keep their reference to the tool, so its assembly and runtime config stay beside the test assembly for `Programs.ToolPath`.
- `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`: remove every remaining `Compile` link and its comment.  Keep the `None` items for `Fixtures/Projects/Single/**`, the MSBuild files and `global.json`, and keep their comment, now naming `SqlSource.Tool.Testing`'s `FixtureProjects` as what copies them.
- `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`: update the comment on `Fixtures/Projects` that names `Fixtures/FixtureProjects.cs`, and the one on `Fixtures/Describers` that names `Fixtures/FixtureCases.cs`, to their new place.
- Add `using SqlSource.Tool.Testing;` / `using SqlSource.Tool.Testing.Fixtures;` where the build asks; in the Postgres project replace `using SqlSource.Tool.Tests...` with them.

- [ ] **Step 5: Restore and build**

Run: `dotnet restore SqlSource.slnx --force-evaluate && dotnet build SqlSource.slnx`
Expected: build succeeds with no warning (the `CA1812` remedy of Task 1, Step 6 applies).

- [ ] **Step 6: Run the tests**

Run (Docker running): `dotnet test --solution SqlSource.slnx`
Expected: all pass.  Totals equal the baseline, except `SqlSource.Tests`, which has two more.  Neither library appears in the output.

- [ ] **Step 7: Run the tests that start the built tool**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*TerminationTests"` and `dotnet test --project tests/SqlSource.Tests.Postgres --filter-class "*ToolProcessTests"`
Expected: both pass, not skipped for a missing tool.

- [ ] **Step 8: Commit**

```bash
git add -A Directory.Packages.props SqlSource.slnx src tests .editorconfig
git commit -m "Share the tool's test helpers through SqlSource.Tool.Testing"
```

### Task 3: Mark the generator's test classes as shared

**Files:**
- Create: `tests/SqlSource.Tests/Generator/AGENTS.md`
- Modify: every `.cs` file of `tests/SqlSource.Tests/Generator/` (first line)
- Modify: `tests/SqlSource.Tests/TestLayoutTests.cs`

**Interfaces:**
- Produces: the header line, exactly: `// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.`

- [ ] **Step 1: Write the failing layout test**

Add to `TestLayoutTests`:

```csharp
private const string SharedHeader =
    "// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.";

[Fact]
public void GeneratorTests_SayTheyAreShared() =>
    Directory
        .EnumerateFiles(GeneratorTests, "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadLines(file).FirstOrDefault() != SharedHeader)
        .Select(Path.GetFileName)
        .ShouldBeEmpty();
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-method "*GeneratorTests_SayTheyAreShared"`
Expected: FAIL, listing the nine test files.

- [ ] **Step 3: Add the header and the folder's `AGENTS.md`**

Put the header as the first line of each file, followed by a blank line, then the existing `using`s.

`tests/SqlSource.Tests/Generator/AGENTS.md`, in the voice of `tests/AGENTS.md`: every `.cs` file here is also compiled into `tests/SqlSource.Tests.RoslynFloor` by the glob in its `.csproj`, so that the generator is tested on Roslyn 4.8.0, the oldest supported; a test here uses only API that version has and hands the compiler C# 12 at most; a helper goes into `tests/SqlSource.Testing`, never into this folder or another folder of `tests/SqlSource.Tests`; a new file starts with the header line; `TestLayoutTests` checks the last two.  End with the maintenance note `tests/AGENTS.md` ends with.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`
Expected: all pass; `SqlSource.Tests` has three more than the baseline, RoslynFloor the same.

- [ ] **Step 5: Commit**

```bash
git add tests/SqlSource.Tests
git commit -m "Mark the generator's tests as compiled into RoslynFloor"
```

### Task 4: Documentation and the whole check

**Files:**
- Modify: `tests/AGENTS.md`, `CONTRIBUTING.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`
- Modify: the comments in `tests/*/*.csproj` that still describe a link that is gone

- [ ] **Step 1: Find every living reference to a moved file or to the links**

Run: `git grep -nE "GeneratorHarness|InMemoryAdditionalText|TestOptionsProvider|TestSidecars|CliRun|CliResult|FakeProcessRunner|FakeProject|FixtureProjects|FixtureReplay|FixtureCases?\b|TempFolder|TestProject\.cs|SidecarFiles|LogReader|Programs\.|Hosts\.|Plans\.cs|Secrets\.cs|compiled (into|a second time)|compiles (the|files)" -- '*.md' '*.csproj' '*.sh' '*.yml' ':!docs/superpowers/plans' ':!docs/superpowers/specs'`
Expected: a list including `tests/AGENTS.md`, `CONTRIBUTING.md`, `src/SqlSource/AGENTS.md:7`, `src/SqlSource.Tool/AGENTS.md:8`.  Dated plans and specs are history and are left alone.

- [ ] **Step 2: Rewrite `tests/AGENTS.md`**

- Opening line: four test projects and two libraries.
- Replace the first three bullets (compiled twice; `TestSidecars` compiled into the tool's tests; the Postgres project compiles files of the others) with:
  - one on the two libraries: what each holds, that `SqlSource.Testing` stays within Roslyn 4.8.0 and C# 12 because RoslynFloor references it, why there are two (the tool's `VersionOverride`), that their types are `internal` and reached by `InternalsVisibleTo`, and that they hold no tests;
  - one on the rule: a test project compiles no `.cs` file of another project, except RoslynFloor's compile of `tests/SqlSource.Tests/Generator/`, described in `tests/SqlSource.Tests/Generator/AGENTS.md`; `TestLayoutTests` checks it;
  - the Postgres facts of the old third bullet that are not about links (the `UsesPostgres` collection, `PostgresContainer.cs`, the tests that need no server, reading `Fixtures/Describers/` from the repository through `RepositoryPaths`, the copy of `Single`, `nightly.yml` leaving it out).  Drop the `CA1812` and `ProcessGate` sentences.
- Update the bullet "Tests mirror the source folders": the fakes and helpers shared with the Postgres project are in `tests/SqlSource.Tool.Testing`; the rest stay at the root of `tests/SqlSource.Tool.Tests`.
- Update the paths of `FakeProcessRunner.cs` and `Programs.StartTool` in the bullet "No test of the tool waits for a time".

- [ ] **Step 3: Update `CONTRIBUTING.md` and the source `AGENTS.md` files**

`CONTRIBUTING.md`: in the test projects table, the RoslynFloor row stays; the `SqlSource.Tool.Tests` row drops "compiles the harness of `tests/SqlSource.Tests/Generator/`" for "references `tests/SqlSource.Testing` for the generator's harness"; the Postgres row drops "It compiles the fixture harness … and `TestSidecars.cs`. It also compiles the command harness …" for its references to the two libraries.  Add one sentence after the table naming the two libraries and what each holds.  `src/SqlSource/AGENTS.md:7` and `src/SqlSource.Tool/AGENTS.md:8`, and any other hit of Step 1: the new paths.

- [ ] **Step 4: Run the whole check**

Run (Docker running): `./pre-commit-validation.sh`
Expected: every step passed.  Test totals: the baseline, plus three in `SqlSource.Tests`.

- [ ] **Step 5: Commit**

```bash
git add -A tests src CONTRIBUTING.md
git commit -m "Document the shared test libraries"
```
