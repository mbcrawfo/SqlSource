using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Testing;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests.SqlServer;

// The container itself: the image the variable names, pinned to its update and to the build the map records, the
// database of the fixtures with its schema applied, the databases of the end-to-end tests seeded, and the skips by the
// server's major.
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

    [Fact]
    public async Task InitializeAsync_EndToEnd_SeedsEachTable()
    {
        SqlServerContainer.EndToEndDatabases.ShouldBe([
            SqlServerContainer.AppDatabase,
            SqlServerContainer.BillingDatabase,
        ]);

        (await CountAsync(SqlServerContainer.AppDatabase, "dbo.accounts")).ShouldBe(3);
        (await CountAsync(SqlServerContainer.AppDatabase, "dbo.notes")).ShouldBe(3);
        (await CountAsync(SqlServerContainer.AppDatabase, "dbo.all_types")).ShouldBe(2);
        (await CountAsync(SqlServerContainer.BillingDatabase, "billing.invoices")).ShouldBe(3);
    }

    // Row 1 has a value in every column, and row 2 NULL in every column but id and rowversion_value, which the server
    // fills.
    [Fact]
    public async Task InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButTwoOfRow2()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = sqlServer.SourceOf(SqlServerContainer.AppDatabase);
        await using var command = source.CreateCommand("SELECT * FROM dbo.all_types ORDER BY id");
        await using var reader = await command.ExecuteReaderAsync(token);

        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(1);
        (await NullColumnsAsync(reader)).ShouldBeEmpty();
        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(2);
        (await NullColumnsAsync(reader)).Count.ShouldBe(reader.FieldCount - 2);
        (await reader.IsDBNullAsync(reader.GetOrdinal("rowversion_value"), token)).ShouldBeFalse();
    }

    // The table of json and vector is made on SQL Server 2025, major 17, and later, and never on an earlier major.
    [Fact]
    public async Task InitializeAsync_NewTypes_OnlyOn2025()
    {
        await using var source = sqlServer.SourceOf(SqlServerContainer.AppDatabase);
        await using var command = source.CreateCommand("SELECT OBJECT_ID('dbo.new_types')");

        var made = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        (made is not DBNull).ShouldBe(sqlServer.Major >= 17);
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

    [Theory]
    [InlineData(14)]
    [InlineData(17)]
    public void SkipUnlessRecordedMajor_RecordedMajor_DoesNotSkip(int server) =>
        Should.NotThrow(() => SqlServerContainer.SkipUnlessRecordedMajor(server, "The things"));

    [Fact]
    public void SkipUnlessRecordedMajor_OtherMajor_SkipsNamingTheRecordedOnes() =>
        Should
            .Throw<SkipException>(() => SqlServerContainer.SkipUnlessRecordedMajor(16, "The things"))
            .Message.ShouldEndWith("The things are pinned for SQL Server 14 and 17 alone.");

    private static async Task<List<string>> NullColumnsAsync(DbDataReader reader)
    {
        var nulls = new List<string>();
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            if (await reader.IsDBNullAsync(ordinal, TestContext.Current.CancellationToken))
            {
                nulls.Add(reader.GetName(ordinal));
            }
        }

        return nulls;
    }

    private async Task<int> CountAsync(string database, string table)
    {
        await using var source = sqlServer.SourceOf(database);
        await using var command = source.CreateCommand($"SELECT COUNT(*) FROM {table}");
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
