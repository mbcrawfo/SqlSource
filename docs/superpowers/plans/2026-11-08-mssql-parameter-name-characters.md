# SQL Server parameter name characters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Under `mssql`, the lexer reads a parameter's name as SQL Server reads a variable's: `@` and one or more characters of `QuoteReader.IsNameCharacterOfSqlServer`, with `@@` still no parameter.

**Architecture:** `SqlDialectRules` gains one member, `IsParameterNameCharacter(char)`, chosen by the existing `NamesHoldAtAndHash` flag, and one helper for the end of a name, which `SqlLexer` and `SqlMarkerScope.ReadParam` share.  Other dialects keep letters, digits and `_`.  C# names come from the existing `PropertyNames` / `SqlIdentifier` rules, unchanged.

**Tech Stack:** C# (`netstandard2.0` generator), xunit v3 + Shouldly, SQL Server end-to-end project.

**Spec:** `docs/superpowers/specs/2026-11-08-mssql-parameter-name-characters-design.md`

## Global Constraints

- The generator stays `netstandard2.0`; no new package reference.
- Allocate nothing per character; `SqlFileParserAllocationTests` stay unchanged and green.
- No second character rule: SQL Server's is `QuoteReader.IsNameCharacterOfSqlServer` (`src/SqlSource/Parsing/Quoting/QuoteReader.cs`).
- Under every dialect, the character after the prefix is never the prefix (`@@x`, `@@ROWCOUNT` stay text).
- Dialects other than SQL Server read letters, digits and `_`, as today.
- The Unicode 3.2 remainder is a limitation remark and a known issue in `docs/manual/sqlserver.md`; no tech-debt item.
- User docs describe effects, never internals (root `AGENTS.md`, rule 4); a `sql` sample's info string names its dialect (`sql mssql`).
- Sidecars are written by `tools/describe-end-to-end.sh` alone, never by hand.
- Run `./format.sh` before validating.

## Review Focus

- `@@ROWCOUNT` and `@@x` under `mssql` once `@` is a name character: still text, not a parameter `@ROWCOUNT` (Task 1, `Lex_AtSignThatStartsNoParameter_IsText` under SqlServer).
- `-- param: @@x int` under `mssql`: SQLSRC111, not a declaration of `@x` (Task 1).
- A list parameter whose name holds a mark, `IN @idsⅠ`: still a list, its values sent as `@idsⅠ_0` (Task 1, `SqlListUses` test).
- `DECLARE @a$b int; SELECT @a$b` under `mssql`: one declared variable, no parameter (Task 1).

---

### Task 1: The rule, its callers, their tests and the docs

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectRules.cs` (new members, beside `IsNameCharacter`)
- Modify: `src/SqlSource/Parsing/SqlLexer.cs:136-158` (drop the static `IsParameterNameCharacter`, use the rules)
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:586-608` (`ReadParam`)
- Test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`, `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`, `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs`, `tests/SqlSource.Tests/Parsing/SqlDeclaredVariablesTests.cs`, `tests/SqlSource.Tests/Parsing/SqlListUsesTests.cs`, `tests/SqlSource.Tests/Generator/ModelNameTests.cs`
- Docs: `docs/manual/parameters-and-tokens.md` (line 29 and the `@` bullet at 34), `docs/manual/sqlserver.md` (the Unicode 3.2 known issue at about line 536), `docs/architecture/generator.md` ("Parameters", first paragraph)

**Interfaces:**
- Produces, in `SqlDialectRules`:
  - `public bool IsParameterNameCharacter(char value)`: `NamesHoldAtAndHash ? QuoteReader.IsNameCharacterOfSqlServer(value) : char.IsLetterOrDigit(value) || value == '_'`.  Its remarks carry the Unicode 3.2 limitation (`@x・` and `@ꞔ`), as `IsNameCharacterOfSqlServer`'s do.
  - `public int FindParameterNameEnd(string text, int nameStart, int end)`: the offset after the name that starts at `nameStart` (the character after the prefix), reading up to `end`; `nameStart` itself when no name follows, which includes a first character equal to `ParameterPrefix`.
- `SqlLexer.TryFindParameterEnd` keeps its look-back (the prefix or a name character before it means no parameter) through `Rules.IsParameterNameCharacter`, and finds the end through `Rules.FindParameterNameEnd`.  `SqlMarkerScope.ReadParam` finds `nameEnd` through `rules.FindParameterNameEnd(text, start + 1, end)`.

- [ ] **Step 1: Measure the baseline.**  Before any change, add a temporary `[Fact]` in `tests/SqlSource.Tests/Parsing/` that parses the file of `SqlFileParserAllocationTests` (under `mssql` and under `postgres`) 2,000 times after a warm-up, timing it with `Stopwatch` and reading `GC.GetAllocatedBytesForCurrentThread()` before and after; run it in `Release` (`dotnet test tests/SqlSource.Tests -c Release --filter <name>`), note the per-parse time and bytes, and keep the test uncommitted for Step 8.

- [ ] **Step 2: Write the failing tests.**
  - `SqlLexerTests`: `Lex_ParameterUnderSqlServer_TakesEveryNameCharacterOfTheServer` (theory, `Rules("SqlServer")`): each of `@xⅠ`, `@a‌b`, `@é`, `@aः`, `@a‿b`, `@a$b`, `@a#b`, `@a@b`, `@$a`, `@#a`, `@́x`, `@Ⅰx`, `@x٣` followed by `;` lexes as `["Parameter:<it>", "Text:;"]`.
  - `Lex_ParameterUnderSqlServer_EndsAtACharacterTheServerRefuses`: `@a·b` gives `["Parameter:@a", "Text:·b"]`, and `@a€` `["Parameter:@a", "Text:€"]`.
  - `Lex_ParameterUnderOtherDialects_EndsAtAMarkOrASign` (theory over every dialect but SqlServer, as `Lex_Parameter_IsFoundUnderEveryDialect` lists them): `@a‌b` gives `["Parameter:@a", "Text:‌b"]`, `@a$b` `["Parameter:@a", "Text:$b"]`, `@xⅠ` `["Parameter:@x", "Text:Ⅰ"]`.  (Check the actual lexing of `$b` under the dialects with dollar quotes and give each its expected lexemes; split the theory by dialect if they differ.)
  - `Lex_AtSignThatStartsNoParameter_IsText` gains a `SqlServer` twin, or a dialect argument, over `a @@ b`, `SELECT @@ROWCOUNT`, `@@x`, `user@host`, `x_@y`, `1@y`, `@ x`, `@`, plus `$@a` and `#@a` for SqlServer alone.
  - `Lex_Construct_IsReadByTheRulesOfTheDialect` gains rows: `@a‌b c` is `Parameter:@a‌b | Text: c` for `SqlServer` and `Parameter:@a | Text:‌b c` for the rest; `@a$b c` likewise (with the rest's lexemes as Step 2's check finds them).
  - `SqlDialectRulesTests`: `IsParameterNameCharacter_OfEachDialect_ReadsTheServersRuleUnderSqlServerAlone` (theory over the dialects of the existing `IsNameCharacter` test): `'a'`, `'1'`, `'_'` true everywhere; `'́'`, `'‌'`, `'Ⅰ'`, `'‿'`, `'$'`, `'#'`, `'@'` equal to `sqlServer`; `'·'`, `'€'`, `' '` false everywhere.  `FindParameterNameEnd_AfterThePrefix_IsTheEndOfTheName`: under SqlServer, `"@a$b;"` from 1 gives 4, `"@@x"` from 1 gives 1, `"@"` from 1 gives 1.
  - `SqlFileParserMarkerTests`: under `-- dialect: mssql`, `-- param: @xⅠ int` declares a parameter named `xⅠ` with type `int`; `-- param: @@x int` is SQLSRC111 (the existing invalid-marker form of that file).
  - `SqlDeclaredVariablesTests`: under SqlServer, `DECLARE @a$b int; SELECT @a$b, @c` declares `a$b`, and the query's one parameter is `c`.
  - `SqlListUsesTests`: under SqlServer, `WHERE id IN @idsⅠ` is one `List` use.
  - `ModelNameTests`: in a `-- dialect: mssql` file, `Run_ParameterWithALetterNumber_GivesItsProperty` (`@xⅠ` gives the property `XⅠ`), and `Run_ParameterWithAFormatCharacter_IsSqlsrc254WithRename` (`@a‌b` gives SQLSRC254 naming the parameter `a‌b` and the property `A‌b`, with the existing rename help), following `Run_ParameterNotAnIdentifier_IsSqlsrc254WithRename`.

- [ ] **Step 3: Run them to see them fail.**  `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlLexerTests|FullyQualifiedName~SqlDialectRulesTests|FullyQualifiedName~SqlFileParserMarkerTests|FullyQualifiedName~SqlDeclaredVariablesTests|FullyQualifiedName~SqlListUsesTests|FullyQualifiedName~ModelNameTests"`: the SqlServer cases fail and the other dialects' pass.

- [ ] **Step 4: Implement the two members of `SqlDialectRules`, and switch `SqlLexer` and `SqlMarkerScope.ReadParam` to them**, as the Interfaces block says.  Update `SqlLexer`'s doc comments and the comment above `TryFindParameterEnd`; the summary of `NamesHoldAtAndHash` now covers parameter names too.

- [ ] **Step 5: Run Step 3's command again.**  Expected: all pass.

- [ ] **Step 6: Run the generator's and the tool's unit tests,** `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tool.Tests`.  Expected: pass, `SqlFileParserAllocationTests` and `GeneratorParityTests` included.

- [ ] **Step 7: Update the docs.**
  - `docs/manual/parameters-and-tokens.md`: the name is letters, digits and `_`; under `mssql`, any character SQL Server takes in a variable's name, a combining mark, a letter number such as `Ⅰ`, a format character such as the zero-width non-joiner, a connector, `$`, `#` and `@` included; `@@` still starts no parameter under any dialect.  A name whose property C# cannot hold, with a format character, `$`, `#` or `@`, is [SQLSRC254](diagnostics.md#sqlsrc254) under `models` and `codegen` (link to [property names](generator.md#property-names)).
  - `docs/manual/sqlserver.md`: the Unicode 3.2 known issue also covers a parameter's name: `@x・` is read as the parameter `@x` followed by text, which SQL Server reads as one variable, [SQLSRC231](diagnostics.md#sqlsrc231); write the name without that character.
  - `docs/manual/README.md`: if its "What works today" table or an FAQ names what a parameter's name holds, update it too.
  - `docs/architecture/generator.md`, "Parameters": the name runs while `SqlDialectRules.IsParameterNameCharacter` holds, SQL Server's name rule under `mssql` and letters, digits and `_` elsewhere, and `FindParameterNameEnd` is the one reader of a name's end, the `-- param:` marker's too; a name never starts with the prefix.

- [ ] **Step 8: Measure again** with Step 1's test on the changed code, in `Release`; record both measurements (time and bytes per parse, both dialects) in your hand-back.  Then delete the temporary test.

- [ ] **Step 9: Format, validate and commit.**  `./format.sh`, then `./pre-commit-validation.sh`; expected: pass.

```bash
git add src/SqlSource tests/SqlSource.Tests docs/manual docs/architecture
git commit -m "fix(parsing): an mssql parameter's name is read as SQL Server reads it"
```

### Task 2: An end-to-end query whose parameters hold a letter number and a combining mark

**Files:**
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Accounts.sql` (a new query at its end)
- Regenerate: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Accounts.sql.json` (by `tools/describe-end-to-end.sh`)
- Test: `tests/DatabaseTests/SqlServer/EndToEnd/AccountQueryTests.cs`

**Interfaces:**
- Consumes: Task 1's lexer.

- [ ] **Step 1: Add the query** to `Accounts.sql`:

```sql
-- name: ListAccountsInRange
-- summary: The accounts whose ids are within two bounds, but one name, with parameters named by a letter number and
-- a combining mark, which SQL Server reads as part of a name.
SELECT id, name
FROM dbo.accounts
WHERE id BETWEEN @idⅠ AND @idⅡ AND name <> @skippé
ORDER BY id;
```

with `@skippé` written decomposed, `e` followed by U+0301 (check with `grep -c $'e\xcc\x81' Accounts.sql`).

- [ ] **Step 2: Write the test** `ListAccountsInRange_BoundsAndASkippedName_ReadsTheAccountsBetween`: `connection.ListAccountsInRange(new ListAccountsInRangeArgs(1, 3, "bob"), cancellationToken: TestContext.Current.CancellationToken)` returns `[new ListAccountsInRangeDto(1, "alice"), new ListAccountsInRangeDto(3, "carol")]`.  Reporting.AccountReports claims the file too; add nothing there.

- [ ] **Step 3: Describe.**  Run `tools/describe-end-to-end.sh` (see `tests/DatabaseTests/README.md` for its arguments; Docker must be running).  Expected: `Accounts.sql.json` gains the entry with the parameters `idⅠ`, `idⅡ`, `skippé`, and no other sidecar changes.  If others change, stop and report.

- [ ] **Step 4: Run the test project,** `dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~AccountQueryTests|FullyQualifiedName~CheckTests"`, then the same on the floor image with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.  Expected: pass on both.

- [ ] **Step 5: Format, validate and commit.**  `./format.sh`, `./pre-commit-validation.sh`; expected: pass.

```bash
git add tests/DatabaseTests/SqlServer/EndToEnd
git commit -m "test(mssql): an end-to-end query with a letter number and a combining mark in its parameters"
```
