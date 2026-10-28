# AGENTS.md - tests/SqlSource.Tests/Generator

- **Every `.cs` file here is compiled twice.**  `tests/SqlSource.Tests` compiles it against a current Roslyn, and `tests/SqlSource.Tests.RoslynFloor` compiles it again, by the glob `../SqlSource.Tests/Generator/**/*.cs` in its `.csproj`, against Roslyn 4.8.0, the oldest the generator supports.  A library cannot do this: the test classes themselves must be compiled against the older Roslyn.  A test here uses only API that Roslyn 4.8.0 has, hands the compiler C# 12 at most (a test that needs a newer C# goes in the folder of the type it tests, outside this one, as `MethodWriterTests`'s awkward names at `LanguageVersion.Latest`), and uses only the packages that `tests/SqlSource.Tests.RoslynFloor/SqlSource.Tests.RoslynFloor.csproj` references, so not `Bogus` or `JsonSchema.Net`.  `tests/SqlSource.Tests.RoslynFloor/RoslynVersionTests.cs` fails if that project resolves another Roslyn.
- **Only test classes live here.**  The harness they run on, `GeneratorHarness`, `InMemoryAdditionalText`, `LevelSettings`, `ModelRuns`, `MethodRuns`, `CachingScene`, `StepReasons`, `NpgsqlReferences`, `SqlClientReferences`, `Sidecars`, `StepOutputs` and `TestOptionsProvider`, is in `tests/SqlSource.Testing/Generator/`, which both projects reference and which stays within Roslyn 4.8.0.  A helper a test here needs goes there, never into this folder or another folder of `tests/SqlSource.Tests`, which RoslynFloor does not compile.
- **Every file starts with the same line:**

  ```csharp
  // Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.
  ```

  `tests/SqlSource.Tests/TestLayoutTests.cs` checks that every file here has it and that every file's name ends in `Tests.cs`.
- **The `Caching*Tests` classes make the generator's step names mean something.**  They read each step's run reasons by its name in `TrackingNames`, through `StepReasons` of `tests/SqlSource.Testing/Generator/`, whose `AllReasons` fails a test on a step that has lost its name; a new step that does real work gets a case in the class of its step.  `CachingScene` gives every run the project's settings file, an additional text of the driver, and MSBuild tells the generator nothing; a test that swaps the options for others changes `SqlSourceBuildFolders` alone.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
