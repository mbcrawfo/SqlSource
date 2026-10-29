# AGENTS.md - src/SqlSource/Diagnostics

The generator's are in `Diagnostics/SqlDiagnostics.cs`, and the errors of the `sqlsource` tool that are not the generator's own in `Diagnostics/ToolDiagnostics.cs`, two of which, `SQLSRC209` and `SQLSRC210`, the generator reports too.  See [diagnostics](../../../docs/architecture/generator.md#diagnostics).

- **Every diagnostic is an error tagged `NotConfigurable` unless its descriptor says why a project may configure it, as a warning or as information; `SqlDiagnosticsTests` lists those by id.**  Each takes its category and the start of its help link from `SqlDiagnostics.Category` and `SqlDiagnostics.HelpLinkBase`.
- **Adding, removing or changing one touches four places:** the descriptor and the `All` of its class; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/manual/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
- **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
- **`SQLSRC901` is a suppression, not a diagnostic.**  Its `SuppressionDescriptor` is not in `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md` or a section of `docs/manual/diagnostics.md`.  `Diagnostics/AttributeConflictSuppressor.cs` suppresses nothing but the types of `AttributeSource.GeneratedTypes` declared in the project being compiled; `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` holds both halves.
- **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, and a helper that builds descriptors hides them from it.
- **`Diagnostics/SettingsFileDiagnostics.cs` is the one place a `SettingsFileError` becomes a diagnostic**, `SQLSRC015` to `SQLSRC020`, at the line and column of its span, for the build and the tool alike.
- **An id below 100 is about the type or the project, one from 101 about a `.sql` file, and one from 200 about the snapshot or the tool** (`tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` holds the tool's).
- **`SQLSRC200` to `SQLSRC280` and `SQLSRC284` are taken**, and phase 7 takes `SQLSRC281` to `SQLSRC283`, which its spec, `docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md`, assigns.
- **Add a new `SqlParseErrorKind` at the end**: the parser ids follow its order.

## Diagnostics with no position

- **`SQLSRC247`, `SQLSRC248`, `SQLSRC267`, `SQLSRC268` and `SQLSRC273` have no position**, so they are reported with `Location.None` in an output step and never travel as a `DiagnosticInfo`.
- **`SQLSRC247`, `SQLSRC248`, `SQLSRC267` and `SQLSRC268` are reported once for the project**, each pair by its own driver's probe and its own engine's `UsesPostgresEntries` or `UsesSqlServerEntries`, independently of the other.
- **`SQLSRC273` is reported once for each engine** that the collected `CodeGenEngines` of the types' methods holds and whose probe finds no driver.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
