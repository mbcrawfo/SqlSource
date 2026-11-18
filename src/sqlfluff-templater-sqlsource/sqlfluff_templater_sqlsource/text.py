"""The small readings of a text that the lexer and the readers share, and the tests of `char` they rest on.

Mirrors `SqlText.cs` in `src/SqlSource/Parsing/`, and the methods of .NET's `System.Char` that the C# parser calls.

The C# parser tests UTF-16 code units, and a `str` here holds code points.  A code point outside the Basic
Multilingual Plane is two units to C#, each a surrogate: of the category `Surrogate`, above U+007F, and not a letter,
a digit, white space or a control character.  Every test here gives what C# gives for a surrogate when it is given
such a code point, whatever the code point's own category, since both units of it test alike.  The corpus of
`TemplatingCorpusTests` cannot tell the two readings apart, as none of its cases puts such a code point where a name,
a prefix or a delimiter is read, so `tests/test_lexer.py` holds what the C# parser gives there.

The categories come from the running Python's `unicodedata`.  Python 3.14 and .NET 10 both have the tables of Unicode
16.0, and agree on every unit of the BMP; an older Python has older tables, and can class a newer character apart.
"""

import re
import unicodedata

# The highest code point of the Basic Multilingual Plane, the last that is one UTF-16 unit.
LAST_SINGLE_UNIT = "\uffff"

# The characters up to U+00FF that char.IsWhiteSpace takes: their categories do not say it for U+0009 to U+000D and
# U+0085, which are control characters.
_LATIN1_WHITE_SPACE = frozenset("\t\n\v\f\r \x85\xa0")

_LETTER_CATEGORIES = frozenset(("Lu", "Ll", "Lt", "Lm", "Lo"))

_LETTER_OR_DIGIT_CATEGORIES = _LETTER_CATEGORIES | {"Nd"}

_SEPARATOR_CATEGORIES = frozenset(("Zs", "Zl", "Zp"))

# What ends a line of SQL.
_LINE_TERMINATOR = re.compile(r"[\r\n]")


def category(value: str) -> str:
    """The category that `char.GetUnicodeCategory` gives, `Cs` for a code point outside the BMP."""
    return unicodedata.category(value) if value <= LAST_SINGLE_UNIT else "Cs"


def is_white_space(value: str) -> bool:
    """As `char.IsWhiteSpace`."""
    if value <= "\xff":
        return value in _LATIN1_WHITE_SPACE
    return category(value) in _SEPARATOR_CATEGORIES


def is_letter(value: str) -> bool:
    """As `char.IsLetter`."""
    return category(value) in _LETTER_CATEGORIES


def is_letter_or_digit(value: str) -> bool:
    """As `char.IsLetterOrDigit`."""
    return category(value) in _LETTER_OR_DIGIT_CATEGORIES


def is_control(value: str) -> bool:
    """As `char.IsControl`."""
    return category(value) == "Cc"


def equals_ascii_ignoring_case(value: str, ascii_word: str) -> bool:
    """Whether `value` is `ascii_word` as `StringComparison.OrdinalIgnoreCase` compares them.

    That comparison folds no character above U+007F to an ASCII one: U+0131 is not `i`, U+017F is not `s`, and only
    ASCII letters fold.
    """
    return len(value) == len(ascii_word) and value.isascii() and value.lower() == ascii_word.lower()


def is_line_terminator(value: str) -> bool:
    """Whether `value` ends a line of SQL: `\\r` or `\\n`."""
    return value in ("\r", "\n")


def trim(text: str, start: int, end: int) -> tuple[int, int]:
    """The span from `start` up to `end` without the white space at either side; empty, at `end`, when that is all."""
    while start < end and is_white_space(text[start]):
        start += 1
    while end > start and is_white_space(text[end - 1]):
        end -= 1
    return start, end


def find_line_end(text: str, start: int, end: int) -> int:
    """The offset of the first line terminator from `start` up to `end`, or `end` when there is none."""
    terminator = _LINE_TERMINATOR.search(text, start, end)
    return end if terminator is None else terminator.start()


def char_at(text: str, index: int) -> str:
    """The character at `index`, or `\\0` at the end of the text and past it."""
    return text[index] if index < len(text) else "\0"
