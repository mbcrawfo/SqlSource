# Shared test libraries - design

Date: 2026-10-16

## Why

The test projects share code by compiling each other's files: a `<Compile Include="../Other.Project/File.cs" Link="..." />` in the consumer's `.csproj`.  Twenty helper files, about 1,500 lines, are shared that way today:

| Owner | Consumer | Files |
|---|---|---|
| `tests/SqlSource.Tool.Tests` | `tests/SqlSource.Tests.Postgres` | `CliResult.cs`, `CliRun.cs`, `FakeProcessRunner.cs`, `FakeProject.cs`, `Hosts.cs`, `LogReader.cs`, `Plans.cs`, `Programs.cs`, `Secrets.cs`, `SidecarFiles.cs`, `TempFolder.cs`, `TestProject.cs`, and `Fixtures/FixtureCase.cs`, `Fixtures/FixtureCases.cs`, `Fixtures/FixtureReplay.cs`, `Fixtures/FixtureProjects.cs` |
| `tests/SqlSource.Tests` | `tests/SqlSource.Tool.Tests` | `Generator/GeneratorHarness.cs`, `Generator/InMemoryAdditionalText.cs`, `Generator/TestOptionsProvider.cs` |
| `tests/SqlSource.Tests` | `tests/SqlSource.Tool.Tests`, `tests/SqlSource.Tests.Postgres` | `Snapshot/TestSidecars.cs` |

The owner's side shows nothing: a shared file sits among unshared ones, and only another project's `.csproj` or `tests/AGENTS.md` says it is compiled elsewhere.  The pattern has already bent the code around it.  `ProcessGate` is kept out of the set because a type only the tool's tests make is reported as never made (`CA1812`) in the Postgres project; a helper a shared file needs has to go "into the file itself or into another file of the set"; and the Postgres tests import the namespace `SqlSource.Tool.Tests` of another test project.  It is tech debt and a poor way to share code.

This design replaces it with two ordinary class libraries that the test projects reference.

## Goals

- No test project compiles a `.cs` file of another project, with one exception: `tests/SqlSource.Tests.RoslynFloor` keeps compiling the generator's test classes (below).
- The twenty files live in two libraries, `SqlSource.Testing` and `SqlSource.Tool.Testing`, in namespaces of their own, referenced by project.
- `tests/SqlSource.Tests.RoslynFloor` still compiles and runs against Roslyn 4.8.0 and nothing newer.
- The exception is visible from where it lives: `tests/SqlSource.Tests/Generator/` holds only test classes, has an `AGENTS.md` saying where and why they are compiled, and every test file there opens with a comment warning that it is.
- The same tests run in each project before and after, with the same count.

## Non-goals

- Linked files that are not code.  The JSON schema, `docs/diagnostics.md`, the package's `SqlSource.props` and `SqlSource.targets`, `global.json`, the fixture projects under `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, the describer's fixtures under `Fixtures/Describers/` and the compiler's `.editorconfig` all stay as they are, linked or copied by the projects that link or copy them today.
- Any change to what a helper does.  This is a move: names of types and members, signatures and behaviour stay.  A file's namespace and its `using`s change; nothing else should.
- The other items of TD-0039 (splitting large test classes, theories that dispatch on a string, small duplicates).
- Helpers that only one project uses today, such as `ProcessGate`, `TestClock`, `FakeDescriber` and the `Postgres*` helpers of `tests/SqlSource.Tool.Tests`.  They stay where they are.

## Anti-goals

These would make the work a failure even if every test passes:

- `tests/SqlSource.Tests.RoslynFloor` resolving a Roslyn newer than 4.8.0, through any reference, direct or transitive.  It would keep passing and stop testing the floor.
- A library that `dotnet test --solution` tries to run as a test project, or that ships in a package.
- Making the shared types or the products' internals `public` to get around visibility.
- A test that silently stops running because it moved or lost its discovery.

## The exception: the generator's test classes

`tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/**/*.cs` a second time against Roslyn 4.8.0.  A library cannot replace that: the test classes themselves must be compiled against the older Roslyn.  It stays, and is made visible:

- After the three harness files move out, `tests/SqlSource.Tests/Generator/` holds only test classes.  It stays where it is.
- `tests/SqlSource.Tests/Generator/AGENTS.md` says that every `.cs` file of the folder is also compiled into `tests/SqlSource.Tests.RoslynFloor` by the glob in its `.csproj`, why (the generator must work on the oldest supported Roslyn), and what that asks of a test there: only API that Roslyn 4.8.0 has, C# 12 at most handed to the compiler, and its helpers in `SqlSource.Testing` rather than in another folder of `tests/SqlSource.Tests`.
- Every test file of the folder opens with a comment, after the license header if there is one, in the repository's voice.  Something like:

  ```csharp
  // Shared: tests/SqlSource.Tests.RoslynFloor compiles this file too, against Roslyn 4.8.0.  See AGENTS.md beside it.
  ```

  A new test file there carries the same comment; the folder's `AGENTS.md` says so.

## The two libraries

Two libraries, not one, because of the Roslyn floor.  `src/SqlSource.Tool/SqlSource.Tool.csproj` raises `Microsoft.CodeAnalysis.CSharp` to 5.9.0 with a `VersionOverride`.  A single library holding both the generator harness and the tool's helpers would reference the tool, and through it bring 5.9.0 into `tests/SqlSource.Tests.RoslynFloor`.

### `tests/SqlSource.Testing`

- References `src/SqlSource` and `Microsoft.CodeAnalysis.CSharp` at the centrally pinned version, 4.8.0, with no `VersionOverride`.  A consumer with its own `VersionOverride="5.9.0"` (`SqlSource.Tests`, `SqlSource.Tool.Tests`, `SqlSource.Tests.Postgres`) still resolves 5.9.0; `SqlSource.Tests.RoslynFloor` resolves 4.8.0.
- Holds `Generator/GeneratorHarness.cs`, `Generator/InMemoryAdditionalText.cs`, `Generator/TestOptionsProvider.cs` and `Snapshot/TestSidecars.cs`, in the namespaces `SqlSource.Testing.Generator` and `SqlSource.Testing.Snapshot`.
- Its code uses only API Roslyn 4.8.0 has, as the harness does today.
- Referenced by `SqlSource.Tests`, `SqlSource.Tests.RoslynFloor`, `SqlSource.Tool.Tests` and `SqlSource.Tests.Postgres` (the last two through `SqlSource.Tool.Testing` or directly; builder's choice).

### `tests/SqlSource.Tool.Testing`

- References `src/SqlSource.Tool`, `src/SqlSource` and `SqlSource.Testing`, and `Microsoft.CodeAnalysis.CSharp` with the same `VersionOverride="5.9.0"` the tool's tests use.
- Holds the sixteen helpers of the tool, at its root and in `Fixtures/`, in the namespaces `SqlSource.Tool.Testing` and `SqlSource.Tool.Testing.Fixtures`.
- Referenced by `SqlSource.Tool.Tests` and `SqlSource.Tests.Postgres`.

### What both have in common

- **Visibility stays `internal`.**  The helpers implement and take internal types of the products (`FakeProcessRunner : IProcessRunner`, `CliRun` calling `DescribeCommand.PlanAsync`), so a `public` helper would not compile.  `src/SqlSource/SqlSource.csproj` adds `InternalsVisibleTo` for both libraries and `src/SqlSource.Tool/SqlSource.Tool.csproj` for `SqlSource.Tool.Testing`; each library grants `InternalsVisibleTo` to the test projects that reference it, and `SqlSource.Testing` also to `SqlSource.Tool.Testing`.
- **Class libraries, not test projects.**  No `OutputType` `Exe`, `IsPackable` false, and nothing that makes Microsoft.Testing.Platform treat them as tests, so `dotnet test --solution SqlSource.slnx` skips them.  The helpers need xunit's `TestContext` and `Assert.SkipUnless`, and Shouldly; the libraries take those from xunit v3's library packages (the extensibility core and the assertions, or whatever v3 offers for a non-test library) rather than from `xunit.v3`, which makes a project a test executable.  Each gets the packages it uses, centrally versioned in `Directory.Packages.props`, and a `packages.lock.json`.
- **Under the same analyzers and `.editorconfig` section as the tests.**  `.editorconfig`'s `[tests/**.cs]` already covers `tests/SqlSource.Testing/` and `tests/SqlSource.Tool.Testing/`.
- **In `SqlSource.slnx`**, in the `/tests/` folder.
- **Folders mirror where the files came from, namespaces follow folders**, as everywhere in the repository.  `ImplicitUsings` is disabled, so each consumer file adds the `using` it needs.

## Test data and paths

The data the moved helpers read stays where it is.  `FixtureCases.Root` and `FixtureProjects` read from `AppContext.BaseDirectory`, which is the output folder of the test project that runs them, not of the library, so they keep finding what each test project copies to its output today.  `tests/SqlSource.Tool.Tests` keeps copying `Fixtures/Projects/**`, `Fixtures/Describers/**`, the MSBuild files and `global.json`; `tests/SqlSource.Tests.Postgres` keeps copying `Fixtures/Projects/Single/**`, the MSBuild files and `global.json`, and keeps reading the describer's fixtures from the repository through `RepositoryPaths`.

`Programs.ToolPath` is `SqlSource.Tool.dll` beside the test assembly; the tool's assembly still reaches each consumer's output through the reference chain.  The builder confirms that `TerminationTests` and `ToolProcessTests` still start it.

## Tests of the helpers

The tests that exercise a moved helper, such as `TestProjectTests` and the test of the replay harness in `tests/SqlSource.Tool.Tests/Fixtures/`, stay in `tests/SqlSource.Tool.Tests` and reference the library.  The libraries hold no tests.

## Documentation

In the same change:

- `tests/AGENTS.md`: remove the three bullets about compiled-in files and the `CA1812` and `ProcessGate` explanation; add one bullet on the two libraries (what each holds, why there are two, the Roslyn floor of `SqlSource.Testing`), one on the rule that a test project compiles no `.cs` file of another project except the generator's tests, and point to `tests/SqlSource.Tests/Generator/AGENTS.md`.  Update the paths in the bullet on tests mirroring source folders and in the one about `TerminationTests` and `Programs.StartTool`.
- `CONTRIBUTING.md`: the test projects table and its sentences on what each project compiles; add the two libraries.
- Any other `AGENTS.md`, plan or doc under `docs/` that is a living document and names a moved file by path; dated plans and specs are history and stay.
- The comments in the four test projects' `.csproj` files that describe the links.

## Verification

- `dotnet build SqlSource.slnx` with warnings as errors, and `./pre-commit-validation.sh`.
- The number of tests each project runs, before and after, is the same; every test that passed passes.
- `tests/SqlSource.Tests.RoslynFloor`'s `RoslynVersionTests` passes, and its `packages.lock.json` still resolves `Microsoft.CodeAnalysis.CSharp` 4.8.0.
- No `<Compile Include="../` remains in any `tests/**/*.csproj` but the RoslynFloor glob.
- `dotnet test --solution SqlSource.slnx` does not list either library.
- `tests/SqlSource.Tests.Postgres` passes against the container.

## Left to the builder

- The exact xunit v3 and Shouldly packages for a library, and whether a library or a consumer declares them.
- Whether `SqlSource.Tool.Tests` and `SqlSource.Tests.Postgres` reference `SqlSource.Testing` directly or only through `SqlSource.Tool.Testing`.
- The wording of the header comment and of `Generator/AGENTS.md`, in the repository's voice.
- The order of commits within the one pull request.
