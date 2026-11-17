from dataclasses import asdict

import pytest
from corpus_reader import CORPUS_DIRECTORY, corpus_cases

from sqlfluff_templater_sqlsource.dialects import canonical_name, option_names, parse_dialect_name
from sqlfluff_templater_sqlsource.lexer import Lexer, read_dialect_header
from sqlfluff_templater_sqlsource.rules import DialectRules

# The codes of the errors that the lexer and the header's -- dialect: marker give.  The plan's other error, SQLSRC120,
# is the parser's.
HEADER_AND_LEXER_CODES = {"SQLSRC101", "SQLSRC102", "SQLSRC111"}

CASES = corpus_cases()


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
    # The plan reports the earlier of the two, the lexer's when they start together.
    first = header.invalid if error is None or (header.invalid and header.invalid.start < error.start) else error
    wanted = expected["error"] if expected["error"] and expected["error"]["code"] in HEADER_AND_LEXER_CODES else None
    assert (asdict(first) if first else None) == wanted
