# AGENTS.md - src/SqlSource

The source generator.  It is loaded into the C# compiler of whoever consumes it, which constrains what the project may target and reference.

- **`netstandard2.0` only.**  The compiler loads Roslyn components built for `netstandard2.0`.  Do not add or change target frameworks in `SqlSource.csproj`.
- **The Roslyn pin is the support floor.**  `Microsoft.CodeAnalysis.CSharp` is pinned to 4.8.0 in `Directory.Packages.props`, the compiler in the .NET 8 SDK and Visual Studio 2022 17.8.  Using an API added after 4.8.0 means raising the pin, which drops supported consumers.  Ask the user before raising it, and update the support target in `README.md` in the same commit.
- **Tests run on a newer Roslyn.**  `tests/SqlSource.Tests` overrides the pin to a current version, so a test passing does not prove the code works on a 4.8 host.  See `docs/tech-debt/TD-0001-generator-not-run-on-roslyn-floor.md`.
- **Language features need runtime support.**  `LangVersion` is `preview`, but `netstandard2.0` lacks the types behind some features.  `Polyfills/IsExternalInit.cs` supplies the one behind `init` accessors and records.  Any other feature (`required` members, for example) needs its own polyfill in `Polyfills/`, declared in the namespace of the type it stands in for; `.editorconfig` turns IDE0130 off for that folder.
- **No collection expression may target `ImmutableArray<T>`.**  The `System.Collections.Immutable` that Roslyn 4.8.0 brings predates support for it (CS9210).  Use `ImmutableArray.Create`, a builder or `ToImmutableArray()`.
- **Models need value equality.**  A type the generator pipeline will cache is a record, and holds its collections in `EquatableArray<T>`.  `ImmutableArray<T>` compares by reference.
- **Keep Roslyn references private.**  Package references here use `PrivateAssets="all"` so that nothing flows to the projects that reference the generator.

## `Parsing/`

`SqlFileParser.Parse` turns the text of one `.sql` file into named SQL blocks, or into errors.

- **Pure.**  No file access and no generator pipeline types.  Comparisons are ordinal; marker keywords and directive names are ordinal ignoring case.
- **Every span is an offset into the file's text**, including for a problem found in a block's cleaned SQL.  `SqlBlockText.ToSourceSpan` maps such a span back to the file.
- **Lexemes cover the text without gaps.**  `SqlMarkerReader` and `SqlTextBuilder` depend on that, and on a quoted region or block comment ending with its closing delimiter.
- **Dialect-sensitive choices live in `SqlLexer` only.**  The `Lex_KnownLimit_*` tests pin where it reads SQL differently from some databases.  Changing one changes the SQL users get.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
