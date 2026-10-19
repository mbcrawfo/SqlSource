# Examples

Each project here shows a user what a project that uses SqlSource looks like.  It builds with `SqlSource.slnx`, so a build that passes proves its sidecars are current with its queries and its code compiles against what is generated.

## What an example is

1. **Small and representative.**  A few queries in each folder show the common path and a few of the settings a user reaches for.  Coverage belongs in the end-to-end projects under `tests/`, never here.
2. **No tests, and nothing runs.**  The project is a library that compiles.  Its classes call the generated code as a user's code would, so a model that changes shape breaks the build.
3. **Only what the manual recommends.**  The project file, the queries and the code are what a user may copy.  Follow the manual's recipes, `docs/manual/recipes.md`: with Dapper, parameters in camelCase, a `-- column:` marker and not a `!` or `?` on an alias, a class model for a query with an array column, and a type handler for a type override that Npgsql does not give.
4. **One folder per output**, `Sql/`, `Models/` and `CodeGen/`, each with one class whose `[SqlSourceGenerate]` attribute sets the folder's output and claims the folder's `.sql` files.  `CodeGen/` uses the default output.
5. **Anything only the repository needs is marked.**  The project file references the generator as `tests/SqlSource.Tests.Postgres` does, imports the package's props and targets by path, and keeps `SQLSRC240` a warning; its comments and the README say what a user writes instead.

## Keeping them current

1. **A change to what a user writes, gets or sees updates the examples that show it**, in the same pull request, by rule 4 of the root `AGENTS.md`.  A phase that delivers a feature adds an example of it to each project the feature applies to.
2. **The sidecars are written by `tools/describe-end-to-end.sh`, never by hand.**  Run it after a change to a query or to a project's `schema.sql`, and read its diff.
3. **`schema.sql` names no owner, role or database.**  The Compose file runs it as its own user in its own database, and the script as `postgres` in a database of its own, and both must give the sidecars the same answer.
4. **The Compose file's image is the tests' default image**, `PostgresContainer.DefaultImage`, and moves with it by rule 5 of the root `AGENTS.md`.
5. **The README lists the versions of Dapper and Npgsql** that `Directory.Packages.props` pins.  A change to either updates it.
6. **The README's links to the manual are relative.**  It is not a packed readme.

## Adding a database

A new database gets a project of its own, `examples/<Engine>Examples`, by the pattern of `PostgresExamples`: its Compose file and `schema.sql`, the three folders, a README, an entry in `SqlSource.slnx` under `/examples/`, and its sidecars written by the same script.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
