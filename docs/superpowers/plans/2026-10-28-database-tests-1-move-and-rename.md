# Database tests, piece 1: move and rename, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the four container test projects into `tests/DatabaseTests/` under their short names, with `SqlSource.DatabaseTests.*` assemblies and namespaces, and give the folder its readme and `AGENTS.md`, with every path, name, check, script, workflow and document that keyed on the old names changed to match.

**Architecture:** A `git mv` of each project, a `Directory.Build.props` in the new folder that derives `AssemblyName` and `RootNamespace` from the project file's name, and `TestScopes` applying the same prefix so `RepositoryRoot`'s scope check keeps finding the running project.  No code changes meaning; this piece is names and paths, and the documents that hold them.

**Tech Stack:** .NET 10 SDK, MSBuild, xunit v3 on Microsoft.Testing.Platform, bash, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-28-database-tests-folder-design.md`, sections "The layout", "The naming pattern" and "Piece 1: move and rename".

## Global Constraints

- Folder and project file carry the short name; assembly and root namespace carry the prefix: `tests/DatabaseTests/Postgres/Postgres.csproj` builds `SqlSource.DatabaseTests.Postgres`.  The four: `Postgres`, `Postgres.Npgsql8`, `SqlServer`, `SqlServer.SqlClient61`.
- `tests/DatabaseTests/Directory.Build.props` imports the root one by the literal path `../../Directory.Build.props`, never `GetPathOfFileAbove`, since `ProjectIncludes.Resolve` returns null for a path that holds a property it does not know and `Refused` then reports it.
- The tree is green after this piece: `./pre-commit-validation.sh --full` passes, and `tests/DatabaseTests/Postgres` passes on `postgres:14.24` and `tests/DatabaseTests/SqlServer` on `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- Every commit passes `./pre-commit-validation.sh` first; no check is skipped or edited.
- The margins, counts and constants of `AllocationTests` are untouched; the classes move with their projects and keep running where they run today.
- Plans and specs under `docs/superpowers/` are history and are not rewritten; every other document that names an old path or name is.
- An `AGENTS.md` has one rule per bullet of at most 80 words and is at most 8 KB; `tests/SqlSource.Tests/Documentation/AgentsFileTests.cs` checks both.
- Commit messages end with the attribution line given in the session's instructions.

## Review Focus

- A file under `tests/DatabaseTests/` that no line of `tools/test-scopes.txt` matches falls to scope `both` and runs every container project: wanted for `Directory.Build.props`, `README.md` is `none` by `*.md`.  Pinned by the new rows of `Of_RealPath_IsDecidedAsIntended` in Task 2.
- An assembly that `RunningProject` reports under a name `TestScopes.Projects` lacks reads every file unchecked: pinned by `RunningProjectTests` in each project, which the moved projects keep, and by `RepositoryFile_Projects_AreTheFourContainerProjects` in Task 2.
- `RestoreLockedMode` in CI fails a restore whose lock file differs: pinned by Task 1's check that `git status` shows no changed `packages.lock.json` after the build.
- A committed sidecar made stale by a marker's namespace change would fail `CheckTests` of the end-to-end projects, which the full validation runs; the contingency is in Task 1.
- A relative link in a moved markdown file that now points one folder too high: pinned by the link check of Task 4.

---

### Task 1: Move the projects and make the solution build

**Files:**
- Move: `tests/SqlSource.Tests.Postgres/` to `tests/DatabaseTests/Postgres/`, `tests/SqlSource.Tests.Postgres.Npgsql8/` to `tests/DatabaseTests/Postgres.Npgsql8/`, `tests/SqlSource.Tests.SqlServer/` to `tests/DatabaseTests/SqlServer/`, `tests/SqlSource.Tests.SqlServer.SqlClient61/` to `tests/DatabaseTests/SqlServer.SqlClient61/`, each `.csproj` renamed to the folder's name.
- Create: `tests/DatabaseTests/Directory.Build.props`
- Modify: `SqlSource.slnx`, `src/SqlSource/SqlSource.csproj:24-25`, `src/SqlSource.Tool/SqlSource.Tool.csproj:33-34`, `tests/SqlSource.Testing/SqlSource.Testing.csproj` (the four `InternalsVisibleTo`), `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj:3-4,24-25`, `.editorconfig:240`, `.gitattributes:22,25`, `Directory.Packages.props:30,35,61,63`, every `.cs` and `.sql` file of the four projects, `tests/SqlSource.Tool.Tests/Fixtures/Projects/SqlServer/App.csproj:4`
- Modify: `tests/SqlSource.Testing/TestScopes.cs:158`, `tests/SqlSource.Tool.Testing/Realistic/RealisticCorpus.cs:22-37`, `tests/DatabaseTests/Postgres/RepositoryPaths.cs`, `tests/DatabaseTests/SqlServer/RepositoryPaths.cs`, `tools/test-scopes.txt`
- Test: `tests/SqlSource.Tests/Validation/TestScopesTests.cs`, `tests/SqlSource.Tests/Validation/TestScopesFileTests.cs`, `tests/SqlSource.Tests/Validation/ContainerProjectInputsTests.cs`, `tests/SqlSource.Tests/TestLayoutTests.cs`, `tests/SqlSource.Tool.Tests/Realistic/RealisticCorpusTests.cs`

**Interfaces:**
- Produces: `TestScopes.AssemblyPrefix`, a `public const string` of `"SqlSource.DatabaseTests."`, and `TestScopes.Projects` keyed by prefix plus the project file's name without its extension.  `RealisticCorpus.Project` stays the short folder name (`"Postgres"`, `"SqlServer"`) and `RealisticCorpus.Folder` resolves `RepositoryRoot.Path("tests", "DatabaseTests", Project, "Realistic")`.

This task is one commit: the solution does not build, and the scope checks do not pass, between the move and the last rename, so nothing in it can be committed alone.

- [ ] **Step 1: Write the failing test of the prefix in `TestScopesTests.cs`**

Beside the existing test that reads `scopes.Projects["X"]`, add:

```csharp
[Fact]
public void Parse_ProjectLine_IsKeyedByThePrefixedProjectName()
{
    var scopes = TestScopes.Parse("project postgres tests/DatabaseTests/Postgres/Postgres.csproj\n");

    scopes.Projects.Keys.ShouldBe(["SqlSource.DatabaseTests.Postgres"]);
    scopes.ProjectFiles.ShouldBe(["tests/DatabaseTests/Postgres/Postgres.csproj"]);
}
```

Change the existing assertions on `scopes.Projects["X"]` and `["Y"]` to `[TestScopes.AssemblyPrefix + "X"]` and `[TestScopes.AssemblyPrefix + "Y"]`.

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Validation.TestScopesTests`
Expected: FAIL, `AssemblyPrefix` does not exist.

- [ ] **Step 3: Add the prefix to `TestScopes`**

In `tests/SqlSource.Testing/TestScopes.cs`, add `public const string AssemblyPrefix = "SqlSource.DatabaseTests.";` with a comment that `tests/DatabaseTests/Directory.Build.props` gives every project of that folder its assembly name from its project file's name under this prefix, and that `RunningProjectTests` of each project fails when the two disagree.  In `AddProject`, key by `AssemblyPrefix + Path.GetFileNameWithoutExtension(words[2])`.  Update the comment on `Projects`.

- [ ] **Step 4: Move the four projects and rename their project files**

```bash
mkdir -p tests/DatabaseTests
git mv tests/SqlSource.Tests.Postgres tests/DatabaseTests/Postgres
git mv tests/DatabaseTests/Postgres/SqlSource.Tests.Postgres.csproj tests/DatabaseTests/Postgres/Postgres.csproj
git mv tests/SqlSource.Tests.Postgres.Npgsql8 tests/DatabaseTests/Postgres.Npgsql8
git mv tests/DatabaseTests/Postgres.Npgsql8/SqlSource.Tests.Postgres.Npgsql8.csproj tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj
git mv tests/SqlSource.Tests.SqlServer tests/DatabaseTests/SqlServer
git mv tests/DatabaseTests/SqlServer/SqlSource.Tests.SqlServer.csproj tests/DatabaseTests/SqlServer/SqlServer.csproj
git mv tests/SqlSource.Tests.SqlServer.SqlClient61 tests/DatabaseTests/SqlServer.SqlClient61
git mv tests/DatabaseTests/SqlServer.SqlClient61/SqlSource.Tests.SqlServer.SqlClient61.csproj tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj
```

Delete the `bin/` and `obj/` folders left in the old locations, if any remain.

- [ ] **Step 5: Write `tests/DatabaseTests/Directory.Build.props`**

```xml
<Project>
    <!--
        Every project of this folder: the folder and the project file carry the short name, Postgres.csproj, and the
        assembly and the root namespace carry the prefix, SqlSource.DatabaseTests.Postgres.  TestScopes of
        tests/SqlSource.Testing derives the same name from a project line of tools/test-scopes.txt, so the two agree
        by construction; tests/DatabaseTests/AGENTS.md states the pattern.  The import is a literal path, since
        ProjectIncludes cannot resolve a property it does not know.
    -->
    <Import Project="../../Directory.Build.props" />
    <PropertyGroup>
        <AssemblyName>SqlSource.DatabaseTests.$(MSBuildProjectName)</AssemblyName>
        <RootNamespace>SqlSource.DatabaseTests.$(MSBuildProjectName)</RootNamespace>
    </PropertyGroup>
</Project>
```

- [ ] **Step 6: Fix the relative paths in the four project files**

Each project is one folder deeper.  In all four: `../../src/` becomes `../../../src/`, `../SqlSource.Testing/` becomes `../../SqlSource.Testing/`, `../SqlSource.Tool.Testing/` becomes `../../SqlSource.Tool.Testing/`, `../SqlSource.Tool.Tests/` becomes `../../SqlSource.Tool.Tests/`, `../../global.json` becomes `../../../global.json`.  In the two driver-floor projects the linked queries change from `../SqlSource.Tests.Postgres/EndToEnd/Queries/` to `../Postgres/EndToEnd/Queries/` and from `../SqlSource.Tests.SqlServer/EndToEnd/Queries/` to `../SqlServer/EndToEnd/Queries/`.  Their header comments that say "the end-to-end project" need no change.

- [ ] **Step 7: Rename the namespaces, in code and in the query markers**

Across every `.cs` and `.sql` file under `tests/DatabaseTests/`, replace `SqlSource.Tests.Postgres` with `SqlSource.DatabaseTests.Postgres` and `SqlSource.Tests.SqlServer` with `SqlSource.DatabaseTests.SqlServer`; both cover the `.Npgsql8` and `.SqlClient61` namespaces.  The `.sql` markers this reaches: `type-override` in `Notes.sql` of both engines, `Methods.sql` and `ExternalRefs.sql` of SQL Server, `input-model` in both `Shapes.sql`, and `model-namespace` in both `Billing/Invoices.sql`.  Then replace every path `tests/SqlSource.Tests.Postgres` with `tests/DatabaseTests/Postgres` and `tests/SqlSource.Tests.SqlServer` with `tests/DatabaseTests/SqlServer` in the same files and in the comments of `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tool.Testing` (the files that `grep -rl` finds, `RealisticCorpusTests.cs` and `Fixtures/Projects/SqlServer/App.csproj` among them).  Then:

```bash
grep -rn "SqlSource\.Tests\.\(Postgres\|SqlServer\)" --include='*.cs' --include='*.sql' --include='*.csproj' --include='*.json' --exclude-dir=bin --exclude-dir=obj tests src
```

Expected: no line.

- [ ] **Step 8: Update the solution, the `InternalsVisibleTo` items and the three dot-files**

`SqlSource.slnx`: the four projects move from the `/tests/` folder into a new `<Folder Name="/tests/DatabaseTests/">` with their new paths, sorted.  `InternalsVisibleTo`: `SqlSource.Tests.Postgres` becomes `SqlSource.DatabaseTests.Postgres` and the same for `SqlServer`, `Postgres.Npgsql8` and `SqlServer.SqlClient61`, in `src/SqlSource/SqlSource.csproj`, `src/SqlSource.Tool/SqlSource.Tool.csproj`, `tests/SqlSource.Testing/SqlSource.Testing.csproj` and `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj`, whose header comment also names the two paths.  `.editorconfig`: the section `[tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs]` becomes `[tests/DatabaseTests/SqlServer/SqlServerContainer.cs]`.  `.gitattributes`: the two `Realistic/Cases/**` lines take the new paths.  `Directory.Packages.props`: the comments at lines 30, 35, 61 and 63 take the new paths.

- [ ] **Step 9: Update `RepositoryPaths`, `RealisticCorpus` and `tools/test-scopes.txt`**

`EndToEndProject` of each `RepositoryPaths` becomes `RepositoryRoot.Path("tests", "DatabaseTests", "Postgres", "Postgres.csproj")`, likewise for SQL Server.  In `RealisticCorpus`, the two instances take `"Postgres"` and `"SqlServer"`, `Folder` becomes `RepositoryRoot.Path("tests", "DatabaseTests", Project, "Realistic")`, `ProjectSettingsFile` likewise, and the comment on `Project` says the folder of the engine's project under `tests/DatabaseTests/`.  In `tools/test-scopes.txt`, the four `project` lines and the four folder lines become:

```
project postgres tests/DatabaseTests/Postgres/Postgres.csproj
project postgres tests/DatabaseTests/Postgres.Npgsql8/Postgres.Npgsql8.csproj
project sqlserver tests/DatabaseTests/SqlServer/SqlServer.csproj
project sqlserver tests/DatabaseTests/SqlServer.SqlClient61/SqlServer.SqlClient61.csproj

postgres tests/DatabaseTests/Postgres/*
postgres tests/DatabaseTests/Postgres.Npgsql8/*
...
sqlserver tests/DatabaseTests/SqlServer/*
sqlserver tests/DatabaseTests/SqlServer.SqlClient61/*
```

The lines for `src/SqlSource.Tool/Describing/` and the fixtures stay.  `tests/DatabaseTests/Postgres/*` does not match `tests/DatabaseTests/Postgres.Npgsql8/...`, since the `/` after the name is literal.

- [ ] **Step 10: Update the tests that name the projects**

`TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects`: the four keys become `SqlSource.DatabaseTests.Postgres`, `SqlSource.DatabaseTests.Postgres.Npgsql8`, `SqlSource.DatabaseTests.SqlServer` and `SqlSource.DatabaseTests.SqlServer.SqlClient61`.  `Of_RealPath_IsDecidedAsIntended` gains four rows:

```csharp
[InlineData("tests/DatabaseTests/Postgres/Postgres.csproj", nameof(TestScope.Postgres))]
[InlineData("tests/DatabaseTests/Postgres.Npgsql8/AppContainer.cs", nameof(TestScope.Postgres))]
[InlineData("tests/DatabaseTests/Directory.Build.props", nameof(TestScope.Both))]
[InlineData("tests/DatabaseTests/README.md", nameof(TestScope.None))]
```

`ContainerProjectInputsTests.Refused_ScopesWithoutTheFixtureProjectLines_NamesTheLinkedFixtures`: the two prefixes become `SqlSource.DatabaseTests.Postgres: ` and `SqlSource.DatabaseTests.SqlServer: ` (read `ProjectIncludes.Refused` to confirm the line's first word is the scopes key).  `TestLayoutTests.TestProjects_CompileIncludes_ReachNoFileOfAnotherProject` enumerates the `.csproj` files of the folders directly under `tests/` and directly under `tests/DatabaseTests/`, with a comment that the database projects sit one level down, and its expectation is unchanged.  `RealisticCorpusTests`: the six expected paths take `tests/DatabaseTests/Postgres/...` and `tests/DatabaseTests/SqlServer/...`.

- [ ] **Step 11: Build and check the lock files**

Run: `dotnet build SqlSource.slnx`
Expected: succeeds with no warning.  Then `git status --short -- '*.lock.json'` shows nothing: a project reference is recorded by name, not path, so no lock file changes.  If one does, read the diff before accepting it.

- [ ] **Step 12: Run the tests that need no container**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: both pass, including `TestScopesTests`, `TestScopesFileTests`, `ContainerProjectInputsTests`, `TestLayoutTests`, `RepositoryRootTests` and `RealisticCorpusTests`.

- [ ] **Step 13: Run the four moved projects**

Run: `dotnet test --project tests/DatabaseTests/Postgres`, then `Postgres.Npgsql8`, `SqlServer`, `SqlServer.SqlClient61`.
Expected: each passes, `RunningProjectTests` and both `CheckTests` among them.  If a `CheckTests` fails because a sidecar differs after a marker's namespace changed, run `tools/describe-end-to-end.sh` after Task 2 has updated it, commit the rewritten sidecars with this task, and say so in the pull request; the spec expects no change, since the hash covers no comment.

- [ ] **Step 14: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes; it runs both engine pairs, since their folders changed.

```bash
git add -A tests src SqlSource.slnx .editorconfig .gitattributes Directory.Packages.props tools/test-scopes.txt
git commit -m "Move the container test projects into tests/DatabaseTests under their short names"
```

### Task 2: The script, the workflows and the tools

**Files:**
- Modify: `pre-commit-validation.sh:3-4,105-137`, `.github/workflows/build.yml:147-168,190-260`, `.github/workflows/nightly.yml:64-69,100-115,135-160`, `tools/describe-end-to-end.sh:7-13,26,35,123,193`, `tools/record-fixtures.sh:23-32`

**Interfaces:**
- Consumes: the paths and assembly names of Task 1.

- [ ] **Step 1: `pre-commit-validation.sh`**

In `needs_allocation`, the pattern `tests/SqlSource.Tests.*/EndToEnd/*` becomes `tests/DatabaseTests/*/EndToEnd/*`, and the condition that treats a `.props` or `.targets` file of the root or of `tests` as an allocation input also takes `tests/DatabaseTests`, with its comment saying that MSBuild imports that folder's `Directory.Build.props` into every database project.  The header comment's "a pair of container test projects" needs no change.  Run `./pre-commit-validation.sh --plan tests/DatabaseTests/Directory.Build.props` and see `allocation tests` marked `run`; run it with `tests/DatabaseTests/README.md` and see both pairs and the allocation tests skipped.

- [ ] **Step 2: `build.yml`**

The `Test` step's two `--filter-not-class` values become `SqlSource.DatabaseTests.Postgres.EndToEnd.AllocationTests` and `SqlSource.DatabaseTests.SqlServer.EndToEnd.AllocationTests`.  The `Test allocations` step's loop uses `--project "tests/DatabaseTests/$engine"` and `--filter-class "SqlSource.DatabaseTests.$engine.EndToEnd.AllocationTests"`.  `test-postgres-floor`: `PROJECT: tests/DatabaseTests/Postgres`, `cache-dependency-path: tests/DatabaseTests/Postgres/packages.lock.json`; `test-sqlserver-floor` likewise with `SqlServer`; the two jobs' comments and the readme paths they cite follow.

- [ ] **Step 3: `nightly.yml`**

The comment of the `test` job's Test step names the four new paths.  `test-postgres`: `PROJECT: tests/DatabaseTests/Postgres` and its cache path; `test-sqlserver` likewise; the comments above both jobs cite the new readme paths.

- [ ] **Step 4: The two tool scripts**

`describe-end-to-end.sh`: `POSTGRES_PROJECT='tests/DatabaseTests/Postgres'`, `SQLSERVER_PROJECT='tests/DatabaseTests/SqlServer'`, the two `describe --force` lines name `Postgres.csproj` and `SqlServer.csproj`, and the header comment's four paths follow.  `record-fixtures.sh`: the two `project=` values and the two `tests=` class names, `SqlSource.DatabaseTests.Postgres.Describer.FixtureCasesTests` and `SqlSource.DatabaseTests.SqlServer.Describer.FixtureCasesTests`.

- [ ] **Step 5: Lint the changed files**

Run: `tools/shellcheck.sh`, `tools/shfmt.sh --check` and `tools/actionlint.sh`
Expected: each passes.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add pre-commit-validation.sh .github/workflows/build.yml .github/workflows/nightly.yml tools/describe-end-to-end.sh tools/record-fixtures.sh
git commit -m "Point the script, the workflows and the tools at tests/DatabaseTests"
```

### Task 3: The folder's readme and AGENTS.md, and the moved documents

**Files:**
- Create: `tests/DatabaseTests/README.md`, `tests/DatabaseTests/AGENTS.md`
- Modify: `tests/README.md`, `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`, and the `AGENTS.md` of `Postgres`, `Postgres/EndToEnd`, `Postgres/Realistic`, `Postgres.Npgsql8`, `SqlServer`, `SqlServer/EndToEnd`, `SqlServer/Realistic`, `SqlServer.SqlClient61`
- Test: `tests/SqlSource.Tests/Documentation/AgentsFileTests.cs` (exists; it finds the new file itself)

- [ ] **Step 1: Write `tests/DatabaseTests/README.md`**

Sections, in this order:

1. A title, `# Database tests`, and two paragraphs: the folder holds every test project that runs against a real PostgreSQL or SQL Server, where the describers' answers, the generated methods and the drivers meet a server; each needs Docker, starts its containers through Testcontainers, and is one of the projects that `tools/test-scopes.txt` names, so `pre-commit-validation.sh` skips it when a change cannot reach it ([`tools/README.md#test-scopes`](../../tools/README.md#test-scopes)).
2. `## The projects`: the table of the four, with the columns of `tests/README.md`'s table (project, Roslyn, driver, runs), the rows moved from there word for word but for the paths.
3. `## Running`: `dotnet test --project tests/DatabaseTests/<name>`, the two image variables with the floor images as examples, and that the two driver-floor projects ignore the variables and run the default image.
4. `## The Npgsql 8 project` and `## The SqlClient 6.1 project`: the two sections of `tests/README.md`, moved whole, paths updated.
5. `## Procedures`: one line each routing to `Postgres/README.md` (Docker and the image, the describer's fixtures, the realistic corpus, the end-to-end project) and `SqlServer/README.md` (Docker and emulation on Apple silicon, the images and their builds, the fixture schema, the fixtures, the corpus, the end-to-end project).

- [ ] **Step 2: Write `tests/DatabaseTests/AGENTS.md`**

Title `# AGENTS.md - tests/DatabaseTests`, one line linking the readme, then these rules, each one bullet with its statement in bold and at most one sentence of reason:

- The naming pattern: folder and project file short, `Postgres/Postgres.csproj`; assembly and root namespace `SqlSource.DatabaseTests.Postgres`, set by `Directory.Build.props` of this folder from the project file's name, never in a project file; every namespace in the project starts with it.
- `TestScopes` of `tests/SqlSource.Testing` derives the same name from a `project` line of `tools/test-scopes.txt`, so a new project here gets its `project` line and its folder's scope lines, and `RunningProjectTests`, copied from a sibling, in the same change.
- Every project here needs Docker; a test that needs the server is in the project's collection (`UsesPostgres`, `UsesSqlServer`, `UsesApp`), which starts one container for the run through `EngineImage` of `tests/SqlSource.Testing`.
- `Postgres.Npgsql8` and `SqlServer.SqlClient61` pin the oldest supported driver and reference `src/SqlSource` and `tests/SqlSource.Testing` alone: a reference to the tool, to `tests/SqlSource.Tool.Testing` or to the engine's describer project is a package downgrade.  `Postgres` and `SqlServer` reference `tests/SqlSource.Tool.Testing` for the describer, fixture and corpus helpers.
- A change here is run on both of the engine's images before it is pushed, since `pre-commit-validation.sh` runs the default image alone.
- The maintenance footer.

Check the size: `wc -c tests/DatabaseTests/AGENTS.md` under 8192.

- [ ] **Step 3: Trim `tests/README.md`**

The opening paragraph counts "seven test projects" and names the four Docker projects: it now says the three projects under `tests/` and the four database projects under [`tests/DatabaseTests/`](DatabaseTests/README.md).  The table keeps the rows of `SqlSource.Tests`, `SqlSource.Tests.RoslynFloor` and `SqlSource.Tool.Tests`, and one line below it routes to the database tests' readme for the other four.  The paragraph after the table that names which projects nightly leaves out and where each engine's procedures are takes the new paths.  The mermaid graph's node labels and the bullets of "What each test project takes from the libraries" take the new paths.  The sections "The Npgsql 8 project" and "The SqlClient 6.1 project" are replaced by one line linking to their new place, and the "Fixture projects" section's two readme links take the new paths.  Every other `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.SqlServer` in the file becomes the new path.

- [ ] **Step 4: Fix the moved documents**

In the two project readmes and eight `AGENTS.md` files: titles take the new path (`# AGENTS.md - tests/DatabaseTests/Postgres`, `# AGENTS.md - tests/DatabaseTests/Postgres/EndToEnd`, the readmes `# tests/DatabaseTests/Postgres` and `# tests/DatabaseTests/SqlServer`); every old path in the text becomes the new one; and every relative link goes one folder further up: `../README.md#` becomes `../../README.md#` where it meant `tests/README.md`, except the two driver-floor `AGENTS.md` links to `#the-npgsql-8-project` and `#the-sqlclient-61-project`, which now point at `../README.md#...`, the folder's readme; `../../docs/`, `../../tools/` become `../../../`; `../SqlSource.Testing/` becomes `../../SqlSource.Testing/`; `../SqlSource.Tests.SqlServer/README.md` becomes `../SqlServer/README.md` and the converse; in `EndToEnd/AGENTS.md` and `Realistic/AGENTS.md`, `../../SqlSource.Testing/` becomes `../../../SqlSource.Testing/` and `../../../docs/` becomes `../../../../docs/`, while `../README.md` (the project's own) stays.

- [ ] **Step 5: Check every relative link of the touched markdown resolves**

```bash
for f in tests/README.md tests/DatabaseTests/README.md tests/DatabaseTests/AGENTS.md $(find tests/DatabaseTests -name '*.md' -not -path '*/bin/*' -not -path '*/obj/*'); do
    d=$(dirname "$f")
    grep -o '](\.[^)#]*' "$f" | sed 's/^](//' | while read -r l; do [ -e "$d/$l" ] || echo "$f -> $l"; done
done
```

Expected: no output.

- [ ] **Step 6: Run the documentation tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Documentation`
Expected: pass; `AgentsFileTests` reads the new file and finds every rule under 80 words and the file under 8 KB.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add tests/README.md tests/DatabaseTests
git commit -m "Give tests/DatabaseTests its readme and AGENTS.md"
```

### Task 4: Every other document that names the old paths

**Files:**
- Modify: `AGENTS.md:25,31,33-35,44`, `CONTRIBUTING.md:10,31-35,46-60,119`, `tests/AGENTS.md:38`, `tools/README.md:68,89,93,95,96`, `tools/AGENTS.md:13-15`, `.github/AGENTS.md`, `.github/workflows/README.md:10,13,33`, `docs/architecture/postgres-describer.md:66`, `docs/architecture/postgres-nullability.md:169`, `docs/architecture/sqlserver-describer.md:368,374,382`, `docs/tech-debt/TD-0034`, `TD-0051`, `TD-0071`, `TD-0077`, `TD-0078`, `TD-0080`, `TD-0082`, `TD-0085`, `TD-0086`, `TD-0087`, `TD-0088` (the files `grep -l` finds under `docs/tech-debt` and `docs/deferred`), `.claude/skills/reviewing-maintainability/SKILL.md:50`

- [ ] **Step 1: Rewrite the paths**

In each file, `tests/SqlSource.Tests.Postgres` becomes `tests/DatabaseTests/Postgres` and `tests/SqlSource.Tests.SqlServer` becomes `tests/DatabaseTests/SqlServer`, in prose and in links alike, and a sentence that lists "the four" by name reads naturally with the short names where the folder is already named.  The root `AGENTS.md`'s floor-image commands in rule 2 of Commits take the new project paths.  `TD-0080`'s items keep their meaning; its links move.  Then:

```bash
grep -rn "SqlSource\.Tests\.\(Postgres\|SqlServer\)" --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=superpowers --exclude-dir=worktrees .
```

Expected: no line.

- [ ] **Step 2: Check the links of the changed documents resolve**

Run the loop of Task 3 step 5 over the files of this task.
Expected: no output.

- [ ] **Step 3: Run the documentation tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace SqlSource.Tests.Documentation`
Expected: pass.

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add AGENTS.md CONTRIBUTING.md tests/AGENTS.md tools .github docs .claude/skills
git commit -m "Rewrite the paths of the database test projects in every document"
```

### Task 5: Full validation, floor images, nightly and the pull request

**Files:** none changed unless a check fails.

- [ ] **Step 1: The full validation**

Run: `./pre-commit-validation.sh --full`
Expected: passes.

- [ ] **Step 2: The floor images**

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`
Run: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`
Expected: both pass.

- [ ] **Step 3: The version check**

Run: `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`
Compare with `VersionPrefix` in `Directory.Build.props` (`0.1.0`).  No tag means nothing to compare; a tag at or above it means asking the user for the new version before opening the pull request.

- [ ] **Step 4: Branch review**

Run the `branch-review:review` skill; address every finding, minor ones included, and run it again until it comes back clean.  A commit made after a clean review needs another.

- [ ] **Step 5: Bring the branch up to date, push, and try the nightly**

Merge `origin/main` into the branch (the app's `sync_with_base_branch` tool in a worktree), rerun `./pre-commit-validation.sh --full` if the merge brought changes, and push.  Then:

```bash
gh workflow run nightly.yml --ref <branch>
```

Expected: the Windows and macOS job and the five Linux jobs pass on the new project paths.  Open the pull request with a description that names the four moves, the naming pattern, the `TestScopes` prefix and the two new documents, says that `CheckTests` confirmed the sidecars needed no rewrite (or that they were rewritten, if Task 1 step 13 took the contingency), links the nightly run, ends with the attribution line, and turn Auto-fix on.
