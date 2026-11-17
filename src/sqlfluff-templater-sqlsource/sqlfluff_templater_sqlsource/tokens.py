"""Finds the `{{name}}` and `{{name:default}}` tokens of a block's SQL, and the default each token's query gives it.

Mirrors `Tokens/TokenScanner.cs`, `Tokens/SqlTokenList.cs`, `Tokens/OpenDefaultFinder.cs`, `Tokens/SqlToken.cs`,
`Tokens/SqlTokenDefault.cs` and `Tokens/SqlTokenOccurrence.cs` in `src/SqlSource/Parsing/`, without the segments the
generator splits the SQL into.
"""

import sys
from collections.abc import Collection
from dataclasses import dataclass

from sqlfluff_templater_sqlsource.errors import CONFLICTING_SETTINGS, RESERVED_TOKEN_NAME, ParseError
from sqlfluff_templater_sqlsource.identifiers import (
    is_identifier_part_character,
    is_identifier_start_character,
    is_reserved_keyword,
    is_valid,
)
from sqlfluff_templater_sqlsource.markers import Marker
from sqlfluff_templater_sqlsource.text import char_at, trim
from sqlfluff_templater_sqlsource.text_builder import BlockText

# No offset of a text reaches it: what the search for "}}" starts from before it has failed.
_NO_LIMIT = sys.maxsize


@dataclass(frozen=True)
class TokenOccurrence:
    """One place a token is written in a block's SQL: its name, the default written there, trimmed, which is empty
    for `{{name:}}` and None for `{{name}}`, and the whole token, braces included, as offsets into the SQL."""

    name: str
    default: str | None
    start: int
    end: int


@dataclass(frozen=True)
class Token:
    """One token of a query, with the default its query gives it: inline or from a `-- token:` marker, or None."""

    name: str
    default: str | None


@dataclass(frozen=True)
class TokenDefault:
    """A default that a `-- token:` marker gives: the token's name, the default without its comments, trimmed, as an
    inline default is read, and the marker.  The default as written and where it starts, which only the parameters of
    a query are read from, are left out."""

    name: str
    value: str
    marker: Marker


@dataclass(frozen=True)
class ReadToken:
    """A token read at an offset: its name, where its default is without the white space around it, None for a token
    with no colon, and the offset just after its closing braces."""

    name: str
    default_span: tuple[int, int] | None
    end: int


def scan(sql: str, ignored_names: Collection[str]) -> tuple[list[TokenOccurrence], list[ParseError]]:
    """Each token of `sql` in order, and the errors found, as offsets into `sql`.  A token whose name is in
    `ignored_names` stays literal text, and one named by a reserved keyword is an error."""
    occurrences: list[TokenOccurrence] = []
    errors: list[ParseError] = []
    index = 0
    # From this offset on there is no "}}": a search for one found none.  A later opener with a default need not
    # search again, so a block of many unclosed openers costs one search and not one for each.
    no_close_from = _NO_LIMIT
    while index < len(sql):
        token, no_close_from = read_token(sql, index, no_close_from)
        if token is None:
            index += 1
            continue

        if token.name in ignored_names:
            pass
        elif is_reserved_keyword(token.name):
            errors.append(ParseError(RESERVED_TOKEN_NAME, index, token.end))
        else:
            default = None if token.default_span is None else sql[token.default_span[0] : token.default_span[1]]
            occurrences.append(TokenOccurrence(token.name, default, index, token.end))
        index = token.end

    return occurrences, errors


def read_token(sql: str, start: int, no_close_from: int = _NO_LIMIT) -> tuple[ReadToken | None, int]:
    """Reads the token that starts at `start`: `{{`, blanks, an identifier, blanks, then `}}`, or a colon and a default
    that runs to the first `}}`.  Anything else that starts with `{{` is literal text, and the token is None.

    `no_close_from` is what the caller has learned from earlier calls on the same SQL: no `}}` starts at or after that
    offset.  It is given back, lowered when a search finds none.
    """
    opener = read_opener(sql, start)
    if opener is None:
        return None, no_close_from

    name_start, name_end, after = opener
    has_default = char_at(sql, after) == ":"
    if not has_default and (char_at(sql, after) != "}" or char_at(sql, after + 1) != "}"):
        return None, no_close_from

    # The name is checked before the search for the closing braces, so that text such as "{{1a:" costs none.
    name = sql[name_start:name_end]
    if not is_valid(name):
        return None, no_close_from

    close = after
    if has_default:
        close = -1 if after + 1 >= no_close_from else sql.find("}}", after + 1)
        if close < 0:
            return None, min(no_close_from, after + 1)

    default_span = trim(sql, after + 1, close) if close > after else None
    return ReadToken(name, default_span, close + 2), no_close_from


def read_opener(sql: str, start: int) -> tuple[int, int, int] | None:
    """Reads what every token starts with: `{{`, blanks, the characters a name can hold, and blanks.

    Gives where the name starts and ends, which may be empty or not a valid name, and where the token goes on, with a
    colon or `}}`; None when `start` does not hold `{{`.
    """
    if char_at(sql, start) != "{" or char_at(sql, start + 1) != "{":
        return None

    name_start = _skip_blanks(sql, start + 2)
    name_end = name_start
    while is_identifier_part_character(char_at(sql, name_end)):
        name_end += 1
    return name_start, name_end, _skip_blanks(sql, name_end)


def _skip_blanks(sql: str, index: int) -> int:
    while char_at(sql, index) in (" ", "\t"):
        index += 1
    return index


def create_token_list(
    text: str,
    sql: BlockText,
    occurrences: list[TokenOccurrence],
    marker_defaults: list[TokenDefault],
    errors: list[ParseError],
) -> list[Token]:
    """The tokens of one query: each name once, in order of first appearance, with the default its query gives.

    Two defaults for one token that differ are a conflict, reported at the one that comes second in the file.  A
    marker for a token the query does not hold is not an error.
    """
    tokens: list[Token] = []
    for occurrence in occurrences:
        index = _index_of(tokens, occurrence.name)
        if index < 0:
            tokens.append(Token(occurrence.name, occurrence.default))
        elif occurrence.default is not None:
            first = tokens[index].default
            if first is None:
                tokens[index] = Token(occurrence.name, occurrence.default)
            elif first != occurrence.default:
                errors.append(_conflict(sql, occurrence))

    for marker in marker_defaults:
        index = _index_of(tokens, marker.name)
        if index < 0:
            continue

        written = tokens[index].default
        if written is None:
            tokens[index] = Token(marker.name, marker.value)
        elif written != marker.value:
            inline = next(found for found in occurrences if found.default is not None and found.name == marker.name)
            if marker.marker.start > sql.to_source_span(inline.start, inline.end)[0]:
                errors.append(ParseError(CONFLICTING_SETTINGS, marker.marker.value_start, marker.marker.value_end))
            else:
                errors.append(_conflict(sql, inline))

    return tokens


def _index_of(tokens: list[Token], name: str) -> int:
    return next((index for index, token in enumerate(tokens) if token.name == name), -1)


def _conflict(sql: BlockText, occurrence: TokenOccurrence) -> ParseError:
    return ParseError(CONFLICTING_SETTINGS, *sql.to_source_span(occurrence.start, occurrence.end))


class OpenDefaultFinder:
    """Finds the token whose default is open at each of a series of offsets of one SQL text, reading each character
    once over the series.

    A default is open at an offset when the first opener of a default after the last `}}` that ends at or before the
    offset, `{{`, blanks, a valid name, blanks and a colon, starts before the offset.  An ignored name opens a default
    too: `scan` reads its token to the next `}}` as it does any other.
    """

    def __init__(self, sql: str) -> None:
        self._sql = sql
        # Every character before this offset has been read, but the last: a "}}" that starts there ends after the
        # offset of the last call, and may end at or before the next.
        self._position = 0
        # The name of the first opener of a default after the last "}}" read, or None when there is none.
        self._open: str | None = None
        # The offset of the last call.
        self._offset = 0
        # Where the opener of the default that the last call found starts, or -1 when it found none.  Only the form
        # that keeps comments reads it, which a plan never builds; it mirrors the C# so that the finder's tests, which
        # check it at each offset, hold here too.
        self.opener_start = -1

    def at(self, offset: int) -> str | None:
        """The name of the token whose default is open at `offset`, or None.  The offsets of successive calls must
        not decrease."""
        if offset < self._offset:
            raise ValueError(f"The offsets must not decrease: {offset} follows {self._offset}.")

        self._offset = offset
        sql = self._sql
        for index in range(self._position, offset):
            if index + 2 <= offset and sql[index] == "}" and sql[index + 1] == "}":
                self._open = None
                self.opener_start = -1
            elif self._open is None and (name := self._read_opener(index)) is not None:
                self._open = name
                self.opener_start = index

        self._position = offset - 1 if offset > self._position else self._position
        return self._open

    # The name of the opener of a default that starts at the index, or None.
    def _read_opener(self, index: int) -> str | None:
        opener = read_opener(self._sql, index)
        if opener is None or char_at(self._sql, opener[2]) != ":":
            return None

        name_start, name_end, _ = opener
        # A name that cannot start an identifier is not valid, and is rejected before a string is made for it.
        if name_start == name_end or not is_identifier_start_character(self._sql[name_start]):
            return None

        name = self._sql[name_start:name_end]
        return name if is_valid(name) else None
