using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Shapes;
using SqlSource.Tests.SqlServer.EndToEnd.Shared;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Shapes.sql, run on the seed of "app" with their class input models as the parameters, into
// their record and class output models.  The tests read this project's own generated models, which the other engine's
// project does not have, so they are not shared.
[Collection(UsesSqlServer.Name)]
public sealed class ShapeQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task QueryAsync_SearchAccounts_ReadsTheAccountsOfEitherParameter()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<SearchAccountsView>(
            ShapeQueries.SearchAccounts,
            new SearchAccountsFilter { AccountId = 1, Name = "c%" }
        );

        accounts.ShouldBe([new SearchAccountsView(1, "alice"), new SearchAccountsView(3, "carol")]);
    }

    [Fact]
    public async Task QueryAsync_ListAccountColumns_ReadsTheSortedClass()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = (await connection.QueryAsync<ListAccountColumnsView>(ShapeQueries.ListAccountColumns)).ToList();

        accounts.ShouldBeEquivalentTo(
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
            }.ToList()
        );
    }

    [Fact]
    public async Task QuerySingleAsync_GetHeadline_ReadsTheNamedModel()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var headline = await connection.QuerySingleAsync<AccountHeadline>(
            ShapeQueries.GetHeadline,
            new GetHeadlineFilter { AccountId = 2 }
        );

        headline.ShouldBe(new AccountHeadline(2, "bob"));
    }

    [Fact]
    public async Task QuerySingleAsync_LookUpAccount_TakesTheInputModelOfAnotherNamespace()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var account = await connection.QuerySingleAsync<LookUpAccountView>(
            ShapeQueries.LookUpAccount,
            new AccountLookup { Email = "carol@example.com" }
        );

        account.ShouldBe(new LookUpAccountView(3, "carol", "suspended"));
    }
}
