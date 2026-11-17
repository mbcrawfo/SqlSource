"""Renders a templating plan into the file sqlfluff lints.

Each span of the plan is one `templated` slice and the text between spans is `literal`, as sqlfluff's
`PlaceholderTemplater` builds them.  sqlfluff never writes a fix into a templated slice, so nothing a slice covers can
change: a token, a parameter, a list, a marker comment, and the white space and comments after a query that the
query's inserted `;` stands over.
"""

from sqlfluff.core.templaters import TemplatedFile
from sqlfluff.core.templaters.base import RawFileSlice, TemplatedFileSlice

from sqlfluff_templater_sqlsource.dialects import DialectChoice
from sqlfluff_templater_sqlsource.lexer import LexemeKind, Lexer
from sqlfluff_templater_sqlsource.list_uses import find_lists
from sqlfluff_templater_sqlsource.plan import Plan, PlanSpan, SpanKind
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.stand_ins import stand_in

# The tag PlaceholderTemplater gives its slices: sqlfluff keeps a violation that touches only slices so tagged, as it
# keeps one in literal SQL, since the slice holds what the user wrote.  A query's end holds no SQL of the user's, and
# a violation there, such as CV06 on its `;`, is not one the user can act on.
_USER_TEXT = "literal"


def render(text: str, plan: Plan, sqlfluff_dialect: str, fname: str) -> TemplatedFile:
    """The file sqlfluff lints in place of `text`, whose plan is `plan`, with the stand-ins of `sqlfluff_dialect`."""
    builder = _Builder(text)
    spans = plan.spans
    index = 0
    while index < len(spans):
        span = spans[index]
        index += 1
        if span.kind == SpanKind.TOKEN:
            # The parameters inside a token come right after it, and are part of what it renders.
            inner = []
            while index < len(spans) and spans[index].start < span.end:
                inner.append(spans[index])
                index += 1
            builder.templated(span.start, span.end, _token(span, inner, plan.dialect, sqlfluff_dialect), _USER_TEXT)
        elif span.kind == SpanKind.PARAMETER:
            builder.templated(span.start, span.end, stand_in(span.name, sqlfluff_dialect), _USER_TEXT)
        elif span.kind == SpanKind.LIST:
            builder.templated(span.start, span.end, f"({stand_in(span.name, sqlfluff_dialect)})", _USER_TEXT)
        elif span.kind == SpanKind.MARKER_COMMENT:
            builder.templated(span.start, span.end, text[span.start : span.end], _USER_TEXT)
        else:
            # The `;` stands over the white space and comments up to the next span, which is the next query's
            # -- name: marker or a comment in marker form, so that no fix can write it, or move it past a comment.
            # When a marker-form comment follows the SQL at once, it stands over nothing.
            end = spans[index].start if index < len(spans) else span.end
            builder.templated(span.start, end, ";" + text[span.start : end], None)
    return builder.finish(fname)


# A token renders its default, or its name when it has none.  Each parameter in the default renders its stand-in, as
# a list where the plan says so; a default from a `-- token:` marker has no parameter spans, and its lists are found in
# the default alone.
def _token(span: PlanSpan, inner: list[PlanSpan], dialect: DialectChoice, sqlfluff_dialect: str) -> str:
    default = span.default
    if default is None:
        return span.name
    rules = DialectRules.for_choice(dialect)
    lexemes, _ = Lexer(default, rules).read_to_end()
    parameters = [lexeme for lexeme in lexemes if lexeme.kind == LexemeKind.PARAMETER]
    if [default[lexeme.start + 1 : lexeme.end] for lexeme in parameters] == [found.name for found in inner]:
        lists = [found.kind == SpanKind.LIST for found in inner]
    else:
        lists = find_lists(default, lexemes, 0, len(lexemes), rules) or [False] * len(parameters)

    parts = []
    written = 0
    for lexeme, is_list in zip(parameters, lists, strict=True):
        replacement = stand_in(default[lexeme.start + 1 : lexeme.end], sqlfluff_dialect)
        parts.append(default[written : lexeme.start])
        parts.append(f"({replacement})" if is_list else replacement)
        written = lexeme.end
    parts.append(default[written:])
    return "".join(parts)


class _Builder:
    """Collects the slices of a file in order, the text between two templated slices as a literal one."""

    def __init__(self, text: str) -> None:
        self._text = text
        self._source_end = 0
        self._rendered: list[str] = []
        self._templated_end = 0
        self._sliced: list[TemplatedFileSlice] = []
        self._raw: list[RawFileSlice] = []

    def templated(self, start: int, end: int, rendered: str, tag: str | None) -> None:
        self._literal(start)
        self._add("templated", start, end, rendered, tag)

    def finish(self, fname: str) -> TemplatedFile:
        self._literal(len(self._text))
        return TemplatedFile(
            source_str=self._text,
            fname=fname,
            templated_str="".join(self._rendered),
            sliced_file=self._sliced,
            raw_sliced=self._raw,
        )

    def _literal(self, end: int) -> None:
        if end > self._source_end:
            self._add("literal", self._source_end, end, self._text[self._source_end : end], None)

    def _add(self, slice_type: str, start: int, end: int, rendered: str, tag: str | None) -> None:
        templated_start = self._templated_end
        self._templated_end += len(rendered)
        self._sliced.append(
            TemplatedFileSlice(slice_type, slice(start, end), slice(templated_start, self._templated_end))
        )
        self._raw.append(RawFileSlice(self._text[start:end], slice_type, start, tag=tag))
        self._rendered.append(rendered)
        self._source_end = end
