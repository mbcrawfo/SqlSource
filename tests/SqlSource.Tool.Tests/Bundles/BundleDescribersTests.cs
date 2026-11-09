using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// The describer a bundle is replayed with: the host's, or PostgreSQL's with the bundle's settings of the planner.
public sealed class BundleDescribersTests
{
    private static DescriberRegistry Registry => ToolHost.Create().Describers;

    // walk-left-hash was recorded with the hash join alone, and its capture's explain carries the settings.
    [Fact]
    public async Task For_PostgresWithSettings_DescribesWithThem()
    {
        var bundle = BundleReplays.OfCase("postgres", "walk-left-hash");
        bundle.ExplainSettings.ShouldNotBeEmpty();

        var registry = Registry;

        var described = BundleDescribers.For(
            SqlDialect.PostgreSql,
            registry,
            bundle.ExplainSettings,
            "postgres",
            BundleInfos.Path
        );
        var replayed = await BundleReplays.ReplayAsync(bundle, BundleInfos.Path, registry);

        described.Value.ShouldBeOfType<PostgresDescriber>().ShouldNotBeSameAs(registry.Find(SqlDialect.PostgreSql));
        // The entry a replay gives is this tool's.
        replayed
            .ShouldBeOfType<ReplayOutcome.Described>()
            .Entry.ShouldBe(
                BundleReplay
                    .ExpectedOf(bundle, BundleInfos.Path)
                    .Value.ShouldBeOfType<ReplayOutcome.Described>()
                    .Entry with
                {
                    ToolVersion = PackageVersion.Prefix,
                }
            );
    }

    // Without its settings the describer asks another explain, which the capture does not hold.
    [Fact]
    public async Task For_PostgresWithoutTheSettingsItWasRecordedWith_MissesTheExplain()
    {
        var bundle = BundleReplays.OfCase("postgres", "walk-left-hash") with { ExplainSettings = [] };

        var replayed = await BundleReplays.ReplayAsync(bundle, BundleInfos.Path);

        replayed.ShouldBeOfType<ReplayOutcome.Missed>().Method.ShouldBe("explain");
    }

    [Fact]
    public void For_NoSettings_IsTheRegistrysDescriber()
    {
        var registry = Registry;

        var described = BundleDescribers.For(SqlDialect.SqlServer, registry, [], "mssql", BundleInfos.Path);

        described.Value.ShouldBeSameAs(registry.Find(SqlDialect.SqlServer));
    }

    [Fact]
    public void For_BadSettingLine_IsSqlsrc288()
    {
        var described = BundleDescribers.For(
            SqlDialect.PostgreSql,
            Registry,
            ["enable_hashjoin=off", "work_mem=1GB"],
            "postgres",
            BundleInfos.Path
        );

        var error = described.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(BundleInfos.Path);
        error.Arguments.ToArray().ShouldBe(["the line 'work_mem=1GB' of explain-settings.txt is no planner setting"]);
    }

    [Fact]
    public void For_SettingLineWithAnEscapeSequence_IsSqlsrc288WithItsEscape() =>
        BundleDescribers
            .For(SqlDialect.PostgreSql, Registry, ["work_mem\u001b[2J\t=1GB"], "postgres", BundleInfos.Path)
            .Failure.ShouldNotBeNull()
            .Arguments.ToArray()
            .ShouldBe(["the line 'work_mem\\u001B[2J\t=1GB' of explain-settings.txt is no planner setting"]);

    [Fact]
    public void For_SqlServerWithSettings_IsSqlsrc288()
    {
        var described = BundleDescribers.For(
            SqlDialect.SqlServer,
            Registry,
            ["enable_hashjoin=off"],
            "mssql",
            BundleInfos.Path
        );

        var error = described.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments.ToArray().ShouldBe(["a SQL Server bundle has no explain-settings.txt"]);
    }

    [Fact]
    public void For_EngineWithNoDescriber_IsSqlsrc291()
    {
        var empty = BundleDescribers.For(
            SqlDialect.PostgreSql,
            DescriberRegistry.Empty,
            [],
            "postgres",
            BundleInfos.Path
        );
        var oracle = BundleDescribers.For(SqlDialect.Oracle, Registry, [], "oracle", BundleInfos.Path);

        var error = empty.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Path.ShouldBe(BundleInfos.Path);
        error.Arguments.ToArray().ShouldBe(["postgres"]);
        oracle.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC291");
        oracle.Failure.Arguments.ToArray().ShouldBe(["oracle"]);
    }
}
