// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generator;

// The harness hands the generator a sidecar beside a .sql file, computes a query's hash as the generator does, and
// fails a test whose current entry does not list its query's parameters as the tool writes them.
public sealed class SidecarHarnessTests
{
    private const string QuerySql = "-- name: Q\nSELECT @id;\n";

    [Fact]
    public void HashOf_Query_IsTheHashOfItsSqlUnderTheDialect()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, QuerySql);

        var parsed = SqlFileParser.Parse(QuerySql, "Users.sql", SqlDialect.PostgreSql);
        var block = parsed.Blocks.Single();
        var expected = SqlQueryHash.Compute(SqlDialect.PostgreSql, block.Segments, block.Tokens, block.Parameters);

        Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql).ShouldBe(expected);
        Sidecars.HashOf(file, "Q", SqlDialect.PostgreSql).ShouldNotBe(Sidecars.HashOf(file, "Q", SqlDialect.Ansi));
    }

    [Fact]
    public void Current_File_ReadsBackWithAnEntryOfEachQuery()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @id;\n\n-- name: R\nSELECT 1;\n");

        var sidecar = Sidecars.Current(file, SqlDialect.PostgreSql, "Q", "R");

        sidecar.Path.ShouldBe(GeneratorHarness.UsersPath + ".json");
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
        var file = new SqlFile(GeneratorHarness.UsersPath, text);

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
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT 1;\n\n-- name: Q\nSELECT 2;\n");

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

    [Fact]
    public void Of_CurrentEntryWithoutTheSqlsParameter_ThrowsNamingBothLists()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, QuerySql);

        var thrown = Should.Throw<InvalidOperationException>(() => Sidecars.Of(file, EntryOf(file, "Q")));

        thrown.Message.ShouldContain("Q");
        thrown.Message.ShouldContain("[]");
        thrown.Message.ShouldContain("[id]");
    }

    [Fact]
    public void Of_CurrentEntryWithAParameterTheSqlLacks_Throws()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT 1;\n");

        _ = Should.Throw<InvalidOperationException>(() => Sidecars.Of(file, EntryOf(file, "Q", Parameter(0, "id"))));
    }

    [Fact]
    public void Of_CurrentEntryOutOfTheSqlsOrder_Throws()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @a, @b;\n");

        _ = Should.Throw<InvalidOperationException>(() =>
            Sidecars.Of(file, EntryOf(file, "Q", Parameter(0, "b"), Parameter(1, "a")))
        );
    }

    [Fact]
    public void Of_CurrentEntryInAnotherSpelling_Throws()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @author;\n");

        _ = Should.Throw<InvalidOperationException>(() =>
            Sidecars.Of(file, EntryOf(file, "Q", Parameter(0, "Author")))
        );
    }

    [Fact]
    public void Of_CurrentEntryWhoseOrdinalIsNotItsPlace_Throws()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @a;\n");

        var thrown = Should.Throw<InvalidOperationException>(() =>
            Sidecars.Of(file, EntryOf(file, "Q", Parameter(1, "a")))
        );

        thrown.Message.ShouldContain("[a@1]");
    }

    [Fact]
    public void Of_StaleEntry_IsNotChecked()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT 1;\n");
        var entry = Sidecars.Entry("Q", "h") with { Parameters = TestSidecars.Of(Parameter(0, "id")) };

        _ = Should.NotThrow(() => Sidecars.Of(file, entry));
    }

    [Fact]
    public void Of_EntryOfAQueryTheFileLacks_IsNotChecked()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, QuerySql);
        var entry = EntryOf(file, "Q", Parameter(0, "x")) with { Name = "Gone" };

        _ = Should.NotThrow(() => Sidecars.Of(file, entry));
    }

    [Fact]
    public void Of_FileThatDoesNotParse_WritesTheSidecar()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT 1;\n\n-- name: Q\nSELECT 2;\n");

        var sidecar = Sidecars.Of(file, Sidecars.Entry("Q", "h"));

        SidecarReader.Read(sidecar.Text!).Sidecar!.Queries.Single().Name.ShouldBe("Q");
    }

    [Fact]
    public void Of_EntryHashedUnderAnotherDialect_IsChecked()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, QuerySql);
        var entry = Sidecars.Entry("Q", Sidecars.HashOf(file, "Q"), "postgres");

        _ = Should.Throw<InvalidOperationException>(() => Sidecars.Of(file, entry));
    }

    [Fact]
    public void Of_EntryListingTheSqlsParameters_WritesTheSidecar()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @b, @a, @b;\n");

        var sidecar = Sidecars.Of(file, EntryOf(file, "Q", Parameter(0, "b"), Parameter(1, "a")));

        SidecarReader
            .Read(sidecar.Text!)
            .Sidecar!.Queries.Single()
            .Parameters.Select(parameter => parameter.Name)
            .ShouldBe(["b", "a"]);
    }

    [Fact]
    public void HandEdited_EntryInAnotherSpelling_WritesItAsGiven()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @author;\n");

        var sidecar = Sidecars.HandEdited(file, EntryOf(file, "Q", Parameter(0, "Author")));

        SidecarReader
            .Read(sidecar.Text!)
            .Sidecar!.Queries.Single()
            .Parameters.Select(parameter => parameter.Name)
            .ShouldBe(["Author"]);
    }

    [Fact]
    public void Current_QueryWithParameters_ListsThemAsTheSqlHasThem()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\nSELECT @b, @a, @b;\n");

        var sidecar = Sidecars.Current(file, SqlDialect.PostgreSql, "Q");

        var parameters = SidecarReader.Read(sidecar.Text!).Sidecar!.Queries.Single().Parameters;
        parameters.Select(parameter => parameter.Name).ShouldBe(["b", "a"]);
        parameters.Select(parameter => parameter.Ordinal).ShouldBe([0, 1]);
        parameters.ShouldAllBe(parameter => parameter.Type == null);
    }

    [Fact]
    public void Current_QueryWithAMarkerOnlyParameter_ListsItLast()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: Q\n-- param: @z int4\nSELECT @a;\n");

        var sidecar = Sidecars.Current(file, SqlDialect.PostgreSql, "Q");

        SidecarReader
            .Read(sidecar.Text!)
            .Sidecar!.Queries.Single()
            .Parameters.Select(parameter => parameter.Name)
            .ShouldBe(["a", "z"]);
    }

    // An entry of the query for its hash under PostgreSQL, against the engine postgres.
    private static SidecarEntry EntryOf(SqlFile file, string name, params SidecarParameter[] parameters) =>
        Sidecars.Entry(name, Sidecars.HashOf(file, name, SqlDialect.PostgreSql)) with
        {
            Parameters = TestSidecars.Of(parameters),
        };

    private static SidecarParameter Parameter(int ordinal, string name) => TestSidecars.Parameter(ordinal, name, null);
}
