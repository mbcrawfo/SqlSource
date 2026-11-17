"""The cases of `SqlTemplatingPlanTests` in `tests/SqlSource.Tests/Parsing/Templating/`, with what the C# parser
gives for each."""

import pytest

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice, DialectOptions
from sqlfluff_templater_sqlsource.errors import ParseError
from sqlfluff_templater_sqlsource.lexer import LexemeKind, Lexer
from sqlfluff_templater_sqlsource.plan import Plan, SpanKind, build_plan
from sqlfluff_templater_sqlsource.rules import DialectRules

ANSI = DialectChoice(Dialect.ANSI)
TOKEN = SpanKind.TOKEN
PARAMETER = SpanKind.PARAMETER
LIST = SpanKind.LIST
MARKER = SpanKind.MARKER_COMMENT
QUERY_END = SpanKind.QUERY_END


def plan_of(text: str, dialect: DialectChoice = ANSI) -> Plan:
    return build_plan(text, "Query.sql", dialect)


def spans(text: str, dialect: DialectChoice = ANSI, plan: Plan | None = None) -> list[tuple]:
    plan = plan or plan_of(text, dialect)
    return [(span.kind, text[span.start : span.end], span.name, span.default) for span in plan.spans]


def at(text: str, part: str) -> tuple[int, int]:
    start = text.index(part)
    return start, start + len(part)


def test_token_with_inline_default_is_one_token_span():
    assert spans("SELECT {{t:users}};") == [(TOKEN, "{{t:users}}", "t", "users")]


def test_token_over_lines_spans_its_lines():
    text = "SELECT 1 /* c */ {{f:AND a = 1\r\n    AND b = 2}}\r\n"

    assert spans(text) == [(TOKEN, "{{f:AND a = 1\r\n    AND b = 2}}", "f", "AND a = 1\n    AND b = 2")]


def test_token_default_from_marker_applies_to_its_query_only():
    text = (
        "-- name: ByA\n-- token: {{cond:a = 1}}\nSELECT * FROM t WHERE {{cond}};\n\n"
        "-- name: ByB\nSELECT * FROM t WHERE 1 = 1 {{cond:AND b = 2}};\n"
    )

    assert [span for span in spans(text) if span[0] == TOKEN] == [
        (TOKEN, "{{cond}}", "cond", "a = 1"),
        (TOKEN, "{{cond:AND b = 2}}", "cond", "AND b = 2"),
    ]


def test_ignored_name_is_no_span():
    text = "-- token-ignore: name\nSELECT 'Hello, {{name}}', {{t:users}}"

    assert spans(text) == [(MARKER, "-- token-ignore: name", None, None), (TOKEN, "{{t:users}}", "t", "users")]


def test_token_without_default_has_no_default():
    assert spans("SELECT {{t}}") == [(TOKEN, "{{t}}", "t", None)]


def test_token_with_empty_default_has_an_empty_default():
    assert spans("SELECT {{t: }}") == [(TOKEN, "{{t: }}", "t", "")]


def test_token_in_comment_is_no_span():
    assert spans("SELECT 1 /* {{note}} */ FROM {{t:users}}") == [(TOKEN, "{{t:users}}", "t", "users")]


def test_token_in_string_is_a_span():
    assert spans("SELECT '{{t}}'") == [(TOKEN, "{{t}}", "t", None)]


def test_unclosed_opener_in_comment_swallows_nothing():
    text = "SELECT * FROM t -- TODO: add {{filter: ...\nWHERE {{cond:id > 0}}\n"

    assert spans(text) == [(TOKEN, "{{cond:id > 0}}", "cond", "id > 0")]


def test_parameter_in_default_is_inside_the_token():
    text = "-- param: @page int\nSELECT * FROM t ORDER BY id {{paging:LIMIT 20 OFFSET @page}}\n"
    plan = plan_of(text)

    assert spans(text, plan=plan) == [
        (MARKER, "-- param: @page int", None, None),
        (TOKEN, "{{paging:LIMIT 20 OFFSET @page}}", "paging", "LIMIT 20 OFFSET @page"),
        (PARAMETER, "@page", "page", None),
    ]
    assert plan.spans[1].start <= plan.spans[2].start and plan.spans[2].end <= plan.spans[1].end


def test_reserved_name_is_no_token():
    plan = plan_of("SELECT {{class}}, {{t}}")

    assert spans("SELECT {{class}}, {{t}}", plan=plan) == [(TOKEN, "{{t}}", "t", None)]
    assert plan.has_other_errors


@pytest.mark.parametrize(
    ("text", "dialect", "expected"),
    [
        ("SELECT * FROM t WHERE id IN @ids AND name = @name", ANSI, [(LIST, "@ids"), (PARAMETER, "@name")]),
        ("SELECT * FROM t WHERE id NOT IN @ids", ANSI, [(LIST, "@ids")]),
        ("SELECT * FROM t WHERE id in /* c */ @ids", DialectChoice(Dialect.POSTGRESQL), [(LIST, "@ids")]),
        ("SELECT * FROM t WHERE id IN -- c\n @ids", ANSI, [(LIST, "@ids")]),
        (
            "SELECT * FROM t WHERE position('@' IN @email) > 0",
            DialectChoice(Dialect.POSTGRESQL),
            [(PARAMETER, "@email")],
        ),
        ("SELECT position(a IN b) FROM t WHERE x IN (@a, @b)", ANSI, [(PARAMETER, "@a"), (PARAMETER, "@b")]),
        ("SELECT * FROM t WHERE position((x IN @a))", ANSI, [(LIST, "@a")]),
        ("SELECT * FROM t JOIN @a", ANSI, [(PARAMETER, "@a")]),
        ("SELECT * FROM t WHERE id IN 'x' @a", ANSI, [(PARAMETER, "@a")]),
        ("SELECT * FROM t WHERE x#IN @a", DialectChoice(Dialect.SQL_SERVER), [(PARAMETER, "@a")]),
        # An operator runs on over T-SQL's name characters @ and #, so the word after it is IN.
        ("SELECT * FROM t WHERE x =#IN @a OR y IN @b", DialectChoice(Dialect.SQL_SERVER), [(LIST, "@a"), (LIST, "@b")]),
        (
            "SELECT * FROM t WHERE x =@@IN @a OR y IN @b",
            DialectChoice(Dialect.SQL_SERVER),
            [(LIST, "@a"), (LIST, "@b")],
        ),
        (
            "SELECT * FROM t WHERE x <>#in @a AND y IN @b",
            DialectChoice(Dialect.SQL_SERVER),
            [(LIST, "@a"), (LIST, "@b")],
        ),
        (
            "SELECT * FROM t WHERE x `@@IN @a OR y IN @b",
            DialectChoice(Dialect.SQL_SERVER),
            [(LIST, "@a"), (LIST, "@b")],
        ),
        ("SELECT * FROM t WHERE x =-IN @a OR y IN @b", ANSI, [(LIST, "@a"), (LIST, "@b")]),
    ],
)
def test_list_is_a_list_span(text, dialect, expected):
    assert [(kind, written) for kind, written, _, _ in spans(text, dialect)] == expected


def test_system_variable_is_no_parameter():
    assert spans("SELECT @id, @@rowcount", DialectChoice(Dialect.SQL_SERVER)) == [(PARAMETER, "@id", "id", None)]


def test_at_in_string_is_no_parameter():
    assert spans("SELECT 'a@b.c', @id") == [(PARAMETER, "@id", "id", None)]


def test_declared_variable_is_a_parameter_span():
    text = "DECLARE @n int = 1;\nSELECT @n"

    assert spans(text, DialectChoice(Dialect.SQL_SERVER)) == [
        (PARAMETER, "@n", "n", None),
        (PARAMETER, "@n", "n", None),
    ]


@pytest.mark.parametrize(
    ("text", "last_sql", "count"),
    [
        ("-- name: A\nSELECT 1 -- note\n\n-- name: B\nSELECT 2\n", "SELECT 1", 1),
        ("-- name: A\nSELECT 'a'\n-- name: B\nSELECT 2\n", "SELECT 'a'", 1),
        ("-- name: A\nSELECT 1;\n-- name: B\nSELECT 2\n", "SELECT 1", 0),
        ("-- name: A\nSELECT 1; -- note\n-- name: B\nSELECT 2;\n", "SELECT 1", 0),
        ("SELECT 1\n", "SELECT 1", 0),
    ],
)
def test_query_without_semicolon_has_query_end(text, last_sql, count):
    ends = [span for span in plan_of(text).spans if span.kind == QUERY_END]

    assert len(ends) == count
    if count:
        offset = text.index(last_sql) + len(last_sql)
        assert (ends[0].start, ends[0].end, ends[0].name, ends[0].default) == (offset, offset, None, None)


def test_query_before_an_empty_block_has_query_end():
    text = "-- name: A\nSELECT 1\n-- name: B\n"

    assert [span[0] for span in spans(text)] == [MARKER, QUERY_END, MARKER]


def test_marker_form_inside_token_is_no_marker_span():
    text = "SELECT 1 {{f:a = 1 -- summary: x\n AND b = 2}}"

    assert spans(text) == [(TOKEN, "{{f:a = 1 -- summary: x\n AND b = 2}}", "f", "a = 1\n AND b = 2")]


def test_query_end_where_a_marker_comment_starts_comes_first():
    text = "-- name: A\nSELECT 1-- summary: x\n-- name: B\nSELECT 2\n"
    plan = plan_of(text)

    assert spans(text, plan=plan) == [
        (MARKER, "-- name: A", None, None),
        (QUERY_END, "", None, None),
        (MARKER, "-- summary: x", None, None),
        (MARKER, "-- name: B", None, None),
    ]
    assert plan.spans[1].start == text.index("-- summary")


def test_hash_comment_in_marker_form_is_no_marker_span():
    assert spans("# token: {{a:x}}\nSELECT 1", DialectChoice(Dialect.MYSQL)) == []


def test_marker_form_is_protected():
    text = "-- name: Q\n-- token: {{a:x}}\n-- just a note\nSELECT {{a}} -- summary: x\n"

    assert spans(text) == [
        (MARKER, "-- name: Q", None, None),
        (MARKER, "-- token: {{a:x}}", None, None),
        (TOKEN, "{{a}}", "a", "x"),
        (MARKER, "-- summary: x", None, None),
    ]


def test_marker_default_without_its_comments_is_the_default():
    text = "-- token: {{f:a /* c */ b}}\nSELECT {{f}}"

    assert spans(text)[-1] == (TOKEN, "{{f}}", "f", "a   b")


def test_marker_default_after_the_last_sql_is_not_applied():
    plan = plan_of("-- name: Q\nSELECT {{a}}\n-- token: {{a:x}}\n")

    assert [span.default for span in plan.spans if span.kind == TOKEN] == [None]
    assert plan.has_other_errors


def test_unterminated_string_is_error():
    plan = plan_of("SELECT {{t:users}}, 'abc")

    assert plan.error == ParseError("SQLSRC101", 20, 21)
    assert plan.spans == []
    assert plan.lexemes == []
    assert not plan.has_other_errors


def test_invalid_dialect_marker_is_error():
    text = "-- dialect: nonsense\nSELECT {{t:users}}"
    plan = plan_of(text)

    assert plan.error == ParseError("SQLSRC111", *at(text, "nonsense"))
    assert plan.spans == []
    assert not plan.has_other_errors


def test_invalid_dialect_marker_before_unterminated_string_is_the_markers_error():
    plan = plan_of("-- dialect: nonsense\nSELECT 'abc")

    assert plan.error is not None and plan.error.code == "SQLSRC111"
    assert plan.has_other_errors


def test_comment_holding_close_braces_is_sqlsrc120():
    text = "-- name: Q\nSELECT 1 {{f:AND a = 1 -- note}}\nORDER BY {{o:name}}\n"
    plan = plan_of(text)

    assert plan.error == ParseError("SQLSRC120", *at(text, "-- note}}"))
    assert plan.spans == []
    assert plan.lexemes != []


@pytest.mark.parametrize(
    ("text", "comment"),
    [
        ("SELECT 1 {{f:a -- note}}", "-- note}}"),
        ("SELECT {{f:a /* }} */ b}}", "/* }} */"),
        ("SELECT {{f:a\n-- }}\n}}", "-- }}"),
        ("SELECT {{f:a}} {{g: b -- }}", "-- }}"),
        ("-- token-ignore: f\nSELECT {{f:a -- }}", "-- }}"),
        ("SELECT {{f:a -- }}\r\nORDER BY {{g:b}}\r", "-- }}"),
    ],
)
def test_comment_that_holds_braces_inside_an_open_default_is_sqlsrc120(text, comment):
    assert plan_of(text).error == ParseError("SQLSRC120", *at(text, comment))


@pytest.mark.parametrize(
    "text",
    ["SELECT 1 -- {{x}}", "SELECT {{f:a}} -- see }}", "SELECT {{f:a -- note\n}}", "SELECT {{f:a /*+ }} */ b}}"],
)
def test_comment_that_hides_no_closing_braces_is_no_error(text):
    assert plan_of(text).error is None


def test_other_error_is_best_effort():
    text = "-- name: Q\nSELECT {{t:users}}\n-- name: Q\nSELECT @id\n"
    plan = plan_of(text)

    assert plan.error is None
    assert plan.has_other_errors
    assert spans(text, plan=plan) == [
        (MARKER, "-- name: Q", None, None),
        (TOKEN, "{{t:users}}", "t", "users"),
        (QUERY_END, "", None, None),
        (MARKER, "-- name: Q", None, None),
        (PARAMETER, "@id", "id", None),
    ]


@pytest.mark.parametrize(
    "text",
    [
        "-- name: Q\nSELECT {{t:users}}\n",
        "-- dialect: postgres\n-- name: Q\n-- token: {{a:x}}\nSELECT {{a}}, {{a:x}}\n",
        "-- generator: sort-input\n-- name: Q\nSELECT 1\n",
    ],
)
def test_accepted_file_has_no_errors(text):
    plan = plan_of(text)

    assert plan.error is None
    assert not plan.has_other_errors


@pytest.mark.parametrize(
    "text",
    [
        "-- name: A\n-- name: B\nSELECT 1\n",
        "SELECT 0;\n-- name: A\nSELECT 1\n",
        "-- summary: x\n-- name: A\nSELECT 1\n",
        "-- name: 1a\nSELECT 1\n",
        "-- name: class\nSELECT 1\n",
        "-- name: A\nSELECT {{a:x}}, {{a:y}}\n",
        "-- name: A\n-- token: {{a:x}}\n-- token: {{a:y}}\nSELECT {{a}}\n",
        "-- name: A\n-- token: {{a}}\nSELECT {{a}}\n",
        "-- name: A\n-- token: {{a:'x}}\nSELECT {{a}}\n",
        "-- name: A\n-- token: {{class:x}}\nSELECT {{a}}\n",
        "-- name: A\n-- token-ignore: 1a\nSELECT 1\n",
        "-- token: {{a:x}}\n-- name: A\nSELECT {{a}}\n",
        "-- name: A\nSELECT 1\n-- dialect: postgres\n",
        "-- dialect: postgres\n-- dialect: mysql\nSELECT 1\n",
    ],
)
def test_rejected_file_has_other_errors(text):
    plan = plan_of(text, DialectChoice(Dialect.POSTGRESQL))

    assert plan.error is None
    assert plan.has_other_errors


def test_unnamed_file_with_an_unusable_name_has_other_errors():
    assert build_plan("SELECT 1", "1st.sql", ANSI).has_other_errors
    assert not build_plan("SELECT 1", "First_query.sql", ANSI).has_other_errors


def test_invalid_dialect_marker_after_a_valid_one_is_error_under_the_valid_one():
    text = "-- dialect: postgres\n-- dialect: nonsense\nSELECT 1"
    plan = plan_of(text)

    assert plan.dialect == DialectChoice(Dialect.POSTGRESQL)
    assert plan.error == ParseError("SQLSRC111", *at(text, "nonsense"))
    assert plan.spans == []


def test_dialect_marker_is_the_resolved_dialect():
    text = '-- dialect: mysql,ansi_quotes\nSELECT "a"'
    plan = plan_of(text)

    assert plan.dialect == DialectChoice(Dialect.MYSQL, DialectOptions.ANSI_QUOTES)
    assert any(lexeme.kind == LexemeKind.QUOTED and text[lexeme.start : lexeme.end] == '"a"' for lexeme in plan.lexemes)


def test_without_dialect_marker_is_the_given_dialect():
    given = DialectChoice(Dialect.MARIADB, DialectOptions.NO_BACKSLASH_ESCAPES)

    assert plan_of("SELECT 1", given).dialect == given


def test_lexemes_are_the_files_lexemes():
    text = "-- name: Q\nSELECT 1\n"

    assert plan_of(text).lexemes == Lexer(text, DialectRules.for_choice(ANSI)).read_to_end()[0]


def test_character_outside_the_bmp_counts_as_one_offset():
    text = "SELECT '\U0001f600', {{t:x}}"

    assert spans(text) == [(TOKEN, "{{t:x}}", "t", "x")]
    assert plan_of(text).spans[0].start == 12
