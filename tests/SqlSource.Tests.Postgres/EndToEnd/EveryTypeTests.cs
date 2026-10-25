using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// Every type of the type map written through a method's parameter and read back through a method's column, on
// Npgsql 10: each column of all_types by InsertEveryType, with the values of row 1 and with the NULLs of row 2, each
// read by the method, and cube, which all_types leaves out, by EchoCube.  Each insert runs in a transaction that is
// rolled back.  Map_EveryKey_IsWrittenAndReadOrNamed of ModelShapeTests holds the map's keys to these tests.
[Collection(UsesPostgres.Name)]
public sealed class EveryTypeTests(PostgresContainer postgres)
{
    private const int FullRowCopyId = 3;

    private const int NullRowCopyId = 4;

    // The length of varchar_value, varchar(20).
    private const int VarcharLength = 20;

    [Fact]
    public async Task InsertEveryType_FullRow_ReadsEveryValueBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var full = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );

        var rows = await connection.InsertEveryType(
            AllTypesQueryTests.ArgsFrom<InsertEveryTypeArgs>(full, FullRowCopyId),
            transaction,
            cancellationToken: token
        );
        var read = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == FullRowCopyId
        );
        await transaction.RollbackAsync(token);

        AllTypesRows.ShouldBeTheFullRow(rows.ShouldHaveSingleItem(), FullRowCopyId);
        AllTypesRows.ShouldBeTheFullRow(read, FullRowCopyId);
    }

    [Fact]
    public async Task InsertEveryType_NullRow_ReadsEveryNullBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var nulls = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.NullRowId
        );

        var rows = await connection.InsertEveryType(
            AllTypesQueryTests.ArgsFrom<InsertEveryTypeArgs>(nulls, NullRowCopyId),
            transaction,
            cancellationToken: token
        );
        var read = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == NullRowCopyId
        );
        await transaction.RollbackAsync(token);

        AllTypesRows.ShouldBeTheNullRow(rows.ShouldHaveSingleItem(), NullRowCopyId);
        AllTypesRows.ShouldBeTheNullRow(read, NullRowCopyId);
    }

    // The parameter is bound as character varying, with no length, so the value reaches the server whole, which refuses
    // it for the column of varchar(20) and never cuts it.
    [Fact]
    public async Task InsertEveryType_VarcharLongerThanItsLength_IsRefusedByTheServer()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var nulls = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.NullRowId
        );
        var arguments = AllTypesQueryTests.ArgsFrom<InsertEveryTypeArgs>(nulls, NullRowCopyId) with
        {
            VarcharValue = new string('x', VarcharLength + 1),
        };

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            connection.InsertEveryType(arguments, transaction, cancellationToken: token)
        );
        await transaction.RollbackAsync(token);

        exception.SqlState.ShouldBe(PostgresErrorCodes.StringDataRightTruncation, exception.MessageText);
    }

    [Fact]
    public async Task EchoCube_AValue_ReadsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var cube = new NpgsqlCube([1.5, -2.25, 3], [4, 5.5, -6]);

        var rows = await connection.EchoCube(new EchoCubeArgs(cube), cancellationToken: token);

        rows.ShouldHaveSingleItem().Value.ShouldBe(cube);
    }
}
