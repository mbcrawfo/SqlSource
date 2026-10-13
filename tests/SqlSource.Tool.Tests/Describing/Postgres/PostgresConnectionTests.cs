using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the driver threw, as the answer of a call holds it.
public class PostgresConnectionTests
{
    [Fact]
    public void ErrorOf_ExceptionOfTheDriver_IsItsMessageAlone() =>
        PostgresConnection
            .ErrorOf(new NpgsqlException("Failed to connect to 127.0.0.1:1"))
            .ShouldBe(new PostgresServerError(null, "Failed to connect to 127.0.0.1:1", null, null, null));

    [Fact]
    public void ErrorOf_ExceptionOfTheServer_HasItsSqlStateMessageDetailAndHint() =>
        PostgresConnection
            .ErrorOf(Server(position: 49))
            .ShouldBe(new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", "a hint", 49));

    // The server sends no position as zero.
    [Fact]
    public void ErrorOf_ExceptionOfTheServerWithPositionZero_HasNoPosition() =>
        PostgresConnection.ErrorOf(Server(position: 0)).Position.ShouldBeNull();

    [Fact]
    public void ErrorOf_ExceptionOfTheServerWithPositionOne_KeepsIt() =>
        PostgresConnection.ErrorOf(Server(position: 1)).Position.ShouldBe(1);

    private static PostgresException Server(int position) =>
        new(
            "relation \"users\" does not exist",
            "ERROR",
            "ERROR",
            "42P01",
            detail: "a detail",
            hint: "a hint",
            position: position
        );
}
