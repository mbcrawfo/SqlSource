# SQL dialects - design

Date: 2026-10-06

Resolves the open part of [`TD-0004`](../../tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md).  The [SQL queries epic](2026-10-05-sql-queries-epic-design.md) is closed; it left a dialect setting out of scope and kept the lexer's dialect-sensitive choices in one place so that one could be added.

## Goal

SqlSource finds comments, strings and quoted identifiers with one set of rules for every database.  Six constructs that are valid in MySQL, Oracle or SQL Server are misread: the build fails on valid SQL, or SQL is silently removed as if it were a comment.

After this change a project says which database its SQL is written for, and the SQL is read by that database's rules.

```xml
<PropertyGroup>
    <SqlSourceDialect>postgres</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" />
</ItemGroup>
```

```sql
-- SqlSource: dialect=mysql

-- name: FindByNote
SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
```

Success is:

- Every row of `TD-0004`'s table is read correctly under its dialect.
- A project that sets nothing gets exactly the SQL it gets today.
- `TD-0004` describes only what still misreads.

## Decisions

Settled with the owner during the design, in this order.

| Decision | Choice | Reason |
|----|----|----|
| What the default means | `ansi` is today's rules, unchanged: ANSI plus `E'..'`, `$tag$`, backticks and nested block comments | Nothing changes for a project that sets nothing.  The defaults of sqlparser-rs (`generic`) and SQLFluff (`ansi`) are permissive unions too. |
| Scope of the directive | One dialect for a file.  `dialect=` is not allowed inside a named query. | The owner's decision: a file does not mix dialects.  SQLFluff's `-- sqlfluff:dialect:` is per file; no surveyed tool has a per-statement override. |
| Where the directive goes | Before the file's first `-- name:` line and before its first SQL.  It takes effect from the next line. | The directive is found by lexing, and it changes how the text after it is lexed.  Text above it is read under the dialect the project gave. |
| Which dialects | `ansi`, `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite`, `oracle` | Every engine whose ADO.NET providers have more than 100 million NuGet downloads.  The next engine has under 40 million.  See Research. |
| How rules are represented | Strategies for behaviour, data for values, composed for each dialect | The owner asked for the strategy pattern.  How a quoted region ends is an algorithm; whether comments nest is a boolean.  One lexer stays dialect-agnostic. |
| Setting part of a project | Metadata on the file's `AdditionalFiles` item, `SqlSourceDialect="mssql"`, beside the project-wide property | The owner asked for a dialect by path.  Item metadata is how the compiler hands a generator a value for one file, and it needs no MSBuild logic in the package.  A dedicated item was considered: it needs a join in `SqlSource.targets` and puts an item and a property under one name. |
| Server modes | Not modelled | `ANSI_QUOTES` and `NO_BACKSLASH_ESCAPES` are off by default.  SQL Server's `QUOTED_IDENTIFIER OFF` moves no boundary.  PostgreSQL's `standard_conforming_strings` is on since 9.1 and cannot be turned off from 19. |
| An invalid value in MSBuild | `SQLSRC011`, with no position, once for each distinct value.  The files it covers are read as `ansi`. | A generator cannot see where a property or metadata was set.  Emitting nothing would bury the one real error under a missing-member error for every query, as with `SQLSRC010`. |
| Names | Lower case in the documents, compared ignoring case, surrounding whitespace ignored.  Aliases: `sqlserver` and `tsql` for `mssql`, `postgresql` for `postgres`. | The owner asked for case-insensitive names.  The aliases are the other spellings in use among sqlglot, SQLFluff, sqlparser-rs, sqlc and Liquibase. |

## Out of scope

- Dialects beyond the seven.  `docs/deferred` records them with what the research found.
- Options that change one rule of a dialect, such as `ANSI_QUOTES`.  The rewritten `TD-0004` proposes them.
- A dedicated `SqlSourceDialect` item.  It can be added later over the metadata without changing the generator.
- A per-query override.
- Anything a dialect could mean beyond where comments, strings and quoted identifiers start and end.  Markers, tokens, `keep-comments` and the generated C# do not depend on the dialect.
- Client commands that are not SQL: `DELIMITER`, `GO`, SQL*Plus `PROMPT` and `REM`, psql `\` commands.

## What a user sees

### Values

| Value | Also accepted | Use it for |
|----|----|----|
| `ansi` | | The default.  Any database without a value of its own, including Db2. |
| `mssql` | `sqlserver`, `tsql` | SQL Server, Azure SQL |
| `postgres` | `postgresql` | PostgreSQL, DuckDB, CockroachDB |
| `mysql` | | MySQL |
| `mariadb` | | MariaDB |
| `sqlite` | | SQLite |
| `oracle` | | Oracle, Firebird |

### Where it is set

Three places.  The first that applies wins.

1. A `dialect=name` directive in the file, on a `-- SqlSource:` line that comes before the file's first `-- name:` line and before its first SQL.
2. `SqlSourceDialect` metadata on the file's `AdditionalFiles` item.
3. The `SqlSourceDialect` MSBuild property.

With none of them, the dialect is `ansi`.  An empty value in 2 or 3 counts as not set.

A file with no `-- name:` line is one query, and has no preamble.  The same rule holds there: the directive goes above the query's SQL.

Comments above the directive, such as a licence header in a block comment, are read under the dialect from 2 or 3.

### Errors

| Situation | Diagnostic | Where |
|----|----|----|
| Unknown name in the property or the metadata | `SQLSRC011`, new | No position |
| Unknown name in the directive, or `dialect` with no value | `SQLSRC111` | The directive |
| `dialect=` inside a named query, or after SQL | `SQLSRC115`, new | The directive |
| Two different dialects before the first SQL of one file | `SQLSRC112` | The second |

The same dialect twice is allowed.

## The rules of each dialect

### Quote readers

A quoted region is a string or a quoted identifier.  The lexer copies it as written and looks for no comment inside it.  How a region ends is one of six algorithms.

| Reader | Ends at |
|----|----|
| Doubled | The closing quote that is not doubled: `''`, `""`, ` `` ` |
| Backslash | As Doubled, and a backslash takes the next character with it |
| Escape-string | As Doubled.  As Backslash when the quote directly follows an `E` or `e` that does not end an identifier.  This is today's reading of `'`. |
| Quote-operator | As Doubled.  When the quote directly follows `q`, `Q`, or one of those after `n` or `N`, and that prefix does not end an identifier: the character after the quote is the opening delimiter, and the region ends at the first closing delimiter that is directly followed by `'`.  `[`, `{`, `<` and `(` close with `]`, `}`, `>` and `)`; any other character closes with itself.  Delimiters are not counted for depth.  A space, tab or line break after the quote is not a delimiter, and the region is read as Doubled. |
| Bracket | The first `]`.  With the escape option, the first `]` that is not followed by `]`. |
| Dollar | The same `$tag$` again.  The tag is empty, or a letter or `_` followed by letters, digits and `_`.  A `$` after an identifier character, or an opener with no closer, is plain text.  This is today's reading of `$`. |

The PostgreSQL continuation is an option of Escape-string.  After an `E` string closes, whitespace that holds at least one line break and is followed by `'` continues the region, and the next part is read with backslash escapes too.  Only whitespace may be in the gap.  A protected region must never be able to hold a `-- name:` line, so a comment in the gap ends the region, and the part after it is read as a plain string.

### The matrix

| | `ansi` | `mssql` | `postgres` | `mysql` | `mariadb` | `sqlite` | `oracle` |
|----|----|----|----|----|----|----|----|
| `'` | Escape-string | Doubled | Escape-string, continued | Backslash | Backslash | Doubled | Quote-operator |
| `"` | Doubled | Doubled | Doubled | Backslash | Backslash | Doubled | Doubled |
| `` ` `` | Doubled | - | - | Doubled | Doubled | Doubled | - |
| `[` | - | Bracket, `]]` escapes | - | - | - | Bracket | - |
| `$` | Dollar | - | Dollar | Dollar | - | - | - |
| Block comments nest | Yes | Yes | Yes | No | No | No | No |
| `--` needs whitespace after it | No | No | No | Yes | Yes | No | No |
| `#` starts a line comment | No | No | No | Yes | Yes | No | No |
| Block comments kept as hints | `/*+` `/*!` | `/*+` `/*!` | `/*+` `/*!` | `/*+` `/*!` | `/*+` `/*!` `/*M!` | `/*+` `/*!` | `/*+` `/*!` |
| `--+` is a hint | No | No | No | No | No | No | Yes |

A dash means the character opens nothing and is plain text.

### Notes on the matrix

- **`ansi` is today's column.**  No cell of it changes.
- **`/*+` and `/*!` are hints in every dialect**, including those with no executable comments.  A comment left in is harmless, and one rule is simpler than seven.
- **`mysql` reads dollar quotes and `mariadb` does not.**  The MySQL lexer has them from 8.4, for routine bodies.  MariaDB reads `$$` as an identifier.  On MySQL 8.0 the reader costs nothing in practice, because an opener needs a matching closer.
- **`--` under `mysql` and `mariadb`** is a comment when the character after it is whitespace or a control character, or when the text ends there.  `5--3` is arithmetic.
- **Markers under `mysql` and `mariadb`.**  `--name: X` with no space is not a comment there, so it is not a marker.  `# name: X` is a comment and not a marker: a marker starts `--`.
- **The Oracle line hint** `--+ FULL(e)` runs to the end of its line.  It is a hint lexeme: kept, copied as written, never a marker.
- **Backtick is not a quote** under `mssql`, `postgres` and `oracle`.  PostgreSQL allows it in operator names; the other two do not use it.
- **An unterminated block comment is an error in every dialect.**  SQLite accepts one that runs to the end of the input.  SqlSource does not, because it would silently take every later query with it.
- **An unterminated `[`, or a `q'` whose closing delimiter is never found, is `SQLSRC101`** at the opening quote or bracket.  An unterminated continuation of an `E` string is reported at the quote that opened the region.
- **Line comments end at `\r` or `\n` in every dialect**, as today.  MySQL, SQLite and CockroachDB end one at `\n` only, which differs for a file with bare `\r` line endings and for nothing else.

### What still misreads

The rewritten `TD-0004` and the README list these.

1. The MySQL and MariaDB server modes `ANSI_QUOTES` and `NO_BACKSLASH_ESCAPES`.  Under the first a `"..."` region has no backslash escapes; under the second no string has.
2. A MySQL or MariaDB `/*!50700 ... */` comment whose body holds a string that contains `*/`.  Where the server ends it depends on the server's version.  SqlSource ends it at the first `*/`.
3. A comment between the parts of a continued PostgreSQL `E` string.
4. CockroachDB's `b'\''`, under `postgres`.  PostgreSQL has no backslash escapes in a `b'..'` literal.
5. Client commands that are not SQL.
6. Every row of today's table when the dialect is `ansi`.  The fix is to set the dialect.

## Structure

| Unit | Folder | Change | Responsibility |
|----|----|----|----|
| `SqlDialect` | `Parsing/` | New | The enum of seven values.  `Ansi` is the default value. |
| `SqlDialectName` | `Parsing/` | New | `TryParse`: a name or an alias to a `SqlDialect`, trimmed and compared ignoring case.  The only place the names are known. |
| `QuoteReader` | `Parsing/Quoting/` | New | The strategy.  `FindEnd(text, start)` returns the offset after the closing delimiter, `Unterminated`, or `NotAQuote`. |
| `DoubledQuoteReader`, `BackslashQuoteReader`, `EscapeStringReader`, `QuoteOperatorReader`, `BracketReader`, `DollarQuoteReader` | `Parsing/Quoting/` | New | One algorithm each, with its options as constructor arguments.  No state between calls. |
| `SqlDialectRules` | `Parsing/` | New | The comment flags and a table from an opening character to its reader.  `For(dialect)` returns one shared instance for each dialect and is the matrix above. |
| `SqlLexer` | `Parsing/` | Changed | Reads by the rules it is given.  Names no dialect and no quoting form.  Can be run in two stages. |
| `SqlPreambleDialect` | `Parsing/` | New | Finds the file's `dialect=` directive among its leading comments, switches the lexer to it, and says where the header ends. |
| `SqlDirectiveScope` | `Parsing/` | Changed | Knows `dialect=`: checks the name, reports a conflict, and reports a directive past the header's end. |
| `SqlMarkerReader` | `Parsing/` | Changed | Checks that a line comment starts `--` before reading it as a marker |
| `SqlFileParser` | `Parsing/` | Changed | `Parse(text, fileName, dialect)` |
| `SqlLexemeKind`, `SqlParseErrorKind` | `Parsing/` | Changed | Documentation for `#` comments and the new hints; `MisplacedDialect` at the end of the error enum |
| `DialectSetting` | `Generation/` | New | Reads the property and the metadata, in the shape of `TokenValidationSetting` |
| `FileDialect` | `Generation/` | New | A file with its effective dialect, and the metadata value as written when it is not valid |
| `ParsedSqlFile`, `SqlFileReader` | `Generation/` | Changed | Carry the invalid metadata value; pass the dialect to the parser |
| `SqlSourceGenerator`, `TrackingNames` | - | Changed | The pipeline below |
| `SqlDiagnostics` | `Diagnostics/` | Changed | Two new descriptors, two widened messages |
| `SqlSource.props`, `SqlSource.targets` | `build/` | Changed | Show the property and the metadata to the compiler; trim the property |

### The strategy

```csharp
internal abstract class QuoteReader
{
    public const int Unterminated = -1;

    public const int NotAQuote = -2;

    /// <summary>
    /// Returns the offset after the closing delimiter of the region that opens at <paramref name="start" />.
    /// </summary>
    public abstract int FindEnd(string text, int start);
}
```

- A reader may look at the characters before `start`.  A prefix such as `E` or `q` stays in the text lexeme before the region, as `E` does today.
- A reader allocates nothing.  Each is created once, as a static field of `SqlDialectRules`.
- `NotAQuote` is how Dollar says that a `$` opens nothing.  The lexer then treats the character as text.

### The rules

```csharp
internal sealed class SqlDialectRules
{
    public bool NestedComments { get; }
    public bool DashNeedsWhitespace { get; }
    public bool HashComments { get; }
    public bool LineHints { get; }
    public bool MariaDbHints { get; }

    public QuoteReader? ReaderFor(char opener);

    public static SqlDialectRules For(SqlDialect dialect);
}
```

`ReaderFor` is a lookup in an array of 128 entries, and null for any character at or above 128.  The array is built once for each dialect.

### The lexer in two stages

The directive has to be found before the text after it is lexed.  So the lexer is an object that holds its position, its lexemes and its rules:

- `TryReadLeadingComment(out lexeme)` skips whitespace and reads one comment, by the rules it holds now.  It returns false, and consumes nothing, at the first character that starts neither whitespace nor a comment, and at the end of the text.  A hint is not a comment here.
- `Rules` can be set between two lexemes.
- `ReadToEnd()` reads the rest and returns the `SqlLexResult`.
- `SqlLexer.Lex(text, rules)` stays, as the one-call form for a text with one dialect.

The lexer still knows nothing about markers.

`SqlPreambleDialect.Apply(lexer, text)` reads leading comments until there are no more, or until one is a `-- name:` marker:

1. The first `-- SqlSource:` marker that holds a `dialect=` directive with a valid name switches the lexer's rules.  Later ones switch nothing.
2. It returns the header's end: the start of the `-- name:` marker, or else the lexer's position when no leading comment was left.

The work is not repeated: the lexemes read here are the first lexemes of the result.

`SqlFileParser` then calls `ReadToEnd()` and runs its existing pass over the lexemes.  That pass reports the errors, through `SqlDirectiveScope`, which is given the header's end:

- A `dialect=` directive that starts at or after the header's end is `MisplacedDialect`.
- A name `SqlDialectName` does not know, or a missing value, is `InvalidDirectiveValue`.
- A second valid `dialect=` before the header's end with a different dialect is `ConflictingDirectives`.

A lexer error still ends the pass, as today.

## Pipeline

```
sqlFiles + options provider   -> (file, metadata value)                          cached by value
         + project dialect    -> FileDialect(file, dialect, invalid value)       [FileDialect]
         + claimed paths      -> SqlFileReader.Read(file, path, dialect, ...)    [ParsedFile]
```

- `DialectSetting` reads `build_property.SqlSourceDialect` from the global options and `build_metadata.AdditionalFiles.SqlSourceDialect` from the options of one file.  Both are trimmed.
- The project's dialect is a step of its own with the tracking name `ProjectDialect`, as `TokenValidation` is.
- The effective dialect is resolved before the parse, in the `FileDialect` step: the metadata when it is set, or else the project's.  The parse step's inputs are the file, its effective dialect and whether a type claims it.
- So an edit to one `.sql` file parses that file only, as now.  A change to the metadata of one folder parses that folder's files.  A change to the property parses the files that fall back to it, and no others.
- This is the opposite of `SqlSourceTokenValidation`, which is kept out of the parse on purpose.  A dialect decides what the parse produces, so it has to be an input.
- A file that no type claims is still never read.

### Reporting an invalid value

- `ParsedSqlFile` carries the file's metadata value as written when it is not a dialect.
- The step that reports file errors from the collected files also takes the project's setting.  It reports `SQLSRC011` once for each distinct invalid value among the property and the claimed files, with `Location.None`.
- A file whose own value is invalid, or that falls back to an invalid property, is read as `ansi`.

## Diagnostics

| Id | Title | Message | Change |
|----|----|----|----|
| `SQLSRC011` | SqlSourceDialect is not valid | `'{0}' is not a SQL dialect; SqlSourceDialect accepts ansi, mssql, postgres, mysql, mariadb, sqlite and oracle` | New |
| `SQLSRC115` | Dialect directive is misplaced | `The 'dialect' directive must come before the file's first query and before any SQL` | New |
| `SQLSRC111` | Directive value is not valid | Widened to cover a value that is present and not accepted | Changed |
| `SQLSRC112` | Directives conflict | Widened from "the other token validation directive" to any directive that conflicts in its scope | Changed |

- Each new id touches the places `src/SqlSource/AGENTS.md` lists: the descriptor and `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, and `SqlDiagnostics.ForParseError` for `SQLSRC115`.
- `SQLSRC011` is the second diagnostic with no position.  Like `SQLSRC010`, it is built in an output step and does not travel as a `DiagnosticInfo`.
- The explanations of `SQLSRC101` and `SQLSRC102` gain a first thing to check: the file's dialect.

## Package

`build/SqlSource.props` gains, with no condition, as for `SqlSourceTokenValidation`:

```xml
<CompilerVisibleProperty Include="SqlSourceDialect" />
<CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="SqlSourceDialect" />
```

`build/SqlSource.targets` trims `SqlSourceDialect` as it trims `SqlSourceTokenValidation`, for the same reason: a value on a line of its own would reach the compiler empty.

Both names start with `SqlSource`, as the root `AGENTS.md` requires.

Two points for the plan to settle by experiment:

- **Metadata written over several lines.**  Metadata given as a child element on a line of its own would reach the compiler empty too, and the file would silently fall back.  Trimming item metadata in MSBuild is not as direct as trimming a property.  If `SqlSource.targets` can do it reliably, in a design-time build as well, it does.  If not, the README shows only the attribute form and a tech-debt item records the gap.
- **What the declaration costs a large project.**  The SDK writes a section for an `AdditionalFiles` item into the generated configuration file.  Whether it does so for every `.sql` file or only for those with the metadata is to be measured on a project with many unclaimed files, and recorded if it matters.

## Testing

- **Readers.**  A test class for each: every way its region ends, the unterminated case, and `NotAQuote` for Dollar.  Quote-operator: each bracket pair, a delimiter that closes with itself, a closing delimiter inside the body, the `n` prefix, a prefix that ends an identifier, whitespace after the quote.
- **Matrix.**  One theory over dialect and construct that pins every cell of the table.
- **Known limits.**  Each `Lex_KnownLimit_*` test stays as the `ansi` reading, and gains a counterpart that shows the right reading under its dialect.  New tests pin the items of "What still misreads" that the lexer can show.
- **Lexer invariants.**  "Lexemes cover the text without gaps" runs under every dialect.  Text that ends where a construct could start is text under every dialect.
- **Comments.**  `--` with and without whitespace, at the end of the text, and before a control character, under `mysql`; `#`; `/*M!`; `--+`; nesting on and off.
- **Names.**  Every value and alias, mixed case, surrounding whitespace, an unknown name, an empty name.
- **Preamble.**
  - A directive below a licence block comment.
  - A `#` comment on the line after `dialect=mysql`.
  - A file with no `-- name:` line.
  - Comments above the directive are read under the dialect that was passed in.
  - `SQLSRC111` for an unknown name and for no value.
  - `SQLSRC112` for two different dialects, and none for the same dialect twice.
  - `SQLSRC115` inside a named query, after SQL in a file with no `-- name:` line, and after SQL in a preamble.
  - The directive line does not reach the SQL.
- **Generator**, in `tests/SqlSource.Tests/Generator/`, so on both Roslyn versions.
  - Precedence: directive over metadata over property over `ansi`.
  - `SQLSRC011` for the property; for metadata, once for a value that two files share; none for a file that no type claims.
  - A file with an invalid value is read as `ansi` and still gets its members.
  - `TestOptionsProvider` gains options for one file.
- **Caching.**  A change to the property runs the parse of the files that fall back to it and of no others.  A change to one file's metadata runs that file's parse.  A change that touches neither runs none.
- **End to end.**  The test project gets a folder set to `mysql` through `AdditionalFiles Update`, and a file with a directive, each with a construct that `ansi` misreads.  The real `.props` and `.targets` carry both.
- **Build files.**  Both declarations are present and nothing puts a condition on them.  `Props_TokenValidationProperty_ReachesTheCompilerInEveryProject` expects one `CompilerVisibleProperty` today and is changed to expect both.
- **Allocation.**  The budget in `SqlFileParserAllocationTests` is not raised.  The same file is also parsed under `mysql` within the same budget.

## Documentation

Each in the commit that makes it true.

- **`README.md`**
  - "Dialect limits" becomes "Dialects": the values, the three places to set one, what each dialect changes, and what still misreads.
  - The directive table gains `dialect=name`.
  - The MSBuild section gains the property and the metadata.
  - Links stay absolute.
- **`docs/diagnostics.md`**: sections for `SQLSRC011` and `SQLSRC115`; the changed text of `SQLSRC101`, `SQLSRC102`, `SQLSRC111` and `SQLSRC112`.
- **`docs/tech-debt/TD-0004`**: rewritten to "What still misreads", with options that change one rule of a dialect as its proposed fix.  It keeps its id.  Its row in `docs/tech-debt/README.md` and its title change to match.
- **`docs/deferred/D-0001`**: the dialects not delivered, with the verified and unverified rules from Research.  `Next id` in `docs/deferred/README.md` moves on.
- **`src/SqlSource/AGENTS.md`**
  - "Dialect-sensitive choices live in `SqlLexer` only" becomes: they live in `SqlDialectRules` and the readers in `Parsing/Quoting/`, and the lexer names no dialect.
  - "The project's setting joins the pipeline after `TypeQueries`" is about token validation.  It gains the contrast: the dialect is an input of the parse, resolved for each file in the `FileDialect` step.
  - The note that `SQLSRC010` is the one diagnostic without a position becomes two.
- **`CONTRIBUTING.md` and `docs/publishing.md`**: no change.

## Research

Done on 2026-10-06.  Rules were read from the scanner source for PostgreSQL, MySQL, MariaDB, SQLite, Firebird, DuckDB, CockroachDB and ClickHouse, and from vendor documentation for SQL Server, Oracle and Db2.  Each was compared with sqlglot's tokenizer settings and sqlparser-rs's dialect traits.

### Usage among .NET developers

Total NuGet downloads of the ADO.NET providers, read on 2026-10-06.  They include CI restores and transitive references, so they are upper bounds.

| Engine | Downloads | Packages |
|----|----|----|
| SQL Server | 2.9 billion | `Microsoft.Data.SqlClient`, `System.Data.SqlClient` |
| PostgreSQL | 1.0 billion | `Npgsql` |
| SQLite | 0.7 billion | `Microsoft.Data.Sqlite.Core`, `Microsoft.Data.Sqlite`, `System.Data.SQLite.Core`, `System.Data.SQLite` |
| MySQL and MariaDB | 0.4 billion | `MySqlConnector`, `MySql.Data` |
| Oracle | 0.17 billion | `Oracle.ManagedDataAccess.Core`, `Oracle.ManagedDataAccess` |
| BigQuery | 39 million | `Google.Cloud.BigQuery.V2` |
| Snowflake | 38 million | `Snowflake.Data` |
| Firebird | 20 million | `FirebirdSql.Data.FirebirdClient` |
| ClickHouse | 12 million | four packages |
| Db2 | 6 million | three packages |
| DuckDB | 5 million | `DuckDB.NET.Data.Full`, `DuckDB.NET.Data` |

The provider packages of DbUp, which runs raw `.sql` files, rank the same five first.

### Findings that go beyond the table in `TD-0004`

- **`q'..'` cannot be recognised without a setting.**  `SELECT q'a', 'b'` is valid in SQL Server, MySQL and MariaDB: a column `q` with a string alias.  This answers the open question in `TD-0004`.
- **Two hint forms are stripped today.**  Oracle's `--+` and MariaDB's `/*M!`.
- **PostgreSQL continues an `E` string across a line break**, and the continuation keeps its backslash escapes.
- **Backtick is not a quote** in PostgreSQL, SQL Server or Oracle.
- **MySQL and MariaDB differ**: `/*M!`, dollar quotes in the MySQL lexer from 8.4, and a `/*!50700` to `/*!99999` comment that MariaDB always skips.
- **SQLite accepts an unterminated block comment**, and ends a `[..]` identifier at the first `]` with no escape.
- **SQL Server's `QUOTED_IDENTIFIER OFF`** turns `"..."` into a string with the same boundaries.
- **`#` is never a comment outside MySQL and MariaDB** among the seven: a temporary table in SQL Server, an operator in PostgreSQL, an identifier character in Oracle.
- **`[` is an identifier quote only in SQL Server and SQLite**: array syntax in PostgreSQL, a `MODEL` cell reference in Oracle.

### Engines not delivered

For `docs/deferred/D-0001`.  UNVERIFIED marks a rule that no primary source confirmed.

| Engine | Read correctly today by | What it would need |
|----|----|----|
| DuckDB | `postgres` | Nothing found |
| CockroachDB | `postgres`, but for `b'\''` | Backslash escapes in a `b'..'` literal |
| Firebird | `oracle` | Nothing found.  Its `q'..'` has no `n` prefix and the same pairing rule. |
| Db2 | `ansi` | Block-comment nesting is UNVERIFIED for Db2 on Linux, UNIX and Windows.  `$`, `#` and `@` are letters, so `$$` is an identifier. |
| Redshift | Neither | `postgres` with backslash escapes in a plain `'...'` string.  Nesting is UNVERIFIED. |
| Snowflake | Neither | `//` line comments, but not in `://`; untagged `$$..$$` strings; backslash escapes; no nesting.  `#` as a comment is UNVERIFIED. |
| BigQuery | Neither | `#` comments; `"..."` as a string; backslash escapes and no doubling; `'''` and `"""` strings over several lines; `r` and `b` prefixes, where a raw string still does not end at `\'`; backtick identifiers with backslash escapes; no nesting |
| ClickHouse | Neither | `#` only before a space or `!`; `//` comments; backslash escapes in strings and in both identifier quotes; `$tag$` heredocs; nesting |
| Spark and Databricks | Neither | Backslash escapes; `"..."` as a string by default; `r'..'` raw strings that end at the first quote; a backslash before a line break continues a `--` comment; nesting.  `$tag$` on Databricks is UNVERIFIED. |
| Trino and Presto | Neither | No nesting; otherwise ANSI |

### Sources

- PostgreSQL: [`scan.l`](https://github.com/postgres/postgres/blob/REL_18_STABLE/src/backend/parser/scan.l), [lexical structure](https://www.postgresql.org/docs/18/sql-syntax-lexical.html), [`standard_conforming_strings`](https://www.postgresql.org/docs/18/runtime-config-compatible.html)
- SQL Server: [block comments](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/slash-star-comment-transact-sql), [identifiers](https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers), [`QUOTED_IDENTIFIER`](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-quoted-identifier-transact-sql), [constants](https://learn.microsoft.com/en-us/sql/t-sql/data-types/constants-transact-sql)
- MySQL: [`sql_lex.cc`](https://github.com/mysql/mysql-server/blob/trunk/sql/sql_lex.cc), [comments](https://dev.mysql.com/doc/refman/8.4/en/comments.html), [string literals](https://dev.mysql.com/doc/refman/8.4/en/string-literals.html), [SQL modes](https://dev.mysql.com/doc/refman/8.4/en/sql-mode.html)
- MariaDB: [`sql_lex.cc`](https://github.com/MariaDB/server/blob/main/sql/sql_lex.cc), [comment syntax](https://mariadb.com/kb/en/comment-syntax/)
- SQLite: [`tokenize.c`](https://github.com/sqlite/sqlite/blob/master/src/tokenize.c), [keywords and quoting](https://www.sqlite.org/lang_keywords.html), [comments](https://www.sqlite.org/lang_comment.html)
- Oracle: [literals](https://docs.oracle.com/en/database/oracle/oracle-database/19/sqlrf/Literals.html), [comments and hints](https://docs.oracle.com/en/database/oracle/oracle-database/19/sqlrf/Comments.html), [PL/SQL lexical units](https://docs.oracle.com/en/database/oracle/oracle-database/19/lnpls/lexical-units.html)
- Firebird: [`Parser.cpp`](https://github.com/FirebirdSQL/firebird/blob/master/src/dsql/Parser.cpp), [literals](https://firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref50/fblangref50-commons.html)
- Db2: [tokens](https://www.ibm.com/docs/en/db2/11.5?topic=elements-tokens), [characters](https://www.ibm.com/docs/en/db2/11.5?topic=elements-characters)
- DuckDB: [`scan.l`](https://github.com/duckdb/duckdb/blob/v1.5.6/third_party/libpg_query/scan.l).  CockroachDB: [`scan.go`](https://github.com/cockroachdb/cockroach/blob/master/pkg/sql/scanner/scan.go).
- Redshift: [LIKE](https://docs.aws.amazon.com/redshift/latest/dg/r_patternmatching_condition_like.html), [`QUOTE_LITERAL`](https://docs.aws.amazon.com/redshift/latest/dg/r_QUOTE_LITERAL.html)
- Snowflake: [string constants](https://docs.snowflake.com/en/sql-reference/data-types-text), [`util_text.py`](https://github.com/snowflakedb/snowflake-connector-python/blob/main/src/snowflake/connector/util_text.py)
- BigQuery: [`lexical.md`](https://github.com/google/zetasql/blob/master/docs/lexical.md).  ClickHouse: [`Lexer.cpp`](https://github.com/ClickHouse/ClickHouse/blob/master/src/Parsers/Lexer.cpp).  Spark: [`SqlBaseLexer.g4`](https://github.com/apache/spark/blob/master/sql/api/src/main/antlr4/org/apache/spark/sql/catalyst/parser/SqlBaseLexer.g4).  Trino: [`SqlBase.g4`](https://github.com/trinodb/trino/blob/master/core/trino-grammar/src/main/antlr4/io/trino/grammar/sql/SqlBase.g4).
- Prior art: [sqlglot `tokens.py`](https://github.com/tobymao/sqlglot/blob/main/sqlglot/tokens.py), [sqlparser-rs dialects](https://github.com/apache/datafusion-sqlparser-rs/blob/main/src/dialect/mod.rs), [SQLFluff in-file configuration](https://github.com/sqlfluff/sqlfluff/blob/main/src/sqlfluff/core/config/fluffconfig.py), [Flyway `Parser.java`](https://github.com/flyway/flyway/blob/main/flyway-core/src/main/java/org/flywaydb/core/internal/parser/Parser.java)
- Usage: the [nuget.org search API](https://azuresearch-usnc.nuget.org/query?q=packageid:Npgsql), and the [Stack Overflow Developer Survey 2025](https://survey.stackoverflow.co/2025/technology)

## Verification

- `./pre-commit-validation.sh` exits zero with every step passed.
- Both test projects pass.
- The pull request states the time and allocation of a parse of a representative file, before and after, under `ansi` and under `mysql`, measured with a `Stopwatch` loop on a `Release` build, as `src/SqlSource/AGENTS.md` requires for a hot path.
- `grep -r "TD-0004" .` finds only text that is true of the rewritten item.
- `VersionPrefix` is compared with the release tags before the pull request is opened.  The repository has no `v*` tag today.
