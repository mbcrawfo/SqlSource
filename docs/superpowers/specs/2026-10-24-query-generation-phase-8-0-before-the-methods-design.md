# Query generation, phase 8.0: before the methods - design

Date: 2026-10-24

Sub-phase 8.0 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It does the two things phase 8 should not start without: it resolves the one item of tech debt the methods build on, `TD-0012`, and it records in a spike what the two drivers do with the calls the generated methods will make, as [phase 6's spike](2026-10-20-query-generation-phase-6-sql-server-spike.md) did for the describer.

It builds on nothing.  Sub-phase 8.1 builds on it.

## Intent

The owner asked that tech debt and deferred work that should come before phase 8 be done first.  The review of every open item found one: `TD-0012`, the shapes of token method that are compiled in tests and never run.  A `CodeGen` query with tokens builds its SQL inside its method by the code that writes the token methods today, so a break in that code would reach the methods unseen.  The rest of the review is recorded in the outline's phase 8 section, under Settled.

The spike is this sub-phase's other half.  The epic and the specs after it state facts about the drivers that phase 8 relies on and that nothing in the repository proves: what `GetFieldValue<T>` does on a `NULL`, how a parameter's name is matched, what `ExecuteNonQuery` counts.  8.1 to 8.4 design against them; each that is wrong would be found only when 8.3 or 8.4 runs, after the generator code is written.  The spike finds them first, at the cost of a document.

## Goals

1. `TD-0012` resolved by its proposed fix: a token-only query and a query of eight tokens are run in `tests/SqlSource.Tests/EndToEnd/` and their returned strings asserted, and the awkward token names are compiled at `LanguageVersion.Latest` on the current Roslyn, `extension` among them.  Its row and file are removed.
2. A spike document, `docs/superpowers/specs/2026-10-24-query-generation-phase-8-spike.md`, that answers each question below on PostgreSQL 14 and 18 with Npgsql 8 and 10, and on SQL Server 2017 and 2025 with SqlClient 6.1 and 7, with the code that answered it summarised and the answer stated plainly.
3. The epic outline's phase 8 section and the sub-phase specs amended where a finding changes a detail they give, in the same pull request, since none is built yet.  A finding that contradicts a decision, one the outline lists under Decided, the not-null read with no `IsDBNull` and the connection rule among them, is not amended by the builder: the pull request stops at the spike's document and asks the owner, by the outline's rule for changing a decision, and amends what the owner decides.

## Non-goals

- Any generator code for the methods.  Sub-phase 8.1.
- Committing the spike's code.  It is throwaway, as phase 6's was; what it found is the document.
- Any other item of tech debt.  The review found none that phase 8 needs first; the performance items `TD-0013` and `TD-0069` stay as they are, and 8.1's caching tests cover the steps it adds.

## Anti-goals

- **A finding stated from documentation alone.**  Each answer is what the code did against the container and the driver, on each version named; where versions differ, the document says which does what.
- **A question left open.**  An answer the spike cannot reach says so and names the sub-phase that must reach it first.

## What exists today

As of commit `0fb40716`, which this spec assumes:

- `TD-0012` is open, with its proposed fix: `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql` and `EndToEndTests.cs`, and `Run_TokenWithAnAwkwardName_Compiles` in `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`, which runs on Roslyn 4.8.0 too and so hands the compiler C# 12.
- The test projects reference Npgsql 10 and SqlClient 7, and the floor projects Npgsql 8.0.9 and SqlClient 6.1.7.  `DefaultImages` and the floor images are as root `AGENTS.md` rule 5 names them.

## Design

### TD-0012

As the item proposes.  The awkward-name test's copy goes in a class outside `tests/SqlSource.Tests/Generator/`, so that `tests/SqlSource.Tests.RoslynFloor` does not compile it, and parses at `LanguageVersion.Latest`; `field` and `extension` are among its rows.

### The spike's questions

Each answered per driver and server version:

1. **`GetFieldValue<T>` on a `NULL`, with no `IsDBNull`.**  For a value type, `string`, `byte[]`, an array of a value type and of a reference type, `Guid`, a `DateTime`, a type a plugin or `MapEnum` reads: does it throw, and what?  The epic's oracle in the corpora depends on a throw for every kind.
2. **`GetFieldValue<T>` of a nullable value type**, `int?`: accepted, or must the method call `IsDBNull` and `GetFieldValue<int>`?
3. **An array that holds a `NULL`** read as `int?[]` and as `int[]`, under Npgsql's default `Array Nullability Mode`.
4. **Parameter names.**  Is `ParameterName` matched with or without `@`, and ignoring case, on each driver through `DbConnection.CreateCommand`?  Does Npgsql rewrite `@name` by default on a command created so, and on one with a transaction?
5. **A declared parameter the text does not use** after a token's fragment drops it: does each driver accept it?
6. **`ExecuteNonQuery`'s count** for `SELECT`, `INSERT ... RETURNING` or `OUTPUT`, `UPDATE` with no rows, a statement under `SET NOCOUNT ON`, and a SQL Server batch of several statements.
7. **Binding by value alone**, with no `DbType`, for each C# type the base maps give: which ones each driver sends as a type the server accepts in the end-to-end queries' contexts, and which need the type named.  This fixes what 8.1's binding by value can run before 8.3 and 8.4.
8. **Npgsql's `DataTypeName`**: set on the parameter the command creates and on an `NpgsqlParameter<T>`, for an array, an enum written as a `string`, `jsonb` as a `string`, a range, a domain; on Npgsql 8 and 10.  Does `NpgsqlParameter<T>` avoid boxing a value type with `DataTypeName` set?
9. **SqlClient's facets**: `Size` of `nvarchar(n)` in characters or bytes, `-1` for `max`; `Precision` and `Scale` of `decimal`; `datetime2` with a `DateTime`; `vector` through `SqlDbTypeExtensions.Vector` on 6.1 and 7; a C# enum read by `GetFieldValue<TEnum>` from an `int` column.
10. **The reader's behaviour**: whether `ReadAsync` followed by the synchronous `GetFieldValue<T>` is buffered for the row under `CommandBehavior.Default` on both drivers, and what `CommandBehavior.SingleResult` and `SingleRow` change, for `one` and `one-optional`, which must read past the first row.
11. **The connection rule**: `OpenAsync` and `CloseAsync` on a connection that is closed, open, or open with a transaction; and that a cancelled token during `ReadAsync` leaves the connection closed again when the method opened it.

### Where the spike runs

Against the default and floor images, by a scratch console project or a scratch test class outside the solution, with the floor drivers by `VersionOverride`.  Nothing of it is committed but the document.

## Testing

`TD-0012`'s tests.  The spike's answers are the spike's evidence; the tests that keep them true are 8.3's and 8.4's.

## Documentation

- `docs/tech-debt/README.md` and `TD-0012`'s file: the row removed and the file deleted.
- `tests/SqlSource.Tests/Generator/AGENTS.md` or `tests/AGENTS.md`, if the class of the awkward-name test needs a rule about where such a test goes.
- The spike document.
- The epic outline: the row of 8.0, and any part of phase 8's section a finding changes.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  `TD-0012` resolved.
2. The spike, and its document.
3. The outline and the specs of 8.1 to 8.6 amended by the findings, and the row set to Done.

## Left to the builder

- The scratch project's shape.
- The document's layout, as long as each question has its answer per version.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The spike is part of 8.0, not a document of its own before it, so that the two prerequisites of phase 8 are one pull request.
- Of the open tech debt, only `TD-0012` comes before phase 8; `TD-0006` gains a known issue in 8.1, and `TD-0013` and `TD-0069` stay as they are.
- A finding that changes a detail of a later sub-phase's spec amends that spec in 8.0's pull request, though specs are otherwise not rewritten, because none of phase 8 is built yet; one that contradicts a decision goes to the owner first.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
