# TD-0076 - The first refusal of a declared type says less, or takes less, than it could

## Problem

Each describer reads the type of a `-- param:` marker before it sends anything, and refuses a type that is not written as a type name, with `SQLSRC228`.  The two readers differ from what they could be in two ways.

- [PostgreSQL's `DeclaredTypeText`](../../src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs) gives one reason for every refusal, `it is not written as a type name`, where [SQL Server's `DeclaredTypeText`](../../src/SqlSource.Tool/Describing/SqlServer/DeclaredTypeText.cs) adds what was found at the first place the text leaves the grammar (a `;`, a comment, a line break, a count of parts, a quote left open).
- SQL Server's reader takes a letter or a digit by `char.IsLetterOrDigit` ([`QuoteReader.IsNameCharacterOfSqlServer`](../../src/SqlSource/Parsing/Quoting/QuoteReader.cs)), which is not what the server takes.  SQL Server 2017 and 2025 also take, after the first character of an unbracketed name, a combining mark (`xé` written with a combining acute accent, Unicode category `Mn`), a letter number (`xⅠ`, `Nl`) and a format character (`x` followed by U+200C, `Cf`), which the reader refuses, so such a name must be written in brackets.  The reader takes a letter newer than its Unicode tables knows (`ꞔ`, U+A794), which the server then refuses with `102`.  A letter outside the Basic Multilingual Plane, which a `char` cannot hold (U+1D4B3, a mathematical letter, for one), is refused by the rule and by the server as the first character of a name, on both images; after the first character the server's answer is not known.

The rule the reader shares with `SqlDeclaredVariables` and `SqlServerErrors` is the one found against both servers for a letter or a digit of any script, `_`, `@`, `#` and `$`, with a letter, `_`, `@` or `#` first; the cases above are where it and the server still differ.

## Why it exists

TD-0073 made the copies of one rule agree and changed no behaviour beyond where its copies already disagreed.  Giving PostgreSQL's refusal a second part changes the text of `SQLSRC228` and its section of the manual, and a new reading of the server's categories needs a check against it, so both were left out of that change.

## Impact

Low.  The first is a message that says less than it could: the user still sees that the type is not a type name, and the example of the manual shows what is read.  The second affects a type whose name holds a combining mark, a letter number or a format character after its first character, which is rare, and the server accepts the name in brackets.

## Proposed fix

Have PostgreSQL's reader give what was found, as SQL Server's does, and change the text of `SQLSRC228` and its section of [`docs/manual/diagnostics.md`](../../docs/manual/diagnostics.md) with it, with a case for each reason in the PostgreSQL side's `DeclaredTypeTextTests`.  Have `QuoteReader.IsNameCharacterOfSqlServer`, for the characters after the first, also take Unicode categories `Mn`, `Mc`, `Nl` and `Cf` (`char.GetUnicodeCategory`), and check the result against SQL Server 2017 and 2025 through `sys.dm_exec_describe_first_result_set(N'SELECT @p AS c', N'@p <type>', 0)`: the cases above, and the letters newer than the server's tables, which it refuses and the rule takes.  Add them to `DeclaredTypeTextTests` and `QuoteReaderTests`.

## Trigger

A user's report of either, or the next change to `DeclaredTypeText` of either describer.
