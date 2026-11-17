import os
import sys
from pathlib import Path

import pytest
from corpus_reader import CORPUS_DIRECTORY, corpus_cases

from sqlfluff_templater_sqlsource import settings
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


def _folders_read(monkeypatch) -> list[str]:
    read: list[str] = []
    original = settings._read_file

    def counting(folder: str) -> str | None:
        read.append(folder)
        return original(folder)

    monkeypatch.setattr(settings, "_read_file", counting)
    return read


def test_no_settings_file_above_a_root_is_read(tmp_path, monkeypatch):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    _write(tmp_path / "app" / "sqlsource.json", '{"root": true}')
    sql = _write(tmp_path / "app" / "inner" / "Query.sql", "SELECT 1")
    read = _folders_read(monkeypatch)

    assert resolve_dialect(sql) == ANSI
    assert read == [os.path.abspath(tmp_path / "app" / "inner"), os.path.abspath(tmp_path / "app")]


def test_no_settings_file_above_the_nearest_dialect_is_read(tmp_path, monkeypatch):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    _write(tmp_path / "app" / "sqlsource.json", '{"dialect": "postgres"}')
    sql = _write(tmp_path / "app" / "Query.sql", "SELECT 1")
    read = _folders_read(monkeypatch)

    assert resolve_dialect(sql) == POSTGRES
    assert read == [os.path.abspath(tmp_path / "app")]


def test_search_goes_past_a_folder_without_a_file(tmp_path):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    sql = _write(tmp_path / "a" / "b" / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == MSSQL


def test_file_name_is_matched_exactly(tmp_path):
    _write(tmp_path / "SqlSource.json", '{"dialect": "postgres"}')
    sql = _write(tmp_path / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == ANSI


@pytest.mark.skipif(sys.platform == "win32" or os.geteuid() == 0, reason="needs a file that cannot be read")
def test_unreadable_settings_file_is_absent(tmp_path):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    unreadable = _write(tmp_path / "sub" / "sqlsource.json", '{"root": true}')
    sql = _write(tmp_path / "sub" / "Query.sql", "SELECT 1")
    unreadable.chmod(0)
    try:
        assert resolve_dialect(sql) == MSSQL
    finally:
        unreadable.chmod(0o644)


def test_folder_named_like_the_settings_file_is_absent(tmp_path):
    _write(tmp_path / "sqlsource.json", '{"dialect": "mssql"}')
    (tmp_path / "sub" / "sqlsource.json").mkdir(parents=True)
    sql = _write(tmp_path / "sub" / "Query.sql", "SELECT 1")

    assert resolve_dialect(sql) == MSSQL


def test_path_with_nul_does_not_raise():
    assert resolve_dialect(Path("a\0b/Query.sql")) == ANSI


def test_byte_order_mark_is_not_part_of_the_text(tmp_path):
    (tmp_path / "sqlsource.json").write_bytes(b'\xef\xbb\xbf{"dialect": "postgres"}')

    assert resolve_dialect(tmp_path / "Query.sql") == POSTGRES


@pytest.mark.parametrize("encoding", ["utf-16-le", "utf-16-be", "utf-32-le", "utf-32-be"])
def test_file_is_decoded_by_its_byte_order_mark(tmp_path, encoding):
    marks = {
        "utf-16-le": b"\xff\xfe",
        "utf-16-be": b"\xfe\xff",
        "utf-32-le": b"\xff\xfe\x00\x00",
        "utf-32-be": b"\x00\x00\xfe\xff",
    }
    (tmp_path / "sqlsource.json").write_bytes(marks[encoding] + '{"dialect": "mssql"}'.encode(encoding))

    assert resolve_dialect(tmp_path / "Query.sql") == MSSQL


def test_control_character_is_not_trimmed_from_a_dialect():
    # .NET's Trim does not remove U+001C, which Python's strip does.
    assert read_settings('{"dialect": "\u001cpostgres"}').dialect is None


def test_first_root_wins():
    assert read_settings('{"root": true, "root": false}').root is True


@pytest.mark.parametrize("number", ["1.5e+3", "-0", "0", "10", "2E-2", "0.5"])
def test_valid_numbers_are_accepted(number):
    assert read_settings('{"a": ' + number + ', "root": true}').root is True


@pytest.mark.parametrize("number", ["1.", ".5", "-", "1e", "1e+", "+1", "00"])
def test_invalid_numbers_are_rejected(number):
    assert read_settings('{"a": ' + number + ', "root": true}').root is None


def test_slash_that_starts_no_comment_is_invalid():
    assert read_settings('{"root": true / }').root is None
    assert read_settings('{"root": true} /').root is None


def test_line_comment_ends_at_a_carriage_return():
    assert read_settings('{"root": true // note\r, "dialect": "mssql"}') == SettingsDialect(True, MSSQL)


def test_invalid_utf8_does_not_raise(tmp_path):
    (tmp_path / "sqlsource.json").write_bytes(b'{"dialect": "postgres", "x": "\xff"}')

    assert resolve_dialect(tmp_path / "Query.sql") == POSTGRES
