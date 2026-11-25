"""How each dialect reads comments, quoted regions and parameters.

Mirrors `SqlDialectRules.cs` and `SqlStringContinuation.cs` in `src/SqlSource/Parsing/`.  `SqlText.cs` is mirrored by
`text.py` instead, since `quoting.py` needs it and this module imports `quoting.py`.

Every dialect-sensitive choice is here or in a reader of `quoting.py`.  The rules below are the whole table: a value
where dialects differ by a setting, a reader where they differ by how a quoted region ends.  Changing a cell changes
the SQL users get.
"""

import enum
import re

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions
from sqlfluff_templater_sqlsource.quoting import (
    ZERO_WIDTH_SPACE,
    BackslashQuoteReader,
    BracketReader,
    DollarQuoteReader,
    DoubledQuoteReader,
    EscapeStringReader,
    QuoteOperatorReader,
    QuoteReader,
    is_name_character_of_postgresql,
    is_name_character_of_sql_server,
)
from sqlfluff_templater_sqlsource.text import find_line_end, is_letter_or_digit, is_white_space


class StringContinuation(enum.Enum):
    """Whether a string goes on after it closes, and what may stand in the gap, which always needs a line break."""

    # A string ends at its closing quote.
    NONE = enum.auto()

    # Only white space may stand in the gap, as in CockroachDB.
    ACROSS_WHITESPACE = enum.auto()

    # White space and line comments may stand in the gap, as in PostgreSQL.
    ACROSS_LINE_COMMENTS = enum.auto()


# The first characters of --, # and /*.
_COMMENT_STARTERS = "-#/"

# The prefix of a parameter in every dialect SqlSource reads.
_AT_SIGN = "@"

# One for each combination of DialectOptions.
_OPTION_SETS = 4


class DialectRules:
    """How one dialect reads comments and quoted regions.  The lexer reads by the rules it is given."""

    def __init__(
        self,
        dialect: DialectChoice,
        readers: dict[str, QuoteReader],
        *,
        nested_comments: bool = False,
        declared_variables: bool = False,
        dash_needs_whitespace: bool = False,
        hash_comments: bool = False,
        line_hints: bool = False,
        mariadb_hints: bool = False,
        string_continuation: StringContinuation = StringContinuation.NONE,
        names_hold_at_and_hash: bool = False,
        carriage_return_ends_line: bool = True,
    ) -> None:
        self.dialect = dialect
        self.parameter_prefix = _AT_SIGN
        # Whether a name that a query declares with DECLARE is a local variable and not a parameter, as in T-SQL.
        self.declared_variables = declared_variables
        # Whether a /* inside a block comment opens a comment that needs its own */.
        self.nested_comments = nested_comments
        # Whether -- starts a comment only before white space, a control character or the end, as in MySQL.
        self.dash_needs_whitespace = dash_needs_whitespace
        # Whether # starts a comment that runs to the end of its line.
        self.hash_comments = hash_comments
        # Whether --+ starts a hint that runs to the end of its line, as in Oracle.
        self.line_hints = line_hints
        # Whether a block comment that starts /*M! is a hint, as in MariaDB.
        self.mariadb_hints = mariadb_hints
        # Whether a string goes on after a gap with a line break, and what the gap may hold.
        self.string_continuation = string_continuation
        # Whether a carriage return with no line feed after it ends a line.  MySQL, MariaDB, SQLite and CockroachDB
        # end a line only at a line feed.  A carriage return and a line feed end a line at the carriage return in every
        # dialect.
        self.carriage_return_ends_line = carriage_return_ends_line
        # Whether a name may hold @ and #, and of the characters above U+007F only those T-SQL takes.  A parameter's
        # name is read by the same rule; otherwise it takes letters, digits and _.
        self._names_hold_at_and_hash = names_hold_at_and_hash
        self._readers = readers
        # Every character that can start something other than plain text: a comment in any dialect, a parameter, or
        # a quoted region in this one.
        self._starters = re.compile("[" + re.escape(_COMMENT_STARTERS + self.parameter_prefix + "".join(readers)) + "]")

    @staticmethod
    def for_choice(choice: DialectChoice) -> "DialectRules":
        """The rules of `choice`: one shared instance for each.  Options that the dialect does not have are ignored."""
        if choice.dialect in (Dialect.MYSQL, Dialect.MARIADB):
            return _BY_DIALECT_AND_OPTIONS[(choice.dialect, choice.options)]
        return _BY_DIALECT_AND_OPTIONS[(choice.dialect, DialectOptions.NONE)]

    def is_name_character(self, value: str) -> bool:
        """True for a character of a name in this dialect: as SQL Server reads it there, as PostgreSQL does
        elsewhere."""
        if self._names_hold_at_and_hash:
            return is_name_character_of_sql_server(value)
        return is_name_character_of_postgresql(value)

    def is_space(self, value: str) -> bool:
        """True for white space, and for the zero-width space where this dialect's names do not take it."""
        return is_white_space(value) or (value == ZERO_WIDTH_SPACE and not self.is_name_character(value))

    def is_parameter_name_character(self, value: str) -> bool:
        """True for a character of a parameter's name: as SQL Server reads a name there, the first character too,
        and a letter, a digit or `_` elsewhere."""
        if self._names_hold_at_and_hash:
            return is_name_character_of_sql_server(value)
        return is_letter_or_digit(value) or value == "_"

    def find_parameter_name_end(self, text: str, name_start: int, end: int) -> int:
        """The offset after the name of a parameter that starts at `name_start`, read no further than `end`.

        It is `name_start` when no name follows, and when the prefix follows: `@@rowcount` is a function.
        """
        if name_start >= end or text[name_start] == self.parameter_prefix:
            return name_start
        index = name_start
        while index < end and self.is_parameter_name_character(text[index]):
            index += 1
        return index

    def is_line_terminator(self, value: str) -> bool:
        """Whether `value` ends a line in this dialect: `\\n`, and `\\r` where `carriage_return_ends_line`."""
        return value == "\n" or (value == "\r" and self.carriage_return_ends_line)

    def find_line_end(self, text: str, start: int, end: int) -> int:
        """The offset where the first line from `start` ends, no further than `end`: at its line terminator, or at the
        `\\r` of a `\\r\\n`, or `end` when no line ends before it."""
        if self.carriage_return_ends_line:
            return find_line_end(text, start, end)
        line_feed = text.find("\n", start, end)
        if line_feed < 0:
            return end
        return line_feed - 1 if line_feed > start and text[line_feed - 1] == "\r" else line_feed

    def find_starter(self, text: str, start: int) -> int:
        """The offset of the first character from `start` on that can start a comment, a parameter or a quoted
        region, or -1 when the rest of the text is plain."""
        found = self._starters.search(text, start)
        return -1 if found is None else found.start()

    def reader_for(self, opener: str) -> QuoteReader | None:
        """The reader of the quoted region that `opener` opens, or None when it opens none."""
        return self._readers.get(opener)


_DOUBLED = DoubledQuoteReader()
_BACKSLASH = BackslashQuoteReader()
_ESCAPE_STRING = EscapeStringReader("Ee")
# CockroachDB's bytes literal, b'...', takes backslash escapes as an E string does.
_BYTES_ESCAPE_STRING = EscapeStringReader("Eeb")
_QUOTE_OPERATOR = QuoteOperatorReader()
_BRACKET = BracketReader(doubled_closer_escapes=False)
_ESCAPED_BRACKET = BracketReader(doubled_closer_escapes=True)
_DOLLAR = DollarQuoteReader()


def _create_mysql_family(dialect: Dialect) -> list[DialectRules]:
    # MySQL and MariaDB read "..." as a string and take backslash escapes in both kinds of quote.  ANSI_QUOTES makes
    # "..." an identifier, which has none; NO_BACKSLASH_ESCAPES takes them from both.  MySQL reads dollar quotes, and
    # MariaDB has its own hint.
    family = []
    for value in range(_OPTION_SETS):
        options = DialectOptions(value)
        no_backslash = bool(options & DialectOptions.NO_BACKSLASH_ESCAPES)
        ansi_quotes = bool(options & DialectOptions.ANSI_QUOTES)
        readers = {
            "'": _DOUBLED if no_backslash else _BACKSLASH,
            '"': _DOUBLED if no_backslash or ansi_quotes else _BACKSLASH,
            "`": _DOUBLED,
        }
        if dialect == Dialect.MYSQL:
            readers["$"] = _DOLLAR
        family.append(
            DialectRules(
                DialectChoice(dialect, options),
                readers,
                dash_needs_whitespace=True,
                hash_comments=True,
                mariadb_hints=dialect == Dialect.MARIADB,
                carriage_return_ends_line=False,
            )
        )
    return family


_BY_DIALECT_AND_OPTIONS: dict[tuple[Dialect, DialectOptions], DialectRules] = {
    (Dialect.ANSI, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.ANSI),
        {"'": _ESCAPE_STRING, '"': _DOUBLED, "`": _DOUBLED, "$": _DOLLAR},
        nested_comments=True,
    ),
    (Dialect.SQL_SERVER, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.SQL_SERVER),
        {"'": _DOUBLED, '"': _DOUBLED, "[": _ESCAPED_BRACKET},
        nested_comments=True,
        declared_variables=True,
        names_hold_at_and_hash=True,
    ),
    (Dialect.POSTGRESQL, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.POSTGRESQL),
        {"'": _ESCAPE_STRING, '"': _DOUBLED, "$": _DOLLAR},
        nested_comments=True,
        string_continuation=StringContinuation.ACROSS_LINE_COMMENTS,
    ),
    (Dialect.COCKROACHDB, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.COCKROACHDB),
        {"'": _BYTES_ESCAPE_STRING, '"': _DOUBLED, "$": _DOLLAR},
        nested_comments=True,
        string_continuation=StringContinuation.ACROSS_WHITESPACE,
        carriage_return_ends_line=False,
    ),
    (Dialect.SQLITE, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.SQLITE),
        {"'": _DOUBLED, '"': _DOUBLED, "`": _DOUBLED, "[": _BRACKET},
        carriage_return_ends_line=False,
    ),
    (Dialect.ORACLE, DialectOptions.NONE): DialectRules(
        DialectChoice(Dialect.ORACLE),
        {"'": _QUOTE_OPERATOR, '"': _DOUBLED},
        line_hints=True,
    ),
    **{(rules.dialect.dialect, rules.dialect.options): rules for rules in _create_mysql_family(Dialect.MYSQL)},
    **{(rules.dialect.dialect, rules.dialect.options): rules for rules in _create_mysql_family(Dialect.MARIADB)},
}
