from pathlib import Path

import pytest
from corpus_reader import CORPUS_DIRECTORY, corpus_cases

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions, parse_dialect_name
from sqlfluff_templater_sqlsource.settings import SettingsDialect, dialect_of_chain, read_settings, resolve_dialect

CASES = corpus_cases()
POSTGRES = DialectChoice(Dialect.POSTGRESQL)
MSSQL = DialectChoice(Dialect.SQL_SERVER)
ANSI = DialectChoice(Dialect.ANSI)


@pytest.mark.parametrize("case", CASES, ids=[case.path.relative_to(CORPUS_DIRECTORY).as_posix() for case in CASES])
def test_chain_matches_corpus(case):
    expected = parse_dialect_name(",".join([case.expected["startDialect"], *case.expected["startOptions"]]))

    assert dialect_of_chain(case.settings) == expected


def test_comments_and_trailing_commas_are_read():
    text = '// a\n{ /* b */ "root": true, // c\n "dialect": "postgres", "other": [1, 2,], }\n'

    assert read_settings(text) == SettingsDialect(True, POSTGRES)


def test_options_are_read():
    result = read_settings('{"dialect": " MySql , ansi-quotes "}')

    assert result.dialect == DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)


def test_escaped_key_and_value_are_unescaped():
    assert read_settings('{"\\u0072oot": true, "dialect": "post\\u0067res"}') == SettingsDialect(True, POSTGRES)


def test_root_false_and_absent():
    assert read_settings('{"root": false}') == SettingsDialect(False, None)
    assert read_settings("{}") == SettingsDialect(False, None)


def test_root_of_wrong_type_is_not_true():
    assert read_settings('{"root": "true"}').root is False


@pytest.mark.parametrize(
    "text",
    [
        "",
        "[]",
        '"x"',
        "5",
        '{"root": true',
        '{"root": true} x',
        '{"root": true,, }',
        '{"root": true /* open }',
        '{"root": 01}',
        '{"dialect": "a\nb", "root": true}',
        '{"root": tru}',
        "{" * 100,
        "[" * 65 + "]" * 65,
        '{"a": ' * 65 + "1" + "}" * 65,
    ],
)
def test_text_that_is_not_one_object_has_unknown_root(text):
    assert read_settings(text) == SettingsDialect(None, None)


def test_nesting_to_the_limit_is_read():
    text = '{"root": true, "a": ' + "[" * 63 + "]" * 63 + "}"

    assert read_settings(text).root is True
    assert read_settings('{"root": true, "a": ' + "[" * 64 + "]" * 64 + "}").root is None


@pytest.mark.parametrize("value", ["5", '""', '"  "', '"nope"', '"postgres,ansi-quotes"', "null", "true", "[]", "{}"])
def test_dialect_that_is_not_valid_sets_nothing(value):
    assert read_settings('{"root": true, "dialect": ' + value + "}") == SettingsDialect(True, None)


def test_first_of_a_duplicated_key_wins():
    assert read_settings('{"dialect": "postgres", "dialect": "mssql"}').dialect == POSTGRES
    assert read_settings('{"dialect": 5, "dialect": "mssql"}').dialect is None


def test_chain_takes_nearest_dialect():
    assert dialect_of_chain(['{"dialect": "postgres"}', '{"dialect": "mssql"}']) == POSTGRES


def test_chain_passes_a_file_that_sets_none():
    assert dialect_of_chain(['{"root": false}', '{"dialect": "mssql"}']) == MSSQL


def test_chain_passes_a_malformed_file():
    assert dialect_of_chain(["{", '{"dialect": "mssql"}']) == MSSQL


def test_chain_stops_after_root():
    assert dialect_of_chain(['{"root": true}', '{"dialect": "mssql"}']) == ANSI


def test_root_file_gives_its_own_dialect():
    assert dialect_of_chain(['{"root": true, "dialect": "postgres"}', '{"dialect": "mssql"}']) == POSTGRES


def test_empty_chain_is_ansi():
    assert dialect_of_chain([]) == ANSI


def _write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def test_sibling_folders_resolve_their_own_dialects(tmp_path):
    _write(tmp_path / "pg" / "sqlsource.json", '{"dialect": "postgres"}')
    _write(tmp_path / "ms" / "sqlsource.json", '{"dialect": "mssql"}')
    pg = _write(tmp_path / "pg" / "Query.sql", "SELECT 1")
    ms = _write(tmp_path / "ms" / "Query.sql", "SELECT 1")

    assert resolve_dialect(pg) == POSTGRES
    assert resolve_dialect(ms) == MSSQL


def test_nearer_file_wins_and_root_ends_the_search(tmp_path):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    _write(tmp_path / "app" / "sqlsource.json", '{"root": true}')
    _write(tmp_path / "app" / "inner" / "sqlsource.json", "not json")
    sql = _write(tmp_path / "app" / "inner" / "deep" / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == ANSI


def test_search_goes_past_a_folder_without_a_file(tmp_path):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    sql = _write(tmp_path / "a" / "b" / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == MSSQL


def test_file_name_is_matched_exactly(tmp_path):
    _write(tmp_path / "SqlSource.json", '{"dialect": "postgres"}')
    sql = _write(tmp_path / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == ANSI


def test_unreadable_settings_file_is_absent(tmp_path):
    (tmp_path / "sqlsource.json").mkdir()
    _write(tmp_path / "sqlsource.json" / "x", "")
    _write(tmp_path / "sub" / "sqlsource.json", '{"root": true}')
    sql = _write(tmp_path / "sub" / "Query.sql", "SELECT 1")
    (tmp_path / "sub" / "sqlsource.json").chmod(0)
    try:
        assert resolve_dialect(sql) == ANSI
    finally:
        (tmp_path / "sub" / "sqlsource.json").chmod(0o644)


def test_invalid_utf8_does_not_raise(tmp_path):
    (tmp_path / "sqlsource.json").write_bytes(b'{"dialect": "postgres", "x": "\xff"}')

    assert resolve_dialect(tmp_path / "Query.sql") == POSTGRES
