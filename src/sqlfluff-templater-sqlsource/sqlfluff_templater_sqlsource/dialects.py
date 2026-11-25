"""The dialects SqlSource reads, their options and their names.

Mirrors `SqlDialect.cs`, `SqlDialectOptions.cs`, `SqlDialectChoice.cs` and `SqlDialectName.cs` in
`src/SqlSource/Parsing/`.
"""

import enum
from dataclasses import dataclass
from typing import TypeVar

from sqlfluff_templater_sqlsource.text import equals_ascii_ignoring_case, trim


class Dialect(enum.Enum):
    """The database whose rules decide where comments, strings and quoted identifiers start and end, valued by the
    names the C# enum gives them."""

    ANSI = "Ansi"
    SQL_SERVER = "SqlServer"
    POSTGRESQL = "PostgreSql"
    MYSQL = "MySql"
    MARIADB = "MariaDb"
    SQLITE = "Sqlite"
    ORACLE = "Oracle"
    COCKROACHDB = "CockroachDb"


class DialectOptions(enum.Flag):
    """Settings of a database server that change one rule of its dialect.  Only MySQL and MariaDB have any.  The
    values are those of the C# enum."""

    NONE = 0
    ANSI_QUOTES = 1
    NO_BACKSLASH_ESCAPES = 2


@dataclass(frozen=True)
class DialectChoice:
    """What a dialect setting names: a dialect, the options of that dialect that are on, and the version of its server,
    zero when none is given, as a versioned comment writes it: `80036` for 8.0.36."""

    dialect: Dialect
    options: DialectOptions = DialectOptions.NONE
    server_version: int = 0


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


# The word of the option that gives the server's version, before its `=`: `version=8.0.36`.
VERSION_OPTION = "version"


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
    server_version = 0
    for part in parts:
        version = _read_version(part)
        if version is not None:
            # The same version twice counts once, as an option does; two versions are no server.
            if found not in (Dialect.MYSQL, Dialect.MARIADB) or server_version not in (0, version):
                return None
            server_version = version
            continue
        option = _find(part, OPTIONS)
        if option is None or not option & allowed:
            return None
        options |= option
    return DialectChoice(found, options, server_version)


# "version=M.m.p", the word ignoring case and white space around the =, each number one or two ASCII digits and the
# major not zero, as a versioned comment writes it: M * 10000 + m * 100 + p.
def _read_version(part: str) -> int | None:
    start, end = trim(part, 0, len(part))
    trimmed = part[start:end]
    word = len(VERSION_OPTION)
    if len(trimmed) <= word or not equals_ascii_ignoring_case(trimmed[:word], VERSION_OPTION):
        return None
    rest = trimmed[word:]
    start, _ = trim(rest, 0, len(rest))
    if start >= len(rest) or rest[start] != "=":
        return None
    rest = rest[start + 1 :]
    start, _ = trim(rest, 0, len(rest))
    numbers = rest[start:].split(".")
    if len(numbers) != 3 or not all(
        0 < len(number) <= 2 and all("0" <= c <= "9" for c in number) for number in numbers
    ):
        return None
    version = int(numbers[0]) * 10000 + int(numbers[1]) * 100 + int(numbers[2])
    return version if version >= 10000 else None


_T = TypeVar("_T")


def _find(part: str, names: tuple[tuple[str, _T], ...]) -> _T | None:
    start, end = trim(part, 0, len(part))
    name = part[start:end]
    return next((value for candidate, value in names if equals_ascii_ignoring_case(name, candidate)), None)
