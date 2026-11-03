# `[SqlSourceGenerate]`: one file by default, and `Path` becomes `Source`

## Intent

A type marked `[SqlSourceGenerate]` today claims every `.sql` file in the folder of its C# file.  The common case is one file per type, and two types in one folder collide.  The new default is one file named for the type: `class UserRepository` reads `UserRepository.sql`.  `Path` is renamed `Source`, still takes a folder or a file, and `Source = "."` gives the old behaviour.

This is a breaking change.  Nothing is released, so no user is affected, and nothing keeps `Path` alive: no alias, no obsolete property, no diagnostic for the old name beyond the compiler's own.

## Behaviour

- **Default.** With `Source` not set, or set to `null` or `""`, the type reads `<Name>.sql` from the folder of the C# file that carries the attribute.
  - `<Name>` is the type's own name, `TargetType.Name`: no `@`, no generic arity (`Repo<T>` reads `Repo.sql`), and a nested type's own name (`Outer.Inner` reads `Inner.sql`).
  - The match ignores case, as every path does (`SqlPath.Comparer`).
  - For a partial type, the folder is the one of the file that carries the attribute.
- **`Source`.** It is exactly what `Path` was: relative to the folder of the attribute's file, a value ending in `.sql` is one file and any other value is a folder.  `"."` is that folder.
- **Diagnostics.**
  - SQLSRC005 changes meaning: the default file is missing.  Message: "No '<Name>.sql' beside this file.  Add it, or set Source."  Title along the lines of "Type has no SQL file".
  - SQLSRC004 keeps its meaning, a `Source` that matches nothing, and its message and title say `Source`.
  - SQLSRC208, the tool's "attribute argument not a literal", names `Source`.
  - `docs/manual/diagnostics.md` is updated for all three.

## Approaches

- **A, chosen: the shared resolver owns the default.**  `PathResolver.FindFiles` takes the type's name beside `Source`, and builds `<Name>.sql` when `Source` is not set.  The generator passes `TargetType.Name`.  The tool reads the declaration's identifier into `TypeClaim` and passes it the same way.  The rule lives once, in the function both already share, whose comment says they must not disagree, and `Resolve` still knows whether `Source` was set, so it picks SQLSRC005 or SQLSRC004.
- **B, rejected: each reader fills in the default.**  Both attribute readers turn a missing `Source` into `"<Name>.sql"`.  The rule is written twice, and `Resolve` can no longer tell a default from a set `Source` without a flag on `TargetType`, which takes back most of what B saves.

## Scope of the change

One pull request.

- **Generator.** `AttributeSource`: `PathProperty` becomes `SourceProperty = "Source"`, and the attribute's text renames the property and documents the new default.  `TargetType.Path` becomes `Source`.  `TargetTypeReader` reads `Source`.  `PathResolver.FindFiles` and `Resolve` as in A.
- **Tool.** `AttributeReader` reads `Source` and the type's identifier.  `TypeClaim` gains the name and renames `Path` to `Source`.  `ListedFiles` passes the name to `FindFiles`.
- **Tests.**
  - A test that relies on the folder lookup gets `Source = "."`.
  - A test about the default is rewritten for the new rule.
  - `Path = ...` becomes `Source = ...` everywhere.
  - New cases, in the generator and the tool alike: the default file found, the default file missing (SQLSRC005), a generic type, a nested type, a file that differs in case, `Source = "."`, and two types in one folder each taking its own file.
- **Examples**, both projects, mirrored.
  - `CodeGen/`: `StoreQueries` splits into `UserQueries`, `OrderLineQueries` and `DeliveryQueries`, each with its `.sql` and sidecar renamed to match.  `CreateDataSource` moves to a small static class of its own, since all three use it.
  - `Models/`: keeps one class with two files and `Source = "."`, to show the folder form.  Its comment says so.
  - `Sql/`: `Counts.sql` becomes `CountQueries.sql`.
  - The example READMEs follow.  The sidecars are written again by `tools/describe-end-to-end.sh`.
- **User docs.**
  - `README.md` getting started: `Queries/Users.sql` becomes `Queries/UserRepository.sql`, and the sentence after the type says it reads the file named for it, linking to the manual for a folder or another file.
  - `docs/manual/generator.md`, `settings.md`, `tool.md` and `recipes.md`: every `Path` and every description of the default.
- **Developer docs.**
  - `docs/architecture/generator.md`, `tool.md` and `README.md`.
  - The AGENTS.md rules that name `Path`: `src/SqlSource/AGENTS.md` and `src/SqlSource.Tool/Planning/AGENTS.md`.
  - `tests/DatabaseTests/Postgres/README.md` and `SqlServer/README.md`.
  - `TD-0031`, where it says "reads `Path`".
- **Not touched.** Past plans and specs under `docs/superpowers/` are records, and stay as written.

## Goals, non-goals, anti-goals

- **Goal:** the one-file default, `Source` in place of `Path`, and the generator and tool always agreeing on a type's files.
- **Non-goal:** any other change to how `Source` resolves a file or a folder, or to settings files.
- **Anti-goal:** a default that the tool and the generator resolve differently.  The build would then report missing descriptions for files the tool never described.

## Left to the builder

- The exact wording of the SQLSRC005 title and of the new XML doc comments, in the house style.
- Where the shared `CreateDataSource` class lives in `CodeGen/`, and its name.
- How the generator and tool tests are grouped, following each project's existing layout.
- The version bump, if `VersionPrefix` is not above the latest tag (AGENTS.md, "Pull requests").
