using System;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The SQL of a case as an in-process run of the generator at Models writes it, from the committed corpora: the query,
// its sidecar and the settings files of a build, with the project's output replaced by Models.  It needs no server.
public sealed class RealisticGenerationTests
{
    private const string TableStatementSql = "TABLE billing.invoices\nORDER BY issued_on DESC\nLIMIT @limit;";

    [Fact]
    public void SqlOf_PostgresCase_IsItsQueryAsTheBuildWritesIt()
    {
        var found = RealisticCorpus.Postgres.ReadNamed("table-statement");

        RealisticGeneration
            .SqlOf(RealisticCorpus.Postgres, found, RealisticModels.EntryOf(found))
            .ShouldBe(TableStatementSql);
    }

    [Fact]
    public void SqlOf_SqlServerCase_IsItsQueryAsTheBuildWritesIt()
    {
        var found = RealisticCorpus.SqlServer.ReadNamed("customers-distinct-from");

        RealisticGeneration
            .SqlOf(RealisticCorpus.SqlServer, found, RealisticModels.EntryOf(found))
            .ShouldBe(
                "SELECT\n    c.id,\n    c.name,\n    c.email,\n    c.time_zone,\n    c.closed_at\n"
                    + "FROM dbo.customers AS c\nWHERE c.time_zone IS DISTINCT FROM @time_zone\nORDER BY c.name;"
            );
    }

    [Fact]
    public void RunAtModels_Case_GivesTheProjectsSettingsAtModels()
    {
        var found = RealisticCorpus.Postgres.ReadNamed("table-statement");
        var query = RealisticGeneration.QueryOf(found);

        var run = RealisticGeneration.RunAtModels(
            RealisticCorpus.Postgres,
            query,
            RealisticGeneration.CommittedSidecarOf(found, query),
            []
        );

        var settings = JsonNode.Parse(run.ProjectSettings.ShouldNotBeNull()).ShouldBeOfType<JsonObject>();
        settings["output"].ShouldNotBeNull().GetValue<string>().ShouldBe("Models");
        settings["dialect"].ShouldNotBeNull().GetValue<string>().ShouldBe("postgres");
        settings["database"].ShouldNotBeNull().GetValue<string>().ShouldBe("app");
        settings["inputModelSuffix"].ShouldNotBeNull().GetValue<string>().ShouldBe("Args");
    }

    // A raise of VersionPrefix before the sidecars are described again: the generator gives SQLSRC240, a warning the
    // build keeps, and the SQL is still the query's.
    [Fact]
    public void ConstantOf_SidecarOfAnotherToolVersion_IsStillTheSql()
    {
        var found = RealisticCorpus.Postgres.ReadNamed("table-statement");
        var query = RealisticGeneration.QueryOf(found);
        var sidecar = Sidecars.Of(query, SidecarFormat.Version, "99.0.0", RealisticModels.EntryOf(found));

        var run = RealisticGeneration.RunAtModels(RealisticCorpus.Postgres, query, sidecar, []);

        RealisticGeneration.ConstantOf(run, found, RealisticModels.EntryOf(found).Name).ShouldBe(TableStatementSql);
    }

    // A query.sql edited without describing it again, whose committed entry is stale: no SQL is given from it.
    [Fact]
    public void ConstantOf_RunWithADiagnostic_ThrowsNamingTheCaseAndTheDiagnostic()
    {
        var found = RealisticCorpus.Postgres.ReadNamed("table-statement");
        var committed = RealisticGeneration.QueryOf(found);
        var query = committed with
        {
            Text = committed
                .Text.ShouldNotBeNull()
                .Replace("LIMIT @limit;", "LIMIT @limit + 0;", StringComparison.Ordinal),
        };
        var run = RealisticGeneration.RunAtModels(
            RealisticCorpus.Postgres,
            query,
            RealisticGeneration.CommittedSidecarOf(found, committed),
            []
        );

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticGeneration.ConstantOf(run, found, RealisticModels.EntryOf(found).Name)
        );

        thrown.Message.ShouldContain("realistic/table-statement");
        thrown.Message.ShouldContain("SQLSRC");
    }

    [Fact]
    public void ConstantOf_NameWithoutAConstant_ThrowsNamingTheCaseAndTheName()
    {
        var found = RealisticCorpus.Postgres.ReadNamed("table-statement");
        var query = RealisticGeneration.QueryOf(found);
        var run = RealisticGeneration.RunAtModels(
            RealisticCorpus.Postgres,
            query,
            RealisticGeneration.CommittedSidecarOf(found, query),
            []
        );

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticGeneration.ConstantOf(run, found, name: "Missing")
        );

        thrown.Message.ShouldContain("realistic/table-statement");
        thrown.Message.ShouldContain("Missing");
    }
}
