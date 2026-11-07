# TD-0106 — an empty list in a SQL Server aggregate: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a SQL Server `codegen` query whose list stands inside an aggregate, a `GROUP BY` or an `OUTPUT` clause runs when the list is empty or null, and the one case that cannot (a list a `NOT` reaches there) is refused at build time with a diagnostic that names the workaround.

**Architecture:** `SqlListUses.Find` gains an overload that, in the same walk, reads for each list use whether a `NOT` reaches it and whether it stands where SQL Server refuses a subquery (`SqlListPlace`).  Under `mssql`, a list use no `NOT` reaches is written empty as `(@ids_0)`, one typed `NULL`, which matches no row and which SQL Server accepts everywhere; one a `NOT` reaches keeps `(SELECT @ids_0 WHERE 1 = 0)`.  A use a `NOT` reaches inside an aggregate, a `GROUP BY` or an `OUTPUT` becomes a new parse error that only a `codegen` method reports, as `SqlQuery.KeptErrors` is only reported where comments are kept.

**Tech Stack:** C# (netstandard2.0 source generator), xUnit v3 + Shouldly, Testcontainers SQL Server.

**Spec:** `docs/tech-debt/TD-0106-an-empty-list-fails-in-a-sql-server-aggregate.md`.  Owner question Q1 (diagnostic only / rewrite + diagnostic / rewrite only) is open; this plan is option 2, the recommendation.  Under option 1, Task 2 is dropped and Task 3 refuses every list in those places; under option 3, Task 3 is dropped and the known issue is narrowed instead of removed.

## Facts established (SQL Server 2017 floor, probed)

- `(SELECT @p WHERE 1 = 0)` is refused inside an aggregate's parentheses (130), its `WITHIN GROUP (ORDER BY …)` (130), an aggregate in `HAVING` or `ORDER BY` (130), `GROUP BY` expressions, `ROLLUP (…)` included (144), and an `OUTPUT` clause of `UPDATE`/`DELETE` (10705).  It is accepted in a windowed aggregate (`… OVER ()`), a `HAVING` outside an aggregate, `ORDER BY`, a derived table under an aggregate, and a scalar subquery.
- `IN (@p)` with `@p` a typed `NULL` is accepted in every one of those places and matches no row.  `NOT IN (@p)` with `NULL` also matches no row, so a list a `NOT` reaches has no scalar empty form.
- T-SQL has no boolean values: outside a `NOT`, a predicate's `UNKNOWN` acts as `FALSE` everywhere (`WHERE`, `ON`, `HAVING`, `CASE WHEN`, `IIF`).  So the `NULL` form is exact for a use the existing `NOT` reading does not reach, with `ANSI_NULLS` on (SqlClient's default).
- td-0105's value reading (`NegatedList` for a call's argument such as `IIF(x IN @l, …)`) is not the fact the rewrite needs: the rewrite uses the `NOT` reading alone (`notHere || notAbove` in `Find`).

## Global Constraints

- Root `AGENTS.md`: user docs describe effects, never internals; `docs/manual/diagnostics.md` gains the new section (a test checks it); no tech-debt or deferred id in user docs; `sql` samples carry an info string (`sql mssql`, `sql error`).
- `src/SqlSource/Diagnostics/AGENTS.md`: a new parse error is a new `SqlParseErrorKind` **at the end** (id `SQLSRC127`, by order), a descriptor written out in full with a literal id, `NotConfigurable`, in `All`, in `SqlDiagnostics.ForParseError`, in `AnalyzerReleases.Unshipped.md`.  Check `SQLSRC127` is still free on `main` before every push.
- `src/SqlSource/Parsing/AGENTS.md`: `Parsing/` is pure; `Find` (5 arguments) keeps allocating its result alone (`Find_QueryWithLists_AllocatesItsUsesAlone`); the hash never changes (a list segment hashes as its text).
- td-0105 owns `NegatedList` and `SqlListUses.Conditions`; this unit's reading lives in its own members (`SqlListUses.Places.cs`, `SqlListPlace`).
- A changed described `.sql` regenerates its sidecar with `tools/describe-end-to-end.sh`; never hand-edit one.
- `src/SqlSource/AGENTS.md` has 359 bytes left: any new rule goes in `src/SqlSource/Parsing/AGENTS.md` or `docs/architecture/generator.md`.

## Review Focus

1. A list a `NOT` reaches inside a **windowed** aggregate (`SUM(…) OVER (…)`) or inside `OVER (PARTITION BY …)` must build and run: no error.
2. A column or alias named like a clause word, `t.output`, `[group]`, `d.group_id`, or the `GROUP` of `WITHIN GROUP`, must not open an `OUTPUT` or `GROUP BY` place.
3. A `GROUP BY` or `OUTPUT` inside a subquery or derived table ends at its closing parenthesis, and a statement ends at `;`: a list after either is in no place.
4. One list used twice, once under a `NOT` and once not, writes each use in its own empty form, and the length the method computes for the SQL equals what it writes (the generated code throws otherwise).
5. A `NOT IN @x` in an aggregate where `@x` is a declared local variable (`DECLARE @x …`) is no list and gets no error.

---

### Task 1: the reading of where a list stands

**Files:**
- Create: `src/SqlSource/Parsing/SqlListPlace.cs`, `src/SqlSource/Parsing/SqlListUses.Places.cs`
- Modify: `src/SqlSource/Parsing/SqlListUses.cs` (the overload, and calls to the reader in `Find`'s loop)
- Test: `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  [Flags] internal enum SqlListPlace { None = 0, UnderNot = 1, InAggregate = 2, InGroupBy = 4, InOutput = 8 }
  // SqlListUses
  public static ImmutableArray<SqlParameterUse> Find(string text, EquatableArray<SqlLexeme> lexemes, int start, int end,
      SqlDialectRules rules, out ImmutableArray<SqlListPlace> places)
  ```
  `places` has one entry per use, aligned with the uses, and is empty exactly when the uses are; a use that is not a list (`Value`, `NullTest`) is `None`.  The 5-argument `Find` is unchanged in result and allocation; both go through one private core so the walk is written once.

Reading rules (the reader is a private struct over pooled arrays, like `Conditions`, fed from `Find`'s loop, inactive for the 5-argument overload):
- `UnderNot`: the `NOT` reading `Find` already has at the parameter (`notHere || notAbove`), not `NegatedList`.
- `InAggregate`: the use is inside the parentheses of a call to a built-in aggregate, `APPROX_COUNT_DISTINCT`, `APPROX_PERCENTILE_CONT`, `APPROX_PERCENTILE_DISC`, `AVG`, `CHECKSUM_AGG`, `COUNT`, `COUNT_BIG`, `GROUPING`, `GROUPING_ID`, `JSON_ARRAYAGG`, `JSON_OBJECTAGG`, `MAX`, `MIN`, `STDEV`, `STDEVP`, `STRING_AGG`, `SUM`, `VAR`, `VARP` (ignoring case; a word after `.` or a quoted name is not one), or of its `WITHIN GROUP (…)`, unless `OVER` follows the call (after its `WITHIN GROUP (…)`, if any).  Decide windowing at the `(` by looking ahead with a copy of the cursor to the matching `)`.
- `InGroupBy`: after `GROUP BY` (the word `GROUP` followed by the word `BY`) at a depth, until, at that depth, one of `HAVING ORDER OPTION FOR UNION EXCEPT INTERSECT WINDOW WITH SELECT INSERT UPDATE DELETE MERGE`, the `)` that closes the depth, or `;`.
- `InOutput`: after the word `OUTPUT` at a depth, until, at that depth, one of `INTO FROM WHERE VALUES SELECT DEFAULT EXEC EXECUTE OPTION`, the `)` that closes the depth, or `;`.
- A place opened at a depth holds for every parenthesis inside it.

- [ ] **Step 1: write the failing tests** — a theory `Find_SqlServer_ReadsWhereEachListStands(string sql, string[] expected)` over `SqlDialectRules.SqlServer`, `expected` the `SqlListPlace` of each use by name (`"None"`, `"UnderNot"`, `"InAggregate"`, `"UnderNot, InAggregate"`, …), with at least these rows:
  - `SELECT SUM(CASE WHEN s IN @a THEN 1 ELSE 0 END) FROM t` → `InAggregate`
  - `SELECT COUNT(IIF(id NOT IN @a, 1, NULL)) FROM t` → `UnderNot, InAggregate`
  - `SELECT SUM(CASE WHEN s NOT IN @a THEN 1 END) OVER (PARTITION BY g) FROM t` → `UnderNot`
  - `SELECT SUM(x) OVER (PARTITION BY CASE WHEN s IN @a THEN 1 END) FROM t` → `None`
  - `SELECT STRING_AGG(s, ',') WITHIN GROUP (ORDER BY CASE WHEN id IN @a THEN 1 END) FROM t` → `InAggregate`
  - `SELECT g FROM t GROUP BY g HAVING SUM(CASE WHEN id IN @a THEN 1 END) > 0` → `InAggregate`
  - `SELECT g FROM t GROUP BY g HAVING g IN @a` → `None`
  - `SELECT COUNT(*) FROM t GROUP BY ROLLUP (CASE WHEN id IN @a THEN 1 ELSE 0 END)` → `InGroupBy`
  - `SELECT COUNT(*) FROM t GROUP BY CASE WHEN id IN @a THEN 1 END ORDER BY CASE WHEN id IN @b THEN 1 END` → `InGroupBy`, `None`
  - `SELECT x FROM (SELECT g AS x FROM t GROUP BY g) AS d WHERE x IN @a` → `None`
  - `SELECT a FROM t GROUP BY a; SELECT b FROM u WHERE b IN @a` → `None`
  - `UPDATE t SET s = 'x' OUTPUT CASE WHEN inserted.id IN @a THEN 1 END WHERE id IN @b` → `InOutput`, `None`
  - `DELETE t OUTPUT deleted.id INTO @log WHERE id NOT IN @a` → `UnderNot`
  - `SELECT t.output, t.[group] FROM t WHERE id IN @a` → `None`
  - `SELECT dbo.SUM(CASE WHEN id IN @a THEN 1 END) FROM t` → `None`
  - `SELECT id FROM t WHERE id = @v AND s IN @a` → `None`, `None`
  - `SELECT MAX(x) FROM (SELECT CASE WHEN id IN @a THEN 1 END AS x FROM t) AS d` → `None`
  Plus `Find_SqlServerWithoutAList_GivesNoPlaces` (`places.IsEmpty`), and a test that the 6-argument `Find` gives the same uses as the 5-argument one for every row of the theory.
- [ ] **Step 2:** run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlListPlacesTests"`; expect compile failure.
- [ ] **Step 3:** implement the enum, the overload and the reader as above.
- [ ] **Step 4:** run the new tests and `--filter "FullyQualifiedName~SqlListUsesTests"`; all pass (the allocation test included).
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit `feat(parsing): read where each SQL Server list stands`.

### Task 2: the empty form of a list no NOT reaches, under mssql

**Files:**
- Modify: `src/SqlSource/Parsing/Tokens/SqlSegment.cs`, `src/SqlSource/Parsing/Tokens/SqlListSegments.cs`, `src/SqlSource/Parsing/SqlFileParser.cs` (`BuildQuery`, and the kept form's split), `src/SqlSource/Generation/Emitting/QueryMethods/ListSqlWriter.cs`, `src/SqlSource/Generation/Emitting/MethodWriter.cs`
- Test: `tests/SqlSource.Tests/Parsing/` (segment test beside the existing `SqlListSegments` tests), `tests/SqlSource.Tests/Generator/Golden/sqlserver/lists.txt` and `lists-no-padding.txt` (regenerated by their test's own update path, as `tests/README.md` says), `tests/DatabaseTests/SqlServer/EndToEnd/` and its copy in `tests/DatabaseTests/SqlServer.SqlClient61/`

**Interfaces:**
- Consumes: Task 1's `Find(…, out places)`.
- Produces: `internal sealed record SqlSegment(SqlSegmentKind Kind, string Text, bool EmptyIsNull = false)` — for a `List` segment, true when its empty form is `(@ids_0)` rather than `(SELECT @ids_0 WHERE 1 = 0)`.  `SqlListSegments.Split(…, ImmutableArray<SqlParameterUse> uses, ImmutableArray<SqlListPlace> places, …)` sets it for a list use whose place lacks `UnderNot` when `places` is not empty.  `SqlFileParser` calls the 6-argument `Find` only when `rules.Dialect == SqlDialect.SqlServer`; other dialects pass empty places.  `SqlQueryHash` is unchanged (it reads `Text` alone).
- `ListSqlWriter.AppendLength(StringBuilder, ListLocals, int list, int uses, bool emptyIsNull)` and `AppendSteps(…, bool emptyIsNull, …)`; `MethodWriter` counts each list's uses by form and calls `AppendLength` once per form used.

- [ ] **Step 1: failing tests**
  - Parsing: `Split_SqlServerListNoNotReaches_IsEmptyAsNull` (`WHERE a IN @ids AND b NOT IN @ids` under `mssql` → first segment `EmptyIsNull: true`, second `false`), and `Split_PostgresList_KeepsTheSubquery` (`EmptyIsNull: false`).
  - Hash: a test that the hash of `SELECT … WHERE a IN @ids` under `mssql` is the same as before (pin today's value).
  - End-to-end SQL Server (`ListQueryTests`, both projects): `CountStatuses_Empty_IsRefusedByTheServer` becomes `CountStatuses_Empty_CountsNone` (`counts.ShouldBe([new CountAccountsWithStatusesDto(0)])`), plus `CountStatuses_Null_CountsNone` if the query takes a null list; new queries in `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Lists.sql` and their tests: a `GROUP BY CASE WHEN status IN @statuses …` (empty → one group of every account), an `UPDATE … OUTPUT CASE WHEN inserted.id IN @ids …` inside a rolled-back transaction or on a scratch row (empty → every flag 0), and a use twice, once in `WHERE … NOT IN @ids` and once in `SUM(CASE WHEN id IN @ids …)`, (empty → every account counted, sum 0).  Name them by what they show; the README's `ListQueryTests` paragraph follows.
- [ ] **Step 2:** run them; the parsing tests fail to compile, the database ones fail with 130/144/10705.
- [ ] **Step 3:** implement; then `tools/describe-end-to-end.sh` for the changed `Lists.sql` (commit its sidecar as written), and regenerate the golden files.
- [ ] **Step 4:** run `tests/SqlSource.Tests`, and `tests/DatabaseTests/SqlServer` filtered to `ListQueryTests`, on the default image and on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`; and `tests/DatabaseTests/SqlServer.SqlClient61` the same filter.
- [ ] **Step 5:** format, validate, commit `fix(generator): an empty SQL Server list that no NOT reaches is sent as one NULL`.

### Task 3: SQLSRC127 for a list a NOT reaches where SQL Server refuses its empty form

**Files:**
- Modify: `src/SqlSource/Parsing/SqlParseErrorKind.cs` (new last member `NegatedListRefusedEmpty`), `src/SqlSource/Parsing/SqlFileParser.cs`, `src/SqlSource/Parsing/SqlBlock.cs`, `src/SqlSource/Generation/SqlQuery.cs`, `src/SqlSource/Generation/SqlFileReader.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs`, `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`, a generator test beside the other method-problem tests (the ones of `SQLSRC275`), `tests/DatabaseTests/SqlServer/EndToEnd/` (the workaround)

**Interfaces:**
- Produces: `SqlBlock.CodeGenErrors` and `SqlQuery.CodeGenErrors` (init properties, default empty; `EquatableArray<SqlParseError>` and `EquatableArray<DiagnosticInfo>`): errors only for a query whose output is `codegen`, which `MethodPlanner.Plan` adds to its problems, so the query gets no method.
- The error, for each list use whose place has `UnderNot` and any of `InAggregate`, `InGroupBy`, `InOutput`, and whose parameter is a list (as `SqlListSegments.KindOf` decides, so a declared variable is passed over), at the use's span in the file.  Arguments: the parameter with its prefix as written, and where it stands: `"inside an aggregate"`, `"in a GROUP BY"`, `"in an OUTPUT clause"` (the first that applies, in that order).
- Descriptor `NegatedListRefusedEmpty`, `SQLSRC127`, title `Negated list stands where SQL Server refuses an empty list`, message: `The list '{0}' stands after a NOT {1}, where SQL Server refuses the form an empty list is sent in.  Test the list in a WITH query, or a subquery in FROM, and use its result here.`  Error, `NotConfigurable`.

- [ ] **Step 1: failing tests** — parser: `Parse_SqlServerNegatedListInAnAggregate_HasACodeGenError` (span and arguments of `SELECT COUNT(IIF(id NOT IN @ids, 1, NULL)) AS c FROM t`), the `GROUP BY` and `OUTPUT` reasons, `Parse_SqlServerNegatedListInAWindowedAggregate_HasNone`, `Parse_DeclaredVariableNegatedInAnAggregate_HasNone`, `Parse_PostgresNegatedListInAnAggregate_HasNone`, and that `result.Errors` stays empty (the file still parses).  Generator: a `codegen` query gets `SQLSRC127` and no method; the same query under `-- output: sql` gets none.  `docs/manual/diagnostics.md`'s test fails until the section is added.  End-to-end: the workaround query `CountAccountsWithoutStatusesTestedFirst` (the `NOT IN` tested in a `WITH` query, its result summed) runs empty and counts every account; it replaces `CountAccountsWithStatusesTestedFirst`, which the `NULL` form makes unneeded.
- [ ] **Step 2:** run; they fail.
- [ ] **Step 3:** implement; add the `## SQLSRC127` section (what it means, a `sql error` sample, the console line, the fix, `See:` the list section of `parameters-and-tokens.md`); re-describe `Lists.sql`.
- [ ] **Step 4:** run `tests/SqlSource.Tests`, `tests/SqlSource.Repository.Tests`, and the SQL Server `ListQueryTests` on default and floor images.
- [ ] **Step 5:** format, validate, commit `feat(generator): SQLSRC127 for a negated list where SQL Server refuses an empty one`.

### Task 4: docs and resolving the item

**Files:** `docs/manual/sqlserver.md` (known issue → narrowed: a list after a `NOT` inside a user-defined aggregate, or a function a token names, still fails empty; the workaround stays; plus one sentence that the generated SQL assumes `ANSI_NULLS` on, beside the `ANSI_WARNINGS` note), `docs/manual/parameters-and-tokens.md` (line 90's bullet and the known issue at line 263), `docs/architecture/generator.md` (the **Lists.** paragraph: the reading of places, the two empty forms, where `SQLSRC127` is reported; line 698), `src/SqlSource/Parsing/AGENTS.md` only if a rule is needed, `tests/DatabaseTests/SqlServer/README.md` (line 171), `docs/tech-debt/README.md` (remove the TD-0106 row), delete `docs/tech-debt/TD-0106-an-empty-list-fails-in-a-sql-server-aggregate.md`, and every `TD-0106` reference (`grep -rn TD-0106 .`).  Check `examples/SqlServerExamples` (its only list is in `WHERE`: no change expected).

- [ ] **Step 1:** edit; `grep -rn "TD-0106" . --exclude-dir=.git` is empty; the known-issue searches for "aggregate" in both pages show only the narrowed issue.
- [ ] **Step 2:** `./format.sh`, `./pre-commit-validation.sh`; commit `docs: an empty list runs in a SQL Server aggregate (TD-0106 resolved)`.
