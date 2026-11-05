// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The C# type a property of a model is declared with, where it is not the type the parameter's or column's type maps
// to: a PostgreSQL array parameter is an IEnumerable<T> of its element in a CodeGen query's input model, which its
// method sends whatever the sequence, and stays an array at Models, where a library hands the value to Npgsql as it is.
// Each run is under postgres on Npgsql 10, at CodeGen unless it says Models, with an entry written for each query's
// current hash.
public sealed class ModelPropertyTypeTests
{
    private const string ParamsModel = "App.FindIdsParams-Model.g.cs";

    private const string Enumerable = "global::System.Collections.Generic.IEnumerable<";

    private static readonly PostgresType IntArray = ArrayOf(Int4, "_int4");

    [Fact]
    public void InputModel_PostgresArrayParameter_IsIEnumerableInCodeGen()
    {
        var run = RunIds(OutputKind.CodeGen, "", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("public sealed record FindIdsParams(" + Enumerable + "int> Ids);");
    }

    [Fact]
    public void InputModel_PostgresArrayParameter_StaysAnArrayInModels()
    {
        var run = RunIds(OutputKind.Models, "", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("public sealed record FindIdsParams(int[] Ids);");
    }

    [Fact]
    public void InputModel_NullablePostgresArrayParameter_IsNullableIEnumerable()
    {
        var run = RunIds(OutputKind.CodeGen, "-- param: @ids null\n", nullable: true);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int>? Ids);");
    }

    [Fact]
    public void InputModel_ArrayWithNullableElementOverride_IsIEnumerableOfNullable()
    {
        var run = RunIds(OutputKind.CodeGen, "-- type-override: int4[]=int?[]\n", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int?> Ids);");
    }

    // A C# type the marker names after -> is taken as written.
    [Fact]
    public void InputModel_ArrayParameterWithMarkerClrType_IsTakenAsWritten()
    {
        var run = RunIds(OutputKind.CodeGen, "-- param: @ids -> int[]\n", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(int[] Ids);");
    }

    // bytea is no array of the database's, and a multirange is an array of the C# alone.
    [Fact]
    public void InputModel_ByteaAndMultirangeParameters_AreNotSequences()
    {
        var multirange = TestSidecars.Postgres(
            name: "int4multirange",
            kind: SidecarValues.Kind.Multirange,
            internalName: "int4multirange",
            subtype: TestSidecars.Postgres(
                name: "int4range",
                kind: SidecarValues.Kind.Range,
                internalName: "int4range",
                subtype: Int4
            )
        );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: FindIds\nSELECT 1 WHERE @data IS NOT NULL AND @ranges IS NOT NULL;\n"
        );

        var run = MethodRuns.Run(
            file,
            [
                Entry(
                    file,
                    "FindIds",
                    [Parameter("data", Catalog("bytea"), false), Parameter("ranges", multirange, false)],
                    null
                ),
            ]
        );

        run.ShouldBeClean();
        var model = run.Sources[ParamsModel];
        model.ShouldContain("(byte[] Data, global::NpgsqlTypes.NpgsqlRange<int>[] Ranges);");
        model.ShouldNotContain("IEnumerable");
    }

    // A domain over an array is sent as its base, the array.
    [Fact]
    public void InputModel_DomainOverArrayParameter_IsIEnumerable()
    {
        var domain = TestSidecars.Postgres(
            name: "id_list",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "id_list",
            baseType: IntArray
        );
        var file = IdsFile("");

        var run = MethodRuns.Run(file, [Entry(file, "FindIds", [Parameter("ids", domain, false)], null)]);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int> Ids);");
    }

    // A column is read as the array Npgsql gives.
    [Fact]
    public void OutputModel_ArrayColumn_StaysAnArray()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetIds\nSELECT ids FROM teams;\n");

        var run = MethodRuns.Run(file, [Entry(file, "GetIds", [], [Column("ids", IntArray)])]);

        run.ShouldBeClean();
        var model = run.Sources["App.GetIdsDto-Model.g.cs"];
        model.ShouldContain("public sealed record GetIdsDto(int[] Ids)");
        model.ShouldNotContain("IEnumerable");
    }

    // FindIds, whose @ids is an integer[], with the markers given.
    private static SqlFile IdsFile(string markers) =>
        new(GeneratorHarness.UsersPath, "-- name: FindIds\n" + markers + "SELECT 1 WHERE 1 = ANY (@ids);\n");

    // FindIds with the markers, its @ids of the nullability given, at the output.
    private static GeneratorRun RunIds(OutputKind output, string markers, bool nullable)
    {
        var file = IdsFile(markers);
        return GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, Entry(file, "FindIds", [Parameter("ids", IntArray, nullable)], null))],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );
    }
}
