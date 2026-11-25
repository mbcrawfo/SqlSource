"""What `SqlDialectRules` gives for the readings the list rule and the parser share, with the C# parser's values."""

import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.rules import DialectRules

SQL_SERVER = DialectRules.for_choice(DialectChoice(Dialect.SQL_SERVER))
POSTGRESQL = DialectRules.for_choice(DialectChoice(Dialect.POSTGRESQL))
MYSQL = DialectRules.for_choice(DialectChoice(Dialect.MYSQL))

ZERO_WIDTH_SPACE = "\u200b"
NON_BMP_LETTER = "\U0001d400"


@pytest.mark.parametrize(
    ("value", "sql_server", "postgresql"),
    [
        ("a", True, True),
        ("_", True, True),
        ("7", True, True),
        ("$", True, True),
        ("@", True, False),
        ("#", True, False),
        ("-", False, False),
        (" ", False, False),
        ("é", True, True),
        # A combining mark: a name character of SQL Server; any character above U+007F is one of PostgreSQL.
        ("\u0301", True, True),
        # A symbol above U+007F.
        ("\u00a9", False, True),
        # SQL Server reads the zero-width space as a space.
        (ZERO_WIDTH_SPACE, False, True),
        # To C# it is two surrogates, which SQL Server does not take and PostgreSQL does.
        (NON_BMP_LETTER, False, True),
    ],
)
def test_is_name_character_reads_names_as_the_dialect_does(value, sql_server, postgresql):
    assert SQL_SERVER.is_name_character(value) == sql_server
    assert POSTGRESQL.is_name_character(value) == postgresql
    assert MYSQL.is_name_character(value) == postgresql


@pytest.mark.parametrize(
    ("value", "sql_server", "postgresql"),
    [
        (" ", True, True),
        ("\t", True, True),
        ("\n", True, True),
        ("\u00a0", True, True),
        ("\u2028", True, True),
        ("a", False, False),
        # Not white space to char.IsWhiteSpace, though Python's str.isspace takes it.
        ("\x1c", False, False),
        # A space only where names do not take it.
        (ZERO_WIDTH_SPACE, True, False),
    ],
)
def test_is_space_is_white_space_and_the_zero_width_space_outside_names(value, sql_server, postgresql):
    assert SQL_SERVER.is_space(value) == sql_server
    assert POSTGRESQL.is_space(value) == postgresql


@pytest.mark.parametrize("dialect", list(Dialect))
def test_only_sql_server_declares_variables(dialect):
    assert DialectRules.for_choice(DialectChoice(dialect)).declared_variables == (dialect == Dialect.SQL_SERVER)


@pytest.mark.parametrize(
    ("dialect", "expected"),
    [
        (Dialect.ANSI, True),
        (Dialect.SQL_SERVER, True),
        (Dialect.POSTGRESQL, True),
        (Dialect.MYSQL, False),
        (Dialect.MARIADB, False),
        (Dialect.SQLITE, False),
        (Dialect.ORACLE, True),
        (Dialect.COCKROACHDB, False),
    ],
)
def test_carriage_return_ends_a_line_where_the_dialect_says(dialect, expected):
    rules = DialectRules.for_choice(DialectChoice(dialect))

    assert rules.carriage_return_ends_line == expected
    assert rules.is_line_terminator("\r") == expected
    assert rules.is_line_terminator("\n")
    assert not rules.is_line_terminator(" ")


# The cases of `FindLineEnd_Range_GivesWhereItsFirstLineEnds`.
@pytest.mark.parametrize(
    ("dialect", "text", "start", "end", "at"),
    [
        (Dialect.ANSI, "ab\rc\nd", 0, 6, 2),
        (Dialect.MYSQL, "ab\rc\nd", 0, 6, 4),
        (Dialect.MYSQL, "ab\r\nd", 0, 5, 2),
        (Dialect.MYSQL, "a\r\r\nd", 0, 5, 2),
        (Dialect.MYSQL, "\r\nd", 1, 3, 1),
        (Dialect.MYSQL, "ab\rc", 0, 4, 4),
        (Dialect.MYSQL, "ab\nc", 0, 2, 2),
        (Dialect.MYSQL, "a\nb\nc", 2, 5, 3),
    ],
)
def test_find_line_end_gives_where_the_first_line_ends(dialect, text, start, end, at):
    assert DialectRules.for_choice(DialectChoice(dialect)).find_line_end(text, start, end) == at


# Rows of `Lex_VersionedComment_IsReadAsTheServerReadsIt`: where the body of a versioned comment starts when the
# server runs it, or None.
@pytest.mark.parametrize(
    ("dialect", "version", "text", "body"),
    [
        (Dialect.MYSQL, 0, "/*! x */", 3),
        (Dialect.MYSQL, 0, "/*!1234 x */", 3),
        (Dialect.MYSQL, 0, "/*!50700 x */", None),
        (Dialect.MYSQL, 80400, "/*!50700 x */", 8),
        (Dialect.MYSQL, 80400, "/*!100100\tx */", None),
        (Dialect.MYSQL, 80400, "/*!100100\nx */", None),
        (Dialect.MYSQL, 80400, "/*!100100, x */", 8),
        (Dialect.MYSQL, 80400, "/*!080400 x */", 9),
        (Dialect.MYSQL, 80400, "/*M!50700 x */", None),
        (Dialect.MARIADB, 110400, "/*!50700 x */", None),
        (Dialect.MARIADB, 110400, "/*!50699 x */", 8),
        (Dialect.MARIADB, 110400, "/*M!50700 x */", 9),
        (Dialect.MARIADB, 110400, "/*!100100, x */", 9),
        (Dialect.MARIADB, 110400, "/*!5060000 x */", None),
    ],
)
def test_runs_versioned_comment_reads_the_version_as_the_server_does(dialect, version, text, body):
    rules = DialectRules.for_choice(DialectChoice(dialect, server_version=version))

    assert rules.runs_versioned_comment(text, 0) == body
