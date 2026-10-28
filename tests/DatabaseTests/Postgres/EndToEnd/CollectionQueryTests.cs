using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Collections.sql, run on the seed of "app" through the methods that CollectionQueries gets,
// each returning the three accounts in the collection type of its marker, which holds the rows in the type the manual
// says.  Half the tests give the method an open connection and half a closed one, which the method opens.  The types
// that the methods declare are read by MethodShapeTests.
[Collection(UsesPostgres.Name)]
public sealed class CollectionQueryTests(PostgresContainer postgres)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    [Fact]
    public async Task ListAsArray_EveryAccount_ReturnsAnArray()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAsArray(cancellationToken: token);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsIEnumerable_EveryAccount_ReturnsAnIEnumerable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAsIEnumerable(cancellationToken: token);

        accounts.ShouldBeOfType<CollectedAccount[]>().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsIReadOnlyCollection_EveryAccount_ReturnsAnIReadOnlyCollection()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAsIReadOnlyCollection(cancellationToken: token);

        accounts.ShouldBeOfType<CollectedAccount[]>().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsIReadOnlyList_EveryAccount_ReturnsAnIReadOnlyList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAsIReadOnlyList(cancellationToken: token);

        accounts.ShouldBeOfType<CollectedAccount[]>().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsICollection_EveryAccount_ReturnsAnICollection()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAsICollection(cancellationToken: token);

        accounts.ShouldBeOfType<List<CollectedAccount>>().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsIList_EveryAccount_ReturnsAnIList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAsIList(cancellationToken: token);

        accounts.ShouldBeOfType<List<CollectedAccount>>().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsList_EveryAccount_ReturnsAList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAsList(cancellationToken: token);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsImmutableArray_EveryAccount_ReturnsAnImmutableArray()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAsImmutableArray(cancellationToken: token);

        accounts.AsEnumerable().ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsImmutableList_EveryAccount_ReturnsAnImmutableList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAsImmutableList(cancellationToken: token);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAsIImmutableList_EveryAccount_ReturnsAnIImmutableList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAsIImmutableList(cancellationToken: token);

        accounts.ShouldBeOfType<ImmutableList<CollectedAccount>>().ShouldBe(Accounts);
    }
}
