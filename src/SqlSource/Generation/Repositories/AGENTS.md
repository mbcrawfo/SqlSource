# AGENTS.md - src/SqlSource/Generation/Repositories

- **No `ISymbol` or syntax node leaves `RepositoryShapeReader`; `ConnectionCandidateReader` is its helper alone.**  The step gives `RepositoryShape` records, compared by value, so that the steps after it stay cached.  See [records between the steps](../../../../docs/architecture/generator.md#records-between-the-steps).
- **`RepositoryShapes` reads every type on every compilation, from the collected `TargetTypes`, never inside `ForAttributeWithMetadataName`.**  That transform runs again only for a changed file, and a base type's member or a type of the interface's name may be in another one; `CachingRepositoryTests` holds it.
- **The reader filters the candidates: only the marked ones when any is, the primary constructor's parameters only when no field or property is one, and of those only the ones that no base-type argument or initializer reads**, which `HeldParameters` names for `SQLSRC028`.  A step after it takes `Candidates` as the conventions' answer and filters nothing again.
- **`DbConnection`, `DbDataSource`, `Task`, `ValueTask` and `CancellationToken` are matched by name and namespace, never by a type's display, which carries a nullable annotation's `?`.**  The attribute is matched by its name, then `AttributeSource.ConnectionAttributeMetadataName`, never by a symbol the compilation looks up, so that a marked member of a base type in a referenced assembly matches too.
- **`ConnectionResolver`'s diagnostics go into `TypeMethods.Diagnostics`, never `TargetType.Diagnostics`,** which would cost the type its other files.  See [the plan](../../../../docs/architecture/generator.md#query-methods).
- **A type with no repository query is never resolved and takes no `RepositoryShape` into its `MethodsInput`**, so its marked members are never checked and a change to its candidates plans none of its methods again.
- **Compare database names with `DatabaseName.Comparer`**, as the tool does: two names that differ only in case are one database.

> Maintenance: this file names specific files, types and members.  If you rename, move, or remove them, update this file in the same commit.
