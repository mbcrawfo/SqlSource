using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.ClrType;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.UserClr;

// The queries of Queries/Grades.sql, run through the methods that UserClrQueries gets: a CLR type of a user's own
// assembly, dbo.Grade, read through a column and sent through a parameter as its .NET build, which the folder's type
// override names.  The server names the type by its assembly, and SqlClient finds the .NET build by that name.
[Collection(UsesSqlServer.Name)]
public sealed class UserClrQueryTests(SqlServerDatabases sqlServer)
{
    [Fact]
    public async Task ListGrades_Seed_ReadsEachGradeAndANull()
    {
        await using var connection = EndToEndSources.UserClr(sqlServer);

        var rows = await connection.ListGrades(cancellationToken: TestContext.Current.CancellationToken);

        rows.Select(static row => (row.Id, row.Grade, row.Maybe))
            .ShouldBe([(1, Grade.Of(5), null), (2, Grade.Of(7), Grade.Of(8))]);
    }

    // The server compares the parameter with the column, so it was sent as a dbo.Grade.
    [Fact]
    public async Task FindByGrade_GradeOfARow_FindsItsRow()
    {
        await using var connection = EndToEndSources.UserClr(sqlServer);

        var rows = await connection.FindByGrade(
            new FindByGradeArgs(Grade.Of(7)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        rows.Select(static row => row.Id).ShouldBe([2]);
    }

    // A grade and a null written through the parameters are read back as they were sent, in a transaction that is
    // rolled back.
    [Fact]
    public async Task AddGrade_GradeAndNull_ReadsThemBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.UserClr(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                (
                    await connection.AddGrade(
                        new AddGradeArgs(3, Grade.Of(9), null),
                        transaction,
                        cancellationToken: token
                    )
                ).ShouldBe(1);
                _ = await connection.AddGrade(
                    new AddGradeArgs(4, Grade.Of(1), Grade.Of(2)),
                    transaction,
                    cancellationToken: token
                );

                var rows = await connection.ListGrades(transaction, cancellationToken: token);
                rows.Where(static row => row.Id > 2)
                    .Select(static row => (row.Id, row.Grade, row.Maybe))
                    .ShouldBe([(3, Grade.Of(9), null), (4, Grade.Of(1), Grade.Of(2))]);
            },
            token
        );
    }

    [Fact]
    public async Task ListViewGrades_Seed_ReadsTheGradesThroughTheView()
    {
        await using var connection = EndToEndSources.UserClr(sqlServer);

        var rows = await connection.ListViewGrades(cancellationToken: TestContext.Current.CancellationToken);

        rows.Select(static row => row.Grade).ShouldBe([Grade.Of(5), Grade.Of(7)]);
    }
}
