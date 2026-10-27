# AGENTS.md - tests/SqlSource.Tests/Generator

The generator's driver tests.  This folder is the one place where a test project compiles files of another.

- **Every `.cs` file here is compiled twice.**  `tests/SqlSource.Tests` compiles it against a current Roslyn, and `tests/SqlSource.Tests.RoslynFloor` compiles it again, by the glob `../SqlSource.Tests/Generator/**/*.cs` in its `.csproj`, against Roslyn 4.8.0, the oldest the generator supports.  A library cannot do this: the test classes themselves must be compiled against the older Roslyn.  A test here uses only API that Roslyn 4.8.0 has, hands the compiler C# 12 at most (a test that needs a newer C# goes in the folder of the type it tests, outside this one, as `MethodWriterTests`'s awkward names at `LanguageVersion.Latest`), and uses only the packages that `tests/SqlSource.Tests.RoslynFloor/SqlSource.Tests.RoslynFloor.csproj` references, so not `Bogus` or `JsonSchema.Net`.  `tests/SqlSource.Tests.RoslynFloor/RoslynVersionTests.cs` fails if that project resolves another Roslyn.
- **Only test classes live here.**  The harness they run on, `GeneratorHarness` (which writes the run's project settings file, with `GeneratorRun.ShouldBeClean` and `GeneratedFile`), `InMemoryAdditionalText`, `LevelSettings` (a setting at each of its levels, the project's settings file, a folder's, the attribute, a header marker and a query's marker), `ModelRuns` (the harness of the model tests), `MethodRuns` (that of the method tests), `CachingScene` and `StepReasons` (the fixture, whose runs read the project's settings file, and the readers of the `Caching*Tests`), `NpgsqlReferences`, `SqlClientReferences`, `Sidecars`, `StepOutputs` and `TestOptionsProvider` (the properties MSBuild tells the generator, of which the generator reads `SqlSourceBuildFolders` alone), is in `tests/SqlSource.Testing/Generator/`, which both projects reference and which stays within Roslyn 4.8.0.  A helper a test here needs goes there, never into this folder or another folder of `tests/SqlSource.Tests`, which RoslynFloor does not compile.
- **Every file starts with the same line:**

  ```csharp
  // Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.
  ```

  A new file starts with it too.  `tests/SqlSource.Tests/TestLayoutTests.cs` checks that every file here has it and that every file's name ends in `Tests.cs`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
