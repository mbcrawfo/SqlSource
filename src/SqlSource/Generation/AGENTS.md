# AGENTS.md - src/SqlSource/Generation

## Generated code

- **The attribute file compiles as C# 7.3; a type's file may use C# 12, and no generated code uses anything newer.**  `AttributeSource.Text` has no `#nullable` directive and no nullable annotation.  Raising the floor in `Generation/Emitting/LanguageSupport.cs` drops consumers and changes the README, as raising the Roslyn pin does.  See [floors](../../../docs/architecture/generator.md#language-and-framework-floors).
- **A compiler warning inside generated code is a bug.**  The driver tests collect them as `GeneratorRun.GeneratedCodeWarnings` and assert that there are none; a new kind of generated code gets a test that does the same.
- **A library's class of conversions is written from the one table that gives its edges**, as `Generation/Converters/NodaTimeConversions.cs` does, so an edge never names a method the class lacks; it names the library's types as text alone, `global::`-qualified.  A new library adds its edges to `Of`, its text to `SourceOf` and its class's name to `ClassMetadataNames` in `Generation/Converters/LibraryConverters.cs`; the CS0436 suppressor reads the last.  See [converters](../../../docs/architecture/generator.md#converters).
- **A `CodeGen` query's only member is its method.**  `TypeEmitter` writes no constant or token method for it, and `Generation/Methods/MethodPlanner.cs` gives no method to one that fails a check, so such a query gets neither; its models are unaffected.  See [query methods](../../../docs/architecture/generator.md#query-methods).

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
