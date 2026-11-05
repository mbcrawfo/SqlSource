// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The generator parameter no-list-padding: carried to the query's method as QueryMethod.PadsLists, true without it.
public sealed class ListPaddingParameterTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("no-list-padding", false)]
    public void Run_NoListPadding_ReachesTheMethodAsPadsLists(string? parameter, bool expected)
    {
        var sql =
            (parameter is null ? string.Empty : "-- generator: " + parameter + "\n")
            + "-- name: ListUsers\nSELECT id, name FROM users;\n";

        var run = Run([new SqlFile(GeneratorHarness.UsersPath, sql)], null);

        run.ShouldBeClean();
        Method(run).PadsLists.ShouldBe(expected);
    }

    private static QueryMethod Method(GeneratorRun run) =>
        StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods.ShouldHaveSingleItem();

    // The first of the files, a .sql file, with its sidecar, at CodeGen under postgres on Npgsql 10, on App.Sample.
    private static GeneratorRun Run(SqlFile[] files, string? projectFile)
    {
        var file = files[0];

        return GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [
                .. files,
                Sidecars.Of(file, Entry(file, "ListUsers", [], [Column("id"), Column("name", Text, nullable: true)])),
            ],
            references: [NpgsqlReferences.Npgsql10],
            generatorParameters: projectFile,
            dialect: "postgres"
        );
    }
}
