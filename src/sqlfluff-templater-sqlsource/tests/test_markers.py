"""What `SqlMarkerReader` gives for the marker form, a name marker's value and where a marker's error is placed."""

import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind, Lexer
from sqlfluff_templater_sqlsource.markers import has_marker_form, place_of, read_marker, split_name
from sqlfluff_templater_sqlsource.rules import DialectRules

ANSI = DialectRules.for_choice(DialectChoice(Dialect.ANSI))


def comments(text: str, dialect: Dialect = Dialect.ANSI) -> list[Lexeme]:
    lexemes, _ = Lexer(text, DialectRules.for_choice(DialectChoice(dialect))).read_to_end()
    return [lexeme for lexeme in lexemes if lexeme.kind == LexemeKind.LINE_COMMENT]


@pytest.mark.parametrize(
    ("text", "form", "marker"),
    [
        ("-- name: A", True, True),
        ("--name:A", True, True),
        ("--\t TOKEN-IGNORE: a", True, True),
        ("  -- summary: x", True, True),
        ("SELECT 1 -- summary: x", True, False),
        ("SELECT 1\n-- column: x", True, True),
        ("-- just a note", False, False),
        ("-- name A", False, False),
        ("-- SqlSource: keep-comments", False, False),
        ("-- tokens: x", False, False),
    ],
)
def test_marker_form_is_the_marker_wherever_it_stands(text, form, marker):
    (comment,) = comments(text)

    assert has_marker_form(text, comment) == form
    assert (read_marker(text, comment, ANSI) is not None) == marker


# A carriage return alone ends no line under MySQL or SQLite, so a comment after it does not start its line.
@pytest.mark.parametrize(("dialect", "marker"), [(Dialect.ANSI, True), (Dialect.MYSQL, False), (Dialect.SQLITE, False)])
def test_comment_after_a_carriage_return_alone_is_a_marker_where_that_ends_a_line(dialect, marker):
    text = "SELECT 1 /* x */\r-- name: A"
    (comment,) = comments(text, dialect)

    assert (read_marker(text, comment, DialectRules.for_choice(DialectChoice(dialect))) is not None) == marker


def test_hash_comment_has_no_marker_form():
    text = "# name: A"
    (comment,) = comments(text, Dialect.MYSQL)

    assert not has_marker_form(text, comment)


def test_text_is_never_in_marker_form():
    assert not has_marker_form("-- name: A", Lexeme(LexemeKind.TEXT, 0, 10))


@pytest.mark.parametrize(
    ("value", "name", "arrow"),
    [
        ("Users", "Users", -1),
        ("Users -> many", "Users", 6),
        ("Users->one", "Users", 5),
        ("-> one", "", 0),
        ("A -> b -> c", "A", 2),
    ],
)
def test_split_name_splits_a_name_marker_at_its_first_arrow(value, name, arrow):
    text = "-- name: " + value
    marker = read_marker(text, comments(text)[0], ANSI)

    (start, end), found = split_name(text, marker.value_start, marker.value_end)

    assert text[start:end] == name
    assert found == (-1 if arrow < 0 else marker.value_start + arrow)


@pytest.mark.parametrize(("text", "place"), [("-- token: {{a}}", "{{a}}"), ("-- token:", "-- token:")])
def test_place_of_a_marker_is_its_value_or_the_marker_when_it_has_none(text, place):
    marker = read_marker(text, comments(text)[0], ANSI)

    start, end = place_of(marker)

    assert text[start:end] == place
