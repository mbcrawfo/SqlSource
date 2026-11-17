from dataclasses import asdict

import pytest
from corpus_reader import CORPUS_DIRECTORY, corpus_cases

from sqlfluff_templater_sqlsource.dialects import canonical_name, option_names, parse_dialect_name
from sqlfluff_templater_sqlsource.plan import build_plan

CASES = corpus_cases()


def _as_dict(span) -> dict:
    return {"kind": span.kind.value, "start": span.start, "end": span.end, "name": span.name, "default": span.default}


@pytest.mark.parametrize("case", CASES, ids=[case.path.relative_to(CORPUS_DIRECTORY).as_posix() for case in CASES])
def test_plan_of_the_case_is_the_one_the_corpus_recorded(case):
    expected = case.expected
    start = parse_dialect_name(",".join([expected["startDialect"], *expected["startOptions"]]))

    plan = build_plan(case.input, case.file_name, start)

    # A file with another error has a best-effort plan: the corpus promises nothing for it.
    if expected["hasOtherErrors"]:
        assert isinstance(plan.spans, list)
        return
    assert canonical_name(plan.dialect.dialect) == expected["dialect"]
    assert option_names(plan.dialect.options) == expected["options"]
    assert [_as_dict(span) for span in plan.spans] == expected["spans"]
    assert (None if plan.error is None else asdict(plan.error)) == expected["error"]


# Files SqlSource rejects with an error that does not stop the plan: the plan is still built, from what the parser
# reads of them.
@pytest.mark.parametrize(
    "text",
    [
        pytest.param("-- name: A\n-- name: B\nSELECT {{t:x}}\n", id="empty block"),
        pytest.param("SELECT 0;\n-- name: A\nSELECT {{t:x}}\n", id="SQL before the first name"),
        pytest.param("-- name: A\nSELECT {{a:x}}, {{a:y}}\n", id="two different defaults"),
        pytest.param("-- name: A\n-- token: {{a:x}}\nSELECT {{a:y}}\n", id="a marker default that differs"),
        pytest.param("-- name: Q\nSELECT {{t:users}}\n-- name: Q\nSELECT @id\n", id="duplicated name"),
    ],
)
def test_rejected_file_gives_best_effort_plan(text):
    plan = build_plan(text, "Query.sql", parse_dialect_name("postgres"))

    assert plan.error is None
    assert plan.has_other_errors
    assert any(span.kind.value == "token" for span in plan.spans)
