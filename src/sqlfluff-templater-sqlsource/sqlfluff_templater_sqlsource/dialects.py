"""The dialects SqlSource reads, their options and their names.

Mirrors `SqlDialect.cs`, `SqlDialectOptions.cs`, `SqlDialectChoice.cs` and `SqlDialectName.cs` in
`src/SqlSource/Parsing/`.
"""

import enum
from dataclasses import dataclass
from typing import TypeVar

from sqlfluff_templater_sqlsource.text import equals_ascii_ignoring_case, trim


class Dialect(enum.Enum):
    """The database whose rules decide where comments, strings and quoted identifiers start and end."""

    ANSI = enum.auto()
    SQL_SERVER = enum.auto()
    POSTGRESQL = enum.auto()
    MYSQL = enum.auto()
    MARIADB = enum.auto()
    SQLITE = enum.auto()
    ORACLE = enum.auto()
    COCKROACHDB = enum.auto()


class DialectOptions(enum.Flag):
    """Settings of a database server that change one rule of its dialect.  Only MySQL and MariaDB have any."""

    NONE = 0
    ANSI_QUOTES = 1
    NO_BACKSLASH_ESCAPES = 2


@dataclass(frozen=True)
class DialectChoice:
    """What a dialect setting names: a dialect, and the options of that dialect that are on."""

    dialect: Dialect
    options: DialectOptions = DialectOptions.NONE


# Each name and alias, with its dialect: the first name of a dialect is its canonical one.
NAMES: tuple[tuple[str, Dialect], ...] = (
    ("ansi", Dialect.ANSI),
    ("mssql", Dialect.SQL_SERVER),
    ("sqlserver", Dialect.SQL_SERVER),
    ("tsql", Dialect.SQL_SERVER),
    ("postgres", Dialect.POSTGRESQL),
    ("postgresql", Dialect.POSTGRESQL),
    ("cockroachdb", Dialect.COCKROACHDB),
    ("cockroach", Dialect.COCKROACHDB),
    ("mysql", Dialect.MYSQL),
    ("mariadb", Dialect.MARIADB),
    ("sqlite", Dialect.SQLITE),
    ("oracle", Dialect.ORACLE),
)

# Each name of an option, with the option.  An option is also accepted as the server spells its SQL mode.
OPTIONS: tuple[tuple[str, DialectOptions], ...] = (
    ("ansi-quotes", DialectOptions.ANSI_QUOTES),
    ("ansi_quotes", DialectOptions.ANSI_QUOTES),
    ("no-backslash-escapes", DialectOptions.NO_BACKSLASH_ESCAPES),
    ("no_backslash_escapes", DialectOptions.NO_BACKSLASH_ESCAPES),
)


def canonical_name(dialect: Dialect) -> str:
    """The name that stands for `dialect` wherever one name is needed: its first in the list."""
    return next(name for name, candidate in NAMES if candidate == dialect)


def option_names(options: DialectOptions) -> list[str]:
    """The name of each option that is on, the first the list gives it, in the order of the list."""
    names = []
    named = DialectOptions.NONE
    for name, option in OPTIONS:
        if option & options and not option & named:
            names.append(name)
            named |= option
    return names


def options_of(dialect: Dialect) -> DialectOptions:
    """The options that `dialect` takes; none for a dialect that takes none."""
    if dialect in (Dialect.MYSQL, Dialect.MARIADB):
        return DialectOptions.ANSI_QUOTES | DialectOptions.NO_BACKSLASH_ESCAPES
    return DialectOptions.NONE


def parse_dialect_name(value: str) -> DialectChoice | None:
    """Reads a name or an alias and the options after it, ignoring case and the white space around each part.

    None for anything else: a name that is not a dialect, an option that does not exist or that the dialect does not
    have, and an empty part.  Case is ignored as .NET's ordinal comparison ignores it, so only ASCII letters fold.
    """
    dialect, *parts = value.split(",")
    found = _find(dialect, NAMES)
    if found is None:
        return None

    allowed = options_of(found)
    options = DialectOptions.NONE
    for part in parts:
        option = _find(part, OPTIONS)
        if option is None or not option & allowed:
            return None
        options |= option
    return DialectChoice(found, options)


_T = TypeVar("_T")


def _find(part: str, names: tuple[tuple[str, _T], ...]) -> _T | None:
    start, end = trim(part, 0, len(part))
    name = part[start:end]
    return next((value for candidate, value in names if equals_ascii_ignoring_case(name, candidate)), None)
