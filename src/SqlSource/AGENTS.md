# AGENTS.md - src/SqlSource

How the generator works: [`docs/architecture/generator.md`](../../docs/architecture/generator.md).

The generator runs inside its consumers' C# compiler, which limits what the project may target and reference.

- **`netstandard2.0` only.**  The compiler loads Roslyn components built for `netstandard2.0`.  Do not add or change target frameworks in `SqlSource.csproj`.
- **The Roslyn pin is the support floor.**  `Microsoft.CodeAnalysis.CSharp` is pinned to 4.14.0 in `Directory.Packages.props`.  An API added after 4.14.0 means raising the pin, which drops consumers: ask the user first, and update the support target in `README.md` in the same commit.
- **Language features need runtime support.**  One whose types `netstandard2.0` lacks, other than `init` and records (`required` members, say), needs a polyfill beside `Polyfills/IsExternalInit.cs`, in the namespace of the type it stands in for.
- **Existing `ImmutableArray` calls stay.**  `Create`, `Empty`, builders and `ToImmutableArray` become collection expressions only with a measurement (TD-0094), never in bulk.  New code may use one.
- **Models need value equality.**  A type the pipeline caches is a record, and holds its collections in `EquatableArray<T>`, never `ImmutableArray<T>`.
- **Performance is a requirement**, in the compiler and IDE, on every edit.  Allocate nothing per character or line, copy text in runs, rent buffers from `ArrayPool<T>`, and make a string only where a result needs one.  The allocation budgets are tests, and `tests/AGENTS.md` holds their rule.
- **A change to a hot path states its time and allocation, before and after, in its pull request**, from a `Stopwatch` loop over a `Release` build.
- **Do not reference `System.Memory`, `System.Buffers` or `System.Collections.Immutable` directly.**  They come with Roslyn at the versions the compiler host loads; a newer one can fail to load.
- **Keep package references private**, with `PrivateAssets="all"`, so nothing flows to the projects that reference the generator.
- **A switch expression over an enum names every member, with no `_ =>` arm**, so a new member is CS8509 at each switch; nothing catches a discard yet (TD-0096).  Every `DatabaseEngine` decision is one: see [adding an engine](../../docs/architecture/generator.md#adding-a-database-engine).

## The pipeline

`SqlSourceGenerator` wires the steps: `Initialize` calls one method for each stage, in the order of the source outputs, and its only logic is the helpers at its end; everything else is in `Generation/`.  A new step goes in the method of its stage, and a new source output where its order among the others falls.  See [the pipeline](../../docs/architecture/generator.md#the-pipeline).

- **No Roslyn object flows between steps**: never an `ISymbol`, a `SyntaxNode`, a `Compilation`, a `Location` or a `Diagnostic`.  Carry a `LocationInfo` or `DiagnosticInfo`, and make the Roslyn object in an output step.  The one exception is the file's `AdditionalText`, compared by reference, that `FileParseInput` (inside `FileClaim` too) and `SidecarInput` carry into the step that reads it.
- **Only the files that [records between the steps](../../docs/architecture/generator.md#records-between-the-steps) lists touch symbols and syntax.**  The `CompilePaths` step reads the syntax trees' paths and nothing else.
- **Every `Collect()` has a `Select` after it that gives an `EquatableArray<T>`**, a record of them or a value with equality, or the step after runs on every edit.  Two collected chains may share one `Select`, as in `AllFileModels`; `CaseCollisions` and `RepeatedPaths` alone take the listed paths as they are.
- **A type's files depend only on the list of paths and its name**, never on a file's text.
- **A `.sql` file that no type claims is never read**, and its errors are never reported.  Whether one is claimed is its `FileClaim`, so a change to `ClaimedPaths` reads only a file whose answer changed.
- **A settings file's problems are reported only when the chain of the folder of a `.sql` file or a C# file reaches it**, from `ReachedSettingsFiles`, so a broken file above a root file never fails a build.  Only compile paths go through `BuildFolders.ReachingPaths`; a `.sql` path is never left out.  See [which files are checked](../../docs/architecture/generator.md#which-files-are-checked).
- **A sidecar is read only when its `.sql` file is claimed, and no step after `SidecarFile` holds its text.**
- **A later step reads an entry of a sidecar only from `SidecarCheckResult.UsableEntries`**, the entries that passed every check, checked once for each file so that a problem is reported once.
- **Compare paths with `SqlPath.Comparer`, after `SqlPath.Normalize`; find one with `SqlPath.IndexOf`, `Contains` or `FindInFolder`, and ask whether one is under a folder with `SqlPath.IsInFolder`.**  Never compare a raw path, call `System.IO.Path`, touch the file system (RS1035) or search a list with a pass over it.  See [paths](../../docs/architecture/generator.md#paths).
- **The lists of paths and of parsed files stay distinct ignoring case.**  A later file whose path differs only by case is left out, and `Generation/Paths/PathCollision.cs`, which alone compares normalised paths ordinally, reports it as `SQLSRC013` when a type claims it.
- **The `sqlsource` tool plans a run with this code.**  A change to a member that `src/SqlSource.Tool/Planning/` calls ([the list](../../docs/architecture/generator.md#the-tool-runs-the-same-code)) changes what the tool describes; `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs` compares the two.
- **A step whose re-runs matter has a name in `Generation/TrackingNames.cs` and a test in the `Caching*Tests` class of its step**, in `tests/SqlSource.Tests/Generator/`.  A small `Select` between two named steps may go unnamed.  `RepeatedPaths` and `AmbiguousHintNames`, almost always empty, need no test.  A change to the pipeline keeps those tests true, or changes them on purpose.
- **A file's check, types, models' properties and models run from its `ParsedFile` node**, a model in the slot of its query and role, never from a `SelectMany` of `ParsedFiles` or of the sorted `Models`; a path in `RepeatedPaths` runs from the collected `ParsedFiles` or `ModelsByFile.OfRepeatedPaths`.  See [the fan-out by file](../../docs/architecture/generator.md#the-fan-out-by-file).
- **Nothing a user can write may make the generator throw.**  An exception drops all of the generator's output (CS8785).
- **A type's methods are planned from its own slice of the project, its `MethodsInput`, and `TypeOutput`'s inputs are never widened for the methods**, so that one type's plan runs again only when something of it changes, and a sidecar's edit writes no type's constants again.
- **The converters reach the methods alone.**  `ConverterSet` is an input of `MethodsInput`, never of `TypeOutput` or of a step of the types or models, and `MappedTypeConverters` stays apart from `MappedTypes`.  See [converters](../../docs/architecture/generator.md#converters).
- **The dialect is resolved for each file before the parse**, by `Generation/FileParseInput.cs` from the chain of the file's folder, as an input of the parse.  See [the dialect](../../docs/architecture/generator.md#how-the-dialect-reaches-the-parse).
- **The build shows the compiler three properties, `SqlSourceBuildFolders`, `SqlSourceTransitivePackageAssemblies` and `SqlSourcePublicExceptions`, and no item metadata; a query's setting is never a property or metadata of the build.**
- **Only stage one of the settings reaches the parse.**  A setting of stage two is never an input of `SqlFileReader.Read`, or a change to it would parse every file again.  See [the two stages](../../docs/architecture/generator.md#the-settings-have-two-stages).
- **Stage two reads a file's settings from its `FileLevel`, the chain of its folder**, and a type's nested class from the chain of its C# file's folder, in `NestedClassCheck`, never from one settings file alone.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
