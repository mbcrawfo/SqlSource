# AGENTS.md - src/SqlSource

The source generator.  It is loaded into the C# compiler of whoever consumes it, which constrains what the project may target and reference.

- **`netstandard2.0` only.**  The compiler loads Roslyn components built for `netstandard2.0`.  Do not add or change target frameworks in `SqlSource.csproj`.
- **The Roslyn pin is the support floor.**  `Microsoft.CodeAnalysis.CSharp` is pinned to 4.8.0 in `Directory.Packages.props`, the compiler in the .NET 8 SDK and Visual Studio 2022 17.8.  Using an API added after 4.8.0 means raising the pin, which drops supported consumers.  Ask the user before raising it, and update the support target in `README.md` in the same commit.
- **The generator tests run on two Roslyn versions.**  `tests/SqlSource.Tests` overrides the pin to a current version.  `tests/SqlSource.Tests.RoslynFloor` compiles the files of `tests/SqlSource.Tests/Generator/` a second time and runs them on the pinned version.  A test in that folder may therefore use only Roslyn API that 4.8.0 has, and may hand the compiler C# 12 at most.  Tests outside that folder run on the current version only.
- **Language features need runtime support.**  `LangVersion` is `preview`, but `netstandard2.0` lacks the types behind some features.  `Polyfills/IsExternalInit.cs` supplies the one behind `init` accessors and records.  Any other feature (`required` members, for example) needs its own polyfill in `Polyfills/`, declared in the namespace of the type it stands in for; `.editorconfig` turns IDE0130 off for that folder.
- **No collection expression may target `ImmutableArray<T>`.**  The `System.Collections.Immutable` that Roslyn 4.8.0 brings predates support for it (CS9210).  Use `ImmutableArray.Create`, a builder or `ToImmutableArray()`.
- **Models need value equality.**  A type the generator pipeline will cache is a record, and holds its collections in `EquatableArray<T>`.  `ImmutableArray<T>` compares by reference.
- **Performance is a requirement.**  The generator runs inside the compiler and the IDE, again on every edit.  Minimise allocations: allocate nothing per character or per line, copy text in runs, rent buffers from `ArrayPool<T>`, and create a string only where a result needs one.  Track it: `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` holds an allocation budget for a parse.  Lower the budget when the code improves, and never raise it to make a change pass.  A change to a hot path states its time and allocation, before and after, in its pull request.  There is no timing benchmark project; measure with a `Stopwatch` loop over a `Release` build.
- **Do not reference `System.Memory`, `System.Buffers` or `System.Collections.Immutable` directly.**  They arrive with Roslyn 4.8.0 at the versions the compiler host loads, so spans and `ArrayPool<T>` are already available.  A newer version can fail to load in the host.
- **Keep Roslyn references private.**  Package references here use `PrivateAssets="all"` so that nothing flows to the projects that reference the generator.

## The pipeline

`SqlSourceGenerator.Initialize` wires the steps and holds no logic.  The steps are in `Generation/`.

- **Only `Generation/TargetTypeReader.cs` touches symbols and syntax.**  Everything after it works on records.  A value that flows between steps never holds an `ISymbol`, a `SyntaxNode`, a `Compilation`, a `Location` or a `Diagnostic`: they defeat caching and keep a compilation alive.  A position is a `LocationInfo` and a problem is a `DiagnosticInfo`, both in `Diagnostics/`, turned into Roslyn objects in an output step.
- **A collected value needs a `Select` after it.**  `Collect()` gives an `ImmutableArray<T>`, which compares by reference, so the step after it would run on every edit.  Wrap the result in `EquatableArray<T>`, as each `Collect()` in `SqlSourceGenerator` does.
- **Resolving a type's `Path` must not depend on the text of any file.**  It uses the list of paths only.  That is what keeps an edit to one `.sql` file from resolving every type again.
- **A `.sql` file that no type claims is never read.**  Its errors are never reported.  A project can hold migration scripts that are not queries.
- **Paths are compared with `SqlPath.Comparer`, after `SqlPath.Normalize`.**  Never compare a raw path, and never call `System.IO.Path` or touch the file system: analyzer rule RS1035 forbids it, and a path from another operating system would be misread.  The lists of paths and of parsed files are sorted with that comparer, and `SqlSourceGenerator.SelectFiles` and `SqlPath.Contains` rely on it.
- **Every step has a name in `Generation/TrackingNames.cs`.**  `tests/SqlSource.Tests/Generator/CachingTests.cs` reads a step's run reasons by that name.  A change to the pipeline keeps those tests true, or changes them on purpose.
- **Generated code must compile under any `LangVersion` a .NET 8 project can set.**  String values are written with `SymbolDisplay.FormatLiteral`, never as raw string literals.  A generated file starts with `// <auto-generated/>` and `#nullable enable`, and uses `\n` line endings.
- **A query that has tokens is skipped** in `Generation/SqlFileReader.cs`, at a `TODO` comment.  Phase 3 of the SQL queries epic replaces the skip with method emission.

## Diagnostics

Every diagnostic is a `DiagnosticDescriptor` in `Diagnostics/SqlDiagnostics.cs`: an error, tagged `NotConfigurable`.

- **Adding, removing or changing one touches four places:** the descriptor and `SqlDiagnostics.All`; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
- **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
- **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, so a helper method that builds descriptors hides them from it.
- **An id below 100 is about the attributed type, and one from 101 is about a `.sql` file.**  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.

## `Parsing/`

`SqlFileParser.Parse` turns the text of one `.sql` file into named SQL blocks, or into errors.

- **Pure.**  No file access and no generator pipeline types.  Comparisons are ordinal; marker keywords and directive names are ordinal ignoring case.
- **Every span is an offset into the file's text**, including for a problem found in a block's cleaned SQL.  `SqlBlockText.ToSourceSpan` maps such a span back to the file.
- **Quoted regions and hints are copied as written**, apart from line endings.  Trimming and blank-line removal must never reach inside one.
- **Lexemes cover the text without gaps.**  `SqlMarkerReader` and `SqlTextBuilder` depend on that, and on a quoted region or block comment ending with its closing delimiter.
- **Dialect-sensitive choices live in `SqlLexer` only.**  The `Lex_KnownLimit_*` tests pin where it reads SQL differently from some databases, and `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists the cases.  Changing one changes the SQL users get.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
