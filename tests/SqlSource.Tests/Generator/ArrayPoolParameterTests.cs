// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The generator parameter no-array-pool: read at every level a list is, the nearest list replacing the farther ones
// whole, and carried to the query's method as QueryMethod.UseArrayPool.  No generated code reads it yet, so the
// methods are written the same with it and without it.
public sealed class ArrayPoolParameterTests
{
    private const string On = "no-array-pool";

    private const string Other = "sort-input";

    private const string Default = "default";

    [Theory]
    // property, metadata, attribute, preamble, query
    [InlineData(null, null, null, null, null, true)]
    [InlineData(On, null, null, null, null, false)]
    [InlineData(On, Default, null, null, null, true)]
    [InlineData(On, Other, null, null, null, true)]
    [InlineData(null, On, null, null, null, false)]
    [InlineData(Other, On, Default, null, null, true)]
    [InlineData(null, null, On, null, null, false)]
    [InlineData(Default, Other, On, null, null, false)]
    [InlineData(On, On, On, Default, null, true)]
    [InlineData(null, null, null, On, null, false)]
    [InlineData(null, null, null, On, Other, true)]
    [InlineData(On, On, On, On, Default, true)]
    [InlineData(null, null, null, Default, On, false)]
    public void Run_NoArrayPool_ComesWholeFromTheMostSpecificLevelThatGivesAList(
        string? property,
        string? metadata,
        string? attribute,
        string? preamble,
        string? query,
        bool expected
    )
    {
        var sql =
            (preamble is null ? string.Empty : "-- generator: " + preamble + "\n")
            + "-- name: ListUsers\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT id, name FROM users;\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            sql,
            Metadata: metadata is null
                ? null
                : new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = metadata }
        );

        var run = Run(file, attribute, property);

        run.ShouldBeClean();
        Method(run).UseArrayPool.ShouldBe(expected);
    }

    [Theory]
    [InlineData("IEnumerable")]
    [InlineData("ICollection")]
    [InlineData("IReadOnlyCollection")]
    [InlineData("IList")]
    [InlineData("IReadOnlyList")]
    [InlineData("Array")]
    [InlineData("List")]
    [InlineData("ImmutableArray")]
    [InlineData("ImmutableList")]
    [InlineData("IImmutableList")]
    public void Run_NoArrayPool_LeavesTheGeneratedTextAsItIs(string collection)
    {
        var collectionType = "CollectionType = GeneratorCollectionType." + collection;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListUsers\nSELECT id, name FROM users;\n");

        var pooled = Run(file, collectionType, null, attributeArguments: true);
        var unpooled = Run(file, collectionType + ", Parameters = \"no-array-pool\"", null, attributeArguments: true);

        pooled.ShouldBeClean();
        unpooled.ShouldBeClean();
        Method(pooled).UseArrayPool.ShouldBeTrue();
        Method(unpooled).UseArrayPool.ShouldBeFalse();
        unpooled.Sources[MethodRuns.MethodsFile].ShouldBe(pooled.Sources[MethodRuns.MethodsFile]);
    }

    [Fact]
    public void Run_PropertyWithAMisspeltWordBesideNoArrayPool_IsAnErrorAndNoArrayPoolApplies()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListUsers\nSELECT id, name FROM users;\n");

        var run = Run(file, null, "no-arraypool no-array-pool");

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'no-arraypool' is not a valid value of SqlSourceGeneratorParameters",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        Method(run).UseArrayPool.ShouldBeFalse();
    }

    [Fact]
    public void Run_AttributeWithAMisspeltWordBesideNoArrayPool_IsAnErrorAtTheAttribute()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListUsers\nSELECT id, name FROM users;\n");

        var run = Run(file, "no-array-pool nope", null);

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(");
        diagnostic.ShouldEndWith("'nope' is not a valid value of Parameters");
    }

    // The one method the run planned for App.Sample.
    private static QueryMethod Method(GeneratorRun run) =>
        StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods.ShouldHaveSingleItem();

    // The file and its sidecar at CodeGen under postgres on Npgsql 10, on App.Sample with the attribute's Parameters,
    // or with the attribute's arguments as they are, and the property.
    private static GeneratorRun Run(SqlFile file, string? attribute, string? property, bool attributeArguments = false)
    {
        SidecarEntry[] entries = [Entry(file, "ListUsers", [], [Column("id"), Column("name", Text, nullable: true)])];
        var arguments = attributeArguments ? attribute : "Parameters = \"" + attribute + "\"";
        var source = attribute is null ? MethodRuns.Source : MethodRuns.SampleSource(arguments!);

        return GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [file, Sidecars.Of(file, entries)],
            references: [NpgsqlReferences.Npgsql10],
            generatorParameters: property,
            dialect: "postgres"
        );
    }
}
