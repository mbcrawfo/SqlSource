# TD-0105 Lists Used As Values Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a PostgreSQL query with an outer join never proves a column not null when an empty list can still return it as null, whatever form negates the list: `SqlListUses` marks as `NegatedList` every list whose truth value the query uses as a value rather than as a condition.

**Architecture:** `SqlListUses.Find` keeps its word scan and its reading of `NOT`. Beside it, a new nested reader, `SqlListUses.Conditions`, follows each list from its parameter to the condition or value that holds it, using one frame per parenthesis and per `CASE ... END`. A list reached from `WHERE`, `ON`, `HAVING` or `WHEN` through `AND`, `OR` and parentheses alone stays `List`. So does a list in the outermost statement's own output (SELECT list, ORDER BY, GROUP BY, RETURNING, SET). Every other list becomes `NegatedList`. `HasNegatedUse` and `WalkScope.DistrustsJoinReduction` need no change.

**Tech Stack:** C# (netstandard2.0 generator project, `src/SqlSource`), xUnit + Shouldly, PostgreSQL 18.6 / 14.24 database tests.

**Spec:** `docs/tech-debt/TD-0105-a-list-negated-without-not-is-trusted-to-filter-nulls.md`

## Decisions and where they depart from the spec

- **Owner's Q1 answer: "c".** Mark every list whose truth value is used as a value. The exception is the outermost statement's own SELECT list, ORDER BY, GROUP BY, RETURNING and SET, which count as safe. This is the spec's second option, made exact, and not its first.
- **Measured on 18.6 and 14.24** (LEFT JOIN, list on the optional side), whether the planner makes the join inner while an empty list returns the null-filled row:
  - It does for `IS FALSE`, `= false`, `false =`, `<> true`, `< true`, `::int = 0` and `IN (false)`.
  - It does not for `IS NOT TRUE`, `IS DISTINCT FROM`, `IS NOT DISTINCT FROM`, `CASE WHEN … THEN false ELSE true END` or `COALESCE(…, false) = false`.
- **The spec's `CASE WHEN` form stays `List`.** A `WHEN` treats `FALSE` and `NULL` alike, so a list as a `WHEN` condition is a condition. `COALESCE(…)` and `IS NOT TRUE` are marked, as uses of a value, though the planner never reduces them: (c) accepts that over-mark.
- **`NegatedList` keeps its name.** td-0106 builds on this file after a sync, so a rename costs it a conflict for no gain. Its doc comment says what it now means.
- **No described `.sql` changes its answer.** Every list in `Lists.sql`, in the examples and in the realistic cases is a clean condition or sits under a `NOT`. A new end-to-end query is added, so its sidecar is regenerated with `tools/describe-end-to-end.sh`.

## Global Constraints

- `src/SqlSource/Parsing/` is pure: no file access, ordinal comparisons, words compared ignoring case (`src/SqlSource/Parsing/AGENTS.md`).
- `SqlListUses.Find` still gives one use per parameter lexeme, in order, or an empty array when no use is a list, and a query without the word `IN` still allocates nothing (`Find_TextWithoutIn_IsEmptyAndAllocatesNothing`).
- The reading of `NOT` (`notHere`, `notAbove`, `Parenthesis`) is unchanged: every existing `NOT` row of `SqlListUsesTests` keeps its answer.
- td-0106 adds its own readings to `SqlListUses.cs` after this merges. The new reader lives in its own file, `SqlListUses.Conditions.cs`, and `SqlListUses.cs` changes only where the loop feeds it.

## Review Focus

1. A list in a common filter shape must stay `List`, or every joined column of the query turns nullable: `lower(x) IN @l`, `x + 1 IN @l`, `data->>'k' IN @l`, `x::text IN @l`, `(@l IS NULL OR x IN @l)`, `a BETWEEN 1 AND 2 AND x IN @l`, a list in `EXISTS (… WHERE …)`, a list in a `JOIN … ON` followed by another `JOIN`, and `WHERE left(a, 1) = 'x' AND …`. Pinned by rows in Task 1.
2. `IS DISTINCT FROM` must not end the `WHERE` it stands in. `FROM` after `DISTINCT` is no clause, so a later `(x IN @l) IS FALSE` is still read in the filter. Row in Task 1.
3. A list in a CTE or a subquery's SELECT list is a value: PostgreSQL pulls it up into the outer filter. Row in Task 1.
4. A statement end, `;`, inside the range resets the reader to the top level. Row in Task 1.
5. Unbalanced text, such as a stray `)` or an unclosed `(` or `CASE`, must never throw, and the uses still number one per parameter. Rows in Task 1.

---

### Task 1: The reader of conditions

**Files:**
- Create: `src/SqlSource/Parsing/SqlListUses.Conditions.cs`
- Modify: `src/SqlSource/Parsing/SqlListUses.cs`: `static partial`, operator runs in `Cursor`, the loop feeds `Conditions`, remarks
- Modify: `src/SqlSource/Parsing/SqlParameterUse.cs` (`NegatedList` doc), `src/SqlSource/Parsing/SqlQueryParameter.cs` (`HasNegatedUse` doc)
- Test: `tests/SqlSource.Tests/Parsing/SqlListUsesTests.cs`, `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`

**Interfaces:**
- Produces: `SqlParameterUse.NegatedList` for every list used as a value, as below. Nothing else changes shape.

**Algorithm.** Frames form a stack. The bottom frame is the top level, which never pops. A frame is pushed at `(` and at `CASE`. Each frame holds:
- `Context`: one of `Output`, `Filter`, `Group` or `Value`.
- `Clean`: no comparison has met the current operand yet.
- `Pending`: the uses of lists in the current operand that no `NOT` negates.
- `Carried`: the clean lists of a `Group` frame's finished operands.
- Whether the frame is a `CASE`.

A list use without an open `NOT` is stored as `List` and added to `Pending`. A list with an open `NOT` stays `NegatedList`, as today.

*Ending an operand* settles each pending use by the frame's context:
- `Output`: it stays `List`.
- `Filter`: `List` if `Clean`, else `NegatedList`.
- `Group`: moved to `Carried` if `Clean`, else `NegatedList`.
- `Value`: `NegatedList`.

Then `Clean` becomes true.

How each item is read:
- `AND`, `OR`: end the operand. The context stays.
- `WHERE`, `ON`, `HAVING`, `WHEN`: end the operand, and the context becomes `Filter`.
- `THEN`, `ELSE`: end the operand. In a `CASE` frame the context becomes `Group`; elsewhere it is "after a clause" (below).
- `CASE`: push a `CASE` frame whose context is `Value`, so that the operand of `CASE x … WHEN` is a value.
- `END`: when the top frame is a `CASE`, end its operand, pop it, and add its `Carried` to the new top's `Pending`. Otherwise it is a clause word.
- **Clause words** end the operand, and the context becomes `Output` in the bottom frame and `Value` in any other. They are `SELECT`, `FROM` (but not after `DISTINCT`), `GROUP`, `ORDER`, `BY`, `LIMIT`, `OFFSET`, `FETCH`, `FOR`, `UNION`, `INTERSECT`, `EXCEPT`, `WINDOW`, `RETURNING`, `SET`, `VALUES`, `USING`, `DO`, `JOIN`, `INNER`, `CROSS`, `FULL`, `NATURAL`, `OUTER`, `LATERAL`, `INTO`, `OUTPUT`, `OPTION`, `UPDATE`, `DELETE` and `INSERT`, plus `LEFT` and `RIGHT` when the next item is not `(`.
- **Test words** make the operand unclean: `IS`, `ISNULL`, `NOTNULL`, `LIKE`, `ILIKE`, `SIMILAR`, `BETWEEN`, `OVERLAPS`.
- Any other word, a parameter that is no list, an `Other` item or a `(`, when the top frame has `Pending`, makes it unclean: anything after a list's parameter applies to its truth value.
- An operator run (a run of `+-*/<>=~!@#%^&|`?` in one text lexeme) makes the operand unclean when it is a comparison (`=`, `<`, `>`, `<=`, `>=`, `<>`, `!=`, `!<`, `!>`, once its trailing `+` and `-` are dropped) or when the top frame has `Pending`.
- `(`: push a frame. Its context is `Group` when the item before it is none, `(`, a comma, an operator run, an `Other`, or one of `AND`, `OR`, `NOT`, `WHERE`, `ON`, `HAVING`, `WHEN`, `THEN`, `ELSE`, `SELECT`, `BY`, `RETURNING`, `DISTINCT` or `CASE`. After any other word it is a call, and its context is `Value`.
- `)`: pop frames down to and including the nearest `(` frame. Each popped frame ends its operand and adds its `Carried` to the frame below it. A `)` with no `(` frame is passed over.
- Comma: end the operand, and the context becomes "after a clause".
- `;`: end and pop every frame but the bottom one, then end its operand, and set its context to `Output`.
- End of the query: as `;`.

- [ ] **Step 1: Write the failing rows.** In `Find_Use_IsClassified`:
  - Change these rows:
    - `f(NOT a, id IN @ids)` → `SELECT NOT a, id IN @ids` (`List`): a comma closes a `NOT`.
    - `(id IN @ids) IS FALSE` → `WHERE (id IN @ids) IS FALSE` (`NegatedList`), and drop the TD-0105 comment.
    - `x IS NOT DISTINCT FROM id IN @ids` → `WHERE NOT b AND x IS NOT DISTINCT FROM id IN @ids`. The `NOT` of `IS NOT` still negates nothing, but the list is now `NegatedList` as a value compared.
  - Add `NegatedList` rows, each `WHERE`-prefixed: `(id IN @ids) IS FALSE`, `(id IN @ids) IS NOT TRUE`, `(id IN @ids) = false`, `false = (id IN @ids)`, `false = id IN @ids`, `id IN @ids = false`, `(id IN @ids) <> true`, `(id IN @ids) < true`, `(id IN @ids)::int = 0`, `(id IN @ids) IN (false)`, `COALESCE(id IN @ids, false) = false`, `CASE id IN @ids WHEN false THEN true END`, `CASE WHEN a THEN id IN @ids END = false`, `f(id IN @ids)`, `a = 1 AND (id IN @ids) IS FALSE`, `x IS DISTINCT FROM y AND (id IN @ids) IS FALSE`, `left(a, 1) = 'x' AND (id IN @ids) IS FALSE`, and `JOIN t ON (t.id IN @ids) = false JOIN u ON true`.
  - Add these `NegatedList` rows too:
    - `WITH s AS (SELECT id IN @ids AS f FROM t) SELECT f FROM s`.
    - `SELECT f FROM (SELECT id IN @ids AS f FROM t) s`.
    - `SELECT (SELECT t.id IN @ids FROM t LIMIT 1)`.
    - `SELECT f FROM (SELECT a WHERE b UNION SELECT id IN @ids AS f) s`: `UNION` and `SELECT` end the subquery's `WHERE`.
  - Add `List` rows: `WHERE id IN @ids`, `WHERE lower(id) IN @ids`, `WHERE id + 1 IN @ids`, `WHERE data->>'k' IN @ids`, `WHERE id::text IN @ids`, `WHERE (@ids IS NULL OR id IN @ids)`, `WHERE a BETWEEN 1 AND 2 AND id IN @ids`, `WHERE a = 1 OR (b AND id IN @ids)`, `WHERE EXISTS (SELECT 1 FROM t WHERE t.id IN @ids)`, `WHERE CASE WHEN id IN @ids THEN false ELSE true END`, `WHERE CASE WHEN a THEN id IN @ids ELSE false END`, `SELECT a FROM t JOIN u ON u.id IN @ids LEFT JOIN v ON true`, `SELECT id IN @ids AS f FROM t`, `SELECT (id IN @ids) = false AS f`, `SELECT a FROM t ORDER BY id IN @ids DESC`, `SELECT a FROM t GROUP BY id IN @ids`, `UPDATE t SET f = id IN @ids WHERE a = 1`, `DELETE FROM t WHERE a = 1 RETURNING id IN @ids`, `SELECT count(*) FILTER (WHERE id IN @ids) FROM t`, `WHERE x LIKE @p AND id IN @ids`, `INSERT INTO t SELECT a FROM u WHERE u.id IN @ids ON CONFLICT DO NOTHING`.
  - Add the two-statement rows: `SELECT (SELECT 1 WHERE (a IN @x) = false); SELECT b IN @y` gives `NegatedList`, `List`, and `SELECT 1 WHERE (a IN @x); SELECT b IN @y` gives `List`, `List`.
  - Add the unbalanced rows: `WHERE (id IN @ids` gives `List`, `WHERE id IN @ids) IS FALSE` gives `NegatedList`, and `WHERE CASE WHEN a THEN (id IN @ids) = false` gives `NegatedList`.
  - In `Parse_ParameterAfterIn_IsAList` add `("SELECT id FROM t WHERE a = @a AND (id IN @ids) IS FALSE", true)`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlListUsesTests|FullyQualifiedName~SqlFileParserParameterTests"`. Expected: the new `NegatedList` rows fail and the rest pass.
- [ ] **Step 3: Implement it.**
  - Make `SqlListUses` `static partial`.
  - In `Cursor`, add `Item.Operator`, which reads a run of operator characters within one text lexeme. Set `_word` to the span of the run, or of the single character of an `Other`, and to an empty span for a quoted region. `IsWord(";")` then works, and `IsComparison()` reads the run.
  - Add `private sealed class Conditions` in the new file, with `Word(Cursor cursor)`, `Open()`, `Close()`, `Comma()`, `Operator(Cursor cursor)`, `Other(Cursor cursor)`, `Parameter(int index, bool isList, bool isNegated)` and `End()`. It holds the builder, so it can set `uses[index] = SqlParameterUse.NegatedList`.
  - The loop creates it after the `HoldsTheWordIn` check, so the allocation test holds, and feeds it every item.
  - Rewrite the remarks of `SqlListUses`, the doc of `NegatedList` and the doc of `HasNegatedUse` to say: negated by a `NOT`, or used as a value.
- [ ] **Step 4: Run** the same tests. Expected: PASS, the allocation test included.
- [ ] **Step 5: Commit** after `./format.sh` and `./pre-commit-validation.sh`: `fix(parsing): a list whose truth is used as a value is read as negated (TD-0105)`.

### Task 2: Describer and end-to-end proof, docs, resolve the item

**Files:**
- Test: `tests/DatabaseTests/Postgres/Describer/WalkTests.cs`. `DescribeAsync_LeftJoinThatAListsTestMakesInner_IsNullableThroughout` is already written. Add `(o.id IN @ids)::int = 0` and `(o.id IN @ids) < true` to it, and add `DescribeAsync_LeftJoinThatAListInWhenFilters_KeepsTheCatalogsAnswer` for `… WHERE CASE WHEN o.id IN @ids THEN true ELSE false END`, which expects `[NotNullCatalog, NullableOuterJoin]`: the join stays outer and only the optional side is nullable.
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Lists.sql` gets the query `ListAccountNotesNotListed`, `WHERE (n.id IN @noteIds) IS FALSE`, and its sidecar is regenerated by `tools/describe-end-to-end.sh`. `tests/DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs` gets `NotesNotListed_Empty_ReadsTheAccountWithoutANote`: an empty list returns carol with a null body, the same rows as `NotesExcept_Empty_…`.
- Docs:
  - `docs/manual/postgresql.md`: the nullability bullet says "a list used as a value or after a `NOT`", and the known issue is removed.
  - `docs/manual/parameters-and-tokens.md`: the line about the list after a `NOT`.
  - `docs/architecture/generator.md`: the **Lists.** paragraph.
  - `docs/architecture/postgres-nullability.md`: line 72.
  - `src/SqlSource.Tool/Describing/Postgres/Walk/AGENTS.md`: line 9, its wording only.
  - `src/SqlSource/Parsing/AGENTS.md`: unchanged unless a new rule is needed. The budget is nearly spent.
- Resolve: delete the TD-0105 file, its row in `docs/tech-debt/README.md`, and every `TD-0105` reference (`grep -rn TD-0105 .`).

- [ ] **Step 1:** Run the walk tests on 18.6. Expected: they now pass. Then run them with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- [ ] **Step 2:** Add the query and the test, run `tools/describe-end-to-end.sh`, and run `ListQueryTests`. Expected: PASS.
- [ ] **Step 3:** Update the docs, resolve the item, run `./format.sh` and `./pre-commit-validation.sh`, and commit: `fix: every list used as a value distrusts the outer joins of its query (TD-0105)`.
- [ ] **Step 4:** Run `./pre-commit-validation.sh --full`, then the PostgreSQL project on `postgres:14.24`.
