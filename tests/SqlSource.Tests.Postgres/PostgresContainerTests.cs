using System;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests.Postgres;

// The container itself: the image the variable names, the database of the fixtures, and its schema applied.
[Collection(UsesPostgres.Name)]
public sealed class PostgresContainerTests(PostgresContainer postgres)
{
    [Fact]
    public async Task ConnectionString_OfTheContainer_OpensTheFixtureDatabaseOfTheImage()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            "SELECT current_database(), current_setting('server_version'), "
                + "to_regclass('billing.invoices') IS NOT NULL, to_regclass('public.user_orders') IS NOT NULL"
        );

        await using var reader = await command.ExecuteReaderAsync(token);

        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(PostgresContainer.Database);
        reader.GetBoolean(2).ShouldBeTrue();
        reader.GetBoolean(3).ShouldBeTrue();
        // "postgres:18.6" runs a server whose server_version is "18.6 (Debian 18.6-1.pgdg13+1)".
        reader.GetString(1).Split(' ')[0].ShouldBe(PostgresContainer.TagOf(PostgresContainer.Image));
    }

    [Fact]
    public async Task InitializeAsync_EndToEndDatabases_AreSeeded()
    {
        PostgresContainer.EndToEndDatabases.ShouldBe([
            PostgresContainer.AppDatabase,
            PostgresContainer.BillingDatabase,
        ]);

        (await CountAsync(PostgresContainer.AppDatabase, "all_types")).ShouldBe(2);
        (await CountAsync(PostgresContainer.AppDatabase, "accounts")).ShouldBe(3);
        (await CountAsync(PostgresContainer.BillingDatabase, "billing.invoices")).ShouldBe(3);
    }

    [Fact]
    public async Task InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButIdOfRow2()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresContainer.AppDatabase));
        await using var command = source.CreateCommand(
            "SELECT id, (SELECT count(*) FROM jsonb_each(to_jsonb(t)) WHERE value = 'null'::jsonb), "
                + "(SELECT count(*) FROM information_schema.columns "
                + "WHERE table_schema = 'public' AND table_name = 'all_types') FROM all_types t ORDER BY id"
        );

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(1);
        reader.GetInt64(1).ShouldBe(0);
        var columns = reader.GetInt64(2);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(2);
        reader.GetInt64(1).ShouldBe(columns - 1);
    }

    // The oid of the text search configuration "simple" is fixed in the catalog, where that of "english" is made by
    // initdb and differs between majors.
    [Fact]
    public async Task InitializeAsync_AllTypesRegconfig_IsTheFixedOidOfSimple()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresContainer.AppDatabase));
        await using var command = source.CreateCommand("SELECT regconfig_value::oid FROM all_types WHERE id = 1");

        var oid = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        oid.ShouldBe(3748u);
    }

    // Every image the tests run, the default or the one the variable names, is pinned to a major and a minor.  A
    // bare major moves to each new minor unseen; see tests/SqlSource.Tests.Postgres/README.md.
    [Fact]
    public void Image_OfTheRun_IsPinnedToAMajorAndAMinor() =>
        PostgresContainer.TagOf(PostgresContainer.Image).ShouldMatch(@"^\d+\.\d+$");

    [Fact]
    public void Image_OfTheRun_IsTheVariablesValueOrTheDefault()
    {
        var named = Environment.GetEnvironmentVariable(PostgresContainer.ImageVariable);

        PostgresContainer.Image.ShouldBe(string.IsNullOrEmpty(named) ? PostgresContainer.DefaultImage : named);
    }

    [Fact]
    public void SkipBelow_ServerOfThatMajor_DoesNotSkip() =>
        Should.NotThrow(() => PostgresContainer.SkipBelow(18, 18, "a reason"));

    [Fact]
    public void SkipBelow_ServerBeforeThatMajor_SkipsWithTheReason() =>
        Should
            .Throw<SkipException>(() => PostgresContainer.SkipBelow(17, 18, "a reason"))
            .Message.ShouldEndWith("a reason");

    [Fact]
    public void SkipBelow_OfTheContainer_AsksTheServersMajor()
    {
        Should.NotThrow(() => postgres.SkipBelow(postgres.Major, "a reason"));
        _ = Should.Throw<SkipException>(() => postgres.SkipBelow(postgres.Major + 1, "a reason"));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(18)]
    public void SkipUnlessRecordedMajor_RecordedMajor_DoesNotSkip(int server) =>
        Should.NotThrow(() => PostgresContainer.SkipUnlessRecordedMajor(server, "The things"));

    [Fact]
    public void SkipUnlessRecordedMajor_OtherMajor_SkipsNamingTheRecordedOnes() =>
        Should
            .Throw<SkipException>(() => PostgresContainer.SkipUnlessRecordedMajor(16, "The things"))
            .Message.ShouldEndWith("The things are pinned for PostgreSQL 14 and 18 alone.");

    private async Task<long> CountAsync(string database, string table)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(database));
        await using var command = source.CreateCommand($"SELECT count(*) FROM {table}");
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
