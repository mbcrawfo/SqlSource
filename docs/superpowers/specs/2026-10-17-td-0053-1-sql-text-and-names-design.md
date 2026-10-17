# TD-0053 PR 1, SQL text and names - design

Date: 2026-10-17

Part of [the TD-0053 design](2026-10-17-td-0053-postgresql-describer-patterns-design.md), whose rules for every PR apply here.  This PR fixes items 1, 2, 3 and 11 of [TD-0053](../../tech-debt/TD-0053-duplicated-and-uneven-patterns-in-the-postgresql-describer.md), and three parts of item 8: the `ServerLines` repeat, the `$n` number reader and `Invariant`.  It runs at the same time as PR 2; see "Order" in the overview.

Paths are under `src/SqlSource.Tool/Describing/Postgres/` unless they say otherwise.  Line numbers are those of `ae6e66f`.

## Done

- One reader of a quoted name, one unescape of `""`, one bare-name rule, and one rule for a character of a word of the user's SQL, each with a single definition that every copy listed below calls.
- One lex-or-throw of the user's SQL.
- `Undeclared`, `Uncast` and their helpers live beside `StatementShape`, not in `PostgresErrors`.
- The new tests listed under "Tests" exist and pass; every existing test passes unchanged, except where a test named a member this PR removes or renames.
- TD-0053 is rewritten without items 1, 2, 3 and 11 and without the three parts of 8 (overview, rule 3).

## Item 1: quoted names, the bare-name rule, a plan's name part

**What is there.**  The check of 2026-10-17 found more than the item lists:

| Copy | Unclosed quote | Other |
|---|---|---|
| `Walk/PlanExpression.cs` `Tokens.SkipQuoted` (305-326), unescape at 170 | returns false | |
| `Walk/PlanPartitions.cs` `Closing` (170-191), unescape at 141 | returns the end of the text | `WithoutAlias` (127-166) has its own name rule at 144-146 |
| `Walk/OutputReference.cs` `Quoted` (69-93) | returns null | null for an empty name `""` |
| `DeclaredTypeText.cs` `TryReadQuoted` (170-197) | refuses | refuses CR, LF and NUL; never unescapes |
| `PostgresSearchPath.cs` `Schemas`, unescape at 61-65 | returns null | joins quoted and bare pieces of one element |
| `OuterJoinGate.cs` 103-121 | | compares the text between the quotes without unescaping, on purpose |

That is four unescapes, not three, and a seventh reader in `OuterJoinGate`.

**The fix.**  A new static class `PostgresIdentifiers` in this folder, which holds:

- `SkipQuoted`: given the text and the index of an opening `"`, finds the index after its closing quote, with `""` read as one character of the name.  It reports an unclosed quote as a failure (a `bool` and an `out` end, or an index that says so; the builder's choice).  Every reader in the table finds its closing quote with it.
- `Unquote`: the text between the quotes with `""` made `"`.  Every unescape uses it.
- `IsBareName`: `[a-z_][a-z0-9_]*`, the name PostgreSQL's `quote_identifier` leaves unquoted, without its keyword check.  `OutputReference.Bare` (53-67, 95) and `PostgresTypeNames.IsBare` (94-97, which is the same rule) call it.  `PostgresTypeNames`' remark at 15-17 on leaving out the keyword check stays where it is.
- The word rule of item 2, below.

Each caller keeps what it does differently today:

- `OutputReference.Quoted` still answers null for an empty name.
- `DeclaredTypeText` still refuses CR, LF and NUL inside the quotes and still does not unescape.  This check is the guard of the text put into `PREPARE`, so it stays visible in `DeclaredTypeText`.
- `PostgresSearchPath`'s bare element is still "anything up to a comma or whitespace", lower-cased.  That is not the identifier rule, so only `SkipQuoted` and `Unquote` are shared there.
- `OuterJoinGate` still compares without unescaping.

**The bare-name rule stays apart from the word rule.**  If `OutputReference.Bare` took the word rule, the entries a plan prints in upper case, such as `CURRENT_DATE`, `CURRENT_USER` and `LOCALTIMESTAMP`, would be read as column references.

**`PlanPartitions.WithoutAlias` reads `PlanExpression`'s tokens.**  `Tokens` is a private nested class of `PlanExpression`.  It becomes `internal`, nested or moved to its own file (the builder's choice), and `WithoutAlias` stops scanning characters.  The scanner and the tokens differ only on text a plan never prints: `e_1 .x` (the tokens drop whitespace), `1e_1.x` (a number running into a name), and an unbalanced entry, which `Tokens.Read` answers null for.  `WithoutAlias` keeps its fallback: an entry it cannot read is returned unchanged.  Its test of whether a name is qualified (158) needs a `Word` token directly followed by a `Dot`, so it checks that the tokens touch, by their spans, and not only their order.

## Item 2: the rule for a character of a word

**What is there.**

| Copy | Rule today |
|---|---|
| `StatementShape.cs:257`, `PostgresErrors.cs:439` | `char.IsLetterOrDigit` or `_` or `$` |
| `StatementShape.cs:223` (`PositionalParameter`) | the same, or anything above U+007F |
| `OuterJoinGate.cs:145-146` | `SqlLexer.IsParameterNameCharacter`: `char.IsLetterOrDigit` or `_`, the lexer's rule for a parameter name, not an identifier |
| `DeclaredTypeText.cs:199` | `char.IsLetterOrDigit` or `_`, and a digit may start a word |
| `Walk/PlanExpression.cs:388-391`, `Walk/PlanPartitions.cs:144-146` | ASCII only, with `$` |

PostgreSQL's rule (`scan.l`): an identifier starts with an ASCII letter, `_` or a byte above 0x7F, and goes on with those, digits and `$`.  In a C# string that is any character above U+007F, surrogate halves included.  The lexer already has this rule for characters after the first: `QuoteReader.IsNameCharacterOfPostgreSql` (`src/SqlSource/Parsing/Quoting/QuoteReader.cs:39`, built on `IsIdentifierCharacter` at 33), which is `protected`.  `SqlSource` has `InternalsVisibleTo` for `SqlSource.Tool`.

**The fix.**

- One definition of the rule.  `QuoteReader`'s rule becomes reachable from the tool: made `internal`, or moved to a small internal static class in `SqlSource/Parsing` that `QuoteReader` also calls (the builder's choice).  `PostgresIdentifiers` exposes it as the rule for a character that continues a word (`IsWordPart`, or a name the builder prefers), and adds the rule for the first character (`IsWordStart`: an ASCII letter, `_`, or above U+007F).  The tool does not copy the rule.
- `StatementShape`, `PostgresErrors`, `OuterJoinGate` and `PositionalParameter` use it.  `OuterJoinGate` stops using `SqlLexer.IsParameterNameCharacter`.
- `PlanExpression` and `WithoutAlias` use it too.  A plan quotes every name with `$`, upper case or a non-ASCII character, so outside quotes it prints only `[a-z0-9_]` names, upper-case keywords and `$n`; every rule gives the same answer there.  `PlanExpression.IsSymbol` (393-397) must stop counting a character above U+007F as a symbol.
- `PositionalParameter` uses the start and part rules together: a `$n` is a parameter unless its `$` continues an identifier, that is unless the run of word characters before it contains a character for which `IsWordStart` holds.  So `a$1`, `€$1`, `§$1` and `1a$1` are not parameters, which they are not today either (PostgreSQL 14 reads `1a$1` as `1` and the label `a$1`; 15 and later refuse it), and `1$1` is the number `1` and then the parameter `$1`, which today is missed.  "Contains", not "starts with", is what keeps `1a$1` as it is.
- `DeclaredTypeText` keeps its rule exactly as it is today, `char.IsLetterOrDigit` or `_` with a digit allowed first, and does not take the shared rule.  It is the guard of the text put into `PREPARE`, and must never let a `$` in: `1$$` would be the number `1` and then a dollar quote, and `1$1` a parameter.  Its rule is stricter than PostgreSQL's on purpose, so a remark beside it says so and why, and a test pins it.  A type whose name holds `$` or a non-letter above U+007F can still be written quoted.

These are the changes in answers the user approved (overview, rule 1).  Each moves the describer closer to PostgreSQL, and no fixture holds one:

- SQL that is not valid: `RefusedKind("CREATE€ …")` stops answering `CREATE`, so the statement reaches the server and comes back as `SQLSRC225`; `Uncast`'s help text for `€CAST(@x AS t)` changes.
- Valid SQL whose name holds `$` or a non-letter above U+007F, which PostgreSQL reads as part of the name:
  - `SELECT 1 AS €into` is no longer refused as `SELECT INTO`; it is described.
  - `SELECT a AS €left FROM t` and `SELECT x$left FROM t` no longer open the outer-join gate.  The gate decides whether the walk runs and, when the walk does not finish, which reading of nullability `BuildColumns` takes, so a column's nullability can change for such a query.
- `1$1` is read as a parameter.

**`Undeclared`, `Uncast` and their helpers move.**  `PostgresErrors.cs:342-439` (`Undeclared`, `Uncast`, `Pieces`, `TextPieces`) moves to a new class beside `StatementShape`, or into it (the builder's choice by size).  `PostgresErrors` calls it.  `Undeclared` keeps lexing `query.Sql` itself without throwing (`PostgresErrors.cs:344`): SQL that does not lex counts every parameter as uncast, on purpose.

## Item 3: the lex-or-throw block

**What is there.**  The same block, with the same message, is in `ParameterPlaceholders.cs:59-65`, `StatementShape.cs:259-270` and `OuterJoinGate.cs:74-80`.  The user's SQL is lexed by `ParameterPlaceholders.Rewrite` (`PostgresSession.cs:163`), `StatementShape.RefusedKind` (168), `StatementShape.PositionalParameter` (173), `OuterJoinGate.IsGated` (297, when the query has columns), `ParameterPlaceholders.AsWritten` (742), `ParameterPlaceholders.Rewrite` again in `NpgsqlCheck.Placement` (`NpgsqlCheck.cs:118`, when the answer is sent), `OuterJoinGate.NamesOldOrNew` (322, when the walk runs), and `PostgresErrors.Undeclared` (on a failed prepare).  Every lex is of the original `request.Sql`.

**The fix.**  One internal static `PostgresSql.Lex(string sql)` in this folder that lexes with the PostgreSQL dialect and throws today's exception with today's message.  The three blocks call it.  No signature changes.  Lexing once and passing the lexemes down is not done: it would change eight signatures and their tests to save a few lexes of a short text (overview, "Calls made").

## Item 8, three parts

- **`ServerLines(error with { Position = null }, 0)`**, six times: `PostgresErrors.cs:260, 452, 462, 473, 555` and `PostgresSession.cs:386`.  One `ServerLinesWithoutPosition(error)` in `PostgresErrors`, called by all six.  The call in `PostgresSession` is a one-line change; PR 3 restructures that file later.
- **A `$n` read as a number.**  `PostgresErrors.NamedIn` (701-725) and `NpgsqlCheck.NumberAt` (202-222) parse a number after `$` in the same way; one reader in `PostgresIdentifiers` (or beside it) serves both.  `NpgsqlCheck.PartAt` (168-173) scans backwards and `StatementShape.PositionalParameter` returns text after a boundary check: neither parses a number, so each keeps its own code.  The item's "four places" becomes two.
- **`Invariant(FormattableString)`** in `NpgsqlCheck.cs:271` and `PostgresTypeNames.cs:99`.  Both are removed for `string.Create(CultureInfo.InvariantCulture, …)`, which `PostgresSession.cs:187` already uses.

## Item 11

The remark at `PostgresTypeNames.cs:17` names `DescribeAsync_TypeOfASchemaNamedByAReservedWord_IsSpelledBare` (`tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs:454`).

## Tests

Existing tests that pin these rules, and must pass unchanged: `Walk/OutputReferenceTests.cs:49-52`, `PostgresTypeNamesTests.cs:56`, `StatementShapeTests.cs:139-163`, `PostgresSearchPathTests.cs:12-31` and `Walk/PlanPartitionsTests.cs:104` (paths under `tests/SqlSource.Tool.Tests/Describing/Postgres/`).

New tests:

- `PostgresIdentifiers`: `SkipQuoted` on a closed name, a name with `""`, and an unclosed quote; `Unquote`; `IsBareName` on `id`, `_x1`, `Id`, `1x` and `café`; `IsWordStart` and `IsWordPart` on an ASCII letter, a digit, `_`, `$`, `é`, `€` and a surrogate half.
- `PostgresSql.Lex`: SQL that lexes, and SQL that does not, with the message.
- The approved changes: `RefusedKind` on `CREATE€ …` (not a refused kind) and on `SELECT 1 AS €into` (not refused); `OuterJoinGate` on `x$left` and `€left` (not gated); `Uncast` on SQL with `€` before `CAST`; `PositionalParameter` on `1$1` (a parameter) and `1a$1` (not one).
- `DeclaredTypeText` as it is: refusing `$type`, `my$type`, `1$$`, `1$1`, `€type` and a quoted name with a line break.
- `OutputReferenceTests`: `CURRENT_DATE` is not a column reference.
- `WithoutAlias` on an entry that is qualified, one that is not, a quoted alias, and one whose tokens would not read, which comes back unchanged.

## Left to the builder

The exact names and signatures of the new members, where `Tokens` lives, where the moved `Uncast` code goes, and how the work is split into commits.
