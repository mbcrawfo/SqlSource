# SQL Server nullability noise Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The SQL Server describer proves not null, and records as `nullable: false`, the columns of the forms the design lists, cutting the realistic corpus's noise from 39 to 15 on 2017 and from 44 to 15 on 2025, and corrects a `PERSISTED NOT NULL` computed column read from one table alone.

**Architecture:** A reader of one T-SQL `SELECT`, over the tokens of the generator's lexer, maps each select item to its column and reads each item's outer form into a proof tree.  The leaves the text cannot decide are asked of the server in one new call, `operands`: `sys.dm_exec_describe_first_result_set` of a `SELECT` of those operands over the query's own `FROM ... WHERE`, behind its own `WITH`.  The session applies the proofs between building the columns and the table match; the `columns`/`table` text gains a tool column `persisted_not_null` read from `sys.computed_columns`.

**Tech Stack:** C# (`net8.0`), xunit v3 with Shouldly, Testcontainers SQL Server 2017 and 2025, SqlClient 7.1.1.

**Spec:** [`docs/superpowers/specs/2026-10-23-sql-server-nullability-noise-design.md`](../specs/2026-10-23-sql-server-nullability-noise-design.md).  Read it first: its "Decisions" sections are the rules every task implements, and its "What the server shows" is why.

## Global Constraints

- The hard rule: never answer `nullable: false` for a column that can be `NULL`.  Anything the reader does not recognise keeps the server's answer, `nullableSource` `server`.
- A form proved is `nullableSource` `expression` (`SidecarValues.NullableSource.Expression`); the computed column is `catalog` (`SidecarValues.NullableSource.Catalog`).
- The user's text reaches the server only as `@tsql` of `sys.dm_exec_describe_first_result_set`, through `SqlServerLive`'s own text (root rule in `src/SqlSource.Tool/AGENTS.md`).  `operands`' text is the user's tokens rearranged and is sent the same way.
- A call is named once and never renamed: `SqlServerCallNames.Operands = "operands"`.  An answer record may gain only a nullable member: `SqlServerColumnRow.PersistedNotNull` is `bool?`, last, defaulting to null.
- The proofs assume `ANSI_WARNINGS` on (design decision 5); the manual's SQL Server page says so as a known issue.
- Every commit passes `./pre-commit-validation.sh` on the exact tree.  After any change to the describer or its tests, also run `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer` before the commit.
- A commit that changes what the describer answers lowers `RealisticNullabilityNoiseTests.Budgets` in itself, and records again with `tools/record-fixtures.sh mssql` and `tools/describe-end-to-end.sh` (AGENTS.md rule 5); a recorded file is never edited by hand.
- AGENTS.md files that name a file are updated in the commit that adds, moves or renames it.  A new folder of more than a few files gets an AGENTS.md with rules only and the maintenance footer.
- Tests have three-part names, `Subject_Condition_Result`, as `tests/AGENTS.md` says.
- No new diagnostic, no new tech-debt id.  TD-0071 is rewritten, not removed.

## Review Focus

1. A select list the reader misreads (a `CASE` with an operator inside, `COLLATE`, `AT TIME ZONE 'UTC'` ending an item, a `WITHIN GROUP (ORDER BY ...)`, `NEXT VALUE FOR`, a `TOP 10` without parentheses): it must prove nothing, never a wrong item.  Task 1 pins each.
2. An operand evaluated differently in `operands` than in the query: a grouping column under `ROLLUP`, an aggregate or `OVER` inside an operand, a schema-qualified call (a CLR aggregate).  Task 2 pins that none is asked; Task 5 runs the `ROLLUP` cases on a server.
3. An `operands` answer that is short, misnamed, an error, or a lost connection: the text-only proofs stand, nothing is reported, the describe goes on to `table`.  Task 3 pins it.
4. A line comment inside an operand (`COALESCE(a, -- x\n b)`) that would swallow ` AS [n]`.  Task 3 pins the line break.
5. A `PERSISTED NOT NULL` column read through a join, a CTE named like a table, a derived table, or under `ROLLUP`: stays nullable.  Tasks 4 and 5 pin them.

---

## File structure

New folder `src/SqlSource.Tool/Describing/SqlServer/Proofs/`, namespace `SqlSource.Tool.Describing.SqlServer.Proofs`:

| File | Responsibility |
|----|----|
| `TsqlToken.cs` | `TsqlToken` and `TsqlTokenKind` |
| `TsqlTokens.cs` | The tokens of a text, from `DescriberSql.Lex` with `SqlDialectRules.SqlServer` |
| `TokenRange.cs` | A range of token indices, `[Start, End)` |
| `SelectBody.cs` | The clauses of one `SELECT` over a token range: items, `FROM ... WHERE`, grouping, the one table |
| `SelectItem.cs` | An item: its expression's range, its name, whether it is a `*` |
| `SelectStatement.cs` | The whole text as `[;] [WITH ...] SELECT ... [;]`, its CTE names and its body |
| `Proof.cs` | A proof tree and its evaluation |
| `ExpressionProofs.cs` | The forms: an expression's range to a `Proof`, collecting the operands to ask |
| `OperandsQuery.cs` | The text of `operands`, and the reading of its answer |
| `ColumnProofs.cs` | For one describe: what to ask, and the columns corrected by the answer; the `table` rows corrected |
| `AGENTS.md` | Rules of the folder |

Modified: `SqlServerCallNames.cs`, `SqlServerCalls.cs` (`SqlServerColumnRow.PersistedNotNull`), `SqlServerLive.cs` (`ColumnsSql` and the reader of its rows), `SqlServerSession.cs` (`ResultAsync`, `MatchAsync`), `SqlServerSession.Calls.cs` (`OperandsAsync`), `SqlServerEvents.cs` (`Operands`), `src/SqlSource.Tool/AGENTS.md`, docs (Task 6).

---

### Task 1: Tokens and the reader of one `SELECT`

**Files:**
- Create: `Proofs/TsqlToken.cs`, `Proofs/TsqlTokens.cs`, `Proofs/TokenRange.cs`, `Proofs/SelectItem.cs`, `Proofs/SelectBody.cs`, `Proofs/SelectStatement.cs`, `Proofs/AGENTS.md`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/TsqlTokensTests.cs`, `.../Proofs/SelectStatementTests.cs`

**Interfaces:**
- Produces:
  - `internal enum TsqlTokenKind { Word, Number, String, QuotedName, Parameter, Symbol }`
  - `internal readonly record struct TsqlToken(TsqlTokenKind Kind, string Text, int Start, int End)` — `Start`/`End` are character offsets in the SQL, `End` exclusive.  `bool Is(string word)`: a `Word` equal to `word` ignoring case.
  - `internal static class TsqlTokens { public static IReadOnlyList<TsqlToken> Read(string sql); }`
  - `internal readonly record struct TokenRange(int Start, int End)` with `int Count`, `bool IsEmpty`.
  - `internal sealed record SelectItem(TokenRange Expression, string? Name, bool IsStar)`
  - `internal enum SelectGrouping { None, Plain, All, Sets }`
  - `internal sealed record SelectBody(IReadOnlyList<SelectItem> Items, TokenRange? FromThroughWhere, SelectGrouping Grouping, bool HasHaving, bool HasTop, bool HasOffset, bool HasFrom, bool IsOneTable)` with `public static SelectBody? Read(IReadOnlyList<TsqlToken> tokens, TokenRange range, IReadOnlyCollection<string> cteNames)`.
  - `internal sealed record SelectStatement(TokenRange With, IReadOnlyList<string> CteNames, SelectBody Body)` with `public static SelectStatement? Read(IReadOnlyList<TsqlToken> tokens)`.

- [ ] **Step 1: Write the failing tests of the tokens**

`TsqlTokensTests`, each `Read_<Input>_<Result>`:
- `SELECT a.b, N'x''y', 0x1F, 1.5e-3, @p, [q]]r], "s" FROM t` gives, in order: Word `SELECT`, Word `a`, Symbol `.`, Word `b`, Symbol `,`, String `N'x''y'` (the `N` joined to the quote with no gap), Symbol `,`, Number `0x1F`, Symbol `,`, Number `1.5e-3`, Symbol `,`, Parameter `@p`, Symbol `,`, QuotedName `[q]]r]`, Symbol `,`, QuotedName `"s"`, Word `FROM`, Word `t`.
- Comments and hints are dropped: `SELECT /* c */ 1 -- d\n, 2` gives `SELECT`, `1`, `,`, `2`.
- `<=`, `>=`, `<>`, `!=`, `!<`, `!>` are one Symbol each; `@@ROWCOUNT` is one Word; `#t` and `##t` are one Word.
- `N 'x'` (with a gap) is Word `N` then String `'x'`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*TsqlTokensTests"`
Expected: FAIL, `TsqlTokens` does not exist.

- [ ] **Step 3: Implement `TsqlTokens.Read`**

Over `DescriberSql.Lex(sql, SqlDialectRules.SqlServer)`: `Quoted` opening with `'` is a String (joined with a directly preceding `N`/`n` Word), opening with `[` or `"` a QuotedName; `Parameter` a Parameter; comments and hints dropped; `Text` split into Words (a letter, `_`, `#` or `@` then letters, digits, `_`, `#`, `@`, `$`), Numbers (digits with `.` and an exponent with its sign, or `0x` and hex digits) and Symbols (the two-character operators above, else one character).  Whitespace separates.

- [ ] **Step 4: Run them to see them pass**

- [ ] **Step 5: Write the failing tests of the reader**

`SelectStatementTests`.  Each reads `TsqlTokens.Read(sql)` and `SelectStatement.Read`.  Names compared as written below, unquoted.
- `Read_PlainSelect_ItemsAndNames`: `SELECT c.id, c.name AS display, n = COUNT(*), 'k' = 1, x y, [a b] FROM dbo.c AS c WHERE c.id > 0 ORDER BY 1` gives six items named `id`, `display`, `n`, `k`, `y`, `a b`, `Grouping` `None`, `FromThroughWhere` from `FROM` to the end of the `WHERE`.
- `Read_WithClause_KeepsItAndItsNames`: `;WITH a AS (SELECT 1 AS x), b (y) AS (SELECT 2) SELECT a.x FROM a` gives `CteNames` `a`, `b` and `With` covering `WITH ... )`.
- `Read_TopDistinct_SkipsThem`: `SELECT DISTINCT TOP (@n) PERCENT WITH TIES a FROM t` and `SELECT TOP 10 a FROM t` each give one item `a`, `HasTop` true.
- `Read_CaseAndWithinGroup_OneItemEach`: `SELECT CASE WHEN a > 1 THEN 'x' ELSE 'y' END AS k, STRING_AGG(b, ',') WITHIN GROUP (ORDER BY b) AS s FROM t GROUP BY a` gives two items, `Grouping` `Plain`.
- `Read_NextValueFor_IsAnItem`: `SELECT NEXT VALUE FOR dbo.s AS n` gives one item `n`, `HasFrom` false.
- `Read_Groupings_AreRead`: `GROUP BY a` Plain; `GROUP BY ALL a` All; `GROUP BY ROLLUP (a)`, `GROUP BY CUBE (a)`, `GROUP BY GROUPING SETS ((a), ())`, `GROUP BY a WITH ROLLUP`, `GROUP BY ()` each Sets.
- `Read_Star_IsAStarItem`: `SELECT * FROM t` and `SELECT t.* FROM t` give one item with `IsStar`.
- `Read_OneTable`: `IsOneTable` true for `FROM dbo.t`, `FROM dbo.t AS x WITH (NOLOCK) WHERE x.a = 1`, `FROM [s].[t] x`; false for a join, `, u`, `CROSS APPLY`, a derived table `(SELECT ...) d`, a function `dbo.f(1) f`, `FOR SYSTEM_TIME ALL`, `PIVOT`, `TABLESAMPLE`, and `FROM a` where `a` is a CTE name.
- `Read_Refused_IsNull`, a theory, each `null`: `SELECT a INTO #x FROM t`; `SELECT a FROM t FOR JSON PATH`; `SELECT a FROM t UNION SELECT b FROM u`; `SELECT a FROM t; SELECT b FROM u`; `UPDATE t SET a = 1 OUTPUT inserted.a`; `DECLARE @x int; SELECT 1 AS a`; `(SELECT a FROM t)`; `WITH c AS (SELECT 1 AS a) INSERT INTO t SELECT a FROM c`; an empty text; `SELECT FROM t`; `SELECT a, FROM t` (an empty item).
- `Read_ItemName_WhereItCannotBeRead_IsNull`: `SELECT a + 1 FROM t` and `SELECT CASE WHEN a = 1 THEN 1 END FROM t` give one item with `Name` null; `SELECT name COLLATE Latin1_General_BIN FROM t` gives `Name` null (a word after `COLLATE` is no alias); `SELECT a AT TIME ZONE 'UTC' FROM t` gives `Name` null (a string after `ZONE` is no alias).
- `Read_Subquery_BodyOfAParenthesizedSelect`: `SelectBody.Read` over the range inside the parentheses of `(SELECT COUNT(*) FROM o WHERE o.c = c.id)` gives one item, `HasHaving`, `HasTop`, `HasOffset` false, `Grouping` None.

- [ ] **Step 6: Run them to see them fail**

- [ ] **Step 7: Implement `SelectStatement.Read` and `SelectBody.Read`**

Depth counts parentheses and `CASE` ... `END` (so a `CASE`'s keywords and operators are never the select's).  The list ends, at depth 0, at `FROM`, `INTO`, `WHERE`, `GROUP` followed by `BY`, `HAVING`, `WINDOW`, `ORDER` followed by `BY`, `OPTION`, `FOR` followed by `XML`, `JSON` or `BROWSE`, `UNION`, `EXCEPT`, `INTERSECT`, `;` or the end.  `FROM ... WHERE` ends at the first of `GROUP BY`, `HAVING`, `WINDOW`, `ORDER BY`, `OPTION`, `FOR XML|JSON|BROWSE`, `;`, the end.  A `FOR` followed by `SYSTEM_TIME` is part of the `FROM`.  A name is unquoted (`[a]]b]` is `a]b`, `"a""b"` is `a"b`, `'a''b'` is `a'b`, `N'..'` drops the `N`).  An item's name: `name = expr` (Word, QuotedName or String, then `=`); `expr AS name`; `expr name` when the last token is a Word, QuotedName or String and the one before it is not `.`, an operator, `COLLATE` or `ZONE`; a column alone (`a`, `a.b`, `[s].[t].[c]`) its last part; otherwise null.

- [ ] **Step 8: Run them to see them pass**

- [ ] **Step 9: Add `Proofs/AGENTS.md`**

Rules only: the reader gives up rather than guess, and a misread must prove nothing; nothing here sends anything; link the architecture page section added in Task 6, `#proofs-of-not-null`; the maintenance footer.

- [ ] **Step 10: Validate and commit**

Run `./pre-commit-validation.sh`; expected: every step passed.

```bash
git add src/SqlSource.Tool/Describing/SqlServer/Proofs tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs
git commit -m "Read one T-SQL SELECT's items and clauses for the nullability proofs"
```

---

### Task 2: The proofs of forms

**Files:**
- Create: `Proofs/Proof.cs`, `Proofs/ExpressionProofs.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs`

**Interfaces:**
- Consumes: Task 1's types.
- Produces:
  - `internal abstract record Proof` with nested `sealed record`s `Always`, `Never`, `Operand(int Index)`, `AnyOf(IReadOnlyList<Proof> Parts)`, `AllOf(IReadOnlyList<Proof> Parts)`; static `Proof.Yes`, `Proof.No`; `bool Holds(IReadOnlyList<bool>? operandNotNull)` (an `Operand` holds only when the list is given and its entry is true); `bool NeedsOperands`.
  - `internal sealed class ExpressionProofs(IReadOnlyList<TsqlToken> tokens, SelectBody body)` with `Proof Of(TokenRange expression)` (the item's expression, in the select's context) and `IReadOnlyList<TokenRange> Operands` (each asked range once, in order of first use; `Operand.Index` indexes it).

- [ ] **Step 1: Write the failing tests**

A helper reads `SELECT <item> AS c FROM t AS t <tail>` and gives `Of(items[0].Expression)` evaluated with every operand answered true, and with every operand answered false, plus the operands' texts.  Write each row of the table below as one `[InlineData]` of a theory `Of_Form_IsProvedAsExpected(item, tail, provedWhenOperandsNotNull, provedWhenOperandsNull, string operands)` (`operands` the asked texts joined by `|`, empty for none).

| Item | Tail | Proved, operands not null | Proved, operands null | Operands |
|----|----|----|----|----|
| `COUNT(*)` | | true | true | |
| `COUNT(DISTINCT t.a)` | `GROUP BY t.b` | true | true | |
| `COUNT_BIG(t.a) OVER (PARTITION BY t.b)` | | true | true | |
| `(SELECT COUNT(*) FROM o WHERE o.c = t.id)` | | true | true | |
| `(SELECT COUNT(*) FROM o GROUP BY o.c)` | | false | false | |
| `(SELECT COUNT(*) FROM o HAVING COUNT(*) > 1)` | | false | false | |
| `(SELECT TOP (1) COUNT(*) FROM o)` | | false | false | |
| `ROW_NUMBER() OVER (ORDER BY t.a)` | | true | true | |
| `RANK() OVER (ORDER BY t.a)`, `DENSE_RANK() OVER (ORDER BY t.a)`, `NTILE(4) OVER (ORDER BY t.a)` | | true | true | |
| `NTILE(@n) OVER (ORDER BY t.a)` | | false | false | |
| `SUM(t.a)` | `GROUP BY t.b` | true | false | `t.a` |
| `MAX(DISTINCT t.a)` | `GROUP BY ROLLUP (t.b)` | true | false | `t.a` |
| `MIN(t.a)` | (no `GROUP BY`) | false | false | |
| `MAX(t.a)` | `GROUP BY ALL t.b` | false | false | |
| `SUM(t.a) OVER (ORDER BY t.id ROWS UNBOUNDED PRECEDING)` | | true | false | `t.a` |
| `SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 2 PRECEDING AND 1 FOLLOWING)` | | true | false | `t.a` |
| `SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)` | | false | false | |
| `SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 2 PRECEDING AND 1 PRECEDING)` | | false | false | |
| `MAX(t.a) OVER w` | `WINDOW w AS (ORDER BY t.id)` | false | false | |
| `COALESCE(t.a, t.b)` | | true | false | `t.a|t.b` |
| `COALESCE(t.a, 0)` | | true | true | `t.a` |
| `COALESCE(t.a, NULL, @p)` | | true | false | `t.a` |
| `COALESCE(t.a, t.b)` | `GROUP BY ROLLUP (t.a, t.b)` | false | false | |
| `CAST(CASE WHEN t.a > 1 THEN 1 ELSE 0 END AS bit)` | | true | false | `CASE WHEN t.a > 1 THEN 1 ELSE 0 END` |
| `CONVERT(varchar(10), t.d, 23)` | | true | false | `t.d` |
| `CONVERT(varchar(10), t.d, @style)` | | false | false | |
| `TRY_CAST(t.a AS int)` | | true (the server's answer, which is nullable) | false | `TRY_CAST(t.a AS int)` |
| `JSON_OBJECT('a': t.a)`, `JSON_ARRAY(t.a)` | | true | true | |
| `t.a * t.b` | | true | false | `t.a|t.b` |
| `COALESCE(p.name + N' > ', N'') + t.name` | | true | false | `t.name` |
| `-t.a` | | true | false | `t.a` |
| `t.a + NULL` | | false | false | `t.a` |
| `SUM(t.q * t.p)` | `GROUP BY t.b` | true | false | `t.q|t.p` |
| `GREATEST(t.a, t.b)` | | true | false | `t.a|t.b` |
| `LEAST(t.a, 1)` | | true | true | `t.a` |
| `dbo.COUNT(t.a)` | | false | false | |
| `t.a` | `GROUP BY ROLLUP (t.a)` | false | false | |
| `t.a` | `GROUP BY t.a` | true | false | `t.a` |
| `AVG(t.a)` | `GROUP BY t.b` | false | false | |
| `CAST(SUM(t.a) AS bigint)` | `GROUP BY t.b` | true | false | `t.a` |
| `SUM(SUM(t.a)) OVER ()` | `GROUP BY t.b` | true | false | `t.a` |
| `(t.a)` | | true | false | `t.a` |
| `'x'`, `N'x'`, `1`, `0x00` | | true | true | |
| `NULL`, `@p` | | false | false | |
| `t.a AT TIME ZONE 'UTC'` | | true (the server's answer) | false | `t.a AT TIME ZONE 'UTC'` |

Also `Operands_SameRangeTwice_AskedOnce`: `COALESCE(t.a, t.a)` gives one operand.

- [ ] **Step 2: Run them to see them fail**

- [ ] **Step 3: Implement `Proof` and `ExpressionProofs`**

The rules are the design's decision 2 table and its paragraph on context.  What the table leaves to the code:
- An expression is split at depth 0 (parentheses and `CASE`...`END`) on `+ - * / %`; a sign at the start or after an operator is unary (`-`, `+`, `~`).  When every piece is a primary (below) the expression is `AllOf` its pieces; when there is one piece it is that primary; when anything else stands at depth 0 (another symbol, `AND`, `OR`, `IS`, `IN`, `LIKE`, `BETWEEN`, `COLLATE`, `AT`, `FOR`...) the whole expression is a leaf.
- A primary is a literal (String, Number), `NULL`, a Parameter, a column (`a`, `a.b`, `[a].[b]`), `( ... )`, a call `name ( ... ) [WITHIN GROUP ( ... )] [OVER ( ... ) | OVER name]`, a `CASE ... END`.  A call's name is bare when no `.` precedes it; a schema-qualified call is a leaf that is never asked.
- A leaf is asked (`Operand`) when its context allows, it is not a Parameter or `NULL`, and it holds at its own level (outside a parenthesized `SELECT`) no aggregate's name followed by `(`, no `OVER` and no schema-qualified call.  The aggregate names: `COUNT`, `COUNT_BIG`, `SUM`, `AVG`, `MIN`, `MAX`, `STDEV`, `STDEVP`, `VAR`, `VARP`, `STRING_AGG`, `GROUPING`, `GROUPING_ID`, `CHECKSUM_AGG`, `APPROX_COUNT_DISTINCT`, `APPROX_PERCENTILE_CONT`, `APPROX_PERCENTILE_DISC`.  Otherwise it is `Never`.
- The select's context forbids asking when `body.Grouping` is `Sets`; the rows' context (an aggregate's operand without `OVER`) always allows.  A window aggregate's operand keeps the select's context.  `body.Grouping` `All` proves nothing in the whole item: `Of` returns `Never`.
- `( SELECT ... )` is read with `SelectBody.Read` over the inner range: proved when its one item is a `COUNT`/`COUNT_BIG` form without `OVER` and it has no `GROUP BY`, `HAVING`, `TOP` or `OFFSET`; any other subquery is a leaf.

- [ ] **Step 4: Run them to see them pass**

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource.Tool/Describing/SqlServer/Proofs tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs
git commit -m "Prove the forms of a T-SQL column that cannot be null, leaving operands to ask"
```

---

### Task 3: The call `operands` and the corrected columns

**Files:**
- Create: `Proofs/OperandsQuery.cs`, `Proofs/ColumnProofs.cs`; fixture `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/select-proved-forms/query.sql` (recorded files by the script)
- Modify: `SqlServerCallNames.cs`, `SqlServerSession.cs` (`ResultAsync`), `SqlServerSession.Calls.cs`, `SqlServerEvents.cs`, `src/SqlSource.Tool/AGENTS.md`, `tests/SqlSource.Tests.SqlServer/Realistic/Describer/RealisticNullabilityNoiseTests.cs`, every recorded fixture and sidecar that changes
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/OperandsQueryTests.cs`, `.../Proofs/ColumnProofsTests.cs`, `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs`, `SqlServerEventsTests.cs` (one more event)

**Interfaces:**
- Consumes: Tasks 1 and 2.
- Produces:
  - `SqlServerCallNames.Operands = "operands"`.
  - `OperandsQuery.Text(string sql, IReadOnlyList<TsqlToken> tokens, SelectStatement statement, IReadOnlyList<TokenRange> operands) -> string`.
  - `OperandsQuery.NotNullOf(SqlServerColumnsAnswer answer, int count) -> IReadOnlyList<bool>?` — null when the answer has an `error`, error rows, or its visible rows are not `count` rows named `1` to `count` in order.
  - `ColumnProofs.Read(string sql, IReadOnlyList<BuiltColumn> columns, IReadOnlyList<SqlServerColumnRow> rows) -> ColumnProofs`, with `string? OperandsSql` (null when nothing needs asking) and `IReadOnlyList<BuiltColumn> Apply(IReadOnlyList<bool>? operandNotNull)`.
  - `SqlServerSession.OperandsAsync(string query, SqlServerColumnsRequest request, CancellationToken)`, answered by `_live.ColumnsAsync`, logged by `SqlServerEvents.Operands` as `mssql.operands` with the members of `mssql.columns`.

- [ ] **Step 1: Write the failing tests of the text and the answer**

`OperandsQueryTests`:
- `Text_WithAndWhere_RearrangesTheQuery`: for `WITH c AS (SELECT 1 AS a) SELECT COALESCE(t.x, t.y) AS z FROM t JOIN c ON c.a = t.id WHERE t.id > @p GROUP BY t.x, t.y ORDER BY z` and operands `t.x`, `t.y`, the text is exactly `"WITH c AS (SELECT 1 AS a)\nSELECT t.x\nAS [1],\nt.y\nAS [2]\nFROM t JOIN c ON c.a = t.id WHERE t.id > @p"` — each piece the source's own characters from its first token's start to its last token's end.
- `Text_LineCommentInOperand_EndsBeforeTheAlias`: operand `COALESCE(t.a, -- x\n t.b)` keeps its comment, and the text parses: the alias stands on the next line.
- `Text_NoFrom_SelectsOperandsAlone`: `SELECT CAST(NEXT VALUE FOR dbo.s AS int) AS a` with its one operand gives `"SELECT NEXT VALUE FOR dbo.s\nAS [1]"`.
- `NotNullOf_Rows_EachOperandsAnswer`: rows `1` not nullable, `2` nullable, plus the hidden `ROWSTAT` → `[true, false]`.
- `NotNullOf_Unusable_IsNull`: an `error`; error rows; one row for two operands; rows named `2`, `1`.

- [ ] **Step 2: Run, see them fail; implement `OperandsQuery`; run, see them pass**

- [ ] **Step 3: Write the failing tests of the columns**

`ColumnProofsTests`, over `SqlServerScript`'s rows (`IdColumn` and rows built `with` it):
- `Read_NothingToAsk_OperandsSqlIsNull`: `SELECT COUNT(*) AS n FROM dbo.users` → `OperandsSql` null, `Apply(null)` gives `n` `false`, `expression`.
- `Apply_OperandNotNull_ColumnProved`: `SELECT COALESCE(nickname, name) AS shown FROM dbo.users` → `OperandsSql` asks `nickname` and `name`; `Apply([false, true])` gives `false`, `expression`; `Apply([false, false])` and `Apply(null)` keep the server's `true`, `server`.
- `Read_NameDiffers_ProvesNothing`: the server names the column `other` where the item says `n` → `Apply(null)` keeps the server's answer.
- `Read_CountDiffers_ProvesNothing`: two items, one visible row.
- `Read_RefusedText_ProvesNothing`: a `UNION`.
- `Apply_ServerAlreadyNotNull_KeepsServer`: a column the server answers not null keeps `server`.

- [ ] **Step 4: Run, see them fail; implement `ColumnProofs`; run, see them pass**

`ColumnProofs.Read` reads the statement, maps each non-star item to the column of its place when the counts agree and every item's `Name` equals the column's name ordinally, and builds a `Proof` for each column the server answers nullable.  `Apply` sets `Nullable = false` and `NullableSource = expression` on a column whose proof holds; every other column is returned as it came.

- [ ] **Step 5: Write the failing session tests**

`SqlServerSessionProofTests`, with `SqlServerScript`:
- `DescribeAsync_FormNeedsOperands_AsksOperandsBeforeTable`: the calls are `parameters`, `columns`, `operands`, and the column is corrected; when every column comes from one table, `table` follows `operands`.
- `DescribeAsync_OperandsFails_KeepsTextProofsAndGoesOn`: `operands` answers `Lost`; the `COUNT(*)` column is still `false`, the `COALESCE` column the server's, and the describe succeeds.
- `DescribeAsync_NothingToAsk_NoOperandsCall`: `SELECT COUNT(*) AS n FROM dbo.users` asks no `operands`.
- `SqlServerEventsTests`: `Operands_Written_LikeColumns` — `mssql.operands` holds `query`, `sql`, `params`, the answer and `durationMs`, in the order of `mssql.columns`; the event order of a describe is `columns`, `operands`, each `column`, `table`, `table-match`.

- [ ] **Step 6: Run, see them fail; wire the session; run, see them pass**

In `ResultAsync`, after `SqlServerColumns.Build` and before the `mssql.column` events: `ColumnProofs.Read(request.Sql, built, rows)`; when `OperandsSql` is not null, `OperandsAsync(request.Name, new SqlServerColumnsRequest(proofs.OperandsSql, declarations))` and `OperandsQuery.NotNullOf(answer, ...)`; then `Apply`.  An `operands` answer never fails the describe.

- [ ] **Step 7: Add the fixture `select-proved-forms`**

`query.sql`, over the fixture schema's `dbo.orders` and `dbo.users`, one query whose columns are, each aliased: a grouping key, `COUNT(*)`, `SUM` of a `NOT NULL` column, `MAX` of a product of two `NOT NULL` columns, `COALESCE(u.nickname, u.name)`, a `CAST` of the grouping key, and `RANK() OVER (ORDER BY ...)`, `FROM dbo.orders AS o JOIN dbo.users AS u ON u.id = o.user_id` with a plain `GROUP BY`.  Check `schema.sql` for the columns' names and nullability before writing it.

- [ ] **Step 8: Record, lower the budgets, read the diff**

Run `tools/record-fixtures.sh mssql` and `tools/describe-end-to-end.sh`.  Read the diff: every change is a column going from `true`/`server` to `false`/`expression`, or a capture gaining `operands`; nothing else.  Then run `dotnet test --project tests/SqlSource.Tests.SqlServer --filter-class "*RealisticNullabilityNoiseTests"`; expected FAIL listing 15 noisy columns on 17.  Set `Budgets` to `{ [14] = 15, [17] = 15 }`, and check that the 15 are the design's "what remains" list.  Run the same class on the floor image (`SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`); expected PASS with 15 on 14.  Fix any end-to-end test that asserted a nullable property of a column now proved (the type is now `int`, not `int?`).

- [ ] **Step 9: Update `src/SqlSource.Tool/AGENTS.md`**

The rule "A column's `nullable` is the server's `is_nullable` and is never narrowed" becomes: it is the server's, narrowed only by a proof of `Describing/SqlServer/Proofs/` (`expression`) and the computed column (Task 4), never widened; `operands` joins the list of what the user's text is sent as.

- [ ] **Step 10: Validate on both images and commit**

`./pre-commit-validation.sh`, then the floor-image run of `tests/SqlSource.Tests.SqlServer`; both pass.

```bash
git add -A
git commit -m "Correct the SQL Server columns the proofs show cannot be null, asking the server about their operands"
```

---

### Task 4: A `PERSISTED NOT NULL` computed column

**Files:**
- Modify: `SqlServerCalls.cs`, `SqlServerLive.cs`, `Proofs/ColumnProofs.cs`, `SqlServerSession.cs` (`MatchAsync`), `SqlServerTableMatcher.cs` (remarks)
- Create: fixture `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/select-computed-outer-join/query.sql`
- Test: `ColumnProofsTests`, `SqlServerSessionTableTests`, `tests/SqlSource.Tests.SqlServer/Describer/ColumnTests.cs`, `TableMatchTests.cs`

**Interfaces:**
- Consumes: Task 3's `ColumnProofs`.
- Produces: `SqlServerColumnRow.PersistedNotNull` (`bool? = null`, last member); `ColumnProofs.TableRows(IReadOnlyList<SqlServerColumnRow> rows) -> IReadOnlyList<SqlServerColumnRow>` (each row with `PersistedNotNull == true` given `IsNullable = false`).

- [ ] **Step 1: Write the failing tests**

- `ColumnTests.ColumnsAsync_PersistedNotNull_IsRead` (server): `SELECT name_len, name_len_p, id FROM dbo.users` gives `PersistedNotNull` false, true, false; the same through `dbo.users` read by `LEFT JOIN` still true (it is the catalog's).
- `ColumnProofsTests.Apply_PersistedNotNullFromOneTable_Catalog`: `SELECT id, name_len_p FROM dbo.users WHERE id = @id` → `name_len_p` `false`, `catalog`, with no operand asked; and for `SELECT * FROM dbo.users` too.
- `ColumnProofsTests.Apply_PersistedNotNull_StaysNullable`, a theory: a `LEFT JOIN`; `FROM dbo.orders AS o JOIN dbo.users AS u ...`; `FROM (SELECT * FROM dbo.users) AS u`; `WITH users AS (...) SELECT name_len_p FROM users`; `GROUP BY ROLLUP (name_len_p)`; `CAST(name_len_p AS bigint)` (an expression, not a column).
- `SqlServerSessionTableTests.DescribeAsync_PersistedNotNull_TableRowsCorrected`: the `table` rows' `name_len_p` is corrected, so the match is `matched`.

- [ ] **Step 2: Run, see them fail**

- [ ] **Step 3: Implement**

`ColumnsSql` reads one more column, `persisted_not_null`, as `bit`: true when `is_computed_column = 1`, no `source_server`, the source database is `DB_NAME()` (compared as `source_type` already is), and `sys.computed_columns` holds the source column of `OBJECT_ID(QUOTENAME(source_schema) + N'.' + QUOTENAME(source_table))` with `is_persisted = 1` and `is_nullable = 0`, its name compared under the database's collation.  `SqlServerLive` reads it into the new member.  In `ColumnProofs`, a visible column whose row has `PersistedNotNull == true` is `false`, `catalog`, when `Body.IsOneTable`, `Body.Grouping` is `None` or `Plain`, and its item is a column alone or the one item is a `*`.  `MatchAsync` matches against `ColumnProofs.TableRows(rows)`.

- [ ] **Step 4: Run, see them pass**

- [ ] **Step 5: Add the fixture `select-computed-outer-join`, record, read the diff**

`query.sql`: `SELECT o.id, u.name_len_p FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id`, its `-- name:` `OrdersWithNameLength`.  Run `tools/record-fixtures.sh mssql` and `tools/describe-end-to-end.sh`.  Expected diff: every `columns` and `table` row gains `persistedNotNull`; `select-computed-not-null`'s `name_len_p` becomes `false`, `catalog`, still `matched`; the new case's `name_len_p` stays `true`, `server`.  The budgets do not move (no corpus column is of this kind); `RealisticNullabilityNoiseTests` passes as it is.

- [ ] **Step 6: Validate on both images and commit**

```bash
git add -A
git commit -m "Answer a PERSISTED NOT NULL computed column read from its one table not null"
```

---

### Task 5: The proofs on a server, and the attempts to break them

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/Describer/ProofTests.cs`

**Interfaces:**
- Consumes: `Sessions.ShouldDescribeAsync`, `SqlServerContainer.ExecuteAsync`/`ScalarAsync`.

- [ ] **Step 1: Write the tests**

The class creates `dbo.proof_rows (id int NOT NULL PRIMARY KEY, g int NULL, a int NOT NULL, b int NULL)` and `dbo.proof_other (id int NOT NULL, rows_id int NULL)` with rows that make each nullable case give `NULL`, and drops them after (as `ColumnTests.ColumnsAsync_JsonAndVector_On2025` does).  Each test describes its query and checks the column's `nullable` and `nullableSource`, then runs the query and checks for a `NULL` in the column (`SELECT COUNT(*) FROM (<query>) AS q WHERE q.c IS NULL`):
- `Describe_ProvedForm_NotNullAndNoNullRow`, a theory over one query of each proved form of the design's goal 1 (13 rows, the arithmetic and `GREATEST`/`LEAST` included, `GREATEST`/`LEAST` and JSON skipped below 16): `false`, `expression`, and no `NULL`.
- `Describe_BreakingAttempt_NullableAndNullRow`, a theory, each `true` and at least one `NULL` row: `MAX(a)` with no `GROUP BY` over `WHERE 1 = 0`; `MAX(a)` under `GROUP BY ALL g` with a `WHERE` that leaves a group empty; `SUM(o.id)` with `proof_other` on the outer side of a `LEFT JOIN`, grouped; a scalar subquery `(SELECT COUNT(*) FROM proof_other GROUP BY rows_id HAVING ...)` that finds no group; `SUM(a) OVER (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)`; `COALESCE(g, b)` under `GROUP BY ROLLUP (g, b)`; `CAST(g AS bigint)` under `GROUP BY ROLLUP (g)`; `CONVERT(varchar(30), SYSDATETIME(), @style)` with `@style` `NULL`; `TRY_CAST(N'x' + CAST(a AS nvarchar(10)) AS int)`; `COALESCE(b, NULL)`; on 16 and later, `(SELECT TOP (1) JSON_OBJECT('a': a) FROM proof_rows WHERE 1 = 0)`; `a + b`; `GREATEST(b, NULL)` on 16 and later.
- `Describe_PersistedNotNull_OnlyFromOneTable`: on a table with `c AS (1) PERSISTED NOT NULL`, from the table alone `false`, `catalog`; through a `LEFT JOIN` that finds no row, `true` and a `NULL` row.
- `Grouping_EmptyInput_ReturnsNoRow`: design fact 3 — `GROUP BY ROLLUP`, `CUBE`, `GROUPING SETS ((g), ())` and `GROUP BY ()` over `WHERE 1 = 0` each return no row.
- `Settings_AnsiWarningsOff_OverflowIsNull`: design fact 1 — with `SET ANSI_WARNINGS OFF; SET ARITHABORT OFF;` in the same batch, `CAST(300 AS tinyint)` is `NULL`; with `ANSI_WARNINGS ON` it raises `220`.  This pins the assumption the known issue documents.

- [ ] **Step 2: Run on 2025 and on 2017**

Run: `dotnet test --project tests/SqlSource.Tests.SqlServer --filter-class "*ProofTests"`, then again with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
Expected: PASS on both.  A failure of a breaking attempt is a wrong `false`: fix the reader or the proof (Tasks 1-4), never the test.

- [ ] **Step 3: Validate and commit**

```bash
git add tests/SqlSource.Tests.SqlServer/Describer/ProofTests.cs
git commit -m "Hold each SQL Server nullability proof against queries that try to break it"
```

---

### Task 6: Documentation and TD-0071

**Files:**
- Modify: `docs/architecture/sqlserver-describer.md`, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (`nullableSource`), `docs/manual/sqlserver.md`, `examples/SqlServerExamples/Models/Orders.sql` and its sidecar, `examples/SqlServerExamples/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `docs/tech-debt/TD-0071-sql-server-answers-nullable-for-columns-that-cannot-be-null.md`, `docs/tech-debt/README.md`, `src/SqlSource.Tool/AGENTS.md` if Task 3's wording needs the link

- [ ] **Step 1: The architecture page**

- The opening paragraphs and "Columns": the nullability is the server's, narrowed by the proofs; no plan walk, `plan` still `skipped`.
- The files table: the folder `Proofs/` and each file.
- The calls table: `operands`, asked after `columns` only when a proof needs it, its request and answer `columns`', never reported.  "One describe, step by step" and its flowchart: the proofs and `operands` between "The columns built" and `table`.
- A new section `## Proofs of not null` (anchor `proofs-of-not-null`): the reader, the forms, the contexts, `operands`' text, the computed column, the `ANSI_WARNINGS` assumption, and that the corpus's safety tests and `ProofTests` hold them.
- "Why a query cannot run a statement": a row for `operands`.  "Events": `mssql.operands`.  "Fixtures": the two new cases.  "The realistic corpus": the budgets 15 and 15; "no correction of the describer was needed" becomes what the proofs correct.

- [ ] **Step 2: The sidecar format**

`nullableSource`: `"catalog"` on SQL Server is a `PERSISTED NOT NULL` computed column read from its one table; `"expression"` on SQL Server is a column the describer proved from its expression and the server's answer about its operands.  `"server"` is no longer "every SQL Server column".

- [ ] **Step 3: The manual**

`docs/manual/sqlserver.md` "Which columns can be null": the list of what SqlSource treats as never null gains the proved forms (in the user's words, no internals); the list of what stays possibly null keeps only what remains (TD-0071's rewrite).  Move the `-- column:` sample off `COUNT(*)` to a column that stays possibly null (`EOMONTH(o.placed_at) AS month_end` or another the server answers nullable; check it on the container), and its paragraph with it.  "Known issues": the "Some columns that are never null" item lists only what remains; add: a connection that turns `ANSI_WARNINGS` and `ARITHABORT` off reads `NULL` where an overflow or a divide by zero would be an error, in a column SqlSource calls never null; SqlClient turns `ANSI_WARNINGS` on, so leave it on.  Check the `sql mssql` samples still parse (the documentation tests).

- [ ] **Step 4: The example**

`CountShippedOrders` in `examples/SqlServerExamples/Models/Orders.sql`: its `-- column: shipped not null` is now redundant; move the marker to a column the server answers nullable and the describer cannot prove (as the manual's sample), re-run `tools/describe-end-to-end.sh`, and update the README row that describes it.

- [ ] **Step 5: TD-0071**

Rewrite it to what remains, as `docs/tech-debt/AGENTS.md` "Changing an item" says: 15 columns on each major (the design's list), by kind; the computed column read through a join, `OUTPUT` statements and set operations as the proofs' limits; Impact stays Medium or becomes Low as the counts warrant (15 of 193 and 209); Proposed fix: the next kinds on the same reader (`EOMONTH`, `AT TIME ZONE`, `LAG`/`LEAD` with a default, a `UNION` whose every branch proves, `OUTPUT inserted` of a computed column).  Update its row in `docs/tech-debt/README.md` and the README of the SQL Server tests ("The noise budget", fixture list).

- [ ] **Step 6: Validate and commit**

`./pre-commit-validation.sh` (the documentation and link tests run in it).

```bash
git add -A
git commit -m "Document the SQL Server nullability proofs and narrow TD-0071 to what remains"
```
