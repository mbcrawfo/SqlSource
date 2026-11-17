"""Recognises marker comments.

Mirrors `Markers/SqlMarker.cs`, `Markers/SqlMarkerKind.cs` and `Markers/SqlMarkerReader.cs` in
`src/SqlSource/Parsing/`, as far as the header of a file needs: the keywords, and reading a marker.
"""

import enum
from dataclasses import dataclass

from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind
from sqlfluff_templater_sqlsource.text import equals_ascii_ignoring_case, is_line_terminator, trim


class MarkerKind(enum.Enum):
    """Which marker a comment is, valued by the names the C# enum gives them."""

    NAME = "Name"
    SUMMARY = "Summary"
    GENERATOR_PARAMETERS = "GeneratorParameters"
    DIALECT = "Dialect"
    TOKEN = "Token"
    TOKEN_IGNORE = "TokenIgnore"
    PARAM = "Param"
    DATABASE = "Database"
    OUTPUT = "Output"
    INPUT_MODEL_SUFFIX = "InputModelSuffix"
    OUTPUT_MODEL_SUFFIX = "OutputModelSuffix"
    MODEL_NAMESPACE = "ModelNamespace"
    INPUT_MODEL_TYPE = "InputModelType"
    OUTPUT_MODEL_TYPE = "OutputModelType"
    INPUT_MODEL = "InputModel"
    OUTPUT_MODEL = "OutputModel"
    COLLECTION_TYPE = "CollectionType"
    TYPE_OVERRIDE = "TypeOverride"
    COLUMN = "Column"
    LIBRARY_TYPES = "LibraryTypes"
    MEMBER_LOCATION = "MemberLocation"
    VISIBILITY = "Visibility"
    NESTED_CLASS = "NestedClass"


# Each keyword with its colon, in the order they are tried.
_KEYWORDS: tuple[tuple[str, MarkerKind], ...] = (
    ("name:", MarkerKind.NAME),
    ("summary:", MarkerKind.SUMMARY),
    ("generator:", MarkerKind.GENERATOR_PARAMETERS),
    ("dialect:", MarkerKind.DIALECT),
    ("token:", MarkerKind.TOKEN),
    ("token-ignore:", MarkerKind.TOKEN_IGNORE),
    ("param:", MarkerKind.PARAM),
    ("database:", MarkerKind.DATABASE),
    ("output:", MarkerKind.OUTPUT),
    ("input-model-suffix:", MarkerKind.INPUT_MODEL_SUFFIX),
    ("output-model-suffix:", MarkerKind.OUTPUT_MODEL_SUFFIX),
    ("model-namespace:", MarkerKind.MODEL_NAMESPACE),
    ("input-model-type:", MarkerKind.INPUT_MODEL_TYPE),
    ("output-model-type:", MarkerKind.OUTPUT_MODEL_TYPE),
    ("input-model:", MarkerKind.INPUT_MODEL),
    ("output-model:", MarkerKind.OUTPUT_MODEL),
    ("collection-type:", MarkerKind.COLLECTION_TYPE),
    ("type-override:", MarkerKind.TYPE_OVERRIDE),
    ("column:", MarkerKind.COLUMN),
    ("library-types:", MarkerKind.LIBRARY_TYPES),
    ("member-location:", MarkerKind.MEMBER_LOCATION),
    ("visibility:", MarkerKind.VISIBILITY),
    ("nested-class:", MarkerKind.NESTED_CLASS),
)


@dataclass(frozen=True)
class Marker:
    """A marker comment: which it is, the whole comment, and the text after the colon without white space around it,
    which may be empty."""

    kind: MarkerKind
    start: int
    end: int
    value_start: int
    value_end: int


def read_marker(text: str, lexeme: Lexeme) -> Marker | None:
    """The marker that `lexeme` is, or None.

    A marker is a line comment that is the first thing on its line and reads `--`, optional blanks, a keyword, a colon.
    """
    if not _is_dash_comment(text, lexeme) or not _starts_line(text, lexeme.start):
        return None

    found = _find_keyword(text, lexeme.start, lexeme.end)
    if found is None:
        return None

    kind, value_start = found
    return Marker(kind, lexeme.start, lexeme.end, *trim(text, value_start, lexeme.end))


# A line comment can start with a # in some dialects.  A marker always starts with two dashes.
def _is_dash_comment(text: str, lexeme: Lexeme) -> bool:
    return lexeme.kind == LexemeKind.LINE_COMMENT and text[lexeme.start] == "-"


# The kind of the keyword after the comment's dashes and blanks, and where its value starts, or None.
def _find_keyword(text: str, start: int, end: int) -> tuple[MarkerKind, int] | None:
    keyword_start = start + 2
    while keyword_start < end and text[keyword_start] in (" ", "\t"):
        keyword_start += 1

    for keyword, kind in _KEYWORDS:
        value_start = keyword_start + len(keyword)
        if value_start <= end and equals_ascii_ignoring_case(text[keyword_start:value_start], keyword):
            return kind, value_start
    return None


# Only blanks may come before the comment on its line.  The lexer guarantees that a line break found this way is not
# inside a quoted region or a block comment: those end with their closing delimiter, never with a blank.
def _starts_line(text: str, start: int) -> bool:
    index = start - 1
    while index >= 0 and text[index] in (" ", "\t"):
        index -= 1
    return index < 0 or is_line_terminator(text[index])
