from corpus import corpus_cases


def test_sqlfluff_finds_the_templater():
    from sqlfluff.core import FluffConfig

    config = FluffConfig(overrides={"dialect": "ansi", "templater": "sqlsource"})
    assert config.get_templater_class().name == "sqlsource"


def test_corpus_is_present():
    assert len(corpus_cases()) > 100
