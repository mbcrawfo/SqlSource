# AGENTS.md - src/SqlSource.Tool/Describing/Postgres/Types

- **The rows of `types` reach what reads them only as the `CheckedTypes` that `PostgresSession.CheckTypes` gives.**
- **The types that carry a modifier are `BuiltInTypes.WithModifier`**, and `BuiltInTypesTests` fails for one that `TypeModifiers`, `ColumnFacets` or `PostgresTypeNames` leaves out ([a column's type](../../../../../docs/architecture/postgres-describer.md#a-columns-type)).
- **The facets, `length`, `precision` and `scale`, are decoded from the modifier by `TypeModifiers` and never parsed from the text.**
- **`ColumnFacets.Of` serves only a capture recorded without `typeModifier`**, and reads by the internal name of the reported type and for no other type.
- **A type's `name` is the tool's own spelling (`PostgresTypeNames`), and a modifier is spelled by the server** ([type names](../../../../../docs/architecture/postgres-describer.md#type-names)).  For a type of a schema other than `pg_catalog`, a server text that does not start with `format_type(oid, -1)` is an `InvalidOperationException`, not a name to guess.
- **`bindName` is written on a parameter's top-level type alone**: never on a column's type, on an `element`, a `base` or a `subtype`, and never with a modifier.  `PostgresBindNames` computes the candidate from the catalog rows alone, whatever the `search_path`.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
