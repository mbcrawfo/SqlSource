"""The `-- dialect:` marker: putting a file's into effect before the text after it is lexed.

Mirrors `SqlDialectMarker.cs` in `src/SqlSource/Parsing/`, and the place `SqlFileParser.InvalidDialect` gives the
error of a `-- dialect:` marker whose value is not a dialect.

A file's dialect is set in its header: the comments that come before its first SQL and before its first `-- name:`
marker.  The header is read under the dialect the lexer starts with, and the marker applies from the line after it.
"""

from dataclasses import dataclass

from sqlfluff_templater_sqlsource.dialects import DialectChoice, parse_dialect_name
from sqlfluff_templater_sqlsource.errors import INVALID_MARKER_VALUE, ParseError
from sqlfluff_templater_sqlsource.lexer import Lexer
from sqlfluff_templater_sqlsource.markers import Marker, MarkerKind, place_of, read_marker
from sqlfluff_templater_sqlsource.rules import DialectRules


@dataclass(frozen=True)
class DialectHeader:
    """What the comments at the start of a file set.

    `end` is the offset where the header ends: a `-- dialect:` marker that starts at or after it is misplaced.
    `named` is the dialect that the marker put into effect names, or None when none did.  `invalid` is the error of
    the first `-- dialect:` marker of the header whose value is not a dialect, or None.
    """

    end: int
    named: DialectChoice | None
    invalid: ParseError | None


def apply_dialect_marker(lexer: Lexer, text: str) -> int:
    """Reads the comments at the start of `text` from `lexer`, switches it to the dialect that the first valid
    `-- dialect:` marker among them names, and returns the offset where the header ends."""
    return read_dialect_header(lexer, text).end


def read_dialect_header(lexer: Lexer, text: str) -> DialectHeader:
    """As `apply_dialect_marker`, and gives the dialect named and the first invalid marker too."""
    named: DialectChoice | None = None
    invalid: ParseError | None = None
    while (comment := lexer.try_read_leading_comment()) is not None:
        marker = read_marker(text, comment, lexer.rules)
        if marker is None:
            continue

        if marker.kind == MarkerKind.NAME:
            return DialectHeader(marker.start, named, invalid)

        if marker.kind != MarkerKind.DIALECT:
            continue

        dialect = try_read(text, marker)
        if dialect is None:
            if invalid is None:
                invalid = invalid_dialect(marker)
        elif named is None:
            lexer.rules = DialectRules.for_choice(dialect)
            named = dialect

    return DialectHeader(lexer.position, named, invalid)


def try_read(text: str, marker: Marker) -> DialectChoice | None:
    """The dialect that a `-- dialect:` marker's value names, or None for a value that is empty or is not a dialect
    with its options."""
    return parse_dialect_name(text[marker.value_start : marker.value_end])


def invalid_dialect(marker: Marker) -> ParseError:
    """The error of a `-- dialect:` marker whose value is not a dialect: at the value, or at the marker when it has
    none."""
    return ParseError(INVALID_MARKER_VALUE, *place_of(marker))
