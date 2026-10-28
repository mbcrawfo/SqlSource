# AGENTS.md - src/SqlSource/Parsing

See [parsing](../../../docs/architecture/generator.md#parsing).

- **Pure.**  No file access and no pipeline types.  Comparisons are ordinal; marker keywords and the names of generator parameters are ordinal ignoring case.
- **Every span is an offset into the file's text**; `SqlBlockText.ToSourceSpan` maps one of a block's built SQL back.
- **Quoted regions and hints are copied as written**, apart from line endings.  Trimming and blank-line removal must never reach inside one.
- **Lexemes cover the text without gaps**, and a quoted region or block comment ends with its closing delimiter: `SqlMarkerReader` and `SqlTextBuilder` depend on both.
- **The small readings of a text are in `SqlText`**, called and never copied.  `SqlText.FindLineEnd` is the one search for the end of a line in `Parsing/`; code that tests one character for a line break calls `SqlText.IsLineTerminator`, which names both terminators of SQL, `\r` and `\n`.  `Generation/Emitting/XmlDocWriter.cs` has the five of a documentation comment, another rule.
- **Dialect-sensitive choices live in `SqlDialectRules` and the readers of `Parsing/Quoting/`.**  `SqlLexer` names no dialect and no quoting form.  `Lex_Construct_IsReadByTheRulesOfTheDialect` in `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` pins every cell; changing one changes the SQL users get.
- **A reader holds no state and allocates nothing.**  A prefix it looks back at stays in the text lexeme before the region.  It reads one part of a string; whether a string goes on is the lexer's call.  See [lexemes](../../../docs/architecture/generator.md#lexemes-and-the-rules-of-a-dialect).
- **A quoted region must never be able to hold a marker.**  Keep each part of a continued string a quoted region of its own, and carry nothing but the pending continuation from one lexeme to the next in `SqlLexer`.
- **A parameter is a lexeme, and `SqlParameterList` alone builds a query's list.**  A parameter inside a token must be in the SQL or declared (`SQLSRC118`).
- **A name the query declares as a local variable is left out of its parameters everywhere, defaults too**, though the lexer still gives it as a parameter lexeme.  `SqlDeclaredVariables` reads the file's lexemes and lexes nothing again, and walks the text only between a `DECLARE` and the end of its list, so a query without the word costs one search.
- **`SqlMarkerScope` alone knows a marker's scope and value.**  A new marker is a `SqlMarkerKind`, a keyword in `SqlMarkerReader`, a row in `IsAllowedInQuery`, `IsAllowedInPreamble` or both, and a `case` in `SqlMarkerScope.Read`.  The theory `Marker_OfEveryKind_HasAKeywordAndAScope` fails until all four are there.
- **`SqlFileParser.Parser.ReadBlock` is three steps in a fixed order**: the block's markers, the query, the settings, with the `EmptyBlock` check after the first and the `InputModelWithoutParameters` check after the second.  Moving one changes which of two errors at one place comes first.  See [markers](../../../docs/architecture/generator.md#markers).
- **A file's `-- dialect:` marker is found before the text after it is lexed**, by `SqlDialectMarker.Apply`, which reports nothing: `SqlFileParser` reports a marker's errors later, from the header's end that `Apply` returns.  The two must agree on what a valid marker is, so both go through `SqlDialectMarker.TryRead`.
- **A dialect's names, and those of its options, are in `SqlDialectName` only.**  The message of `SQLSRC019` for `dialect`, `docs/manual/diagnostics.md` and `schemas/settings-v1.schema.json` repeat them; `SqlDialectName.Accepted` and a test keep the message in step, and `SettingsFileSchemaTests` the schema.
- **The describable dialects are in `SqlDescribableDialects` only**: `postgres` and `mssql`.  `SQLSRC209` and its section of `docs/manual/diagnostics.md` repeat them; a test holds the list to them.
- **The hash has one definition, `SqlQueryHash`, and is not computed during a parse.**  A change to it, to what `SqlTextBuilder` gives for stripped SQL, to how `Parsing/Tokens/TokenScanner.cs` splits segments, to the defaults `Parsing/Tokens/SqlTokenList.cs` gives or to the order of `SqlParameterList`, makes every stored hash stale.
- **Never build the kept form of a query's SQL for every query**: it doubles what a parse allocates.  The tokens, the parameters and the hash come from `Segments`, the SQL without comments.  See [the two forms](../../../docs/architecture/generator.md#a-block-has-two-forms-of-its-sql).
- **The dialect is not a generator parameter.**  `SqlGeneratorParameterScope` holds what `-- generator:` lines give.  A setting with a value of its own is a marker, and a key of a settings file beside it.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
