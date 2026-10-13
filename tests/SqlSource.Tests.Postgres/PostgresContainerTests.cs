using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

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
        // "postgres:18" runs a server whose version starts "18.".  A tag that starts with no number says nothing.
        var major = new string([.. PostgresContainer.Image.Split(':')[^1].TakeWhile(char.IsAsciiDigit)]);
        if (major.Length > 0)
        {
            reader.GetString(1).ShouldStartWith(major + ".");
        }
    }

    [Fact]
    public void Image_OfTheRun_IsTheVariablesValueOrTheDefault()
    {
        var named = Environment.GetEnvironmentVariable(PostgresContainer.ImageVariable);

        PostgresContainer.Image.ShouldBe(string.IsNullOrEmpty(named) ? PostgresContainer.DefaultImage : named);
    }
}
