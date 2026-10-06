# SQL parser - design

Date: 2026-10-05

Phase 1 of the [SQL queries epic](2026-10-05-sql-queries-epic-design.md).

## Goal

Turn the text of one `.sql` file into the named SQL blocks the generator will emit, or into a list of errors.  The parser is pure: it reads no files, knows nothing about the generator pipeline, and produces values that later phases can cache and compare.

Nothing a user can see changes in this phase.  The generator still registers nothing.

## Decisions

| Decision | Choice | Reason |
|----|----|----|
| Structure | Lex the whole file, then group lexemes into blocks | A marker inside a block comment or a string is ignored without extra work, and every error has an exact position |
| Comment rules | One conservative lexer, no dialect setting | A comment left in is harmless and SQL removed is a bug, so where dialects disagree the lexer keeps the text |
| Unbalanced input | An error, and no blocks for the file | A stray `/*` or quote fails loudly instead of swallowing SQL |
| Errors | The parser returns error kinds, not Roslyn diagnostics | Diagnostic ids and descriptors belong where they are reported, in phase 2 |
| Result on error | Any error means no blocks | The generator never emits from a partly valid file |
| Directives | All four are parsed now | They are flags on a block; phase 3 is then emission only |
| Model types | Records with value equality | Phase 2 caches them in the incremental pipeline |
| Malformed token | Literal text | PostgreSQL array literals such as `'{{1,2},{3,4}}'` must pass through |

## Out of scope

- The attribute, file discovery, code emission and Roslyn diagnostics.
- Uniqueness of names across files.
- A dialect setting or an options type for the lexer.  The dialect-sensitive choices are kept together in the lexer so that one can be added later.
- Reformatting SQL: indentation is kept and runs of spaces are not collapsed.
- Line and column positions.  The parser reports offsets; phase 2 converts them.

## Structure

All types are internal and live in `src/SqlSource/Parsing/`.

| Unit | Responsibility | Depends on |
|----|----|----|
| `SqlLexer` | Text to lexemes with spans.  Reports unterminated quotes and comments.  Knows nothing about markers. | - |
| `SqlFileParser` | Lexemes and a file name to blocks: markers, preamble, directives, summaries, comment stripping, whitespace, name and emptiness checks. | `SqlLexer`, `TokenScanner` |
| `TokenScanner` | A block's cleaned SQL to literal and token segments. | - |

### Entry point

`SqlFileParser.Parse(string text, string fileName)` returns a `SqlFileParseResult`.  `fileName` is the file's name with its extension and without a directory.  All comparisons are ordinal unless a rule below says case-insensitive.

### Model

| Type | Members |
|----|----|
| `SqlFileParseResult` | `Blocks`, `Errors`.  When `Errors` is not empty, `Blocks` is empty. |
| `SqlBlock` | `Name`; `NameSpan`, the position of the name in the file, or an empty span at the start of the file when the name comes from the file name; `Summary`, null when the block has none; `PreserveComments`; `TokenValidation`, a nullable flag that is null when neither validation directive applies; `Segments`. |
| `SqlSegment` | A kind, `Literal` or `Token`, and the literal text or the token name. |
| `SqlParseError` | A `SqlParseErrorKind`, a span, and the message arguments. |

- Spans are `Microsoft.CodeAnalysis.Text.TextSpan` offsets into the text passed to `Parse`.
- Adjacent literal segments are merged and a literal segment is never empty, so a block without tokens has exactly one segment.
- The types are records, and their collections are held in an equatable array wrapper so that two results from the same text compare equal.  Records with `init` accessors need `IsExternalInit`, which `netstandard2.0` lacks; this phase adds that polyfill.

## Lexer

The lexer splits the text into these lexemes:

| Lexeme | Form |
|----|----|
| Quoted region | See below.  Copied to the output verbatim. |
| Line comment | `--` to the end of the line, excluding the line terminator |
| Block comment | `/*` to its matching `*/`.  Block comments nest. |
| Hint | A block comment that starts `/*+` or `/*!` outside any other comment.  It is closed like a block comment and is never stripped. |
| Text | Everything else |

A line terminator is `\r\n`, `\n` or `\r`.

### Quoted regions

| Opener | Closer | Escapes |
|----|----|----|
| `'` | `'` | `''` |
| `"` | `"` | `""` |
| `` ` `` | `` ` `` | A doubled backtick |
| `E'` or `e'`, where the letter does not follow an identifier character | `'` | A backslash escapes the next character; `''` |
| `$tag$`, where the tag is empty or a letter or underscore followed by letters, digits and underscores, and the first `$` does not follow an identifier character | The identical `$tag$` | None |

An identifier character is a letter, a digit, `_` or `$`.

A `$tag$` opener with no closer is text, not an error, because `$` has other meanings outside PostgreSQL (`v$session`, `$5.00`, `$action`).

### Lexer errors

An unterminated `'`, `"`, `` ` `` or `E'` region is `UnterminatedQuote`.  An unterminated block comment or hint is `UnterminatedBlockComment`.  Either one ends the pass: the result holds that one error and no blocks.

### Known limits

The lexer follows ANSI rules.  These cases are read differently by some databases, and the README will list them in phase 2 with `preserve-comments` as the workaround:

- MySQL backslash escapes inside a plain `'...'` or `"..."` string.  `'a\'b'` is read as the string `'a\'` followed by `b'`.
- SQL Server `[...]` identifiers that contain `--`, `/*` or a quote.
- MySQL treats `--` as a comment only when whitespace follows it.  The lexer always treats it as a comment.
- `#` is never a comment.  It is an operator in PostgreSQL and a temporary table prefix in SQL Server.  A MySQL `#` comment is left in the SQL.
- MySQL, Oracle and SQLite do not nest block comments.  A `/*` inside a comment there is reported as `UnterminatedBlockComment` or removes more text than the database would.

Each limit has a test that pins the behaviour described.

## Markers

A marker is a line comment that is the first content on its line after any spaces and tabs.  `SELECT 1 -- name: x` contains an ordinary comment, and a `-- name:` inside a block comment or a quoted region is not a line comment at all.

A marker has the form `--`, optional spaces and tabs, a keyword, then `:` with nothing between the keyword and the colon.  The keywords are `name`, `summary` and `SqlSource`, matched case-insensitively.  The value is the rest of the line with surrounding whitespace removed.

Marker lines are removed from the output whole, in every mode.

### `-- name:`

The value must be a valid C# identifier (`SyntaxFacts.IsValidIdentifier`) and must not be a reserved keyword.  Contextual keywords are allowed.  Otherwise the error is `InvalidName`.  A name marker with an invalid value still starts a block, so that errors after it are reported against the right block.  A name that repeats in the file, compared case-sensitively, is `DuplicateName`.

### `-- summary:`

A block's summary is the values of its summary markers, in order, joined with a single space.  A summary marker with an empty value adds nothing.  A block with no summary text has a null `Summary`.

### `-- SqlSource:`

The value is one or more directives separated by whitespace.  Directive names are case-insensitive.

| Directive | Value | Effect on the block |
|----|----|----|
| `preserve-comments` | None | `PreserveComments` is true |
| `no-token-validation` | None | `TokenValidation` is false |
| `token-validation` | None | `TokenValidation` is true |
| `token-ignore=name` | A valid C# identifier, reserved keywords included | `{{name}}` is not a token in this block |

- A directive applies to its whole block wherever it appears in the block.
- Repeating a directive is allowed.  `token-ignore` is repeated to ignore several names.
- A value is written as `directive=value` with no whitespace around `=`.
- `UnknownDirective`: the name is not in the table.
- `EmptyDirectiveLine`: the marker has no directives.
- `InvalidDirectiveValue`: `token-ignore` has no value or a value that is not an identifier, or another directive is given a value.
- `ConflictingDirectives`: `token-validation` and `no-token-validation` both appear in one scope.  A scope is one block, or the preamble.
- A `token-ignore` name that matches no token in the block is allowed.

## Blocks

### A file with name markers

- A block runs from its name marker to the next name marker or the end of the file.
- The preamble is everything before the first name marker.
  - It may hold whitespace, comments and `-- SqlSource:` markers.
  - Its directives apply to every block.  A block's own validation directive overrides the preamble's, and a block's `token-ignore` names add to the preamble's.
  - Its comments are discarded even under `preserve-comments`.
  - Any other content is `SqlBeforeFirstName`.  A `-- summary:` marker is `SummaryBeforeFirstName`.

### A file without name markers

The whole file is one block and there is no preamble.  `-- summary:` and `-- SqlSource:` markers may appear anywhere in it.  The block's name is the file name up to its last `.`, and it must pass the same check as a `-- name:` value; otherwise the error is `InvalidFileName`.

### Empty blocks

A block whose only content is whitespace, comments and markers is `EmptyBlock`, whether or not comments are preserved.  A hint counts as content.

## Output text

The SQL of a block is built from its lexemes.  Comments are stripped or kept first, and then the rules for every mode are applied.

In every mode:

1. Marker lines are removed, including their line terminator.
2. Line terminators become `\n`, including those inside quoted regions.
3. Spaces and tabs at the end of a line are removed, unless the line ends inside a quoted region.
4. Blank lines at the start and end of the block are removed, and the text does not end with a line terminator.
5. Indentation is kept as written, and runs of spaces are not collapsed.

When comments are stripped, the default:

- A line comment is deleted.
- A block comment is replaced by one space, so `SELECT/**/1` becomes `SELECT 1`.
- Every line that is blank, or becomes blank, is removed.  A line break inside a quoted region is never removed.

When comments are preserved, comments and blank lines inside the block stay as written.

Hints and quoted regions are kept in both modes.

## Tokens

The token scanner runs over a block's output text, so it sees quoted regions, hints and preserved comments, and does not see stripped comments.

- A token is `{{`, optional spaces and tabs, a valid C# identifier, optional spaces and tabs, `}}`.
- A `{{` that does not match that form is literal text.
- A token whose name is listed by `token-ignore` is literal text, exactly as written.  This check comes first, so `token-ignore=class` makes `{{class}}` legal.
- Any other token whose name is a reserved keyword is `ReservedTokenName`.
- Names are case-sensitive: `{{Table}}` and `{{table}}` are two tokens.
- A name may appear more than once.  The segments record each occurrence in order; phase 3 derives one parameter per distinct name, ordered by first appearance.

## Errors

The parser reports every error it finds in one pass, except that a lexer error ends the pass.

| Kind | Raised when | Span |
|----|----|----|
| `UnterminatedQuote` | A quoted region has no closing quote | The opening quote |
| `UnterminatedBlockComment` | A block comment or hint is not closed | The opening `/*` |
| `InvalidName` | A `-- name:` value is empty, not an identifier, or a reserved keyword | The value, or the marker when the value is empty |
| `DuplicateName` | A name repeats within the file | The second occurrence |
| `InvalidFileName` | There is no name marker and the file name is not a usable identifier | An empty span at the start of the file |
| `SqlBeforeFirstName` | The preamble contains content other than whitespace, comments and markers | The first such content |
| `SummaryBeforeFirstName` | The preamble contains `-- summary:` | The marker |
| `UnknownDirective` | A directive name is not recognised | The directive |
| `EmptyDirectiveLine` | A `-- SqlSource:` marker has no directives | The marker |
| `InvalidDirectiveValue` | A value is missing, is not an identifier, or is given to a directive that takes none | The directive |
| `ConflictingDirectives` | `token-validation` and `no-token-validation` appear in one scope | The second of the two |
| `EmptyBlock` | A block has no content other than whitespace, comments and markers | The name, or an empty span at the start of the file |
| `ReservedTokenName` | A token name is a reserved keyword and is not ignored | The token |

## Testing

Tests live in `tests/SqlSource.Tests/Parsing/` and use xunit v3 and Shouldly, like the existing test.  The parser types are internal, so `SqlSource.csproj` gives the test assembly `InternalsVisibleTo`.

- **Lexer:** each quoted form and its escape; nested block comments; both hint forms; dollar quotes with and without a tag, after an identifier character, and without a closer; both unterminated cases; each known limit.
- **Parser:** marker recognition and its near misses (a marker after SQL on the same line, inside a block comment, inside a quoted region, with a space before the colon); preamble rules; the file without name markers; directive scope and overrides; both output modes, including each numbered output rule; one test for each error kind that asserts its span; several errors reported from one file.
- **Token scanner:** repeated names; order of appearance; spaces inside the braces; malformed tokens left as text; `token-ignore`, including a reserved keyword and a name that matches nothing.
- **Model:** two parses of the same text produce equal results.

Each rule is written as a failing test before its code.

## Documentation

- `src/SqlSource/AGENTS.md`: the statement that no polyfill is set up becomes wrong and is updated.  The parser's constraints are added: no file access, ordinal comparisons, value-equal results.
- `README.md` and `CONTRIBUTING.md` do not change.  Nothing a user or a contributor does is different after this phase.

## Verification

- `./pre-commit-validation.sh` exits zero with every step passed.
- The existing smoke test still passes: the generator produces no output and no diagnostics.
