"""Finds the dialect a `.sql` file starts with, from the `sqlsource.json` files above it.

Mirrors the reading of `root` and `dialect` in `SettingsFiles/SettingsFileReader.cs`, the file name of
`SettingsFiles/SettingsFile.cs`, and the chain rule of `Generation/Chains/SettingsFileSet.cs` and `SettingsChain.cs`,
all in `src/SqlSource/`.  The grammar is that of `SidecarTokenizer` in `Snapshot/Reading/` in its lenient mode: JSON
with `//` and `/* */` comments and one trailing comma, nested at most `MAX_DEPTH` levels.  Python's `json` module is
not used, as it takes neither.

Nothing here raises.  `read_settings` gives an unknown `root` and no dialect for a text that is not one JSON object.
`resolve_dialect` treats a settings file it cannot read as absent, and goes on to the folder above.  A file is
decoded as `FileText.Read` in `src/SqlSource.Tool/Planning/` does: by its byte order mark, else as UTF-8.
"""

import os
from dataclasses import dataclass
from pathlib import Path

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, parse_dialect_name

FILE_NAME = "sqlsource.json"

# SidecarTokenizer.MaxDepth.
MAX_DEPTH = 64

_ANSI = DialectChoice(Dialect.ANSI)

_WHITE_SPACE = " \t\n\r"
_ESCAPES = {'"': '"', "\\": "\\", "/": "/", "b": "\b", "f": "\f", "n": "\n", "r": "\r", "t": "\t"}
_HEX = frozenset("0123456789abcdefABCDEF")


@dataclass(frozen=True)
class SettingsDialect:
    """What one settings file says of the dialect: whether it is the root of its chain, None when the text is not
    one JSON object, and the dialect it sets, None when it sets none."""

    root: bool | None
    dialect: DialectChoice | None


class _Invalid(Exception):
    """The text breaks the grammar."""


class _Other:
    """The value of `null`, a number, an array or an object, which no setting this module reads accepts."""


_OTHER = _Other()


class _Scanner:
    def __init__(self, text: str) -> None:
        self.text = text
        self.position = 0

    def parse_object(self) -> list[tuple[str, object]]:
        """The members of the top-level object, in order and with any key twice."""
        self._skip_space()
        if self._peek() != "{":
            raise _Invalid
        self.position += 1
        return self._members(1)

    def finish(self) -> None:
        self._skip_space()
        if self.position < len(self.text):
            raise _Invalid

    def _peek(self) -> str:
        return self.text[self.position] if self.position < len(self.text) else ""

    def _skip_space(self) -> None:
        text = self.text
        while self.position < len(text):
            character = text[self.position]
            if character in _WHITE_SPACE:
                self.position += 1
            elif character == "/":
                self._skip_comment()
            else:
                return

    def _skip_comment(self) -> None:
        text = self.text
        following = text[self.position + 1 : self.position + 2]
        if following == "/":
            self.position += 2
            while self.position < len(text) and text[self.position] not in "\n\r":
                self.position += 1
        elif following == "*":
            end = text.find("*/", self.position + 2)
            if end < 0:
                raise _Invalid
            self.position = end + 2
        else:
            raise _Invalid

    def _expect(self, character: str) -> None:
        self._skip_space()
        if self._peek() != character:
            raise _Invalid
        self.position += 1

    def _members(self, depth: int) -> list[tuple[str, object]]:
        members: list[tuple[str, object]] = []
        self._skip_space()
        if self._peek() == "}":
            self.position += 1
            return members
        while True:
            self._skip_space()
            key = self._string()
            self._expect(":")
            members.append((key, self._value(depth)))
            self._skip_space()
            character = self._peek()
            self.position += 1
            if character == "}":
                return members
            if character != ",":
                raise _Invalid
            self._skip_space()
            if self._peek() == "}":
                self.position += 1
                return members

    def _elements(self, depth: int) -> None:
        self._skip_space()
        if self._peek() == "]":
            self.position += 1
            return
        while True:
            self._value(depth)
            self._skip_space()
            character = self._peek()
            self.position += 1
            if character == "]":
                return
            if character != ",":
                raise _Invalid
            self._skip_space()
            if self._peek() == "]":
                self.position += 1
                return

    def _value(self, depth: int) -> object:
        self._skip_space()
        character = self._peek()
        if character == '"':
            return self._string()
        if character in ("{", "[") and depth < MAX_DEPTH:
            self.position += 1
            if character == "{":
                self._members(depth + 1)
            else:
                self._elements(depth + 1)
            return _OTHER
        if character == "t":
            return self._literal("true", True)
        if character == "f":
            return self._literal("false", False)
        if character == "n":
            return self._literal("null", _OTHER)
        if character == "-" or ("0" <= character <= "9"):
            return self._number()
        raise _Invalid

    def _literal(self, word: str, value: object) -> object:
        if not self.text.startswith(word, self.position):
            raise _Invalid
        self.position += len(word)
        return value

    def _digits(self, index: int) -> int:
        text = self.text
        while index < len(text) and "0" <= text[index] <= "9":
            index += 1
        return index

    def _number(self) -> object:
        text = self.text
        first = self.position + 1 if text[self.position] == "-" else self.position
        index = self._digits(first)
        if index == first or (text[first] == "0" and index > first + 1):
            raise _Invalid
        if text[index : index + 1] == ".":
            end = self._digits(index + 1)
            if end == index + 1:
                raise _Invalid
            index = end
        if text[index : index + 1] in ("e", "E"):
            digits = index + 2 if text[index + 1 : index + 2] in ("+", "-") else index + 1
            end = self._digits(digits)
            if end == digits:
                raise _Invalid
            index = end
        self.position = index
        return _OTHER

    def _string(self) -> str:
        text = self.text
        if self._peek() != '"':
            raise _Invalid
        index = self.position + 1
        parts: list[str] = []
        while index < len(text):
            character = text[index]
            if character == '"':
                self.position = index + 1
                return "".join(parts)
            if character < " ":
                raise _Invalid
            if character != "\\":
                parts.append(character)
                index += 1
                continue
            escape = text[index + 1 : index + 2]
            if escape in _ESCAPES:
                parts.append(_ESCAPES[escape])
                index += 2
            elif escape == "u" and len(text) >= index + 6 and all(c in _HEX for c in text[index + 2 : index + 6]):
                parts.append(chr(int(text[index + 2 : index + 6], 16)))
                index += 6
            else:
                raise _Invalid
        raise _Invalid


def read_settings(text: str) -> SettingsDialect:
    """Reads the `root` and the `dialect` of the text of a settings file.  Never raises.

    The first of a key written twice is the one read, a key of the wrong JSON type sets nothing, and a `dialect` that
    is blank or not valid sets none.  A text that is not one JSON object has no known `root`.
    """
    scanner = _Scanner(text)
    try:
        members = scanner.parse_object()
        scanner.finish()
    except _Invalid:
        return SettingsDialect(None, None)

    root = False
    dialect: DialectChoice | None = None
    seen: set[str] = set()
    for key, value in members:
        if key in seen:
            continue
        seen.add(key)
        if key == "root" and isinstance(value, bool):
            root = value
        elif key == "dialect" and isinstance(value, str):
            dialect = parse_dialect_name(value)
    return SettingsDialect(root, dialect)


def dialect_of_chain(texts: list[str]) -> DialectChoice:
    """The dialect of the nearest file of the chain that sets one, ANSI when none does.  `texts` are the settings
    files from the nearest, and the chain ends after a file whose `root` is true."""
    for text in texts:
        settings = read_settings(text)
        if settings.dialect is not None:
            return settings.dialect
        if settings.root is True:
            break
    return _ANSI


def _decode(data: bytes) -> str:
    # Roslyn's SourceText.From(bytes): a byte order mark picks UTF-8, UTF-16 or UTF-32, and without one the bytes are
    # UTF-8.  The mark is not part of the text, and a byte that is not valid is a replacement character.
    if data.startswith(b"\xff\xfe\x00\x00"):
        return data[4:].decode("utf-32-le", errors="replace")
    if data.startswith(b"\x00\x00\xfe\xff"):
        return data[4:].decode("utf-32-be", errors="replace")
    if data.startswith(b"\xff\xfe"):
        return data[2:].decode("utf-16-le", errors="replace")
    if data.startswith(b"\xfe\xff"):
        return data[2:].decode("utf-16-be", errors="replace")
    return data.removeprefix(b"\xef\xbb\xbf").decode("utf-8", errors="replace")


def _read_file(folder: str) -> str | None:
    path = os.path.join(folder, FILE_NAME)
    try:
        if not os.path.isfile(path):
            return None
        # The name is matched exactly, which a file system that ignores case would not do for the open.
        if FILE_NAME not in os.listdir(folder):
            return None
        with open(path, "rb") as file:
            return _decode(file.read())
    except (OSError, ValueError):
        return None


def resolve_dialect(sql_path: Path) -> DialectChoice:
    """The dialect `sql_path` starts with: the settings files of its folder and of each folder above it, up to the
    root of the file system.  A file that cannot be read, a folder that cannot be listed or a name that is a folder
    counts as no settings file there.  The `-- dialect:` marker of the file is applied over this by `build_plan`."""
    texts = []
    try:
        folder = os.path.dirname(os.path.abspath(sql_path))
    except (OSError, ValueError):
        return _ANSI
    while True:
        text = _read_file(folder)
        if text is not None:
            texts.append(text)
        parent = os.path.dirname(folder)
        if parent == folder:
            break
        folder = parent
    return dialect_of_chain(texts)
