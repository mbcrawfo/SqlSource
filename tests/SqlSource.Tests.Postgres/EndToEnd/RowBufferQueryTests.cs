using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/RowBuffers.sql, run through the methods that RowBufferQueries gets, at counts on each side of
// the row buffer's sizes: none, one, its first buffer of 16 and one more, the largest buffer it rents, of 1,024, and
// one more, and 3,000, which grows past it into plain arrays.  Each method returns the numbers from 1 to the count, in
// order, in an array whatever type it declares, and its twin under no-array-pool, which reads without the pool, returns
// the same rows.  An even count runs on an open connection and an odd one on a closed one, which the methods open.
[Collection(UsesPostgres.Name)]
public sealed class RowBufferQueryTests(PostgresContainer postgres)
{
    private static readonly string[] Collections = ["Array", "IEnumerable", "IReadOnlyCollection", "IReadOnlyList"];

    private static readonly int[] Counts = [0, 1, 16, 17, 1024, 1025, 3000];

    public static TheoryData<string, int> CollectionsAndCounts
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var collection in Collections)
            {
                foreach (var count in Counts)
                {
                    data.Add(collection, count);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CollectionsAndCounts))]
    public async Task NumbersAs_EachCount_ReturnsOneToTheCountInAnArray(string collection, int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection =
            count % 2 == 0 ? await source.OpenConnectionAsync(token) : source.CreateConnection();
        var expected = Enumerable.Range(1, count).Select(n => new CountedNumber(n));

        var pooled = await ReadAsync(connection, collection, pooled: true, count, token);
        var unpooled = await ReadAsync(connection, collection, pooled: false, count, token);

        pooled.ShouldBeOfType<CountedNumber[]>().ShouldBe(expected);
        unpooled.ShouldBeOfType<CountedNumber[]>().ShouldBe(pooled);
    }

    // The method of a collection type, read through the pool or under no-array-pool.
    private static Task<IEnumerable<CountedNumber>> ReadAsync(
        NpgsqlConnection connection,
        string collection,
        bool pooled,
        int count,
        CancellationToken token
    ) =>
        (collection, pooled) switch
        {
            ("Array", true) => Rows(connection.NumbersAsArray(new(count), cancellationToken: token)),
            ("IEnumerable", true) => connection.NumbersAsIEnumerable(new(count), cancellationToken: token),
            ("IReadOnlyCollection", true) => Rows(
                connection.NumbersAsIReadOnlyCollection(new(count), cancellationToken: token)
            ),
            ("IReadOnlyList", true) => Rows(connection.NumbersAsIReadOnlyList(new(count), cancellationToken: token)),
            ("Array", false) => Rows(connection.NumbersNoPoolAsArray(new(count), cancellationToken: token)),
            ("IEnumerable", false) => connection.NumbersNoPoolAsIEnumerable(new(count), cancellationToken: token),
            ("IReadOnlyCollection", false) => Rows(
                connection.NumbersNoPoolAsIReadOnlyCollection(new(count), cancellationToken: token)
            ),
            ("IReadOnlyList", false) => Rows(
                connection.NumbersNoPoolAsIReadOnlyList(new(count), cancellationToken: token)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(collection), collection, null),
        };

    // The rows of a method that declares another type, as the IEnumerable that the selector returns.
    private static async Task<IEnumerable<CountedNumber>> Rows<T>(Task<T> rows)
        where T : IEnumerable<CountedNumber> => await rows;
}
