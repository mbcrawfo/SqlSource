# Repository Tests Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the tests that check repository files out of the code test projects into `tests/SqlSource.Repository.Tests`, with the repository-location helpers in a new library, `tests/SqlSource.Repository`, that every reader of the repository references.

**Architecture:** `SqlSource.Repository` is a library with no references that owns `RepositoryRoot`, `RepositoryWorkingFolder`, `TestScopes` and `ProjectIncludes`, now public.  `SqlSource.Repository.Tests` is an xUnit v3 test project that references `SqlSource`, `SqlSource.Tool` and `SqlSource.Repository`, and holds the library's own tests plus every check whose subject is a file outside every test project or how the test projects fit together.  Tests move with `git mv` so their history follows; no assertion changes but one.

**Tech Stack:** .NET 10, xUnit v3 on Microsoft.Testing.Platform, Shouldly, central package management with lock files.

**Spec:** `docs/superpowers/specs/2026-11-05-repository-tests-design.md`

## Global Constraints

- The library is `tests/SqlSource.Repository/SqlSource.Repository.csproj`: `net10.0`, `IsPackable` false, `IsTestProject` false, no `PackageReference` and no `ProjectReference`.
- Its namespace is `SqlSource.Repository`.  The test project's root namespace is `SqlSource.Repository.Tests`, and each moved file's namespace follows its folder in the new project.
- The test project is `tests/SqlSource.Repository.Tests/SqlSource.Repository.Tests.csproj`, with the property group and package block of `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj` (`OutputType` `Exe`, `IsPackable` false, `GitHubActionsTestLogger`, `Microsoft.CodeAnalysis.CSharp` with `VersionOverride="5.9.0"`, `Microsoft.Testing.Extensions.CodeCoverage`, `Shouldly`, `xunit.v3`), and project references to `src/SqlSource`, `src/SqlSource.Tool` and `tests/SqlSource.Repository` alone.
- Neither new project references `SqlSource.Testing`, `SqlSource.Tool.Testing` or `TestHarness`.
- No project's set of references to `src/SqlSource.Tool`, a driver or Roslyn changes, except the new test project's.
- Every move uses `git mv`.  No moved test changes what it asserts, except `RepositoryRootTests.RunningProject_InThisProject_IsSqlSourceTests`, which becomes `RunningProject_InThisProject_IsSqlSourceRepositoryTests` and expects `"SqlSource.Repository.Tests"`.
- Both new projects are in `SqlSource.slnx` and commit their `packages.lock.json`.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit; the final push needs `./pre-commit-validation.sh --full` (root `AGENTS.md`, Commits).
- Docs and plans under `docs/superpowers/plans/` other than this one are history and are not edited.
- An `AGENTS.md` bullet stays at most 80 words and a file at most 8 KB (16 KB for the root one); `AgentsFileTests` checks both.

## Review Focus

- **A test lost in a move.**  A moved file that falls out of every project, or a split test left behind in neither, passes every build.  Each task compares the solution's test list before and after (Task 1, Step 1 and the closing step of each task): the multiset of `Class.Method` names must be unchanged.
- **A container project that reaches the library unseen by the scopes.**  The container projects now reference `SqlSource.Repository`; `ContainerProjectInputsTests` (moved in Task 1) must still pass, which it does only if `tools/test-scopes.txt` has no `none` line covering `tests/SqlSource.Repository/`.
- **A floor project that gains the tool.**  `TestLayoutTests.TestHarness_References_BringNoDriverAndNothingOfTheTool` and `SqlSourceTesting_PackageReferences_BringNoTestcontainers` keep passing after they move (Task 3), and the build of `tests/SqlSource.Tests.RoslynFloor`, `tests/DatabaseTests/Postgres.Npgsql8` and `tests/DatabaseTests/SqlServer.SqlClient61` stays on their pinned packages: their `packages.lock.json` gain only the `sqlsource.repository` project entry.
- **The read guard in the container projects.**  `RepositoryRoot.Path` still finds the running project from the entry assembly, so each `RunningProjectTests` must pass in its own project; run the container projects in Task 1 rather than trusting the plain validation run to skip them.
- **Stale paths in docs.**  A doc that still names `tests/SqlSource.Tests/Documentation/`, `tests/SqlSource.Tests/Validation/`, `SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests` or the helpers in `tests/SqlSource.Testing` reads wrong without failing a test.  Task 4 greps for each.

---

### Task 1: The library and its own tests

**Files:**
- Create: `tests/SqlSource.Repository/SqlSource.Repository.csproj`
- Move (`git mv`) from `tests/SqlSource.Testing/` to `tests/SqlSource.Repository/`: `RepositoryRoot.cs`, `RepositoryWorkingFolder.cs`, `TestScopes.cs`, `ProjectIncludes.cs`
- Create: `tests/SqlSource.Repository.Tests/SqlSource.Repository.Tests.csproj`
- Move (`git mv`) `tests/SqlSource.Tests/Validation/` (the whole folder: `ContainerProjectInputsTests.cs`, `ProjectIncludesTests.cs`, `RepositoryRootTests.cs`, `TestScopesFileTests.cs`, `TestScopesTests.cs`) to `tests/SqlSource.Repository.Tests/Validation/`
- Modify: `SqlSource.slnx`; the `.csproj` of `tests/SqlSource.Testing`, `tests/DatabaseTests/TestHarness`, `tests/SqlSource.Tool.Testing`, `tests/SqlSource.Tests`, `tests/SqlSource.Tool.Tests`, `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61`
- Modify: every `.cs` that uses one of the four types (list: `grep -rlE '\b(RepositoryRoot|RepositoryWorkingFolder|TestScopes?|ProjectIncludes)\b' --include='*.cs' tests | grep -vE '/(bin|obj)/'`)
- Modify: `tools/test-scopes.txt`, `tools/README.md`, `tests/AGENTS.md`, `tests/README.md`, `CONTRIBUTING.md`, `tests/DatabaseTests/AGENTS.md`

**Interfaces:**
- Produces, in namespace `SqlSource.Repository`, assembly `SqlSource.Repository`:
  - `public static class RepositoryRoot` with public `RunningProject`, `WorkingFolder`, `Path(params string[])`, `Check(...)`; `internal` `ScopesFile` and `PathFor(string, TestScopes, params string[])`.
  - `public readonly record struct RepositoryWorkingFolder` with an `internal` constructor and a `public string Path { get; }` (`CliRun.Real` of `tests/SqlSource.Tool.Testing` reads it).
  - `public enum TestScope`, `public sealed class TestScopes`, `public static class ProjectIncludes`, members unchanged.
  - `InternalsVisibleTo` for `SqlSource.Repository.Tests` alone.
- Produces: `tests/SqlSource.Repository.Tests`, which Tasks 2 and 3 add tests to.

- [ ] **Step 1: Record the baseline test list**

Build the solution, then for each test project run its built executable with `--list-tests` (for example `dotnet tests/SqlSource.Tests/bin/Debug/net10.0/SqlSource.Tests.dll --list-tests`), across `SqlSource.Tests`, `SqlSource.Tool.Tests`, `SqlSource.Tests.RoslynFloor` and the container projects.  Reduce each line to `Class.Method` (drop the namespace and any theory arguments), sort, and save to the scratchpad as the baseline.  Every later comparison uses the same reduction.

- [ ] **Step 2: Create the library and move the four helpers**

`git mv` the four files.  Change their namespace to `SqlSource.Repository`, make the five types public as in Interfaces, and make `RepositoryWorkingFolder.Path` public; leave every other member's access as it is.  The `.csproj` has the Global Constraints' properties, a comment in the style of `tests/SqlSource.Testing/SqlSource.Testing.csproj` saying it holds where the repository is and which files a project may read, references nothing so every project can take it, and holds no tests, and `<InternalsVisibleTo Include="SqlSource.Repository.Tests" />`.

- [ ] **Step 3: Point every user at the library**

Add `<ProjectReference>` to `tests/SqlSource.Repository/SqlSource.Repository.csproj` in each `.csproj` listed under Files, using its relative path.  In each `.cs` that uses the types, add `using SqlSource.Repository;`, and drop `using SqlSource.Testing;` where nothing else of that namespace is left (IDE0005 fails the build otherwise).

- [ ] **Step 4: Create the test project and move `Validation/`**

The `.csproj` has the Global Constraints' properties and packages, and references `tests/SqlSource.Repository` only for now; Task 2 adds the product and the tool.  `git mv` the folder, set each file's namespace to `SqlSource.Repository.Tests.Validation`, and swap `using SqlSource.Testing;` for `using SqlSource.Repository;`.  Rename `RunningProject_InThisProject_IsSqlSourceTests` to `RunningProject_InThisProject_IsSqlSourceRepositoryTests` and make it expect `"SqlSource.Repository.Tests"`.  Add both projects to `SqlSource.slnx` beside `tests/SqlSource.Testing`.

- [ ] **Step 5: Restore, build and run**

Run: `dotnet build SqlSource.slnx`
Expected: success with no warnings; new `packages.lock.json` files for both projects; the floor projects' lock files gain only the `sqlsource.repository` project entry.

Run: `dotnet test --project tests/SqlSource.Repository.Tests`
Expected: all pass, including `ContainerProjectInputsTests.Refused_RepositoryScopes_IsEmpty`.

Run each container project's built executable with `--filter-class '*.RunningProjectTests'` (for example `dotnet tests/DatabaseTests/Postgres/bin/Debug/net10.0/SqlSource.DatabaseTests.Postgres.dll --filter-class '*.RunningProjectTests'`), with Docker running.
Expected: pass in all four.

- [ ] **Step 6: Update the scopes and the docs**

- `tools/test-scopes.txt`: add `none tests/SqlSource.Repository.Tests/*` beside `none tests/SqlSource.Tests/*`; the header's `tests/SqlSource.Tests/Validation/` becomes `tests/SqlSource.Repository.Tests/Validation/`.  Add no line for `tests/SqlSource.Repository/`.
- `tools/README.md`: the `tests/SqlSource.Tests/Validation/` path in its scopes section.
- `tests/AGENTS.md`: there are four libraries, not three.  The rule that the libraries' types stay `internal` gains `tests/SqlSource.Repository` as an exception, because the other libraries use its types from their own assemblies.  The reference rule says `SqlSource.Repository` references nothing.  `RepositoryRoot` is now in `tests/SqlSource.Repository`.
- `tests/README.md`: a row for `tests/SqlSource.Repository` in the libraries table (its Roslyn column: none); a sentence in the section that says why the libraries are apart; a `Repository` node in the mermaid graph with an edge from each project that now references it; the "What each test project takes" list names `tests/SqlSource.Repository` for `RepositoryRoot`, `TestScopes` and `ProjectIncludes` instead of `tests/SqlSource.Testing`; the "Reading the repository" section names the new library; a sentence on `tests/SqlSource.Repository.Tests` beside the other test projects.
- `CONTRIBUTING.md`: one sentence for `tests/SqlSource.Repository.Tests` ("runs the checks of the repository's own files: its docs, its AGENTS.md files, its test scopes and the layout of its test projects"), and the libraries sentence names four libraries.
- `tests/DatabaseTests/AGENTS.md`: its two "reference ... alone" rules add `tests/SqlSource.Repository`.

- [ ] **Step 7: Compare the test list and validate**

Rebuild, then re-list as in Step 1, now including `SqlSource.Repository.Tests`.
Expected: the same multiset as the baseline, except `RunningProject_InThisProject_IsSqlSourceTests` → `RunningProject_InThisProject_IsSqlSourceRepositoryTests`.

Run: `./pre-commit-validation.sh`
Expected: pass.

- [ ] **Step 8: Commit**

```bash
git add -A tests/SqlSource.Repository tests/SqlSource.Repository.Tests tests/SqlSource.Testing tests/SqlSource.Tests tests/SqlSource.Tool.Tests tests/SqlSource.Tool.Testing tests/DatabaseTests SqlSource.slnx tools CONTRIBUTING.md tests/AGENTS.md tests/README.md docs/superpowers/specs/2026-11-05-repository-tests-design.md docs/superpowers/plans/2026-11-05-repository-tests.md
git commit -m "refactor(tests): the repository's location in a library of its own, with its tests"
```

---

### Task 2: The checks of the docs and the repository's files

**Files:**
- Move (`git mv`) to `tests/SqlSource.Repository.Tests/`:
  - `tests/SqlSource.Tests/Documentation/` (whole folder, `AGENTS.md` included) → `Documentation/`
  - `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs` → `Diagnostics/DiagnosticsDocumentTests.cs`
  - `tests/SqlSource.Tests/RepositorySettingsFileTests.cs` → `RepositorySettingsFileTests.cs`
  - `tests/SqlSource.Tool.Tests/Watching/EditorRecipeTests.cs` → `Watching/EditorRecipeTests.cs`
- Modify: `tests/SqlSource.Repository.Tests/SqlSource.Repository.Tests.csproj`, `src/SqlSource/SqlSource.csproj`, `src/SqlSource.Tool/SqlSource.Tool.csproj`
- Modify: root `AGENTS.md` (the link-test path in the packed-readmes rule, the diagnostics-test path in the `diagnostics.md` rule, the `AgentsFileTests.cs` path in rule 13 of the AGENTS.md guidelines), `docs/manual/AGENTS.md` (its Tests bullet)

**Interfaces:**
- Consumes: `SqlSource.Repository.RepositoryRoot` (Task 1).
- Consumes, through `InternalsVisibleTo`: `SqlDiagnostics`, `ToolDiagnostics`, `SqlFileParser`, `SettingsFileReader` of `src/SqlSource`; `WatchSession` and `Reporter` of `src/SqlSource.Tool`.

- [ ] **Step 1: Reference the product and the tool**

Add `<ProjectReference>`s to `src/SqlSource/SqlSource.csproj` and `src/SqlSource.Tool/SqlSource.Tool.csproj` in the test project, and `<InternalsVisibleTo Include="SqlSource.Repository.Tests" />` to both `src` projects, each in its existing list.

- [ ] **Step 2: Move the files**

`git mv` each file.  Set namespaces to `SqlSource.Repository.Tests.Documentation`, `.Diagnostics`, `.Watching` and `SqlSource.Repository.Tests`, and swap `using SqlSource.Testing;` for `using SqlSource.Repository;`.  Rewrite a comment only where it names the project the test was in.

- [ ] **Step 3: Build and run**

Run: `dotnet build SqlSource.slnx`
Expected: success with no warnings; `tests/SqlSource.Repository.Tests/packages.lock.json` gains the tool's packages; no other lock file changes.

Run: `dotnet test --project tests/SqlSource.Repository.Tests`
Expected: all pass.

- [ ] **Step 4: Update the docs that name the moved paths**

The root `AGENTS.md` and `docs/manual/AGENTS.md` lines listed under Files: `tests/SqlSource.Tests/Documentation/` becomes `tests/SqlSource.Repository.Tests/Documentation/`, and `tests/SqlSource.Tests/Diagnostics/` (for the diagnostics document test) becomes `tests/SqlSource.Repository.Tests/Diagnostics/`.  `tests/AGENTS.md`'s bullet on `tests/SqlSource.Tests/Documentation/` gets the new path.  `tests/README.md` lists what each test project takes; `tests/SqlSource.Repository.Tests` now also takes the product and the tool for these checks.

- [ ] **Step 5: Compare the test list and validate**

Re-list as in Task 1, Step 1.
Expected: the multiset after Task 1, unchanged.

Run: `./pre-commit-validation.sh`
Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add -A tests src/SqlSource/SqlSource.csproj src/SqlSource.Tool/SqlSource.Tool.csproj AGENTS.md docs/manual/AGENTS.md
git commit -m "refactor(tests): the checks of the docs and the repository's files in SqlSource.Repository.Tests"
```

---

### Task 3: The checks that span test projects

**Files:**
- Split: `tests/SqlSource.Tests/TestLayoutTests.cs`
  - `git mv` it to `tests/SqlSource.Repository.Tests/TestLayoutTests.cs`, keeping `TestProjects_CompileIncludes_ReachNoFileOfAnotherProject`, the `IncludesFromOutside_*` tests, `TestHarness_References_BringNoDriverAndNothingOfTheTool` and `SqlSourceTesting_PackageReferences_BringNoTestcontainers`.
  - Create `tests/SqlSource.Tests/GeneratorFolderTests.cs`, class `GeneratorFolderTests`, with `GeneratorFolder_Files_AreAllTestClasses` and `GeneratorTests_FirstLine_SaysTheyAreShared` and the members only they use.  It stays outside `Generator/`, since RoslynFloor compiles every file there.
- Split: `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`
  - Create `tests/SqlSource.Repository.Tests/Generation/Emitting/RowBufferByHandTests.cs`, class `RowBufferByHandTests`, with `Source_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsAttributeNameAndDepth`, its comment, `HandWrittenCopy` and `HandWrittenDeclaration`.
  - The remaining tests stay in `RowBufferTests.cs`.
- Modify: `tests/AGENTS.md` (its first bullet names `tests/SqlSource.Tests/TestLayoutTests.cs`), `tests/SqlSource.Tests/Generator/AGENTS.md` (its bullet names `TestLayoutTests.cs` for the Generator-folder checks)

**Interfaces:**
- Consumes: `RepositoryRoot`, `ProjectIncludes` (Task 1); `RowBuffer.Source`, `GeneratedText.Indent` of `src/SqlSource` through the `InternalsVisibleTo` of Task 2.

- [ ] **Step 1: Split `TestLayoutTests`**

`git mv` the file so its history follows the larger part.  Move the two Generator-folder tests into `GeneratorFolderTests` in `SqlSource.Tests`.  Set the moved file's namespace to `SqlSource.Repository.Tests`.  Each file keeps only the fields, helpers and `using`s its tests use, and the class comment of each says what it covers.

- [ ] **Step 2: Split `RowBufferTests`**

Move the one test and its two members into `RowBufferByHandTests`, namespace `SqlSource.Repository.Tests.Generation.Emitting`.  Drop the `using`s that the remaining `RowBufferTests.cs` no longer needs (`System.IO`, and `SqlSource.Testing` if nothing else uses it).

- [ ] **Step 3: Build and run**

Run: `dotnet build SqlSource.slnx`
Expected: success with no warnings.

Run: `dotnet test --project tests/SqlSource.Repository.Tests` and `dotnet test --project tests/SqlSource.Tests`
Expected: all pass.

- [ ] **Step 4: Update the docs**

`tests/AGENTS.md`'s first bullet: the compile-includes check is `tests/SqlSource.Repository.Tests/TestLayoutTests.cs`.  `tests/SqlSource.Tests/Generator/AGENTS.md`: the check of the folder's files is `tests/SqlSource.Tests/GeneratorFolderTests.cs`.

- [ ] **Step 5: Compare the test list and validate**

Re-list as in Task 1, Step 1.
Expected: the multiset after Task 2, except that two `TestLayoutTests.*` entries are now `GeneratorFolderTests.*` and the hand-written-copy test is `RowBufferByHandTests.*`, with method names unchanged.

Run: `./pre-commit-validation.sh`
Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add -A tests
git commit -m "refactor(tests): the checks that span test projects in SqlSource.Repository.Tests"
```

---

### Task 4: Sweep and verify the branch

**Files:**
- Modify: whatever the greps below find.

- [ ] **Step 1: Find stale references**

Run, excluding `docs/superpowers/plans/` (other than this plan), `bin/` and `obj/`:

```bash
grep -rnE 'SqlSource\.Tests/(Documentation|Validation)|SqlSource\.Tests/Diagnostics/DiagnosticsDocument|SqlSource\.Tests/(TestLayoutTests|RepositorySettingsFileTests)|SqlSource\.Tool\.Tests/Watching/EditorRecipe|SqlSource\.Testing/(RepositoryRoot|RepositoryWorkingFolder|TestScopes|ProjectIncludes)|three libraries' --include='*.md' --include='*.txt' --include='*.sh' --include='*.yml' --include='*.cs' --include='*.csproj' --include='*.props' . | grep -vE '/(bin|obj)/|docs/superpowers/plans/'
```

Expected: no match, apart from this plan and the spec.  Fix any that are found, and also read each sentence of the docs that names `RepositoryRoot`, `TestScopes` or `ProjectIncludes` with `tests/SqlSource.Testing` beside it.

- [ ] **Step 2: Drop references the moves left unused**

For `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`, check each `PackageReference`, `ProjectReference`, linked `None` item and `InternalsVisibleTo` granted to them against what their remaining tests use; remove one only if no remaining file needs it.  Expected: likely none, since every reference they hold has other users; say so in the commit if nothing changes.

- [ ] **Step 3: Full validation**

Run: `./pre-commit-validation.sh --full`
Expected: pass.

- [ ] **Step 4: Commit any fixes**

```bash
git add -A
git commit -m "docs: the last paths that named the repository tests' old projects"
```

Skip if Steps 1 and 2 changed nothing.

- [ ] **Step 5: Review**

Run the `branch-review:review` skill, address its findings, and run it again until it is clean (root `AGENTS.md`, Pull requests).
