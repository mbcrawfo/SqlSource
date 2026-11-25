"""The sqlfluff templater of SqlSource queries."""

import dataclasses
import os
import re
from pathlib import Path

from sqlfluff.core.config import FluffConfig
from sqlfluff.core.errors import SQLTemplaterError
from sqlfluff.core.formatter import FormatterInterface
from sqlfluff.core.templaters import RawTemplater, TemplatedFile
from sqlfluff.core.templaters.base import large_file_check

from sqlfluff_templater_sqlsource.dialects import DialectChoice, canonical_name, option_names
from sqlfluff_templater_sqlsource.errors import (
    INVALID_MARKER_VALUE,
    TOKEN_CROSSES_COMMENT,
    UNTERMINATED_BLOCK_COMMENT,
    UNTERMINATED_QUOTE,
    ParseError,
)
from sqlfluff_templater_sqlsource.plan import Plan, build_plan
from sqlfluff_templater_sqlsource.render import render
from sqlfluff_templater_sqlsource.rules import DialectRules
from sqlfluff_templater_sqlsource.settings import decode, resolve_dialect
from sqlfluff_templater_sqlsource.stand_ins import UNLINTABLE_OPTIONS, compatible

# The title of each error that stops a plan, as its C# diagnostic descriptor gives it.  A test keeps it equal to
# `tests/corpus/diagnostic-titles.json`.
_TITLES = {
    UNTERMINATED_QUOTE: "Quote is not closed",
    UNTERMINATED_BLOCK_COMMENT: "Comment is not closed",
    INVALID_MARKER_VALUE: "Marker value is not valid",
    TOKEN_CROSSES_COMMENT: "Token crosses a comment",
}

# SqlSource reads a file without its byte order mark.  sqlfluff drops the mark when it detects the encoding, and keeps
# it when the encoding is configured.
_BYTE_ORDER_MARK = "﻿"


class SqlSourceTemplater(RawTemplater):
    """Renders each token as its default and each parameter as a stand-in of the sqlfluff dialect, so that sqlfluff
    can parse a SqlSource file and never changes what SqlSource reads in it."""

    name = "sqlsource"

    @large_file_check
    def process(
        self,
        *,
        in_str: str,
        fname: str,
        config: FluffConfig | None = None,
        formatter: FormatterInterface | None = None,
    ) -> tuple[TemplatedFile | None, list[SQLTemplaterError]]:
        sqlfluff_dialect = config.get("dialect") if config is not None else None
        try:
            mark = len(_BYTE_ORDER_MARK) if in_str.startswith(_BYTE_ORDER_MARK) else 0
            plan = build_plan(in_str[mark:], os.path.basename(fname), resolve_dialect(Path(fname)))
            errors = []
            if sqlfluff_dialect is not None and not compatible(plan.dialect, sqlfluff_dialect):
                errors.append(SQLTemplaterError(_mismatch(plan.dialect, sqlfluff_dialect)))
            if plan.error is not None:
                errors.append(_stopping_error(in_str, plan.error, mark))
            if not DialectRules.for_choice(plan.dialect).carriage_return_ends_line:
                errors.extend(_lone_carriage_return(fname, plan.dialect))
            if errors:
                return None, errors
            return render(in_str, _shifted(plan, mark), sqlfluff_dialect or "ansi", fname), []
        # Any failure is reported as a templating error of this file, where a crash would stop sqlfluff's whole run.
        except Exception as error:
            return None, [SQLTemplaterError(f"sqlsource templater failed: {type(error).__name__}: {error}")]


# Why sqlfluff cannot lint a file under each of `UNLINTABLE_OPTIONS`, named as the option is by `option_names`.
_OPTION_REASONS = {
    "ansi-quotes": 'ansi-quotes makes "..." a quoted identifier, which sqlfluff reads as a string',
    "no-backslash-escapes": (
        "no-backslash-escapes makes a backslash in a string a character, which sqlfluff reads as an escape"
    ),
}


def _mismatch(sqlsource: DialectChoice, sqlfluff_dialect: str) -> str:
    dialect = canonical_name(sqlsource.dialect)
    options = option_names(sqlsource.options & UNLINTABLE_OPTIONS)
    if not options:
        return f"The SqlSource dialect {dialect} cannot be linted with the sqlfluff dialect {sqlfluff_dialect}."
    return (
        f"The SqlSource dialect {dialect} with {' and '.join(options)} cannot be linted with the sqlfluff dialect "
        f"{sqlfluff_dialect}, or with any other: {'; '.join(_OPTION_REASONS[option] for option in options)}."
    )


# A carriage return with no line feed after it, as the file holds it.  sqlfluff reads a file with universal newlines,
# so the text it hands the templater has a line feed there; under a dialect that ends no line at a carriage return
# alone, sqlfluff would then lint a comment's text as SQL, and `fix` would write the line feed and change the query.
_LONE_CARRIAGE_RETURN = re.compile(r"\r(?!\n)")

# What sqlfluff counts as a line break when it reads a file.
_LINE_BREAK = re.compile(r"\r\n|\r|\n")


# The error at the file's first carriage return alone, at its line and position as sqlfluff counts them, or nothing
# for a file that has none or that is not on disk, such as standard input.
def _lone_carriage_return(fname: str, dialect: DialectChoice) -> list[SQLTemplaterError]:
    path = Path(fname)
    if not path.is_file():
        return []
    text = decode(path.read_bytes())
    found = _LONE_CARRIAGE_RETURN.search(text)
    if found is None:
        return []
    before = text[: found.start()]
    breaks = list(_LINE_BREAK.finditer(before))
    line_start = breaks[-1].end() if breaks else 0
    return [
        SQLTemplaterError(
            f"The SqlSource dialect {canonical_name(dialect.dialect)} ends a line only at a line feed, and this file "
            "has a carriage return alone, which sqlfluff reads as a line break, so it could lint and fix a comment's "
            "text as SQL.",
            line_no=len(breaks) + 1,
            line_pos=found.start() - line_start + 1,
        )
    ]


# The error at its line and position, both counted from 1, in the text sqlfluff handed the templater.
def _stopping_error(in_str: str, error: ParseError, mark: int) -> SQLTemplaterError:
    start = error.start + mark
    line_start = in_str.rfind("\n", 0, start) + 1
    return SQLTemplaterError(
        f"{error.code}: {_TITLES.get(error.code, 'SqlSource cannot read this file')}.",
        line_no=in_str.count("\n", 0, start) + 1,
        line_pos=start - line_start + 1,
    )


# The plan of the text after the byte order mark, as offsets into the text with it.
def _shifted(plan: Plan, by: int) -> Plan:
    if by == 0:
        return plan
    return dataclasses.replace(
        plan,
        lexemes=[dataclasses.replace(lexeme, start=lexeme.start + by, end=lexeme.end + by) for lexeme in plan.lexemes],
        spans=[dataclasses.replace(span, start=span.start + by, end=span.end + by) for span in plan.spans],
    )
