using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Shapes;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Shared;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Shapes.sql, run on the seed of "app" through the static methods that ShapeQueries gets under
// MethodLocation.Public, with their class input models as the parameters, into their record and class output models,
// in an IReadOnlyList by the marker of SearchAccounts and in a List by the attribute for the others.  Half the tests
// give the method an open connection and half a closed one, which the method opens.  The tests read this project's own
// generated models, which the other engine's project does not have, so they are not shared.
[Collection(UsesPostgres.Name)]
public sealed class ShapeQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task SearchAccounts_IdOrName_ReadsTheAccountsOfEitherParameter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await ShapeQueries.SearchAccounts(
            connection,
            new SearchAccountsFilter { AccountId = 1, Name = "c%" },
            cancellationToken: token
        );

        accounts.ShouldBe([new SearchAccountsView(1, "alice"), new SearchAccountsView(3, "carol")]);
    }

    [Fact]
    public async Task ListAccountColumns_EveryAccount_ReadsTheSortedClass()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await ShapeQueries.ListAccountColumns(connection, cancellationToken: token);

        // The class has no equality of its own, so its elements are compared by their properties, apart from the
        // collection they are in.
        accounts
            .ToArray()
            .ShouldBeEquivalentTo(
                new[]
                {
                    new ListAccountColumnsView
                    {
                        Id = 1,
                        Name = "alice",
                        Status = "active",
                    },
                    new ListAccountColumnsView
                    {
                        Id = 2,
                        Name = "bob",
                        Status = "closed",
                    },
                    new ListAccountColumnsView
                    {
                        Id = 3,
                        Name = "carol",
                        Status = "suspended",
                    },
                }
            );
    }

    [Fact]
    public async Task GetHeadline_OfBob_ReadsTheNamedModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var headlines = await ShapeQueries.GetHeadline(
            connection,
            new GetHeadlineFilter { AccountId = 2 },
            cancellationToken: token
        );

        headlines.ShouldBe([new AccountHeadline(2, "bob")]);
    }

    [Fact]
    public async Task LookUpAccount_OfCarol_TakesTheInputModelOfAnotherNamespace()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await ShapeQueries.LookUpAccount(
            connection,
            new AccountLookup { Email = "carol@example.com" },
            cancellationToken: token
        );

        accounts.ShouldBe([new LookUpAccountView(3, "carol", "suspended")]);
    }
}
