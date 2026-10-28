using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// A case's sample values put in its SQL so that the server can run it over the seed, as the oracle's check does, once
// RealisticSampleChecks of tests/SqlSource.Tool.Testing has checked them.  The runs through the method and through
// Dapper send them as parameters instead.
internal static class RealisticSamples
{
    // The SQL with each parameter replaced by its sample literal, cast to the type described for it, so that the
    // server runs what it described.  A parameter in a string, a quoted name or a comment is no parameter.
    public static string Substitute(
        string sql,
        IReadOnlyDictionary<string, string> sample,
        IReadOnlyList<DescribedParameter> parameters
    )
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is { } error)
        {
            throw new InvalidOperationException($"The SQL cannot be lexed, {error.Kind}:\n{sql}");
        }

        var text = new StringBuilder(sql);
        // From the end backwards, so that each span still points at its parameter.
        for (var index = lexed.Lexemes.Count - 1; index >= 0; index--)
        {
            var lexeme = lexed.Lexemes[index];
            if (lexeme.Kind != SqlLexemeKind.Parameter)
            {
                continue;
            }

            var name = sql.Substring(lexeme.Span.Start + 1, lexeme.Span.Length - 1);
            var value =
                RealisticSampleChecks.Find(sample, name)
                ?? throw new InvalidOperationException($"The sample has no value of @{name}.");
            var type =
                parameters
                    .FirstOrDefault(parameter =>
                        string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
                    )
                    ?.Type?.Name
                ?? throw new InvalidOperationException($"No type is given for @{name}.");
            _ = text.Remove(lexeme.Span.Start, lexeme.Span.Length).Insert(lexeme.Span.Start, $"({value})::{type}");
        }

        return text.ToString();
    }
}
