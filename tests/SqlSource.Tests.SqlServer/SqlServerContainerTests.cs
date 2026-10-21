using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Testing;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests.SqlServer;

// The container itself: the image the variable names, pinned to its update and to the build the map records, the
// database of the fixtures with its schema applied, and the skips by the server's major.
[Collection(UsesSqlServer.Name)]
public sealed class SqlServerContainerTests(SqlServerContainer sqlServer)
{
    // A tag that names a cumulative update, with a GDR over it or not, and the Ubuntu it runs on.
    private const string PinnedTag = @"^\d{4}-CU\d+(-GDR\d+)?-ubuntu-\d+\.\d+$";

    [Fact]
    public async Task ConnectionString_OfTheContainer_OpensTheFixtureDatabase()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(
            "SELECT DB_NAME(), OBJECT_ID('billing.invoices'), OBJECT_ID('dbo.user_orders')",
            connection
        );

        await using var reader = await command.ExecuteReaderAsync(token);

        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(SqlServerContainer.Database);
        (await reader.IsDBNullAsync(1, token)).ShouldBeFalse();
        (await reader.IsDBNullAsync(2, token)).ShouldBeFalse();
    }

    // Every image the tests run, the default or the one the variable names, names its cumulative update.  A bare year
    // or -latest moves to each new update unseen; see tests/SqlSource.Tests.SqlServer/README.md.
    [Fact]
    public void Image_OfTheRun_NamesItsCumulativeUpdate() =>
        SqlServerContainer.TagOf(SqlServerContainer.Image).ShouldMatch(PinnedTag);

    [Fact]
    public void Image_OfTheRun_IsInTheMapOfBuilds() =>
        SqlServerContainer.ImageBuilds.ShouldContainKey(SqlServerContainer.Image);

    [Fact]
    public void Image_OfTheRun_IsTheVariablesValueOrTheDefault()
    {
        var named = Environment.GetEnvironmentVariable(SqlServerContainer.ImageVariable);

        SqlServerContainer.Image.ShouldBe(string.IsNullOrEmpty(named) ? DefaultImages.SqlServer : named);
    }

    [Fact]
    public async Task Server_OfTheImage_IsTheBuildTheMapRecords()
    {
        var build = await sqlServer.ScalarAsync("SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))");

        build.ShouldBe(SqlServerContainer.ImageBuilds[SqlServerContainer.Image]);
    }

    [Fact]
    public void ImageBuilds_EveryImage_IsPinnedAndNotLatest()
    {
        foreach (var image in SqlServerContainer.ImageBuilds.Keys)
        {
            SqlServerContainer.TagOf(image).ShouldMatch(PinnedTag);
        }
    }

    [Fact]
    public void SkipBelow_ServerOfThatMajor_DoesNotSkip() =>
        Should.NotThrow(() => SqlServerContainer.SkipBelow(17, 17, "a reason"));

    [Fact]
    public void SkipBelow_ServerBeforeThatMajor_SkipsWithTheReason() =>
        Should
            .Throw<SkipException>(() => SqlServerContainer.SkipBelow(14, 17, "a reason"))
            .Message.ShouldEndWith("a reason");

    [Fact]
    public void SkipBelow_OfTheContainer_AsksTheServersMajor()
    {
        Should.NotThrow(() => sqlServer.SkipBelow(sqlServer.Major, "a reason"));
        _ = Should.Throw<SkipException>(() => sqlServer.SkipBelow(sqlServer.Major + 1, "a reason"));
    }
}
