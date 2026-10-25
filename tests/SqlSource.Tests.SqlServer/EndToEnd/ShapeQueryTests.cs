using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Shapes;
using SqlSource.Tests.SqlServer.EndToEnd.Shared;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Shapes.sql, run through their generated methods, static methods of ShapeQueries that take the
// connection first, on the seed of "app" with their class input models as the parameters, into lists of their record
// and class output models.  The tests read this project's own generated models, which the other engine's
// project does not have, so they are not shared.
[Collection(UsesSqlServer.Name)]
public sealed class ShapeQueryTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task SearchAccounts_IdOrName_ReadsTheAccountsOfEitherParameter()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await ShapeQueries.SearchAccounts(
            connection,
            new SearchAccountsFilter { AccountId = 1, Name = "c%" },
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new SearchAccountsView(1, "alice"), new SearchAccountsView(3, "carol")]);
    }

    [Fact]
    public async Task ListAccountColumns_Seed_ReadsTheSortedClass()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = (
            await ShapeQueries.ListAccountColumns(connection, cancellationToken: TestContext.Current.CancellationToken)
        ).ToList();

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
    public async Task GetHeadline_ById_ReadsTheNamedModel()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var headlines = await ShapeQueries.GetHeadline(
            connection,
            new GetHeadlineFilter { AccountId = 2 },
            cancellationToken: TestContext.Current.CancellationToken
        );

        headlines.ShouldBe([new AccountHeadline(2, "bob")]);
    }

    [Fact]
    public async Task LookUpAccount_InputModelOfAnotherNamespace_ReadsTheAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await ShapeQueries.LookUpAccount(
            connection,
            new AccountLookup { Email = "carol@example.com" },
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new LookUpAccountView(3, "carol", "suspended")]);
    }
}
