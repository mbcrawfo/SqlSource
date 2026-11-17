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


@pytest.mark.parametrize("name", ["名前", "naïve", "_id", "1st", "a#b", "a$b", "a@b"])
@pytest.mark.parametrize("sqlfluff_dialect", ["tsql", "mysql", "mariadb", "sqlite", "postgres", "oracle", "duckdb"])
def test_a_name_a_dialect_may_not_lex_takes_a_literal(name, sqlfluff_dialect):
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
