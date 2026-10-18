# Query generation, phase 5.5: the PostgreSQL examples - design

Date: 2026-10-18

Sub-phase 5.5 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It adds a folder of example projects, `examples/`, and its first project, `examples/PostgresExamples`: a small project that shows what a user writes and gets for each output, with a Docker Compose file that starts its database.  The owner added it to phase 5's scope.

It builds on [sub-phase 5.4](2026-10-18-query-generation-phase-5-4-end-to-end-design.md), whose script writes its sidecars, and can be built beside sub-phase 5.6.

## Intent

The tests prove SqlSource; they do not show it.  A user who wants to see what a project that uses SqlSource looks like, what its `.sql` files hold, what the generated code is, and how to run the tool, has the README's snippets today and nothing they can open.  The examples are that project: small, representative, and always true, because they build with the solution and break when they stop matching what SqlSource does.

The owner's words: it does not need to be large, just representative examples of the basic SQL mode, model generation, and eventually full query generation; it does not need to do anything, no tests, but it builds as part of the main solution; and the guidance for agents tells them to keep it updated as features change.

## Goals

1. `examples/PostgresExamples/PostgresExamples.csproj`, in `SqlSource.slnx`, built by every build of the solution.
2. A `docker-compose.yml` beside it that starts PostgreSQL with the example's schema and a little data.
3. Representative queries for each output: `Sql`, `Models` and `CodeGen`, with committed sidecars.
4. A `README.md` that says how to start the database, run the tool and see the generated code.
5. Guidance in `AGENTS.md` and `examples/AGENTS.md` that a change to what a user sees updates the examples, and that a phase that delivers a feature adds an example of it.

## Non-goals

- Tests of the examples, or code that runs.  The project is a library that compiles; it is not executed.
- Coverage.  The end-to-end project of 5.4 covers the combinations; the examples show the common path and a few of the settings a user reaches for.
- An example for SQL Server.  Phase 6 adds `examples/SqlServerExamples` by the same pattern.
- Consuming the published package.  Nothing is published before phase 5 closes; the examples reference the generator as the tests do.

## Anti-goals

- **An example that is out of date and still builds.**  A query edited without its sidecar fails the build by the stale-hash error of sub-phase 5.1, and the examples' sidecars are written by the same script as the end-to-end project's.
- **An example that teaches what a user should not do.**  The example's project file, queries and code are what the manual recommends, so that copying them is right.  Anything only the repository needs, the generator referenced by path instead of the package, is marked as such in the project file and the README.

## Design

### The folder

```
examples/
  AGENTS.md
  PostgresExamples/
    PostgresExamples.csproj
    README.md
    docker-compose.yml
    schema.sql
    Sql/        constants and token methods
    Models/     input and output models
    CodeGen/    the default output; models until query methods arrive
```

`SqlSource.slnx` gains a `/examples/` folder with the project.

### The project

A class library on `net10.0`, not packable.  It uses SqlSource as a consumer would, but with the generator referenced as an analyzer and the props and targets imported by path, as `tests/SqlSource.Tests` does, since no package is published; a comment in the project file says that a user writes a `PackageReference` instead, and the README shows that line.  The repository's central package management makes its other `PackageReference`s carry no version, and its `Directory.Build.props` sets what a user's project may not, so the comment and the README say both: a user's project gives each package a version, and the README lists the versions the example builds with.  It sets `SqlSourceDialect` to `postgres`, as the manual's getting started does, and lists `SQLSRC240` in `WarningsNotAsErrors`, as the end-to-end project does, so that raising `VersionPrefix` does not break the solution's build.  It references Npgsql and Dapper, the two packages a user of the models has, so that its models are mapped as a real project's are and its example code compiles against them.

`TreatWarningsAsErrors` and the repository's analyzers apply to it as to every project, so the example code is held to the repository's standard.

### The database

`docker-compose.yml` starts one service, PostgreSQL, on the image the tests pin by default, `postgres:18.6`, with a fixed database name, user and password for local use, a host port that is unlikely to be taken, and `schema.sql` mounted into `/docker-entrypoint-initdb.d/` so that the database is created with the schema and a few rows on its first start.  The README says that the credentials are for the example alone.

The image is one more place that names the default image, so `AGENTS.md`'s rule 5, which pins every test database to a minor version and lists the places that name it, gains `examples/PostgresExamples/docker-compose.yml`, and so does the image section of `tests/SqlSource.Tests.Postgres/README.md`.

`schema.sql` is small: a handful of tables a reader understands at a glance, users, orders and their lines, with a nullable column, an identity, a foreign key, an enum and a timestamp, and the rows for them.

### The queries

A few files in each folder, each claimed by one class whose attribute sets the folder's output, so that each folder reads as one mode:

| Folder | Shows |
|----|----|
| `Sql/` | A query as a constant, and one with a token as a method, with `[SqlSourceGenerate(Output = GeneratorOutput.Sql)]` |
| `Models/` | A query with parameters and rows, its input and output models; a `SELECT *` that gets a table model; a column made not null with `!`; a project-level type override, `timestamptz` to `System.DateTimeOffset`; one record model and one class model |
| `CodeGen/` | A query at the default output, with a result shape on its name marker.  Until phase 8 it gives the models alone, which its file's comment and the README say |

Each folder has a small class that uses what was generated as a user's code would: the constant passed to Dapper in `Sql/`, the models read and passed through Dapper in `Models/`.  It runs nothing; it compiles, so a model that changes shape breaks it.

### The sidecars

The sidecars are committed and written by `tools/describe-end-to-end.sh`, which 5.4 adds, extended to describe the examples too: it applies `schema.sql` to a database of its own on the same container, `examples`, and runs the tool on the example's project with that connection.  So one command refreshes every committed sidecar of the repository.

The README shows a user's way of doing the same with the Compose file: `docker compose up -d`, then `describe` with the connection in `SQLSOURCE_CONNECTION`, which the tool accepts when the run has one database.  Inside the repository the tool is run as a project, `dotnet run --project src/SqlSource.Tool -- describe`, since the repository pins no published `sqlsource` and an older one would write another `toolVersion`; the README says so, marks that command as the repository's, and shows the installed tool's `dotnet sqlsource describe` as what a user of the package runs.

Whether the examples' sidecars are current with the schema is not tested: the owner asked for no tests of the examples, and a query edited without its sidecar already fails the build.  A change to `schema.sql` that forgets the script leaves a sidecar that may differ from what the tool would write, which the next run of the script shows in its diff.

### The README

What the example is, and for each step what to run and what to look at: start the database, run the tool, open a sidecar, build, and find the generated code, with the IDE's view of generated files and `EmitCompilerGeneratedFiles`.  It links to the repository's README and to the manual for everything it does not explain.  It is not a packed readme, so the rules for those two do not apply, but its links to the manual are relative, as the manual's own are.

### The guidance for agents

- **`AGENTS.md`**, rule 4, "Keep the docs current, each in its own place", gains an item for `examples/`: a change to what a user writes, gets or sees updates the examples that show it, in the same pull request, and a phase of query generation that delivers a feature adds an example of it to each example project the feature applies to.  The item says that the examples build with the solution and that their sidecars are written by `tools/describe-end-to-end.sh`.
- **`examples/AGENTS.md`**, new, under 250 lines, rules only: what each example project shows and what it does not; keep each small; one folder per output; the project file uses the generator as the tests do, and its comment says what a user writes instead; the sidecars are written by the script, never by hand; a new database gets a project of its own, `<Engine>Examples`; and the maintenance footer, since it names files.
- **The epic outline**: the rows and scope of phases 6, 7 and 8 name the examples, `SqlServerExamples` in phase 6, the library maps' examples in phase 7, and the `CodeGen` folders' methods in phase 8.
- **`docs/manual/AGENTS.md`**: its "When a phase lands" list gains a step, to update the examples, pointing to the rule in `AGENTS.md`.

## Testing

None of the example's own, as the owner asked.  `./pre-commit-validation.sh` and CI build the solution, and so the examples, with the generator: a build that passes proves the examples' sidecars are current with their queries and their code compiles against what is generated.

## Documentation

- `examples/PostgresExamples/README.md`, as above.
- The repository's `README.md`: a line, in the getting started or "other ways to use it", that links to the example.  It is packed, so the link is an absolute URL, as `AGENTS.md` requires.
- `CONTRIBUTING.md`: one sentence for the project, and the script's sentence says it covers the examples.
- `tools/README.md`: the script's paragraph.
- `docs/manual/README.md`: a "Find what you need" row for "see a complete project".
- The epic outline: the 5.5 row's status.

## Order of work

One pull request, one commit for each step:

1. The epic's 5.5 row says In progress.  The project, empty, in the solution.
2. The schema and the Compose file.
3. The queries, the classes and the sidecars, through the script.
4. The README and the guidance for agents.
5. The documents, and the epic's row set to Done.

## Left to the builder

- The schema, the queries and the example code, within the table above.
- The Compose file's port, database name and credentials.
- The README's wording.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The examples build on 5.4 for its script, so that one command writes every committed sidecar.
- The project references Npgsql and Dapper, and holds a small class per folder that uses what was generated.
- The examples' sidecars are written by the repository's script, while the README shows the Compose file and the tool, the user's way.
- No check that the examples' sidecars are current with the schema.
- The Compose file uses the tests' default image, and so joins rule 5's list.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
