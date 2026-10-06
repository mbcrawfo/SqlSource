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

## `Parsing/`

`SqlFileParser.Parse` turns the text of one `.sql` file into named SQL blocks, or into errors.

- **Pure.**  No file access and no generator pipeline types.  Comparisons are ordinal; marker keywords and directive names are ordinal ignoring case.
- **Every span is an offset into the file's text**, including for a problem found in a block's cleaned SQL.  `SqlBlockText.ToSourceSpan` maps such a span back to the file.
- **Quoted regions and hints are copied as written**, apart from line endings.  Trimming and blank-line removal must never reach inside one.
- **Lexemes cover the text without gaps.**  `SqlMarkerReader` and `SqlTextBuilder` depend on that, and on a quoted region or block comment ending with its closing delimiter.
- **Dialect-sensitive choices live in `SqlLexer` only.**  The `Lex_KnownLimit_*` tests pin where it reads SQL differently from some databases, and `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists the cases.  Changing one changes the SQL users get.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
