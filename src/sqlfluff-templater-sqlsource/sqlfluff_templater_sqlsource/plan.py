"""The templating plan of a file: the spans that sqlfluff must not see as written, or the error that stops it.

Mirrors `SqlFileParser.PlanTemplating` in `src/SqlSource/Parsing/SqlFileParser.cs`, with the parts of its `Parser`
that a plan rests on: the split into blocks, `ReadBlock`, `BuildQuery`, `FindHiddenTokenEnds`, `FindLastContent` and
`Earliest`.  Mirrors `Templating/SqlTemplatingPlan.cs`, `Templating/SqlTemplatingSpan.cs`,
`Templating/SqlTemplatingSpanKind.cs` and `Templating/SqlTemplatingSpans.cs` too.

The parser's other checks are not ported: a file with an error that does not stop its plan has a best-effort plan,
and `has_other_errors` says so for the errors the port meets on its way.
"""

import enum
from dataclasses import dataclass

from sqlfluff_templater_sqlsource.dialect_marker import invalid_dialect, read_dialect_header, try_read
from sqlfluff_templater_sqlsource.dialects import DialectChoice
from sqlfluff_templater_sqlsource.errors import (
    CONFLICTING_SETTINGS,
    DUPLICATE_NAME,
    EMPTY_BLOCK,
    INVALID_FILE_NAME,
    INVALID_NAME,
    MARKER_AT_END_OF_BLOCK,
    MISPLACED_DIALECT,
    SQL_BEFORE_FIRST_NAME,
    SUMMARY_BEFORE_FIRST_NAME,
    TOKEN_CROSSES_COMMENT,
    ParseError,
)
from sqlfluff_templater_sqlsource.identifiers import is_usable_name
from sqlfluff_templater_sqlsource.lexer import Lexeme, LexemeKind, Lexer
from sqlfluff_templater_sqlsource.list_uses import find_lists
from sqlfluff_templater_sqlsource.marker_scope import MarkerScope, ScopeKind
from sqlfluff_templater_sqlsource.markers import Marker, MarkerKind, has_marker_form, place_of, read_marker, split_name
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.text_builder import BlockText, build
from sqlfluff_templater_sqlsource.tokens import OpenDefaultFinder, Token, TokenOccurrence, create_token_list, scan


class SpanKind(enum.Enum):
    """What a span of a plan is, valued as the corpus names it.  The order of the members is the order of two spans
    that start at one place."""

    # A token, {{name}} or {{name:default}}, in the SQL outside comments.
    TOKEN = "token"
    # A parameter, or a variable the query declares, that is not a list.
    PARAMETER = "parameter"
    # A parameter used as a list, as in id IN @ids or id NOT IN @ids.
    LIST = "list"
    # A line comment in the form of a marker, wherever it stands.
    MARKER_COMMENT = "markerComment"
    # The end of a query that is not the file's last and whose SQL does not end in ;: an empty span right after its
    # last SQL.
    QUERY_END = "queryEnd"


_KIND_ORDER = {kind: order for order, kind in enumerate(SpanKind)}


@dataclass(frozen=True)
class PlanSpan:
    """One span of a file that the templater replaces or protects, as offsets into the file's text, empty for a
    query's end.  `name` is the token's or the parameter's name without its prefix, and `default` the default the
    token's query resolves for it, trimmed; either is None where there is none."""

    kind: SpanKind
    start: int
    end: int
    name: str | None
    default: str | None


@dataclass(frozen=True)
class Plan:
    """What the templater must replace in one file, read as the parser reads it.

    `dialect` is the one it was given, or the one its marker names.  `lexemes` are empty when the lexer stopped.
    `spans` are sorted by start, an empty span before any other at its start, and then by kind; a parameter inside a
    token's default comes after the token and lies inside its span, and a marker comment inside a token is not
    listed.  They are empty when `error` is set: the first error by position that stops the plan, the lexer's, that of
    an invalid `-- dialect:` marker, or SQLSRC120.  `has_other_errors` says that the file has another error, and the
    spans are then the parser's best effort.
    """

    dialect: DialectChoice
    lexemes: list[Lexeme]
    spans: list[PlanSpan]
    error: ParseError | None
    has_other_errors: bool


def build_plan(text: str, file_name: str, dialect: DialectChoice) -> Plan:
    """Reads `text` as SqlSource does and gives what the sqlfluff templater must replace in it, or the error that
    stops it.  `file_name` is the file's name with its extension and without a directory; `dialect` is the one the
    file's settings give, which a `-- dialect:` marker in its header replaces."""
    lexer = Lexer(text, DialectRules.for_choice(dialect))
    header = read_dialect_header(lexer, text)
    lexemes, lex_error = lexer.read_to_end()
    read = dialect if header.named is None else header.named
    if lex_error is not None:
        # The parser has not run, so the other error can only be the one of the two not chosen.
        return Plan(read, lexemes, [], earliest(lex_error, header.invalid), header.invalid is not None)

    # Tokens, parameters and the ends of queries are found by the parser, block by block.
    spans = [
        PlanSpan(SpanKind.MARKER_COMMENT, lexeme.start, lexeme.end, None, None)
        for lexeme in lexemes
        if has_marker_form(text, lexeme)
    ]
    errors = _Parser(text, file_name, lexemes, header.end, lexer.rules, spans).run()

    # Of the file's errors, SQLSRC120 comes from FindHiddenTokenEnds alone.
    crossing = min(
        (found for found in errors if found.code == TOKEN_CROSSES_COMMENT), key=lambda found: found.start, default=None
    )
    error = earliest(header.invalid, crossing)
    return Plan(
        read,
        lexemes,
        _order(spans) if error is None else [],
        error,
        any(found != error for found in errors),
    )


def earliest(first: ParseError | None, second: ParseError | None) -> ParseError | None:
    """The error that starts first, `first` when they start together, or None when neither is set."""
    if first is None or (second is not None and second.start < first.start):
        return second
    return first


# The spans in the order of the plan: by start, an empty span before others at its start, then by kind.  A marker
# comment inside a token is left out, since the token's span protects it.
def _order(spans: list[PlanSpan]) -> list[PlanSpan]:
    ordered = sorted(spans, key=lambda span: (span.start, span.end > span.start, _KIND_ORDER[span.kind]))
    kept = []
    # Every token that starts at or before a marker comment comes before it, so the comment lies in one of them
    # exactly when it ends no later than the furthest end of the tokens seen.
    tokens_end = -1
    for span in ordered:
        if span.kind == SpanKind.TOKEN:
            tokens_end = max(tokens_end, span.end)
        elif span.kind == SpanKind.MARKER_COMMENT and span.end <= tokens_end:
            continue
        kept.append(span)
    return kept


class _Parser:
    """Splits a file into its blocks and reads each as `SqlFileParser`'s parser does, adding the spans of its tokens,
    its parameters and its end, and gives the errors it meets."""

    def __init__(
        self,
        text: str,
        file_name: str,
        lexemes: list[Lexeme],
        header_end: int,
        rules: DialectRules,
        spans: list[PlanSpan],
    ) -> None:
        self._text = text
        self._file_name = file_name
        self._lexemes = lexemes
        self._header_end = header_end
        self._rules = rules
        self._spans = spans
        self._errors: list[ParseError] = []
        self._names: set[str] = set()
        # The dialect that a marker of the file has named so far, to find a second marker that names another.
        self._dialect: DialectChoice | None = None

    def run(self) -> list[ParseError]:
        name_markers = [
            (index, marker)
            for index, lexeme in enumerate(self._lexemes)
            if (marker := read_marker(self._text, lexeme, self._rules)) is not None and marker.kind == MarkerKind.NAME
        ]
        if name_markers:
            self._read_named_blocks(name_markers)
        else:
            self._read_unnamed_file()
        return self._errors

    def _read_unnamed_file(self) -> None:
        last_dot = self._file_name.rfind(".")
        name = self._file_name if last_dot < 0 else self._file_name[:last_dot]
        if not is_usable_name(name):
            self._errors.append(ParseError(INVALID_FILE_NAME, 0, 0))
        self._read_block((0, 0), ScopeKind.WHOLE_FILE, 0, len(self._lexemes))

    # The arrow and shape after a name are not read: an error in them changes no plan.
    def _read_named_blocks(self, name_markers: list[tuple[int, Marker]]) -> None:
        self._read_preamble(name_markers[0][0])
        for position, (index, marker) in enumerate(name_markers):
            end = name_markers[position + 1][0] if position + 1 < len(name_markers) else len(self._lexemes)
            written, _ = split_name(self._text, marker.value_start, marker.value_end)
            name = self._text[written[0] : written[1]]
            name_span = (marker.start, marker.end) if name == "" else written
            if not is_usable_name(name):
                self._errors.append(ParseError(INVALID_NAME, *name_span))
            elif name in self._names:
                self._errors.append(ParseError(DUPLICATE_NAME, *name_span))
            else:
                self._names.add(name)

            # A marker with an unusable name still starts a block, so that what follows is checked as a block.
            self._read_block(name_span, ScopeKind.QUERY, index + 1, end)

    def _read_preamble(self, end: int) -> None:
        scope = MarkerScope(self._text, self._rules, ScopeKind.PREAMBLE, self._errors)
        sql_reported = False
        for index in range(end):
            lexeme = self._lexemes[index]
            marker = read_marker(self._text, lexeme, self._rules)
            if marker is not None:
                if marker.kind == MarkerKind.DIALECT:
                    self._read_dialect(marker)
                elif marker.kind == MarkerKind.SUMMARY:
                    self._errors.append(ParseError(SUMMARY_BEFORE_FIRST_NAME, marker.start, marker.end))
                else:
                    scope.read(marker)
            elif not sql_reported and (content := lexeme.content_span(self._text)) is not None:
                self._errors.append(ParseError(SQL_BEFORE_FIRST_NAME, *content))
                sql_reported = True

    # The block's markers, then the query.  A block without SQL is an error, and has no spans but its markers'.
    def _read_block(self, name_span: tuple[int, int], kind: ScopeKind, start: int, end: int) -> None:
        last_content = self._find_last_content(start, end)
        scope = self._read_block_markers(kind, start, end, last_content)
        if last_content < 0:
            self._errors.append(ParseError(EMPTY_BLOCK, *name_span))
            return

        # A query runs to the next name marker, so it may lack a semicolon: its end is right after its last SQL.
        if end < len(self._lexemes):
            content_end = self._lexemes[last_content].content_span(self._text)[1]
            if self._text[content_end - 1] != ";":
                self._spans.append(PlanSpan(SpanKind.QUERY_END, content_end, content_end, None, None))

        self._build_query(scope, start, end)

    # The scope is the query's, or None when no marker was handed to one, as for most queries.
    def _read_block_markers(self, kind: ScopeKind, start: int, end: int, last_content: int) -> MarkerScope | None:
        scope = None
        for index in range(start, end):
            marker = read_marker(self._text, self._lexemes[index], self._rules)
            if marker is None:
                continue

            if 0 <= last_content < index:
                # A marker comes before the SQL it describes, so one after the block's last SQL is not applied.
                self._errors.append(ParseError(MARKER_AT_END_OF_BLOCK, marker.start, marker.end))
                if marker.kind == MarkerKind.DIALECT:
                    self._read_dialect(marker)
            elif marker.kind == MarkerKind.DIALECT:
                self._read_dialect(marker)
            elif marker.kind not in (MarkerKind.SUMMARY, MarkerKind.NAME):
                if scope is None:
                    scope = MarkerScope(self._text, self._rules, kind, self._errors)
                scope.read(marker)
        return scope

    # The SQL without comments is what the query is: its tokens and its parameters come from it.
    def _build_query(self, scope: MarkerScope | None, start: int, end: int) -> None:
        text = self._text
        sql = build(text, self._rules, self._lexemes, start, end)
        occurrences, scan_errors = scan(sql.text, set() if scope is None else scope.ignored_tokens)
        for found in scan_errors:
            self._errors.append(ParseError(found.code, *sql.to_source_span(found.start, found.end)))
        self._find_hidden_token_ends(sql, start, end)

        defaults = [] if scope is None else scope.token_defaults
        tokens = create_token_list(text, sql, occurrences, defaults, self._errors)
        lists = find_lists(text, self._lexemes, start, end, self._rules) if sql.parameters else []
        self._add_query(sql, occurrences, tokens, lists)

    # Each token with the default its query resolves for its name, and each parameter of the SQL, one inside a token's
    # default too, with whether it is used as a list.  An empty list of lists has none.
    def _add_query(
        self, sql: BlockText, occurrences: list[TokenOccurrence], tokens: list[Token], lists: list[bool]
    ) -> None:
        defaults = {token.name: token.default for token in tokens}
        for occurrence in occurrences:
            start, end = sql.to_source_span(occurrence.start, occurrence.end)
            self._spans.append(PlanSpan(SpanKind.TOKEN, start, end, occurrence.name, defaults[occurrence.name]))

        for position, (parameter_start, parameter_end) in enumerate(sql.parameters):
            start, end = sql.to_source_span(parameter_start, parameter_end)
            kind = SpanKind.LIST if lists and lists[position] else SpanKind.PARAMETER
            self._spans.append(PlanSpan(kind, start, end, self._text[start + 1 : end], None))

    # A comment is gone from the SQL that the tokens are found in, and so is any "}}" it holds: in a default, that is
    # not the end of the token.  Only a comment that holds "}}" is looked at further.  The comments come in order, and
    # so do their places in the SQL, so one finder reads the SQL once for them all.
    def _find_hidden_token_ends(self, sql: BlockText, start: int, end: int) -> None:
        finder = None
        for index in range(start, end):
            lexeme = self._lexemes[index]
            if lexeme.kind not in (LexemeKind.LINE_COMMENT, LexemeKind.BLOCK_COMMENT):
                continue
            if self._text.find("}}", lexeme.start, lexeme.end) < 0:
                continue
            if finder is None:
                finder = OpenDefaultFinder(sql.text)
            if finder.at(sql.to_output(lexeme.start)) is not None:
                self._errors.append(ParseError(TOKEN_CROSSES_COMMENT, lexeme.start, lexeme.end))

    # The place is checked first: a marker in the wrong place is reported as that, whatever it names.
    def _read_dialect(self, marker: Marker) -> None:
        if marker.start >= self._header_end:
            self._errors.append(ParseError(MISPLACED_DIALECT, marker.start, marker.end))
            return

        dialect = try_read(self._text, marker)
        if dialect is None:
            self._errors.append(invalid_dialect(marker))
        elif self._dialect is not None and self._dialect != dialect:
            self._errors.append(ParseError(CONFLICTING_SETTINGS, *place_of(marker)))
        else:
            self._dialect = dialect

    # The index of the last lexeme from start up to end that holds SQL, or -1 when none does.
    def _find_last_content(self, start: int, end: int) -> int:
        for index in range(end - 1, start - 1, -1):
            if self._lexemes[index].content_span(self._text) is not None:
                return index
        return -1
