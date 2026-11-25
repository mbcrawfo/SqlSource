"""Builds the SQL of one block from its lexemes, without its comments, and maps it back to the file.

Mirrors `SqlTextBuilder.cs` and `SqlBlockText.cs` in `src/SqlSource/Parsing/`, without the form that keeps comments,
which a plan never reads.

The SQL is the file's text with its comments and marker lines taken out, trailing blanks and blank lines removed, and
every line terminator a `\\n`.  Quoted regions and hints are copied whole, as they can hold SQL that must not change.
"""

import bisect
from dataclasses import dataclass

from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind
from sqlfluff_templater_sqlsource.markers import read_marker
from sqlfluff_templater_sqlsource.rules import DialectRules


@dataclass(frozen=True)
class BlockText:
    """The SQL of one block after comment handling and white-space clean-up.

    `runs` says where the SQL came from: each entry says that the text from offset `output` onward was copied from
    the file starting at offset `source`, up to the next entry.  The entries are ordered by `output`, and their
    `source` offsets increase with it.  `parameters` holds where each parameter of the SQL is in `text`, prefix
    included, in order.
    """

    text: str
    runs: tuple[tuple[int, int], ...]
    parameters: tuple[tuple[int, int], ...]

    def to_source_span(self, start: int, end: int) -> tuple[int, int]:
        """Converts a non-empty span of `text` into the span of the file's text that it came from.  The two can
        differ in length: a stripped block comment is one space here."""
        return self._to_source(start), self._to_source(end - 1) + 1

    def to_output(self, source_offset: int) -> int:
        """Converts an offset of the file's text into the offset of `text` where the text copied from before it ends.
        Text that is not copied, such as a stripped comment, maps to where the copied text before it ends."""
        run = self._find_run(source_offset, by_source=True)
        if run < 0:
            return 0

        # A run ends where the next begins, or where the text does, less the line breaks written between two lines,
        # which belong to no run.  A run's text holds no line break but as a run of its own, of one character.
        output, source = self.runs[run]
        end = min(
            self.runs[run + 1][0] if run + 1 < len(self.runs) else len(self.text),
            output + (source_offset - source),
        )
        while end - 1 > output and self.text[end - 1] == "\n":
            end -= 1
        return end

    # The last run that starts at or before the offset is the one that holds it.
    def _to_source(self, offset: int) -> int:
        output, source = self.runs[max(self._find_run(offset, by_source=False), 0)]
        return source + (offset - output)

    # The index of the last run that starts at or before the offset, in the source or in the output, or -1.
    def _find_run(self, offset: int, by_source: bool) -> int:
        key = 1 if by_source else 0
        return bisect.bisect_right(self.runs, offset, key=lambda run: run[key]) - 1


def build(text: str, rules: DialectRules, lexemes: list[Lexeme], start: int, end: int) -> BlockText:
    """Builds the SQL of the lexemes from `start` up to `end`, without its comments.  `rules` read them: they say
    what ends a line."""
    if start >= end:
        return BlockText("", (), ())

    writer = _Writer(text, rules, lexemes[end - 1].end - lexemes[start].start)
    for index in range(start, end):
        writer.append(lexemes[index])
    return writer.finish()


class _Writer:
    """The buffer holds the finished output, then the line breaks that would join the next line to it, then the line
    being written.  A line is judged when it ends: kept with its trailing blanks trimmed, or dropped by moving back to
    the end of the finished output."""

    def __init__(self, source: str, rules: DialectRules, size: int) -> None:
        self._source = source
        self._rules = rules
        # The output is never longer than its source: each character written stands for a different character read.
        self._buffer = [""] * size
        self._runs: list[tuple[int, int]] = []
        self._parameters: list[tuple[int, int]] = []
        # The length of the finished output: whole lines, with no line break after the last one.
        self._finished = 0
        # Where the line being written starts and where its content ends.  Blanks after the content are trimmed if
        # the line ends there.  A line whose content is empty is blank.
        self._line_start = 0
        self._content_end = 0
        self._position = 0
        self._is_marker_line = False

    def append(self, lexeme: Lexeme) -> None:
        if lexeme.kind in (LexemeKind.QUOTED, LexemeKind.HINT):
            self._append_protected(lexeme)
        elif read_marker(self._source, lexeme, self._rules) is not None:
            self._is_marker_line = True
        elif lexeme.kind == LexemeKind.PARAMETER:
            # A parameter is on a line with content and never on a marker's line, so the line is kept and the offset
            # stands.
            self._parameters.append((self._position, self._position + lexeme.end - lexeme.start))
            self._append_lines(lexeme)
        elif lexeme.kind == LexemeKind.TEXT:
            self._append_lines(lexeme)
        elif lexeme.kind == LexemeKind.BLOCK_COMMENT:
            # A stripped block comment leaves one space, so that the text on either side of it stays apart.  A
            # stripped line comment leaves nothing.
            self._write(" ", lexeme.start)

    def finish(self) -> BlockText:
        self._end_line()
        return BlockText("".join(self._buffer[: self._finished]), tuple(self._runs), tuple(self._parameters))

    # Text: a line terminator ends the output line.  A carriage return that ends no line in the dialect is copied as
    # written, so that a kept comment never ends early.
    def _append_lines(self, lexeme: Lexeme) -> None:
        index = lexeme.start
        while index < lexeme.end:
            line_end = self._rules.find_line_end(self._source, index, lexeme.end)
            self._copy(index, line_end)
            self._extend_content(index, line_end)
            if line_end < lexeme.end:
                self._end_line()
                self._begin_line()
            index = line_end + self._terminator_length(line_end, lexeme.end)

    # Quoted regions and hints: a line terminator becomes \n and stays inside the output line, and every character
    # counts as content, so that the clean-up of trailing blanks and blank lines never reaches into them.
    def _append_protected(self, lexeme: Lexeme) -> None:
        index = lexeme.start
        while index < lexeme.end:
            line_end = self._rules.find_line_end(self._source, index, lexeme.end)
            self._copy(index, line_end)
            if line_end < lexeme.end:
                self._write("\n", line_end)
            index = line_end + self._terminator_length(line_end, lexeme.end)
        self._content_end = self._position

    def _terminator_length(self, index: int, end: int) -> int:
        if index == end:
            return 0
        source = self._source
        return 2 if source[index] == "\r" and index + 1 < end and source[index + 1] == "\n" else 1

    def _copy(self, start: int, end: int) -> None:
        if end > start:
            self._runs.append((self._position, start))
            self._buffer[self._position : self._position + end - start] = self._source[start:end]
            self._position += end - start

    def _write(self, value: str, source_offset: int) -> None:
        self._runs.append((self._position, source_offset))
        self._buffer[self._position] = value
        self._position += 1

    # Moves the end of the line's content to just after the last character of the copied text that is not a blank.
    def _extend_content(self, start: int, end: int) -> None:
        last = end - 1
        while last >= start and self._source[last] in (" ", "\t"):
            last -= 1
        if last >= start:
            self._content_end = self._position - (end - 1 - last)

    # Keeps the line being written, without its trailing blanks, or drops it.
    def _end_line(self) -> None:
        if not self._is_marker_line and self._content_end > self._line_start:
            self._remove_runs_from(self._content_end)
            self._finished = self._content_end
            return
        self._remove_runs_from(self._line_start)

    # Starts a line after the finished output, joined to it by a line break.  If the line is dropped, the next one
    # starts from the same place.  The kept form alone writes blank lines between two lines.
    def _begin_line(self) -> None:
        self._position = self._finished
        if self._finished > 0:
            self._buffer[self._position] = "\n"
            self._position += 1
        self._line_start = self._position
        self._content_end = self._position
        self._is_marker_line = False

    def _remove_runs_from(self, output: int) -> None:
        while self._runs and self._runs[-1][0] >= output:
            self._runs.pop()
