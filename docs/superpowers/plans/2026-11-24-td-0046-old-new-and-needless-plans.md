# TD-0046: the old and the new row, and a plan nobody needs

**Goal:** fix two parts of TD-0046 in the PostgreSQL describer.  First, tell PostgreSQL 18's old and new row of a `RETURNING` list from a table's alias, so any other alias maps.  Second, stop asking for a plan when the gate's only word is a function's name, such as `left(name, 3)`.  `Custom Scan` waits on the owner (Q1), and `postgres.column` stays as it is.

## What PostgreSQL 18.6 showed

These were run on `postgres:18.6`:

- `RETURNING WITH (OLD AS o)` replaces the name `old`, and `old.v` is then an error.  Naming one row leaves the other its default.  `AS` is required.
- The target's alias always names the target.  `UPDATE t AS old ... RETURNING old.v` returns the target.  A `WITH` name equal to a table's alias is refused.
- A `FROM` table aliased `old` at the top level takes the name too.  The walk keeps it to the heuristic, since telling top-level tables from inner ones needs a parser.
- An inner scope's alias, `INSERT ... SELECT ... FROM s old`, prints as `old.x`, as the old row does.
- `"OLD"` is no old row, so the comparison is exact after folding.  `U&"\0061b"` and `U&"!0061c" UESCAPE '!'` print decoded (`ab.v`, `ac.v`).
- The plan prints a name longer than 63 bytes cut, as the server cuts it.

## Design

- `Statements/OldAndNewNames.cs` reads the SQL and the server's major.  It gives `None`, `Unknown` or a set of names.  It is `None` before 18, and also when the SQL names neither row, by `OuterJoinGate.NamesOldOrNew`.  Otherwise the set is `old`, `new` and every name a `RETURNING WITH (...)` gives, for all clauses together.  It is `Unknown` when a `WITH (` after `RETURNING` cannot be read, or a name cannot be decoded.
- In `NullabilityWalk`, `WalkScope.OldAndNewMayBeNamed` becomes `WalkScope.OldAndNew`.  At a root `ModifyTable`, a qualified alias maps when it is the target's alias or a name the rows cannot take.  `Unknown` keeps the rule on `main`, where only the target's alias maps.  The other readers of the flag (a bare name in an expression, a subplan, a scan's column of no origin, a written value) read `MayBeNamed`.  Before 18 they are now read.
- `OuterJoinGate`:
  - A quoted name counts for `OLD`/`NEW` only when it is exactly `old` or `new`, a `U&"..."` one once decoded with its `UESCAPE`.  One that cannot be decoded still counts.
  - `LEFT`, `RIGHT`, `FULL` and `OUTER` do not count when a `(` follows them, past white space and comments.  All four are reserved words that no alias can take, so a `(` after one is always a call.
- `UnicodeEscape.TryRead` of the generator gets an overload with the escape character.

## Tasks

1. `UnicodeEscape` overload and `OldAndNewNames` with unit tests (`tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/`).
2. `OuterJoinGate` changes with unit tests.
3. The walk and the session, with unit tests of `NullabilityWalkNameTests` and the session's plan tests.
4. Tests on a real server, on 14 and 18: `WalkTests`, `ColumnTests`, `WrittenValueTests`, `ScanTests`.  Rework the tests and the fixture `walk-semi-anti` that used `left(` to gate a query.  Record the fixtures and sidecars again, and update `RealisticNullabilityNoiseTests`' `Budgets` if they move.
5. Docs: `docs/architecture/postgres-nullability.md`, the walk's `AGENTS.md`, the remarks of `PostgresIdentifiers`, `site/reference/databases/postgresql/nullability.md`, and TD-0046 narrowed to what remains, with its row.
