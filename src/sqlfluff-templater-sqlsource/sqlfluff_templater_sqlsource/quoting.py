"""The readers that find where each kind of quoted region ends.

Mirrors `QuoteReader.cs`, `DoubledQuoteReader.cs`, `BackslashQuoteReader.cs`, `EscapeStringReader.cs`,
`QuoteOperatorReader.cs`, `BracketReader.cs` and `DollarQuoteReader.cs` in `src/SqlSource/Parsing/Quoting/`.

A quoted region is a string or a quoted identifier: the lexer copies it as written and looks for no comment inside
it.  A reader holds no state, so one instance serves every file.  It is called at the character that opens the
region, and may look at the characters before it for a prefix such as `E`.
"""

from sqlfluff_templater_sqlsource.text import (
    LAST_SINGLE_UNIT,
    category,
    is_letter,
    is_letter_or_digit,
    is_line_terminator,
)

# The region is opened and never closed.
UNTERMINATED = -1

# The character opens no region here, and is plain text.
NOT_A_QUOTE = -2

# The zero-width space, U+200B, which SQL Server reads as a space, as Unicode 3.2 classed it.
ZERO_WIDTH_SPACE = "\u200b"

# The categories above U+007F that SQL Server takes in a name besides letters and digits.
_SQL_SERVER_NAME_CATEGORIES = frozenset(("Mn", "Mc", "Nl", "Cf", "Pc"))


def is_identifier_character(value: str) -> bool:
    """True for a character that can be part of an unquoted identifier."""
    return is_letter_or_digit(value) or value in ("_", "$")


def is_name_character_of_postgresql(value: str) -> bool:
    """True for a character of a name as PostgreSQL and MySQL read it: any above U+007F is read as a letter."""
    return is_identifier_character(value) or value > "\x7f"


def is_name_character_of_sql_server(value: str) -> bool:
    """True for a character of a name as SQL Server reads one after its first character.

    That is a letter or a digit of any script, a combining mark, a letter number, a format character but the
    zero-width space, a connector, `_`, `@`, `#` or `$`.  A code point outside the BMP is none of these, as each of
    its two UTF-16 units is a surrogate to C#.
    """
    return (
        is_letter_or_digit(value)
        or value in ("_", "@", "#", "$")
        or (value > "\x7f" and value != ZERO_WIDTH_SPACE and category(value) in _SQL_SERVER_NAME_CATEGORIES)
    )


class QuoteReader:
    """Finds where one kind of quoted region ends."""

    def find_end(self, text: str, start: int) -> int:
        """The offset after the closing delimiter of the region that opens at `start`, `UNTERMINATED` or
        `NOT_A_QUOTE`."""
        raise NotImplementedError

    def find_continuation(self, text: str, start: int) -> "QuoteReader | None":
        """The reader of a part that continues the region that opens at `start`, or None when a later part would be
        read as the region itself."""
        return None


def find_doubled_end(text: str, start: int) -> int:
    """The end of a region that closes with the character it opens with, where that character doubled stands for
    itself."""
    quote = text[start]
    index = start + 1
    while True:
        close = text.find(quote, index)
        if close < 0:
            return UNTERMINATED
        if close + 1 < len(text) and text[close + 1] == quote:
            index = close + 2
        else:
            return close + 1


def find_backslash_end(text: str, start: int) -> int:
    """As `find_doubled_end`, and a backslash takes the character after it with it, whatever that is."""
    quote = text[start]
    index = start + 1
    while index < len(text):
        current = text[index]
        if current == "\\":
            index += 2
        elif current != quote:
            index += 1
        elif index + 1 < len(text) and text[index + 1] == quote:
            index += 2
        else:
            return index + 1
    return UNTERMINATED


class DoubledQuoteReader(QuoteReader):
    """The ANSI form: `'it''s'`, `"a""b"`, and `` `a``b` `` where a backtick is a quote."""

    def find_end(self, text: str, start: int) -> int:
        return find_doubled_end(text, start)


class BackslashQuoteReader(QuoteReader):
    """The MySQL and MariaDB form of a string: `'it\\'s'` and `"a\\"b"`.  A doubled quote still stands for one."""

    def find_end(self, text: str, start: int) -> int:
        return find_backslash_end(text, start)


class EscapeStringReader(QuoteReader):
    """A `'...'` string that takes backslash escapes only after a prefix, as in PostgreSQL's `E'it\\'s'`.

    The prefix stays in the text before the region.  As in PostgreSQL, a letter after a character above U+007F is
    part of a name, not a prefix.  `prefixes` holds each prefix in the case it is accepted in: `Ee` for PostgreSQL.
    """

    # A part that continues an escape string takes backslash escapes with no prefix of its own.
    _CONTINUED = BackslashQuoteReader()

    def __init__(self, prefixes: str) -> None:
        self._prefixes = prefixes

    def find_end(self, text: str, start: int) -> int:
        return find_backslash_end(text, start) if self._has_prefix(text, start) else find_doubled_end(text, start)

    def find_continuation(self, text: str, start: int) -> QuoteReader | None:
        return self._CONTINUED if self._has_prefix(text, start) else None

    # A prefix is one character that does not end an identifier: the E of typeE'a' is not one.
    def _has_prefix(self, text: str, quote: int) -> bool:
        return (
            quote > 0
            and text[quote - 1] in self._prefixes
            and (quote < 2 or not is_name_character_of_postgresql(text[quote - 2]))
        )


# The closing delimiter of each opening delimiter of the quote operator that differs from it.
_QUOTE_OPERATOR_CLOSERS = {"[": "]", "{": "}", "<": ">", "(": ")"}


class QuoteOperatorReader(QuoteReader):
    """A `'...'` string that is read by Oracle's quote operator after a `q` prefix, as in `q'[it's]'`.

    The character after the quote is the opening delimiter.  The region ends at the first closing delimiter that is
    directly followed by a quote: delimiters are not counted for depth, so `q'[a[b]c]'` is one string.
    """

    def find_end(self, text: str, start: int) -> int:
        open_ = start + 1
        if (
            not self._has_prefix(text, start)
            or open_ >= len(text)
            or text[open_] in (" ", "\t")
            or is_line_terminator(text[open_])
        ):
            return find_doubled_end(text, start)

        # To C# a delimiter outside the BMP is its high surrogate, and what follows each match is a low surrogate,
        # never a quote: the region is never closed.
        if text[open_] > LAST_SINGLE_UNIT:
            return UNTERMINATED

        close = _QUOTE_OPERATOR_CLOSERS.get(text[open_], text[open_])
        index = text.find(close, open_ + 1)
        while index >= 0:
            if index + 1 < len(text) and text[index + 1] == "'":
                return index + 2
            index = text.find(close, index + 1)
        return UNTERMINATED

    # q or Q, or one of those after n or N, where the prefix does not end an identifier.
    @staticmethod
    def _has_prefix(text: str, quote: int) -> bool:
        if quote == 0 or text[quote - 1] not in ("q", "Q"):
            return False
        before = quote - 2
        if before >= 0 and text[before] in ("n", "N"):
            before -= 1
        return before < 0 or not is_identifier_character(text[before])


class BracketReader(QuoteReader):
    """A bracketed identifier, `[order details]`.

    `doubled_closer_escapes` is true for SQL Server, where `]]` stands for one `]`, and false for SQLite, where the
    first `]` ends the identifier.
    """

    def __init__(self, doubled_closer_escapes: bool) -> None:
        self._doubled_closer_escapes = doubled_closer_escapes

    def find_end(self, text: str, start: int) -> int:
        index = start + 1
        while True:
            close = text.find("]", index)
            if close < 0:
                return UNTERMINATED
            if self._doubled_closer_escapes and close + 1 < len(text) and text[close + 1] == "]":
                index = close + 2
            else:
                return close + 1


class DollarQuoteReader(QuoteReader):
    """PostgreSQL's dollar quote, `$tag$ ... $tag$`, where the tag may be empty.

    A dollar sign has other meanings: a parameter such as `$1`, a money literal, a character of an identifier.  So
    one that follows an identifier character, or that opens a quote which is never closed, is plain text.  As in
    PostgreSQL, a character above U+007F counts as an identifier character, and may be part of a tag.
    """

    def find_end(self, text: str, start: int) -> int:
        if start > 0 and is_name_character_of_postgresql(text[start - 1]):
            return NOT_A_QUOTE

        tag_end = start + 1
        if tag_end < len(text) and (is_letter(text[tag_end]) or text[tag_end] > "\x7f" or text[tag_end] == "_"):
            while tag_end < len(text) and (
                is_letter_or_digit(text[tag_end]) or text[tag_end] > "\x7f" or text[tag_end] == "_"
            ):
                tag_end += 1

        if tag_end >= len(text) or text[tag_end] != "$":
            return NOT_A_QUOTE

        delimiter = text[start : tag_end + 1]
        body = tag_end + 1
        close = text.find(delimiter, body)
        return NOT_A_QUOTE if close < 0 else close + len(delimiter)
