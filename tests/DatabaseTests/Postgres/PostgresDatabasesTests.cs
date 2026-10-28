using System;
using System.Globalization;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.DatabaseTests.Postgres;

// The container itself: the image the variable names, the database of the fixtures with its schema applied, the
// databases of the end-to-end tests seeded, and the skips by the server's major.
[Collection(UsesPostgres.Name)]
public sealed class PostgresDatabasesTests(PostgresDatabases postgres)
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
        reader.GetString(0).ShouldBe(PostgresDatabases.Database);
        reader.GetBoolean(2).ShouldBeTrue();
        reader.GetBoolean(3).ShouldBeTrue();
        // "postgres:18.6" runs a server whose server_version is "18.6 (Debian 18.6-1.pgdg13+1)".
        reader.GetString(1).Split(' ')[0].ShouldBe(EngineImage.TagOf(EngineImage.Postgres.Image));
    }

    // server_version_num is 180006 on 18.6: the major is the tag's first number.
    [Fact]
    public void Major_OfTheRun_IsTheMajorOfTheImagesTag() =>
        postgres.Major.ShouldBe(
            int.Parse(EngineImage.TagOf(EngineImage.Postgres.Image).Split('.')[0], CultureInfo.InvariantCulture)
        );

    [Fact]
    public async Task InitializeAsync_EndToEndDatabases_AreSeeded()
    {
        PostgresDatabases.EndToEndDatabases.ShouldBe([
            PostgresDatabases.AppDatabase,
            PostgresDatabases.BillingDatabase,
        ]);

        (await CountAsync(PostgresDatabases.AppDatabase, "all_types")).ShouldBe(2);
        (await CountAsync(PostgresDatabases.AppDatabase, "accounts")).ShouldBe(3);
        (await CountAsync(PostgresDatabases.BillingDatabase, "billing.invoices")).ShouldBe(3);
    }

    [Fact]
    public async Task InitializeAsync_AllTypes_HasAValueInEveryColumnOfRow1AndNullInEveryColumnButIdOfRow2()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
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
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(PostgresDatabases.AppDatabase));
        await using var command = source.CreateCommand("SELECT regconfig_value::oid FROM all_types WHERE id = 1");

        var oid = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        oid.ShouldBe(3748u);
    }

    // Every image the tests run, the default or the one the variable names, is pinned to a major and a minor.  A
    // bare major moves to each new minor unseen; see tests/DatabaseTests/Postgres/README.md.
    [Fact]
    public void Image_OfTheRun_IsPinnedToAMajorAndAMinor() =>
        EngineImage.TagOf(EngineImage.Postgres.Image).ShouldMatch(@"^\d+\.\d+$");

    [Fact]
    public void Image_OfTheRun_IsTheVariablesValueOrTheDefault()
    {
        var named = Environment.GetEnvironmentVariable(EngineImage.Postgres.Variable);

        EngineImage.Postgres.Image.ShouldBe(string.IsNullOrEmpty(named) ? DefaultImages.Postgres : named);
    }

    [Fact]
    public void SkipBelow_OfTheContainer_AsksTheServersMajor()
    {
        Should.NotThrow(() => postgres.SkipBelow(postgres.Major, "a reason"));
        _ = Should.Throw<SkipException>(() => postgres.SkipBelow(postgres.Major + 1, "a reason"));
    }

    private async Task<long> CountAsync(string database, string table)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionStringOf(database));
        await using var command = source.CreateCommand($"SELECT count(*) FROM {table}");
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
