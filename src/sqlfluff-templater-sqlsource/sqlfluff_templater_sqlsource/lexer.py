"""Splits SQL text into quoted regions, comments, hints, parameters and plain text, and reads a file's dialect marker.

Mirrors `SqlLexer.cs`, `SqlLexeme.cs`, `SqlLexemeKind.cs` and `SqlDialectMarker.cs` in `src/SqlSource/Parsing/`,
with `SqlParameterName.Of`, the part of `Markers/SqlMarkerReader.cs` that finds a `-- name:` or `-- dialect:` marker,
and the place `SqlFileParser.InvalidDialect` gives the error of a `-- dialect:` marker whose value is not a dialect.

The lexer reads by the `DialectRules` it holds and names no dialect and no quoting form.  Where the rules leave a
choice it keeps text, because a comment left in the SQL is harmless and SQL removed from it is a bug.  The one thing
it carries from a lexeme to the next is a string that may go on after a gap: each part of it is a quoted region of
its own, and the gap is ordinary text and comments.
"""

import enum
from dataclasses import dataclass

from sqlfluff_templater_sqlsource.dialects import DialectChoice, parse_dialect_name
from sqlfluff_templater_sqlsource.quoting import NOT_A_QUOTE, UNTERMINATED, QuoteReader
from sqlfluff_templater_sqlsource.rules import DialectRules, StringContinuation
from sqlfluff_templater_sqlsource.text import (
    char_at,
    equals_ascii_ignoring_case,
    find_line_end,
    is_control,
    is_line_terminator,
    is_white_space,
    trim,
)

UNTERMINATED_QUOTE = "SQLSRC101"
UNTERMINATED_BLOCK_COMMENT = "SQLSRC102"
INVALID_MARKER_VALUE = "SQLSRC111"

# The keywords of the two markers that the header reads.
_NAME = "name:"
_DIALECT = "dialect:"


class LexemeKind(enum.Enum):
    """The kinds of lexeme, valued by the names the C# enum gives them."""

    # Anything that is not one of the other kinds.
    TEXT = "Text"
    # A string literal or a quoted identifier, delimiters included.
    QUOTED = "Quoted"
    # -- to the end of the line, without the line terminator; # too where the dialect has it.
    LINE_COMMENT = "LineComment"
    # /* ... */, with any comments nested in it where the dialect nests them.
    BLOCK_COMMENT = "BlockComment"
    # A block comment that starts /*+ or /*!, and where the dialect has them /*M! or a line that starts --+.
    HINT = "Hint"
    # A parameter: the dialect's prefix and a name, as in @id.
    PARAMETER = "Parameter"


@dataclass(frozen=True)
class Lexeme:
    """A run of a file's text with one meaning.  The lexemes of a file cover it without gaps."""

    kind: LexemeKind
    start: int
    end: int

    @property
    def parameter_name(self) -> tuple[int, int]:
        """The span of a parameter lexeme's name: the lexeme less its one-character prefix."""
        return self.start + 1, self.end

    def content_span(self, text: str) -> tuple[int, int] | None:
        """The part of this lexeme that counts as SQL content, or None when it has none.  Comments and white space
        are not content."""
        if self.kind in (LexemeKind.QUOTED, LexemeKind.HINT, LexemeKind.PARAMETER):
            return self.start, self.end
        if self.kind != LexemeKind.TEXT:
            return None
        start, end = trim(text, self.start, self.end)
        return None if start == end else (start, end)


@dataclass(frozen=True)
class LexError:
    """An error that stops the templating plan: its diagnostic's code and where it is."""

    code: str
    start: int
    end: int


class Lexer:
    """Reads a text by the rules it holds, which may be changed between two lexemes."""

    def __init__(self, text: str, rules: DialectRules) -> None:
        self._text = text
        # The rules for the text that has not been read yet.  A file's -- dialect: marker changes them.
        self.rules = rules
        # The offset after the last lexeme that was read.
        self.position = 0
        self._lexemes: list[Lexeme] = []
        self._error: LexError | None = None
        self._text_start = 0
        # A string that may go on after a gap: the reader of its next part, or None when no string is waiting for
        # one; the quote that opens that part; how far the gap has been checked; and whether it has held a line break.
        self._continuation: QuoteReader | None = None
        self._continuation_quote = ""
        self._gap_start = 0
        self._gap_has_line_break = False

    def try_read_leading_comment(self) -> Lexeme | None:
        """Reads the next comment, if only white space comes before it.

        Returns None, and reads nothing, when the next thing in the text is anything else, a hint included, and at
        the end of the text.
        """
        text = self._text
        index = self.position
        while index < len(text) and is_white_space(text[index]):
            index += 1

        if self._error is not None or index >= len(text) or not self._is_comment(index):
            return None

        self.position = index
        self._step()
        return None if self._error is not None else self._lexemes[-1]

    def read_to_end(self) -> tuple[list[Lexeme], LexError | None]:
        """Reads the rest of the text and gives every lexeme, or no lexeme and the error that stopped the lexer."""
        text = self._text
        while self.position < len(text) and self._error is None:
            starter = self.rules.find_starter(text, self.position)
            self.position = len(text) if starter < 0 else starter
            if starter >= 0:
                self._step()

        if self._error is not None:
            return [], self._error

        self._add_pending_text(len(text))
        return list(self._lexemes), None

    def _step(self) -> None:
        text = self._text
        rules = self.rules
        start = self.position
        current = text[start]
        if self._continuation is not None:
            self._read_gap(start)

        if current == "-" and self._is_dash_comment(start):
            is_hint = self._is_line_hint(start)
            kind = LexemeKind.HINT if is_hint else LexemeKind.LINE_COMMENT
            self._add(kind, start, find_line_end(text, start, len(text)))
            self._pass_gap(not is_hint and rules.string_continuation == StringContinuation.ACROSS_LINE_COMMENTS)
        elif current == "#" and rules.hash_comments:
            self._add(LexemeKind.LINE_COMMENT, start, find_line_end(text, start, len(text)))
            self._pass_gap(allowed=False)
        elif current == "/" and char_at(text, start + 1) == "*":
            self._read_block_comment(start)
            self._pass_gap(allowed=False)
        elif current == rules.parameter_prefix and (parameter_end := self._find_parameter_end(start)) is not None:
            self._add(LexemeKind.PARAMETER, start, parameter_end)
            self._pass_gap(allowed=False)
        elif (reader := rules.reader_for(current)) is not None:
            self._read_quoted(reader, start)
        else:
            self.position += 1
            self._pass_gap(allowed=False)

    # A parameter is the prefix and a name, which the rules read.  The prefix follows neither another prefix nor a
    # character of a name, and no name starts with the prefix: "@@rowcount" is a function, and the "@" of "user@host"
    # stands inside a word.  None when there is no parameter here.
    def _find_parameter_end(self, start: int) -> int | None:
        text = self._text
        rules = self.rules
        if start > 0 and (
            text[start - 1] == rules.parameter_prefix or rules.is_parameter_name_character(text[start - 1])
        ):
            return None
        end = rules.find_parameter_name_end(text, start + 1, len(text))
        return end if end > start + 1 else None

    # Checks the text of the gap up to end, where something other than plain text starts.  Anything but white space
    # there ends the string that was waiting for a part.
    def _read_gap(self, end: int) -> None:
        text = self._text
        for index in range(self._gap_start, end):
            value = text[index]
            if not is_white_space(value):
                self._continuation = None
                return
            self._gap_has_line_break |= is_line_terminator(value)
        self._gap_start = end

    # What was just read stood after a string.  The string can still go on only if its gap may hold that.
    def _pass_gap(self, allowed: bool) -> None:
        if allowed:
            self._gap_start = self.position
        else:
            self._continuation = None

    # A comment that is stripped: not a hint, which is kept and can hold SQL.
    def _is_comment(self, index: int) -> bool:
        value = self._text[index]
        if value == "-":
            return self._is_dash_comment(index) and not self._is_line_hint(index)
        if value == "#":
            return self.rules.hash_comments
        if value == "/":
            return char_at(self._text, index + 1) == "*" and not self._is_block_hint(index)
        return False

    def _is_dash_comment(self, index: int) -> bool:
        return char_at(self._text, index + 1) == "-" and (
            not self.rules.dash_needs_whitespace or _is_dash_boundary(char_at(self._text, index + 2))
        )

    def _is_line_hint(self, index: int) -> bool:
        return self.rules.line_hints and char_at(self._text, index + 2) == "+"

    def _is_block_hint(self, index: int) -> bool:
        text = self._text
        return char_at(text, index + 2) in ("+", "!") or (
            self.rules.mariadb_hints and char_at(text, index + 2) == "M" and char_at(text, index + 3) == "!"
        )

    def _add(self, kind: LexemeKind, start: int, end: int) -> None:
        self._add_pending_text(start)
        self._lexemes.append(Lexeme(kind, start, end))
        self._text_start = end
        self.position = end

    def _add_pending_text(self, end: int) -> None:
        if end > self._text_start:
            self._lexemes.append(Lexeme(LexemeKind.TEXT, self._text_start, end))

    def _read_block_comment(self, start: int) -> None:
        text = self._text
        nested = self.rules.nested_comments
        depth = 1
        index = start + 2
        while index < len(text) and depth > 0:
            if nested and text[index] == "/" and char_at(text, index + 1) == "*":
                depth += 1
                index += 2
            elif text[index] == "*" and char_at(text, index + 1) == "/":
                depth -= 1
                index += 2
            else:
                index += 1

        if depth > 0:
            self._error = LexError(UNTERMINATED_BLOCK_COMMENT, start, start + 2)
            return

        self._add(LexemeKind.HINT if self._is_block_hint(start) else LexemeKind.BLOCK_COMMENT, start, index)

    def _read_quoted(self, reader: QuoteReader, start: int) -> None:
        text = self._text
        # A part that continues a string is read as the string was, whatever stands before its own quote.
        pending = self._continuation if self._gap_has_line_break and text[start] == self._continuation_quote else None
        end = (reader if pending is None else pending).find_end(text, start)
        if end == NOT_A_QUOTE:
            self.position += 1
            self._continuation = None
        elif end == UNTERMINATED:
            self._error = LexError(UNTERMINATED_QUOTE, start, start + 1)
        else:
            self._add(LexemeKind.QUOTED, start, end)
            if pending is None:
                self._continuation = (
                    None
                    if self.rules.string_continuation == StringContinuation.NONE
                    else reader.find_continuation(text, start)
                )
                self._continuation_quote = text[start]
            self._gap_start = end
            self._gap_has_line_break = False


# What MySQL wants after the two dashes.  The end of the text is '\0' here, which is a control character.
def _is_dash_boundary(value: str) -> bool:
    return is_white_space(value) or is_control(value)


@dataclass(frozen=True)
class DialectHeader:
    """What the comments at the start of a file set.

    `end` is the offset where the header ends: a `-- dialect:` marker that starts at or after it is misplaced.
    `named` is the dialect that the marker put into effect names, or None when none did.  `invalid` is the error of
    the first `-- dialect:` marker of the header whose value is not a dialect, placed at its value, or at the marker
    when it has none, as the parser places it; or None.
    """

    end: int
    named: DialectChoice | None
    invalid: LexError | None


def apply_dialect_marker(lexer: Lexer, text: str) -> int:
    """Reads the comments at the start of `text` from `lexer`, switches it to the dialect that the first valid
    `-- dialect:` marker among them names, and returns the offset where the header ends."""
    return read_dialect_header(lexer, text).end


def read_dialect_header(lexer: Lexer, text: str) -> DialectHeader:
    """As `apply_dialect_marker`, and gives the dialect named and the first invalid marker too.

    The header is read under the dialect the lexer starts with, and the marker applies from the line after it.
    """
    named: DialectChoice | None = None
    invalid: LexError | None = None
    while (comment := lexer.try_read_leading_comment()) is not None:
        marker = _read_header_marker(text, comment)
        if marker is None:
            continue

        keyword, value_start, value_end = marker
        if keyword == _NAME:
            return DialectHeader(comment.start, named, invalid)

        choice = parse_dialect_name(text[value_start:value_end])
        if choice is None:
            if invalid is None:
                start, end = (value_start, value_end) if value_end > value_start else (comment.start, comment.end)
                invalid = LexError(INVALID_MARKER_VALUE, start, end)
        elif named is None:
            lexer.rules = DialectRules.for_choice(choice)
            named = choice

    return DialectHeader(lexer.position, named, invalid)


# The keyword of a -- name: or -- dialect: marker that `comment` is, and the span of its value, or None.  A marker is
# a line comment that is the first thing on its line and reads --, optional blanks, a keyword, a colon.  A marker of
# another keyword does nothing in the header, so it is read as no marker here.
def _read_header_marker(text: str, comment: Lexeme) -> tuple[str, int, int] | None:
    if comment.kind != LexemeKind.LINE_COMMENT or text[comment.start] != "-" or not _starts_line(text, comment.start):
        return None

    keyword_start = comment.start + 2
    while keyword_start < comment.end and text[keyword_start] in (" ", "\t"):
        keyword_start += 1

    for keyword in (_NAME, _DIALECT):
        value_start = keyword_start + len(keyword)
        if value_start <= comment.end and equals_ascii_ignoring_case(text[keyword_start:value_start], keyword):
            return (keyword, *trim(text, value_start, comment.end))
    return None


# Only blanks may come before the comment on its line.
def _starts_line(text: str, start: int) -> bool:
    index = start - 1
    while index >= 0 and text[index] in (" ", "\t"):
        index -= 1
    return index < 0 or is_line_terminator(text[index])
