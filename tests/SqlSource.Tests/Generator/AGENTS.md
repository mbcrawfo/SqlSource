# AGENTS.md - tests/SqlSource.Tests/Generator

The generator's driver tests.  This folder is the one place where a test project compiles files of another.

- **Every `.cs` file here is compiled twice.**  `tests/SqlSource.Tests` compiles it against a current Roslyn, and `tests/SqlSource.Tests.RoslynFloor` compiles it again, by the glob `../SqlSource.Tests/Generator/**/*.cs` in its `.csproj`, against Roslyn 4.8.0, the oldest the generator supports.  A library cannot do this: the test classes themselves must be compiled against the older Roslyn.  A test here uses only API that Roslyn 4.8.0 has, and hands the compiler C# 12 at most.  `tests/SqlSource.Tests.RoslynFloor/RoslynVersionTests.cs` fails if that project resolves another Roslyn.
- **Only test classes live here.**  The harness they run on, `GeneratorHarness`, `InMemoryAdditionalText` and `TestOptionsProvider`, is in `tests/SqlSource.Testing/Generator/`, which both projects reference and which stays within Roslyn 4.8.0.  A helper a test here needs goes there, never into this folder or another folder of `tests/SqlSource.Tests`, which RoslynFloor does not compile.
- **Every run of the generator states its output.**  The harness's `OutputKind?` parameter, on `GeneratorHarness`'s entry points and on `TestOptionsProvider`, has no default, and neither does a helper of a test class that runs the generator: the test passes it.  `OutputKind.Sql` is the norm.  `null` leaves `SqlSourceOutput` unset, which is the default `CodeGen`, and is for a test of the default or of the output's levels, which says so in a comment.  `Microsoft.CodeAnalysis` has an `OutputKind` too, so a file that imports it names the generator's with `using OutputKind = SqlSource.Settings.OutputKind;`.
- **Every file starts with the same line:**

  ```csharp
  // Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.
  ```

  A new file starts with it too.  `tests/SqlSource.Tests/TestLayoutTests.cs` checks that every file here has it and that every file's name ends in `Tests.cs`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
