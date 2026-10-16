using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Parsing;

namespace SqlSource.Tests.Postgres.Realistic;

// A case's sample values: checked against the parameters of its query, and put in its SQL so that the server can run
// it over the seed.  Every check of the corpus that executes a query runs it so.
internal static class RealisticSamples
{
    // The SQL with each parameter replaced by its sample literal, cast to the type described for it, so that the
    // server runs what it described.  A parameter in a string, a quoted name or a comment is no parameter.
    public static string Substitute(
        string sql,
        IReadOnlyDictionary<string, string> sample,
        IReadOnlyDictionary<string, string> types
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
                Find(sample, name) ?? throw new InvalidOperationException($"The sample has no value of @{name}.");
            var type = Find(types, name) ?? throw new InvalidOperationException($"No type is given for @{name}.");
            _ = text.Remove(lexeme.Span.Start, lexeme.Span.Length).Insert(lexeme.Span.Start, $"({value})::{type}");
        }

        return text.ToString();
    }

    // Fails naming the case and the key when a sample of the case names no parameter of its query, as a key that is
    // misspelt does: its value would be used nowhere.  A key is compared ignoring case, as Substitute finds it.
    public static void CheckKeys(RealisticCase found, IEnumerable<string> parameters)
    {
        var named = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in found.Samples.SelectMany(static sample => sample.Keys))
        {
            if (!named.Contains(key))
            {
                throw new InvalidOperationException(
                    $"A sample of the case {found} names @{key}, which is no parameter of its query."
                );
            }
        }
    }

    // Fails naming the case and the parameter when a sample gives NULL to a parameter whose -- param: marker does
    // not say null: generated code never sends one, so the server would run what no caller can.  The literal NULL
    // alone is read, ignoring case and white space around it.
    public static void CheckNulls(RealisticCase found, IReadOnlyList<SqlQueryParameter> parameters)
    {
        foreach (var (key, value) in found.Samples.SelectMany(static sample => sample))
        {
            if (
                string.Equals(value.Trim(), "NULL", StringComparison.OrdinalIgnoreCase)
                && parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, key, StringComparison.OrdinalIgnoreCase)
                )
                    is { Nullable: not true }
            )
            {
                throw new InvalidOperationException(
                    $"A sample of the case {found} gives @{key} NULL, and no -- param: marker says it is null."
                );
            }
        }
    }

    // By the name as written, then ignoring case, whatever the dictionary compares with.
    private static string? Find(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
