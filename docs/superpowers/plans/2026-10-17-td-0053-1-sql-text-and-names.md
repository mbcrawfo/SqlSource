# TD-0053 PR 1, SQL text and names - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix items 1, 2, 3 and 11 of TD-0053, and from item 8 the `ServerLines` repeat, the `$n` number reader and `Invariant`, with only the behaviour changes the user approved.

**Architecture:** A new `PostgresIdentifiers` holds the quoted-name reader, the unescape, the bare-name rule, PostgreSQL's word rule (from the lexer's `QuoteReader`) and the `$n` number reader; a new `PostgresSql.Lex` is the one lex-or-throw; `Undeclared` and `Uncast` move from `PostgresErrors` to a new `UncastParameters`.  Every copy listed in the design calls them.

**Tech Stack:** C# (.NET 8 tool, netstandard2.0 generator), xUnit, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-17-td-0053-1-sql-text-and-names-design.md`, under the overview `docs/superpowers/specs/2026-10-17-td-0053-postgresql-describer-patterns-design.md`.

## Global Constraints

- No fixture, `capture.json`, `expected.json` or `expected-pg*.txt` changes; no log event's text changes.
- The only answers that change: the user's SQL read with PostgreSQL's word rule, and `1$1` read as a parameter.
- `DeclaredTypeText` keeps `char.IsLetterOrDigit || '_'`, digit allowed first, and its CR/LF/NUL refusal, and never unescapes.
- Stay out of `PlanPartitions.All`, `Appends`, `MembersOf`, the node-type literals, `NullExtends`, `PostgresSession` (but the one `ServerLines` line), `PostgresLive`, the type tables.
- Every existing test passes unchanged.
- `./pre-commit-validation.sh` before every commit; container tests also on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.

Paths below are under `src/SqlSource.Tool/Describing/Postgres/` and `tests/SqlSource.Tool.Tests/Describing/Postgres/` unless they say otherwise.

## Review Focus

- `OutputReference.Parse` on any entry whose name runs into a character that is not `.` (`u.id `, `u.id::text`, `u..id`): still null, now that the bare run is read up to a `.` and checked whole.  Pinned by the existing `Parse_Expression_IsNoReference` cases.
- `PostgresSearchPath` on `"a"b, pg_catalog` (a quoted and a bare piece in one element): still one schema, quoted.  Add the case to `PutsCatalogFirst_PathThatNamesAnotherSchemaFirstOrCannotBeRead_IsFalse` in Task 2.
- `PlanExpression` on an entry holding `$1` after an operator (`(a + $1)`): `$` stays a symbol, so `MayNameColumn` still sees it.  Pinned by the existing `PlanExpressionTests`; `IsSymbol` excludes only `IsWordStart` characters.
- `PositionalParameter` on `_$1` and `9_$1`: still not a parameter (`_` starts a word).  Add both to `PositionalParameter_NotAParameter_IsNull` in Task 3.
- `NamedIn` on a message with `$` and no digits before a `$2`: still finds `$2`.  Pinned by the reader's test in Task 1.

---

### Task 1: `PostgresIdentifiers` and `PostgresSql`

**Files:**
- Modify: `src/SqlSource/Parsing/Quoting/QuoteReader.cs:39` (`IsNameCharacterOfPostgreSql` from `protected` to `internal`)
- Create: `PostgresIdentifiers.cs`, `PostgresSql.cs`
- Test: `PostgresIdentifiersTests.cs`, `PostgresSqlTests.cs`

**Interfaces (produces):**
- `PostgresIdentifiers.SkipQuoted(string text, int at, out int end) -> bool`: the quote at `at` (`"` or `'`), doubled read as one; `end` is the index after the closing quote, or `text.Length` and false when none closes.
- `PostgresIdentifiers.Unquote(ReadOnlySpan<char> quoted) -> string`: the quoted name with its quotes, inner `""` made `"`.
- `PostgresIdentifiers.IsBareName(ReadOnlySpan<char> name) -> bool`: `[a-z_][a-z0-9_]*`.
- `PostgresIdentifiers.IsWordStart(char) -> bool`: ASCII letter, `_`, or above U+007F.
- `PostgresIdentifiers.IsWordPart(char) -> bool`: `QuoteReader.IsNameCharacterOfPostgreSql`.
- `PostgresIdentifiers.ParameterNumberAt(string text, int at) -> int?`: the n of a `$n` at `at`, or null (no `$`, no digit, or overflow).
- `PostgresSql.Lex(string sql) -> EquatableArray<SqlLexeme>`: throws `InvalidOperationException("The sample SQL of a query does not lex, and the plan's parser read it.")`.

- [ ] Tests: `SkipQuoted` on `"ab" x` (true, end 4), `"a""b"` (true, end 6), `'it''s'` (true), `"open` (false, end 5); `Unquote("\"a\"\"b\"")` is `a"b`, `Unquote("\"\"")` is empty; `IsBareName` true for `id`, `_x1`, false for `Id`, `1x`, `café`, empty; `IsWordStart` / `IsWordPart` on `a` (T/T), `1` (F/T), `_` (T/T), `$` (F/T), `é`, `€`, `\uD83D` (T/T); `ParameterNumberAt` on `$12 x` at 0 (12), `$x` (null), `a` (null), `$99999999999` (null).  `PostgresSql.Lex("SELECT 1")` is not empty; `Lex("SELECT 'open")` throws with the message above.
- [ ] Run, see them fail to compile; implement; run green.

### Task 2: one quoted-name reader and one bare-name rule (item 1)

**Files:** `Walk/PlanExpression.cs`, `Walk/PlanPartitions.cs`, `Walk/OutputReference.cs`, `DeclaredTypeText.cs`, `PostgresSearchPath.cs`, `PostgresTypeNames.cs`; tests `Walk/OutputReferenceTests.cs`, `Walk/PlanPartitionsTests.cs`, `PostgresSearchPathTests.cs`.

- `PlanExpression`: `Kind`, `Token` and `Tokens` become `internal` nested (`Tokens.Read` public); `Tokens.SkipQuoted` deleted for `PostgresIdentifiers.SkipQuoted`; `MayNameColumn`'s unescape is `Unquote`.  (Word rule in Task 3.)
- `PlanPartitions.WithoutAlias` becomes `public`, reads `PlanExpression.Tokens.Read(entry)`, returns the entry unchanged when it is null; copies the text between tokens as it is; replaces a `Word` (its text) or `QuotedName` (`Unquote`) token by `\u0001` when it equals the alias, the next token is a `Dot` with `Start == token.End`, and the previous token is not a `Dot` with `End == token.Start`.  `Closing` is deleted.
- `OutputReference.Bare` reads to the next `.` or the end and answers it only when `IsBareName`; `Quoted` is `SkipQuoted` + `Unquote`, still null for an empty name.
- `DeclaredTypeText.TryReadQuoted` is `SkipQuoted` and then refuses CR, LF or NUL between the quotes.
- `PostgresSearchPath.Schemas` reads a quoted piece with `SkipQuoted` + `Unquote` (null when unclosed); its bare piece is unchanged.
- `PostgresTypeNames.IsBare` deleted for `IsBareName`.
- [ ] Tests: `OutputReferenceTests.Parse_Expression_IsNoReference` adds `CURRENT_DATE`, `CURRENT_USER`, `LOCALTIMESTAMP`.  `PlanPartitionsTests`: `WithoutAlias_QualifyingAlias_IsMarked` (`e_1.id`, `(e_1.id + e_1.n)`), `WithoutAlias_AliasThatQualifiesNothing_IsKept` (`e_1`, `x.e_1.id`→ only the first `x` could qualify, `e_1` kept), `WithoutAlias_QuotedAlias_IsMarked` (`"E_1".id` with alias `E_1`), `WithoutAlias_EntryThatDoesNotRead_IsUnchanged` (`(e_1.id`, `'open`).  `PostgresSearchPathTests`: `"a"b, pg_catalog` is false.
- [ ] Run the unit tests green.

### Task 3: one word rule (item 2), `Uncast` moved

**Files:** `StatementShape.cs`, `PostgresErrors.cs`, create `UncastParameters.cs`, `OuterJoinGate.cs`, `Walk/PlanExpression.cs`, `Walk/PlanPartitions.cs` (only if it still has a rule), `DeclaredTypeText.cs` (remark only); tests `StatementShapeTests.cs`, `OuterJoinGateTests.cs`, create `UncastParametersTests.cs`, `DeclaredTypeTextTests.cs`.

**Interfaces:** `UncastParameters.Of(DescribeRequest query) -> List<SqlQueryParameter>`: today's `Undeclared`, lexing without throwing.

- `StatementShape.RefusedKind` scans words with `IsWordPart`.  `PositionalParameter` skips a `$n` when the run of `IsWordPart` characters before the `$`, inside the lexeme, holds an `IsWordStart` one.
- `PostgresErrors` lines 342-439 move to `UncastParameters` with `IsWordPart`; `PostgresErrors` calls `UncastParameters.Of`.
- `OuterJoinGate.IsOfAName` uses `IsWordPart`.
- `PlanExpression.Tokens`: `IsNameStart`/`IsNamePart` deleted for `IsWordStart`/`IsWordPart`; `IsSymbol` excludes `IsWordStart` characters (so `$` stays a symbol).
- `DeclaredTypeText.IsBare` keeps its rule, with a remark: stricter than PostgreSQL on purpose, since `$` would let `1$$` and `1$1` into the `PREPARE`; such a name can be quoted.
- [ ] Tests: `RefusedKind_NameThatHoldsANonLetterAboveAscii_IsReadWhole`: `CREATE€ TABLE t (a int)` and `SELECT 1 AS €into` are null.  `PositionalParameter`: `SELECT 1$1` is `$1`; `SELECT 1a$1`, `SELECT _$1`, `SELECT 9_$1` are null.  `IsGated`: `SELECT x$left FROM t`, `SELECT a AS €left FROM t` are not gated.  `UncastParametersTests`: `SELECT CAST(@x AS int)` none, `SELECT €CAST(@x AS int)` is `x`, SQL that does not lex names every undeclared one.  `DeclaredTypeTextTests.TryRead_NameStricterThanPostgreSql_IsRefused`: `$type`, `my$type`, `1$$`, `1$1`, `€type`, `"a\r\nb"`.
- [ ] Run the unit tests green.

### Task 4: one lex-or-throw (item 3)

- `ParameterPlaceholders.Statement`, `StatementShape.Lexemes` (deleted), `OuterJoinGate.HoldsAnyOf` call `PostgresSql.Lex`.  No signature changes.
- [ ] Existing `*_TextThatDoesNotLex_Throws` tests stay green.

### Task 5: item 8's three parts and item 11

- `PostgresErrors.ServerLinesWithoutPosition(PostgresServerError error) -> EquatableArray<string>`; the five calls in `PostgresErrors` and the one at `PostgresSession.cs:386` use it.
- `PostgresErrors.NamedIn` and `NpgsqlCheck.NumberAt` (deleted) use `ParameterNumberAt`.
- `Invariant` deleted from `NpgsqlCheck` and `PostgresTypeNames` for `string.Create(CultureInfo.InvariantCulture, …)`.
- `PostgresTypeNames`' remark names `DescribeAsync_TypeOfASchemaNamedByAReservedWord_IsSpelledBare`.
- [ ] Unit tests green.

### Task 6: TD-0053 and verification

- Rewrite `docs/tech-debt/TD-0053-*.md` without items 1, 2, 3, 11 and the three parts of 8, keeping 4 to 10 numbered so; rewrite its README row.
- [ ] `./pre-commit-validation.sh` passes; `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres` passes; `git status` shows no fixture or expected file changed.
- [ ] Commit.
