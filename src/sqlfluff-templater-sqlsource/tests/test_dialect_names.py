import pytest
from corpus_reader import dialect_names

from sqlfluff_templater_sqlsource import dialects
from sqlfluff_templater_sqlsource.dialects import (
    Dialect,
    DialectChoice,
    DialectOptions,
    canonical_name,
    option_names,
    parse_dialect_name,
)

# SqlDialectName.Names and SqlDialectName.Options, as TemplatingCorpusTests writes them from the C# code.
_NAMES, _OPTIONS = dialect_names()
NAMES = [(name, Dialect(member)) for name, member in _NAMES]
OPTIONS = [(name, DialectOptions(value)) for name, value in _OPTIONS]

BOTH = DialectOptions.ANSI_QUOTES | DialectOptions.NO_BACKSLASH_ESCAPES


def test_names_and_options_are_those_of_the_parser_in_its_order():
    assert list(dialects.NAMES) == NAMES
    assert list(dialects.OPTIONS) == OPTIONS


@pytest.mark.parametrize(("name", "dialect"), NAMES)
@pytest.mark.parametrize("spelling", [str.lower, str.upper, str.title])
def test_name_gives_its_dialect_in_any_case(name, dialect, spelling):
    assert parse_dialect_name(spelling(name)) == DialectChoice(dialect)


@pytest.mark.parametrize(("name", "option"), OPTIONS)
@pytest.mark.parametrize("dialect", [Dialect.MYSQL, Dialect.MARIADB])
@pytest.mark.parametrize("spelling", [str.lower, str.upper, str.title])
def test_option_of_mysql_and_mariadb_is_read_in_any_case(name, option, dialect, spelling):
    assert parse_dialect_name(f"{canonical_name(dialect)},{spelling(name)}") == DialectChoice(dialect, option)


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        ("  oracle\t", DialectChoice(Dialect.ORACLE)),
        ("\n    sqlite\n", DialectChoice(Dialect.SQLITE)),
        ("mysql,ansi-quotes,no-backslash-escapes", DialectChoice(Dialect.MYSQL, BOTH)),
        ("mariadb,no-backslash-escapes,ansi-quotes", DialectChoice(Dialect.MARIADB, BOTH)),
        ("mysql,ansi-quotes,ansi_quotes", DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)),
        ("mysql,ansi-quotes,ansi_quotes,ANSI-QUOTES", DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)),
        ("  mysql , ansi-quotes ,\tno_backslash_escapes\n", DialectChoice(Dialect.MYSQL, BOTH)),
        ("\n    mysql,\n    ansi-quotes\n", DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)),
    ],
)
def test_white_space_around_a_part_and_a_repeated_option_are_read(value, expected):
    assert parse_dialect_name(value) == expected


@pytest.mark.parametrize(
    "value",
    [
        "",
        "   ",
        "sql-server",
        "pgsql",
        "sql server",
        "my sql",
        "postgres;mysql",
        "oracle,",
        "SqlDialect.Oracle",
        "3",
        "mysql,",
        "mysql,,ansi-quotes",
        "mysql,ansi-quotes,",
        "mysql, ",
        ",mysql",
        "mysql,ansi",
        "mysql,ansi quotes",
        "mysql,ansi-quotes=on",
        "mysql,mariadb",
        "postgres,ansi-quotes",
        "ansi,no-backslash-escapes",
        "oracle,ansi_quotes",
        "cockroachdb,ansi-quotes",
        "crdb",
        "ansi-quotes",
        "ansi-quotes,mysql",
        "mysql;ansi-quotes",
        "mysql ansi-quotes",
        "mysql+ansi-quotes",
        # Ordinal comparison folds no character above U+007F to an ASCII one, as .NET's does not.
        "mysql,ans\u0131-quotes",
        "sql\u0131te",
        "\u017fqlite",
        "mariadb,no-back\u017flash-escapes",
        # .NET's white space, which U+001C is not.
        "\x1cmysql",
    ],
)
def test_anything_else_is_rejected(value):
    assert parse_dialect_name(value) is None


@pytest.mark.parametrize(
    ("dialect", "expected"),
    [
        (Dialect.ANSI, "ansi"),
        (Dialect.SQL_SERVER, "mssql"),
        (Dialect.POSTGRESQL, "postgres"),
        (Dialect.COCKROACHDB, "cockroachdb"),
        (Dialect.MYSQL, "mysql"),
        (Dialect.MARIADB, "mariadb"),
        (Dialect.SQLITE, "sqlite"),
        (Dialect.ORACLE, "oracle"),
    ],
)
def test_canonical_name_is_the_first_name_of_the_dialect(dialect, expected):
    assert canonical_name(dialect) == expected


@pytest.mark.parametrize(
    ("options", "expected"),
    [
        (DialectOptions.NONE, []),
        (DialectOptions.ANSI_QUOTES, ["ansi-quotes"]),
        (DialectOptions.NO_BACKSLASH_ESCAPES, ["no-backslash-escapes"]),
        (BOTH, ["ansi-quotes", "no-backslash-escapes"]),
    ],
)
def test_option_names_are_the_first_name_of_each_option(options, expected):
    assert option_names(options) == expected
