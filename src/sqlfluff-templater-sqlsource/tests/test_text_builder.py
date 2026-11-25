"""The cases of `SqlTextBuilderTests` in `tests/SqlSource.Tests/Parsing/` that build SQL without its comments, the
only form the plan reads."""

import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.lexer import Lexer
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.text_builder import BlockText, build

ANSI = DialectRules.for_choice(DialectChoice(Dialect.ANSI))


def built(text: str, rules: DialectRules = ANSI) -> BlockText:
    lexemes, error = Lexer(text, rules).read_to_end()
    assert error is None
    return build(text, rules, lexemes, 0, len(lexemes))


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        # Line comments are deleted.
        ("SELECT 1 -- c", "SELECT 1"),
        ("SELECT 1 -- c\nFROM t -- d", "SELECT 1\nFROM t"),
        ("SELECT 1\n-- c\nFROM t", "SELECT 1\nFROM t"),
        ("-- c\nSELECT 1\n  -- d", "SELECT 1"),
        # A block comment is one space.
        ("SELECT/**/1", "SELECT 1"),
        ("SELECT a /* x\n y */ FROM t", "SELECT a   FROM t"),
        ("/* c */\nSELECT 1", "SELECT 1"),
        ("SELECT 1 /* a /* b */ c */", "SELECT 1"),
        # Blank lines are removed.
        ("SELECT 1\n\n  \n\t\nFROM t", "SELECT 1\nFROM t"),
        ("\n\nSELECT 1\n\n", "SELECT 1"),
        ("SELECT 1\n/* a */ -- b\nFROM t", "SELECT 1\nFROM t"),
        # Nothing but comments and white space.
        ("-- c\n/* d */", ""),
        ("", ""),
        (" \n ", ""),
        # Line terminators become line feeds.
        ("SELECT 1\r\nFROM t\r\n", "SELECT 1\nFROM t"),
        ("SELECT 1\rFROM t\r", "SELECT 1\nFROM t"),
        ("SELECT 'a\r\nb', 'c\rd'", "SELECT 'a\nb', 'c\nd'"),
        ("SELECT 1\r\n-- summary: x\r\nFROM t", "SELECT 1\nFROM t"),
        ("/*+ a\r\n b */ 1", "/*+ a\n b */ 1"),
        # Trailing blanks are removed, but inside a quoted region.
        ("SELECT 1   \nFROM t\t", "SELECT 1\nFROM t"),
        ("SELECT 'a  \n\n  b'  \n", "SELECT 'a  \n\n  b'"),
        # Marker lines are removed.
        ("-- summary: x\nSELECT 1\n  -- generator: keep-comments\nFROM t\n-- name: Next", "SELECT 1\nFROM t"),
        ("SELECT 1\n-- summary: x   ", "SELECT 1"),
        ("\t-- summary: x\nSELECT 1", "SELECT 1"),
        ("-- dialect: mysql\nSELECT 1", "SELECT 1"),
        ("-- SqlSource: keep-comments\nSELECT 1", "SELECT 1"),
        ("SELECT 'ñ\U0001f600' -- é", "SELECT 'ñ\U0001f600'"),
    ],
)
def test_build_strips_comments_and_cleans_lines(text, expected):
    assert built(text).text == expected


@pytest.mark.parametrize(
    "text",
    [
        "SELECT /*+ H */ 1 /*! M */",
        "SELECT '-- x', \"/* y */\", `-- z` FROM t",
        "SELECT $$ -- x $$",
        "  SELECT 1\n    FROM t",
        "SELECT  1   FROM\tt",
        "/*+ a  \n\n b */",
        "SELECT /*! 'line1\n\nline2   \nx' */ 1",
    ],
)
def test_build_keeps_hints_quoted_regions_and_spacing(text):
    assert built(text).text == text


@pytest.mark.parametrize(
    ("dialect", "text", "expected"),
    [
        (Dialect.MYSQL, "SELECT 1 # c\n# d\nFROM t # e", "SELECT 1\nFROM t"),
        (Dialect.ORACLE, "SELECT --+ FULL(e)  \n  1 -- c\nFROM e", "SELECT --+ FULL(e)  \n  1\nFROM e"),
        (Dialect.MARIADB, "SELECT /*M! SQL_NO_CACHE */ 1 /* c */", "SELECT /*M! SQL_NO_CACHE */ 1"),
    ],
)
def test_build_of_a_dialect_reads_its_comments_and_hints(dialect, text, expected):
    assert built(text, DialectRules.for_choice(DialectChoice(dialect))).text == expected


# MySQL ends a line only at a line feed, so a carriage return alone stays in the SQL.  A carriage return and a line
# feed end a line as in every dialect.
@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("SELECT 1 -- c\r, 2\nFROM t", "SELECT 1\nFROM t"),
        ("SELECT 1,\r  2  \r\n\r\nFROM t", "SELECT 1,\r  2\nFROM t"),
    ],
)
def test_carriage_return_alone_under_mysql_ends_no_line(text, expected):
    assert built(text, DialectRules.for_choice(DialectChoice(Dialect.MYSQL))).text == expected


def test_build_of_a_lexeme_range_uses_only_that_range():
    text = "A\n-- name: X\nB\n"
    lexemes, _ = Lexer(text, ANSI).read_to_end()

    assert build(text, ANSI, lexemes, 0, 1).text == "A"
    assert build(text, ANSI, lexemes, 2, 3).text == "B"
    assert build(text, ANSI, lexemes, 2, 2).text == ""


def test_parameters_are_copied_and_their_places_in_the_sql_are_kept():
    sql = built("SELECT @a, -- note @x\n    @b /* c */ FROM t\n\nWHERE x = @a;\n")

    assert sql.text == "SELECT @a,\n    @b   FROM t\nWHERE x = @a;"
    assert [sql.text[start:end] for start, end in sql.parameters] == ["@a", "@b", "@a"]


def test_to_source_span_maps_a_span_of_the_sql_to_the_same_text_in_the_file():
    text = "-- c\r\n/* x */ SELECT {{a}} -- d\r\n\r\n-- summary: s\r\n  FROM {{b}}"
    sql = built(text)

    for token in ("{{a}}", "{{b}}"):
        start = sql.text.index(token)
        assert sql.to_source_span(start, start + len(token)) == (text.index(token), text.index(token) + len(token))


def test_to_source_span_maps_every_character_of_the_sql_to_its_place_in_the_file():
    text = (
        "  -- lead\r\n\r\n-- summary: s\r\nSELECT 'a  \r\n\r\n b', /* c */ x   \r\n"
        "\t-- token-ignore: q\r\n"
        "\r\n  /*+ h\r\n  i */ FROM t -- d  \r\n   \r\nWHERE {{y}} = $$ z\n $$  \r\n"
    )
    sql = built(text)

    for index, character in enumerate(sql.text):
        if character == "\n":
            continue
        source = text[sql.to_source_span(index, index + 1)[0]]
        assert source == character or (character == " " and source == "/"), f"offset {index} maps to {source!r}"


@pytest.mark.parametrize(
    ("text", "at", "expected"),
    [
        ("SELECT a -- c\nFROM t", "--", "SELECT a"),
        ("SELECT a -- c", "--", "SELECT a"),
        ("SELECT a /* c */ b", "/*", "SELECT a "),
        ("SELECT a /* c */ b", "b", "SELECT a   "),
        ("SELECT a\r\n-- c\r\n\r\nFROM t", "--", "SELECT a"),
        ("-- c\nSELECT a", "--", ""),
        ("SELECT 'x\r\ny' -- c", "--", "SELECT 'x\ny'"),
        ("SELECT a -- c\nFROM t", "FROM", "SELECT a\n"),
    ],
)
def test_to_output_maps_an_offset_of_the_file_to_the_end_of_the_text_copied_from_before_it(text, at, expected):
    sql = built(text)

    assert sql.text[: sql.to_output(text.index(at))] == expected
