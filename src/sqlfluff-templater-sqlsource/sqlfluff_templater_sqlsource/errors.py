"""An error of a file: one that stops its templating plan, or one of the others that the port meets.

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

# A comment hides the closing braces of a token's default: SqlParseErrorKind.TokenCrossesComment.
TOKEN_CROSSES_COMMENT = "SQLSRC120"

# The errors below never stop a plan: a file with one has a best-effort plan.

# A -- name: marker names no usable C# name: SqlParseErrorKind.InvalidName.
INVALID_NAME = "SQLSRC103"

# Two queries of a file have one name: SqlParseErrorKind.DuplicateName.
DUPLICATE_NAME = "SQLSRC104"

# A file without -- name: markers has a name that is no usable C# name: SqlParseErrorKind.InvalidFileName.
INVALID_FILE_NAME = "SQLSRC105"

# SQL stands before a file's first -- name: marker: SqlParseErrorKind.SqlBeforeFirstName.
SQL_BEFORE_FIRST_NAME = "SQLSRC106"

# A -- summary: marker stands before a file's first -- name: marker: SqlParseErrorKind.SummaryBeforeFirstName.
SUMMARY_BEFORE_FIRST_NAME = "SQLSRC107"

# A marker comes after the last SQL of its block: SqlParseErrorKind.MarkerAtEndOfBlock.
MARKER_AT_END_OF_BLOCK = "SQLSRC108"

# Two settings for one thing differ, as two defaults of a token do: SqlParseErrorKind.ConflictingSettings.
CONFLICTING_SETTINGS = "SQLSRC112"

# A block holds no SQL: SqlParseErrorKind.EmptyBlock.
EMPTY_BLOCK = "SQLSRC113"

# A token is named by a reserved C# keyword: SqlParseErrorKind.ReservedTokenName.
RESERVED_TOKEN_NAME = "SQLSRC114"

# A -- dialect: marker stands after the file's header: SqlParseErrorKind.MisplacedDialect.
MISPLACED_DIALECT = "SQLSRC115"

# A marker stands where its scope does not allow it: SqlParseErrorKind.MarkerNotAllowedHere.
MARKER_NOT_ALLOWED_HERE = "SQLSRC116"


@dataclass(frozen=True)
class ParseError:
    """A problem found in a file: its diagnostic's code, and where it is, as offsets into the file's text."""

    code: str
    start: int
    end: int
