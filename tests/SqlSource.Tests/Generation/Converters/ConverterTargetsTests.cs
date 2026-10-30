using Shouldly;
using SqlSource.Generation.Converters;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using Xunit;

namespace SqlSource.Tests.Generation.Converters;

public sealed class ConverterTargetsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_SqlServer_IsSqlServer(bool npgsqlAtLeast10) =>
        ConverterTargetsExtensions.Of(DatabaseEngine.SqlServer, npgsqlAtLeast10).ShouldBe(ConverterTargets.SqlServer);

    [Fact]
    public void Of_PostgresBefore10_IsNpgsqlBefore10() =>
        ConverterTargetsExtensions.Of(DatabaseEngine.PostgreSql, false).ShouldBe(ConverterTargets.NpgsqlBefore10);

    [Fact]
    public void Of_Postgres10_IsNpgsql10() =>
        ConverterTargetsExtensions.Of(DatabaseEngine.PostgreSql, true).ShouldBe(ConverterTargets.Npgsql10);

    [Fact]
    public void Serves_ProjectConverter_ServesEveryTarget()
    {
        var converter = ConverterOf(null);

        converter.Serves(ConverterTargets.SqlServer).ShouldBeTrue();
        converter.Serves(ConverterTargets.NpgsqlBefore10).ShouldBeTrue();
        converter.Serves(ConverterTargets.Npgsql10).ShouldBeTrue();
    }

    [Fact]
    public void Serves_SqlServerEdge_DoesNotServeNpgsql10()
    {
        var converter = ConverterOf(new LibraryScope(SqlLibrary.NodaTime, ConverterTargets.SqlServer));

        converter.Serves(ConverterTargets.SqlServer).ShouldBeTrue();
        converter.Serves(ConverterTargets.Npgsql10).ShouldBeFalse();
        converter.Serves(ConverterTargets.NpgsqlBefore10).ShouldBeFalse();
    }

    private static Converter ConverterOf(LibraryScope? library) =>
        new(
            "global::App.C.A",
            "App.C.A(int)",
            "int",
            true,
            "long",
            true,
            EquatableArray<string>.Empty,
            null,
            library is null ? ConverterOrigin.Project : ConverterOrigin.Library,
            null,
            library
        );
}
