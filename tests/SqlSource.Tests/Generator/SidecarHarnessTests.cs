// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The harness hands the generator a sidecar beside a .sql file, and computes a query's hash as the generator does.
public sealed class SidecarHarnessTests
{
    private const string SqlPath = "/app/Repo/Users.sql";

    private const string QuerySql = "-- name: Q\nSELECT @id;\n";

    [Fact]
    public void HashOf_Query_IsTheHashOfItsSqlUnderTheDialect()
    {
        var file = new SqlFile(SqlPath, QuerySql);

        var parsed = SqlFileParser.Parse(QuerySql, "Users.sql", SqlDialect.PostgreSql);
        var block = parsed.Blocks.Single();
        var expected = SqlQueryHash.Compute(SqlDialect.PostgreSql, block.Segments, block.Tokens, block.Parameters);

        Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql).ShouldBe(expected);
        Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql).ShouldNotBe(Sidecars.HashOf(file, "Q", SqlDialect.Ansi));
    }

    [Fact]
    public void Current_File_ReadsBackWithAnEntryOfEachQuery()
    {
        var file = new SqlFile(SqlPath, "-- name: Q\nSELECT @id;\n\n-- name: R\nSELECT 1;\n");

        var sidecar = Sidecars.Current(file, SqlDialect.PostgreSql, "Q", "R");

        sidecar.Path.ShouldBe(SqlPath + ".json");
        var read = SidecarReader.Read(sidecar.Text!).Sidecar!;
        read.FormatVersion.ShouldBe(SidecarFormat.Version);
        read.ToolVersion.ShouldBe(PackageVersion.Prefix);
        read.Queries.Select(entry => entry.Name).ShouldBe(["Q", "R"]);
        read.Queries.Select(entry => entry.Hash)
            .ShouldBe([
                Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql),
                Sidecars.HashOf(file, "R", SqlDialect.PostgreSql),
            ]);
        read.Queries.Select(entry => entry.Engine).ShouldAllBe(engine => engine == "postgres");
    }

    [Fact]
    public void Current_FileWithADialectMarker_UsesTheMarkersDialect()
    {
        var text = "-- dialect: mssql\n-- name: Q\nSELECT @id;\n";
        var file = new SqlFile(SqlPath, text);

        var sidecar = Sidecars.Current(file, SqlDialect.PostgreSql, "Q");

        var parsed = SqlFileParser.Parse(text, "Users.sql", SqlDialect.SqlServer);
        var block = parsed.Blocks.Single();
        var expected = SqlQueryHash.Compute(SqlDialect.SqlServer, block.Segments, block.Tokens, block.Parameters);
        var entry = SidecarReader.Read(sidecar.Text!).Sidecar!.Queries.Single();
        entry.Hash.ShouldBe(expected);
        entry.Engine.ShouldBe("mssql");
        Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql).ShouldBe(expected);
    }

    [Fact]
    public void HashOf_FileThatDoesNotParse_ThrowsThatItDoesNotParse()
    {
        var file = new SqlFile(SqlPath, "-- name: Q\nSELECT 1;\n\n-- name: Q\nSELECT 2;\n");

        var thrown = Should.Throw<InvalidOperationException>(() => Sidecars.HashOf(file, "Q"));

        thrown.Message.ShouldContain("does not parse: DuplicateName");
    }

    [Fact]
    public void Entry_Query_HasNoRowsAndNoParametersAgainstTheMainDatabase()
    {
        var entry = Sidecars.Entry("Q", "h");

        entry.Name.ShouldBe("Q");
        entry.Hash.ShouldBe("h");
        entry.Engine.ShouldBe("postgres");
        entry.Database.ShouldBe("main");
        entry.ServerVersion.ShouldBe("18.0");
        entry.ResultKind.ShouldBe(SidecarResultKind.None);
        entry.Parameters.Count.ShouldBe(0);
        entry.Columns.ShouldBeNull();
    }
}
