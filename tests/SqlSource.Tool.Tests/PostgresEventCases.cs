using System;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// What the classes of the events' tests share: a query with a parameter as the session prepares and checks it, a query
// of users.id and an aggregate with its script, and a describe through a log.
internal static class PostgresEventCases
{
    public static readonly DescribeRequest Query = new(
        "GetUser",
        "SELECT 1 FROM users WHERE id = @id",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("id", null, null, false)])
    );

    // Query as the session prepares it, and the types it sends for its parameter.
    public const string PreparedSql = "SELECT 1 FROM users WHERE id = $1";

    public static readonly string[] PreparedTypes = ["unknown"];

    // The parameter of Query as "check" is sent it.
    public static readonly (string Name, string Type)[] CheckedParameters = [("id", "integer")];

    public static readonly PostgresColumnsRequest ColumnsOfQuery = new(PostgresScript.Statement(1), 1);

    // What each call of a describe through DescribeAsync takes: the clock moves this far before it is answered.
    public static readonly TimeSpan CallTime = TimeSpan.FromMilliseconds(7);

    // A describe whose every call takes CallTime, so that the durations in the log are exact.
    public static async Task DescribeAsync(PostgresScript script, IRunLog log, DescribeRequest? query = null)
    {
        var clock = new TestClock();
        await using var session = await PostgresScript.SessionAsync(
            HookedExchange.Clocked(clock, CallTime, script.Play()),
            log,
            time: clock
        );
        _ = await session.DescribeAsync(query ?? Query, TestContext.Current.CancellationToken);
    }

    // A query of users.id and an aggregate: one column with an origin and one without.
    public static readonly DescribeRequest Listing = new(
        "ListUsers",
        "SELECT id, count(*) OVER () AS n FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    public static readonly PostgresColumnRow CountColumn = PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid);

    // The script of Listing without "check" and "explain", which a test adds as it needs.
    public static PostgresScript ListingScript() =>
        new PostgresScript()
            .Open()
            .Prepares(1, Listing.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, PostgresTables.UsersId, CountColumn)
            .Tables([PostgresTables.Users.Oid], PostgresTables.Users)
            .Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4)
            .Frees(1);
}
