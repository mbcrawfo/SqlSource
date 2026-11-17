"""An error of a file that stops its templating plan.

Mirrors `SqlParseError.cs` in `src/SqlSource/Parsing/`, with the kind given as the code of the diagnostic that
`SqlDiagnostics.ForParseError` reports for it, and without the arguments of its message.
"""

from dataclasses import dataclass

# A quoted region is not closed: SqlParseErrorKind.UnterminatedQuote.
UNTERMINATED_QUOTE = "SQLSRC101"

# A block comment or hint is not closed: SqlParseErrorKind.UnterminatedBlockComment.
UNTERMINATED_BLOCK_COMMENT = "SQLSRC102"

# A marker's value is not valid, as the value of a -- dialect: marker that is not a dialect:
# SqlParseErrorKind.InvalidMarkerValue.
INVALID_MARKER_VALUE = "SQLSRC111"


@dataclass(frozen=True)
class ParseError:
    """A problem found in a file: its diagnostic's code, and where it is, as offsets into the file's text."""

    code: str
    start: int
    end: int
