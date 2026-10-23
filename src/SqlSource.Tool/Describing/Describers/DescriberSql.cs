using System;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>The user's SQL as the generator's lexer reads it for a describer's dialect.</summary>
internal static class DescriberSql
{
    /// <summary>The lexemes of the SQL.</summary>
    /// <param name="sql">The SQL of a query.</param>
    /// <param name="rules">The rules of the engine's dialect.</param>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static EquatableArray<SqlLexeme> Lex(string sql, SqlDialectRules rules)
    {
        var lexed = SqlLexer.Lex(sql, rules);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        return lexed.Lexemes;
    }
}
