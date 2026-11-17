from dataclasses import asdict

import pytest
from corpus_reader import CORPUS_DIRECTORY, corpus_cases

from sqlfluff_templater_sqlsource.dialect_marker import read_dialect_header
from sqlfluff_templater_sqlsource.dialects import canonical_name, option_names, parse_dialect_name
from sqlfluff_templater_sqlsource.errors import ParseError
from sqlfluff_templater_sqlsource.lexer import Lexer
from sqlfluff_templater_sqlsource.rules import DialectRules

# The codes of the errors that the lexer and the header's -- dialect: marker give.  The plan's other error, SQLSRC120,
# is the parser's.
HEADER_AND_LEXER_CODES = {"SQLSRC101", "SQLSRC102", "SQLSRC111"}

CASES = corpus_cases()


def _earliest(first: ParseError | None, second: ParseError | None) -> ParseError | None:
    """As `SqlFileParser.Earliest`: the error that starts first, `first` when they start together."""
    if first is None or (second is not None and second.start < first.start):
        return second
    return first


@pytest.mark.parametrize("case", CASES, ids=[case.path.relative_to(CORPUS_DIRECTORY).as_posix() for case in CASES])
def test_lexer_reads_the_case_as_the_corpus_recorded(case):
    expected = case.expected
    start = parse_dialect_name(",".join([expected["startDialect"], *expected["startOptions"]]))
    lexer = Lexer(case.input, DialectRules.for_choice(start))

    header = read_dialect_header(lexer, case.input)
    lexemes, error = lexer.read_to_end()

    assert [[lexeme.kind.value, lexeme.start, lexeme.end] for lexeme in lexemes] == expected["lexemes"]
    assert canonical_name(lexer.rules.dialect.dialect) == expected["dialect"]
    assert option_names(lexer.rules.dialect.options) == expected["options"]
    reported = _earliest(error, header.invalid)
    wanted = expected["error"]
    if wanted is not None and wanted["code"] not in HEADER_AND_LEXER_CODES:
        wanted = None
    assert (None if reported is None else asdict(reported)) == wanted
