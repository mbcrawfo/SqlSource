# TD-0076 Declared-Type Refusal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PostgreSQL's first refusal of a `-- param:` type says what it found, as SQL Server's does; SQL Server's name rule takes what the server takes after a name's first character; SQL Server's reader takes a typed `xml` (owner's td-0093 Q2 "c"); TD-0076 is resolved.

**Architecture:** The reasons both engines share (`;`, comment, parts, line break) and the classifier of the character where the grammar is left move to `DescribeFailures.TypeNameFound`; each engine's `DeclaredTypeText` keeps its own shape and not-closed texts.  PostgreSQL's reader turns from `bool` to "null or what was found", as SQL Server's is.  `QuoteReader.IsNameCharacterOfSqlServer` gains Unicode categories, so every caller of the shared rule agrees with the server.

**Tech Stack:** C# / .NET, xUnit v3, Shouldly, Testcontainers (PostgreSQL, SQL Server).

**Spec:** `docs/tech-debt/TD-0076-what-the-first-refusal-of-a-declared-type-says.md`, plus the run notes' td-0093 Q2 (typed xml).

## Where the merged code and the probes differ from the spec

- Probed on `mssql/server:2017-CU31-GDR19-ubuntu-18.04` and `2025-CU9-GDR1-ubuntu-24.04` (identical answers) through `sys.dm_exec_describe_first_result_set(N'SELECT @p AS c', N'@p <type>', 0)`, with `QUOTED_IDENTIFIER ON`: the server reads an unbracketed name by the Unicode 3.2 identifier rule.  After the first character: letters, Nl, digits, Mn, Mc, **Pc** and Cf, plus `_ @ # $`.  First character: letters, **Nl**, `_ @ #`.  Pc and Nl-first are outside the spec's list: owner question Q1 (sent).  Tasks below build the spec's list; Task 3 has a step that applies Q1's answer.
- The server's tables are Unicode 3.2's, .NET's current ones: `ꞔ` (U+A794), U+0600 and the soft hyphen U+00AD (Pd in 3.2) are taken by the rule and refused by the server (102); U+30FB and U+FF65 (Pc in 3.2, Po now) after the first character are refused by the rule and taken by the server.  A letter outside the BMP is refused by both, first or after, on both images.  This remainder is a limitation note (code remark and the manual's known issue), pending Q1.
- `xml(db.dbo.S)` (a three-part schema collection) makes both servers end the session with a level-21 error.  The reader refuses three parts; a test pins it.
- The server takes spaces around the dot of a type name (`dbo . x`), which the reader refuses: owner question Q2 (sent), not built unless the answer is yes.
- The server takes `xml(S)`, `xml(dbo.S)`, `xml(CONTENT dbo.S)`, `xml(DOCUMENT S)`, `xml([dbo].[S])`, `xml("dbo"."S")`, `xml(CONTENT[dbo].[S])`, `xml(CONTENT CONTENT)`, `xml(CONTENT.S)`, `sys.xml(dbo.S)`, `xml (S)`, `xml(max)` (as a collection named max); it refuses `xml(CONTENT)`, `xml(DOCUMENT)`, `xml(.S)`, `xml(CONTENT dbo.S, 1)`, `int(dbo.S)`, `nvarchar(CONTENT S)` with 102.

## Global Constraints

- Nothing a `.sql` file holds may make the describer run a statement: what is sent stays a type name (identifiers, brackets, quotes, `CONTENT`/`DOCUMENT`, numbers, `max`).
- The head of every refusal stays `DescribeFailures.NotATypeName` = `it is not written as a type name`, then `": "` and what was found.
- PostgreSQL's server-originated refusals (a `server:` line, `PostgresErrors` line ~302) keep the bare `NotATypeName`; only `DeclaredTypeText`'s refusals gain the found part.
- `./format.sh`, then `./pre-commit-validation.sh` before every commit.  Describer database tests run on the default image and the floor image (`SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`).
- User docs name no TD id, no describer internals.  `docs/manual/diagnostics.md` SQLSRC228's PostgreSQL bullet keeps td-0093's text about modifiers of words.

## Review Focus

1. A PostgreSQL type that reaches a line break inside a modifier, `geometry(Point\n4326)`: refused, "a type name is on one line" (Task 1 test).
2. `nvarchar(max)` and `VARCHAR(MAX)` still read after the `max` branch becomes a name (Task 2 test, existing rows kept).
3. `xml(maxSchema)` and `xml(CONTENTx)`: a name that starts with `max` or a keyword is one name (Task 2 test).
4. A three-part collection, `xml(a.b.c)` and `xml(CONTENT a.b.c)`: refused with Parts, never sent (Task 2 unit test and Task 2 database test that the session still answers).
5. A combining mark as the first character, `́x`, stays refused with Shape (Task 3 test).

---

### Task 1: PostgreSQL's reasons, and the shared ones

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Describers/DescribeFailures.cs` (add nested `TypeNameFound`)
- Modify: `src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs`
- Modify: `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs` (use the shared constants and classifier; no behaviour change)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/DeclaredTypeTextTests.cs`, `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`, `SqlServerSessionParameterTests.cs:155`, `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs:130`, `.../Postgres/Statements/ParameterPlaceholdersTests.cs:102`, `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs:78`, `tests/SqlSource.Tool.Tests/Describing/Describers/DescribeFailuresTests.cs`

**Interfaces:**
- Produces, in `DescribeFailures`:
  ```csharp
  public static class TypeNameFound
  {
      public const string Semicolon = "a ; is not part of a type name";
      public const string Comment = "a comment is not part of a type name";
      public const string Parts = "a type name has one or two parts";
      public const string OneLine = "a type name is on one line";
      // NotATypeName + ": " + found
      public static string Reason(string found);
      // ";" -> Semicolon, "--" or "/*" -> Comment, a line break (\r \n \u0085    ) -> OneLine,
      // the end or anything else -> shape.
      public static string At(string text, int position, string shape);
      public static bool IsLineBreak(char value);
  }
  ```
- SQL Server's `DeclaredTypeText` keeps `NotClosed` and `Shape`; its `Semicolon`, `Comment`, `Parts`, `OneLine` constants are removed in favour of `DescribeFailures.TypeNameFound.*` (update its tests' references).
- PostgreSQL's `DeclaredTypeText` gains:
  ```csharp
  public const string NotClosed = "a quote or a parenthesis is not closed";
  public const string Modifier = "a modifier in parentheses holds numbers and plain words";
  public const string Shape = "a type name is a name, then type words and one modifier in parentheses, then []";
  public static Outcome<string, string> Read(string text); // failure is TypeNameFound.Reason(found)
  ```

- [ ] **Step 1: Write the failing PostgreSQL test.** Replace `Read_AnythingElse_IsNotATypeName` with `Read_AnythingElse_IsRefusedWithWhatWasFound(string text, string found)` asserting `DeclaredTypeText.Read(text).Failure.ShouldBe(DescribeFailures.NotATypeName + ": " + found, text)`.  Keep every existing row, each with this reason (`TypeNameFound.X` unless noted):
  - Semicolon: `;`, `int;`, `int) AS SELECT 1; DROP TABLE users; --` is Shape (the `)` comes first), `geometry(Point;4326)`.
  - Comment: `int /* x */`, `int -- x`, `int(--)`, `geometry(Point--)`, `geometry(Point -- x)`, `geometry(Point/*x*/)`.
  - OneLine: `int\nfoo`, `int\n`, `"a\nb"`, `"a\rb"`, `"a\r\nb"`, new row `geometry(Point\n4326)`.
  - Parts: `a.b.c`, `.t`, `t.`.
  - `DeclaredTypeText.NotClosed`: `"open`, `int(`, `geometry(Point,4326`.
  - `DeclaredTypeText.Modifier`: `int(1-)`, `geometry('Point',4326)`, `geometry(Point,'4326')`, `geometry("Point",4326)`, `geometry((Point))`, `geometry(Point(1))`, `geometry(4326Point)`, `geometry(4326_point)`, `geometry(-1_a)`, `geometry(-Point)`, `geometry(Point$1)`, `geometry($$Point$$)`, `geometry(Pöint)`, `geometry(Point.Z)`.
  - `DeclaredTypeText.Shape`: everything else (`""`, `" "`, `'`, `int'`, `int) AS SELECT 1`, `int AS SELECT foo`, `int foo`, `int(1)(2)`, `(1)`, `int[`, `int[3]`, `[]`, `int[] foo`, `int"x"`, `"a\0b"`, `geometry(Point)(4326)`, `$type`, `my$type`, `1$$`, `1$1`, `€type`, and the injection row above).
  Add `Read_Semicolon_SaysItIsNotWrittenAsATypeName`: `Read("int; DROP TABLE x").Failure` is `"it is not written as a type name: a ; is not part of a type name"`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Postgres.Statements.DeclaredTypeTextTests"`: FAIL.
- [ ] **Step 3: Implement.** Add `TypeNameFound` (move SQL Server's `Found`/`IsLineBreak` logic there; SQL Server calls `TypeNameFound.At(text, position, Shape)`).  Rewrite PostgreSQL's reader so each `TryRead…` returns `string?` (null when read, else what was found), positioned at the offending character, with `TypeNameFound.At(text, position, Shape)` outside a modifier and `TypeNameFound.At(text, position, Modifier)` inside one; `.` where a type word or the end is expected is `Parts`; a quoted identifier left open is `NotClosed`, one holding `\r`/`\n` is `OneLine`, one holding `\0` is `Shape`; a modifier that reaches the end is `NotClosed`.  Update both readers' remarks: PostgreSQL's names its refusals "each with what was found".
- [ ] **Step 4: Update the callers' tests** to the new reasons: `PostgresSessionTests:130` and the database `ParameterTests:78` (`int) AS SELECT 1; DROP TABLE users; --` gives `NotATypeName + ": " + DeclaredTypeText.Shape`), `ParameterPlaceholdersTests:102` (`int) AS SELECT 1` → Shape), SQL Server tests' references to the moved constants.  `PostgisParameterTests:84` and `PostgresErrorsTests` stay (server-originated or reason passed through).
- [ ] **Step 5: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DeclaredTypeText|FullyQualifiedName~ParameterPlaceholders|FullyQualifiedName~PostgresSession|FullyQualifiedName~SqlServerSession|FullyQualifiedName~DescribeFailures"` and `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~Describer.ParameterTests"`: PASS.
- [ ] **Step 6: Commit** `feat: PostgreSQL's first refusal of a declared type says what it found (TD-0076)`.

### Task 2: SQL Server's typed xml

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs` (`ReadModifier`, `Shape`, remarks)
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`, `tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs`

**Interfaces:**
- `Shape` becomes `"a type name is a name, then (max), one or two numbers or an XML schema collection in parentheses"`.

- [ ] **Step 1: Write the failing tests.** `Read_TypeName_IsTheText` gains `xml(S)`, `xml(dbo.S)`, `xml(CONTENT dbo.S)`, `xml(DOCUMENT S)`, `xml(content dbo.S)`, `xml([dbo].[S])`, `xml("dbo"."S")`, `xml(CONTENT[dbo].[S])`, `xml(CONTENT CONTENT)`, `xml(CONTENT.S)`, `xml(maxSchema)`, `xml(CONTENTx)`, `sys.xml(dbo.S)`, `xml (S)`, `xml( CONTENT dbo.S )`.  The refusal theory gains: `xml(a.b.c)` Parts, `xml(CONTENT a.b.c)` Parts, `xml(.S)` Parts, `xml(CONTENT dbo.S, 1)` Shape, `xml(dbo.S;)` Semicolon, `xml(CONTENT -- x)` Comment, `xml([dbo)` NotClosed.  Keep `nvarchar(max, 2)` Shape and `VARCHAR(MAX)` accepted.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SqlServer.Statements.DeclaredTypeTextTests"`: FAIL.
- [ ] **Step 3: Implement.** In `ReadModifier`, after `(` and spaces: digits as today; else a name start (`[`, `"` or `IsFirst`) reads a collection: read one part; if it was plain (did not start with `[` or `"`) and equals `CONTENT` or `DOCUMENT` ignoring case, and the next character after spaces is neither `)` nor `.`, read a name (one or two parts, `ReadName`) after it; otherwise go back to the part's start and read a name.  The `max` branch goes: `max` reads as a one-part name, which sends the same text.  Anything else is `TypeNameFound.At(text, position, Shape)`.  Remarks say the collection form is the server's to judge for a type that is not `xml`, as the server refuses `int(dbo.S)`.
- [ ] **Step 4: Database tests** in `tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs` (`UsesSqlServer`): `DescribeAsync_XmlOfACollectionTheDatabaseLacks_IsNoSuchCollection` — describing `SELECT @p AS v` with `-- param: @p xml(CONTENT dbo.Nope)` fails `SQLSRC228` with a server line holding `Msg 6314` (the server parsed it as a type); and `DescribeAsync_XmlOfAThreePartCollection_IsRefusedAndTheSessionAnswers` — `xml(db.dbo.S)` fails `SQLSRC228` with reason `NotATypeName + ": " + TypeNameFound.Parts` and no server line, then a second describe on the same session (`SELECT 1 AS v`) succeeds.  Also one schema collection that exists: create `CREATE XML SCHEMA COLLECTION dbo.TdSchemas AS N'<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema"><xsd:element name="a" type="xsd:string"/></xsd:schema>'` (drop in `finally`), describe `-- param: @p xml(DOCUMENT dbo.TdSchemas)`: the parameter is described as type `xml`.  Use the existing `Of(SqlDialectRules.SqlServer, sql, ("p", type))` helper and the file's `Größe` test as the pattern for create/drop.
- [ ] **Step 5: Run** the unit tests and `dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~Describer.ParameterTests"`: PASS.
- [ ] **Step 6: Commit** `feat: SQL Server's -- param: reader takes a typed xml`.

### Task 3: SQL Server's name rule takes Mn, Mc, Nl and Cf after the first character

**Files:**
- Modify: `src/SqlSource/Parsing/Quoting/QuoteReader.cs:41-50`, `src/SqlSource/Parsing/SqlDialectRules.cs:199` (comment), `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs` (remarks, `IsFirst` per Q1)
- Test: `tests/SqlSource.Tests/Parsing/Quoting/QuoteReaderTests.cs`, `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`, `tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs`

**Interfaces:**
- `QuoteReader.IsNameCharacterOfSqlServer(char)` = `char.IsLetterOrDigit(value) || value is '_' or '@' or '#' or '$' || char.GetUnicodeCategory(value) is NonSpacingMark or SpacingCombiningMark or LetterNumber or Format` (+ `ConnectorPunctuation` if Q1 is yes).

- [ ] **Step 1: Failing tests.** `QuoteReaderTests`: the taken theory gains `'́'` (Mn), `'ः'` (Mc), `'Ⅰ'` (Nl), `'‌'` (Cf); a new fact-theory `IsNameCharacterOfSqlServer_LetterNewerThanTheServer_IsTaken` with `'ꞔ'` (the server refuses it with 102; the rule's tables are newer) — named so the divergence is the documented one.  `DeclaredTypeTextTests` accepted rows: `dbo.x́`, `dbo.xः`, `dbo.xⅠ`, `dbo.x‌`, `dbo.ꞔ`; refused rows with Shape: `́x`, `‌x`, `dbo.́x`, `x𝒳`, `𝒳`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QuoteReaderTests"` and the SQL Server reader tests: FAIL.
- [ ] **Step 3: Implement** the rule; rewrite the summary of `IsNameCharacterOfSqlServer` (what the server takes, found against 2017 and 2025, and that the server's tables are Unicode 3.2's: a character added or recategorised later can differ, `ꞔ` taken by the rule and refused by the server) with no TD id; update `SqlDialectRules`' comment ("of the characters above U+007F only letters and digits" → the categories); remove the TD-0076 sentence of SQL Server `DeclaredTypeText`'s remarks.
- [ ] **Step 4: Apply Q1's answer** (when it arrives): if yes, add `ConnectorPunctuation` to the rule and `UnicodeCategory.LetterNumber` to `DeclaredTypeText.IsFirst` and to `TsqlTokens`' word start (`src/SqlSource.Tool/Describing/SqlServer/Proofs/TsqlTokens.cs:111`), with rows `x‿`, `x＿`, `Ⅰx`, `〇x` accepted and in `QuoteReaderTests`.
- [ ] **Step 5: Database test** `DescribeAsync_DeclaredNameTheServerTakes_IsATypeTheDatabaseLacks` (theory, `UsesSqlServer`): for `dbo.x́`, `dbo.xः`, `dbo.xⅠ`, `dbo.x‌` (+ Q1's rows), describing `SELECT @p AS v` with that type fails `SQLSRC228` with `DescribeFailures.NoSuchType` (the server parsed it as a name), and `DescribeAsync_LetterNewerThanTheServer_IsNotAType`: `dbo.ꞔ` fails `SQLSRC228` with `it is not a type` (Msg 102).
- [ ] **Step 6: Run** `dotnet test tests/SqlSource.Tests`, `tests/SqlSource.Tool.Tests` (renaming and proofs use the rule too), and the SQL Server describer database tests: PASS.
- [ ] **Step 7: Commit** `feat: SQL Server's name rule takes a combining mark, a letter number and a format character after the first (TD-0076)`.

### Task 4: Docs and resolving TD-0076

**Files:**
- Modify: `docs/manual/diagnostics.md` (SQLSRC228), `docs/manual/sqlserver.md:535` (known issue), `docs/architecture/sqlserver-describer.md:129,303`, `docs/architecture/postgres-describer.md` (the `DeclaredTypeText` sentence in the errors section if it names the reason), `docs/architecture/bundles.md:307`, `docs/tech-debt/README.md` (remove TD-0076's row only)
- Delete: `docs/tech-debt/TD-0076-what-the-first-refusal-of-a-declared-type-says.md`

- [ ] **Step 1:** SQLSRC228: the PostgreSQL bullet says the reason is followed by what was found for a type the tool refuses before sending (a `;`, a comment, a line break, a count of parts, a quote or a parenthesis left open, a modifier that holds more than numbers and plain words, or the shape), and stays bare with a `server:` line when the server refuses it; keep td-0093's modifier text.  The SQL Server bullet adds `xml(CONTENT dbo.Schemas)`-style collections to the shape.  Add an example `console` block only if one is needed for clarity (the info string is `console`).
- [ ] **Step 2:** `sqlserver.md` known issue: narrowed to what Q1 leaves (characters whose Unicode category changed since the server's tables, such as `・` after the first character), with the bracket workaround; removed if nothing remains.
- [ ] **Step 3:** Architecture pages: the SQL Server grammar sentence adds the schema collection; `bundles.md:307` names the new categories.
- [ ] **Step 4:** Delete the item file and its README row; `grep -rn "TD-0076" . --include='*.cs' --include='*.md' | grep -v '/bin/\|/obj/\|docs/superpowers'` is empty.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`; commit `docs: SQLSRC228 says what was found on PostgreSQL; resolve TD-0076`.

### Final (controller)

- [ ] `./pre-commit-validation.sh --full`; both describers' database projects on their floor images.
- [ ] `/branch-review:review --base origin/main`.
