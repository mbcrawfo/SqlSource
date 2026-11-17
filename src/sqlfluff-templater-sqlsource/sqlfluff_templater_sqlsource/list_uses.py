"""Which parameters of a query are lists, as in `id IN @ids`.

Mirrors the decision of `SqlListUses.Find` in `src/SqlSource/Parsing/SqlListUses.cs` between a list, `List` or
`NegatedList`, and any other use, which is all a plan needs.  A use is a list when the word before it is `IN`, unless
that `IN` stands directly inside the parentheses of a call to `position`, as in `position(a IN b)`, which puts a single
value after it.  Words are runs of the dialect's name characters, so `JOIN` and T-SQL's `x#IN` are not `IN`, and a
quoted region is never a word.  A comment or a hint between two words is passed over as white space.

What `NOT`, `IS`, `AND`, `OR` and commas do decides only whether a list is negated, so it is not read here.  Nor is
`SqlListUses`'s run of operator characters, which, like any character that is neither a name character nor a
parenthesis, only stands between `IN` and what follows it.
"""

import enum
import re

from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.text import equals_ascii_ignoring_case

# The word IN, as an ordinal search ignoring case finds it: only ASCII letters fold.
_IN = re.compile("in", re.IGNORECASE | re.ASCII)


class _Item(enum.Enum):
    """What the cursor over a query's lexemes meets next."""

    END = enum.auto()
    WORD = enum.auto()
    OPEN = enum.auto()
    CLOSE = enum.auto()
    PARAMETER = enum.auto()
    # A quoted region, or any other character that is not white space.
    OTHER = enum.auto()


def find_lists(text: str, lexemes: list[Lexeme], start: int, end: int, rules: DialectRules) -> list[bool]:
    """Whether each parameter lexeme from `start` up to `end`, in order, is used as a list, or an empty list when none
    is.  A query whose text does not hold the word `IN`, in any case, is not walked."""
    if start >= end or not _holds_the_word_in(text, lexemes[start].start, lexemes[end - 1].end, rules):
        return []

    lists: list[bool] = []
    # Whether each open parenthesis is that of a call to position, innermost last.
    open_parentheses: list[bool] = []
    after_in = False
    after_position = False
    cursor = _Cursor(text, lexemes, start, end, rules)
    while (item := cursor.next()) != _Item.END:
        if item == _Item.WORD:
            after_in = cursor.is_word("IN")
            after_position = cursor.is_word("position")
            continue

        if item == _Item.OPEN:
            open_parentheses.append(after_position)
        elif item == _Item.CLOSE and open_parentheses:
            open_parentheses.pop()
        elif item == _Item.PARAMETER:
            lists.append(after_in and not (open_parentheses and open_parentheses[-1]))

        after_in = False
        after_position = False

    return lists if any(lists) else []


# Whether the text from one offset up to another holds IN as a whole word, so that JOIN, INSERT and INTO, which most
# queries hold, cost no walk.
def _holds_the_word_in(text: str, start: int, end: int, rules: DialectRules) -> bool:
    for found in _IN.finditer(text, start, end):
        before, after = found.start(), found.end()
        if (before == start or not rules.is_name_character(text[before - 1])) and (
            after == end or not rules.is_name_character(text[after])
        ):
            return True
    return False


class _Cursor:
    """Reads a query's lexemes one item at a time."""

    def __init__(self, text: str, lexemes: list[Lexeme], start: int, end: int, rules: DialectRules) -> None:
        self._text = text
        self._lexemes = lexemes
        self._end = end
        self._rules = rules
        self._lexeme = start
        self._offset = lexemes[start].start
        self._word = (0, 0)

    def is_word(self, word: str) -> bool:
        """Whether the word the cursor last met is the given one, ignoring case."""
        return equals_ascii_ignoring_case(self._text[self._word[0] : self._word[1]], word)

    def next(self) -> _Item:
        text = self._text
        rules = self._rules
        while self._lexeme < self._end:
            lexeme = self._lexemes[self._lexeme]
            if lexeme.kind != LexemeKind.TEXT:
                self._move_to_next_lexeme()
                if lexeme.kind == LexemeKind.PARAMETER:
                    return _Item.PARAMETER
                if lexeme.kind == LexemeKind.QUOTED:
                    self._word = (0, 0)
                    return _Item.OTHER
                # A comment or a hint is passed over as white space.
                continue

            while self._offset < lexeme.end and rules.is_space(text[self._offset]):
                self._offset += 1
            if self._offset == lexeme.end:
                self._move_to_next_lexeme()
                continue

            # A word never runs on into the next lexeme: one that follows a parameter, a quoted region or a comment
            # starts a word of its own.
            word_start = self._offset
            while self._offset < lexeme.end and rules.is_name_character(text[self._offset]):
                self._offset += 1
            if self._offset > word_start:
                self._word = (word_start, self._offset)
                return _Item.WORD

            value = text[self._offset]
            self._word = (self._offset, self._offset + 1)
            self._offset += 1
            if value == "(":
                return _Item.OPEN
            return _Item.CLOSE if value == ")" else _Item.OTHER

        return _Item.END

    def _move_to_next_lexeme(self) -> None:
        self._lexeme += 1
        if self._lexeme < self._end:
            self._offset = self._lexemes[self._lexeme].start
