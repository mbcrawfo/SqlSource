# AGENTS.md - src/SqlSource/Generation/Placement

See [placement](../../../../docs/architecture/generator.md#placement).

- **`TypeEmitter` and `MethodPlanner` read where a member goes only from `TypePlacement`, through `UsableQueries.Of`**, never from a query's settings, so that the type's file and its methods' file agree on the files used and on each nested class's accessibility.
- **Only `TypeEmitter` passes `TypePlacement.Of` a builder for its diagnostics**; every other caller passes null, so each problem is reported once.
- **The order of the checks in `TypePlacement.Of` decides what a user sees**: a file left out by one check is not seen by the later ones.  A new check goes in the order its summary lists, and changes that summary and the architecture's list.
- **Only a written visibility is checked.**  A default must fit its location, so `PlacementChecks.DefaultVisibility` never gives what `PlacementChecks.VisibilityReason` refuses.
- **`protected` is refused only where C# forbids it**, CS1057 and CS0666; on a sealed type it is written, and both files start with `TypeEmitter.ProtectedPragma`.
- **A nested class is opened only by `TypeEmitter.OpenNestedClass`**, as `static partial` with one accessibility, so the two files' parts of it merge.
- **`PlacementChecks.KeywordOf` and `TextOf` spell each value as a marker writes it**, since the messages quote them and tell a user what to write.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
