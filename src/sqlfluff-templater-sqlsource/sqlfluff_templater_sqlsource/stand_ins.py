"""What sqlfluff sees in place of a parameter, and which sqlfluff dialects can lint a file of each SqlSource dialect.

A stand-in must parse wherever SqlSource lets a parameter stand, and must set off no rule that a literal would not.
The table was settled by `tests/test_sqlfluff_end_to_end.py` against sqlfluff 4.2.0 and 4.5.0; before 4.2, :name sets
off LT01 in every dialect that takes it here.
"""

import re
import unicodedata
from collections.abc import Callable
from typing import NamedTuple

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice

# The stand-in for a parameter whose name the dialect cannot take after its prefix, and for any parameter in a
# dialect without one: a literal parses wherever a value can stand, in every sqlfluff dialect.
LITERAL = "0"


def _ascii_name(pattern: str) -> Callable[[str], bool]:
    compiled = re.compile(pattern)
    return lambda name: compiled.fullmatch(name) is not None


# T-SQL lexes a name of ASCII digits, `_`, `@`, `#`, `$` and letters of any script.  An mssql name of SqlSource's can
# also hold a digit of another script, a mark, a letter number, a format character or a connector, which it does not.
_TSQL_PUNCTUATION = frozenset("_@#$")


def _tsql_name(name: str) -> bool:
    return name != "" and all(
        ("0" <= value <= "9") or value in _TSQL_PUNCTUATION or unicodedata.category(value).startswith("L")
        for value in name
    )


_MYSQL_NAME = _ascii_name(r"[A-Za-z0-9_$]+")

# Not a leading digit or `$`, nor a `#`.
_POSTGRES_NAME = _ascii_name(r"[A-Za-z_][A-Za-z0-9_$]*")


class _Prefix(NamedTuple):
    # What the sqlfluff dialect takes in front of a parameter's name.
    prefix: str
    # Whether the dialect lexes `name` after that prefix: any other name gets LITERAL.
    takes: Callable[[str], bool]


# Each sqlfluff dialect that takes a prefix, and the names it lexes after it, as a probe of each name SqlSource reads
# found them on sqlfluff 4.5.0.  None takes a letter outside ASCII but tsql; the colon dialects take U+0131, U+017F and
# U+212A as the ASCII letters they fold to, which this leaves out.  A dialect not named here gets LITERAL.
_PREFIXES = {
    # @name parses as a variable or a parameter.
    "tsql": _Prefix("@", _tsql_name),
    "mysql": _Prefix("@", _MYSQL_NAME),
    "mariadb": _Prefix("@", _MYSQL_NAME),
    "sqlite": _Prefix("@", _ascii_name(r"[A-Za-z0-9_]+")),
    # @name does not parse, or not everywhere (duckdb fails on `BETWEEN @a AND @b`), and :name does.
    "postgres": _Prefix(":", _POSTGRES_NAME),
    "duckdb": _Prefix(":", _POSTGRES_NAME),
    "greenplum": _Prefix(":", _POSTGRES_NAME),
    "materialize": _Prefix(":", _POSTGRES_NAME),
    # Not a leading `_` either.
    "oracle": _Prefix(":", _ascii_name(r"[A-Za-z][A-Za-z0-9_$]*")),
    # ansi, db2, redshift and snowflake take neither everywhere: snowflake fails on `LIMIT @n` and `LIMIT :n`.
}

# The sqlfluff dialects each SqlSource dialect but ansi can be linted with.  ansi pairs with every sqlfluff dialect.
_COMPATIBLE = {
    Dialect.SQL_SERVER: frozenset({"tsql"}),
    Dialect.POSTGRESQL: frozenset({"postgres", "duckdb", "redshift", "greenplum", "materialize"}),
    Dialect.COCKROACHDB: frozenset({"postgres", "duckdb", "redshift", "greenplum", "materialize"}),
    Dialect.MYSQL: frozenset({"mysql", "mariadb"}),
    Dialect.MARIADB: frozenset({"mariadb", "mysql"}),
    Dialect.SQLITE: frozenset({"sqlite"}),
    Dialect.ORACLE: frozenset({"oracle"}),
}


def stand_in(name: str, sqlfluff_dialect: str) -> str:
    """What sqlfluff sees in place of the parameter `name`, given without its prefix."""
    form = _PREFIXES.get(sqlfluff_dialect)
    if form is None or not form.takes(name):
        return LITERAL
    return form.prefix + name


def compatible(sqlsource: DialectChoice, sqlfluff_dialect: str) -> bool:
    """Whether a file SqlSource reads as `sqlsource` can be linted in `sqlfluff_dialect`."""
    if sqlsource.dialect == Dialect.ANSI:
        return True
    return sqlfluff_dialect in _COMPATIBLE[sqlsource.dialect]
