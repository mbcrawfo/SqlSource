# AGENTS.md - src/SqlSource/Diagnostics

- **Every diagnostic is an error tagged `NotConfigurable` unless its descriptor says why a project may configure it, as a warning or as information; `SqlDiagnosticsTests` lists those by id.**  Each takes its category and the start of its help link from `SqlDiagnostics.Category` and `SqlDiagnostics.HelpLinkBase`.
- **Adding, removing or changing one touches four places:** the descriptor and the `All` of its class; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/manual/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
- **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
- **`SQLSRC901` is a suppression, not a diagnostic.**  Its `SuppressionDescriptor` is not in `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md` or a section of `docs/manual/diagnostics.md`.  `Diagnostics/AttributeConflictSuppressor.cs` suppresses nothing but the types of `AttributeSource.GeneratedTypes` declared in the project being compiled; `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` holds both halves.
- **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, and a helper that builds descriptors hides them from it.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
