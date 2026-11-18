"""The cases of `TokenScannerTests` and `OpenDefaultFinderTests` in `tests/SqlSource.Tests/Parsing/Tokens/`, and the
C# identifier rules that a token's name meets."""

import re

import pytest

from sqlfluff_templater_sqlsource.identifiers import (
    is_identifier_part_character,
    is_identifier_start_character,
    is_reserved_keyword,
    is_usable_name,
    is_valid,
)
from sqlfluff_templater_sqlsource.tokens import OpenDefaultFinder, scan


def scanned(sql: str, *ignored: str) -> list[str]:
    """Each token as name=default, `<none>` for a token without one."""
    occurrences, errors = scan(sql, set(ignored))
    assert errors == []
    return [f"{token.name}={'<none>' if token.default is None else token.default}" for token in occurrences]


@pytest.mark.parametrize(
    ("value", "start", "part"),
    [
        ("a", True, True),
        ("Z", True, True),
        ("_", True, True),
        ("1", False, True),
        ("$", False, False),
        ("é", True, True),
        # A letter number, a combining mark, a connector and a format character.
        ("\u2160", True, True),
        ("\u0301", False, True),
        ("\u203f", False, True),
        ("\u200b", False, True),
        # A code point outside the BMP is two surrogates to C#.
        ("\U0001d400", False, False),
        ("\0", False, False),
    ],
)
def test_identifier_characters_are_those_of_csharp(value, start, part):
    assert is_identifier_start_character(value) == start
    assert is_identifier_part_character(value) == part


@pytest.mark.parametrize(
    ("value", "valid", "usable"),
    [
        ("a", True, True),
        ("größe", True, True),
        ("where", True, True),
        ("var", True, True),
        ("class", True, False),
        ("__arglist", True, False),
        ("", False, False),
        ("1a", False, False),
        ("a\u200b", False, False),
        ("a-b", False, False),
    ],
)
def test_names_are_valid_and_usable_as_in_csharp(value, valid, usable):
    assert is_valid(value) == valid
    assert is_usable_name(value) == usable


def test_reserved_keywords_are_those_of_csharp():
    assert all(is_reserved_keyword(word) for word in ("class", "int", "default", "null", "true", "string", "this"))
    assert not any(is_reserved_keyword(word) for word in ("where", "var", "file", "record", "Class", "extension"))


@pytest.mark.parametrize(
    ("sql", "expected"),
    [
        ("", []),
        ("SELECT 1", []),
        ("SELECT {{a}} x {{b}}", ["a=<none>", "b=<none>"]),
        ("{{a}}{{b}}{{a}}", ["a=<none>", "b=<none>", "a=<none>"]),
        ("{{ a }}", ["a=<none>"]),
        ("{{\ta\t}}", ["a=<none>"]),
        ("{{{name}}}", ["name=<none>"]),
        ("{{A}} {{a}}", ["A=<none>", "a=<none>"]),
        ("{{where}} {{_x1}} {{größe}}", ["where=<none>", "_x1=<none>", "größe=<none>"]),
        ("{{a:b}}", ["a=b"]),
        ("{{ a : b c }}", ["a=b c"]),
        ("{{a:}}", ["a="]),
        ("{{a:  }}", ["a="]),
        ("{{cast:x::int}}", ["cast=x::int"]),
        ("{{a:x\n  AND y}}", ["a=x\n  AND y"]),
        ("{{a:{b} }}", ["a={b}"]),
        ("{{a: {{b: {{c:x}}", ["a={{b: {{c:x"]),
        ("{{1a: {{b:x}}", ["b=x"]),
    ],
)
def test_scan_reads_each_token_with_its_trimmed_default(sql, expected):
    assert scanned(sql) == expected


@pytest.mark.parametrize(
    "sql",
    [
        "{{1,2},{3,4}}",
        "{{table name}}",
        "{{}}",
        "{{ }}",
        "{{1abc}}",
        "{{a.b}}",
        "{{a-b}}",
        "{{\na}}",
        "{{a\n}}",
        "{name}",
        "{ {name}}",
        "{{name}",
        "{{name} }",
        "{{\U0001f600}}",
        "{{a\u200b}}",
        "x {{",
        "x {{a",
        "x {{a}",
        "{{a:b}",
        "{{a:b",
        "{{a b:c}}",
        "{{:c}}",
        "{{a: {{b: {{c:",
        "x {{a: y {{b: z {{a: w",
    ],
)
def test_scan_of_text_that_is_not_a_token_finds_none(sql):
    assert scanned(sql) == []


def test_scan_gives_each_token_its_place():
    sql = "a {{x}} b {{y:1}} c"

    assert [sql[token.start : token.end] for token in scan(sql, set())[0]] == ["{{x}}", "{{y:1}}"]


def test_scan_leaves_an_ignored_name_out_by_its_exact_name():
    assert scanned("x {{raw:@a}} y", "raw") == []
    assert scanned("{{Skip}}", "skip") == ["Skip=<none>"]
    assert scanned("{{class}}", "class") == []


def test_scan_of_a_reserved_name_is_an_error():
    occurrences, errors = scan("x {{default:x}}", set())

    assert occurrences == []
    assert [(error.code, error.start, error.end) for error in errors] == [("SQLSRC114", 2, 15)]


# The rule of OpenDefaultFinderTests, for ASCII names: the first opener of a default after the last "}}" that ends at
# or before the offset, if it starts before it.
def expected_open(sql: str, offset: int) -> tuple[str | None, int]:
    bound = 0
    for index in range(offset - 2, -1, -1):
        if sql[index : index + 2] == "}}":
            bound = index + 2
            break
    for match in re.finditer(r"(?=\{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*:)", sql[bound:]):
        if match.start() + bound < offset:
            return match.group(1), match.start() + bound
    return None, -1


@pytest.mark.parametrize(
    "sql",
    ["{{f:a}} {{g:b }} c {{h:d ", "x}}}{{f:{{g:a}}}}{{ h :b}", "{{1:a {{f:b}}{{g:c", "{{f a {{ g\t: {{h:"],
)
def test_finder_gives_the_rules_answer_at_every_offset_in_turn(sql):
    finder = OpenDefaultFinder(sql)

    for offset in range(len(sql) + 1):
        assert (finder.at(offset), finder.opener_start) == expected_open(sql, offset), f"offset {offset}"


def test_finder_gives_the_rules_answer_at_repeated_and_skipped_offsets():
    sql = "{{f:a}} {{g:b }} c {{h:d "
    finder = OpenDefaultFinder(sql)

    for offset in (0, 3, 3, 7, 7, 15, 16, 25, 25):
        assert (finder.at(offset), finder.opener_start) == expected_open(sql, offset), f"offset {offset}"


def test_finder_refuses_an_offset_less_than_the_last():
    finder = OpenDefaultFinder("{{f:a ")
    finder.at(5)

    with pytest.raises(ValueError):
        finder.at(4)


def test_finder_gives_the_opener_of_an_ignored_name():
    assert OpenDefaultFinder("{{f:x {{g:a ").at(12) == "f"


# A name with a format character, which the scanner does not read as a token.
def test_finder_opens_no_default_for_a_name_that_is_not_valid():
    finder = OpenDefaultFinder("{{a\u200bb:x ")

    assert finder.at(8) is None
    assert finder.opener_start == -1
