# Database tests, piece 4b: TD-0080 items 7, 15 and 18, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the three small items of TD-0080 that are about the database projects: one builder of a realistic sample's dictionary with the reader's comparer (item 7), the image tags and the describer fixtures' folder held in one place (item 15), and the fixture build files linked once, from `tests/Fixtures.props`, with the scope check following the import (item 18).

**Architecture:** Three independent changes, each one commit.  `tests/Fixtures.props` holds the four `None` links that three projects copied, with `$(MSBuildThisFileDirectory)` paths, and `ProjectIncludes.Inputs` learns to read the files that an `Import` names under `tests/`, so the links stay in the scope check's sight.  The harness gets `DescriberFixtures`, the folder and schema of each engine's describer fixtures made when read, as `EndToEndEngine` is, and the two `RepositoryPaths` drop theirs; the two harness unit tests take their tags from `DefaultImages` or use tags that are plainly fake.  `tests/SqlSource.Tool.Testing/Realistic/RealisticSample.cs` builds a sample as `RealisticCaseReader` does, with `StringComparer.OrdinalIgnoreCase`, and PostgreSQL's `RealisticSamples.Substitute` takes the described parameters, as SQL Server's `Batch` does, so no caller builds a dictionary of types.

**Tech Stack:** .NET 10 SDK, MSBuild, xunit v3 on Microsoft.Testing.Platform, Shouldly, bash.

**Spec:** `docs/superpowers/specs/2026-10-28-database-tests-folder-design.md`, section "Piece 4: the remaining items of TD-0080", items 7, 15 and 18, with "Intent", "Goals", "Constraints" and "The layout"; and `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 7, 15 and 18.

## Where the merged code differs from the spec

- Piece 2 merged with `EngineImageTests` and `SqlServerStartTests` already side by side in `tests/SqlSource.Tests/TestHarness/`, with one `FakeStartingContainer`, whose `Image` defaults to the plainly fake `fake:1`; item 15 changes only the two tags they still hold as literals.
- `ProjectIncludes.Inputs` already resolves an `Import` as a path and judges it by its scope (`Refused_ImportOfASkippedFile_NamesIt`); what it does not do is read the imported file's items.  That is what item 18 adds.
- The sibling unit `td-0080-copies` moves the bodies of `RealisticNullabilitySafetyTests` into `tests/SqlSource.Tool.Testing/Realistic/`; it has no commit yet.  Task 3 changes the two PostgreSQL callers of `Substitute` where they are today, and the sync with `main` after that unit merges moves the change to wherever the caller went.

## Global Constraints

- No test checks anything different afterwards: the assertions of every test touched keep their values; only how a test builds its arrangement changes.
- Everything passes on both engines' default images, through `./pre-commit-validation.sh --full`, and on the floor images, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` for `tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` for `tests/DatabaseTests/SqlServer`.
- Every commit passes `./pre-commit-validation.sh` first, after `./format.sh`; no check is skipped or edited.
- The harness references `src/SqlSource` and `tests/SqlSource.Testing` alone and nothing of the tool; `TestLayoutTests` pins it.  `DescriberFixtures` therefore holds paths and nothing of `FixtureCases`.
- The libraries' types stay `internal`.
- `tests/Fixtures.props` is in scope `both` of `tools/test-scopes.txt` by matching no line, as the file's header says an unmatched path is; `./pre-commit-validation.sh --plan tests/Fixtures.props` must show both engine pairs running.
- Only items 7, 15 and 18 of TD-0080 are struck, by editing their lines in "Problem" and "Proposed fix" as item 9 was; nothing else is added to the file.
- Of the documents shared with the sibling units (`tests/README.md`, `tests/DatabaseTests/README.md`, `tests/DatabaseTests/AGENTS.md`), only the lines this change makes wrong are edited.  `tests/AGENTS.md` is at its 8 KB limit and is not touched.
- `tests/DatabaseTests/Postgres/Realistic/` and `tests/DatabaseTests/SqlServer/Realistic/` test classes that item 1 of TD-0080 lists are the sibling's; this unit edits only `RealisticSamplesTests.cs`, `Generation/RealisticParametersTests.cs` and SQL Server's `Describer/RealisticNullabilityOracleTests.cs` there, plus the two callers of `Substitute` (`Describer/RealisticNullabilitySafetyTests.cs` and `Generation/RealisticMethodTests.cs`, each by the lines that build `types` alone), and PostgreSQL's `RealisticSamples.cs`.  Nothing is added to a file of `tests/SqlSource.Tool.Testing/Realistic/` that the sibling edits: `RealisticSample.cs` is a new file.
- Commit messages end with the attribution line given in the session's instructions.

## Review Focus

- An `Import` of a file that does not exist (MSBuild allows one under a `Condition`): `Refused` must still name the import by its scope and not throw.  Pinned by `Refused_ImportOfAFileThatDoesNotExist_IsJudgedByItsPathAlone` in Task 1.
- Two files importing each other: `Inputs` must terminate.  Pinned by `Refused_ImportCycle_Terminates` in Task 1.
- An import outside `tests/` (`src/SqlSource/build/SqlSource.props`, which the two describer projects import): its items are the code under test and are not read, as a referenced project outside `tests/` is not.  Pinned by `Refused_ImportOutsideTests_IsNotRead` in Task 1.
- `global.json` reaches the three projects only through `tests/Fixtures.props` after Task 1, so a scope that skipped it would be missed without the import being followed.  Pinned by `Refused_ScopesWithGlobalJsonNone_NamesItForBothDescriberProjects` in Task 1.
- A test of `RealisticSampleChecks.Find` that is about a dictionary comparing by ordinal would prove nothing over a sample that ignores case.  `Find_Name_IsTheValueByTheNameIgnoringCase` keeps its ordinal dictionary, built inline, in Task 3.

---

### Task 1: Item 18, `tests/Fixtures.props` and the imports that `ProjectIncludes` follows

**Files:**
- Create: `tests/Fixtures.props`
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj:20-53`, `tests/DatabaseTests/Postgres/Postgres.csproj:30-62`, `tests/DatabaseTests/SqlServer/SqlServer.csproj:31-63`, `tests/SqlSource.Testing/ProjectIncludes.cs:100-131`, `tests/README.md:128`, `tools/README.md:82`
- Test: `tests/SqlSource.Tests/Validation/ProjectIncludesTests.cs`, `tests/SqlSource.Tests/Validation/ContainerProjectInputsTests.cs`

**Interfaces:**
- Produces: `tests/Fixtures.props`, a `<Project>` with one `<ItemGroup>` of four `None` items, each `Include="$(MSBuildThisFileDirectory)../src/SqlSource/build/<file>"` (or `../global.json`) with the `Link` and `CopyToOutputDirectory="PreserveNewest"` the three projects use today: `Fixtures/build/SqlSource.props`, `Fixtures/build/SqlSource.targets`, `Fixtures/build/SqlSource.AfterSdk.targets`, `Fixtures/global.json`.  `ProjectIncludes.Inputs` (private) reads, for each project, the files it reads today and every file under `tests/` that an `Import` of any of those names, resolved from the folder of the file that holds it, each judged from the project's folder, and follows the imports of those too; it keeps its signature.

- [ ] **Step 1: Write the failing tests of the followed import in `ProjectIncludesTests.cs`**

Beside `Refused_ImportOfASkippedFile_NamesIt`, with the helpers the file has (`WriteProject`, `Item`, `Import`, `Scopes`):

```csharp
// MSBuild reads the items of an imported file as the project's, from the project's folder.
[Fact]
public void Refused_ItemOfAnImportedFile_IsJudgedFromTheProject()
{
    var scopes = Scopes("none tests/Other/*");
    WriteProject("tests/Shared.props", Item("None", "../Other/File.cs"));
    WriteProject(Import("../Shared.props"));

    ProjectIncludes
        .Refused(scopes, _root.FullName)
        .ShouldBe([TestScopes.AssemblyPrefix + "App: tests/Other/File.cs is None"]);
}

// $(MSBuildThisFileDirectory) in an imported file is that file's folder, here tests/Shared, and not the project's.
[Fact]
public void Refused_ThisFileDirectoryInAnImportedFile_IsJudgedFromThatFile()
{
    var scopes = Scopes("none tests/Other/*");
    WriteProject("tests/Shared/Shared.props", Item("None", "$(MSBuildThisFileDirectory)../Other/File.cs"));
    WriteProject(Import("../Shared/Shared.props"));

    ProjectIncludes
        .Refused(scopes, _root.FullName)
        .ShouldBe([TestScopes.AssemblyPrefix + "App: tests/Other/File.cs is None"]);
}

// An imported file's own imports resolve from its folder, and are read too.
[Fact]
public void Refused_ImportOfAnImportedFile_IsRead()
{
    var scopes = Scopes("none tests/Other/*");
    WriteProject("tests/Shared/Second.props", Item("None", "../Other/File.cs"));
    WriteProject("tests/Shared/First.props", Import("Second.props"));
    WriteProject(Import("../Shared/First.props"));

    ProjectIncludes
        .Refused(scopes, _root.FullName)
        .ShouldBe([TestScopes.AssemblyPrefix + "App: tests/Other/File.cs is None"]);
}

[Fact]
public void Refused_ImportCycle_Terminates()
{
    var scopes = Scopes("none tests/Other/*");
    WriteProject("tests/Shared/First.props", Import("Second.props"));
    WriteProject("tests/Shared/Second.props", Import("First.props"), Item("None", "../Other/File.cs"));
    WriteProject(Import("../Shared/First.props"));

    ProjectIncludes
        .Refused(scopes, _root.FullName)
        .ShouldBe([TestScopes.AssemblyPrefix + "App: tests/Other/File.cs is None"]);
}

// MSBuild allows an Import of a file that is not there, under a Condition; the import is judged by its path alone.
[Fact]
public void Refused_ImportOfAFileThatDoesNotExist_IsJudgedByItsPathAlone()
{
    var scopes = Scopes("none tests/Other/*");
    WriteProject(Import("../Other/Missing.props"));

    ProjectIncludes
        .Refused(scopes, _root.FullName)
        .ShouldBe([TestScopes.AssemblyPrefix + "App: tests/Other/Missing.props is None"]);
}

// A file outside tests/ is the code under test, whose items reach the project through its scope alone, as a
// referenced project outside tests/ does.
[Fact]
public void Refused_ImportOutsideTests_IsNotRead()
{
    var scopes = Scopes("none docs/*");
    WriteProject("src/Shared.props", Item("None", "../docs/Readme.md"));
    WriteProject(Import("../../src/Shared.props"));

    ProjectIncludes.Refused(scopes, _root.FullName).ShouldBeEmpty();
}
```

`Refused_ImportOfAFileThatDoesNotExist_IsJudgedByItsPathAlone` and `Refused_ImportOutsideTests_IsNotRead` pass today and must still pass after Step 3; the other four fail today.

- [ ] **Step 2: Run them to see the four fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Validation.ProjectIncludesTests`
Expected: `Refused_ItemOfAnImportedFile_IsJudgedFromTheProject`, `Refused_ThisFileDirectoryInAnImportedFile_IsJudgedFromThatFile`, `Refused_ImportOfAnImportedFile_IsRead` and `Refused_ImportCycle_Terminates` fail, each expecting one refusal and getting none.

- [ ] **Step 3: Make `ProjectIncludes.Inputs` read the imported files**

In `tests/SqlSource.Testing/ProjectIncludes.cs`, keep the queue of projects and, for each project, replace the loop over `DirectoryBuildChain(root, folder).Prepend(current)` by a second queue of files that starts with those and grows with each file's imports: for each file dequeued that `read.Add((file, folder))` accepts, yield it, load it once, enqueue each import that resolves (`Resolve(fileFolder, fileFolder, folder, project)` for each `;`-separated `Project` of an `Import`, where `fileFolder` is the file's own folder) to a path that starts with `tests` and exists, and, when the file is the project file, enqueue its references as today.  A private helper `Imports(string fileFolder, string projectFolder, XDocument file)` returning `IEnumerable<string>` keeps `Outside` as it is, since an import inside the project's folder is followed too.  Update the comment on `Inputs` and the one on `Refused` ("The files read are ...") to say that every file under `tests/` that an `Import` of any of them names is read too, judged from the project's folder, and that one that does not exist is judged by its path alone.  Keep `Refused`'s reporting of the import's own path, which `Outside` still gives.

- [ ] **Step 4: Run the class to see all pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Validation.ProjectIncludesTests`
Expected: every test of the class passes.

- [ ] **Step 5: Write the failing repository test in `ContainerProjectInputsTests.cs`**

```csharp
// global.json reaches the describer projects only through tests/Fixtures.props, so this holds only while the check
// reads the files that an Import names.
[Fact]
public void Refused_ScopesWithGlobalJsonNone_NamesItForBothDescriberProjects()
{
    var text = File.ReadAllText(RepositoryRoot.Path("tools", "test-scopes.txt"));
    var scopes = TestScopes.Parse("none global.json\n" + text);

    var refused = ProjectIncludes.Refused(scopes, RepositoryRoot.Path());

    refused.ShouldContain("SqlSource.DatabaseTests.Postgres: global.json is None");
    refused.ShouldContain("SqlSource.DatabaseTests.SqlServer: global.json is None");
}
```

It passes today, since each `.csproj` still links `global.json` itself, and it is the test that fails if Step 6 moves the link and Step 3 were undone.  Note that `Refused_ScopesWithoutTheFixtureProjectLines_NamesTheLinkedFixtures` keeps failing without the fixture-project lines after Step 6, since the `Fixtures/Projects/Single/**` and `SqlServer/**` links stay in the project files, where they differ.

- [ ] **Step 6: Write `tests/Fixtures.props` and import it from the three projects**

The file, four-space indented as the project files are, with a comment that says: the package's three MSBuild files and the repository's `global.json`, linked into `Fixtures/` of the output beside the fixture projects, where `FixtureProjects` of `tests/SqlSource.Tool.Testing` copies them from; imported by `tests/SqlSource.Tool.Tests`, `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer`, which sit at different depths, so each path starts at this file's folder; it matches no line of `tools/test-scopes.txt`, so a change to it runs both engines' projects, and `ProjectIncludes` reads its items for each project that imports it.

```xml
<Project>
    <ItemGroup>
        <None
            Include="$(MSBuildThisFileDirectory)../src/SqlSource/build/SqlSource.props"
            Link="Fixtures/build/SqlSource.props"
            CopyToOutputDirectory="PreserveNewest"
        />
        <!-- SqlSource.targets and SqlSource.AfterSdk.targets likewise, then: -->
        <None
            Include="$(MSBuildThisFileDirectory)../global.json"
            Link="Fixtures/global.json"
            CopyToOutputDirectory="PreserveNewest"
        />
    </ItemGroup>
</Project>
```

In `SqlSource.Tool.Tests.csproj`, delete the four `None` items of lines 30 to 45 and add `<Import Project="../Fixtures.props" />` as a top-level element after that `ItemGroup`; shorten the comment of lines 21 to 26 so that it says the fixture projects go to the output, and that the package's MSBuild files and `global.json` come beside them from `../Fixtures.props`.  In `Postgres.csproj` and `SqlServer.csproj`, delete the four `None` items (lines 46 to 61 of the first, 47 to 62 of the second) and add `<Import Project="../../Fixtures.props" />` after that `ItemGroup`, before the `ProjectReference` group; change each comment of lines 32 to 36 the same way.

- [ ] **Step 7: Build, and check the links reach the output**

Run: `dotnet build SqlSource.slnx`
Expected: builds with no warning.  Then `ls tests/SqlSource.Tool.Tests/bin/Debug/net10.0/Fixtures/build tests/DatabaseTests/Postgres/bin/Debug/net10.0/Fixtures/build tests/DatabaseTests/SqlServer/bin/Debug/net10.0/Fixtures/build` lists `SqlSource.AfterSdk.targets`, `SqlSource.props` and `SqlSource.targets` in each, and `Fixtures/global.json` is beside each `build`.  `git status` shows no changed `packages.lock.json`.

- [ ] **Step 8: Run the validation tests and the plan of the script**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Validation`
Expected: all pass, `Refused_RepositoryScopes_IsEmpty` and `Refused_ScopesWithGlobalJsonNone_NamesItForBothDescriberProjects` among them.
Run: `./pre-commit-validation.sh --plan tests/Fixtures.props`
Expected: the plan says both the PostgreSQL and the SQL Server projects run for that path.

- [ ] **Step 9: Update the two documents that say what the check reads**

`tests/README.md:128` and `tools/README.md:82` each list what the check reads: add, after the projects under `tests/` that it references, "and every file under `tests/` that an import of any of those names, `tests/Fixtures.props` among them".  In `tests/README.md`'s "Fixture projects" section, the paragraph that starts "A fixture project is no part of the solution" says the file is copied to the output as it is; add one sentence after it: the package's three MSBuild files and `global.json` come beside the copied fixtures from `tests/Fixtures.props`, which the tool's tests and the two describer projects import.

- [ ] **Step 10: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: passes.  A `.props` change makes the format cover everything and runs both engine pairs.

```bash
git add tests/Fixtures.props tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj tests/DatabaseTests/Postgres/Postgres.csproj tests/DatabaseTests/SqlServer/SqlServer.csproj tests/SqlSource.Testing/ProjectIncludes.cs tests/SqlSource.Tests/Validation/ProjectIncludesTests.cs tests/SqlSource.Tests/Validation/ContainerProjectInputsTests.cs tests/README.md tools/README.md
git commit -m "Link the fixture build files once, from tests/Fixtures.props, and read imported files in the scope check"
```

---

### Task 2: Item 15, the image tags and one `DescriberFixtures` in the harness

**Files:**
- Create: `tests/DatabaseTests/TestHarness/DescriberFixtures.cs`
- Modify: `tests/SqlSource.Tests/TestHarness/EngineImageTests.cs:45-49`, `tests/SqlSource.Tests/TestHarness/SqlServerStartTests.cs:17`, `tests/DatabaseTests/Postgres/RepositoryPaths.cs`, `tests/DatabaseTests/SqlServer/RepositoryPaths.cs`, `tests/DatabaseTests/Postgres/PostgresDatabases.cs:37`, `tests/DatabaseTests/SqlServer/SqlServerDatabases.cs:31`, `tests/DatabaseTests/Postgres/Describer/FixtureCasesTests.cs:18`, `tests/DatabaseTests/Postgres/Describer/FixtureVersionTests.cs:17`, `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs:45`, `tests/DatabaseTests/SqlServer/Describer/FixtureCasesTests.cs:18`, `tests/DatabaseTests/SqlServer/Describer/FixtureVersionTests.cs:17`, `tests/README.md:26,114,116`, `tests/DatabaseTests/README.md:18`, `tests/DatabaseTests/SqlServer/README.md:7`, `tests/DatabaseTests/Postgres/AGENTS.md`, `tests/DatabaseTests/SqlServer/AGENTS.md`
- Test: `tests/SqlSource.Tests/TestHarness/DescriberFixturesTests.cs` (new)

**Interfaces:**
- Produces: in the harness, `internal sealed record DescriberFixtures(string Folder)` with `public static DescriberFixtures Postgres` and `SqlServer`, each made when read, as `EndToEndEngine`'s are, over `RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", "postgres")` and `"mssql"`, and `public string Schema => Path.Combine(Folder, "schema.sql")`.  The projects' `RepositoryPaths` lose `DescriberFixtures` and `PostgresSchema` / `SqlServerSchema` and keep the rest.

- [ ] **Step 1: Write the failing test `DescriberFixturesTests.cs` in `tests/SqlSource.Tests/TestHarness/`**

`tests/SqlSource.Tests` is scope `none`, so it may read either engine's folder.

```csharp
// The folder and schema of each engine's describer fixtures, read from the repository.
public sealed class DescriberFixturesTests
{
    [Theory]
    [InlineData("postgres")]
    [InlineData("mssql")]
    public void Folder_OfEachEngine_IsTheEnginesFolderUnderDescribers(string engine)
    {
        var fixtures = engine == "postgres" ? DescriberFixtures.Postgres : DescriberFixtures.SqlServer;

        fixtures.Folder.ShouldBe(RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", engine));
        fixtures.Schema.ShouldBe(Path.Combine(fixtures.Folder, "schema.sql"));
        File.Exists(fixtures.Schema).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.TestHarness.DescriberFixturesTests`
Expected: does not compile, `DescriberFixtures` not found.

- [ ] **Step 3: Write `DescriberFixtures.cs` in the harness**

The comment: the folder of an engine's describer fixtures, `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/`, its `schema.sql` and its cases, which the engine's describer project reads and records in the repository itself and not in an output folder, since recording writes there; each is made when it is read, so that a project of one engine never reads the other's folder, which `RepositoryRoot` would refuse.  The record as the Interfaces block gives it, with a private `Of(string engine)`.

- [ ] **Step 4: Run it to see it pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.TestHarness.DescriberFixturesTests`
Expected: both rows pass.

- [ ] **Step 5: Move the two projects onto it**

Delete `DescriberFixtures` and `PostgresSchema` from `tests/DatabaseTests/Postgres/RepositoryPaths.cs`, and `DescriberFixtures` and `SqlServerSchema` from the SQL Server one, with the comment on the fixtures' folder; `using System.IO` goes where nothing uses it.  Replace `RepositoryPaths.DescriberFixtures` with `DescriberFixtures.Postgres.Folder` in the three PostgreSQL files and `DescriberFixtures.SqlServer.Folder` in the two SQL Server files, and `RepositoryPaths.PostgresSchema` / `SqlServerSchema` in the two `*Databases.cs` with `DescriberFixtures.Postgres.Schema` / `.SqlServer.Schema`, adding `using SqlSource.DatabaseTests.TestHarness;` where the file lacks it.

- [ ] **Step 6: Take the literal tags out of the two harness tests**

In `EngineImageTests.TagOf_PinnedImage_IsWhatFollowsTheLastColon`, the rows become plainly fake tags of each engine's shape: `("postgres:99.99", "99.99")` and `("mcr.microsoft.com/mssql/server:2099-CU99-GDR99-ubuntu-99.04", "2099-CU99-GDR99-ubuntu-99.04")`.  In `SqlServerStartTests`, `ImageName` becomes `DefaultImages.SqlServer`.

- [ ] **Step 7: Build and run what needs no server, then both describer projects on their default images**

Run: `dotnet build SqlSource.slnx` then `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.TestHarness`
Expected: builds; all pass.
Run: `dotnet test --project tests/DatabaseTests/Postgres --filter-namespace SqlSource.DatabaseTests.Postgres.Describer` and `dotnet test --project tests/DatabaseTests/SqlServer --filter-namespace SqlSource.DatabaseTests.SqlServer.Describer`
Expected: all pass (the fixture cases read their folders through the harness now).

- [ ] **Step 8: Update the documents that name the fixtures' path or the harness's contents**

- `tests/README.md:26`, the harness row: after `EngineImage`'s entry, add `DescriberFixtures`, the folder and the `schema.sql` of each engine's describer fixtures, which its describer project reads and records in the repository.
- `tests/README.md:114` and `:116`: add `DescriberFixtures` to what `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer` reference the harness for.
- `tests/DatabaseTests/README.md:18`: "It holds the images, the start of a container, the folder of each engine's describer fixtures, the files of the end-to-end databases and the two driver-free containers".
- `tests/DatabaseTests/SqlServer/README.md:7`: "It reads those files from the repository through `RepositoryPaths` and the harness's `DescriberFixtures`, found through `RepositoryRoot`".
- `tests/DatabaseTests/Postgres/AGENTS.md`, the rule on reading `Fixtures/Describers/postgres/`: through `DescriberFixtures.Postgres` of the harness, found through `RepositoryRoot.Path`.
- `tests/DatabaseTests/SqlServer/AGENTS.md`, the rule "The project reads the files of its databases from the repository through `RepositoryPaths`": add "and `DescriberFixtures` of the harness".

- [ ] **Step 9: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/DatabaseTests/TestHarness/DescriberFixtures.cs tests/SqlSource.Tests/TestHarness tests/DatabaseTests/Postgres tests/DatabaseTests/SqlServer tests/README.md tests/DatabaseTests/README.md
git commit -m "Hold the describer fixtures' folder once in the harness and take the image tags from DefaultImages"
```

---

### Task 3: Item 7, `RealisticSample.Of` and `Substitute(sql, sample, parameters)`

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Realistic/RealisticSample.cs`
- Modify: `tests/DatabaseTests/Postgres/Realistic/RealisticSamples.cs:16-49`, `tests/DatabaseTests/Postgres/Realistic/Describer/RealisticNullabilitySafetyTests.cs:46-56`, `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticMethodTests.cs:128-134`, `tests/README.md:42`
- Test: `tests/SqlSource.Tool.Tests/Realistic/RealisticSampleChecksTests.cs`, `tests/DatabaseTests/Postgres/Realistic/RealisticSamplesTests.cs`, `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticParametersTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/RealisticSamplesTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticParametersTests.cs`, `tests/DatabaseTests/SqlServer/Realistic/Describer/RealisticNullabilityOracleTests.cs`

**Interfaces:**
- Produces: `internal static class RealisticSample` in namespace `SqlSource.Tool.Testing.Realistic` with `public static Dictionary<string, string> Of(params (string Name, string Value)[] values)`, a dictionary over `StringComparer.OrdinalIgnoreCase`, as `RealisticCaseReader.SamplesOf` builds one.  PostgreSQL's `RealisticSamples.Substitute(string sql, IReadOnlyDictionary<string, string> sample, IReadOnlyList<DescribedParameter> parameters)` returns the same text as today; the type of `@name` is `Type.Name` of the parameter of that name, compared ignoring case, and a parameter not listed or without a `Type` throws `InvalidOperationException` with the message `No type is given for @{name}.`, as today.

- [ ] **Step 1: Write `RealisticSample.cs`**

The comment: a sample of a case built by a test, from its names and values, compared as `RealisticCaseReader` compares a real sample's, ignoring case; the one builder, so that no test builds one with another comparer.  The class as the Interfaces block gives it.

- [ ] **Step 2: Replace the six builders**

In each of the six test files, delete the private `Values` or `Sample` method and call `RealisticSample.Of(...)` where it was called, with `using SqlSource.Tool.Testing.Realistic;` where the file lacks it.  Two exceptions, each one line:
- `RealisticSampleChecksTests.Find_Name_IsTheValueByTheNameIgnoringCase` is about a dictionary that compares by ordinal, as its comment says, and keeps one, inline: `new Dictionary<string, string>(StringComparer.Ordinal) { ["Status"] = "'open'" }`.
- `RealisticSampleChecksTests.Case` takes an `IReadOnlyDictionary<string, string>` and puts it in `Samples` as it is, since `Of` already compares ignoring case.

Every assertion and every row of the six files stays as it is.

- [ ] **Step 3: Change `Substitute` to take the parameters, and its tests**

In `tests/DatabaseTests/Postgres/Realistic/RealisticSamples.cs`, the third parameter becomes `IReadOnlyList<DescribedParameter> parameters` (`using SqlSource.Tool.Describing.Describers;`), and the type lookup finds the parameter by name, ignoring case, and takes its `Type.Name`; the comment on the method says the type is the one described for the parameter.  In `RealisticSamplesTests.cs` of that project, each `Values(("a", "text"), ...)` of types becomes a list of `Described("a", "text")`, with `private static DescribedParameter Described(string name, string type) => new(name, new OtherEngineType(type), null);` (`using SqlSource.Snapshot;`), as the project's `RealisticParametersTests` already types its parameters with `OtherEngineType`; the expected texts stay as they are.

- [ ] **Step 4: Pass the parameters through in the two callers**

In `RealisticNullabilitySafetyTests.cs:46-48` and `RealisticMethodTests.cs:128-130` of `tests/DatabaseTests/Postgres/Realistic/`, delete the `types` dictionary and pass `parameters` to `Substitute`; remove `using System.Linq;` only where nothing else uses it.  Touch no other line of either file: the sibling unit moves the first file's body.

- [ ] **Step 5: Run the tests that need no server, then the realistic tests of both engines**

Run: `dotnet build SqlSource.slnx`, then `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.Realistic.RealisticSampleChecksTests`
Expected: all pass.
Run: `dotnet test --project tests/DatabaseTests/Postgres --filter-namespace SqlSource.DatabaseTests.Postgres.Realistic` and `dotnet test --project tests/DatabaseTests/SqlServer --filter-namespace SqlSource.DatabaseTests.SqlServer.Realistic`
Expected: all pass on the default images.

- [ ] **Step 6: Update `tests/README.md:42`**

After `RealisticSampleChecks`, add `RealisticSample`, which builds a sample as the reader builds one, for the tests of both engines.

- [ ] **Step 7: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/SqlSource.Tool.Testing/Realistic/RealisticSample.cs tests/SqlSource.Tool.Tests/Realistic/RealisticSampleChecksTests.cs tests/DatabaseTests/Postgres/Realistic tests/DatabaseTests/SqlServer/Realistic tests/README.md
git commit -m "Build every realistic sample through RealisticSample.Of and give Substitute the described parameters"
```

---

### Task 4: Strike the items, full validation, floor images, review

**Files:**
- Modify: `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md:15,21,24,45,53,56`

- [ ] **Step 1: Strike items 7, 15 and 18**

In "Problem", wrap each of the three items' text in `~~`, as item 9 is, and add one sentence after each: 7, "Fixed by piece 4 of the database tests: `RealisticSample.Of` in `SqlSource.Tool.Testing/Realistic/`, and PostgreSQL's `Substitute` takes the described parameters."; 15, "Fixed by piece 4 of the database tests: the two tests use `DefaultImages` or a plainly fake tag, and `DescriberFixtures` of the harness holds the folder."; 18, "Fixed by piece 4 of the database tests: `tests/Fixtures.props`, which the three import."  In "Proposed fix", wrap lines 7, 15 and 18 in `~~` and append "Done." to each, as line 9 has.  Nothing else changes.

- [ ] **Step 2: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md
git commit -m "Strike items 7, 15 and 18 of TD-0080"
```

- [ ] **Step 3: The full validation and the floor images**

Run: `./pre-commit-validation.sh --full`
Expected: every check passes.
Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`
Expected: both pass.

- [ ] **Step 4: The version check**

Run: `git fetch --tags origin` and `git tag --list 'v*' --sort=-v:refname | head -n 1`
Expected: no tag, so `VersionPrefix` 0.1.0 needs no change.

- [ ] **Step 5: Branch review**

Run `/branch-review:review --base origin/main` and address its findings until it is clean.  Then ask the implementation manager for permission to push, as the session's instructions say.
