"""Gives the tests the corpus that `TemplatingCorpusTests` writes from the C# parser."""

import json
from dataclasses import dataclass
from pathlib import Path

CORPUS_DIRECTORY = Path(__file__).parent / "corpus"


@dataclass(frozen=True)
class CorpusCase:
    path: Path
    file_name: str
    settings: list[str]
    input: str
    expected: dict


def corpus_cases() -> list[CorpusCase]:
    """Every case of `tests/corpus/**/*.json`, in path order."""
    cases = []
    for path in sorted(CORPUS_DIRECTORY.rglob("*.json")):
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
