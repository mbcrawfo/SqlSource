"""The markers of one scope that change a plan: the defaults of `-- token:` and the names of `-- token-ignore:`.

Mirrors the `-- token:` and `-- token-ignore:` parts of `Markers/SqlMarkerScope.cs` in `src/SqlSource/Parsing/`, with
its tables of where each marker is allowed.  The other markers change no plan, so their values are not read here, and
an error in one is not found.  It is a module of its own, apart from `markers.py`, as it builds a default's SQL with
`text_builder.py`, which reads markers.
"""

import enum

from sqlfluff_templater_sqlsource.errors import (
    CONFLICTING_SETTINGS,
    INVALID_MARKER_VALUE,
    MARKER_NOT_ALLOWED_HERE,
    RESERVED_TOKEN_NAME,
    ParseError,
)
from sqlfluff_templater_sqlsource.identifiers import is_reserved_keyword, is_valid
from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind, Lexer
from sqlfluff_templater_sqlsource.markers import Marker, MarkerKind, place_of
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.text import trim
from sqlfluff_templater_sqlsource.text_builder import build
from sqlfluff_templater_sqlsource.tokens import TokenDefault, read_token


class ScopeKind(enum.Enum):
    """What a scope of markers belongs to."""

    # The markers before a file's first -- name: marker.
    PREAMBLE = enum.auto()
    # The markers of one named query.
    QUERY = enum.auto()
    # The markers of a file without -- name: markers, which is one query and its own preamble.
    WHOLE_FILE = enum.auto()


_ALLOWED_IN_QUERY = frozenset(
    (
        MarkerKind.GENERATOR_PARAMETERS,
        MarkerKind.TOKEN,
        MarkerKind.TOKEN_IGNORE,
        MarkerKind.PARAM,
        MarkerKind.COLUMN,
        MarkerKind.DATABASE,
        MarkerKind.OUTPUT,
        MarkerKind.INPUT_MODEL_TYPE,
        MarkerKind.OUTPUT_MODEL_TYPE,
        MarkerKind.COLLECTION_TYPE,
        MarkerKind.INPUT_MODEL,
        MarkerKind.OUTPUT_MODEL,
        MarkerKind.TYPE_OVERRIDE,
        MarkerKind.LIBRARY_TYPES,
        MarkerKind.MEMBER_LOCATION,
        MarkerKind.VISIBILITY,
        MarkerKind.NESTED_CLASS,
    )
)

_ALLOWED_IN_PREAMBLE = frozenset(
    (
        MarkerKind.GENERATOR_PARAMETERS,
        MarkerKind.DATABASE,
        MarkerKind.OUTPUT,
        MarkerKind.INPUT_MODEL_SUFFIX,
        MarkerKind.OUTPUT_MODEL_SUFFIX,
        MarkerKind.MODEL_NAMESPACE,
        MarkerKind.INPUT_MODEL_TYPE,
        MarkerKind.OUTPUT_MODEL_TYPE,
        MarkerKind.COLLECTION_TYPE,
        MarkerKind.TYPE_OVERRIDE,
        MarkerKind.COLUMN,
        MarkerKind.LIBRARY_TYPES,
        MarkerKind.MEMBER_LOCATION,
        MarkerKind.VISIBILITY,
        MarkerKind.NESTED_CLASS,
    )
)


class MarkerScope:
    """The markers of one scope.  A marker that is not allowed, not valid or in conflict with an earlier one of the
    scope is reported in `errors` and not applied."""

    def __init__(self, text: str, rules: DialectRules, kind: ScopeKind, errors: list[ParseError]) -> None:
        self._text = text
        self._rules = rules
        self._kind = kind
        self._errors = errors
        # The defaults the scope's -- token: markers give, in marker order, each name once.
        self.token_defaults: list[TokenDefault] = []
        # The names the scope's -- token-ignore: markers give, as written.
        self.ignored_tokens: set[str] = set()

    def read(self, marker: Marker) -> None:
        """Reads one marker.  The scope takes it or reports it by what it is the scope of."""
        if not self._is_allowed(marker.kind):
            self._errors.append(ParseError(MARKER_NOT_ALLOWED_HERE, marker.start, marker.end))
        elif marker.kind == MarkerKind.TOKEN:
            self._read_token(marker)
        elif marker.kind == MarkerKind.TOKEN_IGNORE:
            self._read_token_ignore(marker)

    def _is_allowed(self, kind: MarkerKind) -> bool:
        if self._kind == ScopeKind.PREAMBLE:
            return kind in _ALLOWED_IN_PREAMBLE
        if self._kind == ScopeKind.QUERY:
            return kind in _ALLOWED_IN_QUERY
        return kind in _ALLOWED_IN_QUERY or kind in _ALLOWED_IN_PREAMBLE

    # The value is exactly one token with a default, as the SQL would write it.  The default is lexed alone, as the
    # parameter list reads it later: a quote or a comment that does not close inside it would swallow what follows.
    # Two defaults are compared without their comments, as a default in the SQL is read.
    def _read_token(self, marker: Marker) -> None:
        value = self._text[marker.value_start : marker.value_end]
        token, _ = read_token(value, 0)
        if token is None or token.end != len(value) or token.default_span is None:
            self._add_invalid(marker)
            return

        if is_reserved_keyword(token.name):
            self._errors.append(ParseError(RESERVED_TOKEN_NAME, marker.value_start, marker.value_end))
            return

        default_text = value[token.default_span[0] : token.default_span[1]]
        lexemes, error = Lexer(default_text, self._rules).read_to_end()
        if error is not None:
            self._add_invalid(marker)
            return

        default_value = _without_comments(default_text, lexemes, self._rules)
        for existing in self.token_defaults:
            if existing.name == token.name:
                if existing.value != default_value:
                    self._errors.append(ParseError(CONFLICTING_SETTINGS, *place_of(marker)))
                return

        self.token_defaults.append(TokenDefault(token.name, default_value, marker))

    # The value is one name, as a C# identifier is written.  A keyword is allowed: it is a token that is not one.
    def _read_token_ignore(self, marker: Marker) -> None:
        name = self._text[marker.value_start : marker.value_end]
        if not is_valid(name):
            self._add_invalid(marker)
            return
        self.ignored_tokens.add(name)

    def _add_invalid(self, marker: Marker) -> None:
        self._errors.append(ParseError(INVALID_MARKER_VALUE, *place_of(marker)))


# The default as one in the SQL is read: built without comments by the builder of a query's SQL, then trimmed as the
# token scanner trims it.  A default without a comment is its own text.
def _without_comments(default_text: str, lexemes: list[Lexeme], rules: DialectRules) -> str:
    if not any(lexeme.kind in (LexemeKind.LINE_COMMENT, LexemeKind.BLOCK_COMMENT) for lexeme in lexemes):
        return default_text
    built = build(default_text, rules, lexemes, 0, len(lexemes)).text
    start, end = trim(built, 0, len(built))
    return built[start:end]
