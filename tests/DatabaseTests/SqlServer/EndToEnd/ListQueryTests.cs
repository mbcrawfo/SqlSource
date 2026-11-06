using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Lists.sql, run on the seed of "app" through the methods that ListQueries gets, each of which
// sends a parameter for each value of a list: every kind of collection, the counts on each side of the padded sizes,
// an empty list and a null one, two lists, a list used twice, a list inside an aggregate, which SQL Server refuses
// empty, and its workaround, a list sent at its own count, and the counts at the most parameters a query sends SQL
// Server and past it, which the method refuses before it sends.  The lists of the limit are of ids that no account
// has, around the seed's own.
[Collection(UsesSqlServer.Name)]
public sealed class ListQueryTests(SqlServerDatabases sqlServer)
{
    // The most parameters a query sends SQL Server: it takes 2100 in one request, and SqlClient sends two of them, the
    // statement and its declarations, to sp_executesql, which runs it.
    private const int Limit = 2098;

    // The first of the ids that no account has.
    private const int Unused = 1000;

    private static readonly ListedAccount Alice = new(1, "alice", "active");
    private static readonly ListedAccount Bob = new(2, "bob", "closed");
    private static readonly ListedAccount Carol = new(3, "carol", "suspended");

    [Theory]
    [InlineData("array")]
    [InlineData("List")]
    [InlineData("HashSet")]
    [InlineData("lazy")]
    public async Task Ids_EachKindOfCollection_FindsTheRows(string kind)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(CollectionOf(kind, [1, 3])),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // A sequence that is no collection is copied once, so its values are counted and sent from one enumeration.
    [Fact]
    public async Task Ids_LazySequence_IsEnumeratedOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var enumerations = 0;

        IEnumerable<int> Ids()
        {
            enumerations++;
            yield return 2;
            yield return 3;
        }

        var accounts = await connection.ListAccountsByIds(new ListAccountsByIdsArgs(Ids()), cancellationToken: token);

        accounts.ShouldBe([Bob, Carol]);
        enumerations.ShouldBe(1);
    }

    // The counts at a padded size and one past it, 1, 2 and 512, and 3, 5, 513 and 1025, which the method pads with
    // the last value; the ids from 1 to the count find the accounts among them.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(1025)]
    public async Task Ids_CountsAcrossEachPaddingStep_FindTheRows(int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs([.. Enumerable.Range(1, count)]),
            cancellationToken: token
        );

        accounts.ShouldBe(new[] { Alice, Bob, Carol }.Where(account => account.Id <= count));
    }

    [Fact]
    public async Task Ids_Empty_FindsNoRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIds(new ListAccountsByIdsArgs([]), cancellationToken: token);

        accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Except_Empty_FindsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsExcept(new ListAccountsExceptArgs([]), cancellationToken: token);

        accounts.ShouldBe([Alice, Bob, Carol]);
    }

    // A list inside an aggregate runs while it holds a value: carol alone is suspended.
    [Fact]
    public async Task CountStatuses_List_CountsInTheAggregate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var counts = await connection.CountAccountsWithStatuses(
            new CountAccountsWithStatusesArgs(["suspended"]),
            cancellationToken: token
        );

        counts.ShouldBe([new CountAccountsWithStatusesDto(1)]);
    }

    // An empty list is sent as a subquery, which SQL Server refuses inside an aggregate: Msg 130, a known issue of the
    // manual's sqlserver.md (TD-0106).
    [Fact]
    public async Task CountStatuses_Empty_IsRefusedByTheServer()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.CountAccountsWithStatuses(new CountAccountsWithStatusesArgs([]), cancellationToken: token)
        );

        thrown.Number.ShouldBe(130);
    }

    // The known issue's workaround: the test in a WITH query, and the aggregate over its result, runs for an empty
    // list, which matches no account.
    [Fact]
    public async Task CountStatusesTestedFirst_Empty_CountsNone()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var counts = await connection.CountAccountsWithStatusesTestedFirst(
            new CountAccountsWithStatusesTestedFirstArgs([]),
            cancellationToken: token
        );

        counts.ShouldBe([new CountAccountsWithStatusesTestedFirstDto(0)]);
    }

    [Fact]
    public async Task Optional_Null_FindsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs(null),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Bob, Carol]);
    }

    [Fact]
    public async Task Optional_Empty_FindsNoRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs([]),
            cancellationToken: token
        );

        accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Optional_Values_Filters()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs([2]),
            cancellationToken: token
        );

        accounts.ShouldBe([Bob]);
    }

    [Fact]
    public async Task TwoLists_Filter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIdsAndStatuses(
            new ListAccountsByIdsAndStatusesArgs([1, 2, 3], ["active", "closed"]),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Bob]);
    }

    // The first use finds bob, whose id is in the list, and the second alice, whose id plus one is.
    [Fact]
    public async Task UsedTwice_Filters()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsUsedTwice(
            new ListAccountsUsedTwiceArgs([2]),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Bob]);
    }

    // Three values, which the method sends as three, at no padded size.
    [Fact]
    public async Task NoPadding_Filters()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIdsNoPadding(
            new ListAccountsByIdsNoPaddingArgs([1, 3, Unused]),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // The query has no other parameter, so the list is the whole limit.  Padded, it would pass it, so the method sends
    // it as it is.
    [Fact]
    public async Task Ids_AtTheLimit_Runs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(IdsOf(Limit)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    [Fact]
    public async Task Ids_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsByIds(new ListAccountsByIdsArgs(IdsOf(Limit + 1)), cancellationToken: token)
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsByIds' would send 2099 parameters; SQL Server accepts 2098.  "
                + "Pass fewer values in 'Ids', or use a table-valued parameter."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // 2,050 values pad to 2,560, past the limit, which SQL Server would refuse, so the method sends the 2,050.
    [Fact]
    public async Task Ids_PaddingWouldPassTheLimit_SendsExactCounts()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(IdsOf(2_050)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // The two lists count together against the limit: the ids fill it but for the two statuses.
    [Fact]
    public async Task TwoLists_AtTheLimit_Runs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsByIdsAndStatuses(
            new ListAccountsByIdsAndStatusesArgs(IdsOf(Limit - 2), ["active", "suspended"]),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // One id more than TwoLists_AtTheLimit_Runs sends; the message names the list of more values.
    [Fact]
    public async Task TwoLists_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsByIdsAndStatuses(
                new ListAccountsByIdsAndStatusesArgs(IdsOf(Limit - 1), ["active", "suspended"]),
                cancellationToken: token
            )
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsByIdsAndStatuses' would send 2099 parameters; SQL Server accepts 2098.  "
                + "Pass fewer values in 'Ids', or use a table-valued parameter."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The null test of @ids is a parameter of its own, which counts against the limit beside the list's values.
    [Fact]
    public async Task Optional_AtTheLimit_Runs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs(IdsOf(Limit - 1)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    [Fact]
    public async Task Optional_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsOptional(new ListAccountsOptionalArgs(IdsOf(Limit)), cancellationToken: token)
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsOptional' would send 2099 parameters; SQL Server accepts 2098.  "
                + "Pass fewer values in 'Ids', or use a table-valued parameter."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The streaming method counts its lists at the call, so the limit throws there, before the connection is opened.
    [Fact]
    public async Task StreamIds_OverTheLimit_ThrowsBeforeEnumeration()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var exception = Should.Throw<ArgumentException>(() =>
            connection.StreamAccountsByIds(new StreamAccountsByIdsArgs(IdsOf(Limit + 1)), cancellationToken: token)
        );

        exception.ParamName.ShouldBe("parameters");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamIds_Values_YieldsTheRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection
            .StreamAccountsByIds(new StreamAccountsByIdsArgs([3, 1]), cancellationToken: token)
            .ToListAsync(token);

        accounts.ShouldBe([Alice, Carol]);
    }

    // The streaming method counts the list at the call and binds it at the first MoveNextAsync, so a value added
    // between the two is found when the values are bound.
    [Fact]
    public async Task StreamIds_ListChangedAfterTheCall_ThrowsInvalidOperationException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var ids = new List<int> { 1, 3 };

        var stream = connection.StreamAccountsByIds(new StreamAccountsByIdsArgs(ids), cancellationToken: token);
        ids.Add(2);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => stream.ToListAsync(token).AsTask());

        exception.Message.ShouldBe("The list 'Ids' changed after the query counted it.");
    }

    [Fact]
    public async Task StreamIds_EmptyListFilledAfterTheCall_ThrowsInvalidOperationException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var ids = new List<int>();

        var stream = connection.StreamAccountsByIds(new StreamAccountsByIdsArgs(ids), cancellationToken: token);
        ids.Add(1);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => stream.ToListAsync(token).AsTask());

        exception.Message.ShouldBe("The list 'Ids' changed after the query counted it.");
    }

    // The values in a collection of the kind, or in a lazy sequence that is none.
    private static IEnumerable<int> CollectionOf(string kind, int[] values)
    {
        List<int> list = [.. values];
        HashSet<int> set = [.. values];
        return kind switch
        {
            "array" => values,
            "List" => list,
            "HashSet" => set,
            "lazy" => values.Select(static id => id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // A count of ids: the seed's 1 and 3, and then ids that no account has.
    private static int[] IdsOf(int count) => [1, 3, .. Enumerable.Range(Unused, count - 2)];
}
