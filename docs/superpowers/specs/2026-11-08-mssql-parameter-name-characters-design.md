# SQL Server parameter names read as the server reads them

## Goal

Under the `mssql` dialect, the generator's lexer reads a parameter's name the way SQL Server reads a variable's name, so a query whose parameter holds a combining mark, a letter number, a format character such as ZWNJ, a connector, `$`, `#` or `@` is described and generated instead of failing as SQLSRC231.

The finding (td-0076's review, R3-F3): `SqlLexer.IsParameterNameCharacter` takes letters, digits and `_` in every dialect.  SQL Server reads `@xⅠ`, or `@a` + U+200C + `b`, as one variable, so SqlSource lists the parameter `x` or `a` while the server reports `@xⅠ` or `@a‌b`, and the query is SQLSRC231 at describe.

## What the server reads

Probed on `2017-CU31-GDR19-ubuntu-18.04` and `2025-CU9-GDR1-ubuntu-24.04`, with `DECLARE <name> int = 41; SELECT @out = <name> + 1;` through `sp_executesql`; the answers are the same on both:

- **Taken after the `@`:** a letter or a digit of any script (`x٣`, the full-width `Ａb`), a combining mark (Mn `e◌́`, Mc `a◌ः`), a letter number (`xⅠ`), a format character (ZWNJ, ZWJ), a connector (`a‿b`), `$`, `#` and `@`.
- **Taken as the first character after the `@` as well:** every one of those (`@1`, `@_`, `@$a`, `@#a`, `@@a`, `@Ⅰx`, `@◌́x`, `@‌x`, `@‿x`).
- **Refused (102):** the middle dot, `€`, the soft hyphen U+00AD.
- `U+30FB` after the first character is taken; it is the Unicode 3.2 remainder td-0076 documented.

So a variable is `@` followed by one or more characters of SQL Server's name rule after its first character, `QuoteReader.IsNameCharacterOfSqlServer` (td-0076), with no narrower rule for the first one.

## Design

1. **One rule, reused.**  `SqlDialectRules` gains `IsParameterNameCharacter(char)`: under SQL Server (the existing `NamesHoldAtAndHash` flag) `QuoteReader.IsNameCharacterOfSqlServer`, under every other dialect letters, digits and `_` as today.  `SqlLexer.IsParameterNameCharacter` (static) goes; the lexer and `SqlMarkerScope.ReadParam` call the rules' member.  No second character rule is written.
2. **`@@` stays no parameter.**  The server reads `@@a` as a variable, but `@@ROWCOUNT` and the other functions are why the lexer has its rule, and the manual documents it.  A parameter's first character after the prefix is never the prefix, in every dialect: under `mssql`, `@` inside a name (`@a@b`) is part of it, and `@@x` is still text.  The `-- param:` marker follows the same rule, so `-- param: @@x int` stays SQLSRC111.
3. **The character before the prefix** is read by the same member, so the `@` of `user@host` stays inside a word under every dialect, and under `mssql` an `@` after `$`, `#` or a mark is too, as the server reads it.
4. **Other dialects are unchanged.**  PostgreSQL's `@name` is read by Npgsql, whose rule takes no mark or format character; MySQL and MariaDB user variables take `$` and `.` but no marks, and those dialects are not described.  None reads names as SQL Server does, so the change is not free for them and they keep letters, digits and `_`.
5. **C# names come from the existing rules.**  A parameter's property is `PropertyNames.PascalCase` of its name, checked by `SqlIdentifier.IsValid`, which already takes Mn, Mc, Nl and Pc (`@xⅠ` gives `XⅠ`, `@idⅡ` `IdⅡ`) and refuses a format character, since C# ignores it and two names would collide, and `$`, `#`, `@`.  Such a name is SQLSRC254 under `models` and `codegen`, as `unit-price` is; under `sql` the query is a constant with the parameter in its list.  The existing naming rules settle the mapping, so it is not an owner question.
6. **The tool needs no change.**  It plans with the generator's parse and compares the server's names with `SqlParameterName` (ordinal ignoring case); the bundle renaming already reads a word by `IsNameCharacterOfSqlServer`.
7. **Unicode 3.2 remainder.**  The rule reads .NET's current tables and the server Unicode 3.2's, so `@x・` is read as `@x` and text by SqlSource and as one variable by the server (SQLSRC231), and `@ꞔ` the other way round.  A limitation remark on the rules' member and a known issue in `docs/manual/sqlserver.md`, no tech-debt item (the owner's rule for td-0076).

## Performance

The lexer's name loop calls an instance member of the rules in place of a static one.  `IsNameCharacterOfSqlServer` tests `char.IsLetterOrDigit` first and reads a category only above U+007F, so ASCII SQL costs about what it does today, and nothing is allocated.  `SqlFileParserAllocationTests` stay as they are; the pull request states a `Stopwatch` measurement of the parse over a `Release` build, before and after.

## Tests

- `SqlLexerTests`: under `mssql`, `@xⅠ`, `@a‌b`, `@e◌́`, `@a‿b`, `@a$b`, `@a#b`, `@a@b`, `@$a`, `@#a`, `@◌́x` are one parameter each; `@@x` and `@@ROWCOUNT` are none; `@a·b` and `@a€` end at the refused character; the same texts under `postgres` end at the first character that is not a letter, digit or `_`; `$@a` under `mssql` is no parameter.  The table of `Lex_Construct_IsReadByTheRulesOfTheDialect` gains its rows.
- `SqlDialectRulesTests`: the member under each dialect.
- `SqlFileParserMarkerTests`: `-- param: @xⅠ int` declares `xⅠ` under `mssql`; `-- param: @@x int` is SQLSRC111.
- `SqlDeclaredVariablesTests`: `DECLARE @a$b int` declares `a$b`.
- Generator: a parameter `@xⅠ` gives the property `XⅠ`; `@a‌b` and `@a$b` give SQLSRC254.
- End to end, SQL Server: a query of `Queries/Accounts.sql` with parameters named with a letter number and a combining mark, described by `tools/describe-end-to-end.sh` and run through its method.

## Docs

- `docs/manual/parameters-and-tokens.md`: what a parameter's name holds, under `mssql` and elsewhere, and that a name C# cannot hold is SQLSRC254.
- `docs/manual/sqlserver.md`: the Unicode 3.2 known issue covers parameter names.
- `docs/architecture/generator.md`: the parameters section names `SqlDialectRules.IsParameterNameCharacter`.
- `docs/manual/README.md`'s "What works today" if it lists parameter names.
