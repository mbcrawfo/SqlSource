"""The templater run by sqlfluff's own linter: `lint`, `fix` and `format` leave what SqlSource reads as written."""

import codecs
from collections import Counter
from pathlib import Path

import pytest
from corpus_reader import diagnostic_titles
from sqlfluff.core import FluffConfig, Linter
from sqlfluff.core.linter import LintedFile
from test_stand_ins import KEPT

from sqlfluff_templater_sqlsource import templater as templater_module
from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.plan import SpanKind, build_plan
from sqlfluff_templater_sqlsource.stand_ins import LITERAL, stand_in
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


def lint_raw(text: str, sqlfluff_dialect: str) -> LintedFile:
    linter = Linter(config=FluffConfig(overrides={"dialect": sqlfluff_dialect, "templater": "raw", "rules": "all"}))
    return linter.lint_string(text)


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


# Every protected part is messy too, so that a fix that reached inside one would change it: lower-case keywords and
# bad spacing in an inline default, in a marker's default, in a default with a list, and in marker-form comments.
MESSY = (
    "-- dialect: {sqlsource}\n"
    "-- name: First\n"
    "--   token:   {{{{sort:a   desc}}}}\n"
    "SELECT a,b   from t where a = @a and c in @ids ORDER BY {{{{sort}}}}\n"
    "\n"
    "-- name: Second\n"
    "select   a from t where {{{{cond:b=@b   and   c=1 and x in @xs}}}}  --   token:   inline\n"
)


@pytest.mark.parametrize("rules", [None, "all"])
@pytest.mark.parametrize(("sqlfluff_dialect", "sqlsource"), PAIRS)
def test_fix_never_changes_a_token_default_parameter_list_or_marker(sqlfluff_dialect, sqlsource, rules, tmp_path):
    text = MESSY.format(sqlsource=sqlsource)

    linted = lint(text, sqlfluff_dialect, tmp_path, rules=rules)
    result, _ = linted.fix_string()

    assert not [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")]
    # The keywords take the case of the first, and the spacing is fixed, outside the protected parts.
    assert "from t" not in result
    assert " where " not in result
    assert "b   from" not in result
    assert "select   a" not in result
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

    # Positions are not compared: a stand-in is not as long as the literal it is compared with.
    found_codes = Counter((violation.rule_code(), violation.line_no) for violation in found.get_violations())
    expected_codes = Counter((violation.rule_code(), violation.line_no) for violation in expected.get_violations())
    assert found_codes - expected_codes == Counter()
    assert not [code for code in codes(found) if code in ("PRS", "TMP", "LXR")]


@pytest.mark.parametrize(("sqlfluff_dialect", "name"), [(dialect, name) for dialect in KEPT for name in KEPT[dialect]])
def test_stand_in_of_a_kept_name_sets_off_no_rule(sqlfluff_dialect, name):
    # The stand-in is linted as sqlfluff's raw templater hands it on, since SqlSource reads no `$` in a name but in
    # mssql, nor a name after a colon.
    limit = LIMITS.get(sqlfluff_dialect, "ORDER BY a LIMIT {value}")
    value = stand_in(name, sqlfluff_dialect)
    assert value != LITERAL
    query = STAND_IN_QUERY.replace("-- dialect: {sqlsource}\n", "")
    with_stand_in = query.format(
        select=value, equal=value, like=value, list=f"({value})", limit=limit.format(value=value)
    )
    with_literals = query.format(select="1", equal="1", like="'x'", list="(1)", limit=limit.format(value="10"))

    found = lint_raw(with_stand_in, sqlfluff_dialect)
    expected = lint_raw(with_literals, sqlfluff_dialect)

    found_codes = Counter((violation.rule_code(), violation.line_no) for violation in found.get_violations())
    expected_codes = Counter((violation.rule_code(), violation.line_no) for violation in expected.get_violations())
    assert found_codes - expected_codes == Counter()
    assert not [code for code in codes(found) if code in ("PRS", "TMP", "LXR")]


@pytest.mark.parametrize("name", ["_x", "x$y", "x#y", "tötal", "名前"])
@pytest.mark.parametrize(
    "body",
    [
        "DECLARE @{name} INT;\nSET @{name} = 1;\nSELECT @{name};\n",
        "DECLARE @{name} TABLE (a INT);\nSELECT a FROM @{name};\n",
    ],
)
def test_tsql_variable_keeps_its_name(name, body, tmp_path):
    # A literal cannot stand where T-SQL declares, sets or reads a table from a variable.
    text = "-- dialect: mssql\n-- name: Q\n" + body.format(name=name)

    linted = lint(text, "tsql", tmp_path, fix=False)

    assert linted.templated_file.templated_str == text
    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


@pytest.mark.parametrize(
    ("sqlfluff_dialect", "sqlsource", "name"),
    [
        ("postgres", "postgres", "名前"),
        ("postgres", "postgres", "1st"),
        ("duckdb", "postgres", "名前"),
        ("mysql", "mysql", "名前"),
        ("sqlite", "sqlite", "名前"),
        ("oracle", "oracle", "名前"),
        ("oracle", "oracle", "_x"),
        ("tsql", "mssql", "x٣"),
        ("tsql", "mssql", "xⅫ"),
    ],
)
def test_parameter_with_a_name_the_dialect_cannot_lex_parses(sqlfluff_dialect, sqlsource, name, tmp_path):
    text = f"-- dialect: {sqlsource}\n-- name: Q\nSELECT a FROM t WHERE a = @{name} AND b IN @{name};\n"

    linted = lint(text, sqlfluff_dialect, tmp_path, fix=False)

    assert "WHERE a = 0 AND b IN (0);" in linted.templated_file.templated_str
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
        assert protected_texts(linted.fix_string()[0]) == protected_texts(text), text


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


# Queries that sqlfluff's mysql and mariadb read otherwise than the server does under the option.  Under ansi-quotes
# `"order"` and `"x"` are identifiers, which sqlfluff reads as strings, so CV10 would turn `'active'` into the
# identifier `"active"`.  Under no-backslash-escapes `'C:\'` is closed, which sqlfluff reads as open.
ANSI_QUOTES_QUERY = '-- name: A\nSELECT "order" FROM orders WHERE status = \'active\' AND note = "x";\n'
NO_BACKSLASH_ESCAPES_QUERY = "-- name: A\nSELECT 'C:\\' AS p, 'x' AS y FROM t;\n"


@pytest.mark.parametrize("sqlfluff_dialect", ["mysql", "mariadb"])
@pytest.mark.parametrize("sqlsource", ["mysql", "mariadb"])
@pytest.mark.parametrize(
    ("options", "query", "reasons"),
    [
        ("ansi_quotes", ANSI_QUOTES_QUERY, ['ansi-quotes makes "..." a quoted identifier']),
        ("no_backslash_escapes", NO_BACKSLASH_ESCAPES_QUERY, ["no-backslash-escapes makes a backslash"]),
        (
            "ansi_quotes,no_backslash_escapes",
            ANSI_QUOTES_QUERY,
            ['ansi-quotes makes "..." a quoted identifier', "no-backslash-escapes makes a backslash"],
        ),
    ],
    ids=["ansi_quotes", "no_backslash_escapes", "both"],
)
def test_option_sqlfluff_reads_otherwise_is_a_templating_error(
    sqlfluff_dialect, sqlsource, options, query, reasons, tmp_path
):
    (tmp_path / "sqlsource.json").write_text(
        f'{{ "root": true, "dialect": "{sqlsource},{options}" }}', encoding="utf-8"
    )

    linted = lint(query, sqlfluff_dialect, tmp_path, rules="all")

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    description = violations[0].desc()
    assert description.startswith(f"The SqlSource dialect {sqlsource} with ")
    assert f"cannot be linted with the sqlfluff dialect {sqlfluff_dialect}, or with any other: " in description
    for reason in reasons:
        assert reason in description
    assert linted.templated_file is None


def test_option_of_a_dialect_marker_is_a_templating_error(tmp_path):
    (tmp_path / "sqlsource.json").write_text('{ "root": true, "dialect": "mysql" }', encoding="utf-8")

    linted = lint("-- dialect: mysql, ansi-quotes\n" + ANSI_QUOTES_QUERY, "mysql", tmp_path)

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    assert violations[0].desc().startswith("The SqlSource dialect mysql with ansi-quotes cannot be linted")


@pytest.mark.parametrize("sqlfluff_dialect", ["mysql", "mariadb"])
@pytest.mark.parametrize("sqlsource", ["mysql", "mariadb"])
def test_mysql_and_mariadb_without_an_option_pair_with_both(sqlfluff_dialect, sqlsource, tmp_path):
    (tmp_path / "sqlsource.json").write_text(f'{{ "root": true, "dialect": "{sqlsource}" }}', encoding="utf-8")

    linted = lint(ANSI_QUOTES_QUERY, sqlfluff_dialect, tmp_path, rules="all")

    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


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


def test_stopping_errors_take_the_titles_of_the_diagnostics():
    assert diagnostic_titles() == templater_module._TITLES


def test_empty_file_lints(tmp_path):
    linted = lint("", "postgres", tmp_path)

    assert codes(linted) == []


# sqlfluff reads a file with universal newlines, so the templater reads the file itself to find a carriage return
# alone, which these dialects read as no line end: sqlfluff would lint and fix the comment's text after it as SQL.
@pytest.mark.parametrize(
    ("sqlfluff_dialect", "sqlsource"),
    [("mysql", "mysql"), ("mariadb", "mariadb"), ("sqlite", "sqlite"), ("postgres", "cockroachdb")],
)
def test_carriage_return_alone_under_a_dialect_that_ends_no_line_there_is_a_templating_error(
    sqlfluff_dialect, sqlsource, tmp_path
):
    (tmp_path / "sqlsource.json").write_text(f'{{ "root": true, "dialect": "{sqlsource}" }}', encoding="utf-8")
    raw = "-- name: A\nSELECT 1 AS a, -- c\r 2 AS b\nFROM t;\n"
    (tmp_path / "Query.sql").write_bytes(raw.encode("utf-8"))

    linted = lint(raw.replace("\r", "\n"), sqlfluff_dialect, tmp_path)

    violations = linted.get_violations()
    assert [violation.rule_code() for violation in violations] == ["TMP"]
    assert violations[0].desc().startswith(f"The SqlSource dialect {sqlsource} ends a line only at a line feed")
    assert (violations[0].line_no, violations[0].line_pos) == (2, 20)
    assert linted.templated_file is None


@pytest.mark.parametrize(
    ("sqlsource", "raw"),
    [
        # A dialect that ends a line at a carriage return alone reads the file as sqlfluff does.
        ("postgres", "-- name: A\nSELECT 1 AS a -- c\rFROM t;\n"),
        # A carriage return and a line feed end a line in every dialect.
        ("mysql", "-- name: A\r\nSELECT 1 AS a -- c\r\nFROM t;\r\n"),
    ],
)
def test_carriage_return_that_ends_a_line_for_the_dialect_is_linted(sqlsource, raw, tmp_path):
    (tmp_path / "sqlsource.json").write_text(f'{{ "root": true, "dialect": "{sqlsource}" }}', encoding="utf-8")
    (tmp_path / "Query.sql").write_bytes(raw.encode("utf-8"))

    linted = lint(
        raw.replace("\r\n", "\n").replace("\r", "\n"), "mysql" if sqlsource == "mysql" else "postgres", tmp_path
    )

    assert [code for code in codes(linted) if code in ("PRS", "TMP", "LXR")] == []


# The file is decoded as SqlSource decodes it, by its byte order mark: a line feed after a carriage return is found in
# every encoding, and a carriage return alone too.
@pytest.mark.parametrize(
    "encoding", ["utf-8", "utf-8-sig", "utf-16", "utf-16-le", "utf-16-be", "utf-32", "utf-32-le", "utf-32-be"]
)
@pytest.mark.parametrize(("line_end", "tmp"), [("\r\n", False), ("\r", True)])
def test_carriage_return_is_found_in_every_encoding(encoding, line_end, tmp, tmp_path):
    (tmp_path / "sqlsource.json").write_text('{ "root": true, "dialect": "mysql" }', encoding="utf-8")
    raw = f"-- name: A{line_end}SELECT 1 AS a -- c{line_end}FROM t;\n"
    # A codec without a byte order mark of its own gets one, as an editor writes it.
    bom = {
        "utf-16-le": codecs.BOM_UTF16_LE,
        "utf-16-be": codecs.BOM_UTF16_BE,
        "utf-32-le": codecs.BOM_UTF32_LE,
        "utf-32-be": codecs.BOM_UTF32_BE,
    }
    (tmp_path / "Query.sql").write_bytes(bom.get(encoding, b"") + raw.encode(encoding))

    linted = lint(raw.replace("\r\n", "\n").replace("\r", "\n"), "mysql", tmp_path)

    found = [violation for violation in linted.get_violations() if violation.rule_code() == "TMP"]
    assert bool(found) == tmp
    if tmp:
        assert (found[0].line_no, found[0].line_pos) == (1, 11)
