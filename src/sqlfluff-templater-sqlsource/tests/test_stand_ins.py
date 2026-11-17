import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions
from sqlfluff_templater_sqlsource.stand_ins import compatible, stand_in


@pytest.mark.parametrize("sqlfluff_dialect", ["tsql", "mysql", "mariadb", "sqlite"])
def test_at_sign_dialects_keep_the_parameter_as_written(sqlfluff_dialect):
    assert stand_in("user_id", sqlfluff_dialect) == "@user_id"


@pytest.mark.parametrize("sqlfluff_dialect", ["postgres", "oracle", "duckdb", "greenplum", "materialize"])
def test_colon_dialects_take_the_name_after_a_colon(sqlfluff_dialect):
    assert stand_in("user_id", sqlfluff_dialect) == ":user_id"


@pytest.mark.parametrize("sqlfluff_dialect", ["ansi", "db2", "snowflake", "redshift", "bigquery", "no-such-dialect"])
def test_other_dialects_take_a_literal(sqlfluff_dialect):
    assert stand_in("user_id", sqlfluff_dialect) == "0"


# The names each prefixed dialect lexes after its prefix, at each edge of what it takes, as a probe found them on
# sqlfluff 4.5.0.  `test_sqlfluff_end_to_end.py` lints each of them.
KEPT = {
    "tsql": ["_x", "__x", "1st", "x$y", "$x", "x#y", "#x", "x@y", "tötal", "名前", "é", "ǅx", "xʰ"],
    "mysql": ["_x", "1st", "x$y", "$x"],
    "mariadb": ["_x", "1st", "x$y", "$x"],
    "sqlite": ["_x", "1st"],
    "postgres": ["_x", "_", "x$y"],
    "duckdb": ["_x", "_", "x$y"],
    "greenplum": ["_x", "_", "x$y"],
    "materialize": ["_x", "_", "x$y"],
    "oracle": ["x$y", "x_1"],
}

# The names each prefixed dialect cannot lex after its prefix, at each edge of what it takes.
_NOT_ASCII = ["é", "naïve", "名前"]
_COLON_LITERAL = ["1st", "1", "$x", "x#y", "#x", "x@y", "x٣", "", *_NOT_ASCII]
LITERAL = {
    # A digit of another script, a combining mark, a spacing mark, a letter number, two format characters and two
    # connectors: an mssql name of SqlSource's can hold each of them.
    "tsql": ["x٣", "٣x", "xe\u0301", "x\u0903", "xⅫ", "x\u200c", "x\u00ad", "x\u203fy", "x\uff3fy", ""],
    "mysql": ["x#y", "#x", "x@y", "x٣", "", *_NOT_ASCII],
    "mariadb": ["x#y", "#x", "x@y", "x٣", "", *_NOT_ASCII],
    "sqlite": ["x$y", "$x", "x#y", "x@y", "", *_NOT_ASCII],
    # U+0131 parses, as the I it folds to, and is left out.
    "postgres": [*_COLON_LITERAL, "\u0131"],
    "duckdb": _COLON_LITERAL,
    "greenplum": _COLON_LITERAL,
    "materialize": _COLON_LITERAL,
    "oracle": ["_x", "__x", "_", *_COLON_LITERAL],
}


@pytest.mark.parametrize(("sqlfluff_dialect", "name"), [(dialect, name) for dialect in KEPT for name in KEPT[dialect]])
def test_a_name_the_dialect_lexes_keeps_its_form(sqlfluff_dialect, name):
    prefix = "@" if sqlfluff_dialect in ("tsql", "mysql", "mariadb", "sqlite") else ":"
    assert stand_in(name, sqlfluff_dialect) == prefix + name


@pytest.mark.parametrize(
    ("sqlfluff_dialect", "name"), [(dialect, name) for dialect in LITERAL for name in LITERAL[dialect]]
)
def test_a_name_the_dialect_may_not_lex_takes_a_literal(sqlfluff_dialect, name):
    assert stand_in(name, sqlfluff_dialect) == "0"


def test_name_with_digits_and_underscores_is_kept():
    assert stand_in("Page2_size", "postgres") == ":Page2_size"


ANSI = DialectChoice(Dialect.ANSI)
MSSQL = DialectChoice(Dialect.SQL_SERVER)
POSTGRES = DialectChoice(Dialect.POSTGRESQL)
COCKROACH = DialectChoice(Dialect.COCKROACHDB)
MYSQL = DialectChoice(Dialect.MYSQL)
MYSQL_ANSI_QUOTES = DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)
MARIADB = DialectChoice(Dialect.MARIADB)
SQLITE = DialectChoice(Dialect.SQLITE)
ORACLE = DialectChoice(Dialect.ORACLE)

SQLFLUFF_DIALECTS = [
    "ansi",
    "athena",
    "bigquery",
    "clickhouse",
    "databricks",
    "db2",
    "doris",
    "duckdb",
    "exasol",
    "flink",
    "greenplum",
    "hive",
    "impala",
    "mariadb",
    "materialize",
    "mysql",
    "oracle",
    "postgres",
    "redshift",
    "snowflake",
    "soql",
    "sparksql",
    "sqlite",
    "starrocks",
    "teradata",
    "trino",
    "tsql",
    "vertica",
    "no-such-dialect",
]

PAIRS = {
    MSSQL: {"tsql"},
    POSTGRES: {"postgres", "duckdb", "redshift", "greenplum", "materialize"},
    COCKROACH: {"postgres", "duckdb", "redshift", "greenplum", "materialize"},
    MYSQL: {"mysql", "mariadb"},
    MYSQL_ANSI_QUOTES: {"mysql", "mariadb"},
    MARIADB: {"mariadb", "mysql"},
    SQLITE: {"sqlite"},
    ORACLE: {"oracle"},
}


@pytest.mark.parametrize("sqlfluff_dialect", SQLFLUFF_DIALECTS)
def test_ansi_pairs_with_every_sqlfluff_dialect(sqlfluff_dialect):
    assert compatible(ANSI, sqlfluff_dialect)


@pytest.mark.parametrize("sqlfluff_dialect", SQLFLUFF_DIALECTS)
@pytest.mark.parametrize("sqlsource", list(PAIRS), ids=lambda choice: f"{choice.dialect.value}-{choice.options.value}")
def test_other_dialects_pair_only_with_their_list(sqlsource, sqlfluff_dialect):
    assert compatible(sqlsource, sqlfluff_dialect) == (sqlfluff_dialect in PAIRS[sqlsource])
