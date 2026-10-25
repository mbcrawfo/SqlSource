using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// Each of the ten collection types of a many query, set by the project's property, the attribute, the item metadata,
// the file header and a query's marker, run through the methods of the classes of CollectionQueries.cs on the seed of
// "app".  Each test reads the type the method returns, the collection that holds the rows, and the rows.
[Collection(UsesSqlServer.Name)]
public sealed class MethodCollectionTests(SqlServerContainer sqlServer)
{
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    [Fact]
    public async Task ListCollectedAccounts_IReadOnlyListByTheProperty_HoldsAnArray()
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
    public async Task ListCollectedAccounts_IEnumerableByTheAttribute_HoldsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await EnumerableCollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IEnumerable<CollectedAccount>, CollectedAccount[]>(
            typeof(EnumerableCollectionQueries),
            nameof(EnumerableCollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccounts_ICollectionByTheAttribute_HoldsAList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await CollectionInterfaceCollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ICollection<CollectedAccount>, List<CollectedAccount>>(
            typeof(CollectionInterfaceCollectionQueries),
            nameof(CollectionInterfaceCollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccounts_IReadOnlyCollectionByTheAttribute_HoldsAnArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await ReadOnlyCollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<IReadOnlyCollection<CollectedAccount>, CollectedAccount[]>(
            typeof(ReadOnlyCollectionQueries),
            nameof(ReadOnlyCollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccounts_ListByTheAttribute_HoldsAList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await ListCollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<List<CollectedAccount>, List<CollectedAccount>>(
            typeof(ListCollectionQueries),
            nameof(ListCollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListCollectedAccounts_ImmutableListByTheAttribute_HoldsAnImmutableList()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await ImmutableListCollectionQueries.ListCollectedAccounts(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ImmutableList<CollectedAccount>, ImmutableList<CollectedAccount>>(
            typeof(ImmutableListCollectionQueries),
            nameof(ImmutableListCollectionQueries.ListCollectedAccounts),
            rows
        );
    }

    [Fact]
    public async Task ListAccountsByMetadata_ImmutableArrayByTheMetadata_HoldsAnImmutableArray()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await MetadataCollectionQueries.ListAccountsByMetadata(
            connection,
            cancellationToken: TestContext.Current.CancellationToken
        );

        ShouldHold<ImmutableArray<CollectedAccount>, ImmutableArray<CollectedAccount>>(
            typeof(MetadataCollectionQueries),
            nameof(MetadataCollectionQueries.ListAccountsByMetadata),
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
