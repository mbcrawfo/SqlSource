# AGENTS.md - tests

[`README.md`](README.md) says what each test project and library is, which Roslyn it runs on and how they reference each other.

## Projects and libraries

- **A test project compiles no `.cs` file of another project, with one exception.**  Code that two test projects need is in one of the libraries, which they reference.  The exception is `tests/SqlSource.Tests/Generator/`, whose test classes `tests/SqlSource.Tests.RoslynFloor` compiles a second time against Roslyn 4.8.0.  `tests/SqlSource.Tests/TestLayoutTests.cs` checks this.
- **The three libraries, `tests/SqlSource.Testing`, `tests/DatabaseTests/TestHarness` and `tests/SqlSource.Tool.Testing`, hold no tests.**  A helper that one test project alone uses stays in that project; one that a second project comes to need moves into a library.
- **A library references only what every project that references it can take** ([why](README.md#the-projects)): `SqlSource.Testing` stays on the pinned Roslyn and references nothing of the tool, `TestHarness` brings no database driver and nothing of the tool, and `Tool.Testing` references the tool.
- **A test reads the repository only through `RepositoryRoot.Path`**, never by building a path from the root, and `RepositoryRoot.WorkingFolder` is only for starting a process in the root, since `Path` is what refuses a read that `tools/test-scopes.txt` would let a change skip ([when it refuses](README.md#reading-the-repository)).
- **The libraries' types stay `internal`**, except `SettingLevel` of `tests/SqlSource.Testing`, which a public theory takes as its row, and the harness's `PostgresContainer` and `SqlServerContainer`, which the projects' fixtures subclass.
- **A test reads a generated model by reflection through `ModelReflection` of `tests/DatabaseTests/TestHarness`**, which orders the properties as they are declared, since `Type.GetProperties()` does not promise that order.
- **Every test that opens a describer opens it through `Describers.OpenAsync` of `tests/SqlSource.Tool.Testing`** (the tool's tests, `PostgresScript`, `SqlServerScript` and `FixtureReplay` among them), and none builds an `OpenRequest` but to look at it.

## Where a test goes

- **Tests mirror the source folders.**  A test of `src/SqlSource/<Folder>/<Type>.cs` is `tests/SqlSource.Tests/<Folder>/<Type>Tests.cs`, and a test of the tool's `<Folder>/` is in `tests/SqlSource.Tool.Tests/<Folder>/`.  Namespaces follow folders, so a moved test changes its namespace.
- **The driver tests of `tests/SqlSource.Tests/Generator/` stay there**, though some, such as `ModelTests` and `SidecarTests`, are named after types of other folders, since RoslynFloor compiles that folder alone.
- **Classes split from one share their cases through a static helper, and no base class**, as `SqlFileParser*` of `tests/SqlSource.Tests/Parsing/` share `ParseOf`.

## Running the generator

- **Every test that runs the generator or its emitter states its output.**  It passes an `OutputKind` to the harness's `OutputKind?` parameter, which has no default, nor has a helper of a test class that runs the generator: `Sql`, unless the test is of what an output that needs entries does, as the tests of sidecars and of their caching are.
- **A test passes `null` for the output only as the comment on `GeneratorHarness.Run` allows**, and says why in a comment.
- **`Microsoft.CodeAnalysis` has an `OutputKind` too**: a file that imports it names the generator's with `using OutputKind = SqlSource.Settings.OutputKind;`.
- **A test gives a setting in `settings:`, a folder's settings file or a marker, or in `TestProject.Settings` or an `AddSettings` file, never in a property or metadata, which neither the generator nor the tool reads.**  `SqlSourceBuildFolders` is a build input, not a setting, which any test may set; the rule holds even for a test that would show that such a property is not read.

## Programs, time and waits

- **A test starts a program through `Programs.RunAsync`, never `Process.Start`**, and starts `dotnet` as `Programs.Dotnet`, the one that the tool runs, never as `"dotnet"`.
- **A test that waits for what a program or the server does waits through `Eventually`**, whose `Patience` is the one limit, which `ProcessGate` and every caller of `Programs.RunAsync` use too, and not with a loop of `Task.Delay`.
- **Only one test of a project starts the built tool as a process**: `Commands/TerminationTests.cs` of `tests/SqlSource.Tool.Tests`, and `Describer/ToolProcessTests.cs` of each container project, through `ToolProcess.ShouldDescribeOneQueryAsync`.  Each starts it through `Programs` of `tests/SqlSource.Tool.Testing`: `Programs.ToolStart`, or for the closed console `cmd /c start` with `Programs.ToolPath` and `Programs.SetToolEnvironment` ([why](SqlSource.Tool.Testing/Programs.cs)).
- **A run, a host and a session of the tests stand on `TestClock` of `tests/SqlSource.Tool.Testing`**, which stands still until a test moves it, unless the test brings a clock: a test that needs the machine's passes `TimeProvider.System` and says why.

## Flaky tests

- **A test holds on CI's busy runner and under coverage, not only on a quiet machine.**  Each flaky failure so far assumed that nothing else acts during the test.
  - Never let a timer keep a command running or decide an order: SQL Server ends a `WAITFOR` early when it runs short of threads.  Hold the command on a row lock or an application lock, and wait for the state through `Eventually` or `ProcessGate` before acting, as `RunTimeArgumentTests` and `SessionTests` of `tests/DatabaseTests/SqlServer` do.
  - Measure synchronous code's allocations with `GC.GetAllocatedBytesForCurrentThread`, which coverage and other threads leave alone.  A whole-process measure, `GC.GetTotalAllocatedBytes`, goes through `CallAllocations`, which skips under coverage or a profiler, in `tests/DatabaseTests/QueryMethodAllocation`, which runs serially and only in the nightly ([why](DatabaseTests/QueryMethodAllocation/CallAllocations.cs)).
  - A threshold or margin set from what a test measures is measured again as the job that runs it does: CI's `build` job with the whole solution at once and with `--coverage`, or an allocation test's nightly job, alone and without it, as well as alone, and its comment says how.

## Allocation budgets

- **Allocation budgets are tests.**  `Parsing/SqlFileParserAllocationTests.cs`, `Generation/Paths/PathResolverAllocationTests.cs` and `Snapshot/Reading/SidecarReaderAllocationTests.cs` in `tests/SqlSource.Tests` hold a number of bytes, and `ChainOf_PathFarBelowItsNearestFile_AllocatesNothing` of `Generation/Chains/SettingsFileSetTests.cs` holds none.  Lower one when the code improves; never raise one to make a change pass.

## Documents

- **`tests/SqlSource.Tests/Documentation/` checks the links, anchors and SQL samples of the documents a user reads, and the limits of every `AGENTS.md`.**

## Style and CI

- **Assertions are Shouldly's**, and test names are `Method_Scenario_Outcome`.  `.editorconfig` turns the analyzer rules off that this needs, under `[tests/**.cs]`.
- **`Method` is what the test calls**: the C# method in full, `DescribeAsync_` and never `Describe_`, or the command or statement it runs, as the `Describe_` of `DescribeCommandTests`, which runs `describe`, and the `Explain_` of `PlanOutputTests`, which runs the server's `EXPLAIN`.  A test of a value names the value as `Method` and what it is read under as `Scenario`, as `Patience_OfEveryWait_IsTwoMinutes`.
- **A test that needs another current culture takes it from `CultureScope` of `tests/SqlSource.Testing`**, which puts back the one it found.
- **A unit-test class and a container class may share a name**, as the two `NpgsqlCheckTests` do: their folders tell them apart.
- **Every test class is `sealed`**, a `partial` one `public sealed partial class`.
- **`.github/workflows/nightly.yml` names each test project in the Test step of its `test` job.**  A new test project that needs no container is added there, or it never runs on Windows or macOS.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
