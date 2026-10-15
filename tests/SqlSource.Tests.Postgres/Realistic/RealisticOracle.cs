using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// What the server returned for a case's query over the seed: the names of its columns, whether each held a NULL in
// any row, and how many rows there were.
internal sealed record OracleRun(IReadOnlyList<string> Names, IReadOnlyList<bool> SawNull, int Rows);

// Executes a case's query over the seed, independent of what the describer recorded of it.
internal static class RealisticOracle
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
            throw new InvalidOperationException($"The SQL cannot be lexed: {error.Kind}.");
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

    // Reads every row, in a transaction that is rolled back, so that a query that writes leaves the seed as it was.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a case's own query, from the repository, with the case's own sample literals."
    )]
    public static async Task<OracleRun> RunAsync(string connection, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var opened = new NpgsqlConnection(connection);
        await opened.OpenAsync(token);
        await using var transaction = await opened.BeginTransactionAsync(token);
        try
        {
            await using var command = new NpgsqlCommand(sql, opened, transaction);
            await using var reader = await command.ExecuteReaderAsync(token);
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var sawNull = new bool[reader.FieldCount];
            var rows = 0;
            while (await reader.ReadAsync(token))
            {
                rows++;
                for (var ordinal = 0; ordinal < sawNull.Length; ordinal++)
                {
                    sawNull[ordinal] |= await reader.IsDBNullAsync(ordinal, token);
                }
            }

            return new OracleRun(names, sawNull, rows);
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    // By the name as written, then ignoring case, whatever the dictionary compares with.
    private static string? Find(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
