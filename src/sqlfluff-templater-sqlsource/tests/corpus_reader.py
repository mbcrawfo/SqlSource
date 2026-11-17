"""Gives the tests the corpus that `TemplatingCorpusTests` writes from the C# parser."""

import json
from dataclasses import dataclass
from pathlib import Path

CORPUS_DIRECTORY = Path(__file__).parent / "corpus"

# The names a dialect and its options are set by, as SqlDialectName lists them.  It is no case.
DIALECT_NAMES_PATH = CORPUS_DIRECTORY / "dialect-names.json"


@dataclass(frozen=True)
class CorpusCase:
    path: Path
    file_name: str
    settings: list[str]
    input: str
    expected: dict


def corpus_cases() -> list[CorpusCase]:
    """Every case of `tests/corpus/**/*.json`, in path order: every file there but `dialect-names.json`."""
    cases = []
    for path in sorted(CORPUS_DIRECTORY.rglob("*.json")):
        if path == DIALECT_NAMES_PATH:
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        cases.append(
            CorpusCase(
                path=path,
                file_name=data["fileName"],
                settings=data["settings"],
                input=data["input"],
                expected=data["expected"],
            )
        )
    return cases


def dialect_names() -> tuple[list[tuple[str, str]], list[tuple[str, int]]]:
    """`SqlDialectName.Names`, each name with its dialect's member of the C# enum, and `SqlDialectName.Options`, each
    name with its option's value, in their order."""
    data = json.loads(DIALECT_NAMES_PATH.read_text(encoding="utf-8"))
    return [tuple(entry) for entry in data["names"]], [tuple(entry) for entry in data["options"]]
