using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The four places of a query's method.  ExtensionClass and Public are run by AccountQueryTests and ShapeQueryTests;
// here the query of Queries/Locations.sql is run on the seed of "app" through the internal method of
// InternalLocationQueries and through the wrapper of PrivateLocationQueries' private one.  MethodShapeTests reads the
// accessibility of a method of each place.
[Collection(UsesPostgres.Name)]
public sealed class LocationQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task GetLocatedName_Internal_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var account = await InternalLocationQueries.GetLocatedName(
            connection,
            new GetLocatedNameArgs(1),
            cancellationToken: token
        );

        account.ShouldBe(new GetLocatedNameDto(1, "alice"));
    }

    [Fact]
    public async Task ReadLocatedName_PrivateThroughItsWrapper_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var account = await PrivateLocationQueries.ReadLocatedName(connection, new GetLocatedNameArgs(3), token);

        account.ShouldBe(new GetLocatedNameDto(3, "carol"));
    }
}
