using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/ExternalRefs.sql, run on the seed of "app" through the methods that ExternalRefQueries gets:
// the alias type dbo.ExternalId, over bigint, is read and sent as the value type ExternalId, through its converters, as
// a column, nullable or not, and as a parameter.  Two queries read every column of external_refs, into its table
// model.  Each id is beyond the range of an int, so a value cut to one would not match.  A write is rolled back.
[Collection(UsesSqlServer.Name)]
public sealed class ExternalRefQueryTests(SqlServerContainer sqlServer)
{
    // The parameter is sent as the long that ToDatabase returns, and the column read from the long that SqlClient
    // gives, through FromDatabase.
    [Fact]
    public async Task GetExternalRef_AnIdOfTheSeed_ReadsItsRow()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var found = await connection.GetExternalRef(
            new GetExternalRefArgs(new ExternalId(5_000_000_002)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        found.ShouldBe(new ExternalRefsDto(2, 2, new ExternalId(5_000_000_002)));
    }

    [Fact]
    public async Task GetExternalRef_AnIdOfNoRow_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);

        var found = await connection.GetExternalRef(
            new GetExternalRefArgs(new ExternalId(5_000_000_003)),
            cancellationToken: token
        );

        found.ShouldBeNull();
    }

    // The column is an ExternalId?: carol's NULL is read as null, never given to FromDatabase, and the others' longs
    // through it.
    [Fact]
    public async Task ListAccountExternalIds_AnAccountWithNoId_ReadsNull()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var ids = await connection.ListAccountExternalIds(cancellationToken: TestContext.Current.CancellationToken);

        ids.ShouldBe([
            new ListAccountExternalIdsDto(1, new ExternalId(5_000_000_001)),
            new ListAccountExternalIdsDto(2, new ExternalId(5_000_000_002)),
            new ListAccountExternalIdsDto(3, null),
        ]);
    }

    // The largest bigint is written through ToDatabase and read back through FromDatabase, by the insert's OUTPUT and
    // by the query in the same transaction, and the rollback leaves no row of it.
    [Fact]
    public async Task AddExternalRef_InTransaction_WritesTheIdAndRollsBack()
    {
        var token = TestContext.Current.CancellationToken;
        var externalId = new ExternalId(long.MaxValue);
        await using (var connection = EndToEndSources.App(sqlServer))
        {
            await EndToEndSources.RolledBackAsync(
                connection,
                async transaction =>
                {
                    var added = await connection.AddExternalRef(
                        new AddExternalRefArgs(3, 3, externalId),
                        transaction: transaction,
                        cancellationToken: token
                    );

                    added.ShouldBe(new ExternalRefsDto(3, 3, externalId));
                    var found = await connection.GetExternalRef(
                        new GetExternalRefArgs(externalId),
                        transaction: transaction,
                        cancellationToken: token
                    );
                    found.ShouldBe(new ExternalRefsDto(3, 3, externalId));
                },
                token
            );
        }

        await using var after = EndToEndSources.App(sqlServer);
        var afterRollback = await after.GetExternalRef(new GetExternalRefArgs(externalId), cancellationToken: token);
        afterRollback.ShouldBeNull();
    }
}
