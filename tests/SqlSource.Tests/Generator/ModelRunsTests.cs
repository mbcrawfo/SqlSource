// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The helpers of the model tests, where a test would not see a slip in them: the numbering of an entry and the files
// of the models.
public sealed class ModelRunsTests
{
    private static readonly SqlFile Users = new(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n");

    [Fact]
    public void Entry_ParametersAndColumns_AreNumberedByTheirPlace()
    {
        var entry = Entry(Users, "GetUser", [Parameter("a"), Parameter("b")], [Column("x"), Column("y")]);

        entry.Parameters.Select(parameter => parameter.Ordinal).ShouldBe([0, 1]);
        entry.Columns.ShouldNotBeNull().Select(column => column.Ordinal).ShouldBe([0, 1]);
        entry.ResultKind.ShouldBe(SidecarResultKind.Rows);
        entry.Hash.ShouldBe(Sidecars.HashOf(Users, "GetUser", SqlDialect.PostgreSql));
    }

    [Fact]
    public void Entry_NullColumns_HasNoRows()
    {
        var entry = Entry(Users, "GetUser", [Parameter("a")], null);

        entry.ResultKind.ShouldBe(SidecarResultKind.None);
        entry.Columns.ShouldBeNull();
    }

    [Fact]
    public void ModelFiles_Run_AreTheModelsHintNames()
    {
        var run = Run(Users, Entry(Users, "GetUser", [Parameter("id")], [Column("id")]));

        ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs", "App.GetUserParams-Model.g.cs"]);
    }
}
