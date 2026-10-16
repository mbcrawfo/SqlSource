using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
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
    public static void CheckSampleKeys(RealisticCase found, IEnumerable<string> parameters)
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
    // not say null: generated code never sends one, so the oracle would run what no caller can.  The literal NULL
    // alone is read, ignoring case and white space around it.
    public static void CheckSampleNulls(RealisticCase found, IReadOnlyList<SqlQueryParameter> parameters)
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
        OracleRun run;
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

            run = new OracleRun(names, sawNull, rows);
        }
        catch
        {
            await RollBackAfterAsync(transaction);
            throw;
        }

        // Not the test's token: a cancelled test still rolls back what its query wrote.
        await transaction.RollbackAsync(CancellationToken.None);
        return run;
    }

    // The rollback of a query that failed, which fails too when the connection broke.
    private static async Task RollBackAfterAsync(NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException)
        {
            // The query's own error is the one that says why, and the server rolls back what a broken connection
            // wrote.
        }
    }

    // By the name as written, then ignoring case, whatever the dictionary compares with.
    private static string? Find(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
