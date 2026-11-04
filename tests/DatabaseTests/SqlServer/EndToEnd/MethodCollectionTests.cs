using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// Each of the ten collection types of a many query, set by the project's settings file, the file header of each of two
// files and a query's marker, the last on the copies of ListCollectedAccounts in Claimed.sql, run through the methods
// of the classes of CollectionQueries.cs on the seed of "app".  Each test reads the type the method returns, the
// collection that holds the rows, and the rows.
[Collection(UsesSqlServer.Name)]
public sealed class MethodCollectionTests(SqlServerDatabases sqlServer)
{
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    [Fact]
    public async Task ListCollectedAccounts_IReadOnlyListByTheProjectFile_HoldsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IReadOnlyList<CollectedAccount>, CollectedAccount[]>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccountsAsIEnumerable_IEnumerableByTheQuerysMarker_HoldsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccountsAsIEnumerable(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IEnumerable<CollectedAccount>, CollectedAccount[]>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccountsAsIEnumerable),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccountsAsICollection_ICollectionByTheQuerysMarker_HoldsAList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccountsAsICollection(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ICollection<CollectedAccount>, List<CollectedAccount>>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccountsAsICollection),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccountsAsIReadOnlyCollection_IReadOnlyCollectionByTheQuerysMarker_HoldsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccountsAsIReadOnlyCollection(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IReadOnlyCollection<CollectedAccount>, CollectedAccount[]>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccountsAsIReadOnlyCollection),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccountsAsList_ListByTheQuerysMarker_HoldsAList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccountsAsList(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<List<CollectedAccount>, List<CollectedAccount>>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccountsAsList),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccountsAsImmutableList_ImmutableListByTheQuerysMarker_HoldsAnImmutableList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionQueries.ListCollectedAccountsAsImmutableList(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ImmutableList<CollectedAccount>, ImmutableList<CollectedAccount>>(
            typeof(CollectionQueries),
            nameof(CollectionQueries.ListCollectedAccountsAsImmutableList),
            rows
        );
    }

    [Fact]
    public async Task ListAccountsByMetadata_ImmutableArrayByTheFileHeader_HoldsAnImmutableArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await HeaderCollectionQueries.ListAccountsByMetadata(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ImmutableArray<CollectedAccount>, ImmutableArray<CollectedAccount>>(
            typeof(HeaderCollectionQueries),
            nameof(HeaderCollectionQueries.ListAccountsByMetadata),
            rows
        );
    }

    [Fact]
    public async Task ListAccountsByHeader_IListByTheFileHeader_HoldsAList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await MarkerCollectionQueries.ListAccountsByHeader(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IList<CollectedAccount>, List<CollectedAccount>>(
            typeof(MarkerCollectionQueries),
            nameof(MarkerCollectionQueries.ListAccountsByHeader),
            rows
        );
    }

    [Fact]
    public async Task ListAccountsInArray_ArrayByTheQuerysMarker_IsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await MarkerCollectionQueries.ListAccountsInArray(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<CollectedAccount[], CollectedAccount[]>(
            typeof(MarkerCollectionQueries),
            nameof(MarkerCollectionQueries.ListAccountsInArray),
            rows
        );
    }

    [Fact]
    public async Task ListAccountsInImmutableList_IImmutableListByTheQuerysMarker_HoldsAnImmutableList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await MarkerCollectionQueries.ListAccountsInImmutableList(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IImmutableList<CollectedAccount>, ImmutableList<CollectedAccount>>(
            typeof(MarkerCollectionQueries),
            nameof(MarkerCollectionQueries.ListAccountsInImmutableList),
            rows
        );
    }

    // The method of the claim returns a Task of TReturned, whose rows are in a THolding, the seed's accounts in order.
    private static void ShouldHold<TReturned, THolding>(Type claim, string query, TReturned rows)
        where TReturned : IEnumerable<CollectedAccount>
    {
        claim.GetMethod(query, BindingFlags.Public | BindingFlags.Static)!.ReturnType.ShouldBe(typeof(Task<TReturned>));
        _ = rows.ShouldBeOfType<THolding>();
        rows.ShouldBe(Accounts);
    }
}
