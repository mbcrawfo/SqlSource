# TD-0107: a list's null test in an index predicate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On PostgreSQL, a query that tests a list for null inside the index predicate of `ON CONFLICT (...) WHERE` is described, as the running query runs.

**Architecture:** `SampleSql.Build` writes `NULL`, not `(SELECT NULL)`, for a list's null test that stands in the index predicate of `ON CONFLICT (...) WHERE`, which runs to the `DO` of the conflict's action.  It finds the predicates by lexing the sample it built with PostgreSQL's rules, only when the sample holds a null test, so a string or a quoted name that spells the words is no predicate.

**Tech Stack:** C#, xUnit, Shouldly, the describer's PostgreSQL tests (Testcontainers).

**Spec:** `docs/tech-debt/TD-0107-a-lists-null-test-in-an-index-predicate-cannot-be-described.md`

## The choice of fix

The spec offers two fixes.  This plan takes the second, `SampleSql` writing `NULL` in the index predicate, because:

- It needs no reading of the server's message: PostgreSQL translates "cannot use subquery in index predicate" by `lc_messages`, and `0A000` (feature not supported) is a code for many refusals, so a retry keyed on them is fragile.
- The sample stays one function of the query: a describe asks one request, so a capture and a bundle replay ask the same, and no query is described twice.
- `DatabaseRuns` and `BundleReplay`, which 108-run refactors after this unit, do not change.
- Nothing a `.sql` file holds runs a statement: the sample is the same SQL the describer is already given, with one value in place of another.
- `NULL` is safe there: an index predicate only chooses the arbiter index, and reduces no join, so the folded condition costs no nullability.

Checked on `postgres:14.24`: a `boolean` parameter (the running query's null test) and `NULL IS NULL` are accepted in the predicate, and `(SELECT NULL)` is refused with the spec's error.

## Global Constraints

- PostgreSQL only: SQL Server's stand-in is already `NULL` and does not change.
- No change to the generator, the hash, the parser, `DatabaseRuns` or `BundleReplay`.
- No sidecar, fixture or capture changes: no described `.sql` holds such a query.
- Resolving the item follows `docs/tech-debt/AGENTS.md`: delete the item file, its row in `docs/tech-debt/README.md`, the id's references, and the known issues in `docs/manual/postgresql.md` and `docs/manual/parameters-and-tokens.md`.

## Review Focus

- A string or quoted name holding `ON CONFLICT (x) WHERE` is no predicate: the null test after it keeps `(SELECT NULL)`.
- `ON CONFLICT ON CONSTRAINT name DO UPDATE ... WHERE (@ids IS NULL ...)`: that `WHERE` is the action's, no predicate.
- The `WHERE` of `DO UPDATE` after a predicate keeps `(SELECT NULL)`: the predicate ends at `DO`.
- A predicate without parentheses, `WHERE @ids IS NULL OR id IN @ids DO NOTHING`, and lower-case keywords.
- An `INSERT ... ON CONFLICT` inside a `WITH` query, with a null test in the outer `SELECT` too: each test gets its own stand-in.

---

### Task 1: `NULL` in the index predicate, with its tests

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SampleSql.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SampleSqlTests.cs`
- Test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs`

**Interfaces:**
- Produces: `SampleSql.Build(SqlQuery, SqlDialect)`, unchanged in signature.

- [ ] **Step 1: Write the failing unit tests** in `SampleSqlTests`, each `Sample(...)` of a query and `ShouldBe` of its sample:
  - `Build_NullTestInAnIndexPredicate_IsNull`: `INSERT INTO t (id, kind) VALUES (1, 'a') ON CONFLICT (id) WHERE (@kinds IS NULL OR kind IN @kinds) DO NOTHING` → `... WHERE (NULL IS NULL OR kind IN (@kinds)) DO NOTHING`.
  - `Build_NullTestInAnIndexPredicateWithoutParentheses_IsNull`, lower-case keywords: `... on conflict (id) where @kinds is null or kind in @kinds do nothing` → `NULL is null`.
  - `Build_NullTestInTheWhereOfDoUpdate_IsASubqueryOfNull`: predicate and action both test the list; the predicate's is `NULL`, the action's `(SELECT NULL)`.
  - `Build_NullTestAfterOnConstraint_IsASubqueryOfNull`: `ON CONFLICT ON CONSTRAINT t_pkey DO UPDATE SET kind = 'b' WHERE (@kinds IS NULL OR t.kind IN @kinds)`.
  - `Build_NullTestAfterAStringOfTheWords_IsASubqueryOfNull`: `SELECT 'ON CONFLICT (id) WHERE' FROM t WHERE (@ids IS NULL OR id IN @ids)`.
  - `Build_NullTestsInAndAfterAWithQuerysIndexPredicate_EachHasItsOwn`: `WITH i AS (INSERT ... ON CONFLICT (id) WHERE (@ids IS NULL OR id IN @ids) DO NOTHING RETURNING id) SELECT id FROM i WHERE (@ids IS NULL OR id IN @ids)`.
- [ ] **Step 2: Run them and see the predicate cases fail**: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SampleSqlTests"`.
- [ ] **Step 3: Implement in `SampleSql`**: build with `NULL` at each null test, recording where each starts; on PostgreSQL with at least one null test, lex the built SQL with `SqlLexer.Lex(sql, SqlDialectRules.PostgreSql)` and walk its `Text` lexemes (words as runs of `IsNameCharacter`, parentheses with a depth, other lexemes passed over) for `ON CONFLICT (` ... `)` `WHERE`, whose predicate runs to the word `DO` at the `WHERE`'s depth, a `)` below it, a `;` or the end; then write `(SELECT NULL)` for each null test outside every predicate.  A lex error (no sample can have one) means no predicate.  Update the type's doc comment.
- [ ] **Step 4: Run the unit tests**: all of `SampleSqlTests` pass.
- [ ] **Step 5: Describer test** in `ParameterTests`: `DescribeAsync_ListsNullTestInAnIndexPredicate_IsDescribed`, `DescribeRequests.Planned("postgres", "INSERT INTO samples (id, flag) VALUES (@id, true) ON CONFLICT (id) WHERE (@ids IS NULL OR id IN @ids) DO NOTHING")`, parameters `[Inferred("id", int4), Inferred("ids", int4)]`.  Run it on the default image and on `postgres:14.24`.
- [ ] **Step 6: Commit** (`./format.sh`, `./pre-commit-validation.sh` first).

### Task 2: Resolve the item in the docs

**Files:**
- Delete: `docs/tech-debt/TD-0107-a-lists-null-test-in-an-index-predicate-cannot-be-described.md`
- Modify: `docs/tech-debt/README.md` (its row only), `docs/manual/postgresql.md` and `docs/manual/parameters-and-tokens.md` (their known issue only), `docs/architecture/tool.md` (the paragraph on `SampleSql.Build`), `docs/architecture/postgres-nullability.md` (the sentence on the stand-in).

- [ ] **Step 1:** Make the edits; the architecture pages say that a null test in the index predicate of `ON CONFLICT` is `NULL`, and why that is safe.
- [ ] **Step 2:** `grep -rn "TD-0107" . --exclude-dir=.git` finds only this plan.
- [ ] **Step 3: Commit** (`./pre-commit-validation.sh` first).
