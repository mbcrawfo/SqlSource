"""Each rendering rule on a small plan: what sqlfluff sees, and which part of the file each templated slice stands
for."""

from sqlfluff.core.templaters import TemplatedFile

from sqlfluff_templater_sqlsource.dialects import Dialect, DialectChoice
from sqlfluff_templater_sqlsource.plan import Plan, SpanKind, build_plan
from sqlfluff_templater_sqlsource.render import render

ANSI = DialectChoice(Dialect.ANSI)
POSTGRES = DialectChoice(Dialect.POSTGRESQL)
MSSQL = DialectChoice(Dialect.SQL_SERVER)


def plan_of(text: str, dialect: DialectChoice = ANSI) -> Plan:
    plan = build_plan(text, "Query.sql", dialect)
    assert plan.error is None
    return plan


def render_text(text: str, sqlfluff_dialect: str = "postgres", dialect: DialectChoice = POSTGRES) -> TemplatedFile:
    return render(text, plan_of(text, dialect), sqlfluff_dialect, "Query.sql")


def templated_slices(templated: TemplatedFile) -> list[tuple[str, str]]:
    """Each templated slice as the source text it covers and the text sqlfluff sees for it."""
    return [
        (templated.source_str[part.source_slice], templated.templated_str[part.templated_slice])
        for part in templated.sliced_file
        if part.slice_type == "templated"
    ]


def assert_consistent(templated: TemplatedFile) -> None:
    """The slices cover the source and the templated text in order, and each literal slice is the same in both."""
    source_end = 0
    templated_end = 0
    for part, raw in zip(templated.sliced_file, templated.raw_sliced, strict=True):
        assert part.source_slice.start == source_end == raw.source_idx
        assert part.templated_slice.start == templated_end
        assert part.slice_type == raw.slice_type
        assert raw.raw == templated.source_str[part.source_slice]
        if part.slice_type == "literal":
            assert templated.source_str[part.source_slice] == templated.templated_str[part.templated_slice]
            assert part.source_slice != slice(source_end, source_end)
        source_end = part.source_slice.stop
        templated_end = part.templated_slice.stop
    assert source_end == len(templated.source_str)
    assert templated_end == len(templated.templated_str)


def test_token_renders_its_default_trimmed():
    templated = render_text("SELECT * FROM {{t:  users  }};")

    assert templated.templated_str == "SELECT * FROM users;"
    assert templated_slices(templated) == [("{{t:  users  }}", "users")]
    assert_consistent(templated)


def test_token_without_default_renders_its_name():
    templated = render_text("SELECT * FROM {{table}};")

    assert templated.templated_str == "SELECT * FROM table;"
    assert templated_slices(templated) == [("{{table}}", "table")]


def test_token_with_empty_default_renders_nothing():
    templated = render_text("SELECT * FROM t {{extraWhere:}};")

    assert templated.templated_str == "SELECT * FROM t ;"
    assert templated_slices(templated) == [("{{extraWhere:}}", "")]
    assert_consistent(templated)


def test_token_takes_the_default_of_its_marker():
    text = "-- name: Q\n-- token: {{cond:a = 1}}\nSELECT * FROM t WHERE {{cond}};\n"

    templated = render_text(text)

    assert templated.templated_str == "-- name: Q\n-- token: {{cond:a = 1}}\nSELECT * FROM t WHERE a = 1;\n"
    assert ("{{cond}}", "a = 1") in templated_slices(templated)


def test_parameter_in_a_default_is_part_of_the_token_slice():
    text = "SELECT * FROM t ORDER BY id {{paging:LIMIT 20 OFFSET @page}};"

    templated = render_text(text)

    assert templated.templated_str == "SELECT * FROM t ORDER BY id LIMIT 20 OFFSET :page;"
    assert templated_slices(templated) == [("{{paging:LIMIT 20 OFFSET @page}}", "LIMIT 20 OFFSET :page")]
    assert_consistent(templated)


def test_list_in_a_default_is_a_list():
    templated = render_text("SELECT * FROM t WHERE 1 = 1 {{f:AND id IN @ids AND b = @b}};")

    assert templated_slices(templated) == [("{{f:AND id IN @ids AND b = @b}}", "AND id IN (:ids) AND b = :b")]


def test_parameter_in_a_marker_default_is_swapped():
    text = "-- name: Q\n-- token: {{f:AND a = @a}}\nSELECT * FROM t WHERE 1 = 1 {{f}};\n"

    templated = render_text(text)

    assert ("{{f}}", "AND a = :a") in templated_slices(templated)


def test_list_in_a_marker_default_is_a_list():
    text = "-- name: Q\n-- token: {{f:AND id IN @ids}}\nSELECT * FROM t WHERE 1 = 1 {{f}};\n"

    templated = render_text(text)

    assert ("{{f}}", "AND id IN (:ids)") in templated_slices(templated)


def test_default_with_a_comment_renders_without_it():
    text = "SELECT * FROM t WHERE {{f:a /* note */ = @a}};"

    templated = render_text(text)

    assert templated_slices(templated) == [("{{f:a /* note */ = @a}}", "a   = :a")]


def test_default_is_lexed_by_the_dialect_of_the_plan():
    # In SQL Server, [@x] is a quoted name and no parameter.
    text = "SELECT * FROM t WHERE {{f:[@x] = @y}};"

    templated = render_text(text, "tsql", MSSQL)

    assert templated_slices(templated) == [("{{f:[@x] = @y}}", "[@x] = @y")]


def test_parameter_renders_the_stand_in_of_the_sqlfluff_dialect():
    text = "SELECT * FROM t WHERE id = @id;"

    assert render_text(text, "postgres").templated_str == "SELECT * FROM t WHERE id = :id;"
    assert render_text(text, "tsql", ANSI).templated_str == "SELECT * FROM t WHERE id = @id;"
    assert render_text(text, "ansi", ANSI).templated_str == "SELECT * FROM t WHERE id = 0;"
    assert templated_slices(render_text(text)) == [("@id", ":id")]


def test_list_renders_its_stand_in_in_parentheses():
    templated = render_text("SELECT * FROM t WHERE id IN @ids;")

    assert templated.templated_str == "SELECT * FROM t WHERE id IN (:ids);"
    assert templated_slices(templated) == [("@ids", "(:ids)")]
    assert_consistent(templated)


def test_marker_comment_renders_as_itself_in_a_templated_slice():
    text = "-- name: Q\nSELECT 1 -- token: inline\n"

    templated = render_text(text)

    assert templated.templated_str == text
    assert templated_slices(templated) == [("-- name: Q", "-- name: Q"), ("-- token: inline", "-- token: inline")]
    assert_consistent(templated)


def test_query_end_renders_a_semicolon_before_the_text_up_to_the_next_marker():
    text = "-- name: A\nSELECT 1 -- note\n\n-- name: B\nSELECT 2\n"

    templated = render_text(text)

    assert templated.templated_str == "-- name: A\nSELECT 1; -- note\n\n-- name: B\nSELECT 2\n"
    assert templated_slices(templated) == [
        ("-- name: A", "-- name: A"),
        (" -- note\n\n", "; -- note\n\n"),
        ("-- name: B", "-- name: B"),
    ]
    assert_consistent(templated)


def test_query_end_stops_at_a_marker_comment_after_the_query():
    text = "-- name: A\nSELECT 1 -- token: x\n-- name: B\nSELECT 2\n"

    templated = render_text(text)

    assert templated.templated_str == "-- name: A\nSELECT 1; -- token: x\n-- name: B\nSELECT 2\n"
    assert templated_slices(templated)[1:3] == [(" ", "; "), ("-- token: x", "-- token: x")]
    assert_consistent(templated)


def test_query_end_right_before_a_marker_comment_has_an_empty_source():
    text = "-- name: A\nSELECT 1-- token: x\n-- name: B\nSELECT 2\n"

    templated = render_text(text)

    assert templated.templated_str == "-- name: A\nSELECT 1;-- token: x\n-- name: B\nSELECT 2\n"
    assert templated_slices(templated)[1:3] == [("", ";"), ("-- token: x", "-- token: x")]
    end = text.index("-- token")
    assert [part.source_slice for part in templated.sliced_file if part.slice_type == "templated"][1] == slice(end, end)


def test_query_end_after_a_token():
    text = "-- name: A\nSELECT * FROM {{t:users}}\n-- name: B\nSELECT 2\n"

    templated = render_text(text)

    assert templated.templated_str == "-- name: A\nSELECT * FROM users;\n-- name: B\nSELECT 2\n"
    assert_consistent(templated)


def test_every_templated_slice_covers_exactly_its_span():
    text = (
        "-- dialect: postgres\n"
        "-- name: First\n"
        "-- token: {{sort:a DESC}}\n"
        "SELECT a, @b FROM t WHERE c IN @ids AND d = {{d:@d + 1}} ORDER BY {{sort}} -- token: x\n"
        "\n"
        "-- name: Second\n"
        "SELECT 2;\n"
    )
    plan = plan_of(text)

    templated = render(text, plan, "postgres", "Query.sql")

    templated_parts = [part.source_slice for part in templated.sliced_file if part.slice_type == "templated"]
    # A parameter inside a token is part of the token's slice, and a query's end runs to the next span.
    expected = []
    for index, span in enumerate(plan.spans):
        if span.kind == SpanKind.QUERY_END:
            expected.append(slice(span.start, plan.spans[index + 1].start))
        elif not any(other.kind == SpanKind.TOKEN and other.start < span.start < other.end for other in plan.spans):
            expected.append(slice(span.start, span.end))
    assert templated_parts == expected
    assert_consistent(templated)


def test_templated_slices_are_tagged_as_placeholders_but_the_query_end():
    text = "-- name: A\nSELECT {{t:1}}, @p\n-- name: B\nSELECT @ids IN @ids\n"

    templated = render_text(text)

    tags = [(raw.raw, raw.tag) for raw in templated.raw_sliced if raw.slice_type == "templated"]
    assert tags == [
        ("-- name: A", "literal"),
        ("{{t:1}}", "literal"),
        ("@p", "literal"),
        ("\n", None),
        ("-- name: B", "literal"),
        ("@ids", "literal"),
        ("@ids", "literal"),
    ]


def test_file_without_spans_is_one_literal_slice():
    templated = render_text("SELECT 1;\n")

    assert templated.templated_str == "SELECT 1;\n"
    assert [part.slice_type for part in templated.sliced_file] == ["literal"]
    assert_consistent(templated)


def test_empty_file_renders_empty():
    templated = render_text("")

    assert templated.templated_str == ""
    assert templated.sliced_file == []
