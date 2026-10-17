using System;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>The user's SQL as the generator's lexer reads it for PostgreSQL.</summary>
internal static class PostgresSql
{
    /// <summary>The lexemes of the SQL.</summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static EquatableArray<SqlLexeme> Lex(string sql)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        return lexed.Lexemes;
    }
}
