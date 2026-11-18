"""Registers the SqlSource templater with sqlfluff."""

from sqlfluff.core.plugin import hookimpl
from sqlfluff.core.templaters import RawTemplater

from sqlfluff_templater_sqlsource.templater import SqlSourceTemplater


@hookimpl
def get_templaters() -> list[type[RawTemplater]]:
    return [SqlSourceTemplater]
