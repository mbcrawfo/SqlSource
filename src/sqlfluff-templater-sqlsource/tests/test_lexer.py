import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions, parse_dialect_name
from sqlfluff_templater_sqlsource.lexer import Lexer, LexError, apply_dialect_marker, read_dialect_header
from sqlfluff_templater_sqlsource.rules import DialectRules

ANSI = DialectRules.for_choice(DialectChoice(Dialect.ANSI))
MYSQL = DialectRules.for_choice(DialectChoice(Dialect.MYSQL))


def lex(dialect: str, text: str) -> tuple[list[list], LexError | None]:
    lexer = Lexer(text, DialectRules.for_choice(parse_dialect_name(dialect)))
    lexemes, error = lexer.read_to_end()
    return [[lexeme.kind.value, lexeme.start, lexeme.end] for lexeme in lexemes], error


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
        ("postgres", "\U0001f600E'a\\'b'", LexError("SQLSRC101", 7, 8)),
        # A delimiter outside the BMP never closes Oracle's quote operator.
        ("oracle", "SELECT q'\U0001f600a\U0001f600' FROM t", LexError("SQLSRC101", 8, 9)),
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
    assert header.invalid == LexError("SQLSRC111", text.index("nope"), text.index("nope") + 4)
    assert lexer.rules is DialectRules.for_choice(header.named)


def test_invalid_marker_without_a_value_is_placed_at_the_marker():
    text = "-- dialect:\nSELECT 1"

    header = read_dialect_header(Lexer(text, ANSI), text)

    assert header.invalid == LexError("SQLSRC111", 0, 11)


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
