"""The templater run by sqlfluff's own linter: `lint`, `fix` and `format` leave what SqlSource reads as written."""

import codecs
from pathlib import Path

import pytest
from sqlfluff.core import FluffConfig, Linter
from sqlfluff.core.linter import LintedFile

from sqlfluff_templater_sqlsource import templater as templater_module
from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.plan import SpanKind, build_plan
from sqlfluff_templater_sqlsource.templater import SqlSourceTemplater

# The rules of `sqlfluff format`, as its command sets them.
FORMAT_RULES = (
    "capitalisation,layout,ambiguous.union,convention.not_equal,convention.coalesce,"
    "convention.select_trailing_comma,convention.is_null,jinja.padding,structure.distinct"
)

# Each sqlfluff dialect of the stand-in table, with a SqlSource dialect it pairs with.
PAIRS = [
    ("tsql", "mssql"),
    ("mysql", "mysql"),
    ("mariadb", "mariadb"),
    ("sqlite", "sqlite"),
    ("duckdb", "postgres"),
    ("postgres", "postgres"),
    ("oracle", "oracle"),
    ("greenplum", "postgres"),
    ("materialize", "postgres"),
    ("redshift", "postgres"),
    ("postgres", "cockroachdb"),
    ("snowflake", "ansi"),
    ("db2", "ansi"),
    ("ansi", "ansi"),
]

PROTECTED = (SpanKind.TOKEN, SpanKind.PARAMETER, SpanKind.LIST, SpanKind.MARKER_COMMENT)


def lint(
    text: str,
    sqlfluff_dialect: str,
    folder: Path,
    *,
    fix: bool = True,
    rules: str | None = None,
    rule_configs: dict | None = None,
    max_line_length: int | None = None,
) -> LintedFile:
    overrides = {"dialect": sqlfluff_dialect, "templater": "sqlsource"}
    if rules is not None:
        overrides["rules"] = rules
    configs = {"rules": rule_configs or {}}
    if max_line_length is not None:
        configs["core"] = {"max_line_length": max_line_length}
    linter = Linter(config=FluffConfig(configs=configs, overrides=overrides))
    return linter.lint_string(text, fname=str(folder / "Query.sql"), fix=fix)


def fixed(text: str, sqlfluff_dialect: str, folder: Path, **options) -> str:
    result, _ = lint(text, sqlfluff_dialect, folder, **options).fix_string()
    return result


def codes(linted: LintedFile) -> list[str]:
    return sorted(violation.rule_code() for violation in linted.get_violations())


def protected_texts(text: str) -> list[tuple[SpanKind, str]]:
    """What SqlSource reads in a file that must not change: the text of each token, parameter, list and marker
    comment.  The file's own `-- dialect:` marker gives the dialect."""
    plan = build_plan(text, "Query.sql", DialectChoice(Dialect.ANSI))
    assert plan.error is None
    return [(span.kind, text[span.start : span.end]) for span in plan.spans if span.kind in PROTECTED]


def query_ends(text: str) -> int:
    """The number of queries without a semicolon before the next query."""
    plan = build_plan(text, "Query.sql", DialectChoice(Dialect.ANSI))
    return sum(1 for span in plan.spans if span.kind == SpanKind.QUERY_END)


MESSY = (
    "-- dialect: {sqlsource}\n"
    "-- name: First\n"
    "-- token: {{{{sort:a DESC}}}}\n"
    "SELECT a,b   from t where a = @a and c in @ids ORDER BY {{{{sort}}}}\n"
    "\n"
    "-- name: Second\n"
    "select   a from t where {{{{cond:b = @b}}}}  -- token: inline\n"
)


@pytest.mark.parametrize(("sqlfluff_dialect", "sqlsource"), PAIRS)
def test_fix_never_changes_a_token_default_parameter_list_or_marker(sqlfluff_dialect, sqlsource, tmp_path):
    text = MESSY.format(sqlsource=sqlsource)

    linted = lint(text, sqlfluff_dialect, tmp_path)
    result, _ = linted.fix_string()

    assert not [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")]
    # The keywords take the case of the first, and the spacing is fixed.
    assert "from t" not in result
    assert "where" not in result
    assert "b   " not in result
    assert "SELECT   a" not in result
    assert protected_texts(result) == protected_texts(text)
    assert query_ends(result) == query_ends(text) == 1


@pytest.mark.parametrize("rules", [None, "all", FORMAT_RULES])
@pytest.mark.parametrize(
    "terminator",
    [
        {},
        {"multiline_newline": True},
        {"require_final_semicolon": True},
        {"multiline_newline": True, "require_final_semicolon": True},
    ],
)
@pytest.mark.parametrize(
    "tail",
    ["\n", "\n\n\n", "   \n", " -- note\n\n", " /* note */\n", "\n/* note */\n", " -- token: x\n", "-- token: x\n"],
)
def test_inserted_semicolon_is_never_written(rules, terminator, tail, tmp_path):
    text = (
        "-- dialect: postgres\n"
        "-- name: OneLine\n"
        "select a from t" + tail + "-- name: Lines\n"
        "select a,\n  b from t where c = @c" + tail + "-- name: Last\n"
        "select 1\n"
    )

    result = fixed(text, "postgres", tmp_path, rules=rules, rule_configs={"convention.terminator": terminator})

    assert query_ends(result) == query_ends(text) == 2
    assert protected_texts(result) == protected_texts(text)
    # A semicolon can only be added at the end of the last query, which is the file's own.
    assert result.count(";") <= 1
    assert result.count(";") == 0 or result.rstrip().endswith(";")


@pytest.mark.parametrize("terminator", [{}, {"require_final_semicolon": True}])
@pytest.mark.parametrize("tail", ["\n", "\n\n", " -- note\n", " /* note */\n", " -- token: x\n", "-- token: x\n"])
@pytest.mark.parametrize("body", ["SELECT a FROM t", "SELECT\n    a\nFROM t"])
def test_cv06_neither_fires_nor_adds_at_an_inserted_semicolon(terminator, tail, body, tmp_path):
    text = "-- dialect: postgres\n-- name: A\n" + body + tail + "-- name: B\nSELECT 2;\n"

    linted = lint(text, "postgres", tmp_path, rules="CV06", rule_configs={"convention.terminator": terminator})

    assert codes(linted) == []
    assert linted.fix_string()[0] == text


@pytest.mark.parametrize("body", ["SELECT a FROM t", "SELECT\n    a\nFROM t"])
def test_cv06_with_semicolons_on_new_lines_never_writes_one(body, tmp_path):
    # With multiline_newline, CV06 reports that a multi-line query's inserted semicolon is not on a line of its own,
    # anchored on the end of the query's SQL, but cannot move it.
    text = "-- dialect: postgres\n-- name: A\n" + body + "\n-- name: B\nSELECT\n    2\n;\n"

    linted = lint(
        text, "postgres", tmp_path, rules="CV06", rule_configs={"convention.terminator": {"multiline_newline": True}}
    )

    assert codes(linted) == (["CV06"] if "\n" in body else [])
    assert linted.fix_string()[0] == text


@pytest.mark.parametrize(
    ("body", "tail"),
    [
        ("SELECT a FROM t", "\n"),
        ("SELECT a FROM t", " -- note\n"),
        ("SELECT\n    a\nFROM t", " -- note\n"),
        ("SELECT\n    a\nFROM t", " -- note\n\n"),
    ],
)
def test_cv06_on_new_lines_ignores_the_inserted_semicolon(body, tail, tmp_path):
    # The query's end is no user text, so its slice is untagged: a violation anchored in it is dropped as templated,
    # and fix cannot write there.  CV06 anchors a multi-line query's on the comment after its SQL.
    text = "-- dialect: postgres\n-- name: A\n" + body + tail + "-- name: B\nSELECT\n    2\n;\n"

    linted = lint(
        text, "postgres", tmp_path, rules="CV06", rule_configs={"convention.terminator": {"multiline_newline": True}}
    )

    assert codes(linted) == []
    assert linted.fix_string()[0] == text


def test_long_line_fix_never_moves_a_marker_form_comment(tmp_path):
    # LT05 moves a trailing comment onto a line of its own, as it does with sqlfluff's raw templater, where this one
    # would become a marker that SqlSource reads.
    text = "-- dialect: postgres\n-- name: A\nSELECT a FROM t WHERE e = @e -- token: {{x:a long default}}\n"

    linted = lint(
        text,
        "postgres",
        tmp_path,
        rules="LT05",
        rule_configs={"layout.long_lines": {"trailing_comments": "before"}},
        max_line_length=40,
    )

    assert codes(linted) == ["LT05"]
    assert linted.fix_string()[0] == text


def test_unclosed_comment_opener_does_not_swallow_sql(tmp_path):
    text = (
        "-- dialect: postgres\n"
        "-- name: ActiveUsers\n"
        "SELECT * FROM users -- TODO: add {{filter: ...\n"
        "WHERE {{cond:active}} /* {{note: */ AND id = @id;\n"
    )

    linted = lint(text, "postgres", tmp_path, fix=False)

    assert "WHERE active /* {{note: */ AND id = :id;" in linted.templated_file.templated_str
    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


def test_scoped_defaults_parse(tmp_path):
    text = (
        "-- dialect: postgres\n"
        "-- name: ByA\n"
        "-- token: {{cond:a = 1}}\n"
        "SELECT * FROM t WHERE {{cond}};\n"
        "\n"
        "-- name: ByB\n"
        "SELECT * FROM t WHERE 1 = 1 {{cond:AND b = 2}};\n"
        "\n"
        "-- name: ByC\n"
        "-- token: {{cond:c = 3}}\n"
        "SELECT * FROM t WHERE {{cond}};\n"
    )

    linted = lint(text, "postgres", tmp_path, fix=False)

    templated = linted.templated_file.templated_str
    assert "WHERE a = 1;" in templated
    assert "WHERE 1 = 1 AND b = 2;" in templated
    assert "WHERE c = 3;" in templated
    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


def test_parameter_in_default_parses_in_postgres(tmp_path):
    text = (
        "-- dialect: postgres\n"
        "-- name: Page\n"
        "-- param: @page int\n"
        "SELECT * FROM t ORDER BY id {{paging:LIMIT 20 OFFSET @page}};\n"
    )

    linted = lint(text, "postgres", tmp_path, fix=False)

    assert "ORDER BY id LIMIT 20 OFFSET :page;" in linted.templated_file.templated_str
    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


def test_non_ascii_token_name_parses(tmp_path):
    text = "-- dialect: postgres\n-- name: Outer\nSELECT * FROM {{Äußere:users}} WHERE {{Ä:1 = 1}} AND {{naïve}};\n"

    linted = lint(text, "postgres", tmp_path, fix=False)

    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


STAND_IN_QUERY = (
    "-- dialect: {sqlsource}\n"
    "-- name: Find\n"
    "SELECT\n"
    "    a,\n"
    "    {select} AS b\n"
    "FROM t\n"
    "WHERE\n"
    "    a = {equal}\n"
    "    AND b LIKE {like}\n"
    "    AND c IN {list}\n"
    "{limit};\n"
)

LIMITS = {
    "tsql": "ORDER BY a OFFSET {value} ROWS FETCH NEXT {value} ROWS ONLY",
    "oracle": "ORDER BY a FETCH FIRST {value} ROWS ONLY",
    "db2": "ORDER BY a FETCH FIRST {value} ROWS ONLY",
}


@pytest.mark.parametrize(("sqlfluff_dialect", "sqlsource"), PAIRS)
def test_stand_in_sets_off_no_rule(sqlfluff_dialect, sqlsource, tmp_path):
    limit = LIMITS.get(sqlfluff_dialect, "ORDER BY a LIMIT {value}")
    with_parameters = STAND_IN_QUERY.format(
        sqlsource=sqlsource,
        select="@s",
        equal="@e",
        like="@pattern",
        list="@ids",
        limit=limit.format(value="@n"),
    )
    with_literals = STAND_IN_QUERY.format(
        sqlsource=sqlsource, select="1", equal="1", like="'x'", list="(1)", limit=limit.format(value="10")
    )

    found = lint(with_parameters, sqlfluff_dialect, tmp_path, fix=False, rules="all")
    expected = lint(with_literals, sqlfluff_dialect, tmp_path, fix=False, rules="all")

    found_codes = {(violation.rule_code(), violation.line_no) for violation in found.get_violations()}
    expected_codes = {(violation.rule_code(), violation.line_no) for violation in expected.get_violations()}
    assert found_codes - expected_codes == set()
    assert not [code for code in codes(found) if code in ("PRS", "TMP", "LXR")]


@pytest.mark.parametrize(
    ("sqlfluff_dialect", "sqlsource"),
    [("postgres", "postgres"), ("mysql", "mysql"), ("oracle", "oracle"), ("tsql", "mssql")],
)
def test_parameter_with_a_name_the_dialect_cannot_lex_parses(sqlfluff_dialect, sqlsource, tmp_path):
    text = f"-- dialect: {sqlsource}\n-- name: Q\nSELECT a FROM t WHERE a = @名前 AND b = @_x;\n"

    linted = lint(text, sqlfluff_dialect, tmp_path, fix=False)

    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


def test_crlf_file_keeps_its_tokens(tmp_path):
    text = (
        "-- dialect: postgres\r\n"
        "-- name: A\r\n"
        "select   {{t:users}}\r\n"
        "\r\n"
        "-- name: B\r\n"
        "select * from t where 1 = 1 {{f:AND a = 1\r\n"
        "    AND b = 2}}\r\n"
        "    and id = @id\r\n"
    )
    normalised = text.replace("\r\n", "\n")

    linted = lint(text, "postgres", tmp_path)
    result, _ = linted.fix_string()

    assert "\r" not in linted.templated_file.source_str
    assert "users;\n" in linted.templated_file.templated_str
    assert result != normalised
    assert protected_texts(result) == protected_texts(normalised)
    assert query_ends(result) == 1


def test_bom_file_is_not_shifted(tmp_path):
    # sqlfluff reads a file with a byte order mark as utf-8-sig, so the templater never sees the mark, and writes it
    # back when it fixes the file.
    text = "-- dialect: mssql\n-- name: Q\nselect [{{a}}],   {{t:users}} from t where id = @id\n"
    path = tmp_path / "Query.sql"
    path.write_bytes(codecs.BOM_UTF8 + text.encode("utf-8"))
    linter = Linter(config=FluffConfig(overrides={"dialect": "tsql", "templater": "sqlsource"}))

    linter.lint_paths((str(path),), fix=True, apply_fixes=True)

    written = path.read_bytes()
    assert written.startswith(codecs.BOM_UTF8)
    result = written[len(codecs.BOM_UTF8) :].decode("utf-8")
    assert result != text
    assert protected_texts(result) == protected_texts(text)


def test_bom_in_the_text_is_not_part_of_the_plan(tmp_path):
    # With `encoding = utf-8` sqlfluff keeps the mark in the text it hands the templater.  SqlSource never reads the
    # mark, so the plan is that of the text after it, whose -- dialect: marker is read: in MySQL, # starts a comment
    # and {{x}} after it is no token.
    text = "\ufeff-- dialect: mysql\n-- name: Q\nSELECT {{t:users}} FROM t WHERE id = @id # {{x}}\n"
    config = FluffConfig(overrides={"dialect": "mysql", "templater": "sqlsource"})

    templated, errors = SqlSourceTemplater().process(in_str=text, fname=str(tmp_path / "Query.sql"), config=config)

    assert errors == []
    assert (
        templated.templated_str == "\ufeff-- dialect: mysql\n-- name: Q\nSELECT users FROM t WHERE id = @id # {{x}}\n"
    )
    sources = [
        templated.source_str[part.source_slice] for part in templated.sliced_file if part.slice_type == "templated"
    ]
    assert sources == ["-- dialect: mysql", "-- name: Q", "{{t:users}}", "@id"]


def test_rejected_file_lints_without_crash(tmp_path):
    # Files SqlSource rejects with an error that does not stop the plan get a best-effort plan and no templating error.
    rejected = [
        "SELECT 1;\n-- name: A\nSELECT {{t:x}};\n",
        "-- name: A\nSELECT 1;\n-- name: A\nSELECT 2;\n",
        "-- name: A\n-- token: {{f:a = 1}}\nSELECT * FROM t WHERE {{f:b = 2}};\n",
        "-- name: A\n-- name: B\nSELECT 1;\n",
        "-- name: A\nSELECT 1;\n-- summary: late\n",
        "-- name: 1st\nSELECT {{t:x}} FROM t WHERE {{f:a = @undeclared}};\n",
    ]
    for text in rejected:
        linted = lint(text, "postgres", tmp_path)

        assert "TMP" not in codes(linted), text
        assert linted.fix_string()[0].count("{{") == text.count("{{")


def test_failure_inside_the_templater_is_a_templating_error(tmp_path, monkeypatch):
    def fail(*_args, **_kwargs):
        raise ValueError("boom")

    monkeypatch.setattr(templater_module, "build_plan", fail)

    linted = lint("-- name: A\nSELECT 1;\n", "postgres", tmp_path)

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    assert violations[0].desc().startswith("sqlsource templater failed: ")
    assert "boom" in violations[0].desc()


def test_dialect_mismatch_is_a_templating_error(tmp_path):
    linted = lint("-- dialect: postgres\n-- name: A\nSELECT 1;\n", "tsql", tmp_path)

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    assert "postgres" in violations[0].desc()
    assert "tsql" in violations[0].desc()
    assert linted.templated_file is None


def test_dialect_from_a_settings_file_is_checked_too(tmp_path):
    (tmp_path / "sqlsource.json").write_text('{ "dialect": "mssql" }', encoding="utf-8")

    linted = lint("-- name: A\nSELECT 1;\n", "postgres", tmp_path)

    assert [violation.rule_code() for violation in linted.get_violations()] == ["TMP"]
    assert "mssql" in linted.get_violations()[0].desc()


@pytest.mark.parametrize(
    ("text", "code", "line", "position"),
    [
        (
            "-- dialect: postgres\n-- name: Q\nSELECT 1 {{f:AND a = 1 -- note}}\nORDER BY {{o:name}};\n",
            "SQLSRC120",
            3,
            24,
        ),
        ("-- dialect: postgres\n-- name: Q\nSELECT {{t:users}}, 'abc;\n", "SQLSRC101", 3, 21),
        ("-- dialect: postgres\n-- name: Q\nSELECT 1 /* open\n", "SQLSRC102", 3, 10),
        ("-- dialect: nonsense\n-- name: Q\nSELECT {{t:users}};\n", "SQLSRC111", 1, 13),
        ("﻿-- dialect: nonsense\n-- name: Q\nSELECT 1;\n", "SQLSRC111", 1, 14),
    ],
)
def test_stopping_error_names_its_code(text, code, line, position, tmp_path):
    linted = lint(text, "postgres", tmp_path)

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    assert violations[0].desc().startswith(code + ": ")
    assert (violations[0].line_no, violations[0].line_pos) == (line, position)
    assert linted.templated_file is None


def test_empty_file_lints(tmp_path):
    linted = lint("", "postgres", tmp_path)

    assert codes(linted) == []
