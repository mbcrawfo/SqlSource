# Pre-commit Validation Skips Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `./pre-commit-validation.sh` checks only what the uncommitted change could break, `--full` checks everything, and tests fail when the table that decides which database tests run misses a file they read.

**Architecture:** `tools/test-scopes.txt` sorts repository paths into the database test pairs they can affect.  The script reads it to choose test projects, through a temporary solution filter.  Tests read it to refuse any file a container project reads at build time or run time that the table would let it skip.  The other steps (format, linters, package checks) skip on positive triggers in the script, and any path of an unknown kind makes the run full.

**Tech Stack:** bash, .NET 10, xunit v3 on Microsoft.Testing.Platform, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-27-pre-commit-validation-skips-design.md`

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`.  Until Task 4 lands, that is the full script as it is today.  From Task 4 on, a plain run is enough for a commit, and the push needs `--full`.
- No change to CI (`.github/workflows/*.yml`).
- Bash 3.2 compatible: macOS ships it, so no `mapfile` and no associative arrays.  Read git output with `-z` so that the `OddPaths` fixture names are not quoted.
- Scopes file pattern language: `*` matches any run of characters, `/` included, and every other character is literal.  The first matching line wins, an unmatched path is `both`, and separators are normalized to `/` before matching.
- Lines of `.sh` and `.cs` files are at most 120 characters (editorconfig-checker).
- The comments in `RepositoryRoot.cs` and in the script follow the repository's comment style: plain sentences that say why.

## Review Focus

1. **A path with spaces or quotes in the change set** (the `OddPaths` fixture): the script classifies it as its real path.  Task 4 adds `--plan` sample runs with such a path.
2. **A deleted file in the change set**: it still decides scopes and triggers, but is not passed to `--include` or to CSharpier.  Task 4 adds a sample run.
3. **A container project reading its own engine's whole folder**, such as the end-to-end folder with `bin/` and `obj/` beneath it: the run-time check passes.  Task 3's guard test covers a folder holding files of the project's own scope.
4. **`EndToEndEngine` touched from one engine's project**: it must not check the other engine's folder.  Task 3 makes both properties compute on access, and its tests on the two engines prove it.
5. **A change to `tools/test-scopes.txt` alone**: it runs both pairs, although `tools/*` is `none`.  Task 4 adds a sample run.

---

### Task 1: The scopes file and its reader

**Files:**
- Create: `tools/test-scopes.txt`
- Create: `tests/SqlSource.Testing/TestScopes.cs`
- Create: `tests/SqlSource.Tests/Validation/TestScopesTests.cs`
- Create: `tests/SqlSource.Tests/Validation/TestScopesFileTests.cs`

**Interfaces:**
- Produces, in namespace `SqlSource.Testing`, all `internal`:
  - `enum TestScope { None, Postgres, SqlServer, Both }`
  - `sealed class TestScopes`:
    - `static TestScopes Parse(string text)` throws `FormatException` naming the 1-based line, for an unknown scope, a missing pattern or a malformed `project` line.
    - `static TestScopes Repository { get; }` reads `tools/test-scopes.txt` from the repository root found the way `RepositoryRoot` finds it today.  It is lazy.
    - `TestScope Of(string relativePath)` gives the first match, or `Both`.
    - `IReadOnlyList<(TestScope Scope, string Pattern)> Lines { get; }`
    - `IReadOnlyDictionary<string, TestScope> Projects { get; }` maps a project name (the `.csproj` file name without its extension) to its engine.
    - `IReadOnlyList<string> ProjectFiles { get; }`
    - `bool Runs(string project, TestScope scope)` is true for a project that isn't in `Projects`, for `Both`, and for the project's own engine.
    - `static bool Matches(string pattern, string path)`

- [ ] **Step 1: Write the failing tests** in `TestScopesTests.cs`, each parsing a small text:
  - `Of_FirstMatchingLine_Decides`: with `postgres a/b/*` then `none a/*`, `Of("a/b/c")` is `Postgres` and `Of("a/c")` is `None`.
  - `Of_NoMatchingLine_IsBoth`
  - `Of_BackslashSeparators_MatchAsSlashes`: `Of(@"a\b\c")` is `Postgres`.
  - `Matches_Star_CrossesSlashes`: `Matches("*.md", "docs/manual/x.md")` is true.
  - `Matches_OtherCharacters_AreLiteral`: `Matches("a.b", "aXb")` is false, and `Matches("a?b", "aXb")` is false.
  - `Parse_CommentsAndBlankLines_AreIgnored`
  - `Parse_UnknownScope_ThrowsNamingTheLine`: the message contains `line 2`.
  - `Parse_ProjectLine_MapsTheProjectToItsEngine`: `project sqlserver tests/X/X.csproj` gives `Projects["X"] == SqlServer`.
  - `Runs_ProjectOfTheOtherEngine_IsFalse`, `Runs_UnlistedProject_IsTrue`, `Runs_Both_IsTrueForEveryProject`

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class 'SqlSource.Tests.Validation.TestScopesTests'`
Expected: the build fails, because `TestScopes` isn't defined.

- [ ] **Step 3: Implement `TestScopes`** in `tests/SqlSource.Testing/TestScopes.cs`.  `Matches` escapes the pattern with `Regex.Escape`, turns `\*` into `.*`, and anchors it.  Each line splits on runs of spaces.  A line is either `<scope> <pattern>` or `project <postgres|sqlserver> <path>`.

- [ ] **Step 4: Run the tests to verify they pass** (same command).  Expected: all pass.

- [ ] **Step 5: Write `tools/test-scopes.txt`** with a header comment on what the file is, the format, first-match-wins, and that a missing path is `both`.  Then the lines, in this order:
  - `project postgres tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`, and the same for `SqlSource.Tests.Postgres.Npgsql8`
  - `project sqlserver tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj`, and the same for `SqlSource.Tests.SqlServer.SqlClient61`
  - the `postgres`, `sqlserver` and `none` lines of the spec's starting table, in its order, with one pattern per line.

- [ ] **Step 6: Write `TestScopesFileTests.cs`** against `TestScopes.Repository`:
  - `RepositoryFile_EveryPattern_MatchesAFile`: the candidates are every file under the root, skipping folders named `.git`, `bin`, `obj`, `TestResults` and `artifacts`.  No test runs git today, and this keeps it that way.  The failure lists the dead patterns.
  - `RepositoryFile_EveryProjectFile_Exists`
  - `RepositoryFile_Projects_AreTheFourContainerProjects`: the four names, exactly.

- [ ] **Step 7: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace 'SqlSource.Tests.Validation'`
Expected: all pass.

- [ ] **Step 8: Run `./pre-commit-validation.sh` and commit**

```bash
git add tools/test-scopes.txt tests/SqlSource.Testing/TestScopes.cs tests/SqlSource.Tests/Validation
git commit -m "Add the test scopes file and its reader"
```

### Task 2: The build-time check

**Files:**
- Create: `tests/SqlSource.Testing/ProjectIncludes.cs`
- Modify: `tests/SqlSource.Tests/TestLayoutTests.cs` (its `IncludesFromOutside` and `Resolve` move to `ProjectIncludes`)
- Create: `tests/SqlSource.Tests/Validation/ContainerProjectInputsTests.cs`

**Interfaces:**
- Consumes: `TestScopes` (Task 1).
- Produces, `internal static class ProjectIncludes`:
  - `IEnumerable<string> FromOutside(string projectFolder, XDocument project, Func<XElement, bool> items)` is today's logic, with the item filter as a parameter.
  - `IReadOnlyList<string> Expand(string repositoryRoot, string projectFolder, string include)` gives repository-relative paths with `/`.  With no `*`, it is the resolved file.  Otherwise it enumerates every file under the folder before the first `*`, and keeps those whose path matches the include under `TestScopes.Matches` semantics.  That over-matches MSBuild's `*` slightly, which only checks more files.
  - `IReadOnlyList<string> Refused(TestScopes scopes, string repositoryRoot)` gives one `"<project>: <path> is <scope>"` for each file a container project includes from outside its folder that `scopes.Runs(project, scopes.Of(path))` refuses.  Every element with an `Include` counts, `ProjectReference` included, and an element that has only `Remove` or `Update` does not.

- [ ] **Step 1: Write the failing tests** in `ContainerProjectInputsTests.cs`:
  - `Refused_ScopesWithoutTheFixtureProjectLines_NamesTheLinkedFixtures`: parse the real file's text with the two `Fixtures/Projects/...` lines removed.  The result holds a line for `SqlSource.Tests.Postgres` with a path under `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/`, and one for `SqlSource.Tests.SqlServer` under `.../Projects/SqlServer/`.
  - `Refused_RepositoryScopes_IsEmpty`

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class 'SqlSource.Tests.Validation.ContainerProjectInputsTests'`
Expected: the build fails, because `ProjectIncludes` isn't defined.

- [ ] **Step 3: Implement `ProjectIncludes`.**  Then point `TestLayoutTests` at `ProjectIncludes.FromOutside(folder, project, static item => item.Name.LocalName == "Compile")`, with its own tests unchanged.

- [ ] **Step 4: Run the `Validation` namespace and `TestLayoutTests`.**  Expected: all pass.

- [ ] **Step 5: Run `./pre-commit-validation.sh` and commit**

```bash
git add tests/SqlSource.Testing/ProjectIncludes.cs tests/SqlSource.Tests
git commit -m "Refuse a container project input that its test scope would skip"
```

### Task 3: The run-time check

**Files:**
- Modify: `tests/SqlSource.Testing/RepositoryRoot.cs`
- Create: `tests/SqlSource.Testing/RepositoryWorkingFolder.cs`
- Modify: `tests/SqlSource.Testing/EndToEndEngine.cs`
- Modify: `tests/SqlSource.Tool.Testing/CliRun.cs`, `tests/SqlSource.Tool.Testing/Fixtures/FixtureCases.cs`, `tests/SqlSource.Tool.Testing/Realistic/RealisticCorpus.cs`
- Modify: both `RepositoryPaths.cs` files, both `EndToEnd/CheckTests.cs` files, and the four `Describer/Fixture{Cases,Version}Tests.cs` files, plus `Describer/DescribeCommandTests.cs` in `tests/SqlSource.Tests.Postgres`
- Modify: the remaining callers of `RepositoryRoot.Folder` in `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`, which a search for `RepositoryRoot.Folder` lists
- Create: `tests/SqlSource.Tests/Validation/RepositoryRootTests.cs`

**Interfaces:**
- Consumes: `TestScopes` (Task 1).
- Produces:
  - `RepositoryRoot.Path(params string[] parts)` returns the full path, after `Check(RunningProject, relative, TestScopes.Repository)`.  With no parts it is the root.  `Folder` is removed.
  - `RepositoryRoot.RunningProject` is `Assembly.GetEntryAssembly()!.GetName().Name!`.
  - `RepositoryRoot.Check(string project, string relativePath, TestScopes scopes, IEnumerable<string>? filesBelow = null)` throws `InvalidOperationException`.  The message names the project, the path, its scope and `tools/test-scopes.txt`.  It throws when the path, or any file in `filesBelow`, is refused by `scopes.Runs`.  `Path` passes the folder's files, relative and with `/`, when the path is a folder.
  - `RepositoryRoot.WorkingFolder` is a `RepositoryWorkingFolder`, a `readonly record struct` with an `internal string Path`.  Its comment says it is only for starting a process in the root.
  - `CliRun.Real(RepositoryWorkingFolder folder)` is an overload of `CliRun.Real(string)`.
  - `FixtureCases.InFolder(string engineFolder)`; `Of(engine, root)` becomes `InFolder(Path.Combine(root, engine))`.
  - `EndToEndEngine.Postgres` and `.SqlServer` compute on access (`=>`), so that touching one never checks the other's folder.
  - In each `RepositoryPaths`, `DescriberFixtures` is the engine's own folder (`.../Describers/postgres` or `.../mssql`), and the schema path is `Path.Combine(DescriberFixtures, "schema.sql")`.  `Root` is removed.
  - The rule for every caller: `Path.Combine` may go deeper than a path from `RepositoryRoot.Path`, never up.

- [ ] **Step 1: Write the failing tests** in `RepositoryRootTests.cs`, using a parsed scopes text and invented project names:
  - `Check_PathOfTheOtherEngine_ThrowsNamingTheScopesFile`
  - `Check_PathOfNone_ThrowsForAContainerProject`
  - `Check_FolderWithAFileOfTheOtherEngine_Throws`
  - `Check_FolderOfTheProjectsOwnScope_Passes`
  - `Check_UnlistedProject_PassesEverything`
  - `RunningProject_InThisProject_IsSqlSourceTests`, which pins that the entry assembly is the test project.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class 'SqlSource.Tests.Validation.RepositoryRootTests'`
Expected: the build fails, because `Check` and `RunningProject` aren't defined.

- [ ] **Step 3: Implement `RepositoryRoot`, `RepositoryWorkingFolder` and the `CliRun` overload.**  Run Step 2's command: all pass.

- [ ] **Step 4: Move every caller** to the interfaces above, so the solution builds.  `RealisticCorpusTests` gets the root from `RepositoryRoot.Path()`.  The two `CheckTests` call `CliRun.Real(RepositoryRoot.WorkingFolder)`.

Run: `dotnet build SqlSource.slnx`
Expected: 0 errors and 0 warnings.  A search for `RepositoryRoot.Folder` finds nothing.

- [ ] **Step 5: Run the whole suite.**  Every container project now checks its reads.

Run: `dotnet test --solution SqlSource.slnx --no-build`
Expected: all pass.  A refusal here is a read the table misses: fix the table, or fix the read when it reaches the other engine's folder.

- [ ] **Step 6: Show the guard refuses in a real container project, and keep nothing of it.**  Add a test to `tests/SqlSource.Tests.Postgres` that calls `RepositoryRoot.Path("docs", "manual", "README.md")`, and run that project.  It fails with the message from `Check`.  Then delete the test.

- [ ] **Step 7: Run both describers' projects on their floor images**, as root `AGENTS.md` Commits rule 2 requires after a change to a describer's tests.

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer
```

Expected: both pass.

- [ ] **Step 8: Run `./pre-commit-validation.sh` and commit**

```bash
git add tests
git commit -m "Check every repository read of a container test against its scope"
```

### Task 4: The script

**Files:**
- Modify: `pre-commit-validation.sh`
- Modify: `format.sh`

**Interfaces:**
- Consumes: the format of `tools/test-scopes.txt` (Task 1).
- Produces:
  - `pre-commit-validation.sh [--full] [--plan [path ...]]`
    - `--plan` prints each step and each test project with `run` or `skip` and the reason, and runs nothing.
    - Paths after `--plan` replace the change set, so the rules can be tried without editing the tree.
  - `format.sh [--check] [style|analyzers|csharpier ...] [-- file ...]`: files after `--` limit `dotnet format` (`--include`) and CSharpier to them.

- [ ] **Step 1: Add the `-- file ...` form to `format.sh`.**  The usage line and the header comment say it.

Run: `./format.sh --check -- src/SqlSource/Versions.cs`
Expected: passes in about 15s.  With a stray space added before a `;` in that file, it fails.  Revert the file with `git checkout -- src/SqlSource/Versions.cs`.

- [ ] **Step 2: Rewrite `pre-commit-validation.sh`.**  Keep `run_step`, locked mode and the summary.  Add one function per decision near the top, each taking a path and setting a result:
  - `is_known_kind`: the extensions `.cs .csproj .props .targets .slnx .md .sql .json .sh .txt`, a `.yml` or `.yaml` under `.github/workflows/`, `examples/*/docker-compose.yml`, and the names `.editorconfig`, `.gitignore` and `.worktreeinclude`.
  - `formats_everything`: `.editorconfig`, `*.globalconfig`, `*.props`, `*.targets`, `*.csproj`, `global.json`, `dotnet-tools.json`, and `.csharpierrc*` and `.csharpierignore`.
  - `scope_of`: reads `tools/test-scopes.txt` once into two parallel indexed arrays, and gives the first match or `both`.
  - `needs_allocation`: the spec's five triggers.
  - `needs_packages`: the spec's triggers.

  The change set:
  - It is `git diff -z --name-only --no-renames HEAD` plus `git ls-files -z --others --exclude-standard`.
  - An empty change set, `--full`, or any path that fails `is_known_kind` makes everything run.
  - A change to `tools/test-scopes.txt` runs both pairs.

  The steps:
  - Format is skipped when no `.cs` file changed.  Otherwise it passes the changed `.cs` files that exist after `--`, unless one path formats everything.
  - editorconfig-checker always runs.  shellcheck and shfmt run when a `*.sh` file changed, and actionlint when a file under `.github/workflows/` changed.
  - Test writes `{"solution":{"path":"<absolute slnx>","projects":[...]}}` to a `mktemp -d` folder.  The projects are every `Project Path` of `SqlSource.slnx`, minus the `project` lines of skipped engines.  Test then runs `dotnet test --solution <slnf> --no-build`, with `--filter-not-class '*.EndToEnd.AllocationTests'` unless `needs_allocation`.
  - Pack runs `dotnet pack SqlSource.slnx --output <tmp>/packages` once.  The three checks take that folder, and they are skipped when pack fails.
  - The summary shows each skipped step or test pair as `skipped  <name> (<reason>)`.
  - A partial run ends its summary with `Partial run: run ./pre-commit-validation.sh --full before pushing.`

- [ ] **Step 3: Verify the rules with `--plan`.**  Each line below is a command and what it must report.

| Command | Must report |
|----|----|
| `--plan docs/manual/README.md` | format skip, shellcheck/shfmt/actionlint skip, unit projects run, both pairs skip, allocation filtered, packages skip |
| `--plan src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` | format run (one file), Postgres pair run, SQL Server pair skip, allocation filtered, packages run |
| `--plan tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql` | SQL Server pair run, Postgres pair skip, packages skip |
| `--plan tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/App.csproj` | format everything, Postgres pair run |
| `--plan src/SqlSource/Generation/Emitting/RowBuffer.cs` | both pairs run, allocation run |
| `--plan tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Shapes.sql` | SQL Server pair run, allocation run |
| `--plan tools/test-scopes.txt` | both pairs run |
| `--plan LICENSE` | everything runs, reason "unknown kind" |
| `--plan 'tests/SqlSource.Tool.Tests/Fixtures/Projects/OddPaths/<a tracked file with a space>'` | classified under `tests/SqlSource.Tool.Tests/*`: both pairs skip |
| `--plan Directory.Packages.props` | format everything, both pairs run, allocation run, packages run |
| a deleted `.cs` file, by deleting one and running `--plan` with no paths | it isn't passed to format, and its scopes still apply; restore it after |

- [ ] **Step 4: Run it for real twice.**  First, with one docs file edited: it finishes in under a minute, and the summary shows the skips and the reminder.  Revert the edit.  Then run `./pre-commit-validation.sh --full`, which runs every step and passes.

- [ ] **Step 5: Commit**

```bash
git add pre-commit-validation.sh format.sh
git commit -m "Skip the checks an uncommitted change cannot affect"
```

### Task 5: The docs

**Files:**
- Modify: `AGENTS.md`, `CONTRIBUTING.md`, `tools/README.md`, `tools/AGENTS.md`, `tests/README.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `.github/AGENTS.md`, `.claude/skills/reviewing-maintainability/SKILL.md`
- Add: the spec and this plan

- [ ] **Step 1: Update each file** as the spec's "Docs in the same PR" says.  In addition:
  - `AGENTS.md`, Commits: rules 1 and 2 read as a pair.  Rule 2 replaces "run `./pre-commit-validation.sh`" with `./pre-commit-validation.sh --full`.
  - `.github/AGENTS.md`: the steps still mirror the script, and the script's skips are local only.
  - The two container readmes: line 13 and line 15 say the plain script runs them when the change can affect them, and `--full` always.
  - The maintainability skill, line 55: "again on the final tree before the push" becomes `--full`.
  - `tools/README.md`: `format.sh`'s new `-- file ...` form.
  - Each `AGENTS.md` that names a file still ends with its maintenance footer.

- [ ] **Step 2: Check that no doc describes the old behaviour.**

Run: `grep -rn 'pre-commit-validation' --include='*.md' . | grep -v docs/superpowers/`
Expected: each hit is true of the new script.  Read every hit.

- [ ] **Step 3: Run `./pre-commit-validation.sh` and commit**

```bash
git add AGENTS.md CONTRIBUTING.md tools tests .github/AGENTS.md .claude/skills docs/superpowers
git commit -m "Document the pre-commit skips and --full"
```

### Task 6: Push and PR

- [ ] **Step 1: Check the version.**  Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If it isn't greater, ask the user for the new version and set it.
- [ ] **Step 2: Bring the branch up to date with `main`.**  Use `sync_with_base_branch` in a worktree the app made, and resolve any conflicts.
- [ ] **Step 3: Run `./pre-commit-validation.sh --full`** on the tree as it will be pushed.  Expected: every step passes.
- [ ] **Step 4: Push, open the PR, bind it, and turn on Auto-fix** (`CLAUDE.local.md`).
