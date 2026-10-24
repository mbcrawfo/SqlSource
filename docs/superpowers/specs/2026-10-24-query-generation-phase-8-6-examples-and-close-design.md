# Query generation, phase 8.6: the examples and close - design

Date: 2026-10-24

Sub-phase 8.6 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It makes both example projects' `CodeGen/` folders show the generated methods and a converter, as a user would write them, reads the user documentation of phase 8 whole, and closes the phase, as [6.7](2026-10-20-query-generation-phase-6-7-examples-and-close-design.md) closed phase 6.

It builds on 8.5.

## Intent

A user learns SqlSource from the README and the examples.  After phase 8 the common path is the methods, not Dapper: the examples must show that path at its simplest, and the documentation, written in pieces by 8.1 to 8.5, must read as one account of it.

## Goals

1. `examples/PostgresExamples/CodeGen/` and `examples/SqlServerExamples/CodeGen/` hold queries of more than one shape, a collection type other than the default, a query with a token, and a column and a parameter converted by a project's converter, over a domain or alias type added to each example's schema, called from a small class that injects a `DbConnection`, by the rules of `examples/AGENTS.md`; their sidecars written by `tools/describe-end-to-end.sh`.
2. The examples' READMEs show the workflow with the methods.
3. The user documentation of phase 8, in the pages the phase map of `docs/manual/AGENTS.md` names for it, read whole and fixed where it reads as written in pieces, is stale, or says a later release for what now works.
4. The phase map's row of phase 8 replaced as a closing phase replaces its own, the epic's phase 8 section given its Settled record of each sub-phase, and the row set to Done.

## Non-goals

- A test of the examples.  They build with the solution and run nothing, by `examples/AGENTS.md`.
- Any change to the methods.  A finding of the reading that is the code's goes to a fix in this pull request when it is small, or to `docs/tech-debt` when it is not, by root `AGENTS.md`.
- Phase 7's scope, which the owner decides before it is specified.

## Anti-goals

- **An example that shows what the manual does not say**, or a workaround the methods made unnecessary.
- **A page that still says "Coming in a later release" for query methods**, or that names a phase, a sub-phase, a spec or an id of the deferred work.

## What exists today

As of the merge of 8.5, which this spec assumes:

- Each example project has `Sql/`, `Models/` and `CodeGen/`; since 8.1, `CodeGen/`'s class calls the generated method of `ListOrderLines -> many` in place of its Dapper call.
- Each sub-phase of phase 8 wrote the user documentation of what it delivered.

## Design

### The examples

In each project's `CodeGen/`, beside `OrderLines.sql`: a query read as `one-optional`, a write as `rowcount`, a `many` with `-- collection-type:`, and a query with a token; a wrapper type with its converters, `EmailAddress` over a domain `email` of `text` on PostgreSQL and an alias type `dbo.Email` of `nvarchar` on SQL Server, given to the users' email column in each schema, mapped by `-- type-override:` in the headers of `CodeGen/`'s files alone, and read and bound by the methods.  A type override keys on a database type, so the domain or alias type is what names the column; the file's header keeps the override out of `Models/`, whose Dapper code then needs no handler.  The class that calls them takes a `DbConnection` in its constructor and passes the caller's `CancellationToken`.  Comments say what each method shows and point at the manual's sections, and nothing more.  The `Models/` folders keep their Dapper code, which is the example of running models through Dapper.

### The reading

The pages of the phase map, read in the order a user meets them: the README, then `docs/manual/README.md`, `concepts.md`, `generator.md`, `sql-files.md`, `parameters-and-tokens.md`, `settings.md`, `recipes.md`, each database page, `diagnostics.md`, `tool.md`; and `src/SqlSource.Tool/README.md`.  Each finding is fixed in this pull request.  The README's FAQ answers the `InvalidCastException` question 8.2 added, and the status line says the methods work on both engines.

### The close

The phase map row of phase 8 in `docs/manual/AGENTS.md` is replaced by what a later phase must keep true of the methods' pages, as 6.7 replaced phase 6's.  The epic outline's phase 8 section gains, under Settled, a line per sub-phase of what its building and review settled, as phases 5 and 6 have, and its rows say Done.  `README.md`'s callout that covers library maps and query methods together keeps the library maps' part alone.

## Testing

The solution builds with the examples; the documentation tests in `tests/SqlSource.Tests/Documentation/` pass, the links of the two packed readmes among them, and the `sql` samples' dialects.

## Documentation

- `examples/PostgresExamples/` and `examples/SqlServerExamples/`, their READMEs, and `examples/AGENTS.md` if a rule changes.
- The pages of the reading above.
- `docs/manual/AGENTS.md`: the phase map's row of phase 8.
- The epic outline: phase 8's Settled record and its rows set to Done.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The PostgreSQL example.
2. The SQL Server example.
3. The reading, and its fixes.
4. The phase map, the outline's Settled record, and the rows set to Done.

## Left to the builder

- The examples' queries and the id type's name, within the goals.
- The wording of each fix the reading makes.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The examples show a converter for a wrapper over a text domain, not a strongly-typed id: a PostgreSQL domain cannot be an identity column, and an override of `int4` would reach every integer.  The override is in `CodeGen/`'s file headers, so `Models/` is untouched.
- The `Models/` folders keep Dapper, as the example of the models alone.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
