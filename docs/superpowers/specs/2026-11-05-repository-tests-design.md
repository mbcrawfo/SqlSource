# Repository tests in their own project: design

A small change.  It moves tests and test helpers between projects; no test's assertion changes.

## Intent

The code test projects also hold tests that check the repository: its docs, its AGENTS.md files, its scripts' data and
the layout of its test projects.  Mixed in with tests of the product, they are easy to miss, and they blur what each
test project is responsible for.  This change gives them a project of their own, so that each code test project tests
code and the repository's checks are in one place.

## Goals

- A test project, `tests/SqlSource.Repository.Tests`, for the tests whose subject is a repository file outside every
  test project, or how the test projects fit together.
- A library, `tests/SqlSource.Repository`, that owns where the repository is and which files a project may read.
  Everything that reads the repository depends on it, and it depends on nothing.
- No project gains a reference it should not have.  In particular the floor projects (`SqlSource.Tests.RoslynFloor`,
  `Postgres.Npgsql8`, `SqlServer.SqlClient61`) and `TestHarness` must not reach the tool, its drivers or Roslyn 5.9.

## Non-goals

- A test that checks the files of its own project stays in that project.
- Tests of other test helpers (`TestHarness/*`, `GoldenFiles`, the generator and sidecar harnesses, `ExpectedTests`,
  `TestProjectTests`, `RealisticCorpusTests`) stay where they are.
- The PostGIS Dockerfile tests stay in `TestHarness/PostgisContainerTests.cs`.
- No test changes what it asserts.

## Approaches

1. **One project, with every listed test moved, split-out tests included.**  Chosen: it is the only one that fully
   separates the responsibilities.
2. **One project, with the tests that share a file with code tests left in place.**  Less churn, but those checks
   stay hidden among code tests, which is the problem this change fixes.
3. **Two projects, one for docs checks and one for layout and version checks.**  Turned down: the groups are too
   small to be worth two projects that must be kept in step.

For where the repository's location lives:

- **Leave it in `SqlSource.Testing`.**  The new project would depend on the general test library for one concern.
- **Put it in `SqlSource.Repository.Tests` and have `SqlSource.Testing` reference that.**  Turned down: the test
  project references `SqlSource.Tool`, so every user of `SqlSource.Testing` would reach the tool's drivers and Roslyn
  5.9, which breaks the floor projects and `TestLayoutTests.TestHarness_References_BringNoDriverAndNothingOfTheTool`.
  It would also make libraries reference a test executable.
- **A library of its own, `SqlSource.Repository`.**  Chosen: it keeps the inversion the owner asked for (the
  repository's location belongs to the repository area) and references nothing.

## The library: `tests/SqlSource.Repository`

- Not a test project (`IsTestProject` false), no package or project references.
- Takes these files from `tests/SqlSource.Testing/`, all of which use only the base library: `RepositoryRoot.cs`,
  `RepositoryWorkingFolder.cs`, `TestScopes.cs`, `ProjectIncludes.cs`.  Their types become public; the members the
  tests of the check use (`RepositoryRoot.PathFor`, `ScopesFile`) are reached by `InternalsVisibleTo` for
  `SqlSource.Repository.Tests` alone.
- Referenced by `SqlSource.Testing`, `TestHarness`, `SqlSource.Tool.Testing`, and any test project that reads the
  repository directly.  `RepositoryRoot.RunningProject` still reads the entry assembly, so the read guard is unchanged.

## The test project: `tests/SqlSource.Repository.Tests`

References `SqlSource`, `SqlSource.Tool` and `SqlSource.Repository`.  `SqlSource` and `SqlSource.Tool` grant it
`InternalsVisibleTo`.  It needs no `SqlSource.Testing`, `SqlSource.Tool.Testing` or `TestHarness`.

What moves into it:

| From | Test |
|---|---|
| `SqlSource.Tests/Documentation/` | the whole folder: `AgentsFileTests`, `UserDocsLinksTests`, `UserDocsReadingTests`, `UserDocsSamplesTests`, the `UserDocs` helper and its `AGENTS.md` |
| `SqlSource.Tests/Diagnostics/` | `DiagnosticsDocumentTests` |
| `SqlSource.Tests/` | `RepositorySettingsFileTests` |
| `SqlSource.Tests/Validation/` | the whole folder: `TestScopesFileTests`, `TestScopesTests`, `ProjectIncludesTests`, `RepositoryRootTests`, `ContainerProjectInputsTests` |
| `SqlSource.Tests/TestLayoutTests.cs` | `TestProjects_CompileIncludes_ReachNoFileOfAnotherProject`, the `IncludesFromOutside_*` tests, `TestHarness_References_BringNoDriverAndNothingOfTheTool`, `SqlSourceTesting_PackageReferences_BringNoTestcontainers` |
| `SqlSource.Tests/Generation/Emitting/RowBufferTests.cs` | `Source_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsAttributeNameAndDepth` |
| `SqlSource.Tool.Tests/Watching/` | `EditorRecipeTests` |

What stays:

- `TestLayoutTests`: `GeneratorFolder_Files_AreAllTestClasses` and `GeneratorTests_FirstLine_SaysTheyAreShared`, which
  check `SqlSource.Tests`' own `Generator/` folder.
- `QueryMethodCorpusTests.GoldenFolder_EveryFile_IsACase`, which checks `SqlSource.Tests`' own golden folder.
- Both `FixtureVersionTests`: each database project records and reads its own engine's fixtures.
- The image-pin tests of `PostgresDatabasesTests` and `SqlServerDatabasesTests`.
- The four `RunningProjectTests` and `RoslynVersionTests`, which check the project they run in.
- `PostgisContainerTests`, whole.

## What else changes with it

- `SqlSource.slnx` lists both new projects; the solution-wide `dotnet test` of `pre-commit-validation.sh` and CI then
  runs the new tests with no workflow change.
- `tools/test-scopes.txt`: a `none` line for `tests/SqlSource.Repository.Tests/*`, and its header names the new home
  of its check.  The library gets no line, so a change to it runs both engines, as one to `tests/SqlSource.Testing`
  does: the container projects reach it through their references, which `ContainerProjectInputsTests` checks.
- The projects that lose a moved test drop any reference or `InternalsVisibleTo` that only that test needed.
- Docs that name a moved path or the helpers' project: root `AGENTS.md`, `docs/manual/AGENTS.md`, `tests/AGENTS.md`
  (including its rule that the libraries' types stay `internal`, which gains the new library as an exception),
  `tests/README.md`, `tools/README.md`, `CONTRIBUTING.md` (one sentence per project), and the AGENTS.md files of
  `tests/SqlSource.Tests/Generator/`, `tests/DatabaseTests/` and its engine folders.  Plans under
  `docs/superpowers/plans/` are history and are not edited.
- `RepositoryRootTests.RunningProject_InThisProject_IsSqlSourceTests` names the project it runs in, so it changes with
  the move to expect `SqlSource.Repository.Tests`; it is the one assertion that changes.

## Left to the builder

- The namespace of the library (`SqlSource.Repository` is the natural one) and the folder layout inside the new test
  project, mirroring the folders the tests came from.
- Whether `TestLayoutTests` keeps its name in both projects or the two parts are renamed to say what each covers.
- The comments on moved tests, rewritten only where they name the project they were in.
