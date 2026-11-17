"""What sqlfluff sees in place of a parameter, and which sqlfluff dialects can lint a file of each SqlSource dialect.

A stand-in must parse wherever SqlSource lets a parameter stand, and must set off no rule that a literal would not.
The table was settled by `tests/test_sqlfluff_end_to_end.py` against sqlfluff 4.2.0 and 4.5.0; before 4.2, :name sets
off LT01 in every dialect that takes it here.
"""

import re

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice

# The stand-in for a parameter whose name the dialect cannot take after its prefix, and for any parameter in a
# dialect without one: a literal parses wherever a value can stand, in every sqlfluff dialect.
LITERAL = "0"

# The prefix each sqlfluff dialect takes in front of a parameter's name.  A dialect not named here gets LITERAL.
_PREFIXES = {
    # @name parses as a variable or a parameter.
    "tsql": "@",
    "mysql": "@",
    "mariadb": "@",
    "sqlite": "@",
    # @name does not parse, or not everywhere (duckdb fails on `BETWEEN @a AND @b`), and :name does.
    "postgres": ":",
    "oracle": ":",
    "duckdb": ":",
    "greenplum": ":",
    "materialize": ":",
    # ansi, db2, redshift and snowflake take neither everywhere: snowflake fails on `LIMIT @n` and `LIMIT :n`.
}

# The names every prefixed dialect lexes after its prefix: oracle rejects a leading _, postgres a leading digit, mysql
# a #, and each of them a letter above U+007F, T-SQL too in sqlfluff 4.0.
_PLAIN_NAME = re.compile(r"[A-Za-z][A-Za-z0-9_]*")

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
    prefix = _PREFIXES.get(sqlfluff_dialect)
    if prefix is None or not _PLAIN_NAME.fullmatch(name):
        return LITERAL
    return prefix + name


def compatible(sqlsource: DialectChoice, sqlfluff_dialect: str) -> bool:
    """Whether a file SqlSource reads as `sqlsource` can be linted in `sqlfluff_dialect`."""
    if sqlsource.dialect == Dialect.ANSI:
        return True
    return sqlfluff_dialect in _COMPATIBLE[sqlsource.dialect]
