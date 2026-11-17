"""The sqlfluff templater of SqlSource queries."""

from sqlfluff.core.templaters import RawTemplater


class SqlSourceTemplater(RawTemplater):
    name = "sqlsource"

    def process(self, *, in_str, fname, config=None, formatter=None):
        raise NotImplementedError
