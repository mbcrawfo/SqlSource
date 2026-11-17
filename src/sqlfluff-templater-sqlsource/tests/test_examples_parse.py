"""Every query of the example projects renders and parses under its natural sqlfluff dialect."""

from pathlib import Path

import pytest
from corpus_reader import CORPUS_DIRECTORY, CorpusCase, corpus_cases
from sqlfluff.core import FluffConfig, Linter

EXAMPLES_DIRECTORY = CORPUS_DIRECTORY / "repo" / "examples"

# The examples that sqlfluff's own grammar cannot parse: one path under `tests/corpus/repo/examples/` per line, with a
# `#` and the reason, naming the sqlfluff construct.
UNPARSABLE_PATH = Path(__file__).parent / "examples_unparsable.txt"

NATURAL_DIALECTS = {"postgres": "postgres", "mssql": "tsql"}


def unparsable() -> set[str]:
    paths = set()
    for line in UNPARSABLE_PATH.read_text(encoding="utf-8").splitlines():
        path, _, reason = line.partition("#")
        if path.strip():
            assert reason.strip(), f"{path.strip()} has no reason"
            paths.add(path.strip())
    return paths


def example_cases() -> list[CorpusCase]:
    return [case for case in corpus_cases() if case.path.is_relative_to(EXAMPLES_DIRECTORY)]


def write_case(case: CorpusCase, root: Path) -> Path:
    """Writes the case's file below the chain of settings files it holds, the nearest first."""
    folder = root.joinpath(*[f"level{index}" for index in range(len(case.settings))])
    folder.mkdir(parents=True, exist_ok=True)
    for distance, text in enumerate(case.settings):
        (folder.parents[distance - 1] if distance else folder).joinpath("sqlsource.json").write_text(
            text, encoding="utf-8"
        )
    path = folder / case.file_name
    path.write_text(case.input, encoding="utf-8")
    return path


def test_examples_are_present():
    assert len(example_cases()) >= 20


def test_every_unparsable_example_exists():
    names = {case.path.relative_to(EXAMPLES_DIRECTORY).as_posix() for case in example_cases()}
    assert unparsable() <= names


@pytest.mark.parametrize("case", example_cases(), ids=lambda case: case.path.relative_to(EXAMPLES_DIRECTORY).as_posix())
def test_example_parses(case, tmp_path):
    name = case.path.relative_to(EXAMPLES_DIRECTORY).as_posix()
    path = write_case(case, tmp_path)
    sqlfluff_dialect = NATURAL_DIALECTS[case.expected["dialect"]]
    linter = Linter(config=FluffConfig(overrides={"dialect": sqlfluff_dialect, "templater": "sqlsource"}))

    linted = linter.lint_string(case.input, fname=str(path))

    failures = [
        f"{violation.rule_code()} {violation.line_no}:{violation.line_pos} {violation.desc()}"
        for violation in linted.get_violations()
        if violation.rule_code() in ("PRS", "TMP", "LXR")
    ]
    if name in unparsable():
        assert failures, f"{name} parses: remove it from {UNPARSABLE_PATH.name}"
    else:
        assert failures == []
