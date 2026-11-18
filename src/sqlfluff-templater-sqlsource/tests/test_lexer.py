import pytest

from sqlfluff_templater_sqlsource.dialect_marker import apply_dialect_marker, read_dialect_header
from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions, parse_dialect_name
from sqlfluff_templater_sqlsource.errors import ParseError
from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind, Lexer
from sqlfluff_templater_sqlsource.quoting import (
    NOT_A_QUOTE,
    UNTERMINATED,
    BackslashQuoteReader,
    BracketReader,
    DollarQuoteReader,
    DoubledQuoteReader,
    EscapeStringReader,
    QuoteOperatorReader,
    QuoteReader,
)
from sqlfluff_templater_sqlsource.rules import DialectRules

ANSI = DialectRules.for_choice(DialectChoice(Dialect.ANSI))
MYSQL = DialectRules.for_choice(DialectChoice(Dialect.MYSQL))


def lex(dialect: str, text: str) -> tuple[list[list], ParseError | None]:
    lexer = Lexer(text, DialectRules.for_choice(parse_dialect_name(dialect)))
    lexemes, error = lexer.read_to_end()
    return [[lexeme.kind.value, lexeme.start, lexeme.end] for lexeme in lexemes], error


# The rows of Lex_UnterminatedQuote_ReportsTheOpeningQuote, Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter
# and Lex_KnownLimit_* in SqlLexerTests: the error is at the opening delimiter, and no lexeme is given.
@pytest.mark.parametrize(
    ("dialect", "text", "start"),
    [
        ("ansi", "'abc", 0),
        ("ansi", 'x "abc', 2),
        ("ansi", "`abc", 0),
        ("ansi", "'abc''", 0),
        ("ansi", "E'abc\\'", 1),
        ("ansi", "E'abc\\", 1),
        ("ansi", "'a\\'b'", 5),
        ("ansi", "q'[it's]'", 8),
        ("mssql", "x [abc", 2),
        ("mssql", "[abc]]", 0),
        ("sqlite", "[abc", 0),
        ("oracle", "q'[abc]", 1),
        ("oracle", "nq'[abc' x", 2),
        ("mysql", "'abc\\'", 0),
        ("mariadb", '"abc\\"', 0),
        ("postgres", "E'a'\n'b\\'", 5),
        ("postgres", "E'a' -- c\n'b\\'c'\n  'd\\'", 19),
        ("mysql", "/*!50700 '*/' */", 12),
        ("postgres", "b'\\''", 1),
    ],
)
def test_unterminated_quote_is_reported_at_its_opening_delimiter(dialect, text, start):
    assert lex(dialect, text) == ([], ParseError("SQLSRC101", start, start + 1))


# The rows of Lex_UnterminatedBlockComment_ReportsTheOpener and Lex_KnownLimit_CommentOpenerInsideCommentMustBeClosed.
@pytest.mark.parametrize(
    ("text", "start"),
    [("SELECT /* x", 7), ("/*+ x", 0), ("/*/", 0), ("/* /* x */", 0), ("/* a /* b */ SELECT 1", 0)],
)
def test_unterminated_block_comment_is_reported_at_its_opener(text, start):
    assert lex("ansi", text) == ([], ParseError("SQLSRC102", start, start + 2))


@pytest.mark.parametrize(
    "dialect", ["ansi", "mssql", "postgres", "cockroachdb", "mysql", "mariadb", "sqlite", "oracle"]
)
def test_unterminated_block_comment_is_an_error_in_every_dialect(dialect):
    assert lex(dialect, "SELECT 1 /* x") == ([], ParseError("SQLSRC102", 9, 11))


def test_unterminated_block_comment_in_the_header_is_left_to_read_to_end():
    lexer = Lexer("-- a\n/* b", DialectRules.for_choice(DialectChoice(Dialect.ANSI)))

    assert lexer.try_read_leading_comment() is not None
    assert lexer.try_read_leading_comment() is None
    assert lexer.try_read_leading_comment() is None
    assert lexer.read_to_end() == ([], ParseError("SQLSRC102", 5, 7))


# A dollar that opens no quote is plain text, where a quote that is never closed is an error.
@pytest.mark.parametrize("text", ["$1 x", "$a", "$$ x", "$a$ x", "$a$ $b$ x", "$a$ x $A$", "$$"])
def test_dollar_that_opens_no_quote_is_text(text):
    assert lex("postgres", text) == ([["Text", 0, len(text)]], None)


ESCAPE_STRING = EscapeStringReader("Ee")


# The rows of the *ReaderTests in Quoting/ that end in UNTERMINATED or NOT_A_QUOTE.  The reader is called at the
# first opener of the text, as Region.Read calls it.
@pytest.mark.parametrize(
    ("reader", "text", "opener", "expected"),
    [
        (DoubledQuoteReader(), "'abc", "'", UNTERMINATED),
        (DoubledQuoteReader(), "'abc''", "'", UNTERMINATED),
        (DoubledQuoteReader(), "'abc''def", "'", UNTERMINATED),
        (BackslashQuoteReader(), "'abc", "'", UNTERMINATED),
        (BackslashQuoteReader(), "'abc\\'", "'", UNTERMINATED),
        (BackslashQuoteReader(), "'abc\\", "'", UNTERMINATED),
        (BackslashQuoteReader(), "'abc''", "'", UNTERMINATED),
        (ESCAPE_STRING, "E'abc", "'", UNTERMINATED),
        (ESCAPE_STRING, "E'abc\\'", "'", UNTERMINATED),
        (ESCAPE_STRING, "E'abc\\", "'", UNTERMINATED),
        (ESCAPE_STRING, "'abc", "'", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=True), "[abc", "[", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=True), "[", "[", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=True), "[abc]]", "[", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=True), "[abc]]def", "[", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=False), "[abc", "[", UNTERMINATED),
        (BracketReader(doubled_closer_escapes=False), "[", "[", UNTERMINATED),
        (QuoteOperatorReader(), "q'[abc", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'[abc]", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'[abc'", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'[abc] '", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'[", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'", "'", UNTERMINATED),
        (QuoteOperatorReader(), "q'' x", "'", UNTERMINATED),
        (QuoteOperatorReader(), "'abc", "'", UNTERMINATED),
        (DollarQuoteReader(), "$1 x", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$5.00 x", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$1$ x $1$", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$a-b$ x $a-b$", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$a", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$$ x", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$a$ x", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$a$ $b$ x", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$a$ x $A$", "$", NOT_A_QUOTE),
        (DollarQuoteReader(), "$$", "$", NOT_A_QUOTE),
    ],
)
def test_reader_without_a_close_gives_unterminated_or_not_a_quote(reader: QuoteReader, text, opener, expected):
    assert reader.find_end(text, text.index(opener)) == expected


# A code point outside the BMP where a name, a prefix or a delimiter is read, which no corpus case has.  C# reads it
# as two surrogates, and each row is what SqlFileParser.PlanTemplating gives for it.
@pytest.mark.parametrize(
    ("dialect", "text", "expected"),
    [
        ("mssql", "@x\U0001d400 y", [["Parameter", 0, 2], ["Text", 2, 5]]),
        ("postgres", "@x\U0001d400 y", [["Parameter", 0, 2], ["Text", 2, 5]]),
        ("mssql", "@\U0001d400", [["Text", 0, 2]]),
        ("mssql", "\U0001d400@x", [["Text", 0, 1], ["Parameter", 1, 3]]),
        ("postgres", "\U0001d400@x", [["Text", 0, 1], ["Parameter", 1, 3]]),
        ("postgres", "\U0001f600$$a$$", [["Text", 0, 6]]),
        ("postgres", "$\U0001f600$x$\U0001f600$", [["Quoted", 0, 7]]),
        ("oracle", "\U0001d400q'[a]'", [["Text", 0, 2], ["Quoted", 2, 7]]),
        ("mysql", "--\U0001f600", [["Text", 0, 3]]),
    ],
)
def test_code_point_outside_the_bmp_is_read_as_csharp_reads_its_surrogates(dialect, text, expected):
    assert lex(dialect, text) == (expected, None)


@pytest.mark.parametrize(
    ("dialect", "text", "expected"),
    [
        # The E after a name character is no prefix, so the backslash does not escape.
        ("postgres", "\U0001f600E'a\\'b'", ParseError("SQLSRC101", 7, 8)),
        # A delimiter outside the BMP never closes Oracle's quote operator.
        ("oracle", "SELECT q'\U0001f600a\U0001f600' FROM t", ParseError("SQLSRC101", 8, 9)),
    ],
)
def test_code_point_outside_the_bmp_can_leave_a_region_unterminated(dialect, text, expected):
    assert lex(dialect, text) == ([], expected)


# The rows of SqlDialectMarkerTests.
@pytest.mark.parametrize(
    "text",
    [
        "-- dialect: mysql\n-- name: A\nSELECT 1",
        "-- DIALECT: MySql\n-- name: A\nSELECT 1",
        "--dialect:mysql\n-- name: A\nSELECT 1",
        "-- dialect: mysql  \r\n-- name: A\r\nSELECT 1",
        "\n\n  -- dialect: mysql\nSELECT 1",
        "-- a comment\n-- generator: keep-comments\n-- dialect: mysql\nSELECT 1",
        "/* Copyright\n   (c) Example */\n-- dialect: mysql\n-- name: A\nSELECT 1",
    ],
)
def test_marker_in_the_header_switches_the_lexer(text):
    lexer = Lexer(text, ANSI)

    apply_dialect_marker(lexer, text)

    assert lexer.rules is MYSQL


@pytest.mark.parametrize(
    "text",
    [
        "SELECT 1",
        "-- name: A\nSELECT 1",
        "",
        "SELECT 1;\n-- dialect: mysql\n",
        "-- a\nSELECT 1; -- dialect: mysql",
        "/*+ h */\n-- dialect: mysql\nSELECT 1",
        "-- name: A\n-- dialect: mysql\nSELECT 1",
        "-- dialect: pgsql\nSELECT 1",
        "-- dialect:\nSELECT 1",
        "-- dialect: mysql keep-comments\nSELECT 1",
        "-- dialect: postgres,ansi-quotes\nSELECT 1",
        "-- dialect=mysql\nSELECT 1",
        "-- dialects: mysql\nSELECT 1",
        "/* -- dialect: mysql */\nSELECT 1",
        "-- generator: dialect=mysql\nSELECT 1",
    ],
)
def test_no_valid_marker_in_the_header_leaves_the_lexer_as_it_was(text):
    lexer = Lexer(text, ANSI)

    apply_dialect_marker(lexer, text)

    assert lexer.rules is ANSI


def test_invalid_marker_after_a_valid_one_gives_both():
    text = "-- dialect: mysql,ansi-quotes\n-- dialect: nope\n-- dialect: oracle\nSELECT 1"
    lexer = Lexer(text, ANSI)

    header = read_dialect_header(lexer, text)

    assert header.named == DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)
    assert header.invalid == ParseError("SQLSRC111", text.index("nope"), text.index("nope") + 4)
    assert lexer.rules is DialectRules.for_choice(header.named)


def test_invalid_marker_without_a_value_is_placed_at_the_marker():
    text = "-- dialect:\nSELECT 1"

    header = read_dialect_header(Lexer(text, ANSI), text)

    assert header.invalid == ParseError("SQLSRC111", 0, 11)


def test_comment_form_of_the_starting_dialect_above_the_marker_is_read():
    text = "# licence\n-- dialect: postgres\nSELECT 1"
    under_mysql = Lexer(text, MYSQL)
    under_ansi = Lexer(text, ANSI)

    apply_dialect_marker(under_mysql, text)
    header_end = apply_dialect_marker(under_ansi, text)

    assert under_mysql.rules is DialectRules.for_choice(DialectChoice(Dialect.POSTGRESQL))
    assert under_ansi.rules is ANSI
    assert header_end == 0


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("-- dialect: mysql\n# c\nSELECT 1", 21),
        ("-- a\n-- name: A\nSELECT 1", 5),
        ("-- dialect: mysql\n\n  -- name: A\nSELECT 1", 21),
        ("-- name: A\n-- name: B\nSELECT 1", 0),
        ("SELECT 1", 0),
        ("  \n SELECT 1", 0),
        ("", 0),
        ("-- a\nSELECT 1", 4),
        ("-- a\n/* b */ SELECT 1", 12),
        ("-- a\n-- b", 9),
        ("-- a\n/*+ h */ SELECT 1", 4),
    ],
)
def test_header_ends_at_its_name_marker_or_after_its_last_leading_comment(text, expected):
    assert apply_dialect_marker(Lexer(text, ANSI), text) == expected


@pytest.mark.parametrize(
    ("kind", "start", "end", "expected"),
    [
        (LexemeKind.TEXT, 0, 12, (2, 10)),
        (LexemeKind.TEXT, 10, 12, None),
        (LexemeKind.QUOTED, 12, 15, (12, 15)),
        (LexemeKind.PARAMETER, 16, 19, (16, 19)),
        (LexemeKind.HINT, 20, 28, (20, 28)),
        (LexemeKind.LINE_COMMENT, 29, 33, None),
        (LexemeKind.BLOCK_COMMENT, 34, 41, None),
    ],
)
def test_content_span_is_sql_without_comments_and_the_white_space_around_text(kind, start, end, expected):
    text = "  SELECT 1 \n'a' @id /*+ h */ -- c\n/* b */"

    assert Lexeme(kind, start, end).content_span(text) == expected


def test_parameter_name_is_the_lexeme_without_its_prefix():
    assert Lexeme(LexemeKind.PARAMETER, 7, 10).parameter_name == (8, 10)
