using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the major version of the server, its floor, and what is refused before
// anything is asked.  A call the script does not hold is a CaptureMissException, so a describe that asks more than its
// test wrote fails.
public sealed class PostgresSessionTests
{
    // The script holds "open" alone: the server is refused before any query is described.
    [Theory]
    [InlineData("13.12")]
    [InlineData("9.6.24")]
    public async Task OpenAsync_ServerOlderThanTheFloor_IsSqlsrc235(string version)
    {
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, PostgresScript.Opened with { ServerVersion = version })
            .Play();

        var opened = await PostgresScript.OpenAsync(exchange);

        opened.Value.ShouldBeNull();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC235");
        failure.Arguments.ShouldBe([PostgresScript.Database, "PostgreSQL", version, "14"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBeEmpty();
        Failures
            .MessageOf(failure)
            .ShouldBe($"The database 'app' runs PostgreSQL {version}, and the tool supports 14 and later");
    }

    // A server at or above the floor opens, and so does a version with no leading digits, which the tool cannot read.
    [Theory]
    [InlineData("14.0")]
    [InlineData("18beta1")]
    [InlineData("devel")]
    public async Task OpenAsync_ServerAtOrAboveTheFloor_Opens(string version)
    {
        var exchange = new PostgresScript()
            .Answer("open", PostgresOpenRequest.Instance, PostgresScript.Opened with { ServerVersion = version })
            .Play();

        await using var session = await PostgresScript.SessionAsync(exchange);

        session.Server.Version.ShouldBe(version);
    }

    // The script holds "open" alone: nothing may be asked for a text of two statements.
    [Theory]
    [InlineData("DELETE FROM users WHERE id = @id; DELETE FROM orders")]
    [InlineData("SELECT 1;;")]
    public async Task DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing(string sql)
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(Listed(sql, Inferred("id")), TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    // The script holds "open" alone: a statement that PREPARE does not take is refused before anything is asked.
    [Fact]
    public async Task DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Listed("CALL archive(@id)", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC233");
        failure.Arguments.ShouldBe(["Q", "CALL"]);
    }

    // The script holds "open" alone: a "$n" that the SQL writes is refused before anything is asked.
    [Fact]
    public async Task DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Listed("SELECT 1 FROM users WHERE id = @id AND name = $2", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC234");
        failure.Arguments.ShouldBe(["Q", "$2"]);
    }

    // The statements are counted before the kind is read.
    [Fact]
    public async Task DescribeAsync_SeveralStatementsThatStartWithCall_IsSqlsrc230()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Listed("CALL a(); CALL b()"),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Listed("SELECT 1 FROM users WHERE id = @id", Declared("id", "int) AS SELECT 1; DROP TABLE users; --")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[1].ShouldBe("@id");
        failure.Arguments[3].ShouldBe(DescribeFailures.NotATypeName);
    }

    // The statements are counted before the types are read: the first thing wrong is the first reported.
    [Fact]
    public async Task DescribeAsync_SeveralStatementsAndABadType_IsSqlsrc230()
    {
        await using var session = await PostgresScript.SessionAsync(new PostgresScript().Open().Play());

        var described = await session.DescribeAsync(
            Listed("SELECT @id; SELECT 2", Declared("id", "int foo")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
    }
}
