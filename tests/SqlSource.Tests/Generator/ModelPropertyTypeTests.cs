// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The C# type a property of a model is declared with, where it is not the type the parameter's or column's type maps
// to: a PostgreSQL array parameter is an IEnumerable<T> of its element in a Methods query's input model, which its
// method sends whatever the sequence, and stays an array at Models, where a library hands the value to Npgsql as it is.
// Each run is under postgres on Npgsql 10, at Methods unless it says Models, with an entry written for each query's
// current hash.
public sealed class ModelPropertyTypeTests
{
    private const string ParamsModel = "App.FindIdsParams-Model.g.cs";

    private const string Enumerable = "global::System.Collections.Generic.IEnumerable<";

    private static readonly PostgresType IntArray = ArrayOf(Int4, "_int4");

    [Fact]
    public void InputModel_PostgresArrayParameter_IsIEnumerableInMethods()
    {
        var run = RunIds(OutputKind.Methods, "", nullable: false);

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
        var run = RunIds(OutputKind.Methods, "-- param: @ids null\n", nullable: true);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int>? Ids);");
    }

    [Fact]
    public void InputModel_ArrayWithNullableElementOverride_IsIEnumerableOfNullable()
    {
        var run = RunIds(OutputKind.Methods, "-- type-override: int4[]=int?[]\n", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int?> Ids);");
    }

    // A type override that gives the array a type that is no array is taken as written.
    [Fact]
    public void InputModel_ArrayWithListOverride_IsTakenAsWritten()
    {
        var run = RunIds(
            OutputKind.Methods,
            "-- type-override: int4[]=System.Collections.Generic.List<int>\n",
            nullable: false
        );

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(global::System.Collections.Generic.List<int> Ids);");
        run.Sources[ParamsModel].ShouldNotContain("IEnumerable");
    }

    // A C# type the marker names after -> is taken as written.
    [Fact]
    public void InputModel_ArrayParameterWithMarkerClrType_IsTakenAsWritten()
    {
        var run = RunIds(OutputKind.Methods, "-- param: @ids -> int[]\n", nullable: false);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(int[] Ids);");
    }

    // The marker is found by the parameter's name ignoring case, as the typing finds it: in the SQL's spelling, which
    // the tool writes, and in another, which only a person who edited the sidecar writes.
    [Theory]
    [InlineData("Ids", false)]
    [InlineData("ids", true)]
    public void InputModel_MarkerClrTypeOfANameInAnotherCase_IsTakenAsWritten(string entryName, bool handEdited)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: FindIds\n-- param: @ids -> int[]\nSELECT 1 WHERE 1 = ANY (@Ids);\n"
        );
        var entry = Entry(file, "FindIds", [Parameter(entryName, IntArray, false)], null);

        var run = GeneratorHarness.Run(
            OutputKind.Methods,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, handEdited ? Sidecars.HandEdited(file, entry) : Sidecars.Of(file, entry)],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(int[] Ids);");
    }

    // An array of a library's type is an IEnumerable of it at Methods, and at Models, which has the base map's type
    // without the library, the base map's array, so both of the other outputs' changes apply to the one property.
    [Theory]
    [InlineData(nameof(OutputKind.Methods), Enumerable + "global::NodaTime.Instant> Ids")]
    [InlineData(nameof(OutputKind.Models), "global::System.DateTime[] Ids")]
    public void InputModel_ArrayParameterOfALibrarysType_IsItsOutputsType(string output, string expected)
    {
        var file = IdsFile("");
        var instants = ArrayOf(Catalog("timestamptz") with { Name = "timestamp with time zone" }, "_timestamptz");

        var run = GeneratorHarness.Run(
            Enum.Parse<OutputKind>(output),
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, Entry(file, "FindIds", [Parameter("ids", instants, false)], null))],
            references:
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3"),
            ],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        var model = run.Sources[ParamsModel];
        model.ShouldContain("(" + expected + ");");
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

    // A list parameter is an IEnumerable of its element at every output, whose type is the one an ordinary parameter of
    // the described type has.
    [Theory]
    [InlineData(nameof(OutputKind.Methods))]
    [InlineData(nameof(OutputKind.Models))]
    public void InputModel_ListParameter_IsIEnumerableOfTheElement(string output)
    {
        var run = RunList(Enum.Parse<OutputKind>(output), "id IN @ids", "", nullable: null);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("public sealed record FindIdsParams(" + Enumerable + "int> Ids);");
    }

    [Fact]
    public void InputModel_ListWithNullTest_IsNullable()
    {
        var run = RunList(OutputKind.Methods, "(@ids IS NULL OR id IN @ids)", "", nullable: null);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int>? Ids);");
    }

    // The tool writes the marker's null into the sidecar, as nullable.
    [Theory]
    [InlineData(nameof(OutputKind.Methods))]
    [InlineData(nameof(OutputKind.Models))]
    public void InputModel_ListWithNullMarker_IsNullable(string output)
    {
        var run = RunList(Enum.Parse<OutputKind>(output), "id IN @ids", "-- param: @ids null\n", nullable: true);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "int>? Ids);");
    }

    [Fact]
    public void InputModel_ListWithMarkerClrType_IsIEnumerableOfThatType()
    {
        var run = RunList(
            OutputKind.Methods,
            "id IN @ids",
            "-- param: @ids -> AccountId\n",
            nullable: null,
            source: Source + "\nnamespace App { public readonly record struct AccountId(int Value); }\n"
        );

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "global::App.AccountId> Ids);");
    }

    [Fact]
    public void InputModel_ListWithTypeOverride_IsIEnumerableOfTheOverride()
    {
        var run = RunList(OutputKind.Methods, "id IN @ids", "-- type-override: int4=long\n", nullable: null);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + Enumerable + "long> Ids);");
    }

    // A list of a library's type is an IEnumerable of the library's type at Methods and of the base map's without it.
    [Theory]
    [InlineData(nameof(OutputKind.Methods), Enumerable + "global::NodaTime.Instant> Ids")]
    [InlineData(nameof(OutputKind.Models), Enumerable + "global::System.DateTime> Ids")]
    public void InputModel_ListOfNodaTimeInMethods_TakesTheLibrarysElement(string output, string expected)
    {
        var file = ListFile("at IN @ids", "");
        var instant = Catalog("timestamptz") with { Name = "timestamp with time zone" };

        var run = GeneratorHarness.Run(
            Enum.Parse<OutputKind>(output),
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, Entry(file, "FindIds", [Parameter("ids", instant)], null))],
            references:
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3"),
            ],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("(" + expected + ");");
    }

    // FindIds, whose @ids is a list, with the predicate and the markers given.
    private static SqlFile ListFile(string predicate, string markers) =>
        new(GeneratorHarness.UsersPath, "-- name: FindIds\n" + markers + "SELECT 1 FROM t WHERE " + predicate + ";\n");

    private static GeneratorRun RunList(
        OutputKind output,
        string predicate,
        string markers,
        bool? nullable,
        string source = Source
    )
    {
        var file = ListFile(predicate, markers);
        return GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [file, Sidecars.Of(file, Entry(file, "FindIds", [Parameter("ids", Int4, nullable)], null))],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );
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
