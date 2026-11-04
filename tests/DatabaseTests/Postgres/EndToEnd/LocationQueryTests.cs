using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The places of a query's method, by the markers of Queries/Locations.sql, which LocationQueries claims: the query and
// its copies are run on the seed of "app" through the extension method of the connection, the internal method of the
// type, the wrapper of its private one and the method of its nested class Sql.  A public method of the type is run by
// ShapeQueryTests.  MethodShapeTests reads the accessibility of a method of each place.
[Collection(UsesPostgres.Name)]
public sealed class LocationQueryTests(PostgresDatabases postgres)
{
    [Fact]
    public async Task GetLocatedName_ExtensionClass_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var account = await connection.GetLocatedName(new GetLocatedNameArgs(2), cancellationToken: token);

        account.ShouldBe(new GetLocatedNameDto(2, "bob"));
    }

    [Fact]
    public async Task GetLocatedNameInternal_Internal_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var account = await LocationQueries.GetLocatedNameInternal(
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

        var account = await LocationQueries.ReadLocatedName(connection, new GetLocatedNameArgs(3), token);

        account.ShouldBe(new GetLocatedNameDto(3, "carol"));
    }

    [Fact]
    public async Task GetLocatedNameNested_NestedClass_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var account = await LocationQueries.Sql.GetLocatedNameNested(
            connection,
            new GetLocatedNameArgs(1),
            cancellationToken: token
        );

        account.ShouldBe(new GetLocatedNameDto(1, "alice"));
    }
}
