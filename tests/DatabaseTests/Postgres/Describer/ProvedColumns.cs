using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// What the tests of a proof of not null share: a query is described, each column's answer is compared, and the query
// runs too, so that a column answered not null is seen to hold no NULL.
internal static class ProvedColumns
{
    // The one parameter a query of these tests may have, bound to NULL when it runs.
    private const string Parameter = "@p";

    // For each column 'n' where the walk proves it not null and '?' where it does not.
    public static async Task ShouldAnswerAsync(PostgresDatabases postgres, string sql, string expected)
    {
        var request = sql.Contains(Parameter, StringComparison.Ordinal)
            ? DescribeRequests.Of(sql, ("p", "bigint"))
            : DescribeRequests.Of(sql);
        var description = await Sessions.ShouldDescribeAsync(postgres.ConnectionString, request);
        var columns = description.Columns.ShouldNotBeNull();
        string.Concat(columns.Select(static column => column.Nullable == false ? 'n' : '?')).ShouldBe(expected, sql);

        var nulls = await ColumnsWithANullAsync(postgres, sql);
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Nullable == false)
            {
                nulls.ShouldNotContain(index, sql);
            }
        }
    }

    private static async Task<HashSet<int>> ColumnsWithANullAsync(PostgresDatabases postgres, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        if (sql.Contains(Parameter, StringComparison.Ordinal))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("p", NpgsqlDbType.Bigint) { Value = DBNull.Value });
        }

        await using var reader = await command.ExecuteReaderAsync(token);
        var nulls = new HashSet<int>();
        while (await reader.ReadAsync(token))
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (await reader.IsDBNullAsync(index, token))
                {
                    _ = nulls.Add(index);
                }
            }
        }

        return nulls;
    }
}
