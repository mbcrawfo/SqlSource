using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Lists.sql, run on the seed of "app" through the methods that ListQueries gets, each of which
// sends a parameter for each value of a list: every kind of collection, the counts on each side of the padded sizes,
// an empty list and a null one, two lists, a list used twice, a list after NOT IN and one beside its null test by a
// LEFT JOIN, a list sent at its own count, and the counts at PostgreSQL's limit of 65535 parameters and past it, which
// the method refuses before it sends.  The lists of the limit are of ids that no account has, around the seed's own.
[Collection(UsesPostgres.Name)]
public sealed class ListQueryTests(PostgresDatabases postgres)
{
    // The most parameters PostgreSQL takes in one query, and Npgsql too.
    private const int Limit = 65535;

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(ListArguments.CollectionOf(kind, [1, 3])),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // A sequence that is no collection is copied once, so its values are counted and sent from one enumeration.
    [Fact]
    public async Task Ids_LazySequence_IsEnumeratedOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIds(new ListAccountsByIdsArgs([]), cancellationToken: token);

        accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Except_Empty_FindsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsExcept(new ListAccountsExceptArgs([]), cancellationToken: token);

        accounts.ShouldBe([Alice, Bob, Carol]);
    }

    // An empty list after NOT IN keeps every row the LEFT JOIN gives, carol's too, whom it fills with NULLs: the
    // properties of the joined columns can be null, though body is NOT NULL, since a NOT IN of one value would have
    // dropped her row.
    [Fact]
    public async Task NotesExcept_Empty_ReadsTheAccountWithoutANote()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAccountNotesExcept(
            new ListAccountNotesExceptArgs([]),
            cancellationToken: token
        );

        rows.ShouldBe([
            new ListAccountNotesExceptDto(1, "alice", "Asked for a larger plan."),
            new ListAccountNotesExceptDto(1, "alice", "Sent the quote."),
            new ListAccountNotesExceptDto(2, "bob", "Closed the account on site."),
            new ListAccountNotesExceptDto(3, "carol", null),
        ]);
    }

    // A list whose truth IS FALSE tests is read as one that NOT negates: an empty list keeps carol's row, whose body
    // the join fills with NULL, and the method reads it.
    [Fact]
    public async Task NotesNotListed_Empty_ReadsTheAccountWithoutANote()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAccountNotesNotListed(
            new ListAccountNotesNotListedArgs([]),
            cancellationToken: token
        );

        rows.ShouldBe([
            new ListAccountNotesNotListedDto(1, "alice", "Asked for a larger plan."),
            new ListAccountNotesNotListedDto(1, "alice", "Sent the quote."),
            new ListAccountNotesNotListedDto(2, "bob", "Closed the account on site."),
            new ListAccountNotesNotListedDto(3, "carol", null),
        ]);
    }

    [Fact]
    public async Task NotesExcept_Ids_LeavesTheirNotesOut()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAccountNotesExcept(
            new ListAccountNotesExceptArgs([1, 3]),
            cancellationToken: token
        );

        rows.ShouldBe([new ListAccountNotesExceptDto(1, "alice", "Sent the quote.")]);
    }

    // A list beside its null test keeps the LEFT JOIN outer: (@accountIds IS NOT NULL OR n.id > 0) is no filter of the
    // notes when a list is given, so carol, who has none, is read with a NULL body.
    [Fact]
    public async Task NotesOptional_AccountWithoutANote_ReadsItsNullBody()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAccountNotesOptional(
            new ListAccountNotesOptionalArgs([2, 3]),
            cancellationToken: token
        );

        rows.ShouldBe([
            new ListAccountNotesOptionalDto(2, "Closed the account on site."),
            new ListAccountNotesOptionalDto(3, null),
        ]);
    }

    [Fact]
    public async Task NotesOptional_Null_ReadsTheNotesAlone()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAccountNotesOptional(
            new ListAccountNotesOptionalArgs(null),
            cancellationToken: token
        );

        rows.Select(static row => row.Id).ShouldBe([1, 1, 2]);
    }

    [Fact]
    public async Task Optional_Null_FindsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs([2]),
            cancellationToken: token
        );

        accounts.ShouldBe([Bob]);
    }

    // The statuses are of the enum account_status, sent as strings.
    [Fact]
    public async Task TwoLists_Filter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIdsNoPadding(
            new ListAccountsByIdsNoPaddingArgs([1, 3, ListArguments.Unused]),
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(ListArguments.IdsOf(Limit)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    [Fact]
    public async Task Ids_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsByIds(
                new ListAccountsByIdsArgs(ListArguments.IdsOf(Limit + 1)),
                cancellationToken: token
            )
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsByIds' would send 65536 parameters; PostgreSQL accepts 65535.  "
                + "Pass fewer values in 'Ids', or send them as an array with = ANY."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // 65,500 values pad to 65,536, one past the limit, which Npgsql would refuse, so the method sends the 65,500.
    [Fact]
    public async Task Ids_PaddingWouldPassTheLimit_SendsExactCounts()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIds(
            new ListAccountsByIdsArgs(ListArguments.IdsOf(65_500)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // The two lists count together against the limit: the ids fill it but for the two statuses.
    [Fact]
    public async Task TwoLists_AtTheLimit_Runs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsByIdsAndStatuses(
            new ListAccountsByIdsAndStatusesArgs(ListArguments.IdsOf(Limit - 2), ["active", "suspended"]),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    // One id more than TwoLists_AtTheLimit_Runs sends; the message names the list of more values.
    [Fact]
    public async Task TwoLists_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsByIdsAndStatuses(
                new ListAccountsByIdsAndStatusesArgs(ListArguments.IdsOf(Limit - 1), ["active", "suspended"]),
                cancellationToken: token
            )
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsByIdsAndStatuses' would send 65536 parameters; PostgreSQL accepts 65535.  "
                + "Pass fewer values in 'Ids', or send them as an array with = ANY."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The null test of @ids is a parameter of its own, which counts against the limit beside the list's values.
    [Fact]
    public async Task Optional_AtTheLimit_Runs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsOptional(
            new ListAccountsOptionalArgs(ListArguments.IdsOf(Limit - 1)),
            cancellationToken: token
        );

        accounts.ShouldBe([Alice, Carol]);
    }

    [Fact]
    public async Task Optional_OneOverTheLimit_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            connection.ListAccountsOptional(
                new ListAccountsOptionalArgs(ListArguments.IdsOf(Limit)),
                cancellationToken: token
            )
        );

        exception.ParamName.ShouldBe("parameters");
        exception.Message.ShouldStartWith(
            "The query 'ListAccountsOptional' would send 65536 parameters; PostgreSQL accepts 65535.  "
                + "Pass fewer values in 'Ids', or send them as an array with = ANY."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The streaming method counts its lists at the call, so the limit throws there, before the connection is opened.
    [Fact]
    public async Task StreamIds_OverTheLimit_ThrowsBeforeEnumeration()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = Should.Throw<ArgumentException>(() =>
            connection.StreamAccountsByIds(
                new StreamAccountsByIdsArgs(ListArguments.IdsOf(Limit + 1)),
                cancellationToken: token
            )
        );

        exception.ParamName.ShouldBe("parameters");
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task StreamIds_Values_YieldsTheRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
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
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var ids = new List<int>();

        var stream = connection.StreamAccountsByIds(new StreamAccountsByIdsArgs(ids), cancellationToken: token);
        ids.Add(1);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => stream.ToListAsync(token).AsTask());

        exception.Message.ShouldBe("The list 'Ids' changed after the query counted it.");
    }
}
