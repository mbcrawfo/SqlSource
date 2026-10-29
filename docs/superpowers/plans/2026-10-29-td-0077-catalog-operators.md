# TD-0077 Catalog Operators Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The PostgreSQL describer's catalog calls answer the same whatever operators a user's schema on the `search_path` holds.

**Architecture:** Every operator in the catalog SQL of `PostgresLive` is written `OPERATOR(pg_catalog.op)`, so the server looks for it in `pg_catalog` alone and resolves it among `pg_catalog`'s operators exactly as it does today on a path with no user's operator.  A container case runs every catalog call on a path that puts a schema of false-answering operators first and compares each answer with the answer on the default path.

**Tech Stack:** C#, Npgsql 10, xunit v3, Shouldly, Testcontainers (PostgreSQL 14.24 and 18.6).

**Spec:** `docs/tech-debt/TD-0077-describer-catalog-sql-takes-a-users-operators.md`

## Global Constraints

- The requests and answers of the calls do not change, so no capture or fixture is recorded again.
- The container case holds "a user's `=` on `oid` before `pg_catalog`" and runs on PostgreSQL 14 and 18 (`postgres:14.24`, `postgres:18.6`).
- Resolving follows `docs/tech-debt/AGENTS.md`: delete the item file, its row, every `TD-0077` reference, and its known issue in `docs/manual/postgresql.md`.

## Why `OPERATOR(pg_catalog.op)` and not the other two options

- **A fixed `search_path` by `SET LOCAL`** costs two round trips per call (the `SET LOCAL` and the rollback to the savepoint) and does not fit `prepare`: its read of `pg_prepared_statements` follows the `PREPARE` before `KeepPreparedAsync` moves the savepoint, so a rollback there would give up the locks preparing took, and the remarks of `PrepareAsync` ask that nothing is added between the two.
- **Exact casts** serve only where `pg_catalog` is first and not for `conkey` or the comparisons of `regclass` with `oid` in `partitions` and `tables`, which have no exact operator in `pg_catalog`.
- **`OPERATOR(pg_catalog.op)`** changes only the constant SQL texts, adds no round trip, and makes the server choose among `pg_catalog`'s operators alone, the set it chooses from today when no user's operator exists, so every comparison resolves to the operator it resolves to now.  A probe on 14.24 and 18.6 confirmed it for `(oid, oid)`, `= ANY`, `(oid, integer)`, `("char", unknown)` and `(smallint[], smallint[])`.
- `OPERATOR(...)` has the precedence of "any other operator", which is above `AND`, `NOT` and `IS`; no text of the calls combines two operators in one operand, so no parentheses are needed.

## Comparisons that change

Every `=`, `<>`, `>`, `>=` and `= ANY` in `ParameterTypesSql`, `TableRowsHead`, `TableRowsTail`, `TablesSql`, `RelationsSql`, `PartitionsSql`, `OverloadedFunctionsSql`, `OverloadedOperatorsSql` and `TypesSql`.  `SpellingsSql` has none.  `overloads` is written the same way for one rule across the texts, though its casts already protect it; its `COLLATE "C"` becomes `COLLATE pg_catalog."C"`, since a collation too is found by the path.  `ORDER BY`, `DISTINCT` and `UNION` take the type's default operator class, not the path, and stay.

## Review Focus

- A comparison of a `regclass` with an `oid` (`f.oid = t.relid`, `a.relid <> c.oid`), which has no exact operator in `pg_catalog`: a user's `(oid, regclass)` or `(regclass, oid)` anywhere on the path takes it today.  The container case's schema holds both.
- `"char"` against a literal (`c.relkind = 'p'`, `w.ev_type <> '1'`): a user's `("char", "char")` takes it.  The schema holds `=` and `<>` of it.
- `prepare`'s read compares `text` with `text`: a user's `=` of `(text, text)` makes it answer `StatementNotListed`.  The schema holds it.
- Npgsql's own type loading on open runs under the same path; the case must open its connections so that what it measures is the describer's SQL, not the driver's.
- A text change that breaks a call on 14 but not 18 (pg_constraint holds `NOT NULL` rows from 18 only): the case runs on both images.

---

### Task 1: Qualify the catalog operators, with the container case

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` (the SQL constants and the class remarks)
- Modify: `tests/DatabaseTests/Postgres/Describer/ScratchSchema.cs`
- Create: `tests/DatabaseTests/Postgres/Describer/ShadowingOperatorsCallTests.cs`
- Modify: `tests/DatabaseTests/Postgres/AGENTS.md` (the list of tests of the live calls)

**Interfaces:**
- Produces in `ScratchSchema`: `ShadowingOperators` (SQL that can run again: drops and makes the schema `shadowing_operators`), `ShadowingOperatorsPath = "Search Path=shadowing_operators,pg_catalog"`, `DropShadowingOperators`.

- [ ] **Step 1: Add the schema to `ScratchSchema`.**  Operators that answer false, each through a SQL function `shadowing_operators.never(l, r)`: `=` of `(oid, oid)`, `<>` of `(oid, oid)`, `>=` of `(oid, oid)`, `<>` of `(oid, integer)`, `=` of `(text, text)`, `=` of `(name, text)`, `=` and `<>` of `("char", "char")`, `>` of `(smallint, integer)`, `=` of `(smallint[], smallint[])`, `=` of `(oid, regclass)`, `<>` of `(regclass, oid)`.  Name the schema in the header comment's list of objects a test makes and drops.

- [ ] **Step 2: Write the failing tests** in `ShadowingOperatorsCallTests` (collection `UsesPostgres`), one per call: `PrepareAsync_`, `TablesAsync_`, `RelationsAsync_`, `PartitionsAsync_`, `TypesAsync_`, `SpellingsAsync_`, `OverloadsAsync_` + `OperatorsBeforeTheCatalog_AnswersAsOnTheDefaultPath`.  Each asks the call on the default path and on `ShadowingOperatorsPath` and asserts the two answers are equivalent (`ShouldBeEquivalentTo`) with no error.  Requests that exercise every comparison: `tables` and `relations` of `public.scratch_parts` (partitioned, with `NOT NULL` columns) and `public.orders`; `partitions` of `public.scratch_parts_1`; `types` of `1009` (array → `text`) and `3904` (`int4range` → `int4`); `spellings` of `(1043, 36)`; `overloads` of `["count"]`, `["=", "*"]` (answers `["="]` on both, since the schema holds it).  Make the schema in each test and drop it in a `finally`.  Open both connections before making the schema, so Npgsql's type loading is not under it.

- [ ] **Step 3: Run them and see them fail** on `postgres:18.6`: `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "*ShadowingOperatorsCallTests"`.  Expected: every test but `spellings` fails.

- [ ] **Step 4: Write every operator of the texts listed above as `OPERATOR(pg_catalog.op)`**, and `COLLATE pg_catalog."C"`.  Replace the class remarks' "Its operators are not (TD-0077), …" with: its operators are written `OPERATOR(pg_catalog.op)`, so the server takes `pg_catalog`'s, wherever a user's of the same name and types stands.  Update the comment above `OverloadedFunctionsSql` the same way.

- [ ] **Step 5: Run the project's tests on both images and see them pass**: `dotnet test --project tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`; and `dotnet test --project tests/SqlSource.Tool.Tests` (replays, fixtures unchanged).  `git status` shows no fixture changed.

- [ ] **Step 6: Commit.**

### Task 2: Resolve the item in the docs

**Files:**
- Delete: `docs/tech-debt/TD-0077-describer-catalog-sql-takes-a-users-operators.md`
- Modify: `docs/tech-debt/README.md` (its row only)
- Modify: `docs/manual/postgresql.md:323` (remove the known issue)
- Modify: `docs/architecture/postgres-describer.md:151`
- Modify: `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` (the `search_path` rule)

- [ ] **Step 1:** Architecture paragraph: the operators are written `OPERATOR(pg_catalog.op)`, so the server chooses among `pg_catalog`'s alone and a user's operator of the same name is never taken, wherever it stands; drop the `overloads` sentence's claim that it rests on casts alone.
- [ ] **Step 2:** AGENTS.md rule: "write every type, function and operator in it with `pg_catalog.`, an operator as `OPERATOR(pg_catalog.op)`, and pair arrays with `ROWS FROM`".
- [ ] **Step 3:** `grep -rn TD-0077 . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj` finds only earlier plans in `docs/superpowers/plans/`, which are history.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.

Executed natively in this session: two small tasks over one file of SQL and its docs, where a subagent per task costs more than the work.
