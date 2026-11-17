"""The sqlfluff templater of SqlSource queries."""

import dataclasses
import os
from pathlib import Path

from sqlfluff.core.config import FluffConfig
from sqlfluff.core.errors import SQLTemplaterError
from sqlfluff.core.formatter import FormatterInterface
from sqlfluff.core.templaters import RawTemplater, TemplatedFile
from sqlfluff.core.templaters.base import large_file_check

from sqlfluff_templater_sqlsource.dialects import canonical_name
from sqlfluff_templater_sqlsource.errors import (
    INVALID_MARKER_VALUE,
    TOKEN_CROSSES_COMMENT,
    UNTERMINATED_BLOCK_COMMENT,
    UNTERMINATED_QUOTE,
    ParseError,
)
from sqlfluff_templater_sqlsource.plan import Plan, build_plan
from sqlfluff_templater_sqlsource.render import render
from sqlfluff_templater_sqlsource.settings import resolve_dialect
from sqlfluff_templater_sqlsource.stand_ins import compatible

# The title that `site/diagnostics.md` gives each error that stops a plan.
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
                errors.append(
                    SQLTemplaterError(
                        f"The SqlSource dialect {canonical_name(plan.dialect.dialect)} cannot be linted with the "
                        f"sqlfluff dialect {sqlfluff_dialect}."
                    )
                )
            if plan.error is not None:
                errors.append(_stopping_error(in_str, plan.error, mark))
            if errors:
                return None, errors
            return render(in_str, _shifted(plan, mark), sqlfluff_dialect or "ansi", fname), []
        # Any failure is reported as a templating error of this file, where a crash would stop sqlfluff's whole run.
        except Exception as error:
            return None, [SQLTemplaterError(f"sqlsource templater failed: {type(error).__name__}: {error}")]


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
