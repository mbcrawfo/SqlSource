using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// "relations" against a real server: a row for each relation that exists, in the shape "tables" answers.
[Collection(UsesPostgres.Name)]
public sealed class RelationsCallTests(PostgresDatabases postgres)
{
    [Fact]
    public async Task RelationsAsync_RelationsThatExist_AnswerTheirRowsAsTablesDoes()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.RelationsAsync(
            new PostgresRelationsRequest([
                new PostgresRelationName("billing", "invoices"),
                new PostgresRelationName("public", "orders"),
                new PostgresRelationName("public", "users"),
            ]),
            token
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        var rows = answer.Tables.ShouldNotBeNull();
        rows.Select(static row => (row.Schema, row.Name))
            .Order()
            .ShouldBe([("billing", "invoices"), ("public", "orders"), ("public", "users")]);

        // The rows are the ones "tables" gives for the same OIDs.
        var tables = await live.TablesAsync(new PostgresTablesRequest([.. rows.Select(static row => row.Oid)]), token);
        tables.Error.ShouldBeNull(tables.Error?.Message);
        rows.Select(Shape).ShouldBe(tables.Tables.ShouldNotBeNull().Select(Shape));

        var users = rows.Single(static row => row.Name == "users");
        users.Relkind.ShouldBe("r");
        users.Columns.Select(static column => (column.Name, column.NotNull)).ShouldContain(("id", true));
        users.Columns.Select(static column => (column.Name, column.NotNull)).ShouldContain(("email", false));
    }

    // A relation that does not exist, a name in another schema than the relation's, and a name that differs by its
    // case, have no row and no error.
    [Fact]
    public async Task RelationsAsync_RelationsThatDoNotExist_AnswerNoRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.RelationsAsync(
            new PostgresRelationsRequest([
                new PostgresRelationName("billing", "users"),
                new PostgresRelationName("no_such_schema", "users"),
                new PostgresRelationName("public", "Users"),
                new PostgresRelationName("public", "no_such_table"),
                new PostgresRelationName("public", "orders"),
            ]),
            token
        );

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Tables.ShouldNotBeNull().Select(static row => (row.Schema, row.Name)).ShouldBe([("public", "orders")]);
    }

    [Fact]
    public async Task RelationsAsync_NoRelations_AnswersNoRows()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var answer = await live.RelationsAsync(new PostgresRelationsRequest([]), TestContext.Current.CancellationToken);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        answer.Tables.ShouldNotBeNull().ShouldBeEmpty();
    }

    // A row with its columns as one comparable text, since the columns of a record are a list.
    private static string Shape(PostgresTableRow row) =>
        $"{row.Oid} {row.Relkind} {row.Schema}.{row.Name} {row.Relhassubclass} {row.HasForeignPartition} "
        + $"{row.HasInsteadRule}: {string.Join(", ", row.Columns.Select(Shape))}";

    private static string Shape(PostgresAttributeRow column) =>
        $"{column.Attnum} {column.Name} {column.NotNull} {column.Identity} {column.Generated} {column.TypeOid} "
        + $"{column.NotNullValidated}";
}
